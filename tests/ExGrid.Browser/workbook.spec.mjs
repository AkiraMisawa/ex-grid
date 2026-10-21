import { test, expect, settled } from './fixtures.mjs';
import { sheet, cell, clickCell, goTo, expectFocusAt, editor } from './sheet-helpers.mjs';
import { expectKeyboardOn } from './keyboard.mjs';

// The Sheet Tabs (ADR-0331 §4; exsheet-workbook ticket 05), on /workbook under both Chromes: one
// ExSheet holding the Sheets Claim and Summary, a tab for each under the grid. Ctrl+PageDown and
// Ctrl+PageUp show the Sheet after and before; a key typed straight after the switch, at machine
// speed, lands on the Sheet shown, at its own Selection, though on a circuit it told the paint before
// the switch (numbered after it in the Input Order, ADR-0240); each Sheet keeps where it was scrolled;
// a press on a tab leaves the keyboard in the grid.

test.use({ viewport: { width: 1280, height: 1000 } });

for (const chrome of ['builtin', 'mud']) {
    test.describe(`ADR-0331 Sheet Tabs (${chrome})`, () => {
        async function openWorkbook(page) {
            await page.goto(chrome === 'builtin' ? '/workbook' : `/workbook?chrome=${chrome}`);
            await expect(cell(sheet(page), 'A1')).toHaveText('Item');
        }

        const tab = (page, name) => page.locator('.ex-sheet-tab', { hasText: name });

        test('WB-1: Ctrl+PageDown and Ctrl+PageUp show the Sheet after and before, and stop at the last and the first', async ({ page }) => {
            await openWorkbook(page);
            const grid = sheet(page);
            await clickCell(grid, 'B2');

            await page.keyboard.press('Control+PageDown');
            await expect(cell(grid, 'A1')).toHaveText('Claimed');
            await expect(cell(grid, 'B1')).toHaveText('4561.25');
            await expect(tab(page, 'Summary')).toHaveAttribute('aria-selected', 'true');
            await expect(page.locator('#workbook-shown')).toHaveText('Summary');

            await page.keyboard.press('Control+PageDown');
            await page.keyboard.press('Control+PageUp');
            await expect(cell(grid, 'A1')).toHaveText('Item');
            await expect(tab(page, 'Claim')).toHaveAttribute('aria-selected', 'true');
            await expectFocusAt(grid, 'B2');
        });

        test('WB-2: keys typed at once after Ctrl+PageDown land on the Sheet shown, at its own Focus, and nothing on the Sheet left', async ({ page }) => {
            await openWorkbook(page);
            const grid = sheet(page);
            await clickCell(grid, 'A1');
            await page.keyboard.press('Control+PageDown');
            await expect(cell(grid, 'A1')).toHaveText('Claimed');
            await goTo(grid, 'C2');
            await page.keyboard.press('Control+PageUp');
            await expect(cell(grid, 'A1')).toHaveText('Item');
            await clickCell(grid, 'C3');
            await settled(grid);

            // At machine speed: the switch and the typing, with nothing awaited between them.
            await page.keyboard.press('Control+PageDown');
            await page.keyboard.type('77');
            await page.keyboard.press('Enter');
            await settled(grid);

            await expect(cell(grid, 'C2')).toHaveText('77');
            await expect(editor(grid)).toHaveCount(0);
            await page.keyboard.press('Control+PageUp');
            await expect(cell(grid, 'C3')).toHaveText('289.5');
        });

        test('WB-3: each Sheet keeps where it was scrolled', async ({ page }) => {
            await openWorkbook(page);
            const grid = sheet(page);
            await goTo(grid, 'A150');
            await expect(cell(grid, 'A150')).toHaveText('Row 150');

            await tab(page, 'Summary').click();
            await expect(cell(grid, 'A1')).toHaveText('Claimed');
            await expect(cell(grid, 'A1')).toBeInViewport();

            await tab(page, 'Claim').click();
            await expect(cell(grid, 'A150')).toHaveText('Row 150');
            await expect(cell(grid, 'A150')).toBeInViewport();
            await expectFocusAt(grid, 'A150');
        });

        test('WB-4: a press on a tab shows its Sheet and leaves the keyboard in the grid', async ({ page }) => {
            await openWorkbook(page);
            const grid = sheet(page);
            await clickCell(grid, 'B2');

            await tab(page, 'Summary').click();

            await expect(tab(page, 'Summary')).toHaveAttribute('aria-selected', 'true');
            await expect(cell(grid, 'A1')).toHaveText('Claimed');
            await expectKeyboardOn(grid);
            // A key typed straight after a press on a tab is not tested here: the press is not numbered
            // in the Input Order until ExSheet's own surfaces join it (input-order ticket 04), and on a
            // circuit such a key is refused as aimed at the Sheet left.
        });
    });
}
