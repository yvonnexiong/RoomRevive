# Splat Segmenter

Auto-recolors a kitchen Gaussian splat: **countertop → limegreen**, **cabinet fronts → pink**.
Pure geometry, fully offline (numpy + opencv + scipy). The colors are selection masks for
downstream material-swap tooling.

## Run

```bash
python segment_kitchen_spz.py IN.spz OUT.spz
# write straight to the live Unity file so it hot-reloads:
python segment_kitchen_spz.py "..\..\RoomRevive_unity\Assets\RoomRevive\Splats\Host and Gather Kitchen_new.spz" \
                              "..\..\RoomRevive_unity\LiveSplat\kitchen-copy.spz"
```

`LiveSplatLoader` (on the scene's `GaussianSplatRenderer`) watches `RoomRevive_unity/LiveSplat/kitchen-copy.spz`
and repaints within ~1s, in Edit or Play mode. The source `.spz` is never modified in place.

Flags: `--diagnose` (analyze, write nothing) · `--green-only` (skip cabinets) ·
`--debug-dir DIR` (write elevation/plan label PNGs) · `--dump-bands` (print height-band analysis).

## How it works

1. **Decode** the gzip NGSP **SPZ v2 / shDeg0** in original byte order (asserts v2 — Unity's reader rejects others).
2. **Per-splat normal** = world direction of the splat's *smallest-scale axis* (splats lie flat on surfaces).
3. **Up + floor/ceiling** are derived from the data (largest horizontal bands), never hardcoded — all
   thresholds are **relative to room height**, so non-metric exports work too.
4. **Counter** = the mid-height horizontal slab with the most *cabinet mass directly beneath it*
   (a table has legs; a counter has cabinets) and a soft "≈1/3 up the room" prior.
5. **Region-grow**: only ~16–20% of splats have a usable normal, so confident seeds *vote* on the
   footprint (connected components), then **every** splat in the band+footprint is recolored — solid mask, not speckle.
6. **Cabinets** = the under-counter prism, minus floor-to-ceiling wall columns, gated by real vertical mass.
7. **Recolor** the 3 color bytes (limegreen / pink), re-gzip v2.

## Known limits (geometry-only)

- **Appliances** (dishwasher/oven fronts) inside the cabinet run read as cabinet — geometry can't tell them
  from a door. Same for a low side-table that mimics counter+cabinet geometry. These need the optional 2D
  segmentation (ONNX SAM on rendered views, back-projected and intersected with the geometry) to fully clean.
- Mask byte values: limegreen `(50,205,50)`, pink `(255,45,166)`. The Unity color curve saturates these to
  vivid green / hot-pink. A downstream detector should key off the **written byte values**, not screen pixels.
