import { test, expect, record } from './fixtures.mjs';

// What a grid leaves behind (Definition of Done §17), read from the browser's own
// counters over CDP: nodes and event listeners after mounting and disposing it fifty
// times (MEM-2), the module's listeners after one dispose (MEM-4), and the JS heap over
// a ten-minute scroll (MEM-5), with the managed heap beside it (MEM-6). Every count is
// taken after a forced collection, because a detached node is only gone once the
// collector says so.

/** The browser's counters, after the collector has run. */
async function counters(client) {
    await client.send('HeapProfiler.collectGarbage');
    await client.send('HeapProfiler.collectGarbage');
    const { metrics } = await client.send('Performance.getMetrics');
    const value = (name) => metrics.find((m) => m.name === name).value;
    return { nodes: value('Nodes'), listeners: value('JSEventListeners'), jsHeap: value('JSHeapUsedSize') };
}

/** /lifecycle, where a button mounts and disposes a grid bound to its own source. */
async function lifecycle(page) {
    await page.goto('/lifecycle');
    await expect(page.locator('#toggle-grid')).toBeVisible();
    const client = await page.context().newCDPSession(page);
    const scripts = new Map();
    client.on('Debugger.scriptParsed', (e) => scripts.set(e.scriptId, e.url));
    await client.send('Debugger.enable');
    await client.send('Performance.enable');

    const toggle = async (mount) => {
        await page.locator('#toggle-grid').click();
        if (mount) {
            await expect(page.locator('.ex-grid .ex-row').first()).toBeVisible();
        } else {
            await expect(page.locator('.ex-grid')).toHaveCount(0);
        }
    };

    return {
        client,
        // Mounted and listening: the module attaches after the grid's first render — a round
        // trip or two later on the Server host — and the grid says it is busy until then
        // (A11Y-20). Read before that, the root has none of the module's listeners yet (MEM-4
        // came back empty on Server, Chrome, 2026-09-27).
        mount: async () => {
            await toggle(true);
            await expect(page.locator('.ex-grid')).not.toHaveAttribute('aria-busy', /.*/);
        },
        dispose: () => toggle(false),
        cycle: async () => {
            await toggle(true);
            await toggle(false);
        },
        counters: () => counters(client),
        /** The listeners on whatever `expression` evaluates to, with the script that added each. */
        listenersOn: async (expression) => {
            const { result } = await client.send('Runtime.evaluate', { expression });
            const { listeners } = await client.send('DOMDebugger.getEventListeners', { objectId: result.objectId });
            return listeners.map((l) => ({
                type: l.type,
                capture: l.useCapture,
                script: (scripts.get(l.scriptId) ?? '').split('/').pop(),
            }));
        },
    };
}

const key = (l) => `${l.type}${l.capture ? ' (capture)' : ''} @${l.script}`;

/** What `after` holds that `before` did not, as a multiset. */
function added(before, after) {
    const left = before.map(key);
    return after.map(key).filter((k) => {
        const i = left.indexOf(k);
        if (i === -1) {
            return true;
        }
        left.splice(i, 1);
        return false;
    });
}

