// Which collection gives the accumulated cost back (docs/research/css-decided-overflow.md, §3b)?
// The Blazor bench ran `window.gc()` before every scenario and the timeline mode's idle frames
// still cost ~200 ms after a fling; the plain-DOM harness recovered after CDP's
// HeapProfiler.collectGarbage. This puts the two side by side on frames.html. Spike code.
//
//   xvfb-run -a node tools/css-overflow-gc.mjs
import { createRequire } from 'node:module';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const require = createRequire(path.join(here, '../../../tests/ExGrid.Browser/package.json'));
const { chromium } = require('@playwright/test');
const base = pathToFileURL(path.join(here, '../css-overflow/frames.html')).href;
const p50 = (xs) => { const s = [...xs].sort((a, b) => a - b); return +s[Math.ceil(0.5 * s.length) - 1].toFixed(2); };

const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium', headless: false, args: ['--js-flags=--expose-gc'] });
const context = await browser.newContext({ viewport: { width: 1920, height: 1000 }, deviceScaleFactor: 1 });
const report = { browser: browser.version(), when: new Date().toISOString(), runs: [] };
for (const css of ['candidate.css', 'candidate-scroll-state.css']) {
    const page = await context.newPage();
    await page.goto(`${base}?css=${css}`);
    await page.evaluate(() => document.fonts.ready);
    const cdp = await context.newCDPSession(page);
    const idle = async () => p50((await page.evaluate(() => bench.run('Idle', 200))).browser);
    const steps = [];
    steps.push(['fresh page', await idle()]);
    const f = await page.evaluate(() => bench.run('ScrollFling', 300, 15000));
    steps.push([`after a fling (${f.browser.length} frames)`, await idle()]);
    await page.evaluate(() => window.gc());
    steps.push(['after window.gc()', await idle()]);
    await page.evaluate(async () => { for (let i = 0; i < 3; i++) { window.gc(); await new Promise((r) => setTimeout(r, 500)); } });
    steps.push(['after window.gc() ×3, 1.5s apart', await idle()]);
    await cdp.send('HeapProfiler.enable');
    await cdp.send('HeapProfiler.collectGarbage');
    steps.push(['after CDP HeapProfiler.collectGarbage', await idle()]);
    report.runs.push({ css, idleP50ByStep: Object.fromEntries(steps) });
    for (const [s, v] of steps) console.log(`${css.padEnd(28)} ${s.padEnd(40)} idle p50 ${v}`);
    await page.close();
}
await browser.close();
const file = path.join(here, '../results/css-overflow', 'gc-' + new Date().toISOString().replace(/[:.]/g, '-') + '.json');
fs.writeFileSync(file, JSON.stringify(report, null, 2));
console.log('wrote ' + file);
