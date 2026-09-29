import { test, expect } from './fixtures.mjs';
import { sheet, cell, clickCell, expectFocusAt, expectCovers } from './sheet-helpers.mjs';
import { painted, runsOf, sameColour, resolvedColour } from './pixels.mjs';

// Excel's look for the Focus and a single range, and a whole header rule (ADR-0008 and ADR-0030
// of 2026-09-29, ticket 24), read in device pixels from what the browser painted: UX-17, UX-18
// and UX-19. A computed style would say what the cascade resolved; these criteria are about a
// layer above the selection covering part of an outline, which only the pixels show.
//
// /sheet is the page that has every neighbour at once — the Headings, a Pinned Column (A), the
// header — under the built-in Chrome and, with ?chrome=mud, under ExGrid.MudBlazor's. /wide has
// Pinned Columns (A and B) and no Headings. Lines are read 4 CSS px in from a cell's corner: clear
// of its text, which is padded further in, and of the fill handle on the opposite corner.

// Tall enough for the whole Sheet, as sheet.spec.mjs has it.
test.use({ viewport: { width: 1280, height: 1000 } });

const CHROMES = [['built-in', ''], ['MudBlazor', '?chrome=mud']];

async function openSheet(page, query) {
    await page.goto(`/sheet${query}`);
    await expect(cell(sheet(page), 'A1')).toHaveText('Item');
    // The Linked Table's first snapshot lands 1.5 s after the Sheet opens and repaints its rows.
    await expect(cell(sheet(page), 'B12')).toHaveText('318.25', { timeout: 10_000 });
}

/** Leaves the pointer off the grid, so no hover band tints the rows being read. */
async function parkPointer(page, grid) {
    await page.mouse.move(0, 0);
    await expect(grid.locator('.ex-hover-row')).toHaveCount(0);
}

/** The Focus outline's colour as painted (sRGB), whatever syntax the cascade resolved it to. */
async function outlineColour(grid) {
    const css = await grid.locator('.ex-focus').first().evaluate((el) => getComputedStyle(el).outlineColor);
    return resolvedColour(grid.page(), css);
}

/**
 * The outline-coloured runs, in device pixels, across a cell's box and a margin round it: along a
 * row 4 CSS px below its top, and down a column 4 CSS px right of its left. With the outline
 * inside the cell, the first and last run of each are its four edges.
 */
async function runsAround(grid, box, colour, margin = 6) {
    const region = await painted(grid.page(), {
        x: box.x - margin, y: box.y - margin, width: box.width + 2 * margin, height: box.height + 2 * margin,
    });
    return {
        across: runsOf(region.across(box.y + 4, box.x - margin, box.x + box.width + margin), colour),
        down: runsOf(region.down(box.x + 4, box.y - margin, box.y + box.height + margin), colour),
    };
}

/** The four edges of the Focus outline at a cell, in device pixels. */
async function focusEdges(grid, locator) {
    const { across, down } = await runsAround(grid, await locator.boundingBox(), await outlineColour(grid));
    return { left: across[0], right: across.at(-1), top: down[0], bottom: down.at(-1), across, down };
}

/** The painted ground a few pixels into a cell, clear of its outline and its text. */
async function groundOf(page, locator) {
    const box = await locator.boundingBox();
    const region = await painted(page, { x: box.x, y: box.y, width: 12, height: 12 });
    return region.at(box.x + 6, box.y + 6);
}

// ---- UX-17: the header's rule runs under every header cell -------------------------------------

