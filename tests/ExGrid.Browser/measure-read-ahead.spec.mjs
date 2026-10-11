import { test, expect, record, scrollRowToTop } from './fixtures.mjs';
import { API_PORT, LATENCY_CONTROL_URL, SERVER } from './hosting.mjs';
import fs from 'node:fs';
import path from 'node:path';

// LAB ONLY, never merged (lab/remote-read-ahead): ticket 14 of the live-data spec, "A remote
// report's Window is read ahead" — what reading ahead would buy ExPivot over a report served on a
// network, measured before anything is decided. Observational, never gated (AGENTS.md). Runs only
// with EXGRID_MEASURE=read-ahead, on WebAssembly, against hosts already listening, which the suite's
// config then reuses: the published WebAssembly DemoHost (EXGRID_HOSTS, as CI's hosts job publishes
// it) and the published demo API server behind latency-proxy.mjs, which adds the round trip
// ADR-0151's experiment added — 150 ms, half each way. read-ahead-run.sh starts all three:
//
//   EXGRID_HOSTS=/path/to/hosts bash read-ahead-run.sh
//
// /lab-read-ahead shows one ExPivot over the server's report source (as /pivot-db's server pivot),
// asking ?readAhead= rows either side of the range its grid needs (ExPivot.ReadAheadRows, the lab's
// switch), at 0, 30, 60 and 120 rows. A question in flight whose Window covers the range the grid
// needs is kept; "60s" is 60 rows with any other Window superseding it instead, as GridSource.Fetch
// supersedes (ADR-0025), for comparison. "b100" asks aligned blocks of 100 rows instead, as ag-grid's
// Server-Side row model does: the blocks that cover the range widened by 10 rows either side, its
// rowBuffer (ExPivot.ReadAheadBlockRows); "b100m0" covers the range alone. A "t10" after either asks
// before the Window runs out, once the rows on screen and 10 either side are not all in it, as
// ag-grid's rendered rows ask (ExPivot.ReadAheadTriggerRows): "b100t10" is ag-grid's rule.
// EXGRID_MEASURE_LAYOUTS, _PARTS, _INPUTS, _PACES, _SAMPLES, _READ_AHEAD and _LIVE_READ_AHEAD narrow
// a run. For each layout and each read-ahead it records:
//
// - scrolling: how long Placeholders are on screen while the report is scrolled by the wheel, by
//   ArrowDown at the Viewport's foot (a key that scrolls by a row) and by PageDown, each a step at a
//   time (every input left to settle) and held (an input every few animation frames). The time is
//   the page's own: from the animation frame after the DOM first shows a row not held to the frame
//   after the DOM shows none, by a MutationObserver installed before the app's first script;
// - every Window question's answer, as the network saw it: its rows, its bytes as JSON, its
//   metadata's and its rows' share, and the bytes on the wire (Brotli, as the browser asks);
// - live updates: a tick of the server's live updates (the same trades for the same seed, after a
//   reset), the Window Changes it brings over the Window held — their rows and bytes — and the
//   server's time to project the Window (the lab's Server-Timing header);
// - the Windows the report source keeps (LV-22) after the ticks, and what they hold on the
//   server's heap.
//
// A wait waits for what it reads (ADR-0056): an input's effect, then a settled grid — the scroll
// still for two frames, the grid's painted slice the one the offset shows, no Placeholder, and no
// question out. Held inputs are paced by the page's own animation frames, and the pace achieved is
// read back from the inputs' own time stamps.

const MEASURE = process.env.EXGRID_MEASURE;
test.skip(MEASURE !== 'read-ahead', 'the measurement runs only with EXGRID_MEASURE=read-ahead');
test.skip(MEASURE === 'read-ahead' && SERVER,
    'measured on WebAssembly: on the Server host ExPivot and its report source share the server, and no round trip is read ahead of');

