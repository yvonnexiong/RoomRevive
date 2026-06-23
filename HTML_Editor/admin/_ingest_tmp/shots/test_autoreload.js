const puppeteer = require('puppeteer-core');
const fs = require('fs');
const CHROME = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const APPJS = 'C:\\Unity-Git\\RoomRevive\\HTML_Editor\\admin\\public\\app.js';
const sleep = ms => new Promise(r => setTimeout(r, ms));
(async () => {
  const b = await puppeteer.launch({ executablePath: CHROME, headless: 'new', args: ['--no-sandbox'] });
  const pg = await b.newPage();
  const errs = [];
  pg.on('console', m => { if (m.type() === 'error') errs.push(m.text()); });
  await pg.goto('http://localhost:4173', { waitUntil: 'networkidle0', timeout: 60000 });
  await sleep(5000);                                   // let init run + lastUi capture
  await pg.evaluate(() => { window.__probe = 'alive'; });
  const before = await pg.evaluate(() => window.__probe);
  const now = new Date();
  fs.utimesSync(APPJS, now, now);                      // simulate "I edited app.js"
  await sleep(6500);                                   // poll fires (4s) → should reload
  const after = await pg.evaluate(() => window.__probe);
  console.log('probe before =', before, '| after =', after, '| AUTO-RELOADED =', (after == null));
  console.log('console errors =', errs.length, errs.slice(0, 3));
  await b.close();
})().catch(e => { console.error('ERR', e.stack || e.message); process.exit(1); });
