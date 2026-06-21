"""Fill price + specs for CoffeeMachines items that lack data (page-sourced). Merges via the server."""
import requests, re, time
BASE = "http://localhost:4173"
H = {"User-Agent": "Mozilla/5.0"}

def jina(url, tries=3):
    for _ in range(tries):
        try:
            t = requests.get("https://r.jina.ai/" + url, headers=H, timeout=180).text
            if len(t) > 1500:
                return t
        except Exception:
            pass
        time.sleep(2)
    return ""

def toint(s):
    return int(re.sub(r"[.\s]", "", s))

cat = requests.get(BASE + "/api/catalog", timeout=60).json()
todo = [i for i in cat["items"] if i["category"] == "CoffeeMachines" and not (i.get("product") or {}).get("price")]
print("TO FILL", len(todo))
filled = 0
for it in todo:
    p = it.get("product") or {}
    url = p.get("productPageUrl")
    if not url:
        print("NOURL", p.get("sku")); continue
    t = jina(url)
    if not t:
        print("FETCHFAIL", p.get("sku")); continue
    upd = {"_specSource": "miele.dk page (2026-06-20)"}
    m = re.search(r"([\d.]+),00\s*(?:kr|DKK)", t)
    if m:
        upd["price"] = toint(m.group(1)); upd["currency"] = "DKK"
    h = re.search(r"[Hh]øjde[^\d]{0,30}(\d{3})\s*mm", t)
    w = re.search(r"[Bb]redde[^\d]{0,30}(\d{3})\s*mm", t)
    de = re.search(r"[Dd]ybde[^\d]{0,30}(\d{3})\s*mm", t)
    if h and w and de:
        upd["dimensions"] = "H%s × W%s × D%s mm" % (h.group(1), w.group(1), de.group(1))
    else:
        m = re.search(r"(\d{3})\s*[×x]\s*(\d{3})\s*[×x]\s*(\d{3})\s*mm", t)
        if m:
            upd["dimensions"] = "%s × %s × %s mm" % (m.group(1), m.group(2), m.group(3))
    m = re.search(r"[Vv]and[a-zæøå]*[^\d]{0,30}(\d[.,]\d)\s*[lL]\b", t) or re.search(r"(\d[.,]\d)\s*[lL]\b", t)
    if m:
        upd["waterTank"] = m.group(1).replace(",", ".") + " L"
    m = re.search(r"(\d{3})\s*g\b", t)
    if m:
        upd["beanContainer"] = m.group(1) + " g"
    m = re.search(r"(\d[.,]\d{1,2})\s*kW", t)
    if m:
        upd["power"] = m.group(1).replace(",", ".") + " kW"
    requests.put(BASE + "/api/items/%s" % it["id"], json={"product": upd}, timeout=60)
    filled += 1
    print("FILLED", p.get("sku"), "price=", upd.get("price"), "dims=", upd.get("dimensions"))
    time.sleep(1)
print("DONE filled", filled)
