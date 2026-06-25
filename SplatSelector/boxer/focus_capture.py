"""Focused capture for cleaner detection: hide splats outside 1.5x the coarse box,
then render a ring of views + 8 corner-diagonal views, all framed on the object.

Isolating the object removes wall/floor/floater clutter -> OWLv2 detects it cleanly
and tightly, which makes the multi-view carve much sharper.
"""
import json, os
import numpy as np
from spz_io import parse_spz
from render import render, save_png, box_keep, subset_scene

REGION = os.environ.get("REGIONBOX", "box_boxernet.json")   # coarse box defining the region
EXPAND = float(os.environ.get("EXPAND", 1.5))
rb = json.load(open(REGION))
C = np.array(rb["center"]); half = rb["half"]; yaw = rb["yaw"]
fs = rb.get("front_sign", 1.0)

data = json.load(open("cameras.json")); SPZ = data["spz"]
s = parse_spz(SPZ)
# NOTE: masking to black kills OWLv2 (object on a void is out-of-distribution).
# Keep full scene context by default; MASK=1 only for clean human inspection renders.
if os.environ.get("MASK", "0") == "1":
    keep = box_keep(s["positions"], C, half, yaw, EXPAND)
    print(f"masked to {EXPAND}x box: {keep.sum():,}/{s['n']:,} splats kept")
    s = subset_scene(s, keep)
else:
    print("full-scene focus (context kept for detector)")

u = np.array([np.sin(yaw), 0, np.cos(yaw)]); w = np.array([np.cos(yaw), 0, -np.sin(yaw)]); Y = np.array([0, 1.0, 0])
hw, hh, hd = half
W = Hh = 700
FOV = 60.0
tan = np.tan(np.deg2rad(FOV) / 2)
RP = dict(min_alpha=0.6, max_r=4, exposure=1.2, near_cull=0.3, bg=(0.06, 0.07, 0.09))

def dist(ih, iv):
    return max(ih, iv) / tan / 0.55 + max(hw, hh, hd)

cams = []; i = 0
OUT = "frames_focus"; os.makedirs(OUT, exist_ok=True)

def add(eye, up, tag):
    global i
    img, view, f = render(s, eye, C, W=W, H=Hh, fov_y_deg=FOV, subsample=400000, up=tuple(up), **RP)
    name = f"foc_{i:03d}"
    save_png(img, os.path.join(OUT, name + ".png"))
    cams.append(dict(name=name, eye=[float(x) for x in eye], target=[float(x) for x in C],
                     W=W, H=Hh, fov_y_deg=FOV, focal=float(f), view=view.flatten().tolist(), tag=tag))
    i += 1

# ring: 16 yaws x 2 heights, looking at center
for hy in (C[1] - hh * 0.3, C[1] + hh * 0.3):
    for a in np.linspace(0, 2 * np.pi, 16, endpoint=False):
        d = np.array([np.sin(a), 0, np.cos(a)])
        add(C + d * dist(max(hw, hd), hh) + np.array([0, hy - C[1], 0]), Y, "ring")

# 8 corner-diagonal views: sight down each box corner toward center
for sx in (-1, 1):
    for sz in (-1, 1):
        for sy in (-1, 1):
            cdir = sx * u * hw + sz * w * hd + sy * Y * hh
            cdir = cdir / np.linalg.norm(cdir)
            add(C + cdir * dist(max(hw, hd, hh), max(hw, hh)), Y, "corner")

json.dump(dict(spz=SPZ, up=[0, 1, 0], cams=cams), open("cameras_focus.json", "w"))
print(f"rendered {len(cams)} focused views (16x2 ring + 8 corners) -> {OUT}/")
