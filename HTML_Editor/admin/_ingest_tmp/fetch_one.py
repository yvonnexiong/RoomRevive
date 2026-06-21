import sys, re, time, struct, requests
H = {"User-Agent": "Mozilla/5.0"}
GLB = re.compile(r"https://media\.miele\.com/downloads/[0-9a-f]{2}/[0-9a-f]{2}/[A-Za-z0-9_]+\.glb")
IMG = re.compile(r"https://media\.miele\.com/images/[0-9/]+/\d+\.png")
purl, outdir, pid = sys.argv[1], sys.argv[2], sys.argv[3]
def jina(u):
    return requests.get("https://r.jina.ai/" + u, headers={**H, "X-Return-Format": "html"}, timeout=180).text
page = ""
for _ in range(4):
    page = jina(purl)
    if len(page) > 200000 and GLB.search(page):
        break
    time.sleep(3)
g = GLB.findall(page)
print("len", len(page), "glbs", len(g))
if g:
    d = requests.get(g[0], headers=H, timeout=180).content
    if d[:4] == b"glTF":
        d = d[:struct.unpack("<I", d[8:12])[0]]
    open(outdir + "\\" + pid + ".glb", "wb").write(d)
    im = IMG.findall(page)
    if im:
        open(outdir + "\\" + pid + ".png", "wb").write(requests.get(im[0], headers=H, timeout=120).content)
    print("OK", len(d), d[:4] == b"glTF")
else:
    print("STILL_NO_GLB")
