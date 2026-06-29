'use strict';
// Golden tests from SELECTION_LOGIC.md §8 — counts must match the prototype exactly.
const fs   = require('fs');
const path = require('path');
const { run } = require('./selection_core');

const catalog = JSON.parse(
  fs.readFileSync(path.join(__dirname, '../shared/catalog.json'), 'utf8')
);

const GOLDEN = [
  {
    answers:  { style: 'modern', tone: 'light', household: 'standard', budget: 'Signature' },
    expected: { kitchens: 12, Fridges: 6, Cooktops: 7, Hoods: 9, Microwaves: 3, Dishwashers: 2, CoffeeMachines: 1 },
  },
  {
    answers:  { style: 'designer', tone: 'dark', household: 'host', budget: 'Premium' },
    expected: { kitchens: 5, Fridges: 8, Cooktops: 4, Hoods: 13, Microwaves: 2, Dishwashers: 3, CoffeeMachines: 4 },
  },
  {
    answers:  { style: 'natural & scandinavian', tone: 'wood', household: 'compact', budget: 'Essential' },
    expected: { kitchens: 6, Fridges: 3, Cooktops: 3, Hoods: 6, Microwaves: 4, Dishwashers: 2, CoffeeMachines: 20 },
  },
  {
    answers:  { style: 'cottage style', tone: 'light', household: 'standard', budget: 'any' },
    expected: { kitchens: 6, Fridges: 13, Cooktops: 1, Hoods: 29, Microwaves: 6, Dishwashers: 5, CoffeeMachines: 4 },
  },
];

let passed = 0;
let failed = 0;

for (const { answers, expected } of GOLDEN) {
  const label = `${answers.style} / ${answers.tone} / ${answers.household} / ${answers.budget}`;
  const result = run(answers, catalog);
  const issues = [];

  if (result.kitchens.count !== expected.kitchens) {
    issues.push(`kitchens: got ${result.kitchens.count}, want ${expected.kitchens}`);
  }
  for (const [cat, want] of Object.entries(expected)) {
    if (cat === 'kitchens') continue;
    const a = result.appliances.find(x => x.category === cat);
    if (!a) { issues.push(`missing category ${cat}`); continue; }
    if (a.count !== want) issues.push(`${cat}: got ${a.count}, want ${want}`);
  }

  if (issues.length === 0) {
    console.log(`PASS  ${label}`);
    passed++;
  } else {
    console.log(`FAIL  ${label}`);
    issues.forEach(i => console.log(`      ${i}`));
    failed++;
  }
}

console.log(`\n${passed}/${passed + failed} tests passed`);
process.exit(failed > 0 ? 1 : 0);
