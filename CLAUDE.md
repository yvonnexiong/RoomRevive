# CLAUDE.md

## Project
RoomRevive is an XR room makeover prototype.

It reconstructs a user’s real room (same layout, dimensions), then transforms it into different lifestyle expressions based on selected intents.

Core message:
"This isn’t just your room—this is how you want to live."

---

## Product Goal
Help users visualize their own kitchen transformed without changing layout.

This must feel like:
- the SAME room
- DIFFERENT ways of living in it

Avoid:
- full redesigns that break spatial recognition

---

## Final Intents

1. Calm & Unwind
2. Host & Gather
3. Fast & Focused

---

## Intent Definitions

### Calm & Unwind
Quiet, minimal, warm, restorative  
End-of-day decompression

### Host & Gather
Warm, social, inviting  
Designed for connection

### Fast & Focused
Efficient, structured, purposeful  
Optimized for speed

---

## Transformation Rules

Keep base layout constant.

Only modify:
1. Lighting (highest impact)
2. Surface styling
3. Activity cues (subtle motion)
4. Material/cabinet expression
5. Composition

---

## Product Interaction Model

Hotspots (minimal):
- cabinet edge → Nobilia
- fridge → Miele
- lighting → Neuhaus

On tap:
→ show compact side card FIRST

Compact card includes:
- 1 emotional/lifestyle sentence
- product name
- thumbnail
- CTA: "Explore options"

Expanded view (optional):
- variants
- specs
- price

Important:
DO NOT show heavy specs upfront.

---

## UX Principles

- Emotion before specification
- Same room, different life
- Minimal friction
- Clean, premium UI
- Make intents clearly distinct

---

## Visual Rules by Intent

### Calm & Unwind
- warm soft lighting (2700–3000K)
- 1–2 objects only
- subtle steam / glow
- matte cabinets
- centered composition

### Host & Gather
- brighter warm lighting
- curated table setup
- social cues
- layered visuals

### Fast & Focused
- neutral/cool lighting
- functional surfaces
- active cooking cues
- sharp structure

---

## Development Guidelines

- Preserve spatial recognition
- Separate:
  - intent system
  - product system
- Build modular systems:
  - intents
  - hotspots
  - products

- Avoid overengineering
- Optimize for clarity and iteration

---

## Working Style

When working:
1. Read CLAUDE.md + docs first
2. Propose a plan before coding
3. Implement in small steps
4. Keep code modular
5. Avoid unrelated changes