"""Discover products in a Miele category page (JS-rendered -> jina default/markdown mode)."""
import sys, re, requests

H = {"User-Agent": "Mozilla/5.0"}

def jina(url, html=False):
    h = dict(H)
    if html:
        h["X-Return-Format"] = "html"
    return requests.get("https://r.jina.ai/" + url, headers=h, timeout=180).text

url = sys.argv[1]
t = jina(url, html=False)
print("LEN", len(t))
seen = {}
for pid, slug in re.findall(r"/product/(\d+)/([a-z0-9\-]+)", t):
    seen.setdefault(pid, slug)
print("PRODUCTS", len(seen))
for pid, slug in seen.items():
    print(pid + "\t" + slug)
