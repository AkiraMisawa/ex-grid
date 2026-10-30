import { test, expect, setRoundTrip, alterPage, circuitQuiet, chromeHost } from './fixtures.mjs';
import { sheet, openSheet, cell, pressCell, expectFocusAt, pressAt } from './sheet-helpers.mjs';
import { expectKeyboardOn } from './keyboard.mjs';

// Keys typed while Format Cells opens (ticket 93; ADR-0050 item 16 and ADR-0039, notes of
// 2026-10-01 and 2026-10-13). On a circuit, what a command or a key opens takes the keyboard a round
// trip or more later, and a key typed in between reached the grid: a digit opened an edit behind
// Format Cells, under both Chromes. Now the grid is told where the keyboard is going — its own
// popover under the built-in Chrome, a frame of the Chrome's own under ExSheet.MudBlazor's — and
// holds the keys typed meanwhile for it. Every row of the table the gap was shown with is run here:
// a key typed straight after reaches Format Cells' focused control, and never starts an edit. Under
// the built-in Chrome a digit typed once the menu had gone was lost, on nothing until Format Cells
// took the keyboard (ADR-0039, 2026-10-13); the root now has it in between. The round trip is
// injected on the Server host; on WebAssembly the same tests are the case without one. C2 holds 0.5.

// Tall enough that the Sheet, Formula Bar to scrollbar, is inside the window.
test.use({ viewport: { width: 1280, height: 1000 } });

const ROUND_TRIP_MS = 80;

/** Format Cells: the grid's popover under either Chrome, over the Script Core (ADR-0410). */
function formatCellsOf(grid) {
    return grid.locator('.ex-popover-consumer');
}

// What a key reaching Format Cells lands on: the grid's popover, which under the Wrapper's Chrome holds the tabs
// the Wrapper's Razor drew (ADR-0410).
const REACHING_FORMAT_CELLS = '.ex-popover-consumer';

/**
 * Notes, from here on, every key that reaches Format Cells: typed on what holds the keyboard there,
 * or handed to it by the grid's hold (ADR-0039, ADR-0010). A key held on the Sheet's root is not
 * noted until it is handed on. Read with keysReachingFormatCells.
 */
async function noteKeysReachingFormatCells(page) {
    await alterPage(page, (selector) => {
        window.__keysReachingFormatCells = [];
        const listener = (e) => {
            if (e.target instanceof Element && e.target.closest(selector)) {
                window.__keysReachingFormatCells.push(e.key);
            }
        };
        // Count after the root's capture-phase gate. A key aimed at the tabs while a hold
        // still stands is stopped there and replayed later; document capture would count
        // both the intercepted event and its delivery, though the tabs receive only one.
        document.addEventListener('keydown', listener);
        return () => {
            document.removeEventListener('keydown', listener);
            delete window.__keysReachingFormatCells;
        };
    }, REACHING_FORMAT_CELLS);
}

const keysReachingFormatCells = (page) => page.evaluate(() => window.__keysReachingFormatCells);

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
        // spec-lint: early-read: the menu's answer was awaited just above (its first item focused, an expectation the lint cuts at its object literal), and each later turn after the one that ends the loop's body
        for (let n = 0; n < 10 && !(await item.evaluate((e) => e === document.activeElement)); n++) {
            const items = page.getByRole('menuitem');
            // spec-lint: early-read: as the loop's condition: the menu's answer, or the last ArrowDown's, has been awaited before this turn began
            const from = await items.evaluateAll((items) => items.findIndex((e) => e === document.activeElement));
            expect(from).toBeGreaterThanOrEqual(0);
            await page.keyboard.press('ArrowDown');
            await expect(items.nth(from)).not.toBeFocused();
        }
        await expect(item).toBeFocused();
        await page.keyboard.press('Enter');
        return;
    }
    await item.click();
    if (how === 'a press, then the menu gone') {
        // The render that removes the menu has landed, looked for frame by frame: Format Cells takes
        // the keyboard a round trip or more after it, so the key is typed in between.
        await page.waitForFunction(() => document.querySelector('.ex-grid .ex-popover[role=menu]') === null, null, { polling: 'raf' });
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

        for (const how of ['a press', 'a press, then the menu gone', 'Enter', 'Ctrl+1']) {
            test(`ticket 93/ADR-0050/ADR-0039: a digit typed straight after ${how} reaches Format Cells, and never starts an edit ${chromeHost(chrome)}`, async ({ page }) => {
                const grid = sheet(page);
                await pressCell(grid, 'C2');
                const formatCells = formatCellsOf(grid);
                await noteKeysReachingFormatCells(page);

                await openBy(page, grid, 'C2', how);
                await page.keyboard.type('5');

                await expect(formatCells).toBeVisible();
                await expect(formatCells.getByRole('tab', { name: 'Number', exact: true })).toBeFocused();
                // The key, held while the keyboard was on its way, is handed on to Format Cells once
                // it holds the keyboard — or reaches it there, typed after — once, and nowhere else.
                await expect.poll(() => keysReachingFormatCells(page)).toEqual(['5']);
                await circuitQuiet();
                expect(await keysReachingFormatCells(page)).toEqual(['5']);
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
            test(`ticket 93/ADR-0039: End typed straight after ${how} reaches Format Cells' tabs, in order behind the digit ${chromeHost(chrome)}`, async ({ page }) => {
                const grid = sheet(page);
                await pressCell(grid, 'C2');
                const formatCells = formatCellsOf(grid);

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

        test(`ticket 93/ADR-0018: the keys held for one Sheet's Format Cells are never the other's, and the other types as ever ${chromeHost(chrome)}`, async ({ page }) => {
            const left = sheet(page, 0);
            const right = sheet(page, 1);
            const formatCells = formatCellsOf(left);
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
