// ExPivot's live redraw in the browser, redraw after redraw, no forced collection: /pivot-live-costs
// on a published WebAssembly host, one batch per press (?step=1, one record amended), each redraw
// timed from Apply to the frame that shows the report, as ticket 01's pivot-costs measurement times
// it (measure-live-costs.spec.mjs: the page's own clock, a MutationObserver installed before the
// app's first script, M1's paint stamp). Unlike that measurement, nothing is warm-up: every redraw
// is kept, in order, with the WebAssembly linear memory after it. The OOM loop's own step time is
// beside it for comparison (`loopStyleS`: from the press to the report showing the new value, by
// 50 ms polling, without the loop's forced collection).
//
//   node browser-pivot.mjs <baseUrl> <out.json> <label> [a] [b] [redraws]
import { createRequire } from 'node:module';
import fs from 'node:fs';
const require = createRequire('/Users/akira338/github/ex-grid/.claude/worktrees/live-data-next-cc/tests/ExGrid.Browser/package.json');
const { chromium } = require('playwright');

const [base, out, label, aArg = '1000', bArg = '100', nArg = '15'] = process.argv.slice(2);
const A = Number(aArg);
const B = Number(bArg);
const N = Number(nArg);

function probe() {
    const p = { longTasks: [], watches: [] };
    window.__costs = p;
    const keep = (entries) => { for (const e of entries) p.longTasks.push({ start: e.startTime, duration: e.duration }); };
    p.flush = () => {};
    try {
        const observer = new PerformanceObserver((list) => keep(list.getEntries()));
        observer.observe({ type: 'longtask', buffered: true });
        p.flush = () => keep(observer.takeRecords());
    } catch {
        p.noLongTasks = true;
    }
    const text = (selector) => document.querySelector(selector)?.textContent ?? null;
    p.watch = (name, selector) => {
        const before = text(selector);
        p.watches = p.watches.filter((w) => w.name !== name);
        p.watches.push({ name, holds: () => { const now = text(selector); return now !== null && now !== before; }, changedAt: null, frameAt: null, paintedAt: null });
    };
    p.watchExpect = (name, view, expect) => {
        const armed = text(expect);
        p.watches = p.watches.filter((w) => w.name !== name);
        p.watches.push({ name, holds: () => { const want = text(expect); return want !== null && want !== '' && want !== armed && (text(view) ?? '').includes(want); }, changedAt: null, frameAt: null, paintedAt: null });
    };
    p.seen = (name) => p.watches.find((w) => w.name === name) ?? null;
    new MutationObserver(() => {
        const now = performance.now();
        for (const w of p.watches) {
            if (w.changedAt === null && w.holds()) {
                w.changedAt = now;
                requestAnimationFrame(() => {
                    w.frameAt = performance.now();
                    const channel = new MessageChannel();
                    channel.port1.onmessage = () => { w.paintedAt = performance.now(); };
                    channel.port2.postMessage(0);
                });
            }
        }
    }).observe(document, { subtree: true, childList: true, attributes: true, characterData: true });
    p.tasks = (from, to) => {
        const during = p.longTasks.filter((t) => t.start < to && t.start + t.duration > from);
        return { count: during.length, longestMs: Math.round(Math.max(0, ...during.map((t) => t.duration))), totalMs: Math.round(during.reduce((s, t) => s + t.duration, 0)) };
    };
}

