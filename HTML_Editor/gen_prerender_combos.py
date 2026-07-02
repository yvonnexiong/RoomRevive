# gen_prerender_combos.py — build prerender_combos.json from the Unity cabinet ProductData.
#
# Scans every cabinet ProductData .asset, reads splatCabMaterial / splatWtMaterial, and writes a
# manifest the browser's "Pre-render all cabinets" batch reads. The 'key' is computed identically to
# the editor's safeName() and to Unity's PrerenderKey(), so all three agree on the .spz filename.
#
# Run:  python gen_prerender_combos.py

import os, re, json, glob

ROOT = os.path.dirname(os.path.abspath(__file__))
CAB_DIR = os.path.normpath(os.path.join(
    ROOT, "..", "RoomRevive_unity",
    "Assets", "RoomRevive", "UI", "ProductBrowser", "Data", "Product", "Cabinets", "FromCatalog"))
OUT = os.path.join(ROOT, "prerender_combos.json")

def field(text, name):
    # [ \t] so the match never crosses a newline into the next field's value.
    m = re.search(r'^[ \t]*' + re.escape(name) + r':[ \t]*(.*?)[ \t]*$', text, re.MULTILINE)
    v = m.group(1).strip() if m else ""
    return "" if v in ("", "''", '""') else v

def safe(s):
    if not s:
        return ""
    s = re.sub(r'\.[^.]+$', '', s)              # strip extension
    s = re.sub(r'[^a-zA-Z0-9._-]+', '_', s)     # sanitize (matches editor safeName)
    return s

def key_for(cab, wt):
    return safe(cab) + ("__" + safe(wt) if wt else "")

combos = []
for path in sorted(glob.glob(os.path.join(CAB_DIR, "*.asset"))):
    with open(path, "r", encoding="utf-8") as f:
        text = f.read()
    cab = field(text, "splatCabMaterial")
    wt  = field(text, "splatWtMaterial")
    if not cab and not wt:
        continue
    combos.append({
        "key": key_for(cab, wt),
        "cab": cab or None,
        "wt":  wt or None,
        "id":  field(text, "id"),
        "name": field(text, "productName"),
    })

# De-dup by key (identical cab+wt pairs share one .spz).
seen, unique = set(), []
for c in combos:
    if c["key"] in seen:
        continue
    seen.add(c["key"])
    unique.append(c)

with open(OUT, "w", encoding="utf-8") as f:
    json.dump(unique, f, indent=1)

print(f"Wrote {len(unique)} combos ({len(combos)} styles, {len(combos)-len(unique)} duplicate keys) -> {OUT}")
