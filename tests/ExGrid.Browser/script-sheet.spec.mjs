import { test, expect } from './fixtures.mjs';
import { sheet, cell, enter } from './sheet-helpers.mjs';

// Blazor's ExSheet over its Script Core (ADR-0370 section 1), on /script-sheet, on both Blazor hosts: the one path no
// other layer sees whole. The page binds its Sheet Document two ways in .NET, so the document crosses to the browser
// as the JSON both engines read, every change the user types crosses back to the page's handler, and a command and a
// document of the page's own cross to the Sheet again. What the Sheet does with a key is the Script Core's, held by its
// own specs; what crosses is held member for member by the bridge's tests in layers 1 and 2.

test('ADR-0370 section 1, ADR-0048: the Sheet Document crosses both ways between .NET and the Sheet in the browser, and .NET\'s Undo, its own document, its own edit, a save and an opening reach the Sheet', async ({ page }) => {
    await page.goto('/script-sheet');
    const grid = sheet(page);
    const document = page.locator('#script-sheet-document');
    // A3 is =SUM(A1:A2), computed in the browser from the document .NET wrote.
    await expect(cell(grid, 'A3')).toHaveText('5');
    await expect(document).toHaveText('Cells in the document: 3');

    await enter(page, grid, 'B1', '10');
    await expect(document).toHaveText('Cells in the document: 4');
    await enter(page, grid, 'A1', '7');
    await expect(cell(grid, 'A3')).toHaveText('10');

    await page.locator('#script-sheet-undo').click();
    await expect(page.locator('#script-sheet-undone')).toHaveText('Undone');
    await expect(cell(grid, 'A3')).toHaveText('5');
    await expect(cell(grid, 'B1')).toHaveText('10');

    // The document the page opened on, handed back, replaces what the user typed.
    await page.locator('#script-sheet-reset').click();
    await expect(cell(grid, 'B1')).toHaveText('');
    await expect(document).toHaveText('Cells in the document: 3');

    // An edit .NET made, as SheetWire writes it, done by the Sheet in the browser.
    await page.locator('#script-sheet-enter').click();
    await expect(cell(grid, 'A3')).toHaveText('9');
    await expect(cell(grid, 'A2')).toHaveText('7');

    // A save and an opening, each taking its turn among the Sheet's inputs and calling back into .NET (ADR-0330
    // section 10): .NET's copy of the Workbook computes what the browser shows, and the file opens over a later change.
    await page.locator('#script-sheet-save').click();
    await expect(page.locator('#script-sheet-file')).toHaveText('Saved, A3 = 9');
    await enter(page, grid, 'A1', '100');
    await expect(cell(grid, 'A3')).toHaveText('107');
    await page.locator('#script-sheet-open').click();
    await expect(page.locator('#script-sheet-file')).toHaveText('Opened');
    await expect(cell(grid, 'A3')).toHaveText('9');
});
