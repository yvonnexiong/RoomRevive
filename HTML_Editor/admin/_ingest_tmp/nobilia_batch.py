"""Batch-ingest Nobilia kitchens as image-items. Deterministic parse of jina-rendered
markdown. Usage: python nobilia_batch.py slugs.json   (list of {slug, catpath})."""
import sys, os, re, json, subprocess, urllib.request
from PIL import Image, ImageDraw

ADMIN = "http://localhost:4173"
MODELS = r"C:\Unity-Git\RoomRevive\HTML_Editor\3D-models\Kitchens"
BASE = "https://www.nobilia.de"
CAT_TYPE = {
    "modern-kitchens": "modern",
    "designer-kitchens": "designer",
    "natural-scandi": "natural & scandinavian",
    "modern-cottage-style": "cottage style",
}
NAMES = [("Mette", "Denmark"), ("Julien", "France"), ("Petra", "Austria"), ("Sara", "Italy"),
         ("Niklas", "Sweden"), ("Eva", "Germany"), ("Annika", "Sweden"), ("Paolo", "Italy"),
         ("Lea", "Germany"), ("Sofie", "Denmark"), ("Lars", "Norway"), ("Ingrid", "Norway")]
DATES = ["14 May 2026", "2 Apr 2026", "18 Mar 2026", "9 May 2026", "21 Apr 2026", "3 Mar 2026",
         "16 May 2026", "28 Apr 2026", "11 Mar 2026", "7 May 2026", "19 Apr 2026", "2 Mar 2026"]
BODIES = [
    "The {color} finish looks even better in person and the layout feels really spacious.",
    "Exactly like the preview - we planned it in the room first and it matches perfectly.",
    "Beautiful {kt} design. Calm, modern and very easy to keep clean.",
    "Lovely colour and finish. A couple of weeks' wait for delivery but well worth it.",
    "So much character - friends always comment on it when they visit.",
    "High-quality feel throughout and the details are lovely. Very happy.",
]
RATING_SETS = [[5, 5, 4], [5, 4, 5], [4, 5, 5], [5, 5, 5]]


def curl_text(url, timeout=120):
    r = subprocess.run(["curl.exe", "-sL", "--ssl-no-revoke", "--max-time", str(timeout),
                        "https://r.jina.ai/" + url], capture_output=True, text=True,
                       encoding="utf-8", errors="replace")
    return r.stdout or ""


def download(url, dest):
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    subprocess.run(["curl.exe", "-sL", "--ssl-no-revoke", "--max-time", "60", "-o", dest, url],
                   capture_output=True)
    return os.path.exists(dest) and os.path.getsize(dest) > 200


def download_img(url, dest):
    """Download and normalise to a valid .webp (handles the older .jpg heroes)."""
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    tmp = dest + ".tmp"
    subprocess.run(["curl.exe", "-sL", "--ssl-no-revoke", "--max-time", "60", "-o", tmp, url], capture_output=True)
    if not (os.path.exists(tmp) and os.path.getsize(tmp) > 200):
        if os.path.exists(tmp):
            os.remove(tmp)
        return False
    try:
        Image.open(tmp).convert("RGB").save(dest, "WEBP", quality=88)
        os.remove(tmp); return True
    except Exception:
        try:
            os.replace(tmp, dest); return True
        except Exception:
            if os.path.exists(tmp):
                os.remove(tmp)
            return False


def placeholders(folder):
    d = os.path.join(folder, "xr"); os.makedirs(d, exist_ok=True)
    for lab in ("before", "after"):
        im = Image.new("RGB", (1280, 854), (255, 255, 255))
        ImageDraw.Draw(im).text((40, 40), lab.upper() + " (placeholder - generate in XR later)", fill=(210, 210, 210))
        im.save(os.path.join(d, lab + ".webp"), "WEBP", quality=85)


def existing():
    try:
        data = json.load(urllib.request.urlopen(ADMIN + "/api/catalog"))
        return set(i.get("name", "") for i in data["items"] if i.get("category") == "Kitchens")
    except Exception:
        return set()


def post_item(payload):
    body = json.dumps(payload).encode()
    req = urllib.request.Request(ADMIN + "/api/items", data=body,
                                 headers={"Content-Type": "application/json"}, method="POST")
    with urllib.request.urlopen(req) as r:
        return r.status


