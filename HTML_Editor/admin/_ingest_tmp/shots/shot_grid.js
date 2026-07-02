const puppeteer = require('puppeteer-core');
const path = require('path');
const CHROME = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const sleep = ms => new Promise(r => setTimeout(r, ms));
const CAT = process.argv[2], OUT = process.argv[3] || (CAT + '.png');
(async () => {
  const b = await puppeteer.launch({ executablePath: CHROME, headless: 'new',
    args: ['--no-sandbox', '--ignore-gpu-blocklist', '--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--window-size=1440,1500'] });
  const pg = await b.newPage();
  await pg.setViewport({ width: 1440, height: 1500, deviceScaleFactor: 1 });
  await pg.goto('http://localhost:4173', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await sleep(2500);
  await pg.evaluate(c => { const e = [...document.querySelectorAll('*')].filter(x => [...x.childNodes].some(n => n.nodeType === 3 && n.textContent.trim() === c)); if (e[0]) e[0].click(); }, CAT);
  await sleep(4000); // let model-viewers render
  await pg.evaluate(() => window.scrollTo(0, 0));
  await sleep(500);
  await pg.screenshot({ path: path.join(__dirname, OUT) });
  console.log('SHOT', CAT); await b.close();
})().catch(e => { console.error('ERR', e.stack || e.message); process.exit(1); });
