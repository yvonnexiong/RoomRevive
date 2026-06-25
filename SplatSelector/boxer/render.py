"""Minimal CPU Gaussian-splat renderer (numpy).

Renders each splat as an isotropic screen-space Gaussian sized from its largest
3D scale, alpha-composited back-to-front. Good enough for object recognition and
for recording the exact camera matrices used to reproject 3D splat centers.
Conventions match pink_to_marble_editor.html: up = +Y, camera looks down -Z.
"""
import numpy as np


def density_keep(positions, opaque_mask, cell=0.10, min_count=6):
    """Boolean keep-mask removing isolated 'floater' splats: keep opaque points
    that fall in a voxel containing >= min_count opaque points."""
    idx = np.where(opaque_mask)[0]
    keys = np.floor(positions[idx] / cell).astype(np.int64)
    _, inv, counts = np.unique(keys, axis=0, return_inverse=True, return_counts=True)
    good = counts[inv] >= min_count
    keep = np.zeros(len(positions), bool)
    keep[idx[good]] = True
    return keep


def box_keep(positions, center, half, yaw, expand=1.5):
    """Keep-mask for splats within `expand`x an oriented box (gravity-aligned)."""
    c = np.asarray(center)
    u = np.array([np.sin(yaw), 0, np.cos(yaw)]); w = np.array([np.cos(yaw), 0, -np.sin(yaw)])
    d = positions - c
    du = d @ u; dw = d @ w; dy = d[:, 1]
    return (np.abs(du) < half[0]*expand) & (np.abs(dw) < half[2]*expand) & (np.abs(dy) < half[1]*expand)


def subset_scene(s, keep):
    return {**s, "n": int(keep.sum()),
            "positions": s["positions"][keep], "colors": s["colors"][keep],
            "alphas": s["alphas"][keep], "scales_log": s["scales_log"][keep],
            "quats": s["quats"][keep]}


def look_at(eye, target, up=(0, 1, 0)):
    eye = np.asarray(eye, float); target = np.asarray(target, float); up = np.asarray(up, float)
    z = eye - target; z /= (np.linalg.norm(z) or 1)
    x = np.cross(up, z); x /= (np.linalg.norm(x) or 1)
    y = np.cross(z, x)
    R = np.stack([x, y, z])               # world->cam rotation rows
    t = -R @ eye
    M = np.eye(4)
    M[:3, :3] = R; M[:3, 3] = t
    return M  # 4x4 world->camera


def project(positions, view, W, H, fov_y):
    """Return pixel coords (px,py), depth (>0 in front), and focal length."""
    n = positions.shape[0]
    ph = np.concatenate([positions, np.ones((n, 1))], axis=1)
    cam = ph @ view.T                      # (n,4) camera space
    z = cam[:, 2]
    depth = -z                             # +z forward distance
    f = (H / 2) / np.tan(fov_y / 2)        # focal in pixels
    px = W / 2 + f * cam[:, 0] / (-z)
    py = H / 2 - f * cam[:, 1] / (-z)
    return px, py, depth, f


def render(scene, eye, target, W=720, H=720, fov_y_deg=60.0,
           up=(0, 1, 0), bg=(0.06, 0.07, 0.09), max_r=5, subsample=None,
           min_alpha=0.45, near_cull=0.45, exposure=1.6, seed=0, scale_mult=1.0):
    view = look_at(eye, target, up)
    fov_y = np.deg2rad(fov_y_deg)
    pos = scene["positions"]; col = scene["colors"]; op = scene["alphas"] / 255.0
    scales = np.exp(scene["scales_log"]).max(axis=1)   # largest world-space sigma

    idx = np.arange(pos.shape[0])
    keep = op > min_alpha
    idx = idx[keep]
    if subsample is not None and idx.size > subsample:
        rng = np.random.default_rng(seed)
        idx = rng.choice(idx, subsample, replace=False)

    px, py, depth, f = project(pos[idx], view, W, H, fov_y)
    rad = f * scales[idx] / np.maximum(depth, 1e-3) * scale_mult   # projected sigma in px
    rad = np.clip(rad, 0.6, max_r)

    front = depth > near_cull
    onx = (px > -max_r) & (px < W + max_r) & (py > -max_r) & (py < H + max_r)
    m = front & onx
    idx, px, py, depth, rad = idx[m], px[m], py[m], depth[m], rad[m]

    order = np.argsort(-depth)             # far -> near (back to front)
    idx, px, py, rad = idx[order], px[order], py[order], rad[order]
    cols = np.clip(col[idx] * exposure, 0, 1); alps = op[idx]

    img = np.zeros((H, W, 3), np.float32) + np.asarray(bg, np.float32)

    # cache gaussian kernels per integer radius
    kern = {}
    def kernel(r):
        ri = int(round(r))
        if ri not in kern:
            k = max(1, ri)
            ax = np.arange(-2 * k, 2 * k + 1)
            xx, yy = np.meshgrid(ax, ax)
            g = np.exp(-(xx * xx + yy * yy) / (2 * (k * k)))
            g[g < 0.04] = 0.0
            kern[ri] = g.astype(np.float32)
        return kern[ri], int(round(r)) if round(r) >= 1 else 1

    for i in range(idx.size):
        g, k = kernel(rad[i])
        half = g.shape[0] // 2
        cx, cy = int(round(px[i])), int(round(py[i]))
        x0, x1 = cx - half, cx + half + 1
        y0, y1 = cy - half, cy + half + 1
        gx0 = max(0, -x0); gy0 = max(0, -y0)
        ix0 = max(0, x0); iy0 = max(0, y0)
        ix1 = min(W, x1); iy1 = min(H, y1)
        if ix1 <= ix0 or iy1 <= iy0:
            continue
        gsub = g[gy0:gy0 + (iy1 - iy0), gx0:gx0 + (ix1 - ix0)]
        a = (gsub * alps[i])[..., None]
        patch = img[iy0:iy1, ix0:ix1]
        img[iy0:iy1, ix0:ix1] = patch * (1 - a) + cols[i] * a

    return img, view, f


