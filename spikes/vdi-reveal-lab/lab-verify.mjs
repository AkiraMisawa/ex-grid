// Checks each lab reveal mode does what it says: records, frame by frame after Ctrl+Down, the
// scroll offset, the rows in view and the scroller's opacity, plus every scroll event.
// Usage: node lab-verify.mjs <base url, e.g. http://127.0.0.1:8823/ex-grid-lab/> [modes]
import { createRequire } from 'node:module';
const require = createRequire('/home/akira/src/hobby/ex-grid/tests/ExGrid.Browser/');
const { chromium } = require('playwright-core');

const base = process.argv[2];
const modes = (process.argv[3] ?? 'ABCDEFGHIJ').split('');
const browser = await chromium.launch({ headless: false, channel: 'chrome' });
let failed = false;
for (const mode of modes) {
    const page = await (await browser.newContext({ viewport: { width: 1280, height: 800 } })).newPage();
    const logs = [];
    page.on('console', m => logs.push(`[${m.type()}] ${m.text()}`));
    page.on('pageerror', e => logs.push(`[pageerror] ${e.message}`));
    await page.goto(`${base}showcase/blotter?reveal=${mode}`, { waitUntil: 'load' });
    await page.waitForSelector('.ex-grid .ex-viewport .ex-row', { timeout: 60_000 });
    await page.waitForTimeout(1000);
    const status = await page.locator('.blotter-status').first().textContent();
    const box = await page.locator('.ex-grid .ex-scroller').boundingBox();
    await page.mouse.click(box.x + box.width * 0.6, box.y + 90);
    await page.waitForTimeout(400);
    // A frame recorder: one line per animation frame, and one per scroll event.
    await page.evaluate(() => {
        const s = document.querySelector('.ex-grid > .ex-scroller');
        const v = s.querySelector('.ex-viewport');
        const t0 = performance.now();
        window.__lab = [];
        const inView = () => {
            const r = s.getBoundingClientRect();
            return [...v.querySelectorAll(':scope > .ex-row')]
                .filter(x => { const q = x.getBoundingClientRect(); return q.bottom > r.top + 40 && q.top < r.bottom; }).length;
        };
        const note = (kind) => window.__lab.push(`${String(Math.round(performance.now() - t0)).padStart(4)}ms ${kind.padEnd(6)} top=${Math.round(s.scrollTop)} inView=${inView()} opacity=${s.style.opacity || '-'}`);
        s.addEventListener('scroll', () => note('scroll'), { passive: true });
        const frame = () => { note('frame'); if (performance.now() - t0 < 900) requestAnimationFrame(frame); };
        requestAnimationFrame(frame);
    });
    await page.keyboard.press('Control+ArrowDown');
    await page.waitForTimeout(1100);
    const timeline = await page.evaluate(() => window.__lab);
    const end = await page.evaluate(() => {
        const s = document.querySelector('.ex-grid > .ex-scroller');
        const v = s.querySelector('.ex-viewport');
        const r = s.getBoundingClientRect();
        const rows = [...v.querySelectorAll(':scope > .ex-row')];
        const seen = rows.filter(x => { const q = x.getBoundingClientRect(); return q.bottom > r.top + 40 && q.top < r.bottom; });
        return { top: Math.round(s.scrollTop), inView: seen.length, last: seen.at(-1)?.getAttribute('aria-rowindex'), opacity: s.style.opacity };
    });
    // Collapse identical consecutive frame lines, keep every change.
    const shown = timeline.filter((line, i) => i === 0 || line.slice(5) !== timeline[i - 1].slice(5));
    const modeLog = logs.find(l => l.includes('[ex-grid lab]'));
    const errors = logs.filter(l => /^\[(error|pageerror)\]/.test(l) && !l.includes('404'));
    const ok = end.last === '25000' && end.inView > 0 && end.opacity === '' && errors.length === 0
        && modeLog?.endsWith(`mode ${mode}`) && status.includes(`LAB reveal ${mode}`);
    failed ||= !ok;
    console.log(`== mode ${mode}: ${ok ? 'OK' : 'FAIL'}  console="${modeLog}"  status ends "${status.trim().slice(-14)}"  end=${JSON.stringify(end)}`);
    for (const line of shown) console.log('   ' + line);
    if (errors.length) console.log('   errors:', errors.join(' | '));
    await page.context().close();
}
await browser.close();
process.exit(failed ? 1 : 0);
