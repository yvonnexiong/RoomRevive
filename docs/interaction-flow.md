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

User grabs the world-space seam handle and pulls left to reveal passthrough (real kitchen).
Pulling right returns to the full intent splat.

See `before-after-slider.md` for full design.

---

## Step 3: Exploration

Hotspots:
- cabinet
- oven
- lighting

---

## Step 4: Compact Card

Appears on right side

Structure:
1. Meaning (one sentence)
2. Product name
3. Thumbnail
4. CTA

---

## Step 5: Expanded View

Optional:
- variants
- specs
- price

---

## UX Rule

Meaning → then product  
Never reverse this order