const list = (name, fallback) => (process.env[name] ?? fallback).split(',').map((s) => s.trim()).filter(Boolean);
// A read-ahead is a number of rows; with an `s` after it, a question in flight is superseded by any
// other Window, as GridSource.Fetch's is (ADR-0025) — the comparison, scrolling only. `b` and a
// number is aligned blocks of that many rows, covering the range and 10 rows either side, or the
// rows an `m` and a number after it says. A `t` and a number after either asks once the rows on
// screen and that many either side are not all in the Window, before it runs out.
const variant = (text) => {
    const m = /^(?:b(\d+)(?:m(\d+))?|(\d+)(s?))(?:t(\d+))?$/.exec(text);
    if (!m) throw new Error(`a read-ahead is a number of rows, 60s, b100, b100m0 or either with t10 after it, not "${text}"`);
    const trigger = m[5] === undefined ? 0 : Number(m[5]);
    if (m[1] !== undefined) {
        return { key: text, rows: 0, supersedes: false, block: Number(m[1]), margin: m[2] === undefined ? 10 : Number(m[2]), trigger };
    }
    return { key: text, rows: Number(m[3]), supersedes: m[4] === 's', block: 0, margin: 10, trigger };
};
const SCROLL_READ_AHEADS = list('EXGRID_MEASURE_READ_AHEAD', '0,30,60,120,60s').map(variant);
const LIVE_READ_AHEADS = list('EXGRID_MEASURE_LIVE_READ_AHEAD', '0,30,60,120').map(variant);
const LAYOUTS = list('EXGRID_MEASURE_LAYOUTS', 'narrow,wide');
const PARTS = list('EXGRID_MEASURE_PARTS', 'scroll,live');
const INPUTS = list('EXGRID_MEASURE_INPUTS', 'wheel,arrow,pagedown');
const PACES = list('EXGRID_MEASURE_PACES', 'step,held');
const SAMPLES = Number(process.env.EXGRID_MEASURE_SAMPLES ?? 3);
const RTT = Number(process.env.EXGRID_MEASURE_RTT ?? 150);
const TICKS = Number(process.env.EXGRID_MEASURE_TICKS ?? 10);
// How many trades a tick moves: the demo's live updates move twenty (/pivot-live); a thousand is
// PV-21's batch, and enough to reach a narrow Window of a long report.
const TICK_TRADES = list('EXGRID_MEASURE_TICK_TRADES', '20,1000').map(Number);
// How far each scrolling sample goes, in rows: four times the widest read-ahead, so that even 120
// rows either side runs out a few times in a sample.
const DISTANCE = Number(process.env.EXGRID_MEASURE_ROWS ?? 480);
// The demo API server itself, not the proxy: the run's own questions to it are not the page's.
const DIRECT_API = process.env.EXGRID_MEASURE_API ?? `http://localhost:${API_PORT + 1000}`;
const OUT = path.resolve(process.env.EXGRID_MEASURE_OUT ?? 'test-results/read-ahead');

// A wheel notch, in pixels, and how many animation frames a held input waits after the one before
// it has been dispatched: a wheel spun at about twenty notches a second, and a key held at the
// twenty-five to thirty presses a second a held key repeats at. Dispatching takes time of its own,
// so the pace achieved is read back from the inputs' own time stamps, and recorded.
const NOTCH_PX = 100;
const HELD_FRAMES = { wheel: 2, arrow: 1, pagedown: 1 };

test.use({ viewport: { width: 1400, height: 1100 }, freshDocument: true, actionTimeout: 60_000 });

const REPORT = '.ex-pivot .ex-pivot-sheet > .ex-grid';
const WINDOW_URL = /\/api\/pivot\/reports\/([0-9a-f]{32})\/window$/;

