# Onboarding Data Bridge

How the Unity onboarding UI (Track 1) exchanges data with the Selection Core (Track 2)
and the Visualizer (Track 3) using plain JSON files on disk.

---

## Design principles

**Unity never loads `catalog.json`.** It only writes four answer values and reads a
pre-resolved result. All catalog logic lives in Track 2.

**All files live outside `Assets/`** — in the shared `Onboarding/` folder next to the
Unity project. Replacing any file takes effect on the next Play-mode run with no Unity
reimport required.

**ReadyUI is fully self-contained.** It reads `onboarding_selection.json` directly
via its own `FileSystemWatcher`. It does not depend on `OnboardingFlowController` for
data — you can drop it into any scene and it will update as soon as the file changes.

---

## File locations

All paths are relative to the repository root
(`C:/…/XR_hackathon_projects/RoomRevive/`).

| File | Owner | Direction |
|---|---|---|
| `Onboarding/shared/catalog.json` | Track 2 | Track 2 reads; Unity never touches |
| `Onboarding/onboarding_answers.json` | Track 1 (Unity) writes | Track 2 reads |
| `Onboarding/onboarding_selection.json` | Track 2 writes | Track 1 (Unity) reads |

In Unity **editor** mode these paths resolve to the actual repo folder via
`Application.dataPath + "/../.."`.

On a **Quest device** they resolve to `Application.persistentDataPath` — push/pull
files via ADB:
```
adb push onboarding_selection.json /sdcard/Android/data/<package>/files/
adb pull /sdcard/Android/data/<package>/files/onboarding_answers.json .
```

---

## Step-by-step data flow

```
[User completes Q1–Q4]
        │
        ▼
[ReviewPanel animation plays]
        │
        ▼  OnBuildBReady() in OnboardingFlowController
[Unity writes onboarding_answers.json]      ←── OnboardingBridge.SubmitAnswers()
[Unity spawns: node track2_selection_core/cli.js]  ←── OnboardingBridge.RunSelectionCore()
[Unity shows ReadyUI]
        │
        ▼  ReadyUI.OnEnable → OnboardingReadyController
[ReadyUI hides product rows, starts "Transforming …" dots]
[ReadyUI starts its own FileSystemWatcher on onboarding_selection.json]
[If file already exists on disk, reads it immediately]
        │
        │  (node process runs in background, ~instant)
        ▼
[cli.js reads onboarding_answers.json + catalog.json]
[cli.js runs selection_core.js]
[cli.js resolves product names, writes onboarding_selection.json]
        │
        ▼  FileSystemWatcher fires (or immediate read on OnEnable)
[ReadyUI parses SelectionResult JSON]
[ReadyUI updates product name text in each row]
[Product rows stagger in with 0.07s delay each]
```

If `onboarding_selection.json` is not written within **4 seconds** (e.g. Node.js not
installed), the ReadyUI falls back and fades in the placeholder names baked into the prefab.

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

Unity parses **only** `intent`, `tagline`, and `rows`. The extra fields (`profile`,
`kitchens`, `appliances`, `answers`) are written by `cli.js` for Track 3 and ignored by Unity.

```json
{
  "intent":  "Host & Gather",
  "tagline": "warm, social, ready for guests",
  "rows": [
    { "category": "Kitchen",        "name": "TOUCH 337",            "id": "698ecf189cbe241c" },
    { "category": "Fridge",         "name": "KFN 4795 AD",          "id": "531c55b910e270b8" },
    { "category": "Cooktop",        "name": "KM 7464 FL",           "id": "a74e3b4450166891" },
    { "category": "Hood",           "name": "DAC 2940 Stella",      "id": "ec74e0e9e03c013a" },
    { "category": "Microwave",      "name": "M 2240 SC",            "id": "..." },
    { "category": "Dishwasher",     "name": "G 5632 SCU Active S",  "id": "..." },
    { "category": "Coffee machine", "name": "CM 6360 MilkPerfection","id": "..." }
  ],
  "profile":    { ... },
  "kitchens":   { ... },
  "appliances": [ ... ],
  "answers":    { ... }
}
```

**Critical for Unity:** `name` must be the human-readable product name already resolved
from `catalog.json`. Unity displays it directly and never resolves `id` at runtime.

**For Track 3:** `id` is preserved so the visualizer can look up full catalog records
(images, finishes, dimensions) without re-running selection.

Row order must match the ReadyUI card order:
Kitchen → Fridge → Cooktop → Hood → Microwave → Dishwasher → Coffee machine.

---

## Unity-side components

### `OnboardingBridge.cs` — on `OnboardingFlowUI` root

Added by **Phase 7** builder. Handles the write side only.

