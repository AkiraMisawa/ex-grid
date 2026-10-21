import { test, expect, watchNextKey, keySeenUntouched, circuitQuiet, settled } from './fixtures.mjs';
import { activeDescendant } from './keyboard.mjs';
import { painted, paints, blend } from './pixels.mjs';

// Find in ExPivot with real keys (ADR-0332 §1 and §4), on /pivot under ExPivot's own markup and under
// ExPivot.MudBlazor's Chrome, which must give the same outcomes (ADR-0061). Ctrl+F on the report opens
// the report grid's own panel — never the browser's find bar — and the keys typed straight after it
// land in its field; Enter steps through the cells the report lays out, by the text shown; a
// collapsed group is not searched inside and nothing expands; and the painted matches are marked,
// the current one apart. The column labels stand in the report grid's headers, which the grid searches
// itself before the rows (ADR-0333). Ctrl+H opens nothing, since a report is not written, and the Pivot Toolbar
// says so in Excel's words (§2).
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
const notice = (page, chrome) => pivot(page).locator(chrome === 'mud' ? '.mud-ex-pivot-refusal-notice' : '.ex-pivot-refusal-notice');

// The last input the report grid has answered, which its core writes on the root's first child
// (data-ex-answered, ADR-0220): a step answered onto the cell it started from changes nothing else.
const answered = (page) => report(page).evaluate((root) => Number(root.querySelector(':scope > [data-ex-answered]')?.getAttribute('data-ex-answered') ?? 0));

/** Enter in the find field, waited for until its step has been answered and the report has settled. */
async function step(page) {
    const before = await answered(page);
    await page.keyboard.press('Enter');
    await expect.poll(() => answered(page), 'the step was answered').toBeGreaterThan(before);
    await settled(report(page));
    await circuitQuiet();
}

/** A colour as the cascade keeps it, `rgb(…)`, `rgba(…)` or `color(srgb … / …)`, in sRGB bytes and the
 * alpha the browser holds, a whole number of 255ths. */
function rgba(css) {
    const srgb = css.match(/^color\(srgb ([\d.e-]+) ([\d.e-]+) ([\d.e-]+)(?: \/ ([\d.e-]+))?\)$/);
    const legacy = css.match(/^rgba?\(([\d.]+), ([\d.]+), ([\d.]+)(?:, ([\d.]+))?\)$/);
    const parts = srgb ? srgb.slice(1, 4).map((v) => Number(v) * 255) : legacy?.slice(1, 4).map(Number);
    if (!parts) throw new Error(`a colour this test cannot read: ${css}`);
    const alpha = Math.round(Number((srgb ?? legacy)[4] ?? 1) * 255) / 255;
    return { rgb: parts, alpha };
}

/** What a cell of column 0 paints on its right, clear of its text. */
async function groundOf(page, row) {
    const box = await cell(page, row, 0).boundingBox();
    const region = await painted(page, box);
    return region.at(box.x + box.width - 6, box.y + box.height / 2);
}

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
            await circuitQuiet();
            const unmarked = { 5: await groundOf(page, 5), 10: await groundOf(page, 10) };
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

            // Painted, each in its own fill, laid over the cell's ground on its ::before, as ExGrid paints it.
            await circuitQuiet();
            const fill = async (row) => rgba(await cell(page, row, 0).evaluate((el) => getComputedStyle(el, '::before').backgroundColor));
            const [match, current] = [await fill(10), await fill(5)];
            expect(match.alpha, 'the match fill is seen').toBeGreaterThan(0);
            expect(current.alpha, 'the current fill is seen').toBeGreaterThan(0);
            expect([...current.rgb, current.alpha]).not.toEqual([...match.rgb, match.alpha]);
            for (const [row, mark] of [[10, match], [5, current]]) {
                const expected = blend(mark.rgb, mark.alpha, unmarked[row]);
                const seen = await groundOf(page, row);
                expect(paints(seen, expected), `row ${row} paints ${expected.map((v) => v.toFixed(2))}, saw ${seen}`).toBe(true);
            }

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

            // Each step is waited for until it is answered: the second lands where the first did, so the
            // Focus alone cannot tell it has been taken.
            for (let n = 0; n < 2; n++) {
                await step(page);
                await expect(report(page)).toHaveAttribute('aria-activedescendant', /-r7c0$/);
                await expect(report(page).locator('.ex-match-mark-current')).toHaveCount(1);
                await expect(cell(page, 7, 0)).toHaveClass(/ex-match-mark-current/);
            }
            // The report kept its shape: APAC still collapsed, the Grand Total still on row 10.
            await expect(cell(page, 4, 0).locator('.ex-pivot-toggle')).toHaveAttribute('aria-expanded', 'false');
            await expect(cell(page, 10, 0)).toContainText('Grand Total');
        });

        test(`ADR-0333: a column label is found in the report's headers, marked and said, and the Focus stays (${chrome})`, async ({ page }) => {
            await open(page, chrome);
            // Bond is a Product, on Columns: it is a header of the report grid, and no row says it.
            const bond = report(page).locator('.ex-header-cell').filter({ hasText: /^Bond$/ });
            await expect(bond).toHaveCount(1);

            await page.keyboard.press('ControlOrMeta+f');
            await page.keyboard.type('bond');
            await expect(findField(page)).toHaveValue('bond');
            await expect(bond).toHaveClass(MARK);

            await step(page);
            await expect(bond).toHaveClass(/ex-match-mark-current/);
            await expect(report(page).locator('.ex-popover-find [role=status]')).toHaveText('Found in a header');
            await expect(report(page)).toHaveAttribute('aria-activedescendant', /-r1c1$/);

            // A lone match finds itself: the step goes round the rows and comes back to it.
            await step(page);
            await expect(bond).toHaveClass(/ex-match-mark-current/);
            await expect(report(page).locator('.ex-match-mark-current')).toHaveCount(1);
            await expect(report(page)).toHaveAttribute('aria-activedescendant', /-r1c1$/);
            await expect(findField(page)).toBeFocused();
        });

        test(`ADR-0332 §2: Ctrl+H on the report opens nothing, keeps the key from the browser, and the Pivot Toolbar says why (${chrome})`, async ({ page }) => {
            await open(page, chrome);

            await watchNextKey(page, ['h', 'H']);
            await page.keyboard.press('ControlOrMeta+h');
            // spec-lint: early-read: the keydown listener records the key inside the press's own dispatch, so the answer is final once the press returns
            expect(await keySeenUntouched(page), 'the report grid took Ctrl+H').not.toBe(true);

            await expect(notice(page, chrome)).toContainText('Cannot change this part of a PivotTable report.');
            await settled(report(page));
            await circuitQuiet();
            await expect(report(page).locator('.ex-popover-find')).toHaveCount(0);
            await expect(report(page)).toHaveAttribute('aria-activedescendant', /-r1c1$/);
        });
    });
}