/** The probe, an init script: Placeholder episodes and inputs, on the page's own clock. */
function probe(report) {
    const p = { episodes: [], flings: [], inputs: [], current: null, fling: null, settle: null };
    window.__ra = p;
    // A row not held is a Placeholder with no cell at all; a row held but skipped during a fling
    // paints its Pinned Columns (ADR-0004). Only the first is waiting for the report source.
    p.count = () => {
        const viewport = document.querySelector(`${report} .ex-viewport`);
        let missing = 0;
        let fling = 0;
        if (viewport) {
            for (const row of viewport.querySelectorAll('.ex-row.ex-placeholder')) {
                if (row.querySelector('[role=gridcell]')) fling++;
                else missing++;
            }
        }
        return { missing, fling };
    };
    const framed = (episode, field) => requestAnimationFrame(() => { episode[field] = performance.now(); });
    const follow = (name, list, rows, now) => {
        const open = p[name];
        if (rows > 0 && !open) {
            p[name] = { start: now, startFrame: null, end: null, endFrame: null, maxRows: rows };
            framed(p[name], 'startFrame');
        } else if (rows > 0) {
            open.maxRows = Math.max(open.maxRows, rows);
        } else if (open) {
            open.end = now;
            framed(open, 'endFrame');
            list.push(open);
            p[name] = null;
        }
    };
    new MutationObserver(() => {
        const now = performance.now();
        const { missing, fling } = p.count();
        follow('current', p.episodes, missing, now);
        follow('fling', p.flings, fling, now);
    }).observe(document, { subtree: true, childList: true, attributes: true, attributeFilter: ['class'] });
    window.addEventListener('keydown', (e) => p.inputs.push({ type: e.key, at: e.timeStamp }), { capture: true });
    window.addEventListener('wheel', (e) => p.inputs.push({ type: 'wheel', at: e.timeStamp }), { capture: true, passive: true });
    p.reset = () => {
        p.episodes = [];
        p.flings = [];
        p.inputs = [];
    };
    const episodes = (list) => list.map((e) => ({
        frameMs: e.endFrame !== null && e.startFrame !== null ? e.endFrame - e.startFrame : null,
        domMs: e.end - e.start,
        maxRows: e.maxRows,
        at: e.startFrame,
    }));
    p.summary = () => ({
        episodes: episodes(p.episodes),
        flings: episodes(p.flings),
        open: p.current !== null || p.fling !== null,
        inputs: p.inputs.map((i) => i.at),
    });
    // The grid's state as the page holds it, for the waits and the readings: the scroll offset, the
    // first row the grid painted, the Focus, and the lab page's count of Window questions.
    p.state = () => {
        const root = document.querySelector(report);
        const scroller = root?.querySelector(':scope > .ex-scroller');
        const viewport = scroller?.querySelector('.ex-viewport');
        if (!root || !scroller || !viewport) return null;
        const rowHeight = Number(/(?:^|[;\s])--ex-row-height:\s*([\d.]+)px/.exec(root.getAttribute('style') ?? '')?.[1]);
        const status = document.querySelector('#lab-status')?.textContent ?? '';
        const counter = (name) => Number(new RegExp(`${name} (\\d+)`).exec(status)?.[1] ?? NaN);
        return {
            top: scroller.scrollTop,
            first: Number(viewport.getAttribute('data-ex-first-row')),
            painted: viewport.querySelectorAll('.ex-row').length,
            rowHeight,
            rowCount: Number(root.getAttribute('aria-rowcount')),
            active: root.getAttribute('aria-activedescendant'),
            // The Focus cell's row, from its id ({prefix}r{row}c{column}); null while it is not painted.
            focusRow: Number(/r(\d+)c\d+$/.exec(root.getAttribute('aria-activedescendant') ?? '')?.[1] ?? NaN),
            asked: counter('asked'),
            answered: counter('answered'),
            cancelled: counter('cancelled'),
            failed: counter('failed'),
            inFlight: counter('in flight'),
            ticks: counter('ticks'),
            ...p.count(),
        };
    };
    // Settled: the scroll offset still for two animation frames, the slice the grid painted the one
    // that offset shows, no Placeholder of either kind, and no Window question out. Polled once a
    // frame; a new token starts the count again.
    p.settledFor = (token) => {
        const now = p.state();
        if (!now) return false;
        if (p.settle?.token !== token || p.settle.top !== now.top) {
            p.settle = { token, top: now.top, still: 0 };
            return false;
        }
        p.settle.still++;
        return p.settle.still >= 2 && now.first === Math.floor(now.top / now.rowHeight + 1e-6)
            && now.missing === 0 && now.fling === 0 && now.inFlight === 0;
    };
}

const read = (page) => page.evaluate(() => window.__ra.state());

/** Waits until the grid has settled (the probe's settledFor), and answers its state then. */
async function settled(page) {
    const token = Math.random();
    await page.waitForFunction((token) => window.__ra.settledFor(token), token, { polling: 'raf', timeout: 120_000 });
    return read(page);
}

/** Waits for `frames` animation frames of the page's own. */
const frames = (page, count) => page.evaluate((count) => new Promise((resolve) => {
    let left = count;
    const next = () => (--left <= 0 ? resolve() : requestAnimationFrame(next));
    requestAnimationFrame(next);
}), count);

/** The scroll offset moved from `top`: an input has taken effect. */
const movedFrom = (page, top) => page.waitForFunction(({ report, top }) =>
    document.querySelector(report)?.querySelector(':scope > .ex-scroller')?.scrollTop !== top,
{ report: REPORT, top }, { polling: 'raf', timeout: 60_000 });

const median = (values) => {
    const sorted = values.filter((v) => typeof v === 'number' && Number.isFinite(v)).sort((a, b) => a - b);
    return sorted.length === 0 ? null : sorted[Math.floor(sorted.length / 2)];
};

/** The median, the least and the most of a number over the runs, and how many runs. */
function spread(values) {
    const numbers = values.filter((v) => typeof v === 'number' && Number.isFinite(v));
    if (numbers.length === 0) return null;
    const round = (v) => Math.round(v * 10) / 10;
    return { median: round(median(numbers)), min: round(Math.min(...numbers)), max: round(Math.max(...numbers)), n: numbers.length };
}

