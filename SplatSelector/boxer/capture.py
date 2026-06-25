"""Render a set of posed interior views and record exact camera metadata.

Outputs frames/<name>.png and cameras.json (view matrix, focal, W/H per frame)
so 3D splat centers can be reprojected for multi-view voting.
"""
import json, os, sys, time
import numpy as np
from spz_io import parse_spz
from render import render, save_png

SPZ = sys.argv[1] if len(sys.argv) > 1 else r"C:\Users\chril\Downloads\Original Kitchen-restyled.spz"
OUT = "frames"
W = H = 700
FOV = 75.0
# render tuning (override per-scene noise)
RP = dict(min_alpha=float(os.environ.get("MINA", 0.7)),
          max_r=int(os.environ.get("MAXR", 3)),
          exposure=float(os.environ.get("EXPO", 1.2)),
          near_cull=float(os.environ.get("NEARC", 0.6)),
          bg=tuple(float(x) for x in os.environ.get("BG", "0.06,0.07,0.09").split(",")))
EYE_OFF = float(os.environ.get("EYEOFF", 2.0))   # height above floor
SUB = int(os.environ.get("SUB", 400000))

os.makedirs(OUT, exist_ok=True)
s = parse_spz(SPZ)
p = s["positions"]; op = s["alphas"] / 255.0; solid = op > 0.5
cx, cz = p[solid, 0].mean(), p[solid, 2].mean()
floor = np.percentile(p[solid, 1], 2)
eye_h = floor + EYE_OFF

# camera positions: center + 4 offsets toward quadrants, all at eye height
offs = [(0, 0), (1.6, 1.6), (-1.6, 1.6), (1.6, -1.6), (-1.6, -1.6)]
yaws = np.linspace(0, 2 * np.pi, 8, endpoint=False)

cams = []
t0 = time.time()
i = 0
for ox, oz in offs:
    eye = np.array([cx + ox, eye_h, cz + oz])
    for yi, th in enumerate(yaws):
        d = np.array([np.sin(th), 0, np.cos(th)])
        target = eye + d * 3
        img, view, f = render(s, eye, target, W=W, H=H, fov_y_deg=FOV, subsample=SUB, **RP)
        name = f"cam_{i:02d}"
        save_png(img, os.path.join(OUT, name + ".png"))
        cams.append(dict(name=name, eye=eye.tolist(), target=target.tolist(),
                         W=W, H=H, fov_y_deg=FOV, focal=float(f),
                         view=view.flatten().tolist()))
        i += 1
    print(f"position ({ox},{oz}) done  {time.time()-t0:.1f}s")

with open("cameras.json", "w") as fh:
    json.dump(dict(spz=SPZ, up=[0, 1, 0], cams=cams), fh)
print(f"rendered {len(cams)} views -> {OUT}/  (cameras.json)")
