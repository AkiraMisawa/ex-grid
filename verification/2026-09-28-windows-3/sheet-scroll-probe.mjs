// Beside sheet-vs-excel.spec.mjs items 3 and 5 and active-cell cases 7 and 8, which set the
// Sheet's scrollTop to n × 28 and then expect row n + 1 to be painted: which row is at the top of
// /sheet's view after scrollTop = 99 × 28 and 299 × 28, with the display's own scale (150% here)
// and with Chrome told the scale is 1 (--force-device-scale-factor=1). Also the Layout Ceiling the
// grid was told and the spacer's declared height. Reads only; prints one JSON document.
//
//     node sheet-scroll-probe.mjs http://localhost:5299
import { chromium } from '@playwright/test';

const BASE = process.argv[2] ?? 'http://localhost:5299';
const out = { base: BASE, runs: [] };
for (const scale of [null, 1]) {
    const browser = await chromium.launch({ channel: 'chrome', headless: false, args: scale ? [`--force-device-scale-factor=${scale}`] : [] });
    const page = await browser.newPage();
    await page.goto(`${BASE}/sheet`);
    await page.locator('#demo-interactive').waitFor({ state: 'attached', timeout: 60_000 });
    await page.waitForFunction(() => !document.querySelector('.ex-grid[aria-busy]'), null, { timeout: 60_000 });
    await page.locator(".ex-grid [id$='-r0c0']").first().waitFor();
    await page.waitForTimeout(1000);
    const run = { forcedScale: scale, steps: [] };
    for (const n of [99, 299]) {
        const r = await page.evaluate(async (n) => {
            const root = document.querySelector('.ex-grid');
            const sc = root.querySelector('.ex-scroller');
            sc.scrollTop = n * 28;
            await new Promise((done) => setTimeout(done, 1200));
            const head = root.querySelector('[role=columnheader]');
            const headBottom = head.getBoundingClientRect().bottom;
            let top = null;
            for (const el of sc.querySelectorAll('[id]')) {
                const m = /-r(\d+)c\d+$/.exec(el.id);
                if (!m) continue;
                const b = el.getBoundingClientRect();
                if (b.height > 0 && b.bottom > headBottom + 0.5 && (top === null || Number(m[1]) < top)) top = Number(m[1]);
            }
            const spacer = root.querySelector('.ex-spacer');
            return {
                asked: n * 28, scrollTop: sc.scrollTop, scrollHeight: sc.scrollHeight, dpr: window.devicePixelRatio,
                ceiling: root.querySelector('.ex-ceiling-probe > div')?.getBoundingClientRect().height ?? null,
                spacerDeclared: spacer ? Number((/height: ([\d.]+)px/.exec(spacer.getAttribute('style')) ?? [])[1]) : null,
                topRowNumber: top === null ? null : top + 1,
                rowNPlus1Painted: !!sc.querySelector(`[id$='-r${n}c0']`),
            };
        }, n);
        run.steps.push(r);
    }
    out.runs.push(run);
    await browser.close();
}
console.log(JSON.stringify(out, null, 2));
