# RoomRevive — Selection Output Contract

The Selection Core takes the four answers + `catalog.json` and emits one JSON
payload. The visualizer renders the right side from it.

**Design rule:** output items are **references**, not copies. Each product is
stored only as its catalog `id`. Everything visible about a product
(name, brand, finish, price, images, frontCode, worktopCode…) is looked up from
`catalog.json` by that id. The payload carries only what the catalog does **not**
contain — the runtime-computed fields (counts, match strength, why-text, relaxed
flags, the derived profile).

## Reference key

- **`id`** (top-level catalog item id, 16-hex) — the canonical reference. Present
  on every item, unique, resolves the whole record. This is all the output stores
  per product.
- `modelKey` — *not stored in the output.* It exists as a product field for
  appliances but **not for kitchens** (favorites.json derives `TOUCH_337` from the
  kitchen's name), so it isn't a universal key. The visualizer can read it from
  the resolved catalog record if it needs a human-readable handle.

This mirrors `favorites.json`, which keys by `id` (+ a convenience `modelKey`);
the output keeps just the `id`.

---

## Input

```json
{ "style": "modern", "tone": "light", "household": "standard", "budget": "Signature" }
```

| field | values |
|---|---|
| style | `modern` · `designer` · `cottage style` · `natural & scandinavian` |
| tone | `light` · `dark` · `wood` · `bold` |
| household | `compact` · `standard` · `host` |
| budget | `Essential` · `Signature` · `Premium` · `any` |

---

## Output

```json
{
  "answers": { "style": "...", "tone": "...", "household": "...", "budget": "..." },

  "profile": {
    "intent": "Fast & Focused",
    "tagline": "bright, efficient — in, fed, and out",
    "cabinetDirection": "Nobilia · Modern / Handleless — handleless slab fronts, light matte",
    "applianceFinish": "white, clean steel — bright, neutral",
    "lighting": "neutral bright · 3500–4000 K",
    "tags": ["light & airy","clean lines","family","flow","considered"]
  },

  "kitchens": {
    "count": 12,
    "budgetRelaxed": false,
    "shortlist": [ { "id": "698ecf189cbe241c" }, { "id": "e92c380f55b079ad" } ]
  },

  "appliances": [
    {
      "category": "Fridges",
      "count": 6,
      "relaxed": false,
      "matchStrength": 3,
      "why": "Why: finish matches your palette · sized for the household · within budget.",
      "topPick": { "id": "531c55b910e270b8" },
      "options": [ "531c55b910e270b8", "a74e3b4450166891", "ec74e0e9e03c013a" ]
    }
  ]
}
```

### Field notes (all computed — not in catalog)

**profile** — derived from the answers via lookup tables (intent/tagline by style,
lighting by intent, applianceFinish by tone, tags from all four). See
SELECTION_LOGIC for the tables.

**kitchens**
- `count` — kitchens after the hard style+palette filter (+ soft budget).
- `budgetRelaxed` — `true` when no kitchen matched the chosen tier so budget was
  dropped; the visualizer shows the "closest in price" note.
- `shortlist` — ranked `id` references (cap the array length as you like).

**appliances[]** — one entry per category, always present, never empty:
- `count` — options after size+budget+palette, floored (Fridges ≥2, others ≥1).
- `relaxed` — `true` when the floor forced a constraint to drop (palette→budget→size).
- `matchStrength` — 0–3 (size + palette + budget hits) → the dot indicator.
- `why` — generated rationale string for the top pick.
- `topPick` — `id` reference to the best-scored option.
- `options` — ranked `id` references (cap length to taste, e.g. top 8).

---

## Resolving references (visualizer side)

```
catalogById = index catalog.json items by item.id      // build once
record      = catalogById[ ref.id ]                     // full product record
name        = record.name
finish      = record.product.color
priceTier   = record.product.priceTier
frontCode   = record.product.frontCode                 // kitchens → swatch image
worktopCode = record.product.worktopCode               // kitchens → paired worktop
images      = record.product.heroImage / additionalImages
modelKey    = record.product.modelKey                  // appliances only
```

So the visualizer needs **two inputs**: this payload + `catalog.json`. The payload
says *which* items and *why*; the catalog says *what they are*.

A real, runnable example payload is in `sample_output.json`.
