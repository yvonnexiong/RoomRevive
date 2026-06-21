"""Put a EUR price on every item: take the DKK price (existing field or product page), EUR=round(DKK/7.46).
Deterministic — no LLM. Writes via the admin server API (single writer)."""
import requests, re, time
BASE = "http://localhost:4173"
H = {"User-Agent": "Mozilla/5.0"}
RATE = 7.46

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
    m = re.search(r"([\d.]+),\d{2}\s*kr", t) or re.search(r"([\d.]+)\s*kr\b", t) or re.search(r"DKK\s*([\d.]+)", t)
    return int(m.group(1).replace(".", "")) if m else None

cat = requests.get(BASE + "/api/catalog", timeout=60).json()
priced = reused = 0
noprice = []
for it in cat["items"]:
    p = it.get("product") or {}
    if p.get("currency") == "EUR" and p.get("price"):
        continue
    dkk = None
    if p.get("price") and p.get("currency") in (None, "DKK", ""):
        try:
            dkk = int(round(float(p["price"]))); reused += 1
        except Exception:
            dkk = None
    if dkk is None and p.get("productPageUrl"):
        dkk = dkk_from(jina(p["productPageUrl"]))
        time.sleep(0.4)
    if dkk:
        eur = round(dkk / RATE)
        requests.put(BASE + "/api/items/" + it["id"],
                     json={"product": {"price": eur, "currency": "EUR", "priceDKK": dkk}}, timeout=30)
        priced += 1
        print(it["category"], "|", p.get("name"), dkk, "DKK ->", eur, "EUR")
    else:
        noprice.append("%s | %s" % (it["category"], p.get("name")))
print("PRICED", priced, "| reused-existing-DKK", reused, "| NO PRICE FOUND", len(noprice))
for n in noprice:
    print("  NOPRICE", n)
