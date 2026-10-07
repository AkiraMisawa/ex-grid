import { test, expect } from './fixtures.mjs';
import { HOST_PORT, SERVER } from './hosting.mjs';
import fs from 'node:fs';

// Harness for ticket 01 of docs/specs/live-data, repeated by ticket 13 (never committed): what one live update costs in the
// browser, at the sizes the ticket asks for, on a published build. Three measurements, each run only
// when asked for:
//
//   EXGRID_MEASURE=live-costs   /grid-live-local on WebAssembly: Apply to the frame, measure-live's
//                               method (LV-15), at EXGRID_COSTS_ROWS rows and EXGRID_COSTS_BATCH
//                               changes; with EXGRID_COSTS_PROBE=1 the page also times the source
//                               alone and the grid's two checks of a pushed Window (ticket 02).
//   EXGRID_MEASURE=live-bytes   /grid-live-local on the Server host, one batch per press (?step=1):
//                               each press's render batch as Chrome reports it, and the bytes Kestrel
//                               wrote to the circuit's connection (M2's method, GET /api/wire).
//   EXGRID_MEASURE=pivot-costs  /pivot-live-costs on WebAssembly: Apply to the frame that shows the
//                               report, at EXGRID_COSTS_A × EXGRID_COSTS_B leaves.
//
// Every time is the page's own (performance.now). Each result is appended to EXGRID_COSTS_OUT.

const MEASURE = process.env.EXGRID_MEASURE;
const ROWS = Number(process.env.EXGRID_COSTS_ROWS ?? 1_000_000);
const BATCH = Number(process.env.EXGRID_COSTS_BATCH ?? 1000);
const RUNS = Number(process.env.EXGRID_COSTS_RUNS ?? 15);
const WARMUP = Number(process.env.EXGRID_COSTS_WARMUP ?? 3);
const PROBE = process.env.EXGRID_COSTS_PROBE === '1';
// Ticket 13: the source's Window pushed by the page (?push=1), vouched for or not (?vouch=1).
const PUSH = process.env.EXGRID_COSTS_PUSH === '1';
const VOUCH = process.env.EXGRID_COSTS_VOUCH === '1';
const A = Number(process.env.EXGRID_COSTS_A ?? 1000);
const B = Number(process.env.EXGRID_COSTS_B ?? 9);
const OUT = process.env.EXGRID_COSTS_OUT;
// Each pivot run as it lands, so a run that fails part way leaves what it measured.
const PROGRESS = process.env.EXGRID_COSTS_PROGRESS;

test.skip(!['live-costs', 'live-bytes', 'pivot-costs'].includes(MEASURE), 'runs only with EXGRID_MEASURE=live-costs, live-bytes or pivot-costs');

test.use({ viewport: { width: 1400, height: 1100 }, freshDocument: true, actionTimeout: 120_000 });

function save(name, result) {
    console.log(`${name} ${JSON.stringify({ ...result, runs: undefined, samples: undefined }, null, 1)}`);
    if (!OUT) {
        return;
    }
    const all = fs.existsSync(OUT) ? JSON.parse(fs.readFileSync(OUT, 'utf8')) : [];
    all.push({ name, at: new Date().toISOString(), ...result });
    fs.writeFileSync(OUT, `${JSON.stringify(all, null, 2)}\n`);
}

/** measure-live.spec.mjs's probe: watches on the DOM, each keeping the end of the task that made it
 * hold and the animation frame after it; and every long task. */
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
    // Holds once the view shows the text the expect element names, after that text has moved from
    // what it named when the watch was armed.
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
                // The frame's animation callbacks, then — M1's "paint" — the first task after the
                // frame, which runs once its style, layout and paint are done.
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

const median = (values) => {
    const sorted = [...values].sort((a, b) => a - b);
    return sorted.length === 0 ? null : sorted[Math.floor(sorted.length / 2)];
};

function spread(results, pick) {
    const values = results.map(pick).filter((v) => typeof v === 'number' && Number.isFinite(v));
    return values.length === 0 ? null : { median: median(values), min: Math.min(...values), max: Math.max(...values), n: values.length };
}

