import numpy as np
from PIL import Image
import sys, os

PLY = r"C:\Unity-Git\RoomRevive\HTML_Editor\Splats\YinanOriginalHighQuality.ply"
OUT_DIR = r"C:\Unity-Git\RoomRevive\HTML_Editor\Splats"

# ---- load ----
with open(PLY, 'rb') as fh:
    hdr = b''
    while b'end_header' not in hdr:
        hdr += fh.readline()
    # parse counts/props from header
    text = hdr.decode('ascii', 'replace')
    n = 0
    props = []
    for line in text.splitlines():
        if line.startswith('element vertex'):
            n = int(line.split()[-1])
        elif line.startswith('property float'):
            props.append(line.split()[-1])
    cols = len(props)
    data = np.fromfile(fh, dtype='<f4', count=n*cols).reshape(n, cols)

idx = {name: i for i, name in enumerate(props)}
xyz = data[:, [idx['x'], idx['y'], idx['z']]].astype(np.float64)
f_dc = data[:, [idx['f_dc_0'], idx['f_dc_1'], idx['f_dc_2']]]
opacity = data[:, idx['opacity']]
scale = data[:, [idx['scale_0'], idx['scale_1'], idx['scale_2']]]

# SH DC -> base color
C0 = 0.28209479177387814
rgb = np.clip(0.5 + C0 * f_dc, 0, 1)
alpha = 1.0 / (1.0 + np.exp(-opacity))          # sigmoid
gsize = np.exp(scale).mean(1)                    # world-space radius approx

print("loaded", len(xyz), "gaussians")
print("bounds min", xyz.min(0), "max", xyz.max(0))

# drop near-transparent
keep = alpha > 0.05
xyz, rgb, alpha, gsize = xyz[keep], rgb[keep], alpha[keep], gsize[keep]
print("kept", len(xyz), "after opacity cull")

center = np.median(xyz, 0)
extent = np.percentile(xyz, 97, 0) - np.percentile(xyz, 3, 0)
radius = np.linalg.norm(extent) * 0.6
print("center", center, "radius", radius)

W, H = 1280, 960
fov = np.radians(60)
focal = (W * 0.5) / np.tan(fov * 0.5)


def look_at(eye, target, up=np.array([0, 1.0, 0])):
    f = target - eye; f /= np.linalg.norm(f)
    s = np.cross(f, up); s /= np.linalg.norm(s)
    u = np.cross(s, f)
    R = np.stack([s, u, -f], 0)   # world->cam rotation
    return R, eye


def render(eye, target, fname, up=np.array([0, 1.0, 0])):
    R, eye = look_at(np.asarray(eye, float), np.asarray(target, float), up)
    cam = (xyz - eye) @ R.T
    z = -cam[:, 2]                       # depth in front of camera
    infront = z > 0.05
    cam = cam[infront]; col = rgb[infront]; a = alpha[infront]
    gs = gsize[infront]; z = z[infront]
    px = focal * cam[:, 0] / z + W * 0.5
    py = -focal * cam[:, 1] / z + H * 0.5
    # projected radius in pixels
    pr = np.clip(focal * gs / z, 0.6, 6.0)
    onscreen = (px > -10) & (px < W+10) & (py > -10) & (py < H+10)
    px, py, col, a, z, pr = px[onscreen], py[onscreen], col[onscreen], a[onscreen], z[onscreen], pr[onscreen]

    order = np.argsort(-z)              # far -> near (painter's)
    px, py, col, a, pr = px[order], py[order], col[order], a[order], pr[order]

    img = np.zeros((H, W, 3), np.float32)
    acc = np.zeros((H, W), np.float32)  # accumulated alpha (front-to-back not needed for over)

    xi = px.astype(np.int32); yi = py.astype(np.int32)
    ri = np.round(pr).astype(np.int32)
    # splat as small squares, "over" compositing far->near
    for dy in range(-3, 4):
        for dx in range(-3, 4):
            m = (np.abs(dx) <= ri) & (np.abs(dy) <= ri)
            if not m.any():
                continue
            xx = xi[m] + dx; yy = yi[m] + dy
            inb = (xx >= 0) & (xx < W) & (yy >= 0) & (yy < H)
            xx, yy = xx[inb], yy[inb]
            cc = col[m][inb]; aa = a[m][inb]
            # gaussian falloff within footprint
            rr = ri[m][inb]
            fall = np.exp(-(dx*dx + dy*dy) / (2 * np.maximum(rr*0.5, 0.5)**2))
            aw = aa * fall
            img[yy, xx] = img[yy, xx] * (1 - aw)[:, None] + cc * aw[:, None]
            acc[yy, xx] = acc[yy, xx] * (1 - aw) + aw

    out = np.clip(img * 255, 0, 255).astype(np.uint8)
    Image.fromarray(out).save(os.path.join(OUT_DIR, fname))
    print("wrote", fname, "splats drawn:", len(px))


r = radius
c = center
# Try several orbit viewpoints around the room center
render(c + np.array([ r,  r*0.3,  r]), c, "preview_1.png")
render(c + np.array([-r,  r*0.3,  r]), c, "preview_2.png")
render(c + np.array([ r,  r*0.3, -r]), c, "preview_3.png")
render(c + np.array([ 0,  r*0.8,  0.01]), c, "preview_top.png", up=np.array([0,0,1.0]))
