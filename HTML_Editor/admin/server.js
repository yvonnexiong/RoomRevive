/**
 * 3D Model Admin — zero-dependency Node server.
 *
 * Serves an admin UI for managing categories of 3D models stored on disk.
 * Data lives in catalog.json (next to this file). The actual model files stay
 * in ../3D-models/<Category>/*.glb and are never touched unless you explicitly
 * use the "Delete from disk" action.
 *
 * Run:  node server.js   (then open http://localhost:4173)
 */

const http = require('http');
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const PORT = process.env.PORT || 4173;
const ROOT = __dirname;
const MODELS_DIR = path.resolve(ROOT, '..', '3D-models');
const CATALOG_PATH = path.join(ROOT, 'catalog.json');
const PUBLIC_DIR = path.join(ROOT, 'public');
const FAVS_PATH = path.join(ROOT, 'favorites.json');
// The Homepage dashboard bundle (served live at /dashboard).
const HOMEPAGE_PATH = path.resolve(ROOT, '..', '..', 'Homepage', 'RoomRevive Dashboard (RoomCapture before).html');

const MODEL_EXTS = ['.glb', '.fbx']; // .glb renders live; .fbx is catalogued (no in-browser 3D preview)
const isModel = (f) => MODEL_EXTS.includes(path.extname(f).toLowerCase());
const SUFFIXES = ['_realsize', '_cinematic', '_front', '_q3']; // stripped to compute a grouping key

// "Swatch roots": container folders whose SUBfolders are surfaced as their own image
// categories (the image file IS the preview, like Kitchens). e.g. DesignElements/Handles
// becomes a "Handles" category of swatch items. Each swatch image -> one image item.
const SWATCH_ROOTS = ['DesignElements'];
const IMAGE_EXTS = ['.jpg', '.jpeg', '.png', '.webp'];
const isImage = (f) => IMAGE_EXTS.includes(path.extname(f).toLowerCase());
const SWATCH_LABELS = { Fronts: 'Front', Handles: 'Handle', Worktops: 'Worktop', CarcaseColours: 'Carcase' };

// When true, catalog entries whose file disappears are removed automatically
// (never shown as "missing" in the UI). Set false to keep them flagged instead.
const AUTO_PRUNE_MISSING = true;

// Drop every item currently flagged missing. Returns how many were removed.
function pruneMissing(cat) {
  const before = cat.items.length;
  cat.items = cat.items.filter(i => !i.missing);
  return before - cat.items.length;
}

// ---------- catalog persistence ----------

function loadCatalog() {
  try {
    return JSON.parse(fs.readFileSync(CATALOG_PATH, 'utf8'));
  } catch {
    return { categories: [], items: [] };
  }
}

function saveCatalog(cat) {
  fs.writeFileSync(CATALOG_PATH, JSON.stringify(cat, null, 2));
}

function id() {
  return crypto.randomBytes(8).toString('hex');
}

function prettyName(file) {
  return file.replace(/\.(glb|fbx)$/i, '').replace(/_/g, ' ').trim();
}

function groupKey(file) {
  let base = file.replace(/\.[^.]+$/, '');
  for (const s of SUFFIXES) {
    if (base.toLowerCase().endsWith(s)) base = base.slice(0, -s.length);
  }
  return base.toLowerCase();
}