/** Where each top-level property's value lies in a JSON text, as [start, end) offsets. */
function topLevelSpans(text) {
    const spans = {};
    let depth = 0;
    let key = null;
    let valueStart = -1;
    for (let i = 0; i < text.length; i++) {
        const c = text[i];
        if (c === '"') {
            let j = i + 1;
            while (j < text.length && text[j] !== '"') j += text[j] === '\\' ? 2 : 1;
            if (depth === 1 && key === null) key = text.slice(i + 1, j);
            i = j;
        } else if (c === ':' && depth === 1 && key !== null && valueStart < 0) {
            valueStart = i + 1;
        } else if (c === '{' || c === '[') {
            depth++;
        } else if (c === '}' || c === ']') {
            depth--;
            if (depth === 0 && key !== null) {
                spans[key] = [valueStart, i];
                key = null;
                valueStart = -1;
            }
        } else if (c === ',' && depth === 1 && key !== null) {
            spans[key] = [valueStart, i];
            key = null;
            valueStart = -1;
        }
    }
    return spans;
}

const serverTiming = (header) => Object.fromEntries((header ?? '').split(',').map((part) => {
    const [name, ...params] = part.trim().split(';');
    const dur = params.map((p) => /^dur=([\d.]+)$/.exec(p.trim())?.[1]).find((d) => d !== undefined);
    return [name, dur === undefined ? null : Number(dur)];
}).filter(([name]) => name));

/** Every Window question the page sends, as the network sees it, with its answer read whole. */
function watchWindows(page) {
    const log = { entries: [], pending: new Set(), reportId: null };
    const byRequest = new Map();
    page.on('request', (request) => {
        const m = WINDOW_URL.exec(request.url());
        if (!m || request.method() !== 'POST') return;
        log.reportId = m[1];
        let body = {};
        try {
            body = JSON.parse(request.postData() ?? '{}');
        } catch {
            // Read as it is; the counts below say what is missing.
        }
        const entry = {
            requestedAt: Date.now(),
            start: body.window?.start,
            count: body.window?.count,
            baseline: body.baseline != null,
            markChanges: body.markChanges,
            refreshData: body.refreshData,
            outcome: 'out',
        };
        log.entries.push(entry);
        byRequest.set(request, entry);
    });
    page.on('requestfinished', (request) => {
        const entry = byRequest.get(request);
        if (!entry) return;
        entry.finishedAt = Date.now();
        const job = (async () => {
            const response = await request.response();
            const sizes = await request.sizes();
            const headers = await response.allHeaders();
            const body = await response.body();
            const text = body.toString('utf8');
            const spans = topLevelSpans(text);
            const bytes = (name) => (spans[name] ? Buffer.byteLength(text.slice(spans[name][0], spans[name][1])) : null);
            const json = JSON.parse(text);
            const rows = json.rows ?? (json.changes ?? []).map((change) => change.row);
            Object.assign(entry, {
                outcome: response.status() === 200 ? 'answered' : `status ${response.status()}`,
                complete: json.rows != null,
                rows: json.rows?.length ?? null,
                changes: json.rows == null ? (json.changes ?? []).length : null,
                reportRows: json.metadata?.rowCount ?? null,
                valueColumns: json.metadata?.valueColumns?.length ?? null,
                filledCells: rows.reduce((sum, row) => sum + (row?.values ?? []).filter((v) => v !== null).length, 0),
                rawBytes: body.length,
                metadataBytes: bytes('metadata'),
                // A complete Window's rows, or Window Changes' rows: the other is null or empty.
                rowsBytes: json.rows != null ? bytes('rows') : null,
                changesBytes: json.rows == null ? bytes('changes') : null,
                encodedBytes: sizes.responseBodySize,
                headerBytes: sizes.responseHeadersSize,
                requestBytes: sizes.requestBodySize,
                encoding: headers['content-encoding'] ?? null,
                timing: serverTiming(headers['server-timing']),
                server: headers['x-lab-window'] ? JSON.parse(headers['x-lab-window']) : null,
            });
        })().catch((error) => {
            entry.outcome = 'unread';
            entry.error = String(error);
        });
        log.pending.add(job);
        job.finally(() => log.pending.delete(job));
    });
    page.on('requestfailed', (request) => {
        const entry = byRequest.get(request);
        if (!entry) return;
        entry.finishedAt = Date.now();
        entry.outcome = 'failed';
        entry.failure = request.failure()?.errorText ?? null;
    });
    log.settled = () => Promise.all([...log.pending]);
    log.since = (mark) => log.entries.slice(mark);
    return log;
}

async function setRoundTrip(ms) {
    const response = await fetch(`${LATENCY_CONTROL_URL}/?rtt=${ms}`, { method: 'POST' });
    if (!response.ok) throw new Error(`the latency proxy at ${LATENCY_CONTROL_URL} refused rtt=${ms}: ${response.status}`);
    const check = await fetch(`${LATENCY_CONTROL_URL}/`);
    const said = Number((await check.text()).trim().replace('rtt=', ''));
    expect(said, 'the latency proxy holds the round trip asked for').toBe(ms);
    return said;
}

