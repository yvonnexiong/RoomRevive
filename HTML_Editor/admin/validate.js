// Validate catalog.json against the ProductData schema and report completeness.
// Run anytime:  node validate.js
const fs = require('fs');
const path = require('path');
const CAT = path.join(__dirname, 'catalog.json');

// keep in sync with product-mcp.js PRODUCT_FIELDS
const FIELDS = {
  brand:{type:'string',req:true}, name:{type:'string',req:true}, subtitle:{type:'string'},
  sku:{type:'string'}, emotionalLine:{type:'string'}, headline:{type:'string'}, description:{type:'string'},
  features:{type:'array'}, fridgeCapacity:{type:'number'}, freezerCapacity:{type:'number'},
  annualEnergy:{type:'number'}, noise:{type:'number'}, energyClass:{type:'string'}, dimensions:{type:'string'},
  color:{type:'string'}, swatchColor:{type:'string'}, variantGroup:{type:'string'},
  price:{type:'number'}, currency:{type:'string'}, rating:{type:'number'}, reviewCount:{type:'number'},
  reviews:{type:'array'}, productSheetUrl:{type:'string'}, productPageUrl:{type:'string'}, modelKey:{type:'string'},
  localImage:{type:'string'},   // /models-resolvable path to the rendered local image (png/gif)
  // dishwasher / cross-category
  placeSettings:{type:'number'}, energyPer100Cycles:{type:'number'}, waterPerCycle:{type:'number'}, noiseClass:{type:'string'},
  // coffee machines
  waterTank:{type:'number'}, beanContainer:{type:'number'}, milkContainer:{type:'number'}, pumpBar:{type:'number'}, _dimSource:{type:'string'},
  // pricing
  priceDKK:{type:'number'}, _priceQuality:{type:'string'}, _priceSource:{type:'string'},
  // hoods
  airflow:{type:'number'}, airflowNote:{type:'string'},
  // cooktops
  zones:{type:'number'}, induction:{type:'boolean'}, totalPowerKw:{type:'number'}, boosterKw:{type:'number'},
  energyClassNote:{type:'string'},
  // microwaves
  capacityL:{type:'number'}, microwavePowerW:{type:'number'}, grill:{type:'boolean'}, grillNote:{type:'string'},
  grillPowerW:{type:'number'}, turntableCm:{type:'number'}, weightKg:{type:'number'},
  // shared notes / provenance metadata
  dimensionsNote:{type:'string'}, _specSource:{type:'string'}, _dataQuality:{type:'string'}, _note:{type:'string'},
  _copySource:{type:'string'}, _copyQuality:{type:'string'},
  // kitchens (image-based items, rendered in the dedicated kitchen panel)
  productType:{type:'string'}, priceLabel:{type:'string'}, range:{type:'string'}, flag:{type:'string'}, kitchenType:{type:'string'},
  front:{type:'string'}, carcase:{type:'string'}, worktop:{type:'string'}, handle:{type:'string'},
  frontImage:{type:'string'}, carcaseImage:{type:'string'}, worktopImage:{type:'string'}, handleImage:{type:'string'},
  heroImage:{type:'string'}, additionalImages:{type:'array'}, beforeImage:{type:'string'}, afterImage:{type:'string'},
  gallery:{type:'array'}, _source:{type:'string'}, reviewScore:{type:'number'}, reviewStars:{type:'number'},
  antiFingerprint:{type:'boolean'},
  designElementRefs:{type:'object'},   // kitchen -> {front,carcase,worktop,handle} design-element ids
};
const SPEC_FIELDS = ['fridgeCapacity','freezerCapacity','annualEnergy','noise','energyClass','dimensions'];

const cat = JSON.parse(fs.readFileSync(CAT, 'utf8'));
const typeOk = (v,t) => t==='array' ? Array.isArray(v) : t==='number' ? typeof v==='number' : t==='boolean' ? typeof v==='boolean' : t==='object' ? (typeof v==='object' && !Array.isArray(v)) : typeof v==='string';

const per = {}; const typeErrors = []; const missingReq = []; const dupes = {};
for (const it of cat.items) {
  const c = per[it.category] ||= { total:0, withProduct:0, hasSpecs:0, hasPageUrl:0, hasSheet:0, complete:0 };
  c.total++;
  const p = it.product;
  // duplicate detection — fall back to the unique item id for image-based items
  // (kitchens) that legitimately have no modelKey/model file.
  const key = (p && p.modelKey) || it.file || it.id || it.name;
  (dupes[it.category+'|'+key] ||= []).push(it.name);
  if (!p || !Object.keys(p).length) continue;
  c.withProduct++;
  if (!p.brand || !p.name) missingReq.push(`${it.category}/${it.name}`);
  for (const [k,v] of Object.entries(p)) {
    if (v==null || k==='lastFetched') continue;
    const f = FIELDS[k];
    if (!f) { typeErrors.push(`${it.name}: unknown field "${k}"`); continue; }
    if (v!=='' && !typeOk(v, f.type)) typeErrors.push(`${it.name}: ${k} should be ${f.type}, got ${typeof v}`);
  }
  const hasSpecs = SPEC_FIELDS.some(k => p[k]!=null && p[k]!=='');
  if (hasSpecs) c.hasSpecs++;
  if (p.productPageUrl) c.hasPageUrl++;
  if (p.productSheetUrl) c.hasSheet++;
  if (p.brand && p.name && hasSpecs && p.productPageUrl) c.complete++;
}

console.log('Per category — total / withProduct / hasSpecs / hasPageUrl / hasSheet / complete:');
for (const [cat_, c] of Object.entries(per))
  console.log(`  ${cat_.padEnd(12)} ${c.total} / ${c.withProduct} / ${c.hasSpecs} / ${c.hasPageUrl} / ${c.hasSheet} / ${c.complete}`);

const dupList = Object.entries(dupes).filter(([,v]) => v.length>1);
console.log(`\nDuplicate model keys/files: ${dupList.length}`);
dupList.slice(0,10).forEach(([k,v]) => console.log(`  ${k} ×${v.length}`));
console.log(`Items missing required (brand/name): ${missingReq.length}`);
console.log(`Type errors: ${typeErrors.length}`);
typeErrors.slice(0,10).forEach(e => console.log('  '+e));

// items that have a source link but no specs yet — the enrichment to-do list
const todo = cat.items.filter(i => i.product && (i.product.productPageUrl||i.product.productSheetUrl)
  && !SPEC_FIELDS.some(k => i.product[k]!=null && i.product[k]!==''));
console.log(`\nHave a link but no specs yet (enrich next): ${todo.length}`);
todo.slice(0,30).forEach(i => console.log(`  ${i.category}: ${i.product.name||i.name}`));
