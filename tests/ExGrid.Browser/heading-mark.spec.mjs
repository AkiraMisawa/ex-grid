import { test, expect, alterPage, circuitQuiet, twoFrames } from './fixtures.mjs';
import { sheet, cell, clickCell, columnHeader, expectFocusAt, openSheet, pressAt } from './sheet-helpers.mjs';
import { painted, resolvedColour, paints, blend, onDeviceGrid, wholePixelCentres, edgePixels, onDevicePixels } from './pixels.mjs';

// The Heading Mark (ADR-0008, 2026-10-13; UX-21), read in device pixels from what the browser
// painted: for every Selection, the Headings of the columns and the rows that hold part of it carry
// a line of two Device Pixels on their edge toward the cells, where the readings of 2026-10-12 (R4)
// measured Excel's — under a column's Heading and down a row's right edge. Wherever a header or Row
// Headings stand: a Sheet's Headings, ExGrid's own header, ExPivot's report. What the core decided —
// which Headings, which boxes, the token — is layer 2's (HeadingMarkTests, ShippedStylesheetTests);
// here, that the line lies on the edge, two Device Pixels wide, in the colour, and that a layer above
// it covers it where it covers the columns.
//
// /sheet has the Headings, a Pinned Column (A) and the header, under the built-in Chrome and, with
// ?chrome=mud, under ExGrid.MudBlazor's. /wide is ExGrid with no Theme, Pinned Columns A and B and no
// Row Headings. /pivot is ExPivot's report.

// Tall enough for the whole Sheet, as selection-look.spec.mjs has it. Under chrome-150 the window
// Chrome draws at the display scale its command line sets, as an OS sets it.
test.use({ viewport: async ({}, use, testInfo) => use(testInfo.project.name === 'chrome-150' ? null : { width: 1280, height: 1000 }) });

const CHROMES = [['built-in', 'builtin'], ['MudBlazor', 'mud']];

/** The Device Pixels across each edge of a Heading's box that paint `colour` exactly (`edgePixels`):
 * 0 is the box's own last Device Pixel at that edge, 1 the next one in. */
async function marked(page, locator, colour) {
    const { scale, ...edges } = await edgePixels(page, await locator.boundingBox(), colour);
    return edges;
}

/** Where a marked Heading's line is: its two Device Pixels at the edge toward the cells, and no
 * other edge. */
const ON_THE_BOTTOM = { left: [], right: [], top: [], bottom: [0, 1] };
const ON_THE_RIGHT = { left: [], right: [0, 1], top: [], bottom: [] };
const NOWHERE = { left: [], right: [], top: [], bottom: [] };

/**
 * The line along a box's whole edge toward the cells, `side` 'bottom' or 'right': its two Device
 * Pixels, read on the pixels they cover whole (`wholePixelCentres`) through a region grown out to the
 * device grid (`onDeviceGrid`). Answers how many were read and how many paint `colour` (`paints`),
 * so a line covered along part of its length, or not drawn there, shows as fewer.
 */
async function lineAlong(page, box, side, colour) {
    const scale = await page.evaluate(() => devicePixelRatio);
    const two = 2 / scale;
    const line = side === 'bottom'
        ? { x: box.x, y: box.y + box.height - two, width: box.width, height: two }
        : { x: box.x + box.width - two, y: box.y, width: two, height: box.height };
    const region = await painted(page, onDeviceGrid(line, scale));
    const { xs, ys } = wholePixelCentres(line, scale);
    let painting = 0;
    for (const x of xs) {
        for (const y of ys) {
            painting += paints(region.at(x, y), colour) ? 1 : 0;
        }
    }
    return { read: xs.length * ys.length, painting };
}

/** Leaves the pointer off the grid, so no hover band is painted over what is read. */
async function parkPointer(page, grid) {
    await page.mouse.move(0, 0);
    await expect(grid.locator('.ex-hover-row')).toHaveCount(0);
}

/** A Row Heading of the Sheet by its row number. */
const rowHeading = (grid, number) => grid.locator(`.ex-row[aria-rowindex="${number}"] > .ex-row-heading`);

/** The mark's colour as the cascade resolved it, in sRGB bytes. */
async function markColour(page, grid) {
    return resolvedColour(page, await grid.locator('.ex-heading-mark').first().evaluate((el) => getComputedStyle(el).borderBottomColor));
}

