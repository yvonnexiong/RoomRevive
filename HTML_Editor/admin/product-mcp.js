#!/usr/bin/env node
/**
 * RoomRevive Product MCP server  —  single-file, zero-dependency.
 *
 * A "data-layer bot": it owns catalog.json (the same file the admin + web +
 * Unity read), keeps it consistent, and lets another bot/agent grab product
 * data from a URL and save it in the ProductData structure we designed.
 *
 * The SERVER owns: schema, storage, dedupe, the product-page link.
 * The CALLING agent owns: extracting fields from the fetched page.
 *
 * Transport: MCP over stdio (newline-delimited JSON-RPC 2.0).
 * Register with Claude Code:
 *     claude mcp add roomrevive-products -- node "<abs path>/product-mcp.js"
 *
 * EDIT ME: the PRODUCT_FIELDS schema below is the single place to change what
 * data a product holds — it mirrors the ProductData diagram.
 */

const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const CATALOG_PATH = path.join(__dirname, 'catalog.json');

// ── ProductData schema (edit here to change the model) ──────────────────────
// type: 'string' | 'number' | 'string[]' | 'url'   ·  req: shown to bots as required
const PRODUCT_FIELDS = {
  brand:           { type: 'string', req: true },
  name:            { type: 'string', req: true },
  subtitle:        { type: 'string' },
  sku:             { type: 'string' },                 // also the dedupe key
  emotionalLine:   { type: 'string' },
  headline:        { type: 'string' },
  description:     { type: 'string' },
  features:        { type: 'string[]' },
  fridgeCapacity:  { type: 'number' },
  freezerCapacity: { type: 'number' },
  annualEnergy:    { type: 'number' },
  noise:           { type: 'number' },
  energyClass:     { type: 'string' },
  dimensions:      { type: 'string' },
  color:           { type: 'string' },
  swatchColor:     { type: 'string' },
  variantGroup:    { type: 'string' },
  price:           { type: 'number' },
  priceDKK:        { type: 'number' },                 // Danish price (kr)
  _priceQuality:   { type: 'string' },
  _priceSource:    { type: 'string' },
  currency:        { type: 'string' },
  rating:          { type: 'number' },
  reviewCount:     { type: 'number' },
  reviews:         { type: 'string[]' },
  productSheetUrl: { type: 'url' },
  productPageUrl:  { type: 'url' },                     // link back to the source
  modelKey:        { type: 'string' },                 // ties web GLB ↔ Unity splat
  // dishwasher / cross-category
  placeSettings:   { type: 'number' },
  energyPer100Cycles: { type: 'number' },
  waterPerCycle:   { type: 'number' },
  noiseClass:      { type: 'string' },
  // hoods
  airflow:         { type: 'number' },                 // max luftydelse m³/h (boost)
  airflowNote:     { type: 'string' },
  // cooktops
  zones:           { type: 'number' },
  induction:       { type: 'boolean' },
  totalPowerKw:    { type: 'number' },
  boosterKw:       { type: 'number' },
  energyClassNote: { type: 'string' },
  // microwaves
  capacityL:       { type: 'number' },
  microwavePowerW: { type: 'number' },
  grill:           { type: 'boolean' },
  grillNote:       { type: 'string' },
  grillPowerW:     { type: 'number' },
  // coffee machines
  waterTank:       { type: 'number' },                 // litres
  beanContainer:   { type: 'number' },                 // grams (total)
  milkContainer:   { type: 'number' },                 // litres
  pumpBar:         { type: 'number' },
  // shared notes / provenance metadata
  dimensionsNote:  { type: 'string' },
  _specSource:     { type: 'string' },
  _dataQuality:    { type: 'string' },
  _copySource:     { type: 'string' },
  _copyQuality:    { type: 'string' },
  _note:           { type: 'string' },
};
// fields the bot must never overwrite (managed by the model-file scan / Unity)
const PROTECTED = ['modelKey'];

// ── catalog persistence ─────────────────────────────────────────────────────
const load = () => { try { return JSON.parse(fs.readFileSync(CATALOG_PATH, 'utf8')); } catch { return { categories: [], items: [] }; } };
const save = (c) => fs.writeFileSync(CATALOG_PATH, JSON.stringify(c, null, 2));
const newId = () => crypto.randomBytes(8).toString('hex');
const log = (...a) => process.stderr.write('[product-mcp] ' + a.join(' ') + '\n');   // logs → stderr only

function coerce(product) {
  const out = {}; const warnings = [];
  for (const [k, v] of Object.entries(product || {})) {
    const f = PRODUCT_FIELDS[k];
    if (!f) { warnings.push(`unknown field "${k}" ignored`); continue; }
    if (v == null) { out[k] = v; continue; }
    if (f.type === 'number') { const n = Number(v); out[k] = Number.isFinite(n) ? n : null; }
    else if (f.type === 'string[]') out[k] = Array.isArray(v) ? v.map(String) : String(v).split(/\s*[\n,]\s*/).filter(Boolean);
    else out[k] = String(v);
  }
  return { product: out, warnings };
}

