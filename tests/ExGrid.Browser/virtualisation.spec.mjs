import { test, expect, alterPage, circuitQuiet, revealRepainted } from './fixtures.mjs';

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

// ADR-0012, 2026-10-08: under Citrix with the browser's hardware acceleration off, a far reveal
// left the Viewport white until the offset moved again. Every reveal is now followed by a repaint:
// the offset a pixel away in the next frame and back in the one after. The core must hear nothing
// of it, and a scroll the user makes in between must stand. Whether it paints under Citrix is
// checked by hand there (VZ-18); these check the mechanism.

/** The wire (if any) quiet, and a reveal's repaint over: what the grid does next is done. */
async function settled(page) {
    await circuitQuiet();
    await revealRepainted(page);
}

/** Rows cover the readable height of the Viewport where the scroller stands: nothing white. */
async function paintedWhereItStands(page) {
    return page.evaluate(() => {
        const scroller = document.querySelector('.ex-grid > .ex-scroller');
        const box = scroller.getBoundingClientRect();
        const top = box.top + scroller.querySelector('.ex-header').getBoundingClientRect().height;
        const bottom = box.top + scroller.clientHeight;
        const rows = [...scroller.querySelectorAll('.ex-viewport > .ex-row')].map((row) => row.getBoundingClientRect());
        return rows.length > 0
            && Math.min(...rows.map((r) => r.top)) <= top + 0.5
            && Math.max(...rows.map((r) => r.bottom)) >= bottom - 0.5;
    });
}

test('a reveal is repainted: the offset moves a pixel and back, and the core paints nothing for it (VZ-18, ADR-0012)', async ({ page }) => {
    await openWide(page);
    await cell(page, 0, 2).click({ force: true });
    await alterPage(page, () => {
        const scroller = document.querySelector('.ex-grid > .ex-scroller');
        const viewport = scroller.querySelector('.ex-viewport');
        const record = { offsets: [], slices: 0 };
        const onScroll = () => record.offsets.push(scroller.scrollTop);
        const slices = new MutationObserver((changes) => { record.slices += changes.length; });
        scroller.addEventListener('scroll', onScroll, { passive: true });
        slices.observe(viewport, { attributes: true, attributeFilter: ['data-ex-first-row'] });
        window.vz18 = record;
        return () => {
            scroller.removeEventListener('scroll', onScroll);
            slices.disconnect();
            delete window.vz18;
        };
    });

    // PageDown lands on a row's edge, so a pixel back is the row before it: a core that heard
    // the repaint would paint another slice.
    await page.keyboard.press('PageDown');
    await expect.poll(() => page.evaluate(() => {
        const o = window.vz18.offsets;
        return o.length >= 3 && o.at(-3) === o.at(-1) && o.at(-2) < o.at(-1) && o.at(-2) >= o.at(-1) - 1.5;
    }), { message: 'the offset moved a pixel back and returned (T, T − 1, T)' }).toBe(true);
    await settled(page);

    const { offsets, slices } = await page.evaluate(() => window.vz18);
    expect(offsets.at(-1)).toBeGreaterThan(0);
    expect(slices, 'one slice painted: the reveal\'s, none for the pixel').toBe(1);
    expect(await paintedWhereItStands(page)).toBe(true);
});