// Scan the 3D-models folder and merge any new files into the catalog.
// - new category folders are added
// - new .glb files become items
// - existing items are kept (preserving user edits)
// - items whose file no longer exists are flagged missing:true
function scan(cat) {
  // missing       = total items currently missing (for display)
  // newlyMissing  = items that just transitioned present -> missing
  // recovered     = items that just transitioned missing -> present
  const result = { added: 0, missing: 0, newlyMissing: 0, recovered: 0, categories: 0 };
  if (!fs.existsSync(MODELS_DIR)) return result;

  const folders = fs.readdirSync(MODELS_DIR, { withFileTypes: true })
    .filter(d => d.isDirectory())
    .map(d => d.name);
  const folderSet = new Set(folders);

  // mark an item missing, tracking the transition
  const markMissing = (it) => {
    if (!it.missing) result.newlyMissing++;
    it.missing = true;
    result.missing++;
  };

  for (const folder of folders) {
    if (SWATCH_ROOTS.includes(folder)) continue; // handled by the swatch pass below

    if (!cat.categories.find(c => c.name === folder)) {
      cat.categories.push({ id: id(), name: folder, createdAt: Date.now() });
      result.categories++;
    }

    const dir = path.join(MODELS_DIR, folder);
    const files = fs.readdirSync(dir);
    const models = files.filter(isModel);
    const gifs = files.filter(f => f.toLowerCase().endsWith('.gif'));
    const pngs = files.filter(f => f.toLowerCase().endsWith('.png'));

    for (const file of models) {
      const existing = cat.items.find(i => i.category === folder && i.file === file);
      const key = groupKey(file);
      const gif = gifs.find(g => groupKey(g) === key) || null;
      const png = pngs.find(p => groupKey(p) === key) || null;
      let bytes = 0;
      try { bytes = fs.statSync(path.join(dir, file)).size; } catch {}

      if (existing) {
        if (existing.missing) result.recovered++; // file came back
        existing.missing = false;
        existing.bytes = bytes;
        if (gif && !existing.gif) existing.gif = gif;
        if (png && !existing.thumb) existing.thumb = png;
      } else {
        cat.items.push({
          id: id(),
          category: folder,
          file,
          name: prettyName(file),
          description: '',
          status: 'active',
          gif,
          thumb: png,
          bytes,
          missing: false,
          createdAt: Date.now(),
        });
        result.added++;
      }
    }

    // flag items whose file disappeared from an existing folder
    // (image items — e.g. kitchens — have no model file; never prune them)
    for (const it of cat.items.filter(i => i.category === folder)) {
      if (!it.image && !models.includes(it.file)) markMissing(it);
    }
  }

  // Swatch roots: each subfolder under a root becomes its own image category.
  // Swatch items are image items (have .image), so the missing/prune guards above skip them.
  for (const root of SWATCH_ROOTS) {
    const rootDir = path.join(MODELS_DIR, root);
    if (!fs.existsSync(rootDir)) continue;
    const subs = fs.readdirSync(rootDir, { withFileTypes: true })
      .filter(d => d.isDirectory()).map(d => d.name);
    for (const sub of subs) {
      if (!cat.categories.find(c => c.name === sub)) {
        cat.categories.push({ id: id(), name: sub, createdAt: Date.now() });
        result.categories++;
      }
      const subDir = path.join(rootDir, sub);
      const label = SWATCH_LABELS[sub] || '';
      for (const file of fs.readdirSync(subDir).filter(isImage)) {
        const existing = cat.items.find(i => i.category === sub && i.file === file);
        const url = `/models/${root}/${sub}/${file}`;
        let bytes = 0;
        try { bytes = fs.statSync(path.join(subDir, file)).size; } catch {}
        if (existing) {
          existing.image = url; existing.bytes = bytes; existing.missing = false;
        } else {
          const code = file.replace(/\.[^.]+$/, '');
          cat.items.push({
            id: id(),
            category: sub,
            file,
            name: label ? `${label} ${code}` : code,
            description: '',
            status: 'active',
            image: url,
            bytes,
            missing: false,
            product: { code, elementType: sub },
            createdAt: Date.now(),
          });
          result.added++;
        }
      }
    }
  }

  // flag items whose entire category folder was removed
  for (const it of cat.items) {
    if (!it.image && !folderSet.has(it.category)) markMissing(it);
  }
  return result;
}

// ---------- http helpers ----------

function sendJSON(res, code, obj) {
  const body = JSON.stringify(obj);
  res.writeHead(code, { 'Content-Type': 'application/json', 'Content-Length': Buffer.byteLength(body) });
  res.end(body);
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    let data = '';
    req.on('data', c => { data += c; if (data.length > 5e6) req.destroy(); });
    req.on('end', () => { try { resolve(data ? JSON.parse(data) : {}); } catch (e) { reject(e); } });
    req.on('error', reject);
  });
}

