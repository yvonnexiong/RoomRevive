# catalog.json — Changelog

RoomRevive product catalog. All edits below are **additive only**: new fields on
the `product` object, derived from data already in the catalog. No existing value
was modified, no field removed, no item added or deleted.

## Verified diff (original upload → current)

| Check | Result |
|---|---|
| Item count | 474 → 474 (unchanged) |
| Categories | 11, identical |
| Item IDs | identical set |
| Existing values modified | **0** |
| Fields removed | none |
| Items gaining fields | 222 (83 Kitchens + 139 appliances) |
| Items unchanged | 252 |

Untouched categories (byte-for-byte): **Fronts**, Worktops, Handles,
CarcaseColours. (Fronts is intentionally left as a plain code+image library —
see Notes.)

---

## Added fields

### Kitchens — 83 items, 6 new fields (in `product`)

| Field | Type | Meaning | Source |
|---|---|---|---|
| `priceGroup` | int 1–10 | Nobilia-style price ordinal | derived from front finish |
| `priceTier` | string | Essential / Signature / Premium | from `priceGroup` |
| `priceGroupBasis` | string | finish keyword that set the group (auditable) | front finish |
| `tone` | string | light / dark / wood / bold / other | from `color` |
| `frontCode` | string | front swatch number (→ Fronts image library) | parsed from `front` |
| `worktopCode` | string\|null | paired worktop number (seeds slaved worktop) | parsed from `worktop` |

### Fridges — 29 items, 1 new field (in `product`)

| Field | Type | Meaning | Source |
|---|---|---|---|
| `priceTier` | string | Essential / Signature / Premium | from real EUR `price` (terciles) |

### Dishwashers, Cooktops, Hoods, Microwaves, CoffeeMachines — 110 items, 1 new field each (in `product`)

| Field | Type | Meaning | Source |
|---|---|---|---|
| `priceTier` | string\|null | Essential / Signature / Premium | from real EUR `price`, **per-category** terciles |

Per-category terciles (not a global threshold) so every category has options at
every budget tier — "Essential" always returns a dishwasher, hood, coffee
machine, etc.

---

## Derivation logic

### Kitchens · priceGroup (finish → 1–10; highest-cost signal wins)
```
10  ultra-high-gloss / premium-honed lacquer
 9  premium / perfect / premium-lacquer-matt
 8  lacquer, ultra-matt
 7  high gloss, glass
 6  supermatt, honed
 5  structured, wood-texture laminate
 4  reproduction (wood/stone/concrete)
 3  matt
 2  solid colour (basic)
```
priceTier: group ≤4 = Essential · 5–7 = Signature · 8–10 = Premium
Source for the 10-group / +5%-step structure: Nobilia FAQ + trade catalogs.
NOTE: heuristic ordinal, NOT Nobilia's official price book. Reflects finish
cost-order, not quotable prices. Swap in real groups later via `priceGroupBasis`
without touching the pipeline.

### Kitchens · tone (from `color`)
wood (oak/walnut/wood reproductions) · dark (black/graphite/concrete/slate) ·
bold (green/blue/aqua/red/etc.) · light (white/alpine/ivory/sand/grey) · other.

### Kitchens · frontCode / worktopCode
Parsed from the `front` / `worktop` text (matches "Front NNN",
"Worktop NNN", "Countertops NNN"). 12 kitchens have no worktop in the
source → `worktopCode` = null.

### Fridges · priceTier (real EUR price terciles)
≤ €1,876 Essential · ≤ €2,452 Signature · > €2,452 Premium
(1 fridge has no price → priceTier = null.)

### Appliances · priceTier (per-category EUR price terciles)
Each category split into thirds by its own price range:
```
Dishwashers    ≤€1,017  ≤€1,165  >€1,165
Cooktops       ≤€1,460  ≤€1,782  >€1,782
Hoods          ≤€1,138  ≤€2,197  >€2,197
Microwaves     ≤€1,259  ≤€1,862  >€1,862
CoffeeMachines ≤€1,661  ≤€1,983  >€1,983
```

---

## Resulting distributions

```
Kitchen priceTier  : Essential 28 · Signature 26 · Premium 29
Kitchen priceGroup : g2:10 g3:2 g4:16 g5:6 g6:11 g7:9 g8:9 g9:5 g10:15
Kitchen tone       : light 36 · dark 19 · wood 13 · bold 13 · other 2
Kitchen frontCode  : 83/83 populated
Kitchen worktopCode: 71/83 populated (12 null)
Fridge  priceTier  : Essential 10 · Signature 10 · Premium 8 · (1 null)
Dishwasher priceTier   : Essential 2 · Signature 2 · Premium 1
Cooktop priceTier      : Essential 9 · Signature 7 · Premium 7
Hood priceTier         : Essential 18 · Signature 16 · Premium 16
Microwave priceTier    : Essential 5 · Signature 4 · Premium 3
CoffeeMachine priceTier: Essential 8 · Signature 6 · Premium 6
```

---

## Notes for Unity integration

- **One-table model.** The questionnaire filters Kitchens only. Each kitchen
  carries its own `frontCode` (→ swatch image) and `worktopCode` (→ paired
  worktop), so there is no parallel Fronts table to keep in sync.
- **Fronts category stays bare** (code + image). Front variety in the
  configurator is driven by filtering Kitchens on `tone` + `priceTier`, not by
  attributes on the Front records.
- **Worktop is slaved** to the chosen kitchen via `worktopCode`, not filtered by
  the questionnaire.
- File written with 1-space indent.
