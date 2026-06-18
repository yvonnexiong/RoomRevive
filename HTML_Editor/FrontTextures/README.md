# Nobilia Front → Seamless PBR Texture Pipeline

Generates large, seamless, tileable textures for Nobilia kitchen **fronts**, matched in
colour/material to the real finishes, for projection onto parts of a Gaussian splat.

Outputs a 3-map PBR set per front (`_albedo`, `_normal`, `_roughness`), named like the
`WorktopMaterials` folder: `412_Macchiato_albedo.png`.

## Why reference crops (not text prompts)
Exact colour match ("Macchiato" not "a beige") is the whole point, so each material is
driven by a **clean reference crop** fed to gpt-image-1's *edit* endpoint. The crop is a
head-on swatch with **no text, logo, or handle** — same style as `WorktopMaterials/*.jpg`.

## Setup
```powershell
pip install -r requirements.txt
# provide your key (either works):
#   setx OPENAI_API_KEY "sk-..."     # then restart the shell
#   OR drop the key in a file named  .openai_key  next to this README (gitignored)
```

## Use
1. Add a clean crop per front to `references/`, e.g. `references/412_Macchiato.jpg`.
2. Add/confirm the entry in `materials.json` (number, name, target `hex`, `finish`,
   `roughness_base`).
3. Run:
   ```powershell
   python generate_fronts.py 412      # one material
   python generate_fronts.py          # everything in materials.json
   python generate_fronts.py --no-api # re-derive normal/roughness from existing albedo
   ```

## Pipeline stages
1. **Generate** — gpt-image-1 edit, reference-guided, asked for a tileable swatch.
2. **Seamless** — cross-blend with a half-shifted copy so opposite edges match exactly.
3. **Colour-correct** — shift mean colour to the material's target `hex`.
4. **Upscale** — Lanczos to `OUT_SIZE` (2048; raise to 4096 or swap in Real-ESRGAN).
5. **PBR** — `_normal` (wrap-mode Sobel) and `_roughness` (per-material base + variation).

## Quality notes / knobs (top of `generate_fronts.py`)
- `OUT_SIZE`, `GEN_SIZE`, `SEAM_BAND` are tunable.
- The seamless cross-blend is **excellent for uniform finishes** (lacquer, concrete,
  structured matte) and **softer for strong woodgrain** — the blend can ghost the grain.
  For woodgrain fronts, prefer a higher-res reference and consider an inpaint-based seam
  heal later (noted as a future step).
- Normal/roughness are *derived* from albedo — fast and good enough for splat projection.
  If the splat shader wants true measured maps, swap in a height-from-photo step.