for (const [chrome, mode] of CHROMES) {
    test(`UX-21 (${chrome} Chrome): on the Sheet the Headings of the columns and rows several ranges hold carry a line of two Device Pixels on their edge toward the cells, in the outline's colour, and the Headings between them none (ADR-0008, 2026-10-13; R4)`, async ({ page }) => {
        await openSheet(page, mode);
        const grid = sheet(page);
        await clickCell(grid, 'B3');
        await expectFocusAt(grid, 'B3');
        // The Focus outline's colour, while one cell stands: the Paper's green, or the palette's primary.
        const outline = await resolvedColour(page, await grid.locator('.ex-focus').first().evaluate((el) => getComputedStyle(el, '::after').borderTopColor));
        // R4's shape: a range, then Ctrl and a cell apart from it. Column D and row 5 lie between.
        await clickCell(grid, 'C4', { modifiers: ['Shift'] });
        await clickCell(grid, 'E6', { modifiers: ['ControlOrMeta'] });
        await expectFocusAt(grid, 'E6');
        await expect(grid.locator('.ex-focus-among-ranges')).toHaveCount(1);
        await expect(grid.locator('.ex-header > .ex-heading-mark')).toHaveCount(2);
        await expect(grid.locator('.ex-heading-marks-rows > .ex-heading-mark')).toHaveCount(2);
        await parkPointer(page, grid);
        await circuitQuiet();
        // The mark is the outline's colour (2026-10-13, decided with the user: where nothing names its
        // own token it is the Focus outline's).
        expect(await markColour(page, grid)).toEqual(outline);
        // B's Heading on device pixels, where its edges are read; every box is read after.
        await onDevicePixels(page, columnHeader(grid, 'B'));

        for (const letters of ['B', 'C', 'E']) {
            const edges = await marked(page, columnHeader(grid, letters), outline);
            expect(edges, `${letters}: ${JSON.stringify(edges)}`).toEqual(ON_THE_BOTTOM);
        }
        const d = await marked(page, columnHeader(grid, 'D'), outline);
        expect(d, `D: ${JSON.stringify(d)}`).toEqual(NOWHERE);
        for (const number of [3, 4, 6]) {
            const edges = await marked(page, rowHeading(grid, number), outline);
            expect(edges, `row ${number}: ${JSON.stringify(edges)}`).toEqual(ON_THE_RIGHT);
        }
        const five = await marked(page, rowHeading(grid, 5), outline);
        expect(five, `row 5: ${JSON.stringify(five)}`).toEqual(NOWHERE);

        // Along its whole length: one line under B and C, unbroken where they meet, as Excel's ran on
        // under A and B (R4 (2)); and one down rows 3 and 4.
        const columns = await grid.locator('.ex-header > .ex-heading-mark').first().boundingBox();
        const underBC = await lineAlong(page, columns, 'bottom', outline);
        expect(underBC.read).toBeGreaterThan(0);
        expect(underBC, JSON.stringify(underBC)).toEqual({ read: underBC.read, painting: underBC.read });
        const rows = await grid.locator('.ex-heading-marks-rows > .ex-heading-mark').first().boundingBox();
        const downThreeFour = await lineAlong(page, rows, 'right', outline);
        expect(downThreeFour.read).toBeGreaterThan(0);
        expect(downThreeFour, JSON.stringify(downThreeFour)).toEqual({ read: downThreeFour.read, painting: downThreeFour.read });
    });

    test(`UX-21 (${chrome} Chrome): a whole column marks its Heading and every Row Heading in view, and a press on the corner, which selects every cell, every Heading (ADR-0008, 2026-10-13; R5)`, async ({ page }) => {
        await openSheet(page, mode);
        const grid = sheet(page);
        await clickCell(grid, 'B3');
        await expectFocusAt(grid, 'B3');
        const outline = await resolvedColour(page, await grid.locator('.ex-focus').first().evaluate((el) => getComputedStyle(el, '::after').borderTopColor));
        await pressAt(columnHeader(grid, 'C'));
        await expectFocusAt(grid, 'C1');
        await parkPointer(page, grid);
        await circuitQuiet();
        await onDevicePixels(page, columnHeader(grid, 'C'));

        // A single range's outline lies inside it where the header would cover its outer pixels
        // (ADR-0008, 2026-10-01; UX-18): its top side is the two Device Pixels just below the Heading,
        // beside the mark's two on the Heading's edge (built as both decisions read; Excel's outline
        // and its Heading's line were one line there, R5 (2)).
        expect(await marked(page, columnHeader(grid, 'C'), outline)).toEqual({ ...ON_THE_BOTTOM, bottom: [-2, -1, 0, 1] });
        expect(await marked(page, columnHeader(grid, 'B'), outline)).toEqual(NOWHERE);
        for (const number of [1, 2, 7, 12]) {
            const edges = await marked(page, rowHeading(grid, number), outline);
            expect(edges, `row ${number}: ${JSON.stringify(edges)}`).toEqual(ON_THE_RIGHT);
        }

        // The corner selects every cell: every column's Heading in view is marked too, the pinned A's
        // among them.
        await pressAt(grid.locator('.ex-headings-corner'));
        await expect(grid.locator('.ex-heading-marks-pinned > .ex-heading-mark')).toHaveCount(1);
        await circuitQuiet();
        await twoFrames(page);
        for (const letters of ['A', 'B', 'D']) {
            const edges = await marked(page, columnHeader(grid, letters), outline);
            expect(edges, `${letters}: ${JSON.stringify(edges)}`).toEqual({ ...ON_THE_BOTTOM, bottom: [-2, -1, 0, 1] });
        }
    });
}

