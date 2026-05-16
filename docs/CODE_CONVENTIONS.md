# Code & Folder Conventions

Rules for **new** code in RoomRevive. Existing code may not yet follow these — that's fine, migrate opportunistically when you're already editing the file. **Don't do big-bang renames or moves.**

Companion docs:
- [`AI_EDIT_PROTOCOL.md`](AI_EDIT_PROTOCOL.md) — how to make changes safely (read state → diff → minimal edit).
- [`../RoomRevive_unity/Assets/RoomRevive/UI/IntentSelector/Docs/IntentSelector_AI_Context.md`](../RoomRevive_unity/Assets/RoomRevive/UI/IntentSelector/Docs/IntentSelector_AI_Context.md) — the reference implementation of these conventions.

---

## TL;DR

1. **All project-authored content lives under `Assets/RoomRevive/`.** Never add new files to `Assets/Scripts/`.
2. **One folder per feature**, self-contained (Scripts + Prefabs + Data + Docs together).
3. **Folder structure mirrors the C# namespace.** `RoomRevive.IntentSelector` → `Assets/RoomRevive/UI/IntentSelector/`.
4. **MVP-flavoured architecture** for new subsystems (see `IntentSelector` for the reference).
5. **UI is prefab-first.** Visual hierarchy lives in `.prefab` assets, editable in Prefab Mode. Runtime code only `Bind()`s data — it does not build hierarchy.
6. **UnityEvents on routers** for inspector-wired side effects; **C# events** for code-to-code.
7. **Microsoft C# naming** (`PascalCase` types/methods, `camelCase` locals, `_camelCase` private fields).
8. File-size is a smell, not a rule. ~300 lines is a hint to extract; >500 is a real signal.
9. We do **not** use VContainer or other DI frameworks. Constructor injection via DI is a future option, not a current rule.

---

## 1. Folder structure

### Where new code goes

```
Assets/
├── RoomRevive/                     ← Everything we author lives here.
│   ├── Audio/                      ← Feature folders, named after the domain.
│   ├── Splat/
│   ├── Intent/
│   ├── Furniture/
│   ├── Hotspots/
│   ├── UI/
│   │   └── IntentSelector/         ← Self-contained module (see below).
│   └── Util/                       ← Genuinely cross-cutting helpers (e.g. KeyInput).
│
├── MetaXR/  Oculus/  Plugins/      ← Third-party — at default location, never reorganise.
├── Resources/  Settings/  XR/      ← Unity defaults.
└── Scenes/                         ← Existing scene location; new scenes prefer Assets/RoomRevive/Scenes/.
```

### Rules

- **One named root for project code.** `Assets/RoomRevive/` is non-negotiable. Don't create siblings (`Assets/MyStuff/`, `Assets/Scripts/`).
- **Feature folders, not architectural layers.** Prefer `Splat/`, `Intent/`, `Hotspots/` over `Models/`, `Views/`, `Managers/`. Layers fragment a feature across the project; features keep it together.
- **No `Core/` dumping ground.** If something feels like it belongs in `Core/`, ask: is it actually a feature with a name (`Audio`, `Splat`)? Put it there. If it's a 50-line utility used by 3+ features, put it in `Util/`.
- **Self-contained modules.** A module folder owns its assets:
  ```
  Assets/RoomRevive/UI/IntentSelector/
  ├── Prefabs/                  module's prefabs
  ├── Data/                     module's ScriptableObjects
  ├── Snapshots/                module's auto-generated state dumps
  ├── GeneratedSprites/         module's baked sprites
  ├── Docs/                     module's *_AI_Context.md
  └── Scripts/
      ├── Runtime/
      └── Editor/               Unity auto-routes to the editor assembly
  ```
  This means a feature can be understood, zipped, or removed as a unit.

### Folder ↔ namespace mapping

The namespace path must match the folder path under `Assets/RoomRevive/`:

| Folder | Namespace |
|---|---|
| `Assets/RoomRevive/UI/IntentSelector/Scripts/Runtime/Foo.cs` | `namespace RoomRevive.IntentSelector { class Foo … }` |
| `Assets/RoomRevive/Audio/Bar.cs` | `namespace RoomRevive.Audio { class Bar … }` |
| `Assets/RoomRevive/Util/KeyInput.cs` | global namespace (utility) |

Editor scripts inside a module live in `Scripts/Editor/` and use `<ModuleNamespace>.EditorTools`.

