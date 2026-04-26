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

Three invisible hotspots activate after intent selection:

| Hotspot | Product |
|---|---|
| Cabinet edge | Nobilia |
| Fridge | Miele KFN 7734 E |
| Lighting | Neuhaus |

**Interaction: gaze dwell (eyes only, no hands)**
- User looks at a hotspot sphere (r=0.15, Hotspot layer)
- Sphere scales up 1.3× on gaze enter
- After 0.7s of held gaze → `OnGazeSelect` fires → product card appears
- No ray or hand interaction — gaze raycasts directly from `CenterEyeAnchor`

This replaced an earlier ray/hand approach. `RayInteractable` on hotspots conflicted with ISDK's candidate pool and broke canvas ray interactions. Gaze bypasses ISDK entirely.

See `GazeHotspotInteraction.md` for full technical detail.

---

## Step 4: Compact Card

Appears upper-right of view (1.4m forward, 0.45m right, 0.05m up from eye).

Structure:
1. Meaning (one emotional sentence)
2. Brand + product name
3. Thumbnail
4. "Explore options" CTA

Auto-hides after 5s or on Close button tap.

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