test('UX-21 at the display\'s scale: ExGrid\'s own header is marked in the Focus outline\'s colour, which a Theme recolours, and its own token wins (ADR-0008, 2026-10-13; ADR-0090)', async ({ page }, testInfo) => {
    // /wide: ExGrid with no Theme and no Row Headings, read at the scale the display gives: 100%
    // here, and a real 150% under the chrome-150 project, as an OS sets it.
    await page.goto('/wide');
    const grid = page.locator('.ex-grid').first();
    await expect(grid.locator('.ex-row').first()).toBeVisible();
    const at = (id) => grid.locator(`[id$='-${id}']`);
    const header = (column) => grid.locator(`.ex-header > .ex-header-cell[aria-colindex="${column + 1}"]`);
    const scale = await page.evaluate(() => devicePixelRatio);
    expect(scale).toBe(testInfo.project.name === 'chrome-150' ? 1.5 : 1);
    await expect.poll(() => grid.evaluate((root) => /--ex-dp:\s*([\d.]+)px/.exec(root.getAttribute('style') ?? '')?.[1])).toBe(`${1 / scale}`);
    await at('r3c3').click({ force: true });
    await at('r4c4').click({ force: true, modifiers: ['Shift'] });
    await expect(grid).toHaveAttribute('aria-activedescendant', /-r3c3$/);
    // No Row Headings on /wide: the header alone carries the mark.
    await expect(grid.locator('.ex-header > .ex-heading-mark')).toHaveCount(1);
    await expect(grid.locator('.ex-heading-marks-rows')).toHaveCount(0);
    await page.mouse.move(0, 0);
    await circuitQuiet();
    await twoFrames(page);
    await onDevicePixels(page, header(3));

    // With no Theme, the Focus outline's colour, which is CanvasText.
    const canvasText = await resolvedColour(page, 'CanvasText');
    expect(await marked(page, header(3), canvasText)).toEqual(ON_THE_BOTTOM);
    expect(await marked(page, header(4), canvasText)).toEqual(ON_THE_BOTTOM);
    expect(await marked(page, header(2), canvasText)).toEqual(NOWHERE);
    expect(await marked(page, header(5), canvasText)).toEqual(NOWHERE);

    // A Theme that colours the Focus colours the mark with it (2026-10-13, decided with the user: where
    // nothing names its own token, the mark is the Focus outline's colour)…
    await alterPage(page, () => {
        document.body.style.setProperty('--ex-focus-outline', 'rgb(0, 0, 200)');
        return () => document.body.style.removeProperty('--ex-focus-outline');
    });
    await twoFrames(page);
    expect(await marked(page, header(3), [0, 0, 200])).toEqual(ON_THE_BOTTOM);
    // …and its own token, where a Theme names it, wins over the Focus outline's.
    await alterPage(page, () => {
        document.body.style.setProperty('--ex-heading-mark', 'rgb(200, 0, 0)');
        return () => document.body.style.removeProperty('--ex-heading-mark');
    });
    await twoFrames(page);
    expect(await marked(page, header(4), [200, 0, 0])).toEqual(ON_THE_BOTTOM);
    const line = await lineAlong(page, await grid.locator('.ex-header > .ex-heading-mark').boundingBox(), 'bottom', [200, 0, 0]);
    expect(line, JSON.stringify(line)).toEqual({ read: line.read, painting: line.read });
});

