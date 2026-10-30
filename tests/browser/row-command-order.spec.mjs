import { test, expect, settled, circuitQuiet } from './fixtures.mjs';

for (const chrome of ['builtin', 'mud']) {
    test(`ADR-0240/0160: ${chrome} row commands wait, refuse departed rows and continue after refusal @blazor`, async ({ page }) => {
        await page.goto(`/row-command-order?chrome=${chrome}`);
        const grid = page.locator('.ex-grid');
        await settled(grid);
        await grid.focus();
        await page.keyboard.press('Control+b');
        await grid.locator('.ex-row .ex-action').nth(0).click();
        await grid.locator('.ex-row .ex-mark').nth(1).click();
        // The Consumer remains unfinished while both clicks and the Window updates arrive.
        await page.locator('#drop-row-order').click();
        await page.locator('#restore-row-order').click();
        await circuitQuiet();
        await expect(page.locator('#row-order-trace')).toHaveText('started');
        await page.locator('#release-row-order').click();
        await settled(grid);
        await expect(page.locator('#row-order-trace')).toHaveText('started | finished | action:RowLeftTheWindow | mark:RowLeftTheWindow');
        await expect(grid.locator('.ex-announce')).toHaveText('The row is no longer available. Repeat the command on the current data.');
        await grid.locator('.ex-row .ex-action').nth(0).click();
        await grid.locator('.ex-row .ex-mark').nth(1).click();
        await settled(grid);
        await expect(page.locator('#row-order-trace')).toHaveText('started | finished | action:RowLeftTheWindow | mark:RowLeftTheWindow | action:0 | mark:1');
    });
    test(`ADR-0240: ${chrome} row commands follow keys held behind a Consumer @blazor`, async ({ page }) => {
        await page.goto(`/row-command-order?chrome=${chrome}`);
        const grid = page.locator('.ex-grid');
        await settled(grid);
        await grid.focus();
        await page.keyboard.press('Control+b');
        await page.keyboard.press('Control+i');
        await grid.locator('.ex-row .ex-action').nth(0).click();
        await grid.locator('.ex-row .ex-mark').nth(1).click();
        await page.locator('#release-row-order').click();
        await settled(grid);
        await expect(page.locator('#row-order-trace')).toHaveText('started | finished | second | action:0 | mark:1');
    });

}