const browser = await chromium.launch({ channel: 'chrome', headless: true });
const page = await browser.newPage({ viewport: { width: 1400, height: 1100 } });
const consoleErrors = [];
let outOfMemory = null;
page.on('console', (m) => {
    if (m.type() === 'error' || m.type() === 'warning') consoleErrors.push(`${m.type()}: ${m.text().slice(0, 300)}`);
    if (outOfMemory === null && /Out of memory|OutOfMemoryException/.test(m.text())) outOfMemory = m.text().split('\n').slice(0, 4).join(' | ');
});
page.on('pageerror', (e) => consoleErrors.push(`pageerror: ${String(e).slice(0, 300)}`));
await page.addInitScript(probe);
await page.goto(`${base}/pivot-live-costs?a=${A}&b=${B}&batch=1&step=1`, { timeout: 600_000 });
await page.locator('#pivot-live-costs-made').filter({ hasText: 'records made in' }).waitFor({ timeout: 600_000 });
const report = '#pivot-live-costs .ex-pivot .ex-pivot-sheet > .ex-grid .ex-viewport';
await page.locator(report).locator('.ex-row').first().waitFor({ timeout: 900_000 });
const load = await page.locator('#pivot-live-costs-made').textContent();
const linear = () => page.evaluate(() => {
    const rt = globalThis.getDotnetRuntime?.(0) ?? globalThis.Blazor?.runtime;
    const heap = rt?.Module?.HEAPU8?.length ?? rt?.localHeapViewU8?.()?.length ?? null;
    return heap == null ? null : Math.round(heap / 1048576);
});
const step = page.locator('#pivot-live-costs-step');
const runs = [];
let lastFrame = null;
for (let i = 1; i <= N && outOfMemory === null; i++) {
    if (lastFrame !== null)
        await page.waitForFunction((at) => performance.now() - at > 400, lastFrame, { polling: 20, timeout: 60_000 });
    await page.evaluate(({ status, view }) => {
        window.__costs.watch('batch', status);
        window.__costs.watchExpect('view', view, '#pivot-live-costs-expect');
    }, { status: '#pivot-live-costs-status', view: report });
    const pressed = Date.now();
    await step.click({ noWaitAfter: true });
    await page.waitForFunction(() => window.__costs.seen('batch').changedAt !== null, null, { polling: 10, timeout: 600_000 });
    await page.waitForFunction(() => window.__costs.seen('view').paintedAt !== null, null, { polling: 20, timeout: 900_000 });
    const loopStyleS = (Date.now() - pressed) / 1000;
    const r = await page.evaluate(() => {
        const p = window.__costs;
        p.flush();
        const batch = p.seen('batch');
        const view = p.seen('view');
        const line = document.querySelector('#pivot-live-costs-status').textContent;
        const applied = Number(/\(exact ([\d.]+)\)/.exec(line)[1]);
        const startedAt = batch.changedAt - applied;
        const tasks = p.tasks(startedAt - 50, view.frameAt);
        return {
            appliedMs: applied,
            applyToReportPaintedMs: Math.round(view.paintedAt - startedAt),
            longestTaskMs: tasks.longestMs,
            longTasksTotalMs: tasks.totalMs,
            frameAt: view.paintedAt,
        };
    });
    r.redraw = i;
    r.loopStyleS = loopStyleS;
    r.linearMB = await linear();
    lastFrame = r.frameAt;
    delete r.frameAt;
    runs.push(r);
    console.log(JSON.stringify(r));
}
const values = (k) => runs.map((r) => r[k]);
const sorted = (a) => [...a].sort((x, y) => x - y);
const stat = (k) => { const s = sorted(values(k)); return s.length ? { min: s[0], median: s[Math.floor(s.length / 2)], max: s[s.length - 1], n: s.length } : null; };
const record = { label, base, at: new Date().toISOString(), a: A, b: B, redraws: N, load, outOfMemory, consoleErrors,
    applyToReportPaintedMs: stat('applyToReportPaintedMs'), appliedMs: stat('appliedMs'), loopStyleS: stat('loopStyleS'), longestTaskMs: stat('longestTaskMs'), runs };
console.log(JSON.stringify({ ...record, runs: undefined }));
const all = fs.existsSync(out) ? JSON.parse(fs.readFileSync(out, 'utf8')) : [];
all.push(record);
fs.writeFileSync(out, `${JSON.stringify(all, null, 2)}\n`);
await browser.close();
