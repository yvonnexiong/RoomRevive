"""
Nobilia front -> seamless PBR texture pipeline.

Stages per material (see materials.json):
  1. GENERATE  - gpt-image-1 edit endpoint, driven by a clean reference crop,
                 asked to produce a larger, evenly-lit, tileable swatch.
  2. SEAMLESS  - cross-blend with a half-shifted copy so opposite edges match
                 exactly (guaranteed tiling). Good for lacquer/concrete/structured;
                 woodgrain gets a softer result - see README.
  3. COLOR FIX - shift the swatch mean toward the material's target hex.
  4. UPSCALE   - Lanczos to the requested size (swap in Real-ESRGAN later).
  5. PBR       - derive _normal and _roughness from the albedo.

Outputs land in output/ as:
  <num>_<Name>_albedo.png  <num>_<Name>_normal.png  <num>_<Name>_roughness.png

Run:
  python generate_fronts.py            # all materials in materials.json
  python generate_fronts.py 412        # just material number 412
  python generate_fronts.py --no-api   # skip OpenAI; re-process existing albedo

Needs OPENAI_API_KEY in the environment (or a .openai_key file next to this script).
"""

import argparse
import json
import os
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).parent
REF_DIR = ROOT / "references"
OUT_DIR = ROOT / "output"
WORK_DIR = ROOT / "work"
OUT_SIZE = 2048            # final albedo edge length (px). Bump to 4096 if you upscale.
GEN_SIZE = 1024           # gpt-image-1 generation size (512/1024/1536 square)
SEAM_BAND = 0.5            # fraction of the image cross-blended for seamlessness


# ---------------------------------------------------------------- key loading
def load_api_key():
    key = os.environ.get("OPENAI_API_KEY")
    if key:
        return key.strip()
    keyfile = ROOT / ".openai_key"
    if keyfile.exists():
        return keyfile.read_text(encoding="utf-8").strip()
    return None


# ---------------------------------------------------------------- stage 1: gen
def generate(material, key):
    """Call gpt-image-1 edit with the reference crop. Returns a PIL RGB image."""
    from openai import OpenAI

    ref_path = ROOT / material["ref"]
    if not ref_path.exists():
        raise FileNotFoundError(
            f"Reference crop missing: {ref_path}\n"
            f"  Drop a clean head-on crop (no text/logo/handle) there first."
        )

    client = OpenAI(api_key=key)
    prompt = (
        f"A flat, head-on photograph of a {material.get('finish','matte')} "
        f"kitchen cabinet front surface in the exact same colour and material as the "
        f"reference image (Nobilia '{material['name']}'). Evenly lit, no shadows, no "
        f"vignette, no perspective, no handles, no hardware, no text or logos. "
        f"A continuous, seamlessly tileable material texture that repeats with no visible edges."
    )
    with open(ref_path, "rb") as f:
        resp = client.images.edit(
            model="gpt-image-1",
            image=f,
            prompt=prompt,
            size=f"{GEN_SIZE}x{GEN_SIZE}",
        )
    import base64
    raw = base64.b64decode(resp.data[0].b64_json)
    tmp = WORK_DIR / f"{material['number']}_gen.png"
    tmp.write_bytes(raw)
    return Image.open(tmp).convert("RGB")


# ------------------------------------------------------------ stage 2: seamless
def make_seamless(img, band=SEAM_BAND):
    """Cross-blend the image with a half-shifted copy so edges wrap exactly."""
    a = np.asarray(img).astype(np.float32)
    h, w = a.shape[:2]
    rolled = np.roll(np.roll(a, w // 2, axis=1), h // 2, axis=0)

    # Edge-only blend: alpha=1 across the whole interior (kept crisp, no ghosting),
    # dipping to 0 only within a band of `band` (fraction) of each edge. There the
    # half-shifted copy fills in - and because rolled[edge] == interior of the
    # opposite side, the result wraps continuously. Interior detail is untouched.
    def edge_ramp(n):
        b = max(1, int(n * band))
        d = np.minimum(np.arange(n), n - 1 - np.arange(n))  # distance to nearest edge
        r = np.clip(d / b, 0, 1)
        return (r * r * (3 - 2 * r))  # smoothstep

    rx = edge_ramp(w)[None, :, None]
    ry = edge_ramp(h)[:, None, None]
    alpha = np.minimum(rx, ry)  # blend if near EITHER edge
    out = a * alpha + rolled * (1 - alpha)
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8))


