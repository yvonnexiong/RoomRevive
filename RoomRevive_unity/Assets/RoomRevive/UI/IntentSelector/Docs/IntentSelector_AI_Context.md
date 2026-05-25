# IntentSelector — AI Context

This document is the source of truth for the IntentSelector subsystem.
Read this before editing any code in `Assets/RoomRevive/UI/IntentSelector/`.

## Read this first

Before changing anything in this subsystem:

1. Read **[`docs/AI_EDIT_PROTOCOL.md`](../../../../docs/AI_EDIT_PROTOCOL.md)** — the project-wide rule is *read current state, then edit minimally*.
2. Read **`Snapshots/IntentSelectorUI.md`** — auto-generated dump of the current prefab + data state. If it's missing or stale, regenerate it via `Tools/RoomRevive/Intent Selector/Export Snapshot` (or the 📸 button in the controller inspector). **Snapshot is the source of truth for "what the user currently has." Code defaults are the source of truth for "what a fresh project would have."** They drift.
3. Read **[`DESIGNER_GUIDE.md`](DESIGNER_GUIDE.md)** to understand what designers can change without developer help, and the common task recipes they follow. Many "code change" requests are actually designer-safe inspector edits — your job is to recognise that and route accordingly.
4. Diff your goal against the snapshot. Only touch what's missing or wrong.
5. Default to **Sync** (non-destructive) over **Rebuild** (destructive). Only Rebuild when the user explicitly asks for a from-scratch regen.

## TL;DR

- **Prefab-based**, **data-driven**. Cards come from `IntentStateData` ScriptableObjects in an `IntentStateCatalog`.
- **Do not rebuild layout from runtime code.** The visual UI lives in `IntentSelectorUI.prefab`. Runtime clones the `CardTemplate` once per catalog state at `Start`.
- **No C# switch-case for adding states.** Routers map states to UnityEvents / scene objects via inspector lists.

## Source of truth

| Concern | Lives in |
|---|---|
| Card content (title, subtitle, image, icon, audio clip, visibility flags) | `IntentStateData` asset |
| Card order and count | `IntentStateCatalog.states` |
| Visual look-and-feel (scale, overlay alpha) | `IntentSelectorTheme` asset |
| Selection state (selected/hovered/pressed) | `IntentSelectorController` (scene component) |
| Visual hierarchy | `IntentSelectorUI.prefab` |
| Card visual template (fallback) | `CardTemplate` inside the prefab |
| Per-state card prefab (primary) | `IntentStateData.cardPrefab` → an asset under `Prefabs/Cards/` |

## Folder layout

```
Assets/RoomRevive/UI/IntentSelector/
├── Prefabs/
│   ├── IntentSelectorUI.prefab
│   └── Cards/
│       ├── Card_Calm.prefab
│       ├── Card_Host.prefab
│       └── Card_Fast.prefab
├── Data/
│   ├── IntentStateCatalog.asset
│   ├── IntentState_Calm.asset
│   ├── IntentState_Host.asset
│   ├── IntentState_Fast.asset
│   └── IntentSelectorTheme.asset
├── Scripts/
│   ├── Runtime/
│   │   ├── IntentStateData.cs
│   │   ├── IntentStateCatalog.cs
│   │   ├── IntentSelectorTheme.cs
│   │   ├── IntentStateEvent.cs
│   │   ├── IntentSelectorController.cs
│   │   ├── IntentSelectorView.cs
│   │   ├── IntentCardView.cs
│   │   ├── IntentCardPointerProxy.cs
│   │   ├── IntentUnityEventRouter.cs
│   │   ├── IntentAudioRouter.cs
│   │   ├── IntentVisibilityRouter.cs
│   │   ├── MetaWorldSpaceCanvasSetup.cs
│   │   └── HeadFollowWorldUI.cs
│   └── Editor/
│       ├── IntentSelectorPrefabBinder.cs
│       └── IntentSelectorPrefabCreator.cs
└── Docs/
    └── IntentSelector_AI_Context.md
```

## Prefab object names (load-bearing — the editor binder finds children by these names)

- `IntentSelectorUI` (root)
- `ISDK_RayInteractionSurface`
- `ViewRoot`
- `HeaderPill`
- `CardsContainer`
- `CardTemplate`
- `Visual`
- `ImageArea`
- `LabelArea`
- `IconContent`
- `TitleText`
- `SubtitleText`
- `StateOverlay`

Renaming any of these breaks the binder. Add new children freely — they won't be touched.

## Adding a new room / intent state

No code changes required. Steps:

1. **Create asset:** Right-click in `Data/` → `Create / RoomRevive / Intent Selector / Intent State`. Set `id`, `displayName`, `subtitle`, sprites, `atmosphereMusic` (optional), visibility flags.
2. **Card prefab:** either (a) duplicate an existing `Prefabs/Cards/Card_*.prefab`, rename it, edit it in Prefab Mode, and drag it into the new state's `cardPrefab` slot — **or** (b) leave `cardPrefab` empty to use the shared `CardTemplate` from the main prefab.
3. **Add to catalog:** Drag the new SO into `IntentStateCatalog.states`. Position determines visual order.
4. **Optional — scene wiring:** On the prefab/scene's `IntentUnityEventRouter`, add a binding entry with the new SO and wire `onSelected` to whatever scene action you want (e.g. `SplatManager.SetHostKitchenNewCabinetA()`).
5. **Optional — visibility:** If the state needs to toggle GameObjects beyond the four canonical ones (ProductUI / CabinetUI / Fridges / Cabinets), add an `IntentVisibilityBinding` on `IntentVisibilityRouter`.

