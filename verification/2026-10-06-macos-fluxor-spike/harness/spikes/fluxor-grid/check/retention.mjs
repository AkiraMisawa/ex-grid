// Which page stays reachable after it is left (check 6's aside): from Home, open each page in
// `order` and come back, `visits` times, then collect and count on Home. Pages are followed by weak
// references (Census.Track).   node retention.mjs base order visits
import { chromium } from 'playwright';
const [base = 'http://localhost:5899', order = 'w2,w1', visits = '3'] = process.argv.slice(2);
const browser = await chromium.launch({ channel: 'chrome', headless: true });
const page = await browser.newPage({ viewport: { width: 1300, height: 1000 } });
const problems = [];
page.on('console', (m) => { if (m.type() === 'error' || m.type() === 'warning') problems.push(m.text()); });
await page.goto(`${base}/`);
await page.locator('#home-census-count').waitFor({ timeout: 60000 });
async function census() {
    const before = await page.textContent('#home-census');
    await page.click('#home-census-count');
    await page.waitForFunction((b) => document.querySelector('#home-census').textContent !== b, before);
    return JSON.parse(await page.textContent('#home-census'));
}
for (let visit = 1; visit <= Number(visits); visit++) {
    for (const href of order.split(',')) {
        await page.click(`a[href="${href}"]`);
        await page.locator(href === 'blank' ? '#blank' : '.ex-grid .ex-row').first().waitFor();
        await page.click('#home');
        await page.locator('#home-census-count').waitFor();
    }
    const c = await census();
    console.log(`order ${order}, after visit ${visit}: ${JSON.stringify(c.tracked)} heap ${c.heapBytes}`);
}
console.log('problems', JSON.stringify(problems));
await browser.close();