async function api(method, route, body) {
    const response = await fetch(`${DIRECT_API}${route}`, {
        method,
        headers: body === undefined ? {} : { 'Content-Type': 'application/json' },
        body: body === undefined ? undefined : JSON.stringify(body),
    });
    if (!response.ok) throw new Error(`${method} ${route} answered ${response.status}: ${await response.text()}`);
    return response.status === 204 ? null : response.json();
}

/**
 * The questions' log the page kept since it was last written: Window answers and client checks. The
 * button's click is dispatched, not made, so the keyboard stays where it is; the dump's numbered
 * first line says the one asked for has been written.
 */
async function pageLog(page) {
    const lines = async () => ((await page.locator('#lab-log').textContent()) ?? '')
        .split('\n').filter((line) => line.trim()).map((line) => JSON.parse(line));
    const before = (await lines())[0]?.n ?? 0;
    await page.locator('#lab-dump').dispatchEvent('click');
    await expect.poll(async () => (await lines())[0]?.n, { timeout: 60_000 }).toBe(before + 1);
    return (await lines()).slice(1);
}

/** Opens the lab page for a layout and a read-ahead, the probe in place, and waits for the report. */
async function open(page, layout, readAhead, more = '') {
    await page.goto(`/lab-read-ahead?layout=${layout}&readAhead=${readAhead.rows}&supersede=${readAhead.supersedes ? 1 : 0}`
        + `&block=${readAhead.block}&blockMargin=${readAhead.margin}&trigger=${readAhead.trigger}${more}`, { timeout: 600_000 });
    await expect(page.locator('#lab-read-ahead')).toHaveText(String(readAhead.rows));
    await expect(page.locator('#lab-supersedes')).toHaveText(readAhead.supersedes ? /superseding/ : /^$/);
    await expect(page.locator('#lab-block')).toHaveText(String(readAhead.block));
    await expect(page.locator('#lab-block-margin')).toHaveText(String(readAhead.margin));
    await expect(page.locator('#lab-trigger')).toHaveText(String(readAhead.trigger));
    await expect(page.locator(`${REPORT} .ex-viewport .ex-row`).first()).toBeVisible({ timeout: 300_000 });
    const state = await settled(page);
    expect(state.rowCount, 'the report has rows to scroll').toBeGreaterThan(0);
    return state;
}

/** The value cell of a painted row: its second cell, the first after the pinned label. */
const valueCell = (page, row) => page.locator(`${REPORT} .ex-viewport .ex-row[aria-rowindex="${row + 1}"] [role=gridcell]`).nth(1);

/**
 * Puts the report at `startRow` and the Focus where `input` scrolls from: ArrowDown at the
 * Viewport's foot, so that each press scrolls by a row; PageDown and the wheel at the top row.
 */
async function prepare(page, input, startRow) {
    const grid = page.locator(REPORT);
    await scrollRowToTop(grid, startRow);
    let state = await settled(page);
    await valueCell(page, state.first).click({ force: true });
    await expect.poll(async () => (await read(page)).active, { timeout: 60_000 }).not.toBeNull();
    state = await settled(page);
    if (input === 'arrow') {
        // Down the Viewport until a press scrolls: the Focus is then at its foot.
        for (let press = 0; ; press++) {
            expect(press, 'ArrowDown reaches the Viewport\'s foot').toBeLessThan(100);
            const before = state;
            await page.keyboard.press('ArrowDown');
            await page.waitForFunction(({ report, active }) =>
                document.querySelector(report)?.getAttribute('aria-activedescendant') !== active,
            { report: REPORT, active: before.active }, { polling: 'raf', timeout: 60_000 });
            state = await settled(page);
            if (state.top !== before.top) break;
        }
    }
    return state;
}

const PRESS = { arrow: 'ArrowDown', pagedown: 'PageDown' };

async function act(page, input) {
    if (input === 'wheel') {
        await page.mouse.wheel(0, NOTCH_PX);
    } else {
        await page.keyboard.press(PRESS[input]);
    }
}

/** How many inputs scroll about DISTANCE rows. */
function inputsFor(input, state, pageRows) {
    if (input === 'arrow') return DISTANCE;
    if (input === 'pagedown') return Math.round(DISTANCE / pageRows);
    return Math.round((DISTANCE * state.rowHeight) / NOTCH_PX);
}

