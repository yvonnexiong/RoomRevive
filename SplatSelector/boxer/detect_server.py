"""Warm detection backend — supports OWLv2, Grounding DINO, and YOLO World.

The viewer POSTs {anchor, nodes, fov, exclude, detector} to /detect.
Each backend is lazy-loaded on first use. Returns {shots, box}.
"""
import truststore; truststore.inject_into_ssl()
import os, json, time, io, base64
import numpy as np
import torch
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlparse, parse_qs
from PIL import Image, ImageDraw
from spz_io import parse_spz, parse_ply
from render import render_ewa, subset_scene, look_at

PORT   = 8777
# load .env for API keys
_env_path = os.path.join(os.path.dirname(__file__), ".env")
if os.path.exists(_env_path):
    for _line in open(_env_path):
        _line = _line.strip()
        if _line and not _line.startswith("#") and "=" in _line:
            _k, _v = _line.split("=", 1); os.environ.setdefault(_k.strip(), _v.strip())
SERVED = r"C:\Unity-Git\RoomRevive\HTML_Editor\detected_boxes.json"
T2D    = 0.02
RATIO  = 0.40
PROMPTS = ["a refrigerator", "an oven", "a microwave", "a dishwasher",
           "a washing machine", "a sink", "a kitchen cabinet", "a kitchen appliance", "a freezer"]
NAME    = ["refrigerator","oven","microwave","dishwasher","washing machine","sink","cabinet","appliance","freezer"]
COLOR_MAP = {"refrigerator":[0.36,1.0,0.54],"fridge":[0.36,1.0,0.54],
             "oven":[0.32,0.78,1.0],"microwave":[1.0,0.62,0.2],
             "dishwasher":[0.95,0.45,0.85],"washing":[0.85,0.45,0.95],
             "sink":[0.4,0.85,1.0],"cabinet":[0.85,0.8,0.4],
             "freezer":[0.5,0.85,1.0],"cooktop":[1.0,0.4,0.4],"stovetop":[1.0,0.4,0.4]}
def label_color(cls):
    cls = cls.lower()
    for k,v in COLOR_MAP.items():
        if k in cls: return v
    return [1.0, 0.85, 0.1]   # yellow default
YOLO_CLASSES = ["refrigerator","oven","microwave","dishwasher","washing machine",
                "sink","kitchen cabinet","kitchen appliance","stove","freezer","chest freezer"]

# ── scene ────────────────────────────────────────────────────────────────────
def floater_mask(P, alpha, cell=0.06, maxd=15):
    solid = alpha > 0.2; vox = np.floor(P/cell).astype(np.int64)
    OFF, A, B = 1<<19, 1<<40, 1<<20
    key = (vox[:,0]+OFF)*A+(vox[:,1]+OFF)*B+(vox[:,2]+OFF)
    uk, uc = np.unique(key[solid], return_counts=True); dens = np.zeros(len(P), np.int64)
    for dx in (-1,0,1):
        for dy in (-1,0,1):
            for dz in (-1,0,1):
                nk=(vox[:,0]+dx+OFF)*A+(vox[:,1]+dy+OFF)*B+(vox[:,2]+dz+OFF)
                ix=np.clip(np.searchsorted(uk,nk),0,len(uk)-1); dens+=np.where(uk[ix]==nk,uc[ix],0)
    return dens < maxd

