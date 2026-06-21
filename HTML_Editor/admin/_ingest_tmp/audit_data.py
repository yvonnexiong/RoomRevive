"""Master data-coverage audit: which CoffeeMachines items have complete data."""
import requests
cat = requests.get("http://localhost:4173/api/catalog", timeout=60).json()
items = [i for i in cat["items"] if i["category"] == "CoffeeMachines"]
complete = 0
rows = []
for i in items:
    p = i.get("product", {}) or {}
    has_price = p.get("price") not in (None, "", 0)
    has_dims = bool(p.get("dimensions"))
    ok = has_price and has_dims
    complete += ok
    rows.append((ok, p.get("name", "?"), p.get("color", "?"), p.get("price"), "Y" if has_dims else "n", p.get("sku", "?")))
for ok, name, color, price, dims, sku in sorted(rows, key=lambda r: (r[0], r[1])):
    print(("OK " if ok else "GAP"), "%-24s %-18s price=%-7s dims=%s sku=%s" % (name, color, price, dims, sku))
print("COMPLETE %d / %d" % (complete, len(items)))
