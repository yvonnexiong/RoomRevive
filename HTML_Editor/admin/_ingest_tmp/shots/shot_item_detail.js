const puppeteer = require('puppeteer-core');
const path = require('path');
const CHROME = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const sleep = ms => new Promise(r => setTimeout(r, ms));
const CAT = process.argv[2], NAME = process.argv[3], OUT = process.argv[4];
(async () => {
  const b = await puppeteer.launch({ executablePath: CHROME, headless: 'new',
    args: ['--no-sandbox', '--ignore-gpu-blocklist', '--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--window-size=1100,1700'] });
  const pg = await b.newPage();
  await pg.setViewport({ width: 1100, height: 1700, deviceScaleFactor: 1.4 });
  await pg.goto('http://localhost:4173', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await sleep(2200);
  await pg.evaluate(c => { const e = [...document.querySelectorAll('*')].filter(x => [...x.childNodes].some(n => n.nodeType === 3 && n.textContent.trim() === c)); if (e[0]) e[0].click(); }, CAT);
  await sleep(1200);
  const ok = await pg.evaluate(nm => { const c = [...document.querySelectorAll('.card')].find(x => x.textContent.includes(nm)); if (!c) return false; c.querySelector('.details').click(); return true; }, NAME);
  await sleep(2000);
  await pg.evaluate(() => { const c = document.querySelector('.modal .content'); if (c) { c.style.maxHeight = 'none'; c.style.overflow = 'visible'; } const m = document.querySelector('.modal'); if (m) m.style.maxHeight = 'none'; const ov = document.querySelector('.overlay'); if (ov) { ov.style.position = 'absolute'; ov.style.alignItems = 'flex-start'; } });
  await sleep(300);
  await pg.screenshot({ path: path.join(__dirname, OUT), fullPage: true });
  console.log('SHOT', NAME, 'opened=' + ok); await b.close();
})().catch(e => { console.error('ERR', e.stack || e.message); process.exit(1); });
