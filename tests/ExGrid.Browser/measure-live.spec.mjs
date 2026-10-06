import { test, expect, record } from './fixtures.mjs';
import { SERVER } from './hosting.mjs';

// LV-15 (Definition of Done §31): 1,000 changes to 1,000,000 rows on the in-process page
// /grid-live-local (ADR-0141, D8), from the source's Apply to the frame that shows them, in the
// browser, against PV-21's 0.2 s. Recorded and never gated (§1, AGENTS.md). The criterion asks for
// a published WebAssembly build without AOT, so this runs only when asked for, against a host that
// serves one, which the suite's config then reuses (or with EXGRID_HOSTS naming the published
// hosts):
//
//   EXGRID_MEASURE=live npx playwright test measure-live.spec.mjs --project=chrome
//
// Every time is taken in the page's own clock (performance.now), so the runner's latency is in none
// of it, the way measure-pivot.spec.mjs times PV-21's 1,000 changes on /pivot-live: the page writes
// its status line once Apply has returned, saying how long Apply took, and a MutationObserver
// installed before the app's first script keeps the time of the task that wrote it, and of the
// animation frame after it. On WebAssembly the source shows a batch that comes after a quiet
// interval at once, inside Apply, and the grid renders it inside that same task. A
// PerformanceObserver collects every long task (over 50 ms) from the document's start.

const MEASURE = process.env.EXGRID_MEASURE;
// The page's own count; another only to try the measurement out quickly.
const MILLION = Number(process.env.EXGRID_MEASURE_TRADES ?? 1_000_000);

test.skip(MEASURE !== 'live', 'the measurement runs only with EXGRID_MEASURE=live');
test.skip(MEASURE === 'live' && SERVER, 'LV-15 is measured on WebAssembly: run it without EXGRID_HOSTING=server');

// A document of its own: the measurement starts from a real load, with the probe installed before
// the app's first script.
test.use({ viewport: { width: 1400, height: 1100 }, freshDocument: true, actionTimeout: 60_000 });

const STATUS = '#grid-live-local-status';
const VIEWPORT = '.ex-grid .ex-viewport';

/**
 * The probe, an init script: a watch is a condition on the DOM, checked whenever the DOM changes; it
 * keeps the time of the change that made it hold — at the end of the task that made it — and of the
 * animation frame after it.
 */
function probe() {
    const p = { longTasks: [], watches: [] };
    window.__lv15 = p;
    try {
        new PerformanceObserver((list) => {
            for (const e of list.getEntries()) {
                p.longTasks.push({ start: e.startTime, duration: e.duration });
            }
        }).observe({ type: 'longtask', buffered: true });
    } catch {
        p.noLongTasks = true;
    }
    const text = (selector) => document.querySelector(selector)?.textContent ?? null;
    p.watch = (name, selector) => {
        const before = text(selector);
        p.watches = p.watches.filter((w) => w.name !== name);
        p.watches.push({ name, holds: () => { const now = text(selector); return now !== null && now !== before; }, changedAt: null, frameAt: null });
    };
    p.seen = (name) => p.watches.find((w) => w.name === name) ?? null;
    new MutationObserver(() => {
        const now = performance.now();
        for (const w of p.watches) {
            if (w.changedAt === null && w.holds()) {
                w.changedAt = now;
                requestAnimationFrame(() => { w.frameAt = performance.now(); });
            }
        }
    }).observe(document, { subtree: true, childList: true, attributes: true, characterData: true });
    p.tasks = (from, to) => {
        const during = p.longTasks.filter((t) => t.start < to && t.start + t.duration > from);
        return Math.round(Math.max(0, ...during.map((t) => t.duration)));
    };
    p.taskAt = (at) => p.longTasks.find((t) => t.start <= at && at <= t.start + t.duration + 1) ?? null;
}

