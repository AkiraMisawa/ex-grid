import { test, expect, record } from './fixtures.mjs';
import { API_URL, SERVER } from './hosting.mjs';
import fs from 'node:fs';

// PV-21 and DA-17 (Definition of Done §29 and §30): what ExPivot and the Snapshot cost over a
// million records in the browser, recorded and never gated (§1, AGENTS.md). The criteria ask for a
// published WebAssembly build without AOT, so this runs only when asked for, against a host that
// serves one and a demo API server holding a million trades, both already listening, which the
// suite's config then reuses:
//
//   EXGRID_MEASURE=pivot EXGRID_MEASURE_CSV=/path/to/trades.csv npx playwright test measure-pivot.spec.mjs
//
// EXGRID_MEASURE_CSV names /pivot-csv's trade export of a million trades, as DemoCsv writes it.
// Every time is taken in the page's own clock (performance.now), so the runner's latency is in
// none of it: from the input, by the event's own time stamp as a capture-phase listener installed
// before the app's first script hears it, to the first animation frame after the DOM showed the
// answer. A PerformanceObserver collects every long task (over 50 ms) from the document's start,
// and each result names the longest one while the answer came, and while the page settled after.

const MEASURE = process.env.EXGRID_MEASURE;
const CSV = process.env.EXGRID_MEASURE_CSV;
// The in-process pages' count; another only to try the measurements out quickly.
const MILLION = Number(process.env.EXGRID_MEASURE_TRADES ?? 1_000_000);

test.skip(MEASURE !== 'pivot', 'the measurements run only with EXGRID_MEASURE=pivot');
test.skip(MEASURE === 'pivot' && SERVER, 'PV-21 is measured on WebAssembly: run it without EXGRID_HOSTING=server');

// A document of its own for every test: each measurement starts from a real load, with the probe
// installed before the app's first script, and no test's million records are another's to collect.
// An action that cannot find its target fails within a minute rather than at the test's hour.
test.use({ viewport: { width: 1400, height: 1100 }, freshDocument: true, actionTimeout: 60_000 });

const REPORT = '.ex-pivot .ex-pivot-sheet > .ex-grid';
const VIEWPORT = `${REPORT} .ex-viewport`;
const DB_STATUS = '#pivot-db-snapshot-status';
const DB_REPORT = `#pivot-db-snapshot ${VIEWPORT}`;
const LIVE_STATUS = '#pivot-live-local-status';
const LIVE_REPORT = `#pivot-live-local ${VIEWPORT}`;

// What a page's load is timed by, watched from before its first script: a watch named in a third
// place holds only once that one has.
const LOAD_WATCHES = {
    '/pivot': [
        ['read', { kind: 'matches', selector: '#pivot-read', pattern: 'trades read into a Snapshot' }],
        ['first report', { kind: 'exists', selector: `${VIEWPORT} .ex-row` }, 'read'],
    ],
    '/pivot-db': [
        ['read', { kind: 'matches', selector: DB_STATUS, pattern: '^Read [\\d,]+ trades' }],
        ['first report', { kind: 'exists', selector: `${DB_REPORT} .ex-row` }, 'read'],
    ],
};

// The answer to a gesture that changes the report's rows: the grid's row count moves.
const ROW_COUNT = { kind: 'attribute-changes', selector: REPORT, name: 'aria-rowcount' };

/**
 * The probe, an init script: what the page is asked to time, in its own clock. A watch is a
 * condition on the DOM, checked whenever the DOM changes: it keeps the time of the change that made
 * it hold — at the end of the task that made it — and of the animation frame after it. A gesture is
 * armed with the answer it waits for; its input is the first one heard after that.
 */
