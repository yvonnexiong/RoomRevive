# Phase 1 Implementation Plan — RoomRevive XR Prototype

> Kitchen rendered via Gaussian Splats (WorldLabs `.spz` files).
> 3 intent worlds are pre-baked — lighting, surfaces, and props are embedded in each splat.
> Intent switching = swapping the asset on a single `GaussianSplatRenderer` (not toggling 3 renderers — toggling SetActive on GPU resources crashes Unity).

---

## 1. Scene / Data Architecture

**Scene hierarchy (actual):**
```
MainScene
├── OVRCameraRig
│   └── OVRInteractionComprehensive
├── GazeHotspotDetector                ← root GameObject, raycasts from CenterEyeAnchor
├── SplatPivot                         ← moved/rotated during alignment
│   ├── SplatRenderer                  ← single GaussianSplatRenderer, asset swapped at runtime
│   │   └── GSCutout                   ← GaussianCutout (Box), driven by BeforeAfterSlider
│   └── Hotspots                       ← enabled by IntentManager after first intent pick
│       ├── HotspotCabinet
│       ├── HotspotFridge
│       └── HotspotLighting
└── ProductCardCanvas                  ← world-space, head-following
```

**ScriptableObjects (3 types):**

```
IntentSO
├── id, displayName
└── splatAsset : GaussianSplatAsset    ← asset reference (splatWorld moved out of SO)

HotspotSO
├── id, displayName
└── linkedProduct   → ProductSO

ProductSO
├── id, brandName, productName
├── emotionalLine   (1 sentence)
├── thumbnail       (Sprite, currently null)
└── variants[]      { name, imagePath, price }
```

---

## 2. Startup & World Alignment Flow

See `startup-alignment-flow.md` for full design.

**App states:** `Startup → Aligning → IntentSelect → Experience`

**`StartupController`** — owns the state machine:
- On Start pressed: enables `SplatPivot` at user's feet, shows alignment UI
- On Confirm pressed: hides sphere, fires `OnAlignmentComplete` → `IntentManager` initializes

**Scene hierarchy:**
```
SplatPivot                    ← moved/rotated by AlignmentSphere
├── SplatRenderer             ← child of pivot
└── AlignmentSphere           ← glowing grabbable, deactivated after confirm
```

**`AlignmentSphere`** — OVR Grabbable, constrains motion to XZ position + Y rotation only.

**`BillboardPanel`** — Start screen canvas, faces `CenterEyeAnchor` each frame.

> `IntentManager.Start()` must NOT auto-load default intent — deferred until `OnAlignmentComplete`.

---

## 3. Intent System

Since appearance is pre-baked into each `.spz`, intent switching is a splat renderer toggle — no lighting or material controllers needed.

**`IntentManager`** — singleton, owns current intent state:

```
IntentManager
├── CurrentIntent : IntentSO
├── SetIntent(IntentSO)
│   ├── disable current splatWorld
│   └── enable intent.splatWorld
└── OnIntentChanged : Action<IntentSO>
```

**Intent selector UI:** world-space panel with 3 buttons, visible at start. On selection it dismisses and hotspots appear.

> No `LightingController`, `SurfaceStylingController`, or `PropController` needed —
> all appearance is baked into the splat.

---

## 3. Hotspot Interaction System

Each hotspot is an invisible `SphereCollider` (r=0.15) in world space, manually positioned to align with the splat's visual features. Interaction is **gaze dwell only — no ray or hand interaction**.

> Original plan used OVR ray interactor. Replaced because `RayInteractable` on hotspots conflicted with ISDK's candidate pool and broke all canvas ray interactions. Gaze bypasses ISDK entirely.

**`GazeHotspotDetector`** (root GameObject in scene):
```
GazeHotspotDetector
├── dwellTime = 0.7s
├── hotspotLayer (layer 8)
└── Update()
    ├── Raycast from CenterEyeAnchor, Hotspot layer only
    ├── OnGazeEnter / OnGazeExit on target change
    └── OnGazeSelect after 0.7s dwell → fires OnAnySelected static event
```

**`HotspotInteractable`** (MonoBehaviour on each hotspot GameObject):
```
HotspotInteractable
├── data : HotspotSO
├── OnGazeEnter()   ← scale up 1.3×
├── OnGazeExit()    ← reset scale
├── OnGazeDwell(t)  ← optional progress feedback
└── OnGazeSelect()  → fires OnAnySelected(ProductSO) static event
```

Hotspots are hidden at start (`IntentManager.Awake()` disables them), enabled on first intent selection.

> Phase 2 idea: use `GaussianCutout` (inverted) on hotspot hover to subtly dim
> everything outside the hovered region — cheap, render-time only.

