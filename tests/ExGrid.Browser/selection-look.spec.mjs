import { test, expect, alterPage, circuitQuiet, twoFrames } from './fixtures.mjs';
import { sheet, cell, clickCell, expectFocusAt, expectCovers, pressAt } from './sheet-helpers.mjs';
import { painted, runsOf, sameColour, resolvedColour, paints, onDeviceGrid, wholePixelCentres, edgePixels, onDevicePixels } from './pixels.mjs';

// Excel's look for the Focus and a single range, and a whole header rule (ADR-0008 and ADR-0030
// of 2026-09-29, ticket 24), and for the Focus among several ranges (ADR-0008 of 2026-10-12), read
// in device pixels from what the browser painted: UX-17, UX-18 and UX-19. A computed style would
// say what the cascade resolved; these criteria are about a layer above the selection covering part
// of an outline, and a line lying on Device Pixels, which only the pixels show.
//
// /sheet is the page that has every neighbour at once — the Headings, a Pinned Column (A), the
// header — under the built-in Chrome and, with ?chrome=mud, under ExGrid.MudBlazor's. /wide has
// Pinned Columns (A and B) and no Headings. Lines are read 4 CSS px in from a cell's corner: clear
// of its text, which is padded further in, and of the fill handle on the opposite corner.

// Tall enough for the whole Sheet, as sheet.spec.mjs has it.
// Under chrome-150 the window Chrome draws at the display scale its command line sets, as an OS sets
// it: a viewport of Playwright's own is emulated at a scale of 1, which would take the 150% away. Only
// the Focus among several ranges at the display's scale runs there (playwright.config.mjs).
test.use({ viewport: async ({}, use, testInfo) => use(testInfo.project.name === 'chrome-150' ? null : { width: 1280, height: 1000 }) });

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

/** The Focus outline's colour as painted (sRGB), whatever syntax the cascade resolved it to: the
 * border of its box of its own, the ::after (ADR-0008, 2026-10-01). */
async function outlineColour(grid) {
    const css = await grid.locator('.ex-focus').first().evaluate((el) => getComputedStyle(el, '::after').borderTopColor);
    return resolvedColour(grid.page(), css);
}

