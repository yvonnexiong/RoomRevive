"""Real BoxerNet inference on the splat's rendered views.

Pipeline (matches facebookresearch/boxer):
  rendered posed view -> OWLv2 ('refrigerator') 2D box -> BoxerNet 3D lift.
Coordinate handling:
  - OpenGL camera (my renderer) -> CV camera (Boxer): R_cv = R_gl @ diag(1,-1,-1)
  - splat world (+Y up) -> Boxer world (+Z up, gravity=-Z): Mrot = Rx(90 deg)
  - splat centers are fed as the semi-dense point cloud (sdp_w)
  - output OBBs are rotated back to splat world for saving/visualization.
"""
import truststore; truststore.inject_into_ssl()
import os, sys, json, time
import numpy as np
import torch
from PIL import Image

BOXER = os.path.join(os.path.dirname(os.path.abspath(__file__)), "boxer_repo")
sys.path.insert(0, BOXER)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from boxernet.boxernet import BoxerNet
from owl.owl_wrapper import OwlWrapper
from utils.tw.camera import CameraTW
from utils.tw.pose import PoseTW
from spz_io import parse_spz

PROMPTS = os.environ.get("PROMPT", "a refrigerator,a fridge,a stainless steel refrigerator").split(",")
THRESH2D = float(os.environ.get("T2D", 0.15))
THRESH3D = float(os.environ.get("T3D", 0.1))
N_SDP = int(os.environ.get("NSDP", 60000))
HW = 960
device = "cpu"
torch.set_num_threads(os.cpu_count())

Mrot = np.array([[1, 0, 0], [0, 0, -1], [0, 1, 0]], float)  # Rx(90): splat(+Y up)->boxer(+Z up)
GL2CV = np.diag([1.0, -1.0, -1.0])

# CAMERAS/FRAMES may be comma-separated parallel lists to combine multiple passes
OUTBOX = os.environ.get("OUTBOX", "box.json")
cam_files = os.environ.get("CAMERAS", "cameras.json").split(",")
frame_dirs = os.environ.get("FRAMES", "frames").split(",")
cams = []
for cf, fd in zip(cam_files, frame_dirs):
    for c in json.load(open(cf))["cams"]:
        c["_frames"] = fd
        cams.append(c)
data = json.load(open(cam_files[0]))
s = parse_spz(data["spz"])
P = s["positions"]; op = s["alphas"] / 255.0
pts = P[op > 0.5]
rng = np.random.default_rng(0)
sel = rng.choice(len(pts), min(N_SDP, len(pts)), replace=False)
sdp_boxer = (Mrot @ pts[sel].T).T.astype(np.float32)
sdp_w = torch.from_numpy(sdp_boxer)  # (N,3) in boxer world
print(f"sdp points: {sdp_w.shape[0]}")

print("loading OWLv2 ...")
owl = OwlWrapper(device, text_prompts=PROMPTS, min_confidence=THRESH2D,
                 precision="float32", warmup=False)
print("loading BoxerNet ...")
ckpt = os.path.join(BOXER, "ckpts", "boxernet_hw960in4x6d768-3e37cfc4.ckpt")
net = BoxerNet.load_from_checkpoint(ckpt, device=device)
print("boxernet hw =", net.hw)

f = (HW / 2) / np.tan(np.deg2rad(cams[0]["fov_y_deg"]) / 2)
cam0 = CameraTW.from_surreal(
    width=torch.tensor([float(HW)]), height=torch.tensor([float(HW)]),
    type_str="Pinhole", params=torch.tensor([f, f, HW / 2.0, HW / 2.0]))

