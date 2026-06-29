# RoomRevive — Selection Logic

Implementation-ready rules for the Selection Core (the part that turns four
answers + `catalog.json` into the output payload). Language-agnostic pseudocode;
ports directly to C#. The HTML prototype is the reference implementation — the
golden tests at the end pin exact numbers to match it.

---

## 0. Pipeline

```
answers (style, tone, household, budget)
  + catalog.json
  → derive runtime fields (capacity class, appliance tone)
  → select kitchens   (hard filter + soft budget)
  → select appliances (filter + floor relax + rank)
  → derive profile    (lookup tables)
  → emit payload (see OUTPUT_CONTRACT.md)
```

Catalog already carries (additively enriched): `tone`, `priceTier`, `priceGroup`,
`frontCode`, `worktopCode` on kitchens; `priceTier` on all appliances. The two
classifications below are derived at runtime from raw fields.

---

## 1. Runtime classifications

### 1a. Appliance finish tone — from `product.color`
Lowercase the colour string, first match wins:
```
contains blackberry | rose gold | aluminium | red          → "bold"
contains obsidian | black | graphite | blackboard          → "dark"
contains "steel look"                                       → "wood"
contains white | pearlbeige | lotus | ivory | alpine       → "light"
otherwise (incl. stainless / clean steel)                  → "neutral"
```
`neutral` is the key rule: steel/stainless matches **any** palette.

### 1b. Capacity class — per category
```
Fridges:      width≥75cm OR fridgeCapacity≥300 → host
              fridgeCapacity≥220               → standard
              else                             → compact
Dishwashers:  placeSettings≤10 → compact ; else standard
Cooktops:     zones≥5 → host ; zones≥4 → standard ; else compact
Hoods:        width≥900mm → host ; width≥600 → standard ; else compact
              (no width → standard)
Microwaves:   litres≤17 → compact ; else standard
              (litres parsed from headline/name "NN L"; default 26)
CoffeeMachines: always standard (no size axis)
```
`width`/`litres` parse the first number out of `dimensions` / `headline`.

### 1c. Capacity acceptance (household answer → allowed classes)
```
compact (1–2) → { compact }
standard (3–4) → { standard, compact }
host (5+)      → { host, standard }
```

---

## 2. Answer → field mapping

| answer | drives |
|---|---|
| style | kitchen `kitchenType` (hard) |
| tone | kitchen `tone` (hard) · appliance finish tone (match) |
| household | appliance capacity class |
| budget | `priceTier` (kitchen soft · appliance hard-with-floor) |

## 3. Match predicates

```
toneMatch(itemTone, answer)  = answer is empty OR itemTone==answer OR itemTone=="neutral"
sizeOk(cat, item, household)  = household empty OR capacityClass(cat,item) ∈ CAP_OK[household]
budgetOk(item, budget)        = budget empty OR budget=="any" OR item.priceTier==budget
```

---

## 4. Kitchen selection — hard filter, soft budget

```
base = kitchens where kitchenType==style AND tone==palette        // both HARD
if budget not in (empty, "any"):
    onTier = base where priceTier==budget
    if onTier not empty:  kits = onTier ;          budgetRelaxed = false
    else:                 kits = base ;            budgetRelaxed = true   // drop budget, keep style+palette
else:
    kits = base ;                                  budgetRelaxed = false

shortlist = kits as id references   // order: catalog order, or sort by priceGroup asc (UX choice)
count = kits.length
```

**Impossible combos:** `scandinavian + dark` and `scandinavian + bold` yield an
empty `base`. The UI greys these out so they're unreachable. If the Core is ever
called with one, return `count: 0` (or echo the disabled rule) — never fabricate.

Budget never empties kitchens: it only relaxes. Style+palette is guaranteed
non-empty for every reachable combo (verified).

---

## 5. Appliance selection — filter, floor relax, rank

Per category, **always returns ≥ floor items, never empty:**
```
floor(cat) = 2 if cat=="Fridges" else 1

pool = all items in category
opt  = pool where sizeOk AND toneMatch AND budgetOk
relaxed = false
if opt.length < floor(cat):  opt = pool where sizeOk AND budgetOk ; relaxed = true   // drop palette
if opt.length < floor(cat):  opt = pool where sizeOk             ; relaxed = true   // drop budget
if opt.length < floor(cat):  opt = pool                          ; relaxed = true   // drop size
count = opt.length
```

