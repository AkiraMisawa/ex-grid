import { test, expect, alterPage } from './fixtures.mjs';
import { sheet, cell, expectFocusAt, boxOf, spanOf, nameBox, pressCell } from './sheet-helpers.mjs';

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
    await expect(rowHeading(grid, 1048575)).toBeVisible();
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

const centreOf = (box) => ({ x: box.x + (box.width / 2), y: box.y + (box.height / 2) });

/** Waits until the range painted over the scrollable columns is whole columns from..to: across
 *  exactly their headings, from row 1 down past the bottom of the readable area. Column A is
 *  pinned, so B onwards are painted in the scrollable layer. */
async function expectWholeColumns(grid, from, to) {
    const range = grid.locator('.ex-selection .ex-range');
    const want = await spanOf(grid, `${from}1`, `${to}1`);
    const { bottom } = await readableOf(grid);
    await expect.poll(async () => {
        const box = await range.boundingBox();
        if (!box) {
            return 'not painted';
        }
        const near = (p, q) => Math.abs(p - q) <= 1.5;
        return near(box.x, want.x) && near(box.width, want.width) && near(box.y, want.y) && box.y + box.height >= bottom - 1
            ? 'whole columns'
            : JSON.stringify({ box, want, bottom });
    }).toBe('whole columns');
}

/** Waits until the pinned part of the range is whole rows from..to: from row `from`'s top, exactly
 *  their height. */
async function expectWholeRows(grid, from, to) {
    const range = grid.locator('.ex-selection-pinned .ex-range');
    const want = await spanOf(grid, `A${from}`, `A${to}`);
    await expect.poll(async () => {
        const box = await range.boundingBox();
        if (!box) {
            return 'not painted';
        }
        const near = (p, q) => Math.abs(p - q) <= 1.5;
        return near(box.y, want.y) && near(box.height, want.height) && near(box.x, want.x)
            ? 'whole rows'
            : JSON.stringify({ box, want });
    }).toBe('whole rows');
}

test('DC-42: a drag across Column Headings selects whole columns, over the Headings and over the cells', async ({ page }) => {
    const grid = sheet(page);
    const b = centreOf(await boxOf(heading(grid, 'B')));
    await page.mouse.move(b.x, b.y);
    await page.mouse.down();
    // The press selects at once: column B, the Focus on its first row.
    await expect(grid).toHaveAttribute('aria-activedescendant', /-r0c1$/);
    await expectWholeColumns(grid, 'B', 'B');

    const d = centreOf(await boxOf(heading(grid, 'D')));
    await page.mouse.move(d.x, d.y, { steps: 8 });
    await expectWholeColumns(grid, 'B', 'D');

    // Down over the cells: only the pointer's column counts, whatever row it is over.
    const e3 = centreOf(await boxOf(cell(grid, 'E3')));
    await page.mouse.move(e3.x, e3.y, { steps: 8 });
    await expectWholeColumns(grid, 'B', 'E');
    await expect(grid).toHaveAttribute('aria-activedescendant', /-r0c1$/);

    await page.mouse.up();
    await expectFocusAt(grid, 'B1');
    await expectWholeColumns(grid, 'B', 'E');
    // Nothing sorted: the rows are where they were.
    await expect(cell(grid, 'A2')).toHaveText('Apples');
    await expect(cell(grid, 'A4')).toHaveText('Plums');
});

test('DC-42: Shift+press on a Column Heading extends from the Focus\'s column, and the drag goes on', async ({ page }) => {
    const grid = sheet(page);
    await pressCell(grid, 'C3');

    const e = centreOf(await boxOf(heading(grid, 'E')));
    await page.mouse.move(e.x, e.y);
    await page.keyboard.down('Shift');
    await page.mouse.down();
    await expectWholeColumns(grid, 'C', 'E');
    const f = centreOf(await boxOf(heading(grid, 'F')));
    await page.mouse.move(f.x, f.y, { steps: 6 });
    await expectWholeColumns(grid, 'C', 'F');
    await page.mouse.up();
    await page.keyboard.up('Shift');

    // The Focus stayed where it was.
    await expectFocusAt(grid, 'C3');
});

test('DC-42: a drag across Row Headings selects whole rows, over the Headings and over the cells', async ({ page }) => {
    const grid = sheet(page);
    const three = centreOf(await boxOf(rowHeading(grid, 3)));
    await page.mouse.move(three.x, three.y);
    await page.mouse.down();
    await expect(grid).toHaveAttribute('aria-activedescendant', /-r2c0$/);

    const six = centreOf(await boxOf(rowHeading(grid, 6)));
    await page.mouse.move(six.x, six.y, { steps: 8 });
    await expectWholeRows(grid, 3, 6);

    const c8 = centreOf(await boxOf(cell(grid, 'C8')));
    await page.mouse.move(c8.x, c8.y, { steps: 8 });
    await expectWholeRows(grid, 3, 8);

    await page.mouse.up();
    await expectFocusAt(grid, 'A3');
    await expectWholeRows(grid, 3, 8);
});

