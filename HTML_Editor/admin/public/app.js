// 3D Model Admin — front-end
const $ = (s, el = document) => el.querySelector(s);
const $$ = (s, el = document) => [...el.querySelectorAll(s)];

let state = { categories: [], items: [], activeCat: null, search: '', status: '' };

const api = {
  async get() { return (await fetch('/api/catalog')).json(); },
  async scan() { return (await fetch('/api/scan', { method: 'POST' })).json(); },
  async addCat(name) { return (await fetch('/api/categories', { method: 'POST', headers: J, body: JSON.stringify({ name }) })).json(); },
  async renameCat(id, name) { return (await fetch('/api/categories/' + id, { method: 'PUT', headers: J, body: JSON.stringify({ name }) })).json(); },
  async delCat(id) { return (await fetch('/api/categories/' + id, { method: 'DELETE' })).json(); },
  async addItem(d) { return (await fetch('/api/items', { method: 'POST', headers: J, body: JSON.stringify(d) })).json(); },
  async updItem(id, d) { return (await fetch('/api/items/' + id, { method: 'PUT', headers: J, body: JSON.stringify(d) })).json(); },
  async delItem(id) { return (await fetch('/api/items/' + id, { method: 'DELETE' })).json(); },
  async delItemFile(id) { return (await fetch('/api/items/' + id + '/file', { method: 'DELETE' })).json(); },
  async pruneMissing(category) { return (await fetch('/api/prune-missing', { method: 'POST', headers: J, body: JSON.stringify({ category: category || null }) })).json(); },
};
const J = { 'Content-Type': 'application/json' };

function toast(msg) {
  const t = document.createElement('div');
  t.className = 'toast'; t.textContent = msg;
  document.body.appendChild(t);
  setTimeout(() => t.remove(), 2600);
}

let lastRev = null, lastUi = null;
async function getVer() { try { return await (await fetch('/api/version')).json(); } catch { return null; } }

async function load() {
  state = { ...state, ...(await api.get()) };
  if (state.activeCat && !state.categories.find(c => c.name === state.activeCat)) state.activeCat = null;
  if (!state.activeCat && state.categories[0]) state.activeCat = state.categories[0].name;
  render();
  const v = await getVer(); // resync so our own edits don't trigger a "changed" toast
  if (v) { lastRev = v.rev; if (lastUi === null) lastUi = v.ui; }
}

// Live auto-refresh: the server watches the folder and auto-merges new models;
// here we just notice the catalog changed and re-render. Skips while a dialog is open.
async function poll() {
  if ($('#modalRoot').children.length) return;   // a dialog is open — don't disturb edits
  const v = await getVer();
  if (!v) return;
  if (lastUi !== null && v.ui !== lastUi) { location.reload(); return; } // admin code changed → reload for fresh UI
  lastUi = v.ui;
  const rev = v.rev;
  if (lastRev !== null && rev !== lastRev) {
    const before = state.items.length;
    const missBefore = state.items.filter(i => i.missing).length;
    await load();
    const diff = state.items.length - before;
    const missDiff = state.items.filter(i => i.missing).length - missBefore;
    if (missDiff > 0) toast(`⚠ ${missDiff} file${missDiff > 1 ? 's' : ''} went missing`);
    else if (missDiff < 0) toast(`✓ ${-missDiff} file${missDiff < -1 ? 's' : ''} restored`);
    else if (diff > 0) toast(`+${diff} new model${diff > 1 ? 's' : ''} detected`);
    else toast('Catalog updated');
  }
}
setInterval(poll, 4000);

function render() { renderCats(); renderGrid(); renderMissingBtn(); }

function renderMissingBtn() {
  const btn = $('#missingBtn');
  const n = state.items.filter(i => i.missing).length;
  if (n) { btn.style.display = ''; btn.textContent = `⚠ ${n} missing`; }
  else btn.style.display = 'none';
}

function renderCats() {
  const list = $('#catList');
  list.innerHTML = '';
  state.categories.forEach(c => {
    const inCat = state.items.filter(i => i.category === c.name);
    const count = inCat.length;
    const missing = inCat.filter(i => i.missing).length;
    const div = document.createElement('div');
    div.className = 'cat' + (state.activeCat === c.name ? ' active' : '');
    div.innerHTML = `<span class="name">${esc(c.name)}</span>
      <span class="edit" title="Rename / delete">✎</span>
      ${missing ? `<span class="catwarn" title="${missing} file${missing > 1 ? 's' : ''} missing">⚠ ${missing}</span>` : ''}
      <span class="count">${count}</span>`;
    div.onclick = () => { state.activeCat = c.name; render(); };
    div.querySelector('.edit').onclick = (e) => { e.stopPropagation(); editCategory(c); };
    list.appendChild(div);
  });
  if (!state.categories.length) list.innerHTML = '<div class="meta" style="color:var(--mut)">No categories yet.</div>';
}

function renderGrid() {
  const grid = $('#grid');
  let items = state.items.filter(i => i.category === state.activeCat);
  if (state.search) {
    const q = state.search.toLowerCase();
    items = items.filter(i => (i.name + ' ' + (i.product?.name || '')).toLowerCase().includes(q));
  }
  if (state.status === '__missing__') items = items.filter(i => i.missing);
  else if (state.status) items = items.filter(i => i.status === state.status);

  if (!items.length) {
    grid.innerHTML = `<div class="empty">No models here.<br><br>
      <button class="primary" onclick="document.getElementById('addItemBtn').click()">+ Add a model</button></div>`;
    return;
  }
  grid.innerHTML = '';
  items.forEach(it => grid.appendChild(card(it)));
}

