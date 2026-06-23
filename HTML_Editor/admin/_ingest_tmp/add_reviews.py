"""Add 3 mock reviews (+ aggregate) to every non-Kitchens catalog item via the admin API."""
import json, urllib.request, time
ADMIN = "http://localhost:4173"
NAMES = [("Mette", "Denmark"), ("Julien", "France"), ("Petra", "Austria"), ("Sara", "Italy"),
         ("Niklas", "Sweden"), ("Eva", "Germany"), ("Annika", "Sweden"), ("Paolo", "Italy"),
         ("Lea", "Germany"), ("Sofie", "Denmark"), ("Lars", "Norway"), ("Ingrid", "Norway"),
         ("Tom", "Netherlands"), ("Clara", "Belgium"), ("Hugo", "Spain"), ("Marta", "Poland")]
DATES = ["14 May 2026", "2 Apr 2026", "18 Mar 2026", "9 May 2026", "21 Apr 2026", "3 Mar 2026",
         "16 May 2026", "28 Apr 2026", "11 Mar 2026", "7 May 2026", "19 Apr 2026", "2 Mar 2026",
         "23 May 2026", "5 Apr 2026", "27 Feb 2026", "12 May 2026"]
RATING_SETS = [[5, 5, 4], [5, 4, 5], [4, 5, 5], [5, 5, 5], [5, 4, 4], [4, 5, 4]]
BODIES = [
    "Works perfectly and looks great in our kitchen. Very happy with it.",
    "Excellent quality - exactly as described. Would buy from this brand again.",
    "Reliable and quiet in use. A premium feel for the price.",
    "Easy to use and easy to clean. No complaints at all.",
    "Does the job beautifully. Delivery took a couple of weeks but worth the wait.",
    "Great build quality and a lovely finish. Highly recommend.",
    "Exactly what we needed. Simple to install and works flawlessly.",
    "Sleek design and very efficient. The family loves it.",
]


def reviews_for(seed):
    off = sum(ord(c) for c in seed)
    rs = RATING_SETS[off % len(RATING_SETS)]
    out = []
    for k in range(3):
        nm, ct = NAMES[(off + k) % len(NAMES)]
        out.append({"rating": rs[k], "author": nm, "country": ct,
                    "date": DATES[(off + k) % len(DATES)],
                    "body": BODIES[(off + k) % len(BODIES)]})
    score = round(sum(rs) / 3 * 10) / 10
    return out, score, round(score * 2) / 2


def main():
    data = json.load(urllib.request.urlopen(ADMIN + "/api/catalog"))
    upd = skip = fail = 0
    for it in data["items"]:
        if it.get("category") == "Kitchens":
            continue
        p = it.get("product") or {}
        if isinstance(p.get("reviews"), list) and p["reviews"]:
            skip += 1; continue
        revs, score, stars = reviews_for(it["id"])
        body = json.dumps({"product": {"reviews": revs, "reviewScore": score,
                                       "reviewStars": stars, "reviewCount": len(revs)}}).encode()
        req = urllib.request.Request(ADMIN + "/api/items/" + it["id"], data=body,
                                     headers={"Content-Type": "application/json"}, method="PUT")
        try:
            with urllib.request.urlopen(req) as r:
                if r.status == 200:
                    upd += 1
            time.sleep(0.06)  # throttle: each PUT rewrites the whole catalog.json
        except urllib.error.HTTPError as e:
            fail += 1
            print("FAIL %s / %s -> %s %s" % (it.get("category"), (it.get("name") or "")[:40],
                                             e.code, e.read()[:160]))
        except Exception as e:
            fail += 1
            print("ERR  %s -> %r" % ((it.get("name") or "")[:40], e))
    print("updated=%d  skipped(already had reviews)=%d  failed=%d" % (upd, skip, fail))


if __name__ == "__main__":
    main()
