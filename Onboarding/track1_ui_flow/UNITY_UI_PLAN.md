# RoomRevive — Unity UI Implementation Plan

Build the onboarding UI in Unity from the reference prototype. The work splits into
**two independent builds** joined by one data payload:

- **Build A — Onboarding flow** (Q1–Q4 + preferences review). Self-contained UI, no
  catalog data. Its only output is a four-value input payload.
- **Build B — Result page** ("Your kitchen is ready"). Data-bound. Renders the
  Selection Core's output (one recommended item per category) and triggers the
  visualizer/VR transition.

Why split: everything in Build A is static UI driven by the user's taps and can be
built and tested with zero backend. Build B can't be finished until the Selection
Core returns real recommendations, so it's gated on that track. Splitting lets Build A
ship and get tested immediately while the data layer is still in progress.

---

## Visual oracle

`onboarding_UI_style_demo.html` is the source of truth for look, layout, motion, and
interaction. Open it in a browser and match it. It contains **both** builds (run it
through to the end to see the result page). When this doc and the prototype disagree,
the prototype wins for visuals; this doc wins for data behavior and acceptance.

Supporting files:
- `UI_SPEC.md` — design tokens + answer→value→image mapping (authoritative for values)
- `UI_TEXT.md` — all copy
- `images/` — 8 answer images (`kitchen_style_1–4.png`, `swatches_stack_1–4.png`)
- `shared/OUTPUT_CONTRACT.md` — the input + output payload schemas (the A↔B seam)
- `shared/catalog.json` — resolve item `id` → display name (Build B only)

---

## Shared foundation (both builds)

- **Palette:** Surface base `#B5BCD0`, Surface light `#D4D9E5`, Surface deep `#7C85A0`
  (selected/active accent), Ink primary `#3A4055` (text), Ink secondary `#6B7388`
  (subtext), Card inner `#C7CDDD` (option panels / cards). Build these as a shared
  theme asset.
- **Type:** Alan Sans (400–700).
- **Layout:** one centered column, ~480px-equivalent width. Banner, options, and nav
  all share that width so the title always sits over content of the same width.
- **Banner:** rounded panel, Surface light→base, centered Ink title + Ink-2 subtitle.
- **Environment:** the prototype fakes a blurred kitchen behind the panels to simulate
  passthrough. In Unity that's the real passthrough/scene — the panels just float over
  it. Don't recreate the blurred image.
- **Radii:** 12–14px panels/cards, 10px buttons.

---

## Build A — Onboarding flow

### Screens
Five screens in one panel: Q1 Style, Q2 Palette, Q3 Household, Q4 Investment, then the
Preferences Review.

**Questions (Q1–Q4)**
- Progress: 4-segment bar at the top, filled up to the current step. **Questions only.**
- Banner shows the question (title) + its supporting line (subtitle) — copy in UI_TEXT.
- Q1/Q2 = 2×2 **image cards**: square (1:1) photo, caption band below it (Card inner)
  with bold Ink title + Ink-2 subtitle.
- Q3/Q4 = **text rows** (Card inner), Ink label, Ink-2 sub where present.
- **Single select.** Selected option → Surface deep fill + white text (+ ring on cards).
- **Back / Next** at the bottom; Next disabled until an option is chosen. Q4's Next
  reads "See my kitchen".

**Answer → emitted value** (exact strings — see UI_SPEC §3):

| Q | key | values |
|---|---|---|
| 1 Style | `style` | `modern`, `designer`, `cottage style`, `natural & scandinavian` |
| 2 Palette | `tone` | `light`, `dark`, `wood`, `bold` |
| 3 Household | `household` | `compact`, `standard`, `host` |
| 4 Investment | `budget` | `Essential`, `Signature`, `Premium`, `any` |

### Interaction rules
- **Impossible-combo greying:** when `style = natural & scandinavian`, disable the Dark
  and Colourful options on Q2 (dim ~35%, not tappable). If the user goes back and picks
  Scandinavian after already choosing Dark/Colourful, clear that palette selection. All
  other styles allow all four palettes. (Drive this from a small style→allowed-tones
  table so it stays data-driven, not hard-coded per option.)
- **"Show All"** emits `budget = "any"` — a real value (no budget filter), not a skip.
- Back preserves earlier answers.

### Preferences Review (the bridge screen)
- **No top progress bar.**
- Banner: "Personalizing your dream kitchen" + animated dots.
- Animates through the four captured selections — each row (Style, Palette, Cooking
  for, Investment) fades up with a check badge, staggered ~0.5s apart, while a progress
  bar fills (~2.7s).