function card(it) {
  const el = document.createElement('div');
  el.className = 'card' + (it.missing ? ' missing' : '');
  const src = it.file ? `/models/${encodeURIComponent(it.category)}/${encodeURIComponent(it.file)}` : '';
  const ext = (it.file.split('.').pop() || '').toLowerCase();
  const canPreview = ext === 'glb' || ext === 'gltf';
  let preview;
  if (it.image) {
    // image items (e.g. kitchens) have no 3D model — the hero photo IS the preview
    const isrc = assetUrl(it, it.image);
    preview = `<div class="preview imageview">
      <img src="${isrc}" loading="lazy" alt="${esc(it.name || '')}"
        style="width:100%;height:100%;object-fit:cover;display:block">
    </div>`;
  } else if (it.missing || !it.file) {
    preview = `<div class="preview noview missingview">
      <div class="fileicon">⚠<span>FILE MISSING</span><small>${esc(it.file || 'no file')}</small></div>
    </div>`;
  } else if (canPreview) {
    preview = `<div class="preview">
      <span class="badge">${fmtBytes(it.bytes)}</span>
      <model-viewer src="${src}" camera-controls auto-rotate disable-zoom interaction-prompt="none"
        camera-orbit="35deg 75deg auto" loading="lazy" reveal="auto"></model-viewer>
    </div>`;
  } else {
    // .fbx and other formats can't render in-browser — show a placeholder
    preview = `<div class="preview noview">
      <span class="badge">${fmtBytes(it.bytes)}</span>
      <div class="fileicon">📦<span>${esc(ext.toUpperCase())}</span><small>no live preview</small></div>
    </div>`;
  }
  const p = it.product || {};
  const hasP = !!(it.product && Object.keys(p).length);

  const price = p.price != null ? `<span class="price-tag">${fmtPrice(p.price, p.currency)}</span>`
    : (p.priceLabel ? `<span class="price-tag soft">${esc(p.priceLabel)}</span>` : '');
  const energy = p.energyClass ? `<span class="energy energy-${esc((p.energyClass[0] || '').toUpperCase())}">${esc(p.energyClass)}</span>` : '';
  const swatch = p.swatchColor ? `<span class="swatch" style="background:${esc(p.swatchColor)}"></span>` : '';
  const subText = p.subtitle || (it.image ? [p.color, p.productType].filter(Boolean).join(' · ') : '');
  const sub = subText ? `<div class="meta">${esc(subText)}</div>` : '';
  const finish = (!it.image && (p.color || p.dimensions))
    ? `<div class="meta">${swatch}${esc([p.color, p.dimensions].filter(Boolean).join(' · '))}</div>` : '';

  // Schema-agnostic spec row: render whatever key specs the item actually has.
  // Curated display rules for known fields; fields not listed here are skipped on the card
  // (they still appear in the full Details modal). Internal fields (_specSource etc.) are never shown.
  const SPEC_FIELDS = [
    { key: 'fridgeCapacity',  fmt: v => `<b>${v} L</b> fridge` },
    { key: 'freezerCapacity', fmt: v => `<b>${v} L</b> freezer` },
    { key: 'noise',           fmt: v => `<b>${v}</b> dB` },
    { key: 'annualEnergy',    fmt: v => `<b>${v}</b> kWh/yr` },
    { key: 'waterTank',       fmt: v => `<b>${esc(String(v))}</b> water` },
    // Hoods
    { key: 'airflow',         fmt: v => `<b>${v}</b> m³/h` },
    // Cooktops
    { key: 'zones',           fmt: v => `<b>${v}</b> zones` },
    { key: 'totalPowerKw',    fmt: v => `<b>${v} kW</b> total` },
    { key: 'boosterKw',       fmt: v => `<b>${v} kW</b> boost` },
    { key: 'induction',       fmt: v => v ? `<b>induction</b>` : null },
    // Microwaves
    { key: 'capacityL',       fmt: v => `<b>${v} L</b>` },
    { key: 'microwavePowerW', fmt: v => `<b>${v} W</b>` },
    { key: 'grill',           fmt: v => v ? `<b>grill</b>` : null },
    { key: 'grillPowerW',     fmt: v => `<b>${v} W</b> grill` },
    { key: 'turntableCm',     fmt: v => `<b>Ø ${v} cm</b>` },
    { key: 'weightKg',        fmt: v => `<b>${v} kg</b>` },
    { key: 'beanContainer',   fmt: v => `<b>${esc(String(v))}</b> beans` },
    { key: 'power',           fmt: v => `<b>${esc(String(v))}</b> power` },
    { key: 'energyClass',     fmt: v => `energy <b>${esc(String(v))}</b>` },
    { key: 'dimensions',      fmt: v => `<span>${esc(String(v))}</span>` },
  ];
  const specs = [];
  for (const { key, fmt } of SPEC_FIELDS) {
    if (p[key] != null && specs.length < 4) { const h = fmt(p[key]); if (h) specs.push(`<span>${h}</span>`); }
  }
  const specsHTML = specs.length ? `<div class="specs">${specs.join('')}</div>` : '';

  const feats = Array.isArray(p.features) ? p.features.slice(0, 6) : [];
  const featHTML = feats.length ? `<div class="chips">${feats.map(f => `<span class="chip">${esc(f)}</span>`).join('')}</div>` : '';

  const blurb = p.headline || p.emotionalLine || it.description;

  // compact review badge on the card: half-star visual + score + count
  const rScore = p.reviewScore != null ? Number(p.reviewScore) : null;
  const rStars = p.reviewStars != null ? Number(p.reviewStars) : (rScore != null ? Math.round(rScore * 2) / 2 : 0);
  const reviewBadge = p.reviewCount ? `<div class="card-rev">${starbar(rStars)}<b>${(rScore != null ? rScore : 0).toFixed(1)}</b><span class="card-revn">(${p.reviewCount})</span></div>` : '';

  el.innerHTML = `
    ${preview}
    <div class="body">
      ${it.missing ? `<div class="missing-banner">⚠ File no longer on disk — <code>3D-models/${esc(it.category)}/${esc(it.file || '—')}</code></div>` : ''}
      <div class="titlerow"><div class="name">${esc(p.name || it.name)}</div>${price}</div>
      ${sub}
      <div class="meta"><span class="status-dot s-${it.status}"></span>${it.status}${p.brand ? ` · ${esc(p.brand)}` : ''}${energy}</div>
      ${reviewBadge}
      ${finish}
      ${specsHTML}
      ${featHTML}
      ${blurb ? `<div class="desc">${esc(blurb)}</div>` : ''}
    </div>
    <div class="actions">
      ${hasP ? `<button class="details">⛶ Details</button>` : ''}
      <button class="edit">✎ Edit</button>
      <button class="danger del">🗑</button>
    </div>`;
  if (hasP) el.querySelector('.details').onclick = () => productDetail(it);
  el.querySelector('.edit').onclick = () => editItem(it);
  el.querySelector('.del').onclick = () => deleteItem(it);
  return el;
}