/**
 * The outline-coloured runs, in device pixels, across a cell's box and a margin round it: along a
 * row 4 CSS px below its top, and down a column 4 CSS px right of its left. With the outline on
 * the cell's edges, outside them or inside where something would cover it (ADR-0008, 2026-10-01),
 * the first and last run of each are its four edges.
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
    test(`UX-17 (${chrome} Chrome): the header's rule runs under the Headings' corner and a pinned header as under any other (ADR-0029/0050)`, async ({ page }) => {
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

test('UX-18: the Focus outline has four equal edges beside a Pinned Column, beside the Headings, under the header and in the open (ADR-0008)', async ({ page }) => {
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

test('UX-18: with Pinned Columns and no Headings, the Focus outline has four equal edges beside the pinned block, under the header and at the grid\'s left edge (ADR-0008)', async ({ page }) => {
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

test('UX-18: a single range\'s outline has four equal edges beside a Pinned Column, beside the Headings, under the header and in the open, its sides outside the range where nothing covers them (ADR-0008, 2026-10-01)', async ({ page }) => {
    await openSheet(page, '');
    const grid = sheet(page);
    const colour = await (async () => {
        await clickCell(grid, 'D9');
        return outlineColour(grid);
    })();
    // B3:C4 beside the pinned A, A3:B4 beside the Headings, C1:D2 under the header, C3:D4 in the open.
    const found = {};
    for (const [from, to] of [['B3', 'C4'], ['A3', 'B4'], ['C1', 'D2'], ['C3', 'D4']]) {
        await clickCell(grid, from);
        await clickCell(grid, to, { modifiers: ['Shift'] });
        await expectCovers(grid.locator('.ex-selection .ex-range, .ex-selection-pinned .ex-range').first(), grid, from, to);
        await parkPointer(page, grid);
        const first = await cell(grid, from).boundingBox();
        const last = await cell(grid, to).boundingBox();
        const box = { x: first.x, y: first.y, width: last.x + last.width - first.x, height: last.y + last.height - first.y };
        const { across, down } = await runsAround(grid, box, colour);
        found[`${from}:${to}`] = { across, down };
        expect(across, `${from}:${to}: an edge each side, nothing between (${JSON.stringify({ across, down })})`).toHaveLength(2);
        expect(down, `${from}:${to}: an edge each side, nothing between (${JSON.stringify({ across, down })})`).toHaveLength(2);
        expect([across[1], down[0], down[1]], `${from}:${to}: ${JSON.stringify({ across, down })}`).toEqual([across[0], across[0], across[0]]);
    }
    // The same width at every place.
    expect(new Set(Object.values(found).map((e) => e.across[0])).size, JSON.stringify(found)).toBe(1);
});

// ---- UX-19: Excel's look for the Focus and a single range ------------------------------------------

for (const [chrome, query] of CHROMES) {
    test(`UX-19 (${chrome} Chrome): one cell selected is the Focus outline alone, with no selection tint (ADR-0008)`, async ({ page }) => {
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

    test(`UX-19 (${chrome} Chrome): a single range carries one outline round it, and its tint leaves out the Focus inside it, which has no outline (ADR-0008/0029)`, async ({ page }) => {
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
        expect(await groundOf(page, cell(grid, 'B3')), 'the selection\'s tint does not cover the Focus').toEqual(ground);
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
        // Drawn over the range's tint, not beneath it: the outline's pixels are its own colour. B's
        // left lies against the pinned A, where the outline's side stays inside the range (ADR-0008,
        // 2026-10-01).
        const edge = region.at(b3.x + 1, (b3.y + d6.y + d6.height) / 2);
        expect(sameColour(edge, colour, 8), JSON.stringify({ edge, colour })).toBe(true);
    });

    test(`UX-19 (${chrome} Chrome): several ranges are each tinted with no outline and no fill handle, and the Focus among them is untinted, with a thin border of one Device Pixel inside its cell in the Focus outline's colour (ADR-0008, 2026-10-12)`, async ({ page }) => {
        await openSheet(page, query);
        const grid = sheet(page);
        await clickCell(grid, 'B3');
        await expectFocusAt(grid, 'B3');
        // The Focus outline's colour, while one cell stands.
        const outline = await outlineColour(grid);
        await clickCell(grid, 'C4', { modifiers: ['Shift'] });
        await expectCovers(grid.locator('.ex-selection .ex-range'), grid, 'B3', 'C4');
        await expect(grid.locator('.ex-fill-handle')).toHaveCount(1);
        await clickCell(grid, 'E6', { modifiers: ['ControlOrMeta'] });
        await clickCell(grid, 'F7', { modifiers: ['ControlOrMeta', 'Shift'] });
        await expect(grid.locator('.ex-selection .ex-range')).toHaveCount(2);
        await expectCovers(grid.locator('.ex-selection .ex-range').nth(1), grid, 'E6', 'F7');
        await expectFocusAt(grid, 'E6');
        await expect(grid.locator('.ex-focus.ex-focus-among-ranges')).toHaveCount(1);
        await expect(grid.locator('.ex-fill-handle')).toHaveCount(0);
        await parkPointer(page, grid);
        // E6's edges on device pixels, where its border is read; every box is read after.
        const e6 = await onDevicePixels(page, cell(grid, 'E6'));

        const ground = await groundOf(page, cell(grid, 'D9'));
        expect(sameColour(await groundOf(page, cell(grid, 'C4')), ground), 'the first range is tinted').toBe(false);
        expect(sameColour(await groundOf(page, cell(grid, 'F6')), ground), 'the second range is tinted').toBe(false);
        expect(await groundOf(page, cell(grid, 'E6')), 'the selection\'s tint does not cover the Focus').toEqual(ground);
        // The border is the Focus outline's colour (2026-10-12, decided with the user: where nothing
        // names its own token), so it is the outline's green on the Paper and the palette's primary
        // under the MudBlazor Chrome.
        const border = await resolvedColour(page, await grid.locator('.ex-focus-among-ranges').evaluate((el) => getComputedStyle(el, '::after').borderTopColor));
        expect(border, JSON.stringify({ border, outline })).toEqual(outline);
        // No range has an outline: nothing across the first one's top row or down its first column,
        // and nothing past the second one's right edge. Round the Focus, nothing in that colour but
        // its border, one device pixel a side.
        const first = await runsAround(grid, await cell(grid, 'B3').boundingBox(), outline);
        expect(first, JSON.stringify(first)).toEqual({ across: [], down: [] });
        const f6 = await cell(grid, 'F6').boundingBox();
        const beyond = await painted(page, { x: f6.x + 4, y: f6.y, width: f6.width + 2, height: 8 });
        expect(runsOf(beyond.across(f6.y + 4, f6.x + 4, f6.x + f6.width + 2), outline)).toEqual([]);
        const around = await runsAround(grid, e6, outline);
        expect(around, JSON.stringify(around)).toEqual({ across: [1, 1], down: [1, 1] });
        // That border: one Device Pixel on each side, a Device Pixel in from the top and left edges
        // and from the gridline it holds on the right and the bottom (ADR-0008, 2026-10-12).
        const edges = await edgePixels(page, e6, border);
        const gridline = Math.max(1, Math.floor(edges.scale + 1e-9));
        expect(edges, JSON.stringify(edges)).toEqual({
            scale: edges.scale, left: [1], top: [1], right: [gridline + 1], bottom: [gridline + 1],
        });
    });

    test(`UX-19 (${chrome} Chrome): a single range across the pinned boundary shows no seam there, and slides beneath the pinned block (ADR-0008/0004)`, async ({ page }) => {
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
            await circuitQuiet();
            await twoFrames(page);
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
            // The Focus's cell is pinned and the tint stays off it: nothing of the scrollable
            // part is painted over the pinned block, where it passes beneath.
            expect(await groundOf(page, cell(grid, 'A3')), `scrolled ${scrollLeft}`).toEqual(ground);
            expect(sameColour(await groundOf(page, cell(grid, 'A4')), ground), `scrolled ${scrollLeft}`).toBe(false);
        }
        await grid.locator('.ex-scroller').evaluate((scroller) => { scroller.scrollLeft = 0; });
    });
}

test('UX-19 at the display\'s scale: ExGrid\'s own Focus among several ranges is one Device Pixel inside its cell in the Focus outline\'s colour, and its own token recolours it (ADR-0008, 2026-10-12; ADR-0090)', async ({ page }, testInfo) => {
    // /wide: ExGrid with no Theme, Pinned Columns A and B and no Headings, read at the scale the
    // display gives: 100% here, and a real 150% under the chrome-150 project, as an OS sets it. A
    // scale emulated through the DevTools protocol is not read: the screenshot is not taken at it,
    // and its pixels are the emulation's (device-pixel-225.spec.mjs's note).
    await page.goto('/wide');
    const grid = page.locator('.ex-grid').first();
    await expect(grid.locator('.ex-row').first()).toBeVisible();
    const at = (id) => grid.locator(`[id$='-${id}']`);
    const canvasText = await resolvedColour(page, 'CanvasText');
    const scale = await page.evaluate(() => devicePixelRatio);
    expect(scale).toBe(testInfo.project.name === 'chrome-150' ? 1.5 : 1);
    // The grid has been told its Device Pixel (ADR-0090), and every line reads it.
    await expect.poll(() => grid.evaluate((root) => /--ex-dp:\s*([\d.]+)px/.exec(root.getAttribute('style') ?? '')?.[1])).toBe(`${1 / scale}`);
    await at('r3c3').click({ force: true });
    await at('r4c4').click({ force: true, modifiers: ['Shift'] });
    await expect(grid).toHaveAttribute('aria-activedescendant', /-r3c3$/);
    await expect(grid.locator('.ex-range-single')).toHaveCount(1);
    await expect(grid.locator('.ex-focus-among-ranges')).toHaveCount(0);
    await at('r6c5').click({ force: true, modifiers: ['ControlOrMeta'] });
    await at('r7c6').click({ force: true, modifiers: ['ControlOrMeta', 'Shift'] });
    await expect(grid).toHaveAttribute('aria-activedescendant', /-r6c5$/);
    await expect(grid.locator('.ex-focus.ex-focus-among-ranges')).toHaveCount(1);
    await expect(grid.locator('.ex-range-single')).toHaveCount(0);
    await page.mouse.move(0, 0);
    await circuitQuiet();
    await twoFrames(page);
    // The page lays /wide's rows at a fraction of a pixel: moved onto device pixels to be read.
    const box = await onDevicePixels(page, at('r6c5'));

    // Each side's device pixels. A gridline is whole Device Pixels, one at 100% and 150% (ADR-0090).
    const gridline = Math.max(1, Math.floor(scale + 1e-9));
    const expected = { scale, left: [1], top: [1], right: [gridline + 1], bottom: [gridline + 1] };
    // With no Theme, the Focus outline's colour, which is CanvasText.
    const plain = await edgePixels(page, box, canvasText);
    expect(plain, JSON.stringify(plain)).toEqual(expected);

    // A Theme that colours the Focus colours this border with it (2026-10-12, decided with the user:
    // where nothing names its own token, the border is the Focus outline's colour)…
    await alterPage(page, () => {
        document.body.style.setProperty('--ex-focus-outline', 'rgb(0, 0, 200)');
        return () => document.body.style.removeProperty('--ex-focus-outline');
    });
    await twoFrames(page);
    const followed = await edgePixels(page, box, [0, 0, 200]);
    expect(followed, JSON.stringify(followed)).toEqual(expected);
    // …and its own token, where a Theme names it, wins over the Focus outline's.
    await alterPage(page, () => {
        document.body.style.setProperty('--ex-focus-among-ranges-outline', 'rgb(200, 0, 0)');
        return () => document.body.style.removeProperty('--ex-focus-among-ranges-outline');
    });
    await twoFrames(page);
    const own = await edgePixels(page, box, [200, 0, 0]);
    expect(own, JSON.stringify(own)).toEqual(expected);
});

test('UX-19: the range outline reads --ex-selection-outline, and a Theme that sets only the Focus outline colours both (ADR-0008/0029)', async ({ page }) => {
    await openSheet(page, '');
    const grid = sheet(page);
    await clickCell(grid, 'B3');
    await clickCell(grid, 'D6', { modifiers: ['Shift'] });
    await expectCovers(grid.locator('.ex-selection .ex-range'), grid, 'B3', 'D6');
    await parkPointer(page, grid);
    // Each outline is the border of its box of its own, the ::after (ADR-0008, 2026-10-01).
    const colours = () => grid.evaluate((root) => ({
        range: getComputedStyle(root.querySelector('.ex-selection .ex-range'), '::after').borderTopColor,
        focus: getComputedStyle([...root.querySelectorAll('.ex-focus')].find((el) => el.getBoundingClientRect().width > 0), '::after').borderTopColor,
    }));
    // The range's left edge, as painted, along B4's row.
    const paintedEdge = async (rgb) => {
        const b4 = await cell(grid, 'B4').boundingBox();
        const region = await painted(page, { x: b4.x - 4, y: b4.y, width: 12, height: 8 });
        return runsOf(region.across(b4.y + 4, b4.x - 4, b4.x + 6), rgb, 24);
    };

    // By default the range outline is the Focus outline's colour.
    const defaults = await colours();
    expect(defaults.range).toBe(defaults.focus);

    // A Theme that sets only the Focus outline's token colours both. On body, which outlives the
    // page: alterPage takes the tokens off as the test ends (ADR-0056).
    await alterPage(page, () => {
        document.body.style.setProperty('--ex-focus-outline', 'rgb(200, 0, 0)');
        return () => document.body.style.removeProperty('--ex-focus-outline');
    });
    expect(await colours()).toEqual({ range: 'rgb(200, 0, 0)', focus: 'rgb(200, 0, 0)' });
    expect(await paintedEdge([200, 0, 0])).toHaveLength(1);

    // The range's own token colours the range alone.
    await alterPage(page, () => {
        document.body.style.setProperty('--ex-selection-outline', 'rgb(0, 128, 0)');
        return () => document.body.style.removeProperty('--ex-selection-outline');
    });
    expect(await colours()).toEqual({ range: 'rgb(0, 128, 0)', focus: 'rgb(200, 0, 0)' });
    expect(await paintedEdge([0, 128, 0])).toHaveLength(1);
});

test('UX-19: under forced colors every range is outlined and the Focus inside a single range takes its outline back (ADR-0008/0029)', async ({ page }) => {
    await openSheet(page, '');
    const grid = sheet(page);
    await clickCell(grid, 'B3');
    await clickCell(grid, 'D6', { modifiers: ['Shift'] });
    await expectCovers(grid.locator('.ex-selection .ex-range'), grid, 'B3', 'D6');
    const outlines = () => grid.evaluate((root) => {
        const read = (el) => {
            const s = getComputedStyle(el);
            const box = getComputedStyle(el, '::after');
            return {
                style: s.outlineStyle, width: s.outlineWidth, offset: s.outlineOffset,
                box: box.content === 'none' ? 'none' : `${box.borderTopStyle} ${box.borderTopWidth}`,
            };
        };
        return {
            range: read(root.querySelector('.ex-selection .ex-range')),
            focus: read([...root.querySelectorAll('.ex-focus')].find((el) => el.getBoundingClientRect().width > 0)),
        };
    });
    const normal = await outlines();
    // The single range's outline is the border of its box of its own, where Excel's lies
    // (ADR-0008, 2026-10-01); the element itself draws none.
    expect(normal.range.style, JSON.stringify(normal)).toBe('none');
    expect(normal.range.box, JSON.stringify(normal)).toBe(`solid ${normal.range.width}`);
    // The Focus inside a single range is marked by its missing tint alone…
    expect(normal.focus.style).toBe('none');
    expect(normal.focus.box).toBe('none');

    // …which forced colors discard, so there both borders are visible. D2 uses their own
    // boxes to replace only Heading-covered sides while retaining inside borders elsewhere.
    await page.emulateMedia({ forcedColors: 'active' });
    await expect.poll(async () => (await outlines()).focus.box).toBe('solid 2px');
    const forced = await outlines();
    expect(forced.range, JSON.stringify(forced)).toEqual({ style: 'none', width: forced.range.width, offset: `-${forced.range.width}`, box: 'solid 2px' });
    expect(forced.focus, JSON.stringify(forced)).toEqual({ style: 'none', width: forced.focus.width, offset: `-${forced.focus.width}`, box: 'solid 2px' });
});

// ---- DC-12: the fill handle on a whole axis, beside its Heading (ADR-0050; 2026-10-12, decided with
//      the user: it follows Excel, from the seventeenth Windows run's h3; the readings of 2026-10-12, R5)

/** A Column Heading by its letters, and a Row Heading by its number, as headings.spec.mjs finds them. */
const columnHeading = (grid, letters) => grid.locator('.ex-header-cell', { hasText: new RegExp(`^${letters}$`) });
const rowHeading = (grid, number) => grid.locator('.ex-row .ex-row-heading', { hasText: new RegExp(`^${number}$`) });