---

## 4. Product Card UI Structure

World-space canvas (600×420px @ 0.001 scale), head-following (not parented to hotspot). Single compact state for Phase 1.

**Compact card** (always shown first — enforces emotion-before-spec rule):
```
┌─────────────────────────────────┐
│  "The kitchen that winds         │
│   your evening down."           │  ← emotionalLine (large, light weight)
│                                 │
│  [thumb]  Nobilia Frame          │  ← brand + product name
│           Cabinet Line          │
│                                 │
│  [Close]  [ Explore options → ] │
└─────────────────────────────────┘
```

**Expanded card** — Phase 2, not yet implemented (`OnExplore()` is a debug log stub).

**`ProductCardUI`** (on ProductCardCanvas):
```
ProductCardUI
├── Subscribes to HotspotInteractable.OnAnySelected
├── Show(ProductSO) — populates text/thumbnail, activates canvas
├── Auto-hides after 5s
└── Close button for instant dismiss
```

Position: 1.4m forward, 0.45m right, 0.05m up from `CenterEyeAnchor`. Always faces camera direction.

---

## 5. Folder Structure

```
Assets/
└── RoomRevive/
    ├── Data/
    │   ├── Intents/
    │   │   ├── Intent_Calm.asset
    │   │   ├── Intent_Host.asset
    │   │   └── Intent_Fast.asset
    │   ├── Hotspots/
    │   │   ├── Hotspot_Cabinet.asset
    │   │   ├── Hotspot_Fridge.asset
    │   │   └── Hotspot_Lighting.asset
    │   └── Products/
    │       ├── Product_Nobilia.asset
    │       ├── Product_Miele.asset
    │       └── Product_Neuhaus.asset
    ├── Scripts/
    │   ├── Intent/
    │   │   ├── IntentSO.cs
    │   │   ├── IntentManager.cs
    │   │   ├── IntentSelectorUI.cs
    │   │   └── IntentDebugSwitcher.cs   ← remove before shipping
    │   ├── Hotspot/
    │   │   ├── HotspotSO.cs
    │   │   ├── HotspotInteractable.cs
    │   │   ├── GazeHotspotDetector.cs
    │   │   └── ProductCardUI.cs
    │   ├── Product/
    │   │   └── ProductSO.cs
    │   ├── BeforeAfter/
    │   │   └── BeforeAfterSlider.cs
    │   ├── Startup/
    │   │   ├── StartupController.cs
    │   │   ├── AlignmentSphere.cs
    │   │   ├── BillboardPanel.cs
    │   │   └── SplatOpacitySlider.cs
    │   └── UI/
    │       └── HeadFollowCanvas.cs
    ├── Prefabs/
    │   ├── Hotspot_Prefab.prefab
    │   └── ProductCard_Prefab.prefab
    └── UI/
        └── ProductCard/
```

---

## 6. Before / After Slider

See `before-after-slider.md` for the full design.

> Original design (SeamHandle + grabbable seam) was replaced with a **head-following UI slider** for simpler interaction.

**Component:**
- `BeforeAfterSlider` — head-following world-space canvas slider (0–1), lerps `GSCutout.localPosition` between before/after positions
  - 0 = before: `(-0.32, 1.45, -2.88)` — cutout covers splats, passthrough visible
  - 1 = after: `(-4.3, 1.45, -5.65)` — cutout outside splat bounds, full splat visible
  - Default = 1 (after)
  - `ResetToAfter()` called on intent switch

**Scene setup:**
```
BeforeAfterUI       ← World Space Canvas, BeforeAfterSlider script, ray+poke interaction
SplatPivot
└── SplatRenderer
    └── GSCutout    ← GaussianCutout (Box), driven by BeforeAfterSlider
```

**Folder:**
```
Scripts/
└── BeforeAfter/
    └── BeforeAfterSlider.cs
```

---

## Build Order

1. `IntentSO`, `HotspotSO`, `ProductSO` — data layer first, no Unity dependency
2. Import `.spz` files, set up 3 `GaussianSplatRenderer` GameObjects in scene
3. `IntentManager` — toggle splat renderers, verify switching works
4. Intent selector UI
5. Startup & alignment flow — `StartupController`, `BillboardPanel`, `AlignmentSphere` ← **NEXT**
6. Before/after slider — `BeforeAfterController`, `SeamHandle`, `GaussianCutoutBox`
7. `GazeHotspotDetector` + `HotspotInteractable` — gaze dwell system, manually position colliders in editor ✓
8. `ProductCardUI` — compact card, head-following, wired to `OnAnySelected` event ✓
9. Wire data assets — swap hardcoded data for ScriptableObjects