That's it. No switch-case to update, no enum to extend.

## How cards are wired in the prefab

When `Tools/RoomRevive/Intent Selector/Create Default Assets And Prefab` runs:

1. It bakes `Card_Calm.prefab`, `Card_Host.prefab`, `Card_Fast.prefab` under `Prefabs/Cards/` and assigns each to its `IntentStateData.cardPrefab`.
2. It then builds `IntentSelectorUI.prefab` with those card prefabs **already nested** inside `CardsContainer` as linked prefab instances. The shared `CardTemplate` stays in the prefab as an inactive fallback for any future state that doesn't author its own card.
3. `IntentSelectorView.instantiateCardsFromCatalog` is set to `false` and `cardViews` is pre-populated, so at runtime the view just rebinds the children — no re-instantiation, no flicker.

Two editing entry points:
- **Open `IntentSelectorUI.prefab` in Prefab Mode** → tweaks live as overrides on the nested card instances (good for one-off layout adjustments per slot).
- **Open `Card_Calm.prefab` (or sibling) in Prefab Mode** → edits become the new baseline for every place that uses it (good for restyling the whole look).

## Architecture

```
IntentSelectorController            ← owns selection state, raises events
        │
        │ onStateSelected(IntentStateData)
        ├──────────────► IntentUnityEventRouter   (Splat actions / arbitrary UnityEvents)
        ├──────────────► IntentAudioRouter         (plays state.atmosphereMusic)
        └──────────────► IntentVisibilityRouter    (toggles GameObjects via flags / bindings)

IntentSelectorView                  ← owns CardTemplate + cardsContainer
        │
        ├── Instantiate(CardTemplate) × catalog.Count at Start
        │
        └── Each card: IntentCardView + IntentCardPointerProxy
                       (forwards pointer events to controller)
```

`IntentSelectorController` has **no** scene-side-effect logic — it just selects and emits. Side effects are wired via UnityEvents on the routers, which is what makes the system data-driven without code changes.

## Meta Interaction SDK

`MetaWorldSpaceCanvasSetup` (on the prefab root) sets up:
- `Canvas` (world space) + `CanvasScaler` + `GraphicRaycaster`
- `PointableCanvas`
- A child `ISDK_RayInteractionSurface` with `PlaneSurface` + `BoundsClipper` + `RectTransformBoundsClipperDriver` + `ClippedPlaneSurface` + `RayInteractable`
- An `EventSystem` with `PointableCanvasModule` (created if missing)

This component is independent of card UI — it only configures the canvas and ray plumbing.

## Bootstrapping the system

Menu: **Tools / RoomRevive / Intent Selector / Create Default Assets And Prefab**

This:
1. Creates folders if missing.
2. Creates default SOs (Theme, Calm, Host, Fast, Catalog).
3. Builds `IntentSelectorUI.prefab` with the full hierarchy and component wiring.
4. Wires controller `onStateSelected/onStateConfirmed` → routers via persistent UnityEvent listeners.

Re-run anytime — existing assets are reused, prefab is overwritten.

**Manual prefab tweaks:** edit `IntentSelectorUI.prefab` in Prefab Mode. The runtime view will not regenerate it. To re-bind serialized references after renaming child objects, run:

**Tools / RoomRevive / Intent Selector / Rebind Intent Selector Prefab** (with the prefab root selected).

## Backwards compatibility

The legacy `RoomRevive.IntentCardSelectorUI` component has been reduced to a thin wrapper.
- Same GUID → existing scene references still resolve.
- Same `OnIntentSelected` static `Action<int>` → existing listeners (`IntentManager`) still fire.
- Methods `SelectCard / SelectPreviousCard / SelectNextCard / ConfirmSelection / Show / Hide` forward to the new controller.

To migrate a scene fully:
1. Run the prefab creator menu above.
2. Drop the new prefab into the scene.
3. Remove or disable the legacy wrapper component.

## Rules for AI agents editing this subsystem

- **Do not** add hard-coded `if (state.id == "calm")` or enum-based switches to choose behavior. Use a binding list or visibility flag on `IntentStateData`.
- **Do not** rebuild the card UI in code from `OnValidate`. Use the prefab + `CardTemplate`.
- **Do not** hand-edit `.prefab` / `.asset` YAML. Use the editor binder/creator scripts.
- **Do** preserve the prefab child names listed above.
- **Do** put new scene side effects in a router, not in the controller.
- **Do** keep `IntentSelectorController` UI-agnostic. It must not reference `SplatManager`, `AudioManager`, or specific GameObjects.
