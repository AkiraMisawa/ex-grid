import { test, expect } from './fixtures.mjs';

// Column widths and a box that narrows, against the /sizing page (ADR-0016 and ADR-0043,
// decided 2026-09-25). The estimates are checked against what the browser paints: a
// header or a value the estimate says fits must not come out cut, which only a real
// layout can show. The page's grid fills the window's width; its two pinned columns are
// 110px and 120px.

test.beforeEach(async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/sizing');
    await expect(grid(page).locator('.ex-row').first()).toBeVisible();
});

function grid(page) {
    return page.locator('.ex-grid').first();
}

function header(page, name) {
    return grid(page).locator('.ex-header-cell', { hasText: name });
}

async function clickCell(page, row, column) {
    // Cells are pointer-events: none by design; the Viewport is the delegated target.
    await grid(page).locator(`[id$='r${row}c${column}']`).click({ force: true });
}

// Whether an element's content overflows its box: what an ellipsis, or a clipped run of
// hashes, looks like to the layout.
async function overflows(locator) {
    return locator.evaluate((el) => el.scrollWidth > el.clientWidth);
}

async function dragGrip(page, name, dx) {
    const box = await header(page, name).locator('.ex-resize-grip').boundingBox();
    const x = box.x + (box.width / 2);
    const y = box.y + (box.height / 2);
    await page.mouse.move(x, y);
    await page.mouse.down();
    await page.mouse.move(x + dx, y, { steps: 4 });
    await page.mouse.up();
}

test('a sorted Auto header and a Japanese Auto header paint whole (FN-12d, ADR-0016)', async ({ page }) => {
    await expect(header(page, 'Notional')).toHaveAttribute('aria-sort', 'ascending');
    expect(await overflows(header(page, 'Notional'))).toBe(false);
    expect(await overflows(header(page, '評価額'))).toBe(false);
});

test('a bold total the estimate fits paints whole, and a #### run fits its cell (FN-12e, ADR-0016)', async ({ page }) => {
    await clickCell(page, 0, 2);
    await page.keyboard.press('Control+End');
    const total = grid(page).locator('.ex-row-total');
    await expect(total).toBeVisible();

    const notional = total.locator(".ex-cell[id$='c2']");
    await expect(notional).not.toHaveText(/#/);
    expect(await overflows(notional)).toBe(false);

    const narrow = grid(page).locator(".ex-cell[id$='c4']").first();
    await expect(narrow).toHaveText(/^#+$/);
    expect(await overflows(narrow)).toBe(false);
});

test('a double-click on an edge fits a value the Viewport has not painted (FN-12a, ADR-0016)', async ({ page }) => {
    await expect(grid(page).locator('.ex-cell', { hasText: 'A note far longer' })).toHaveCount(0);

    await header(page, 'Note').locator('.ex-resize-grip').dblclick({ force: true });

    await expect(page.locator('#width-status')).toHaveText(/^Widths: Note=\d/);
    await expect(page.locator('#sort-status')).toHaveText('Sorts: Notional ascending');
    // Bring the long note on screen: the fitted column shows it whole.
    await clickCell(page, 0, 5);
    for (let i = 0; i < 60; i++)
        await page.keyboard.press('ArrowDown');
    const note = grid(page).locator('.ex-cell', { hasText: 'A note far longer' });
    await expect(note).toBeVisible();
    expect(await overflows(note)).toBe(false);
});

test('a press on an edge that does not move reports no width and sorts nothing (FN-12b, ADR-0016)', async ({ page }) => {
    await header(page, 'Note').locator('.ex-resize-grip').click({ force: true });
    await dragGrip(page, 'Note', 3);

    await expect(page.locator('#width-status')).toHaveText('Widths:');
    await expect(page.locator('#sort-status')).toHaveText('Sorts: Notional ascending');
});

test('Shift+click on a header selects whole columns and does not sort (SR-2a, ADR-0012)', async ({ page }) => {
    await clickCell(page, 2, 2);

    await header(page, 'Narrow').click({ modifiers: ['Shift'], position: { x: 20, y: 12 }, force: true });

    await expect(page.locator('#selection-status')).toHaveText('Selection: 0,2,120,3');
    await expect(page.locator('#sort-status')).toHaveText('Sorts: Notional ascending');
});

test('dragging the edge of one of several whole columns resizes them all (FN-12c, ADR-0016)', async ({ page }) => {
    await clickCell(page, 2, 2);
    await header(page, 'Narrow').click({ modifiers: ['Shift'], position: { x: 20, y: 12 }, force: true });
    await expect(page.locator('#selection-status')).toHaveText('Selection: 0,2,120,3');

    await dragGrip(page, 'Narrow', 40);

    await expect(page.locator('#width-status')).toHaveText('Widths: Notional=100;Amount=100;Narrow=100');
});

test('a window narrower than the pinned block suspends pinning, and widening restores it (FN-6a, UX-11b, ADR-0043)', async ({ page }) => {
    await expect(grid(page).locator('.ex-cell.ex-pinned').first()).toBeVisible();
    const bookWidth = (await header(page, 'Book').boundingBox()).width;

    await page.setViewportSize({ width: 250, height: 800 });
    await expect(grid(page).locator('.ex-cell.ex-pinned')).toHaveCount(0);
    // Nothing is stretched or squeezed: the column keeps its width in a narrow box.
    expect((await header(page, 'Book').boundingBox()).width).toBe(bookWidth);

    // An arrow into a scrollable column brings it on screen, not under a pinned block.
    await clickCell(page, 0, 0);
    for (let i = 0; i < 3; i++)
        await page.keyboard.press('ArrowRight');
    const scroller = await grid(page).locator('.ex-scroller').boundingBox();
    const focusId = await grid(page).getAttribute('aria-activedescendant');
    const focused = await page.locator(`[id='${focusId}']`).boundingBox();
    expect(focused.x).toBeGreaterThanOrEqual(scroller.x - 0.5);
    expect(focused.x + focused.width).toBeLessThanOrEqual(scroller.x + scroller.width + 0.5);

    await page.setViewportSize({ width: 1280, height: 800 });
    await expect(grid(page).locator('.ex-cell.ex-pinned').first()).toBeVisible();
    await expect(page.locator('#pinned-status')).toHaveText('Pinned: 2');
    expect((await header(page, 'Book').boundingBox()).width).toBe(bookWidth);
});
