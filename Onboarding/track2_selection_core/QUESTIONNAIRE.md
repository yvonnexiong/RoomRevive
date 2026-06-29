# RoomRevive — Onboarding Questionnaire Spec

Four questions narrow every item in the build. Designed for XR: short, tappable,
no typing. Two questions are **universal axes** (touch every category via fields
present on all of them); two are **category-shaped** (drive the cabinet system
and appliance capacity).

| Axis | Question | Universal? | Primary field(s) |
|---|---|---|---|
| Style / intent | Q1 | cabinet-shaped | `kitchenType`, `tone` |
| Palette | Q2 | **universal** | `color` / finish (every category) |
| Household | Q3 | capacity-shaped | size / capacity per category |
| Budget | Q5 | **universal** | `priceTier` (from `price`, every category) |

> Q4 (fridge "how you stock up") was removed — the fridge is settled by
> size/household. Numbering keeps Q5 for the budget question.

---

## The four questions (copy)

### Q1 — "When this kitchen feels right, what's the vibe?"  → `kitchenType` + `tone`
- Clean and uncluttered → **modern** (36)
- A statement with presence → **designer** (25)
- Warm and lived-in → **cottage** (15)
- Calm, natural, light woods → **scandinavian** (7)

Maps to intent: modern ≈ Fast & Focused · designer ≈ Host & Gather ·
cottage/scandi ≈ Calm & Unwind.

### Q2 — "Which palette feels like home?"  → `tone`, then finish-match on every item
- Light and airy → **light** → White · Clean Steel · Stainless · Brilliant White
- Dark and dramatic → **dark** → Obsidian Black · Black Steel · Graphite
- Warm wood → **wood** → Steel Look / warm neutral
- A pop of colour → **bold** → accent finishes (e.g. coffee machine reds, rose gold)

This is the thread that ties the whole kitchen together — one answer sets the
finish on the cabinets AND every appliance.

### Q3 — "Who's it for?"  → capacity, per category
- Just me / a pair → **compact**
- A busy household → **standard**
- I love to host → **max**

### Q5 — "Where do you want to invest?"  → `priceTier`, every category *(skippable)*
- Keep it smart and simple → **Essential**
- Balanced — quality where it shows → **Signature**
- Go premium, this is the heart of the home → **Premium**
- Show me everything → **skip (no budget filter)**

Soft framing keeps the live-how-you-want tone. Skippable because budget is the
question most likely to feel transactional.

---

## How each question narrows each category

### Kitchens (83) — the cabinet system
- Q1 `kitchenType` → 83 → 7–36
- Q2 `tone` → light 36 / dark 19 / wood 13 / bold 13
- Q5 `priceTier` → Essential 28 / Signature 26 / Premium 29
- Lands at a handful; the chosen kitchen carries `frontCode` (its cabinet front)
  and `worktopCode` (its **slaved** worktop pairing) into the configurator.

### Fridges (29)
- Q3 household → width + capacity: compact (≤220 L) · standard (60 cm, ≥250 L) ·
  host (91 cm French-door)
- Q2 finish-match → White/Clean Steel (light) · Black Steel/Obsidian (dark) ·
  Steel Look (wood)
- Q5 `priceTier` → Essential 10 / Signature 10 / Premium 8

### Dishwashers (5)
- Q3 → `placeSettings` 9–14 + width 45/60 cm: compact = 45 cm/9 · standard/host = 60 cm/14
- Q2 → White (light) · Clean Steel (steel/dark)
- Q5 → price €1,004–1,299

### Cooktops (23)
- Q3 → `zones` 0–5 + width: compact = 2–3 zones · standard = 4 · host = 5 / flex
- Q2 → palette is narrow here (Black / Black+steel frame / Stainless) — finish-match
  mostly confirms dark vs steel rather than splitting hard
- Q5 → price €870–2,050

### Hoods (50)
- Q3 → width, matched to the cooktop width
- Q2 → Stainless (light/steel) · Obsidian Black (dark) · Brilliant White / Pearlbeige (light)
- Q5 → price €535–4,530 (widest spread — strong budget cut)
- Bonus: `noise` 61–74 dB → quietest hoods surface for the Calm/scandi intent

### Microwaves (12)
- Q3 → capacity 17 L (compact) vs 26 L (standard/host)
- Q2 → Brilliant White / Clean Steel / Stainless (light) · Obsidian Black / Graphite (dark)
- Q5 → price €803–2,050

### CoffeeMachines (20)
- Q1 intent → **featured** for Calm & Unwind (the ritual), de-emphasised for Focus
- Q2 → Lotus/Brilliant white (light) · Obsidian black (dark) · Blackberry red /
  Rose gold / Aluminium silver (bold accent)
- Q5 → price €1,192–4,436

### Configurator components — not questionnaire-filtered
- **Fronts (83)** — plain swatch library; front variety is driven by filtering
  Kitchens on `tone` + `priceTier` (each kitchen → one `frontCode`).
- **Worktops (62)** — **slaved** to the chosen kitchen via `worktopCode`, browsable
  within material family. Not filtered by the questionnaire.
- **Handles (94), CarcaseColours (13)** — configurator swatches; applied per
  kitchen pairing, refined in-configurator.

---

## Selection rules

1. **Universal axes are hard filters.** Q2 finish-match and Q5 budget apply to
   every category that has the field.
2. **Capacity is one coherent answer.** Q3 sets compact/standard/host once and
   applies the matching tier to fridge, dishwasher, cooktop, hood, microwave —
   so the whole suite is sized consistently (no 45 cm dishwasher next to a
   French-door fridge).
3. **Budget bends on thin pools.** If a category's surviving set is < ~4 after a
   hard budget cut, treat budget as a soft sort (on-tier first, then adjacent
   tiers) so a row never collapses to one item. Tone stays hard.
4. **Intent weights, not just filters.** Q1 shifts *emphasis* — Calm features the
   coffee machine and quiet hood; Host maxes capacity; Focus favours
   compact/fast — even where it isn't a hard field filter.

## Reflect-back (before showing results)
Name the answers and the exclusions:
> "Because you want something calm and light, cooking for two, kept simple — we
> pulled the scandinavian cabinets in Essential, matched a quiet Clean Steel
> fridge and the lowest-noise hood, and skipped the gloss statement kitchens."

Naming what you *cut* sells the personalization harder than the filtering.

---

## Implementation gap

`priceTier` currently exists only on **Kitchens** and **Fridges**. For Q5 to be a
hard filter on dishwashers, cooktops, hoods, microwaves and coffee machines, each
needs a `priceTier` derived from its own real `price` (same tercile method).
Until then, Q5 filters cabinets + fridge and can only *sort* the other appliances
by price. One additive pass fixes it.