test('DC-43: a Column Heading drag at the right edge scrolls sideways, and never down', async ({ page }) => {
    const grid = sheet(page);
    const b = centreOf(await boxOf(heading(grid, 'B')));
    await page.mouse.move(b.x, b.y);
    await page.mouse.down();
    await expect(grid).toHaveAttribute('aria-activedescendant', /-r0c1$/);
    const readable = await readableOf(grid);

    // Into the right-hand edge band, along the header.
    await page.mouse.move(readable.right - 3, b.y, { steps: 8 });
    await expect.poll(async () => (await scrollOf(grid)).left, { timeout: 10_000 }).toBeGreaterThan(200);
    // And down into the bottom band as well, over the cells: still sideways only.
    await page.mouse.move(readable.right - 3, readable.bottom - 3, { steps: 4 });
    await page.waitForTimeout(600);
    expect((await scrollOf(grid)).top).toBe(0);

    await page.mouse.up();
    // The Focus stayed on B1, now off screen, so the Name Box is where it is read.
    await expect(nameBox(grid)).toHaveValue('B1');
    expect((await scrollOf(grid)).top).toBe(0);
});

test('DC-43: a Row Heading drag at the bottom edge scrolls down, and never sideways', async ({ page }) => {
    const grid = sheet(page);
    const three = centreOf(await boxOf(rowHeading(grid, 3)));
    await page.mouse.move(three.x, three.y);
    await page.mouse.down();
    await expect(grid).toHaveAttribute('aria-activedescendant', /-r2c0$/);
    const readable = await readableOf(grid);

    // Into the bottom-right corner: both edge bands at once.
    await page.mouse.move(readable.right - 3, readable.bottom - 3, { steps: 8 });
    await expect.poll(async () => (await scrollOf(grid)).top, { timeout: 10_000 }).toBeGreaterThan(200);
    await page.waitForTimeout(300);
    expect((await scrollOf(grid)).left).toBe(0);

    await page.mouse.up();
    // The Focus stayed on A3, now off screen, so the Name Box is where it is read.
    await expect(nameBox(grid)).toHaveValue('A3');
    expect((await scrollOf(grid)).left).toBe(0);
});

test('DC-44: over more than one column the Size Tip shows the size at the Extent\'s Heading, and the Name Box is empty', async ({ page }) => {
    const grid = sheet(page);
    const tip = grid.locator('.ex-size-tip');
    const b = centreOf(await boxOf(heading(grid, 'B')));
    await page.mouse.move(b.x, b.y);
    await page.mouse.down();
    // Over one column: no tip, and the Name Box names the Focus.
    await expect(nameBox(grid)).toHaveValue('B1');
    await expect(tip).toHaveCount(0);

    const d = await boxOf(heading(grid, 'D'));
    await page.mouse.move(d.x + (d.width / 2), b.y, { steps: 8 });
    await expect(tip).toHaveText('1048576R x 3C');
    await expect(nameBox(grid)).toHaveValue('');
    // At D's heading, just beneath the band, and inside the grid's box.
    const box = await boxOf(tip);
    const root = await boxOf(grid);
    expect(Math.abs(box.x - d.x)).toBeLessThanOrEqual(1);
    expect(Math.abs(box.y - (d.y + d.height))).toBeLessThanOrEqual(1);
    expect(box.x).toBeGreaterThanOrEqual(root.x - 0.5);
    expect(box.x + box.width).toBeLessThanOrEqual(root.x + root.width + 0.5);
    expect(box.y + box.height).toBeLessThanOrEqual(root.y + root.height + 0.5);
    // Painted whole: the estimate it is sized by holds its text (ADR-0016's contract).
    expect(await tip.evaluate((el) => el.scrollWidth <= el.clientWidth)).toBe(true);

    // Back over one column: the tip goes and the Name Box names the Focus again.
    await page.mouse.move(b.x, b.y, { steps: 8 });
    await expect(tip).toHaveCount(0);
    await expect(nameBox(grid)).toHaveValue('B1');

    await page.mouse.move(d.x + (d.width / 2), b.y, { steps: 8 });
    await expect(tip).toHaveText('1048576R x 3C');
    await page.mouse.up();
    await expect(tip).toHaveCount(0);
    await expect(nameBox(grid)).toHaveValue('B1');
});

test('DC-44: a Row Heading drag shows its size beside the Extent\'s Row Heading', async ({ page }) => {
    const grid = sheet(page);
    const tip = grid.locator('.ex-size-tip');
    const three = centreOf(await boxOf(rowHeading(grid, 3)));
    await page.mouse.move(three.x, three.y);
    await page.mouse.down();
    await expect(nameBox(grid)).toHaveValue('A3');
    await expect(tip).toHaveCount(0);

    const six = await boxOf(rowHeading(grid, 6));
    await page.mouse.move(three.x, six.y + (six.height / 2), { steps: 8 });
    await expect(tip).toHaveText('4R x 16384C');
    await expect(nameBox(grid)).toHaveValue('');
    const box = await boxOf(tip);
    expect(Math.abs(box.y - six.y)).toBeLessThanOrEqual(1);
    expect(Math.abs(box.x - (six.x + six.width))).toBeLessThanOrEqual(1);

    await page.mouse.up();
    await expect(tip).toHaveCount(0);
    await expect(nameBox(grid)).toHaveValue('A3');
});

