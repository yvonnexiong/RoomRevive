# Before / After Slider

## Concept

A world-space vertical seam the user grabs and pulls to reveal the real kitchen (passthrough) on the left and the intent Gaussian Splat world on the right.

"Before" = Meta Quest passthrough (real kitchen)
"After"  = Gaussian Splat intent world, spatially aligned on top

---

## Why It Works

The Gaussian Splat renderer composites on top of passthrough by default.
The existing `GaussianCutout` (Box) system hides splats inside a volume at render time.
Where splats are cut out, passthrough shows through underneath naturally — no extra cameras or compositing tricks needed.

---

## Mechanism

A `GaussianCutout` Box volume covers the left portion of the kitchen:

```
[← PASSTHROUGH | SPLAT →]
         ↑
    grab this seam
```

| Slider value | Cutout box | Result |
|---|---|---|
| 0 (full left) | covers entire kitchen | full passthrough (before) |
| 0.5 (center) | covers left half | split view |
| 1 (full right) | covers nothing | full splat (after) |

Default when an intent is selected = 1 (full splat visible).

---

## Interaction

A **vertical grabbable seam** at the dividing boundary:
- Thin glowing vertical bar the user grabs with hand or controller
- Constrained to move along the kitchen's X axis only
- Moves in real time as the user pulls left or right
- Handled by the existing OVR interaction rig (no extra setup needed)

Recommended input: **grab** (hand closes around seam handle), since the boundary is typically within arm's reach of the kitchen island. Test ray-drag as fallback for far-side reaches.

---

## Components

**`BeforeAfterController`**
- Owns `sliderValue` (float 0–1)
- `SetSlider(float)` — repositions the `GaussianCutout` box and `SeamHandle` to match
- `ResetToAfter()` — called automatically on every intent switch (resets to 1)

**`SeamHandle`** (MonoBehaviour on the grabbable seam GameObject)
- `OnGrab()`, `OnMove()`, `OnRelease()`
- Maps world X position along rail → calls `BeforeAfterController.SetSlider()`

**`SeamVisual`**
- Thin vertical plane at the boundary
- Glowing edge material to make the seam legible in XR

---

## Scene Setup

```
BeforeAfter
├── SeamHandle          ← OVR Grabbable, constrained to X axis
├── SeamVisual          ← thin glowing vertical plane
└── GaussianCutoutBox   ← GaussianCutout (Box), child of active SplatWorld
```

`OVRPassthroughLayer` on `OVRCameraRig` — standard Meta Quest passthrough setup, required.

---

## Integration with Intent System

- Intent selected → splat switches → `BeforeAfterController.ResetToAfter()` called
- Slider persists during exploration (user can compare before/after while viewing hotspots)
- Works identically across all 3 intents — "before" is always the real passthrough kitchen

---

## Files to Create

| File | Location |
|---|---|
| `BeforeAfterController.cs` | `Assets/RoomRevive/Scripts/BeforeAfter/` |
| `SeamHandle.cs` | `Assets/RoomRevive/Scripts/BeforeAfter/` |
| `SeamHandle_Prefab.prefab` | `Assets/RoomRevive/Prefabs/` |
