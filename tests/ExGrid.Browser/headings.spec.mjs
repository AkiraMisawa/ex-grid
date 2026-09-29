import { test, expect } from './fixtures.mjs';
import { sheet, cell, expectFocusAt, boxOf } from './sheet-helpers.mjs';

// The Headings as Excel's (ADR-0050 item 1, ADR-0052 and ADR-0012, all as settled on 2026-09-29),
// on the DemoHost's /sheet: an axis a range spans end to end is not scrolled for (SR-2c), a drag
// across Column Headings or Row Headings (DC-42), its edge band along the Heading's axis only
// (DC-43), the Size Tip (DC-44), and Ctrl+click and Ctrl+drag (SR-2e, DC-42). The same gestures
// on a plain ExGrid's column header are in sizing.spec.mjs, beside Shift+click (SR-2a).
//
// The Sheet's column A is pinned; the page's Sheet holds a small table at A1:D5.

// Tall enough that the Sheet, Formula Bar to horizontal scrollbar, is inside the window, as in
// sheet.spec.mjs: a pointer below the window's edge reaches nothing.
test.use({ viewport: { width: 1280, height: 1000 } });

test.beforeEach(async ({ page }) => {
    await page.goto('/sheet');
    await expect(page.locator('#demo-interactive')).toBeAttached({ timeout: 30_000 });
    await expect(cell(sheet(page), 'A1')).toHaveText('Item');
});

/** A Column Heading by its letters. */
function heading(grid, letters) {
    return grid.locator('.ex-header-cell', { hasText: new RegExp(`^${letters}$`) });
}

/** A Row Heading by its number. */
function rowHeading(grid, number) {
    return grid.locator('.ex-row .ex-row-heading', { hasText: new RegExp(`^${number}$`) });
}

const scrollOf = (grid) => grid.locator('.ex-scroller').evaluate((el) => ({ top: el.scrollTop, left: el.scrollLeft }));

/** The readable box of the scroller: its client area, the header band and the scrollbars left out. */
const readableOf = (grid) => grid.locator('.ex-scroller').evaluate((el) => {
    const box = el.getBoundingClientRect();
    const header = el.querySelector('.ex-header');
    const band = header ? header.getBoundingClientRect().height : 0;
    return {
        left: box.left + el.clientLeft,
        top: box.top + el.clientTop + band,
        right: box.left + el.clientLeft + el.clientWidth,
        bottom: box.top + el.clientTop + el.clientHeight,
    };
});

test('SR-2c: with the view at the top, a Column Heading click then Shift+→ leaves scrollTop where it was', async ({ page }) => {
    const grid = sheet(page);
    expect((await scrollOf(grid)).top).toBe(0);

    await heading(grid, 'C').click({ force: true });
    await expectFocusAt(grid, 'C1');
    await page.keyboard.press('Shift+ArrowRight');

    // C:D: the range is two columns wide, and the Focus has not moved.
    const range = grid.locator('.ex-selection .ex-range');
    const c1 = await boxOf(cell(grid, 'C1'));
    const d1 = await boxOf(cell(grid, 'D1'));
    await expect.poll(async () => Math.round((await boxOf(range)).width)).toBe(Math.round(d1.x + d1.width - c1.x));
    await expectFocusAt(grid, 'C1');
    // A reveal writes after the render that painted the range, and on a circuit its scroll event is
    // a round trip behind: long enough for either to have landed.
    await page.waitForTimeout(500);
    expect((await scrollOf(grid)).top).toBe(0);
    await expect(cell(grid, 'C1')).toBeVisible();
});

test('SR-2c: Shift+↑ from a whole column leaves it one row short, and scrolls to show the Extent', async ({ page }) => {
    const grid = sheet(page);
    await heading(grid, 'C').click({ force: true });
    await expectFocusAt(grid, 'C1');

    await page.keyboard.press('Shift+ArrowUp');

    // The Extent moved from the last row to the one above it, and the view went to it (Excel does).
    const extent = cell(grid, 'C1048575');
    await expect(extent).toBeVisible();
    const box = await boxOf(extent);
    const readable = await readableOf(grid);
    expect(box.y).toBeGreaterThanOrEqual(readable.top - 1);
    expect(box.y + box.height).toBeLessThanOrEqual(readable.bottom + 1);
    await expect(rowHeading(grid, 1048576)).toBeVisible();
    await expectFocusAt(grid, 'C1');
});

test('SR-2c: with the view at the left, a Row Heading click then Shift+↓ leaves scrollLeft where it was', async ({ page }) => {
    const grid = sheet(page);
    expect((await scrollOf(grid)).left).toBe(0);

    await rowHeading(grid, 3).click({ force: true });
    await expectFocusAt(grid, 'A3');
    await page.keyboard.press('Shift+ArrowDown');

    const a3 = await boxOf(cell(grid, 'A3'));
    await expect.poll(async () => Math.round((await boxOf(grid.locator('.ex-selection-pinned .ex-range'))).height))
        .toBe(Math.round(2 * a3.height));
    await page.waitForTimeout(500);
    expect((await scrollOf(grid)).left).toBe(0);
    await expect(heading(grid, 'B')).toBeVisible();
});