function probe(loadWatches) {
    const p = { longTasks: [], gesture: null, watches: [] };
    window.__pv21 = p;
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
    const condition = (spec) => {
        switch (spec.kind) {
            case 'exists': return () => document.querySelector(spec.selector) !== null;
            case 'matches': {
                const pattern = new RegExp(spec.pattern);
                return () => pattern.test(text(spec.selector) ?? '');
            }
            case 'changes': {
                const before = text(spec.selector);
                return () => {
                    const now = text(spec.selector);
                    return now !== null && now !== before;
                };
            }
            case 'attribute-changes': {
                const read = () => document.querySelector(spec.selector)?.getAttribute(spec.name) ?? null;
                const before = read();
                return () => read() !== before;
            }
            default: throw new Error(`no watch of kind ${spec.kind}`);
        }
    };
    p.check = () => {
        const now = performance.now();
        for (const w of p.watches) {
            if (w.changedAt !== null) {
                continue;
            }
            if (w.after && p.seen(w.after)?.changedAt == null) {
                continue;
            }
            if (w.holds()) {
                w.changedAt = now;
                requestAnimationFrame(() => { w.frameAt = performance.now(); });
            }
        }
    };
    p.watch = (name, spec, after = null) => {
        p.watches = p.watches.filter((w) => w.name !== name);
        p.watches.push({ name, holds: condition(spec), after, changedAt: null, frameAt: null });
        p.check();
    };
    p.seen = (name) => p.watches.find((w) => w.name === name) ?? null;

    p.arm = (answer) => {
        p.gesture = { t0: null, input: null, heardAt: null, firstChange: null, firstFrame: null };
        p.watch('answer', answer);
    };
    const heard = (e) => {
        const g = p.gesture;
        if (g && g.t0 === null) {
            g.t0 = e.timeStamp;
            g.input = e.type;
            g.heardAt = performance.now();
        }
    };
    for (const type of ['pointerdown', 'mousedown', 'keydown', 'click', 'input', 'change']) {
        window.addEventListener(type, heard, { capture: true });
    }
    new MutationObserver(() => {
        const g = p.gesture;
        if (g && g.t0 !== null && g.firstChange === null) {
            g.firstChange = performance.now();
            requestAnimationFrame(() => { g.firstFrame = performance.now(); });
        }
        p.check();
    }).observe(document, { subtree: true, childList: true, attributes: true, characterData: true });

    const round = (ms) => Math.round(ms * 10) / 10;
    // The long tasks that ran in [from, to]: how many, the longest and their total.
    p.tasks = (from, to) => {
        const during = p.longTasks.filter((t) => t.start < to && t.start + t.duration > from);
        return {
            count: during.length,
            longestMs: Math.round(Math.max(0, ...during.map((t) => t.duration))),
            totalMs: Math.round(during.reduce((sum, t) => sum + t.duration, 0)),
        };
    };
    // The long task a moment fell in, if it fell in one.
    p.taskAt = (at) => p.longTasks.find((t) => t.start <= at && at <= t.start + t.duration + 1) ?? null;
    p.result = () => {
        const g = p.gesture;
        const answer = p.seen('answer');
        return {
            input: g.input,
            // How long the input waited for its listener: the page was busy when it came.
            inputDelayMs: round(g.heardAt - g.t0),
            firstVisualAnswerMs: round(g.firstFrame - g.t0),
            answerMs: round(answer.frameAt - g.t0),
            blocked: p.tasks(g.t0, answer.frameAt),
            blockedWhileSettling: p.tasks(g.t0, performance.now()),
        };
    };

    for (const [name, spec, after] of loadWatches[location.pathname] ?? []) {
        p.watch(name, spec, after);
    }
}

const median = (values) => {
    const sorted = [...values].sort((a, b) => a - b);
    return sorted.length === 0 ? null : sorted[Math.floor(sorted.length / 2)];
};

/** The median, the best and the worst of a number over a measurement's runs. */
function spread(results, pick) {
    const values = results.map(pick).filter((v) => typeof v === 'number' && Number.isFinite(v));
    return values.length === 0 ? null : { median: median(values), min: Math.min(...values), max: Math.max(...values) };
}

/** A gesture's runs, summed up. */
const summary = (results) => ({
    runs: results.length,
    firstVisualAnswerMs: spread(results, (r) => r.firstVisualAnswerMs),
    answerMs: spread(results, (r) => r.answerMs),
    longestTaskMs: spread(results, (r) => r.blocked.longestMs),
    longestTaskSettlingMs: spread(results, (r) => r.blockedWhileSettling.longestMs),
    inputDelayMs: spread(results, (r) => r.inputDelayMs),
});

