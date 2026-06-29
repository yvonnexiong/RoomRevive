'use strict';
// Usage: node cli.js [answers.json] [catalog.json] [output.json]
// Defaults match the paths OnboardingBridge.cs uses.
const fs   = require('fs');
const path = require('path');
const { run } = require('./selection_core');

const root = path.join(__dirname, '..');

const answersFile = process.argv[2] || path.join(root, 'onboarding_answers.json');
const catalogFile = process.argv[3] || path.join(root, 'shared/catalog.json');
const outputFile  = process.argv[4] || path.join(root, 'onboarding_selection.json');

// Display labels Unity shows in the ReadyUI card (matches BuildReadyPage row order)
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
  const answers = JSON.parse(fs.readFileSync(answersFile, 'utf8'));
  const catalog = JSON.parse(fs.readFileSync(catalogFile, 'utf8'));

  // Build id → display name index
  const byId = {};
  catalog.items.forEach(item => {
    byId[item.id] = (item.product && item.product.name) || item.name || item.id;
  });

  const result = run(answers, catalog);

  // Build the rows[] array Unity parses for the ReadyUI card
  const rows = [];
  if (result.kitchens.shortlist.length > 0) {
    const id = result.kitchens.shortlist[0].id;
    rows.push({ category: 'Kitchen', name: byId[id] || id, id });
  }
  result.appliances.forEach(a => {
    const id = a.topPick.id;
    rows.push({ category: DISPLAY_CAT[a.category] || a.category, name: byId[id] || id, id });
  });

  // Output: Unity-friendly header + rows, plus full contract for Track 3
  const output = {
    intent:     result.profile.intent,
    tagline:    result.profile.tagline,
    rows,
    // Full output contract — Track 3 uses these
    profile:    result.profile,
    kitchens:   result.kitchens,
    appliances: result.appliances,
    answers:    result.answers,
  };

  fs.writeFileSync(outputFile, JSON.stringify(output, null, 2), 'utf8');

  console.log(`[${new Date().toLocaleTimeString()}] Written → ${outputFile}`);
  console.log(`  intent:   ${result.profile.intent}`);
  console.log(`  kitchens: ${result.kitchens.count}${result.kitchens.budgetRelaxed ? ' (budget relaxed)' : ''}`);
  result.appliances.forEach(a => {
    const flag = a.relaxed ? ' (relaxed)' : '';
    console.log(`  ${a.category.padEnd(16)} top: ${byId[a.topPick.id] || a.topPick.id}${flag}`);
  });
}

runOnce();
