// A live update on /grid-live-local, WebAssembly: Apply to the painted frame. A standalone port of
// ticket 01's harness (measure-live-costs.spec.mjs, EXGRID_MEASURE=live-costs): the same probe —
// a MutationObserver installed before the app's first script, the frame's animation callback, and
// the first task after that frame (M1's paint stamp) — the same flow (resume, see the batch's
// status change, pause), and the same readings. Without the layer-3 fixture, so it runs from a
// scratch directory against a host on this agent's own port.
//
//   node browser-live.mjs <baseUrl> <out.json> <label> [rows] [batch] [runs] [warmup]
import { createRequire } from 'node:module';
import fs from 'node:fs';
const require = createRequire('/Users/akira338/github/ex-grid/.claude/worktrees/live-data-next-cc/tests/ExGrid.Browser/package.json');
const { chromium } = require('playwright');

const [base, out, label, rowsArg = '1000000', batchArg = '1000', runsArg = '15', warmArg = '3'] = process.argv.slice(2);
const ROWS = Number(rowsArg);
const BATCH = Number(batchArg);
const RUNS = Number(runsArg);
const WARMUP = Number(warmArg);

function probe() {
    const p = { longTasks: [], watches: [] };
    window.__costs = p;
    const keep = (entries) => {
        for (const e of entries) {
            p.longTasks.push({ start: e.startTime, duration: e.duration });
        }
    };
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
    p.taskAt = (at) => p.longTasks.find((t) => t.start <= at && at <= t.start + t.duration + 1) ?? null;
}

const median = (values) => { const s = [...values].sort((a, b) => a - b); return s.length === 0 ? null : s[Math.floor(s.length / 2)]; };
const spread = (results, pick) => {
    const values = results.map(pick).filter((v) => typeof v === 'number' && Number.isFinite(v));
    return values.length === 0 ? null : { median: median(values), min: Math.min(...values), max: Math.max(...values), n: values.length };
};
const until = async (fn, what, timeout = 600_000) => {
    const start = Date.now();
    while (!(await fn())) {
        if (Date.now() - start > timeout)
            throw new Error(`never reached: ${what}`);
        await new Promise((r) => setTimeout(r, 10));
    }
};

const browser = await chromium.launch({ channel: 'chrome', headless: true });
const page = await browser.newPage({ viewport: { width: 1400, height: 1100 } });
const consoleErrors = [];
page.on('console', (m) => { if (m.type() === 'error' || m.type() === 'warning') consoleErrors.push(`${m.type()}: ${m.text().slice(0, 300)}`); });
page.on('pageerror', (e) => consoleErrors.push(`pageerror: ${String(e).slice(0, 300)}`));
await page.addInitScript(probe);
await page.goto(`${base}/grid-live-local?rows=${ROWS}&batch=${BATCH}&interval=0`, { timeout: 600_000 });
await page.locator('#grid-live-local-made').filter({ hasText: `${ROWS.toLocaleString('en-US')} trades made in` }).waitFor({ timeout: 600_000 });
const viewport = '.ex-grid .ex-viewport';
await page.locator(viewport).locator('.ex-row').first().waitFor({ timeout: 300_000 });
const load = await page.locator('#grid-live-local-made').textContent();
const toggle = page.locator('#grid-live-local-toggle');
await toggle.click();
await until(async () => (await toggle.textContent()) === "Resume the page's changes", 'paused');

const runs = [];
for (let run = 0; run < WARMUP + RUNS; run++) {
    await page.evaluate(({ status, view }) => {
        window.__costs.watch('batch', status);
        window.__costs.watch('view', view);
    }, { status: '#grid-live-local-status', view: viewport });
    await toggle.click();
    await page.waitForFunction(() => window.__costs.seen('batch').changedAt !== null, null, { polling: 10, timeout: 600_000 });
    await toggle.click();
    await until(async () => (await toggle.textContent()) === "Resume the page's changes", 'paused again');
    await page.waitForFunction(() => window.__costs.seen('batch').paintedAt !== null, null, { polling: 20, timeout: 600_000 });
    const gridChanged = await page.evaluate(() => window.__costs.seen('view').changedAt !== null);
    if (gridChanged)
        await page.waitForFunction(() => window.__costs.seen('view').paintedAt !== null, null, { polling: 20, timeout: 600_000 });
    const r = await page.evaluate(() => {
        const p = window.__costs;
        p.flush();
        const batch = p.seen('batch');
        const view = p.seen('view');
        const line = document.querySelector('#grid-live-local-status').textContent;
        const applied = Number(/\(exact ([\d.]+)\)/.exec(line)[1]);
        const startedAt = batch.changedAt - applied;
        const frameAt = view.frameAt ?? batch.frameAt;
        const paintedAt = view.paintedAt ?? batch.paintedAt;
        const task = p.taskAt(batch.changedAt);
        return {
            status: line,
            appliedMs: applied,
            applyToFrameMs: Math.round((frameAt - startedAt) * 10) / 10,
            applyToPaintedMs: Math.round((paintedAt - startedAt) * 10) / 10,
            frameToPaintedMs: Math.round((paintedAt - frameAt) * 10) / 10,
            endOfTaskToFrameMs: Math.round((frameAt - batch.changedAt) * 10) / 10,
            gridChanged: view.changedAt !== null,
            taskStartToFrameMs: task ? Math.round(frameAt - task.start) : null,
            longestTaskMs: p.tasks(startedAt - 50, frameAt).longestMs,
        };
    });
    if (run >= WARMUP)
        runs.push(r);
}
const record = {
    label, base, at: new Date().toISOString(), rows: ROWS, batch: BATCH, load, warmup: WARMUP,
    appliedMs: spread(runs, (r) => r.appliedMs),
    applyToFrameMs: spread(runs, (r) => r.applyToFrameMs),
    applyToPaintedMs: spread(runs, (r) => r.applyToPaintedMs),
    frameToPaintedMs: spread(runs, (r) => r.frameToPaintedMs),
    endOfTaskToFrameMs: spread(runs, (r) => r.endOfTaskToFrameMs),
    taskStartToFrameMs: spread(runs, (r) => r.taskStartToFrameMs),
    longestTaskMs: spread(runs, (r) => r.longestTaskMs),
    gridChanged: runs.filter((r) => r.gridChanged).length,
    consoleErrors,
    runs,
};
console.log(JSON.stringify({ ...record, runs: undefined }, null, 1));
const all = fs.existsSync(out) ? JSON.parse(fs.readFileSync(out, 'utf8')) : [];
all.push(record);
fs.writeFileSync(out, `${JSON.stringify(all, null, 2)}\n`);
await browser.close();
