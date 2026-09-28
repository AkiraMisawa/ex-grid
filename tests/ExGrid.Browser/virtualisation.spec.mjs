import { test, expect } from './fixtures.mjs';

// The structural invariants that produce the timing (Definition of Done §1), at the
// Definition of Done's own scenario (§12): /wide, 1,000,000 rows × 100 columns, 28px
// rows, a 900×600 Viewport, two Pinned Columns. DOM size as a function of the Viewport
// and never of TotalCount, the far corner reachable and painted right, a selection of
// 10⁸ cells costing one rectangle, and no interop that grows with what is painted.
// Console errors fail the run, exactly as everywhere (fixtures.mjs).

const ROWS = 1_000_000;
const BOOKS = ['Rates', 'Credit', 'FX', 'Equity', 'Commodity'];

async function elementCount(page) {
    return page.evaluate(() => document.querySelector('.ex-grid').querySelectorAll('*').length);
}

/** A cell by its absolute position — the id ends -r{row}c{column} (ADR-0033). */
function cell(page, row, column) {
    return page.locator(`.ex-grid [id$='-r${row}c${column}']`);
}

/** What /wide's data says a row holds (DemoData.WideColumns), checked against what is painted. */
async function expectRowPainted(page, row) {
    await expect(cell(page, row, 0)).toHaveText(`K-${String(row).padStart(6, '0')}`, { timeout: 15_000 });
    await expect(cell(page, row, 1)).toHaveText(BOOKS[row % BOOKS.length]);
    // M01, the first scrollable column: ((row·7919 + 2·104729) mod 1,999,999) / 100.
    const expected = ((row * 7919 + 2 * 104729) % 1_999_999) / 100;
    expect(Number(await cell(page, row, 2).textContent())).toBeCloseTo(expected, 2);
}

async function openWide(page, rows) {
    await page.goto(rows === undefined ? '/wide' : `/wide?rows=${rows}`);
    const grid = page.locator('.ex-grid');
    await expect(grid).toHaveAttribute('aria-rowcount', String(rows ?? ROWS));
    await expectRowPainted(page, 0);
    return grid;
}

test('the far corner is reachable and painted at 10⁶ rows (BIG-1)', async ({ page }) => {
    const grid = await openWide(page);

    await cell(page, 0, 0).click({ force: true });
    await page.keyboard.press('ControlOrMeta+End');

    // The last row lands and paints real cells; the pinned columns are present.
    await expect(cell(page, ROWS - 1, 99)).toBeVisible({ timeout: 15_000 });
    await expect(grid.locator('.ex-cell.ex-pinned').first()).toBeVisible();

    await page.keyboard.press('ControlOrMeta+Home');
    await expect(cell(page, 0, 0)).toBeVisible({ timeout: 15_000 });
});

