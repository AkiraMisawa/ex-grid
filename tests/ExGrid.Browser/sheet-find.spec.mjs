import { test, expect, watchNextKey, keySeenUntouched, settled } from './fixtures.mjs';
import { sheet, cell, clickCell, expectFocusAt } from './sheet-helpers.mjs';
import { expectKeyboardOn } from './keyboard.mjs';

// Find and Replace on a Sheet with real keys (ADR-0332 §2, §4, §5; find-and-replace ticket 04), on
// /workbook under both Chromes: ExSheet draws the find panel itself, with Within — the Sheet shown, the
// default, or the Workbook. Within the Workbook, a step past the last match of the Sheet shown goes on to
// the next Sheet, shows it and selects the match there, marked as the current one; Replace All writes every
// Sheet as one undo step and says the count. The built-in panel and ExSheet.MudBlazor's must give the same
// outcomes (the spine's fourth rule).
//
// The page's Workbook: Claim (A1 Item, A2 Flights, A3 "Hotel, nights", A4 Taxis, A5 Total; D2:D5
// Formulas; A41:A200 "Row N") and Summary (A1 Claimed, B1 =Claim!D5, A2 Travel, B2 =Claim!D2+Claim!D4).

test.use({ viewport: { width: 1280, height: 1000 } });

const CURRENT = /(^|\s)ex-match-mark-current(\s|$)/;

for (const chrome of ['builtin', 'mud']) {
    test.describe(`ADR-0332 Find and Replace on a Sheet (${chrome})`, () => {
        const parts = chrome === 'mud'
            ? {
                withinSheet: 'button.mud-ex-sheet-find-within-sheet',
                withinWorkbook: 'button.mud-ex-sheet-find-within-workbook',
                replaceField: '.mud-ex-grid-find-replace-field input',
                replaceAll: 'button.mud-ex-grid-find-replace-all',
                outcome: '.mud-ex-grid-find-outcome',
            }
            : {
                withinSheet: 'input.ex-sheet-find-within-sheet',
                withinWorkbook: 'input.ex-sheet-find-within-workbook',
                replaceField: 'input.ex-find-replace-field',
                replaceAll: 'button.ex-find-replace-all',
                outcome: '.ex-find-outcome',
            };

        const tab = (page, name) => page.locator('.ex-sheet-tab', { hasText: name });
        const panel = (page) => sheet(page).locator('.ex-popover-find');
        const findField = (page) => panel(page).locator('input').first();

        async function openWorkbook(page) {
            await page.goto(chrome === 'builtin' ? '/workbook' : `/workbook?chrome=${chrome}`);
            await expect(cell(sheet(page), 'A1')).toHaveText('Item');
            await clickCell(sheet(page), 'A1');
            await expectFocusAt(sheet(page), 'A1');
        }

        /** Ctrl+F or Ctrl+H on the Sheet, taken by it and not the browser, and the text typed straight after it. */
        async function open(page, key, text) {
            await watchNextKey(page, [key, key.toUpperCase()]);
            await page.keyboard.press(`ControlOrMeta+${key}`);
            // spec-lint: early-read: the keydown listener records the key inside the press's own dispatch, so the answer is final once the press returns
            expect(await keySeenUntouched(page), `the Sheet took Ctrl+${key.toUpperCase()}`).not.toBe(true);
            await page.keyboard.type(text);
            await expect(findField(page)).toHaveValue(text);
            await expect(findField(page)).toBeFocused();
        }

        /** Whether a Within choice is the chosen one: a radio input checked, or a MudButton with aria-checked. */
        async function expectChosen(locator) {
            if (chrome === 'mud') await expect(locator).toHaveAttribute('aria-checked', 'true');
            else await expect(locator).toBeChecked();
        }

        /**
         * Within the Workbook, clicked and not waited on: the click is numbered in the Input Order (ADR-0240), so
         * the step pressed straight after it searches the Workbook however late the circuit hears it.
         */
        async function withinWorkbook(page) {
            await panel(page).locator(parts.withinWorkbook).click();
        }

        test('SF-1: within the Sheet, the default, a step searches the Sheet shown alone and says no match (ADR-0332 §5)', async ({ page }) => {
            await openWorkbook(page);
            const grid = sheet(page);

            await open(page, 'f', 'travel');
            await expectChosen(panel(page).locator(parts.withinSheet));
            await page.keyboard.press('Enter');

            await expect(panel(page).locator(parts.outcome)).toContainText('No match');
            await expect(tab(page, 'Claim')).toHaveAttribute('aria-selected', 'true');
            await expectFocusAt(grid, 'A1');
        });

        test('SF-2: within the Workbook, a step goes on to the next Sheet, shows it and selects the match there, marked as current (ADR-0332 §4, §5)', async ({ page }) => {
            await openWorkbook(page);
            const grid = sheet(page);
            await open(page, 'f', 'travel');
            await withinWorkbook(page);

            await findField(page).press('Enter');
            await expect(tab(page, 'Summary')).toHaveAttribute('aria-selected', 'true');
            await expect(page.locator('#workbook-shown')).toHaveText('Summary');
            await expectFocusAt(grid, 'A2');
            await expect(cell(grid, 'A2')).toHaveClass(CURRENT);
            await expectChosen(panel(page).locator(parts.withinWorkbook));

            // And back to Claim for a match there, past the end of Summary.
            await findField(page).fill('total');
            await findField(page).press('Enter');
            await expect(tab(page, 'Claim')).toHaveAttribute('aria-selected', 'true');
            await expectFocusAt(grid, 'A5');
            await expect(page.locator('#workbook-shown')).toHaveText('Claim');
            await expect(cell(grid, 'A5')).toHaveClass(CURRENT);
            await expect(findField(page)).toBeFocused();
        });

        test('SF-3: Replace All within the Workbook writes every Sheet, says the count, and is one undo step (ADR-0332 §2, §5)', async ({ page }) => {
            await openWorkbook(page);
            const grid = sheet(page);
            await open(page, 'h', 'el');
            await withinWorkbook(page);
            const field = panel(page).locator(parts.replaceField);
            await field.click();
            await page.keyboard.type('EL');
            await expect(field).toHaveValue('EL');

            await panel(page).locator(parts.replaceAll).click();

            await expect(panel(page).locator(parts.outcome)).toHaveText('Replaced 2 cells.');
            await expect(cell(grid, 'A3')).toHaveText('HotEL, nights');

            await page.keyboard.press('Escape');
            await expect(panel(page)).toHaveCount(0);
            await tab(page, 'Summary').click();
            await expect(cell(grid, 'A2')).toHaveText('TravEL');
            await expectKeyboardOn(grid);

            // One step undoes the write on both Sheets.
            await page.keyboard.press('ControlOrMeta+z');
            await expect(cell(grid, 'A2')).toHaveText('Travel');
            await tab(page, 'Claim').click();
            await expect(cell(grid, 'A3')).toHaveText('Hotel, nights');
            await settled(grid);
        });
    });
}