for (const [chrome, query] of CHROMES) {
    test(`UX-17 (${chrome} Chrome): the header's rule runs under the Headings' corner and a pinned header as under any other`, async ({ page }) => {
        await openSheet(page, query);
        const grid = sheet(page);
        const places = await grid.evaluate((root) => {
            const mid = (el) => { const r = el.getBoundingClientRect(); return r.left + r.width / 2; };
            const header = root.querySelector('.ex-header').getBoundingClientRect();
            const headers = [...root.querySelectorAll('.ex-header .ex-header-cell')];
            return {
                bottom: header.bottom,
                corner: mid(root.querySelector('.ex-headings-corner')),
                pinned: mid(headers.find((h) => h.classList.contains('ex-pinned'))),
                unpinned: mid(headers.find((h) => !h.classList.contains('ex-pinned'))),
            };
        });
        const left = places.corner - 20;
        const band = await painted(page, { x: left, y: places.bottom - 4, width: places.unpinned + 20 - left, height: 4 });
        // The band's last 4 CSS px, top to bottom, under each: its ground, then its rule.
        const under = {
            corner: band.down(places.corner, places.bottom - 4, places.bottom - 0.01),
            pinned: band.down(places.pinned, places.bottom - 4, places.bottom - 0.01),
            unpinned: band.down(places.unpinned, places.bottom - 4, places.bottom - 0.01),
        };
        // The band's own rule is there, under an ordinary header cell…
        expect(under.unpinned.some((pixel) => !sameColour(pixel, under.unpinned[0])), JSON.stringify(under)).toBe(true);
        // …and the same pixels run under the corner and under column A's header (to a unit or
        // two: each is composited on a layer of its own).
        const same = (a, b) => a.length === b.length && a.every((pixel, i) => sameColour(pixel, b[i], 3));
        expect(same(under.corner, under.unpinned), JSON.stringify(under)).toBe(true);
        expect(same(under.pinned, under.unpinned), JSON.stringify(under)).toBe(true);
    });
}

// ---- UX-18: the Focus outline is as wide on each of its four sides wherever it stands -------------

test('UX-18: the Focus outline has four equal edges beside a Pinned Column, beside the Headings, under the header and in the open', async ({ page }) => {
    await openSheet(page, '');
    const grid = sheet(page);
    // B4 beside the pinned A, A4 beside the Headings, C1 under the header, C4 away from all three.
    const found = {};
    for (const address of ['B4', 'A4', 'C1', 'C4']) {
        await clickCell(grid, address);
        await expectFocusAt(grid, address);
        await expectCovers(grid.locator('.ex-focus').first(), grid, address, address);
        await parkPointer(page, grid);
        found[address] = await focusEdges(grid, cell(grid, address));
    }
    for (const [address, edges] of Object.entries(found)) {
        expect(edges.across, `${address}: an edge each side, nothing between (${JSON.stringify(edges)})`).toHaveLength(2);
        expect(edges.down, `${address}: an edge each side, nothing between (${JSON.stringify(edges)})`).toHaveLength(2);
        expect([edges.right, edges.top, edges.bottom], `${address}: ${JSON.stringify(edges)}`).toEqual([edges.left, edges.left, edges.left]);
    }
    // And the same width at every place.
    expect(new Set(Object.values(found).map((e) => e.left)).size, JSON.stringify(found)).toBe(1);
});

test('UX-18: with Pinned Columns and no Headings, the Focus outline has four equal edges beside the pinned block, under the header and at the grid\'s left edge', async ({ page }) => {
    await page.goto('/wide');
    const grid = page.locator('.ex-grid').first();
    await expect(grid.locator('.ex-row').first()).toBeVisible();
    const found = {};
    // r0c2: the first scrollable column, in the first row. r3c0: the first pinned column, against
    // the grid's own left edge. r3c4: in the open.
    for (const id of ['r0c2', 'r3c0', 'r3c4']) {
        const target = grid.locator(`[id$='-${id}']`);
        await target.click({ force: true });
        await expect(grid).toHaveAttribute('aria-activedescendant', new RegExp(`-${id}$`));
        await parkPointer(page, grid);
        found[id] = await focusEdges(grid, target);
    }
    for (const [id, edges] of Object.entries(found)) {
        expect([edges.right, edges.top, edges.bottom], `${id}: ${JSON.stringify(edges)}`).toEqual([edges.left, edges.left, edges.left]);
        expect(edges.left, `${id}: ${JSON.stringify(edges)}`).toBeGreaterThan(0);
    }
});

// ---- UX-19: Excel's look for the Focus and a single range ------------------------------------------

