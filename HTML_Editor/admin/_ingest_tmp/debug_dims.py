import requests, re
H = {"User-Agent": "Mozilla/5.0"}
def get(u, html=False):
    h = dict(H)
    if html:
        h["X-Return-Format"] = "html"
    return requests.get("https://r.jina.ai/" + u, headers=h, timeout=180).text

page = get("https://www.miele.dk/product/11541630/fritstaende-espressomaskine-cm-5410-silence-obsidiansort")
ds = get("https://media.miele.com/downloads/k-/da/FS_11541630_DKD_DK-da.pdf")
for label, t in [("PAGE", page), ("DATASHEET", ds)]:
    print("=====", label, "len", len(t))
    for kw in ["Højde", "Bredde", "Dybde", "Mål", "Dimension", "Vandbeholder", "højde"]:
        i = t.find(kw)
        print(" ", kw, "->", repr(t[i - 3:i + 45]) if i >= 0 else "NOT FOUND")
    ms = re.findall(r".{0,22}\bmm\b", t)
    print("  mm-ctx:", [m.strip() for m in ms[:6]])
    cm = re.findall(r".{0,18}\bcm\b", t)
    print("  cm-ctx:", [m.strip() for m in cm[:6]])
