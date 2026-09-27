// Keys typed into /sheet straight after a click, on the Server host and on WebAssembly: where do
// they land? Found while running sheet-vs-excel.spec.mjs on the Server host, where cells typed
// that way came out empty or wrong. Run on Windows from a copy of tests/ExGrid.Browser:
//
//     node typing-probe.mjs http://localhost:5298 http://localhost:5299
//
// For each host and each pause between the click and the first key (0, 100, 300 ms), it clicks
// E1, types "=F1", presses Enter, and reads back what E1 holds and what the page's other cells
// changed to. Prints one JSON document.
import { chromium } from '@playwright/test';

const hosts = process.argv.slice(2);
const browser = await chromium.launch({ channel: 'chrome', headless: false });
const out = [];
for (const base of hosts) {
    for (const pause of [0, 100, 300]) {
        for (const trial of [1, 2, 3]) {
            const page = await browser.newPage();
            const logs = [];
            page.on('console', (m) => { if (m.type() === 'error' || m.type() === 'warning') logs.push(`${m.type()}: ${m.text()}`); });
            await page.goto(`${base}/sheet`);
            // As the suite's fixture waits on the Server host: interactive, and no grid busy.
            await page.locator('#demo-interactive').waitFor({ state: 'attached' }).catch(() => {});
            await page.waitForFunction(() => !document.querySelector('.ex-grid[aria-busy]'));
            const grid = page.locator('.ex-grid').first();
            await grid.locator("[id$='-r0c0']").waitFor();
            const texts = () => grid.evaluate((root) => Object.fromEntries(
                [...root.querySelectorAll('.ex-cell')].map((c) => [c.id.replace(/^.*-(r\d+c\d+)$/, '$1'), c.textContent.trim()])));
            const before = await texts();
            await grid.locator("[id$='-r0c4']").click({ force: true });
            if (pause) await page.waitForTimeout(pause);
            await page.keyboard.type('=F1');
            await page.keyboard.press('Enter');
            await page.waitForTimeout(1500);
            const after = await texts();
            const changed = Object.fromEntries(Object.entries(after).filter(([k, v]) => before[k] !== v));
            await grid.locator("[id$='-r0c4']").click({ force: true });
            await page.waitForTimeout(300);
            const e1Entry = await grid.locator('input.ex-formula-bar-text').inputValue();
            out.push({ base, pause, trial, e1Entry, changedCells: changed, console: logs });
            await page.close();
        }
    }
}
await browser.close();
console.log(JSON.stringify(out, null, 1));
