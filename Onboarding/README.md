# RoomRevive — Onboarding Handoff

Everything needed to implement the intent-driven kitchen onboarding: a four-question
flow that filters the Nobilia catalog down to a kitchen + appliance suite.

The work splits into **three tracks that build in parallel**, joined by two contracts
(the input payload and the output payload). Each track has its own folder; `shared/`
holds what more than one track needs.

```
shared/                  catalog.json + CHANGELOG + OUTPUT_CONTRACT (the cross-track contract)
track1_ui_flow/          the 4 Unity question pages
track2_selection_core/   the filtering/scoring logic (pure, no UI)
track3_visualizer/       renders the result (the "right side")
```

## The seam between tracks — `shared/OUTPUT_CONTRACT.md`
- **UI flow → Core:** input payload `{ style, tone, household, budget }`
- **Core → Visualizer:** output payload (id references + computed fields)

Both schemas live in `shared/OUTPUT_CONTRACT.md` — the single source of truth for
the data passing between tracks. Pin it first; then all three tracks proceed
independently.

## Start here, per track

**Track 1 — UI flow** (`track1_ui_flow/`)
**Start with `UNITY_UI_PLAN.md`** — the implementation plan. It splits the work into
Build A (the Q1–Q4 onboarding + preferences review, self-contained UI) and Build B
(the data-bound "Your kitchen is ready" result page), with acceptance criteria for each.
`onboarding_UI_style_demo.html` is the current visual + interaction oracle (open it in a
browser; it covers both builds). `UI_SPEC.md` is authoritative for design tokens and the
answer→value→image mapping; `UI_TEXT.md` holds all copy; images in `images/`.
(`onboarding_demo.html` is the earlier split-screen data-browsing prototype — reference
only.) Build A emits the input payload defined in `../shared/OUTPUT_CONTRACT.md`; Build B
renders the Core's output payload.

**Track 2 — Selection Core** (`track2_selection_core/`)
Implement `SELECTION_LOGIC.md` against `../shared/catalog.json`, emitting the output
payload in `../shared/OUTPUT_CONTRACT.md`. **Make the golden tests in SELECTION_LOGIC
§8 pass** — they're taken from the validated prototype, so green = behaviour matches.

**Track 3 — Visualizer** (`track3_visualizer/`)
Build against `../shared/OUTPUT_CONTRACT.md` + `sample_output.json` (a real payload).
Resolve each `id` against `../shared/catalog.json` for names/finishes/images.

## Notes
- `catalog.json` is enriched additively (tone, priceTier, priceGroup, frontCode,
  worktopCode); see `shared/CHANGELOG.md`. Nothing original was modified.
- `QUESTIONNAIRE.md` (in Core) is the design rationale behind the logic.
