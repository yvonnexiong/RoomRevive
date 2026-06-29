'use strict';
// Watches onboarding_answers.json and re-runs selection whenever Unity updates it.
// Run once before entering Play Mode: node watch.js
const fs   = require('fs');
const path = require('path');
const { run } = require('./selection_core');

const root        = path.join(__dirname, '..');
const answersFile = path.join(root, 'onboarding_answers.json');
const catalogFile = path.join(root, 'shared/catalog.json');
const outputFile  = path.join(root, 'onboarding_selection.json');

const DISPLAY_CAT = {
  Kitchens:       'Kitchen',
  Fridges:        'Fridge',
  Cooktops:       'Cooktop',
  Hoods:          'Hood',
  Microwaves:     'Microwave',
  Dishwashers:    'Dishwasher',
  CoffeeMachines: 'Coffee machine',
};

function runOnce() {
  try {
    const answers = JSON.parse(fs.readFileSync(answersFile, 'utf8'));
    const catalog = JSON.parse(fs.readFileSync(catalogFile, 'utf8'));

    const byId = {};
    catalog.items.forEach(item => {
      byId[item.id] = (item.product && item.product.name) || item.name || item.id;
    });

    const result = run(answers, catalog);

    const rows = [];
    if (result.kitchens.shortlist.length > 0) {
      const id = result.kitchens.shortlist[0].id;
      rows.push({ category: 'Kitchen', name: byId[id] || id, id });
    }
    result.appliances.forEach(a => {
      const id = a.topPick.id;
      rows.push({ category: DISPLAY_CAT[a.category] || a.category, name: byId[id] || id, id });
    });

    const output = {
      intent:     result.profile.intent,
      tagline:    result.profile.tagline,
      rows,
      profile:    result.profile,
      kitchens:   result.kitchens,
      appliances: result.appliances,
      answers:    result.answers,
    };

    fs.writeFileSync(outputFile, JSON.stringify(output, null, 2), 'utf8');
    console.log(`[${new Date().toLocaleTimeString()}] ✓ ${result.profile.intent} — wrote ${outputFile}`);
  } catch (e) {
    console.error(`[${new Date().toLocaleTimeString()}] ✗ ${e.message}`);
  }
}

console.log(`Watching ${answersFile} …`);
console.log('Enter Unity Play Mode and complete the flow to trigger selection.\n');

// Debounce: wait 100ms after last change before running (file may still be writing)
let timer;
fs.watch(answersFile, () => {
  clearTimeout(timer);
  timer = setTimeout(runOnce, 100);
});
