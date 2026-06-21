# Catalog system — agent contracts (the seams)

Shared coordination doc for the three agents that build the RoomRevive product catalog.
Every agent reads this at the start of a task. It is the source of truth for WHO does WHAT and WHO may WRITE.

## Source of truth
- `catalog.json` (in this folder) is the shared data the dashboard renders. Everyone may READ it.

## Single-writer rule (the #1 discipline)
- **`server.js` is the ONLY process that writes `catalog.json`.** All writes go through its REST API.
- `product-data` persists by calling that API (`product-mcp.js` is the client) — it does NOT write the file.
- `model-generator` writes ONLY binaries into `3D-models/<Category>/`; the watcher reflects them.
- Writes are atomic (temp file + rename) and merge-on-write (never clobber a field another agent owns).

## Ownership map
| Agent | Owns | Must NOT |
|---|---|---|
| model-generator | binaries in `3D-models/<Category>/`, `_manifest.csv`, renders/QC, category→product-list discovery | write catalog.json |
| product-data | specs, the `ProductData` schema, `validate.js`, datasheet fetching, URL linking | write catalog.json directly; fabricate values |
| catalog-admin | `server.js`, UI, folder watcher, ALL catalog.json writes, browser refresh | generate models; invent schema fields |

## Pipeline order (URL → dashboard)
1. `model-generator` discovers the product list from the URL (handles bot-blocking) and emits material numbers **+ the agreed file stem (`modelKey`) per product**.
2. `product-data` EXTRACTS specs from each datasheet — **does not write the catalog yet**.
3. `model-generator` builds `<stem>.glb` (+`.fbx`) into `3D-models/<Category>/`; the server scan creates the file-backed item.
4. `product-data` ATTACHES specs via `PUT /api/items/:id` (the item now exists and is file-backed).
5. `catalog-admin` verifies + refreshes the browser.

> ⚠️ **Order matters because of `AUTO_PRUNE_MISSING = true` in server.js:** any item whose file is not on disk is REMOVED on the next scan/watch tick. So specs must be WRITTEN only AFTER the model file exists — **extract early, write late**. (Refinement option: make `pruneMissing` skip items that already carry `product` data, so a data-first order becomes safe.)

## Definition of done (the gates)
- product-data: every product in the list has an entry; required fields populated or blank-with-reason; `validate.js` passes; no dupes.
- model-generator: GLB+FBX exist, scaled, correct image; QC passes or item flagged for review.
- catalog-admin: catalog reflects disk; merge preserved foreign fields; browser refreshed.

## Self-documentation protocol
Each agent keeps ONE rules file it reads every run and appends learnings to:
- product-data → `admin/DATA_RULES.md`
- model-generator → `3DModelGenerator/PIPELINE_RULES.md`
- catalog-admin → `admin/ADMIN_RULES.md`

Curated "Operating Rules" stay SHORT (read each run); raw corrections go in the "Journal" section; durable ones get promoted up into Operating Rules.

## Verified contract details (from the actual code, 2026-06-19)

### The join key — `modelKey` == model filename stem
A product's DATA and its 3D MODEL only land on the SAME catalog item if they agree on the key:
- model-generator names the file `<stem>.glb` / `<stem>.fbx` in `3D-models/<Category>/`.
- product-data sets `product.modelKey = "<stem>"` (filename WITHOUT extension) in `save_product`.
- Example: file `G5540SCU_brilliantwhite_realsize.glb` ↔ `modelKey: "G5540SCU_brilliantwhite_realsize"`.
- Disagree → you get TWO items (one data-only/`missing`, one file-only/no specs). Agree on the stem first.

### Item shape — two non-colliding zones
- Top-level (`category, file, name, bytes, gif, thumb, missing`) = model-file metadata; owned by the scan / model-generator.
- Nested `product: {}` = specs; owned by product-data (merged shallowly; `modelKey` is protected).
- Never write the other zone.

### Who writes `catalog.json` today (the violation to fix)
- `server.js` (http://localhost:4173) writes on: startup scan, the `fs.watch` auto-scan (800ms debounce), and every `/api` write (`/api/scan`, `/api/categories`, `/api/items`, `/api/prune-missing`).
- `product-mcp.js` writes it DIRECTLY as a separate process (`save_product`). ← single-writer violation; this is what races the server.
- Fix (later): port `save_product`'s upsert into a server endpoint (e.g. `POST /api/products`) and make product-mcp an HTTP client; add atomic temp+rename. **Until then: never run `save_product` while the server is up — stop it during bulk data writes.**

### Browser refresh — already half-built
- `GET /api/version` → `{rev: <mtimeMs>, items, categories}`. Poll THAT (cheap) and only refetch `/api/catalog` when `rev` changes. Replaces the blind timer; SSE optional later.

### Creating a NEW category — any of these do it
- Drop a `3D-models/<NewCategory>/` folder → the watcher's scan adds it.
- `POST /api/categories {name}` → adds it AND mkdirs the folder.
- `save_product {category: <NewCategory>, ...}` → auto-creates it.

## Master / orchestrator rules (learned 2026-06-20)
- **Do the FULL category.** Given a category link, process EVERY product in it — never silently ship a self-chosen sample. If scope genuinely must be cut, say so explicitly and ask first.
- **3D model source of truth = the product's web 3D-viewer GLB** (PIPELINE_RULES rule 0), NOT the FBX. Truncate it to its header-declared length. The admin only ever receives that real GLB; we never invent materials when a web GLB exists.
- **Definition of done includes DATA coverage.** A batch is NOT done when the models exist — every item must also have its data filled (price + specs, or blank-with-reason). The master runs a coverage audit (`audit_data.py`) before accepting; any gaps go back to `product-data` to fill.
- **"Data filled" means ALL schema fields, not just specs.** A complete item also needs the marketing copy — `subtitle`, `emotionalLine`/`headline`, `description`, `features`, `color` — which come from the PRODUCT PAGE (the datasheet carries specs only). The coverage audit must check these too, not just price+dimensions. (A shallow audit wrongly passed the cooktops "23/23" while every copy field was empty — 2026-06-20.)
- **Output language = English; price = EUR.** The source (miele.dk) is Danish — all stored copy (`subtitle`/`headline`/`description`/`features`) must be **English**, translated faithfully, keeping Miele product/feature names verbatim (Con@ctivity, PowerFlex, TwinBooster, CleanCover, MilkPerfection…). Every item needs a **price in EUR**: take the DKK price off the product page, set `price` = round(DKK ÷ 7.46) (the DKK→EUR peg), `currency` = "EUR", and keep the original in `priceDKK`. Coverage audit must check: English copy + a EUR price on every item.
- **Corrections persist the same turn.** The moment the user (or master) makes a correction, it lands in the relevant agent's OWN context file — `PIPELINE_RULES.md` / `DATA_RULES.md` / `ADMIN_RULES.md` / this file — never left only in chat. Preferred: prompt that agent to self-journal it (it knows its own history best, e.g. via the `/journal` skill); if the agent isn't running, master writes it immediately. This is a standing master rule.
- **Verify in the browser, not just the JSON.** "Data is filled" is only true once it actually renders in the admin UI — data present in `catalog.json` but invisible in the dashboard is still a defect (cache or display logic).
