"""Reconnaissance: replicate miele_fetch for one product, with verbose logging."""
import os, re, io, sys, json, zipfile, requests

H = {"User-Agent": "Mozilla/5.0"}
GLBRE = r"https://media\.miele\.com/downloads/[0-9a-f]{2}/[0-9a-f]{2}/[^\"'\\ )]+\.zip"
IMGRE = r"https://media\.miele\.com/images/[0-9/]+/\d+\.png"

def jina(url, html=True):
    h = dict(H)
    if html:
        h["X-Return-Format"] = "html"
    return requests.get("https://r.jina.ai/" + url, headers=h, timeout=180).text

product_url = sys.argv[1]
workdir = sys.argv[2]
os.makedirs(workdir, exist_ok=True)

t = jina(product_url, html=True)
open(os.path.join(workdir, "_page.html"), "w", encoding="utf-8").write(t)
zips = list(dict.fromkeys(re.findall(GLBRE, t)))
imgs = list(dict.fromkeys(re.findall(IMGRE, t)))
print("PAGE_LEN", len(t))
print("ZIPS", len(zips))
for z in zips[:12]:
    print("  z", z)
print("IMGS", len(imgs))
for i in imgs[:12]:
    print("  i", i)

# also surface any other download/cad hints
for kw in ["fbx", "obj", ".zip", "cad", "download"]:
    print("hint", kw, t.lower().count(kw))

out = {"workdir": workdir, "fbx": "", "obj": "", "image": "", "formats": []}
for z in zips:
    try:
        data = requests.get(z, headers=H, timeout=180).content
        print("zip", z[-46:], "bytes", len(data), "pk", data[:2] == b"PK")
        if data[:2] != b"PK":
            continue
        zf = zipfile.ZipFile(io.BytesIO(data))
        for n in zf.namelist():
            e = n.lower().rsplit(".", 1)[-1]
            if e not in out["formats"]:
                out["formats"].append(e)
        for n in zf.namelist():
            if n.lower().endswith(".fbx") and not out["fbx"]:
                zf.extractall(workdir); out["fbx"] = os.path.join(workdir, n)
            if n.lower().endswith(".obj") and not out["obj"]:
                zf.extractall(workdir); out["obj"] = os.path.join(workdir, n)
    except Exception as e:
        print("ziperr", str(e)[:120])

if imgs:
    p = os.path.join(workdir, "front.png")
    open(p, "wb").write(requests.get(imgs[0], headers=H, timeout=120).content)
    out["image"] = p

print("RESULT", json.dumps(out))