def load_scene(spz_path_or_bytes):
    global SCENE, SP, OPQ, SCENE_IS_DEFAULT
    # cached pre-rendered frames only apply to the default kitchen scene; recognise
    # it whether loaded by path at startup OR re-uploaded by the viewer (match by size)
    if isinstance(spz_path_or_bytes, (bytes, bytearray)):
        SCENE_IS_DEFAULT = (KITCHEN_SPZ_SIZE is not None and len(spz_path_or_bytes) == KITCHEN_SPZ_SIZE)
    else:
        SCENE_IS_DEFAULT = True
    import tempfile
    is_ply = False
    if isinstance(spz_path_or_bytes, (bytes, bytearray)):
        is_ply = spz_path_or_bytes[:3] == b"ply"
        suffix = ".ply" if is_ply else ".spz"
        tmp = tempfile.NamedTemporaryFile(suffix=suffix, delete=False)
        tmp.write(spz_path_or_bytes); tmp.close(); path = tmp.name
    else:
        path = spz_path_or_bytes
        is_ply = path.lower().endswith(".ply")
    fmt = "PLY" if is_ply else "SPZ"
    print(f"loading scene from {os.path.basename(path)} ({fmt})...")
    s = (parse_ply if is_ply else parse_spz)(path); P = s["positions"]; op = s["alphas"]/255.0
    # use crop if available, otherwise full scene
    if os.path.exists("room_crop.json"):
        crop = json.load(open("room_crop.json")); Cv = np.array(crop["center"]); Hh = np.array(crop["size"])/2
        ins = (np.abs(P[:,0]-Cv[0])<=Hh[0])&(np.abs(P[:,1]-Cv[1])<=Hh[1])&(np.abs(P[:,2]-Cv[2])<=Hh[2])
        s = subset_scene(s, ins & ~floater_mask(P, op))
    else:
        s = subset_scene(s, ~floater_mask(P, op))
    SCENE = s; SP = SCENE["positions"]; OPQ = SCENE["alphas"]/255.0 > 0.5
    # floor level = LOWEST significant slab (densest-low picks the countertop, since
    # floors are sparse in splats). Cross-check with the 2nd percentile and take the
    # higher (more conservative) of the two. Room centroid tells which box face is front.
    global FLOOR_Y, ROOM_CTR
    yall = SP[OPQ][:,1]; ymn,ymx = float(yall.min()), float(yall.max())
    hcnt,edg = np.histogram(yall, bins=140, range=(ymn,ymx))
    above = np.where(hcnt >= 0.10*hcnt.max())[0]
    slab = float(edg[above[0]]+(edg[1]-edg[0])/2) if len(above) else ymn
    FLOOR_Y = max(slab, float(np.percentile(yall, 2)))
    ROOM_CTR = SP[OPQ].mean(0)
    print(f"scene ready: {SCENE['n']:,} splats  floor={FLOOR_Y:.2f}")
    if isinstance(spz_path_or_bytes, (bytes, bytearray)):
        os.unlink(path)

_kitchen_spz = json.load(open("cameras.json"))["spz"]
KITCHEN_SPZ_SIZE = os.path.getsize(_kitchen_spz) if os.path.exists(_kitchen_spz) else None
load_scene(_kitchen_spz)

# ── lazy model registry ───────────────────────────────────────────────────────
_models = {}

def get_owlv2():
    if "owlv2" not in _models:
        print("loading OWLv2...")
        from transformers import Owlv2Processor, Owlv2ForObjectDetection
        p = Owlv2Processor.from_pretrained("google/owlv2-base-patch16-ensemble")
        m = Owlv2ForObjectDetection.from_pretrained("google/owlv2-base-patch16-ensemble").eval()
        torch.set_num_threads(os.cpu_count())
        _models["owlv2"] = (p, m)
        print("OWLv2 ready")
    return _models["owlv2"]

def get_gdino():
    if "gdino" not in _models:
        print("loading Grounding DINO...")
        from transformers import AutoProcessor, AutoModelForZeroShotObjectDetection
        p = AutoProcessor.from_pretrained("IDEA-Research/grounding-dino-base")
        m = AutoModelForZeroShotObjectDetection.from_pretrained("IDEA-Research/grounding-dino-base").eval()
        _models["gdino"] = (p, m)
        print("Grounding DINO ready")
    return _models["gdino"]

def get_yoloworld():
    if "yoloworld" not in _models:
        print("loading YOLO World...")
        from ultralytics import YOLO
        m = YOLO("yolov8s-worldv2.pt")
        m.set_classes(YOLO_CLASSES)
        _models["yoloworld"] = m
        print("YOLO World ready")
    return _models["yoloworld"]

# ── per-backend detection: returns list of (score, box[x0,y0,x1,y1]) for ONLY_LABEL ──
def detect_owlv2(pic, W, H):
    proc, model = get_owlv2()
    inp = proc(text=[PROMPTS], images=pic, return_tensors="pt")
    with torch.no_grad(): out = model(**inp)
    r = proc.post_process_object_detection(out, threshold=T2D, target_sizes=torch.tensor([[H,W]]))[0]
    hits = []
    for sc, lb, bx in zip(r["scores"].tolist(), r["labels"].tolist(), r["boxes"].tolist()):
        hits.append((float(sc), [float(v) for v in bx], NAME[lb]))
    return hits