// Resolve a stored image reference to a loadable URL. Accepts an absolute LOCAL path
// (e.g. C:\...\3D-models\Kitchens\TOUCH_337\hero.webp), a category-relative path, a
// root path, or an http(s) URL. Absolute local paths are served via the /models route.
function assetUrl(it, val) {
  if (!val) return '';
  const v = String(val).replace(/\\/g, '/');
  if (/^https?:/i.test(v)) return v;
  const enc = s => s.split('/').filter(Boolean).map(encodeURIComponent).join('/');
  const i = v.toLowerCase().indexOf('3d-models/');
  if (i >= 0) return '/models/' + enc(v.slice(i + 10));   // ' 3d-models/' length is 10
  if (v.startsWith('/')) return v;
  return '/models/' + enc(it.category + '/' + v);          // category-relative fallback
}

// Star bar with fractional fill — value 0..5 (nearest half), gold fill over grey stars.
function starbar(value) {
  const pct = Math.max(0, Math.min(100, (Number(value) || 0) / 5 * 100));
  return `<span class="starbar" title="${(Number(value) || 0)} / 5"><span class="sb-bg">★★★★★</span><span class="sb-fg" style="width:${pct}%">★★★★★</span></span>`;
}

// Shared reviews block: overall score (stored float) + half-star visual + count, then each
// review (stars, author, country · date, body). Used by both kitchens and appliances.
function reviewsHTML(p) {
  const reviews = Array.isArray(p.reviews) ? p.reviews : [];
  if (!reviews.length) return '';
  const half = n => Math.round((Number(n) || 0) * 2) / 2;
  const count = p.reviewCount != null ? p.reviewCount : reviews.length;
  const score = p.reviewScore != null ? Number(p.reviewScore)
    : Math.round(reviews.reduce((s, r) => s + (Number(r.rating) || 0), 0) / reviews.length * 10) / 10;
  const aggStars = p.reviewStars != null ? Number(p.reviewStars) : half(score);
  const head = `<div class="kp-revtop"><div class="kp-revscore">${score.toFixed(1)}</div>
      <div>${starbar(aggStars)}<div class="kp-revcount">${count} review${count === 1 ? '' : 's'}</div></div></div>`;
  return `${head}<div class="kp-revs">${reviews.map(r => `
    <div class="kp-rev"><div class="kp-revhead">${starbar(half(r.rating))}
      <b class="kp-revname">${esc(r.author || 'Anonymous')}</b>
      <span class="kp-revmeta">Reviewed in ${esc(r.country || '—')} · ${esc(r.date || '')}</span></div>
      <div class="kp-revbody">${esc(r.body || '')}</div></div>`).join('')}</div>`;
}

// Rich panel for image-based kitchen items: hero, the 4 elements, gallery, XR before/after,
// the flag note, and reviews. Keeps all the saved kitchen data viewable in the admin.
function kitchenPanel(it, p) {
  const url = f => assetUrl(it, f);
  const hero = it.image ? `<img class="kp-hero" src="${url(it.image)}" alt="">` : '';
  const els = [['Front', p.front, p.frontImage], ['Carcase', p.carcase, p.carcaseImage],
              ['Worktop', p.worktop, p.worktopImage], ['Handle', p.handle, p.handleImage]]
    .filter(([, v, img]) => v || img)
    .map(([l, v, img]) => `<div class="kp-el">
      <div class="kp-sw">${img ? `<img src="${url(img)}" alt="">` : ''}</div>
      <div><b>${esc(l)}</b><div class="kp-elv">${esc(v || '—')}</div></div></div>`).join('');
  const adds = Array.isArray(p.additionalImages) && p.additionalImages.length
    ? `<div class="kp-strip">${p.additionalImages.map(g => `<img src="${url(g)}" alt="">`).join('')}</div>` : '';
  const ba = (p.beforeImage || p.afterImage) ? `<div class="kp-ba">
      <figure><img src="${url(p.beforeImage || '')}" alt=""><figcaption>Before</figcaption></figure>
      <figure><img src="${url(p.afterImage || '')}" alt=""><figcaption>With ${esc(p.name || it.name)}</figcaption></figure>
    </div>` : '';
  const flagNote = (p.flag && typeof p.flag === 'object') ? p.flag.note : p.flag;
  const flag = flagNote ? `<div class="kp-flag">🚩 ${esc(flagNote)}</div>` : '';
  const revs = reviewsHTML(p);
  const sec = (t, h) => h ? `<div class="kp-sec">${t}</div>${h}` : '';
  return `<div class="kpanel">
    ${hero}
    ${sec('The four elements', els ? `<div class="kp-els">${els}</div>` : '')}
    ${sec('Additional images', adds)}
    ${sec('XR before / after', ba)}
    ${sec('Flagged', flag)}
    ${sec('Reviews', revs)}
  </div>`;
}

// Clean detail view for image-based kitchen items — shows ONLY the agreed kitchen data points,
// never the appliance/model rows (model file, SKU, file size, energy, etc.).
function kitchenDetail(it, p) {
  const rows = [];
  const row = (k, v) => { if (v == null || v === '') return; rows.push(`<tr><td>${esc(k)}</td><td>${v}</td></tr>`); };
  row('Brand', esc(p.brand));
  row('Name', esc(p.name || it.name));
  row('Colour', esc(p.color));
  row('Product type', esc(p.productType));
  row('Kitchen type', esc(p.kitchenType));
  row('Headline', esc(p.headline));
  row('Description', esc(p.description || it.description));
  row('Price', esc(p.priceLabel || (p.price != null ? fmtPrice(p.price, p.currency) : '')));
  row('Product page', p.productPageUrl ? `<a class="linkbtn" href="${esc(p.productPageUrl)}" target="_blank" rel="noopener">Open page ↗</a>` : '');
  modal({
    wide: true,
    title: esc(p.name || it.name),
    bodyHTML: `${kitchenPanel(it, p)}<table class="detail-tbl"><tbody>${rows.join('')}</tbody></table>`,
    footHTML: `<button class="ghost" data-close>Close</button><button class="primary" id="dv_edit">✎ Edit</button>`,
    onMount(root, close) {
      root.querySelector('[data-close]').onclick = close;
      root.querySelector('#dv_edit').onclick = () => { close(); editItem(it); };
    }
  });
}

