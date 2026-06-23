const puppeteer = require('puppeteer-core');
const path = require('path');
const CHROME = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const sleep = ms => new Promise(r => setTimeout(r, ms));
const NAME = process.argv[2] || 'FRAME 617';
const OUT = process.argv[3] || 'Kitchen_detail.png';
(async () => {
  const browser = await puppeteer.launch({
    executablePath: CHROME, headless: 'new',
    args: ['--no-sandbox', '--ignore-gpu-blocklist', '--use-gl=angle', '--use-angle=swiftshader',
           '--enable-unsafe-swiftshader', '--window-size=1100,1700'],
  });
  const page = await browser.newPage();
  await page.setViewport({ width: 1100, height: 1700, deviceScaleFactor: 1.4 });
  await page.goto('http://localhost:4173', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await sleep(2200);
  await page.evaluate(() => {
    const els = [...document.querySelectorAll('*')].filter(
      e => [...e.childNodes].some(c => c.nodeType === 3 && c.textContent.trim() === 'Kitchens'));
    if (els[0]) els[0].click();
  });
  await sleep(1200);
  const opened = await page.evaluate((nm) => {
    const card = [...document.querySelectorAll('.card')].find(c => c.textContent.includes(nm));
    if (!card) return false; card.querySelector('.details').click(); return true;
  }, NAME);
  await sleep(2200);
  await page.evaluate(() => { const c = document.querySelector('.modal .content'); if (c) { c.style.maxHeight = 'none'; c.style.overflow = 'visible'; } const m = document.querySelector('.modal'); if (m) m.style.maxHeight = 'none'; const ov = document.querySelector('.overlay'); if (ov) { ov.style.position = 'absolute'; ov.style.alignItems = 'flex-start'; } });
  await sleep(400);
  const file = path.join(__dirname, OUT);
  await page.screenshot({ path: file, fullPage: true });
  console.log('SHOT', NAME, 'opened=' + opened, file);
  await browser.close();
})().catch(e => { console.error('ERR', e.stack || e.message); process.exit(1); });
