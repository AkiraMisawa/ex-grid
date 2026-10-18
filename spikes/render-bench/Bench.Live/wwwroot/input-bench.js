// What the Input Order costs (ADR-0240, "What it may cost"), run in the page against SettledGrid, the
// same page as the Settled Mark's measurement. Every input is dispatched at machine speed, many in one
// task, as a script drives the grid: keys on the element holding the keyboard at the moment of each
// dispatch, presses on the Viewport (the target of every mouse event in the rows) at the cell's own
// coordinates. Each phase reads only what the user sees — the Focus the grid names
// (aria-activedescendant), the Cell Editor's text, whether a popover stands — at animation frames,
// never the Settled Mark, so both builds are measured the same way.
//
//   typing     `chars` printable keys typed in one task onto the editable column: ms until the Cell
//              Editor shows all of them in order; then Escape, ms until the edit is gone.
//   presses    `presses` clicks (mousedown, mouseup, click) on cells in one task, walking rows and
//              columns: ms until the Focus names the last cell pressed.
//   menuBurst  `menus` times Shift+F10 (open the Context Menu), Escape, ArrowDown, all in one task:
//              ms until the Focus names the row `menus` below and no popover stands. A dropped or
//              reordered key leaves the Focus elsewhere or a menu open, and the phase times out.
//   menuSteps  `menus` times: ContextMenu key, read until the popover stands; Escape, read until it is
//              gone. Each open and each close is timed, as a user would wait for it.
// Console errors and exceptions are collected by the driver (tools/cdp-input.mjs).
(() => {
    const frame = () => new Promise((resolve) => requestAnimationFrame(resolve));
    const frames = async (n) => { for (let i = 0; i < n; i++) await frame(); };
    const root = () => document.querySelector('#settled-bench .ex-grid');
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
    const popover = () => root()?.querySelector('.ex-popover') ?? null;
    const editor = () => root()?.querySelector('input.ex-editor[data-ex-cell]') ?? null;
    const keyTarget = () => {
        const a = document.activeElement;
        return a && a !== document.body ? a : root();
    };
    const key = (k, { shift = false, ctrl = false } = {}) => {
        const target = keyTarget();
        const init = { key: k, code: k.length === 1 ? `Key${k.toUpperCase()}` : k, shiftKey: shift, ctrlKey: ctrl, bubbles: true, cancelable: true, composed: true };
        for (const type of ['keydown', 'keyup']) target.dispatchEvent(new KeyboardEvent(type, init));
    };
    const until = async (predicate, what, timeoutMs = 20_000) => {
        const start = performance.now();
        while (!predicate()) {
            if (performance.now() - start > timeoutMs) {
                throw new Error(`timed out waiting for ${what}; the Focus is ${focusId()}, popover ${popover() !== null}, editor ${editor()?.value ?? 'none'}`);
            }
            await frame();
        }
        return performance.now();
    };
    const prefix = () => focusId().replace(/r\d+c\d+$/, '');
    const press = (row, col) => {
        const viewport = root().querySelector('.ex-viewport');
        const cell = document.getElementById(`${prefix()}r${row}c${col}`);
        if (!cell) throw new Error(`no cell r${row}c${col} painted`);
        const box = cell.getBoundingClientRect();
        const init = { clientX: box.left + box.width / 2, clientY: box.top + box.height / 2, button: 0, buttons: 1, detail: 1, bubbles: true, cancelable: true, composed: true, view: window };
        viewport.dispatchEvent(new MouseEvent('mousedown', init));
        viewport.dispatchEvent(new MouseEvent('mouseup', { ...init, buttons: 0 }));
        viewport.dispatchEvent(new MouseEvent('click', { ...init, buttons: 0 }));
    };
    // Back to the first cell, painted and settled: no popover, no edit, the scroller at the top.
    const home = async () => {
        await until(() => popover() === null && editor() === null, 'nothing open before home');
        root().querySelector(':scope > .ex-scroller').scrollTop = 0;
        await frames(10);
        key('Home', { ctrl: true });
        await until(at(0, 0), 'the Focus at r0c0');
        await frames(10);
    };

    // After a phase that failed: close whatever stands, so the next phase starts from nothing open.
    const recover = async () => {
        for (let i = 0; i < 10 && (popover() !== null || editor() !== null); i++) {
            key('Escape');
            await frames(30);
        }
    };
    // A phase that times out is recorded as failed, with what the user saw, and the round goes on.
    const phase = async (entry, name, body) => {
        try {
            await body();
        } catch (error) {
            entry[`${name}Failed`] = String(error.message ?? error);
            await recover();
        }
    };

    window.inputBench = {
        // Interactive, not merely prerendered: the Keyboard Field stands only once the script is attached.
        ready: () => !!root()?.querySelector('.ex-row') && !!root()?.querySelector('.ex-key-field'),
        hasMark: () => root()?.hasAttribute('data-ex-answered') === true,
        run: async ({ rounds = 7, chars = 20, presses = 20, menus = 10 } = {}) => {
            let t0;
            const r = root();
            (r.querySelector('.ex-key-field') ?? r).focus();
            key('ArrowDown');
            await until(() => focusId() !== '', 'a Focus');
            const result = { hasMark: window.inputBench.hasMark(), userAgent: navigator.userAgent, chars, presses, menus, rounds: [] };
            for (let round = 0; round < rounds; round++) {
                const entry = {};

                // typing: a printable key opens the editor holding it; the rest are held and typed in.
                await phase(entry, 'typing', async () => {
                    await home();
                    key('ArrowDown');
                    await until(at(1, 0), 'the Focus at r1c0');
                    await frames(5);
                    const text = Array.from({ length: chars }, (_, i) => String.fromCharCode(97 + ((i + round) % 26))).join('');
                    t0 = performance.now();
                    for (const c of text) key(c);
                    entry.typingMs = (await until(() => editor()?.value === text, `the editor to hold "${text}"`)) - t0;
                    await frames(5);
                    t0 = performance.now();
                    key('Escape');
                    entry.typingEscapeMs = (await until(() => editor() === null && at(1, 0)(), 'the edit to close')) - t0;
                });

                // presses: rows 2.., columns cycling 0..3, all in one task.
                await phase(entry, 'presses', async () => {
                    await home();
                    const targets = Array.from({ length: presses }, (_, i) => ({ row: 2 + i, col: (i + round) % 4 }));
                    const last = targets[targets.length - 1];
                    t0 = performance.now();
                    for (const { row, col } of targets) press(row, col);
                    entry.pressesMs = (await until(at(last.row, last.col), `the Focus at r${last.row}c${last.col}`)) - t0;
                });

                // menuBurst: open, close, move, `menus` times in one task.
                await phase(entry, 'menuBurst', async () => {
                    await home();
                    t0 = performance.now();
                    for (let i = 0; i < menus; i++) {
                        key('F10', { shift: true });
                        key('Escape');
                        key('ArrowDown');
                    }
                    entry.menuBurstMs = (await until(() => at(menus, 0)() && popover() === null, `the Focus at r${menus}c0 and no popover`, 10_000)) - t0;
                });

                // menuSteps: each open and close waited for.
                await phase(entry, 'menuSteps', async () => {
                    await home();
                    const opens = [];
                    const closes = [];
                    for (let i = 0; i < menus; i++) {
                        t0 = performance.now();
                        key('ContextMenu');
                        opens.push((await until(() => popover() !== null, 'the Context Menu to open')) - t0);
                        await frame();
                        t0 = performance.now();
                        key('Escape');
                        closes.push((await until(() => popover() === null, 'the Context Menu to close')) - t0);
                        await frame();
                    }
                    entry.menuOpenMs = opens;
                    entry.menuCloseMs = closes;
                });
                result.rounds.push(entry);
            }
            return result;
        },
    };
})();
