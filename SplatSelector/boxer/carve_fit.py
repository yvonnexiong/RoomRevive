"""General multi-view silhouette carving -> tight oriented box.

Object-agnostic. For an open-vocab text prompt:
  1. OWLv2 detects the object's 2D box in every rendered view.
  2. Every opaque splat is projected into each *detecting* view.
  3. Keep points that land INSIDE the 2D box in a high fraction of the views that
     see them (consistency ratio). Views from many angles carve away the wall and
     neighbours behind/beside the object -> a tight 3D hull.
  4. Fit a gravity-aligned OBB (yaw from PCA of the carved points).

No colour/size assumptions -> works for any object. Tunable: RATIO, MINSEEN.

Env: PROMPT, RATIO, MINSEEN, MINVOTE, CAMERAS (comma list), FRAMES (comma list),
     OUTBOX, T2D (detector threshold), REGION (gross sanity radius, m).
"""
import truststore; truststore.inject_into_ssl()
import json, os, sys, time
import numpy as np
import torch
from PIL import Image
from transformers import Owlv2Processor, Owlv2ForObjectDetection
from spz_io import parse_spz

PROMPTS = os.environ.get("PROMPT", "a refrigerator,a fridge").split(",")
RATIO = float(os.environ.get("RATIO", 0.65))
MINSEEN = int(os.environ.get("MINSEEN", 4))
MINVOTE = int(os.environ.get("MINVOTE", 3))
T2D = float(os.environ.get("T2D", 0.1))
OUTBOX = os.environ.get("OUTBOX", "box.json")
REGION = float(os.environ.get("REGION", 0))   # 0 = off; else keep pts within this of detections' center
cam_files = os.environ.get("CAMERAS", "cameras.json,cameras_orbit.json").split(",")
frame_dirs = os.environ.get("FRAMES", "frames,frames_orbit").split(",")

# ---- gather views ----
views = []
spz = None
for cf, fd in zip(cam_files, frame_dirs):
    d = json.load(open(cf)); spz = d["spz"]
    for c in d["cams"]:
        c["_frames"] = fd; views.append(c)
s = parse_spz(spz)
P = s["positions"]; op = s["alphas"] / 255.0
opq = op > 0.5
idx = np.where(opq)[0]
pts = P[idx]
M = pts.shape[0]
print(f"{len(views)} views, {M:,} opaque points, prompt={PROMPTS}")

# ---- OWLv2 detection per view (best box), cached by prompt+views ----
import hashlib
ckey = hashlib.md5(("|".join(PROMPTS) + "|" + ",".join(cam_files) + f"|{T2D}").encode()).hexdigest()[:10]
cache = f"dets_cache_{ckey}.json"
byname = {}
if os.path.exists(cache):
    byname = json.load(open(cache))
    print(f"loaded {len(byname)} cached detections ({cache})")
else:
    proc = Owlv2Processor.from_pretrained("google/owlv2-base-patch16-ensemble")
    model = Owlv2ForObjectDetection.from_pretrained("google/owlv2-base-patch16-ensemble").eval()
    torch.set_num_threads(os.cpu_count())
    t0 = time.time()
    for cam in views:
        img = Image.open(os.path.join(cam["_frames"], cam["name"] + ".png")).convert("RGB")
        inp = proc(text=[PROMPTS], images=img, return_tensors="pt")
        with torch.no_grad():
            out = model(**inp)
        r = proc.post_process_object_detection(out, threshold=T2D,
                                               target_sizes=torch.tensor([img.size[::-1]]))[0]
        if len(r["scores"]) == 0:
            continue
        b = int(r["scores"].argmax())
        byname[cam["name"]] = [[float(x) for x in r["boxes"][b]], float(r["scores"][b])]
    json.dump(byname, open(cache, "w"))
    print(f"detections in {len(byname)}/{len(views)} views ({time.time()-t0:.0f}s) -> {cache}")
