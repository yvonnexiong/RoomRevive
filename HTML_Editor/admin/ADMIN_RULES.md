# catalog-admin — operating rules & journal

Read this at the start of every task and follow the Operating Rules.
Keep Operating Rules SHORT and authoritative. Log raw corrections in the Journal; promote durable ones up.

## Operating Rules
<!-- Curated do/don't list. Seed this from your own history on first run (see bottom). -->
- **Single writer.** Only `server.js` writes `catalog.json`, and only through its REST API. Never hand-edit the file or run one-off scripts that write it — I did this with `merge_urls.js --apply` and an ad-hoc cleanup script, and that races the watcher and the other agents. Route every write through the API.
- **Merge, never clobber.** A write touches only the fields it changes; leave fields owned by `product-data` / `model-generator` intact.
- **Run the server detached, then verify it.** A backgrounded process gets reaped and the page looks "down." Launch detached and confirm `GET /api/catalog` actually responds before saying it's up.
- **Bust the browser cache after UI edits.** Bump `app.js?v=N` and keep UI assets `no-store`. A "data isn't showing" report is usually a stale `app.js`, not missing data.
- **Empty must look empty.** No realistic placeholder text in form inputs — it gets mistaken for real values.
- **Scan every model format on disk** (`.glb` and `.fbx`). A category that looks empty is often an unsupported extension, not absent data.
- **Missing/renamed files: prune, don't pile up.** A rename = old file gone + new file added; auto-prune the stale entry (including when a whole category folder disappears) and dedup glb/fbx + renamed variants, so the UI never fills with dead cards.
- **Confirm the source before writing.** When the human points loosely ("the json file"), verify which file/field they mean first — I guessed `product-urls.json` when they meant `catalog.json`.
- **Make whole controls clickable**, not just the text label inside them.
- **Card spec row must be schema-agnostic.** Use a `SPEC_FIELDS` table (not category-specific `if` chains) so any new field a product-data agent adds automatically renders on the card. Adding a new product field = add one entry to `SPEC_FIELDS`, never rewrite the loop.

## Definition of done
- catalog.json reflects every model file on disk (watcher add/prune ran).
- merge-on-write preserved fields owned by other agents.
- Browser was refreshed (version-field bump or SSE), not left to a blind timer.

## Journal (append-only — newest first)
<!-- Format:
### 2026-06-19 — short title
- what went wrong / what the human had to tell me
- the rule that prevents it next time
-->

### 2026-06-20 — product-data added category-specific fields (hoods/cooktops/microwaves); SPEC_FIELDS mirrored
- product-data agent added new schema fields to validate.js and product-mcp.js for three categories: hoods (`airflow`), cooktops (`zones`, `totalPowerKw`, `boosterKw`, `induction`), microwaves (`capacityL`, `microwavePowerW`, `grill`, `grillPowerW`). Without mirroring these into app.js SPEC_FIELDS, none rendered on cards.
- Fix: added 10 new SPEC_FIELDS entries with sensible labels/units; booleans (`induction`, `grill`) return null from fmt() when false, so also hardened the loop to skip null fmt results. Bumped app.js?v=10 → v=11.
- Concrete reinforcement of the existing rule: **adding a new product field = add one SPEC_FIELDS entry**. product-data owns the schema; admin must mirror display rules promptly or the UI silently shows empty cards despite real data in the catalog.

### 2026-06-20 — Filled coffee data didn't show: card specs are hardcoded to fridge fields (FIXED)
- product-data filled price + datasheet dims (waterTank, power) for all 20 espresso machines (confirmed in `catalog.json`), but the dashboard still "looked like only 4 had data." Two causes: (1) a stale browser / cached `app.js`; (2) the card's spec row only renders FRIDGE fields (`fridgeCapacity/freezerCapacity/noise/annualEnergy`), so coffee specs (`dimensions/waterTank/beanContainer/power`) never display even when present. Price does show.
- Fix applied 2026-06-20: replaced the four hardcoded fridge `if` checks in `app.js` lines 140-145 with a `SPEC_FIELDS` table that covers both fridge and coffee-machine fields (`fridgeCapacity`, `freezerCapacity`, `noise`, `annualEnergy`, `waterTank`, `beanContainer`, `power`, `energyClass`, `dimensions`). The loop caps at 4 spec chips per card; all fields still appear in the full Details modal. Bumped `app.js?v=9` → `v=10` in `index.html`. Verified: `GET /api/catalog` live, CM 5410 Silence (sku 11541630) returns `waterTank: "1.3 L"` and `dimensions: "H360 × W241 × D460 mm"`, and served `app.js` contains the new SPEC_FIELDS table.
- Rule: the card specs must be **schema-agnostic** — render whatever spec fields exist for the item, per "surface any field present" principle; do not hardcode one category's fields. And cache-bust `app.js?v=N` after edits, then **verify in the browser**, not just the JSON.

### 2026-06-19 — I wrote catalog.json directly (single-writer violation)
- To merge product-page URLs I ran `merge_urls.js --apply`, which wrote `catalog.json` straight to disk; I also hand-removed a test entry with a node script. At the same time an external/product-data process was writing the file too (a `catalog.backup.json` appeared and 8 items gained specs).
- Rule: every catalog write goes through `server.js`'s API — no scripts, no editors. Concurrent + direct writers are exactly how the file shifts or corrupts.

### 2026-06-19 — "Update items from the json file" — I picked the wrong file
- I assumed `product-urls.json` and did a whole URL-merge; the human meant `catalog.json` itself.
- Rule: resolve which file/field a loose reference means before acting on it.

### 2026-06-19 — Missing files should vanish, not linger
- After folder renames the catalog held 138 stale "FILE MISSING" entries; the human had to tell me twice to just remove them.
- Rule: auto-prune entries whose file (or whole category folder) is gone; treat a rename as remove-old + add-new and dedup so stale records don't accumulate.

### 2026-06-19 — Microwaves looked empty because they were .fbx
- The scanner only matched `.glb`, so the Microwaves category (all `.fbx`) showed zero items even though files were present.
- Rule: scan every model format on disk; an empty-looking category may just be an extension the scanner ignores.

### 2026-06-19 — "No data in the fields" was stale cache + decoy placeholders
- The human opened a dishwasher's (data-less) Edit form and thought data was missing. Two causes: the browser served a cached `app.js`, and my placeholder text was realistic fridge copy that looked like real values.
- Rule: cache-bust UI assets after every edit (`no-store` + `?v=`), and never use realistic placeholder text.

### 2026-06-19 — Server kept appearing "down"
- I launched node in the background; it got reaped between turns, so the URL showed nothing.
- Rule: start the server as a detached process and verify `/api/catalog` responds before reporting it running.

### 2026-06-19 — Category tabs wouldn't switch
- I'd put the click handler only on the label text, so clicking the rest of the row did nothing.
- Rule: bind interactions to the whole control, not just the inner text.