def detect_gdino(pic, W, H):
    proc, model = get_gdino()
    text = ". ".join(NAME) + "."   # all classes as grounding text
    inp = proc(images=pic, text=text, return_tensors="pt")
    with torch.no_grad(): out = model(**inp)
    r = proc.post_process_grounded_object_detection(out, inp["input_ids"],
        box_threshold=T2D, text_threshold=T2D, target_sizes=[(H, W)])[0]
    hits = []
    for sc, bx, lbl in zip(r["scores"].tolist(), r["boxes"].tolist(), r["labels"]):
        hits.append((float(sc), [float(v) for v in bx], str(lbl)))
    return hits

def detect_yoloworld(pic, W, H):
    model = get_yoloworld()
    results = model.predict(pic, conf=T2D, verbose=False)
    hits = []
    for box in results[0].boxes:
        sc = float(box.conf); x1,y1,x2,y2 = box.xyxy[0].tolist()
        cls = YOLO_CLASSES[int(box.cls)] if int(box.cls) < len(YOLO_CLASSES) else "object"
        hits.append((sc, [x1,y1,x2,y2], cls))
    return hits

def detect_roboflow(pic, W, H):
    import requests, base64, io as _io
    api_key = os.environ.get("ROBOFLOW_API_KEY", "")
    if not api_key:
        print("ROBOFLOW_API_KEY not set in .env"); return []
    buf = _io.BytesIO(); pic.save(buf, "JPEG", quality=90)
    b64 = base64.b64encode(buf.getvalue()).decode()
    # POST base64 image to Roboflow hosted inference (no SDK needed, any Python version)
    url = f"https://serverless.roboflow.com/visionaid-kitchen/1?api_key={api_key}&confidence=5&overlap=30"
    resp = requests.post(url, data=b64, headers={"Content-Type": "application/x-www-form-urlencoded"}, timeout=30)
    resp.raise_for_status()
    hits = []
    for pred in resp.json().get("predictions", []):
        sc = pred["confidence"]
        x, y, w, h = pred["x"], pred["y"], pred["width"], pred["height"]
        hits.append((float(sc), [x-w/2, y-h/2, x+w/2, y+h/2], pred.get("class", "object")))
    return hits

BACKENDS = {"owlv2": detect_owlv2, "gdino": detect_gdino, "yoloworld": detect_yoloworld, "roboflow": detect_roboflow}
BACKEND_LABEL = {"owlv2": "OWLv2", "gdino": "Grounding DINO", "yoloworld": "YOLO World", "roboflow": "Roboflow Kitchen"}

# ── focused detector used by auto_detect (faster than full PROMPTS).
# Synonym prompts collapse to one class name via AUTO_NAMES (same index order). ──
AUTO_PROMPTS = ["a refrigerator", "an oven", "a microwave"]
AUTO_NAMES   = ["refrigerator", "oven", "microwave"]

def detect_owlv2_auto(pic, W, H):
    proc, model = get_owlv2()
    inp = proc(text=[AUTO_PROMPTS], images=pic, return_tensors="pt")
    with torch.no_grad(): out = model(**inp)
    r = proc.post_process_object_detection(out, threshold=T2D, target_sizes=torch.tensor([[H,W]]))[0]
    hits = []
    for sc, lb, bx in zip(r["scores"].tolist(), r["labels"].tolist(), r["boxes"].tolist()):
        if lb < len(AUTO_NAMES):
            hits.append((float(sc), [float(v) for v in bx], AUTO_NAMES[lb]))
    return hits

# ── OWLv2-only: preload OWLv2 so first /auto-detect is fast ──────────────────
get_owlv2()
print(f"ready (OWLv2). listening on :{PORT}")

