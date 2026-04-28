# Gaze Hotspot Polish — Design & Implementation Plan
> **Status: implemented.** See `GazeHotspotInteraction.md` for the final built state.
> Notes below reflect original design intent; actual implementation differs in some details.

## Goal

Make the gaze interaction feel premium and legible. At every moment the user should know:
- where a hotspot is (at rest)
- that their gaze is registering (enter)
- that something is about to happen (dwell)
- that the action completed (select)

---

## Design

### 1. Hotspot at rest — ambient glow dot
A small soft glowing dot (~3cm world-space diameter), barely visible, with a slow pulse.
- Opacity: 20% → 50% → 20%, 3s cycle
- Makes hotspots discoverable without drawing attention
- Rendered as a SpriteRenderer with additive blending

### 2. Gaze enter — awakening
- Scale 1.0 → 1.3× over **0.2s, ease-out** (currently: instant pop)
- Glow ring appears and brightens

### 3. Dwell — ring progress indicator *(most important)*
A thin circular arc fills clockwise over the 0.7s dwell. The user sees something is actively happening and knows to hold their gaze.
- World-space Canvas child on the hotspot
- Unity Image component: Fill Method = Radial360, Fill Origin = Top, Clockwise
- `fillAmount` driven by `OnGazeDwell(t)` (0 → 1)
- On gaze exit before complete: ring lerps back to 0 (does not snap)

### 4. Select — confirm burst
The moment dwell completes, a brief ripple plays before the card appears.
- Ring expands outward + fades over 0.2s
- Hotspot scales back down smoothly
- Product card fades in (alpha 0 → 1 over 0.2s, not instant SetActive)

### 5. Intent-aware colors
Hotspot visuals adapt to the active intent.

| Intent | Ring / glow color | Pulse speed |
|---|---|---|
| Calm & Unwind | Warm amber `#F5A623`, low opacity | 3s, slow |
| Host & Gather | Warm white `#FFF5E0`, brighter | 2.5s |
| Fast & Focused | Cool blue `#4FC3F7`, crisp | 1.5s or none |

---

## Implementation Plan

### Files to change

| File | Change |
|---|---|
| `HotspotInteractable.cs` | Replace instant scale with coroutines; wire dwell ring; add burst; listen for intent color |
| `ProductCardUI.cs` | Replace `SetActive(true)` with canvas group fade-in/out |
| `GazeHotspotDetector.cs` | No changes needed |

### New GameObjects (per hotspot)

```
HotspotCabinet
  ├─ [SphereCollider, HotspotInteractable]   ← existing
  ├─ GlowDot                                 ← new: SpriteRenderer, additive
  └─ DwellRingCanvas                         ← new: world-space Canvas
       └─ DwellRingImage                          ← Image, Radial360 fill
```

### Step-by-step

#### Step 1 — Eased scale (HotspotInteractable)
Replace `OnGazeEnter` / `OnGazeExit` instant scale with coroutines:

```csharp
IEnumerator ScaleTo(Vector3 target, float duration)
{
    Vector3 start = transform.localScale;
    float t = 0f;
    while (t < 1f)
    {
        t += Time.deltaTime / duration;
        transform.localScale = Vector3.Lerp(start, target, Mathf.SmoothStep(0, 1, t));
        yield return null;
    }
    transform.localScale = target;
}
```

- Enter: `ScaleTo(_baseScale * 1.3f, 0.2f)`
- Exit: `ScaleTo(_baseScale, 0.15f)`
- Cancel any running coroutine before starting a new one

#### Step 2 — Dwell ring (HotspotInteractable)
Add a serialized `Image _dwellRing` field. Wire in `OnGazeDwell`:

```csharp
public void OnGazeDwell(float t)
{
    if (_dwellRing != null) _dwellRing.fillAmount = t;
}
```

On gaze exit, lerp `fillAmount` back to 0 in a coroutine instead of snapping.

#### Step 3 — Select burst (HotspotInteractable)
In `OnGazeSelect`, play a burst coroutine before firing the event:

```csharp
IEnumerator PlayBurst()
{
    // expand ring outward + fade
    float t = 0f;
    while (t < 1f)
    {
        t += Time.deltaTime / 0.2f;
        _dwellRing.fillAmount = 1f;
        _dwellRingRect.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 1.5f, t);
        Color c = _dwellRing.color;
        c.a = Mathf.Lerp(1f, 0f, t);
        _dwellRing.color = c;
        yield return null;
    }
    // reset ring
    _dwellRingRect.localScale = Vector3.one;
    _dwellRing.fillAmount = 0f;
    // fire event
    OnAnySelected?.Invoke(_data.linkedProduct);
}
```

#### Step 4 — Card fade-in (ProductCardUI)
Add a `CanvasGroup` to the card and fade alpha instead of toggling SetActive:

```csharp
IEnumerator FadeIn()
{
    gameObject.SetActive(true);
    float t = 0f;
    while (t < 1f)
    {
        t += Time.deltaTime / 0.2f;
        _canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
        yield return null;
    }
    _canvasGroup.alpha = 1f;
}
```

Similarly `FadeOut` before `SetActive(false)`.

#### Step 5 — Ambient glow dot
- Create a child `GlowDot` under each hotspot with a soft circle sprite
- Material: Unlit, blending mode Additive
- Coroutine pulses `SpriteRenderer.color.a` between 0.2 and 0.5

#### Step 6 — Intent colors
- `HotspotInteractable` subscribes to `IntentManager.OnIntentChanged`
- On change: update `_dwellRing.color` and `GlowDot` tint to the intent color
- Define the 3 color + pulse-speed pairs as serialized fields (set in Inspector)

---

## Implementation Order

1. Eased scale (quick win, most visible)
2. Dwell ring (highest UX impact)
3. Card fade-in (polish)
4. Select burst (delight)
5. Ambient glow dot (discoverability)
6. Intent colors (ties to theme system)

---

## Out of scope for this task
- Thumbnail sprites on ProductSO (separate backlog item)
- Expanded card / variants (Phase 2 items 1 & 2)
- Audio (Phase 3)