const probed = new WeakSet();

/** A real load of `path`, the probe in place before the app's first script. */
async function openMeasured(page, path) {
    if (!probed.has(page)) {
        await page.addInitScript(probe, LOAD_WATCHES);
        probed.add(page);
    }
    await page.goto(path, { timeout: 600_000 });
}

/** Waits until nothing has run long for half a second: the observer's entries have landed, and so
 *  has the work a gesture leaves after its answer, such as the Items a report filter band lists. */
async function settle(page) {
    await page.waitForFunction(() => {
        const p = window.__pv21;
        const last = Math.max(p.seen('answer')?.frameAt ?? 0, ...p.longTasks.map((t) => t.start + t.duration));
        return performance.now() - last > 500;
    }, null, { polling: 100, timeout: 300_000 });
}

/**
 * One gesture, timed: its target hovered first and left to rest, so that what the pointer's
 * arrival paints is not taken for the answer; then armed with the answer it waits for, acted and
 * settled. Answers the probe's result.
 */
async function timed(page, answer, act, target) {
    if (target) {
        await target.hover();
        await page.waitForTimeout(300);
    }
    await page.evaluate((spec) => window.__pv21.arm(spec), answer);
    await act();
    await page.waitForFunction(() => window.__pv21.seen('answer')?.frameAt != null, null, { polling: 50, timeout: 300_000 });
    await settle(page);
    return page.evaluate(() => window.__pv21.result());
}

const pane = (page) => page.getByRole('region', { name: 'PivotTable Fields' });
const entry = (page, caption) => pane(page).getByRole('button', { name: `Options for ${caption}`, exact: true });
const tick = (page, caption) => pane(page).getByRole('checkbox', { name: caption, exact: true });
const toolbarButton = (page, name) => page.locator('.ex-pivot .ex-pivot-report').getByRole('button', { name, exact: true });
const menuItem = (page, name) => page.getByRole('menu').getByRole('menuitem', { name, exact: true });

/** Opens /pivot over a million trades and waits for its first report: what the load took. */
async function openMillion(page) {
    await openMeasured(page, `/pivot?trades=${MILLION}`);
    await expect(page.locator('#pivot-read')).toContainText(`${MILLION.toLocaleString('en-US')} trades read into a Snapshot`, { timeout: 600_000 });
    await expect(page.locator(VIEWPORT).locator('.ex-row').first()).toBeVisible({ timeout: 300_000 });
    await page.waitForFunction(() => window.__pv21.seen('first report')?.frameAt != null, null, { polling: 100, timeout: 120_000 });
    await page.waitForTimeout(1_000);
    return page.evaluate(() => {
        const p = window.__pv21;
        const read = p.seen('read');
        const report = p.seen('first report');
        // The trades are made and read in the task that renders the page, which ends with the line.
        const task = p.taskAt(read.changedAt);
        return {
            readIntoSnapshotMs: Number(/in ([\d,]+) ms/.exec(document.querySelector('#pivot-read').textContent)[1].replace(/,/g, '')),
            pageTaskMs: task ? Math.round(task.duration) : null,
            firstQuestionMs: Math.round(report.frameAt - read.frameAt),
            firstQuestionLongestTaskMs: p.tasks(read.frameAt, report.frameAt).longestMs,
            firstReportAtMs: Math.round(report.frameAt),
        };
    });
}

