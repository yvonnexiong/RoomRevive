# Backlog

## Phase 1 (Core Prototype)

- [x] Define data models (IntentSO, HotspotSO, ProductSO)
- [x] Import .spz files, set up GaussianSplatRenderer in scene
- [x] Implement intent switching (single GaussianSplatRenderer, swap asset at runtime via IntentManager)
- [x] Build intent selector UI (world-space, 3 buttons)
- [x] Startup & world alignment flow (Start screen → grabbable pivot sphere → Confirm)
- [x] Implement before/after slider (head-following UI slider, moves GSCutout local position)
- [x] Add hotspot interaction (gaze dwell, 0.7s — replaced ray interactor approach; `GazeHotspotDetector` + `HotspotInteractable`)
- [x] Build compact product card UI (head-following world-space canvas, auto-hides after 5s)

---

## Utilities (always-on extras)

- [x] Splat opacity slider — head-following world-space slider, controls `GaussianSplatRenderer.m_OpacityScale` (0–1). Useful for comparing splat vs passthrough without the before/after seam. `SplatOpacitySlider.cs`, `SplatOpacityUI` canvas.

---

## Phase 2

- [ ] Expanded product view
- [ ] Variants system
- [ ] GaussianCutout hover effect (dim scene outside hovered hotspot region)
- [ ] Animation polish (motion cues)

---

## Phase 3

- [ ] Performance optimization
- [ ] Personalization
- [ ] Sound layer

---

## Implementation Notes

### Startup & Alignment Flow
- `SplatPivot` is the scene root — `SplatRenderer` is a child, so pivot movement moves the whole splat world
- `AlignmentSphere` drives `SplatPivot` XZ position + Y rotation each frame (no event wiring needed — sphere only moves when grabbed)
- `_floorY` captured in `OnEnable` so Y-lock is always correct when sphere is repositioned
- Sphere spawns 1.5m above pivot (waist/chest height for easy grabbing)
- `IntentManager.autoInitOnStart = false` — init deferred until `StartupController.OnAlignConfirmed()`
- `IntentSelectorUI` positioned in front of user at the moment it's activated
- `OVRManager.AllowRecenter = false` + `OVRManager.boundary.SetVisible(false)` called every frame to suppress boundary
- `OnTrackingAcquired` restores saved pivot transform after any tracking loss/recenter
- All UI canvases (StartUI, AlignmentUI, SplatOpacityUI) use ray + poke ISDK interaction

### Intent System
- Single `GaussianSplatRenderer` (not 3 separate ones — toggling SetActive on GPU resources crashes Unity)
- Asset swap via `splatRenderer.m_Asset = intent.splatAsset` wrapped in a coroutine (disable → wait frame → set asset → wait frame → re-enable)
- `IntentSO` holds `GaussianSplatAsset` reference (project asset, works in ScriptableObject)
- `splatWorld` moved out of `IntentSO` into `IntentManager` (scene objects can't be referenced from ScriptableObjects)
- Input uses new Unity Input System (`Keyboard.current`) not legacy `Input` class

### Hotspot & Product Card
- Hotspots use gaze dwell (not ray/hand) — `RayInteractable` conflicted with ISDK candidate pool and broke canvas interactions
- `GazeHotspotDetector` raycasts from `CenterEyeAnchor` on Hotspot layer (layer 8); 0.7s dwell fires `OnGazeSelect`
- `HotspotInteractable.OnAnySelected` static event decouples hotspots from the card
- `ProductCardUI` head-follows at 1.4m forward, 0.45m right, 0.05m up; auto-hides after 5s
- Hotspots enabled by `IntentManager` only after first intent selection
- `IntentDebugSwitcher` (keyboard 1/2/3) is a temporary test tool — remove before shipping

### WorldLabs / Gaussian Splatting Setup
- Package embedded locally at `Packages/com.worldlabs.gaussian-splatting/` (patched asmdef for Unity 6 compatibility)
- `GaussianSplatURPFeature` added to both `PC_Renderer` and `Mobile_Renderer`
- Render Graph Compatibility Mode enabled (`m_EnableRenderCompatibilityMode: 1`)
- Graphics APIs: D3D12 first on Windows, Vulkan on Android
- Splat assets imported via `Window → WorldLabs → WorldLabsUnityIntegration` into `Assets/WorldLabsWorlds/`
- `IntentDebugSwitcher` (keyboard 1/2/3) is a temporary test tool — remove before shipping