---

## 2. C# style

### Naming (Microsoft conventions — [reference](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names))

- `PascalCase` — types, methods, properties, public fields, constants.
- `camelCase` — local variables, parameters.
- `_camelCase` — private/protected fields.
- `I` prefix — interfaces (`IIntentRouter`).
- No Hungarian notation, no `m_` prefix in new code.

### Write code like a book

- Method names read like sentences: `BindCardsFromCatalog`, not `DoStuff` or `Process`.
- Avoid abbreviations beyond domain-standard ones (XR, UI, SO, etc.).
- A non-coder skimming a class should grasp roughly what it does in 30 seconds.

### KISS / DRY / SOLID applied

- **KISS first.** When in doubt, the simpler solution wins.
- **DRY second.** Don't deduplicate two callsites that *happen* to look the same today. Wait for the third before extracting.
- **SOLID third.** Apply Single-Responsibility and Dependency-Inversion when you feel real pain. Skip the others until something concrete demands them. No interfaces "for testability" until there's a test.

### File size

- ~300 lines: smell. If you're already editing the file, consider extracting.
- >500 lines: real signal. Plan a split before adding more.
- Existing offenders (`SplatManager.cs` ~1500) get refactored *next time you're in there for other work*, not as a dedicated pass.

---

## 3. Component access

### Required dependencies on the same GameObject

```csharp
[RequireComponent(typeof(Canvas))]
public class MyView : MonoBehaviour
{
    Canvas _canvas;
    void Awake() => _canvas = GetComponent<Canvas>();
}
```

`[RequireComponent]` guarantees the component exists; the `GetComponent` call is cached once in `Awake`.

### Scene-wide singletons

OK to use a `GetOrFindInstance()` pattern in `Awake`/`OnEnable`/`Start`, **never per-frame**:

```csharp
void Awake() => _audio = AudioManager.GetOrFindInstance();
void Update() => _audio.DoStuff();  // cached reference, no Find call
```

Existing managers (`SplatManager.Instance`, `AudioManager.Instance`, `ParticlesManager.Instance`) are fine.

### `FindObjectOfType` and friends

- Editor scripts: always fine.
- Runtime `Awake`/`Start`: fine for auto-find fallback.
- Runtime per-frame: **never** — cache the reference once.

### Inspector-wired references

Prefer `[SerializeField]` references over `Find*` calls when the relationship is fixed. Drop the dependency into the inspector slot.

---

## 4. Architecture for new subsystems — MVP-flavoured

We use the same shape that the `IntentSelector` subsystem uses. **When in doubt, copy that pattern.**

| Role | Lives in | Responsibilities |
|---|---|---|
| **Model (Data)** | ScriptableObjects (`*Data.asset`) + Catalogs | State + content. No scene refs. No behaviour beyond simple helpers. |
| **View** | `MonoBehaviour` on prefab | Visual hierarchy, animations, pointer events. Knows nothing about scene side effects. |
| **Presenter (Controller)** | `MonoBehaviour`, UI-agnostic | Owns selection / current state. Raises events. **Never** references `SplatManager`, `AudioManager`, or specific GameObjects. |
| **Routers** | `MonoBehaviour` | Listen to Presenter events, perform scene side effects (audio, splat swap, visibility). One router per concern. |

### Why this shape

- **Testable presenter.** No Unity-API calls in the selection logic.
- **No hard-coded switch statements.** Adding a new state = adding an SO + a router binding. Zero code edits.
- **View can be reskinned.** Replace the prefab without touching logic.

### Reference implementation

`Assets/RoomRevive/UI/IntentSelector/` is the canonical example.

| New subsystem need | Pattern to copy from IntentSelector |
|---|---|
| A list of selectable things (rooms, products, modes) | `IntentStateCatalog` + `IntentStateData` |
| Visual styling shared across items | `IntentSelectorTheme` ScriptableObject |
| One-prefab-per-item with shared template | `Card_*.prefab` + nested prefab instances |
| Inspector-editable scene side effects | `IntentUnityEventRouter` |
| Audio / visibility wiring driven by data | `IntentAudioRouter`, `IntentVisibilityRouter` |
| One-click setup of all the above | `IntentSelectorPrefabCreator` editor menu |

---

## 5. UI: prefab-first, never code-generated