test('UX-21: the mark over a Pinned Column\'s header stays with it, and a scrollable column\'s passes beneath the pinned headers as the column does (ADR-0008, 2026-10-13; ADR-0004)', async ({ page }) => {
    await page.goto('/wide');
    const grid = page.locator('.ex-grid').first();
    await expect(grid.locator('.ex-row').first()).toBeVisible();
    const at = (id) => grid.locator(`[id$='-${id}']`);
    const header = (column) => grid.locator(`.ex-header > .ex-header-cell[aria-colindex="${column + 1}"]`);
    const scroller = grid.locator('.ex-scroller');
    // A colour nothing else on the page paints, so a covered line reads as no pixel of it at all.
    const red = [200, 0, 0];
    await alterPage(page, () => {
        document.body.style.setProperty('--ex-heading-mark', 'rgb(200, 0, 0)');
        return () => document.body.style.removeProperty('--ex-heading-mark');
    });
    /** Scrolls the rows so that `column`'s first 30 px lie beneath the pinned block. */
    const tuck = async (column) => {
        const pinnedRight = await header(1).evaluate((el) => el.getBoundingClientRect().right);
        const left = await header(column).evaluate((el) => el.getBoundingClientRect().left);
        const by = await scroller.evaluate((s, step) => { s.scrollLeft += step; return s.scrollLeft; }, left - pinnedRight + 30);
        await expect.poll(() => scroller.evaluate((s) => s.scrollLeft)).toBe(by);
        await circuitQuiet();
        await twoFrames(page);
    };

    // B, pinned, to D: the pinned part over B's header, the rest over C's and D's.
    await at('r3c1').click({ force: true });
    await at('r3c3').click({ force: true, modifiers: ['Shift'] });
    await expect(grid.locator('.ex-heading-marks-pinned > .ex-heading-mark')).toHaveCount(1);
    await expect(grid.locator('.ex-header > .ex-heading-mark')).toHaveCount(1);
    await page.mouse.move(0, 0);
    await tuck(2);
    await onDevicePixels(page, header(1));
    const whole = async (column) => lineAlong(page, await header(column).boundingBox(), 'bottom', red);
    // A is not marked; B's line stays whole while C passes beneath it.
    expect(await whole(0)).toMatchObject({ painting: 0 });
    const underB = await whole(1);
    expect(underB, JSON.stringify(underB)).toEqual({ read: underB.read, painting: underB.read });
    // C's part still in view, right of the pinned block, is marked along all of it.
    const pinnedRight = (await header(1).boundingBox()).x + (await header(1).boundingBox()).width;
    const c = await header(2).boundingBox();
    const cInView = await lineAlong(page, { x: pinnedRight, y: c.y, width: c.x + c.width - pinnedRight, height: c.height }, 'bottom', red);
    expect(cInView.read).toBeGreaterThan(0);
    expect(cInView, JSON.stringify(cInView)).toEqual({ read: cInView.read, painting: cInView.read });

    // D to E, scrollable only: no pinned layer, and D's mark passes beneath A's and B's headers.
    await scroller.evaluate((s) => { s.scrollLeft = 0; });
    await expect.poll(() => scroller.evaluate((s) => s.scrollLeft)).toBe(0);
    await at('r3c3').click({ force: true });
    await at('r3c4').click({ force: true, modifiers: ['Shift'] });
    await expect(grid.locator('.ex-heading-marks-pinned')).toHaveCount(0);
    await page.mouse.move(0, 0);
    await tuck(3);
    expect(await whole(0)).toMatchObject({ painting: 0 });
    expect(await whole(1)).toMatchObject({ painting: 0 });
    const e = await whole(4);
    expect(e, JSON.stringify(e)).toEqual({ read: e.read, painting: e.read });
    await scroller.evaluate((s) => { s.scrollLeft = 0; });
});

