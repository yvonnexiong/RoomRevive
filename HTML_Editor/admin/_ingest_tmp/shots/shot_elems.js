const puppeteer = require('puppeteer-core');
const path = require('path');
const CHROME = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const sleep = ms => new Promise(r => setTimeout(r, ms));
const NAME = process.argv[2], OUT = process.argv[3];
(async () => {
  const b = await puppeteer.launch({ executablePath: CHROME, headless: 'new',
    args: ['--no-sandbox', '--ignore-gpu-blocklist', '--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--window-size=1100,1700'] });
  const pg = await b.newPage();
  await pg.setViewport({ width: 1100, height: 1700, deviceScaleFactor: 2 });
  await pg.goto('http://localhost:4173', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await sleep(2200);
  await pg.evaluate(() => { const e = [...document.querySelectorAll('*')].filter(x => [...x.childNodes].some(c => c.nodeType === 3 && c.textContent.trim() === 'Kitchens')); if (e[0]) e[0].click(); });
  await sleep(1200);
  await pg.evaluate(nm => { const c = [...document.querySelectorAll('.card')].find(x => x.textContent.includes(nm)); c.querySelector('.details').click(); }, NAME);
  await sleep(2200);
  const box = await pg.evaluate(() => { const els = document.querySelector('.kp-els'); const hero = document.querySelector('.kp-hero'); const a = hero.getBoundingClientRect(), b = els.getBoundingClientRect(); return { x: a.left - 12, y: a.top - 8, w: a.width + 24, h: (b.bottom - a.top) + 16 }; });
  await pg.screenshot({ path: path.join(__dirname, OUT), clip: { x: box.x, y: box.y, width: box.w, height: box.h } });
  console.log('SHOT', OUT); await b.close();
})().catch(e => { console.error('ERR', e.stack || e.message); process.exit(1); });
