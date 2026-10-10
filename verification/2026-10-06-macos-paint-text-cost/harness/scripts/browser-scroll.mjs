// Scripted scrolling on /grid-live-local at 10^6 rows, paused (?step=1: no batch is applied), on a
// published WebAssembly host: per scenario, the main thread's script and task time per step (CDP
// Performance.getMetrics, deltas), the frame intervals while it runs (requestAnimationFrame), and
// every long task (PerformanceObserver, longtask). One step per animation frame.
//
//   node browser-scroll.mjs <baseUrl> <out.json> <label> [runs] [warmup] [live|wide]
//
// live: /grid-live-local?rows=1000000&step=1 (11 columns, 480 px high). wide: /wide, 10^6 rows ×
// 100 columns, both axes virtualised, 600 × 900 — and a horizontal scenario, a new column slice
// every frame (back and forth between two slices less than a Viewport apart, so never a fling).
//
// Scenarios: one row a frame (120 frames); one page a frame, not a fling (60 frames); a fling,
// three pages a frame (60 frames), with the settle after it (Placeholders, ADR-0004).
import { createRequire } from 'node:module';
import fs from 'node:fs';
const require = createRequire('/Users/akira338/github/ex-grid/.claude/worktrees/live-data-next-cc/tests/ExGrid.Browser/package.json');
const { chromium } = require('playwright');

const [base, out, label, runsArg = '5', warmArg = '1', which = 'live'] = process.argv.slice(2);
const RUNS = Number(runsArg);
const WARMUP = Number(warmArg);
const browser = await chromium.launch({ channel: 'chrome', headless: true });
const page = await browser.newPage({ viewport: { width: 1400, height: 1100 } });
const consoleErrors = [];
page.on('console', (m) => { if (m.type() === 'error' || m.type() === 'warning') consoleErrors.push(`${m.type()}: ${m.text().slice(0, 300)}`); });
page.on('pageerror', (e) => consoleErrors.push(`pageerror: ${String(e).slice(0, 300)}`));
await page.addInitScript(() => {
    const p = { longTasks: [] };
    window.__scroll = p;
    try {
        const observer = new PerformanceObserver((list) => { for (const e of list.getEntries()) p.longTasks.push({ start: e.startTime, duration: e.duration }); });
        observer.observe({ type: 'longtask', buffered: true });
        p.flush = () => { for (const e of observer.takeRecords()) p.longTasks.push({ start: e.startTime, duration: e.duration }); };
    } catch {
        p.flush = () => {};
        p.noLongTasks = true;
    }
});
const cdp = await page.context().newCDPSession(page);
await cdp.send('Performance.enable', { timeDomain: 'threadTicks' });
const metrics = async () => Object.fromEntries((await cdp.send('Performance.getMetrics')).metrics.map((m) => [m.name, m.value]));

const t0 = Date.now();
let load;
if (which === 'wide') {
    await page.goto(`${base}/wide`, { timeout: 600_000 });
    await page.locator('.ex-grid .ex-viewport .ex-row').first().waitFor({ timeout: 300_000 });
    load = await page.locator('h1').textContent();
} else {
    await page.goto(`${base}/grid-live-local?rows=1000000&step=1`, { timeout: 600_000 });
    await page.locator('#grid-live-local-made').filter({ hasText: '1,000,000 trades made in' }).waitFor({ timeout: 600_000 });
    await page.locator('.ex-grid .ex-viewport .ex-row').first().waitFor({ timeout: 300_000 });
    load = await page.locator('#grid-live-local-made').textContent();
}
const geometry = await page.evaluate(() => {
    const scroller = document.querySelector('.ex-grid .ex-scroller');
    const rows = [...document.querySelectorAll('.ex-grid .ex-viewport .ex-row')];
    const rowHeight = rows[0].getBoundingClientRect().height;
    return { rowHeight, clientHeight: scroller.clientHeight, scrollHeight: scroller.scrollHeight, clientWidth: scroller.clientWidth, scrollWidth: scroller.scrollWidth, rows: rows.length, cells: rows[0].querySelectorAll('[role=gridcell]').length };
});
console.log(JSON.stringify({ label, loadedIn: (Date.now() - t0) / 1000, load, geometry }));

// A page: the rows that fit, which is never more than RowsPerViewport, so never a fling; a fling
// is three of them.
const page1 = Math.floor((geometry.clientHeight - geometry.rowHeight) / geometry.rowHeight);
const scenarios = [
    { name: 'one row a frame', frames: 120, rows: 1 },
    { name: 'one page a frame', frames: 60, rows: page1 },
    { name: 'fling, three pages a frame, and its settle', frames: 60, rows: 3 * page1, settle: true },
];
if (which === 'wide' && geometry.scrollWidth > geometry.clientWidth * 2)
    scenarios.push({ name: 'a new column slice every frame', frames: 60, rows: 0, sideways: geometry.clientWidth - 150 });

