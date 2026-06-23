const puppeteer = require('puppeteer-core');
const path = require('path');
const CHROME = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const sleep = ms => new Promise(r => setTimeout(r, ms));
(async () => {
  const browser = await puppeteer.launch({
    executablePath: CHROME, headless: 'new',
    args: ['--no-sandbox', '--ignore-gpu-blocklist', '--use-gl=angle', '--use-angle=swiftshader',
           '--enable-unsafe-swiftshader', '--window-size=1440,1400'],
  });
  const page = await browser.newPage();
  await page.setViewport({ width: 1440, height: 1400, deviceScaleFactor: 1 });
  await page.goto('http://localhost:4173', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await sleep(2500);
  const clicked = await page.evaluate(() => {
    const els = [...document.querySelectorAll('*')].filter(
      e => [...e.childNodes].some(c => c.nodeType === 3 && c.textContent.trim() === 'Kitchens'));
    if (els[0]) { els[0].click(); return true; } return false;
  });
  await sleep(2500);
  const file = path.join(__dirname, 'Kitchens.png');
  await page.screenshot({ path: file });
  console.log('SHOT Kitchens clicked=' + clicked, file);
  await browser.close();
})().catch(e => { console.error('ERR', e.stack || e.message); process.exit(1); });
