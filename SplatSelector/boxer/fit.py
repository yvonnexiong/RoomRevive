"""Multi-view vote + gravity-aligned oriented box fit.

For every OWLv2 detection, project the splat centers into that view, keep the
visible front-surface splats inside the 2D box, and accumulate votes (weighted by
detection score). Cluster the voted splats in 3D, pick the cluster supported by
the most distinct views, and fit a gravity-aligned (yaw-only) oriented box.
"""
import json, os
import numpy as np
from spz_io import parse_spz

FRONT_BAND = 0.5     # m: keep splats within this depth of the box's near surface
CELL = 0.18          # m: clustering grid cell
MIN_SUPPORT = int(os.environ.get("MINSUP", 2))   # splat must fall in a detection box in >= this many distinct views

data = json.load(open("cameras.json"))
cams = {c["name"]: c for c in data["cams"]}
dets = json.load(open("detections.json"))
s = parse_spz(data["spz"])
P = s["positions"]; op = s["alphas"] / 255.0
solid = op > 0.5
sidx = np.where(solid)[0]
pts = P[sidx]                          # (M,3) candidate geometry
M = pts.shape[0]

votes = np.zeros(M)
support = [set() for _ in range(M)]    # distinct views supporting each splat
det_list = [(d["name"], det) for d in dets for det in d["dets"]]
print(f"{len(det_list)} detections across {sum(1 for d in dets if d['dets'])} views")

for vi, (name, det) in enumerate(det_list):
    cam = cams[name]
    view = np.array(cam["view"]).reshape(4, 4)
    f = cam["focal"]; W = cam["W"]; H = cam["H"]
    x0, y0, x1, y1 = det["box"]
    ph = np.concatenate([pts, np.ones((M, 1))], 1)
    camsp = ph @ view.T
    z = camsp[:, 2]; depth = -z
    px = W / 2 + f * camsp[:, 0] / np.maximum(-z, 1e-6)
    py = H / 2 - f * camsp[:, 1] / np.maximum(-z, 1e-6)
    inside = (depth > 0.2) & (px >= x0) & (px <= x1) & (py >= y0) & (py <= y1)
    if inside.sum() < 30:
        continue
    dmin = np.percentile(depth[inside], 4)         # near surface in the box
    vis = inside & (depth < dmin + FRONT_BAND)
    w = det["score"]
    votes[vis] += w
    for i in np.where(vis)[0]:
        support[i].add(name)

supp_count = np.array([len(s) for s in support])
hist = {k: int((supp_count == k).sum()) for k in range(1, supp_count.max() + 1)}
print(f"support-count histogram (views per splat): {hist}")
cand = supp_count >= MIN_SUPPORT
print(f"voted splats (>= {MIN_SUPPORT} views): {cand.sum()}")

# 3D connected-component clustering on a grid over candidate splats
ci = np.floor(pts[cand] / CELL).astype(np.int64)
cand_idx = np.where(cand)[0]
cellmap = {}
for k, c in enumerate(map(tuple, ci)):
    cellmap.setdefault(c, []).append(k)        # k indexes into cand_idx
seen = set(); clusters = []
for c in cellmap:
    if c in seen:
        continue
    stack = [c]; comp = []
    while stack:
        cc = stack.pop()
        if cc in seen or cc not in cellmap:
            continue
        seen.add(cc); comp.extend(cellmap[cc])
        x, y, z = cc
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    nb = (x + dx, y + dy, z + dz)
                    if nb in cellmap and nb not in seen:
                        stack.append(nb)
    clusters.append(comp)

def cluster_score(comp):
    gi = cand_idx[comp]
    views = set().union(*[support[i] for i in gi])
    return (len(views), votes[gi].sum(), len(gi)), views

clusters.sort(key=lambda c: cluster_score(c)[0], reverse=True)
best = clusters[0]
(nview, vsum, sz), views = cluster_score(best)
print(f"best cluster: {sz} splats, {nview} distinct views {sorted(views)}, votescore {vsum:.2f}")

gi = cand_idx[best]
fp = pts[gi]                               # fridge splats (subset of opaque)

# gravity-aligned OBB: yaw from PCA of XZ, full extent in Y
xz = fp[:, [0, 2]]
xz0 = xz - xz.mean(0)
cov = xz0.T @ xz0 / len(xz0)
evals, evecs = np.linalg.eigh(cov)
axis = evecs[:, np.argmax(evals)]          # dominant horizontal direction
yaw = np.arctan2(axis[1], axis[0])
ca, sa = np.cos(-yaw), np.sin(-yaw)
xr = ca * xz[:, 0] - sa * xz[:, 1]
zr = sa * xz[:, 0] + ca * xz[:, 1]
y = fp[:, 1]

def rng(a):
    return np.percentile(a, 1.5), np.percentile(a, 98.5)
xlo, xhi = rng(xr); zlo, zhi = rng(zr); ylo, yhi = rng(y)
cxr, czr = (xlo + xhi) / 2, (zlo + zhi) / 2
# rotate center back to world XZ
cw, sw = np.cos(yaw), np.sin(yaw)
cx = cw * cxr - sw * czr
cz = sw * cxr + cw * czr
cy = (ylo + yhi) / 2
ex, ey, ez = (xhi - xlo) / 2, (yhi - ylo) / 2, (zhi - zlo) / 2

# 8 corners
corners = []
for sx in (-1, 1):
    for sy in (-1, 1):
        for sz in (-1, 1):
            lx, lz = sx * ex, sz * ez
            wx = cw * lx - sw * lz + cx
            wz = sw * lx + cw * lz + cz
            corners.append([wx, cy + sy * ey, wz])
corners = np.array(corners)

box = dict(center=[float(cx), float(cy), float(cz)],
           half=[float(ex), float(ey), float(ez)],
           yaw=float(yaw), corners=corners.tolist(),
           size=[float(2 * ex), float(2 * ey), float(2 * ez)],
           n_splats=int(len(gi)), n_views=int(nview), views=sorted(views))
json.dump(box, open("box.json", "w"), indent=1)
print(f"\nOBB center=({cx:.2f},{cy:.2f},{cz:.2f})  "
      f"size W×H×D = {2*ex:.2f} × {2*ey:.2f} × {2*ez:.2f} m  yaw={np.degrees(yaw):.0f}deg")
print("saved box.json")
