"""Master-review evidence: our GLB render vs the real Miele photo, side by side."""
from PIL import Image
d = r"C:\Unity-Git\RoomRevive\HTML_Editor\admin\_ingest_tmp"
pairs = [
    ("obsidian", d + r"\qc3_front.png",      d + r"\cad\11541580_NER_CM5310_OBSW.png"),
    ("white",    d + r"\q_white_front.png",  d + r"\cad_6160\11579650_NER_CM6160_LOWS.png"),
    ("graphite", d + r"\q_graphite2_front.png", d + r"\cad_6560\11579720_NER_CM6560_GRPF.png"),
]
H = 340
def load(p):
    im = Image.open(p).convert("RGB")
    w = int(im.width * H / im.height)
    return im.resize((w, H))
rows = []
for _, mp, pp in pairs:
    m = load(mp); p = load(pp)
    row = Image.new("RGB", (m.width + p.width + 24, H), (245, 245, 245))
    row.paste(m, (0, 0)); row.paste(p, (m.width + 24, 0))
    rows.append(row)
W = max(r.width for r in rows)
out = Image.new("RGB", (W, sum(r.height for r in rows) + 24), (245, 245, 245))
y = 0
for r in rows:
    out.paste(r, (0, y)); y += r.height + 12
out.save(d + r"\compare.png")
print("saved", out.size)
