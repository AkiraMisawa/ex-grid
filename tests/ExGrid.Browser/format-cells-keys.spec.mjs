import { test, expect, setRoundTrip } from './fixtures.mjs';
import { sheet, openSheet, cell, pressCell, expectFocusAt, pressAt } from './sheet-helpers.mjs';
import { expectKeyboardOn } from './keyboard.mjs';

// Keys typed while Format Cells opens (ticket 93; ADR-0050 item 16 and ADR-0039, notes of
// 2026-10-01). On a circuit, what a command or a key opens takes the keyboard a round trip or more
// later, and a key typed in between reached the grid: a digit opened an edit behind Format Cells,
// under both Chromes. Now the grid is told where the keyboard is going — its own popover under the
// built-in Chrome, a frame of the Chrome's own under ExSheet.MudBlazor's — and holds the keys
// typed meanwhile for it. Every row of the table the gap was shown with is run here: a key typed
// straight after reaches Format Cells' focused control, or is dropped, and never starts an edit.
// The round trip is injected on the Server host; on WebAssembly the same tests are the case
// without one. C2 holds 0.5.

// Tall enough that the Sheet, Formula Bar to scrollbar, is inside the window.
test.use({ viewport: { width: 1280, height: 1000 } });

const ROUND_TRIP_MS = 80;

/** Format Cells: the grid's popover under the built-in Chrome, a MudDialog under the other. */
function formatCellsOf(page, grid, chrome) {
    return chrome === 'mud' ? page.locator('.mud-dialog.mud-ex-sheet-format-cells') : grid.locator('.ex-popover-consumer');
}

/** Opens Format Cells over the Focus cell as the row of the table says, and returns at once. */
async function openBy(page, grid, address, how) {
    if (how === 'Ctrl+1') {
        await page.keyboard.press('Control+1');
        return;
    }
    await pressAt(cell(grid, address), { button: 'right' });
    const item = page.getByRole('menuitem', { name: 'Format Cells…' });
    if (how === 'Enter') {
        // The menu answers keys from its own place, which starts on its first item.
        await expect(page.getByRole('menuitem', { name: 'Copy' })).toBeFocused();
        for (let n = 0; n < 10 && !(await item.evaluate((e) => e === document.activeElement)); n++) {
            await page.keyboard.press('ArrowDown');
            await page.waitForTimeout(ROUND_TRIP_MS * 2);
        }
        await expect(item).toBeFocused();
        await page.keyboard.press('Enter');
        return;
    }
    await item.click();
    if (how === 'a press, then a round trip') {
        await page.waitForTimeout(100);
    }
}

for (const chrome of ['builtin', 'mud']) {
    test.describe(`under the ${chrome} Chrome, a key typed straight after Format Cells is chosen`, () => {
        test.beforeEach(async ({ page }) => {
            await openSheet(page, chrome);
            await setRoundTrip(ROUND_TRIP_MS);
        });

        test.afterEach(async () => {
            await setRoundTrip(0);
        });

        for (const how of ['a press', 'a press, then a round trip', 'Enter', 'Ctrl+1']) {
            test(`ticket 93/ADR-0050: a digit typed straight after ${how} is Format Cells' or dropped, and never starts an edit`, async ({ page }) => {
                const grid = sheet(page);
                await pressCell(grid, 'C2');
                const formatCells = formatCellsOf(page, grid, chrome);

                await openBy(page, grid, 'C2', how);
                await page.keyboard.type('5');

                await expect(formatCells).toBeVisible();
                await expect(formatCells.getByRole('tab', { name: 'Number', exact: true })).toBeFocused();
                // The key, held while the keyboard was on its way, has been handed on by now.
                await page.waitForTimeout(ROUND_TRIP_MS * 3);
                await expect(grid.locator('.ex-viewport .ex-editor')).toHaveCount(0);

                await page.keyboard.press('Escape');
                await expect(formatCells).toHaveCount(0);
                await expect(cell(grid, 'C2')).toHaveText('0.5');
                await expectKeyboardOn(grid);
                await page.keyboard.press('ArrowDown');
                await expectFocusAt(grid, 'C3');
            });
        }

        for (const how of ['Enter', 'Ctrl+1']) {
            test(`ticket 93/ADR-0039: End typed straight after ${how} reaches Format Cells' tabs, in order behind the digit`, async ({ page }) => {
                const grid = sheet(page);
                await pressCell(grid, 'C2');
                const formatCells = formatCellsOf(page, grid, chrome);

                await openBy(page, grid, 'C2', how);
                await page.keyboard.type('5');
                await page.keyboard.press('End');

                await expect(formatCells.getByRole('tab', { name: 'Fill', exact: true })).toHaveAttribute('aria-selected', 'true');
                await expect(formatCells.getByRole('tab', { name: 'Fill', exact: true })).toBeFocused();
                await expect(grid.locator('.ex-viewport .ex-editor')).toHaveCount(0);
                await page.keyboard.press('Escape');
                await expect(formatCells).toHaveCount(0);
                await expectKeyboardOn(grid);
            });
        }
    });

    test.describe(`two Sheets on /sheets under the ${chrome} Chrome`, () => {
        test.beforeEach(async ({ page }) => {
            await page.goto(chrome === 'mud' ? '/sheets?chrome=mud' : '/sheets');
            await expect(page.locator('#demo-interactive')).toBeAttached({ timeout: 30_000 });
            await expect(cell(sheet(page, 0), 'A1')).toHaveText('Left');
            await expect(cell(sheet(page, 1), 'A1')).toHaveText('Right');
            await setRoundTrip(ROUND_TRIP_MS);
        });

        test.afterEach(async () => {
            await setRoundTrip(0);
        });

        test('ticket 93/ADR-0018: the keys held for one Sheet\'s Format Cells are never the other\'s, and the other types as ever', async ({ page }) => {
            const left = sheet(page, 0);
            const right = sheet(page, 1);
            const formatCells = formatCellsOf(page, left, chrome);
            await pressCell(left, 'B2');

            await page.keyboard.press('Control+1');
            await page.keyboard.type('5');
            await expect(formatCells.getByRole('tab', { name: 'Number', exact: true })).toBeFocused();
            await page.keyboard.press('Escape');
            await expect(formatCells).toHaveCount(0);
            await expectKeyboardOn(left);

            // The right Sheet hears its own keys: nothing of the left's hold is left standing.
            await pressCell(right, 'B3');
            await page.keyboard.type('7');
            await page.keyboard.press('Enter');
            await expect(cell(right, 'B3')).toHaveText('7');
            await expect(left.locator('.ex-viewport .ex-editor')).toHaveCount(0);
        });
    });
}
