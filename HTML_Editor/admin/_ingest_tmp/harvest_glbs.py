"""Download the real web 3D-viewer GLB (+ thumb) for every product in a Miele category.
Per PIPELINE_RULES rule 0: use the <model-viewer> GLB, truncate to header-declared length."""
import os, re, sys, json, time, struct, requests

H = {"User-Agent": "Mozilla/5.0"}
GLBWEB = re.compile(r"https://media\.miele\.com/downloads/[0-9a-f]{2}/[0-9a-f]{2}/[A-Za-z0-9_]+\.glb")
IMGRE = re.compile(r"https://media\.miele\.com/images/[0-9/]+/\d+\.png")

def jina(url, html=False, tries=2):
    h = dict(H)
    if html:
        h["X-Return-Format"] = "html"
    for i in range(tries):
        try:
            t = requests.get("https://r.jina.ai/" + url, headers=h, timeout=180).text
            if len(t) > 2000:
                return t
        except Exception:
            pass
        time.sleep(2)
    return ""

cat_url, outdir = sys.argv[1], sys.argv[2]
os.makedirs(outdir, exist_ok=True)
listing = jina(cat_url, html=False)
prods = {}
for pid, slug in re.findall(r"/product/(\d+)/([a-z0-9\-]+)", listing):
    prods.setdefault(pid, slug)
print("PRODUCTS", len(prods)); sys.stdout.flush()

results = []
for pid, slug in prods.items():
    purl = "https://www.miele.dk/product/%s/%s" % (pid, slug)
    page = jina(purl, html=True)
    glbs = GLBWEB.findall(page)
    imgs = list(dict.fromkeys(IMGRE.findall(page)))
    if not glbs:
        print(pid, slug, "NO_GLB len", len(page)); results.append({"id": pid, "slug": slug, "status": "noglb"}); sys.stdout.flush(); time.sleep(1); continue
    try:
        data = requests.get(glbs[0], headers=H, timeout=180).content
        magic = data[:4] == b"glTF"
        if magic:
            declared = struct.unpack("<I", data[8:12])[0]
            data = data[:declared]
        open(os.path.join(outdir, pid + ".glb"), "wb").write(data)
        if imgs:
            open(os.path.join(outdir, pid + ".png"), "wb").write(requests.get(imgs[0], headers=H, timeout=120).content)
        print(pid, slug, "OK", len(data), "magic", magic); sys.stdout.flush()
        results.append({"id": pid, "slug": slug, "status": "ok", "size": len(data), "glb": glbs[0]})
    except Exception as e:
        print(pid, slug, "GLB_FAIL", str(e)[:60]); results.append({"id": pid, "slug": slug, "status": "glbfail"}); sys.stdout.flush()
    json.dump(results, open(os.path.join(outdir, "_manifest.json"), "w"), indent=2)
    time.sleep(1)

ok = sum(1 for r in results if r["status"] == "ok")
print("DONE", ok, "/", len(prods))
