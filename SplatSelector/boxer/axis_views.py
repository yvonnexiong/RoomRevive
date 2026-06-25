"""Render the box face-on along each of its axes, so each front edge is visible.

Camera is placed on each box axis looking at the center, so you sight straight
down the bounding-box edges and can judge per-face fit. Front-face edges are drawn
yellow, the rest green.
"""
import json, os, sys
import numpy as np
from PIL import Image, ImageDraw
from spz_io import parse_spz
from render import render, look_at

box = json.load(open(os.environ.get("BOX", "box.json")))
c = np.array(box["center"]); hw, hh, hd = box["half"]; yaw = box["yaw"]
data = json.load(open("cameras.json")); s = parse_spz(data["spz"])
p = s["positions"]; op = s["alphas"] / 255.0; solid = op > 0.5
room = np.array([p[solid, 0].mean(), p[solid, 1].mean(), p[solid, 2].mean()])

u = np.array([np.sin(yaw), 0, np.cos(yaw)])      # width axis
w = np.array([np.cos(yaw), 0, -np.sin(yaw)])     # depth axis
Y = np.array([0, 1.0, 0])
sfront = np.sign(np.dot(room - c, w)) or 1.0
fdir = w * sfront

# corners in the same order as build_corners (sz=+1 -> one depth face)
def mk(sx, sz, sy):
    return c + sx * u * hw + sz * w * hd + sy * Y * hh
cor = np.array([mk(-1,-1,-1), mk(1,-1,-1), mk(1,1,-1), mk(-1,1,-1),
                mk(-1,-1,1), mk(1,-1,1), mk(1,1,1), mk(-1,1,1)])
EDGES = [(0,1),(1,2),(2,3),(3,0),(4,5),(5,6),(6,7),(7,4),(0,4),(1,5),(2,6),(3,7)]
front_sz = 1 if sfront > 0 else -1
front_idx = {2,3,6,7} if front_sz == 1 else {0,1,4,5}

W = H = 760
FOV = 52.0
tan = np.tan(np.deg2rad(FOV) / 2)
RP = dict(min_alpha=0.6, max_r=5, exposure=1.0, near_cull=0.4, bg=(0.10,0.11,0.13))

def dist(eh, ev):  # so box fills ~65% of frame
    return max(eh, ev) / tan / 0.65 + 0.3

views = {
    "front": (c + fdir * dist(hw, hh), Y),
    "back":  (c - fdir * dist(hw, hh), Y),
    "left":  (c - u * dist(hd, hh), Y),
    "right": (c + u * dist(hd, hh), Y),
    "top":   (c + Y * dist(hw, hd), -fdir),
}
os.makedirs("axis", exist_ok=True)
for name, (eye, up) in views.items():
    img, view, f = render(s, eye, c, W=W, H=H, fov_y_deg=FOV, subsample=450000, up=tuple(up), **RP)
    pic = Image.fromarray((np.clip(img, 0, 1) ** 0.9 * 255).astype(np.uint8))
    d = ImageDraw.Draw(pic)
    ph = np.concatenate([cor, np.ones((8, 1))], 1)
    cm = ph @ view.T; z = cm[:, 2]
    px = W / 2 + f * cm[:, 0] / np.where(z != 0, -z, 1e-6)
    py = H / 2 - f * cm[:, 1] / np.where(z != 0, -z, 1e-6)
    for i, j in EDGES:
        if z[i] < -0.02 and z[j] < -0.02:
            col = (255, 220, 40) if (i in front_idx and j in front_idx) else (70, 255, 110)
            d.line([px[i], py[i], px[j], py[j]], fill=col, width=4)
    d.rectangle([8, 8, 150, 30], fill=(0, 0, 0))
    d.text((13, 12), f"{name.upper()}  {2*hw:.2f}x{2*hh:.2f}x{2*hd:.2f}", fill=(120, 255, 150))
    pic.save(os.path.join("axis", name + ".png"))
print("axis views (front-face edges in YELLOW):", ", ".join(views))
