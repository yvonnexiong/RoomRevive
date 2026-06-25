"""Detect across ALL allowed object types over the existing multi-view set, pick the
single highest-confidence object, carve its splats by multi-view 2D-box consistency,
fit a gravity-aligned 3D OBB, and APPEND it to detected_boxes.json in the served folder
so the open viewer adds it live (no reload)."""
import truststore; truststore.inject_into_ssl()
import os, json, time, hashlib
import numpy as np
import torch
from PIL import Image
from transformers import Owlv2Processor, Owlv2ForObjectDetection
from spz_io import parse_spz

SERVED = r"C:\Unity-Git\RoomRevive\HTML_Editor\detected_boxes.json"
T2D, RATIO, MINSEEN = 0.10, 0.55, 2
PROMPTS = ["a refrigerator", "an oven", "a microwave", "a dishwasher"]
COLOR   = {0:[0.36,1.0,0.54], 1:[0.32,0.78,1.0], 2:[1.0,0.62,0.2], 3:[0.95,0.45,0.85]}
# types already recognised by the first pass -> not candidates again
EXCLUDE = set(int(x) for x in os.environ.get("EXCLUDE", "0,1,2").split(",") if x != "")
APPEND  = os.environ.get("APPEND", "0") == "1"
CAMS    = ["cameras.json", "cameras_orbit.json"]; FRAMES = ["frames", "frames_orbit"]

views, spz = [], None
for cf, fd in zip(CAMS, FRAMES):
    d = json.load(open(cf)); spz = d["spz"]
    for c in d["cams"]:
        c["_fd"] = fd; views.append(c)
s = parse_spz(spz); P = s["positions"]; opq = s["alphas"]/255.0 > 0.5
idx = np.where(opq)[0]; pts = P[idx]; M = len(pts)
print(f"{len(views)} views, {M:,} opaque pts")

ckey = hashlib.md5(("ALL|"+",".join(CAMS)+f"|{T2D}").encode()).hexdigest()[:10]
cache = f"dets_all_{ckey}.json"
if os.path.exists(cache):
    perview = json.load(open(cache)); print(f"loaded cache {cache}")
else:
    proc = Owlv2Processor.from_pretrained("google/owlv2-base-patch16-ensemble")
    model = Owlv2ForObjectDetection.from_pretrained("google/owlv2-base-patch16-ensemble").eval()
    torch.set_num_threads(os.cpu_count()); perview = {}; t0 = time.time()
    for i, cam in enumerate(views):
        img = Image.open(os.path.join(cam["_fd"], cam["name"]+".png")).convert("RGB")
        inp = proc(text=[PROMPTS], images=img, return_tensors="pt")
        with torch.no_grad(): out = model(**inp)
        r = proc.post_process_object_detection(out, threshold=T2D, target_sizes=torch.tensor([img.size[::-1]]))[0]
        perview[cam["name"]] = [[int(l), [float(x) for x in b], float(sc)]
                               for sc, l, b in zip(r["scores"].tolist(), r["labels"].tolist(), r["boxes"].tolist())]
        if i % 15 == 0: print(f"  {i}/{len(views)} ({time.time()-t0:.0f}s)")
    json.dump(perview, open(cache, "w")); print(f"cached -> {cache}")

# best detection per label across all views, restricted to non-excluded (allowed) types
bestlab, bestsc = None, -1
for name, ds in perview.items():
    for lab, box, sc in ds:
        if lab in EXCLUDE: continue
        if sc > bestsc: bestsc, bestlab = sc, lab
print("\nconfidence per type (best view):")
for lab in range(len(PROMPTS)):
    sc = max([d[2] for ds in perview.values() for d in ds if d[0]==lab] or [0])
    tag = " [already recognised]" if lab in EXCLUDE else ("   <-- winner" if lab==bestlab else "")
    print(f"  {PROMPTS[lab].split()[-1]:<12} {sc:.2f}{tag}")
if bestlab is None:
    print("no allowed (non-excluded) type detected."); raise SystemExit
label = PROMPTS[bestlab].split()[-1]
print(f"\nWINNER: {label}  ({bestsc:.2f})")

# carve with the winner label's box per view
camby = {c["name"]: c for c in views}
seen = np.zeros(M, np.int32); vote = np.zeros(M, np.int32); ph = np.concatenate([pts, np.ones((M,1))], 1)
for name, ds in perview.items():
    win = [d for d in ds if d[0]==bestlab]
    if not win: continue
    box = max(win, key=lambda d:d[2])[1]; cam = camby[name]
    view = np.array(cam["view"]).reshape(4,4); f = cam["focal"]; W=cam["W"]; H=cam["H"]
    cm = ph @ view.T; z = cm[:,2]; front = -z > 0.05
    px = W/2 + f*cm[:,0]/np.where(z!=0,-z,1e9); py = H/2 - f*cm[:,1]/np.where(z!=0,-z,1e9)
    inimg = front & (px>=0)&(px<W)&(py>=0)&(py<H); x0,y0,x1,y1 = box
    seen += inimg; vote += inimg & (px>=x0)&(px<=x1)&(py>=y0)&(py<=y1)
ratio = vote/np.maximum(seen,1); obj = (seen>=MINSEEN) & (ratio>=RATIO)
print(f"carved {obj.sum():,} splats")
objp = pts[obj]
ctr0 = np.median(objp,0); objp = objp[np.linalg.norm(objp-ctr0,axis=1) < 1.8]

xz = objp[:,[0,2]]; xz0 = xz-xz.mean(0); ev,evec = np.linalg.eigh(xz0.T@xz0/len(xz0))
yaw = float(np.arctan2(evec[0,np.argmax(ev)], evec[1,np.argmax(ev)]))
u = np.array([np.sin(yaw),0,np.cos(yaw)]); w = np.array([np.cos(yaw),0,-np.sin(yaw)])
du,dw,dy = objp@u, objp@w, objp[:,1]
lo=lambda a:np.percentile(a,2); hi=lambda a:np.percentile(a,98)
duL,duH,dwL,dwH,dyL,dyH = lo(du),hi(du),lo(dw),hi(dw),lo(dy),hi(dy)
hu,hw,hy = (duH-duL)/2,(dwH-dwL)/2,(dyH-dyL)/2
ctr = u*((duL+duH)/2) + w*((dwL+dwH)/2) + np.array([0,(dyL+dyH)/2,0])
corner=lambda su,sw,sy:(ctr+u*(su*hu)+w*(sw*hw)+np.array([0,sy*hy,0])).tolist()
corners=[corner(-1,-1,-1),corner(-1,1,-1),corner(1,1,-1),corner(1,-1,-1),
         corner(-1,-1,1),corner(-1,1,1),corner(1,1,1),corner(1,-1,1)]

existing = []
if APPEND and os.path.exists(SERVED):
    try: existing = json.load(open(SERVED))
    except: existing = []
existing.append({"id": f"auto_{label}_{int(time.time())}", "name": f"{label} {bestsc:.2f}",
                 "color": COLOR[bestlab], "corners": corners})
json.dump(existing, open(SERVED,"w"))
print(f"\nAPPENDED '{label}' box -> {SERVED}  (now {len(existing)} dynamic box(es))")
print(f"  size {[round(2*hu,2),round(2*hy,2),round(2*hw,2)]}  center {ctr.round(2).tolist()}  yaw {np.degrees(yaw):.0f}deg")