/**
 * Whether the fill handle is painted where it stands, read in device pixels: the middle of the
 * pixels it covers whole shows the handle's own colour, judged with `paints`, and no layer above
 * the selection — the header, the Headings — covers it. The region is grown out to the device
 * grid, so each pixel read is the page's.
 */
async function handleShows(page, grid) {
    const handle = await grid.locator('.ex-fill-handle').boundingBox();
    const colour = await resolvedColour(page, await grid.locator('.ex-fill-handle')
        .evaluate((el) => getComputedStyle(el).backgroundColor));
    const scale = await page.evaluate(() => window.devicePixelRatio);
    const region = await painted(page, onDeviceGrid(handle, scale));
    const { xs, ys } = wholePixelCentres(handle, scale);
    const middle = region.at(xs[Math.floor(xs.length / 2)], ys[Math.floor(ys.length / 2)]);
    return { shows: paints(middle, colour), middle, colour };
}

for (const [chrome, query] of CHROMES) {
    test(`DC-12 (${chrome} Chrome): a whole column's handle stands at its first row's top-right, beside its Heading and wholly below it, where Excel's straddles the Heading's edge (ADR-0050; the seventeenth Windows run, h3; the readings of 2026-10-12, R5 (2))`, async ({ page }) => {
        await openSheet(page, query);
        const grid = sheet(page);
        await columnHeading(grid, 'C').click({ force: true });
        await expectFocusAt(grid, 'C1');
        await parkPointer(page, grid);

        const c1 = await cell(grid, 'C1').boundingBox();
        const header = await columnHeading(grid, 'C').boundingBox();
        await expect.poll(async () => {
            const box = await grid.locator('.ex-fill-handle').boundingBox();
            return box === null ? 'not painted' : JSON.stringify({
                centredOnTheRightEdge: Math.abs(box.x + box.width / 2 - (c1.x + c1.width)) <= 1,
                topOnTheFirstRowsTop: Math.abs(box.y - c1.y) <= 1,
                belowTheHeading: box.y >= header.y + header.height - 1,
            });
        }).toBe(JSON.stringify({ centredOnTheRightEdge: true, topOnTheFirstRowsTop: true, belowTheHeading: true }));
        const seen = await handleShows(page, grid);
        expect(seen.shows, JSON.stringify(seen)).toBe(true);
    });

    test(`DC-12 (${chrome} Chrome): a whole row's handle stands at its first column's bottom-left, beside its Heading and wholly right of it, where Excel's stands on the Heading's edge (ADR-0050; the seventeenth Windows run, h3; the readings of 2026-10-12, R5 (2))`, async ({ page }) => {
        await openSheet(page, query);
        const grid = sheet(page);
        await pressAt(rowHeading(grid, 3));
        await expectFocusAt(grid, 'A3');
        await parkPointer(page, grid);

        const a3 = await cell(grid, 'A3').boundingBox();
        const heading = await rowHeading(grid, 3).boundingBox();
        await expect.poll(async () => {
            const box = await grid.locator('.ex-fill-handle').boundingBox();
            return box === null ? 'not painted' : JSON.stringify({
                leftOnTheFirstColumnsLeft: Math.abs(box.x - a3.x) <= 1,
                centredOnTheBottomEdge: Math.abs(box.y + box.height / 2 - (a3.y + a3.height)) <= 1,
                rightOfTheHeading: box.x >= heading.x + heading.width - 1,
            });
        }).toBe(JSON.stringify({ leftOnTheFirstColumnsLeft: true, centredOnTheBottomEdge: true, rightOfTheHeading: true }));
        const seen = await handleShows(page, grid);
        expect(seen.shows, JSON.stringify(seen)).toBe(true);
    });

    test(`DC-12 (${chrome} Chrome): with row 1 scrolled away, a whole column's handle stays at the top-right of the part in view, beside the header and uncovered (ADR-0050; the readings of 2026-10-12, R5 (5))`, async ({ page }) => {
        await openSheet(page, query);
        const grid = sheet(page);
        await columnHeading(grid, 'C').click({ force: true });
        await expectFocusAt(grid, 'C1');
        await parkPointer(page, grid);
        // Three rows down, as R5's three presses on the scroll bar's arrow left it: row 4 at the top.
        const rowHeight = (await cell(grid, 'C2').boundingBox()).height;
        await grid.locator('.ex-scroller').first().evaluate((scroller, px) => { scroller.scrollTop = px; }, 3 * rowHeight);
        await expect(grid.locator('.ex-viewport').first()).toHaveAttribute('data-ex-first-row', '3');

        const c4 = await cell(grid, 'C4').boundingBox();
        const header = await columnHeading(grid, 'C').boundingBox();
        await expect.poll(async () => {
            const box = await grid.locator('.ex-fill-handle').boundingBox();
            return box === null ? 'not painted' : JSON.stringify({
                centredOnTheRightEdge: Math.abs(box.x + box.width / 2 - (c4.x + c4.width)) <= 1,
                topOnTheTopInView: Math.abs(box.y - c4.y) <= 1,
                belowTheHeading: box.y >= header.y + header.height - 1,
            });
        }).toBe(JSON.stringify({ centredOnTheRightEdge: true, topOnTheTopInView: true, belowTheHeading: true }));
        const seen = await handleShows(page, grid);
        expect(seen.shows, JSON.stringify(seen)).toBe(true);
    });
}

