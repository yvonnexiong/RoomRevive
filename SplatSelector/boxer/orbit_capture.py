"""Pass-2 active capture: dense orbit of views framed on the detected fridge.

Reads the pass-1 box center, places cameras on the room-facing side (so we don't
shoot through the wall behind the fridge) across several yaws, heights and radii,
all looking at the fridge. Many confident looks -> better BoxerNet fusion.
"""
import json, os
import numpy as np
from spz_io import parse_spz
from render import render, save_png, density_keep, subset_scene

box = json.load(open("box.json"))
C = np.array(box["center"])
data = json.load(open("cameras.json"))
SPZ = data["spz"]
s = parse_spz(SPZ)
p = s["positions"]; op = s["alphas"] / 255.0; solid = op > 0.5
room = np.array([p[solid, 0].mean(), p[solid, 1].mean(), p[solid, 2].mean()])
floor = np.percentile(p[solid, 1], 2)

# direction from fridge toward the room interior (the open, viewable side)
d = room - C
base = np.arctan2(d[0], d[2])               # yaw pointing into the room
W = H = 700
FOV = 72.0
RP = dict(min_alpha=0.7, max_r=3, exposure=1.2, near_cull=0.6)   # detector-friendly (high contrast)
SUB = 300000

OUT = "frames_orbit"; os.makedirs(OUT, exist_ok=True)
yaws = np.linspace(-1.15, 1.15, 11)          # +-66 deg around the room-facing axis
radii = [2.7, 3.6]                           # back off: fridge ~half-frame (OWL drops >0.9-frame boxes)
heights = [floor + 1.1, floor + 1.7]         # two eye heights
cams = []; i = 0
for r in radii:
    for hy in heights:
        for dy in yaws:
            a = base + dy
            eye = np.array([C[0] + r * np.sin(a), hy, C[2] + r * np.cos(a)])
            tgt = C.copy()
            img, view, f = render(s, eye, tgt, W=W, H=H, fov_y_deg=FOV, subsample=SUB, **RP)
            name = f"orb_{i:03d}"
            save_png(img, os.path.join(OUT, name + ".png"))
            cams.append(dict(name=name, eye=eye.tolist(), target=tgt.tolist(),
                             W=W, H=H, fov_y_deg=FOV, focal=float(f),
                             view=view.flatten().tolist()))
            i += 1
json.dump(dict(spz=SPZ, up=[0, 1, 0], cams=cams), open("cameras_orbit.json", "w"))
print(f"rendered {len(cams)} orbit views around fridge {C.round(2).tolist()} -> {OUT}/")