test('PV-21/DA-17: a million trades read from objects on /pivot, and the gestures over them', async ({ page }, testInfo) => {
    test.setTimeout(3_600_000);
    const load = await openMillion(page);
    const runs = {};
    const add = (name, result) => (runs[name] ??= []).push(result);
    const report = { kind: 'changes', selector: VIEWPORT };
    const menu = { kind: 'exists', selector: '[role=menu]' };

    for (let round = 0; round < 5; round++) {
        // A field's menu opened, then its Items sorted Z to A and back: laid out from the answer held.
        add('open a field\'s menu', await timed(page, menu, () => entry(page, 'Region').click(), entry(page, 'Region')));
        add('sort Z to A', await timed(page, report, () => menuItem(page, 'Sort Z to A').click(), menuItem(page, 'Sort Z to A')));
        await entry(page, 'Region').click();
        add('sort A to Z', await timed(page, report, () => menuItem(page, 'Sort A to Z').click(), menuItem(page, 'Sort A to Z')));

        // The first outer Item collapsed and expanded from its ± button.
        const toggle = page.locator(`${REPORT} .ex-pivot-toggle`).first();
        add('collapse', await timed(page, report, () => toggle.click(), toggle));
        add('expand', await timed(page, report, () => toggle.click(), toggle));

        // A change of form from the Layout menu, and back.
        const layout = toolbarButton(page, 'Layout');
        add('open the Layout menu', await timed(page, menu, () => layout.click(), layout));
        const tabular = page.getByRole('menu').getByRole('menuitemradio', { name: 'Show in Tabular Form' });
        add('change of form: Tabular', await timed(page, report, () => tabular.click(), tabular));
        await layout.click();
        const compact = page.getByRole('menu').getByRole('menuitemradio', { name: 'Show in Compact Form' });
        add('change of form: Compact', await timed(page, report, () => compact.click(), compact));

        // Changes that need a new question: Book ticked into Rows and unticked again.
        add('new question: tick Book', await timed(page, ROW_COUNT, () => tick(page, 'Book').check(), tick(page, 'Book')));
        add('new question: untick Book', await timed(page, ROW_COUNT, () => tick(page, 'Book').uncheck(), tick(page, 'Book')));

        // And through the report filter band: USD alone, then (All) again.
        const band = toolbarButton(page, 'Filter Currency');
        const panel = page.getByRole('dialog', { name: 'Filter Currency' });
        const ok = panel.getByRole('button', { name: 'OK' });
        add('open the report filter', await timed(page, { kind: 'exists', selector: '[role=dialog]' }, () => band.click(), band));
        await panel.getByRole('checkbox', { name: '(Select All)' }).uncheck();
        await panel.getByRole('checkbox', { name: 'USD', exact: true }).check();
        add('new question: filter to USD', await timed(page, report, () => ok.click(), ok));
        await band.click();
        await panel.getByRole('checkbox', { name: '(Select All)' }).check();
        add('new question: filter to (All)', await timed(page, report, () => ok.click(), ok));
    }

    const entries = {
        'DA-17 objects, browser (/pivot?trades=1000000)': {
            records: MILLION,
            readIntoSnapshotMs: load.readIntoSnapshotMs,
            how: 'PivotSource.From(trades, DemoPivotData.Fields): one synchronous build, timed by the page',
            pageTaskMs: load.pageTaskMs,
        },
        'PV-21 first question at load, /pivot over 1,000,000 trades': {
            answerMs: load.firstQuestionMs,
            longestTaskMs: load.firstQuestionLongestTaskMs,
            firstReportAtMs: load.firstReportAtMs,
        },
        'PV-21 gestures, /pivot over 1,000,000 trades': Object.fromEntries(Object.entries(runs).map(([name, results]) => [name, summary(results)])),
    };
    record(testInfo.project.name, entries);
    console.log(`PV-21 ${JSON.stringify(entries, null, 1)}`);
});

async function moveEntry(page, caption, command) {
    await entry(page, caption).click();
    await menuItem(page, command).click();
    await expect(page.getByRole('menu')).toHaveCount(0);
}

/** Waits until no long task has ended for half a second. */
const quiet = (page) => page.waitForFunction(() => {
    const p = window.__pv21;
    return performance.now() - Math.max(0, ...p.longTasks.map((t) => t.start + t.duration)) > 500;
}, null, { polling: 100, timeout: 300_000 });

/**
 * Each layout built in the pane while Defer Layout Update holds it, then asked once, by Update —
 * four times on one load of /pivot over a million trades, going back to the page's first layout
 * with its Reset between them (the Consumer's layout: Defer Layout Update stays ticked). The first
 * time is the first question of its kind since the load, the code it runs not yet run; the other
 * three are summed up apart from it.
 */
