"""Evaluate the K-node views (exported by the viewer's L key) for a 3D object.

Reads kpose.json {anchor, nodes, fov}: the yellow K hit point and the green node
positions. Renders an EWA view from each node looking at the anchor, runs OWLv2 over
several appliance prompts, and if the object is seen in enough views, carves the splats
by multi-view 2D-box consistency and fits a gravity-aligned OBB. Writes the box to
detected_boxes.json in the served folder so the open viewer adds it live (no reload).
"""
import truststore; truststore.inject_into_ssl()
import os, json, time
import numpy as np
import torch
from PIL import Image, ImageDraw
from transformers import Owlv2Processor, Owlv2ForObjectDetection
from spz_io import parse_spz
from render import render_ewa, subset_scene

KPOSE   = os.environ.get("KPOSE", r"C:\Users\chril\Downloads\kpose.json")
SERVED  = r"C:\Unity-Git\RoomRevive\HTML_Editor\detected_boxes.json"
T2D     = float(os.environ.get("T2D", 0.10))
RATIO   = float(os.environ.get("RATIO", 0.55))   # fraction of seeing views the splat must be inside the 2D box
REGION  = float(os.environ.get("REGION", 1.6))   # keep carved splats within this of the anchor (m)
PRESENT = float(os.environ.get("PRESENT", 0.18)) # min best score to declare an object present

PROMPTS = ["a refrigerator", "an oven", "a microwave", "a dishwasher",
           "a kitchen cabinet", "a sink", "a kitchen appliance"]
COLOR = {"refrigerator":[0.36,1.0,0.54], "oven":[0.32,0.78,1.0], "microwave":[1.0,0.62,0.2],
         "dishwasher":[0.95,0.45,0.85], "cabinet":[0.85,0.8,0.4], "sink":[0.4,0.85,1.0], "appliance":[1.0,0.85,0.1]}

def floater_mask(P, alpha, cell=0.06, maxd=15):
    solid = alpha > 0.2; vox = np.floor(P/cell).astype(np.int64)
    OFF, A, B = 1 << 19, 1 << 40, 1 << 20
    key = (vox[:,0]+OFF)*A + (vox[:,1]+OFF)*B + (vox[:,2]+OFF)
    uk, uc = np.unique(key[solid], return_counts=True); dens = np.zeros(len(P), np.int64)
    for dx in (-1,0,1):
        for dy in (-1,0,1):
            for dz in (-1,0,1):
                nk = (vox[:,0]+dx+OFF)*A+(vox[:,1]+dy+OFF)*B+(vox[:,2]+dz+OFF)
                ix = np.clip(np.searchsorted(uk, nk), 0, len(uk)-1)
                dens += np.where(uk[ix] == nk, uc[ix], 0)
    return dens < maxd

kp = json.load(open(KPOSE))
anchor = np.array(kp["anchor"], float)
nodes  = np.array(kp["nodes"], float).reshape(-1, 3)
fovdeg = float(np.degrees(kp.get("fov", 1.047)))
SPZ = json.load(open("cameras.json"))["spz"]
print(f"anchor {anchor.round(2).tolist()}  nodes {len(nodes)}  fov {fovdeg:.0f}deg  spz {SPZ}")

s = parse_spz(SPZ); P = s["positions"]; op = s["alphas"]/255.0
crop = json.load(open("room_crop.json")); C = np.array(crop["center"]); Hh = np.array(crop["size"])/2
inside = (np.abs(P[:,0]-C[0])<=Hh[0]) & (np.abs(P[:,1]-C[1])<=Hh[1]) & (np.abs(P[:,2]-C[2])<=Hh[2])
s = subset_scene(s, inside & ~floater_mask(P, op))
P = s["positions"]; opq = s["alphas"]/255.0 > 0.5
print(f"crop splats {s['n']:,}")

proc = Owlv2Processor.from_pretrained("google/owlv2-base-patch16-ensemble")
model = Owlv2ForObjectDetection.from_pretrained("google/owlv2-base-patch16-ensemble").eval()
torch.set_num_threads(os.cpu_count())

