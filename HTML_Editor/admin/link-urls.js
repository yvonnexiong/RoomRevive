// Link product-urls.json into catalog.json: set productPageUrl + sku on matching
// items, create new items for entries we have no model for. Re-runnable.
//   node link-urls.js
const fs = require('fs');
const path = require('path');
const CAT = path.join(__dirname, 'catalog.json');
const URLS = path.join(__dirname, 'product-urls.json');
const newId = () => require('crypto').randomBytes(8).toString('hex');

const T = { 'å':'aa','â':'a','ä':'a','á':'a','à':'a','ø':'o','ö':'o','ô':'o','æ':'ae','é':'e','è':'e','ü':'u','ß':'ss' };
const norm = s => String(s || '').toLowerCase().replace(/[åâäáàøöôæéèüß]/g, c => T[c] || c).replace(/[^a-z0-9]/g, '');
const skuOf = u => (u.match(/\/product\/(\d+)\//) || [])[1] || '';
const base = f => (f || '').replace(/\.glb$/i, '');

// dishwasher names diverge too much (English GLB vs Danish site) — map explicitly
const ALIAS = {
  'g5540scuslactivebrillanthvid': 'G5540SCU_brilliantwhite_realsize',
  'g5540scuslactivecleansteel':   'G5540SCU_cleansteel_realsize',
  'g5611uactivebrillanthvid':     'G5611U_brilliantwhite_realsize',
  'g5632scuactivesbrillanthvid':  'G5632SCU_brilliantwhite_realsize',
  'g5632scuactivescleansteel':    'G5632SCU_cleansteel_realsize',
};

const cat = JSON.parse(fs.readFileSync(CAT, 'utf8'));
const urls = JSON.parse(fs.readFileSync(URLS, 'utf8'));
const report = {};
const R = c => (report[c] ||= { exact: 0, prefix: 0, alias: 0, created: 0 });

function linkItem(it, e) {
  it.product ||= {};
  if (!it.product.brand) it.product.brand = 'Miele';
  if (!it.product.name) it.product.name = it.name;
  if (it.product.currency == null) it.product.currency = 'EUR';
  if (!it.product.modelKey && it.file) it.product.modelKey = base(it.file);
  it.product.productPageUrl = e.url;
  if (!it.product.sku) it.product.sku = skuOf(e.url);
}

const used = new Set();
const linked = new Set();          // catalog item ids already given a url this run

// pass 1: alias + exact
for (const e of urls) {
  const inCat = cat.items.filter(i => i.category === e.category && !linked.has(i.id));
  const k = norm(e.name);
  let it = null;
  if (ALIAS[k]) it = inCat.find(i => base(i.file) === ALIAS[k]);
  if (it) { linkItem(it, e); used.add(e.url); linked.add(it.id); R(e.category).alias++; continue; }
  it = inCat.find(i => norm(base(i.file)) === k || norm((i.product?.name || '') + (i.product?.color || '')) === k);
  if (it) { linkItem(it, e); used.add(e.url); linked.add(it.id); R(e.category).exact++; }
}
// pass 2: catalog model-name is a prefix of the provided full name (colour-less GLB items)
for (const e of urls) {
  if (used.has(e.url)) continue;
  const k = norm(e.name);
  const cands = cat.items.filter(i => i.category === e.category && !linked.has(i.id) && base(i.file) && norm(base(i.file)).length >= 5 && k.startsWith(norm(base(i.file))));
  if (cands.length === 1) { linkItem(cands[0], e); used.add(e.url); linked.add(cands[0].id); R(e.category).prefix++; }
}
// pass 3: create new items for the rest
for (const e of urls) {
  if (used.has(e.url)) continue;
  if (!cat.categories.find(c => c.name === e.category)) cat.categories.push({ id: newId(), name: e.category, createdAt: Date.now() });
  cat.items.push({ id: newId(), category: e.category, file: '', name: e.name, description: '', status: 'active',
    gif: null, thumb: null, bytes: 0, missing: true, createdAt: Date.now(),
    product: { brand: 'Miele', name: e.name, sku: skuOf(e.url), currency: 'EUR', features: [], reviews: [], productPageUrl: e.url, modelKey: null } });
  used.add(e.url); R(e.category).created++;
}

fs.writeFileSync(CAT, JSON.stringify(cat, null, 2));
console.log('Per category — exact / prefix / alias / created:');
for (const [c, r] of Object.entries(report)) console.log(`  ${c.padEnd(12)} ${r.exact} / ${r.prefix} / ${r.alias} / ${r.created}`);
// catalog items still without a url
const noUrl = cat.items.filter(i => !(i.product && i.product.productPageUrl));
console.log(`\nItems still without URL: ${noUrl.length}`);
const byCat = {}; noUrl.forEach(i => (byCat[i.category] ||= []).push(i.product?.name || i.name));
for (const [c, list] of Object.entries(byCat)) console.log(`  ${c} (${list.length}): ${list.join(', ')}`);
console.log(`\nTotal items: ${cat.items.length}`);
