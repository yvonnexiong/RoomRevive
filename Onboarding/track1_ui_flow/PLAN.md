# Track 1 — Unity UI Build Plan

Visual oracle: `onboarding_UI_style_demo.html` (open in browser — covers Q1–Q4, Review, Result).  
Design tokens: `UI_SPEC.md` · Copy: `UI_TEXT.md` · Data contract: `../shared/OUTPUT_CONTRACT.md`

**How to use this file:** update `[ ]` → `[x]` as tasks complete. Each phase ends with a
clear "done when" checkpoint before moving on.

---

## Status snapshot

| Phase | Name | Status |
|---|---|---|
| 0 | Foundation — theme asset | ✅ done |
| 1 | Q1 visual shell (one page, no logic) | ✅ done |
| 2 | Q1 interactive (single-select states) | ✅ done |
| 3 | Extend to Q2 – Q4 (data-driven) | ⬜ not started |
| 4 | Navigation + progress bar | ⬜ not started |
| 5 | Preferences Review screen | ⬜ not started |
| 6 | Payload assembly + Bridge call | ⬜ not started |
| 7 | Build B — Result page (stubbed) | ⬜ not started |
| 8 | End-to-end wire (live data) | ⬜ not started |

---

## Phase 0 — Foundation: theme asset
*Shared by every screen. Do this first so all later work pulls from one place.*

- [ ] Create `OnboardingTheme.cs` (ScriptableObject): palette colors, font references, radii
- [ ] Populate tokens from `UI_SPEC.md §1`:
  - Surface base `#B5BCD0`, surf-light `#D4D9E5`, surf-deep `#7C85A0`
  - Card inner `#C7CDDD`
  - Ink `#3A4055`, ink-2 `#6B7388`
  - Teal (selection) `#7BA8C4`
- [ ] Add Alan Sans font asset (import from Google Fonts if not already in project)
- [ ] Create the theme asset at `Assets/Onboarding/Data/OnboardingTheme.asset`

**Done when:** theme asset exists and compiles cleanly.

---

## Phase 1 — Q1 visual shell (one page, no logic)

> **Tweak point.** This phase produces a single static screen for Q1 (Style).
> Get the visual exactly right here — spacing, card size, font sizes, radii —
> before any logic or additional pages are added. Nothing in this phase is
> interactive; all four cards are just rendered.

### 1a — Panel + banner
- [ ] Create `OnboardingFlowUI.prefab` (World Space Canvas, ~480px wide column)
- [ ] Banner panel: rounded rect (surf-light, 8px radius), centered column
  - [ ] Title TMP: "Which style do you prefer?" — ink, 700, ~22px
  - [ ] Subtitle TMP: "1 of 4" (placeholder) — ink-2, 400, ~13px

### 1b — Progress bar (visual only)
- [ ] 4-segment bar at top of banner area
- [ ] Active segment: surf-deep fill; inactive: surf-light fill (dimmed)
- [ ] Segment 1 lit, segments 2–4 unlit (hardcoded for now)

### 1c — Image card grid (2×2, hardcoded Q1 data)
Each card:
- [ ] Card root: rounded rect (card-inner, 8px), square aspect ratio
- [ ] Photo fills card (no tint)
- [ ] Caption band at bottom: surf-light background, full card width
  - [ ] Label TMP: e.g. "Clean & Uncluttered" — ink, 600, ~14px
  - [ ] Subtitle TMP: e.g. "Modern" — ink-2, 400, ~11px
- [ ] Wire up the 4 Q1 images (`kitchen_style_1–4.png`) to the 4 cards

### 1d — Back / Next buttons (visual only)
- [ ] Back button: ghost style (outline, ink text) — left
- [ ] Next button: surf-deep fill, white text — right, visually dimmed (disabled look)
- [ ] No click handlers yet

**Done when:** Q1 screen looks like the prototype in Play mode. Cards are the right
size, banner sits correctly, caption text is readable. Sign off here before Phase 2.

