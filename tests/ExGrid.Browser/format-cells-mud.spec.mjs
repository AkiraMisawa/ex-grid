import { test, expect } from './fixtures.mjs';
import { sheet, openSheet, cell, pressCell, expectFocusAt } from './sheet-helpers.mjs';
import { expectKeyboardOn } from './keyboard.mjs';

// Format Cells under ExSheet.MudBlazor's Chrome (ADR-0071, ticket 53; SH-45, SH-47). The Wrapper's tabs stand in the
// grid's popover frame (ADR-0410), whose keys are the Script Core's and are judged by format-cells.spec.mjs; this file
// judges what the Wrapper adds there, its Razor read by the adapter's script. What it offers and what OK sets are
// ExSheet's, as under the built-in Chrome (format-cells.spec.mjs). The Number Format is what is set and read back on
// the page, because it is what the Sheet paints on this branch: C2 holds 0.5.

// Tall enough that the Sheet, Formula Bar to scrollbar, is inside the window.
test.use({ viewport: { width: 1280, height: 1000 } });

test.describe('over the Script Core, on /sheet?chrome=mud', () => {
    test.beforeEach(async ({ page }) => {
        await openSheet(page, 'mud');
    });

    test('SH-45/ADR-0410: the Wrapper\'s tabs stand in the grid\'s frame, a choice pressed and OK at once set it as one undo step, and the keyboard is the Sheet\'s again', async ({ page }) => {
        const grid = sheet(page);
        const frame = grid.locator('.ex-popover-consumer .mud-ex-sheet-script-format-cells');
        await pressCell(grid, 'C2');
        await page.locator('#sheet-format-cells').click();
        await expect(frame).toBeVisible();
        await expect(page.locator('.mud-dialog')).toHaveCount(0);
        await expect(frame.getByRole('tab', { name: 'Number', exact: true })).toHaveAttribute('aria-selected', 'true');

        // OK pressed straight after the choice: the adapter's script read the radio as it fired, not a render later.
        await frame.getByLabel('Percentage', { exact: true }).click();
        await frame.getByRole('button', { name: 'OK', exact: true }).click();

        await expect(frame).toHaveCount(0);
        await expect(cell(grid, 'C2')).toHaveText('50.00%');
        await expectKeyboardOn(grid);
        await page.keyboard.press('ArrowDown');
        await expectFocusAt(grid, 'C3');
        await page.keyboard.press('ControlOrMeta+Z');
        await expect(cell(grid, 'C2')).toHaveText('0.5');
    });
});
