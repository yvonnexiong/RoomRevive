"""Walk the room (inside the saved crop box), facing the flat splat surfaces head-on
at varied heights (incl. very low). Render clear EWA images, run OWLv2 2D detection,
and write a gallery. Only tracks inside the crop box."""
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

crop = json.load(open("room_crop.json"))
C = np.array(crop["center"]); Hh = np.array(crop["size"]) / 2.0
SPZ = json.load(open("cameras.json"))["spz"]
PROMPTS = ["a refrigerator", "an oven", "a microwave", "a dishwasher"]
COLORS  = [(90,255,120), (90,200,255), (255,160,40), (255,90,220)]
THRESH = 0.12

s = parse_spz(SPZ); P = s["positions"]; op = s["alphas"] / 255.0
inside = (np.abs(P[:,0]-C[0]) <= Hh[0]) & (np.abs(P[:,1]-C[1]) <= Hh[1]) & (np.abs(P[:,2]-C[2]) <= Hh[2])
fl = floater_mask(P, op)
s = subset_scene(s, inside & ~fl)
P2 = s["positions"]; op2 = s["alphas"]/255.0
floor = float(np.percentile(P2[op2>0.5, 1], 2))
print(f"crop splats: {s['n']:,}  floor {floor:.2f}")

# walk: stand at interior spots (varied height, some VERY low) and look OUTWARD at the
# surrounding surfaces from several yaws -> face-on views at a good distance.
cx, cy, cz = C; hx, hy, hz = Hh
spots = [
    (cx,       floor+1.55, cz),      # high, centre
    (cx,       floor+0.25, cz),      # VERY low, centre
    (cx,       floor+1.05, cz-2.8),  # mid, one end
    (cx,       floor+0.40, cz+2.8),  # low, other end
    (cx-1.1,   floor+1.05, cz+0.8),  # mid, off to a side
    (cx+0.9,   floor+0.30, cz-1.2),  # VERY low, side
]
NY = 6
poses = []
for (sx, sy, sz_) in spots:
    for k in range(NY):
        th = 2*np.pi*k/NY
        d = np.array([np.sin(th), 0, np.cos(th)])
        poses.append((np.array([sx, sy, sz_]), np.array([sx, sy, sz_]) + d*3.0))
print(f"{len(poses)} poses ({len(spots)} spots x {NY} yaws)")

proc = Owlv2Processor.from_pretrained("google/owlv2-base-patch16-ensemble")
model = Owlv2ForObjectDetection.from_pretrained("google/owlv2-base-patch16-ensemble").eval()
torch.set_num_threads(os.cpu_count())

W = H = 680
os.makedirs("walk", exist_ok=True)
import time; t0 = time.time()
for i, (eye, target) in enumerate(poses):
    img, _, _ = render_ewa(s, eye, target, W=W, H=H, fov_y_deg=70,
                           min_alpha=0.2, near_cull=0.15, exposure=1.5, scale_mult=1.3)
    pic = Image.fromarray((np.clip(img, 0, 1)**0.9 * 255).astype(np.uint8))
    inp = proc(text=[PROMPTS], images=pic, return_tensors="pt")
    with torch.no_grad():
        out = model(**inp)
    r = proc.post_process_object_detection(out, threshold=THRESH, target_sizes=torch.tensor([[H, W]]))[0]
    items = sorted(zip(r["scores"].tolist(), r["labels"].tolist(), r["boxes"].tolist()), key=lambda t:-t[0])[:6]
    dr = ImageDraw.Draw(pic)
    for sc, lb, (x0,y0,x1,y1) in items:
        col = COLORS[lb]; dr.rectangle([x0,y0,x1,y1], outline=col, width=3)
        t = f"{PROMPTS[lb].split()[-1]} {sc:.2f}"
        dr.rectangle([x0,y0-13,x0+len(t)*6.3,y0], fill=(0,0,0)); dr.text((x0+2,y0-12), t, fill=col)
    dr.rectangle([0,0,90,15], fill=(0,0,0)); dr.text((4,2), f"shot {i}", fill=(230,230,230))
    pic.save(f"walk/w{i:02d}.png")
    print(f"shot {i:>2}: " + (", ".join(f"{PROMPTS[lb].split()[-1]}={sc:.2f}" for sc,lb,_ in items) or "-") + f"  ({time.time()-t0:.0f}s)")
print(f"saved {len(poses)} -> walk/")