# ── main detect function ──────────────────────────────────────────────────────
def detect(anchor, nodes, fovdeg, exclude, detector="owlv2"):
    # K/L capture: render the user's L-node views, then run the SAME pipeline as the
    # room auto-detect (OWLv2 per-class → carve → density-width/percentile-depth → ray-shrink).
    W = H = 560
    pts = SP[OPQ]; M = len(pts)
    class_dets = {}; shots = []          # cls → [(view, f, W, H, box, score)]
    for node in nodes:
        img, view, f = render_ewa(SCENE, node, anchor, W=W, H=H, fov_y_deg=fovdeg,
                                  min_alpha=0.2, near_cull=0.12, exposure=1.5, scale_mult=1.3)
        pic = Image.fromarray((np.clip(img,0,1)**0.9*255).astype(np.uint8))
        hits = detect_owlv2_auto(pic, W, H)      # OWLv2, fridge/oven/microwave
        best = {}
        for sc, box, cls in hits:
            if cls not in best or sc > best[cls][0]: best[cls] = (sc, box)
        for cls,(sc,box) in best.items():
            class_dets.setdefault(cls,[]).append((view, f, W, H, box, sc))
        dr = ImageDraw.Draw(pic)
        if best:
            for cls,(sc,(x0,y0,x1,y1)) in best.items():
                dr.rectangle([x0,y0,x1,y1], outline=(255,90,220), width=3)
                t=f"{cls} {sc:.2f}"; dr.rectangle([x0,y0-14,x0+len(t)*7,y0],fill=(0,0,0)); dr.text((x0+2,y0-13),t,fill=(255,90,220))
        else:
            dr.rectangle([0,0,130,16], fill=(0,0,0)); dr.text((3,2), "no object", fill=(255,170,170))
        buf = io.BytesIO(); pic.save(buf, "JPEG", quality=72)
        shots.append("data:image/jpeg;base64," + base64.b64encode(buf.getvalue()).decode())
    if not class_dets: return {"shots": shots, "box": None}
    # carve only splats near the K anchor so the box locks onto the pointed-at object
    nearm = np.linalg.norm(pts-np.array(anchor),axis=1) < 1.8
    pts_n = pts[nearm]
    if len(pts_n) < 40: return {"shots": shots, "box": None}
    ph_n = np.concatenate([pts_n, np.ones((len(pts_n),1))], 1)
    # pick the class detected with highest confidence at this anchor
    best_cls = max(class_dets, key=lambda c: max(d[5] for d in class_dets[c]))
    if len(class_dets[best_cls]) < 2: return {"shots": shots, "box": None}
    box = _carve_fit(best_cls, class_dets[best_cls], pts_n, ph_n, idp="L", tag="L-OWLv2")
    return {"shots": shots, "box": box}

# ── per-face ray-shrink: pull each supported box face in to the visible product ─
def _ray_shrink(ctr, yaw, hu, hw, hy, objp, res=240, fov=55.0):
    """Shrink-wrap the box onto the detector-carved splats. From the 8 box-corner
    angles, z-buffer the carved splats to get the NEAREST (visible) surface, then
    pull each supported face in to that surface (wall/cabinet behind is occluded).
    Returns refined (ctr, hu, hw, hy). Faces with little support are left as-is."""
    u = np.array([np.sin(yaw),0,np.cos(yaw)]); w = np.array([np.cos(yaw),0,-np.sin(yaw)]); Y = np.array([0,1.0,0])
    M = len(objp); tan = np.tan(np.deg2rad(fov)/2)
    dist = max(hu,hw,hy)/tan/0.5 + max(hu,hw,hy)
    ph = np.concatenate([objp, np.ones((M,1))], 1)
    visible = np.zeros(M, bool)
    for sx in (-1,1):
        for sz in (-1,1):
            for sy in (-1,1):
                cdir = sx*u*hu + sz*w*hw + sy*Y*hy; nrm = np.linalg.norm(cdir)
                if nrm < 1e-6: continue
                eye = ctr + cdir/nrm*dist; view = look_at(eye, ctr, (0,1,0)); f = (res/2)/tan
                cm = ph @ view.T; z = cm[:,2]; depth = -z
                px = res/2 + f*cm[:,0]/np.where(z!=0,-z,1e9); py = res/2 - f*cm[:,1]/np.where(z!=0,-z,1e9)
                ok = (depth>0.05)&(px>=0)&(px<res)&(py>=0)&(py<res)
                if not ok.any(): continue
                pix = py[ok].astype(np.int64)*res + px[ok].astype(np.int64)
                gi = np.where(ok)[0]; order = np.argsort(-depth[ok])     # far first; nearest wins
                buf = np.full(res*res, -1, np.int64); buf[pix[order]] = gi[order]
                visible[np.unique(buf[buf>=0])] = True
    vis = objp[visible]
    if len(vis) < 30: return ctr, hu, hw, hy        # not enough surface, keep fit
    minsup = max(20, int(0.03*len(vis)))
    d = vis - ctr; du = d@u; dw = d@w; dy = d[:,1]
    def face(coord, half):
        lo, hi = -half, half
        if np.sum(coord >  0.5*half) >= minsup: hi = min(half, np.percentile(coord,98))
        if np.sum(coord < -0.5*half) >= minsup: lo = max(-half, np.percentile(coord,2))
        return lo, hi
    uL,uH = face(du,hu); wL,wH = face(dw,hw); yL,yH = face(dy,hy)
    ctr2 = ctr + u*((uL+uH)/2) + w*((wL+wH)/2) + np.array([0,(yL+yH)/2,0])
    return ctr2, (uH-uL)/2, (wH-wL)/2, (yH-yL)/2