dets = [(cam, byname[cam["name"]][0], byname[cam["name"]][1]) for cam in views if cam["name"] in byname]

# ---- carve ----
seen = np.zeros(M, np.int32); vote = np.zeros(M, np.int32)
for cam, box, sc in dets:
    view = np.array(cam["view"]).reshape(4, 4); f = cam["focal"]; W = cam["W"]; H = cam["H"]
    x0, y0, x1, y1 = box
    ph = np.concatenate([pts, np.ones((M, 1))], 1)
    cm = ph @ view.T; z = cm[:, 2]; depth = -z
    front = depth > 0.05
    px = W / 2 + f * cm[:, 0] / np.where(z != 0, -z, 1e9)
    py = H / 2 - f * cm[:, 1] / np.where(z != 0, -z, 1e9)
    inimg = front & (px >= 0) & (px < W) & (py >= 0) & (py < H)
    inbox = inimg & (px >= x0) & (px <= x1) & (py >= y0) & (py <= y1)
    seen += inimg; vote += inbox
ratio = vote / np.maximum(seen, 1)
obj = (seen >= MINSEEN) & (vote >= MINVOTE) & (ratio >= RATIO)
print(f"carved object points: {obj.sum():,}  (ratio>={RATIO}, seen>={MINSEEN})")

np.save("obj_idx.npy", idx[obj])   # global indices of detected object splats (for rayfit)
objp = pts[obj]
if REGION > 0:
    ctr0 = np.median(objp, axis=0)
    objp = objp[np.linalg.norm(objp - ctr0, axis=1) < REGION]

# ---- gravity-aligned OBB: yaw from BoxerNet (reliable, general) else PCA ----
yawsrc = os.environ.get("YAWSRC", "boxernet")
if yawsrc == "boxernet" and os.path.exists("box_boxernet.json"):
    yaw = float(json.load(open("box_boxernet.json"))["yaw"])
else:
    xz = objp[:, [0, 2]]; xz0 = xz - xz.mean(0)
    ev, evec = np.linalg.eigh(xz0.T @ xz0 / len(xz0))
    axis = evec[:, np.argmax(ev)]
    yaw = float(np.arctan2(axis[0], axis[1]))
u = np.array([np.sin(yaw), 0, np.cos(yaw)]); w = np.array([np.cos(yaw), 0, -np.sin(yaw)])
du = objp @ u; dw = objp @ w; dy = objp[:, 1]
FIT = os.environ.get("FIT", "pct")
def dense_extent(vals, bins=70, frac=0.12):
    h, e = np.histogram(vals, bins=bins)
    above = np.where(h >= frac * h.max())[0]
    return e[above[0]], e[above[-1] + 1]
if FIT == "density":   # trim sparse surround tails -> hug the dense object core (general)
    duL, duH = dense_extent(du); dyL, dyH = dense_extent(dy); dwL, dwH = dense_extent(dw)
else:
    lo = lambda a: np.percentile(a, 1.5); hi = lambda a: np.percentile(a, 98.5)
    duL, duH, dwL, dwH, dyL, dyH = lo(du), hi(du), lo(dw), hi(dw), lo(dy), hi(dy)
# clamp the bottom face to the auto-detected floor (peak of opaque-Y histogram, lower 40%)
if os.environ.get("CLAMP_FLOOR", "0") == "1":
    yall = P[opq][:, 1]; ymn, ymx = float(yall.min()), float(yall.max())
    hcnt, edg = np.histogram(yall, bins=140, range=(ymn, ymx))
    fb = int(np.argmax(hcnt[:int(140 * 0.4)]))
    floor = edg[fb] + (edg[1] - edg[0]) / 2
    dyL = floor
    print(f"  floor clamp: bottom set to {floor:.2f} m")
