import { test, expect } from '@playwright/test';

// The structural invariants that produce the timing (Definition of Done §1): DOM size
// as a function of the Viewport and never of TotalCount, the far corner reachable at
// 100,000 rows, and the pinned block painted through it all. Console errors fail the
// run, exactly as in features.spec.mjs.

let consoleErrors;
let pageErrors;

test.beforeEach(({ page }) => {
    consoleErrors = [];
    pageErrors = [];
    page.on('console', (message) => {
        if (message.type() === 'error') {
            consoleErrors.push(message.text());
        }
    });
    page.on('pageerror', (error) => pageErrors.push(String(error)));
});

test.afterEach(() => {
    expect(consoleErrors, 'zero console errors across the run (CON-1)').toEqual([]);
    expect(pageErrors, 'zero uncaught page errors (CON-2)').toEqual([]);
});

async function elementCount(page) {
    return page.evaluate(() => document.querySelector('.ex-grid').querySelectorAll('*').length);
}

test('the far corner is reachable and painted at 100,000 rows (BIG-1-shaped, on /wide)', async ({ page }) => {
    await page.goto('/wide');
    const grid = page.locator('.ex-grid');
    await expect(grid.locator('.ex-row').first()).toBeVisible();

    await grid.locator('.ex-cell').first().click({ force: true });
    await page.keyboard.press('ControlOrMeta+End');

    // The last row lands and paints real cells; the pinned columns are present.
    await expect(grid.locator("[id$='r99999c99']")).toBeVisible({ timeout: 15000 });
    await expect(grid.locator('.ex-cell.ex-pinned').first()).toBeVisible();

    await page.keyboard.press('ControlOrMeta+Home');
    await expect(grid.locator("[id$='r0c0']")).toBeVisible({ timeout: 15000 });
});

test('scrolling changes which rows exist, not how many elements do (VZ-1-shaped)', async ({ page }) => {
    await page.goto('/wide');
    const grid = page.locator('.ex-grid');
    await expect(grid.locator('.ex-row').first()).toBeVisible();

    const atTop = await elementCount(page);
    await page.evaluate(() => { document.querySelector('.ex-scroller').scrollTop = 500000; });
    await page.waitForTimeout(400); // past the settle delay: real cells are back
    const midway = await elementCount(page);

    // The counts sit within a whisker of each other — the straddling row can differ.
    expect(Math.abs(midway - atTop)).toBeLessThanOrEqual(300);
    // And nothing per-frame accumulated: scroll again and return.
    await page.evaluate(() => { document.querySelector('.ex-scroller').scrollTop = 0; });
    await page.waitForTimeout(400);
    expect(Math.abs(await elementCount(page) - atTop)).toBeLessThanOrEqual(300);
});

test('the selection overlay is rectangles, not per-cell paint (SL-6/DOM-2-shaped)', async ({ page }) => {
    await page.goto('/wide');
    const grid = page.locator('.ex-grid');
    await expect(grid.locator('.ex-row').first()).toBeVisible();
    await grid.locator('.ex-cell').first().click({ force: true });
    const before = await elementCount(page);

    await page.keyboard.press('ControlOrMeta+A');
    await expect(page.locator('#selection-status')).toContainText('10000000');

    const after = await elementCount(page);
    // Ctrl+A over 10^7 cells adds the overlay rectangles and nothing else.
    expect(after - before).toBeLessThanOrEqual(6);
});