// ADR-0053: a million rows of 28 px are 28,000,000 px, and at 150% Chrome lays out nothing
// taller than 22,369,618 CSS px. Above the Layout Ceiling the spacer is compressed; below
// it, it is the true height. Either way it must be laid out exactly as declared — a clamped
// spacer is the silent failure — and the last row must end flush with the readable bottom.
test('the spacer is laid out as declared under the Layout Ceiling, and the last row ends flush (VZ-15)', async ({ page }, testInfo) => {
    const grid = await openWide(page);
    const measure = () => page.evaluate(() => {
        const root = document.querySelector('.ex-grid');
        const spacer = root.querySelector('.ex-spacer');
        return {
            // Read from the attribute: the CSSOM serialises a length to six significant
            // figures, so spacer.style.height says 2.23694e+07px for 22,369,362.
            declared: Number(/height: ([\d.]+)px/.exec(spacer.getAttribute('style'))[1]),
            laidOut: spacer.getBoundingClientRect().height,
            ceiling: root.querySelector('.ex-ceiling-probe > div').getBoundingClientRect().height,
            dpr: window.devicePixelRatio,
        };
    });
    // The ceiling is told after attach; the spacer follows it within a render.
    await expect.poll(async () => {
        const { declared, ceiling } = await measure();
        return declared <= ceiling;
    }, { timeout: 10_000 }).toBe(true);
    const measured = await measure();
    testInfo.annotations.push({ type: 'measured', description: JSON.stringify(measured) });

    expect(Math.abs(measured.laidOut - measured.declared), 'the spacer was clamped').toBeLessThanOrEqual(1);
    if (testInfo.project.name === 'chrome-150') {
        // What makes this project a test at all: the true height does not fit here.
        expect(measured.ceiling, 'the display scale did not move the ceiling, so this proves nothing')
            .toBeLessThan(ROWS * 28);
        expect(measured.declared).toBeLessThan(ROWS * 28);
    }

    await page.evaluate(() => {
        const scroller = document.querySelector('.ex-scroller');
        scroller.scrollTop = scroller.scrollHeight;
    });
    await expectRowPainted(page, ROWS - 1);
    const edges = await page.evaluate((last) => {
        const scroller = document.querySelector('.ex-scroller');
        const box = scroller.getBoundingClientRect();
        const row = document.querySelector(`.ex-grid [id$='-r${last}c0']`).getBoundingClientRect();
        return { readableBottom: box.top + scroller.clientTop + scroller.clientHeight, lastBottom: row.bottom };
    }, ROWS - 1);
    expect(Math.abs(edges.lastBottom - edges.readableBottom), JSON.stringify(edges)).toBeLessThanOrEqual(1);

    // A selection of every row is painted as the visible part of one rectangle, never laid
    // out a million rows tall — which the browser would have clamped like the spacer.
    await cell(page, ROWS - 1, 0).click({ force: true });
    await page.keyboard.press('ControlOrMeta+A');
    await expect(grid.locator('.ex-selection .ex-range')).toHaveCount(1);
    const heights = await page.evaluate(() => ({
        range: document.querySelector('.ex-selection .ex-range').getBoundingClientRect().height,
        scroller: document.querySelector('.ex-scroller').clientHeight,
    }));
    expect(heights.range, JSON.stringify(heights)).toBeLessThanOrEqual(heights.scroller + (3 * 28));
});

test('the element count is the same at 10³, 10⁵ and 10⁶ rows (VZ-1, BIG-2, DOM-1)', async ({ page }) => {
    const counts = {};
    for (const rows of [1_000, 100_000, ROWS]) {
        await openWide(page, rows);
        await page.waitForTimeout(400); // past the settle delay: nothing is a Placeholder
        counts[rows] = await elementCount(page);
    }

    // Identical, not close: the same Viewport paints the same rows and columns whatever
    // the total, and nothing under the root is a function of it (ADR-0004 P1).
    expect(counts, JSON.stringify(counts)).toEqual({ 1000: counts[ROWS], 100000: counts[ROWS], [ROWS]: counts[ROWS] });
});

test('the first and the last row paint their own data, there and back again (BIG-5)', async ({ page }) => {
    await openWide(page);

    await page.evaluate(() => {
        const scroller = document.querySelector('.ex-scroller');
        scroller.scrollTop = scroller.scrollHeight;
    });
    await expectRowPainted(page, ROWS - 1);

    await page.evaluate(() => { document.querySelector('.ex-scroller').scrollTop = 0; });
    await expectRowPainted(page, 0);
    // And the far end once more: the round trip left nothing stale behind.
    await page.evaluate(() => {
        const scroller = document.querySelector('.ex-scroller');
        scroller.scrollTop = scroller.scrollHeight;
    });
    await expectRowPainted(page, ROWS - 1);
});

