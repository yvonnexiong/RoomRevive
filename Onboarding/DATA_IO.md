# Onboarding Data I/O

---

## UI 1 — OnboardingFlowUI
Questionnaire (Q1–Q4) + Review screen.

**Input:** user selections in the UI (style, tone, household size, budget)

**Output:** written to disk when user completes the review animation
`Onboarding/onboarding_answers.json`
```json
{
  "style":     "modern",
  "tone":      "light",
  "household": "compact",
  "budget":    "Signature"
}
```

---

## UI 2 — ReadyUI
"Your kitchen is ready" screen showing selected products.

**Input:** reads from disk automatically the moment the file appears/changes
`Onboarding/onboarding_selection.json`
```json
{
  "intent":  "Host & Gather",
  "tagline": "warm, social, ready for guests",
  "rows": [
    { "category": "Kitchen",        "name": "TOUCH 337",              "id": "abc123" },
    { "category": "Fridge",         "name": "KFN 4795 AD",            "id": "abc123" },
    { "category": "Cooktop",        "name": "KM 7464 FL",             "id": "abc123" },
    { "category": "Hood",           "name": "DAC 2940 Stella",        "id": "abc123" },
    { "category": "Microwave",      "name": "M 2240 SC",              "id": "abc123" },
    { "category": "Dishwasher",     "name": "G 5632 SCU Active S",    "id": "abc123" },
    { "category": "Coffee machine", "name": "CM 6360 MilkPerfection", "id": "abc123" }
  ]
}
```

**Output:** displays product names in the UI. No file written.

**Rules:**
- `name` = human-readable product name — Unity displays it directly, never resolves `id`
- `rows` must be exactly 7, in this order: Kitchen → Fridge → Cooktop → Hood → Microwave → Dishwasher → Coffee machine
- ReadyUI is standalone — it works without OnboardingFlowUI as long as the file exists on disk

---

Both files live in `Onboarding/` at the repo root — **not** inside the Unity project folder.
