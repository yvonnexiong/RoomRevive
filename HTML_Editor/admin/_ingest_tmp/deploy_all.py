import os, json, shutil, requests
WEB = r"C:\Unity-Git\RoomRevive\HTML_Editor\admin\_ingest_tmp\web"
TMP = r"C:\Unity-Git\RoomRevive\HTML_Editor\admin\_ingest_tmp"
DST = r"C:\Unity-Git\RoomRevive\HTML_Editor\3D-models\CoffeeMachines"
BASE = "http://localhost:4173"

COL = {"obsidiansort": "Obsidian black", "brombarrod": "Blackberry red", "rosaguld": "Rose gold",
       "lotushvid": "Lotus white", "grafitgra": "Graphite grey", "brillanthvid": "Brilliant white"}
SER = {"silence": "Silence", "milkperfection": "MilkPerfection", "coffeepassion": "CoffeePassion", "coffeeselect": "CoffeeSelect"}
RICHKEYS = {"price", "currency", "dimensions", "features", "headline", "description",
            "rating", "reviewCount", "subtitle", "swatchColor", "variantGroup", "productSheetUrl"}

manifest = json.load(open(os.path.join(WEB, "_manifest.json")))
id2slug = {m["id"]: m["slug"] for m in manifest}

rich = {}
for f in ["CM5310_obsidianblack", "CM5310_brombaerrod", "CM6160_lotuswhite", "CM6560_graphite"]:
    p = os.path.join(TMP, f + ".product.json")
    if os.path.exists(p):
        j = json.load(open(p)); rich[j["product"]["sku"]] = j["product"]

def parse(slug):
    toks = slug.replace("fritstaende-espressomaskine-", "").split("-")
    num = toks[1]; i = 2; series = None
    if i < len(toks) and toks[i] in SER:
        series = toks[i]; i += 1
    coltoks = toks[i:]
    matt = "mat" in coltoks
    color = next((COL[t] for t in coltoks if t in COL), None)
    if any(t in ("alu", "solv", "met") for t in coltoks):
        color = "Aluminium silver"
    if not color:
        color = " ".join(t for t in coltoks if t not in ("mat", "pf", "cm")).title() or "—"
    if matt:
        color += " matt"
    name = "CM " + num + (" " + SER[series] if series else "")
    return name, color

# remove old fake-build files
for stem in ["CM5310_obsidianblack_realsize", "CM5310_brombaerrod_realsize", "CM6160_lotuswhite_realsize", "CM6560_graphite_realsize"]:
    for ext in (".glb", ".png"):
        p = os.path.join(DST, stem + ext)
        if os.path.exists(p):
            os.remove(p)

deployed = []
for pid, slug in id2slug.items():
    g = os.path.join(WEB, pid + ".glb")
    if not os.path.exists(g):
        continue
    mk = slug.replace("fritstaende-espressomaskine-", "").replace("-", "_")
    shutil.copy(g, os.path.join(DST, mk + ".glb"))
    th = os.path.join(WEB, pid + ".png")
    if os.path.exists(th):
        shutil.copy(th, os.path.join(DST, mk + ".png"))
    name, color = parse(slug)
    deployed.append((pid, slug, mk, name, color))

r = requests.post(BASE + "/api/scan", timeout=90).json()
print("SCAN added", r.get("added"), "pruned", r.get("pruned"))
cat = requests.get(BASE + "/api/catalog", timeout=90).json()
byfile = {(i["category"], i["file"]): i for i in cat["items"]}
n = 0
for pid, slug, mk, name, color in deployed:
    it = byfile.get(("CoffeeMachines", mk + ".glb"))
    if not it:
        print("MISS", mk); continue
    prod = {"brand": "Miele", "name": name, "color": color, "sku": pid, "modelKey": mk,
            "productPageUrl": "https://www.miele.dk/product/%s/%s" % (pid, slug)}
    if pid in rich:
        for k, v in rich[pid].items():
            if k in RICHKEYS:
                prod[k] = v
    requests.put(BASE + "/api/items/%s" % it["id"], data=json.dumps({"name": name, "product": prod}),
                 headers={"Content-Type": "application/json"}, timeout=60)
    n += 1
    print("OK %-26s | %s" % (name, color))
ver = requests.get(BASE + "/api/version", timeout=30).json()
print("ATTACHED", n, "| VERSION items", ver["items"], "categories", ver["categories"])