async function questions(page, layouts) {
    await openMillion(page);
    const firstRows = await page.locator(REPORT).getAttribute('aria-rowcount');
    await pane(page).getByRole('checkbox', { name: 'Defer Layout Update' }).check();
    const update = pane(page).getByRole('button', { name: 'Update', exact: true });
    const results = {};
    for (const layout of layouts) {
        const runs = [];
        for (let run = 0; run < 4; run++) {
            console.log(`${layout.name}, run ${run + 1}`);
            await page.locator('#pivot-reset').click();
            await expect(page.locator(REPORT)).toHaveAttribute('aria-rowcount', firstRows, { timeout: 300_000 });
            await expect(page.locator('.ex-pivot-refusal-notice')).toHaveCount(0);
            await quiet(page);
            await layout.build();
            await expect(update).toBeEnabled();
            const result = await timed(page, layout.answer ?? ROW_COUNT, () => update.click(), update);
            result.reportRows = Number(await page.locator(REPORT).getAttribute('aria-rowcount'));
            result.notice = await page.locator('.ex-pivot-refusal-notice').textContent({ timeout: 1_000 }).catch(() => null);
            runs.push(result);
        }
        results[layout.name] = {
            first: { answerMs: runs[0].answerMs, longestTaskMs: runs[0].blocked.longestMs, firstVisualAnswerMs: runs[0].firstVisualAnswerMs },
            ...summary(runs.slice(1)),
            reportRows: runs[0].reportRows,
            notice: runs[0].notice ?? undefined,
        };
    }
    return results;
}

test('PV-21: questions of a thousand to thirty thousand leaves, over a million trades on /pivot', async ({ page }, testInfo) => {
    test.setTimeout(3_600_000);
    // Below the cap's layouts, to see where a question passes 0.3 s: 270 dates, ten books, five
    // products and TRUE or FALSE, a million trades filling every combination.
    const results = await questions(page, [
        {
            name: 'Rows TradeDate; Columns Product: 1,350 combinations',
            build: async () => {
                await tick(page, 'Desk').uncheck();
                await tick(page, 'Region').uncheck();
                await tick(page, 'Trade date').check();
            },
        },
        {
            name: 'Rows TradeDate, Book; Columns Product: 13,500 combinations',
            build: async () => {
                await tick(page, 'Desk').uncheck();
                await tick(page, 'Region').uncheck();
                await tick(page, 'Trade date').check();
                await tick(page, 'Book').check();
            },
        },
        {
            name: 'Rows TradeDate, Book, Confirmed; Columns Product: 27,000 combinations',
            build: async () => {
                await tick(page, 'Desk').uncheck();
                await tick(page, 'Region').uncheck();
                await tick(page, 'Trade date').check();
                await tick(page, 'Book').check();
                await tick(page, 'Confirmed').check();
            },
        },
    ]);
    record(testInfo.project.name, { 'PV-21 questions below the cap, /pivot over 1,000,000 trades': results });
    console.log(`PV-21 below the cap ${JSON.stringify(results, null, 1)}`);
});