function probeValues(line) {
    const m = /\[probe ([^\]]*)\]/.exec(line);
    if (!m) {
        return null;
    }
    return Object.fromEntries(m[1].split(' ').map((kv) => kv.split('=')).map(([k, v]) => [k, Number(v)]));
}

/** One batch: resumed, the batch's task seen, paused again before the next tick. */
async function oneBatch(page, toggle, statusSelector, viewSelector) {
    await page.evaluate(({ status, view }) => {
        window.__costs.watch('batch', status);
        window.__costs.watch('view', view);
    }, { status: statusSelector, view: viewSelector });
    await toggle.click();
    await page.waitForFunction(() => window.__costs.seen('batch').changedAt !== null, null, { polling: 10, timeout: 600_000 });
    await toggle.click();
    await expect(toggle).toHaveText("Resume the page's changes");
    await page.waitForFunction(() => window.__costs.seen('batch').paintedAt !== null, null, { polling: 20, timeout: 600_000 });
}

test('ticket 01: a live update on /grid-live-local, WebAssembly', async ({ page }) => {
    test.skip(MEASURE !== 'live-costs' || SERVER, 'live-costs runs on WebAssembly');
    test.setTimeout(3_600_000);
    await page.addInitScript(probe);
    await page.goto(`/grid-live-local?rows=${ROWS}&batch=${BATCH}&interval=0${PROBE ? '&probe=1' : ''}${PUSH ? '&push=1' : ''}${VOUCH ? '&vouch=1' : ''}`, { timeout: 600_000 });
    await expect(page.locator('#grid-live-local-made')).toContainText(`${ROWS.toLocaleString('en-US')} trades made in`, { timeout: 600_000 });
    const viewport = '.ex-grid .ex-viewport';
    await expect(page.locator(viewport).locator('.ex-row').first()).toBeVisible({ timeout: 300_000 });
    const load = await page.locator('#grid-live-local-made').textContent();
    const toggle = page.locator('#grid-live-local-toggle');
    await toggle.click();
    await expect(toggle).toHaveText("Resume the page's changes");

    const runs = [];
    for (let run = 0; run < WARMUP + RUNS; run++) {
        await oneBatch(page, toggle, '#grid-live-local-status', viewport);
        // The grid's own mutations are in the batch's task, so whether it changed is known now; when
        // no painted row changed, nothing of the grid's shows, and the batch's frame is the frame.
        const gridChanged = await page.evaluate(() => window.__costs.seen('view').changedAt !== null);
        if (gridChanged) {
            await page.waitForFunction(() => window.__costs.seen('view').paintedAt !== null, null, { polling: 20, timeout: 600_000 });
        }
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
        r.probe = probeValues(r.status);
        r.first = Number(/\[first=(\d+)\]/.exec(r.status)?.[1] ?? NaN);
        if (run >= WARMUP) {
            runs.push(r);
        }
    }
    save(PUSH ? (VOUCH ? 'grid-wasm-pushed-vouched' : 'grid-wasm-pushed') : 'grid-wasm', {
        rows: ROWS, batch: BATCH, probe: PROBE, push: PUSH, vouch: VOUCH, load, warmup: WARMUP,
        appliedMs: spread(runs, (r) => r.appliedMs),
        applyToFrameMs: spread(runs, (r) => r.applyToFrameMs),
        applyToPaintedMs: spread(runs, (r) => r.applyToPaintedMs),
        frameToPaintedMs: spread(runs, (r) => r.frameToPaintedMs),
        endOfTaskToFrameMs: spread(runs, (r) => r.endOfTaskToFrameMs),
        taskStartToFrameMs: spread(runs, (r) => r.taskStartToFrameMs),
        longestTaskMs: spread(runs, (r) => r.longestTaskMs),
        gridChanged: runs.filter((r) => r.gridChanged).length,
        probeSourceMs: spread(runs, (r) => r.probe?.source),
        probeKeysMs: spread(runs, (r) => r.probe?.keys),
        probeRowsMs: spread(runs, (r) => r.probe?.rows),
        lastStatus: runs.at(-1).status,
        runs,
    });
});

