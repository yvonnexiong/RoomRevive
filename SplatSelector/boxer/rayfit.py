"""Per-face shrink-wrap by ray casting from the corner angles.

From each of the 8 box-corner camera angles, z-buffer the opaque splats to get the
NEAREST (visible) splat per pixel -> the object's visible surface from that angle
(the wall behind is occluded, so it's excluded). Then for each box face that those
angles actually observe, pull the face IN to the visible surface if there is an air
gap; if the face already meets splats, leave it. Faces with little support from
these angles (e.g. the back against the wall) are left as-is.

General / object-agnostic: only uses geometry + the chosen viewing angles.
"""
import json, os
import numpy as np
from spz_io import parse_spz
from render import look_at

box = json.load(open("box.json"))
c = np.array(box["center"]); hw, hh, hd = [float(x) for x in box["half"]]; yaw = float(box["yaw"])
fs = box.get("front_sign", 1.0)
RES = int(os.environ.get("RES", 320))
FOV = 55.0
MINSUP = int(os.environ.get("MINSUP", 120))   # min visible pts near a face to adjust it
SLAB = float(os.environ.get("SLAB", 0.6))      # face neighbourhood = outer (1-SLAB) of half-extent

def floater_mask(P, alpha, cell=0.06, maxd=15):
    """True for isolated splats (few neighbours in a 3x3x3 voxel block) — same
    definition as the viewer. These are excluded before ray casting."""
    solid = alpha > 0.2
    vox = np.floor(P / cell).astype(np.int64)
    OFF, A, B = 1 << 19, 1 << 40, 1 << 20
    key = (vox[:, 0] + OFF) * A + (vox[:, 1] + OFF) * B + (vox[:, 2] + OFF)
    uk, uc = np.unique(key[solid], return_counts=True)
    dens = np.zeros(len(P), np.int64)
    for dx in (-1, 0, 1):
        for dy in (-1, 0, 1):
            for dz in (-1, 0, 1):
                nk = (vox[:, 0] + dx + OFF) * A + (vox[:, 1] + dy + OFF) * B + (vox[:, 2] + dz + OFF)
                idx = np.clip(np.searchsorted(uk, nk), 0, len(uk) - 1)
                hit = uk[idx] == nk
                dens += np.where(hit, uc[idx], 0)
    return dens < maxd


data = json.load(open("cameras.json")); s = parse_spz(data["spz"])
P = s["positions"]; opq = s["alphas"] / 255.0 > 0.5
if os.environ.get("HIDE_FLOATERS", "1") == "1":   # ray-cast AFTER floaters are hidden
    fl = floater_mask(P, s["alphas"] / 255.0,
                      float(os.environ.get("FL_CELL", 0.06)), int(os.environ.get("FL_MAXD", 15)))
    opq = opq & ~fl
    print(f"floaters excluded from ray casting: {int(fl.sum()):,}")
# Restrict to detector-selected object splats so flush cabinets/walls don't count
# as "the face hits a splat". Falls back to all opaque if no carve mask present.
if os.environ.get("USE_OBJMASK", "1") == "1" and os.path.exists("obj_idx.npy"):
    sel = np.zeros(len(P), bool); sel[np.load("obj_idx.npy")] = True
    sel &= opq; print("using carved object mask:", int(sel.sum()), "splats")
else:
    sel = opq; print("using all opaque splats")
idx = np.where(sel)[0]; pts = P[idx]; M = len(pts)

u = np.array([np.sin(yaw), 0, np.cos(yaw)]); w = np.array([np.cos(yaw), 0, -np.sin(yaw)]); Y = np.array([0, 1.0, 0])
tan = np.tan(np.deg2rad(FOV) / 2)
dist = max(hw, hh, hd) / tan / 0.5 + max(hw, hh, hd)