results = []  # list of dicts: name, prob, corners_splat (8,3), label
t0 = time.time()
for ci, cam in enumerate(cams):
    img = Image.open(os.path.join(cam["_frames"], cam["name"] + ".png")).convert("RGB").resize((HW, HW))
    img_t = torch.from_numpy(np.asarray(img).astype(np.float32) / 255.0).permute(2, 0, 1).unsqueeze(0)
    bb2d, scores2d, labels2d, _ = owl.forward(img_t * 255.0, False, resize_to_HW=(HW, HW))
    if bb2d.shape[0] == 0:
        print(f"[{ci+1}/{len(cams)}] {cam['name']}: no 2D"); continue

    view = np.array(cam["view"]).reshape(4, 4)
    eye = np.array(cam["eye"])
    Rwc_gl = view[:3, :3].T                 # cam_gl -> world (splat)
    Rwc_cv = Rwc_gl @ GL2CV
    Rwc_b = Mrot @ Rwc_cv                    # boxer world
    t_b = Mrot @ eye
    T_wr = PoseTW.from_Rt(torch.tensor(Rwc_b, dtype=torch.float32).unsqueeze(0),
                          torch.tensor(t_b, dtype=torch.float32).unsqueeze(0))
    datum = {"img0": img_t, "cam0": cam0, "T_world_rig0": T_wr,
             "rotated0": torch.tensor([False]), "sdp_w": sdp_w, "bb2d": bb2d}
    out = net.forward(datum)
    obb = out["obbs_pr_w"].cpu()[0]
    prob = obb.prob.squeeze(-1)
    corners_b = obb.bb3corners_world.numpy()          # (M,8,3) boxer world
    sizes = (obb.bb3_max_object - obb.bb3_min_object).numpy()  # (M,3) h,w,d(vertical)
    best = []
    for m in range(len(prob)):
        if float(prob[m]) < THRESH3D:
            continue
        cs = (Mrot.T @ corners_b[m].T).T            # back to splat world
        lab = PROMPTS[int(labels2d[m])] if m < len(labels2d) else "?"
        best.append((float(prob[m]), lab, cs.tolist(), [float(v) for v in sizes[m]]))
    best.sort(key=lambda x: -x[0])
    for pr, lab, cs, sz in best:
        results.append(dict(name=cam["name"], prob=pr, label=lab, corners=cs, size=sz))
    pp = ",".join(f"{b[0]:.2f}" for b in best) or "-"
    print(f"[{ci+1}/{len(cams)}] {cam['name']}: 2D={bb2d.shape[0]} 3D>{THRESH3D}: {pp}  ({time.time()-t0:.0f}s)")

json.dump(results, open("boxer_raw.json", "w"))
print(f"\n{len(results)} raw 3D boxes across frames -> boxer_raw.json")

EDGES = [[0,1],[1,2],[2,3],[3,0],[4,5],[5,6],[6,7],[7,4],[0,4],[1,5],[2,6],[3,7]]

def obb_params(corners):
    """Gravity-aligned OBB params from 8 world corners (y vertical). Returns
    center, W(long horiz), H(vertical), D(short horiz), yaw(long-axis angle)."""
    c = corners.mean(0)
    H = corners[:, 1].max() - corners[:, 1].min()
    bottom = corners[np.argsort(corners[:, 1])[:4]]
    b0 = bottom[0]
    nn = np.argsort(np.linalg.norm(bottom - b0, axis=1))
    e1 = bottom[nn[1]] - b0; e2 = bottom[nn[2]] - b0
    L1, L2 = np.linalg.norm(e1), np.linalg.norm(e2)
    longv, W, D = (e1, L1, L2) if L1 >= L2 else (e2, L2, L1)
    yaw = float(np.arctan2(longv[0], longv[2]))
    return c, float(W), float(H), float(D), yaw

def build_corners(center, W, H, D, yaw):
    u = np.array([np.sin(yaw), 0, np.cos(yaw)]) * (W / 2)   # long horizontal
    v = np.array([np.cos(yaw), 0, -np.sin(yaw)]) * (D / 2)  # short horizontal
    a = np.array([0, H / 2, 0])                             # vertical
    b = [center - u - v - a, center + u - v - a, center + u + v - a, center - u + v - a]
    return np.array(b + [p + 2 * a for p in b])

if not results:
    print("no boxes; nothing to fuse"); sys.exit(0)

# optional region filter: keep only detections near REGION_C (x,y,z) within REGION_R
if os.environ.get("REGION_C"):
    rc = np.array([float(v) for v in os.environ["REGION_C"].split(",")])
    rr = float(os.environ.get("REGION_R", 0.9))
    results = [r for r in results if np.linalg.norm(np.mean(r["corners"], axis=0) - rc) < rr]
    print(f"region filter near {rc.tolist()} r={rr}: {len(results)} detections kept")
    if not results:
        print("no detections in region"); sys.exit(0)

