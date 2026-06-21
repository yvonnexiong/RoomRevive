"""Normalize mojibake/× in the dimensions field to ASCII 'x' via the server API (single writer)."""
import requests
BASE = "http://localhost:4173"
cat = requests.get(BASE + "/api/catalog", timeout=60).json()
fixed = 0
for it in cat["items"]:
    p = it.get("product") or {}
    d = p.get("dimensions")
    if isinstance(d, str) and ("Ã—" in d or "×" in d):
        nd = d.replace("Ã—", "x").replace("×", "x")
        nd = " ".join(nd.split())
        requests.put(BASE + "/api/items/" + it["id"], json={"product": {"dimensions": nd}}, timeout=30)
        fixed += 1
        print(it["category"], "|", p.get("name"), "->", nd)
print("FIXED", fixed)
