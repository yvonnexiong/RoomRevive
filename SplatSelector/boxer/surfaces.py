"""Detect flat surfaces from splat STRUCTURE and face them head-on.

Each Gaussian is a flat disc: its smallest-scale axis = the surface normal. We keep
flat, opaque splats inside the room crop, cluster them into planar patches (cells with
many co-oriented normals), then place the camera along each surface normal looking at
it -> truly face-on views. Then OWLv2 2D detection + gallery.
"""
import truststore; truststore.inject_into_ssl()
import os, json, time
import numpy as np
import torch
from PIL import Image, ImageDraw
from transformers import Owlv2Processor, Owlv2ForObjectDetection
from spz_io import parse_spz
from render import render_ewa, subset_scene

def floater_mask(P, alpha, cell=0.06, maxd=15):
    solid = alpha > 0.2; vox = np.floor(P/cell).astype(np.int64)
    OFF, A, B = 1 << 19, 1 << 40, 1 << 20
    key = (vox[:, 0]+OFF)*A + (vox[:, 1]+OFF)*B + (vox[:, 2]+OFF)
    uk, uc = np.unique(key[solid], return_counts=True); dens = np.zeros(len(P), np.int64)
    for dx in (-1, 0, 1):
        for dy in (-1, 0, 1):
            for dz in (-1, 0, 1):
                nk = (vox[:, 0]+dx+OFF)*A+(vox[:, 1]+dy+OFF)*B+(vox[:, 2]+dz+OFF)
                ix = np.clip(np.searchsorted(uk, nk), 0, len(uk)-1)
                dens += np.where(uk[ix] == nk, uc[ix], 0)
    return dens < maxd

crop = json.load(open("room_crop.json")); C = np.array(crop["center"]); Hh = np.array(crop["size"])/2
SPZ = json.load(open("cameras.json"))["spz"]
PROMPTS = ["a refrigerator", "an oven", "a microwave", "a dishwasher"]
COLORS = [(90,255,120), (90,200,255), (255,160,40), (255,90,220)]
s = parse_spz(SPZ); P = s["positions"]; op = s["alphas"]/255.0
inside = (np.abs(P[:,0]-C[0])<=Hh[0]) & (np.abs(P[:,1]-C[1])<=Hh[1]) & (np.abs(P[:,2]-C[2])<=Hh[2])
s = subset_scene(s, inside & ~floater_mask(P, op))
P = s["positions"]; op = s["alphas"]/255.0; q = s["quats"]; sc = np.exp(s["scales_log"])
N = s["n"]; print(f"crop splats {N:,}")

# per-splat surface normal = world dir of the smallest-scale axis; flatness = s_min/s_mid
w_, x_, y_, z_ = q[:,0], q[:,1], q[:,2], q[:,3]
R = np.empty((N, 3, 3))
R[:,0,0]=1-2*(y_*y_+z_*z_); R[:,0,1]=2*(x_*y_-w_*z_); R[:,0,2]=2*(x_*z_+w_*y_)
R[:,1,0]=2*(x_*y_+w_*z_); R[:,1,1]=1-2*(x_*x_+z_*z_); R[:,1,2]=2*(y_*z_-w_*x_)
R[:,2,0]=2*(x_*z_-w_*y_); R[:,2,1]=2*(y_*z_+w_*x_); R[:,2,2]=1-2*(x_*x_+y_*y_)
k = np.argmin(sc, axis=1)
normal = np.take_along_axis(R, k[:, None, None], axis=2)[:, :, 0]
normal /= (np.linalg.norm(normal, axis=1, keepdims=True) + 1e-9)
ss = np.sort(sc, axis=1)
flat = (ss[:,0] < 0.6*ss[:,1]) & (op > 0.5)             # disc-like + solid
fi = np.where(flat)[0]
print(f"flat splats {fi.size:,}")

