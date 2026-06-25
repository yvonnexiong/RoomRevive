"""Build a multi-box viewer from several box_*.json files."""
import json, sys

no_autoload = "--no-autoload" in sys.argv
no_boxes    = "--no-boxes"    in sys.argv

SPZ = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("--") else "YinanOriginalKitchen.spz"
DST = sys.argv[2] if len(sys.argv) > 2 and not sys.argv[2].startswith("--") else r"C:\Users\chril\Downloads\kitchen_boxes_viewer.html"

OBJS = [] if no_boxes else [
    ("box_fridge_final.json",     "Refrigerator", [0.36, 1.0, 0.54]),
    ("box_oven_final.json",       "Oven",         [0.32, 0.78, 1.0]),
    ("box_microwave_final.json",  "Microwave",    [1.0, 0.62, 0.2]),
    ("box_dishwasher_final.json", "Dishwasher",   [0.95, 0.45, 0.85]),
]
boxes = []
for f, label, color in OBJS:
    b = json.load(open(f))
    boxes.append(dict(label=label, color=color,
                      corners=[[round(v, 4) for v in c] for c in b["corners"]],
                      size=[round(v, 3) for v in b["size"]]))
js = json.dumps(boxes)

if no_autoload:
    autoload_js = ""
    spz_name    = ".spz / .ply"
else:
    autoload_js = (
        f"// auto-load when served (file:// can't fetch local files; falls back to manual drop)\n"
        f"fetch('Splats/{SPZ}').then(r=>r.ok?r.blob():Promise.reject())"
        f".then(b=>loadSPZ(new File([b],'{SPZ}'))).catch(()=>{{}});"
    )
    spz_name = SPZ

html = open("multi_viewer_template.html", encoding="utf-8").read()
html = html.replace("__BOXES__", js).replace("__SPZ__", spz_name).replace("__AUTOLOAD__", autoload_js)
open(DST, "w", encoding="utf-8").write(html)
print("wrote", DST, "with", len(boxes), "boxes", "(no-autoload)" if no_autoload else "")