const MIME = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.css': 'text/css',
  '.json': 'application/json', '.glb': 'model/gltf-binary', '.fbx': 'application/octet-stream',
  '.gif': 'image/gif', '.png': 'image/png', '.jpg': 'image/jpeg', '.svg': 'image/svg+xml',
  '.webp': 'image/webp',
};

function serveFile(res, filePath) {
  fs.stat(filePath, (err, stat) => {
    if (err || !stat.isFile()) { res.writeHead(404); return res.end('Not found'); }
    const ext = path.extname(filePath).toLowerCase();
    // never cache the UI assets so edits always show up; large model files may cache
    const noStore = ['.html', '.js', '.css', '.json'].includes(ext);
    res.writeHead(200, {
      'Content-Type': MIME[ext] || 'application/octet-stream',
      'Content-Length': stat.size,
      'Cache-Control': noStore ? 'no-store, must-revalidate' : 'no-cache',
    });
    fs.createReadStream(filePath).pipe(res);
  });
}

// Resolve a path safely under a base dir (block traversal)
function safeJoin(base, ...parts) {
  const p = path.resolve(base, ...parts);
  if (p !== base && !p.startsWith(base + path.sep)) return null;
  return p;
}

// ---------- live dashboard (favorites.json -> Homepage cards) ----------
// Mirrors Homepage/sync_dashboard_from_favorites.py, but emits image src as
// /models/... URLs and lets the browser pick object-fit from natural size.

const CAT_SINGULAR = {
  Dishwashers: 'Dishwasher', Fridges: 'Fridge', Cooktops: 'Cooktop', Hoods: 'Hood',
  Microwaves: 'Microwave', CoffeeMachines: 'Coffee Machine', Kitchens: 'Kitchen',
};
const DASH_LABELS = ['interior', 'detail', 'context', 'controls'];
const withUnit = (v, u) => (v === null || v === undefined || v === '') ? null : `${v} ${u}`;

function buildProductData(category, p) {
  const rows = [['Model', p.name], ['Part No.', p.sku]];
  if (category === 'Fridges')
    rows.push(['Energy class', p.energyClass], ['Fridge capacity', withUnit(p.fridgeCapacity, 'L')],
      ['Freezer capacity', withUnit(p.freezerCapacity, 'L')], ['Annual energy use', withUnit(p.annualEnergy, 'kWh')],
      ['Noise level', withUnit(p.noise, 'dB')]);
  else if (category === 'Dishwashers')
    rows.push(['Energy class', p.energyClass], ['Place settings', p.placeSettings],
      ['Water per cycle', withUnit(p.waterPerCycle, 'L')], ['Noise class', p.noiseClass]);
  else if (category === 'Cooktops')
    rows.push(['Zones', p.zones], ['Induction', p.induction ? 'Yes' : null], ['Total power', withUnit(p.totalPowerKw, 'kW')]);
  else if (category === 'Hoods')
    rows.push(['Energy class', p.energyClass], ['Airflow', withUnit(p.airflow, 'm³/h')],
      ['Noise', withUnit(p.noise, 'dB')], ['Annual energy', withUnit(p.annualEnergy, 'kWh')]);
  else if (category === 'Microwaves')
    rows.push(['Capacity', withUnit(p.capacityL, 'L')], ['Power', withUnit(p.microwavePowerW, 'W')]);
  rows.push(['Dimensions', p.dimensions], ['Colour', p.color]);
  const o = {};
  for (const [k, v] of rows) if (v !== null && v !== undefined && v !== '') o[k] = String(v);
  return o;
}