# appliance-top: an appliance is PROUD of its surround; the cabinet above it is
# recessed. So the top of the front-most (proud) surface = the appliance top.
# General geometric signal (no colour), works for any object set into cabinetry.
if os.environ.get("APPLIANCE_TOP", "0") == "1":
    Pn = P[opq]
    dun = Pn @ u; dwn = Pn @ w; dyn = Pn[:, 1]
    # provisional center to know which depth side faces the room
    prov = u * ((duL+duH)/2) + w * ((dwL+dwH)/2)
    room = np.array([P[opq, 0].mean(), P[opq, 1].mean(), P[opq, 2].mean()])
    fsgn = np.sign(np.dot(room - prov, w)) or 1.0
    fcoord = dwn * fsgn; fext = (dwH if fsgn > 0 else -dwL)   # front-most coordinate / extent
    PROUD = float(os.environ.get("PROUD", 0.12))
    foot = (dun > duL) & (dun < duH) & (dyn > dyL + 0.1)
    front = foot & (fcoord > fext - PROUD)        # the proud front slab
    if front.sum() > 200:
        new_top = float(np.percentile(dyn[front], 95))
        if new_top < dyH:
            print(f"  appliance-top(proud): top {dyH:.2f} -> {new_top:.2f} m ({front.sum()} front pts)")
            dyH = new_top
hw, hd, hh = (duH-duL)/2, (dwH-dwL)/2, (dyH-dyL)/2
cu, cw, cy = (duL+duH)/2, (dwL+dwH)/2, (dyL+dyH)/2
# Depth is poorly constrained by carving (few oblique views) -> take depth + its
# placement from BoxerNet, which infers occluded extent for ANY object. Carve only
# the well-constrained width + height. CARVE_DEPTH=1 to override.
# Per-axis best source: carve constrains WIDTH well; HEIGHT & DEPTH from BoxerNet
# (stable + infers occluded extent). HSRC/CARVE_DEPTH=carve to override.
bn = json.load(open("box_boxernet.json")) if os.path.exists("box_boxernet.json") else None
if bn and os.environ.get("CARVE_DEPTH", "0") != "1":
    hd = float(bn["half"][2]); cw = float(np.array(bn["center"]) @ w)
if bn and os.environ.get("HSRC", "carve") == "boxernet":
    hh = float(bn["half"][1]); cy = float(bn["center"][1])
center = u * cu + w * cw + np.array([0, cy, 0])

def build_corners(c, hw, hh, hd, yaw):
    uu = np.array([np.sin(yaw),0,np.cos(yaw)])*hw; vv = np.array([np.cos(yaw),0,-np.sin(yaw)])*hd; a=np.array([0,hh,0])
    b=[c-uu-vv-a,c+uu-vv-a,c+uu+vv-a,c-uu+vv-a]; return np.array(b+[q+2*a for q in b])
corners = build_corners(center, hw, hh, hd, yaw)
room = np.array([P[opq,0].mean(),P[opq,1].mean(),P[opq,2].mean()])
front_sign = float(np.sign(np.dot(room-center, w)) or 1.0)
box = dict(method="carve", prompt=PROMPTS, corners=corners.tolist(),
           edges=[[0,1],[1,2],[2,3],[3,0],[4,5],[5,6],[6,7],[7,4],[0,4],[1,5],[2,6],[3,7]],
           center=[float(x) for x in center], half=[float(hw),float(hh),float(hd)],
           size=[float(2*hw),float(2*hh),float(2*hd)], yaw=yaw, front_sign=front_sign,
           n_obj=int(obj.sum()), n_det_views=len(dets))
json.dump(box, open(OUTBOX, "w"), indent=1)
print(f"CARVED OBB: WxHxD = {2*hw:.2f} x {2*hh:.2f} x {2*hd:.2f} m  "
      f"center=({center[0]:.2f},{center[1]:.2f},{center[2]:.2f}) yaw={np.degrees(yaw):.0f} -> {OUTBOX}")
