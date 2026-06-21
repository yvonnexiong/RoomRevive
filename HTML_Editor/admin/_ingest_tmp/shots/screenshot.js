const puppeteer = require('puppeteer-core');
const path = require('path');
const CHROME = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const OUT = __dirname;
const cats = ['Dishwashers', 'Fridges', 'Cooktops', 'Hoods', 'Microwaves', 'CoffeeMachines'];
const sleep = ms => new Promise(r => setTimeout(r, ms));

(async () => {
  const browser = await puppeteer.launch({
    executablePath: CHROME,
    headless: 'new',
    args: ['--no-sandbox', '--ignore-gpu-blocklist', '--use-gl=angle', '--use-angle=swiftshader',
           '--enable-unsafe-swiftshader', '--window-size=1440,1900'],
  });
  const page = await browser.newPage();
  await page.setViewport({ width: 1440, height: 1900, deviceScaleFactor: 1 });
  await page.goto('http://localhost:4173', { waitUntil: 'domcontentloaded', timeout: 60000 });
  await sleep(3000);

  for (const cat of cats) {
    const clicked = await page.evaluate((name) => {
      const els = [...document.querySelectorAll('*')].filter(
        e => [...e.childNodes].some(c => c.nodeType === 3 && c.textContent.trim() === name));
      if (els[0]) { els[0].click(); return true; }
      return false;
    }, cat);
    await sleep(700);
    await page.evaluate(() => window.scrollTo(0, 0));
    await sleep(3500); // let model-viewers render
    const file = path.join(OUT, cat + '.png');
    await page.screenshot({ path: file });
    console.log('SHOT', cat, 'clicked=' + clicked, file);
  }
  await browser.close();
  console.log('DONE');
})().catch(e => { console.error('ERR', e.stack || e.message); process.exit(1); });
