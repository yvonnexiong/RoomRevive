"""Same ceiling-anchored camera, but rendered with anisotropic EWA splatting (all
splats) for viewer-quality clarity."""
import json, time
import numpy as np
from spz_io import parse_spz
from render import render_ewa, save_png, subset_scene
from ceil_shot import floater_mask

s = parse_spz(json.load(open("cameras.json"))["spz"])
P = s["positions"]; op = s["alphas"] / 255.0
fl = floater_mask(P, op)
axis = (P[:, 0]**2 + P[:, 2]**2 < 0.35**2) & (op > 0.5) & (~fl) & (P[:, 1] > 0.5)
ceiling = float(np.percentile(P[axis, 1], 4)); eye = np.array([0.0, ceiling-0.20, 0.0])
s2 = subset_scene(s, ~fl)
target = np.array([0.4, -0.38, 1.86])
print(f"ceiling {ceiling:.2f}  cam {eye.round(2).tolist()}  splats {s2['n']:,}")
t = time.time()
img, _, _ = render_ewa(s2, eye, target, W=760, H=760, fov_y_deg=78,
                       min_alpha=0.2, near_cull=0.2, exposure=1.45, scale_mult=1.3)
save_png(img, "ceil_shot_ewa.png", gamma=0.9)
print(f"rendered EWA in {time.time()-t:.0f}s -> ceil_shot_ewa.png")