test('ticket 01: the bytes of a live update on /grid-live-local, Server host', async ({ page }) => {
    test.skip(MEASURE !== 'live-bytes' || !SERVER, 'live-bytes runs on the Server host');
    test.setTimeout(3_600_000);
    const wireUrl = `http://localhost:${HOST_PORT}/api/wire`;
    const wire = async () => (await (await fetch(wireUrl)).json());
    const cdp = await page.context().newCDPSession(page);
    const frames = [];
    const counts = new Map();
    const KINDS = ['JS.RenderBatch', 'OnRenderCompleted', 'DispatchBrowserEvent', 'BeginInvokeDotNetFromJS', 'EndInvokeJSFromDotNet',
        'JS.BeginInvokeJS', 'JS.EndInvokeDotNet', 'UpdateRootComponents', 'StartCircuit', 'ConnectCircuit', 'JS.AttachComponent'];
    const classify = (buffer) => {
        const text = buffer.toString('latin1', 0, Math.min(buffer.length, 96));
        for (const kind of KINDS) {
            if (text.includes(kind)) {
                return kind;
            }
        }
        return buffer.length <= 8 ? 'small' : 'other';
    };
    const count = (dir, kind) => counts.get(`${dir}:${kind}`) ?? 0;
    let extensions = null;
    const onFrame = (dir) => ({ response }) => {
        const buffer = response.opcode === 2 ? Buffer.from(response.payloadData, 'base64') : Buffer.from(response.payloadData, 'utf8');
        const kind = classify(buffer);
        frames.push({ dir, bytes: buffer.length, kind, at: Date.now() });
        counts.set(`${dir}:${kind}`, count(dir, kind) + 1);
    };
    cdp.on('Network.webSocketFrameReceived', onFrame('in'));
    cdp.on('Network.webSocketFrameSent', onFrame('out'));
    cdp.on('Network.webSocketHandshakeResponseReceived', ({ response }) => {
        extensions = response.headers['Sec-WebSocket-Extensions'] ?? response.headers['sec-websocket-extensions'] ?? null;
    });
    await cdp.send('Network.enable', { maxTotalBufferSize: 0, maxResourceBufferSize: 0 });

    await page.goto(`/grid-live-local?rows=${ROWS}&batch=${BATCH}&interval=0&step=1`, { timeout: 600_000 });
    await expect(page.locator('#grid-live-local-made')).toContainText(`${ROWS.toLocaleString('en-US')} trades made in`, { timeout: 600_000 });
    await expect(page.locator('.ex-grid .ex-viewport .ex-row').first()).toBeVisible({ timeout: 300_000 });
    const step = page.locator('#grid-live-local-step');
    await expect(step).toBeEnabled();

    // Quiet first: every batch the load sent has been acknowledged.
    const until = async (condition, what) => {
        const start = Date.now();
        while (!(await condition())) {
            if (Date.now() - start > 120_000) {
                throw new Error(`never reached: ${what}`);
            }
            await new Promise((r) => setTimeout(r, 2));
        }
    };
    await until(() => count('out', 'OnRenderCompleted') >= count('in', 'JS.RenderBatch'), 'the load acknowledged');
    // The connection this harness polls GET /api/wire on writes its own response at every reading:
    // two readings with nothing between them name it, and it is never taken for the circuit's.
    const quiet0 = await wire();
    const quiet1 = await wire();
    const poll = new Set(quiet1.filter((c) => c.written > (quiet0.find((b) => b.id === c.id)?.written ?? 0)).map((c) => c.id));

    // Every connection's bytes per press; the circuit's connection is the one that wrote the most
    // over all the measured presses (a press's first moments can see a static file or the demo API
    // write more on another connection).
    const delta = (before, after) => Object.fromEntries(after.map((c) => [c.id, c.written - (before.find((b) => b.id === c.id)?.written ?? 0)]));
    const samples = [];
    for (let i = 0; i < WARMUP + RUNS; i++) {
        const batches = count('in', 'JS.RenderBatch');
        const completed = count('out', 'OnRenderCompleted');
        const outBefore = frames.filter((f) => f.dir === 'out').length;
        const before = await wire();
        await step.click({ noWaitAfter: true });
        await until(() => count('in', 'JS.RenderBatch') >= batches + 1 && count('out', 'OnRenderCompleted') >= completed + 1, 'the press rendered');
        const after = await wire();
        const inBatches = frames.filter((f) => f.dir === 'in' && f.kind === 'JS.RenderBatch').slice(batches);
        const sent = frames.filter((f) => f.dir === 'out').slice(outBefore).reduce((s, f) => s + f.bytes, 0);
        // The marks of this batch end a second later, and the grid renders again to take them off: a
        // second render batch per update when a painted cell was marked. Read after a pause that
        // outlasts the Change Highlight (1 s on the page), as what that later batch carried.
        const b2 = count('in', 'JS.RenderBatch');
        const w2 = await wire();
        await page.waitForTimeout(1_400);
        await until(() => count('out', 'OnRenderCompleted') >= count('in', 'JS.RenderBatch'), 'the unmark acknowledged');
        const laterBatches = frames.filter((f) => f.dir === 'in' && f.kind === 'JS.RenderBatch').slice(b2);
        const w3 = await wire();
        if (i >= WARMUP) {
            samples.push({
                renderBatches: inBatches.length,
                payload: inBatches.reduce((s, f) => s + f.bytes, 0),
                wireByConnection: delta(before, after),
                clientPayload: sent,
                laterRenderBatches: laterBatches.length,
                laterPayload: laterBatches.reduce((s, f) => s + f.bytes, 0),
                laterWireByConnection: delta(w2, w3),
            });
        }
    }
    const totals = {};
    for (const s of samples) {
        for (const [id, bytes] of Object.entries(s.wireByConnection)) {
            totals[id] = (totals[id] ?? 0) + bytes;
        }
    }
    const connection = Object.entries(totals).filter(([id]) => !poll.has(id)).sort((a, b) => b[1] - a[1])[0]?.[0] ?? null;
    for (const s of samples) {
        s.wire = s.wireByConnection[connection] ?? 0;
        s.laterWire = s.laterWireByConnection[connection] ?? 0;
    }
    save('grid-server-bytes', {
        rows: ROWS, batch: BATCH, warmup: WARMUP, webSocketExtensions: extensions, connection, poll: [...poll],
        payload: spread(samples, (s) => s.payload),
        wire: spread(samples, (s) => s.wire),
        renderBatches: spread(samples, (s) => s.renderBatches),
        clientPayload: spread(samples, (s) => s.clientPayload),
        laterRenderBatches: spread(samples, (s) => s.laterRenderBatches),
        laterPayload: spread(samples, (s) => s.laterPayload),
        laterWire: spread(samples, (s) => s.laterWire),
        samples,
    });
});