// Read-only view of the COMPLETE product record — every field stored in catalog.json.
function productDetail(it) {
  const p = it.product || {};
  if (it.image) return kitchenDetail(it, p);   // kitchens get the dedicated clean view above
  const src = it.file ? `/models/${encodeURIComponent(it.category)}/${encodeURIComponent(it.file)}` : '';
  const ext = (it.file.split('.').pop() || '').toLowerCase();
  const canPreview = ext === 'glb' || ext === 'gltf';
  const rows = [];
  const row = (k, v) => { if (v == null || v === '') return; rows.push(`<tr><td>${esc(k)}</td><td>${v}</td></tr>`); };
  row('Brand', esc(p.brand));
  row('Product name', esc(p.name));
  row('Subtitle', esc(p.subtitle));
  row('SKU', esc(p.sku));
  row('Status', esc(it.status));
  row('Category', esc(it.category));
  row('Model file', `<code>${esc(it.file || '—')}</code>`);
  row('Model key', esc(p.modelKey));
  row('Emotional line', esc(p.emotionalLine));
  row('Headline', esc(p.headline));
  row('Description', esc(p.description || it.description));
  if (Array.isArray(p.features) && p.features.length)
    row('Features', `<div class="chips">${p.features.map(f => `<span class="chip">${esc(f)}</span>`).join('')}</div>`);
  // Category-aware spec rows (fridge fields for fridges, microwave fields for microwaves, …)
  for (const f of specsFor(it.category)) {
    const v = p[f.key];
    if (v == null || v === '') continue;
    const disp = f.key === 'energyClass'
      ? `<span class="energy energy-${esc((String(v)[0] || '').toUpperCase())}">${esc(String(v))}</span>`
      : f.type === 'bool' ? (v ? 'Yes' : 'No')
        : esc(String(v)) + (f.unit ? ` ${f.unit}` : '');
    row(f.label, disp);
  }
  row('Colour', p.swatchColor ? `<span class="swatch" style="background:${esc(p.swatchColor)}"></span>${esc(p.color || '')}` : esc(p.color));
  row('Price', p.price != null ? fmtPrice(p.price, p.currency) : '');
  row('Rating', p.rating != null ? `${p.rating} ★ (${p.reviewCount || 0} reviews)` : '');
  row('Variant group', esc(p.variantGroup));
  row('Product sheet', p.productSheetUrl ? `<a class="linkbtn" href="${esc(p.productSheetUrl)}" target="_blank" rel="noopener">Open PDF ↗</a>` : '');
  row('Product page', p.productPageUrl ? `<a class="linkbtn" href="${esc(p.productPageUrl)}" target="_blank" rel="noopener">Open page ↗</a>` : '');
  row('File size', fmtBytes(it.bytes));

  // Schema-agnostic: render any product field the bot added that has no curated row above,
  // so the admin always matches whatever is in catalog.json.
  const extra = Object.keys(p).filter(k => !DETAIL_KNOWN.has(k) && fmtVal(p[k]) !== '');
  if (extra.length) {
    rows.push(`<tr><td colspan="2" class="detail-sec">Other fields</td></tr>`);
    extra.forEach(k => row(prettyLabel(k), fmtVal(p[k])));
  }

  modal({
    wide: true,
    title: esc(p.name || it.name),
    bodyHTML: `
      ${it.image ? kitchenPanel(it, p) : ''}
      ${src && !it.missing && canPreview ? `<div class="preview" style="aspect-ratio:16/9;border-radius:10px;overflow:hidden">
        <model-viewer src="${src}" camera-controls auto-rotate camera-orbit="35deg 75deg auto" style="width:100%;height:100%"></model-viewer></div>` : ''}
      <table class="detail-tbl"><tbody>${rows.join('')}</tbody></table>
      ${reviewsHTML(p) ? `<div class="kp-sec">Reviews</div>${reviewsHTML(p)}` : ''}`,
    footHTML: `<button class="ghost" data-close>Close</button><button class="primary" id="dv_edit">✎ Edit</button>`,
    onMount(root, close) {
      root.querySelector('[data-close]').onclick = close;
      root.querySelector('#dv_edit').onclick = () => { close(); editItem(it); };
    }
  });
}

// ---------- modals ----------
function modal({ title, bodyHTML, footHTML, onMount, wide }) {
  const root = $('#modalRoot');
  root.innerHTML = `<div class="overlay"><div class="modal${wide ? ' wide' : ''}">
    <h3>${title}</h3><div class="content">${bodyHTML}</div><div class="foot">${footHTML}</div>
  </div></div>`;
  const close = () => root.innerHTML = '';
  root.querySelector('.overlay').onclick = (e) => { if (e.target.classList.contains('overlay')) close(); };
  if (onMount) onMount(root, close);
  return close;
}

