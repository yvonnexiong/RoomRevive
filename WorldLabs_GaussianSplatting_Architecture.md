# WorldLabs Gaussian Splatting — Architecture Overview

## Pipeline

```
WorldLabs API  →  SPZ File  →  GaussianSplatRenderer  →  URP Render Pipeline  →  Screen
```

---

## Layer 1: WorldLabs API Client (`Runtime/WorldLabs/`)

**`WorldLabsClient.cs`** handles all API communication:
- Authenticates via `WLT-Api-Key` header (loaded from a `.env` file via `EnvLoader.cs`)
- Calls `POST /marble/v1/worlds:generate` to create a 3D world from a text/image prompt
- Polls `GET /marble/v1/operations/{id}` until generation is complete
- Fetches the resulting world's `.spz` file URL from `GET /marble/v1/worlds/{worldId}`

**`WorldLabsWorldManager.cs`** is the glue between the API and the renderer:
1. Calls `ListWorldsAsync()` / `LoadWorldAsync()` on the client
2. Downloads the binary `.spz` file from WorldLabs' CDN
3. Processes it on a background thread into `RuntimeSplatData`
4. Spawns a GameObject with a `GaussianSplatRenderer` component and feeds it the data
5. Reports progress events at 5% → 35% → 90% → 100%

**`WorldBrowserController.cs`** provides a ready-made in-game UI for browsing and loading worlds.

---

## Layer 2: Gaussian Splat Renderer (`Runtime/GaussianSplatting/`)

**`SPZFileReader.cs`** — parses the `.spz` format:
- Gunzips the file, validates a 16-byte header (magic `0x5053474e`)
- Reads 6 packed binary sections: positions (24-bit), opacity, RGB color, scale, rotation quaternions, and spherical harmonics coefficients
- Uses a Burst-compiled parallel job (`UnpackDataJob`) to decompress all splats across multiple CPU cores

**`GaussianSplatRenderer.cs`** — the per-frame GPU renderer:
- Uploads splat data into `GraphicsBuffer` objects (positions, rotations, SH coefficients, colors)
- Each frame: gathers visible splats → GPU sorts by distance from camera (via compute shaders using Radix Sort or AMD FidelityFX) → calculates view-space covariances → issues procedural draw calls

**`GaussianSplatURPFeature.cs`** — hooks into Unity's URP:
- Registered as a `ScriptableRendererFeature` on the URP renderer asset
- Injects a custom `ScriptableRenderPass` at the **BeforeRenderingTransparents** stage
- Renders splats into a float16 RGBA render texture, then composites it onto the camera target via `GaussianComposite.shader`

---

## Shaders (`Shaders/`)

| File | Role |
|---|---|
| `RenderGaussianSplats.shader` | Main splat rasterization |
| `GaussianComposite.shader` | Blends splat texture onto camera target |
| `GaussianSplatting.hlsl` | Core math (2D Gaussian projection) |
| `SphericalHarmonics.hlsl` | View-dependent color from SH coefficients |
| `DeviceRadixSort.hlsl` / `GpuSortFidelityFX.hlsl` | GPU depth sorting |

---

## Cutout / Masking System (`GaussianCutout.cs`)

The toolkit includes a built-in system to hide parts of a splat at render time.

**How it works:**
- `GaussianCutout` defines a volume (Ellipsoid or Box) in world space
- The renderer collects all cutouts in `m_Cutouts[]` and uploads them to the GPU as a `StructuredBuffer<GaussianCutoutShaderData>`
- In the compute shader, each splat's position is tested against every cutout via `IsSplatCut()`
- Splats inside a cutout volume are hidden by setting `centerClipPos.w = 0` — fully excluded from the draw call, not just made transparent

**Cutout shapes:**

| Shape | Clip logic |
|---|---|
| Ellipsoid | `dot(p, p) > 1.0` = outside |
| Box | `any(abs(p) > 0.5)` = outside |

**`m_Invert` flag:** flips the logic to hide everything *outside* the volume instead — useful for isolating a region of interest.

**Usage:**
1. Add a GameObject near your `GaussianSplatRenderer`
2. Attach the `GaussianCutout` component
3. Choose shape (Ellipsoid or Box), position/scale/rotate to cover the region to hide
4. Add the GameObject to the `m_Cutouts` array on `GaussianSplatRenderer`
5. Toggle `m_Invert` to keep the inside and hide everything else

> Note: Cutouts are render-time only — they do not delete splats from the asset. Use `layersToCut` to target specific splat layers if the asset has multiple layers.

---

## Setup Checklist

1. Add package via git URL (`com.worldlabs.gaussian-splatting`)
2. Add API key to `.env` file (`WORLDLABS_API_KEY=...`)
3. Add `GaussianSplatURPFeature` to the URP Renderer asset
4. Enable **Render Graph compatibility mode** in Project Settings
5. Add `WorldLabsWorldManager` + `WorldBrowserController` to a GameObject in the scene
6. At runtime: enter a text prompt → API generates world → `.spz` downloads → renderer displays it in real-time
