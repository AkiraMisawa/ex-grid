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
        mount: () => toggle(true),
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
    const baseline = await grid.counters();
    const byFramework = added(freshOnDocument, await grid.listenersOn('document'));
    expect(byFramework.every((l) => /@blazor\.webassembly(\.\w+)?\.js$/.test(l)), byFramework.join(', ')).toBe(true);
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
    await grid.cycle(); // Blazor's own delegated listeners land here (MEM-2 checks which)
    const baseline = await grid.counters();

    await grid.mount();
    await page.evaluate(() => { window.__disposedRoot = document.querySelector('.ex-grid'); });
    const attached = (await grid.listenersOn('window.__disposedRoot')).map(key).sort();
    // The per-instance handle's five, on the instance root and nowhere else (ADR-0018):
    // the capture-phase keys, the pointer report and the two clipboard events.
    expect(attached).toEqual([
        'copy', 'keydown (capture)', 'mouseleave', 'mousemove', 'paste',
    ].map((t) => expect.stringMatching(new RegExp(`^${t.replace(/[()]/g, '\\$&')} @ex-grid(\\.\\w+)?\\.js$`))));

    await grid.dispose();
    // Held on purpose, so what is still attached to it can be read after disposal.
    await expect.poll(async () => (await grid.listenersOn('window.__disposedRoot')).map(key),
        { message: 'no listener left on the disposed root' }).toEqual([]);

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
    const managedHeap = () => page.evaluate(() => DotNet.invokeMethodAsync('ExGrid.DemoHost', 'ManagedHeapBytes'));
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
