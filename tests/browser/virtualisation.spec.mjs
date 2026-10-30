import { test, expect, alterPage, circuitQuiet, revealRepainted, dataRowCount } from './fixtures.mjs';

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
    await expect.poll(() => dataRowCount(grid)).toBe(rows ?? ROWS);
    await expectRowPainted(page, 0);
    return grid;
}

test('the far corner is reachable and painted at 10⁶ rows (BIG-1) @behaviour', async ({ page }) => {
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

test('a reveal is repainted: the offset moves a pixel and back, and the core paints nothing for it (VZ-18, ADR-0012) @behaviour', async ({ page }) => {
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

test('a scroll the user makes during a reveal\'s repaint stands, and the grid paints where it stands (VZ-18, ADR-0012) @behaviour', async ({ page }) => {
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
test('the spacer is laid out as declared under the Layout Ceiling, and the last row ends flush (VZ-15) @behaviour', async ({ page }, testInfo) => {
    const grid = await openWide(page);
    const measure = () => page.evaluate(() => {
        const root = document.querySelector('.ex-grid');
        const spacer = root.querySelector('.ex-spacer');
        return {
            // Read from the attribute: the CSSOM serialises a length to six significant
            // figures, so spacer.style.height says 2.23694e+07px for 22,369,362.
            declared: Number(/height: ([\d.]+)px/.exec(spacer.getAttribute('style'))[1]),
            // Both read from layout, not from a rect: a rect is single precision, so at 2^24 px
            // and above it moves in steps of 2, and the React host's page puts the grid where
            // that rounding reads the spacer 2 px over its true height (2026-10-30).
            laidOut: spacer.offsetHeight,
            ceiling: root.querySelector('.ex-ceiling-probe > div').offsetHeight,
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

test('the element count is the same at 10³, 10⁵ and 10⁶ rows (VZ-1, BIG-2, DOM-1) @behaviour', async ({ page }) => {
    const counts = {};
    for (const rows of [1_000, 100_000, ROWS]) {
        await openWide(page, rows);
        // Past the settle delay: nothing is a Placeholder, and the host has said all it will.
        await expect(page.locator('.ex-grid .ex-placeholder')).toHaveCount(0);
        await circuitQuiet();
        counts[rows] = await elementCount(page);
    }

    // Identical, not close: the same Viewport paints the same rows and columns whatever
    // the total, and nothing under the root is a function of it (ADR-0004 P1).
    expect(counts, JSON.stringify(counts)).toEqual({ 1000: counts[ROWS], 100000: counts[ROWS], [ROWS]: counts[ROWS] });
});

test('the first and the last row paint their own data, there and back again (BIG-5) @behaviour', async ({ page }) => {
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
test('a scroll to the end made as the grid becomes ready is not undone by the Layout Ceiling (BIG-5, ADR-0053) @behaviour', async ({ page }, testInfo) => {
    await page.addInitScript(() => {
        let done = false;
        // The circuit replaces the prerendered grid, so the ready one is found wherever it is.
        // And styled: on WebAssembly the package's stylesheet is a root component the app renders
        // into the head after it boots, and a grid can be ready before it has loaded (ADR-0123: it
        // paints unstyled until then). Unstyled, the scroller is no scroll container, and the
        // browser drops the scroll at 0: the grid stayed on row 0, on CI's chrome shard 4/4 of
        // #388, and every time with the stylesheet held back 3 s. Nothing the grid could have
        // heard, and nothing a user could do there. The first moment the grid is both is still
        // before the Layout Ceiling can have been heard, whose probe is styled inline.
        const scrollWhenReady = () => {
            const root = done ? null : document.querySelector('.ex-grid:not([aria-busy])');
            const scroller = root?.querySelector('.ex-scroller');
            if (!scroller || getComputedStyle(scroller).overflowY === 'visible') {
                return;
            }
            done = true;
            const spacer = scroller.querySelector('.ex-spacer');
            window.__spacerAtScroll = Number(/height: ([\d.]+)px/.exec(spacer.getAttribute('style'))[1]);
            scroller.scrollTop = scroller.scrollHeight;
        };
        new MutationObserver(scrollWhenReady)
            .observe(document, { subtree: true, childList: true, attributes: true, attributeFilter: ['aria-busy'] });
        // A stylesheet's load changes no markup, so it is heard as itself.
        document.addEventListener('load', scrollWhenReady, true);
    });
    await page.goto('/wide');
    await expect.poll(() => dataRowCount(page.locator('.ex-grid'))).toBe(ROWS);
    const firstRow = async () => Number(await page.locator('.ex-grid .ex-viewport').getAttribute('data-ex-first-row'));

    await expect.poll(firstRow, { timeout: 15_000 }).toBeGreaterThan(ROWS / 2);
    // spec-lint: fixed-wait: the Layout Ceiling is told by a ResizeObserver, not taken by the gate, so the Settled Mark does not cover its anchor (ADR-0220, As built), and nothing marks that none is still on its way
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

test('scrolling changes which rows exist, not how many elements do (ADR-0004 P1, across a scroll) @behaviour', async ({ page }) => {
    await openWide(page);

    const atTop = await elementCount(page);
    await page.evaluate(() => { document.querySelector('.ex-scroller').scrollTop = 500000; });
    // Past the settle delay: the slice there is painted in place of the top's, real cells are
    // back, and the host has said all it will.
    await expect(cell(page, 0, 0)).toHaveCount(0);
    await expect(page.locator('.ex-grid .ex-placeholder')).toHaveCount(0);
    await circuitQuiet();
    const midway = await elementCount(page);

    // The counts sit within a whisker of each other — the straddling row can differ.
    expect(Math.abs(midway - atTop)).toBeLessThanOrEqual(300);
    // And nothing per-frame accumulated: scroll again and return.
    await page.evaluate(() => { document.querySelector('.ex-scroller').scrollTop = 0; });
    await expectRowPainted(page, 0);
    await expect(page.locator('.ex-grid .ex-placeholder')).toHaveCount(0);
    await circuitQuiet();
    expect(Math.abs(await elementCount(page) - atTop)).toBeLessThanOrEqual(300);
});

test('Ctrl+A over 10⁶ × 100 is one rectangle, counted as 10⁸, and the next key is answered (BIG-3, SL-6, DOM-2) @behaviour', async ({ page }) => {
    await openWide(page);
    await cell(page, 0, 0).click({ force: true });
    // The press painted: one cell is the Focus outline alone, and nothing more is on its way.
    await expect(page.locator('#selection-status')).toContainText('Selected: 1 ');
    await expect(page.locator('.ex-grid .ex-focus')).toHaveCount(1);
    await circuitQuiet();
    const before = await elementCount(page);

    await page.keyboard.press('ControlOrMeta+A');
    const status = page.locator('#selection-status');
    await expect(status).toContainText('Selected: 100000000');
    // One rectangle: the page names the range count only when there is more than one.
    await expect(status).not.toContainText('ranges');

    // Ctrl+A over 10^8 cells adds the overlay rectangles and nothing else: with A and B pinned and
    // the Focus in A, a tint and a box in each layer the range reaches, and the pinned shade layer
    // that holds its pinned tint (ADR-0206), plus the Heading Mark in the scrollable header
    // band it now reaches (ADR-0008 D1/D2). The pinned header already had its mark.
    const grid = page.locator('.ex-grid');
    await expect(grid.locator('.ex-shade-pinned > .ex-range-tint')).toHaveCount(1);
    await expect(grid.locator('.ex-selection-pinned > .ex-range')).toHaveCount(1);
    await expect(grid.locator('.ex-selection > .ex-range-tint')).toHaveCount(1);
    await expect(grid.locator('.ex-selection > .ex-range')).toHaveCount(1);
    await circuitQuiet();
    const after = await elementCount(page);
    await expect(grid.locator(".ex-header > .ex-heading-mark")).toHaveCount(1);
    await expect(grid.locator(".ex-heading-marks-pinned > .ex-heading-mark")).toHaveCount(1);
    expect(after - before).toBe(6);

    // Not frozen: the next key is taken and collapses the selection to one cell.
    await page.keyboard.press('ArrowDown');
    await expect(status).toContainText('Selected: 1 ');
});

/**
 * Counts every crossing between the grid and .NET, in both directions, by a conditional breakpoint
 * that never pauses: on the first statement of each method of the handle Blazor's ExGrid holds on
 * the Script Core (.NET calling JavaScript), and on the one door every call into .NET passes, the
 * Shared Script's `crossingCalls` (ADR-0350 section 7, ADR-0411). The handle is located in the
 * source the module was written as, so a moved line cannot silently stop being counted — a site
 * that cannot be placed fails the test. The modules are shipped minified (ADR-0123), possibly under
 * a fingerprinted name: the handle's methods are found in the source its source map carries, and
 * each is placed where the map says it was served. The door is found in the Shared Script as
 * served, since its condition reads the method's name from the served function's own parameter.
 */
async function countGridInterop(page) {
    const client = await page.context().newCDPSession(page);
    const scripts = {};
    client.on('Debugger.scriptParsed', (e) => {
        const name = /\/_content\/ExGrid\/(ex-grid-core|ex-grid-script)(\.[\w-]+)*\.js(\?v=[0-9a-f]{12})?$/.exec(e.url)?.[1];
        if (name) scripts[name] = e;
    });
    await client.send('Debugger.enable');
    await expect.poll(() => scripts['ex-grid-core'] && scripts['ex-grid-script'], { message: 'the grid\'s modules are loaded' }).toBeTruthy();

    const placed = async (script, line, column, counter) => {
        const { locations } = await client.send('Debugger.getPossibleBreakpoints', {
            start: { scriptId: script.scriptId, lineNumber: line, columnNumber: column },
            restrictToFunction: true,
        });
        expect(locations.length, `a breakpoint for ${counter}`).toBeGreaterThan(0);
        await client.send('Debugger.setBreakpoint', {
            location: locations[0],
            condition: `(globalThis.__exInterop[${counter}] = (globalThis.__exInterop[${counter}] ?? 0) + 1, false)`,
        });
    };

    // .NET→JS: each method of the object `attach` answers, in blazor-grid.ts.
    const core = scripts['ex-grid-core'];
    expect(core.sourceMapURL, 'the module is served with its source map').toBeTruthy();
    const response = await page.request.get(new URL(core.sourceMapURL, core.url).href);
    expect(response.ok(), 'the source map is served').toBe(true);
    const map = await response.json();
    const source = map.sources.findIndex((path) => path.endsWith('blazor/src/blazor-grid.ts'));
    expect(source, 'the module carries blazor-grid.ts').toBeGreaterThanOrEqual(0);
    const written = map.sourcesContent[source];
    const segments = decodeMappings(map.mappings).filter((m) => m.source === source);
    const attach = written.indexOf('\nexport function attach(');
    const handle = written.indexOf('\n    return Object.freeze({', attach);
    const handleEnd = written.indexOf('\n    });', handle);
    expect(attach >= 0 && handle > attach && handleEnd > handle, 'the handle was found').toBe(true);
    const body = written.slice(handle, handleEnd);
    const members = [...body.matchAll(/^ {8}(\w+):/gm)].map((m) => m[1]);
    const sites = [...body.matchAll(/^ {8}(\w+): (?:async )?\((?:[^()]|\([^()]*\))*\)(?:: [^=]+)? =>\s*/gm)];
    expect(sites.map((m) => m[1]), 'every handle member placed').toEqual(members);
    expect(members, 'the handle was found').toContain('update');
    for (const m of sites) {
        const before = written.slice(0, handle + m.index + m[0].length).split('\n');
        const line = before.length - 1;
        const column = before.at(-1).length;
        // The first mapped place at or after the written one, on its line or a later one.
        let best;
        for (const s of segments) {
            if (s.sourceLine < line || (s.sourceLine === line && s.sourceColumn < column)) continue;
            if (!best || s.sourceLine < best.sourceLine || (s.sourceLine === best.sourceLine && s.sourceColumn < best.sourceColumn)) best = s;
        }
        expect(best, `a served place for ${m[1]}`).toBeTruthy();
        await placed(core, best.line, best.column, JSON.stringify(`.NET→JS ${m[1]}`));
    }

    // JS→.NET: the door, once, counted by the method it calls.
    const shared = scripts['ex-grid-script'];
    const { scriptSource } = await client.send('Debugger.getScriptSource', { scriptId: shared.scriptId });
    const doors = [...scriptSource.matchAll(/invokeMethodAsync:\(([\w$]+),\.\.\.[\w$]+\)=>\{/g)];
    expect(doors.length, 'the one door every call into .NET passes').toBe(1);
    const before = scriptSource.slice(0, doors[0].index + doors[0][0].length).split('\n');
    await placed(shared, before.length - 1, before.at(-1).length, `"JS→.NET " + ${doors[0][1]}`);
    await page.evaluate(() => { globalThis.__exInterop = {}; });
}

/**
 * A source map's `mappings`, decoded: each segment's place in the served script (`line`,
 * `column`), the source it was written in (`source`, an index into `sources`) and its place there
 * (`sourceLine`, `sourceColumn`), all from 0.
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
                segments.push({ line, column, source, sourceLine, sourceColumn });
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
                // spec-lint: fixed-wait: a counting window: the interop calls the settle makes after the last frame are counted; no state says none is still to come
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

test('a scroll frame makes at most one interop call, whatever is painted (PF-1) @blazor', async ({ page }) => {
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
        // The scroll moved. The Script Core reads it in the browser and paints from the rows it
        // holds: what crosses is a request for rows it does not hold yet, and their answer —
        // never a call per cell, per row, or per column painted.
        expect(events, detail).toBeGreaterThan(0);
        expect(total, detail).toBeLessThanOrEqual(frames);
    }
    // The counters count: the fling reaches rows the browser does not hold, asks .NET for them, and
    // is sent them.
    const { calls } = motions['a fling, two Viewports per frame'];
    expect(Object.keys(calls).some((name) => name.startsWith('JS→.NET ')), JSON.stringify(calls)).toBe(true);
    expect(Object.keys(calls).some((name) => name.startsWith('.NET→JS ')), JSON.stringify(calls)).toBe(true);
});

// ---------------------------------------------------------------------------------------------
// DOM-3 and FN-6 (ADR-0004), at the Definition of Done's scenario. /wide's columns
// (DemoData.WideColumns): Key 140px and Book 110px, pinned, then 98 metrics of 91px each.

const WIDE_WIDTHS = [140, 110, ...Array(98).fill(91)];
const WIDE_PINNED = 2;

/**
 * The columns a row paints at a horizontal offset, recomputed here from the declared widths:
 * the pinned ones, then every scrollable column the readable width reaches past the pinned block
 * — one partly on screen included, one wholly under the pinned block not. ColumnGeometry's
 * ScrollableSliceAt, with the offset clamped as the browser clamps it (ADR-0004).
 */
function columnsOnScreen(scrollLeft, readableWidth) {
    const offsets = [0];
    for (const width of WIDE_WIDTHS) {
        offsets.push(offsets.at(-1) + width);
    }
    const left = Math.min(Math.max(scrollLeft, 0), Math.max(0, offsets.at(-1) - readableWidth));
    const from = left + offsets[WIDE_PINNED];
    const to = left + readableWidth;
    let first = WIDE_PINNED;
    while (first < WIDE_WIDTHS.length && offsets[first + 1] <= from) {
        first++;
    }
    let end = first;
    while (end < WIDE_WIDTHS.length && offsets[end] < to) {
        end++;
    }
    return [...Array(WIDE_PINNED).keys(), ...Array.from({ length: end - first }, (_, i) => first + i)];
}

/** What the grid holds in the DOM: each painted row's cells by column, the header's columns,
 * every cell counted, and the offset and readable width the browser holds. */
function cellsInDom(grid) {
    return grid.evaluate((root) => {
        const scroller = root.querySelector(':scope > .ex-scroller');
        const rows = [...root.querySelectorAll('.ex-viewport .ex-row')];
        return {
            scrollLeft: scroller.scrollLeft,
            readable: scroller.clientWidth,
            placeholders: rows.filter((row) => row.classList.contains('ex-placeholder')).length,
            rows: rows.map((row) => [...row.querySelectorAll('.ex-cell')].map((cell) => Number(/c(\d+)$/.exec(cell.id)?.[1]))),
            header: [...root.querySelectorAll('.ex-header .ex-header-cell')].map((cell) => Number(cell.getAttribute('aria-colindex')) - 1),
            cells: root.querySelectorAll('.ex-viewport .ex-row .ex-cell').length,
        };
    });
}

test('the cells in the DOM are the painted rows × (pinned + scrollable columns on screen), exactly (DOM-3, ADR-0004) @behaviour', async ({ page }) => {
    const grid = await openWide(page);

    // The top left; panned right; deep down and panned to an offset that straddles columns; and
    // past both ends, where the browser clamps.
    for (const [top, left] of [[0, 0], [0, 1000], [28 * 5000, 4321], [28 * 777_777, 1e9]]) {
        await grid.evaluate((root, [top, left]) => {
            const scroller = root.querySelector(':scope > .ex-scroller');
            scroller.scrollTop = top;
            scroller.scrollLeft = left;
        }, [top, left]);
        // Settled where the browser stands: no Placeholder is left, and the header is painted for
        // the offset the browser holds.
        await expect.poll(async () => {
            const dom = await cellsInDom(grid);
            return dom.placeholders === 0
                && JSON.stringify(dom.header) === JSON.stringify(columnsOnScreen(dom.scrollLeft, dom.readable));
        }, { timeout: 15_000, message: `the grid never settled at ${top}, ${left}` }).toBe(true);
        await circuitQuiet();

        const dom = await cellsInDom(grid);
        const expected = columnsOnScreen(dom.scrollLeft, dom.readable);
        const where = `at scrollTop ${top}, scrollLeft ${dom.scrollLeft}, ${dom.readable}px readable`;
        expect(dom.header, `the header's columns ${where}`).toEqual(expected);
        expect(dom.rows.length, `rows painted ${where}`).toBeGreaterThan(0);
        for (const [i, columns] of dom.rows.entries()) {
            expect(columns, `the ${i}th painted row's columns ${where}`).toEqual(expected);
        }
        // DOM-3's arithmetic: painted rows × (pinned + painted scrollable columns), exactly.
        expect(dom.cells, `cells ${where}`).toBe(dom.rows.length * expected.length);
    }
});

/**
 * Looks at where each painted row of the Window has its pinned cells: present, wearing ex-pinned,
 * and at their place from the scrollport's left edge (Key at 0, Book at 140) however far the rest
 * has panned. That is `position: sticky`, which only a browser lays out. With no `stops` it looks
 * once, at what is there, and answers what it saw.
 *
 * With `stops` it flings between them, a stop each animation frame, and looks after every change
 * the grid makes to its own DOM, so a state that lasts one render is seen without a wait. It keeps
 * flinging until it has seen what it is there to see: `looks` looks, and at least one of them at a
 * row painted as a Placeholder — a render in the middle of the fling. No count of frames is long
 * enough everywhere: on the Server host each of those renders is a round trip behind the scroll.
 * Then it stops by itself and marks what it saw `done`; the test waits for that under the suite's
 * bound (`flingSeen`). It also stops if its grid leaves the document, as a failed test's does when
 * the next test mounts its page, so nothing outlives the test it ran in. Runs in the page.
 */
function flingAndLook(root, { first, last, stops = [], looks = 0 }) {
    const scroller = root.querySelector(':scope > .ex-scroller');
    // A row's aria-rowindex counts the header's rows first (ADR-0033): one row, or the tiers and
    // the leaf row in a rowgroup.
    const header = scroller.querySelector(':scope > .ex-spacer > .ex-header');
    const headerRows = !header ? 0 : header.getAttribute('role') === 'row' ? 1 : header.querySelectorAll(':scope > [role=row]').length;
    const seen = { looks: 0, midFling: 0, wrong: [], done: false };
    const look = () => {
        const edge = scroller.getBoundingClientRect().left + scroller.clientLeft;
        let flung = false;
        for (const row of root.querySelectorAll('.ex-viewport .ex-row')) {
            const index = Number(row.getAttribute('aria-rowindex')) - 1 - headerRows;
            const placeholder = row.classList.contains('ex-placeholder');
            flung = flung || placeholder;
            if (index < first || index > last || seen.wrong.length >= 20) {
                continue;
            }
            const state = placeholder ? 'mid-fling' : 'settled';
            for (const [column, at] of [[0, 0], [1, 140]]) {
                const cell = row.querySelector(`[id$='r${index}c${column}']`);
                if (!cell || !cell.classList.contains('ex-pinned')) {
                    seen.wrong.push(`row ${index} (${state}): pinned column ${column} is not painted`);
                    continue;
                }
                const x = cell.getBoundingClientRect().left - edge;
                if (Math.abs(x - at) > 1) {
                    seen.wrong.push(`row ${index} (${state}): pinned column ${column} stands ${x}px from the left edge, not ${at}px`);
                }
            }
        }
        seen.looks++;
        seen.midFling += flung ? 1 : 0;
    };
    if (stops.length === 0) {
        look();
        seen.done = true;
        return seen;
    }
    // Kept on the grid's own root, which the next test's page replaces, so the test can read it.
    root.pinnedFling = seen;
    const observer = new MutationObserver(look);
    observer.observe(root, { subtree: true, childList: true, attributes: true, attributeFilter: ['class', 'style'] });
    let frame = 0;
    const step = () => {
        if (!root.isConnected || (seen.midFling > 0 && seen.looks >= looks)) {
            observer.disconnect();
            seen.done = root.isConnected;
            return;
        }
        const stop = stops[frame % stops.length];
        scroller.scrollTop = stop.top;
        scroller.scrollLeft = stop.left;
        frame++;
        requestAnimationFrame(step);
    };
    requestAnimationFrame(step);
    return null;
}

/** What the fling `flingAndLook` started has seen so far. */
function flingSeen(grid) {
    return grid.evaluate((root) => {
        const seen = root.pinnedFling;
        return seen && { looks: seen.looks, midFling: seen.midFling, wrong: [...seen.wrong], done: seen.done };
    });
}

test('the Pinned Columns are painted at the left edge at every offset, mid-fling included (FN-6, ADR-0004) @behaviour', async ({ page }) => {
    const grid = await openWide(page);
    // The Window /wide answered for the first Viewport, read ahead 60 rows either side (Wide.razor).
    const status = await page.locator('.demo-status').textContent();
    const [, first, last] = (/Window: rows (\d+)–(\d+)/.exec(status) ?? []).map(Number);
    expect(last, `the Window holds both stops: ${status}`).toBeGreaterThanOrEqual(40 + 22);

    // Two stops a fling apart on both axes — 40 rows down, more than the 600px Viewport, and
    // 4,000px across, more than its width — both inside the Window, so every row painted in
    // between is a row in hand: a fling paints its pinned cells alone, and these must be there.
    // It flings until it has looked 60 times and seen a render in the middle of the fling.
    const stops = [{ top: 0, left: 2000 }, { top: 40 * 28, left: 6000 }];
    await grid.evaluate(flingAndLook, { first, last, stops, looks: 60 });
    // What it has seen so far is what a failure prints: how many looks, how many mid-fling.
    await expect.poll(() => flingSeen(grid), {
        message: 'the fling looked 60 times and saw a render in its middle',
    }).toMatchObject({ done: true });
    const flinging = await flingSeen(grid);

    expect(flinging.wrong, `pinned cells while flinging (${flinging.looks} looks, ${flinging.midFling} mid-fling)`).toEqual([]);
    expect(flinging.midFling, 'a render in the middle of the fling was seen').toBeGreaterThan(0);

    // And once it has settled where it stopped.
    await expect(grid.locator('.ex-placeholder')).toHaveCount(0, { timeout: 15_000 });
    await circuitQuiet();
    const settled = await grid.evaluate(flingAndLook, { first, last });
    expect(settled.wrong, 'pinned cells once settled').toEqual([]);
    expect(settled.midFling).toBe(0);
});