All UI in RoomRevive is **authored as prefabs and edited in Prefab Mode.** Runtime/`Awake`/`OnValidate` code may bind data into a prefab, but it must **not** build hierarchy, instantiate `Image`/`TextMeshProUGUI` components, or generate sprites every frame.

### Why this rule exists

The legacy `IntentCardSelectorUI` did the opposite — it built ~30 `Image`/TMP/`Button` components from code in `OnValidate`. The result:
- Every inspector field change tore down and rebuilt the UI, flashing the screen.
- The visual was not editable in Prefab Mode (anything you changed got wiped on next rebuild).
- A 2000-line script that nobody else could maintain.
- Generated textures leaked memory unless cleaned up perfectly.
- No way to A/B variants, no way to override per-state.

The refactor replaced it with editable prefab assets + ScriptableObject data + a thin `Bind()` step at runtime. That is the only acceptable shape going forward.

### Rules for new UI

1. **The visual hierarchy is a `.prefab` asset.** A human can double-click it, open Prefab Mode, drag a child, change a font size, save. That's the authoritative source of how the UI looks.
2. **Per-variant prefabs use nested prefab instances.** If you have 3 cards with the same shape and different content, make 3 prefabs (`Card_Calm.prefab`, `Card_Host.prefab`, `Card_Fast.prefab`). Nest them inside the parent container prefab so each shows up as an editable child in Prefab Mode.
3. **Runtime code only `Bind`s data into the prefab.** A `View` MonoBehaviour holds serialized references to its `TextMeshProUGUI`, `Image`, etc. children. The `Bind(stateData, index, controller)` method assigns `text`, `sprite`, `color` — nothing more.
4. **Sprites, fonts, themes are project assets.** Not generated at play time. If you need procedurally-generated sprites (rounded masks, gradients), bake them once via an **editor menu** that writes PNG assets to disk. See `IntentSelectorSpriteFactory.cs`.
5. **One-time prefab construction belongs in an editor script, not runtime.** It's fine — encouraged, even — to have a `MyFeaturePrefabCreator` editor menu that *builds* the prefab once when you click it. The prefab is then the authoritative artifact. The script is a setup tool, not a runtime renderer.
6. **`OnValidate` must not rebuild hierarchy.** It may set values on existing serialized references. It may not call `new GameObject(...)` or `AddComponent<Image>()`.
7. **Expose Snapshot / Sync / Rebuild on UI tools** (per [`AI_EDIT_PROTOCOL.md`](AI_EDIT_PROTOCOL.md)) so manual prefab edits aren't silently wiped. The 📸 / 🔗 / ⚠ buttons on `IntentSelectorController` are the reference UX.

### Acceptable shape, by example

```
Assets/RoomRevive/UI/<Feature>/
├── Prefabs/
│   ├── <Feature>UI.prefab           ← the main UI prefab, editable
│   └── Cards/                        ← per-variant nested-prefab children
│       ├── Card_A.prefab
│       └── Card_B.prefab
├── Data/                             ← ScriptableObjects bound into the UI at runtime
├── GeneratedSprites/                 ← baked PNGs, only via editor menu
├── Snapshots/                        ← auto-dumped state for AI/devs to diff against
└── Scripts/
    ├── Runtime/
    │   ├── <Feature>Controller.cs    ← owns state, raises events
    │   ├── <Feature>View.cs          ← holds prefab refs, calls Bind
    │   ├── <Feature>CardView.cs      ← one-card visual
    │   └── *Router.cs                ← side effects via UnityEvents
    └── Editor/
        ├── <Feature>PrefabCreator.cs ← one-shot menu / inspector button
        ├── <Feature>SpriteFactory.cs ← bakes any procedural sprites once
        └── <Feature>SnapshotExporter.cs
```

### Anti-patterns to reject

- Building `GameObject`/`Image`/`TextMeshProUGUI` trees inside `Awake`, `OnEnable`, or `OnValidate`.
- "Helper" methods like `MakeUIObject(parent, name)` called dozens of times to construct a layout at startup.
- Procedural sprite generation (`Texture2D.SetPixels32` + `Sprite.Create`) at runtime. Bake to a PNG once via an editor menu.
- Per-frame `Instantiate(prefab)` calls — pool or pre-place.
- "Mode flags" like `useGeneratedRoundedCorners` that switch between procedural and asset-based rendering. Pick the asset path.

---

