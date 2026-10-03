import { test, expect } from './fixtures.mjs';
import { sheet, openSheet, cell, clickCell, editor, candidates, enter, bar } from './sheet-helpers.mjs';

test.use({ viewport: { width: 1280, height: 1000 } });

for (const chrome of ['builtin', 'mud']) {
    test.describe(`observed functions under ${chrome}`, () => {
        test.beforeEach(async ({ page }) => openSheet(page, chrome));

        test('ADR-0047/0051: observed function completion commits and recalculates the extended SUMIF range', async ({ page }) => {
            const grid = sheet(page);
            await enter(page, grid, 'E2', '1');
            await enter(page, grid, 'E3', '2');
            await enter(page, grid, 'F2', '10');
            await enter(page, grid, 'F3', '20');
            await clickCell(grid, 'G2');
            await page.keyboard.type('=SUMI');
            await expect(candidates(grid)).toHaveText(['SUMIF', 'SUMIFS']);
            await page.keyboard.press('Tab');
            await expect(editor(grid)).toHaveValue('=SUMIF(');
            await expect(grid.locator('.ex-completion')).toContainText('criteria');
            await page.keyboard.type('E2:E3,2,F2)');
            await page.keyboard.press('Enter');
            await expect(cell(grid, 'G2')).toHaveText('20');
            await enter(page, grid, 'F3', '40');
            await expect(cell(grid, 'G2')).toHaveText('40');
            await clickCell(grid, 'G2');
            await expect(bar(grid)).toHaveValue('=SUMIF(E2:E3,2,F2)');
            await clickCell(grid, 'G3');
            await page.keyboard.type('=SUMIFS(F2:F3,E2:E3,2,');
            await expect(grid.locator('.ex-completion strong')).toHaveText('criteria_range1');
            await page.keyboard.type('E2:E3,');
            await expect(grid.locator('.ex-completion strong')).toHaveText('criteria1');
            await page.keyboard.type('\">0\")');
            await page.keyboard.press('Enter');
            await expect(cell(grid, 'G3')).toHaveText('40');
        });

        test('ADR-0047: lookup, rounding, calendar and text functions show their Values through the Sheet', async ({ page }) => {
            const grid = sheet(page);
            await enter(page, grid, 'E2', '1');
            await enter(page, grid, 'E3', '2');
            await enter(page, grid, 'F2', '10');
            await enter(page, grid, 'F3', '20');
            for (const [address, formula, expected] of [
                ['G2', '=VLOOKUP(2,E2:F3,2,FALSE)', '20'],
                ['G3', '=MROUND(10,3)', '9'],
                ['G4', '=CEILING.MATH(-2.5,1)', '-2'],
                ['G5', '=DATEDIF(DATE(2020,2,29),DATE(2021,2,28),"M")', '11'],
                ['G6', '=UPPER("abc")', 'ABC'],
                ['G7', '=SEARCH("B","abc")', '2'],
            ]) {
                await enter(page, grid, address, formula);
                await expect(cell(grid, address)).toHaveText(expected);
            }
        });

        test('ADR-0058: the lookup mode list writes the chosen value with Tab', async ({ page }) => {
            const grid = sheet(page);
            await enter(page, grid, 'E2', '1');
            await enter(page, grid, 'E3', '2');
            await enter(page, grid, 'F2', '10');
            await enter(page, grid, 'F3', '20');
            await clickCell(grid, 'G2');
            await page.keyboard.type('=VLOOKUP(2,E2:F3,2,');
            await expect(candidates(grid)).toHaveText(['TRUE - Approximate match', 'FALSE - Exact match']);
            await page.keyboard.press('ArrowDown');
            await page.keyboard.press('Tab');
            await expect(editor(grid)).toHaveValue('=VLOOKUP(2,E2:F3,2,FALSE');
            await expect(candidates(grid)).toHaveCount(0);
            await page.keyboard.type(')');
            await page.keyboard.press('Enter');
            await expect(cell(grid, 'G2')).toHaveText('20');
        });
    });
}
