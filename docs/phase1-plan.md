# Phase 1 Implementation Plan — RoomRevive XR Prototype

> Kitchen rendered via Gaussian Splats (WorldLabs `.spz` files).
> 3 intent worlds are pre-baked — lighting, surfaces, and props are embedded in each splat.
> Intent switching = toggling which `GaussianSplatRenderer` is active.

---

## 1. Scene / Data Architecture

**Scene hierarchy:**
```
MainScene
├── OVRCameraRig
│   └── OVRInteractionComprehensive
├── SplatWorlds                        ← 3 renderers, one active at a time
│   ├── SplatWorld_Calm               (GaussianSplatRenderer)
│   ├── SplatWorld_Host               (GaussianSplatRenderer)
│   └── SplatWorld_Fast               (GaussianSplatRenderer)
├── Hotspots                           ← manually positioned, invisible colliders
│   ├── HotspotCabinet
│   ├── HotspotOven
│   └── HotspotLighting
└── UI
    └── ProductCardCanvas              ← world-space
```

**ScriptableObjects (3 types):**

```
IntentSO
├── id, displayName
└── splatWorld : GaussianSplatRenderer   ← reference to its renderer

HotspotSO
├── id, displayName
├── worldAnchorOffset (Vector3)
└── linkedProduct   → ProductSO

ProductSO
├── id, brandName, productName
├── emotionalLine   (1 sentence)
├── thumbnail       (Sprite)
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

Each hotspot is an invisible trigger collider in world space, manually positioned to align with the splat's visual features (cabinet edge, oven, lighting fixture). The OVR ray interactor works against the collider — not the splat geometry.

**`HotspotAnchor`** (MonoBehaviour on each hotspot GameObject):
```
HotspotAnchor
├── data : HotspotSO
├── OnHovered()    ← pulse ring visual, scale up slightly
├── OnUnhovered()  ← reset
└── OnSelected()   → HotspotManager.Activate(this)
```

**`HotspotManager`** — coordinates which hotspot is active (only one at a time):
```
HotspotManager
├── activeHotspot : HotspotAnchor
└── Activate(HotspotAnchor)
    ├── deactivates previous
    └── fires OnHotspotActivated(HotspotSO) → ProductCardController
```

Hotspots are hidden at start, enabled by `IntentManager` once an intent is selected.

> Phase 2 idea: use `GaussianCutout` (inverted) on hotspot hover to subtly dim
> everything outside the hovered region — cheap, render-time only.

---

## 4. Product Card UI Structure

World-space canvas, parented to the `HotspotAnchor`, positioned to its right. Two states:

**Compact card** (always shown first — enforces emotion-before-spec rule):
```
┌─────────────────────────────────┐
│  "The kitchen that winds         │
│   your evening down."           │  ← emotionalLine (large, light weight)
│                                 │
│  [thumb]  Nobilia Frame          │  ← brand + product name
│           Cabinet Line          │
│                                 │
│         [ Explore options → ]   │  ← CTA button
└─────────────────────────────────┘
```

**Expanded card** (slides in on CTA — optional, user-driven):
```
┌─────────────────────────────────┐
│  ← Back                         │
│  Nobilia Frame                  │
│  ───────────────                │
│  [var1] [var2] [var3]           │  ← variant thumbnails
│                                 │
│  Soft-close hinges              │
│  Width: 60cm                    │  ← minimal specs
│                                 │
│  From €1,200                    │
└─────────────────────────────────┘
```

**`ProductCardController`**:
```
ProductCardController
├── ShowCompact(ProductSO)
├── ShowExpanded()
├── Hide()
└── state : { Hidden, Compact, Expanded }
```

Transitions: fade + slide (~0.2s). No heavy animations in Phase 1.

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
    │   │   ├── Hotspot_Oven.asset
    │   │   └── Hotspot_Lighting.asset
    │   └── Products/
    │       ├── Product_Nobilia.asset
    │       ├── Product_Miele.asset
    │       └── Product_Neuhaus.asset
    ├── Scripts/
    │   ├── Intent/
    │   │   ├── IntentSO.cs
    │   │   └── IntentManager.cs
    │   ├── Hotspot/
    │   │   ├── HotspotSO.cs
    │   │   ├── HotspotAnchor.cs
    │   │   └── HotspotManager.cs
    │   └── Product/
    │       ├── ProductSO.cs
    │       └── ProductCardController.cs
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
7. `HotspotAnchor` + `HotspotManager` — manually position colliders, wire to OVR ray interactor
8. `ProductCardController` — compact card only, hardcoded data first
9. Wire data assets — swap hardcoded data for ScriptableObjects