// Dependency-free pixel-size reader for webp / png / jpeg. Returns {w,h} or null.
function imageSize(file) {
  let fd;
  try {
    fd = fs.openSync(file, 'r');
    const buf = Buffer.alloc(64);
    fs.readSync(fd, buf, 0, 64, 0);
    // PNG
    if (buf.slice(0, 8).toString('hex') === '89504e470d0a1a0a')
      return { w: buf.readUInt32BE(16), h: buf.readUInt32BE(20) };
    // WebP (RIFF....WEBP)
    if (buf.slice(0, 4).toString('ascii') === 'RIFF' && buf.slice(8, 12).toString('ascii') === 'WEBP') {
      const chunk = buf.slice(12, 16).toString('ascii');
      if (chunk === 'VP8 ') return { w: buf.readUInt16LE(26) & 0x3fff, h: buf.readUInt16LE(28) & 0x3fff };
      if (chunk === 'VP8L') {
        const b = buf.slice(21, 25);
        const bits = b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
        return { w: (bits & 0x3fff) + 1, h: ((bits >> 14) & 0x3fff) + 1 };
      }
      if (chunk === 'VP8X')
        return { w: 1 + (buf[24] | buf[25] << 8 | buf[26] << 16), h: 1 + (buf[27] | buf[28] << 8 | buf[29] << 16) };
    }
    // JPEG: scan SOF markers
    if (buf[0] === 0xff && buf[1] === 0xd8) {
      const stat = fs.statSync(file);
      const all = Buffer.alloc(Math.min(stat.size, 1 << 20));
      fs.readSync(fd, all, 0, all.length, 0);
      let o = 2;
      while (o + 9 < all.length) {
        if (all[o] !== 0xff) { o++; continue; }
        const m = all[o + 1];
        if (m >= 0xc0 && m <= 0xcf && m !== 0xc4 && m !== 0xc8 && m !== 0xcc)
          return { h: all.readUInt16BE(o + 5), w: all.readUInt16BE(o + 7) };
        o += 2 + all.readUInt16BE(o + 2);
      }
    }
  } catch { /* fall through */ }
  finally { if (fd !== undefined) fs.closeSync(fd); }
  return null;
}

// Square gallery frame -> a landscape image must "cover" (fill height, crop sides)
// so it never shows white bars top/bottom; a portrait image "contain"s (full height,
// side padding). Falls back to "cover" if dimensions can't be read.
function fitFor(file) {
  const s = imageSize(file);
  if (!s || !s.h) return 'cover';
  return s.w > s.h ? 'cover' : 'contain';
}

function listDashImages(category, folder) {
  const baseDisk = path.join(MODELS_DIR, category, folder);
  const heroDisk = path.join(baseDisk, 'hero.webp');
  if (!fs.existsSync(heroDisk)) return [];
  const baseUrl = `/models/${encodeURIComponent(category)}/${encodeURIComponent(folder)}`;
  const imgs = [{ src: `${baseUrl}/hero.webp`, label: 'front', fit: fitFor(heroDisk) }];
  const gdir = path.join(baseDisk, 'gallery');
  if (fs.existsSync(gdir)) {
    const gal = fs.readdirSync(gdir).filter(f => f.endsWith('.webp'))
      .sort((a, b) => (+(a.match(/g(\d+)/) || [0, 0])[1]) - (+(b.match(/g(\d+)/) || [0, 0])[1]));
    gal.slice(0, 4).forEach((g, i) =>
      imgs.push({ src: `${baseUrl}/gallery/${g}`, label: DASH_LABELS[i] || `view ${i + 2}`, fit: fitFor(path.join(gdir, g)) }));
  }
  return imgs;
}