test('DC-44: the Size Tip is redrawn only when the Extent moves to another Heading', async ({ page }) => {
    const grid = sheet(page);
    const tip = grid.locator('.ex-size-tip');
    const b = centreOf(await boxOf(heading(grid, 'B')));
    const d = await boxOf(heading(grid, 'D'));
    await page.mouse.move(b.x, b.y);
    await page.mouse.down();
    await page.mouse.move(d.x + 4, b.y, { steps: 6 });
    await expect(tip).toHaveText('1048576R x 3C');

    // Every change to the tip's element, its text or its place, from here on.
    await alterPage(page, () => {
        window.__sizeTipWrites = 0;
        const root = document.querySelector('.ex-grid:has(> .ex-formula-bar)');
        const isTip = (node) => node?.nodeType === 1 && node.classList.contains('ex-size-tip');
        const observer = new MutationObserver((records) => {
            for (const record of records) {
                if (isTip(record.target) || isTip(record.target.parentNode)
                    || [...record.addedNodes, ...record.removedNodes].some(isTip)) {
                    window.__sizeTipWrites++;
                }
            }
        });
        observer.observe(root, { subtree: true, childList: true, attributes: true, characterData: true });
        return () => {
            observer.disconnect();
            delete window.__sizeTipWrites;
        };
    });
    const writes = () => page.evaluate(() => window.__sizeTipWrites);

    // Along D, over its heading and then over its cells: the Extent stays on D.
    await page.mouse.move(d.x + d.width - 4, b.y, { steps: 6 });
    const d4 = await boxOf(cell(grid, 'D4'));
    await page.mouse.move(d4.x + (d4.width / 2), d4.y + (d4.height / 2), { steps: 6 });
    await page.waitForTimeout(300);
    expect(await writes()).toBe(0);

    // Onto E: the tip is redrawn for the new Heading.
    const e4 = await boxOf(cell(grid, 'E4'));
    await page.mouse.move(e4.x + (e4.width / 2), e4.y + (e4.height / 2), { steps: 6 });
    await expect(tip).toHaveText('1048576R x 4C');
    expect(await writes()).toBeGreaterThan(0);
    await page.mouse.up();
});

test('SR-2e/DC-42: Ctrl+click on a Column Heading adds the column, and a second takes it out', async ({ page }) => {
    const grid = sheet(page);
    await pressCell(grid, 'B7');
    const ranges = grid.locator('.ex-range');

    await heading(grid, 'D').click({ force: true, modifiers: ['ControlOrMeta'] });
    // B7 stands, and D:D is added with the Focus on its first row.
    await expectFocusAt(grid, 'D1');
    await expect(ranges).toHaveCount(2);
    await expectWholeColumns(grid, 'D', 'D');

    await heading(grid, 'D').click({ force: true, modifiers: ['ControlOrMeta'] });
    // D:D, the range made last, is gone: the Focus goes to the range still standing.
    await expectFocusAt(grid, 'B7');
    await expect(ranges).toHaveCount(1);
});

test('SR-2e/DC-42: Ctrl+drag across Column Headings adds the columns crossed as one range', async ({ page }) => {
    const grid = sheet(page);
    await pressCell(grid, 'B7');

    const c = centreOf(await boxOf(heading(grid, 'C')));
    const e = centreOf(await boxOf(heading(grid, 'E')));
    await page.mouse.move(c.x, c.y);
    await page.keyboard.down('ControlOrMeta');
    await page.mouse.down();
    await page.mouse.move(e.x, e.y, { steps: 8 });
    await page.mouse.up();
    await page.keyboard.up('ControlOrMeta');

    await expectFocusAt(grid, 'C1');
    await expect(grid.locator('.ex-range')).toHaveCount(2);
    await expectWholeColumns(grid, 'C', 'E');
});

test('DC-42: Ctrl+click on a Row Heading adds the row, and a second takes it out', async ({ page }) => {
    const grid = sheet(page);
    await pressCell(grid, 'C2');
    const pinnedRanges = grid.locator('.ex-selection-pinned .ex-range');

    await rowHeading(grid, 5).click({ force: true, modifiers: ['ControlOrMeta'] });
    await expectFocusAt(grid, 'A5');
    // Row 5 runs across the pinned A as well; C2 is scrollable only.
    await expect(pinnedRanges).toHaveCount(1);
    await expectWholeRows(grid, 5, 5);

    await rowHeading(grid, 5).click({ force: true, modifiers: ['ControlOrMeta'] });
    await expectFocusAt(grid, 'C2');
    await expect(pinnedRanges).toHaveCount(0);
});
