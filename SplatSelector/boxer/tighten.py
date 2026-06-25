"""Snap the BoxerNet box to hug the real fridge splats.

Keeps BoxerNet's orientation (yaw), depth (hd) and depth-placement, but snaps the
WIDTH, HEIGHT and their center to the robust extent of the fridge points inside
the box. Fridge points = opaque points in the box that are colour-neutral
(stainless: R~=B), which rejects the warm tan cabinet surround / wood floor.
"""
import json, os
import numpy as np
from spz_io import parse_spz

box = json.load(open("box.json"))
json.dump(box, open("box_boxernet.json", "w"), indent=1)   # backup raw BoxerNet box
c = np.array(box["center"]); hw, hh, hd = box["half"]; yaw = box["yaw"]
WARM = float(os.environ.get("WARM", 0.06))    # R-B threshold: >WARM = tan wall/floor (reject)
LUMAX = float(os.environ.get("LUMAX", 0.62))  # reject bright white cabinets (fridge stainless is darker)
EXP = float(os.environ.get("EXPAND", 1.18))   # search region = box * this

data = json.load(open("cameras.json")); s = parse_spz(data["spz"])
p = s["positions"]; col = s["colors"]; op = s["alphas"] / 255.0
u = np.array([np.sin(yaw), 0, np.cos(yaw)]); w = np.array([np.cos(yaw), 0, -np.sin(yaw)])
du = (p - c) @ u; dw = (p - c) @ w; dy = p[:, 1] - c[1]

lum = 0.299 * col[:, 0] + 0.587 * col[:, 1] + 0.114 * col[:, 2]
inbox = (np.abs(du) < hw * EXP) & (np.abs(dw) < hd * EXP) & (np.abs(dy) < hh * EXP) & (op > 0.5)
neutral = (col[:, 0] - col[:, 2]) < WARM        # stainless ~ neutral; tan/wood rejected
fr = inbox & neutral & (lum < LUMAX)            # darker than white cabinets
print(f"in-box opaque: {inbox.sum():,}   fridge (neutral+dark): {fr.sum():,}")

du_f, dy_f = du[fr], dy[fr]
du_lo, du_hi = np.percentile(du_f, [2, 98])
dy_lo, dy_hi = np.percentile(dy_f, [1, 99])
new_hw = (du_hi - du_lo) / 2
new_hh = (dy_hi - dy_lo) / 2
center = c + u * ((du_lo + du_hi) / 2)          # shift along width
center[1] += (dy_lo + dy_hi) / 2                # shift vertical; depth (w) untouched

def build_corners(c, hw, hh, hd, yaw):
    uu = np.array([np.sin(yaw), 0, np.cos(yaw)]) * hw
    vv = np.array([np.cos(yaw), 0, -np.sin(yaw)]) * hd
    a = np.array([0, hh, 0])
    b = [c - uu - vv - a, c + uu - vv - a, c + uu + vv - a, c - uu + vv - a]
    return np.array(b + [q + 2 * a for q in b])

room = np.array([p[op > 0.5, 0].mean(), p[op > 0.5, 1].mean(), p[op > 0.5, 2].mean()])
front_sign = float(np.sign(np.dot(room - center, w)) or 1.0)
corners = build_corners(center, new_hw, new_hh, hd, yaw)
box.update(method="boxernet+snap", corners=corners.tolist(), front_sign=front_sign,
           center=[float(x) for x in center],
           half=[float(new_hw), float(new_hh), float(hd)],
           size=[float(2 * new_hw), float(2 * new_hh), float(2 * hd)])
json.dump(box, open("box.json", "w"), indent=1)
print(f"tightened: WxHxD = {2*new_hw:.2f} x {2*new_hh:.2f} x {2*hd:.2f} m "
      f"(was {[round(2*x,2) for x in (hw,hh,hd)]})  center=({center[0]:.2f},{center[1]:.2f},{center[2]:.2f})")
