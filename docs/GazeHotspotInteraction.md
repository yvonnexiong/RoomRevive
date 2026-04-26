# Gaze Hotspot Interaction — Design Flow

## Overview

Hotspots are invisible interaction points placed in the kitchen scene. When the user gazes at one long enough, a product card appears with a brief emotional description and product details. No hand interaction required — eyes only.

---

## Interaction Flow

```
User picks an intent
        ↓
Hotspots become active in scene
        ↓
User gazes at a hotspot sphere (SphereCollider, r=0.15)
        ↓
Sphere scales up 1.3× (gaze enter feedback)
        ↓
User holds gaze for 0.7s (dwell)
        ↓
Sphere returns to normal scale
OnGazeSelect fires → OnAnySelected event → ProductCardUI.Show()
        ↓
Product card appears (head-following, slightly right of center)
        ↓
Card auto-hides after 5s
   OR user taps Close button
```

---

## Hotspots

| Name             | Position (local to SplatPivot) | Product         |
|------------------|-------------------------------|-----------------|
| HotspotCabinet   | (-1.2, 1.35, 3.2)             | Nobilia cabinet |
| HotspotFridge    | (1.862, 0.9, 2.736)           | Miele KFN 7734 E|
| HotspotLighting  | (-0.4, 2.3, 2.5)              | Neuhaus lighting|

Positions are rough estimates — tune visually in the editor against the Gaussian Splat.

---

## Product Card

- World-space canvas, 600×420px @ 0.001 scale
- Follows head: 1.4m forward, 0.45m right, 0.05m up from `CenterEyeAnchor`
- Always faces user (billboard rotation)
- Content: emotional line → brand name → product name → thumbnail (null for now)
- Auto-hides after **5s** (configurable via `Auto Hide Delay` in Inspector)
- Close button for instant dismiss

---

## Scripts

| Script                  | Responsibility                                              |
|-------------------------|-------------------------------------------------------------|
| `GazeHotspotDetector`   | Raycasts from CenterEyeAnchor, tracks dwell timer, fires OnGazeSelect |
| `HotspotInteractable`   | Scale feedback on enter/exit, fires `OnAnySelected` static event |
| `ProductCardUI`         | Subscribes to event, populates and shows/hides the card     |
| `HeadFollowCanvas`      | Generic head-follow (used by TestCanvas, not ProductCardCanvas) |

---

## Data Assets

```
HotspotSO  →  linkedProduct (ProductSO)
                  ├─ brandName
                  ├─ productName
                  ├─ emotionalLine
                  └─ thumbnail (Sprite, currently null)
```

---

## Key Design Decisions

**Why gaze instead of ray/hand interaction?**
`RayInteractable` on hotspots conflicted with ISDK's candidate pool and broke all canvas ray interactions. Gaze bypasses ISDK entirely.

**Why a static event (`OnAnySelected`) instead of a direct reference?**
Keeps hotspots and the card decoupled — hotspots don't need to know the card exists.

**Why auto-hide instead of gaze-off detection?**
The card follows the head, so there is no spatial "looking away." A timer is the correct dismissal pattern for a head-locked UI.

---

## Before Final Build

- [ ] Tune all 3 hotspot positions in editor against the splat
- [ ] Assign thumbnail sprites to all 3 ProductSO assets
- [x] Delete `TestCanvas` from MainScene
- [ ] Delete `DebugVisual` sphere children from each hotspot
- [ ] Remove `GazeTestScene` from build settings
