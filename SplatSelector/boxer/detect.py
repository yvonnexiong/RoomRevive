"""OWLv2 open-vocabulary detection (Boxer's 2D detector stage).

Prompts each rendered view with refrigerator/fridge text queries and records
2D boxes. Saves detections.json and annotated previews for hits.
"""
import json, os, time
import truststore; truststore.inject_into_ssl()  # verify via Windows cert store (TLS proxy)
import numpy as np
import torch
from PIL import Image, ImageDraw
from transformers import Owlv2Processor, Owlv2ForObjectDetection

PROMPTS = ["a refrigerator", "a fridge", "a stainless steel refrigerator"]
THRESH = 0.12
MODEL = "google/owlv2-base-patch16-ensemble"

cams = json.load(open("cameras.json"))["cams"]
proc = Owlv2Processor.from_pretrained(MODEL)
model = Owlv2ForObjectDetection.from_pretrained(MODEL)
model.eval()
torch.set_num_threads(os.cpu_count())

os.makedirs("dets", exist_ok=True)
results = []
t0 = time.time()
for ci, cam in enumerate(cams):
    path = os.path.join("frames", cam["name"] + ".png")
    img = Image.open(path).convert("RGB")
    inputs = proc(text=[PROMPTS], images=img, return_tensors="pt")
    with torch.no_grad():
        out = model(**inputs)
    ts = torch.tensor([img.size[::-1]])
    r = proc.post_process_object_detection(out, threshold=THRESH, target_sizes=ts)[0]
    boxes = r["boxes"].tolist(); scores = r["scores"].tolist(); labels = r["labels"].tolist()
    dets = [dict(box=b, score=float(sc), label=PROMPTS[l])
            for b, sc, l in zip(boxes, scores, labels)]
    # keep top 3 per view
    dets = sorted(dets, key=lambda d: -d["score"])[:3]
    results.append(dict(name=cam["name"], dets=dets))
    if dets:
        d = ImageDraw.Draw(img)
        for det in dets:
            x0, y0, x1, y1 = det["box"]
            d.rectangle([x0, y0, x1, y1], outline=(255, 40, 160), width=3)
            d.text((x0 + 3, y0 + 3), f"{det['score']:.2f}", fill=(255, 255, 0))
        img.save(os.path.join("dets", cam["name"] + ".png"))
    print(f"[{ci+1}/{len(cams)}] {cam['name']}: "
          f"{', '.join(f'{x['score']:.2f}' for x in dets) or '-'}  ({time.time()-t0:.0f}s)")

json.dump(results, open("detections.json", "w"))
hits = sum(1 for r in results if r["dets"])
print(f"done: {hits}/{len(results)} views with detections in {time.time()-t0:.0f}s")