// ADR-0053 / ADR-0028: a change of the Layout Ceiling keeps the first visible row and writes
// the anchored offset to the browser. On a Server circuit the ceiling can be told after the grid
// stopped being busy, and a scroll made in between is still on the wire when the anchor is
// taken: the anchor, computed from row 0, was written over it and the grid went back to the top
// (BIG-5 on chrome-150, 2 of 4 on Windows, verification/2026-09-28-windows-3). So the scroll is
// made here in the very task in which the grid stops being busy, before the ceiling can have
// been heard, and it must not be undone. At scale 1 nothing is compressed and this is BIG-5's
// own case: the last row.
//
// Where the rows land when compressed depends on which the grid heard first. The ceiling first
// (the usual order on a circuit): the scroll is read through the compressed geometry, and the
// last row is painted. The scroll first (always on WebAssembly, where the offset is read within
// the frame): the untold geometry showed row 798,894 there, and ADR-0053's anchor keeps it, with
// the thumb moved to match. Either way the view is where the scroll took it, never the top.
test('a scroll to the end made as the grid becomes ready is not undone by the Layout Ceiling (BIG-5, ADR-0053)', async ({ page }, testInfo) => {
    await page.addInitScript(() => {
        let done = false;
        // The circuit replaces the prerendered grid, so the ready one is found wherever it is.
        new MutationObserver(() => {
            const root = done ? null : document.querySelector('.ex-grid:not([aria-busy])');
            if (!root) {
                return;
            }
            done = true;
            const scroller = root.querySelector('.ex-scroller');
            const spacer = scroller.querySelector('.ex-spacer');
            window.__spacerAtScroll = Number(/height: ([\d.]+)px/.exec(spacer.getAttribute('style'))[1]);
            scroller.scrollTop = scroller.scrollHeight;
        }).observe(document, { subtree: true, childList: true, attributes: true, attributeFilter: ['aria-busy'] });
    });
    await page.goto('/wide');
    await expect(page.locator('.ex-grid')).toHaveAttribute('aria-rowcount', String(ROWS));
    const firstRow = async () => Number(await page.locator('.ex-grid .ex-viewport').getAttribute('data-ex-first-row'));

    await expect.poll(firstRow, { timeout: 15_000 }).toBeGreaterThan(ROWS / 2);
    await page.waitForTimeout(400); // past the settle delay, and past any anchor still on its way
    const first = await firstRow();
    // Recorded, since which the grid heard first is the browser's timing, not the test's.
    testInfo.annotations.push({
        type: 'measured',
        description: JSON.stringify({ spacerAtScroll: await page.evaluate(() => window.__spacerAtScroll), first }),
    });
    expect(first, 'the scroll was undone').toBeGreaterThan(ROWS / 2);
    await expectRowPainted(page, first + 1);
    // Uncompressed, the geometry never changed under the scroll: the end is the end.
    const declared = await page.evaluate(() => Number(/height: ([\d.]+)px/.exec(document.querySelector('.ex-spacer').getAttribute('style'))[1]));
    if (declared >= ROWS * 28) {
        await expectRowPainted(page, ROWS - 1);
    }
});

test('scrolling changes which rows exist, not how many elements do (ADR-0004 P1, across a scroll)', async ({ page }) => {
    await openWide(page);

    const atTop = await elementCount(page);
    await page.evaluate(() => { document.querySelector('.ex-scroller').scrollTop = 500000; });
    await page.waitForTimeout(400); // past the settle delay: real cells are back
    const midway = await elementCount(page);

    // The counts sit within a whisker of each other — the straddling row can differ.
    expect(Math.abs(midway - atTop)).toBeLessThanOrEqual(300);
    // And nothing per-frame accumulated: scroll again and return.
    await page.evaluate(() => { document.querySelector('.ex-scroller').scrollTop = 0; });
    await page.waitForTimeout(400);
    expect(Math.abs(await elementCount(page) - atTop)).toBeLessThanOrEqual(300);
});

test('Ctrl+A over 10⁶ × 100 is one rectangle, counted as 10⁸, and the next key is answered (BIG-3, SL-6, DOM-2)', async ({ page }) => {
    await openWide(page);
    await cell(page, 0, 0).click({ force: true });
    const before = await elementCount(page);

    await page.keyboard.press('ControlOrMeta+A');
    const status = page.locator('#selection-status');
    await expect(status).toContainText('Selected: 100000000');
    // One rectangle: the page names the range count only when there is more than one.
    await expect(status).not.toContainText('ranges');

    const after = await elementCount(page);
    // Ctrl+A over 10^8 cells adds the overlay rectangles and nothing else.
    expect(after - before).toBeLessThanOrEqual(6);

    // Not frozen: the next key is taken and collapses the selection to one cell.
    await page.keyboard.press('ArrowDown');
    await expect(status).toContainText('Selected: 1 ');
});

/**
 * Counts every crossing between the grid's module and .NET, in both directions, by a
 * conditional breakpoint that never pauses: on the first statement of each method of
 * the per-instance handle (.NET calling JavaScript), and on each call into .NET the
 * module makes. Located in the source the page was served, so a moved line cannot
 * silently stop being counted — a site that cannot be placed fails the test.
 */
