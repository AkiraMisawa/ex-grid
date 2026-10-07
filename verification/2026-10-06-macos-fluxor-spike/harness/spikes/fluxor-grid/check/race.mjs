// A race the reducer has to close (check 2, Server): the feed changes the edited cell on its own
// thread every few milliseconds while the user commits over it. The grid refuses a commit over a
// cell whose painted text changed (ADR-0142); a tick reduced after the grid's judgement and before
// the edit's reducer reaches the reducer instead, which refuses the cell — and the grid, which
// has closed its editor, never hears of it.
//   node race.mjs base variant attempts
import { chromium } from 'playwright';
const [base = 'http://localhost:5898', variant = 'w1', attempts = '40'] = process.argv.slice(2);
const KEY_FIELD = ':scope > .ex-scroller > .ex-spacer > .ex-viewport > .ex-key-field-layer > input.ex-key-field';
const browser = await chromium.launch({ channel: 'chrome', headless: true });
const page = await browser.newPage({ viewport: { width: 1300, height: 1000 } });
const problems = [];
page.on('console', (m) => { if (m.type() === 'error' || m.type() === 'warning') problems.push(m.text()); });
const path = { w1: '/w1', w2: '/w2', w2src: '/w2?edits=source' }[variant];
await page.goto(`${base}${path}${path.includes('?') ? '&' : '?'}interval=5&perTick=20&ticks=0&addEvery=0`);
await page.locator('#fg-interactive').waitFor({ state: 'attached', timeout: 60000 });
const grid = page.locator('.ex-grid');
await grid.locator('.ex-row').first().waitFor();
const text = async (id) => ((await page.textContent(`#${id}`)) ?? '').trim();
const until = async (read, what) => { const t = Date.now(); while (Date.now() - t < 20000) { const v = await read(); if (v) return v; await new Promise((r) => setTimeout(r, 10)); } throw new Error(what); };
await page.click('#feed-start');
await until(async () => Number((await text('ticks')).split(' ')[1]) > 5, 'feed');
let gridRefused = 0, landed = 0;
for (let i = 0; i < Number(attempts); i++) {
    const before = await text('commit-refused');
    const writes = await text('last-write');
    await grid.locator("[id$='-r0c6']").click({ force: true });   // Price of T00000, ticked every batch
    await page.keyboard.type(String(200 + i));
    await page.keyboard.press('Enter');
    const outcome = await until(async () => {
        if ((await text('commit-refused')) !== before) return 'grid';
        if ((await text('last-write')) !== writes) return 'store';
        return null;
    }, 'an outcome');
    if (outcome === 'grid') {
        gridRefused++;
        await page.keyboard.press('Escape');
        await until(async () => (await grid.locator('input.ex-editor').count()) === 0, 'editor closed');
    } else landed++;
}
await page.click('#feed-stop');
console.log(JSON.stringify({ variant, attempts: Number(attempts), refusedByGrid: gridRefused, raisedToStore: landed,
    store: await text('last-write'), storeRefused: await text('store-refused'), ticks: await text('ticks'), problems }));
await browser.close();
