// The main-thread CPU time of a selecting drag on /sheet, read from the Chrome DevTools Protocol's
// Performance.getMetrics around it (ADR-0008, 2026-09-29: the hole's cost is measured before it is
// claimed). One headless Chrome, one page, several drags; prints one JSON line with a row per drag.
//
//     node drag-cost.mjs <base URL> <label> <drags> <start cell id suffix>
//
// A drag presses the start cell (r2c1 is B3, r2c0 is A3, which is pinned), then moves one cell at
// a time, ten times: one row down each time and one column right for the first four, so the range
// grows from 2 to 55 cells and holds the Focus in its corner throughout. Each move waits until the
// range's painted height shows it landed, so every drag is ten renders on either host. The metrics
// are thread-tick durations in ms, summed over the ten moves: TaskDuration (all main-thread work),
// ScriptDuration, RecalcStyleDuration and LayoutDuration. EXGRID_INJECT, if set, is a stylesheet
// added to the page first — how the hole or the range's isolation is switched off on the build
// that has them. The wait polls on animation frames, so its own script is in every figure alike.
const { chromium } = await import(process.env.EXGRID_PLAYWRIGHT
    ?? new URL('../../tests/ExGrid.Browser/node_modules/playwright/index.mjs', import.meta.url).href);

const [base, label, drags = '5', start = 'r2c1'] = process.argv.slice(2);
const browser = await chromium.launch({ channel: 'chrome', headless: true });
const context = await browser.newContext({ deviceScaleFactor: 2, viewport: { width: 1400, height: 1000 } });
const page = await context.newPage();
const problems = [];
page.on('console', (m) => { if (m.type() === 'error' || m.type() === 'warning') problems.push(m.text()); });
page.on('pageerror', (e) => problems.push(String(e)));
const cdp = await context.newCDPSession(page);
await cdp.send('Performance.enable', { timeDomain: 'threadTicks' });
const metrics = async () => {
    const { metrics: list } = await cdp.send('Performance.getMetrics');
    const m = Object.fromEntries(list.map((x) => [x.name, x.value]));
    return { task: m.TaskDuration, script: m.ScriptDuration, style: m.RecalcStyleDuration, layout: m.LayoutDuration };
};

await page.goto(`${base}/sheet`);
await page.locator('#demo-interactive').waitFor({ state: 'attached' });
await page.waitForFunction(() => !document.querySelector('.ex-grid[aria-busy]'));
if (process.env.EXGRID_INJECT) {
    await page.addStyleTag({ content: process.env.EXGRID_INJECT });
}
const grid = page.locator('.ex-grid').first();
const rows = [];
for (let drag = 0; drag < Number(drags); drag++) {
    const box = await grid.locator(`[id$='-${start}']`).first().boundingBox();
    const x = box.x + box.width / 2;
    const y = box.y + box.height / 2;
    await page.mouse.move(x, y);
    await page.mouse.down();
    // The move handler attaches with the press's render, a round trip away on the Server host.
    await page.waitForTimeout(300);
    const before = await metrics();
    for (let i = 1; i <= 10; i++) {
        await page.mouse.move(x + Math.min(i, 4) * box.width, y + i * box.height);
        await page.waitForFunction((h) => [...document.querySelector('.ex-grid').querySelectorAll('.ex-selection .ex-range')]
            .some((r) => Math.abs(r.getBoundingClientRect().height - h) <= 2), (i + 1) * box.height, { polling: 'raf' });
    }
    const after = await metrics();
    await page.mouse.up();
    // Collapse onto the cell above the start, ready for the next drag.
    await page.mouse.click(x, y - box.height);
    await page.waitForTimeout(100);
    const ms = (k) => Math.round(1000 * (after[k] - before[k]) * 100) / 100;
    rows.push({ task: ms('task'), script: ms('script'), style: ms('style'), layout: ms('layout') });
}
console.log(JSON.stringify({ base, label, start, inject: process.env.EXGRID_INJECT ?? '', chrome: browser.version(), rows, problems }));
await browser.close();
