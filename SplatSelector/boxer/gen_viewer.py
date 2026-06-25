"""Inject box.json into the viewer template -> a scene-specific viewer HTML."""
import json, re, sys

import os
box = json.load(open("box.json"))
_here = os.path.dirname(os.path.abspath(__file__))
tmpl = open(os.path.join(_here, "fridge_box_viewer_template.html"), encoding="utf-8").read()

def arr(a, p=4):
    return "[" + ",".join(f"{v:.{p}f}" for v in a) + "]"
corners = "[" + ",".join(arr(c) for c in box["corners"]) + "]"
views = "[" + ",".join(f'"{v}"' for v in box.get("views", [])) + "]"
edges_js = ""
if box.get("edges"):
    edges_js = "edges:[" + ",".join(f"[{a},{b}]" for a, b in box["edges"]) + "],\n "
fs_js = f'front_sign:{box["front_sign"]:.0f},' if "front_sign" in box else ""
lit = (f'const BOX={{center:{arr(box["center"])},half:{arr(box["half"])},yaw:{box["yaw"]:.4f},{fs_js}\n'
       f' {edges_js}size:{arr(box["size"],3)},views:{views},\n corners:{corners}}};')

out = re.sub(r"const BOX=\{[\s\S]*?\]\};", lit, tmpl, count=1)

scene = sys.argv[1] if len(sys.argv) > 1 else "the kitchen"
theta = float(sys.argv[2]) if len(sys.argv) > 2 else 2.4
rad = float(sys.argv[3]) if len(sys.argv) > 3 else 3.2
out = out.replace("cam.radius=3.2;cam.theta=2.4;cam.phi=1.4;",
                  f"cam.radius={rad};cam.theta={theta};cam.phi=1.4;")
out = out.replace("Original Kitchen-restyled.spz", scene)

dst = sys.argv[4] if len(sys.argv) > 4 else r"C:\Users\chril\Downloads\fridge_box_viewer.html"
open(dst, "w", encoding="utf-8").write(out)
print("wrote", dst, "  size", box["size"])
