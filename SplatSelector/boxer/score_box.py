"""Quality metric for a detected 3D box -> track improvement over time.

Primary score = mean 2D IoU between the projected 3D box (its 2D bounding rectangle)
and the OWLv2 2D detection box, over all detecting views. A box that hugs the object
projects onto the detector's box from every angle -> IoU -> 1.

Usage:  python score_box.py <label> [BOX=box.json]
Appends a row to scores.csv so successive iterations are comparable.
"""
import json, os, sys, glob, csv, time
import numpy as np

label = sys.argv[1] if len(sys.argv) > 1 else "run"
box = json.load(open(os.environ.get("BOX", "box.json")))
C = np.array(box["corners"])

# gather cameras (any cameras_*.json referenced by the cached detections)
cam_by_name = {}
for cf in glob.glob("cameras*.json"):
    for c in json.load(open(cf))["cams"]:
        cam_by_name[c["name"]] = c

# use the most recent detection cache
caches = sorted(glob.glob("dets_cache_*.json"), key=os.path.getmtime)
if not caches:
    print("no dets_cache_*.json — run carve_fit.py first"); sys.exit(1)
byname = json.load(open(caches[-1]))

def iou(a, b):
    ix0, iy0 = max(a[0], b[0]), max(a[1], b[1])
    ix1, iy1 = min(a[2], b[2]), min(a[3], b[3])
    iw, ih = max(0, ix1 - ix0), max(0, iy1 - iy0)
    inter = iw * ih
    ua = (a[2]-a[0])*(a[3]-a[1]) + (b[2]-b[0])*(b[3]-b[1]) - inter
    return inter / ua if ua > 0 else 0.0

ious = []
for name, (dbox, score) in byname.items():
    cam = cam_by_name.get(name)
    if cam is None:
        continue
    view = np.array(cam["view"]).reshape(4, 4); f = cam["focal"]; W = cam["W"]; H = cam["H"]
    ph = np.concatenate([C, np.ones((8, 1))], 1); cm = ph @ view.T; z = cm[:, 2]
    if (z > -0.02).all():
        continue
    px = W/2 + f*cm[:, 0]/np.where(z != 0, -z, 1e9)
    py = H/2 - f*cm[:, 1]/np.where(z != 0, -z, 1e9)
    proj = [px.min(), py.min(), px.max(), py.max()]
    ious.append(iou(proj, dbox))

ious = np.array(ious)
mean_iou = float(ious.mean()) if len(ious) else 0.0
med_iou = float(np.median(ious)) if len(ious) else 0.0
sz = box["size"]

# ---- geometry tightness (NOT capped by loose 2D boxes) ----
# fill = how much of the box volume the opaque splats inside it actually span.
# A hugging box -> inside points fill it -> fill~1; a loose box -> fill<1.
fill = float("nan")
try:
    from spz_io import parse_spz
    cams_any = json.load(open(sorted(glob.glob("cameras*.json"))[0]))
    s = parse_spz(cams_any["spz"]); P = s["positions"]; opq = s["alphas"] / 255.0 > 0.5
    c = np.array(box["center"]); hw, hh, hd = box["half"]; yaw = box["yaw"]
    u = np.array([np.sin(yaw), 0, np.cos(yaw)]); w = np.array([np.cos(yaw), 0, -np.sin(yaw)])
    d = P[opq] - c; du = d @ u; dw = d @ w; dy = d[:, 1]
    ins = (np.abs(du) <= hw) & (np.abs(dw) <= hd) & (np.abs(dy) <= hh)
    if ins.sum() > 50:
        ext = (np.ptp(du[ins]) * np.ptp(dy[ins]) * np.ptp(dw[ins]))
        fill = float(ext / (8 * hw * hh * hd))
except Exception as e:
    print("fill calc skipped:", e)

print(f"[{label}] mean2D_IoU={mean_iou:.3f}  median={med_iou:.3f}  views={len(ious)}  "
      f"fill={fill:.3f}  size={[round(x,2) for x in sz]}  method={box.get('method','?')}")

new = not os.path.exists("scores.csv")
with open("scores.csv", "a", newline="") as fh:
    w = csv.writer(fh)
    if new:
        w.writerow(["time", "label", "method", "mean_iou", "median_iou", "fill", "views", "W", "H", "D"])
    w.writerow([time.strftime("%Y-%m-%d %H:%M"), label, box.get("method", "?"),
                f"{mean_iou:.3f}", f"{med_iou:.3f}", f"{fill:.3f}", len(ious), *[f"{x:.2f}" for x in sz]])
