import { test, expect } from './fixtures.mjs';
import { cell, clickCell, pressCell, editor, expectCovers } from './sheet-helpers.mjs';
import { expectActiveDescendant } from './keyboard.mjs';

// Blazor's ExGrid over the Script Core given the Consumer's own script for Formula entry, and Blazor's ExSheet over it
// telling its linked columns' colours, on /script-formula-entry, on both Blazor hosts (ADR-0431). That the four
// functions load before the grid is mounted, are named in its options and are refused when given later, and that the
// colours cross as places, is layer 2's; what each function's answer does to a key is the bridge's and the Script
// Core's. Here, that the page's module is imported and asked as each key is decided, with keys typed at once, so that
// nothing a key means waits on .NET on Blazor Server; and that a Sheet in no Pointing Scope outlines a grid the page
// wired to it.

test('ADR-0431: a grid given the page\'s script points, cycles with F4 and outlines each Reference, with the keys typed at once', async ({ page }) => {
    await page.goto('/script-formula-entry');
    const grid = page.locator('#script-formula-entry-lines .ex-grid');
    // The grid has no names of its own for cells, so its Name Box stays empty: the Focus is read where it is carried.
    await clickCell(grid, 'D2');
    await expectActiveDescendant(grid, /-r1c3$/);

    // At machine speed: every answer is the script's in the browser, so no key waits for the one before it to cross.
    await page.keyboard.type('=');
    await page.keyboard.press('ArrowLeft');
    await page.keyboard.press('F4');
    await page.keyboard.type('+');
    await page.keyboard.press('ArrowUp');

    await expect(editor(grid)).toHaveValue('=$C$2+D1');
    // The References the script found, each outlined over the cell it names, in the order of the palette.
    await expectCovers(grid.locator('.ex-reference-outline.ex-reference-1'), grid, 'C2', 'C2');
    await expectCovers(grid.locator('.ex-reference-outline.ex-reference-2'), grid, 'D1', 'D1');

    await page.keyboard.press('Enter');

    await expect(cell(grid, 'D2')).toHaveText('=$C$2+D1');
    await expect(grid.locator('.ex-reference-outline')).toHaveCount(0);
});

test('ADR-0431: a Sheet in no Pointing Scope tells its linked columns\' colours, and the grid the page hands them outlines the column', async ({ page }) => {
    await page.goto('/script-formula-entry');
    const sheet = page.locator('#script-formula-entry-sheet .ex-grid');
    const table = page.locator('#script-formula-entry-positions .ex-grid');
    await pressCell(sheet, 'B1');

    await page.keyboard.type('=SUM(Positions[PV])');

    await expect(editor(sheet)).toHaveValue('=SUM(Positions[PV])');
    await expect(table.locator('.ex-reference-outline')).toHaveCount(1);
    await expectCovers(table.locator('.ex-reference-outline.ex-reference-1'), table, 'C1', 'C3');

    await page.keyboard.press('Escape');

    await expect(editor(sheet)).toHaveCount(0);
    await expect(table.locator('.ex-reference-outline')).toHaveCount(0);
});
