"""Whole-catalog data coverage audit, per category."""
import requests
from collections import defaultdict
cat = requests.get("http://localhost:4173/api/catalog", timeout=60).json()
SPEC = ["price", "dimensions", "energyClass", "features", "fridgeCapacity", "freezerCapacity",
        "waterTank", "noise", "annualEnergy", "power", "capacity"]
res = defaultdict(lambda: {"total": 0, "full": 0, "miss": []})
for i in cat["items"]:
    c = i["category"]; p = i.get("product") or {}
    r = res[c]; r["total"] += 1
    if any(p.get(k) not in (None, "", [], 0) for k in SPEC):
        r["full"] += 1
    else:
        r["miss"].append(i.get("name") or i.get("file"))
T = W = 0
print("== COVERAGE ==")
for c, r in res.items():
    T += r["total"]; W += r["full"]
    print("%-16s %3d/%-3d with specs   (%d empty)" % (c, r["full"], r["total"], len(r["miss"])))
print("TOTAL %d/%d with specs, %d EMPTY" % (W, T, T - W))
for c, r in res.items():
    if r["miss"]:
        print("\n%s — %d empty:" % (c, len(r["miss"])))
        for m in r["miss"][:50]:
            print("   -", m)
