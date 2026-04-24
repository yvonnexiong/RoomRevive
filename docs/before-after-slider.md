# Before / After Slider

## Concept

A head-following UI slider (0–1) that moves the `GSCutout` GaussianCutout box between two local positions to reveal passthrough ("before") or show the full splat world ("after").

- **0 = Before** — passthrough (real kitchen visible, cutout covers the splats)
- **1 = After** — full splat intent world visible (cutout moved out of splat bounds)
- **Default = 1 (After)**

---

## Design Change Note

> Original design used a world-space vertical grabbable seam handle (`SeamHandle`) the user physically drags.
> This was replaced with a **head-following UI slider** for simpler interaction and more reliable UX in XR.

---

## Mechanism

The `GSCutout` (a `GaussianCutout` Box volume, child of `SplatRenderer`) is moved between two **local positions**:

| Slider | GSCutout local position | Result |
|---|---|---|
| 0 (Before) | (-0.32, 1.45, -2.88) | Cutout covers splats → passthrough shows |
| 1 (After)  | (-4.3, 1.45, -5.65)  | Cutout outside splat bounds → full splat |

Lerp between positions is linear. Y is fixed at 1.45 throughout.

---

## Components

**`BeforeAfterSlider`** (MonoBehaviour on `BeforeAfterUI` canvas)
- `cutoutTransform` → `GSCutout` Transform
- `slider` → Unity `Slider` (0–1)
- `beforePosition` = (-0.32, 1.45, -2.88)
- `afterPosition` = (-4.3, 1.45, -5.65)
- `ResetToAfter()` — called on every intent switch (resets slider to 1)
- Follows user head via `LateUpdate()` (same pattern as `SplatOpacitySlider`)

---

## Scene Setup

```
BeforeAfterUI        ← World Space Canvas, head-following, BeforeAfterSlider script
├── Background
├── Label            ← "Before / After"
├── Slider           ← Unity Slider, 0–1
├── ISDK_RayCanvasInteraction
└── ISDK_PokeCanvasInteraction

SplatPivot
└── SplatRenderer
    └── GSCutout     ← GaussianCutout (Box), driven by BeforeAfterSlider
```

---

## Integration with Intent System

- Intent selected → `BeforeAfterSlider.ResetToAfter()` should be called to reset to full splat view
- Slider persists during exploration within a single intent

---

## Files

| File | Location |
|---|---|
| `BeforeAfterSlider.cs` | `Assets/RoomRevive/Scripts/BeforeAfter/` |