# 8 corner-diagonal cameras, looking at the box center
visible = np.zeros(M, bool)
ncam = 0
for sx in (-1, 1):
    for sz in (-1, 1):
        for sy in (-1, 1):
            cdir = sx * u * hw + sz * w * hd + sy * Y * hh
            cdir = cdir / np.linalg.norm(cdir)
            eye = c + cdir * dist
            view = look_at(eye, c, (0, 1, 0))
            f = (RES / 2) / tan
            ph = np.concatenate([pts, np.ones((M, 1))], 1)
            cm = ph @ view.T; z = cm[:, 2]; depth = -z
            px = (RES / 2 + f * cm[:, 0] / np.where(z != 0, -z, 1e9))
            py = (RES / 2 - f * cm[:, 1] / np.where(z != 0, -z, 1e9))
            ok = (depth > 0.05) & (px >= 0) & (px < RES) & (py >= 0) & (py < RES)
            pix = (py[ok].astype(np.int64) * RES + px[ok].astype(np.int64))
            gi = np.where(ok)[0]; dep = depth[ok]
            order = np.argsort(-dep)               # far first; nearest written last wins
            buf = np.full(RES * RES, -1, np.int64)
            buf[pix[order]] = gi[order]
            hit = buf[buf >= 0]
            visible[np.unique(hit)] = True
            ncam += 1
vis = pts[visible]
print(f"{ncam} corner angles, {visible.sum():,} visible surface splats")

# per-axis max a face may EXTEND beyond the box (m); 0 = shrink-only (default)
GROW_U = float(os.environ.get("GROW_U", os.environ.get("GROW", 0)))   # width
GROW_D = float(os.environ.get("GROW_D", os.environ.get("GROW", 0)))   # depth
GROW_Y = float(os.environ.get("GROW_Y", os.environ.get("GROW", 0)))   # height
GMAX = max(GROW_U, GROW_D, GROW_Y)
d = vis - c; du = d @ u; dw = d @ w; dy = d[:, 1]
near = (np.abs(du) < hw * 1.15 + GMAX) & (np.abs(dw) < hd * 1.15 + GMAX) & (np.abs(dy) < hh * 1.15 + GMAX)
du, dw, dy = du[near], dw[near], dy[near]

def face(coord, half, name, grow=0.0):
    """Fit each plane to the visible surface where supported: shrink to an air gap,
    or grow up to +grow if the object extends past the box."""
    lo, hi = -half, half
    sup_hi = np.sum(coord > SLAB * half); sup_lo = np.sum(coord < -SLAB * half)
    if sup_hi >= MINSUP:
        hi = min(half + grow, np.percentile(coord, 98))
    if sup_lo >= MINSUP:
        lo = max(-(half + grow), np.percentile(coord, 2))
    print(f"  {name}: support -/+ = {sup_lo}/{sup_hi}  plane {lo:+.2f}..{hi:+.2f} (half was {half:.2f}, grow {grow})")
    return lo, hi

print("per-face fit (only faces these angles observe):")
uL, uH = face(du, hw, "width(L/R)", GROW_U)
wL, wH = face(dw, hd, "depth(B/F)", GROW_D)
yL, yH = face(dy, hh, "height(Bo/T)", GROW_Y)
center = c + u * ((uL + uH) / 2) + w * ((wL + wH) / 2) + np.array([0, (yL + yH) / 2, 0])
hw2, hd2, hh2 = (uH - uL) / 2, (wH - wL) / 2, (yH - yL) / 2

def build_corners(c, hw, hh, hd, yaw):
    uu = np.array([np.sin(yaw),0,np.cos(yaw)])*hw; vv = np.array([np.cos(yaw),0,-np.sin(yaw)])*hd; a=np.array([0,hh,0])
    b=[c-uu-vv-a,c+uu-vv-a,c+uu+vv-a,c-uu+vv-a]; return np.array(b+[q+2*a for q in b])
corners = build_corners(center, hw2, hh2, hd2, yaw)
box.update(method="rayfit", corners=corners.tolist(), center=[float(x) for x in center],
           half=[float(hw2), float(hh2), float(hd2)], size=[float(2*hw2), float(2*hh2), float(2*hd2)])
json.dump(box, open("box.json", "w"), indent=1)
print(f"RAYFIT box: WxHxD = {2*hw2:.2f} x {2*hh2:.2f} x {2*hd2:.2f} m  (was {[round(2*x,2) for x in (hw,hh,hd)]})")