function findItem(cat, category, product) {
  const inCat = cat.items.filter(i => i.category === category);
  if (product.sku)      { const m = inCat.find(i => i.product?.sku && i.product.sku === product.sku); if (m) return m; }
  if (product.modelKey) { const m = inCat.find(i => i.product?.modelKey === product.modelKey); if (m) return m; }
  if (product.name)     { const m = inCat.find(i => (i.product?.name) === product.name && (i.product?.color || '') === (product.color || '')); if (m) return m; }
  return null;
}

async function fetchSource(url) {
  const res = await fetch(url, { headers: {
    'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120 Safari/537.36',
    'Accept-Language': 'en-GB,en;q=0.9,da;q=0.8',
  }});
  const ct = res.headers.get('content-type') || '';
  if (!res.ok) return { ok: false, status: res.status, contentType: ct, note: `HTTP ${res.status} — the source may block bots (try the datasheet PDF on media.miele.com instead).` };
  if (ct.includes('pdf') || !/(text|html|json|xml)/.test(ct))
    return { ok: true, status: res.status, contentType: ct, note: 'Binary/PDF — not returned as text. Download it and read the file directly.' };
  let text = await res.text();
  text = text.replace(/<script[\s\S]*?<\/script>/gi, ' ').replace(/<style[\s\S]*?<\/style>/gi, ' ')
             .replace(/<[^>]+>/g, ' ').replace(/&nbsp;/g, ' ').replace(/\s+/g, ' ').trim();
  return { ok: true, status: res.status, contentType: ct, text: text.slice(0, 60000) };
}

// ── tools ───────────────────────────────────────────────────────────────────
const TOOLS = {
  get_schema: {
    description: 'Return the ProductData field schema (what to grab for each product) and dedupe rules.',
    inputSchema: { type: 'object', properties: {} },
    run: async () => ({ fields: PRODUCT_FIELDS, dedupeKey: 'sku → modelKey → name+color', protected: PROTECTED }),
  },
  list_categories: {
    description: 'List categories with item counts and how many have product specs.',
    inputSchema: { type: 'object', properties: {} },
    run: async () => {
      const c = load();
      return c.categories.map(cat => {
        const items = c.items.filter(i => i.category === cat.name);
        const withData = items.filter(i => i.product && i.product.brand).length;
        return { category: cat.name, items: items.length, withProductData: withData };
      });
    },
  },
  list_products: {
    description: 'List products, optionally filtered by category. Returns name, sku, color, link and data status.',
    inputSchema: { type: 'object', properties: { category: { type: 'string' } } },
    run: async ({ category }) => {
      const c = load();
      return c.items.filter(i => !category || i.category === category).map(i => {
        const p = i.product || {};
        return { id: i.id, category: i.category, name: p.name || i.name, color: p.color || null,
          sku: p.sku || null, productPageUrl: p.productPageUrl || null,
          status: p.productSheetUrl ? 'verified' : (p.fridgeCapacity != null || p.price != null ? 'partial' : 'name-only') };
      });
    },
  },
  get_product: {
    description: 'Get the full product record by sku, modelKey, or name (optionally within a category).',
    inputSchema: { type: 'object', properties: { sku: { type: 'string' }, modelKey: { type: 'string' }, name: { type: 'string' }, category: { type: 'string' } } },
    run: async (a) => {
      const c = load();
      const it = c.items.find(i => (!a.category || i.category === a.category) && i.product &&
        ((a.sku && i.product.sku === a.sku) || (a.modelKey && i.product.modelKey === a.modelKey) || (a.name && i.product.name === a.name)));
      return it || { error: 'not found' };
    },
  },
  fetch_source: {
    description: 'Fetch a URL (product page / category / search results) and return it as plain text so you can extract product fields. PDFs/binary are flagged, not returned.',
    inputSchema: { type: 'object', properties: { url: { type: 'string' } }, required: ['url'] },
    run: async ({ url }) => fetchSource(url),
  },
  find_product_links: {
    description: 'Fetch a category/listing page and return candidate product-page URLs found on it.',
    inputSchema: { type: 'object', properties: { url: { type: 'string' }, pattern: { type: 'string', description: 'optional regex links must match, default "/product/"' } }, required: ['url'] },
    run: async ({ url, pattern }) => {
      const res = await fetch(url, { headers: { 'User-Agent': 'Mozilla/5.0' } });
      if (!res.ok) return { ok: false, status: res.status };
      const html = await res.text();
      const re = new RegExp(pattern || '/product/', 'i');
      const links = [...html.matchAll(/href="([^"]+)"/gi)].map(m => m[1])
        .filter(h => re.test(h)).map(h => { try { return new URL(h, url).href; } catch { return null; } }).filter(Boolean);
      return { ok: true, links: [...new Set(links)] };
    },
  },
  save_product: {
    description: 'Upsert a product into catalog.json (consistent + deduped). Matches existing by sku→modelKey→name+color and merges; never overwrites protected fields. Always pass productPageUrl (the source link) so we can re-fetch later.',
    inputSchema: { type: 'object', properties: {
      category: { type: 'string', description: 'e.g. Fridges, Hoods, Cooktops' },
      product: { type: 'object', description: 'fields from get_schema; brand+name required' },
    }, required: ['category', 'product'] },
    run: async ({ category, product }) => {
      if (!category) return { error: 'category required' };
      const { product: clean, warnings } = coerce(product);
      if (!clean.brand || !clean.name) return { error: 'product.brand and product.name are required', warnings };
      const c = load();
      if (!c.categories.find(x => x.name === category)) c.categories.push({ id: newId(), name: category, createdAt: Date.now() });
      let it = findItem(c, category, clean), created = false;
      if (!it) {
        it = { id: newId(), category, file: clean.modelKey ? clean.modelKey + '.glb' : '', name: clean.name,
          description: '', status: 'active', gif: null, thumb: null, bytes: 0, missing: !clean.modelKey, createdAt: Date.now(), product: {} };
        c.items.push(it); created = true;
      }
      const before = JSON.stringify(it.product || {});
      const merged = { ...(it.product || {}) };
      for (const [k, v] of Object.entries(clean)) { if (PROTECTED.includes(k) && merged[k]) continue; if (v != null && v !== '') merged[k] = v; }
      merged.lastFetched = new Date().toISOString();
      it.product = merged;
      save(c);
      const changed = before !== JSON.stringify(it.product);
      return { ok: true, action: created ? 'created' : (changed ? 'updated' : 'unchanged'), id: it.id, category, name: merged.name, warnings };
    },
  },
  consistency_report: {
    description: 'Report data-consistency issues: duplicate modelKeys across categories, items missing required fields, and items with no source link.',
    inputSchema: { type: 'object', properties: {} },
    run: async () => {
      const c = load(); const byKey = {}; const dupes = []; const missingReq = []; const noLink = [];
      for (const i of c.items) {
        const p = i.product || {};
        if (p.modelKey) { (byKey[p.modelKey] ||= []).push(i.category); }
        if (p.brand && (!p.name)) missingReq.push({ id: i.id, category: i.category });
        if (p.brand && !p.productPageUrl && !p.productSheetUrl) noLink.push(`${i.category}/${p.name || i.name}`);
      }
      for (const [k, cats] of Object.entries(byKey)) if (cats.length > 1) dupes.push({ modelKey: k, categories: cats });
      return { duplicateModelKeys: dupes, missingRequired: missingReq, withoutSourceLink: noLink.length, sample: noLink.slice(0, 10) };
    },
  },
};

