# Boxer box-detection — improvement backlog

Saved 2026-06-16. General goal: make 3D box detection on a splat **tighter and more
reliable, for ANY object** (prompt-driven), not per-example nudges. Iterate against a
**quality metric** (see `score_box.py`) so "better over time" is measurable.

## The metric (already implemented: `score_box.py`)
Primary score = **mean 2D IoU** between the projected 3D box (its 2D bounding rect) and
the OWLv2 2D detection box, averaged over all detecting views. A box that truly hugs the
object projects onto the detector's 2D box from every angle → IoU→1.
- Secondary (TODO): **coverage** (% object points inside box, want ~0.98) and
  **tightness** (carved-points bbox volume / box volume, want ~1; low = box too empty).
- `score_box.py <label>` appends a row to `scores.csv`. Compare rows across iterations;
  "becoming better" = mean_iou ↑ and tightness → 1. Use it to accept/reject every change.

## Backlog (do these when there is token budget)

1. **Box-region masking for cleaner pictures** (user idea). Before rendering any view,
   hide all splats outside **1.5× the current (coarse) box** (box-local AABB test, reuse
   `inBox`-style projection). Isolates the object from wall/cabinets/floor/floaters →
   OWLv2 detects more reliably and the carve is cleaner. Add a `box_mask=` option to
   `render.subset_scene` / a helper `box_keep(positions, box, expand=1.5)`. Use it in
   `capture.py`, `orbit_capture.py`, `axis_views.py`, and the detection pass of
   `carve_fit.py`. Re-detect on masked frames → expect higher detection rate + IoU.

2. **8 corner-diagonal snapshots** (user idea). Render 8 views, one sighting along each
   of the box's 8 corners (camera on the corner diagonal looking at center), so all three
   edges meeting at each corner line up. Add `mode=corners` to `axis_views.py`. Feed these
   8 (masked) snaps into detection/carving too — more diverse angles → better carve.

3. **Carving sweep + accept by metric.** `carve_fit.py` already caches detections
   (`dets_cache_*.json`). Sweep RATIO∈{0.7,0.8,0.9}, MINSEEN∈{5,8}; run `score_box.py`
   for each; keep the best mean_iou. The 0.65 default was too loose (box grew). Higher
   ratio + masked+corner views should tighten. This is the core "make detection better".

4. **Tighten depth too** once width/height hug: anchor the front face to the object's
   front surface, keep BoxerNet depth as a floor (BoxerNet infers occluded back).

5. **Two-stage locate→orbit→carve, fully prompt-driven** so it generalizes: pass-1
   coarse box (capture→OWLv2→BoxerNet), then masked orbit + corner snaps around it, then
   carve, then score. Wrap as `detect_object.py --prompt "<obj>"`.

## Per-object-type rules
- **Floor-clamp bottom** (extend the box's bottom face down to the detected floor):
  applies to object types: **{oven}** only (as of 2026-06-16, per user).
  To add a type later: run `python clamp_bottom.py box_<type>_final.json` on its box,
  or pass `CLAMP_FLOOR=1` when carving it. Countertop items (microwave) must NOT use it.
- **Per-axis grow** (rayfit GROW_U/GROW_D/GROW_Y): used for microwave width only.

## Findings (2026-06-16 iteration — see scores.csv)
- **Masking to black kills OWLv2** (object on a void = out-of-distribution → 0 detections).
  Masking is for human inspection only; detect on full-scene context renders.
- **Depth & yaw must come from BoxerNet**, not carving: carving depth is unstable
  (1.06 at low ratio → 0.30 at high). Carve constrains WIDTH well; height is shaky
  (top/bottom seen in few views), depth not at all.
- **mean-2D-IoU plateaus ~0.64 and is biased**: with loose 2D detection boxes it
  *rewards slightly-loose* 3D boxes (they fill the 2D box) and penalizes tight ones —
  e.g. tight 0.92-wide box scored 0.59 < loose 1.21-wide at 0.64. So IoU is a
  localization sanity check, NOT a tightness metric here.
- **`fill` term is degenerate** (~1.0 always) because a box fit to its contained points
  fills itself. Real surround-contamination needs GT segmentation → not available.
- **Root bottleneck = 2D detection looseness on low-quality CPU renders.** Genuine next
  lever: feed the detector crisp GPU-rendered frames (screenshot the WebGL viewer) or a
  tighter detector. Until then, best practical box = per-axis: carved width (tight),
  BoxerNet height+depth+yaw. Current `box.json` uses this (0.92 x 2.11 x 0.72).

## Validation
After each change, run `score_box.py`, AND render `axis_views.py` (face-on) + the 8
corner views, AND test a **second object** (e.g. `PROMPT="an oven"`) to confirm generality.
Current best box: see `box.json`; raw BoxerNet in `box_boxernet.json`.