test('mounting and disposing the grid fifty times returns nodes and listeners to baseline (MEM-2)', async ({ page }) => {
    const grid = await lifecycle(page);
    const fresh = await grid.counters();
    const freshOnDocument = await grid.listenersOn('document');

    // One cycle before the baseline, and what it may leave is checked, not assumed.
    // Blazor delegates events: the first time any component on a page handles an event
    // name, the framework adds one listener for that name to the document and keeps it
    // for the page's life. The grid is the first thing on this page to handle scroll,
    // mousedown and the rest, so the first mount adds those — and nothing else may.
    await grid.cycle();
    // Read once the module has let go of the document, as MEM-4 reads it: the handle's dispose
    // is an interop call, a round trip after the grid leaves the page on the Server host, and
    // read before it lands the selectionchange listener still stood (CI, Edge on the Server
    // host, 2026-10-02). A listener the dispose never takes off still fails here.
    await expect.poll(async () => (await grid.listenersOn('document')).map(key)
        .filter((k) => /@ex-grid(\.[\w-]+)*\.js(\?v=[0-9a-f]{12})?$/.test(k)), { message: 'the first cycle\'s grid let go of the document' })
        .toEqual([]);
    const baseline = await grid.counters();
    const byFramework = added(freshOnDocument, await grid.listenersOn('document'));
    // Blazor's own script is blazor.webassembly.js on the WebAssembly host and
    // blazor.web.js on the Server host (§24).
    expect(byFramework.every((l) => /@blazor\.(webassembly|web)(\.\w+)?\.js$/.test(l)), byFramework.join(', ')).toBe(true);
    expect(baseline.listeners - fresh.listeners, `the first cycle added only ${byFramework.join(', ')}`)
        .toBe(byFramework.length);
    expect(baseline.nodes, 'the first cycle left no nodes').toBe(fresh.nodes);

    for (let i = 0; i < 50; i++) {
        await grid.cycle();
    }

    // Disposal finishes asynchronously (the handle's dispose is an interop call), so
    // the counts are read until they settle — and fail if they do not.
    let drift;
    await expect.poll(async () => {
        const after = await grid.counters();
        drift = { nodes: after.nodes - baseline.nodes, listeners: after.listeners - baseline.listeners };
        return Math.max(Math.abs(drift.nodes), Math.abs(drift.listeners));
    }, { message: 'nodes and listeners within ±2 of the baseline after 50 cycles' }).toBeLessThanOrEqual(2);
    record(test.info().project.name, { 'MEM-2': { cycles: 50, baseline: { nodes: baseline.nodes, listeners: baseline.listeners }, drift } });
});

test('disposal takes the module\'s listeners off the root, and the count comes back (MEM-4)', async ({ page }) => {
    const grid = await lifecycle(page);
    // The module is the Script Core's bundle (ex-grid-script) and the Blazor adapter over it (ex-grid-core).
    const moduleScript = /@ex-grid(-[\w]+)?(\.[\w-]+)*\.js(\?v=[0-9a-f]{12})?$/;
    const fromModule = (t) => expect.stringMatching(new RegExp(`^${t.replace(/[()]/g, '\\$&')} ${moduleScript.source}`));
    const moduleOnDocument = async () => (await grid.listenersOn('document')).map(key)
        .filter((k) => moduleScript.test(k));
    // Blazor's own delegated listeners land here (MEM-2 checks which). Mounted until it
    // listens, and read once the module has let go, so the baseline is not taken while an
    // attach or a dispose is still on the wire (a Server circuit's round trip).
    await grid.mount();
    await grid.dispose();
    await expect.poll(moduleOnDocument, { message: 'the warm-up grid let go of the document' }).toEqual([]);
    const baseline = await grid.counters();

    await grid.mount();
    await page.evaluate(() => { window.__disposedRoot = document.querySelector('.ex-grid'); });
    const attached = (await grid.listenersOn('window.__disposedRoot')).map(key).sort();
    // What the Script grid attaches to its root, each taken off by its dispose:
    // - every input the Input Root holds while a commit is pending, in the capture phase, ahead of
    //   the grid's own listeners (ADR-0380 section 3, ADR-0240): seventeen types;
    // - the grid's own capture-phase keys, press, release, focus and keyup (ADR-0018/0010, ADR-0080),
    //   the focus leaving it, the two clipboard events (ADR-0005), and the hover band's mousemove
    //   and mouseleave (ADR-0029);
    // - and the Blazor adapter's capture-phase key and press, which tell .NET how many of the
    //   numbered calls it was sent before the grid hears the input (ADR-0240 section 11).
    const held = ['auxclick', 'beforeinput', 'click', 'compositionend', 'compositionstart', 'compositionupdate',
        'contextmenu', 'copy', 'cut', 'dblclick', 'input', 'keydown', 'keyup', 'mousedown', 'mousemove', 'mouseup', 'paste']
        .map((t) => `${t} (capture)`);
    const own = ['keydown (capture)', 'mousedown (capture)', 'mouseup (capture)', 'focus (capture)', 'keyup (capture)',
        'focusout', 'copy', 'paste', 'mousemove', 'mouseleave'];
    const adapter = ['keydown (capture)', 'mousedown (capture)'];
    // Sorted as `attached` is, with the script after each type.
    expect(attached).toEqual([...held, ...own, ...adapter].map((t) => `${t} @`).sort().map((t) => fromModule(t.slice(0, -2))));
    // And none on the document: the Script grid follows the editor's caret on the editor itself
    // (its keyup and mouseup), where the C# grid heard `selectionchange` on the document (ADR-0051).
    expect(await moduleOnDocument()).toEqual([]);

    await grid.dispose();
    // Held on purpose, so what is still attached to it can be read after disposal.
    await expect.poll(async () => (await grid.listenersOn('window.__disposedRoot')).map(key),
        { message: 'no listener left on the disposed root' }).toEqual([]);
    await expect.poll(moduleOnDocument, { message: 'no listener of the module left on the document' }).toEqual([]);

    await page.evaluate(() => { delete window.__disposedRoot; });
    expect((await grid.counters()).listeners, 'the listener count is back to its baseline').toBe(baseline.listeners);
});