test('a scroll the user makes during a reveal\'s repaint stands, and the grid paints where it stands (VZ-18, ADR-0012)', async ({ page }) => {
    await openWide(page);
    await cell(page, 0, 2).click({ force: true });
    // As the reveal lands, a scroll of the user's own to the middle, made in the frame the repaint
    // moves the offset away in: its scroll event is read while the repaint still answers for the
    // reveal, and nothing moves after it unless the grid is told to read again.
    await alterPage(page, () => {
        const root = document.querySelector('.ex-grid');
        const scroller = root.querySelector(':scope > .ex-scroller');
        const landed = new MutationObserver(() => {
            landed.disconnect();
            requestAnimationFrame(() => {
                scroller.scrollTop = Math.round(scroller.scrollHeight / 2);
                window.vz18user = scroller.scrollTop;
            });
        });
        landed.observe(root, { attributes: true, attributeFilter: ['data-ex-reveal'] });
        return () => {
            landed.disconnect();
            delete window.vz18user;
        };
    });

    await page.keyboard.press('ControlOrMeta+ArrowDown');
    await expect.poll(() => page.evaluate(() => window.vz18user ?? null)).not.toBeNull();
    await settled(page);

    const [user, standing, first] = await page.evaluate(() => {
        const scroller = document.querySelector('.ex-grid > .ex-scroller');
        return [window.vz18user, scroller.scrollTop, Number(scroller.querySelector('.ex-viewport').getAttribute('data-ex-first-row'))];
    });
    expect(standing, 'the user\'s scroll stands').toBe(user);
    expect(first, 'the slice is the middle\'s, not the last rows\'').toBeLessThan(ROWS - 1000);
    expect(first).toBeGreaterThan(1000);
    await expect.poll(() => paintedWhereItStands(page), { message: 'rows cover the Viewport where it stands' }).toBe(true);
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

// ADR-0053 / ADR-0028: on a Server circuit the Layout Ceiling can be told after the grid stopped
// being busy, and a scroll made in between is still on the wire when the ceiling arrives: an
// anchor computed from row 0 was written over it and the grid went back to the top (BIG-5 on
// chrome-150, 2 of 4 on Windows, verification/2026-09-28-windows-3). So the scroll is made here
// in the very task in which the grid stops being busy, before the ceiling can have been heard.
//
// The first ceiling told is the initial measurement, not a change ("Settled after the third
// Windows run"): nothing is anchored, and the browser's offset is read through the geometry it
// gives. So whichever the grid heard first, the scroll to the end paints the last row. The
// ceiling first (the usual order on a circuit): the scroll is read through the compressed
// geometry. The scroll first (always on WebAssembly, where the offset is read within the frame):
// the untold geometry showed row 798,894, and the first ceiling re-reads the same offset as the
// end — it used to anchor on row 798,894 and stay there. At scale 1 nothing is compressed and
// this is BIG-5's own case.
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
    // Compressed or not, on either host: the end is the end.
    await expectRowPainted(page, ROWS - 1);
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
 * module makes. Located in the source the module was written as, so a moved line cannot
 * silently stop being counted — a site that cannot be placed fails the test. The module is
 * shipped minified (ADR-0123), possibly under a fingerprinted name: the sites are found in the
 * source its source map carries, and each is placed where the map says it was served.
 */
async function countGridInterop(page) {
    const client = await page.context().newCDPSession(page);
    let module;
    client.on('Debugger.scriptParsed', (e) => {
        if (/\/_content\/ExGrid\/ex-grid(\.[\w-]+)*\.js$/.test(e.url)) {
            module = e;
        }
    });
    await client.send('Debugger.enable');
    await expect.poll(() => module, { message: 'ex-grid.js is loaded' }).toBeTruthy();

    const { scriptSource: served } = await client.send('Debugger.getScriptSource', { scriptId: module.scriptId });
    let scriptSource = served;
    let toServed = (line, column) => ({ lineNumber: line, columnNumber: column });
    if (module.sourceMapURL) {
        const response = await page.request.get(new URL(module.sourceMapURL, module.url).href);
        expect(response.ok(), 'the source map is served').toBe(true);
        const map = await response.json();
        scriptSource = map.sourcesContent[0];
        const segments = decodeMappings(map.mappings);
        toServed = (line, column) => {
            // The first mapped place at or after the written one, on its line or a later one.
            let best;
            for (const m of segments) {
                if (m.sourceLine < line || (m.sourceLine === line && m.sourceColumn < column)) continue;
                if (!best || m.sourceLine < best.sourceLine || (m.sourceLine === best.sourceLine && m.sourceColumn < best.sourceColumn)) best = m;
            }
            expect(best, `a served place for ${line}:${column}`).toBeTruthy();
            return { lineNumber: best.line, columnNumber: best.column };
        };
    }
    const at = (index) => {
        const before = scriptSource.slice(0, index).split('\n');
        return { scriptId: module.scriptId, ...toServed(before.length - 1, before.at(-1).length) };
    };
    const sites = [];
    // The handle is the object `attach` returns. Since ADR-0080 it is named first
    // (`const handle = {`), because the Keyboard Field's composition end grants the editor's
    // waiting request through it, and returned after; before, it was returned as written.
    const named = scriptSource.lastIndexOf('\n    const handle = {');
    const handle = named >= 0 ? named : scriptSource.lastIndexOf('\n    return {');
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

/**
 * A source map's `mappings`, decoded: each segment's place in the served script (`line`,
 * `column`) and in the source it was written as (`sourceLine`, `sourceColumn`), all from 0.
 */
function decodeMappings(mappings) {
    const digits = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/';
    const segments = [];
    let sourceLine = 0, sourceColumn = 0, source = 0, name = 0;
    mappings.split(';').forEach((text, line) => {
        let column = 0;
        for (const segment of text.split(',')) {
            if (!segment) continue;
            const values = [];
            let value = 0, shift = 0;
            for (const c of segment) {
                const digit = digits.indexOf(c);
                value += (digit & 31) << shift;
                if (digit & 32) {
                    shift += 5;
                } else {
                    values.push(value & 1 ? -(value >>> 1) : value >>> 1);
                    value = 0;
                    shift = 0;
                }
            }
            column += values[0];
            if (values.length >= 4) {
                source += values[1];
                sourceLine += values[2];
                sourceColumn += values[3];
                if (values.length >= 5) name += values[4];
                segments.push({ line, column, sourceLine, sourceColumn });
            }
        }
    });
    return segments;
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
