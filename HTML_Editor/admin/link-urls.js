// Save product-urls.json into catalog.json as productPageUrl (+ sku) on each
// matching item. MATCH-ONLY: never creates items. Re-runnable. Reports misses.
//   node link-urls.js
const fs = require('fs');
const path = require('path');
const CAT = path.join(__dirname, 'catalog.json');
const URLS = path.join(__dirname, 'product-urls.json');

const T = { 'å':'aa','â':'a','ä':'a','á':'a','à':'a','ø':'o','ö':'o','ô':'o','æ':'ae','é':'e','è':'e','ü':'u','ß':'ss' };
const norm = s => String(s || '').toLowerCase().replace(/[åâäáàøöôæéèüß]/g, c => T[c] || c).replace(/[^a-z0-9]/g, '');
const skuOf = u => (u.match(/\/product\/(\d+)\//) || [])[1] || '';
const base = f => (f || '').replace(/\.(glb|fbx|gltf)$/i, '');

const ALIAS = {
  'g5540scuslactivebrillanthvid': 'G5540SCU_brilliantwhite_realsize',
  'g5540scuslactivecleansteel':   'G5540SCU_cleansteel_realsize',
  'g5611uactivebrillanthvid':     'G5611U_brilliantwhite_realsize',
  'g5632scuactivesbrillanthvid':  'G5632SCU_brilliantwhite_realsize',
  'g5632scuactivescleansteel':    'G5632SCU_cleansteel_realsize',
};

const cat = JSON.parse(fs.readFileSync(CAT, 'utf8'));
const urls = JSON.parse(fs.readFileSync(URLS, 'utf8'));
const report = {}; const R = c => (report[c] ||= { exact: 0, prefix: 0, alias: 0, miss: 0 });

function setUrl(it, e) {
  it.product ||= {};
  if (!it.product.brand) it.product.brand = 'Miele';
  if (!it.product.name) it.product.name = it.name;
  if (!it.product.modelKey && it.file) it.product.modelKey = base(it.file);
  if (it.product.currency == null) it.product.currency = 'EUR';
  it.product.productPageUrl = e.url;
  if (!it.product.sku) it.product.sku = skuOf(e.url);
}

const linked = new Set(); const missed = [];
// pass 1: alias + exact (file base, or product name+colour)
for (const e of urls) {
  const inCat = cat.items.filter(i => i.category === e.category && !linked.has(i.id));
  const k = norm(e.name);
  let it = ALIAS[k] ? inCat.find(i => base(i.file) === ALIAS[k]) : null;
  if (it) { setUrl(it, e); linked.add(it.id); R(e.category).alias++; e._done = 1; continue; }
  it = inCat.find(i => norm(base(i.file)) === k || norm((i.product?.name || '') + (i.product?.color || '')) === k);
  if (it) { setUrl(it, e); linked.add(it.id); R(e.category).exact++; e._done = 1; }
}
// pass 2: catalog file-base is a prefix of the provided full name (colour-less items)
for (const e of urls) {
  if (e._done) continue;
  const k = norm(e.name);
  const cands = cat.items.filter(i => i.category === e.category && !linked.has(i.id) && base(i.file) && norm(base(i.file)).length >= 5 && k.startsWith(norm(base(i.file))));
  if (cands.length === 1) { setUrl(cands[0], e); linked.add(cands[0].id); R(e.category).prefix++; e._done = 1; }
}
for (const e of urls) if (!e._done) { R(e.category).miss++; missed.push(`${e.category}: ${e.name}`); }

fs.writeFileSync(CAT, JSON.stringify(cat, null, 2));
console.log('Linked per category — exact / prefix / alias / unmatched:');
for (const [c, r] of Object.entries(report)) console.log(`  ${c.padEnd(12)} ${r.exact} / ${r.prefix} / ${r.alias} / ${r.miss}`);
const withUrl = cat.items.filter(i => i.product && i.product.productPageUrl).length;
console.log(`\nItems now with productPageUrl: ${withUrl} / ${cat.items.length}`);
if (missed.length) { console.log(`\nUnmatched URL entries (${missed.length}) — no catalog item to attach to:`); missed.forEach(m => console.log('  ' + m)); }
