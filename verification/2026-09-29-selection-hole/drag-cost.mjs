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
//
// Extended for ADR-0206 (2026-10-11, verification/2026-10-11-linux-pinned-shade): EXGRID_PIN, if
// set, is /sheet's ?pin= (how many leading columns are pinned; the page's own is 1), and
// EXGRID_CHROME=mud runs the Sheet under ExSheet.MudBlazor's Chrome (?chrome=mud). The wait reads
// the tallest range of the Sheet in either layer, so a range that is wholly pinned is waited on
// too. EXGRID_EXECUTABLE, if set, is the Chromium to launch in place of the installed Chrome, and
// EXGRID_HEADED=1 runs it headed (under xvfb on a machine without a display). The page is scrolled
// to put the Sheet at the window's top first.
const { chromium } = await import(process.env.EXGRID_PLAYWRIGHT
    ?? new URL('../../tests/ExGrid.Browser/node_modules/playwright/index.mjs', import.meta.url).href);

const [base, label, drags = '5', start = 'r2c1'] = process.argv.slice(2);
const browser = await chromium.launch(process.env.EXGRID_EXECUTABLE
    ? { executablePath: process.env.EXGRID_EXECUTABLE, headless: process.env.EXGRID_HEADED !== '1', ignoreDefaultArgs: ['--hide-scrollbars'] }
    : { channel: 'chrome', headless: process.env.EXGRID_HEADED !== '1' });
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

const query = [
    process.env.EXGRID_PIN ? `pin=${process.env.EXGRID_PIN}` : null,
    process.env.EXGRID_CHROME === 'mud' ? 'chrome=mud' : null,
].filter(Boolean).join('&');
await page.goto(`${base}/sheet${query ? `?${query}` : ''}`);
await page.locator('#demo-interactive').waitFor({ state: 'attached' });
await page.waitForFunction(() => !document.querySelector('.ex-grid[aria-busy]'));
if (process.env.EXGRID_INJECT) {
    await page.addStyleTag({ content: process.env.EXGRID_INJECT });
}
const grid = page.locator('.ex-grid').first();
// The Sheet at the top of the window, so the ten rows a drag moves down stay inside its Viewport
// wherever the host's page puts it (the WebAssembly DemoHost's /sheet stands lower than the Server
// host's; a move past the Viewport's edge does not extend the range, and the wait never ends).
await grid.evaluate((root) => root.scrollIntoView({ block: 'start' }));
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
        await page.waitForFunction((h) => [...document.querySelector('.ex-grid').querySelectorAll('.ex-selection .ex-range, .ex-selection-pinned .ex-range')]
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
console.log(JSON.stringify({ base, label, start, query, inject: process.env.EXGRID_INJECT ?? '', chrome: browser.version(), rows, problems }));
await browser.close();
