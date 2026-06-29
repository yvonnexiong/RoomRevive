'use strict';
// Usage: node cli.js [answers.json] [catalog.json] [output.json]
// Defaults read from ../onboarding_answers.json and ../shared/catalog.json,
// write to ../onboarding_selection.json — matching the paths OnboardingBridge.cs uses.
const fs   = require('fs');
const path = require('path');
const { run } = require('./selection_core');

const root = path.join(__dirname, '..');

const answersFile = process.argv[2] || path.join(root, 'onboarding_answers.json');
const catalogFile = process.argv[3] || path.join(root, 'shared/catalog.json');
const outputFile  = process.argv[4] || path.join(root, 'onboarding_selection.json');

const answers = JSON.parse(fs.readFileSync(answersFile, 'utf8'));
const catalog = JSON.parse(fs.readFileSync(catalogFile, 'utf8'));

const result = run(answers, catalog);

fs.writeFileSync(outputFile, JSON.stringify(result, null, 2), 'utf8');

console.log(`Written → ${outputFile}`);
console.log(`  intent:   ${result.profile.intent}`);
console.log(`  kitchens: ${result.kitchens.count}${result.kitchens.budgetRelaxed ? ' (budget relaxed)' : ''}`);
result.appliances.forEach(a => {
  const flag = a.relaxed ? ' (relaxed)' : '';
  console.log(`  ${a.category.padEnd(16)} ${a.count} options  strength ${a.matchStrength}/3${flag}`);
});
