# Onboarding Data Bridge

How the Unity onboarding UI (Track 1) exchanges data with the Selection Core (Track 2)
and the Visualizer (Track 3) using plain JSON files on disk.

---

## Design principle

**Unity never loads `catalog.json`.** It only writes four answer values and reads a
pre-resolved result. All catalog logic lives in Track 2.

**All files live outside `Assets/`** — in the shared `Onboarding/` folder next to the
Unity project. Replacing any file takes effect on the next Play-mode run with no Unity
reimport required.

---

## File locations

All paths are relative to the repository root
(`C:/…/XR_hackathon_projects/RoomRevive/`).

| File | Owner | Direction |
|---|---|---|
| `Onboarding/shared/catalog.json` | Track 2 | Track 2 reads; Unity never touches |
| `Onboarding/onboarding_answers.json` | Track 1 (Unity) writes | Track 2 reads |
| `Onboarding/onboarding_selection.json` | Track 2 writes | Track 1 (Unity) reads |

In Unity **editor** mode these paths resolve to the actual repo folder.
On a **Quest device** they resolve to `Application.persistentDataPath` — push/pull
files via ADB:
```
adb push onboarding_selection.json /sdcard/Android/data/<package>/files/
adb pull /sdcard/Android/data/<package>/files/onboarding_answers.json .
```

The path logic lives in `OnboardingBridge.cs` (`ProjectRoot` property).

---

## Step-by-step data flow

```
[User completes Q1–Q4]
        │
        ▼
[ReviewPanel animation plays]
        │
        ▼  OnBuildBReady() in OnboardingFlowController
[Unity writes onboarding_answers.json]  ←── OnboardingBridge.SubmitAnswers()
[Unity shows ReadyUI — dots animate, rows hidden]
[Unity starts FileSystemWatcher on onboarding_selection.json]
        │
        │  (Track 2 CLI runs in parallel)
        ▼
[Track 2 reads onboarding_answers.json]
[Track 2 runs selection against catalog.json]
[Track 2 writes onboarding_selection.json]
        │
        ▼  FileSystemWatcher fires → OnboardingBridge.onSelectionReceived event
[Unity parses SelectionResult JSON]
[Unity calls OnboardingReadyController.BindData(rows)]
[ReadyUI product rows update + stagger in]
```

If `onboarding_selection.json` is not written within **4 seconds**, the ReadyUI falls
back and fades in whatever placeholder names are baked into the prefab.

---

## JSON schemas

### `onboarding_answers.json` (Unity → Track 2)

```json
{
  "style":     "modern",
  "tone":      "light",
  "household": "compact",
  "budget":    "Signature"
}
```

| field | valid values |
|---|---|
| `style` | `modern` · `designer` · `cottage style` · `natural & scandinavian` |
| `tone` | `light` · `dark` · `wood` · `bold` |
| `household` | `compact` · `standard` · `host` |
| `budget` | `Essential` · `Signature` · `Premium` · `any` |

These are the internal values (not display labels). Source of truth: `shared/OUTPUT_CONTRACT.md`.

---

### `onboarding_selection.json` (Track 2 → Unity)

```json
{
  "intent":  "Fast & Focused",
  "tagline": "bright, efficient — in, fed, and out",
  "rows": [
    { "category": "Kitchen",        "name": "TOUCH 337",            "id": "698ecf189cbe241c" },
    { "category": "Fridge",         "name": "KDN 4174 E Active",    "id": "531c55b910e270b8" },
    { "category": "Cooktop",        "name": "CS 7612 FL",           "id": "a74e3b4450166891" },
    { "category": "Hood",           "name": "DA 1260",              "id": "ec74e0e9e03c013a" },
    { "category": "Microwave",      "name": "M 2224 SC",            "id": "..." },
    { "category": "Dishwasher",     "name": "G 5540 SCU SL Active", "id": "..." },
    { "category": "Coffee machine", "name": "CM 5310 Silence",      "id": "..." }
  ]
}
```

**Critical for Unity:** the `name` field must be the human-readable product name
already resolved from `catalog.json`. Unity displays `name` directly — it does not
resolve `id` at runtime.

**Useful for Track 3:** the `id` field is preserved so the visualizer can look up the
full catalog record (images, finishes, dimensions, etc.) without re-running selection.

Row order must match the display order in the ReadyUI card:
Kitchen → Fridge → Cooktop → Hood → Microwave → Dishwasher → Coffee machine.

---

## Unity-side components

### `OnboardingBridge.cs` — `RoomRevive.Onboarding`

MonoBehaviour on the root `OnboardingFlowUI` GameObject (added by **Phase 7** builder).

| Member | Role |
|---|---|
| `answersRelativePath` | Configurable path for the answers file (default: `Onboarding/onboarding_answers.json`) |
| `selectionRelativePath` | Configurable path for the selection file (default: `Onboarding/onboarding_selection.json`) |
| `SubmitAnswers(style, tone, household, budget)` | Called by FlowController. Writes answers JSON and starts watching. |
| `onSelectionReceived` (UnityEvent\<string\>) | Fires on the main thread when the selection file changes. Payload is raw JSON. |

### `OnboardingFlowController.cs`

On review animation complete (`OnBuildBReady`):
1. Calls `bridge.SubmitAnswers(q1Value, q2Value, q3Value, q4Value)`
2. Shows ReadyUI (`GoToPage(5)`)

On `onSelectionReceived`:
1. Parses JSON into `SelectionResult`
2. Calls `readyCtrl.BindData(result.rows)`

### `OnboardingReadyController.cs`

On enable: hides rows, starts dots, waits for `BindData()` or 4s timeout.

`BindData(SelectionRow[] rows)`: sets each `Product` TMP text, then triggers row
stagger-in animation.

---

## Track 2 integration checklist

- [ ] Read `Onboarding/onboarding_answers.json` on file change (or poll)
- [ ] Run `selection_core.js` with those answers + `shared/catalog.json`
- [ ] For each `topPick.id`, resolve `name` from catalog (`item.name` or `item.product.name`)
- [ ] Write `Onboarding/onboarding_selection.json` in the schema above
- [ ] Rows must be in this order: Kitchen, Fridge, Cooktop, Hood, Microwave, Dishwasher, Coffee machine
- [ ] Write atomically (write to `.tmp`, then rename) to avoid partial reads

---

## Replacing catalog.json

Drop a new `shared/catalog.json` in place. No Unity rebuild or reimport needed.
Track 2 picks it up on next run. Unity never reads it.

---

## Testing without Track 2

Manually write `Onboarding/onboarding_selection.json` with the schema above.
The `FileSystemWatcher` fires as soon as the file is written or modified.
Unity's ReadyUI will update within one frame.