// Dedicated edit form for image-based kitchen items — only the agreed kitchen fields,
// no fridge/appliance specs. Saves a clean product object (no stray keys).
function editKitchen(it) {
  const p = it.product || {};
  const flagNote = (p.flag && typeof p.flag === 'object') ? (p.flag.note || '') : (p.flag || '');
  const catOpts = state.categories.map(c =>
    `<option value="${esc(c.name)}" ${c.name === it.category ? 'selected' : ''}>${esc(c.name)}</option>`).join('');
  const adds = Array.isArray(p.additionalImages) ? p.additionalImages.join('\n') : '';
  const reviewsJSON = JSON.stringify(Array.isArray(p.reviews) ? p.reviews : [], null, 2);
  const elRow = e => `
    <div class="row">
      <div class="field"><label>${e[0].toUpperCase() + e.slice(1)}</label><input id="k_${e}" value="${esc(p[e] || '')}"></div>
      <div class="field"><label>${e} image</label><input id="k_${e}img" value="${esc(p[e + 'Image'] || '')}" placeholder="${esc(it.category)}/elements/${e}.webp"></div>
    </div>`;
  modal({
    wide: true,
    title: 'Edit kitchen',
    bodyHTML: `
      <div class="form-sec first">Item</div>
      <div class="field"><label>Catalog name</label><input id="m_name" value="${esc(it.name)}"></div>
      <div class="row">
        <div class="field"><label>Category</label><select id="m_cat">${catOpts}</select></div>
        <div class="field"><label>Status</label>
          <select id="m_status">${['active', 'draft', 'archived'].map(s => `<option ${s === it.status ? 'selected' : ''}>${s}</option>`).join('')}</select></div>
      </div>

      <div class="form-sec">Identity</div>
      <div class="row">
        <div class="field"><label>Brand</label><input id="k_brand" value="${esc(p.brand || '')}"></div>
        <div class="field"><label>Name</label><input id="k_name" value="${esc(p.name || '')}"></div>
      </div>
      <div class="row">
        <div class="field"><label>Colour</label><input id="k_color" value="${esc(p.color || '')}"></div>
        <div class="field"><label>Product type</label><input id="k_ptype" value="${esc(p.productType || '')}"></div>
      </div>
      <div class="field"><label>Kitchen type</label><input id="k_ktype" value="${esc(p.kitchenType || '')}" placeholder="modern / designer / natural-scandi / cottage style"></div>
      <div class="field"><label>Headline</label><input id="k_headline" value="${esc(p.headline || '')}"></div>
      <div class="field"><label>Description</label><textarea id="k_desc" rows="3">${esc(p.description || '')}</textarea></div>
      <div class="row">
        <div class="field"><label>Price label</label><input id="k_price" value="${esc(p.priceLabel || '')}" placeholder="Price on request"></div>
        <div class="field"><label>Product page URL</label><input id="k_page" value="${esc(p.productPageUrl || '')}"></div>
      </div>

      <div class="form-sec">The four elements <span class="hint">— value + image path</span></div>
      ${['front', 'carcase', 'worktop', 'handle'].map(elRow).join('')}

      <div class="form-sec">Images</div>
      <div class="field"><label>Hero image (main picture)</label><input id="k_hero" value="${esc(p.heroImage || it.image || '')}"></div>
      <div class="field"><label>Additional images <span class="hint">— one path per line</span></label><textarea id="k_adds" rows="3">${esc(adds)}</textarea></div>
      <div class="row">
        <div class="field"><label>Before image</label><input id="k_before" value="${esc(p.beforeImage || '')}"></div>
        <div class="field"><label>After image</label><input id="k_after" value="${esc(p.afterImage || '')}"></div>
      </div>

      <div class="form-sec">Luis note</div>
      <div class="field"><label>Luis note</label><input id="k_flagnote" value="${esc(flagNote)}"></div>

      <div class="form-sec">Reviews <span class="hint">— JSON list: rating, author, country, date, body (overall score is auto-computed)</span></div>
      <div class="field"><textarea id="k_reviews" rows="8" spellcheck="false">${esc(reviewsJSON)}</textarea></div>`,
    footHTML: `<button class="ghost" data-close>Cancel</button><button class="primary" id="m_save">Save</button>`,
    onMount(root, close) {
      root.querySelector('[data-close]').onclick = close;
      root.querySelector('#m_save').onclick = async () => {
        let parsed;
        try { parsed = JSON.parse($('#k_reviews').value.trim() || '[]'); }
        catch (e) { return toast('Reviews JSON is invalid'); }
        if (!Array.isArray(parsed)) return toast('Reviews must be a JSON list');
        // normalize each review to the agreed shape (no title), then derive the overall score
        const reviews = parsed.map(r => ({
          rating: Number(r.rating) || 0,
          author: (r.author || '').toString(),
          country: (r.country || '').toString(),
          date: (r.date || '').toString(),
          body: (r.body || '').toString(),
        }));
        const reviewCount = reviews.length;
        const reviewScore = reviewCount ? Math.round(reviews.reduce((s, r) => s + r.rating, 0) / reviewCount * 10) / 10 : 0;
        const reviewStars = Math.round(reviewScore * 2) / 2;
        const v = sel => $(sel).value.trim();
        const product = {
          brand: v('#k_brand'), name: v('#k_name'), color: v('#k_color'), productType: v('#k_ptype'), kitchenType: v('#k_ktype'),
          headline: v('#k_headline'), description: v('#k_desc'),
          priceLabel: v('#k_price'), productPageUrl: v('#k_page'),
          front: v('#k_front'), carcase: v('#k_carcase'), worktop: v('#k_worktop'), handle: v('#k_handle'),
          frontImage: v('#k_frontimg'), carcaseImage: v('#k_carcaseimg'),
          worktopImage: v('#k_worktopimg'), handleImage: v('#k_handleimg'),
          heroImage: v('#k_hero'),
          additionalImages: $('#k_adds').value.split('\n').map(s => s.trim()).filter(Boolean),
          beforeImage: v('#k_before'), afterImage: v('#k_after'),
          flag: v('#k_flagnote'),
          reviews, reviewScore, reviewStars, reviewCount,
        };
        const d = {
          name: v('#m_name'), category: $('#m_cat').value, status: $('#m_status').value,
          image: v('#k_hero'), file: '', description: v('#k_desc'), product,
        };
        if (!d.name) return toast('Catalog name is required');
        await api.updItem(it.id, d);
        close(); toast('Saved'); await load();
      };
    }
  });
}

// ---- Category-aware spec model ----------------------------------------------
// Each category shows its own spec fields in the edit form AND the Details modal.
// type: 'number' | 'text' | 'bool'. unit is appended in the Details view only.
const CATEGORY_SPECS = {
  Fridges: [
    { key: 'fridgeCapacity',     label: 'Fridge capacity (L)',  type: 'number', unit: 'L',      ph: '141' },
    { key: 'freezerCapacity',    label: 'Freezer capacity (L)', type: 'number', unit: 'L',      ph: 'optional' },
    { key: 'annualEnergy',       label: 'Annual energy (kWh)',  type: 'number', unit: 'kWh/yr', ph: '73' },
    { key: 'noise',              label: 'Noise (dB)',           type: 'number', unit: 'dB',     ph: '34' },
    { key: 'energyClass',        label: 'Energy class',         type: 'text',                   ph: 'D' },
    { key: 'dimensions',         label: 'Dimensions',           type: 'text',                   ph: '60 × 85 × 61 cm' },
  ],
  Microwaves: [
    { key: 'capacityL',          label: 'Capacity (L)',         type: 'number', unit: 'L',  ph: '17' },
    { key: 'microwavePowerW',    label: 'Microwave power (W)',  type: 'number', unit: 'W',  ph: '800' },
    { key: 'grill',              label: 'Grill',                type: 'bool' },
    { key: 'grillPowerW',        label: 'Grill power (W)',      type: 'number', unit: 'W',  ph: '1500' },
    { key: 'turntableCm',        label: 'Turntable Ø (cm)',     type: 'number', unit: 'cm', ph: '27' },
    { key: 'weightKg',           label: 'Weight (kg)',          type: 'number', unit: 'kg', ph: '15' },
    { key: 'dimensions',         label: 'Dimensions (W×H×D)',   type: 'text',               ph: '595 × 310 × 372 mm' },
  ],
  Dishwashers: [
    { key: 'placeSettings',      label: 'Place settings',       type: 'number',                ph: '14' },
    { key: 'energyPer100Cycles', label: 'Energy /100 cycles',   type: 'number', unit: 'kWh',   ph: '62' },
    { key: 'waterPerCycle',      label: 'Water /cycle (L)',     type: 'number', unit: 'L',     ph: '9.9' },
    { key: 'noiseClass',         label: 'Noise class',          type: 'text',                  ph: 'B' },
    { key: 'energyClass',        label: 'Energy class',         type: 'text',                  ph: 'C' },
    { key: 'dimensions',         label: 'Dimensions',           type: 'text',                  ph: '598 × 845 × 600 mm' },
  ],
  Cooktops: [
    { key: 'zones',              label: 'Cooking zones',        type: 'number',                ph: '4' },
    { key: 'induction',          label: 'Induction',            type: 'bool' },
    { key: 'totalPowerKw',       label: 'Total power (kW)',     type: 'number', unit: 'kW',    ph: '7.3' },
    { key: 'boosterKw',          label: 'Booster (kW)',         type: 'number', unit: 'kW',    ph: '3.7' },
    { key: 'dimensions',         label: 'Dimensions',           type: 'text',                  ph: '806 × 520 mm' },
  ],
  Hoods: [
    { key: 'airflow',            label: 'Airflow (m³/h)',       type: 'number', unit: 'm³/h',  ph: '650' },
    { key: 'noiseClass',         label: 'Noise class',          type: 'text',                  ph: 'B' },
    { key: 'energyClass',        label: 'Energy class',         type: 'text',                  ph: 'A' },
    { key: 'dimensions',         label: 'Dimensions',           type: 'text',                  ph: '898 mm wide' },
  ],
  CoffeeMachines: [
    { key: 'waterTank',          label: 'Water tank (L)',       type: 'number', unit: 'L',     ph: '1.5' },
    { key: 'beanContainer',      label: 'Bean container (g)',   type: 'number', unit: 'g',     ph: '300' },
    { key: 'milkContainer',      label: 'Milk container (L)',   type: 'number', unit: 'L',     ph: '0.5' },
    { key: 'pumpBar',            label: 'Pump pressure (bar)',  type: 'number', unit: 'bar',   ph: '15' },
    { key: 'dimensions',         label: 'Dimensions',           type: 'text',                  ph: '241 × 360 × 461 mm' },
  ],
};
const specsFor = cat => CATEGORY_SPECS[cat] || CATEGORY_SPECS.Fridges;

