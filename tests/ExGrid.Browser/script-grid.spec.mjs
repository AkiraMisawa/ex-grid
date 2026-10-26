import { test, expect, circuitQuiet, settled } from './fixtures.mjs';

// Blazor's ExGrid over the Script Core (ADR-0350 section 7), on /script-grid, on both Blazor hosts: the one
// path no other layer sees whole. The grid decides in the browser and the rows stay in .NET, so an edit has
// to cross to the Consumer's handler and come back as the row its source then holds, and a sort has to reach
// the C# source and come back as its order. The bridge's own logic is layer 2's (src/npm/blazor/test), and a
// value's text on the wire is the shared cases' (tests/cases/blazor).

test.beforeEach(async ({ page }) => {
    await page.goto('/script-grid');
    await expect(grid(page).locator('.ex-row').first()).toBeVisible();
});

function grid(page) {
    return page.locator('.ex-grid').first();
}

const cell = (page, row, column) => grid(page).locator(`[id$='r${row}c${column}']`);

test('ADR-0350 section 7: an edit reaches the Consumer in .NET, and the cell paints the row its source then holds', async ({ page }) => {
    await expect(cell(page, 0, 0)).toHaveText('Credit-001');
    await cell(page, 0, 0).click({ force: true });
    await page.keyboard.type('Zeta');
    await page.keyboard.press('Enter');

    await expect(page.locator('#script-edit')).toHaveText('Edit: Book of Credit-001 set to Zeta');
    await settled(grid(page));
    await circuitQuiet();
    await expect(cell(page, 0, 0)).toHaveText('Zeta');
    await expect(page.locator('#script-selection')).toHaveText('Selection: 1 range(s), Focus at row 1, column 0');
});

test('ADR-0350 section 7, ADR-0001: a header click sorts through the source in .NET, and the rows come back in its order', async ({ page }) => {
    const notional = grid(page).locator('.ex-header-cell').nth(2);
    await notional.click({ position: { x: 30, y: 14 }, force: true });

    await expect(notional).toHaveAttribute('aria-sort', 'ascending');
    await settled(grid(page));
    await circuitQuiet();
    const shown = await Promise.all([0, 1, 2].map(row => cell(page, row, 2).textContent()));
    expect(shown).toEqual(['-496956', '-494971', '-494956']);
});
