"""Price the 10 remaining fridges: with-sku -> fetch /product/<sku>/; no-sku -> same-model sibling (marked)."""
import requests, re, time
BASE = "http://localhost:4173"; H = {"User-Agent": "Mozilla/5.0"}; RATE = 7.46

def jina(u, tries=3):
    for _ in range(tries):
        try:
            t = requests.get("https://r.jina.ai/" + u, headers=H, timeout=120).text
            if len(t) > 800:
                return t
        except Exception:
            pass
        time.sleep(2)
    return ""

def dkk_from(t):
    m = re.search(r"([\d.]+),\d{2}\s*kr", t) or re.search(r"([\d.]+)\s*kr\b", t)
    return int(m.group(1).replace(".", "")) if m else None

cat = requests.get(BASE + "/api/catalog", timeout=60).json()
targets = [i for i in cat["items"] if i["category"] == "Fridges" and not (i.get("product") or {}).get("price")]
namemap = {}
# pass 1: items with a SKU -> fetch their own page
for it in targets:
    p = it["product"]; sku = p.get("sku")
    if not sku:
        continue
    url = "https://www.miele.dk/product/%s/" % sku
    dkk = dkk_from(jina(url))
    if dkk:
        eur = round(dkk / RATE)
        requests.put(BASE + "/api/items/" + it["id"], json={"product": {"price": eur, "currency": "EUR", "priceDKK": dkk, "productPageUrl": url, "_priceQuality": "page"}}, timeout=30)
        namemap[p["name"]] = dkk
        print("PAGE   ", p["name"], "|", p.get("modelKey"), dkk, "DKK ->", eur, "EUR")
    else:
        print("NOPRICE-PAGE", p["name"], sku)
    time.sleep(0.5)
# pass 2: no SKU -> same-model sibling price (best-effort)
for it in targets:
    p = it["product"]
    if p.get("sku"):
        continue
    dkk = namemap.get(p["name"])
    if dkk:
        eur = round(dkk / RATE)
        requests.put(BASE + "/api/items/" + it["id"], json={"product": {"price": eur, "currency": "EUR", "priceDKK": dkk, "_priceQuality": "estimate-same-model"}}, timeout=30)
        print("SIBLING", p["name"], "|", p.get("modelKey"), dkk, "DKK ->", eur, "EUR (est)")
    else:
        print("STILL-NOPRICE", p["name"], p.get("modelKey"))
print("DONE")
