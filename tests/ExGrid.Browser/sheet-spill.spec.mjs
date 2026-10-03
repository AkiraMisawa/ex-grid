import { test, expect } from './fixtures.mjs';
import { sheet, cell, clickCell, bar, editor, expectFocusAt } from './sheet-helpers.mjs';

// ADR-0125: a Formula whose result is an array spills. /sheet?case=spill holds 1, 2, 3 in A1:A3 and
// =A1:A3*10 in C1, which spills down C1:C3. A spilled cell's Formula Bar shows the Anchor's Formula
// dimmed and read-only, under both Chromes; typing into a spilled cell makes the Anchor #SPILL!.

for (const chrome of ['builtin', 'mud']) {
    test.describe(`${chrome} Chrome`, () => {
        const url = chrome === 'builtin' ? '/sheet?case=spill' : '/sheet?case=spill&chrome=mud';

        test(`ADR-0125: a spill paints, and a spilled cell's bar shows the Anchor's Formula dimmed (${chrome})`, async ({ page }) => {
            await page.goto(url);
            const grid = sheet(page);
            await expect(cell(grid, 'C3')).toHaveText('30');
            await expect(cell(grid, 'C2')).toHaveText('20');

            await clickCell(grid, 'C1');
            await expectFocusAt(grid, 'C1');
            await expect(bar(grid)).toHaveValue('=A1:A3*10');
            const own = await bar(grid).evaluate(e => getComputedStyle(e).color);

            await clickCell(grid, 'C2');
            await expectFocusAt(grid, 'C2');
            await expect(bar(grid)).toHaveValue('=A1:A3*10');
            await expect(bar(grid)).toHaveAttribute('readonly', '');
            await expect.poll(() => bar(grid).evaluate(e => getComputedStyle(e).color)).not.toBe(own);
        });

        test(`ADR-0125: typing into a spilled cell makes the Anchor #SPILL!, and Delete lets it spill again (${chrome})`, async ({ page }) => {
            await page.goto(url);
            const grid = sheet(page);
            await expect(cell(grid, 'C2')).toHaveText('20');

            await clickCell(grid, 'C2');
            await expectFocusAt(grid, 'C2');
            await page.keyboard.type('7');
            await expect(editor(grid)).toHaveValue('7');
            await page.keyboard.press('Enter');
            await expect(cell(grid, 'C1')).toHaveText('#SPILL!');
            await expect(cell(grid, 'C2')).toHaveText('7');
            await expect(cell(grid, 'C3')).toHaveText('');

            await clickCell(grid, 'C2');
            await expectFocusAt(grid, 'C2');
            await page.keyboard.press('Delete');
            await expect(cell(grid, 'C1')).toHaveText('10');
            await expect(cell(grid, 'C3')).toHaveText('30');
        });
    });
}