for (const [chrome, query] of CHROMES) {
    test(`UX-19 (${chrome} Chrome): one cell selected is the Focus outline alone, untinted`, async ({ page }) => {
        await openSheet(page, query);
        const grid = sheet(page);
        await clickCell(grid, 'C3');
        await expectFocusAt(grid, 'C3');
        await expect(grid.locator('.ex-range')).toHaveCount(0);
        await parkPointer(page, grid);

        expect(await groundOf(page, cell(grid, 'C3'))).toEqual(await groundOf(page, cell(grid, 'D9')));
        const edges = await focusEdges(grid, cell(grid, 'C3'));
        expect([edges.across.length, edges.down.length], JSON.stringify(edges)).toEqual([2, 2]);
    });

    test(`UX-19 (${chrome} Chrome): a single range carries one outline round it, and the Focus inside it is untinted and not outlined`, async ({ page }) => {
        await openSheet(page, query);
        const grid = sheet(page);
        await clickCell(grid, 'C3');
        await expectFocusAt(grid, 'C3');
        const oneCell = await focusEdges(grid, cell(grid, 'C3'));
        await clickCell(grid, 'B3');
        await clickCell(grid, 'D6', { modifiers: ['Shift'] });
        await expectCovers(grid.locator('.ex-selection .ex-range'), grid, 'B3', 'D6');
        await expectFocusAt(grid, 'B3');
        await parkPointer(page, grid);

        const ground = await groundOf(page, cell(grid, 'D9'));
        expect(await groundOf(page, cell(grid, 'B3')), 'the Focus is untinted').toEqual(ground);
        expect(sameColour(await groundOf(page, cell(grid, 'C4')), ground), 'the rest of the range is tinted').toBe(false);
        // Across B3's row and down B's column: the range's two edges, and nothing where B3 meets
        // C3 or B4 — the Focus has no outline of its own. Each edge as wide as the Focus outline.
        const b3 = await cell(grid, 'B3').boundingBox();
        const d6 = await cell(grid, 'D6').boundingBox();
        const colour = await outlineColour(grid);
        const region = await painted(page, { x: b3.x - 6, y: b3.y - 6, width: d6.x + d6.width - b3.x + 12, height: d6.y + d6.height - b3.y + 12 });
        const across = runsOf(region.across(b3.y + 4, b3.x - 6, d6.x + d6.width + 6), colour);
        const down = runsOf(region.down(b3.x + 4, b3.y - 6, d6.y + d6.height + 6), colour);
        expect(across, JSON.stringify({ across, oneCell })).toEqual([oneCell.left, oneCell.left]);
        expect(down, JSON.stringify({ down, oneCell })).toEqual([oneCell.left, oneCell.left]);
    });

    test(`UX-19 (${chrome} Chrome): several ranges are each tinted with no outline, and the Focus among them is untinted and outlined`, async ({ page }) => {
        await openSheet(page, query);
        const grid = sheet(page);
        await clickCell(grid, 'B3');
        await clickCell(grid, 'C4', { modifiers: ['Shift'] });
        await expectCovers(grid.locator('.ex-selection .ex-range'), grid, 'B3', 'C4');
        await clickCell(grid, 'E6', { modifiers: ['ControlOrMeta'] });
        await clickCell(grid, 'F7', { modifiers: ['ControlOrMeta', 'Shift'] });
        await expect(grid.locator('.ex-selection .ex-range')).toHaveCount(2);
        await expectCovers(grid.locator('.ex-selection .ex-range').nth(1), grid, 'E6', 'F7');
        await expectFocusAt(grid, 'E6');
        await parkPointer(page, grid);

        const ground = await groundOf(page, cell(grid, 'D9'));
        const colour = await outlineColour(grid);
        expect(sameColour(await groundOf(page, cell(grid, 'C4')), ground), 'the first range is tinted').toBe(false);
        expect(sameColour(await groundOf(page, cell(grid, 'F6')), ground), 'the second range is tinted').toBe(false);
        expect(await groundOf(page, cell(grid, 'E6')), 'the Focus is untinted').toEqual(ground);
        // The first range has no outline: nothing across its top row or down its first column.
        const first = await runsAround(grid, await cell(grid, 'B3').boundingBox(), colour);
        expect(first, JSON.stringify(first)).toEqual({ across: [], down: [] });
        // The Focus has its own, and the range it is in has none past it: F6's right edge is bare.
        const focus = await focusEdges(grid, cell(grid, 'E6'));
        expect([focus.across.length, focus.down.length], JSON.stringify(focus)).toEqual([2, 2]);
        const f6 = await cell(grid, 'F6').boundingBox();
        const beyond = await painted(page, { x: f6.x + 4, y: f6.y, width: f6.width + 2, height: 8 });
        expect(runsOf(beyond.across(f6.y + 4, f6.x + 4, f6.x + f6.width + 2), colour)).toEqual([]);
    });

    test(`UX-19 (${chrome} Chrome): a single range across the pinned boundary shows no seam there, and slides beneath the pinned block`, async ({ page }) => {
        await openSheet(page, query);
        const grid = sheet(page);
        await clickCell(grid, 'A3');
        await clickCell(grid, 'C5', { modifiers: ['Shift'] });
        await expectFocusAt(grid, 'A3');
        await expect(grid.locator('.ex-selection-pinned .ex-range')).toHaveCount(1);
        await expect(grid.locator('.ex-selection .ex-range')).toHaveCount(1);
        await parkPointer(page, grid);
        const colour = await outlineColour(grid);
        const ground = await groundOf(page, cell(grid, 'D9'));

        for (const scrollLeft of [0, 40]) {
            await grid.locator('.ex-scroller').evaluate((scroller, left) => { scroller.scrollLeft = left; }, scrollLeft);
            // The scrollable part moves with the content; the render that follows a scroll is a
            // round trip away on the Server host, and changes nothing here that is read.
            await expect.poll(() => grid.locator('.ex-scroller').evaluate((s) => s.scrollLeft)).toBe(scrollLeft);
            await page.waitForTimeout(200);
            const a3 = await cell(grid, 'A3').boundingBox();
            const a4 = await cell(grid, 'A4').boundingBox();
            const c4 = await cell(grid, 'C4').boundingBox();
            const boundary = a3.x + a3.width;
            const region = await painted(page, { x: a3.x - 6, y: a3.y - 6, width: c4.x + c4.width - a3.x + 12, height: c4.y + c4.height - a3.y + 12 });
            // Across row 4: the range's left and right edges, and nothing at the boundary.
            const across = runsOf(region.across(a4.y + 4, a3.x - 6, c4.x + c4.width + 6), colour);
            expect(across, `scrolled ${scrollLeft}: ${JSON.stringify(across)}`).toHaveLength(2);
            // The top edge runs through the boundary, as wide on one side as on the other.
            const beforeIt = runsOf(region.down(boundary - 3, a3.y - 6, a3.y + 6), colour);
            const afterIt = runsOf(region.down(boundary + 3, a3.y - 6, a3.y + 6), colour);
            expect(afterIt, `scrolled ${scrollLeft}: ${JSON.stringify({ beforeIt, afterIt })}`).toEqual(beforeIt);
            expect(beforeIt).toHaveLength(1);
            // The Focus's cell is pinned and stays untinted: nothing of the scrollable part is
            // painted over the pinned block, where it passes beneath.
            expect(await groundOf(page, cell(grid, 'A3')), `scrolled ${scrollLeft}`).toEqual(ground);
            expect(sameColour(await groundOf(page, cell(grid, 'A4')), ground), `scrolled ${scrollLeft}`).toBe(false);
        }
        await grid.locator('.ex-scroller').evaluate((scroller) => { scroller.scrollLeft = 0; });
    });
}

