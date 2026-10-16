// The Settled Mark's measurement (ADR-0220), run in the page against SettledGrid. Keys are dispatched
// on the element holding the keyboard, as the grid's capture-phase listener hears any keydown: the
// gate, the core and the render are what is measured, the same in both builds. Each phase reads what
// the user sees — the Focus the grid names (aria-activedescendant) — at animation frames, never the
// mark, which only one build has.
//
//   typingBurst  `burst` ArrowDowns dispatched in one task, as a script types: ms until the Focus
//                names the last row.
//   typingFrames one ArrowDown per animation frame for `frames` frames, as auto-repeat types: the
//                frames' lengths, and ms from the last key until the Focus names its row.
//   scrolling    the scroller moved 37px per animation frame for `frames` frames: the frames' lengths.
// On the Server host, each burst's bytes on the wire too (GET /api/wire, Bench.Server).
(() => {
    const frame = () => new Promise((resolve) => requestAnimationFrame(resolve));
    const root = () => document.querySelector('#settled-bench .ex-grid');
    const focusId = () => {
        const r = root();
        return r?.querySelector('.ex-key-field')?.getAttribute('aria-activedescendant')
            ?? r?.getAttribute('aria-activedescendant') ?? '';
    };
    const press = (key, ctrl = false) => {
        const target = document.activeElement && root()?.contains(document.activeElement) ? document.activeElement : root();
        for (const type of ['keydown', 'keyup']) {
            target.dispatchEvent(new KeyboardEvent(type, { key, code: key, ctrlKey: ctrl, bubbles: true, cancelable: true, composed: true }));
        }
    };
    const until = async (predicate, timeoutMs = 20_000) => {
        const start = performance.now();
        while (!predicate()) {
            if (performance.now() - start > timeoutMs) {
                throw new Error(`timed out waiting; the Focus is ${focusId()}`);
            }
            await frame();
        }
        return performance.now();
    };
    const atRow = (row) => () => /r(\d+)c0$/.exec(focusId())?.[1] === String(row);
    const wire = async () => {
        try {
            const answer = await fetch('/api/wire');
            if (!answer.ok) return null;
            return (await answer.json()).reduce((sum, c) => sum + c.written, 0);
        } catch {
            return null;
        }
    };
    // Back to the first row, painted: the scroll first, since a Focus already on row 0 but scrolled
    // out of sight is not moved by Ctrl+Home, and so not brought back into view.
    const home = async () => {
        root().querySelector(':scope > .ex-scroller').scrollTop = 0;
        for (let i = 0; i < 10; i++) await frame();
        press('Home', true);
        await until(atRow(0));
        for (let i = 0; i < 10; i++) await frame();
    };
    const stats = (values) => {
        const sorted = [...values].sort((a, b) => a - b);
        const at = (q) => sorted[Math.min(sorted.length - 1, Math.floor(q * sorted.length))];
        return { n: sorted.length, p50: at(0.5), p95: at(0.95), max: sorted[sorted.length - 1], over20: sorted.filter((v) => v > 20).length };
    };

    window.settledBench = {
        ready: () => !!root()?.querySelector('.ex-row'),
        hasMark: () => root()?.hasAttribute('data-ex-answered') === true,
        run: async ({ rounds = 5, burst = 40, frames = 120 } = {}) => {
            const r = root();
            (r.querySelector('.ex-key-field') ?? r).focus();
            // A grid with no Focus places one on its first arrow; Ctrl+Home moves only a Focus there is.
            press('ArrowDown');
            await until(() => focusId() !== '');
            const result = { hasMark: window.settledBench.hasMark(), userAgent: navigator.userAgent, rounds: [] };
            await home();
            for (let round = 0; round < rounds; round++) {
                const entry = {};
                await home();
                const bytesBefore = await wire();
                const t0 = performance.now();
                for (let i = 0; i < burst; i++) press('ArrowDown');
                entry.typingBurstMs = (await until(atRow(burst))) - t0;
                // The renders and the answers a burst puts on the wire, once all of them have landed.
                for (let i = 0; i < 30; i++) await frame();
                const bytesAfter = await wire();
                entry.burstBytes = bytesBefore === null ? null : bytesAfter - bytesBefore;

                await home();
                const lengths = [];
                let last = performance.now();
                for (let i = 0; i < frames; i++) {
                    press('ArrowDown');
                    await frame();
                    const now = performance.now();
                    lengths.push(now - last);
                    last = now;
                }
                const lastKey = performance.now();
                entry.typingFramesLagMs = (await until(atRow(frames))) - lastKey;
                entry.typingFrames = stats(lengths);

                await home();
                const scroller = r.querySelector(':scope > .ex-scroller');
                const scrolled = [];
                last = performance.now();
                for (let i = 0; i < frames; i++) {
                    scroller.scrollTop += 37;
                    await frame();
                    const now = performance.now();
                    scrolled.push(now - last);
                    last = now;
                }
                entry.scrolling = stats(scrolled);
                result.rounds.push(entry);
            }
            return result;
        },
    };
})();
