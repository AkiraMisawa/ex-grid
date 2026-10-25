// The core comparison (ADR-0350 section 15), run in the page against /core/<scenario>. It reads only
// what every core writes — the DOM contract (role, aria-rowindex, aria-colindex, the Focus named by
// aria-activedescendant, the scroller, --ex-row-height) — and presses only the page's own controls
// (CoreGrid.razor, CorePivot.razor say what they do), so the same script measures the C# core today
// and the Script Core under React and under both Blazor hosts later, on the same pages.
//
// Every input is made at machine speed and every reading is of what the user sees: a
// MutationObserver on the scenario's box sees the change land ("dom"), and the animation frame after
// it is when it is on screen ("frame"). Nothing waits on a clock but the gap a phase leaves for the
// page to settle, which is never inside a timed span.
//
//   grid, template  for each Window size (1,000, 10,000, 100,000 rows):
//                     pushAll   a new Window, every row new: until every painted row shows it
//                     pushOne   a new Window instance with row 0 new: until row 0 shows it
//                     tick K    K painted rows replaced (1, 5, 25), once and waited for, `ticks` times;
//                               then once per frame for `frames` frames: the frames' lengths, and the
//                               lag from the last tick until it is painted
//                   then, at the largest Window:
//                     mount     the grid put back over the Window: until its rows are painted
//                     keys      `burst` ArrowDowns in one task; one per frame for `frames` frames
//                     scroll    37 px per frame (slow) and 50 rows per frame (fling) for `frames` frames
//   sheet           editAll   a number typed into A1 and Enter: until every painted B cell reads it
//                   editOne   a number typed into L1, which nothing reads: until L1 reads it
//                   scroll    as above
//   pivot           amend     a Change Batch of one trade: until the first book's P&L reads it
//                   layout    the columns swapped between Currency and Product: until the headers say so
//                   scroll    as above
//
// On the Server host each operation's bytes on the circuit's connection too (GET /api/wire,
// Bench.Server): the connection that moved most during it, after per-message compression.
// Console errors and exceptions are collected by the driver (tools/cdp-core.mjs).
(() => {
    const frame = () => new Promise((resolve) => requestAnimationFrame(resolve));
    const frames = async (n) => { for (let i = 0; i < n; i++) await frame(); };
    const box = () => document.getElementById('core-bench');
    const root = () => box()?.querySelector('.ex-grid') ?? null;
    const scroller = () => root().querySelector(':scope > .ex-scroller');
    const state = () => document.getElementById('core-state');
    const click = (id) => {
        const button = document.getElementById(id);
        if (!button) throw new Error(`no control #${id} on the page`);
        button.click();
    };

    // The cells the user sees, by the accessibility surface (ADR-0033): absolute 1-based indices, the
    // header's row first.
    const dataRows = () => [...root().querySelectorAll('[role=row]')].filter((r) => r.querySelector('[role=gridcell]'));
    const cellText = (row, col) => row.querySelector(`[role=gridcell][aria-colindex="${col}"]`)?.textContent ?? null;
    const rowAt = (ariaRow) => root().querySelector(`[role=row][aria-rowindex="${ariaRow}"]`);
    const headers = () => [...root().querySelectorAll('[role=columnheader]')].map((h) => h.textContent.trim());
    const rowHeight = () => {
        const height = parseFloat(getComputedStyle(root()).getPropertyValue('--ex-row-height'));
        if (!(height > 0)) throw new Error('the root states no --ex-row-height');
        return height;
    };

    const focusId = () => {
        const r = root();
        return r?.querySelector('.ex-key-field')?.getAttribute('aria-activedescendant')
            ?? r?.getAttribute('aria-activedescendant') ?? '';
    };
    const focusAt = () => {
        const m = /r(\d+)c(\d+)$/.exec(focusId());
        return m ? { row: Number(m[1]), col: Number(m[2]) } : null;
    };
    const at = (row, col) => () => { const f = focusAt(); return f?.row === row && f?.col === col; };
    const key = (k, { ctrl = false } = {}) => {
        const a = document.activeElement;
        const target = a && root()?.contains(a) ? a : root();
        const code = /^[0-9]$/.test(k) ? `Digit${k}` : k;
        const init = { key: k, code, ctrlKey: ctrl, bubbles: true, cancelable: true, composed: true };
        for (const type of ['keydown', 'keyup']) target.dispatchEvent(new KeyboardEvent(type, init));
    };

    // Resolves with when the predicate first held: "dom" as the mutation that made it hold landed,
    // "frame" at the animation frame after it.
    const seen = (predicate, what, timeoutMs = 30_000) => new Promise((resolve, reject) => {
        let done = false;
        let observer = null;
        const start = performance.now();
        const finish = (dom) => {
            done = true;
            observer?.disconnect();
            requestAnimationFrame(() => resolve({ dom, frame: performance.now() }));
        };
        const check = () => {
            if (done) return;
            if (predicate()) { finish(performance.now()); return; }
            if (performance.now() - start > timeoutMs) {
                done = true;
                observer?.disconnect();
                reject(new Error(`timed out waiting for ${what}; the Focus is ${focusId() || 'none'}`));
            }
        };
        if (predicate()) { finish(performance.now()); return; }
        observer = new MutationObserver(check);
        observer.observe(document.body, { subtree: true, childList: true, characterData: true, attributes: true });
        const poll = () => { if (!done) { check(); requestAnimationFrame(poll); } };
        requestAnimationFrame(poll);
    });
    const timed = async (act, predicate, what) => {
        const t0 = performance.now();
        act();
        const s = await seen(predicate, what);
        return { dom: s.dom - t0, frame: s.frame - t0 };
    };

    // Bytes on the circuit, per connection; null where the host has no /api/wire (WebAssembly, React).
    const wire = async () => {
        try {
            const answer = await fetch('/api/wire');
            if (!answer.ok || !(answer.headers.get('content-type') ?? '').includes('json')) return null;
            return new Map((await answer.json()).map((c) => [c.id, c.written]));
        } catch {
            return null;
        }
    };
    const moved = (before, after) => {
        if (before === null || after === null) return null;
        let most = 0;
        for (const [id, written] of after) most = Math.max(most, written - (before.get(id) ?? 0));
        return most;
    };
    // Runs the operation and returns what it took and the bytes it put on the wire once it has landed.
    const metered = async (body) => {
        const before = await wire();
        const value = await body();
        await frames(20);
        return { value, bytes: moved(before, await wire()) };
    };

    const stats = (values) => {
        const sorted = [...values].sort((a, b) => a - b);
        const q = (p) => sorted[Math.min(sorted.length - 1, Math.floor(p * sorted.length))];
        return { n: sorted.length, p50: q(0.5), p95: q(0.95), max: sorted[sorted.length - 1] };
    };
    const lengths = async (count, each) => {
        const out = [];
        let last = performance.now();
        for (let i = 0; i < count; i++) {
            each(i);
            await frame();
            const now = performance.now();
            out.push(now - last);
            last = now;
        }
        return out;
    };
    // A phase that fails is recorded with what the user saw, and the round goes on.
    const phase = async (entry, name, body) => {
        try {
            entry[name] = await body();
        } catch (error) {
            entry[`${name}Failed`] = String(error.message ?? error);
        }
    };

    // The row at the top of the Viewport, painted: what a scroll must have reached.
    const topPainted = () => {
        const ariaRow = Math.floor(scroller().scrollTop / rowHeight()) + 2;
        return () => rowAt(ariaRow) !== null && rowAt(ariaRow).querySelector('[role=gridcell]') !== null;
    };
    const scrolling = async (count) => {
        const run = async (step) => {
            scroller().scrollTop = 0;
            await frames(10);
            const out = await lengths(count, () => { scroller().scrollTop += step; });
            const t0 = performance.now();
            const s = await seen(topPainted(), 'the top row painted after the scroll');
            const result = { frames: stats(out), lagDom: s.dom - t0 };
            scroller().scrollTop = 0;
            await frames(10);
            return result;
        };
        return { slow: await run(37), fling: await run(50 * rowHeight()) };
    };

    // --- grid, template --------------------------------------------------------------------------
    const gen = () => Number(state().dataset.gen);
    const books = () => dataRows().map((r) => cellText(r, 1));
    const allAt = (g) => () => { const b = books(); return b.length > 0 && b.every((t) => t?.startsWith(`G${g}-`)); };
    const rowZeroAt = (g) => () => books().includes(`G${g}-0`);
    const firstRowsAt = (g, k) => () => books().filter((t) => t?.startsWith(`G${g}-`)).length === k;
    const gridHome = async () => {
        scroller().scrollTop = 0;
        await seen(() => books().includes(`G${gen()}-0`), 'row 0 painted');
        await frames(5);
    };
    const prepare = async (id) => {
        click(id);
        await seen(() => state().dataset.prepared !== '', 'the page to prepare a Window');
        await frames(5);
        return Number(state().dataset.prepared);
    };

    const runGrid = async (opts, round) => {
        const entry = { sizes: {} };
        for (const size of opts.sizes) {
            const s = (entry.sizes[size] = {});
            await phase(s, 'pushAll', async () => {
                await gridHome();
                const g = await prepare(`core-prepare-all-${size}`);
                return metered(() => timed(() => click('core-push'), allAt(g), `every painted row at G${g}`));
            });
            await phase(s, 'pushOne', async () => {
                await gridHome();
                const g = await prepare('core-prepare-one');
                return metered(() => timed(() => click('core-push'), rowZeroAt(g), `row 0 at G${g}`));
            });
            for (const k of opts.ticks) {
                await phase(s, `tick${k}`, async () => {
                    await gridHome();
                    const once = await metered(async () => {
                        const out = [];
                        for (let i = 0; i < opts.tickRepeats; i++) {
                            const g = gen() + 1;
                            out.push(await timed(() => click(`core-tick-${k}`), firstRowsAt(g, k), `${k} rows at G${g}`));
                            await frames(3);
                        }
                        return out;
                    });
                    const perTick = once.bytes === null ? null : once.bytes / opts.tickRepeats;
                    const last = gen() + opts.frames;
                    const sustained = await metered(async () => {
                        const out = await lengths(opts.frames, () => click(`core-tick-${k}`));
                        const t0 = performance.now();
                        const s2 = await seen(firstRowsAt(last, k), `${k} rows at G${last}`);
                        return { frames: stats(out), lagDom: s2.dom - t0 };
                    });
                    return {
                        once: { dom: stats(once.value.map((v) => v.dom)), frame: stats(once.value.map((v) => v.frame)), bytesPerTick: perTick },
                        sustained: { ...sustained.value, bytesPerTick: sustained.bytes === null ? null : sustained.bytes / opts.frames },
                    };
                });
            }
        }
        await phase(entry, 'mount', async () => {
            // Every row at one generation first, untimed, so that "painted" means every painted row at it.
            await gridHome();
            const g = await prepare(`core-prepare-all-${opts.sizes[opts.sizes.length - 1]}`);
            click('core-push');
            await seen(allAt(g), `every painted row at G${g}`);
            await frames(10);
            click('core-unmount');
            await seen(() => root() === null, 'the grid gone');
            await frames(10);
            return metered(() => timed(() => click('core-mount'), () => root() !== null && allAt(g)(), 'the grid painted'));
        });
        await phase(entry, 'keys', async () => {
            await gridHome();
            const r = root();
            (r.querySelector('.ex-key-field') ?? r).focus();
            if (focusAt() === null) {
                key('ArrowDown');
                await seen(() => focusAt() !== null, 'a Focus');
            }
            key('Home', { ctrl: true });
            await seen(at(0, 0), 'the Focus at r0c0');
            await frames(5);
            const burst = await metered(async () => {
                const t0 = performance.now();
                for (let i = 0; i < opts.burst; i++) key('ArrowDown');
                const s = await seen(at(opts.burst, 0), `the Focus at r${opts.burst}c0`);
                return { dom: s.dom - t0, frame: s.frame - t0 };
            });
            key('Home', { ctrl: true });
            await seen(at(0, 0), 'the Focus back at r0c0');
            await frames(5);
            const out = await lengths(opts.frames, () => key('ArrowDown'));
            const t0 = performance.now();
            const s = await seen(at(opts.frames, 0), `the Focus at r${opts.frames}c0`);
            key('Home', { ctrl: true });
            await seen(at(0, 0), 'the Focus back at r0c0');
            return { burst: { ...burst.value, bytes: burst.bytes }, perFrame: { frames: stats(out), lagDom: s.dom - t0 } };
        });
        await phase(entry, 'scroll', () => scrolling(opts.frames));
        return entry;
    };

    // --- sheet -----------------------------------------------------------------------------------
    // Sheet row r is aria-rowindex r + 1; column B is aria-colindex 2. B(r) = A1 + r + 1.
    const everyBReads = (v) => () => {
        const rows = dataRows().filter((r) => Number(r.getAttribute('aria-rowindex')) >= 3);
        return rows.length > 0 && rows.every((r) => cellText(r, 2) === String(v + Number(r.getAttribute('aria-rowindex')) - 1 + 1));
    };
    const typeNumber = (v) => { for (const c of String(v)) key(c); key('Enter'); };
    const sheetFocus = async (col) => {
        const r = root();
        (r.querySelector('.ex-key-field') ?? r).focus();
        key('Home', { ctrl: true });
        await seen(at(0, 0), 'the Focus at A1');
        for (let i = 0; i < col; i++) key('ArrowRight');
        await seen(at(0, col), `the Focus at r0c${col}`);
        await frames(5);
    };
    const runSheet = async (opts, round) => {
        const entry = {};
        await phase(entry, 'editAll', async () => {
            const out = [];
            const meter = await metered(async () => {
                for (let i = 0; i < opts.edits; i++) {
                    await sheetFocus(0);
                    const v = 1000 + 100 * round + i;
                    out.push(await timed(() => typeNumber(v), everyBReads(v), `every B cell to read A1 = ${v}`));
                }
            });
            return { dom: stats(out.map((o) => o.dom)), frame: stats(out.map((o) => o.frame)), bytesPerEdit: meter.bytes === null ? null : meter.bytes / opts.edits };
        });
        await phase(entry, 'editOne', async () => {
            const out = [];
            const meter = await metered(async () => {
                for (let i = 0; i < opts.edits; i++) {
                    await sheetFocus(11);
                    const v = 5000 + 100 * round + i;
                    out.push(await timed(() => typeNumber(v), () => cellText(rowAt(2), 12) === String(v), `L1 to read ${v}`));
                }
            });
            return { dom: stats(out.map((o) => o.dom)), frame: stats(out.map((o) => o.frame)), bytesPerEdit: meter.bytes === null ? null : meter.bytes / opts.edits };
        });
        await phase(entry, 'scroll', () => scrolling(opts.frames));
        return entry;
    };

    // --- pivot -----------------------------------------------------------------------------------
    // ExPivot redraws at once when its last redraw was more than its interval (250 ms) ago: each
    // operation waits that long first, untimed, so what is timed is the redraw and not the wait.
    const settle = () => frames(24);
    const painted = (text) => [...root().querySelectorAll('[role=gridcell]')].some((c) => c.textContent === text);
    const runPivot = async (opts, round) => {
        const entry = {};
        await phase(entry, 'amend', async () => {
            const out = [];
            const meter = await metered(async () => {
                for (let i = 0; i < opts.edits; i++) {
                    await settle();
                    const before = state().dataset.expected;
                    out.push(await timed(() => click('core-amend'),
                        () => state().dataset.expected !== before && painted(state().dataset.expected), 'the first book\'s new P&L painted'));
                }
            });
            return { dom: stats(out.map((o) => o.dom)), frame: stats(out.map((o) => o.frame)), bytesPerOp: meter.bytes === null ? null : meter.bytes / opts.edits };
        });
        await phase(entry, 'layout', async () => {
            const out = [];
            const meter = await metered(async () => {
                for (let i = 0; i < opts.edits; i++) {
                    await settle();
                    const toProduct = state().dataset.columns === 'Currency';
                    const [show, hide] = toProduct ? ['Swap', 'USD'] : ['USD', 'Swap'];
                    out.push(await timed(() => click('core-layout'),
                        () => { const h = headers(); return h.includes(show) && !h.includes(hide); }, `the columns to show ${show}`));
                }
            });
            return { dom: stats(out.map((o) => o.dom)), frame: stats(out.map((o) => o.frame)), bytesPerOp: meter.bytes === null ? null : meter.bytes / opts.edits };
        });
        await phase(entry, 'scroll', () => scrolling(opts.frames));
        return entry;
    };

    const scenarios = { grid: runGrid, template: runGrid, sheet: runSheet, pivot: runPivot };

    window.coreBench = {
        // Interactive, not merely rendered: rows painted and the page's controls answering.
        ready: () => (root()?.querySelector('[role=gridcell]') ?? null) !== null,
        run: async (scenario, options = {}) => {
            const run = scenarios[scenario];
            if (!run) throw new Error(`no scenario ${scenario}`);
            const opts = { rounds: 5, sizes: [1000, 10000, 100000], ticks: [1, 5, 25], tickRepeats: 5, frames: 60, burst: 40, edits: 5, ...options };
            const result = {
                scenario,
                opts,
                env: { userAgent: navigator.userAgent, cores: navigator.hardwareConcurrency, devicePixelRatio, innerWidth, innerHeight },
                rounds: [],
            };
            await frames(30);
            for (let round = 0; round < opts.rounds; round++) result.rounds.push(await run(opts, round));
            return result;
        },
    };
})();
