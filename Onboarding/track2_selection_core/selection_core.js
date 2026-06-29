'use strict';

// ── Runtime classifications ──────────────────────────────────────────────────

function deriveTone(color) {
  if (!color) return 'neutral';
  const c = color.toLowerCase();
  if (/blackberry|rose gold|aluminium|red/.test(c)) return 'bold';
  if (/obsidian|black|graphite|blackboard/.test(c)) return 'dark';
  if (/steel look/.test(c)) return 'wood';
  if (/white|pearlbeige|lotus|ivory|alpine/.test(c)) return 'light';
  return 'neutral';
}

function parseFirstNumber(str) {
  if (!str) return null;
  const m = str.match(/\d+(\.\d+)?/);
  return m ? parseFloat(m[0]) : null;
}

function deriveCapacityClass(category, product) {
  switch (category) {
    case 'Fridges': {
      const width = parseFirstNumber(product.dimensions);
      const cap = product.fridgeCapacity || 0;
      if (width >= 75 || cap >= 300) return 'host';
      if (cap >= 220) return 'standard';
      return 'compact';
    }
    case 'Dishwashers':
      return (product.placeSettings || 0) <= 10 ? 'compact' : 'standard';
    case 'Cooktops': {
      const zones = product.zones || 0;
      if (zones >= 5) return 'host';
      if (zones >= 4) return 'standard';
      return 'compact';
    }
    case 'Hoods': {
      // dimensions are in mm for hoods
      const width = parseFirstNumber(product.dimensions);
      if (width === null) return 'standard';
      if (width >= 900) return 'host';
      if (width >= 600) return 'standard';
      return 'compact';
    }
    case 'Microwaves': {
      const text = product.headline || product.name || '';
      const m = text.match(/(\d+)\s*[Ll]\b/);
      const litres = m ? parseInt(m[1], 10) : 26;
      return litres <= 17 ? 'compact' : 'standard';
    }
    case 'CoffeeMachines':
    default:
      return 'standard';
  }
}

// ── Match predicates ─────────────────────────────────────────────────────────

const CAP_OK = {
  compact:  ['compact'],
  standard: ['standard', 'compact'],
  host:     ['host', 'standard'],
};

function toneMatch(itemTone, answer) {
  if (!answer) return true;
  return itemTone === answer || itemTone === 'neutral';
}

function sizeOk(capClass, household) {
  if (!household) return true;
  return CAP_OK[household].includes(capClass);
}

function budgetOk(priceTier, budget) {
  if (!budget || budget === 'any') return true;
  return priceTier === budget;
}

// ── Kitchen selection ────────────────────────────────────────────────────────

function selectKitchens(items, answers) {
  const { style, tone, budget } = answers;

  let base = items.filter(it => it.category === 'Kitchens');
  if (style) base = base.filter(it => it.product.kitchenType === style);
  if (tone)  base = base.filter(it => it.product.tone === tone);

  let kits = base;
  let budgetRelaxed = false;

  if (budget && budget !== 'any') {
    const onTier = base.filter(it => it.product.priceTier === budget);
    if (onTier.length > 0) {
      kits = onTier;
    } else {
      kits = base;
      budgetRelaxed = base.length > 0;
    }
  }

  const sorted = [...kits].sort((a, b) => (a.product.priceGroup || 0) - (b.product.priceGroup || 0));

  return {
    count: kits.length,
    budgetRelaxed,
    shortlist: sorted.slice(0, 6).map(it => ({ id: it.id })),
  };
}

// ── Appliance selection ──────────────────────────────────────────────────────

const APPLIANCE_CATS = ['Fridges', 'Cooktops', 'Hoods', 'Microwaves', 'Dishwashers', 'CoffeeMachines'];
const FLOOR = { Fridges: 2 };
function floorOf(cat) { return FLOOR[cat] || 1; }

function buildWhy(tonem, capfit, household, budm) {
  const parts = [];
  parts.push(tonem ? 'finish matches your palette' : 'a neutral finish');
  if (capfit && household) {
    if (household === 'compact')     parts.push('sized to fit');
    else if (household === 'host')   parts.push('scaled to host');
    else                             parts.push('sized for the household');
  }
  parts.push(budm ? 'within budget' : 'closest in price');
  return 'Why: ' + parts.join(' · ') + '.';
}