# ── carve + fit a gravity-aligned OBB from multi-view 2D detections ───────────
CARVE_MINC = 0.10   # absolute min detection confidence to use a view in the carve
CARVE_FRAC = 0.0    # use all views above CARVE_MINC (more angles → tighter carve)
CARVE_RATIO = 0.65  # splat must fall inside the 2D box in >= this fraction of seeing views
CARVE_TRIM = 1.3    # drop carved splats farther than this (m) from the cluster median

def _carve_fit(cls, dets, pts, ph, idp="auto", tag="OWLv2"):
    """dets: list of (view4x4, f, W, H, box, score). Returns a box dict or None.
    idp is the id prefix ('auto' for room scan, 'L' for K/L capture so they persist)."""
    M = len(pts)
    # drop stray low-confidence detections (false positives in bad views bloat the box)
    best = max(d[5] for d in dets); thr = max(CARVE_MINC, CARVE_FRAC*best)
    dets = [d for d in dets if d[5] >= thr]
    if len(dets) < 2:
        print(f"  {cls}: <2 views above conf {thr:.2f}, skip"); return None
    seen = np.zeros(M, np.int32); vote = np.zeros(M, np.int32)
    for view, f, W, H, box, sc in dets:
        cm = ph @ view.T; z = cm[:,2]; front = -z > 0.05
        px = W/2 + f*cm[:,0]/np.where(z!=0,-z,1e9); py = H/2 - f*cm[:,1]/np.where(z!=0,-z,1e9)
        inimg = front&(px>=0)&(px<W)&(py>=0)&(py<H); x0,y0,x1,y1 = box
        seen += inimg; vote += inimg & (px>=x0)&(px<=x1)&(py>=y0)&(py<=y1)
    ratio = vote/np.maximum(seen,1); obj = (seen>=2)&(ratio>=CARVE_RATIO)
    if obj.sum() < 40:
        print(f"  {cls}: {int(obj.sum())} carved pts, skip"); return None
    objp = pts[obj]
    # drop carve outliers far from the cluster median (flush cabinets / stray splats)
    ctr0 = np.median(objp,0); objp = objp[np.linalg.norm(objp-ctr0,axis=1) < CARVE_TRIM]
    if len(objp) < 40: print(f"  {cls}: too sparse after outlier trim, skip"); return None
    xz = objp[:,[0,2]]; xz0 = xz-xz.mean(0); ev,evec = np.linalg.eigh(xz0.T@xz0/len(xz0))
    yaw = float(np.arctan2(evec[0,np.argmax(ev)], evec[1,np.argmax(ev)]))
    u = np.array([np.sin(yaw),0,np.cos(yaw)]); wa = np.array([np.cos(yaw),0,-np.sin(yaw)])
    du,dw,dy = objp@u, objp@wa, objp[:,1]
    # density-based extent: pull each face IN past sparse tails to the dense object
    # core (splats are floater-free here, so tails are flush-cabinet bleed, not noise)
    def dense_extent(vals, bins=70, frac=0.15):
        h,e = np.histogram(vals, bins=bins); ab = np.where(h >= frac*h.max())[0]
        if len(ab)==0: return float(vals.min()), float(vals.max())
        return float(e[ab[0]]), float(e[ab[-1]+1])
    # WIDTH carves cleanly (many oblique views) → density extent removes cabinet bleed.
    # DEPTH/HEIGHT are poorly constrained (we mostly see front faces) → percentile, so
    # the box keeps real thickness instead of collapsing onto the visible front slab.
    pl=lambda a:np.percentile(a,3); ph_=lambda a:np.percentile(a,97)
    duL,duH = dense_extent(du); dwL,dwH = pl(dw),ph_(dw); dyL,dyH = pl(dy),ph_(dy)
    hu,hw,hy = (duH-duL)/2,(dwH-dwL)/2,(dyH-dyL)/2
    ctr = u*((duL+duH)/2)+wa*((dwL+dwH)/2)+np.array([0,(dyL+dyH)/2,0])
    raw = [round(2*hu,2),round(2*hy,2),round(2*hw,2)]
    # ray-shrink each supported face onto the visible product surface (nudge tighter)
    ctr, hu, hw, hy = _ray_shrink(ctr, yaw, hu, hw, hy, objp)

    # back to face bounds in the (u, wa, y) frame for the top/floor nudges
    cu, cw = float(ctr@u), float(ctr@wa); cyy = float(ctr[1])
    dyL, dyH = cyy-hy, cyy+hy

    # TOP nudge-down: cast along the front-top edge — if it's above the appliance (no
    # splats there), pull the top down to the top of the proud (room-facing) front face.
    fsgn = np.sign(np.dot(ROOM_CTR-ctr, wa)) or 1.0
    fco = (objp@wa)*fsgn; fext = np.percentile(fco, 97)
    duo = objp@u; inband = np.abs(duo-cu) < hu*1.05
    front = inband & (fco > fext - 0.40)            # proud front slab within the width
    if front.sum() > 60:
        top_front = float(np.percentile(objp[front][:,1], 97))
        if top_front < dyH - 0.03:
            print(f"  {cls}: top nudge {2*hy:.2f} → front-edge top {dyH:.2f}→{top_front:.2f}")
            dyH = top_front

    # FLOOR: floor-standing appliances sit on the floor — clamp the bottom to floor level.
    if any(k in cls for k in ("refrigerator","fridge","oven","dishwasher","freezer","washing")):
        if abs(dyL - FLOOR_Y) < 0.5:                # only if already near the floor
            dyL = FLOOR_Y

    hy = (dyH-dyL)/2; cyy = (dyL+dyH)/2
    ctr = u*cu + wa*cw + np.array([0.0, cyy, 0.0])
    cn=lambda su,sw,sy:(ctr+u*(su*hu)+wa*(sw*hw)+np.array([0,sy*hy,0])).tolist()
    corners=[cn(-1,-1,-1),cn(-1,1,-1),cn(1,1,-1),cn(1,-1,-1),cn(-1,-1,1),cn(-1,1,1),cn(1,1,1),cn(1,-1,1)]
    best_sc = max(d[5] for d in dets)
    print(f"  → {cls}: {len(objp):,} pts  raw {raw} → shrink {[round(2*hu,2),round(2*hy,2),round(2*hw,2)]}  conf {best_sc:.2f}")
    return {"id":f"{idp}_{cls}_{int(time.time()*1000)%100000}", "name":f"{cls} [{tag}] {best_sc:.2f}",
            "color":label_color(cls), "corners":corners}

