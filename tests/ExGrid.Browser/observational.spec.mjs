import { test, expect, record } from './fixtures.mjs';

// The observational numbers (Definition of Done §1, §16-§18): recorded in metrics.json
// and compared by a person, never gated. What IS asserted here is only that each
// measurement measured something — a record of zeros would be a silent failure of the
// harness, not a fast grid.
//
// All of it on /wide, the Definition of Done's scenario (§12): 10⁶ rows × 100 columns,
// 28px rows, a 900×600 Viewport, two Pinned Columns — with horizontal virtualisation on
// (about 220 cells in the DOM) and off (about 2,200), ADR-0004's two columns.

// The whole 600px Viewport has to be inside the window: Playwright's default window is
// 720px tall, which leaves the grid's lower rows off screen, where a pointer hits nothing
// and a browser may skip what it would otherwise paint.
test.use({ viewport: { width: 1280, height: 1000 } });

const FLING_PX = 1800; // three Viewports: past ADR-0004's one-Viewport fling threshold

function median(values) {
    const sorted = [...values].sort((a, b) => a - b);
    return sorted[Math.floor(sorted.length / 2)];
}

function summary(values) {
    const sorted = [...values].sort((a, b) => a - b);
    const round = (v) => Math.round(v * 100) / 100;
    return {
        count: values.length,
        median: round(median(values)),
        min: round(sorted[0]),
        p95: round(sorted[Math.min(sorted.length - 1, Math.floor(sorted.length * 0.95))]),
        max: round(sorted.at(-1)),
    };
}

/** Script + style + layout time the renderer has spent so far, in ms (CDP's own totals). */
async function workMs(client) {
    const { metrics } = await client.send('Performance.getMetrics');
    const seconds = (name) => metrics.find((m) => m.name === name).value;
    return (seconds('ScriptDuration') + seconds('RecalcStyleDuration') + seconds('LayoutDuration')) * 1000;
}

/** Resolves after `count` animation frames have been produced. */
function frames(page, count = 2) {
    return page.evaluate((count) => new Promise((resolve) => {
        let left = count;
        const tick = () => (--left === 0 ? resolve() : requestAnimationFrame(tick));
        requestAnimationFrame(tick);
    }), count);
}

async function openWide(page) {
    await page.goto('/wide');
    await expect(page.locator(".ex-grid [id$='-r0c0']")).toHaveText('K-000000');
    const client = await page.context().newCDPSession(page);
    await client.send('Performance.enable');
    return client;
}

async function setVirtualised(page, on) {
    const button = page.locator('#toggle-virtualise');
    if (((await button.textContent()) ?? '').includes(on ? ': off' : ': on')) {
        await button.click();
    }
    await expect(button).toContainText(on ? ': on' : ': off');
    await page.waitForTimeout(400);
}

async function domCounts(page) {
    return page.evaluate(() => {
        const g = document.querySelector('.ex-grid');
        return {
            elements: g.querySelectorAll('*').length,
            cells: g.querySelectorAll('.ex-cell').length,
            rows: g.querySelectorAll('.ex-row').length,
        };
    });
}

/**
 * The repaint after a fling settles (ADR-0004's table): the fling itself is let paint
 * its Placeholders first, and only then does the clock start, so what is counted is the
 * one frame that swaps them for real cells. Median of `samples`.
 */
async function settleRepaints(page, client, samples) {
    const costs = [];
    for (let i = 0; i < samples; i++) {
        await page.evaluate((px) => {
            const scroller = document.querySelector('.ex-scroller');
            const max = scroller.scrollHeight - scroller.clientHeight;
            scroller.scrollTop = scroller.scrollTop + px > max ? 0 : scroller.scrollTop + px;
        }, FLING_PX);
        await frames(page, 2);
        await page.waitForTimeout(30); // well inside the 150 ms settle delay
        const before = await workMs(client);
        await page.waitForTimeout(400);
        await frames(page, 2);
        costs.push((await workMs(client)) - before);
    }
    return costs;
}

/** Frame intervals from requestAnimationFrame while scrolling by `step` px a frame. */
async function frameIntervals(page, axis, step, count) {
    return page.evaluate(({ axis, step, count }) => new Promise((resolve) => {
        const scroller = document.querySelector('.ex-scroller');
        const intervals = [];
        let last;
        const tick = (now) => {
            if (last !== undefined) {
                intervals.push(now - last);
            }
            last = now;
            if (intervals.length === count) {
                resolve(intervals);
                return;
            }
            const max = axis === 'scrollTop'
                ? scroller.scrollHeight - scroller.clientHeight
                : scroller.scrollWidth - scroller.clientWidth;
            scroller[axis] = scroller[axis] + step > max ? 0 : scroller[axis] + step;
            requestAnimationFrame(tick);
        };
        requestAnimationFrame(tick);
    }), { axis, step, count });
}

