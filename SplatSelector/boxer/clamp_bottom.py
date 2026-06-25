"""Extend a box's BOTTOM face down to the floor (keep the top). For floor-standing
appliances (ovens/ranges, fridges). Floor = peak of the opaque-Y histogram (lower 40%)."""
import json, sys
import numpy as np
from spz_io import parse_spz

path = sys.argv[1]
b = json.load(open(path))
c = np.array(b["center"]); hw, hh, hd = b["half"]; yaw = b["yaw"]
s = parse_spz(json.load(open("cameras.json"))["spz"])
P = s["positions"]; opq = s["alphas"] / 255.0 > 0.5
yall = P[opq, 1]; ymn, ymx = float(yall.min()), float(yall.max())
hc, edg = np.histogram(yall, bins=140, range=(ymn, ymx))
fb = int(np.argmax(hc[:int(140 * 0.4)]))
floor = edg[fb] + (edg[1] - edg[0]) / 2

top = c[1] + hh
new_cy = (top + floor) / 2
new_hh = (top - floor) / 2

def build(c, hw, hh, hd, yaw):
    u = np.array([np.sin(yaw), 0, np.cos(yaw)]) * hw
    v = np.array([np.cos(yaw), 0, -np.sin(yaw)]) * hd
    a = np.array([0, hh, 0])
    bb = [c - u - v - a, c + u - v - a, c + u + v - a, c - u + v - a]
    return np.array(bb + [q + 2 * a for q in bb])

center = np.array([c[0], new_cy, c[2]])
cor = build(center, hw, new_hh, hd, yaw)
b.update(center=[float(x) for x in center], half=[float(hw), float(new_hh), float(hd)],
         size=[float(2 * hw), float(2 * new_hh), float(2 * hd)], corners=cor.tolist())
json.dump(b, open(path, "w"), indent=1)
print(f"floor={floor:.2f}  bottom extended -> {path}: size {[round(x,2) for x in b['size']]} center {[round(x,2) for x in center.tolist()]}")
