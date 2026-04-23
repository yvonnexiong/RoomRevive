# Technical Plan

## Core Systems

### 1. Scene System
- base room model
- preserved layout

### 2. Intent System
- applies transformations
- controls lighting, materials, props

### 3. Hotspot System
- anchor points in scene
- triggers UI

### 4. Product System
- maps hotspot → product
- supports variants

### 5. UI System
- compact card
- expanded card

---

## Data Model (Suggested)

Intent:
- id
- name
- lighting config
- surface config
- activity cues

Hotspot:
- id
- position
- linked product

Product:
- id
- name
- brand
- thumbnail
- variants

---

## Key Constraint

DO NOT change layout  
Only transform perception

---

## Future Expansion

- personalization
- time of day
- sound layer