# RoomRevive Onboarding — UI Direction Reference

> Ground truth for the **implemented** UI as of Phase 2 (Q1 interactive).
> Use this when building Q2–Q4, Review, and Result pages.
> When this doc and `UI_SPEC.md` conflict on visuals, **this doc wins** — it reflects
> decisions made during the Unity build that differ from the original spec.

---

## 1. Canvas & Layout

| Property | Value |
|---|---|
| Render mode | World Space |
| Canvas size | 480 × 780 units (1 unit = 1 mm) |
| World scale | 0.001 |
| Panel padding | 20 left/right, 16 top/bottom |
| Element spacing | **12 px** everywhere — between cards, between sections |

All spacing between major sections (progress bar → banner → card grid → nav bar)
uses the same **12 px** gap so the layout feels rhythmically consistent.

---

## 2. Color Palette

### Surface colors
| Token | Hex | Use |
|---|---|---|
| `SurfaceBase` | `#B5BCD0` | Mid-gradient, general surface |
| `SurfaceLight` | `#D4D9E5` | Banner background (80% opacity), gradient highlight |
| `SurfaceDeep` | `#7C85A0` | Progress bar active segment |
| `CardInner` | `#C7CDDD` | Card body fill, ring default (invisible) |
| `CardCaption` | `#BBC2D4` | Caption band (CardInner – 4% per channel) |

### Ink colors
| Token | Hex | Use |
|---|---|---|
| `InkPrimary` | `#3A4055` | Card label, banner title, button bg, selected state |
| `InkSecondary` | `#6B7388` | Card subtitle, banner subtitle |

### Accent
| Token | Hex | Use |
|---|---|---|
| `BtnText` | `#E6E9F0` | Button label text (light blue-white) |

> **No separate teal accent.** Selected state uses `InkPrimary` (`#3A4055`) —
> same as the Next button — so selection and CTA read as one visual language.

---

## 3. Background Gradient

Matches WelcomeUI exactly. Generated as `Assets/Onboarding/Sprites/BgGradient.png`
(256 × 416 px, stretched to fill the 480 × 780 canvas).

| Stop | Hex | Position |
|---|---|---|
| Center | `#D4D9E5` | 0% |
| Mid | `#B5BCD0` | 65% |
| Edge | `#9CA4BC` | 100% |

Radial origin: **35% from left, 25% from top** (CSS convention).
Corner radius in texture: 24 px (soft corners on the panel).

The panel background is an `Image` child with `ignoreLayout = true` so the
`VerticalLayoutGroup` skips it while it stretch-fills behind all content.

---

## 4. Typography

- **Font:** Alan Sans Variable (`Assets/RoomRevive/Font/AlanSans-VariableFont_wght SDF.asset`)
- **TMP color assignment:** always use `.linear` suffix — `tmp.color = color.linear`
- **Image.color:** no `.linear` needed