function specInputHTML(f, p) {
  const v = p[f.key];
  if (f.type === 'bool') {
    const opt = (val, lab) => `<option value="${val}" ${String(v) === val ? 'selected' : ''}>${lab}</option>`;
    return `<div class="field"><label>${esc(f.label)}</label><select id="sp_${f.key}">
      <option value="" ${v == null ? 'selected' : ''}>—</option>${opt('true', 'Yes')}${opt('false', 'No')}</select></div>`;
  }
  const t = f.type === 'number' ? 'number' : 'text';
  return `<div class="field"><label>${esc(f.label)}</label><input id="sp_${f.key}" type="${t}" value="${esc(v ?? '')}" placeholder="${esc(f.ph || '')}"></div>`;
}
function specsFormHTML(cat, p) {
  const fields = specsFor(cat);
  let html = '';
  for (let i = 0; i < fields.length; i += 2)
    html += `<div class="row">${specInputHTML(fields[i], p)}${fields[i + 1] ? specInputHTML(fields[i + 1], p) : ''}</div>`;
  return html;
}
function collectSpecs(cat) {
  const out = {};
  for (const f of specsFor(cat)) {
    const el = $('#sp_' + f.key); if (!el) continue;
    const raw = el.value.trim();
    out[f.key] = f.type === 'bool' ? (raw === '' ? null : raw === 'true')
      : f.type === 'number' ? (raw === '' ? null : Number(raw))
        : raw;
  }
  return out;
}

function editItem(it) {
  if (it && it.image) return editKitchen(it);   // kitchens use the dedicated form above
  const isNew = !it;
  it = it || { name: '', file: '', description: '', status: 'active', category: state.activeCat };
  const p = it.product || {};
  const catOpts = state.categories.map(c =>
    `<option value="${esc(c.name)}" ${c.name === it.category ? 'selected' : ''}>${esc(c.name)}</option>`).join('');
  const currencyOpts = ['EUR', 'DKK', 'USD'].map(c =>
    `<option ${c === (p.currency || 'EUR') ? 'selected' : ''}>${c}</option>`).join('');
  const features = Array.isArray(p.features) ? p.features.join('\n') : '';
  modal({
    wide: true,
    title: isNew ? 'Add product' : 'Edit product',
    bodyHTML: `
      <div class="form-sec first">Model file</div>
      <div class="field"><label>Catalog name</label><input id="m_name" value="${esc(it.name)}"></div>
      <div class="row">
        <div class="field"><label>Category</label><select id="m_cat">${catOpts}</select></div>
        <div class="field"><label>Status</label>
          <select id="m_status">
            ${['active', 'draft', 'archived'].map(s => `<option ${s === it.status ? 'selected' : ''}>${s}</option>`).join('')}
          </select></div>
      </div>
      <div class="field"><label>Model file (.glb in the category folder)</label>
        <input id="m_file" value="${esc(it.file || '')}" placeholder="e.g. K_4003_D_Hvid.glb"></div>

      <div class="form-sec">Identity</div>
      <div class="row">
        <div class="field"><label>Brand</label><input id="p_brand" value="${esc(p.brand || '')}" placeholder="e.g. Miele"></div>
        <div class="field"><label>Product name</label><input id="p_name" value="${esc(p.name || '')}" placeholder="e.g. K 4003 D"></div>
      </div>
      <div class="field"><label>Subtitle</label><input id="p_subtitle" value="${esc(p.subtitle || '')}" placeholder="e.g. Freestanding refrigerator · white"></div>
      <div class="field"><label>SKU</label><input id="p_sku" value="${esc(p.sku || '')}" placeholder="e.g. 12389230"></div>

      <div class="form-sec">Card copy</div>
      <div class="field"><label>Emotional line</label><input id="p_emotional" value="${esc(p.emotionalLine || '')}" placeholder="short tagline…"></div>
      <div class="field"><label>Headline</label><input id="p_headline" value="${esc(p.headline || '')}" placeholder="one-line headline…"></div>
      <div class="field"><label>Description</label><textarea id="p_description" rows="2">${esc(p.description || '')}</textarea></div>

      <div class="form-sec">Features <span class="hint">— one per line</span></div>
      <div class="field"><textarea id="p_features" rows="4" placeholder="DailyFresh&#10;FlexiBoard&#10;ComfortClean&#10;Miele@home (Wi-Fi)">${esc(features)}</textarea></div>

      <div class="form-sec">Specs <span class="hint">— ${esc(it.category)}</span></div>
      <div id="specsBox">${specsFormHTML(it.category, p)}</div>

      <div class="form-sec">Finish &amp; price</div>
      <div class="row">
        <div class="field"><label>Colour</label><input id="p_color" value="${esc(p.color || '')}" placeholder="White"></div>
        <div class="field"><label>Price</label><input id="p_price" type="number" value="${p.price ?? ''}" placeholder="870"></div>
        <div class="field"><label>Currency</label><select id="p_currency">${currencyOpts}</select></div>
      </div>

      <div class="form-sec">Links</div>
      <div class="field"><label>Product sheet URL (produktblad)</label><input id="p_sheet" value="${esc(p.productSheetUrl || '')}" placeholder="https://media.miele.com/…/FS_….pdf"></div>
      <div class="field"><label>Product page URL</label><input id="p_page" value="${esc(p.productPageUrl || '')}" placeholder="https://www.miele.dk/product/…"></div>`,
    footHTML: `<button class="ghost" data-close>Cancel</button><button class="primary" id="m_save">Save</button>`,
    onMount(root, close) {
      root.querySelector('[data-close]').onclick = close;
      // Re-render the spec inputs when the category changes (each category has its own fields).
      const catSel = root.querySelector('#m_cat');
      if (catSel) catSel.addEventListener('change', () => {
        const box = root.querySelector('#specsBox');
        if (box) box.innerHTML = specsFormHTML(catSel.value, it.product || {});
      });
      root.querySelector('#m_save').onclick = async () => {
        const num = (sel) => { const v = $(sel).value.trim(); return v === '' ? null : Number(v); };
        const file = $('#m_file').value.trim();
        const product = {
          brand: $('#p_brand').value.trim(),
          name: $('#p_name').value.trim(),
          subtitle: $('#p_subtitle').value.trim(),
          sku: $('#p_sku').value.trim(),
          emotionalLine: $('#p_emotional').value.trim(),
          headline: $('#p_headline').value.trim(),
          description: $('#p_description').value.trim(),
          features: $('#p_features').value.split('\n').map(s => s.trim()).filter(Boolean),
          ...collectSpecs($('#m_cat').value),
          color: $('#p_color').value.trim(),
          price: num('#p_price'),
          currency: $('#p_currency').value,
          productSheetUrl: $('#p_sheet').value.trim(),
          productPageUrl: $('#p_page').value.trim(),
          // modelKey is the stable id both web and Unity resolve to their own asset
          modelKey: file ? file.replace(/\.glb$/i, '') : null,
        };
        const d = {
          name: $('#m_name').value.trim(),
          category: $('#m_cat').value,
          status: $('#m_status').value,
          file,
          description: it.description || '',
          product,
        };
        if (!d.name) return toast('Catalog name is required');
        if (isNew) await api.addItem(d); else await api.updItem(it.id, d);
        close(); toast(isNew ? 'Product added' : 'Saved'); await load();
      };
    }
  });
}