- Settles: banner → "Your preferences" / "Got it — finding your kitchen".
- Then **auto-advances** to Build B (no button).

### Output (the seam)
On reaching the review screen, Build A:
1. Assembles the input payload:
   `{ "style": …, "tone": …, "household": …, "budget": … }`
2. Calls the Selection Core with it **while the review animation plays** — the
   animation masks compute latency.
3. When the Core returns (or the animation finishes, whichever is later), transitions
   to Build B with the Core's output payload.

Build A contains **no filtering logic** — it only collects four values and kicks off
the Core. Schema: `shared/OUTPUT_CONTRACT.md`.

### Acceptance — Build A
- Visually matches `onboarding_UI_style_demo.html` (Q1–Q4 + review).
- Emits the exact value strings above (case/space-sensitive).
- Scandinavian greys out Dark + Colourful; conflict clears on back-nav.
- Review animation plays and auto-advances; no progress bar on the review screen.
- Produces a valid input payload and triggers the Core.

---

## Build B — Result page ("Your kitchen is ready")

This screen is **data-bound** — it renders whatever the Selection Core returned.

### Layout (match the prototype's last page)
- **No top progress bar.**
- Banner: "Your kitchen is ready" / "Here's what we'll bring into your room".
- A single **changes card** (Card inner): eyebrow "In this room", then one row per
  category — category label (Ink-2, left) → product name (Ink, bold, right). Rows fade
  in staggered.
- Below the card, plain centered loading text **"Transforming your kitchen …"** with
  pulsing dots (Ink primary). **No button.**
- No estimate / price.

### Categories shown (one row each, in this order)
Kitchen, Fridge, Cooktop, Hood, Microwave, Dishwasher, Coffee machine — i.e. the seven
catalog product types that get placed in the scene.

### Data binding (replaces the prototype's placeholders)
The prototype hard-codes a `RECOMMENDED` list — **that is fake.** In Unity:
- Read the Selection Core's output payload (see `OUTPUT_CONTRACT.md`,
  `sample_output.json` for a real example).
- For each category, take the **topPick `id`** and resolve its display name from
  `shared/catalog.json` (item `name` / `product.name`). Render that as the row's value.
- If a category has no pick, omit its row (don't show a placeholder).

### Transition
"Transforming your kitchen …" is a passive state: on entering Build B, kick off the
passthrough transition / Gaussian-splat scene swap. There's no confirm button — the
page appearing *is* the trigger. (If product wants an explicit confirm later, that's a
separate decision.)

### Acceptance — Build B
- Layout matches the prototype's last page (card, rows, loading text, no button, no
  price).
- Every row's product name comes from the Core payload resolved against the catalog —
  none hard-coded.
- Rows reflect the user's actual answers (different input → different items).
- Entering the page starts the scene transition.

---

## The A ↔ B data seam

```
Build A  →  input payload { style, tone, household, budget }
              →  Selection Core (track 2)
              →  output payload { profile, per-category topPick id + ranked ids, … }
              →  Build B renders rows (id → name via catalog) + starts transition
```

Both payload schemas are fixed in `shared/OUTPUT_CONTRACT.md`. Build against that
contract; the Core can be stubbed with `sample_output.json` until it's live, which lets
Build B be developed in parallel with real-looking data.

---

## Suggested Unity structure (advisory)

- **Questions as data:** a ScriptableObject per question — prompt, subtitle, option list
  (label, subtitle, value, image), and the style→allowed-tones table — so copy/values
  live in assets, not code. Mirrors UI_TEXT / UI_SPEC.
- **One flow controller** drives screen index, holds the four answers, enforces single-
  select + greying, and builds the input payload.
- **One option-panel prefab** parameterized for image-card vs text-row.
- **Build B** is a separate scene/prefab that takes the output payload and a catalog
  lookup; the changes-card row is its own small prefab bound to {category, name}.
- Keep Build A and Build B as separate scenes/prefabs so they can be iterated
  independently.

---

## Suggested order of work
1. Shared theme asset (palette, type, banner, button styles).
2. Build A: question screens (static) → single-select + nav → greying rule → review
   animation → input-payload assembly. Test end-to-end against the prototype with the
   Core stubbed.
3. Build B: changes-card layout against `sample_output.json` → real catalog id→name
   binding → transition trigger.
4. Wire the seam: A emits payload → Core → B renders. Replace stub with live Core.

Reference the prototype continuously — it answers almost every "how should this look or
move" question.
