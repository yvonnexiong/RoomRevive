# ProductBrowser — Designer Guide

> **Module path:** `Assets/RoomRevive/UI/ProductBrowser/`
> **Namespace:** `RoomRevive.ProductBrowser`
> **Last updated:** 2026-05-17

---

## What this system does

ProductBrowser is the UI that opens when the user taps a hotspot (fridge, cabinets, lights).
It has two panels:

| Panel | What it shows |
|-------|--------------|
| **Discover** | Compact teaser card — product image, brand, emotional line, "Explore options" CTA |
| **Swap** | Full product card — brand (with accent dot), name, subtitle, headline, description, spec chips, "From … price", favorite CTA, prev/next navigation, dot indicators, and a small product image pinned top-right |

Both panels are always present in the scene as disabled GameObjects. A hotspot calls `ProductBrowserController.OpenDiscover(categoryData)` to show the right one.

---

## Scene setup (first time)

1. In Unity, open **Tools → RoomRevive → Product Browser → Rebuild All Prefabs**
   - Creates `ProductBrowserUI.prefab`, `ProductDiscoverPanel.prefab`, `ProductSwapPanel.prefab`
   - Creates default category assets: `Category_Fridges`, `Category_Cabinets`, `Category_Lights`

2. Drag `ProductBrowserUI.prefab` into your scene.

3. On the `ProductBrowserController` component:
   - The `view` reference should already be wired. If not, press **🔧 Auto-Bind References**.

4. On the `ProductVariantRouter` component:
   - For **Fridges**: assign `fridgeVariants[0/1/2]` — the 3D fridge GameObjects in the scene.
   - For **Cabinets**: assign `cabinetSplatRooms[0/1/2]` — the SplatRoom enum values.
   - For **Lights**: wire `onLightingVariantSelected` UnityEvent to your light-change logic.

5. On each hotspot: call `ProductBrowserController.OpenDiscover(categoryAsset)` — drag the matching `Category_Fridges/Cabinets/Lights.asset` into the call.

---

## Editing panel visuals

### Path A — Prefab Mode (recommended)

1. Double-click `ProductDiscoverPanel.prefab` or `ProductSwapPanel.prefab` to open Prefab Mode.
2. Edit layout, colors, fonts, spacing freely — nothing will be overwritten by code.
3. Save the prefab.

> ⚠ Do **not** rename or delete the wired UI children (ProductImage, BrandLabel, etc.)
> unless you also update the matching field on `ProductDiscoverView` / `ProductSwapView`.

### Path B — AI-assisted

Give the AI this guide and the snapshot file at
`Assets/RoomRevive/UI/ProductBrowser/Snapshots/ProductBrowserUI.md`.

Run **📸 Export Snapshot** first so the snapshot reflects the actual current state.
Then describe your change: *"Make the Discover card taller, increase the product name font to 24"*.

---

## Adding or editing products

### Adding a product to a catalog

1. **Right-click** in the Project window → **Create → RoomRevive / Product Browser / Product Data**
2. Fill in: `brandName`, `productName`, `subtitle`, `emotionalLine` (shown as the Swap headline),
   `shortDescription`, `specs` (one chip per array entry, e.g. `141 L`), `productImage`, `fromPrice`
3. Open the matching catalog asset (e.g. `FridgesCatalog.asset`)
4. Add the new `ProductData` to the `products` list

> The **Add New Fridge** / **Add New Cabinet** wizards do all of this in one step and now
> include **Subtitle** and **Specs (one per line)** fields.

### Swap-panel fields at a glance

| Slot | Source field | Notes |
|------|-------------|-------|
| Brand dot | `ProductCategoryData.accentColor` | Tinted per category, not per product |
| Brand | `ProductData.brandName` | |
| Name | `ProductData.productName` | |
| Subtitle | `ProductData.subtitle` | Hidden when empty |
| Headline | `ProductData.emotionalLine` | Same field the Discover card uses |
| Description | `ProductData.shortDescription` | |
| Spec chips | `ProductData.specs[]` | Up to 4 chips; row hidden when empty |
| Price | `ProductData.fromPrice` | A leading "From" renders as a separate label |
| Image | `ProductData.productImage` | Small thumbnail pinned top-right |

Changes to a `ProductData` asset immediately refresh any open swap panels in the editor.

### Editing an existing product

Open the `ProductData` asset in the Inspector and edit directly.
The swap panel in the scene updates live if that product is currently displayed.

### Reordering products

Drag items in the `ProductCatalog.products` list. The dot indicators and nav buttons update automatically.

---

## Adding a new category (e.g. Countertops)

1. **Create → RoomRevive / Product Browser / Product Category** — name it `Category_Countertops`
2. Set `id`, `displayName`, `swapType` (use `LightingOnly` if no geometry swap, or add a new type)
3. Assign a catalog asset
4. Wire the hotspot to call `OpenDiscover(Category_Countertops)`
5. Wire the swap effect on `ProductVariantRouter.onLightingVariantSelected` (or extend the router)

---

## Inspector buttons reference

| Button / Menu | What it does | Safe? |
|--------|-------------|-------|
| 📸 Export Snapshot | Writes current state to `Snapshots/ProductBrowserUI.md` | ✅ Read-only |
| 🔗 Sync | Fills null references, creates missing assets | ✅ Non-destructive |
| 🔧 Auto-Bind | Wires Controller → View → Panels | ✅ Only fills nulls |
| **Rebuild Swap Panel (Safe)** | Regenerates **only** `ProductSwapPanel.prefab` and re-wires its buttons inside `ProductBrowserUI.prefab` without recreating the root | ✅ Scene instances keep their category / variant assignments |
| ⚠ Rebuild (Destructive) | Recreates all three prefabs from scratch | ❌ Destroys manual edits **and** orphans scene-instance overrides |

**Rule of thumb:** to change the Swap panel layout in code, edit `ProductBrowserPrefabCreator.BuildSwapPrefab()`
then run **Tools → RoomRevive → Product Browser → Rebuild Swap Panel (Safe)**. Only use the full
destructive Rebuild when starting completely fresh.

---

## What designers should NOT change without a developer

- The C# scripts (Controller, View, Routers)
- The `ProductSwapType` enum values on category assets — changing swap type requires router re-wiring
- Removing GameObjects that are serialized references on View scripts
- The folder or namespace structure

---

## File map

```
Data/Product/
  ProductData.cs              SO definition — one per product
  ProductCatalog.cs           SO definition — ordered list of products
  ProductCategoryData.cs      SO definition — one per category (Fridges/Cabinets/Lights)
  Category_Fridges.asset
  Category_Cabinets.asset
  Category_Lights.asset

Prefabs/Product/
  ProductBrowserUI.prefab     Root — always in scene, owns both panels
  ProductDiscoverPanel.prefab Teaser card
  ProductSwapPanel.prefab     Variant browser

Scripts/Runtime/Product/
  ProductBrowserController    State machine + selection logic
  ProductBrowserView          Root view — shows/hides panels
  ProductDiscoverView         Binds data into Discover panel
  ProductSwapView             Binds data into Swap panel
  ProductVariantRouter        Side effects: fridge swap / splat swap / light event
  ProductVisibilityRouter     Shows/hides scene objects per state or product

Scripts/Editor/Product/
  ProductBrowserControllerEditor  Custom inspector with setup buttons
  ProductBrowserPrefabCreator     Builds prefabs from scratch (menu + Rebuild button)
  ProductBrowserPrefabBinder      Non-destructive sync (menu + Sync button)
  ProductBrowserSnapshotExporter  Exports Markdown snapshot
```