Relax order = **palette → budget → size** (least to most important; size last
because it's physical fit).

### Scoring (within `opt`), for the top pick + match strength
```
capfit = sizeOk(cat,item,household) ? 1 : 0
tonem  = toneMatch(item.tone, palette) ? 1 : 0
budm   = budgetOk(item, budget) ? 1 : 0
score  = 4*capfit + 2*tonem + 1*budm

ranked      = opt sorted by score desc
topPick     = ranked[0]            // id reference
matchStrength = capfit + tonem + budm    // 0..3  → dot indicator
options     = ranked ids (cap to top 8)
```

### Why-text (top pick)
```
parts = []
parts += (tonem ? "finish matches your palette" : "a neutral finish")
if capfit and household set: parts += (compact?"sized to fit" : host?"scaled to host" : "sized for the household")
parts += (budm ? "within budget" : "closest in price")
why = "Why: " + join(parts, " · ") + "."
```

---

## 6. Profile derivation (from answers, lookup tables)

```
INTENT[style] = (title, tagline)
  modern        → ("Fast & Focused", "bright, efficient — in, fed, and out")
  designer      → ("Host & Gather",  "open, social — made to entertain")
  cottage style → ("Calm & Unwind",  "warm, tactile, lived-in ease")
  scandinavian  → ("Calm & Unwind",  "quiet, natural, restorative")

CABINET_DIR[style]
  modern   → "Nobilia · Modern / Handleless"   front: "handleless slab fronts"
  designer → "Nobilia · Designer / Statement"  front: "lacquer slab fronts"
  cottage  → "Nobilia · Modern Cottage / Country" front: "frame fronts"
  scandi   → "Nobilia · Natural & Scandinavian" front: "slab fronts, light wood"
TONE_FINISH[tone] = light:"light, matte"  dark:"deep, matte"  wood:"matte wood"  bold:"colour accent"
cabinetDirection = CABINET_DIR.label + " — " + CABINET_DIR.front + (tone ? ", "+TONE_FINISH[tone] : "")

APPLIANCE_FINISH[tone]
  light → "white, clean steel — bright, neutral"
  dark  → "matte black, obsidian — deep, dramatic"
  wood  → "steel look, warm neutral — soft, natural"
  bold  → "accent tones over steel — characterful"

LIGHTING[intent]
  Calm & Unwind  → "warm soft light · 2700–3000 K"
  Host & Gather  → "warm-neutral · 3000–3500 K"
  Fast & Focused → "neutral bright · 3500–4000 K"

TAGS (in order, skip when answer absent)
  TONE  light:"light & airy"  dark:"dark & moody"  wood:"warm natural"  bold:"bold accent"
  STYLE modern:"clean lines"  designer:"statement"  cottage:"tactile framed"  scandi:"natural calm"
  CAP   compact:"intimate"  standard:"family"  host:"a crowd"
  VERB  Calm:"savour"  Host:"gather"  Focus:"flow"        (by intent)
  TIER  Essential:"essential"  Signature:"considered"  Premium:"premium"  (skip if "any")
  → tags = [TONE?, STYLE, CAP?, VERB, TIER?]
```

---

## 7. Invariants (assert in tests)

1. Kitchens never zero for any **reachable** combo (impossible palette combos are
   UI-disabled). Budget only relaxes, never empties.
2. Every appliance category returns ≥ floor: **Fridges ≥ 2, all others ≥ 1**, for
   every answer combination.
3. Output items are id references only — no catalog fields duplicated.
4. Relax order is palette → budget → size; `relaxed` flag set whenever any drop
   happens.

---

## 8. Golden tests (parity with the prototype)

Run these answer sets through the Core; assert the counts match exactly.

| style | tone | household | budget | kitchens | Fri | Coo | Hoo | Mic | Dis | Cof |
|---|---|---|---|---|---|---|---|---|---|---|
| modern | light | standard | Signature | 12 | 6 | 7 | 9 | 3 | 2 | 1 |
| designer | dark | host | Premium | 5 | 8 | 4 | 13 | 2 | 3 | 4 |
| natural & scandinavian | wood | compact | Essential | 6 | 3 | 3 | 6 | 4 | 2 | 20 |
| cottage style | light | standard | any | 6 | 13 | 1 | 29 | 6 | 5 | 4 |

(Counts are the option-set sizes after filter + floor relax. `cottage/light/any`
leaves budget open, so appliances filter on size + palette only.)

A worked payload for `modern/light/standard/Signature` is in `sample_output.json`.