// ── MCP stdio JSON-RPC loop ─────────────────────────────────────────────────
function send(msg) { process.stdout.write(JSON.stringify(msg) + '\n'); }

async function handle(req) {
  const { id, method, params } = req;
  if (method === 'initialize')
    return send({ jsonrpc: '2.0', id, result: { protocolVersion: params?.protocolVersion || '2025-06-18',
      capabilities: { tools: {} }, serverInfo: { name: 'roomrevive-products', version: '1.0.0' } } });
  if (method === 'notifications/initialized' || method?.startsWith('notifications/')) return;
  if (method === 'ping') return send({ jsonrpc: '2.0', id, result: {} });
  if (method === 'tools/list')
    return send({ jsonrpc: '2.0', id, result: { tools: Object.entries(TOOLS).map(([name, t]) => ({ name, description: t.description, inputSchema: t.inputSchema })) } });
  if (method === 'tools/call') {
    const t = TOOLS[params?.name];
    if (!t) return send({ jsonrpc: '2.0', id, error: { code: -32601, message: 'unknown tool' } });
    try {
      const result = await t.run(params.arguments || {});
      return send({ jsonrpc: '2.0', id, result: { content: [{ type: 'text', text: JSON.stringify(result, null, 2) }] } });
    } catch (e) {
      return send({ jsonrpc: '2.0', id, result: { isError: true, content: [{ type: 'text', text: 'Error: ' + e.message }] } });
    }
  }
  if (id != null) send({ jsonrpc: '2.0', id, error: { code: -32601, message: 'method not found: ' + method } });
}

let buf = '';
process.stdin.setEncoding('utf8');
process.stdin.on('data', chunk => {
  buf += chunk;
  let nl;
  while ((nl = buf.indexOf('\n')) >= 0) {
    const line = buf.slice(0, nl).trim(); buf = buf.slice(nl + 1);
    if (line) { try { handle(JSON.parse(line)); } catch (e) { log('parse error', e.message); } }
  }
});
log('ready — catalog:', CATALOG_PATH);