---

## Phase 2 — Q1 interactive (single-select states)

*Add behaviour to the Q1 shell. Still only Q1 — not yet data-driven.*

- [ ] `OnboardingOptionCardView.cs`: attach to each card prefab
  - [ ] `Bind(label, subtitle, sprite)` — populates TMP + image
  - [ ] `SetSelected(bool)` — teal 2px ring + teal caption background + white caption text
  - [ ] `SetDisabled(bool)` — 35% opacity, pointer events off
  - [ ] Click handler calls back to controller
- [ ] Single-select logic: selecting one card deselects the others
- [ ] Next button: enabled (surf-deep) only when a selection exists, disabled (dimmed) otherwise
- [ ] Back button: on Q1, Back does nothing (or hide it — match prototype)

**Done when:** tapping a Q1 card highlights it with teal ring, tapping another moves
the ring, Next enables. Matches the selected/unselected states in the prototype.

---

## Phase 3 — Extend to Q2 – Q4 (data-driven)

*Parameterize what Phase 2 hardcoded, then add the remaining pages.*

### 3a — Data model
- [ ] `OnboardingOptionData.cs` (serializable struct): `label`, `subtitle`, `value`, `image`
- [ ] `OnboardingQuestionData.cs` (ScriptableObject): `prompt`, `List<OnboardingOptionData>`, `useImageCards` bool
- [ ] Populate `Q1_Style.asset` – `Q4_Investment.asset` with values from `UI_SPEC.md §3`
  (Q1/Q2 use image cards; Q3/Q4 use text rows)

### 3b — Text row variant
- [ ] `OnboardingTextRowView.cs` (reuses `SetSelected` / `SetDisabled` interface)
  - [ ] Label TMP (ink, 600) + optional subtitle TMP (ink-2, 400)
  - [ ] Selected state: teal fill, white text

### 3c — Page factory
- [ ] `OnboardingFlowController.cs`: given a `OnboardingQuestionData`, build the correct
  card grid (image 2×2) or text row list, bind data, wire click callbacks
- [ ] Pages 2–4 render correctly from their ScriptableObject assets

**Done when:** all four question pages render from data assets and the card/row
variant switches automatically (Q1/Q2 = cards, Q3/Q4 = rows).

---

## Phase 4 — Navigation + progress bar

- [ ] Back / Next page transitions (instant or short fade — match prototype)
- [ ] Progress bar: segment N lit for page N (drive from `currentPage` index)
- [ ] Back on Q1: hide Back button (or disable it)
- [ ] Next carries the current answer forward; navigating back preserves it
- [ ] Q4 Next button label reads "See my kitchen" instead of "Next"
- [ ] **Impossible-combo rule:** driven by a `style → allowedTones[]` lookup table
  (not hardcoded per option) in `OnboardingFlowController`
  - [ ] On entering Q2: disable options whose value is not in `allowedTones[selectedStyle]`
  - [ ] On back-nav from Q2: if stored tone is now disabled, clear it

**Done when:** full Q1 → Q2 → Q3 → Q4 flow works, back-nav preserves answers,
Scandinavian correctly greys Dark + Colourful on Q2.

---

## Phase 5 — Preferences Review screen

*No progress bar on this screen.*

### 5a — Layout
- [ ] Banner: "Personalizing your dream kitchen" + animated dots TMP
- [ ] 4 summary rows (hidden at start): icon/badge + answer label
- [ ] Progress bar (separate from the Q1–Q4 one): fills left-to-right

### 5b — Animations (all C# coroutines, no Animator)
- [ ] **Animated dots:** coroutine cycling `""` → `"."` → `".."` → `"..."` every 0.4s
- [ ] **Staggered row reveal:** loop over 4 rows, each fades up (alpha 0→1, small Y offset)
  with `WaitForSeconds(0.5f)` between each