os.makedirs("kdet", exist_ok=True)
W = H = 680
dets = []   # (view4x4, focal, box, score, label)
t0 = time.time()
for i, node in enumerate(nodes):
    img, view, f = render_ewa(s, node, anchor, W=W, H=H, fov_y_deg=fovdeg,
                              min_alpha=0.2, near_cull=0.12, exposure=1.5, scale_mult=1.3)
    pic = Image.fromarray((np.clip(img,0,1)**0.9*255).astype(np.uint8))
    inp = proc(text=[PROMPTS], images=pic, return_tensors="pt")
    with torch.no_grad():
        out = model(**inp)
    r = proc.post_process_object_detection(out, threshold=T2D, target_sizes=torch.tensor([[H, W]]))[0]
    items = sorted(zip(r["scores"].tolist(), r["labels"].tolist(), r["boxes"].tolist()), key=lambda t:-t[0])
    dr = ImageDraw.Draw(pic)
    if items:
        sc, lb, bx = items[0]
        dets.append((view, f, bx, sc, lb))
        x0,y0,x1,y1 = bx; dr.rectangle([x0,y0,x1,y1], outline=(255,230,40), width=3)
        t = f"{PROMPTS[lb].split()[-1]} {sc:.2f}"; dr.rectangle([x0,y0-13,x0+len(t)*6.3,y0], fill=(0,0,0)); dr.text((x0+2,y0-12), t, fill=(255,230,40))
    pic.save(f"kdet/n{i:02d}.png")
    print(f"node {i:>2}: " + (f"{PROMPTS[items[0][1]].split()[-1]}={items[0][0]:.2f}" if items else "-") + f"  ({time.time()-t0:.0f}s)")

if not dets:
    print("NO object detected from any K-node view."); json.dump([], open(SERVED,"w")); raise SystemExit

best = max(dets, key=lambda d: d[3])
bestscore = best[3]
# label vote weighted by score
votes = {}
for _,_,_,sc,lb in dets:
    votes[lb] = votes.get(lb, 0)+sc
lb = max(votes, key=votes.get)
label = PROMPTS[lb].split()[-1]
print(f"\nbest score {bestscore:.2f}  -> label '{label}'  ({len(dets)} detecting views)")
if bestscore < PRESENT:
    print(f"below PRESENT={PRESENT}; declaring NO confident object."); json.dump([], open(SERVED,"w")); raise SystemExit

# ---- multi-view carve: keep splats inside the 2D box in >=RATIO of seeing views ----
pts = P[opq]; M = len(pts)
ph = np.concatenate([pts, np.ones((M,1))], 1)
seen = np.zeros(M, np.int32); vote = np.zeros(M, np.int32)
for view, f, box, sc, _ in dets:
    cm = ph @ view.T; z = cm[:,2]; depth = -z; front = depth > 0.05
    px = W/2 + f*cm[:,0]/np.where(z!=0,-z,1e9)
    py = H/2 - f*cm[:,1]/np.where(z!=0,-z,1e9)
    inimg = front & (px>=0)&(px<W)&(py>=0)&(py<H)
    x0,y0,x1,y1 = box
    seen += inimg; vote += inimg & (px>=x0)&(px<=x1)&(py>=y0)&(py<=y1)
ratio = vote/np.maximum(seen,1)
obj = (seen>=2) & (ratio>=RATIO) & (np.linalg.norm(pts-anchor, axis=1) < REGION)
print(f"carved object splats: {obj.sum():,}")
if obj.sum() < 80:
    print("too few carved points to fit a box."); json.dump([], open(SERVED,"w")); raise SystemExit
objp = pts[obj]

# ---- gravity-aligned OBB (yaw from PCA of carved XZ) ----
xz = objp[:,[0,2]]; xz0 = xz - xz.mean(0)
ev, evec = np.linalg.eigh(xz0.T @ xz0 / len(xz0)); axis = evec[:, np.argmax(ev)]
yaw = float(np.arctan2(axis[0], axis[1]))
u = np.array([np.sin(yaw),0,np.cos(yaw)]); w = np.array([np.cos(yaw),0,-np.sin(yaw)])
du = objp@u; dw = objp@w; dy = objp[:,1]
lo = lambda a: np.percentile(a,2); hi = lambda a: np.percentile(a,98)
duL,duH,dwL,dwH,dyL,dyH = lo(du),hi(du),lo(dw),hi(dw),lo(dy),hi(dy)
cu,cw,cy = (duL+duH)/2,(dwL+dwH)/2,(dyL+dyH)/2
hu,hw,hy = (duH-duL)/2,(dwH-dwL)/2,(dyH-dyL)/2
ctr = u*cu + w*cw + np.array([0,cy,0])
def corner(su,sw,sy): return (ctr + u*(su*hu) + w*(sw*hw) + np.array([0,sy*hy,0])).tolist()
corners = [corner(-1,-1,-1),corner(-1,1,-1),corner(1,1,-1),corner(1,-1,-1),
           corner(-1,-1,1),corner(-1,1,1),corner(1,1,1),corner(1,-1,1)]
color = COLOR.get(label, [1.0,0.85,0.1])
out = [{"id": f"kdet_{int(time.time())}", "name": f"{label} {bestscore:.2f}", "color": color, "corners": corners}]
json.dump(out, open(SERVED, "w"))
print(f"\nWROTE box -> {SERVED}")
print(f"  size {[round(2*hu,2),round(2*hy,2),round(2*hw,2)]}  center {ctr.round(2).tolist()}  yaw {np.degrees(yaw):.0f}deg")
