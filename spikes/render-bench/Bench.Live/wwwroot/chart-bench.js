// ExChart's measurement (ADR-0283, M4), run in the page against ChartBench: 1,566 dates × six series.
// Inputs are dispatched on the elements ex-chart.js and Blazor listen on, at machine speed. Each
// operation reads only what the user sees, and is timed twice: `dom`, when a MutationObserver sees the
// change land in the document, and `frame`, at the animation frame after it, where the browser paints.
//
//   pointer   `moves` pointer moves onto another position, each waited for: ms until the tooltip
//             names the new date. Then one move per animation frame for `frames` frames, as a hand
//             moves a mouse: the frames' lengths, and ms from the last move until its date shows.
//   hide      a legend entry clicked `toggles` times, hiding and showing a series in turn: ms until
//             the entry reads hidden (shown), which is the render that redraws the plot without it.
//   range     a drag across the plot `drags` times: ms until the range is narrowed (Reset range is
//             enabled); then a double-click, ms until the whole range is back.
//   replace   "Next run" clicked `runs` times, as a Consumer hands over tomorrow's data: ms until the
//             first series' path is redrawn. The proof of concept's 644 ms (Server) and 1.8 s
//             (WebAssembly) were this redraw.
// On the Server host, each phase's bytes on the wire too (GET /api/wire, Bench.Server).
(() => {
    let phase = 'start';
    // performance.now() in the callback, not its argument: that is the frame's start, before the change.
    const frame = () => new Promise((resolve) => requestAnimationFrame(() => resolve(performance.now())));
    const root = () => document.querySelector('#chart-bench .ex-chart');
    const hit = () => root()?.querySelector('.ex-chart-hit[data-ex-panel="0"]');
    const tooltipDate = () => root()?.querySelector('.ex-chart-tooltip-position')?.textContent ?? '';
    const reset = () => root()?.querySelector('.ex-chart-reset');
    const firstPath = () => root()?.querySelector('.ex-chart-series .ex-chart-path')?.getAttribute('d') ?? '';
    const entry = (key) => root()?.querySelector(`.ex-chart-legend-entry[data-series="${key}"]`);

    // Resolves with the time the predicate first held, as the document changed; and the frame after.
    const until = (predicate, timeoutMs = 30_000) => new Promise((resolve, reject) => {
        const settle = (dom) => {
            observer.disconnect();
            clearTimeout(timer);
            frame().then((painted) => resolve({ dom, frame: painted }));
        };
        const observer = new MutationObserver(() => {
            if (predicate()) settle(performance.now());
        });
        const timer = setTimeout(() => {
            observer.disconnect();
            reject(new Error(`timed out waiting in ${phase}; the tooltip reads "${tooltipDate()}", Reset range is ${reset()?.disabled ? 'disabled' : 'enabled'}`));
        }, timeoutMs);
        observer.observe(document.body, { subtree: true, childList: true, attributes: true, characterData: true });
        if (predicate()) settle(performance.now());
    });
    const pointer = (type, target, x, buttons = 0) => {
        const box = target.getBoundingClientRect();
        target.dispatchEvent(new PointerEvent(type, {
            pointerId: 1, pointerType: 'mouse', isPrimary: true, button: type === 'pointermove' ? -1 : 0, buttons,
            clientX: box.left + x, clientY: box.top + box.height / 2, bubbles: true, cancelable: true, composed: true,
        }));
    };
    const click = (target, type = 'click') => target.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, composed: true, detail: type === 'dblclick' ? 2 : 1 }));
    const wire = async () => {
        try {
            const answer = await fetch('/api/wire');
            if (!answer.ok) return null;
            return (await answer.json()).reduce((sum, c) => sum + c.written, 0);
        } catch {
            return null;
        }
    };
    const stats = (values) => {
        const sorted = [...values].sort((a, b) => a - b);
        const at = (q) => sorted[Math.min(sorted.length - 1, Math.floor(q * sorted.length))];
        const r = (v) => Math.round(v * 100) / 100;
        return { n: sorted.length, p50: r(at(0.5)), p95: r(at(0.95)), max: r(sorted[sorted.length - 1]) };
    };
    // Times one operation: `act` dispatches it, `seen` says the user sees its result.
    const timed = async (act, seen, into) => {
        const t0 = performance.now();
        act();
        const at = await until(seen);
        into.dom.push(at.dom - t0);
        into.frame.push(at.frame - t0);
    };
    const series = () => ({ dom: [], frame: [] });
    const summary = (s) => ({ dom: stats(s.dom), frame: stats(s.frame) });
    const quiet = async (n = 10) => { for (let i = 0; i < n; i++) await frame(); };
    const onWire = async (work) => {
        const before = await wire();
        await work();
        await quiet(30);
        const after = await wire();
        return before === null ? null : after - before;
    };

    window.chartBench = {
        ready: () => !!hit() && !!root()?.querySelector('.ex-chart-series'),
        run: async ({ rounds = 5, moves = 40, frames = 120, toggles = 10, drags = 10, runs = 10 } = {}) => {
            const layer = hit();
            const width = layer.getBoundingClientRect().width;
            // The pointer entry is attached after the first render, and a move before it is heard by
            // nothing: moved again each frame until a tooltip says it is attached. Not measured.
            for (let i = 0; tooltipDate() === ''; i++) {
                if (i > 1800) throw new Error('the pointer entry never answered a move');
                pointer('pointermove', layer, width / 2 + (i % 2) * 7);
                await frame();
            }
            const result = { userAgent: navigator.userAgent, dates: 1566, series: 6, plotWidth: width, rounds: [] };
            for (let round = 0; round < rounds; round++) {
                const r = {};

                // Each step is 7 px, past the ~0.6 px between two of 1,566 positions on the plot.
                phase = 'pointer';
                const move = series();
                let x = 10;
                r.pointerBytes = await onWire(async () => {
                    for (let i = 0; i < moves; i++) {
                        const before = tooltipDate();
                        x = 10 + ((x - 10 + 7 * 13) % (width - 20));
                        const at = x;
                        await timed(() => pointer('pointermove', layer, at), () => tooltipDate() !== before, move);
                    }
                });
                r.pointer = summary(move);

                const lengths = [];
                const xs = [];
                await quiet();
                let last = performance.now();
                for (let i = 0; i < frames; i++) {
                    const at = 10 + ((i * 7) % (width - 20));
                    xs.push(at);
                    pointer('pointermove', layer, at);
                    await frame();
                    const now = performance.now();
                    lengths.push(now - last);
                    last = now;
                }
                const lastMove = performance.now();
                // The last move's date: a probe on the same offset after the sweep has settled would
                // move nothing, so the lag is read as the tooltip coming to rest.
                let stable = tooltipDate();
                let restAt = lastMove;
                for (let still = 0; still < 10; still++) {
                    await frame();
                    if (tooltipDate() !== stable) { stable = tooltipDate(); restAt = performance.now(); still = -1; }
                }
                r.sweep = { frames: stats(lengths), lagMs: Math.round((restAt - lastMove) * 100) / 100 };
                layer.dispatchEvent(new PointerEvent('pointerleave', { pointerId: 1, pointerType: 'mouse' }));
                root().dispatchEvent(new PointerEvent('pointerleave', { pointerId: 1, pointerType: 'mouse' }));
                await quiet();

                phase = 'hide';
                const hide = series();
                const keys = ['epe-before', 'pfe-after', 'ene-after'];
                r.hideBytes = await onWire(async () => {
                    for (let i = 0; i < toggles; i++) {
                        const target = entry(keys[i % keys.length]);
                        const wasHidden = target.classList.contains('ex-chart-hidden');
                        await timed(() => click(target), () => entry(keys[i % keys.length]).classList.contains('ex-chart-hidden') !== wasHidden, hide);
                    }
                });
                // Show whatever an odd count left hidden.
                for (const key of keys) {
                    if (entry(key).classList.contains('ex-chart-hidden')) {
                        click(entry(key));
                        await until(() => !entry(key).classList.contains('ex-chart-hidden'));
                    }
                }
                r.hide = summary(hide);
                await quiet();

                phase = 'range';
                const narrow = series();
                const widen = series();
                r.rangeBytes = await onWire(async () => {
                    for (let i = 0; i < drags; i++) {
                        const from = width * (0.2 + 0.02 * i);
                        const to = width * (0.7 - 0.02 * i);
                        // A drag carries the drawing it began on, and is refused once another is shown
                        // (spine rule 6): the pointer comes to rest first, as a hand's does, until the
                        // tooltip says the pointer entry has the drawing the widening drew.
                        pointer('pointermove', hit(), from);
                        await until(() => tooltipDate() !== '');
                        await timed(() => {
                            const l = hit();
                            pointer('pointerdown', l, from, 1);
                            pointer('pointermove', l, to, 1);
                            pointer('pointerup', l, to, 0);
                        }, () => reset() && !reset().disabled, narrow);
                        await timed(() => click(hit(), 'dblclick'), () => reset() && reset().disabled, widen);
                    }
                });
                r.narrow = summary(narrow);
                r.widen = summary(widen);
                root().dispatchEvent(new PointerEvent('pointerleave', { pointerId: 1, pointerType: 'mouse' }));
                await quiet();

                phase = 'replace';
                const replace = series();
                r.replaceBytes = await onWire(async () => {
                    for (let i = 0; i < runs; i++) {
                        const before = firstPath();
                        await timed(() => click(document.getElementById('chart-next-run')), () => firstPath() !== before, replace);
                    }
                });
                r.replace = summary(replace);
                result.rounds.push(r);
            }
            return result;
        },
    };
})();
