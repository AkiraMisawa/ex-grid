import { test, expect } from './fixtures.mjs';

// Column widths and a box that narrows, against the /sizing page (ADR-0016 and ADR-0045,
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
    // On a circuit the Focus lands a round trip after the click; the next key or
    // Shift+click is measured from it.
    await expect(grid(page)).toHaveAttribute('aria-activedescendant', new RegExp(`-r${row}c${column}$`));
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
    expect(await overflows(header(page, '評価額（円）'))).toBe(false);
});

test('a Japanese header double-clicked to fit paints whole at its fitted width (FN-12d, ADR-0016)', async ({ page }) => {
    await header(page, '評価額（円）').locator('.ex-resize-grip').dblclick({ force: true });

    await expect(page.locator('#width-status')).toHaveText(/^Widths: Amount=\d/);
    const fitted = Number((await page.locator('#width-status').textContent()).split('=')[1]);
    await expect.poll(async () => (await header(page, '評価額（円）').boundingBox()).width).toBeCloseTo(fitted, 0);
    expect(await overflows(header(page, '評価額（円）'))).toBe(false);
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

// The pointer at the middle of a header's label, clear of its menu button and its grip.
async function headerCentre(page, name) {
    const box = await header(page, name).boundingBox();
    return { x: box.x + Math.min(20, box.width / 3), y: box.y + (box.height / 2) };
}

test('a press on a header released on its own column is a click, and sorts (SR-2d, SR-1, ADR-0012)', async ({ page }) => {
    const from = await headerCentre(page, 'Notional');
    await page.mouse.move(from.x, from.y);
    await page.mouse.down();
    await page.mouse.move(from.x + 6, from.y, { steps: 3 });
    await page.mouse.up();

    await expect(page.locator('#sort-status')).toHaveText('Sorts: Notional descending');
    await expect(page.locator('#selection-status')).toHaveText('Selection:');
});

test('a press that reaches another header selects whole columns, and never sorts, even released on its own (SR-2d, ADR-0012)', async ({ page }) => {
    const from = await headerCentre(page, 'Notional');
    const to = await headerCentre(page, 'Narrow');
    await page.mouse.move(from.x, from.y);
    await page.mouse.down();
    // A press selects nothing by itself.
    await page.mouse.move(from.x + 6, from.y, { steps: 3 });
    await expect(page.locator('#selection-status')).toHaveText('Selection:');

    await page.mouse.move(to.x, to.y, { steps: 8 });
    await expect(page.locator('#selection-status')).toHaveText('Selection: 0,2,120,3');
    // The Focus on the first visible row, where the Viewport already is.
    await expect(grid(page)).toHaveAttribute('aria-activedescendant', /-r0c2$/);

    await page.mouse.move(from.x, from.y, { steps: 8 });
    await page.mouse.up();

    await expect(page.locator('#selection-status')).toHaveText('Selection: 0,2,120,1');
    await page.waitForTimeout(500);
    await expect(page.locator('#sort-status')).toHaveText('Sorts: Notional ascending');
});

test('a press on one header released on another sorts nothing: the click lands on the header all the same (SR-2d, ADR-0012)', async ({ page }) => {
    const from = await headerCentre(page, 'Notional');
    const to = await headerCentre(page, 'Narrow');
    await page.mouse.move(from.x, from.y);
    await page.mouse.down();
    await page.mouse.move(to.x, to.y, { steps: 8 });
    await page.mouse.up();

    await expect(page.locator('#selection-status')).toHaveText('Selection: 0,2,120,3');
    await page.waitForTimeout(500);
    await expect(page.locator('#sort-status')).toHaveText('Sorts: Notional ascending');
});

test('a press on a header that reaches another column\'s cells selects whole columns (SR-2d, ADR-0012)', async ({ page }) => {
    const from = await headerCentre(page, 'Notional');
    const amount = await grid(page).locator("[id$='-r4c3']").boundingBox();
    await page.mouse.move(from.x, from.y);
    await page.mouse.down();
    await page.mouse.move(amount.x + (amount.width / 2), amount.y + (amount.height / 2), { steps: 8 });

    await expect(page.locator('#selection-status')).toHaveText('Selection: 0,2,120,2');
    await page.mouse.up();
    await expect(page.locator('#selection-status')).toHaveText('Selection: 0,2,120,2');
    await expect(page.locator('#sort-status')).toHaveText('Sorts: Notional ascending');
});

test('Ctrl+click on a header adds the whole column, a second takes it out, and neither sorts (SR-2e, ADR-0012)', async ({ page }) => {
    await clickCell(page, 2, 0);

    await header(page, 'Notional').click({ modifiers: ['ControlOrMeta'], position: { x: 20, y: 12 }, force: true });
    await expect(page.locator('#selection-status')).toHaveText('Selection: 2,0,1,1;0,2,120,1');
    // The Focus on the added column's first visible row.
    await expect(grid(page)).toHaveAttribute('aria-activedescendant', /-r0c2$/);

    await header(page, 'Notional').click({ modifiers: ['ControlOrMeta'], position: { x: 20, y: 12 }, force: true });
    await expect(page.locator('#selection-status')).toHaveText('Selection: 2,0,1,1');
    await expect(grid(page)).toHaveAttribute('aria-activedescendant', /-r2c0$/);
    await page.waitForTimeout(300);
    await expect(page.locator('#sort-status')).toHaveText('Sorts: Notional ascending');
});

test('Ctrl+drag across headers adds the columns crossed as one range, and does not sort (SR-2e, ADR-0012)', async ({ page }) => {
    await clickCell(page, 2, 0);
    const from = await headerCentre(page, 'Notional');
    const to = await headerCentre(page, 'Narrow');

    await page.mouse.move(from.x, from.y);
    await page.keyboard.down('ControlOrMeta');
    await page.mouse.down();
    await page.mouse.move(to.x, to.y, { steps: 8 });
    await page.mouse.up();
    await page.keyboard.up('ControlOrMeta');

    await expect(page.locator('#selection-status')).toHaveText('Selection: 2,0,1,1;0,2,120,3');
    await page.waitForTimeout(300);
    await expect(page.locator('#sort-status')).toHaveText('Sorts: Notional ascending');
});

test('Meta counts as Ctrl on a header only where Meta is Command (SR-2e, ADR-0012)', async ({ page }) => {
    await clickCell(page, 2, 0);

    await header(page, 'Notional').click({ modifiers: ['Meta'], position: { x: 20, y: 12 }, force: true });

    if (process.platform === 'darwin') {
        // Cmd+click is Ctrl+click here: the column is added and nothing sorts.
        await expect(page.locator('#selection-status')).toHaveText('Selection: 2,0,1,1;0,2,120,1');
        await expect(page.locator('#sort-status')).toHaveText('Sorts: Notional ascending');
    } else {
        // Meta is the OS's key here: the click is a plain click, and sorts.
        await expect(page.locator('#sort-status')).toHaveText('Sorts: Notional descending');
        await expect(page.locator('#selection-status')).toHaveText('Selection: 2,0,1,1');
    }
});

test('a drag on a resize grip resizes and is not a Heading drag (ticket 21, ADR-0016)', async ({ page }) => {
    await dragGrip(page, 'Note', 30);

    await expect(page.locator('#width-status')).toHaveText('Widths: Note=150');
    await expect(page.locator('#selection-status')).toHaveText('Selection:');
    await expect(page.locator('#sort-status')).toHaveText('Sorts: Notional ascending');
});

test('with the view at the top, a header Shift+click then Shift+→ never scrolls down (SR-2c, ADR-0052)', async ({ page }) => {
    const scroller = grid(page).locator('.ex-scroller');
    await clickCell(page, 0, 2);
    await header(page, 'Narrow').click({ modifiers: ['Shift'], position: { x: 20, y: 12 }, force: true });
    await expect(page.locator('#selection-status')).toHaveText('Selection: 0,2,120,3');

    await page.keyboard.press('Shift+ArrowRight');

    await expect(page.locator('#selection-status')).toHaveText('Selection: 0,2,120,4');
    // A reveal writes after the render, and on a circuit its scroll event is a round trip behind.
    await page.waitForTimeout(500);
    expect(await scroller.evaluate((el) => el.scrollTop)).toBe(0);
});

test('with the view at the top, Ctrl+Space on the first row then Shift+→ never scrolls down (SR-2c, ADR-0052)', async ({ page }) => {
    const scroller = grid(page).locator('.ex-scroller');
    await clickCell(page, 0, 2);
    await page.keyboard.press('Control+Space');
    await expect(page.locator('#selection-status')).toHaveText('Selection: 0,2,120,1');

    await page.keyboard.press('Shift+ArrowRight');

    await expect(page.locator('#selection-status')).toHaveText('Selection: 0,2,120,2');
    await page.waitForTimeout(500);
    expect(await scroller.evaluate((el) => el.scrollTop)).toBe(0);

    // Shift+↑ leaves the columns a row short of the last, and the view goes to the Extent.
    await page.keyboard.press('Shift+ArrowUp');
    await expect(page.locator('#selection-status')).toHaveText('Selection: 0,2,119,2');
    await expect(grid(page).locator("[id$='-r118c3']")).toBeVisible();
    await expect.poll(async () => scroller.evaluate((el) => el.scrollTop)).toBeGreaterThan(0);
});

test('dragging the edge of one of several whole columns resizes them all (FN-12c, ADR-0016)', async ({ page }) => {
    await clickCell(page, 2, 2);
    await header(page, 'Narrow').click({ modifiers: ['Shift'], position: { x: 20, y: 12 }, force: true });
    await expect(page.locator('#selection-status')).toHaveText('Selection: 0,2,120,3');

    await dragGrip(page, 'Narrow', 40);

    await expect(page.locator('#width-status')).toHaveText('Widths: Notional=100;Amount=100;Narrow=100');
});

async function headerWidths(page) {
    return grid(page).locator('.ex-header-cell').evaluateAll((cells) =>
        Object.fromEntries(cells.map((c) => [c.textContent, c.getBoundingClientRect().width])));
}

test('columns narrower than a wide box are not stretched, and a resize changes no column width (UX-11b, ADR-0045)', async ({ page }) => {
    const wide = await headerWidths(page);
    expect(Object.keys(wide)).toHaveLength(6);
    // Every painted width is the resolved one written inline, not a share of the box.
    for (const [name, width] of Object.entries(wide)) {
        const inline = await header(page, name).evaluate((c) => parseFloat(c.style.width));
        expect(width, name).toBeCloseTo(inline, 1);
    }
    // The columns end short of the box: the rest is empty, as an empty sheet is.
    const scroller = await grid(page).locator('.ex-scroller').boundingBox();
    const last = await header(page, 'Note').boundingBox();
    expect(last.x + last.width).toBeLessThan(scroller.x + scroller.width - 100);

    await page.setViewportSize({ width: 700, height: 800 });
    await expect.poll(async () => (await grid(page).locator('.ex-scroller').boundingBox()).width).toBeLessThan(700);
    const narrow = await headerWidths(page);
    for (const [name, width] of Object.entries(narrow))
        expect(width, name).toBeCloseTo(wide[name], 1);
});

test('a window narrower than the pinned block suspends pinning, and widening restores it (FN-6a, ADR-0045)', async ({ page }) => {
    await expect(grid(page).locator('.ex-cell.ex-pinned').first()).toBeVisible();
    const bookWidth = (await header(page, 'Book').boundingBox()).width;

    await page.setViewportSize({ width: 250, height: 800 });
    await expect(grid(page).locator('.ex-cell.ex-pinned')).toHaveCount(0);
    expect((await header(page, 'Book').boundingBox()).width).toBe(bookWidth);

    // An arrow into a scrollable column brings it on screen, not under a pinned block.
    await clickCell(page, 0, 0);
    for (let i = 0; i < 3; i++)
        await page.keyboard.press('ArrowRight');
    await expect(grid(page)).toHaveAttribute('aria-activedescendant', /-r0c3$/);
    // The reveal's scroll lands a frame after the attribute, or a round trip on a circuit. While
    // it flings, the scrollable cells are skipped and the attribute is cleared (ADR-0004,
    // ADR-0033): that is "not yet", not a cell to wait for.
    await expect.poll(async () => {
        const scroller = await grid(page).locator('.ex-scroller').boundingBox();
        const focused = await grid(page).locator("[id$='-r0c3']").boundingBox({ timeout: 100 }).catch(() => null);
        if (focused === null)
            return false;
        return focused.x >= scroller.x - 0.5 && focused.x + focused.width <= scroller.x + scroller.width + 0.5;
    }, { message: 'the Focus is whole on screen' }).toBe(true);

    await page.setViewportSize({ width: 1280, height: 800 });
    await expect(grid(page).locator('.ex-cell.ex-pinned').first()).toBeVisible();
    await expect(page.locator('#pinned-status')).toHaveText('Pinned: 2');
    expect((await header(page, 'Book').boundingBox()).width).toBe(bookWidth);
});
