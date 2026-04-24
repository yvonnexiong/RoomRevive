# Startup & World Alignment Flow

## Overview

Before the main RoomRevive experience begins, the user goes through a two-step startup flow:
1. **Start screen** — billboard UI, introduces the app
2. **Alignment step** — user physically places and orients the splat world onto their real kitchen

This replaces the need for spatial anchors. The user aligns once per session.

---

## Step 1: Start Screen

A billboard (head-following) panel appears when the app launches.

**Contents:**
```
┌─────────────────────────────────┐
│                                 │
│         RoomRevive              │  ← project name, large
│                                 │
│   [ Start ]                     │  ← single button, centered
│                                 │
└─────────────────────────────────┘
```

**Behavior:**
- Panel floats ~1.5–2m in front of the user, always facing them (billboard)
- Splat scene is NOT active yet — passthrough only
- User presses **Start**
- The splat `SplatRenderer` is enabled and spawned at the user's current world position (floor level, facing user's current forward direction)
- Start panel dismisses
- Step 2 begins immediately

**Why spawn at user position:**
The splat origin is placed at the user's feet at the moment they press Start. This gives a consistent, reproducible starting point each session. The user is expected to be standing near or inside their kitchen.

---

## Step 2: World Alignment

A world-space panel appears near the user with alignment instructions. A **glowing grabbable sphere** appears at the center of the splat world as a pivot handle.

**Alignment panel:**
```
┌──────────────────────────────────────┐
│                                      │
│  Grab the sphere to align            │
│  the kitchen to your real room       │
│                                      │
│  [ Confirm — world is aligned ]      │
│                                      │
└──────────────────────────────────────┘
```

**Grabbable sphere behavior:**
- Small glowing sphere (radius ~0.05m), positioned at the splat world's origin (floor level)
- Emissive glow material (intent-neutral color, e.g. soft white/blue)
- User can **grab** it with hand or controller
- While grabbed:
  - **Position**: sphere moves freely in XZ (floor plane) — drags the entire `SplatRenderer` with it
  - **Rotation**: sphere can be rotated around the Y axis only — rotates the entire `SplatRenderer` around its vertical axis
  - Y position is locked (no vertical drift)
- Sphere is parented to a `SplatPivot` GameObject; `SplatRenderer` is a child of `SplatPivot`

**Confirm button:**
- User presses **Confirm — world is aligned**
- Sphere disappears (deactivated)
- Alignment panel dismisses
- Intent Selector UI appears → normal experience begins
- `IntentManager` is initialized, default intent loads

---

## Scene Hierarchy

```
SplatPivot                    ← AlignmentController moves this
├── SplatRenderer             ← GaussianSplatRenderer, child of pivot
└── AlignmentSphere           ← grabbable sphere, deactivated after confirm
```

---

## Components

**`StartupController`** (MonoBehaviour, on a dedicated GameObject)
- Owns the full startup state machine
- States: `Start → Aligning → Experience`
- `OnStartPressed()` — enables `SplatPivot`, positions it at user's feet, shows alignment UI
- `OnAlignConfirmed()` — hides sphere + alignment UI, fires `OnAlignmentComplete` event → `IntentManager` starts

**`AlignmentSphere`** (MonoBehaviour on the sphere GameObject)
- Hooks into OVR grab events
- `OnGrabbed()` — begins tracking pivot position/rotation delta
- `OnMoved(Vector3 delta, float yRotationDelta)` — applies to `SplatPivot` transform
- Y position locked throughout; XZ position and Y rotation free
- `Hide()` — called by `StartupController.OnAlignConfirmed()`

**`BillboardPanel`** (MonoBehaviour on the Start screen canvas)
- Updates rotation each frame to face `CenterEyeAnchor`
- Positioned at fixed distance (1.8m) in front of user at start
- Dismissed on Start press

---

## App State Machine

```
AppState.Startup
    │  user presses Start
    ▼
AppState.Aligning
    │  user presses Confirm
    ▼
AppState.IntentSelect       ← IntentSelectorUI appears
    │  user selects intent
    ▼
AppState.Experience         ← hotspots active, before/after slider active
```

---

## Integration with Existing Systems

- `IntentManager.Start()` must NOT auto-load the default intent — initialization is deferred until `StartupController.OnAlignConfirmed()` fires
- `IntentSelectorUI` starts hidden; shown by `StartupController` after alignment
- `BeforeAfterController` resets on every intent switch (unchanged)
- `SplatRenderer` is a child of `SplatPivot` — all existing transform assumptions remain valid; just reference `SplatPivot` as the world root instead of `SplatRenderer` directly

---

## Files to Create

| File | Location |
|---|---|
| `StartupController.cs` | `Assets/RoomRevive/Scripts/Startup/` |
| `AlignmentSphere.cs` | `Assets/RoomRevive/Scripts/Startup/` |
| `BillboardPanel.cs` | `Assets/RoomRevive/Scripts/Startup/` |
| `AlignmentSphere_Prefab.prefab` | `Assets/RoomRevive/Prefabs/` |
| `StartUI_Prefab.prefab` | `Assets/RoomRevive/Prefabs/` |
| `AlignmentUI_Prefab.prefab` | `Assets/RoomRevive/Prefabs/` |