function buildDashboard() {
  let favs = [];
  try { favs = JSON.parse(fs.readFileSync(FAVS_PATH, 'utf8')).favorites || []; } catch {}
  const cat = loadCatalog();
  const byId = {};
  for (const it of cat.items) byId[it.id] = it;
  const out = [];
  for (const fav of favs) {
    const it = byId[fav.id];
    if (!it) continue;
    const p = it.product || {};
    const folder = fav.modelKey || (it.file || '').replace(/\.glb$/i, '');
    const imgs = listDashImages(it.category, folder);
    const rs = (p.reviews || []).map(r => r.rating);
    const rating = rs.length ? Math.round(rs.reduce((a, b) => a + b, 0) / rs.length * 2) / 2 : 4.5;
    const e = {
      id: 'fav-' + fav.id.slice(0, 10), name: p.name, series: p.color || '', variant: p.color || '',
      category: CAT_SINGULAR[it.category] || it.category, manufacturer: p.brand || 'Miele',
      mieleUrl: p.productPageUrl || '', price: p.price, priceNote: 'incl. VAT', savedOn: '2026-06-23T15:30:00Z',
      rating, reviewCount: p.reviewCount || rs.length, fromCatalog: fav.id, modelKey: folder,
      swatch: 'linear-gradient(120deg, #DCE0E6 0%, #C7CCD2 50%, #AAB0BA 100%)',
      delivery: ['Plus delivery costs', 'Delivery within 5–7 working days', 'Old appliance collection & disposal free of charge'],
      blurb: `${p.subtitle || ''}. ${p.headline || ''}.`.replace(/^\.\s*/, '').trim(),
      features: (p.features || []).slice(0, 5).map(f => ({ name: f, desc: '' })),
      productData: buildProductData(it.category, p),
      reviews: (p.reviews || []).map(r => ({ stars: r.rating, date: r.date, author: r.author, location: 'Reviewed in ' + r.country, text: r.body })),
      images: imgs, gallery: [],
    };
    if (imgs[0]) e.thumb = imgs[0].src;
    out.push(e);
  }
  return out;
}

// Injected into the served Homepage: load live products before the bundle's
// data.js runs, fix each gallery image's object-fit from its natural size
// (square frame -> always fill vertical), and reload when the data changes.
const DASHBOARD_INJECT = `<script>(function(){
  try{var x=new XMLHttpRequest();x.open('GET','/api/dashboard.json',false);x.send();
    if(x.status===200)window.__LIVE_PRODUCTS=JSON.parse(x.responseText);}catch(e){console.error('[live] dashboard fetch failed',e);}
  function fixFit(img){if(!img.naturalWidth)return;var cover=img.naturalWidth>img.naturalHeight;
    img.style.objectFit=cover?'cover':'contain';
    var g=img.closest&&img.closest('.rr-gal');if(g)g.style.background=cover?'#E4E7EE':'#F3F4F8';}
  function scan(){document.querySelectorAll('img.rr-gal-img').forEach(function(im){
    if(im.complete)fixFit(im);else im.addEventListener('load',function(){fixFit(im);},{once:true});});}
  document.addEventListener('DOMContentLoaded',scan);
  new MutationObserver(scan).observe(document.documentElement,{childList:true,subtree:true});
  var last=null;setInterval(function(){fetch('/api/version').then(function(r){return r.json();}).then(function(v){
    var sig=v.rev+'|'+v.fav;if(last!==null&&last!==sig)location.reload();last=sig;}).catch(function(){});},1500);
})();</script>`;

