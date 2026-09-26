import { test, expect } from './fixtures.mjs';

// ViewportSize.Fill on the height (ADR-0028, rewritten 2026-09-26), against the /fill page:
// the grid takes a definite parent's height — a sized box, the rest of a flex column under a
// toolbar, a Material paper under its toolbar (ADR-0030) — names a parent with no height
// rather than guessing, and follows a box that follows the window. Until 2026-09-26 a
// Fill-height grid settled at its header band in any box and painted no row.

const ROW = 24;

function box(page, selector) {
    return page.locator(selector).boundingBox();
}

async function rowsIn(page, container) {
    return page.locator(`${container} .ex-row`).count();
}

test.describe('in parents with a definite height', () => {
    test.beforeEach(async ({ page }) => {
        await page.setViewportSize({ width: 1280, height: 1400 });
        await page.goto('/fill');
        await expect(page.locator('#fixed-box .ex-row').first()).toBeVisible();
    });

    test('a sized box: the grid is the box, rows reach its bottom, and the DOM is the box\'s (VZ-12a, ADR-0028)', async ({ page }) => {
        const parent = await box(page, '#fixed-box');
        const grid = await box(page, '#fixed-box .ex-grid');
        expect(grid.height).toBeCloseTo(parent.height, 0);

        const scroller = await box(page, '#fixed-box .ex-scroller');
        const last = await page.locator('#fixed-box .ex-row').last().boundingBox();
        expect(last.y + last.height).toBeGreaterThanOrEqual(scroller.y + scroller.height - ROW);
        // 500 rows in the result; what is in the DOM is what 300px holds (P1).
        expect(await rowsIn(page, '#fixed-box')).toBeLessThanOrEqual(Math.ceil(300 / ROW) + 2);
    });

    test('the rest of a flex column: the grid starts under the toolbar and ends with the column (VZ-12a, ADR-0028)', async ({ page }) => {
        const column = await box(page, '#flex-box');
        const toolbar = await box(page, '#flex-toolbar');
        const grid = await box(page, '#flex-box .ex-grid');
        expect(grid.y).toBeCloseTo(toolbar.y + toolbar.height, 0);
        expect(grid.y + grid.height).toBeCloseTo(column.y + column.height, 0);
        expect(await rowsIn(page, '#flex-box')).toBeGreaterThan(5);
    });

    test('inside the paper: the Fill grid takes what the toolbar leaves; a declared grid is unchanged (WR-7a, ADR-0030)', async ({ page }) => {
        const paper = await page.locator('#fill-paper').evaluate((el) => {
            const r = el.getBoundingClientRect();
            const s = getComputedStyle(el);
            return { bottom: r.bottom - parseFloat(s.paddingBottom) - parseFloat(s.borderBottomWidth) };
        });
        const grid = await box(page, '#fill-paper .ex-grid');
        expect(grid.y + grid.height).toBeCloseTo(paper.bottom, 0);
        expect(await rowsIn(page, '#fill-paper')).toBeGreaterThan(5);

        const declared = await box(page, '#declared-paper .ex-scroller');
        expect(declared.height).toBeCloseTo(200, 0);
    });
});

test.describe('in a parent with no height', () => {
    test.use({ expectedWarnings: [/parent gives it no height/] });

    test('nothing is painted and the grid names the parent (VZ-12b, ADR-0028)', async ({ page }) => {
        await page.goto('/fill?parent=auto');
        await expect(page.locator('#auto-box .ex-grid')).toBeVisible();
        await page.waitForTimeout(500);
        expect(await rowsIn(page, '#auto-box')).toBe(0);
    });
});

test.describe('in a box that follows the window', () => {
    test.beforeEach(async ({ page }) => {
        await page.setViewportSize({ width: 1280, height: 800 });
        await page.goto('/fill?parent=window');
        await expect(page.locator('#window-box .ex-row').first()).toBeVisible();
    });

    test('the Viewport follows the window, both ways (VZ-12a, ADR-0028/0043)', async ({ page }) => {
        const tall = await rowsIn(page, '#window-box');
        await page.setViewportSize({ width: 1280, height: 500 });
        await expect.poll(async () => rowsIn(page, '#window-box')).toBeLessThan(tall);
        await page.setViewportSize({ width: 1280, height: 800 });
        await expect.poll(async () => rowsIn(page, '#window-box')).toBe(tall);
    });

    test('a column that cannot be filtered shows its commands alone, and Tab closes them as a Cancel (KB-30, ADR-0039/0044)', async ({ page }) => {
        // This grid sorts and has no filter sink: the popover is the menu alone.
        const grid = page.locator('#window-box .ex-grid');
        await grid.locator("[id$='r1c1']").click({ force: true });
        await expect(grid).toHaveAttribute('aria-activedescendant', /-r1c1$/);
        await page.keyboard.press('Alt+ArrowDown');
        await expect(grid.locator('.ex-popover [role=menu]')).toBeVisible();
        await expect(grid.locator('.ex-popover-filter')).toHaveCount(0);
        await expect.poll(() => page.evaluate(() => document.activeElement?.closest('[role=menu]') !== null)).toBe(true);

        await page.keyboard.press('ArrowDown');
        await page.keyboard.press('Tab');

        await expect(grid.locator('.ex-popover')).toHaveCount(0);
        await expect(grid).toBeFocused();
        await expect(page.locator('#sort-status')).toHaveText('Sorts: none');
    });

    test('a column menu with less than a row of room closes, and the keyboard is the grid\'s (UX-11a, ADR-0040)', async ({ page }) => {
        const grid = page.locator('#window-box .ex-grid');
        await grid.locator("[id$='r1c1']").click({ force: true });
        await expect(grid).toHaveAttribute('aria-activedescendant', /-r1c1$/);
        await page.keyboard.press('Alt+ArrowDown');
        await expect(grid.locator('.ex-popover')).toBeVisible();

        // The box is the window less 220px: at 250 it is 30px, 6px under the header.
        await page.setViewportSize({ width: 1280, height: 250 });

        await expect(grid.locator('.ex-popover')).toHaveCount(0);
        await expect(grid).toBeFocused();
        await page.setViewportSize({ width: 1280, height: 800 });
        await page.keyboard.press('ArrowDown');
        await expect(grid).toHaveAttribute('aria-activedescendant', /-r2c1$/);
    });
});
