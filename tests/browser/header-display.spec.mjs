import { test, expect, settled, setTicked } from './fixtures.mjs';

// The Header Display against /header-display (ADR-0500): a compact header gives the column menu's ▾ and the marks of
// the column's Sort and Filter one Header Box inside the cell's padding. What a header is charged and what a press
// means are judged without a browser (grid-header-display.test.ts, the shared metric cases). Here is what only a
// layout and a real pointer show: that the room charged is the room the stylesheet keeps, so no label lies under the
// box or is cut; that a press on the box and a click beside it land on different things; that the marks are painted;
// and that nothing a column comes to hold moves a column on screen.

const WIDE = { width: 1600, height: 1000 };

// The columns whose header, not their values, decides an Auto width on the page, by label.
const HEADER_DECIDED = ['ID', 'CCY', 'Tenor', 'Qty', 'Live', 'Rate', 'DV01'];

const gridIn = (page, id) => page.locator(`#${id} .ex-grid`);

const header = (grid, label) => grid.locator('.ex-header-cell', { hasText: new RegExp(`^${label.replace(/[()]/g, '\\$&')}$`) });

/** Each header's painted width, by its label. */
const widthsOf = (grid) => grid.evaluate((root) => Object.fromEntries(
    [...root.querySelectorAll('.ex-header-cell')].map((cell) => [cell.firstChild.textContent, cell.getBoundingClientRect().width])));

/** What a header's layout says of its label: whether it is cut, and how far its last glyph ends before the Header Box
 * begins, which is negative where the label lies under the box. */
const labelOf = (cell) => cell.evaluate((el) => {
    const range = document.createRange();
    range.selectNodeContents(el.firstChild);
    const box = el.querySelector('.ex-menu-button, .ex-header-state');
    return {
        cut: el.scrollWidth > el.clientWidth,
        clearOfBox: box.getBoundingClientRect().left - range.getBoundingClientRect().right,
    };
});

/** What the Header Box of a header paints: the picture of a state, or the ▾ of none. */
const boxOf = (cell) => cell.locator('.ex-menu-button').evaluate((button) => {
    const after = getComputedStyle(button, '::after');
    return { picture: after.maskImage !== 'none', glyph: after.content, strength: after.opacity };
});

async function open(page) {
    await page.setViewportSize(WIDE);
    await page.goto('/header-display');
    for (const id of ['held-grid', 'standard-grid', 'compact-grid']) {
        await expect(gridIn(page, id).locator('.ex-row').first()).toBeVisible();
        // A header band painted before the widths are all told is built again once they are (ADR-0220).
        await settled(gridIn(page, id));
    }
}

test('FN-12g: a compact header\'s Auto width is the standard one\'s less 28px where the header decides, and no label is cut or lies under its box (ADR-0500) @behaviour', async ({ page }) => {
    await open(page);
    const standard = await widthsOf(gridIn(page, 'standard-grid'));
    const compact = await widthsOf(gridIn(page, 'compact-grid'));

    // A column edge is drawn on a Device Pixel, so a painted width is within one of the width charged.
    for (const label of HEADER_DECIDED) {
        expect(standard[label] - compact[label], label).toBeGreaterThan(27);
        expect(standard[label] - compact[label], label).toBeLessThan(29);
    }
    for (const label of Object.keys(compact)) {
        const read = await labelOf(header(gridIn(page, 'compact-grid'), label));
        expect(read.cut, `${label} is cut`).toBe(false);
        expect(read.clearOfBox, `${label} lies under its box`).toBeGreaterThanOrEqual(0);
    }
});

test('FN-12f: under a compact header a click sorts and a press on the box opens the menu, the box paints the Sort and the Filter, and no width moves (ADR-0500) @behaviour', async ({ page }) => {
    await open(page);
    const grid = gridIn(page, 'held-grid');
    const declared = await widthsOf(grid);

    await page.locator('#header-display-compact').click();
    await expect(grid.locator('.ex-header')).toHaveAttribute('data-ex-header-display', 'compact');
    await settled(grid);
    // FN-12h: a width somebody set stands, and none is reported.
    expect(await widthsOf(grid)).toEqual(declared);
    await expect(page.locator('#width-status')).toHaveText('Changes:');
    expect(await boxOf(header(grid, 'CCY'))).toEqual({ picture: false, glyph: '"▾"', strength: '0.55' });

    // A click on the header, beside the box, is the header's: it sorts.
    const tenor = header(grid, 'Tenor');
    const cell = await tenor.boundingBox();
    await page.mouse.click(cell.x + 12, cell.y + (cell.height / 2));
    await expect(page.locator('#sort-status')).toHaveText('Sorts: Tenor ascending');
    await expect(tenor).toHaveAttribute('aria-sort', 'ascending');
    expect(await boxOf(tenor)).toEqual({ picture: true, glyph: '""', strength: '1' });

    // A press on the box is the menu's, whatever the box shows: nothing is sorted by it.
    const box = await tenor.locator('.ex-menu-button').boundingBox();
    await page.mouse.click(box.x + (box.width / 2), box.y + (box.height / 2));
    const popover = grid.locator('.ex-popover');
    await expect(popover.locator('[role=menuitem]').first()).toBeVisible();
    await expect(page.locator('#sort-status')).toHaveText('Sorts: Tenor ascending');
    expect(await widthsOf(grid), 'an open menu moves no column').toEqual(declared);

    // Filtered from that menu, the column says so in its box and in the button's name.
    await setTicked(popover.locator('.ex-popover-list label input[type=checkbox]').first(), false);
    await popover.locator('.ex-popover-actions button', { hasText: 'OK' }).click();
    await expect(page.locator('#filter-status')).toHaveText('Filtered: Tenor');
    await expect(tenor).toHaveAttribute('data-ex-filtered', '');
    await expect(tenor.locator('.ex-menu-button')).toHaveAccessibleName('Column menu, filtered');
    expect(await boxOf(tenor)).toEqual({ picture: true, glyph: '""', strength: '1' });

    await header(grid, 'CCY').hover({ force: true });
    expect(await widthsOf(grid), 'a Sort, a Filter and a pointer move no column').toEqual(declared);
});

test('FN-12f: Blazor\'s HeaderDisplay changes the header of the grid that stands, and each grid\'s Auto widths are its own display\'s (ADR-0500) @blazor', async ({ page }) => {
    await open(page);
    const grid = gridIn(page, 'held-grid');
    const declared = await widthsOf(grid);
    const root = await grid.elementHandle();

    await page.locator('#header-display-compact').click();
    await expect(page.locator('#header-display-status')).toHaveText('Display: compact');
    await expect(grid.locator('.ex-header')).toHaveAttribute('data-ex-header-display', 'compact');
    await settled(grid);

    expect(await root.evaluate((el) => el.isConnected), 'the grid that stood is the grid that stands').toBe(true);
    expect(await widthsOf(grid)).toEqual(declared);
    await expect(page.locator('#width-status')).toHaveText('Changes:');

    const standard = await widthsOf(gridIn(page, 'standard-grid'));
    const compact = await widthsOf(gridIn(page, 'compact-grid'));
    for (const label of HEADER_DECIDED) {
        expect(standard[label] - compact[label], label).toBeGreaterThan(27);
        expect(standard[label] - compact[label], label).toBeLessThan(29);
    }
});