// ---------- routes ----------

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, `http://${req.headers.host}`);
  const pathname = decodeURIComponent(url.pathname);
  const method = req.method;

  try {
    // --- live Homepage dashboard, served same-origin so it can fetch data + images ---
    if (pathname === '/dashboard' || pathname === '/dashboard/') {
      let html;
      try { html = fs.readFileSync(HOMEPAGE_PATH, 'utf8'); }
      catch { res.writeHead(404); return res.end('Homepage bundle not found at ' + HOMEPAGE_PATH); }
      // Run our boot script first so window.__LIVE_PRODUCTS is set before data.js.
      html = html.replace('<head>', '<head>' + DASHBOARD_INJECT);
      res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-store' });
      return res.end(html);
    }

    // --- static model/preview files: /models/<Category>/<file> ---
    if (pathname.startsWith('/models/')) {
      const rel = pathname.slice('/models/'.length);
      const fp = safeJoin(MODELS_DIR, rel);
      if (!fp) { res.writeHead(403); return res.end('Forbidden'); }
      return serveFile(res, fp);
    }

    // --- API ---
    if (pathname.startsWith('/api/')) {
      const cat = loadCatalog();

      if (pathname === '/api/catalog' && method === 'GET') {
        return sendJSON(res, 200, cat);
      }

      // live dashboard data built from favorites.json + catalog.json
      if (pathname === '/api/dashboard.json' && method === 'GET') {
        return sendJSON(res, 200, buildDashboard());
      }

      // cheap change-detection signal for the client to poll
      if (pathname === '/api/version' && method === 'GET') {
        let rev = 0, ui = 0, fav = 0;
        try { rev = fs.statSync(CATALOG_PATH).mtimeMs; } catch {}
        // app.js mtime → clients auto-reload the page when the admin code changes
        try { ui = fs.statSync(path.join(PUBLIC_DIR, 'app.js')).mtimeMs; } catch {}
        // favorites.json mtime → the /dashboard page reloads when favorites change
        try { fav = fs.statSync(FAVS_PATH).mtimeMs; } catch {}
        return sendJSON(res, 200, { rev, ui, fav, items: cat.items.length, categories: cat.categories.length });
      }

      if (pathname === '/api/scan' && method === 'POST') {
        const r = scan(cat);
        r.pruned = AUTO_PRUNE_MISSING ? pruneMissing(cat) : 0;
        saveCatalog(cat);
        return sendJSON(res, 200, { ...r, catalog: cat });
      }

      // catalog-only cleanup of entries whose file is gone. Optional ?category= to scope.
      if (pathname === '/api/prune-missing' && method === 'POST') {
        const b = await readBody(req);
        const scope = b.category || null;
        const before = cat.items.length;
        cat.items = cat.items.filter(i => !(i.missing && (!scope || i.category === scope)));
        const removed = before - cat.items.length;
        saveCatalog(cat);
        return sendJSON(res, 200, { removed });
      }

      // categories
      if (pathname === '/api/categories' && method === 'POST') {
        const b = await readBody(req);
        const name = (b.name || '').trim();
        if (!name) return sendJSON(res, 400, { error: 'name required' });
        if (cat.categories.find(c => c.name.toLowerCase() === name.toLowerCase()))
          return sendJSON(res, 409, { error: 'category exists' });
        const c = { id: id(), name, createdAt: Date.now() };
        cat.categories.push(c);
        // create the folder on disk too
        try { fs.mkdirSync(path.join(MODELS_DIR, name), { recursive: true }); } catch {}
        saveCatalog(cat);
        return sendJSON(res, 201, c);
      }

      const catMatch = pathname.match(/^\/api\/categories\/([^/]+)$/);
      if (catMatch) {
        const c = cat.categories.find(x => x.id === catMatch[1]);
        if (!c) return sendJSON(res, 404, { error: 'not found' });
        if (method === 'PUT') {
          const b = await readBody(req);
          if (b.name) c.name = b.name.trim();
          saveCatalog(cat);
          return sendJSON(res, 200, c);
        }
        if (method === 'DELETE') {
          // catalog-only: remove category + its items from catalog (files untouched)
          cat.categories = cat.categories.filter(x => x.id !== c.id);
          cat.items = cat.items.filter(i => i.category !== c.name);
          saveCatalog(cat);
          return sendJSON(res, 200, { ok: true });
        }
      }

      // items
      if (pathname === '/api/items' && method === 'POST') {
        const b = await readBody(req);
        const item = {
          id: id(),
          category: b.category || '',
          file: b.file || '',
          name: (b.name || '').trim() || 'Untitled',
          description: b.description || '',
          status: b.status || 'active',
          gif: null, thumb: null, bytes: 0,
          // image items (kitchens) carry a hero image instead of a 3D model file
          image: b.image || null,
          missing: !b.file && !b.image,
          product: (b.product && typeof b.product === 'object') ? b.product : {},
          createdAt: Date.now(),
        };
        cat.items.push(item);
        saveCatalog(cat);
        return sendJSON(res, 201, item);
      }

      const itemMatch = pathname.match(/^\/api\/items\/([^/]+)(\/file)?$/);
      if (itemMatch) {
        const it = cat.items.find(x => x.id === itemMatch[1]);
        if (!it) return sendJSON(res, 404, { error: 'not found' });
        const fileScope = !!itemMatch[2];

        if (method === 'PUT') {
          const b = await readBody(req);
          for (const k of ['name', 'description', 'status', 'category', 'image']) {
            if (k in b) it[k] = typeof b[k] === 'string' ? b[k] : it[k];
          }
          // product-data fields live in a nested object so they never collide
          // with the model-file metadata above. Merge so partial saves work.
          if (b.product && typeof b.product === 'object') {
            it.product = { ...(it.product || {}), ...b.product };
          }
          saveCatalog(cat);
          return sendJSON(res, 200, it);
        }

        if (method === 'DELETE' && !fileScope) {
          // catalog-only delete (safe) — leaves the .glb on disk
          cat.items = cat.items.filter(x => x.id !== it.id);
          saveCatalog(cat);
          return sendJSON(res, 200, { ok: true, fileKept: true });
        }

        if (method === 'DELETE' && fileScope) {
          // hard delete — remove the actual file from disk, then the catalog entry
          const fp = it.file ? safeJoin(MODELS_DIR, it.category, it.file) : null;
          let fileDeleted = false;
          if (fp && fs.existsSync(fp)) {
            try { fs.unlinkSync(fp); fileDeleted = true; }
            catch (e) { return sendJSON(res, 500, { error: 'could not delete file: ' + e.message }); }
          }
          cat.items = cat.items.filter(x => x.id !== it.id);
          saveCatalog(cat);
          return sendJSON(res, 200, { ok: true, fileDeleted });
        }
      }

      return sendJSON(res, 404, { error: 'unknown endpoint' });
    }

    // --- static UI ---
    let rel = pathname === '/' ? '/index.html' : pathname;
    const fp = safeJoin(PUBLIC_DIR, '.' + rel);
    if (!fp) { res.writeHead(403); return res.end('Forbidden'); }
    return serveFile(res, fp);
  } catch (e) {
    sendJSON(res, 500, { error: e.message });
  }
});