/** One sample: `count` inputs from a settled start, each settled (step) or paced (held). */
async function scrollSample(page, net, { input, pace, startRow, pageRows }) {
    const preparing = net.entries.length;
    const start = await prepare(page, input, startRow);
    const count = inputsFor(input, start, pageRows);
    await net.settled();
    await pageLog(page);
    const mark = net.entries.length;
    // The Windows asked while the sample was prepared: the one it starts from among them.
    const setup = net.since(preparing).map((e) => ({ start: e.start, count: e.count, outcome: e.outcome, rows: e.rows }));
    await page.evaluate(() => window.__ra.reset());
    const began = Date.now();
    for (let i = 0; i < count; i++) {
        const before = pace === 'step' ? await read(page) : null;
        await act(page, input);
        if (pace === 'step') {
            await movedFrom(page, before.top);
            await settled(page);
        } else {
            await frames(page, HELD_FRAMES[input]);
        }
    }
    if (pace === 'held') {
        // Every input heard; and every key's move made, since the grid moves the Focus for a key once
        // its own code has run: ArrowDown a row a press, PageDown a page a press.
        const focus = input === 'arrow' ? start.focusRow + count : input === 'pagedown' ? start.focusRow + count * pageRows : null;
        await page.waitForFunction(({ count, focus }) => {
            const p = window.__ra;
            return p.inputs.length >= count && (focus === null || p.state()?.focusRow === focus);
        }, { count, focus }, { polling: 'raf', timeout: 120_000 });
    }
    const end = await settled(page);
    const elapsed = Date.now() - began;
    await net.settled();
    const summary = await page.evaluate(() => window.__ra.summary());
    const log = await pageLog(page);
    const network = net.since(mark);
    const keys = summary.inputs;
    const intervals = keys.slice(1).map((at, i) => at - keys[i]);
    const placeholder = summary.episodes.map((e) => e.frameMs);
    return {
        input,
        pace,
        startRow,
        firstRow: start.first,
        lastRow: end.first,
        rowsScrolled: end.first - start.first,
        inputs: count,
        inputsHeard: keys.length,
        inputIntervalMs: spread(intervals),
        elapsedMs: elapsed,
        placeholderMs: Math.round(placeholder.reduce((sum, ms) => sum + (ms ?? 0), 0) * 10) / 10,
        episodes: summary.episodes,
        flings: summary.flings,
        openAtEnd: summary.open,
        questions: {
            asked: end.asked - start.asked,
            answered: end.answered - start.answered,
            cancelled: end.cancelled - start.cancelled,
            failed: end.failed - start.failed,
        },
        setup,
        network,
        pageLog: log,
    };
}

function write(name, value) {
    fs.mkdirSync(OUT, { recursive: true });
    fs.writeFileSync(path.join(OUT, name), `${JSON.stringify(value, null, 1)}\n`);
}

/** Every answer of a set of samples, summed up: its rows and bytes, and the times around it. */
function answers(entries, pageLogs) {
    const answered = entries.filter((e) => e.outcome === 'answered');
    const windows = pageLogs.filter((l) => l.kind === 'window');
    const checks = pageLogs.filter((l) => l.kind === 'check' && l.rows >= 0);
    return {
        answered: answered.length,
        failedOrCancelled: entries.filter((e) => e.outcome === 'failed').length,
        rows: spread(answered.map((e) => e.rows ?? e.changes)),
        changes: spread(answered.filter((e) => !e.complete).map((e) => e.changes)),
        rawBytes: spread(answered.map((e) => e.rawBytes)),
        metadataBytes: spread(answered.map((e) => e.metadataBytes)),
        rowsBytes: spread(answered.map((e) => (e.complete ? e.rowsBytes : e.changesBytes))),
        encodedBytes: spread(answered.map((e) => e.encodedBytes)),
        requestBytes: spread(answered.map((e) => e.requestBytes)),
        encodings: [...new Set(answered.map((e) => e.encoding))],
        filledCells: spread(answered.map((e) => e.filledCells)),
        valueColumns: spread(answered.map((e) => e.valueColumns)),
        server: {
            computeMs: spread(answered.map((e) => e.timing?.compute)),
            projectMs: spread(answered.map((e) => e.timing?.project)),
            digestMs: spread(answered.map((e) => e.timing?.digest)),
            serializeMs: spread(answered.map((e) => e.timing?.serialize)),
            endpointMs: spread(answered.map((e) => e.timing?.endpoint)),
        },
        client: {
            arrivedMs: spread(windows.map((w) => w.arrivedMs)),
            parseMs: spread(windows.map((w) => w.parseMs)),
            checkMs: spread(checks.map((c) => c.ms)),
        },
    };
}

const environment = (page) => ({
    at: new Date().toISOString(),
    browser: page.context().browser()?.version() ?? null,
    roundTripMs: RTT,
    readAheads: { scroll: SCROLL_READ_AHEADS.map((v) => v.key), live: LIVE_READ_AHEADS.map((v) => v.key) },
    samples: SAMPLES,
    distanceRows: DISTANCE,
    notchPx: NOTCH_PX,
    heldFrames: HELD_FRAMES,
    tickTrades: TICK_TRADES,
    ticks: TICKS,
    commit: process.env.EXGRID_MEASURE_COMMIT ?? null,
});