# ----------------------------------------------------------- stage 3: color fix
def color_correct(img, hex_target):
    if not hex_target:
        return img
    tgt = np.array([int(hex_target[i:i + 2], 16) for i in (1, 3, 5)], dtype=np.float32)
    a = np.asarray(img).astype(np.float32)
    mean = a.reshape(-1, 3).mean(axis=0)
    shifted = a + (tgt - mean)
    return Image.fromarray(np.clip(shifted, 0, 255).astype(np.uint8))


# -------------------------------------------------------------- stage 4: upscale
def upscale(img, size=OUT_SIZE):
    return img.resize((size, size), Image.LANCZOS)


# ----------------------------------------------------------------- stage 5: PBR
def to_luminance(img):
    a = np.asarray(img).astype(np.float32) / 255.0
    return 0.299 * a[..., 0] + 0.587 * a[..., 1] + 0.114 * a[..., 2]


def make_normal(img, strength=2.0):
    lum = to_luminance(img)
    # wrap-mode gradients keep the normal map tileable too.
    gx = np.roll(lum, -1, axis=1) - np.roll(lum, 1, axis=1)
    gy = np.roll(lum, -1, axis=0) - np.roll(lum, 1, axis=0)
    nx, ny = -gx * strength, -gy * strength
    nz = np.ones_like(lum)
    n = np.stack([nx, ny, nz], axis=-1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    rgb = ((n * 0.5 + 0.5) * 255).astype(np.uint8)
    return Image.fromarray(rgb)


def make_roughness(img, base):
    lum = to_luminance(img)
    # subtle per-pixel variation around the material's base roughness.
    var = (lum - lum.mean()) * 0.15
    r = np.clip(base + var, 0, 1)
    return Image.fromarray((r * 255).astype(np.uint8))


# ------------------------------------------------------------------- per-material
def process(material, key, mode):
    """mode: 'api'   -> generate via gpt-image-1 from the reference crop
            'ref'   -> use the reference crop directly as the albedo base (no API)
            'redo'  -> re-derive normal/roughness from an existing albedo
    """
    num, name = material["number"], material["name"]
    stem = f"{num}_{name.replace(' ', '_')}"
    albedo_path = OUT_DIR / f"{stem}_albedo.png"

    if mode == "api":
        print(f"  [{num}] generating from {material['ref']} ...")
        img = make_seamless(generate(material, key))
        img = upscale(color_correct(img, material.get("hex")))
        img.save(albedo_path)
    elif mode == "ref":
        ref = ROOT / material["ref"]
        if not ref.exists():
            print(f"  [{num}] no reference crop; skipping.")
            return
        img = upscale(color_correct(make_seamless(Image.open(ref).convert("RGB")),
                                    material.get("hex")))
        img.save(albedo_path)
    else:  # redo
        if not albedo_path.exists():
            print(f"  [{num}] no existing albedo; skipping.")
            return
        img = Image.open(albedo_path).convert("RGB")

    make_normal(img).save(OUT_DIR / f"{stem}_normal.png")
    make_roughness(img, material.get("roughness_base", 0.6)).save(
        OUT_DIR / f"{stem}_roughness.png"
    )
    print(f"  [{num}] {name} -> albedo / normal / roughness done")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("only", nargs="*", help="material number(s), e.g. 412 334")
    ap.add_argument("--mode", choices=("api", "ref", "redo"), default="ref",
                    help="api=gpt-image-1 gen, ref=use crop directly (default, no API), "
                         "redo=re-derive normal/roughness from existing albedo")
    args = ap.parse_args()

    for d in (REF_DIR, OUT_DIR, WORK_DIR):
        d.mkdir(parents=True, exist_ok=True)

    mats = json.loads((ROOT / "materials.json").read_text(encoding="utf-8"))["materials"]
    if args.only:
        mats = [m for m in mats if m["number"] in set(args.only)]
        if not mats:
            sys.exit(f"No material(s) {args.only} in materials.json")

    key = load_api_key() if args.mode == "api" else None
    if args.mode == "api" and not key:
        sys.exit("No OPENAI_API_KEY in env and no .openai_key file.")

    print(f"Processing {len(mats)} material(s) in '{args.mode}' mode:")
    for m in mats:
        try:
            process(m, key, args.mode)
        except Exception as e:
            print(f"  [{m['number']}] FAILED: {e}")


if __name__ == "__main__":
    main()