test('PV-21: a question near the 200,000-leaf cap, and past it, over a million trades on /pivot', async ({ page }, testInfo) => {
    test.setTimeout(3_600_000);
    // The trades' 270 dates, 49 quantities, 499 notionals, five products, five currencies and three
    // regions are drawn independently, so a million of them fill nearly every combination of a few:
    // these are the combinations, and the leaves are nearly as many.
    const layouts = [
        {
            name: 'Rows TradeDate, Quantity; Columns Product: 66,150 combinations',
            build: async () => {
                await tick(page, 'Desk').uncheck();
                await tick(page, 'Region').uncheck();
                await tick(page, 'Trade date').check();
                await tick(page, 'Quantity').check();
                await moveEntry(page, 'Sum of Quantity', 'Move to Row Labels');
            },
        },
        {
            name: 'Rows TradeDate, Notional: 134,730 combinations',
            build: async () => {
                await tick(page, 'Desk').uncheck();
                await tick(page, 'Region').uncheck();
                await moveEntry(page, 'Product', 'Remove Field');
                await tick(page, 'Trade date').check();
                await tick(page, 'Notional').check();
                await moveEntry(page, 'Sum of Notional', 'Move to Row Labels');
            },
        },
        {
            name: 'Rows TradeDate, Quantity; Columns Product, Region: 198,450 combinations',
            build: async () => {
                await tick(page, 'Desk').uncheck();
                await moveEntry(page, 'Region', 'Move to Column Labels');
                await tick(page, 'Trade date').check();
                await tick(page, 'Quantity').check();
                await moveEntry(page, 'Sum of Quantity', 'Move to Row Labels');
            },
        },
        {
            name: 'Rows TradeDate, Quantity; Columns Product, Currency: 330,750 combinations, refused',
            build: async () => {
                await tick(page, 'Desk').uncheck();
                await tick(page, 'Region').uncheck();
                await moveEntry(page, 'Currency', 'Move to Column Labels');
                await tick(page, 'Trade date').check();
                await tick(page, 'Quantity').check();
                await moveEntry(page, 'Sum of Quantity', 'Move to Row Labels');
            },
            answer: { kind: 'matches', selector: '.ex-pivot-refusal-notice', pattern: 'needs more than 200,000 cells' },
        },
    ];
    const results = await questions(page, layouts);
    record(testInfo.project.name, { 'PV-21 the cap on leaves, /pivot over 1,000,000 trades': results });
    console.log(`PV-21 cap ${JSON.stringify(results, null, 1)}`);
});

test('PV-21: 1,000 changes folded into a million trades on /pivot-live', async ({ page }, testInfo) => {
    test.setTimeout(3_600_000);
    await openMeasured(page, `/pivot-live?trades=${MILLION}&batch=1000`);
    await expect(page.locator(LIVE_REPORT).locator('.ex-row').first()).toBeVisible({ timeout: 600_000 });
    // The server's live updates are its own load on this machine: off before anything is timed.
    const serverToggle = page.locator('#pivot-live-server-toggle');
    await expect(serverToggle).toHaveText("Turn the server's live updates off", { timeout: 120_000 });
    await serverToggle.click();
    await expect(serverToggle).toHaveText("Turn the server's live updates on");
    const localToggle = page.locator('#pivot-live-local-toggle');
    await localToggle.click();
    await expect(localToggle).toHaveText("Resume the page's changes");

    const runs = [];
    for (let run = 0; run < 12; run++) {
        // Quiet for longer than the redraw interval, so that the batch is asked for at once (ADR-0067).
        await page.waitForTimeout(1_500);
        await page.evaluate(({ status, report }) => {
            const p = window.__pv21;
            p.watch('batch', { kind: 'changes', selector: status });
            p.watch('report', { kind: 'changes', selector: report });
        }, { status: LIVE_STATUS, report: LIVE_REPORT });
        await localToggle.click();
        await page.waitForFunction(() => window.__pv21.seen('batch').changedAt !== null, null, { polling: 10, timeout: 60_000 });
        // One batch: paused again before the next tick, a quarter of a second later.
        await localToggle.click();
        await expect(localToggle).toHaveText("Resume the page's changes");
        await page.waitForFunction(() => window.__pv21.seen('batch').frameAt !== null && window.__pv21.seen('report').frameAt !== null,
            null, { polling: 50, timeout: 60_000 });
        await page.waitForTimeout(500);
        runs.push(await page.evaluate((status) => {
            const p = window.__pv21;
            const batch = p.seen('batch');
            const report = p.seen('report');
            const line = document.querySelector(status).textContent;
            const applied = Number(/applied in ([\d,]+) ms/.exec(line)[1].replace(/,/g, ''));
            // The batch's task ends with its status line, which it writes once Apply has returned:
            // Apply began `applied` ms before that, and a little more. The long task holding the
            // line, when the batch ran long enough to be one, says where the whole task began.
            const startedAt = batch.changedAt - applied;
            const task = p.taskAt(batch.changedAt);
            return {
                status: line,
                appliedMs: applied,
                applyToReportFrameMs: Math.round(report.frameAt - startedAt),
                reportInTheBatchsFrame: report.frameAt <= batch.frameAt + 1,
                taskStartToReportFrameMs: task ? Math.round(report.frameAt - task.start) : null,
                longestTaskMs: p.tasks(startedAt - 50, report.frameAt).longestMs,
            };
        }, LIVE_STATUS));
    }
    const result = {
        runs: runs.length,
        appliedMs: spread(runs, (r) => r.appliedMs),
        applyToReportFrameMs: spread(runs, (r) => r.applyToReportFrameMs),
        taskStartToReportFrameMs: spread(runs, (r) => r.taskStartToReportFrameMs),
        longestTaskMs: spread(runs, (r) => r.longestTaskMs),
        reportInTheBatchsFrame: runs.filter((r) => r.reportInTheBatchsFrame).length,
        lastStatus: runs.at(-1).status,
    };
    record(testInfo.project.name, { 'PV-21 1,000 changes, /pivot-live over 1,000,000 trades': result });
    console.log(`PV-21 1,000 changes ${JSON.stringify(result, null, 1)}`);
});

