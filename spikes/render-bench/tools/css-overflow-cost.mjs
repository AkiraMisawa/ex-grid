// Browser-side frame cost of a CSS-decided #### with no Blazor in the loop
// (docs/research/css-decided-overflow.md). Drives css-overflow/frames.html: every
// (candidate, scenario) pair on a FRESH page, then an accumulation experiment that runs a
// fling and measures idle frames before it, after it, and after a forced garbage
// collection. Spike code.
//
//   xvfb-run -a node tools/css-overflow-cost.mjs [executablePath]
import { createRequire } from 'node:module';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const require = createRequire(path.join(here, '../../../tests/ExGrid.Browser/package.json'));
const { chromium } = require('@playwright/test');
const exe = process.argv[2] ?? '/opt/pw-browsers/chromium';
const base = pathToFileURL(path.join(here, '../css-overflow/frames.html')).href;
const FRAMES = Number(process.env.FRAMES ?? 600);
// EXTRA is appended to the page's query (e.g. "&width=160&pseudo=off"); SCENARIOS narrows
// the scenario list; ACCUMULATION=0 skips the accumulation experiment.
const EXTRA = process.env.EXTRA ?? '';

const stats = (xs) => {
    const s = [...xs].sort((a, b) => a - b);
    const pct = (p) => s[Math.min(s.length - 1, Math.max(0, Math.ceil(p * s.length) - 1))];
    return s.length ? { n: s.length, p50: +pct(0.5).toFixed(2), p95: +pct(0.95).toFixed(2), max: +s[s.length - 1].toFixed(2), mean: +(s.reduce((a, b) => a + b, 0) / s.length).toFixed(2) } : null;
};

const browser = await chromium.launch({ executablePath: exe, headless: false });
const context = await browser.newContext({ viewport: { width: 1920, height: 1000 }, deviceScaleFactor: 1 });
const report = { browser: browser.version(), frames: FRAMES, cells: 800, query: EXTRA, when: new Date().toISOString(), pairs: [], accumulation: [] };
const outDir = path.join(here, '../results/css-overflow');
fs.mkdirSync(outDir, { recursive: true });
const file = path.join(outDir, 'cost-' + (EXTRA ? EXTRA.replace(/[&=]/g, '_').replace(/^_/, '') + '-' : '') + new Date().toISOString().replace(/[:.]/g, '-') + '.json');
// Written after every measurement, so a run cut short still leaves what it measured.
const save = () => fs.writeFileSync(file, JSON.stringify(report, null, 2));
const consoleMessages = [];

async function freshPage(css) {
    const page = await context.newPage();
    page.on('console', (m) => consoleMessages.push(`${css}: ${m.type()}: ${m.text()}`));
    page.on('pageerror', (e) => consoleMessages.push(`${css}: pageerror: ${e.message}`));
    await page.goto(`${base}?css=${css}${EXTRA}`);
    await page.evaluate(() => document.fonts.ready);
    return page;
}

const candidates = (process.env.CANDIDATES ?? 'none,candidate.css,inert,candidate-scroll-state.css').split(',');
const scenarios = (process.env.SCENARIOS ?? 'Idle,ScrollSlow,ScrollFling,ChurnBurst,ChurnTrickle').split(',');

for (const css of candidates) {
    for (const sc of scenarios) {
        const page = await freshPage(css);
        await page.evaluate((sc) => bench.run(sc, 60), sc);
        const r = await page.evaluate(({ sc, n }) => bench.run(sc, n), { sc, n: FRAMES });
        const changedBrowser = r.browser.filter((_, i) => r.changed[i]);
        const row = {
            css, scenario: sc,
            dom: stats(r.dom), browser: stats(r.browser),
            changedFramesBrowser: changedBrowser.length && changedBrowser.length < r.browser.length ? stats(changedBrowser) : null,
            hashed: await page.evaluate(() => bench.hashed()),
            animations: await page.evaluate(() => document.getAnimations().length),
            frames: r.browser.length, cappedByTime: r.cappedByTime,
        };
        report.pairs.push(row);
        save();
        console.log(`${css.padEnd(28)} ${sc.padEnd(13)} frames ${row.frames} browser p50 ${row.browser.p50} p95 ${row.browser.p95} max ${row.browser.max} | dom p50 ${row.dom.p50} | changed p95 ${row.changedFramesBrowser?.p95 ?? '-'} | hashed ${row.hashed} anims ${row.animations}`);
        await page.close();
    }
}

// Accumulation: does the cost of an idle frame depend on how many cells have come and gone?
for (const css of process.env.ACCUMULATION === '0' ? [] : candidates.filter((c) => c !== 'none' && c !== 'inert').concat(['none'])) {
    const page = await freshPage(css);
    const cdp = await context.newCDPSession(page);
    const idle = async () => stats((await page.evaluate((n) => bench.run('Idle', n), 300)).browser);
    const heap = async () => (await cdp.send('Runtime.getHeapUsage')).usedSize;
    const steps = [];
    await page.evaluate(() => bench.run('Idle', 60));
    steps.push({ step: 'idle, fresh page', browser: await idle(), heapBytes: await heap() });
    for (const n of [1, 2, 3]) {
        const f = (await page.evaluate((k) => bench.run('ScrollFling', k), 300)).browser;
        steps.push({ step: `fling #${n} (${f.length} frames, ${f.length * 800} cells replaced)`, browser: stats(f), heapBytes: await heap() });
        steps.push({ step: `idle after fling #${n}`, browser: await idle(), heapBytes: await heap() });
    }
    await cdp.send('HeapProfiler.enable');
    await cdp.send('HeapProfiler.collectGarbage');
    steps.push({ step: 'idle after a forced GC', browser: await idle(), heapBytes: await heap() });
    const fling = stats((await page.evaluate((k) => bench.run('ScrollFling', k), 300)).browser);
    steps.push({ step: 'fling after the forced GC', browser: fling, heapBytes: await heap() });
    report.accumulation.push({ css, steps });
    save();
    for (const s of steps) console.log(`ACC ${css.padEnd(28)} ${s.step.padEnd(44)} p50 ${s.browser.p50} p95 ${s.browser.p95} heap ${(s.heapBytes / 1e6).toFixed(1)}MB`);
    await page.close();
}

report.console = consoleMessages;
await browser.close();
save();
console.log('wrote ' + file);
