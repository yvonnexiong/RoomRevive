# RoomRevive — UI Spec (Unity question flow)

Covers the four-question onboarding UI only. The visual + interaction reference
is `onboarding_demo.html`; this pins the tokens, the flow, and the
answer→value→image mapping so the Unity build matches it and emits the correct
input payload for the Selection Core.

---

## 1. Design tokens

Pulled from the prototype. Use as a shared palette/style asset.

| token | value | use |
|---|---|---|
| base | `#B5BCD0` | periwinkle base |
| surf-light | `#D4D9E5` | light surface |
| surf-deep | `#7C85A0` | deep surface |
| card-inner | `#C7CDDD` | card fill |
| ink | `#3A4055` | primary text |
| ink-2 | `#6B7388` | secondary text / hints |
| teal | `#7BA8C4` | **selection accent** |
| amber | `#E6A560` | relaxed / "closest" state |
| peach | `#E8B894` | (intent accent, spare) |

- **Typeface:** Alan Sans (Google Fonts) — weights 400/500/600/700.
- **Radius:** 8px standard (cards, pills, panels). Selection ring 2px teal.
- **Glass panel:** fill `rgba(213,217,229,0.35)` + 20px blur + 1px `rgba(255,255,255,.45)` border.
- **Screen background:** `radial-gradient(120% 120% at 20% 0%, #D4D9E5 0%, #B5BCD0 55%, #A6AEC4 100%)`.
- **Card fill:** `linear-gradient(135deg, rgba(255,255,255,.42), rgba(199,205,221,.30))`.

---

## 2. Flow — 4 pages

One question per page. Single-select. Progress indicator 1–4. Back/Next; Next
enables once an option is selected. After Q4 → resolve via Core → result/profile
screen (separate track, see OUTPUT_CONTRACT).

```
Page 1  Style      (image cards, 2×2)
Page 2  Palette    (image cards, 2×2)   ← some options may be disabled (see §4)
Page 3  Household  (text options)
Page 4  Investment (text options)  → build input payload → Selection Core
```

Question text (numbered):
1. Which style do you prefer?
2. Which colours feel most like home?
3. How many are you usually cooking for?
4. How much would you like to invest?

Recommend modelling each question as data (ScriptableObject): prompt + list of
options {label, subtitle, value, image}. Copy lives in UI_TEXT.md.

---

## 3. Answer → value → image mapping  (the UI contract)

The **value** is what the flow emits to the Core. Don't emit labels.

### Page 1 · Style → `style`  (image cards)
| label | subtitle | value | image |
|---|---|---|---|
| Clean & Uncluttered | Modern | `modern` | kitchen_style_1.png |
| Bold & Dramatic | Designer | `designer` | kitchen_style_2.png |
| Warm & Cozy | Cottage | `cottage style` | kitchen_style_3.png |
| Calm & Natural | Scandinavian | `natural & scandinavian` | kitchen_style_4.png |

### Page 2 · Palette → `tone`  (image cards)
| label | value | image |
|---|---|---|
| Light & Airy | `light` | swatches_stack_1.png |
| Dark & Moody | `dark` | swatches_stack_2.png |
| Warm Wood Tones | `wood` | swatches_stack_3.png |
| Colourful & Playful | `bold` | swatches_stack_4.png |

### Page 3 · Household → `household`  (text)
| label | value |
|---|---|
| 1–2 people | `compact` |
| 3–4 people | `standard` |
| 5+ people | `host` |

### Page 4 · Investment → `budget`  (text)
| label | subtitle | value |
|---|---|---|
| Essential | Affordable | `Essential` |
| Signature | Mid-range | `Signature` |
| Premium | High-end | `Premium` |
| Show All | Explore everything | `any` |

Values are case- and space-sensitive (`cottage style`, `natural & scandinavian`,
`Essential`) — they must match the catalog/Core exactly.

---

## 4. Interaction rules

**Single select:** one option per page; tapping another moves the selection.

**Selection state (cards):** teal 2px ring + teal caption bar, white caption text.
Don't tint the photo. **Text options:** filled teal background, white text.

**Impossible-combo greying (Page 2 depends on Page 1):**
Some style + palette combinations don't exist, so disable those palette options
once a style is chosen.

| style | palettes available | disabled |
|---|---|---|
| modern | light, dark, wood, bold | — |
| designer | light, dark, wood, bold | — |
| cottage style | light, dark, wood, bold | — |
| natural & scandinavian | light, wood | **dark, bold** |

Disabled options render dimmed (~35% opacity) and are not tappable. If the user
goes back and changes style such that their chosen palette becomes disabled,
**clear the palette selection**. (Today only Scandinavian disables anything.)

**Budget skip:** "Show All" emits `budget = "any"` — a real value meaning "no
budget filter," not a skipped question.

**Progress / nav:** 1–4 indicator; Back preserves earlier answers; Next disabled
until the current page has a selection.

---

## 5. Output of the flow → input to the Core

After Page 4, build exactly:

```json
{ "style": "...", "tone": "...", "household": "...", "budget": "..." }
```

Hand this to `SelectionCore.Resolve(input, catalog)`. See OUTPUT_CONTRACT.md for
what comes back and SELECTION_LOGIC.md for what the Core does with it. The flow
itself contains **no filtering logic** — it only collects four values.

---

## 6. Assets to ship with this spec
- `UI_TEXT.md` — all copy
- `kitchen_style_1–4.png`, `swatches_stack_1–4.png` — answer images
- `onboarding_demo.html` — visual + interaction reference
