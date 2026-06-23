const puppeteer = require('puppeteer-core');
const path = require('path');
const CHROME = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const sleep = ms => new Promise(r => setTimeout(r, ms));
(async () => {
  const browser = await puppeteer.launch({
    executablePath: CHROME, headless: 'new',
    args: ['--no-sandbox', '--ignore-gpu-blocklist', '--use-gl=angle', '--use-angle=swiftshader',
           '--enable-unsafe-swiftshader', '--window-size=900,2400'],
  });
  const page = await browser.newPage();
  await page.setViewport({ width: 900, height: 2400, deviceScaleFactor: 1.4 });
  await page.goto('http://localhost:4173', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await sleep(2200);
  await page.evaluate(() => {
    const els = [...document.querySelectorAll('*')].filter(
      e => [...e.childNodes].some(c => c.nodeType === 3 && c.textContent.trim() === 'Kitchens'));
    if (els[0]) els[0].click();
  });
  await sleep(1000);
  await page.evaluate(() => {
    const card = [...document.querySelectorAll('.card')].find(c => c.textContent.includes('TOUCH 337'));
    card.querySelector('.edit').click();
  });
  await sleep(1200);
  // expand the modal so the whole scrollable form is captured
  await page.evaluate(() => {
    const m = document.querySelector('.modal'); if (m) { m.style.maxHeight = 'none'; m.style.height = 'auto'; }
    const c = document.querySelector('.modal .content'); if (c) { c.style.maxHeight = 'none'; c.style.overflow = 'visible'; }
    const ov = document.querySelector('.overlay'); if (ov) { ov.style.position = 'absolute'; ov.style.alignItems = 'flex-start'; }
  });
  await sleep(400);
  const file = path.join(__dirname, 'Kitchen_337_editfull.png');
  await page.screenshot({ path: file, fullPage: true });
  console.log('SHOT', file);
  await browser.close();
})().catch(e => { console.error('ERR', e.stack || e.message); process.exit(1); });