def render_ewa(scene, eye, target, W=820, H=820, fov_y_deg=70.0, up=(0, 1, 0),
               bg=(0.06, 0.07, 0.09), min_alpha=0.2, near_cull=0.2, exposure=1.4,
               scale_mult=1.0, subsample=None, max_px=48, seed=0):
    """Anisotropic EWA Gaussian splatting (matches the WebGL viewer): each splat is an
    elliptical Gaussian from its projected 3D covariance, composited back-to-front.
    Renders ALL splats by default -> viewer-quality clarity (slower than render())."""
    view = look_at(eye, target, up); fov_y = np.deg2rad(fov_y_deg)
    Wv = view[:3, :3]; tv = view[:3, 3]
    pos = scene["positions"]; col = scene["colors"]; op = scene["alphas"] / 255.0
    idx = np.where(op > min_alpha)[0]
    if subsample and idx.size > subsample:
        idx = np.random.default_rng(seed).choice(idx, subsample, replace=False)
    P = pos[idx]
    cam = P @ Wv.T + tv                       # camera-space coords
    z = cam[:, 2]; depth = -z
    f = (H / 2) / np.tan(fov_y / 2)
    keep = depth > near_cull
    idx, P, cam, depth = idx[keep], P[keep], cam[keep], depth[keep]
    px = W / 2 + f * cam[:, 0] / depth
    py = H / 2 + f * cam[:, 1] / depth        # note: +f*y/depth (cam y, -z forward)
    # 3D covariance from quaternion + scale
    q = scene["quats"][idx]; sc = np.exp(scene["scales_log"][idx]) * scale_mult
    w_, x_, y_, z_ = q[:, 0], q[:, 1], q[:, 2], q[:, 3]
    R = np.empty((len(idx), 3, 3))
    R[:, 0, 0] = 1-2*(y_*y_+z_*z_); R[:, 0, 1] = 2*(x_*y_-w_*z_); R[:, 0, 2] = 2*(x_*z_+w_*y_)
    R[:, 1, 0] = 2*(x_*y_+w_*z_); R[:, 1, 1] = 1-2*(x_*x_+z_*z_); R[:, 1, 2] = 2*(y_*z_-w_*x_)
    R[:, 2, 0] = 2*(x_*z_-w_*y_); R[:, 2, 1] = 2*(y_*z_+w_*x_); R[:, 2, 2] = 1-2*(x_*x_+y_*y_)
    M = R * sc[:, None, :]
    S = M @ M.transpose(0, 2, 1)              # world cov (N,3,3)
    tz = cam[:, 2]
    J = np.zeros((len(idx), 3, 3))
    J[:, 0, 0] = -f / tz; J[:, 1, 1] = -f / tz
    J[:, 0, 2] = f * cam[:, 0] / (tz*tz); J[:, 1, 2] = f * cam[:, 1] / (tz*tz)
    T = np.einsum('nij,jk->nik', J, Wv)
    C = T @ S @ T.transpose(0, 2, 1)
    cxx = C[:, 0, 0] + 0.3; cyy = C[:, 1, 1] + 0.3; cxy = C[:, 1, 0]
    det = cxx*cyy - cxy*cxy
    ok = det > 1e-6
    order = np.argsort(-depth)
    img = np.zeros((H, W, 3), np.float32) + np.asarray(bg, np.float32)
    cols = np.clip(col[idx] * exposure, 0, 1); alps = op[idx]
    for k in order:
        if not ok[k]:
            continue
        a, b, c = cxx[k], cxy[k], cyy[k]
        mid = 0.5*(a+c); rr = np.sqrt(max(0.0, (0.5*(a-c))**2 + b*b)); l1 = mid+rr
        R_ = int(min(max_px, np.ceil(3.0*np.sqrt(max(l1, 0.25)))))
        if R_ < 1:
            R_ = 1
        cx0, cy0 = int(round(px[k])), int(round(py[k]))
        x0 = max(0, cx0-R_); x1 = min(W, cx0+R_+1); y0 = max(0, cy0-R_); y1 = min(H, cy0+R_+1)
        if x1 <= x0 or y1 <= y0:
            continue
        d = det[k]; i00 = c/d; i11 = a/d; i01 = -b/d
        xs = np.arange(x0, x1)-px[k]; ys = np.arange(y0, y1)-py[k]
        gx, gy = np.meshgrid(xs, ys)
        powr = -0.5*(i00*gx*gx + 2*i01*gx*gy + i11*gy*gy)
        g = np.exp(np.clip(powr, -8, 0)) * alps[k]
        ga = g[..., None]
        patch = img[y0:y1, x0:x1]
        img[y0:y1, x0:x1] = patch*(1-ga) + cols[k]*ga
    return img, view, f


def save_png(img, path, gamma=0.9):
    from PIL import Image
    out = np.clip(img, 0, 1) ** gamma
    Image.fromarray((out * 255).astype(np.uint8)).save(path)