test('DA-17: a million trades read from Arrow on /pivot-db', async ({ page }, testInfo) => {
    test.setTimeout(3_600_000);
    const status = await (await fetch(`${API_URL}/api/status`)).json();
    expect(status.trades, 'the demo API server holds a million trades').toBe(1_000_000);
    await openMeasured(page, '/pivot-db');
    await expect(page.locator(DB_STATUS)).toContainText('Read 1,000,000 trades', { timeout: 300_000 });
    await expect(page.locator(DB_REPORT).locator('.ex-row').first()).toBeVisible({ timeout: 300_000 });
    // The server's pivot answers in SQL beside it: its report is in before anything else is timed.
    await expect(page.locator(`#pivot-db-server ${VIEWPORT} .ex-row`).first()).toBeVisible({ timeout: 300_000 });
    await page.waitForTimeout(2_000);

    // The request is the network's and the server's; the browser's part runs from its last byte to
    // the status line that says the Snapshot is read.
    const reading = (statusLine) => {
        const p = window.__pv21;
        const read = p.seen('read');
        const request = performance.getEntriesByType('resource').filter((e) => e.name.includes('/api/trades.arrows')).at(-1);
        const text = document.querySelector(statusLine).textContent;
        return {
            status: text,
            pageMs: Number(/in ([\d,]+) ms/.exec(text)[1].replace(/,/g, '')),
            requestMs: Math.round(request.responseEnd - request.startTime),
            browserReadMs: Math.round(read.changedAt - request.responseEnd),
            longestTaskMs: p.tasks(request.responseEnd, read.frameAt).longestMs,
            readTaskMs: Math.round(p.taskAt(read.changedAt)?.duration ?? 0),
        };
    };
    const first = await page.evaluate(reading, DB_STATUS);
    first.firstQuestionMs = await page.evaluate(() => Math.round(window.__pv21.seen('first report').frameAt - window.__pv21.seen('read').frameAt));

    // Read again with the page's button: the server serves the stream it built, kept for its version.
    const again = [];
    for (let run = 0; run < 5; run++) {
        await page.evaluate((statusLine) => {
            const p = window.__pv21;
            p.watch('reading', { kind: 'matches', selector: statusLine, pattern: '^Reading' });
            p.watch('read', { kind: 'matches', selector: statusLine, pattern: '^Read [\\d,]+ trades' }, 'reading');
        }, DB_STATUS);
        await page.locator('#pivot-db-reread').click();
        await page.waitForFunction(() => window.__pv21.seen('read')?.frameAt != null, null, { polling: 50, timeout: 300_000 });
        await expect(page.locator('#pivot-db-reread')).toBeEnabled();
        await page.waitForTimeout(3_000);
        again.push(await page.evaluate(reading, DB_STATUS));
    }
    const result = {
        trades: status.trades,
        firstRead: first,
        readAgain: {
            runs: again.length,
            pageMs: spread(again, (r) => r.pageMs),
            requestMs: spread(again, (r) => r.requestMs),
            browserReadMs: spread(again, (r) => r.browserReadMs),
            longestTaskMs: spread(again, (r) => r.longestTaskMs),
        },
    };
    record(testInfo.project.name, { 'DA-17 Arrow, browser (/pivot-db)': result });
    console.log(`DA-17 Arrow ${JSON.stringify(result, null, 1)}`);
});

