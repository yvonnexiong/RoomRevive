"""Overlay the fitted 3D OBB edges onto rendered frames for verification."""
import json, sys, os
import numpy as np
from PIL import Image, ImageDraw

data = json.load(open("cameras.json"))
cams = {c["name"]: c for c in data["cams"]}
box = json.load(open("box.json"))
C = np.array(box["corners"])

edges = box.get("edges") or [(i, j) for i in range(8) for j in range(i + 1, 8)
                             if bin(i ^ j).count("1") == 1]

names = sys.argv[1:] or box["views"]
os.makedirs("overlay", exist_ok=True)
for name in names:
    cam = cams[name]
    view = np.array(cam["view"]).reshape(4, 4)
    f = cam["focal"]; W = cam["W"]; H = cam["H"]
    ph = np.concatenate([C, np.ones((8, 1))], 1)
    cs = ph @ view.T
    z = cs[:, 2]
    px = W / 2 + f * cs[:, 0] / np.maximum(-z, 1e-6)
    py = H / 2 - f * cs[:, 1] / np.maximum(-z, 1e-6)
    img = Image.open(os.path.join("frames", name + ".png")).convert("RGB")
    d = ImageDraw.Draw(img)
    for i, j in edges:
        if z[i] < -0.05 and z[j] < -0.05:   # both in front
            d.line([px[i], py[i], px[j], py[j]], fill=(60, 255, 90), width=3)
    img.save(os.path.join("overlay", name + ".png"))
    print("overlay/" + name + ".png")
