import { test, expect } from './fixtures.mjs';
import { sheet, cell, clickCell, pressCell, editor } from './sheet-helpers.mjs';

// Blazor's ExSheet and ExGrid over the Script Core in one Pointing Scope (ADR-0430), on /script-pointing, on both
// Blazor hosts: the C# Scope names the Scope the browser holds, the grid's rows cross with what .NET read of each as
// the table's row, and a refusal decided in the page reaches .NET's handler. What a press or an arrow writes is the
// Script Core's, held by its own tests; a Scope of both kinds refused is held in layer 2.

const LOOKUP_4471 = '=XLOOKUP("R-4471", Positions[Id], Positions[PV])';

test('ADR-0430: a press on a Script grid of the Scope writes the key .NET read from its row, and a refusal reaches .NET @blazor', async ({ page }) => {
    await page.goto('/script-pointing');
    const grid = sheet(page);
    const table = page.locator('#script-pointing-positions .ex-grid');

    // At machine speed: the press follows the key with no wait, as the Scope is held in the page (ADR-0430 section 5).
    await pressCell(grid, 'B2');
    await page.keyboard.type('=');
    await clickCell(table, 'C3');
    await expect(editor(grid)).toHaveValue(LOOKUP_4471);
    await page.keyboard.press('Enter');
    await expect(cell(grid, 'B2')).toHaveText('318.25');

    // The fourth position's key is blank: the press writes nothing, and the page's handler says why.
    await pressCell(grid, 'B3');
    await page.keyboard.type('=');
    await clickCell(table, 'A4');
    await expect(page.locator('#script-pointing-refused')).not.toHaveText('Nothing refused yet');
    await expect(editor(grid)).toHaveValue('=');
});