async function countGridInterop(page) {
    const client = await page.context().newCDPSession(page);
    let module;
    client.on('Debugger.scriptParsed', (e) => {
        if (/\/_content\/ExGrid\/ex-grid(\.\w+)?\.js$/.test(e.url)) {
            module = e;
        }
    });
    await client.send('Debugger.enable');
    await expect.poll(() => module, { message: 'ex-grid.js is loaded' }).toBeTruthy();

    const { scriptSource } = await client.send('Debugger.getScriptSource', { scriptId: module.scriptId });
    const at = (index) => {
        const before = scriptSource.slice(0, index).split('\n');
        return { scriptId: module.scriptId, lineNumber: before.length - 1, columnNumber: before.at(-1).length };
    };
    const sites = [];
    const handle = scriptSource.lastIndexOf('\n    return {');
    for (const m of scriptSource.slice(handle).matchAll(/^ {8}(\w+): \([^)]*\) =>\s*/gm)) {
        sites.push({ name: `.NET→JS ${m[1]}`, index: handle + m.index + m[0].length });
    }
    for (const m of scriptSource.matchAll(/core\.invokeMethod(?:Async)?\(\s*'(\w+)'/g)) {
        sites.push({ name: `JS→.NET ${m[1]}`, index: m.index });
    }
    // Every member of the handle and every call into .NET, not only the ones shaped the
    // way the patterns above expect: one written another way would go uncounted.
    const members = [...scriptSource.slice(handle).matchAll(/^ {8}(\w+):/gm)].map((m) => `.NET→JS ${m[1]}`);
    const calls = [...scriptSource.matchAll(/core\.invokeMethod/g)].length;
    expect(sites.filter((s) => s.name.startsWith('.NET')).map((s) => s.name), 'every handle member placed')
        .toEqual(members);
    expect(sites.filter((s) => s.name.startsWith('JS')).length, 'every call into .NET placed').toBe(calls);
    expect(members, 'the handle was found').toContain('.NET→JS getScrollOffset');

    for (const site of sites) {
        const { locations } = await client.send('Debugger.getPossibleBreakpoints', {
            start: at(site.index),
            restrictToFunction: true,
        });
        expect(locations.length, `a breakpoint for ${site.name}`).toBeGreaterThan(0);
        const name = JSON.stringify(site.name);
        await client.send('Debugger.setBreakpoint', {
            location: locations[0],
            condition: `(globalThis.__exInterop[${name}] = (globalThis.__exInterop[${name}] ?? 0) + 1, false)`,
        });
    }
    await page.evaluate(() => { globalThis.__exInterop = {}; });
}

/** Scrolls by `step` px per frame for `frames` frames on one axis, then lets it settle. */
async function scrollFrames(page, axis, step, frames) {
    return page.evaluate(({ axis, step, frames }) => new Promise((resolve) => {
        const scroller = document.querySelector('.ex-scroller');
        globalThis.__exInterop = {};
        let done = 0;
        let events = 0;
        const onScroll = () => { events++; };
        scroller.addEventListener('scroll', onScroll, { passive: true });
        const tick = () => {
            if (done === frames) {
                // Past the settle delay, so a call the settle makes is counted too.
                setTimeout(() => {
                    scroller.removeEventListener('scroll', onScroll);
                    resolve({ frames, events, calls: { ...globalThis.__exInterop } });
                }, 600);
                return;
            }
            done++;
            scroller[axis] += step;
            requestAnimationFrame(tick);
        };
        requestAnimationFrame(tick);
    }), { axis, step, frames });
}

test('a scroll frame makes at most one interop call, whatever is painted (PF-1)', async ({ page }) => {
    await openWide(page);
    await countGridInterop(page);

    const motions = {
        'rows, one per frame': await scrollFrames(page, 'scrollTop', 28, 60),
        'columns, one per frame': await scrollFrames(page, 'scrollLeft', 90, 60),
        'a fling, two Viewports per frame': await scrollFrames(page, 'scrollTop', 1200, 20),
    };

    for (const [motion, { frames, events, calls }] of Object.entries(motions)) {
        const total = Object.values(calls).reduce((a, b) => a + b, 0);
        const detail = `${motion}: ${frames} frames, ${events} scroll events, calls ${JSON.stringify(calls)}`;
        // The scroll moved and was read, never twice for one event (PF-4) — fewer when a
        // burst is coalesced into the last position (ASY-3)…
        expect(events, detail).toBeGreaterThan(0);
        expect(calls['.NET→JS getScrollOffset'] ?? 0, detail).toBeGreaterThan(0);
        expect(calls['.NET→JS getScrollOffset'], detail).toBeLessThanOrEqual(events);
        // …and by nothing else: no call per cell, per row, or per column painted.
        expect(total, detail).toBeLessThanOrEqual(frames);
    }
});