test('mount to first painted row at 10⁶ rows (BIG-7)', async ({ page }, testInfo) => {
    const started = Date.now();
    await page.goto('/wide');
    await expect(page.locator('.ex-grid .ex-row').first()).toBeVisible();
    const firstPaintMs = Date.now() - started;

    record(testInfo.project.name, {
        'BIG-7': { mountToFirstPaintedRowMs: firstPaintMs, totalRows: 1_000_000 },
    });
    expect(firstPaintMs).toBeGreaterThan(0);
});

test('the DOM, the settle repaint and the frame intervals, virtualised and not (DOM-5, PF-6, BIG-6)', async ({ page }, testInfo) => {
    test.setTimeout(180_000);
    const client = await openWide(page);
    const results = {};

    for (const setting of ['on', 'off']) {
        await setVirtualised(page, setting === 'on');
        const dom = await domCounts(page);
        const settle = await settleRepaints(page, client, 8);
        await page.evaluate(() => { document.querySelector('.ex-scroller').scrollTop = 0; });
        await page.waitForTimeout(400);
        const ordinaryRows = await frameIntervals(page, 'scrollTop', 28, 150);
        const ordinaryColumns = await frameIntervals(page, 'scrollLeft', 90, 150);
        const fling = await frameIntervals(page, 'scrollTop', FLING_PX, 30);
        await page.waitForTimeout(400);

        results[setting] = {
            dom,
            settleRepaintMs: summary(settle),
            ordinaryScrollFrameMs: { rows: summary(ordinaryRows), columns: summary(ordinaryColumns) },
            flingFrameMs: summary(fling),
        };
        expect(dom.cells, `cells painted with virtualisation ${setting}`).toBeGreaterThan(0);
        expect(median(settle), `a settle repaint was measured (${setting})`).toBeGreaterThan(0);
    }

    record(testInfo.project.name, {
        // Compared with ADR-0004's 279 / 2,326 elements and 220 / 2,200 cells.
        'DOM-5': { on: results.on.dom, off: results.off.dom },
        // Compared with ADR-0004's 16.3 ms / 49.8 ms settle and 8.3 ms frames.
        'PF-6': results,
        // The same measurement at 10⁶ rows, with the setting's default (on).
        'BIG-6': {
            totalRows: 1_000_000,
            settleRepaintMs: results.on.settleRepaintMs,
            sustainedScrollFrameMs: results.on.ordinaryScrollFrameMs.rows,
        },
    });
    // The setting is what separates them, so the two must not have measured the same DOM.
    expect(results.off.dom.cells).toBeGreaterThan(results.on.dom.cells);
});

test('a selection drag, one row a step, costs this much per step (PF-7)', async ({ page }, testInfo) => {
    const client = await openWide(page);
    const scroller = await page.locator('.ex-scroller').boundingBox();
    const status = page.locator('#selection-status');
    // ADR-0008 dragged a 10 × 8 rectangle's edge one row at a time. Beside the pinned
    // block a 900px Viewport shows seven whole scrollable columns, and a pointer past
    // the Viewport's edge is outside the grid, so this is 10 × 7 — one overlay layer,
    // as there. Its bottom edge moves down five rows and back up five, so the pointer
    // never reaches the edge band and scrolling stays held, as it was held there.
    const cellAt = async (row, column) => {
        const box = await page.locator(`.ex-grid [id$='-r${row}c${column}']`).boundingBox();
        return { x: box.x + box.width / 2, y: box.y + box.height / 2 };
    };
    const from = await cellAt(3, 2);
    const to = await cellAt(12, 8);
    expect(to.x).toBeLessThan(scroller.x + scroller.width - 20);
    expect(to.y + 5 * 28).toBeLessThan(scroller.y + scroller.height - 40);

    await page.mouse.move(from.x, from.y);
    await page.mouse.down();
    await page.mouse.move(to.x, to.y, { steps: 4 });
    await expect(status).toContainText(/Selected: 70\b/);

    const costs = [];
    let rows = 10;
    for (let step = 0; step < 40; step++) {
        const delta = Math.floor(step / 5) % 2 === 0 ? 1 : -1;
        rows += delta;
        const before = await workMs(client);
        await page.mouse.move(to.x, to.y + (rows - 10) * 28);
        await frames(page, 2);
        costs.push((await workMs(client)) - before);
        // Every step moved the selection; a step that did not would be timing nothing.
        await expect(status).toContainText(new RegExp(`Selected: ${rows * 7}\\b`));
    }
    await page.mouse.up();

    record(testInfo.project.name, {
        // Compared with ADR-0008's overlay maximum, 2.10 ms (19.70 ms for per-cell classes).
        'PF-7': { rectangle: '10 rows × 7 columns, edge moved ±5 rows', steps: costs.length, stepCostMs: summary(costs) },
    });
    expect(median(costs)).toBeGreaterThan(0);
});
