const puppeteer = require('puppeteer-core');
const path = require('path');
const CHROME = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const sleep = ms => new Promise(r => setTimeout(r, ms));
(async () => {
  const browser = await puppeteer.launch({
    executablePath: CHROME, headless: 'new',
    args: ['--no-sandbox', '--ignore-gpu-blocklist', '--use-gl=angle', '--use-angle=swiftshader',
           '--enable-unsafe-swiftshader', '--window-size=1100,1700'],
  });
  const page = await browser.newPage();
  await page.setViewport({ width: 1100, height: 1700, deviceScaleFactor: 1 });
  await page.goto('http://localhost:4173', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await sleep(2200);
  await page.evaluate(() => {
    const els = [...document.querySelectorAll('*')].filter(
      e => [...e.childNodes].some(c => c.nodeType === 3 && c.textContent.trim() === 'Kitchens'));
    if (els[0]) els[0].click();
  });
  await sleep(1500);
  const opened = await page.evaluate(() => {
    const card = [...document.querySelectorAll('.card')].find(c => c.textContent.includes('TOUCH 337'));
    if (!card) return false;
    const e = card.querySelector('.edit'); if (!e) return false; e.click(); return true;
  });
  await sleep(1800);
  const file = path.join(__dirname, 'Kitchen_337_edit.png');
  await page.screenshot({ path: file, fullPage: true });
  console.log('SHOT edit opened=' + opened, file);
  await browser.close();
})().catch(e => { console.error('ERR', e.stack || e.message); process.exit(1); });
