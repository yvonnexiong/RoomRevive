"""Dishwasher search, OWLv2, from the ceiling pivot with viewer-quality EWA renders.
Camera at origin cast up to the ceiling, dropped slightly, pivots there; 10 views
spun around aiming down ~38 deg. Anisotropic splatting (all splats) for clear images."""
import truststore; truststore.inject_into_ssl()
import os, json
import numpy as np
import torch
from PIL import Image, ImageDraw
from transformers import Owlv2Processor, Owlv2ForObjectDetection
from spz_io import parse_spz
from render import render_ewa, subset_scene

def floater_mask(P, alpha, cell=0.06, maxd=15):
    solid = alpha > 0.2
    vox = np.floor(P / cell).astype(np.int64)
    OFF, A, B = 1 << 19, 1 << 40, 1 << 20
    key = (vox[:, 0]+OFF)*A + (vox[:, 1]+OFF)*B + (vox[:, 2]+OFF)
    uk, uc = np.unique(key[solid], return_counts=True)
    dens = np.zeros(len(P), np.int64)
    for dx in (-1, 0, 1):
        for dy in (-1, 0, 1):
            for dz in (-1, 0, 1):
                nk = (vox[:, 0]+dx+OFF)*A + (vox[:, 1]+dy+OFF)*B + (vox[:, 2]+dz+OFF)
                ix = np.clip(np.searchsorted(uk, nk), 0, len(uk)-1)
                dens += np.where(uk[ix] == nk, uc[ix], 0)
    return dens < maxd

SPZ = json.load(open("cameras.json"))["spz"]
PROMPTS = ["a dishwasher", "a dishwasher panel", "a built-in dishwasher"]
THRESH = 0.10
s = parse_spz(SPZ); P = s["positions"]; op = s["alphas"] / 255.0
fl = floater_mask(P, op)
axis = (P[:, 0]**2 + P[:, 2]**2 < 0.35**2) & (op > 0.5) & (~fl) & (P[:, 1] > 0.5)
ceiling = float(np.percentile(P[axis, 1], 4))
eye = np.array([0.0, ceiling - 0.20, 0.0])
s = subset_scene(s, ~fl)
pitch = np.deg2rad(38)
print(f"ceiling {ceiling:.2f}  pivot {eye.round(2).tolist()}  splats {s['n']:,}")

proc = Owlv2Processor.from_pretrained("google/owlv2-base-patch16-ensemble")
model = Owlv2ForObjectDetection.from_pretrained("google/owlv2-base-patch16-ensemble").eval()
torch.set_num_threads(os.cpu_count())

W = H = 760
os.makedirs("det10", exist_ok=True)
best = None
for i, th in enumerate(np.linspace(0, 2*np.pi, 10, endpoint=False)):
    dv = np.array([np.sin(th)*np.cos(pitch), -np.sin(pitch), np.cos(th)*np.cos(pitch)])
    target = eye + dv*2.5
    img, _, _ = render_ewa(s, eye, target, W=W, H=H, fov_y_deg=82,
                           min_alpha=0.2, near_cull=0.2, exposure=1.45, scale_mult=1.3)
    pic = Image.fromarray((np.clip(img, 0, 1)**0.9 * 255).astype(np.uint8))
    inp = proc(text=[PROMPTS], images=pic, return_tensors="pt")
    with torch.no_grad():
        out = model(**inp)
    r = proc.post_process_object_detection(out, threshold=THRESH, target_sizes=torch.tensor([[H, W]]))[0]
    dets = sorted(zip(r["scores"].tolist(), r["boxes"].tolist()), key=lambda t: -t[0])[:6]
    dr = ImageDraw.Draw(pic)
    for j, (sc, (x0, y0, x1, y1)) in enumerate(dets):
        col = (255, 90, 220) if j == 0 else (150, 90, 150)
        dr.rectangle([x0, y0, x1, y1], outline=col, width=3 if j == 0 else 2)
        t = f"dishwasher {sc:.2f}"
        dr.rectangle([x0, y0-14, x0+len(t)*6.5, y0], fill=(0, 0, 0)); dr.text((x0+2, y0-13), t, fill=col)
    dr.rectangle([0, 0, 165, 16], fill=(0, 0, 0)); dr.text((4, 2), f"angle {i}  yaw {int(np.degrees(th))} deg", fill=(230, 230, 230))
    pic.save(f"det10/a{i}.png")
    top = dets[0][0] if dets else 0.0
    if dets and (best is None or top > best[1]):
        best = (i, top)
    print(f"angle {i:>2} (yaw {int(np.degrees(th)):>3}): top dishwasher = {top:.2f}  ({len(dets)} dets)")
print(f"\nBEST: angle {best[0]} score {best[1]:.2f}" if best else "\nno dishwasher found")
print("saved det10/a0..a9.png")
