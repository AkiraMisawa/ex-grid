import { test, expect } from './fixtures.mjs';
import { sheet, cell, clickCell, expectFocusAt, expectCovers } from './sheet-helpers.mjs';
import { painted, runsOf, sameColour, resolvedColour } from './pixels.mjs';

// The Focus outline inside its cell, and a whole header rule (ADR-0008 of 2026-09-29, ticket
// 24), read in device pixels from what the browser painted: UX-17 and UX-18. A computed style
// would say what the cascade resolved; these criteria are about a layer above the selection
// covering part of an outline, which only the pixels show.
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
