# Gaze Hotspot Interaction — Design & Technical Reference

## Overview

Hotspots are interaction points placed in the kitchen scene. When the user gazes at one long enough, a product card appears. No hand interaction required — eyes only.

Each hotspot has two visual layers that respond to where the user is looking:
- **GlowDot** — a soft circle that grows as the gaze approaches
- **DwellRingCanvas** — a ring that fades in once the gaze is locked on

---

## Interaction Flow

```
User picks an intent
        ↓
Hotspots become active in scene
        ↓
User looks in the general direction of a hotspot (within 2m gaze-ray distance)
        ↓
GlowDot begins scaling up from 0 (proximity-driven, continuous)
        ↓
Gaze ray enters activation zone (SphereCollider r=0.5)
        ↓
GlowDot animates to full size (0.24), DwellRing fades in
        ↓
User holds gaze for 0.7s (dwell)
        ↓
Ring fades out, OnGazeSelect fires → ProductCardUI fades in
        ↓
Product card auto-hides after 2s
   OR user taps Close button
```

---

## Hotspots

| Name             | Position (local to SplatPivot) | Product          |
|------------------|-------------------------------|------------------|
| HotspotCabinet   | (-1.2, 1.35, 3.2)             | Nobilia cabinet  |
| HotspotFridge    | (1.862, 0.9, 2.736)           | Miele KFN 7734 E |
| HotspotLighting  | (-0.4, 2.3, 2.5)              | Neuhaus lighting |

Each hotspot hierarchy:
```
HotspotCabinet
  ├─ [SphereCollider r=0.5, HotspotInteractable]
  ├─ GlowDot          ← SpriteRenderer, additive material, FaceCamera
  └─ DwellRingCanvas  ← World-space Canvas, FaceCamera
       └─ DwellRingImage   ← Image, Radial360 fillAmount=1, alpha-controlled
```

---

## GlowDot — Proximity Animation

The GlowDot is invisible at rest. It scales up based on how close the gaze ray passes to the hotspot center.

- **Outer radius**: 2.0m — dot starts appearing
- **Inner radius**: 0.5m — dot reaches full size (matches activation collider)
- Scale is smoothed with `Lerp(current, target, dt * 8)` for fluid motion
- Color matches the active intent (`IntentSO.hotspotColor`)
- Alpha fixed at 60% when visible

**Gaze enter** (raycast hits collider):
- Proximity control hands off to a coroutine
- Dot animates to `_glowDotSize` (0.24) over 0.35s with smooth ease
- DwellRing fades from alpha 0 → 1 over 0.35s

**Gaze exit** (before dwell completes):
- `_isGazed = false` — proximity resumes from current scale, smoothly brings it back down
- DwellRing fades back to alpha 0 over 0.35s

**On select** (dwell complete):
- Ring fades out (0.15s)
- Proximity takes the dot back to 0 naturally as user looks toward the card
- Product card fades in

---

## DwellRing — Gaze Lock Indicator

A world-space canvas with a full-circle Image (Radial360 fill, `fillAmount = 1`).
Visibility is controlled entirely through alpha — fill never changes.

- Invisible at rest (alpha = 0)
- Fades in when gaze enters the activation zone
- Stays steady during the 0.7s dwell (no animation)
- Fades out on exit or select
- Both GlowDot and DwellRingCanvas have `FaceCamera` to billboard toward the user

---

## Product Card

- World-space canvas, 600×420px @ 0.001 scale
- Follows head: 1.4m forward, 0.45m right, 0.05m up from `CenterEyeAnchor`
- Always faces user (matches camera forward rotation)
- Content: emotional line → brand name → product name → thumbnail
- Fades in over 0.2s (CanvasGroup alpha — card stays active, never SetActive(false))
- Auto-hides after **2s**
- Close button triggers fade-out

---

## Intent Colors

Each IntentSO carries a `hotspotColor` and `hotspotPulseSpeed`:

| Intent         | Ring / Dot color       |
|----------------|------------------------|
| Calm & Unwind  | Warm amber `#F5A623`   |
| Host & Gather  | Warm white `#FFF5E0`   |
| Fast & Focused | Cool blue `#4FC3F7`    |

Colors update live when intent changes — current alpha is preserved during the switch.

---

## Scripts

| Script                | Responsibility                                                                 |
|-----------------------|--------------------------------------------------------------------------------|
| `GazeHotspotDetector` | Raycast from CenterEyeAnchor, dwell timer, proximity calc for all hotspots     |
| `HotspotInteractable` | GlowDot scale, ring alpha, proximity scale method, fires `OnAnySelected`       |
| `ProductCardUI`       | Subscribes to event, fades card in/out via CanvasGroup                         |
| `FaceCamera`          | Billboards GlowDot and DwellRingCanvas toward the user every LateUpdate        |

---

## Data Assets

```
HotspotSO  →  linkedProduct (ProductSO)
                  ├─ brandName
                  ├─ productName
                  ├─ emotionalLine
                  └─ thumbnail (Sprite, currently null)

IntentSO   →  splatAsset
           →  hotspotColor    (Color)
           →  hotspotPulseSpeed (float, reserved for future ambient effects)
```

---

## Key Design Decisions

**Why gaze instead of ray/hand interaction?**
`RayInteractable` on hotspots conflicted with ISDK's candidate pool and broke all canvas ray interactions. Gaze bypasses ISDK entirely.

**Why proximity-based GlowDot instead of always-visible?**
An always-visible dot draws the eye regardless of intent. A dot that grows as you approach rewards natural curiosity — users discover hotspots by looking around, not by scanning for UI.

**Why alpha-only for the DwellRing (no fill animation)?**
Fill animation (clockwise arc) felt mechanical and distracting. A steady ring that simply appears gives the same "locked on" signal without any moving parts during dwell.

**Why CanvasGroup for ProductCard hide/show instead of SetActive?**
`StartCoroutine` silently fails on inactive GameObjects. Keeping the card always active and controlling visibility through CanvasGroup alpha avoids this and allows fade animations to always work.

**Why static event (`OnAnySelected`) instead of a direct reference?**
Keeps hotspots and the card fully decoupled — hotspots don't need to know the card exists.

---

## Tuning Reference

| Parameter               | Location                          | Current value |
|-------------------------|-----------------------------------|---------------|
| Dwell time              | `GazeHotspotDetector` Inspector   | 0.7s          |
| Proximity outer radius  | `GazeHotspotDetector` Inspector   | 2.0m          |
| Proximity inner radius  | `GazeHotspotDetector` Inspector   | 0.5m          |
| Collider radius         | Each hotspot SphereCollider       | 0.5m          |
| GlowDot full size       | `HotspotInteractable` Inspector   | 0.24          |
| GlowDot alpha           | Code (`_glowDot.color.a`)         | 0.6           |
| Card auto-hide delay    | `ProductCardUI` Inspector         | 2s            |

---

## Before Final Build

- [ ] Tune all 3 hotspot positions in editor against the splat
- [ ] Assign thumbnail sprites to all 3 ProductSO assets
- [x] DebugVisual spheres deactivated on all hotspots
