"""Place camera at origin, ray-cast straight up to the ceiling, drop slightly below
it, pivot there, and render ONE picture with wide (2.2x) splats."""
import json
import numpy as np
from spz_io import parse_spz
from render import render, save_png, subset_scene

def floater_mask(P, alpha, cell=0.06, maxd=15):
    solid = alpha > 0.2
    vox = np.floor(P / cell).astype(np.int64)
    OFF, A, B = 1 << 19, 1 << 40, 1 << 20
    key = (vox[:, 0]+OFF)*A + (vox[:, 1]+OFF)*B + (vox[:, 2]+OFF)
    uk, uc = np.unique(key[solid], return_counts=True)
    dens = np.zeros(len(P), np.int64)
    for dx in (-1, 0, 1):
        for dy in (-1, 0, 1):
            for dz in (-1, 0, 1):
                nk = (vox[:, 0]+dx+OFF)*A + (vox[:, 1]+dy+OFF)*B + (vox[:, 2]+dz+OFF)
                idx = np.clip(np.searchsorted(uk, nk), 0, len(uk)-1)
                dens += np.where(uk[idx] == nk, uc[idx], 0)
    return dens < maxd

s = parse_spz(json.load(open("cameras.json"))["spz"])
P = s["positions"]; op = s["alphas"] / 255.0

# ray cast from (0,0,0) toward (0,1,0): nearest splat on the vertical axis above origin = ceiling
fl = floater_mask(P, op)
axis = (P[:, 0]**2 + P[:, 2]**2 < 0.35**2) & (op > 0.5) & (~fl) & (P[:, 1] > 0.5)
ys = P[axis, 1]
ceiling = float(np.percentile(ys, 4))     # first hit going up = underside of ceiling
cam_y = ceiling - 0.20                      # move slightly down from the ceiling
eye = np.array([0.0, cam_y, 0.0])
print(f"ceiling hit at y={ceiling:.2f} ({axis.sum()} axis splats)  camera at {eye.round(2).tolist()}")

# pivot here; aim down toward the kitchen (toward the dishwasher) so the shot is useful
s2 = subset_scene(s, ~fl)                   # floaters hidden for a cleaner image
target = np.array([0.4, -0.38, 1.86])
img, _, _ = render(s2, eye, target, W=820, H=820, fov_y_deg=78, subsample=400000,
                   min_alpha=0.5, max_r=14, exposure=1.5, near_cull=0.2, scale_mult=2.2)
save_png(img, "ceil_shot.png", gamma=0.9)
print("saved ceil_shot.png")
