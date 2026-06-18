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

const MODEL_EXTS = ['.glb', '.fbx']; // .glb renders live; .fbx is catalogued (no in-browser 3D preview)
const isModel = (f) => MODEL_EXTS.includes(path.extname(f).toLowerCase());
const SUFFIXES = ['_realsize', '_cinematic', '_front', '_q3']; // stripped to compute a grouping key

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
    for (const it of cat.items.filter(i => i.category === folder)) {
      if (!models.includes(it.file)) markMissing(it);
    }
  }

  // flag items whose entire category folder was removed
  for (const it of cat.items) {
    if (!folderSet.has(it.category)) markMissing(it);
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

// ---------- routes ----------

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, `http://${req.headers.host}`);
  const pathname = decodeURIComponent(url.pathname);
  const method = req.method;

  try {
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

      // cheap change-detection signal for the client to poll
      if (pathname === '/api/version' && method === 'GET') {
        let rev = 0;
        try { rev = fs.statSync(CATALOG_PATH).mtimeMs; } catch {}
        return sendJSON(res, 200, { rev, items: cat.items.length, categories: cat.categories.length });
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
          missing: !b.file,
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
          for (const k of ['name', 'description', 'status', 'category']) {
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