| Role | Size | Weight | Color |
|---|---|---|---|
| Banner title (question) | 26 px | Bold | `InkPrimary` |
| Banner subtitle ("1 of 4") | 12 px | Normal | `InkSecondary` |
| Card label | 15 px | Bold | `InkPrimary` / white when selected |
| Card subtitle | 12 px | Normal | `InkSecondary` / white when selected |
| Button label | 14 px | Bold | `BtnText` (#E6E9F0) |

---

## 5. Progress Bar

- Height: **4 px**
- Shape: pill sprite (12 × 4 px, 2 px radius, 9-sliced)
- Segment gap: **6 px**
- Active segment color: `SurfaceDeep` (#7C85A0)
- Inactive segment color: `SurfaceDeep` @ **35% opacity**
- Implementation: wrapper RT with `LayoutElement(preferredHeight:4)` + inner RT
  with `HorizontalLayoutGroup` (avoids HLG overriding LE height)

---

## 6. Banner (Question Header)

- Sprite: `RoundedRect.png` (8 px radius, 9-sliced)
- Color: `SurfaceLight` @ **80% opacity** (gradient shows faintly through)
- Height: **84 px**
- Padding: 16 left/right, 14 top, 10 bottom
- Internal spacing: **4 px** between title and subtitle
- Alignment: center

---

## 7. Image Cards (Q1 / Q2)

### Structure (per card)
```
CellWrapper  (GridLayout child — OnboardingOptionCardView here)
  Ring       (Image, roundRect, CardInner default / InkPrimary selected)
  CardBody   (Image, roundRect, CardInner, inset 2 px — Mask + Button + OnboardingCardInteractionProxy)
    Photo    (Image, fills CardBody minus caption height, anchor-based)
    Caption  (Image, bottom 56 px, anchor-based — VerticalLayoutGroup inside)
      Label  (TMP, 15 px Bold)
      Sub    (TMP, 12 px Normal)
```

### Grid layout
| Property | Value |
|---|---|
| Cell size | 215 × 271 px |
| Cell spacing | 12 × 12 px |
| Columns | 2 (fixed) |
| Grid preferred height | 554 px |
| Photo area | 211 × 211 px (square — card 271 − 56 caption − 4 inset = 211) |
| Caption height | 56 px |
| Caption padding | 14 left/right, 10 top, 12 bottom |
| Caption label/sub spacing | 2 px |

### States

| State | Ring | Caption bg | Label | Subtitle |
|---|---|---|---|---|
| Default | `CardInner` (invisible) | `CardCaption` | `InkPrimary` | `InkSecondary` |
| **Selected** | `InkPrimary` | `InkPrimary` | white | white |
| Disabled | — | — | — | CanvasGroup alpha 35% |

Ring is `CardInner` by default so it blends in. When selected, the 2 px inset
between Ring and CardBody reveals `InkPrimary` as the selection border.

### Hover / press animation (cards only)
Animated on `CellWrapper.localScale` via coroutine. Ease-out curve.

| Event | Scale | Duration |
|---|---|---|
| Hover enter | 1.0 → 1.04 | 150 ms |
| Hover exit | 1.04 → 1.0 | 120 ms |
| Press down | → 0.97 | 80 ms |
| Press up | → 1.0 (or 1.04 if hovering) | 100 ms |

Fired via `OnboardingCardInteractionProxy` on CardBody (standard `IPointer*`
interfaces — no changes needed for XR ray or poke migration).

---

## 8. Text Row Cards (Q3 / Q4)

Not yet built. Should follow the same single-select logic but as full-width rows
instead of a 2×2 image grid. Suggested spec:

- Background: `CardInner`, 8 px radius
- Label: `InkPrimary`, 15 px Bold (left-aligned)
- Subtitle (if present): `InkSecondary`, 12 px Normal
- Selected: `InkPrimary` fill, white label/subtitle
- Height per row: ~56 px
- Padding: 20 left/right, 16 top/bottom
- Row gap: 10 px

---

## 9. Buttons

### Next button
| Property | Value |
|---|---|
| Background | `InkPrimary` (#3A4055) |
| Label | `BtnText` (#E6E9F0), 14 px Bold |
| Radius | 8 px (roundRect sprite, Sliced) |
| Height | 44 px |
| Width | Stretches from x=132 to right edge of nav bar (Back + 12 px gap) |
| Hover | Instant lighter blue: `#4E5469` (LightenColor +0.12) |
| Press | Same as hover |
| Disabled | `InkPrimary` image color (unchanged); `CanvasGroup.alpha = 0.45` |
| Enabled | `CanvasGroup.alpha = 1.0` |

Button uses Unity `ColorBlock` with `fadeDuration = 0` (instant, no animation).
`CanvasGroup` on the button RT drives the enabled/disabled dimming.

### Back button
| Property | Value |
|---|---|
| Background | `SurfaceLight` (#D4D9E5) |
| Label | `InkPrimary` (#3A4055), 14 px Bold |
| Radius | 8 px |
| Height | 44 px |
| Width | 120 px, anchored left |
| Hover | Instant lighter: `#E8ECF4` |
| On Q1 | Hidden (`CanvasGroup.alpha = 0`, blocksRaycasts = false) |
| On Q1 | Next button expands to full nav width (`offsetMin.x = 0`) |

---

## 10. Nav Bar

- Height: **44 px** (matches button height — no empty space that inflates visual gap)
- Sits at the bottom of the panel VL
- All gaps (sections ↔ nav, cards ↔ nav) = 12 px

---

## 11. Single-Select Logic

Implemented in `OnboardingQ1Controller` (Phase 2). Pattern to reuse for Q2–Q4:

1. Maintain `_selected` reference
2. On card click: deselect previous → select new → call `RefreshNext()`
3. `RefreshNext()`: `Button.interactable = hasSelection`, `CanvasGroup.alpha = has ? 1 : 0.45`
4. Back button: restore previous page's answer if stored
5. On Q1 only: Back is hidden, Next spans full width

---

## 12. XR Migration Path

When ready to move from editor mouse to XR:

1. Replace `GraphicRaycaster` on Canvas with `TrackedDeviceGraphicRaycaster`
2. `OnboardingCardInteractionProxy` — **no changes** (already uses `IPointer*`)
3. Button `ColorBlock` hover — **no changes** (Button responds to same events)
4. For XR poke: add `XRPokeFilter` to the Canvas; same scripts fire unchanged
5. Optional: add `IXRHoverEnterHandler` / `IXRHoverExitHandler` to the proxy
   for distance-based hover feedback (e.g. haptic pulse before contact)
