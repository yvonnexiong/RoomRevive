# Interaction Flow

## Entry: Startup & Alignment

**Start screen** (billboard, head-following):
- Shows "RoomRevive" and a single "Start" button
- Passthrough only — splat not active yet

User presses **Start**:
- Splat world spawns at user's current position/orientation

**Alignment step** (world-space panel):
- "Grab the sphere to align the kitchen to your real room"
- User grabs glowing sphere → drags and rotates splat world onto real kitchen (Y axis rotation + XZ position only)
- User presses **Confirm — world is aligned** → sphere disappears

See `startup-alignment-flow.md` for full design.

---

## Step 1: Intent Selection
User chooses:
- Calm & Unwind
- Host & Gather
- Fast & Focused

---

## Step 2: Transformation
Scene updates:
- lighting
- surfaces
- activity cues

No layout change

Slider resets to full "after" (splat fully visible) on every intent switch.

---

## Step 2b: Before / After Comparison (optional)

A head-following slider (top-right of view) lets the user drag between 0 and 1:
- 0 = passthrough (real kitchen)
- 1 = full splat intent world (default)

Moves `GSCutout` local position linearly between two preset positions.
See `before-after-slider.md` for full design.

---

## Step 3: Exploration — Gaze Hotspots

Three hotspots activate after intent selection:

| Hotspot | Product |
|---|---|
| Cabinet edge | Nobilia |
| Fridge | Miele KFN 7734 E |
| Lighting | Neuhaus |

**Interaction: gaze dwell (eyes only, no hands)**
- A soft **GlowDot** grows from invisible as the user's gaze approaches within 2m of the hotspot center
- Dot reaches full size when gaze enters the activation zone (SphereCollider r=0.5)
- A **DwellRing** fades in simultaneously as a "locked on" indicator
- After 0.7s held gaze → product card fades in
- Both dot and ring face the user at all times (billboard)
- No ray or hand interaction — gaze raycasts directly from `CenterEyeAnchor`

See `GazeHotspotInteraction.md` for full technical detail.

---

## Step 4: Compact Card

Appears upper-right of view (1.4m forward, 0.45m right, 0.05m up from eye).

Structure:
1. Meaning (one emotional sentence)
2. Brand + product name
3. Thumbnail
4. "Explore options" CTA

Auto-hides after 2s or on Close button tap.

---

## Step 5: Expanded View

Optional (not yet implemented):
- variants
- specs
- price

---

## UX Rule

Meaning → then product  
Never reverse this order