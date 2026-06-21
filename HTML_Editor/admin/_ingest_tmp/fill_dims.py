"""Fill datasheet-verified dimensions (+ tank/beans/power) for items missing dims."""
import requests, re, time
BASE = "http://localhost:4173"
H = {"User-Agent": "Mozilla/5.0"}
def jina(u, tries=3):
    for _ in range(tries):
        try:
            t = requests.get("https://r.jina.ai/" + u, headers=H, timeout=180).text
            if len(t) > 800:
                return t
        except Exception:
            pass
        time.sleep(2)
    return ""

cat = requests.get(BASE + "/api/catalog", timeout=60).json()
todo = [i for i in cat["items"] if i["category"] == "CoffeeMachines" and not (i.get("product") or {}).get("dimensions")]
print("TO FILL DIMS", len(todo))
filled = 0
for it in todo:
    p = it.get("product") or {}
    sku = p.get("sku")
    ds = jina("https://media.miele.com/downloads/k-/da/FS_%s_DKD_DK-da.pdf" % sku)
    if not ds:
        print("FETCHFAIL", sku); continue
    upd = {"_dimSource": "Miele datasheet"}
    bw = re.search(r"Produktbredde i mm\s*(\d{2,3})", ds)
    bh = re.search(r"Produkth.jde i mm\s*(\d{2,3})", ds)
    bd = re.search(r"Produktdybde i mm\s*(\d{2,3})", ds)
    if bw and bh and bd:
        upd["dimensions"] = "H%s × W%s × D%s mm" % (bh.group(1), bw.group(1), bd.group(1))
    wt = re.search(r"Vandbeholderens kapacitet i liter\s*(\d[.,]\d)", ds)
    if wt:
        upd["waterTank"] = wt.group(1).replace(",", ".") + " L"
    bc = re.search(r"B.nnebeholder[^\d]{0,30}(\d{3})", ds)
    if bc:
        upd["beanContainer"] = bc.group(1) + " g"
    pw = re.search(r"(\d[.,]\d{1,2})\s*kW", ds)
    if pw:
        upd["power"] = pw.group(1).replace(",", ".") + " kW"
    if "dimensions" in upd:
        requests.put(BASE + "/api/items/%s" % it["id"], json={"product": upd}, timeout=60)
        filled += 1
        print("FILLED", sku, upd.get("dimensions"), upd.get("waterTank", ""), upd.get("power", ""))
    else:
        print("NODIMS", sku, "len", len(ds))
    time.sleep(1)
print("DONE", filled)