test('PV-21/DA-17: a CSV of a million rows read on /pivot-csv', async ({ page }, testInfo) => {
    test.setTimeout(3_600_000);
    test.skip(!CSV || !fs.existsSync(CSV), 'EXGRID_MEASURE_CSV names the trade export of a million trades to read');
    const runs = [];
    for (let run = 0; run < 3; run++) {
        await openMeasured(page, '/pivot-csv');
        await expect(page.locator('#csv-status')).toHaveText('Choose a file, or a sample.');
        await page.evaluate(() => {
            const p = window.__pv21;
            p.watch('read', { kind: 'matches', selector: '#csv-status', pattern: 'rows read in' });
            p.watch('first report', { kind: 'exists', selector: '.ex-pivot .ex-pivot-sheet > .ex-grid .ex-viewport .ex-row' }, 'read');
            p.arm({ kind: 'matches', selector: '#csv-status', pattern: 'rows read in' });
        });
        await page.locator('#csv-file').setInputFiles(CSV);
        await page.waitForFunction(() => window.__pv21.seen('first report')?.frameAt != null, null, { polling: 100, timeout: 900_000 });
        await settle(page);
        runs.push(await page.evaluate(() => {
            const p = window.__pv21;
            const g = p.gesture;
            const text = document.querySelector('#csv-status').textContent;
            const read = p.seen('read');
            const report = p.seen('first report');
            return {
                status: text,
                pageSeconds: Number(/in ([\d.]+) s/.exec(text)[1]),
                input: g.input,
                firstVisualAnswerMs: Math.round(g.firstFrame - g.t0),
                readMs: Math.round(read.frameAt - g.t0),
                firstQuestionMs: Math.round(report.frameAt - read.frameAt),
                reportMs: Math.round(report.frameAt - g.t0),
                blockedWhileReading: p.tasks(g.t0, read.frameAt),
                blockedUntilReport: p.tasks(g.t0, report.frameAt),
            };
        }));
    }
    const result = {
        rows: 1_000_000,
        fileMiB: Math.round((fs.statSync(CSV).size / 1048576) * 10) / 10,
        runs: runs.length,
        pageSeconds: spread(runs, (r) => r.pageSeconds),
        readMs: spread(runs, (r) => r.readMs),
        firstQuestionMs: spread(runs, (r) => r.firstQuestionMs),
        reportMs: spread(runs, (r) => r.reportMs),
        firstVisualAnswerMs: spread(runs, (r) => r.firstVisualAnswerMs),
        longestTaskWhileReadingMs: spread(runs, (r) => r.blockedWhileReading.longestMs),
        longestTaskUntilReportMs: spread(runs, (r) => r.blockedUntilReport.longestMs),
        lastStatus: runs.at(-1).status,
    };
    record(testInfo.project.name, { 'PV-21/DA-17 CSV of 1,000,000 rows, browser (/pivot-csv)': result });
    console.log(`PV-21 CSV ${JSON.stringify(result, null, 1)}`);
});

// The file above reaches the reader through InputFile's stream, from JavaScript; the page's own
// sample is written in memory and read from a MemoryStream. A tenth of the rows, so the two say
// how much of the file's time is the reading itself.
test('DA-17: the trade export\'s 100,000-trade sample read from memory on /pivot-csv', async ({ page }, testInfo) => {
    test.setTimeout(3_600_000);
    const runs = [];
    for (let run = 0; run < 3; run++) {
        await openMeasured(page, '/pivot-csv');
        await page.locator('#csv-sample-large').click();
        await expect(page.locator('#csv-status')).toContainText('100,000 rows read in', { timeout: 600_000 });
        runs.push(Number(/in ([\d.]+) s/.exec(await page.locator('#csv-status').textContent())[1]));
    }
    const result = { rows: 100_000, from: 'a MemoryStream (the page\'s "100,000 trades" sample)', pageSeconds: spread(runs, (s) => s) };
    record(testInfo.project.name, { 'DA-17 CSV of 100,000 rows from memory, browser (/pivot-csv)': result });
    console.log(`DA-17 CSV sample ${JSON.stringify(result, null, 1)}`);
});
