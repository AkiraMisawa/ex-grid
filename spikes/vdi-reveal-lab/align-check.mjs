// L must place every row exactly where A does: for the same offsets, each visible row's index and
// its top relative to the scroller, and the Focus outline's box, compared mode by mode.
// Usage: node align-check.mjs <site base url> [modes, default AL]
import { createRequire } from 'node:module';
const require = createRequire('/home/akira/src/hobby/ex-grid/tests/ExGrid.Browser/');
const { chromium } = require('playwright-core');
const [base, modes = 'AL'] = process.argv.slice(2);
const browser = await chromium.launch({ headless: false, channel: 'chrome' });
const results = {};
for (const mode of modes) {
    const page = await browser.newPage({ viewport: { width: 1280, height: 800 } });
    await page.goto(`${base}showcase/blotter?reveal=${mode}`, { waitUntil: 'load' });
    await page.waitForSelector('.ex-grid .ex-row', { timeout: 60000 });
    await page.waitForTimeout(800);
    await page.getByRole('button', { name: /Pause/ }).first().click().catch(() => {});
    const box = await page.locator('.ex-grid .ex-scroller').boundingBox();
    await page.mouse.click(box.x + box.width * 0.4, box.y + 90);
    const snap = () => page.evaluate(() => {
        const s = document.querySelector('.ex-grid > .ex-scroller'), r = s.getBoundingClientRect();
        const rows = [...s.querySelectorAll('.ex-viewport > .ex-row')]
            .map(x => [x.getAttribute('aria-rowindex'), Math.round((x.getBoundingClientRect().top - r.top) * 100) / 100])
            .filter(([, t]) => t > -20 && t < r.height);
        const f = s.querySelector('.ex-focus')?.getBoundingClientRect();
        return JSON.stringify({ top: Math.round(s.scrollTop), rows, focus: f && [Math.round((f.top - r.top) * 100) / 100, Math.round(f.left - r.left)] });
    });
    const out = [];
    for (const step of ['Control+ArrowDown', 'PageUp', 'Control+ArrowUp', 'PageDown', 'set 12345', 'ArrowDown']) {
        if (step.startsWith('set')) {
            await page.evaluate((v) => { document.querySelector('.ex-grid > .ex-scroller').scrollTop = v; }, Number(step.slice(4)));
        } else {
            await page.keyboard.press(step);
        }
        await page.waitForTimeout(700);
        out.push(`${step}: ${await snap()}`);
    }
    results[mode] = out;
    await page.close();
}
await browser.close();
const [first, ...rest] = modes;
for (const mode of rest) {
    const same = results[mode].every((line, i) => line === results[first][i]);
    console.log(`${mode} vs ${first}: ${same ? 'IDENTICAL rows, offsets and Focus box at every step' : 'DIFFERENT'}`);
    if (!same) results[mode].forEach((line, i) => { if (line !== results[first][i]) console.log(`  ${first}: ${results[first][i].slice(0, 300)}\n  ${mode}: ${line.slice(0, 300)}`); });
}
