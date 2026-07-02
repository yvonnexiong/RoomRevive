"""
Render a 360 equirectangular panorama from inside a Gaussian splat.

Usage:
    python render_360.py                         # camera at splat center
    python render_360.py --pos 0.1 0.3 0.1       # camera at a specific point
    python render_360.py --box -1 0 -1 1 2 1     # camera at center of an AABB
                                                 # (minx miny minz maxx maxy maxz)
    python render_360.py --pos 0 1 0 --out my360.png --w 6144

The --box form matches the "BOX CUT-OUT" tool: pass the same min/max corners
and the camera is placed at the box center.
"""
import numpy as np
from PIL import Image
import argparse, os

PLY = r"C:\Unity-Git\RoomRevive\HTML_Editor\Splats\YinanOriginalHighQuality.ply"
OUT_DIR = r"C:\Unity-Git\RoomRevive\HTML_Editor\Splats"

ap = argparse.ArgumentParser()
ap.add_argument("--pos", nargs=3, type=float, default=None, help="camera x y z")
ap.add_argument("--box", nargs=6, type=float, default=None,
                help="minx miny minz maxx maxy maxz -> camera at center")
ap.add_argument("--ply", default=PLY)
ap.add_argument("--out", default="pano_360.png")
ap.add_argument("--w", type=int, default=4096, help="output width (height = w/2)")
ap.add_argument("--up", default="y", choices=["x", "y", "z"], help="world up axis")
ap.add_argument("--maxdist", type=float, default=0.0,
                help="ignore gaussians farther than this (0 = no limit)")
args = ap.parse_args()

# ---- load PLY ----
with open(args.ply, "rb") as fh:
    hdr = b""
    while b"end_header" not in hdr:
        hdr += fh.readline()
    text = hdr.decode("ascii", "replace")
    n = 0
    props = []
    for line in text.splitlines():
        if line.startswith("element vertex"):
            n = int(line.split()[-1])
        elif line.startswith("property float"):
            props.append(line.split()[-1])
    cols = len(props)
    data = np.fromfile(fh, dtype="<f4", count=n * cols).reshape(n, cols)

idx = {name: i for i, name in enumerate(props)}
xyz = data[:, [idx["x"], idx["y"], idx["z"]]].astype(np.float64)
f_dc = data[:, [idx["f_dc_0"], idx["f_dc_1"], idx["f_dc_2"]]]
opacity = data[:, idx["opacity"]]
scale = data[:, [idx["scale_0"], idx["scale_1"], idx["scale_2"]]]

C0 = 0.28209479177387814
rgb = np.clip(0.5 + C0 * f_dc, 0, 1)
alpha = 1.0 / (1.0 + np.exp(-opacity))
gsize = np.exp(scale).mean(1)

keep = alpha > 0.05
xyz, rgb, alpha, gsize = xyz[keep], rgb[keep], alpha[keep], gsize[keep]

# ---- camera position ----
if args.box is not None:
    b = np.array(args.box, float)
    eye = (b[:3] + b[3:]) * 0.5
elif args.pos is not None:
    eye = np.array(args.pos, float)
else:
    eye = np.median(xyz, 0)
print("camera at", eye, "| gaussians:", len(xyz))

# remap so chosen up axis -> +Y for the panorama
order = {"x": [1, 0, 2], "y": [0, 1, 2], "z": [0, 2, 1]}[args.up]
P = xyz[:, order]
E = eye[order]

vec = P - E
dist = np.linalg.norm(vec, axis=1)
ok = dist > 1e-4
if args.maxdist > 0:
    ok &= dist < args.maxdist
vec, dist, col, a, gs = vec[ok], dist[ok], rgb[ok], alpha[ok], gsize[ok]

# spherical: longitude around Y, latitude up/down
lon = np.arctan2(vec[:, 0], -vec[:, 2])          # [-pi, pi]
lat = np.arcsin(np.clip(vec[:, 1] / dist, -1, 1))  # [-pi/2, pi/2]

W = args.w
H = W // 2
px = (lon / (2 * np.pi) + 0.5) * W
py = (0.5 - lat / np.pi) * H

# angular radius -> pixels
ang = gs / dist
pr = np.clip(ang * (W / (2 * np.pi)), 0.6, 8.0)

# painter's: far -> near
o = np.argsort(-dist)
px, py, col, a, pr = px[o], py[o], col[o], a[o], pr[o]

img = np.zeros((H, W, 3), np.float32)
xi = px.astype(np.int32)
yi = py.astype(np.int32)
ri = np.round(pr).astype(np.int32)

R = 8
for dy in range(-R, R + 1):
    for dx in range(-R, R + 1):
        m = (np.abs(dx) <= ri) & (np.abs(dy) <= ri)
        if not m.any():
            continue
        xx = (xi[m] + dx) % W                 # wrap horizontally (seamless)
        yy = yi[m] + dy
        inb = (yy >= 0) & (yy < H)
        xx, yy = xx[inb], yy[inb]
        cc = col[m][inb]
        rr = ri[m][inb]
        fall = np.exp(-(dx * dx + dy * dy) / (2 * np.maximum(rr * 0.5, 0.5) ** 2))
        aw = a[m][inb] * fall
        img[yy, xx] = img[yy, xx] * (1 - aw)[:, None] + cc * aw[:, None]

out = np.clip(img * 255, 0, 255).astype(np.uint8)
path = os.path.join(OUT_DIR, args.out)
Image.fromarray(out).save(path)
print("wrote", path, f"({W}x{H} equirectangular)")
