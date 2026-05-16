# CLAUDE.md

## Project
RoomRevive is an XR room makeover prototype.

It reconstructs a user’s real room (same layout, dimensions), then transforms it into different lifestyle expressions based on selected intents.

Core message:
"This isn’t just your room—this is how you want to live."

---

## Product Goal
Help users visualize their own kitchen transformed without changing layout.

This must feel like:
- the SAME room
- DIFFERENT ways of living in it

Avoid:
- full redesigns that break spatial recognition

---

## Final Intents

1. Calm & Unwind
2. Host & Gather
3. Fast & Focused

---

## Intent Definitions

### Calm & Unwind
Quiet, minimal, warm, restorative  
End-of-day decompression

### Host & Gather
Warm, social, inviting  
Designed for connection

### Fast & Focused
Efficient, structured, purposeful  
Optimized for speed

---

## Transformation Rules

Keep base layout constant.

Only modify:
1. Lighting (highest impact)
2. Surface styling
3. Activity cues (subtle motion)
4. Material/cabinet expression
5. Composition

---

## Product Interaction Model

Hotspots (minimal):
- cabinet edge → Nobilia
- fridge → Miele
- lighting → Neuhaus

On tap:
→ show compact side card FIRST

Compact card includes:
- 1 emotional/lifestyle sentence
- product name
- thumbnail
- CTA: "Explore options"

Expanded view (optional):
- variants
- specs
- price

Important:
DO NOT show heavy specs upfront.

---

## UX Principles

- Emotion before specification
- Same room, different life
- Minimal friction
- Clean, premium UI
- Make intents clearly distinct

---

## Visual Rules by Intent

### Calm & Unwind
- warm soft lighting (2700–3000K)
- 1–2 objects only
- subtle steam / glow
- matte cabinets
- centered composition

### Host & Gather
- brighter warm lighting
- curated table setup
- social cues
- layered visuals

### Fast & Focused
- neutral/cool lighting
- functional surfaces
- active cooking cues
- sharp structure

---

## Development Guidelines

- Preserve spatial recognition
- Separate:
  - intent system
  - product system
- Build modular systems:
  - intents
  - hotspots
  - products

- Avoid overengineering
- Optimize for clarity and iteration

---

## Working Style

When working:
1. Read CLAUDE.md + docs first
2. Propose a plan before coding
3. Implement in small steps
4. Keep code modular
5. Avoid unrelated changes

## Conventions for new code

Before writing any new code, **read `docs/CODE_CONVENTIONS.md`**. It covers the project's rules for folder structure, naming, file-size discipline, component access, and the MVP-flavoured architecture used for new subsystems. The IntentSelector is the canonical reference implementation.

Short version:

- New code lives under `Assets/RoomRevive/<Feature>/`. Never `Assets/Scripts/`.
- Folder structure mirrors the namespace (`Assets/RoomRevive/UI/IntentSelector/` ↔ `RoomRevive.IntentSelector`).
- Features are self-contained: their `Prefabs/`, `Data/`, `Docs/`, `Scripts/Runtime/`, `Scripts/Editor/` all live in one module folder.
- New subsystems follow the IntentSelector shape: ScriptableObject data + View prefab + UI-agnostic Controller + Routers for scene side effects. No `switch (intent.id)` in code.
- **All UI is prefab-first.** The visual hierarchy lives in a `.prefab` editable in Prefab Mode. Runtime code only `Bind()`s data into it. Never build `Image`/`TextMeshProUGUI` trees in `Awake` / `OnValidate`. If you need procedural sprites (rounded masks, gradients), bake them to PNG once via an editor menu — never at play time.
- **UnityEvents are the default** for anything a designer should be able to wire (state-selected callbacks, button clicks, gaze hooks). Reserve C# events for code-to-code communication a designer would never need to see.
- Every new subsystem ships with a **`DESIGNER_GUIDE.md`** in its `Docs/` folder. Use [`docs/DESIGNER_GUIDE_TEMPLATE.md`](docs/DESIGNER_GUIDE_TEMPLATE.md) as the starting point. The guide documents both editing paths: (A) hand-edit in Prefab Mode / inspector, (B) AI-assisted via Codex/Claude with the snapshot as input. The IntentSelector's [`DESIGNER_GUIDE.md`](RoomRevive_unity/Assets/RoomRevive/UI/IntentSelector/Docs/DESIGNER_GUIDE.md) is the reference.
- No VContainer, no `Interface` for things you'll never mock, no `Core/` dumping ground.

## Editing prefabs and Unity-serialized assets

Before changing any `.prefab`, `.unity`, or `.asset` file (or any code that regenerates one), **read `docs/AI_EDIT_PROTOCOL.md`**. Short version:

1. **Read the current state first.** For the IntentSelector, run `Tools/RoomRevive/Intent Selector/Export Snapshot` (or the 📸 button on `IntentSelectorController`) and read `Assets/RoomRevive/UI/IntentSelector/Snapshots/IntentSelectorUI.md`. That file is the ground truth for what the user currently has — code defaults may differ.
2. **Diff your goal against the snapshot.** What's already correct? What needs to change?
3. **Default to Sync, not Rebuild.** Sync is non-destructive (adds missing pieces, leaves overrides alone). Rebuild regenerates from scratch and wipes manual edits — only use it when the user explicitly asks.
4. **Never hand-write large prefab YAML.** Use the editor scripts under `Assets/RoomRevive/UI/IntentSelector/Scripts/Editor/`.