## 6. Events: UnityEvents vs. C# events

**Default to UnityEvents.** A core goal of this project is that designers can wire and rewire scene behaviour without touching code. C# events should be the exception, not the rule.

| Use this | When |
|---|---|
| **`UnityEvent<T>`** on a `MonoBehaviour` | **Default for anything a designer should be able to hook up** — buttons, state selections, gaze interactions, completion callbacks. The other end is a scene object dropped into the Inspector. Routers in the IntentSelector all use this. |
| **`event Action<T>`** (C# event) | Code-to-code only, where the relationship is fixed and a designer should never see or change it (e.g. internal communication between two scripts on the same prefab that the designer wouldn't recognise). |
| **`static Action<T>`** | Global broadcast. Cheap to subscribe from anywhere. Use sparingly — these are hidden coupling. |

### Why UnityEvents are the default

- Designers can wire `SplatManager.SetCalmRoom()` into a `UnityEvent` slot in the inspector — no developer round-trip.
- New states / interactions don't require code edits, only inspector edits.
- Behaviour is **inspectable** in the inspector — you can see what fires on what without reading source.
- AI assistants can describe wiring changes in plain English ("drag SplatManager into the onSelected slot of the Host binding") that the designer can execute.

The IntentSelector uses all three:
- `IntentUnityEventRouter.bindings[i].onSelected` → UnityEvent (designer wires `SplatManager.SetCalmRoom()`).
- `IntentSelectorController.onStateSelected` → UnityEvent (designer can also use this, and the prefab wires it to routers automatically).
- `IntentSelectorController.OnIntentSelected` → static `Action<int>` (legacy compatibility for `IntentManager`).

---

## 7. What we deliberately don't adopt (and when to reconsider)

### VContainer / Zenject / DI frameworks

**Not adopting now.** Reasons:
1. Existing singletons (`SplatManager.Instance` etc.) would require a multi-day refactor with no behavioural improvement.
2. Mixing DI with singletons is the worst of both worlds.
3. Unity's `[SerializeField]` is already a form of inspector-driven DI for this project's scale.
4. Meta SDK / XR components don't play cleanly with constructor injection.

**Reconsider when:**
- Team grows past 2 active engineers.
- Unit tests for presenters/services become a real need.
- Project transitions from prototype to product.

### "Interfaces by default" for testability

Don't. Make an interface when you have a concrete reason to mock or swap implementations. A `class Foo` is cheaper than `interface IFoo + class Foo` and easier to read.

### `Models/`, `Views/`, `Controllers/` top-level folders

Don't. Feature folders only. The MVP roles live inside each feature's namespace.

---

## 8. Checklist before adding a new module

Use this as a pre-commit mental scan:

- [ ] Folder is under `Assets/RoomRevive/<Feature>/`, not at `Assets/Scripts/` or elsewhere.
- [ ] Namespace matches the folder path.
- [ ] Scripts are split into `Scripts/Runtime/` and (if needed) `Scripts/Editor/`.
- [ ] Data lives as ScriptableObjects under the module's `Data/` folder.
- [ ] Prefabs live under the module's `Prefabs/` folder.
- [ ] If there are state-driven side effects, they go through a Router with UnityEvent bindings — not a switch statement.
- [ ] No `Update()` calls to `GetComponent` / `FindObjectOfType` — cache in `Awake`.
- [ ] No file >500 lines without a comment explaining why a split is deferred.
- [ ] If the module regenerates assets, expose a **Snapshot / Sync / Rebuild** trio per the AI Edit Protocol.
- [ ] **UI is authored as a `.prefab`** in `Prefabs/`, editable in Prefab Mode. No `GameObject` / `Image` / `TextMeshProUGUI` construction in `Awake`, `OnEnable`, or `OnValidate`. The runtime only `Bind()`s data into the existing prefab.
- [ ] **Designer-facing side effects use `UnityEvent<T>`**, not C# events. A designer should be able to wire a new scene action without touching code.
- [ ] **A `DESIGNER_GUIDE.md` exists** at `Assets/RoomRevive/<…>/<Feature>/Docs/DESIGNER_GUIDE.md`, filled in from [`docs/DESIGNER_GUIDE_TEMPLATE.md`](DESIGNER_GUIDE_TEMPLATE.md). It documents both editing paths (Prefab Mode + AI-assisted), lists the common tasks, and calls out what designers should NOT change without help.
