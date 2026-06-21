const fs = require('fs');
const path = require('path');
const DIR = __dirname;
const urls = require(path.join(DIR,'product-urls.json'));
const APPLY = process.argv.includes('--apply');

const dk = s => (s||'').toLowerCase()
  .replace(/å/g,'aa').replace(/ø/g,'o').replace(/æ/g,'ae')
  .replace(/ü/g,'u').replace(/é/g,'e').replace(/ß/g,'ss');
function tokens(name){
  return dk(name)
    .replace(/([a-z])(\d)/g,'$1 $2').replace(/(\d)([a-z])/g,'$1 $2')
    .split(/[^a-z0-9]+/).filter(Boolean);
}
const DROP = new Set([
  'hvid','brilliantwhite','brillanthvid','brilliantweiss','weiss','white',
  'cleansteel','clean','steel','staallook','stallook','staal','stal','rustfrit','rustfri',
  'blacksteel','blackboard','black','sort','obsidiansort','obsidian',
  'grafitgra','grafitgraa','grafit','graa','gra','mathvid','matsort','mat',
  'glas','brillant','silver','silber','dor',
  'realsize','sl','active','s','cinematic','front'
]);
const modelKey = name => tokens(name).filter(t => !DROP.has(t)).join(' ');

// always read the freshest catalog from disk
const cat = JSON.parse(fs.readFileSync(path.join(DIR,'catalog.json'),'utf8'));

const idx = new Map();
for (const u of urls){
  const k = dk(u.category)+'|'+modelKey(u.name);
  if(!idx.has(k)) idx.set(k, []);
  idx.get(k).push(u);
}
let set=0, miss=0, changed=0; const unmatched=[]; const per={};
for (const it of cat.items){
  const p = it.product || (it.product = {});
  const k1 = dk(it.category)+'|'+modelKey(p.name || '');
  const k2 = dk(it.category)+'|'+modelKey(it.name || '');
  const cands = idx.get(k1) || idx.get(k2);
  per[it.category] = per[it.category] || {m:0,t:0};
  per[it.category].t++;
  if (cands && cands.length){
    set++; per[it.category].m++;
    // prefer a candidate whose color tokens overlap the item's name; else first
    const itColor = new Set(tokens((p.name||'')+' '+it.name).filter(t=>DROP.has(t)));
    let best = cands[0], bestScore = -1;
    for (const c of cands){
      const cc = tokens(c.name).filter(t=>DROP.has(t));
      const score = cc.filter(t=>itColor.has(t)).length;
      if (score > bestScore){ bestScore = score; best = c; }
    }
    if (APPLY && p.productPageUrl !== best.url){ p.productPageUrl = best.url; changed++; }
  } else {
    miss++; unmatched.push(it.category+' / '+(p.name||it.name));
  }
}
console.log((APPLY?'APPLIED':'WOULD SET'), set, '/', cat.items.length, '| unmatched:', miss, APPLY?('| fields changed: '+changed):'');
Object.keys(per).sort().forEach(c=>console.log('  ',c+':',per[c].m,'/',per[c].t));
if(!APPLY){ console.log('--- unmatched ---'); unmatched.forEach(x=>console.log('  ',x)); }

if (APPLY){
  fs.copyFileSync(path.join(DIR,'catalog.json'), path.join(DIR,'catalog.before-urls.json'));
  fs.writeFileSync(path.join(DIR,'catalog.json'), JSON.stringify(cat,null,2));
  console.log('catalog.json written (backup: catalog.before-urls.json)');
}