test('UX-19: under forced colors every range is outlined and the Focus inside a single range takes its outline back (ADR-0008/0029)', async ({ page }) => {
    await openSheet(page, '');
    const grid = sheet(page);
    await clickCell(grid, 'B3');
    await clickCell(grid, 'D6', { modifiers: ['Shift'] });
    await expectCovers(grid.locator('.ex-selection .ex-range'), grid, 'B3', 'D6');
    const outlines = () => grid.evaluate((root) => {
        const read = (el) => {
            const s = getComputedStyle(el);
            return { style: s.outlineStyle, width: s.outlineWidth, offset: s.outlineOffset };
        };
        return {
            range: read(root.querySelector('.ex-selection .ex-range')),
            focus: read([...root.querySelectorAll('.ex-focus')].find((el) => el.getBoundingClientRect().width > 0)),
        };
    });
    const normal = await outlines();
    // Every outline lies inside its box: offset by its own width inward.
    expect(normal.range, JSON.stringify(normal)).toEqual({ style: 'solid', width: normal.range.width, offset: `-${normal.range.width}` });
    // The Focus inside a single range is marked by its missing tint alone…
    expect(normal.focus.style).toBe('none');

    // …which forced colors discard, so there it is outlined again, and the range still is.
    await page.emulateMedia({ forcedColors: 'active' });
    await expect.poll(async () => (await outlines()).focus.style).toBe('solid');
    const forced = await outlines();
    expect(forced.range, JSON.stringify(forced)).toEqual({ style: 'solid', width: forced.range.width, offset: `-${forced.range.width}` });
    expect(forced.focus, JSON.stringify(forced)).toEqual({ style: 'solid', width: forced.focus.width, offset: `-${forced.focus.width}` });
});