// MEM-5 runs ten minutes and MEM-6 is read at its end, so both run only when asked for:
// EXGRID_SOAK=1 (§22 Step 4). Skipped otherwise, by name — and a skip is a failure at
// sign-off unless results.md records the soak run that discharges it.
test('a ten-minute scripted scroll does not grow the JS heap (MEM-5, MEM-6)', async ({ page }, testInfo) => {
    test.skip(process.env.EXGRID_SOAK !== '1', 'the ten-minute soak runs with EXGRID_SOAK=1 (§22 Step 4)');
    const minutes = 10;
    const sampleEveryMs = 30_000;
    test.setTimeout((minutes + 3) * 60_000);

    await page.goto('/wide');
    await expect(page.locator(".ex-grid [id$='-r0c0']")).toHaveText('K-000000');
    const client = await page.context().newCDPSession(page);
    await client.send('Performance.enable');
    const managedHeap = () => page.evaluate(() => DotNet.invokeMethodAsync('ExGrid.DemoPages', 'ManagedHeapBytes'));
    const managedBefore = await managedHeap();

    // The script: ordinary scrolling a row a frame, bouncing between the ends of a
    // stretch; a sideways pan of a column a frame for half a second in every two; and
    // a fling to a new place every ten seconds. Seeded, so a run can be repeated.
    await page.evaluate(() => {
        const scroller = document.querySelector('.ex-scroller');
        let seed = 20260924;
        const next = () => {
            seed = (seed * 1103515245 + 12345) % 2147483648;
            return seed / 2147483648;
        };
        let direction = 1;
        let frame = 0;
        window.__soak = { running: true, frames: 0 };
        const tick = () => {
            if (!window.__soak.running) {
                return;
            }
            frame++;
            const maxTop = scroller.scrollHeight - scroller.clientHeight;
            const maxLeft = scroller.scrollWidth - scroller.clientWidth;
            if (frame % 600 === 0) {
                scroller.scrollTop = Math.floor(next() * maxTop);
            } else if (frame % 120 < 30) {
                scroller.scrollLeft = (scroller.scrollLeft + 90) % maxLeft;
            } else {
                if (scroller.scrollTop >= maxTop || frame % 900 === 0) {
                    direction = -1;
                } else if (scroller.scrollTop <= 0 || frame % 900 === 450) {
                    direction = 1;
                }
                scroller.scrollTop += 28 * direction;
            }
            window.__soak.frames = frame;
            requestAnimationFrame(tick);
        };
        requestAnimationFrame(tick);
    });

    const samples = [(await counters(client)).jsHeap];
    for (let elapsed = sampleEveryMs; elapsed <= minutes * 60_000; elapsed += sampleEveryMs) {
        // spec-lint: fixed-wait: a measurement's pacing: the heap is sampled at fixed times while the soak runs, and no state marks a sample's moment
        await page.waitForTimeout(sampleEveryMs);
        samples.push((await counters(client)).jsHeap);
    }
    const frames = await page.evaluate(() => {
        window.__soak.running = false;
        return window.__soak.frames;
    });
    const managedAfter = await managedHeap();

    const sorted = [...samples].sort((a, b) => a - b);
    const median = sorted[Math.floor(sorted.length / 2)];
    const last = samples.at(-1);
    record(testInfo.project.name, {
        'MEM-5': { minutes, sampleEveryMs, frames, jsHeapUsedSamples: samples, median, last },
        'MEM-6': { managedHeapBytesBefore: managedBefore, managedHeapBytesAfter: managedAfter },
    });

    expect(frames, 'the script scrolled').toBeGreaterThan(minutes * 60 * 10);
    expect(Math.abs(last - median) / median, `last ${last} against median ${median}: ${samples.join(', ')}`)
        .toBeLessThanOrEqual(0.2);
});
