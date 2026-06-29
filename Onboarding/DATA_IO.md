# Onboarding Data I/O

## INPUT — Unity writes this when user finishes the questionnaire
**`Onboarding/onboarding_answers.json`**
```json
{
  "style":     "modern",
  "tone":      "light",
  "household": "compact",
  "budget":    "Signature"
}
```

## OUTPUT — your script writes this, Unity reads it
**`Onboarding/onboarding_selection.json`**
```json
{
  "intent":  "Host & Gather",
  "tagline": "warm, social, ready for guests",
  "rows": [
    { "category": "Kitchen",        "name": "TOUCH 337",             "id": "abc123" },
    { "category": "Fridge",         "name": "KFN 4795 AD",           "id": "abc123" },
    { "category": "Cooktop",        "name": "KM 7464 FL",            "id": "abc123" },
    { "category": "Hood",           "name": "DAC 2940 Stella",       "id": "abc123" },
    { "category": "Microwave",      "name": "M 2240 SC",             "id": "abc123" },
    { "category": "Dishwasher",     "name": "G 5632 SCU Active S",   "id": "abc123" },
    { "category": "Coffee machine", "name": "CM 6360 MilkPerfection","id": "abc123" }
  ]
}
```

**Rules:**
- `name` = human-readable product name (Unity displays it directly, never resolves `id`)
- `rows` must be exactly 7, in this order: Kitchen → Fridge → Cooktop → Hood → Microwave → Dishwasher → Coffee machine
- Both files live in `Onboarding/` at the repo root — not inside the Unity project

Unity picks up the output file automatically the moment it's written to disk.