# cluster flat splats into planar patches: voxel cell + coherent mean normal
CELL = 0.5
vox = np.floor(P[fi]/CELL).astype(np.int64)
key = (vox[:,0]+512)*1048576 + (vox[:,1]+512)*1024 + (vox[:,2]+512)
order = np.argsort(key); key_s = key[order]; fi_s = fi[order]
patches = []
b = 0
while b < len(key_s):
    e = b
    while e < len(key_s) and key_s[e] == key_s[b]:
        e += 1
    grp = fi_s[b:e]
    if grp.size >= 30:
        nmean = normal[grp].mean(0); coh = np.linalg.norm(nmean)
        if coh > 0.70:                                   # normals agree -> a real flat surface
            ctr = P[grp].mean(0); nrm = nmean/coh
            if abs(nrm[1]) < 0.8:                         # vertical-ish surface (wall / front), not floor/ceiling
                patches.append((ctr, nrm, grp.size))
    b = e
# merge nearby patches with similar normal
patches.sort(key=lambda t:-t[2]); merged = []
for ctr, nrm, cnt in patches:
    hit = False
    for m in merged:
        if np.linalg.norm(ctr-m[0]) < 0.8 and abs(np.dot(nrm, m[1])) > 0.88:
            m[3] += cnt; hit = True; break
    if not hit:
        merged.append([ctr, nrm, cnt, cnt])
merged.sort(key=lambda t:-t[3])
surfaces = merged[:16]
print(f"{len(surfaces)} surfaces (top {len(surfaces)})")

proc = Owlv2Processor.from_pretrained("google/owlv2-base-patch16-ensemble")
model = Owlv2ForObjectDetection.from_pretrained("google/owlv2-base-patch16-ensemble").eval()
torch.set_num_threads(os.cpu_count())
W = Hh_img = 680; DIST = 2.2
os.makedirs("surf", exist_ok=True); t0 = time.time()
for i, (ctr, nrm, _, cnt) in enumerate(surfaces):
    n_in = nrm * (1 if np.dot(nrm, C-ctr) > 0 else -1)   # point toward room interior
    eye = ctr + n_in*DIST; up = (0, 1, 0) if abs(n_in[1]) < 0.9 else (0, 0, 1)
    img, _, _ = render_ewa(s, eye, ctr, W=W, H=Hh_img, fov_y_deg=70, up=up,
                           min_alpha=0.2, near_cull=0.15, exposure=1.5, scale_mult=1.3)
    pic = Image.fromarray((np.clip(img,0,1)**0.9*255).astype(np.uint8))
    inp = proc(text=[PROMPTS], images=pic, return_tensors="pt")
    with torch.no_grad():
        out = model(**inp)
    r = proc.post_process_object_detection(out, threshold=0.12, target_sizes=torch.tensor([[Hh_img, W]]))[0]
    items = sorted(zip(r["scores"].tolist(), r["labels"].tolist(), r["boxes"].tolist()), key=lambda t:-t[0])[:6]
    dr = ImageDraw.Draw(pic)
    for scr, lb, (x0,y0,x1,y1) in items:
        c = COLORS[lb]; dr.rectangle([x0,y0,x1,y1], outline=c, width=3)
        t = f"{PROMPTS[lb].split()[-1]} {scr:.2f}"; dr.rectangle([x0,y0-13,x0+len(t)*6.3,y0], fill=(0,0,0)); dr.text((x0+2,y0-12), t, fill=c)
    dr.rectangle([0,0,210,15], fill=(0,0,0)); dr.text((4,2), f"surf {i}  n=({nrm[0]:.2f},{nrm[1]:.2f},{nrm[2]:.2f})", fill=(230,230,230))
    pic.save(f"surf/s{i:02d}.png")
    print(f"surf {i:>2} cnt={cnt:>5} n=({nrm[0]:+.2f},{nrm[1]:+.2f},{nrm[2]:+.2f}): " +
          (", ".join(f"{PROMPTS[lb].split()[-1]}={scr:.2f}" for scr,lb,_ in items) or "-") + f"  ({time.time()-t0:.0f}s)")
print(f"saved {len(surfaces)} -> surf/")