// Two-stage delete: catalog-only (safe) OR delete-from-disk (guarded second confirm)
function deleteItem(it) {
  modal({
    title: 'Delete “' + it.name + '”',
    bodyHTML: `
      <div class="warn-box">Choose how to delete this model.</div>
      <div class="field"><b>Remove from catalog (safe)</b>
        <div class="meta">Takes it out of this admin list. The file
        <code>${esc(it.file || '—')}</code> stays on disk and reappears on Rescan.</div></div>
      <hr style="border:none;border-top:1px solid var(--line);margin:0">
      <div class="field"><b style="color:var(--danger)">Delete file from disk</b>
        <div class="meta">Permanently deletes the actual <code>.glb</code> from the folder. Cannot be undone.</div></div>`,
    footHTML: `
      <button class="ghost" data-close>Cancel</button>
      <button id="d_cat">Remove from catalog</button>
      <button class="danger" id="d_file">Delete from disk…</button>`,
    onMount(root, close) {
      root.querySelector('[data-close]').onclick = close;
      root.querySelector('#d_cat').onclick = async () => {
        await api.delItem(it.id); close(); toast('Removed from catalog (file kept)'); await load();
      };
      root.querySelector('#d_file').onclick = () => { close(); confirmHardDelete(it); };
    }
  });
}

// Second, explicit warning the user must agree to before touching the disk.
function confirmHardDelete(it) {
  modal({
    title: '⚠ Permanently delete file?',
    bodyHTML: `
      <div class="danger-box">
        This will <b>permanently delete</b> the file from disk:<br><br>
        <code>3D-models/${esc(it.category)}/${esc(it.file || '—')}</code><br>
        (${fmtBytes(it.bytes)})<br><br>
        This cannot be undone. Type <b>DELETE</b> below to confirm.
      </div>
      <div class="field"><input id="d_confirm" placeholder="Type DELETE to enable" autocomplete="off"></div>`,
    footHTML: `<button class="ghost" data-close>Cancel</button>
      <button class="danger" id="d_go" disabled>Permanently delete</button>`,
    onMount(root, close) {
      root.querySelector('[data-close]').onclick = close;
      const go = root.querySelector('#d_go');
      const inp = root.querySelector('#d_confirm');
      inp.oninput = () => { go.disabled = inp.value.trim().toUpperCase() !== 'DELETE'; };
      inp.focus();
      go.onclick = async () => {
        const r = await api.delItemFile(it.id);
        close();
        toast(r.fileDeleted ? 'File permanently deleted' : 'Catalog entry removed (file was already gone)');
        await load();
      };
    }
  });
}

function editCategory(c) {
  modal({
    title: 'Category: ' + c.name,
    bodyHTML: `
      <div class="field"><label>Name</label><input id="c_name" value="${esc(c.name)}"></div>
      <div class="meta">Deleting a category removes it and its items from the catalog only — the files on disk are kept.</div>`,
    footHTML: `<button class="danger ghost" id="c_del" style="margin-right:auto">Delete category</button>
      <button class="ghost" data-close>Cancel</button><button class="primary" id="c_save">Save</button>`,
    onMount(root, close) {
      root.querySelector('[data-close]').onclick = close;
      root.querySelector('#c_save').onclick = async () => {
        const name = $('#c_name').value.trim();
        if (name && name !== c.name) { await api.renameCat(c.id, name); if (state.activeCat === c.name) state.activeCat = name; }
        close(); await load();
      };
      root.querySelector('#c_del').onclick = async () => {
        if (state.activeCat === c.name) state.activeCat = null;
        await api.delCat(c.id); close(); toast('Category removed from catalog'); await load();
      };
    }
  });
}