test('UX-21: under forced colors the line is kept, two Device Pixels on the edge, in the system colour a range\'s outline is restated in (ADR-0008, 2026-10-13)', async ({ page }) => {
    await page.goto('/wide');
    const grid = page.locator('.ex-grid').first();
    await expect(grid.locator('.ex-row').first()).toBeVisible();
    const at = (id) => grid.locator(`[id$='-${id}']`);
    const header = (column) => grid.locator(`.ex-header > .ex-header-cell[aria-colindex="${column + 1}"]`);
    await at('r3c3').click({ force: true });
    await at('r3c4').click({ force: true, modifiers: ['Shift'] });
    await expect(grid.locator('.ex-header > .ex-heading-mark')).toHaveCount(1);
    await expect(grid.locator('.ex-selection > .ex-range')).toHaveCount(1);
    await page.mouse.move(0, 0);

    await page.emulateMedia({ forcedColors: 'active' });
    // The range is restated by its outline, in Highlight (ADR-0008), and the mark's line in the same.
    const read = () => grid.evaluate((root) => ({
        range: getComputedStyle(root.querySelector('.ex-selection > .ex-range')).outlineStyle,
        same: getComputedStyle(root.querySelector('.ex-heading-mark')).borderBottomColor
            === getComputedStyle(root.querySelector('.ex-selection > .ex-range')).outlineColor,
    }));
    await expect.poll(read).toEqual({ range: 'solid', same: true });
    // Chrome's forced Highlight may be translucent (rgba(5, 0, 73, 0.8) under its emulation), so the
    // line paints it over the header's ground: the exact blend, judged with `paints`.
    const { line, ground } = await grid.evaluate((root) => ({
        line: getComputedStyle(root.querySelector('.ex-heading-mark')).borderBottomColor,
        ground: getComputedStyle(root.querySelector('.ex-header')).backgroundColor,
    }));
    const channels = (css) => /rgba?\(([^)]*)\)/.exec(css)[1].split(',').map(Number);
    const [r, g, b, alpha = 1] = channels(line);
    const colour = blend([r, g, b], Math.round(alpha * 255) / 255, channels(ground).slice(0, 3));
    await circuitQuiet();
    await twoFrames(page);
    await onDevicePixels(page, header(3));
    expect(await marked(page, header(3), colour)).toEqual(ON_THE_BOTTOM);
    expect(await marked(page, header(4), colour)).toEqual(ON_THE_BOTTOM);
    expect(await marked(page, header(2), colour)).toEqual(NOWHERE);
});

for (const [chrome, mode] of CHROMES) {
    test(`UX-21 (${chrome} Chrome): ExPivot's report header is marked over the selected value's column and not over the labels', in the Focus outline's colour (ADR-0008, 2026-10-13; ADR-0059)`, async ({ page }) => {
        await page.goto(`/pivot?chrome=${mode}`);
        const report = page.locator('.ex-pivot .ex-pivot-sheet > .ex-grid');
        const rows = report.locator('.ex-viewport .ex-row');
        await expect(rows.first()).toBeVisible({ timeout: 30_000 });
        await expect.poll(() => page.locator('.ex-pivot').evaluate((p) => getComputedStyle(p).display)).toBe('flex');
        const header = (column) => report.locator(`.ex-header > .ex-header-cell[aria-colindex="${column + 1}"]`);

        // Row 1's first value: column 1, beside the pinned labels in column 0.
        await pressAt(rows.nth(1).locator('[role=gridcell]').nth(1));
        await expect(report.locator('.ex-header > .ex-heading-mark')).toHaveCount(1);
        await expect(report.locator('.ex-heading-marks-pinned')).toHaveCount(0);
        await page.mouse.move(0, 0);
        await circuitQuiet();
        const outline = await resolvedColour(page, await report.locator('.ex-focus').first().evaluate((el) => getComputedStyle(el, '::after').borderTopColor));
        expect(await markColour(page, report)).toEqual(outline);
        await onDevicePixels(page, header(1));
        expect(await marked(page, header(1), outline)).toEqual(ON_THE_BOTTOM);
        expect(await marked(page, header(0), outline)).toEqual(NOWHERE);
    });
}