- [ ] **Progress bar fill:** parallel coroutine, `Image.fillAmount` 0→1 over 2.7s
- [ ] **Banner swap:** after all rows revealed → title = "Your preferences",
  subtitle = "Got it — finding your kitchen", dots stop

### 5c — Auto-advance gate
- [ ] `_animDone` flag: set when banner swap completes
- [ ] `_coreDone` flag: set when `OnboardingBridge.onSelectionReceived` fires
- [ ] `WaitUntil(() => _animDone && _coreDone)` → transition to Build B

**Done when:** review screen plays through the full animation sequence and would
auto-advance (Build B doesn't exist yet — log a message as placeholder).

---

## Phase 6 — Payload assembly + Bridge call

- [ ] On entering the Review screen, assemble:
  ```json
  { "style": "...", "tone": "...", "household": "...", "budget": "..." }
  ```
- [ ] Call `OnboardingBridge.SubmitAnswers(style, tone, household, budget)`
  — this writes `onboarding_answers.json` and starts watching for the result
- [ ] Wire `OnboardingBridge.onSelectionReceived` → set `_coreDone = true`
  and store the JSON string for Build B

**Done when:** completing Q4 and watching the review animation also produces a valid
`onboarding_answers.json` on disk. Running `node cli.js` manually produces
`onboarding_selection.json`. Build B can be stubbed from that file.

---

## Phase 7 — Build B: Result page (stubbed, then live)

*Start against `../track3_visualizer/sample_output.json`; replace with live data in Phase 8.*

### 7a — Catalog loader
- [ ] `CatalogLookup.cs`: loads `shared/catalog.json`, indexes items by `id`
- [ ] `GetDisplayName(id)` returns `item.name` (or `item.product.name`)

### 7b — Layout
- [ ] Banner: "Your kitchen is ready" / "Here's what we'll bring into your room"
- [ ] Changes card (card-inner): eyebrow "In this room"
- [ ] One row per category, in order:
  Kitchen · Fridge · Cooktop · Hood · Microwave · Dishwasher · Coffee machine
  — `category label` (ink-2, left) + `product name` (ink, bold, right)
- [ ] Rows start hidden; staggered fade-in (same coroutine pattern as Review)
- [ ] "Transforming your kitchen …" with pulsing dots below card (ink primary, no button)

### 7c — Data binding
- [ ] `SelectionOutput.cs`: deserializes the output payload JSON
  (`answers`, `profile`, `kitchens`, `appliances[]`)
- [ ] For each appliance category: take `topPick.id`, resolve name via `CatalogLookup`
- [ ] For kitchen row: take `kitchens.shortlist[0].id`, resolve name
- [ ] Omit any row whose id fails to resolve

### 7d — Scene transition trigger
- [ ] On entering Build B, fire the passthrough/splat scene transition
  (wire to existing `IntentManager` / scene-swap mechanism)

**Done when:** Build B renders all 7 rows with real names from `sample_output.json`
and the scene transition fires on entry.

---

## Phase 8 — End-to-end wire (live data)

- [ ] Replace `sample_output.json` stub with live `onselectionReceived` JSON from Bridge
- [ ] Confirm round-trip: answer Q1–Q4 → Review plays → Bridge writes answers →
  `node cli.js` runs (manually or triggered) → Bridge fires received → Build B renders
- [ ] Test all four golden-test answer combos produce different Build B layouts
- [ ] Remove any debug logs added during development

**Done when:** the full loop works end-to-end with no hardcoded data.

---

## Notes

- Phases 1–2 are the visual tuning gate. Don't move to Phase 3 until Q1 is signed off.
- Build B (Phases 7–8) can be developed in parallel with Phases 5–6 once the
  data contract is clear — use `sample_output.json` as the stub.
- The Review screen's `_coreDone` flag expects Track 2 (`cli.js`) to be run externally
  for now. Full automation (auto-launch on answers written) is a Phase 8 decision.