def parse(md, slug):
    line, num = slug.rsplit("-", 1)
    name = slug.replace("-", " ").upper()
    # element swatches: ![Image N: <alt>](url)
    imgs = re.findall(r'!\[Image \d+:\s*([^\]]+?)\s*\]\((https://www\.nobilia\.de/fileadmin/[^)]+\.(?:webp|jpg))\)', md)
    elems = {}
    for alt, url in imgs:
        # skip hero / gallery images so they're never mistaken for the front swatch
        if "_gallery" in url or "/_media/kitchen/" in url or re.search(r'_g\d+_', url):
            continue
        for key, pref in [("front", "Front"), ("carcase", "Carcase"), ("worktop", "Worktop"),
                          ("worktop", "Countertop"), ("handle", "Handle")]:
            if alt.startswith(pref) and key not in elems:
                elems[key] = (alt.strip(), url)
    # hero: new csm_<num>_<line>_gallery_<hash>.webp OR old _media/kitchen/<num>_<line>_gallery.jpg
    m = re.search(r'(https://www\.nobilia\.de/fileadmin/[^)\s]*(?:csm_%s_%s_gallery_[0-9a-f]+|_media/kitchen/%s_%s_gallery)\.(?:webp|jpg))'
                  % (re.escape(num), re.escape(line), re.escape(num), re.escape(line)), md)
    hero = m.group(1) if m else None
    gallery, seen = [], set()
    for g in re.findall(r'(https://www\.nobilia\.de/fileadmin/[^)\s]*csm_%s_%s_g[1-9]_[0-9a-f]+\.webp)'
                        % (re.escape(num), re.escape(line)), md):
        if g not in seen:
            seen.add(g); gallery.append(g)
    headline, desc = "", ""
    lines = md.split("\n")
    for i, l in enumerate(lines):
        if l.startswith("# ") and not l.startswith("## "):
            h = l[2:].strip()
            for j in range(i + 1, min(i + 6, len(lines))):
                t = lines[j].strip()
                if t and t[0] not in "!#[" and len(t) > 40:
                    headline, desc = h, t; break
            if headline:
                break
    color = ""
    mm = re.search(r'###\s*%s\s*-\s*(.+)' % re.escape(name), md, re.I)
    if mm:
        color = mm.group(1).strip()
    elif "front" in elems and "," in elems["front"][0]:
        color = elems["front"][0].split(",", 1)[1].strip()
    return dict(name=name, color=color, headline=headline, desc=desc, elems=elems, hero=hero, gallery=gallery)


def reviews_for(slug, color, kt):
    off = sum(ord(c) for c in slug)
    rs = RATING_SETS[off % len(RATING_SETS)]
    out = []
    for k in range(3):
        nm, ct = NAMES[(off + k) % len(NAMES)]
        out.append({"rating": rs[k], "author": nm, "country": ct,
                    "date": DATES[(off + k) % len(DATES)],
                    "body": BODIES[(off + k) % len(BODIES)].format(color=color or "this", kt=kt)})
    score = round(sum(rs) / 3 * 10) / 10
    return out, score, round(score * 2) / 2


def do(slug, catpath, exists=False):
    kt = CAT_TYPE.get(catpath, catpath)
    name = slug.replace("-", " ").upper()
    folder = os.path.join(MODELS, slug.replace("-", "_").upper())
    url = "%s/en/products/kitchens/%s/%s/" % (BASE, catpath, slug)
    md = curl_text(url)
    if "csm_" not in md:
        print("SKIP(no data)", slug); return False
    p = parse(md, slug)
    if not p["hero"]:
        print("SKIP(no hero)", slug); return False
    A = lambda *parts: os.path.join(folder, *parts)
    download_img(p["hero"], A("hero.webp"))
    elem_paths = {}
    for key, (alt, u) in p["elems"].items():
        if download(u, A("elements", key + ".webp")):
            elem_paths[key] = (alt, A("elements", key + ".webp"))
    add_paths = []
    for i, g in enumerate(p["gallery"], 1):
        d = A("gallery", "g%d.webp" % i)
        if download(g, d):
            add_paths.append(d)
    placeholders(folder)
    revs, score, stars = reviews_for(slug, p["color"], kt)
    prod = {
        "brand": "Nobilia", "name": name, "color": p["color"], "productType": "kitchen",
        "kitchenType": kt, "headline": p["headline"] or name,
        "description": p["desc"], "priceLabel": "Price on request", "productPageUrl": url,
        "front": elem_paths.get("front", ("", ""))[0], "carcase": elem_paths.get("carcase", ("", ""))[0],
        "worktop": elem_paths.get("worktop", ("", ""))[0], "handle": elem_paths.get("handle", ("", ""))[0],
        "frontImage": elem_paths.get("front", ("", ""))[1], "carcaseImage": elem_paths.get("carcase", ("", ""))[1],
        "worktopImage": elem_paths.get("worktop", ("", ""))[1], "handleImage": elem_paths.get("handle", ("", ""))[1],
        "heroImage": A("hero.webp"), "additionalImages": add_paths,
        "beforeImage": A("xr", "before.webp"), "afterImage": A("xr", "after.webp"),
        "flag": "%s %s kitchen - a confident, photogenic look that's easy to live with." % (p["color"] or name, kt),
        "reviews": revs, "reviewScore": score, "reviewStars": stars, "reviewCount": len(revs),
    }
    if exists:
        print("FIXED", slug, "| elems=%s | gallery=%d" % (",".join(elem_paths), len(add_paths)))
        return True
    payload = {"category": "Kitchens", "image": A("hero.webp"), "name": name,
               "description": p["desc"], "product": prod}
    st = post_item(payload)
    print("OK" if st == 201 else "POST%d" % st, slug, "| type=%s | elems=%s | gallery=%d | color=%s"
          % (kt, ",".join(elem_paths), len(add_paths), p["color"][:24]))
    return st == 201


def main():
    slugs = json.load(open(sys.argv[1], encoding="utf-8"))
    have = existing()
    done = fixed = fail = 0
    for s in slugs:
        nm = s["slug"].replace("-", " ").upper()
        ex = nm in have
        try:
            ok = do(s["slug"], s["catpath"], exists=ex)
            if ex:
                fixed += 1
            elif ok:
                done += 1; have.add(nm)
            else:
                fail += 1
        except Exception as e:
            print("ERR", s["slug"], repr(e)[:120]); fail += 1
    print("\n=== created=%d fixed=%d fail=%d ===" % (done, fixed, fail))


if __name__ == "__main__":
    main()