/**
 * A load's code run once before it is timed: the page's runtime is new with every load, so its
 * first questions, parses and renders run code for the first time. A few of each input, settled,
 * from `startRow`; nothing of it is recorded.
 */
async function warmUp(page, startRow, pageRows) {
    for (const input of ['pagedown', 'wheel', 'arrow']) {
        await prepare(page, input, startRow);
        for (let i = 0; i < (input === 'arrow' ? 8 : 4); i++) {
            const before = await read(page);
            await act(page, input);
            await movedFrom(page, before.top);
            await settled(page);
        }
    }
}

for (const layout of LAYOUTS) {
    test(`read-ahead: the ${layout} report scrolled by the wheel, ArrowDown and PageDown`, async ({ page }, testInfo) => {
        test.skip(!PARTS.includes('scroll'), 'EXGRID_MEASURE_PARTS leaves scrolling out');
        test.setTimeout(4 * 3_600_000);
        await page.addInitScript(probe, REPORT);
        const net = watchWindows(page);
        const result = { layout, environment: environment(page), readAheads: {} };
        const summary = {};
        await setRoundTrip(RTT);
        for (const readAhead of SCROLL_READ_AHEADS) {
            const loaded = await open(page, layout, readAhead);
            // Three places to scroll from, the same for every read-ahead, clear of either end.
            const first = 200;
            const last = loaded.rowCount - DISTANCE - 200;
            test.skip(last < first, `the ${layout} report has ${loaded.rowCount} rows: too few to scroll ${DISTANCE} from three places`);
            const starts = Array.from({ length: SAMPLES }, (_, i) => Math.round(first + (i * (last - first)) / Math.max(1, SAMPLES - 1)));
            // The rows PageDown moves: the rows the Viewport shows whole (ADR-0012), read from one press.
            const pageRows = await measurePageRows(page, starts[0]);
            await warmUp(page, starts[0], pageRows);
            const samples = [];
            for (const input of INPUTS) {
                for (const pace of PACES) {
                    for (const startRow of starts) {
                        console.log(`${layout}, read-ahead ${readAhead.key}: ${input} ${pace} from row ${startRow}`);
                        samples.push(await scrollSample(page, net, { input, pace, startRow, pageRows }));
                    }
                }
            }
            result.readAheads[readAhead.key] = { reportRows: loaded.rowCount, rowHeight: loaded.rowHeight, paintedRows: loaded.painted, pageRows, samples };
            const by = {};
            for (const input of INPUTS) {
                for (const pace of PACES) {
                    const runs = samples.filter((s) => s.input === input && s.pace === pace);
                    by[`${input} ${pace}`] = {
                        samples: runs.length,
                        rowsScrolled: spread(runs.map((s) => s.rowsScrolled)),
                        placeholderMs: spread(runs.map((s) => s.placeholderMs)),
                        episodes: spread(runs.map((s) => s.episodes.length)),
                        episodeMs: spread(runs.flatMap((s) => s.episodes.map((e) => e.frameMs))),
                        episodeMaxRows: spread(runs.flatMap((s) => s.episodes.map((e) => e.maxRows))),
                        flingEpisodes: spread(runs.map((s) => s.flings.length)),
                        flingMs: spread(runs.map((s) => s.flings.reduce((sum, e) => sum + (e.frameMs ?? 0), 0))),
                        questionsAsked: spread(runs.map((s) => s.questions.asked)),
                        questionsCancelled: spread(runs.map((s) => s.questions.cancelled)),
                        inputIntervalMs: spread(runs.map((s) => s.inputIntervalMs?.median)),
                        elapsedMs: spread(runs.map((s) => s.elapsedMs)),
                        answers: answers(runs.flatMap((s) => s.network), runs.flatMap((s) => s.pageLog)),
                    };
                }
            }
            summary[readAhead.key] = {
                reportRows: loaded.rowCount,
                paintedRows: loaded.painted,
                pageRows,
                answers: answers(samples.flatMap((s) => s.network), samples.flatMap((s) => s.pageLog)),
                by,
            };
            write(`${layout}-scroll.json`, result);
            write(`${layout}-scroll-summary.json`, summary);
        }
        record(testInfo.project.name, { [`read-ahead ${layout} scrolling, ${RTT} ms round trip`]: summary });
        console.log(`read-ahead ${layout} scrolling ${JSON.stringify(summary, null, 1)}`);
    });

    test(`read-ahead: live updates over the ${layout} report's Window`, async ({ page }, testInfo) => {
        test.skip(!PARTS.includes('live'), 'EXGRID_MEASURE_PARTS leaves live updates out');
        test.setTimeout(3_600_000);
        await page.addInitScript(probe, REPORT);
        const net = watchWindows(page);
        const result = { layout, environment: environment(page), readAheads: {} };
        const summary = {};
        await setRoundTrip(RTT);
        // One pass first, recorded nowhere: the server's code for a live tick, its computation and its
        // Window Changes has then run before any of it is timed.
        await liveRun(page, net, layout, LIVE_READ_AHEADS[0], TICK_TRADES[0], 2);
        for (const tickTrades of TICK_TRADES) {
            for (const readAhead of LIVE_READ_AHEADS) {
                const key = `${readAhead.key} rows, ${tickTrades} trades a tick`;
                console.log(`${layout} live: read-ahead ${readAhead.key}, ${tickTrades} trades a tick`);
                const run = await liveRun(page, net, layout, readAhead, tickTrades, TICKS);
                result.readAheads[key] = run;
                const live = run.ticks.flatMap((t) => t.network).filter((e) => e.baseline && e.outcome === 'answered');
                summary[key] = {
                    readAhead: readAhead.key,
                    tickTrades,
                    reportRows: run.reportRows,
                    windowRows: spread(live.map((e) => e.server?.windowRows)),
                    windowChanges: spread(live.filter((e) => !e.complete).map((e) => e.changes)),
                    completeAnswers: live.filter((e) => e.complete).length,
                    perTick: live.map((e) => ({
                        changes: e.changes,
                        complete: e.complete,
                        rawBytes: e.rawBytes,
                        changesBytes: e.complete ? e.rowsBytes : e.changesBytes,
                        metadataBytes: e.metadataBytes,
                        encodedBytes: e.encodedBytes,
                        computeMs: e.timing?.compute,
                        projectMs: e.timing?.project,
                    })),
                    answers: answers(live, run.ticks.flatMap((t) => t.pageLog)),
                    placed: answers(run.placedAnswers, run.placedLog),
                    kept: run.keptAfter,
                    keptHeapBytes: run.released.heldBytes,
                };
                write(`${layout}-live.json`, result);
                write(`${layout}-live-summary.json`, summary);
            }
        }
        record(testInfo.project.name, { [`read-ahead ${layout} live updates, ${RTT} ms round trip`]: summary });
        console.log(`read-ahead ${layout} live ${JSON.stringify(summary, null, 1)}`);
    });
}