# ── auto-detect fridge/oven/microwave with OWLv2 (cached frames if available) ──
def auto_detect():
    pts = SP[OPQ]; M = len(pts); ph = np.concatenate([pts, np.ones((M,1))], 1)
    class_dets = {}   # cls → [(view, f, W, H, box, score)]
    shots = []

    use_cached = SCENE_IS_DEFAULT and os.path.exists("cameras.json") and os.path.isdir("frames")
    if use_cached:
        cam_files = [("cameras.json","frames")]
        if os.path.exists("cameras_orbit.json") and os.path.isdir("frames_orbit"):
            cam_files.append(("cameras_orbit.json","frames_orbit"))
        views = []
        for cf, fd in cam_files:
            for c in json.load(open(cf))["cams"]:
                p = os.path.join(fd, c["name"]+".png")
                if os.path.exists(p): views.append((c, p))
        print(f"auto_detect: CACHED frames mode — {len(views)} pre-rendered views, {M:,} splats")
        # per-frame OWLv2 detection cache (keyed by path+mtime+promptset) → instant re-runs.
        # The prompt hash invalidates the cache automatically when AUTO_PROMPTS changes.
        import hashlib
        phash = hashlib.md5("|".join(AUTO_PROMPTS).encode()).hexdigest()[:8]
        DCACHE = "auto_dets_cache.json"
        try: dcache = json.load(open(DCACHE))
        except: dcache = {}
        t0 = time.time(); dirty = False
        for i,(cam,png) in enumerate(views):
            pic = Image.open(png).convert("RGB"); W,H = pic.size
            view = np.array(cam["view"],float).reshape(4,4); f = cam["focal"]
            ckey = f"{png}:{int(os.path.getmtime(png))}:{phash}"
            if ckey in dcache:
                hits = [(h[0], h[1], h[2]) for h in dcache[ckey]]
            else:
                hits = detect_owlv2_auto(pic, W, H); dcache[ckey] = hits; dirty = True
            best = {}
            for sc, box, cls in hits:
                if cls not in best or sc > best[cls][0]: best[cls] = (sc, box)
            for cls,(sc,box) in best.items():
                class_dets.setdefault(cls,[]).append((view, f, W, H, box, sc))
            if best:
                ann = pic.copy(); dr = ImageDraw.Draw(ann)
                for cls,(sc,box) in best.items():
                    x0,y0,x1,y1=box; dr.rectangle([x0,y0,x1,y1],outline=(255,90,220),width=3)
                    t=f"{cls} {sc:.2f}"; dr.rectangle([x0,y0-16,x0+len(t)*8,y0],fill=(0,0,0)); dr.text((x0+2,y0-14),t,fill=(255,90,220))
                ann.thumbnail((360,360)); buf=io.BytesIO(); ann.save(buf,"JPEG",quality=72)
                shots.append("data:image/jpeg;base64,"+base64.b64encode(buf.getvalue()).decode())
                print(f"  {cam['name']}: "+", ".join(f"{c} {v[0]:.2f}" for c,v in best.items())+f"  ({time.time()-t0:.0f}s)")
        if dirty:
            try: json.dump(dcache, open(DCACHE,"w"))
            except Exception as e: print("dcache write failed:", e)
    else:
        # live render fallback for uploaded scenes (slower)
        W = H = 560; fovdeg = 62.0
        centroid = np.median(pts, axis=0)
        dists = np.linalg.norm(pts[:,[0,2]] - centroid[[0,2]], axis=1)
        radius = float(np.clip(np.percentile(dists,40)*1.3, 1.2, 2.5))
        print(f"auto_detect: LIVE render mode — centroid {centroid.round(2).tolist()} r={radius:.2f} {M:,} splats")
        angles = np.linspace(0, 2*np.pi, 8, endpoint=False)
        rings = [(centroid[1]+0.1, centroid[1]-0.3), (centroid[1]+0.4, centroid[1]+0.1), (centroid[1]+0.7, centroid[1]+0.5)]
        t0 = time.time()
        for ri,(cam_y,look_y) in enumerate(rings):
            look = np.array([centroid[0], look_y, centroid[2]])
            for a in angles:
                node = np.array([centroid[0]+radius*np.cos(a), cam_y, centroid[2]+radius*np.sin(a)])
                img, view, f = render_ewa(SCENE, node, look.tolist(), W=W, H=H, fov_y_deg=fovdeg,
                                          min_alpha=0.2, near_cull=0.12, exposure=1.5, scale_mult=1.3)
                pic = Image.fromarray((np.clip(img,0,1)**0.9*255).astype(np.uint8))
                hits = detect_owlv2_auto(pic, W, H)
                best = {}
                for sc, box, cls in hits:
                    if cls not in best or sc > best[cls][0]: best[cls] = (sc, box)
                for cls,(sc,box) in best.items():
                    class_dets.setdefault(cls,[]).append((view, f, W, H, box, sc))
                dr = ImageDraw.Draw(pic)
                for cls,(sc,box) in best.items():
                    x0,y0,x1,y1=box; dr.rectangle([x0,y0,x1,y1],outline=(255,90,220),width=2)
                    t=f"{cls} {sc:.2f}"; dr.rectangle([x0,y0-14,x0+len(t)*7,y0],fill=(0,0,0)); dr.text((x0+2,y0-13),t,fill=(255,90,220))
                buf=io.BytesIO(); pic.save(buf,"JPEG",quality=72)
                shots.append("data:image/jpeg;base64,"+base64.b64encode(buf.getvalue()).decode())
                if best: print(f"  ring{ri}: "+", ".join(f"{c} {v[0]:.2f}" for c,v in best.items())+f"  ({time.time()-t0:.0f}s)")

    boxes = []
    for cls, dets in class_dets.items():
        if len(dets) < 2:
            print(f"  {cls}: {len(dets)} view(s), skip"); continue
        bx = _carve_fit(cls, dets, pts, ph)
        if bx: boxes.append(bx)

    try: existing=json.load(open(SERVED))
    except: existing=[]
    # replace any previous auto-detect boxes so repeated scans don't accumulate duplicates
    existing=[b for b in existing if not str(b.get("id","")).startswith("auto_")]
    existing.extend(boxes); json.dump(existing,open(SERVED,"w"))
    print(f"auto_detect done: {len(boxes)} box(es) → {SERVED}")
    return {"shots": shots[:18], "boxes": boxes}

