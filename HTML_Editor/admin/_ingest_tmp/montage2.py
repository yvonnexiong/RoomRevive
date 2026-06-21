from PIL import Image
d = r"C:\Unity-Git\RoomRevive\HTML_Editor\admin\_ingest_tmp"
imgs = [d + r"\qc3_front.png", d + r"\q_v2_front.png", d + r"\cad\11541580_NER_CM5310_OBSW.png"]
H = 400
loaded = []
for p in imgs:
    im = Image.open(p).convert("RGB")
    w = int(im.width * H / im.height)
    loaded.append(im.resize((w, H)))
gap = 16
W = sum(i.width for i in loaded) + gap * (len(loaded) + 1)
out = Image.new("RGB", (W, H + 2 * gap), (245, 245, 245))
x = gap
for im in loaded:
    out.paste(im, (x, gap)); x += im.width + gap
out.save(d + r"\compare2.png")
print("saved", out.size)