test('DC-12: every cell (Ctrl+A) shows no fill handle, as Excel showed none in view (ADR-0050; the readings of 2026-10-12, R5 (1))', async ({ page }) => {
    await openSheet(page, '');
    const grid = sheet(page);
    // A cell clear of the page's data, as R5 pressed Ctrl+A on an empty sheet.
    await clickCell(grid, 'F12');
    await expect(grid.locator('.ex-fill-handle')).toHaveCount(1);
    await expect(grid.locator('.ex-range')).toHaveCount(0);

    await page.keyboard.press('ControlOrMeta+A');

    // Every cell selected: a range is drawn, the Focus stays, and the handle is gone.
    await expect(grid.locator('.ex-selection .ex-range')).not.toHaveCount(0);
    await expectFocusAt(grid, 'F12');
    await expect(grid.locator('.ex-fill-handle')).toHaveCount(0);
});

test('UX-19: under forced colors the Focus among several ranges takes its outline back in place of its thin border, and every range is outlined (ADR-0008, 2026-10-12)', async ({ page }) => {
    await openSheet(page, '');
    const grid = sheet(page);
    await clickCell(grid, 'B3');
    await clickCell(grid, 'D6', { modifiers: ['Shift'] });
    await clickCell(grid, 'F8', { modifiers: ['ControlOrMeta'] });
    await clickCell(grid, 'G9', { modifiers: ['ControlOrMeta', 'Shift'] });
    await expect(grid.locator('.ex-selection .ex-range')).toHaveCount(2);
    await expectFocusAt(grid, 'F8');
    await expect(grid.locator('.ex-focus.ex-focus-among-ranges')).toHaveCount(1);
    const outlines = () => grid.evaluate((root) => {
        const read = (el) => {
            const s = getComputedStyle(el);
            const box = getComputedStyle(el, '::after');
            return { style: s.outlineStyle, offset: s.outlineOffset, box: box.content === 'none' ? 'none' : `${box.borderTopStyle} ${box.borderTopWidth}` };
        };
        return {
            ranges: [...root.querySelectorAll('.ex-selection .ex-range')].map(read),
            focus: read(root.querySelector('.ex-focus-among-ranges')),
            width: getComputedStyle(root.querySelector('.ex-focus-among-ranges')).outlineWidth,
        };
    });
    // The thin border is the Focus's box of its own, no wider than a CSS pixel, and no outline;
    // neither range draws anything of its own.
    const normal = await outlines();
    expect(normal.focus.style, JSON.stringify(normal)).toBe('none');
    expect(normal.focus.box, JSON.stringify(normal)).toMatch(/^solid [\d.]+px$/);
    expect(parseFloat(normal.focus.box.slice('solid '.length)), JSON.stringify(normal)).toBeLessThanOrEqual(1);
    expect(normal.ranges.map((range) => [range.style, range.box]), JSON.stringify(normal)).toEqual([['none', 'none'], ['none', 'none']]);

    // Forced colors restore a visible two-Device-Pixel Focus border; each range is outlined too.
    // Here no Heading covers it, so the restored border remains inside its cell.
    await page.emulateMedia({ forcedColors: 'active' });
    await expect.poll(async () => (await outlines()).focus.box).toBe('solid 2px');
    const forced = await outlines();
    expect(forced.focus, JSON.stringify(forced)).toEqual({ style: 'none', offset: `-${forced.width}`, box: 'solid 2px' });
    expect(forced.ranges.map((range) => [range.style, range.box]), JSON.stringify(forced)).toEqual([['solid', 'none'], ['solid', 'none']]);
});