/**
 * One live run: the generated trades again, so that every run takes the same ticks from the same
 * data; the page loaded at `readAhead`, its Window placed a third of the way down (row 1,000 at
 * most) and read ahead; then `ticks` ticks of `tickTrades` trades each, the seeds 1, 2, …, each
 * answered before the next; and the Windows the report source keeps then, and what they hold.
 */
async function liveRun(page, net, layout, readAhead, tickTrades, ticks) {
    await api('POST', '/api/reset');
    const loaded = await open(page, layout, readAhead, `&tickTrades=${tickTrades}`);
    const row = Math.min(1000, Math.max(0, Math.floor(loaded.rowCount / 3)));
    await net.settled();
    await pageLog(page);
    const placing = net.entries.length;
    await scrollRowToTop(page.locator(REPORT), row);
    const placed = await settled(page);
    await net.settled();
    // The complete Window the scroll asked for, read ahead: what a Window costs this layout.
    const placedAnswers = net.since(placing);
    const placedLog = await pageLog(page);
    const report = net.reportId;
    const keptBefore = await api('GET', `/api/lab/kept/${report}`);
    const done = [];
    for (let tick = 1; tick <= ticks; tick++) {
        const before = await read(page);
        const mark = net.entries.length;
        await page.locator('#lab-tick').click();
        // The tick lands, ExPivot is told, and asks for the Window it holds: one answer more.
        await page.waitForFunction(({ ticks, answered }) => {
            const now = window.__ra.state();
            return now !== null && now.ticks >= ticks && now.answered > answered && now.inFlight === 0;
        }, { ticks: before.ticks + 1, answered: before.answered }, { polling: 'raf', timeout: 120_000 });
        await settled(page);
        await net.settled();
        done.push({ tick, network: net.since(mark), pageLog: await pageLog(page) });
    }
    const keptAfter = await api('GET', `/api/lab/kept/${report}`);
    const released = await api('POST', `/api/lab/kept/${report}/release`);
    return {
        reportRows: loaded.rowCount, row, window: { start: placed.first }, placedAnswers, placedLog,
        keptBefore, keptAfter, released, ticks: done,
    };
}

/** The rows one PageDown moves, read from one press at `startRow`: the Focus's and the Viewport's. */
async function measurePageRows(page, startRow) {
    const before = await prepare(page, 'pagedown', startRow);
    await page.keyboard.press('PageDown');
    await movedFrom(page, before.top);
    const after = await settled(page);
    expect(after.first - before.first, 'PageDown moves the Viewport by the rows it moves the Focus (ADR-0012)')
        .toBe(after.focusRow - before.focusRow);
    return after.focusRow - before.focusRow;
}
