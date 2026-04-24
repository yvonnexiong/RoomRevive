# Backlog

## Phase 1 (Core Prototype)

- [x] Define data models (IntentSO, HotspotSO, ProductSO)
- [x] Import .spz files, set up GaussianSplatRenderer in scene
- [x] Implement intent switching (single GaussianSplatRenderer, swap asset at runtime via IntentManager)
- [x] Build intent selector UI (world-space, 3 buttons)
- [ ] Add hotspot interaction (manually position colliders, wire to OVR ray interactor) ← **YOU ARE HERE**
- [ ] Build compact product card UI
- [ ] Implement before/after slider (GaussianCutout + SeamHandle + BeforeAfterController)

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

### Intent System
- Single `GaussianSplatRenderer` (not 3 separate ones — toggling SetActive on GPU resources crashes Unity)
- Asset swap via `splatRenderer.m_Asset = intent.splatAsset` wrapped in a coroutine (disable → wait frame → set asset → wait frame → re-enable)
- `IntentSO` holds `GaussianSplatAsset` reference (project asset, works in ScriptableObject)
- `splatWorld` moved out of `IntentSO` into `IntentManager` (scene objects can't be referenced from ScriptableObjects)
- Input uses new Unity Input System (`Keyboard.current`) not legacy `Input` class

### WorldLabs / Gaussian Splatting Setup
- Package embedded locally at `Packages/com.worldlabs.gaussian-splatting/` (patched asmdef for Unity 6 compatibility)
- `GaussianSplatURPFeature` added to both `PC_Renderer` and `Mobile_Renderer`
- Render Graph Compatibility Mode enabled (`m_EnableRenderCompatibilityMode: 1`)
- Graphics APIs: D3D12 first on Windows, Vulkan on Android
- Splat assets imported via `Window → WorldLabs → WorldLabsUnityIntegration` into `Assets/WorldLabsWorlds/`
- `IntentDebugSwitcher` (keyboard 1/2/3) is a temporary test tool — remove before shipping