test('ticket 01: a live redraw on /pivot-live-costs, WebAssembly', async ({ page }) => {
    test.skip(MEASURE !== 'pivot-costs' || SERVER, 'pivot-costs runs on WebAssembly');
    test.setTimeout(3_600_000);
    await page.addInitScript(probe);
    await page.goto(`/pivot-live-costs?a=${A}&b=${B}&batch=${BATCH}&step=1${PROBE ? '&probe=1' : ''}`, { timeout: 600_000 });
    await expect(page.locator('#pivot-live-costs-made')).toContainText('records made in', { timeout: 600_000 });
    const report = '#pivot-live-costs .ex-pivot .ex-pivot-sheet > .ex-grid .ex-viewport';
    await expect(page.locator(report).locator('.ex-row').first()).toBeVisible({ timeout: 900_000 });
    const load = await page.locator('#pivot-live-costs-made').textContent();
    const step = page.locator('#pivot-live-costs-step');
    await expect(step).toBeEnabled();

    const runs = [];
    let lastFrame = null;
    // The page's .NET runtime reports running out of memory in the console; the run stops there,
    // rather than waiting out a redraw that will not land.
    let outOfMemory = null;
    const outOfMemorySeen = new Promise((resolve) => {
        page.on('console', (m) => {
            if (outOfMemory === null && /Out of memory|OutOfMemoryException/.test(m.text())) {
                outOfMemory = m.text().split('\n').slice(0, 4).join(' | ');
                resolve();
            }
        });
    });
    for (let run = 0; run < WARMUP + RUNS; run++) {
        // The redraw interval (250 ms) has passed since the last report reached the screen, so the
        // batch is asked for at once (ADR-0067): a condition on the page's clock, not a sleep.
        if (lastFrame !== null) {
            await page.waitForFunction((at) => performance.now() - at > 400, lastFrame, { polling: 20, timeout: 60_000 });
        }
        // One batch per press; the report has landed when it shows the first record's new value.
        await page.evaluate(({ status, view }) => {
            window.__costs.watch('batch', status);
            window.__costs.watchExpect('view', view, '#pivot-live-costs-expect');
        }, { status: '#pivot-live-costs-status', view: report });
        await step.click({ noWaitAfter: true });
        await page.waitForFunction(() => window.__costs.seen('batch').changedAt !== null, null, { polling: 10, timeout: 600_000 });
        await Promise.race([
            page.waitForFunction(() => window.__costs.seen('view').paintedAt !== null, null, { polling: 20, timeout: 900_000 }),
            outOfMemorySeen,
        ]);
        if (outOfMemory !== null) {
            if (PROGRESS) {
                fs.appendFileSync(PROGRESS, `${JSON.stringify({ a: A, b: B, batch: BATCH, run, outOfMemory })}\n`);
            }
            throw new Error(`run ${run} (${run - WARMUP + 1} after the warm-up): ${outOfMemory}`);
        }
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
                status: line,
                appliedMs: applied,
                applyToReportFrameMs: Math.round(view.frameAt - startedAt),
                applyToReportPaintedMs: Math.round(view.paintedAt - startedAt),
                longestTaskMs: tasks.longestMs,
                longTasks: tasks.count,
                longTasksTotalMs: tasks.totalMs,
                frameAt: view.paintedAt,
            };
        });
        r.probe = probeValues(r.status);
        lastFrame = r.frameAt;
        if (PROGRESS) {
            fs.appendFileSync(PROGRESS, `${JSON.stringify({ a: A, b: B, batch: BATCH, run, ...r })}\n`);
        }
        if (run >= WARMUP) {
            runs.push(r);
        }
    }
    save('pivot-wasm', {
        a: A, b: B, batch: BATCH, probe: PROBE, load, warmup: WARMUP,
        appliedMs: spread(runs, (r) => r.appliedMs),
        applyToReportFrameMs: spread(runs, (r) => r.applyToReportFrameMs),
        applyToReportPaintedMs: spread(runs, (r) => r.applyToReportPaintedMs),
        longestTaskMs: spread(runs, (r) => r.longestTaskMs),
        longTasks: spread(runs, (r) => r.longTasks),
        longTasksTotalMs: spread(runs, (r) => r.longTasksTotalMs),
        probeCubeMs: spread(runs, (r) => r.probe?.cube),
        probeReportMs: spread(runs, (r) => r.probe?.report),
        probeKeysMs: spread(runs, (r) => r.probe?.keys),
        probeRowsMs: spread(runs, (r) => r.probe?.rows),
        probeNextCubeMs: spread(runs, (r) => r.probe?.nextCube),
        probeNextReportMs: spread(runs, (r) => r.probe?.nextReport),
        probeNamed: spread(runs, (r) => r.probe?.named),
        probeShared: spread(runs, (r) => r.probe?.shared),
        reportRows: runs.at(-1)?.probe?.reportRows ?? null,
        lastStatus: runs.at(-1).status,
        runs,
    });
});
