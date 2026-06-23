"""Build the de-duplicated union of kitchen slugs across the 4 category listing pages."""
import subprocess, re, json
CATS = ["modern-kitchens", "designer-kitchens", "natural-scandi", "modern-cottage-style"]
BASE = "https://www.nobilia.de/en/products/kitchens/"
PAT = re.compile(r'/en/products/kitchens/(modern-kitchens|designer-kitchens|natural-scandi|modern-cottage-style)/([a-z]+-\d+)/')
found = {}
for cat in CATS:
    md = subprocess.run(["curl.exe", "-sL", "--ssl-no-revoke", "--max-time", "120",
                         "https://r.jina.ai/" + BASE + cat + "/"],
                        capture_output=True, text=True, encoding="utf-8", errors="replace").stdout
    n0 = len(found)
    for m in PAT.finditer(md):
        cp, slug = m.group(1), m.group(2)
        if slug not in found:
            found[slug] = cp
    print(cat, "-> +%d new (total %d)" % (len(found) - n0, len(found)))
out = [{"slug": s, "catpath": c} for s, c in sorted(found.items())]
json.dump(out, open(r"C:\Unity-Git\RoomRevive\HTML_Editor\admin\_ingest_tmp\all_slugs.json", "w"), indent=1)
from collections import Counter
print("BY TYPE:", dict(Counter(c for c in found.values())))
print("TOTAL", len(out))
