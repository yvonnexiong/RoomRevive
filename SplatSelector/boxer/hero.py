"""High-res proof render: splat + fitted fridge OBB drawn boldly."""
import json
import numpy as np
from PIL import Image, ImageDraw
from spz_io import parse_spz
from render import render, project, look_at

data = json.load(open("cameras.json"))
box = json.load(open("box.json"))
C = np.array(box["corners"])
s = parse_spz(data["spz"])
edges = box.get("edges") or [(i, j) for i in range(8) for j in range(i + 1, 8) if bin(i ^ j).count("1") == 1]

W = H = 1000
# frame the fridge from a 3/4 angle
ctr = np.array(box["center"])
import os
eo = [float(x) for x in os.environ.get("EYEVEC", "2.2,0.35,-2.0").split(",")]
eye = ctr + np.array(eo)
RP = dict(min_alpha=float(os.environ.get("MINA", 0.45)), max_r=int(os.environ.get("MAXR", 6)),
          exposure=float(os.environ.get("EXPO", 1.0)), near_cull=float(os.environ.get("NEARC", 0.45)))
img, view, f = render(s, eye, ctr, W=W, H=H, fov_y_deg=58, subsample=320000, **RP)
pic = Image.fromarray((np.clip(img, 0, 1) ** 0.9 * 255).astype(np.uint8))
d = ImageDraw.Draw(pic)

ph = np.concatenate([C, np.ones((8, 1))], 1)
cs = ph @ view.T
z = cs[:, 2]
px = W / 2 + f * cs[:, 0] / np.maximum(-z, 1e-6)
py = H / 2 - f * cs[:, 1] / np.maximum(-z, 1e-6)
for i, j in edges:
    if z[i] < -0.05 and z[j] < -0.05:
        d.line([px[i], py[i], px[j], py[j]], fill=(70, 255, 110), width=5)
label = f"refrigerator  {box['size'][0]:.2f}W x {box['size'][1]:.2f}H x {box['size'][2]:.2f}D m"
d.rectangle([10, 10, 12 + len(label) * 11, 40], fill=(0, 0, 0))
d.text((16, 16), label, fill=(120, 255, 150))
pic.save("hero.png")
print("saved hero.png", box["size"])
