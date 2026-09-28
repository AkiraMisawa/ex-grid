// The first run's typing-probe-2.mjs (../2026-09-27-windows-excel/typing-probe-2.mjs), for
// verify-on-windows-3.md Part C: on /sheet, click F1, type 1, Enter; F2, 2; F3, 3; F7, 7, with a
// pause between the steps, then read F1:F8 and the Name Box. Changed from the second run's copy in
// three ways, and nothing else:
//
// - TRIALS trials per pause (twelve for this run), not three.
// - Before the first click it waits as tests/ExGrid.Browser/fixtures.mjs does: #demo-interactive
//   attached (the Routes component renders it once RendererInfo.IsInteractive, which on the Server
//   host is once the circuit has connected), then no .ex-grid with aria-busy. The second run's copy
//   waited for the same two, but let the first wait time out silently after 30 s (`.catch`); here a
//   wait that runs out is recorded against the trial, and the trial is not typed.
// - Each trial records how long those waits took from the navigation's end.
//
//     set PAUSES=0,30,60 & set TRIALS=12 & node typing-probe-2.mjs http://localhost:6298
import { chromium } from '@playwright/test';
const hosts = process.argv.slice(2);
const TRIALS = Number(process.env.TRIALS ?? 3);
const browser = await chromium.launch({ channel: process.env.EXGRID_CHANNEL ?? 'chrome', headless: false });
const out = [];
for (const base of hosts) {
    for (const pause of (process.env.PAUSES ?? "0,300").split(",").map(Number)) {
        for (let trial = 1; trial <= TRIALS; trial++) {
            const page = await browser.newPage();
            await page.goto(`${base}/sheet`);
            const t0 = Date.now();
            const waited = {};
            try {
                await page.locator('#demo-interactive').waitFor({ state: 'attached', timeout: 60_000 });
                waited.interactiveMs = Date.now() - t0;
                await page.waitForFunction(() => !document.querySelector('.ex-grid[aria-busy]'), null, { timeout: 60_000 });
                waited.notBusyMs = Date.now() - t0;
            } catch (e) {
                waited.error = String(e).split('\n')[0];
                out.push({ base, pause, trial, waited });
                await page.close();
                continue;
            }
            const grid = page.locator('.ex-grid').first();
            await grid.locator("[id$='-r0c0']").waitFor();
            for (const [row, v] of [[0, '1'], [1, '2'], [2, '3'], [6, '7']]) {
                await grid.locator(`[id$='-r${row}c5']`).click({ force: true });
                if (pause) await page.waitForTimeout(pause);
                await page.keyboard.type(v);
                await page.keyboard.press('Enter');
                if (pause) await page.waitForTimeout(pause);
            }
            await page.waitForTimeout(1500);
            const f = await grid.evaluate((root) => [0, 1, 2, 3, 4, 5, 6, 7].map((r) => root.querySelector(`[id$='-r${r}c5']`)?.textContent.trim()));
            const nameBox = await grid.locator('input.ex-name-box').inputValue();
            out.push({ base, pause, trial, waited, F1_F8: f, nameBox });
            await page.close();
        }
    }
}
await browser.close();
console.log(JSON.stringify(out));
