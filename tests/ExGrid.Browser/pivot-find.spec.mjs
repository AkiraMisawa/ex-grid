import { test, expect, watchNextKey, keySeenUntouched } from './fixtures.mjs';
import { activeDescendant } from './keyboard.mjs';

// Find in ExPivot with real keys (ADR-0332 §1 and §4), on /pivot under ExPivot's own markup and under
// ExPivot.MudBlazor's Chrome, which must give the same outcomes (ADR-0061). Ctrl+F on the report opens
// the report grid's own panel — never the browser's find bar — and the keys typed straight after it
// land in its field; Enter steps through the cells the report lays out, by the text shown; a
// collapsed group is not searched inside and nothing expands; and the painted matches are marked,
// the current one apart.
//
// The page's first layout: Region and Desk on Rows in the Compact form, Product on Columns, Sum of P&L.
// Its rows: 0 Americas, 1 Credit, 2 FX, 3 Rates, 4 APAC, 5 Equities, 6 FX, 7 Rates, 8 EMEA, 9 Credit,
// 10 Equities, 11 FX, 12 Rates, 13 Grand Total. With APAC collapsed: 4 APAC, 5 EMEA, 6 Credit,
// 7 Equities, 8 FX, 9 Rates, 10 Grand Total.

test.use({ viewport: { width: 1400, height: 1100 } });

const pivot = (page) => page.locator('.ex-pivot');
const report = (page) => pivot(page).locator('.ex-pivot-sheet > .ex-grid');
const rows = (page) => report(page).locator('.ex-viewport .ex-row');
const cell = (page, row, column) => report(page).locator(`[id$='-r${row}c${column}']`);
const findField = (page) => report(page).locator('.ex-popover-find input').first();
const MARK = /(^|\s)ex-match-mark(\s|$)/;

async function open(page, chrome) {
    await page.goto(`/pivot?chrome=${chrome}`);
    await expect(rows(page).first()).toBeVisible({ timeout: 30_000 });
    await expect.poll(() => pivot(page).evaluate((p) => getComputedStyle(p).display)).toBe('flex');
    await expect(cell(page, 13, 0)).toContainText('Grand Total');
    // The report takes the keyboard: a value cell pressed, as a user does. The viewport takes the
    // pointer and finds the cell under it, so the press is forced.
    await cell(page, 1, 1).click({ force: true });
    await expect(report(page)).toHaveAttribute('aria-activedescendant', /-r1c1$/);
}

for (const chrome of ['builtin', 'mud']) {
    test.describe(`under the ${chrome} Chrome`, () => {
        test(`ADR-0332 §1: Ctrl+F on the report opens its find panel, the keys typed after it land in its field, and Enter steps through the labels as shown, wrapping (${chrome})`, async ({ page }) => {
            await open(page, chrome);

            await watchNextKey(page, ['f', 'F']);
            await page.keyboard.press('ControlOrMeta+f');
            await page.keyboard.type('equities');
            // spec-lint: early-read: the keydown listener records the key inside the press's own dispatch, so the answer is final once the press returns
            expect(await keySeenUntouched(page), 'the report grid took Ctrl+F').not.toBe(true);

            await expect(findField(page)).toBeFocused();
            await expect(findField(page)).toHaveValue('equities');

            for (const row of [5, 10, 5]) {
                await page.keyboard.press('Enter');
                await expect.poll(() => activeDescendant(report(page))).toMatch(new RegExp(`-r${row}c0$`));
            }
            await page.keyboard.press('Shift+Enter');
            await expect.poll(() => activeDescendant(report(page))).toMatch(/-r10c0$/);
            await expect(findField(page)).toBeFocused();
        });

        test(`ADR-0332 §4: the report's painted matches are marked as they are typed, and the current one apart (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            await page.keyboard.press('ControlOrMeta+f');
            await page.keyboard.type('equities');
            await expect(findField(page)).toHaveValue('equities');

            await expect(cell(page, 5, 0)).toHaveClass(MARK);
            await expect(cell(page, 10, 0)).toHaveClass(MARK);
            await expect(report(page).locator('.ex-match-mark')).toHaveCount(2);
            await expect(report(page).locator('.ex-match-mark-current')).toHaveCount(0);

            await page.keyboard.press('Enter');
            await expect.poll(() => activeDescendant(report(page))).toMatch(/-r5c0$/);
            await expect(cell(page, 5, 0)).toHaveClass(/ex-match-mark-current/);
            await expect(report(page).locator('.ex-match-mark-current')).toHaveCount(1);

            // Painted, in a colour of its own, on the cell's ::before, as ExGrid paints it.
            const ground = (row) => cell(page, row, 0).evaluate((el) => getComputedStyle(el, '::before').backgroundColor);
            const [match, current] = [await ground(10), await ground(5)];
            expect(match).not.toMatch(/^(transparent|rgba\(0, 0, 0, 0\))$/);
            expect(current).not.toBe(match);

            // The marks go when the panel closes.
            await page.keyboard.press('Escape');
            await expect(report(page).locator('.ex-popover-find')).toHaveCount(0);
            await expect(report(page).locator('.ex-match-mark')).toHaveCount(0);
        });

        test(`ADR-0332 §1: a collapsed group is not searched inside, and nothing expands to show a match (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            // APAC's − button, on row 4.
            await cell(page, 4, 0).locator('.ex-pivot-toggle').click();
            await expect(cell(page, 4, 0).locator('.ex-pivot-toggle')).toHaveAttribute('aria-expanded', 'false');
            await expect(cell(page, 10, 0)).toContainText('Grand Total');
            await cell(page, 1, 1).click({ force: true });
            await expect(report(page)).toHaveAttribute('aria-activedescendant', /-r1c1$/);

            await page.keyboard.press('ControlOrMeta+f');
            await page.keyboard.type('equities');
            await expect(findField(page)).toHaveValue('equities');
            // Only EMEA's Equities is laid out, and marked; APAC's lies inside the collapsed group.
            await expect(report(page).locator('.ex-match-mark')).toHaveCount(1);
            await expect(cell(page, 7, 0)).toHaveClass(MARK);

            for (let step = 0; step < 2; step++) {
                await page.keyboard.press('Enter');
                await expect.poll(() => activeDescendant(report(page))).toMatch(/-r7c0$/);
            }
            // The report kept its shape: APAC still collapsed, the Grand Total still on row 10.
            await expect(cell(page, 4, 0).locator('.ex-pivot-toggle')).toHaveAttribute('aria-expanded', 'false');
            await expect(cell(page, 10, 0)).toContainText('Grand Total');
        });
    });
}