# ── HTTP handler ──────────────────────────────────────────────────────────────
class H(BaseHTTPRequestHandler):
    def _cors(self):
        self.send_header("Access-Control-Allow-Origin","*")
        self.send_header("Access-Control-Allow-Methods","POST, OPTIONS, GET")
        self.send_header("Access-Control-Allow-Headers","Content-Type")
    def do_OPTIONS(self):
        self.send_response(204); self._cors(); self.end_headers()
    def do_GET(self):
        u = urlparse(self.path)
        if u.path == "/forget":
            bid = parse_qs(u.query).get("id",[""])[0]
            try: cur = json.load(open(SERVED))
            except: cur = []
            cur = [b for b in cur if b.get("id") != bid]
            json.dump(cur, open(SERVED,"w")); print(f"/forget {bid} -> {len(cur)} left")
            self.send_response(200); self._cors(); self.end_headers(); self.wfile.write(b"ok")
        else:
            self.send_response(404); self._cors(); self.end_headers()
    def do_POST(self):
        path = urlparse(self.path).path
        if path == "/load-spz":
            n = int(self.headers.get("Content-Length",0)); data = self.rfile.read(n)
            try: load_scene(data); msg = f"loaded {SCENE['n']:,} splats"
            except Exception as e: msg = f"error: {e}"; print("load-spz error:", e)
            self.send_response(200); self._cors(); self.send_header("Content-Type","application/json"); self.end_headers()
            self.wfile.write(json.dumps({"status": msg}).encode()); return
        if path == "/auto-detect":
            t0=time.time()
            try: res=auto_detect()
            except Exception as e: import traceback; traceback.print_exc(); res={"shots":[],"boxes":[]}
            print(f"/auto-detect: {len(res['boxes'])} box(es)  ({time.time()-t0:.0f}s)")
            self.send_response(200); self._cors(); self.send_header("Content-Type","application/json"); self.end_headers()
            self.wfile.write(json.dumps(res).encode()); return
        n = int(self.headers.get("Content-Length",0)); body = json.loads(self.rfile.read(n) or "{}")
        anchor = body.get("anchor"); nodes = np.array(body.get("nodes",[]),float).reshape(-1,3)
        fovdeg = float(np.degrees(body.get("fov",1.047))); exclude = set(body.get("exclude",[]))
        detector = body.get("detector","owlv2")
        t0 = time.time()
        try: res = detect(anchor, nodes, fovdeg, exclude, detector)
        except Exception as e: import traceback; traceback.print_exc(); res = {"shots":[],"box":None}
        box = res.get("box")
        print(f"/detect [{detector}]: {len(nodes)} nodes -> " + (box["name"] if box else "none") + f"  ({time.time()-t0:.0f}s)")
        if box:
            try: cur = json.load(open(SERVED))
            except: cur = []
            cur.append(box); json.dump(cur, open(SERVED,"w"))
        self.send_response(200); self._cors(); self.send_header("Content-Type","application/json"); self.end_headers()
        self.wfile.write(json.dumps(res).encode())
    def log_message(self, *a): pass

ThreadingHTTPServer(("127.0.0.1", PORT), H).serve_forever()