function selectCategory(items, category, answers) {
  const { tone, household, budget } = answers;
  const floor = floorOf(category);

  const pool = items
    .filter(it => it.category === category)
    .map(it => ({
      id: it.id,
      _tone:     deriveTone(it.product.color),
      _capClass: deriveCapacityClass(category, it.product),
      _tier:     it.product.priceTier,
    }));

  const sOk = x => sizeOk(x._capClass, household);
  const tOk = x => toneMatch(x._tone, tone);
  const bOk = x => budgetOk(x._tier, budget);

  let opt = pool.filter(x => sOk(x) && tOk(x) && bOk(x));
  let relaxed = false;

  if (opt.length < floor) { opt = pool.filter(x => sOk(x) && bOk(x)); relaxed = true; }
  if (opt.length < floor) { opt = pool.filter(x => sOk(x));            relaxed = true; }
  if (opt.length < floor) { opt = [...pool];                            relaxed = true; }

  const scored = opt.map(x => {
    const capfit = sOk(x) ? 1 : 0;
    const tonem  = toneMatch(x._tone, tone) ? 1 : 0;
    const budm   = bOk(x) ? 1 : 0;
    return { x, score: 4 * capfit + 2 * tonem + budm, capfit, tonem, budm };
  }).sort((a, b) => b.score - a.score);

  const top = scored[0];
  const matchStrength = top.capfit + top.tonem + top.budm;

  return {
    category,
    count: opt.length,
    relaxed,
    topPick: { id: top.x.id },
    matchStrength,
    why: buildWhy(top.tonem === 1, top.capfit === 1, household, top.budm === 1),
    options: scored.slice(0, 8).map(s => s.x.id),
  };
}

// ── Profile derivation ───────────────────────────────────────────────────────

const INTENT = {
  'modern':                 ['Fast & Focused', 'bright, efficient — in, fed, and out'],
  'designer':               ['Host & Gather',  'open, social — made to entertain'],
  'cottage style':          ['Calm & Unwind',  'warm, tactile, lived-in ease'],
  'natural & scandinavian': ['Calm & Unwind',  'quiet, natural, restorative'],
};

const CABINET_DIR = {
  'modern':                 { label: 'Nobilia · Modern / Handleless',       front: 'handleless slab fronts' },
  'designer':               { label: 'Nobilia · Designer / Statement',       front: 'lacquer slab fronts' },
  'cottage style':          { label: 'Nobilia · Modern Cottage / Country',   front: 'frame fronts' },
  'natural & scandinavian': { label: 'Nobilia · Natural & Scandinavian',     front: 'slab fronts, light wood' },
};

const TONE_FINISH     = { light: 'light, matte', dark: 'deep, matte', wood: 'matte wood', bold: 'colour accent' };
const APPLIANCE_FINISH = {
  light: 'white, clean steel — bright, neutral',
  dark:  'matte black, obsidian — deep, dramatic',
  wood:  'steel look, warm neutral — soft, natural',
  bold:  'accent tones over steel — characterful',
};
const LIGHTING = {
  'Calm & Unwind':  'warm soft light · 2700–3000 K',
  'Host & Gather':  'warm-neutral · 3000–3500 K',
  'Fast & Focused': 'neutral bright · 3500–4000 K',
};

const PT_TONE  = { light: 'light & airy', dark: 'dark & moody', wood: 'warm natural', bold: 'bold accent' };
const PT_STYLE = {
  'modern': 'clean lines', 'designer': 'statement',
  'cottage style': 'tactile framed', 'natural & scandinavian': 'natural calm',
};
const PT_CAP  = { compact: 'intimate', standard: 'family', host: 'a crowd' };
const PT_VERB = { 'Calm & Unwind': 'savour', 'Host & Gather': 'gather', 'Fast & Focused': 'flow' };
const PT_TIER = { Essential: 'essential', Signature: 'considered', Premium: 'premium' };

function deriveProfile(answers) {
  const { style, tone, household, budget } = answers;
  const [intent, tagline] = INTENT[style] || ['', ''];
  const cab = CABINET_DIR[style] || { label: '', front: '' };

  const cabinetDirection =
    cab.label + ' — ' + cab.front + (tone ? ', ' + (TONE_FINISH[tone] || '') : '');

  const tags = [];
  if (tone && PT_TONE[tone])              tags.push(PT_TONE[tone]);
  if (style && PT_STYLE[style])           tags.push(PT_STYLE[style]);
  if (household && PT_CAP[household])     tags.push(PT_CAP[household]);
  if (intent && PT_VERB[intent])          tags.push(PT_VERB[intent]);
  if (budget && budget !== 'any' && PT_TIER[budget]) tags.push(PT_TIER[budget]);

  return {
    intent,
    tagline,
    cabinetDirection,
    applianceFinish: APPLIANCE_FINISH[tone] || '',
    lighting: LIGHTING[intent] || '',
    tags,
  };
}

// ── Main ─────────────────────────────────────────────────────────────────────

function run(answers, catalog) {
  const items = catalog.items;
  return {
    answers,
    profile:    deriveProfile(answers),
    kitchens:   selectKitchens(items, answers),
    appliances: APPLIANCE_CATS.map(cat => selectCategory(items, cat, answers)),
  };
}

module.exports = { run, deriveTone, deriveCapacityClass, toneMatch };
