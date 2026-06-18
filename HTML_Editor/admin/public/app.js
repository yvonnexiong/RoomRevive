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

let lastRev = null;
async function getRev() { try { return (await (await fetch('/api/version')).json()).rev; } catch { return lastRev; } }

async function load() {
  state = { ...state, ...(await api.get()) };
  if (state.activeCat && !state.categories.find(c => c.name === state.activeCat)) state.activeCat = null;
  if (!state.activeCat && state.categories[0]) state.activeCat = state.categories[0].name;
  render();
  lastRev = await getRev(); // resync so our own edits don't trigger a "changed" toast
}

// Live auto-refresh: the server watches the folder and auto-merges new models;
// here we just notice the catalog changed and re-render. Skips while a dialog is open.
async function poll() {
  if ($('#modalRoot').children.length) return;
  const rev = await getRev();
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
  if (it.missing || !it.file) {
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

  const price = p.price != null ? `<span class="price-tag">${fmtPrice(p.price, p.currency)}</span>` : '';
  const energy = p.energyClass ? `<span class="energy energy-${esc((p.energyClass[0] || '').toUpperCase())}">${esc(p.energyClass)}</span>` : '';
  const swatch = p.swatchColor ? `<span class="swatch" style="background:${esc(p.swatchColor)}"></span>` : '';
  const sub = p.subtitle ? `<div class="meta">${esc(p.subtitle)}</div>` : '';
  const finish = (p.color || p.dimensions)
    ? `<div class="meta">${swatch}${esc([p.color, p.dimensions].filter(Boolean).join(' · '))}</div>` : '';

  const specs = [];
  if (p.fridgeCapacity != null) specs.push(`<span><b>${p.fridgeCapacity} L</b> fridge</span>`);
  if (p.freezerCapacity != null) specs.push(`<span><b>${p.freezerCapacity} L</b> freezer</span>`);
  if (p.noise != null) specs.push(`<span><b>${p.noise}</b> dB</span>`);
  if (p.annualEnergy != null) specs.push(`<span><b>${p.annualEnergy}</b> kWh/yr</span>`);
  const specsHTML = specs.length ? `<div class="specs">${specs.join('')}</div>` : '';

  const feats = Array.isArray(p.features) ? p.features.slice(0, 6) : [];
  const featHTML = feats.length ? `<div class="chips">${feats.map(f => `<span class="chip">${esc(f)}</span>`).join('')}</div>` : '';

  const blurb = p.headline || p.emotionalLine || it.description;

  el.innerHTML = `
    ${preview}
    <div class="body">
      ${it.missing ? `<div class="missing-banner">⚠ File no longer on disk — <code>3D-models/${esc(it.category)}/${esc(it.file || '—')}</code></div>` : ''}
      <div class="titlerow"><div class="name">${esc(p.name || it.name)}</div>${price}</div>
      ${sub}
      <div class="meta"><span class="status-dot s-${it.status}"></span>${it.status}${p.brand ? ` · ${esc(p.brand)}` : ''}${energy}</div>
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

// Read-only view of the COMPLETE product record — every field stored in catalog.json.
function productDetail(it) {
  const p = it.product || {};
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
  row('Fridge capacity', p.fridgeCapacity != null ? `${p.fridgeCapacity} L` : '');
  row('Freezer capacity', p.freezerCapacity != null ? `${p.freezerCapacity} L` : '');
  row('Annual energy', p.annualEnergy != null ? `${p.annualEnergy} kWh/yr` : '');
  row('Noise', p.noise != null ? `${p.noise} dB` : '');
  row('Energy class', p.energyClass ? `<span class="energy energy-${esc((p.energyClass[0] || '').toUpperCase())}">${esc(p.energyClass)}</span>` : '');
  row('Dimensions', esc(p.dimensions));
  row('Colour', p.swatchColor ? `<span class="swatch" style="background:${esc(p.swatchColor)}"></span>${esc(p.color || '')}` : esc(p.color));
  row('Price', p.price != null ? fmtPrice(p.price, p.currency) : '');
  row('Rating', p.rating != null ? `${p.rating} ★ (${p.reviewCount || 0} reviews)` : '');
  row('Variant group', esc(p.variantGroup));
  row('Product sheet', p.productSheetUrl ? `<a class="linkbtn" href="${esc(p.productSheetUrl)}" target="_blank" rel="noopener">Open PDF ↗</a>` : '');
  row('Product page', p.productPageUrl ? `<a class="linkbtn" href="${esc(p.productPageUrl)}" target="_blank" rel="noopener">Open page ↗</a>` : '');
  row('File size', fmtBytes(it.bytes));

  modal({
    wide: true,
    title: esc(p.name || it.name),
    bodyHTML: `
      ${src && !it.missing && canPreview ? `<div class="preview" style="aspect-ratio:16/9;border-radius:10px;overflow:hidden">
        <model-viewer src="${src}" camera-controls auto-rotate camera-orbit="35deg 75deg auto" style="width:100%;height:100%"></model-viewer></div>` : ''}
      <table class="detail-tbl"><tbody>${rows.join('')}</tbody></table>`,
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

function editItem(it) {
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

      <div class="form-sec">Specs</div>
      <div class="row">
        <div class="field"><label>Fridge capacity (L)</label><input id="p_fridge" type="number" value="${p.fridgeCapacity ?? ''}" placeholder="141"></div>
        <div class="field"><label>Freezer capacity (L)</label><input id="p_freezer" type="number" value="${p.freezerCapacity ?? ''}" placeholder="optional"></div>
      </div>
      <div class="row">
        <div class="field"><label>Annual energy (kWh)</label><input id="p_energy" type="number" value="${p.annualEnergy ?? ''}" placeholder="73"></div>
        <div class="field"><label>Noise (dB)</label><input id="p_noise" type="number" value="${p.noise ?? ''}" placeholder="34"></div>
      </div>
      <div class="row">
        <div class="field"><label>Energy class</label><input id="p_class" value="${esc(p.energyClass || '')}" placeholder="D"></div>
        <div class="field"><label>Dimensions</label><input id="p_dims" value="${esc(p.dimensions || '')}" placeholder="60 × 85 × 61 cm"></div>
      </div>

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
          fridgeCapacity: num('#p_fridge'),
          freezerCapacity: num('#p_freezer'),
          annualEnergy: num('#p_energy'),
          noise: num('#p_noise'),
          energyClass: $('#p_class').value.trim(),
          dimensions: $('#p_dims').value.trim(),
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