# cluster per-frame boxes by center proximity
cent = np.array([np.mean(r["corners"], axis=0) for r in results])
used = np.zeros(len(results), bool); clusters = []
for i in range(len(results)):
    if used[i]:
        continue
    grp = [i]; used[i] = True
    for j in range(i + 1, len(results)):
        if not used[j] and np.linalg.norm(cent[j] - cent[i]) < 0.6:
            grp.append(j); used[j] = True
    clusters.append(grp)
def cinfo(g):
    views = {results[k]["name"] for k in g}
    probs = [results[k]["prob"] for k in g]
    ctr = np.mean([np.mean(results[k]["corners"], axis=0) for k in g], axis=0)
    return len(views), max(probs), sum(probs) / len(probs), ctr

# show every candidate location found across the room, ranked by best detection score
print("candidate clusters (location  views  maxprob  meanprob):")
for g in sorted(clusters, key=lambda g: -cinfo(g)[1]):
    nv, mx, mn, ctr = cinfo(g)
    print(f"  {ctr.round(2).tolist()}  views={nv}  max={mx:.2f}  mean={mn:.2f}")

SEL = os.environ.get("SELECT", "support")
if SEL == "maxprob":   # the candidate with the single highest detection score
    best = max(clusters, key=lambda g: (cinfo(g)[1], cinfo(g)[0]))
else:                  # most distinct views, then summed prob
    clusters.sort(key=lambda g: (len({results[k]["name"] for k in g}),
                                 sum(results[k]["prob"] for k in g)), reverse=True)
    best = clusters[0]
print(f"selected ({SEL}): center {cinfo(best)[3].round(2).tolist()} views={cinfo(best)[0]} max={cinfo(best)[1]:.2f}")

# confidence-weighted fusion of all observations in the cluster
ps = np.array([results[k]["prob"] for k in best])
prm = [obb_params(np.array(results[k]["corners"])) for k in best]
Wp = ps / ps.sum()
center = sum(Wp[i] * prm[i][0] for i in range(len(best)))
Wd = float(sum(Wp[i] * prm[i][1] for i in range(len(best))))
Hd = float(sum(Wp[i] * prm[i][2] for i in range(len(best))))
Dd = float(sum(Wp[i] * prm[i][3] for i in range(len(best))))
yaws = np.array([prm[i][4] for i in range(len(best))])
yaw = 0.5 * float(np.arctan2((Wp * np.sin(2 * yaws)).sum(), (Wp * np.cos(2 * yaws)).sum()))
corners = build_corners(center, Wd, Hd, Dd, yaw)

# spread = fusion quality metric
cstd = np.std(np.array([prm[i][0] for i in range(len(best))]), axis=0)
sstd = np.std([[prm[i][1], prm[i][2], prm[i][3]] for i in range(len(best))], axis=0)
nviews = len({results[k]["name"] for k in best})
box = dict(method="boxernet+wfuse", corners=corners.tolist(), edges=EDGES,
           center=[float(x) for x in center], size=[Wd, Hd, Dd],
           half=[Wd / 2, Hd / 2, Dd / 2], yaw=yaw,
           prob=float(ps.mean()), prob_max=float(ps.max()),
           n_obs=len(best), n_views=int(nviews),
           center_std=[float(x) for x in cstd], size_std=[float(x) for x in sstd],
           views=sorted({results[k]["name"] for k in best}))
json.dump(box, open(OUTBOX, "w"), indent=1)
print(f"FUSED ({len(best)} obs / {nviews} views): WxHxD = {Wd:.2f} x {Hd:.2f} x {Dd:.2f} m  "
      f"center=({center[0]:.2f},{center[1]:.2f},{center[2]:.2f})  "
      f"conf mean={ps.mean():.2f} max={ps.max():.2f}\n"
      f"  spread: center std={cstd.round(3).tolist()} m, size std={sstd.round(3).tolist()} m -> {OUTBOX}")