// Review & clean up catalog entries whose files are gone (catalog-only — files are already off disk).
function cleanupMissing() {
  const missing = state.items.filter(i => i.missing);
  const byCat = {};
  missing.forEach(i => { (byCat[i.category] = byCat[i.category] || []).push(i); });
  const withData = missing.filter(i => i.product && Object.keys(i.product).length).length;
  const rows = Object.keys(byCat).sort().map(c =>
    `<tr><td>${esc(c)}</td><td>${byCat[c].length}</td></tr>`).join('');
  modal({
    wide: true,
    title: `⚠ ${missing.length} missing file${missing.length > 1 ? 's' : ''}`,
    bodyHTML: `
      <div class="warn-box">These catalog entries point to files that are no longer on disk
        (usually from renaming or moving models). Removing them is <b>catalog-only</b> — there are no files left to delete.${withData ? `<br><br><b>${withData}</b> of them have product data you entered, which would be lost.` : ''}</div>
      <table class="detail-tbl"><tbody>${rows}</tbody></table>
      <div class="meta">Tip: use the <b>⚠ Missing files</b> status filter to inspect them individually first.</div>`,
    footHTML: `<button class="ghost" data-close>Cancel</button>
      ${state.activeCat && byCat[state.activeCat] ? `<button id="cm_cat">Remove ${byCat[state.activeCat].length} in ${esc(state.activeCat)}</button>` : ''}
      <button class="danger" id="cm_all">Remove all ${missing.length}</button>`,
    onMount(root, close) {
      root.querySelector('[data-close]').onclick = close;
      const catBtn = root.querySelector('#cm_cat');
      if (catBtn) catBtn.onclick = async () => { const r = await api.pruneMissing(state.activeCat); close(); toast(`Removed ${r.removed} missing entr${r.removed === 1 ? 'y' : 'ies'}`); await load(); };
      root.querySelector('#cm_all').onclick = async () => { const r = await api.pruneMissing(); close(); toast(`Removed ${r.removed} missing entr${r.removed === 1 ? 'y' : 'ies'}`); await load(); };
    }
  });
}

function addCategory() {
  modal({
    title: 'New category',
    bodyHTML: `<div class="field"><label>Name</label><input id="nc_name" placeholder="e.g. Ovens"></div>
      <div class="meta">A matching folder will be created under <code>3D-models/</code>.</div>`,
    footHTML: `<button class="ghost" data-close>Cancel</button><button class="primary" id="nc_save">Create</button>`,
    onMount(root, close) {
      root.querySelector('[data-close]').onclick = close;
      const save = async () => {
        const name = $('#nc_name').value.trim();
        if (!name) return toast('Name required');
        const r = await api.addCat(name);
        if (r.error) return toast(r.error);
        state.activeCat = name; close(); toast('Category created'); await load();
      };
      root.querySelector('#nc_save').onclick = save;
      const inp = root.querySelector('#nc_name'); inp.focus();
      inp.onkeydown = (e) => { if (e.key === 'Enter') save(); };
    }
  });
}

// ---------- helpers ----------
function esc(s) { return String(s ?? '').replace(/[&<>"']/g, m => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[m])); }
function fmtBytes(b) { if (!b) return '—'; const u = ['B', 'KB', 'MB', 'GB']; let i = 0; while (b >= 1024 && i < 3) { b /= 1024; i++; } return b.toFixed(b < 10 && i > 0 ? 1 : 0) + ' ' + u[i]; }
function fmtPrice(v, c) { try { return new Intl.NumberFormat(undefined, { style: 'currency', currency: c || 'EUR', maximumFractionDigits: 0 }).format(v); } catch { return `${v} ${c || ''}`.trim(); } }

// product keys that already have a curated row in the Details view
const DETAIL_KNOWN = new Set(['brand', 'name', 'subtitle', 'sku', 'modelKey', 'emotionalLine',
  'headline', 'description', 'features', 'fridgeCapacity', 'freezerCapacity', 'annualEnergy',
  'noise', 'energyClass', 'dimensions', 'color', 'swatchColor', 'price', 'priceDKK', 'currency', 'rating',
  'reviewCount', 'reviewScore', 'reviewStars', 'reviews', 'variantGroup', 'productSheetUrl', 'productPageUrl',
  // category spec fields (rendered as curated rows above)
  'capacityL', 'microwavePowerW', 'grill', 'grillPowerW', 'turntableCm', 'weightKg',
  'placeSettings', 'energyPer100Cycles', 'waterPerCycle', 'noiseClass',
  'zones', 'induction', 'totalPowerKw', 'boosterKw', 'airflow', 'waterTank', 'beanContainer', 'milkContainer', 'pumpBar',
  // kitchen fields — rendered in the dedicated kitchen panel, not as raw "Other fields"
  'productType', 'kitchenType', 'priceLabel', 'front', 'carcase', 'worktop', 'handle',
  'frontImage', 'carcaseImage', 'worktopImage', 'handleImage', 'heroImage',
  'additionalImages', 'beforeImage', 'afterImage', 'flag']);

// turn a field key into a readable label: camelCase / snake_case -> "Title case"
function prettyLabel(k) {
  return k.replace(/[_-]+/g, ' ').replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/^./, c => c.toUpperCase());
}

// generic value formatter for arbitrary field types (string / number / bool / array / object / url)
function fmtVal(v) {
  if (v == null || v === '') return '';
  if (Array.isArray(v)) return v.length
    ? `<div class="chips">${v.map(x => `<span class="chip">${esc(typeof x === 'object' ? JSON.stringify(x) : String(x))}</span>`).join('')}</div>` : '';
  if (typeof v === 'object') return `<code>${esc(JSON.stringify(v))}</code>`;
  if (typeof v === 'string' && /^https?:\/\//i.test(v))
    return `<a class="linkbtn" href="${esc(v)}" target="_blank" rel="noopener">${esc(v)} ↗</a>`;
  return esc(String(v));
}

// ---------- wire up ----------
$('#addCatBtn').onclick = addCategory;
$('#addItemBtn').onclick = () => editItem(null);
$('#missingBtn').onclick = cleanupMissing;
$('#searchBox').oninput = (e) => { state.search = e.target.value; renderGrid(); };
$('#statusFilter').onchange = (e) => { state.status = e.target.value; renderGrid(); };
$('#scanBtn').onclick = async () => {
  const r = await api.scan();
  toast(`Rescan: +${r.added} new, ${r.missing} missing`);
  await load();
};

load();