| Member | Role |
|---|---|
| `answersRelativePath` | Inspector-configurable. Default: `Onboarding/onboarding_answers.json` |
| `selectionRelativePath` | Inspector-configurable. Default: `Onboarding/onboarding_selection.json` |
| `SubmitAnswers(style, tone, household, budget)` | Called by FlowController on review complete. Writes answers JSON and spawns node. |
| `RunSelectionCore()` | Internal. Spawns `node track2_selection_core/cli.js` as a background process. |
| `onSelectionReceived` (UnityEvent\<string\>) | Fires when `onboarding_selection.json` changes. Available for custom wiring in the Inspector; not used by built-in components. |

---

### `OnboardingFlowController.cs` — on `OnboardingFlowUI` root

Manages Q1–Q4 → Review → ReadyUI navigation. **Does not pass data to ReadyUI.**

On review animation complete (`OnBuildBReady`):
1. Calls `bridge.SubmitAnswers(q1, q2, q3, q4)` — writes answers + spawns node
2. Calls `GoToPage(5)` — shows ReadyUI

The `_readyUI` Inspector field must point to the **ReadyUI scene instance** so
FlowController can call `SetActive(true)` at the right moment.
ReadyUI handles its own data loading once active.

---

### `OnboardingReadyController.cs` — on `ReadyPanel` inside `ReadyUI`

Self-contained. No dependency on FlowController for data.

| Member | Role |
|---|---|
| `_rowGroups` | Array of `CanvasGroup` — one per product row. Alpha driven by stagger animation. **Serialized by Phase 8b builder.** |
| `_productTmps` | Array of `TextMeshProUGUI` — right-column product name per row. Updated by `BindData`. **Serialized by Phase 8b builder.** |
| `_noteTmp` | The "Transforming your kitchen …" TMP. Dots animated while waiting. |
| `_selectionRelativePath` | Path to watch, relative to repo root. Default matches Bridge. |
| `_dataTimeoutSeconds` | Fallback timer (default 4s). Rows fade in with placeholder names if data doesn't arrive in time. |

**`OnEnable` behaviour:**
1. Resets state, restarts coroutine
2. Starts `FileSystemWatcher` on `onboarding_selection.json`
3. If file already exists on disk, reads and applies it immediately (no wait for file event)

**`BindData(SelectionRow[] rows)`:** updates each `_productTmps[i].text` and sets
`_dataReady = true`, which unblocks the stagger-in animation.

---

## Setup checklist (first-time / after code changes)

Run these Unity editor menu items **in order** before entering Play Mode:

1. `Tools → RoomRevive → Onboarding → Phase 0` — bake sprites
2. `Tools → RoomRevive → Onboarding → Phase 1` — build Q1 panel
3. `Tools → RoomRevive → Onboarding → Phase 4` — build Q2 panel
4. `Tools → RoomRevive → Onboarding → Phase 5` — build Q3 panel
5. `Tools → RoomRevive → Onboarding → Phase 6` — build Q4 panel
6. `Tools → RoomRevive → Onboarding → Phase 8a` — build Review panel
7. `Tools → RoomRevive → Onboarding → Phase 7` — add FlowController + Bridge to root
8. `Tools → RoomRevive → Onboarding → Phase 8b — Build Ready Page (static)` — build ReadyUI prefab

After Phase 8b: drag `Assets/Onboarding/Prefabs/ReadyUI.prefab` into the scene, then
drag that scene instance into the `OnboardingFlowUI → OnboardingFlowController → _readyUI` field.

> **Important:** Phase 8b serializes `_rowGroups` and `_productTmps` refs into the prefab.
> If ReadyUI shows placeholder product names, re-run Phase 8b and re-drag the prefab.

---

## Track 2 integration

`track2_selection_core/cli.js` is already implemented and handles the full pipeline.
Unity spawns it automatically — no manual step required.

**What `cli.js` does:**
1. Reads `Onboarding/onboarding_answers.json`
2. Reads `shared/catalog.json`
3. Runs `selection_core.js` against both
4. Resolves each `topPick.id` → display name via `item.product.name`
5. Writes `Onboarding/onboarding_selection.json`

**Manual re-run** (e.g. to test with different answers without entering Play Mode):
```
node Onboarding/track2_selection_core/cli.js
```

**Requirements:** Node.js must be installed and on PATH. If Unity logs
`Could not launch node`, install Node from https://nodejs.org.

---

## Replacing catalog.json

Drop a new `shared/catalog.json` in place. No Unity rebuild or reimport needed.
Track 2 picks it up on the next run. Unity never reads it.

---

## Testing without Track 2

Manually write `Onboarding/onboarding_selection.json` with the schema above.
`OnboardingReadyController` watches for file changes — ReadyUI updates within one frame.

You can also test ReadyUI standalone (without going through Q1–Q4):
1. Make sure `onboarding_selection.json` exists on disk
2. Set `ReadyUI` active in the Hierarchy
3. ReadyUI reads the file on `OnEnable` and populates rows immediately