const median = (values) => {
    const sorted = [...values].sort((a, b) => a - b);
    return sorted.length === 0 ? null : sorted[Math.floor(sorted.length / 2)];
};

/** The median, the best and the worst of a number over the runs. */
function spread(results, pick) {
    const values = results.map(pick).filter((v) => typeof v === 'number' && Number.isFinite(v));
    return values.length === 0 ? null : { median: median(values), min: Math.min(...values), max: Math.max(...values) };
}

test('LV-15: 1,000 changes applied to a million rows on /grid-live-local, from Apply to the frame that shows them', async ({ page }, testInfo) => {
    test.setTimeout(3_600_000);
    await page.addInitScript(probe);
    await page.goto(`/grid-live-local?rows=${MILLION}&batch=1000`, { timeout: 600_000 });
    await expect(page.locator('#grid-live-local-made')).toContainText(`${MILLION.toLocaleString('en-US')} trades made in`, { timeout: 600_000 });
    await expect(page.locator(VIEWPORT).locator('.ex-row').first()).toBeVisible({ timeout: 300_000 });
    const load = await page.locator('#grid-live-local-made').textContent();
    const toggle = page.locator('#grid-live-local-toggle');
    await toggle.click();
    await expect(toggle).toHaveText("Resume the page's changes");

    const runs = [];
    for (let run = 0; run < 12; run++) {
        // Quiet for longer than the gathering interval, so that the batch is shown at once (ADR-0141).
        await page.waitForTimeout(1_500);
        await page.evaluate(({ status, viewport }) => {
            window.__lv15.watch('batch', status);
            window.__lv15.watch('grid', viewport);
        }, { status: STATUS, viewport: VIEWPORT });
        await toggle.click();
        await page.waitForFunction(() => window.__lv15.seen('batch').changedAt !== null, null, { polling: 10, timeout: 60_000 });
        // One batch: paused again before the next tick, a quarter of a second later.
        await toggle.click();
        await expect(toggle).toHaveText("Resume the page's changes");
        await page.waitForFunction(() => window.__lv15.seen('batch').frameAt !== null && window.__lv15.seen('grid').frameAt !== null,
            null, { polling: 50, timeout: 60_000 });
        await page.waitForTimeout(500);
        runs.push(await page.evaluate((status) => {
            const p = window.__lv15;
            const batch = p.seen('batch');
            const shown = p.seen('grid');
            const line = document.querySelector(status).textContent;
            const applied = Number(/applied in ([\d,]+) ms/.exec(line)[1].replace(/,/g, ''));
            // The batch's task ends with its status line, which it writes once Apply has returned:
            // Apply began `applied` ms before that, and a little more.
            const startedAt = batch.changedAt - applied;
            const task = p.taskAt(batch.changedAt);
            return {
                status: line,
                appliedMs: applied,
                applyToFrameMs: Math.round(shown.frameAt - startedAt),
                gridInTheBatchsFrame: shown.frameAt <= batch.frameAt + 1,
                taskStartToFrameMs: task ? Math.round(shown.frameAt - task.start) : null,
                longestTaskMs: p.tasks(startedAt - 50, shown.frameAt),
            };
        }, STATUS));
    }
    const result = {
        rows: MILLION,
        load,
        runs: runs.length,
        appliedMs: spread(runs, (r) => r.appliedMs),
        applyToFrameMs: spread(runs, (r) => r.applyToFrameMs),
        taskStartToFrameMs: spread(runs, (r) => r.taskStartToFrameMs),
        longestTaskMs: spread(runs, (r) => r.longestTaskMs),
        gridInTheBatchsFrame: runs.filter((r) => r.gridInTheBatchsFrame).length,
        lastStatus: runs.at(-1).status,
        against: 'PV-21: 0.2 s',
    };
    record(testInfo.project.name, { 'LV-15 1,000 changes, /grid-live-local over 1,000,000 rows': result });
    console.log(`LV-15 ${JSON.stringify(result, null, 1)}`);
});