// Always scan on startup so anything added while the server was off is picked up.
(function init() {
  const cat = loadCatalog();
  const r = scan(cat);
  const pruned = AUTO_PRUNE_MISSING ? pruneMissing(cat) : 0;
  if (r.added || r.categories || r.newlyMissing || r.recovered || pruned) {
    saveCatalog(cat);
    console.log(`Startup scan: +${r.categories} categories, +${r.added} models, ${pruned} removed (missing).`);
  }
})();

// Watch the models folder and auto-merge new files/folders as they appear.
// Debounced because a single file copy can fire many fs events.
let watchTimer = null;
function autoScan() {
  clearTimeout(watchTimer);
  watchTimer = setTimeout(() => {
    try {
      const cat = loadCatalog();
      const r = scan(cat);
      const pruned = AUTO_PRUNE_MISSING ? pruneMissing(cat) : 0;
      if (r.added || r.categories || r.newlyMissing || r.recovered || pruned) {
        saveCatalog(cat); // bumps catalog.json mtime → clients notice via /api/version
        console.log(`Auto-scan: +${r.added} added, ${pruned} removed (missing), ${r.recovered} recovered.`);
      }
    } catch (e) { console.error('auto-scan failed:', e.message); }
  }, 800);
}
try {
  fs.watch(MODELS_DIR, { recursive: true }, () => autoScan());
  console.log('Watching 3D-models folder for new files…');
} catch (e) {
  console.error('Could not watch models folder (manual Rescan still works):', e.message);
}

server.listen(PORT, () => {
  console.log(`\n  3D Model Admin running:  http://localhost:${PORT}`);
  console.log(`  Models folder:           ${MODELS_DIR}`);
  console.log(`  Catalog file:            ${CATALOG_PATH}\n`);
});
