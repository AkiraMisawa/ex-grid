import { test, expect } from './fixtures.mjs';

// Row Marks (ADR-0043) on /marks: 1,000,000 rows through GridSource.Fetch with a mark
// adapter, 28px rows in a 600px Viewport. Columns: Mark 0, Index 1, Desk 2, Amount 3.
// Desk is Alpha for every fifth row, so "Alpha only" leaves 200,000 of them. What is
// asserted is what the user reads: which checkboxes are ticked and what the count says.

const ROWS = 1_000_000;
const ROW_HEIGHT = 28;

function cell(page, row, column) {
    return page.locator(`.ex-grid [id$='-r${row}c${column}']`);
}

function rowBoxes(page) {
    return page.locator('.ex-grid .ex-row .ex-mark');
}

function headerBox(page) {
    return page.locator('.ex-grid .ex-header .ex-mark');
}

function markCount(page) {
    return page.locator('.ex-grid .ex-status .ex-mark-count');
}

async function open(page) {
    await page.goto('/marks');
    await expect(page.locator('#demo-interactive')).toBeAttached();
    await expect(cell(page, 0, 1)).toHaveText('0', { timeout: 15_000 });
    await expect(page.locator('.ex-grid')).not.toHaveClass(/ex-loading/);
}

async function expectEveryPaintedRowTicked(page, ticked) {
    const boxes = rowBoxes(page);
    await expect(boxes.first()).toBeVisible();
    const states = await boxes.evaluateAll((all) => all.map((box) => box.getAttribute('aria-checked')));
    expect(states.length).toBeGreaterThan(10);
    expect(new Set(states)).toEqual(new Set([ticked ? 'true' : 'false']));
}

test('after "mark all", rows scrolled to far away paint ticked (MK-6)', async ({ page }) => {
    await open(page);

    await headerBox(page).click();
    await expect(markCount(page)).toHaveText(`${ROWS} marked`, { timeout: 15_000 });
    await expect(headerBox(page)).toHaveAttribute('aria-checked', 'true');

    const far = 700_000;
    await page.locator('.ex-grid .ex-scroller').evaluate((scroller, top) => { scroller.scrollTop = top; }, far * ROW_HEIGHT);
    await expect(cell(page, far, 1)).toHaveText(String(far), { timeout: 15_000 });
    await expect(page.locator('.ex-grid .ex-placeholder')).toHaveCount(0, { timeout: 15_000 });

    await expectEveryPaintedRowTicked(page, true);
});

test('a filter keeps the marks and names the ones outside it (MK-7)', async ({ page }) => {
    await open(page);
    await headerBox(page).click();
    await expect(markCount(page)).toHaveText(`${ROWS} marked`, { timeout: 15_000 });

    await page.locator('#filter-alpha').click();
    await expect(markCount(page)).toHaveText(`${ROWS} marked (800000 outside the current filter)`, { timeout: 15_000 });
    await expect(cell(page, 0, 2)).toHaveText('Alpha', { timeout: 15_000 });
    await expectEveryPaintedRowTicked(page, true);

    await page.locator('#filter-none').click();
    await expect(markCount(page)).toHaveText(`${ROWS} marked`, { timeout: 15_000 });
    await expect(cell(page, 1, 2)).toHaveText('Bravo', { timeout: 15_000 });
    await expectEveryPaintedRowTicked(page, true);
});

test('Space in the Mark Column brings the selected rows into line (MK-1)', async ({ page }) => {
    await open(page);
    await rowBoxes(page).nth(1).click();
    await expect(markCount(page)).toHaveText('1 marked', { timeout: 15_000 });

    // Select rows 0-3 of the Mark Column: a mixed block, so Space marks all of it.
    await cell(page, 0, 0).click({ position: { x: 4, y: 4 }, force: true });
    await page.keyboard.press('Shift+ArrowDown');
    await page.keyboard.press('Shift+ArrowDown');
    await page.keyboard.press('Shift+ArrowDown');
    await page.keyboard.press(' ');

    await expect(markCount(page)).toHaveText('4 marked', { timeout: 15_000 });
    for (let row = 0; row < 4; row++) {
        await expect(rowBoxes(page).nth(row)).toHaveAttribute('aria-checked', 'true');
    }
    await expect(rowBoxes(page).nth(4)).toHaveAttribute('aria-checked', 'false');

    // All marked now: Space again unmarks the block.
    await page.keyboard.press(' ');
    await expect(markCount(page)).toHaveCount(0, { timeout: 15_000 });
});

test('a row arriving after "mark all" is not marked, and the header turns to "some" (MK-8)', async ({ page }) => {
    await open(page);
    await headerBox(page).click();
    await expect(headerBox(page)).toHaveAttribute('aria-checked', 'true', { timeout: 15_000 });

    await page.locator('#add-row').click();

    await expect(headerBox(page)).toHaveAttribute('aria-checked', 'mixed', { timeout: 15_000 });
    await expect(markCount(page)).toHaveText(`${ROWS} marked`);
});

test('an action over a mark whose row was deleted elsewhere reports it as gone (ADR-0043)', async ({ page }) => {
    await open(page);
    await rowBoxes(page).nth(3).click();
    await expect(markCount(page)).toHaveText('1 marked', { timeout: 15_000 });

    await page.locator('#delete-row').click();
    await page.locator('#approve').click();

    await expect(page.locator('#approve-result')).toHaveText('Approved 0 of 1 marked rows; 1 no longer exist.');
});