async function scrollRun(scenario, startRow) {
    // Start still: placed, settled, painted.
    await page.evaluate(async ({ top }) => {
        const scroller = document.querySelector('.ex-grid .ex-scroller');
        scroller.scrollTop = top;
        scroller.scrollLeft = 0;
        await new Promise((r) => setTimeout(r, 400));
        await new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r)));
    }, { top: startRow * geometry.rowHeight });
    const before = await metrics();
    const r = await page.evaluate(async ({ frames, step, settle, sideways }) => {
        const scroller = document.querySelector('.ex-grid .ex-scroller');
        const viewport = document.querySelector('.ex-grid .ex-viewport');
        const intervals = [];
        const start = performance.now();
        let last = start;
        for (let i = 0; i < frames; i++) {
            if (sideways)
                scroller.scrollLeft = i % 2 === 0 ? sideways : 0;
            else
                scroller.scrollTop += step;
            await new Promise((resolve) => requestAnimationFrame(resolve));
            const now = performance.now();
            intervals.push(now - last);
            last = now;
        }
        const scrolledAt = performance.now();
        let settledMs = null;
        if (settle) {
            // The settle render: the Placeholders give way to the rows (ADR-0004), 150 ms after the
            // last scroll, on the grid's own timer.
            const placeholders = () => viewport.querySelector('.ex-row[aria-busy="true"]') !== null;
            const deadline = scrolledAt + 3000;
            while (performance.now() < deadline) {
                await new Promise((resolve) => setTimeout(resolve, 5));
                if (!placeholders() && performance.now() - scrolledAt > 150)
                    break;
            }
            await new Promise((resolve) => requestAnimationFrame(resolve));
            settledMs = performance.now() - scrolledAt;
        }
        await new Promise((resolve) => setTimeout(resolve, 300));
        await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
        const end = performance.now();
        const p = window.__scroll;
        p.flush();
        const during = p.longTasks.filter((t) => t.start < end && t.start + t.duration > start);
        const sorted = [...intervals].sort((a, b) => a - b);
        return {
            frames,
            firstRow: viewport.getAttribute('data-ex-first-row'),
            intervalP50: sorted[Math.floor(sorted.length / 2)],
            intervalP95: sorted[Math.floor(sorted.length * 0.95)],
            intervalMax: sorted[sorted.length - 1],
            intervalMean: intervals.reduce((s, v) => s + v, 0) / intervals.length,
            over17: intervals.filter((v) => v > 16.7).length,
            over34: intervals.filter((v) => v > 33.4).length,
            scrollingMs: scrolledAt - start,
            settledMs,
            longTasks: during.length,
            longestTaskMs: during.length ? Math.max(...during.map((t) => t.duration)) : 0,
            longTaskTotalMs: during.reduce((s, t) => s + t.duration, 0),
        };
    }, { frames: scenario.frames, step: scenario.rows * geometry.rowHeight, settle: !!scenario.settle, sideways: scenario.sideways ?? 0 });
    const after = await metrics();
    r.scriptMsPerStep = ((after.ScriptDuration - before.ScriptDuration) * 1000) / scenario.frames;
    r.taskMsPerStep = ((after.TaskDuration - before.TaskDuration) * 1000) / scenario.frames;
    r.scriptMs = (after.ScriptDuration - before.ScriptDuration) * 1000;
    r.taskMs = (after.TaskDuration - before.TaskDuration) * 1000;
    r.layoutMs = (after.LayoutDuration - before.LayoutDuration) * 1000;
    return r;
}

const median = (values) => { const s = [...values].sort((a, b) => a - b); return s[Math.floor(s.length / 2)]; };
const spread = (runs, key) => {
    const values = runs.map((r) => r[key]).filter((v) => typeof v === 'number' && Number.isFinite(v));
    return values.length ? { min: Math.min(...values), median: median(values), max: Math.max(...values), n: values.length } : null;
};

const results = [];
for (const scenario of scenarios) {
    const runs = [];
    for (let run = 0; run < WARMUP + RUNS; run++) {
        const r = await scrollRun(scenario, 100_000);
        if (run >= WARMUP)
            runs.push(r);
    }
    const summary = {
        scenario: scenario.name, rowsPerStep: scenario.rows, frames: scenario.frames,
        scriptMsPerStep: spread(runs, 'scriptMsPerStep'), taskMsPerStep: spread(runs, 'taskMsPerStep'),
        intervalP50: spread(runs, 'intervalP50'), intervalP95: spread(runs, 'intervalP95'), intervalMax: spread(runs, 'intervalMax'),
        over17: spread(runs, 'over17'), over34: spread(runs, 'over34'),
        longestTaskMs: spread(runs, 'longestTaskMs'), longTasks: spread(runs, 'longTasks'), settledMs: spread(runs, 'settledMs'),
        runs,
    };
    console.log(JSON.stringify({ ...summary, runs: undefined }));
    results.push(summary);
}
const record = { label, which, base, at: new Date().toISOString(), load, geometry, warmup: WARMUP, runs: RUNS, consoleErrors, results };
const all = fs.existsSync(out) ? JSON.parse(fs.readFileSync(out, 'utf8')) : [];
all.push(record);
fs.writeFileSync(out, `${JSON.stringify(all, null, 2)}\n`);
console.log(JSON.stringify({ consoleErrors }));
await browser.close();
