import { test, expect, watchNextKey, keySeenUntouched, scrollRowToTop, circuitQuiet, settled, holdFrames } from './fixtures.mjs';
import { expectKeyboardOn, activeDescendant } from './keyboard.mjs';
import { acrossEdges, blend, onDevicePixels, paints } from './pixels.mjs';

// Find with real keys (ADR-0055): Ctrl+F on a focused grid opens the grid's own panel — never
// the browser's find bar, which sees only the painted rows — the keys typed straight after it
// land in the field, Enter steps through every row including the unpainted ones, and Escape
// hands the keyboard back. /features is bound to GridSource.From, the reference search. Run once
// per Chrome: the built-in panel and ExGrid.MudBlazor's must give identical outcomes (WR-5).
//
// Its Trader column (1) repeats every three rows through four names: rows 3-5 are Novak, then
// rows 15-17, and so on to 397-399. Notional (2) is unique per row. While the panel holds a search,
// the painted Novaks wear the Match Mark (ADR-0332 §4), judged by the reference rule.

function grid(page) {
    return page.locator('.ex-grid').first();
}

const MARK = /(^|\s)ex-match-mark(\s|$)/;

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

async function focusedCell(page) {
    return activeDescendant(grid(page));
}

for (const chrome of ['builtin', 'mud']) {
    test.describe(`under the ${chrome} Chrome`, () => {
        test.beforeEach(async ({ page }) => {
            await page.goto(`/features?chrome=${chrome}`);
            await expect(grid(page).locator('.ex-row').first()).toBeVisible();
            await grid(page).locator("[id$='r0c0']").click({ force: true });
        });

        test('Ctrl+F opens the grid\'s panel, the keys typed after it land in its field, and Enter steps (FD-1/FD-3/FD-5, ADR-0055)', async ({ page }) => {
            await watchNextKey(page, ['f', 'F']);
            // Typed together: the panel takes DOM focus a render later, and the letters are
            // held until it has (ADR-0010/0039).
            await page.keyboard.press('ControlOrMeta+f');
            await page.keyboard.type('novak');
            // spec-lint: early-read: the keydown listener records the key inside the press's own dispatch, so the answer is final once the press returns
            expect(await keySeenUntouched(page), 'the grid took Ctrl+F').not.toBe(true);

            const panel = grid(page).locator('.ex-popover-find');
            await expect(panel).toBeVisible();
            const field = panel.locator('input').first();
            await expect(field).toBeFocused();
            await expect(field).toHaveValue('novak');

            await page.keyboard.press('Enter');
            await expect.poll(() => focusedCell(page)).toMatch(/r3c1$/);
            await page.keyboard.press('Enter');
            await expect.poll(() => focusedCell(page)).toMatch(/r4c1$/);
            await page.keyboard.press('Shift+Enter');
            await expect.poll(() => focusedCell(page)).toMatch(/r3c1$/);
            // The panel stands through the steps, and the field keeps the keyboard.
            await expect(field).toBeFocused();
        });

        // ADR-0240's note of 2026-10-21. The panel's field takes the keyboard a round trip after Ctrl+F on a
        // circuit, and the hold behind Ctrl+F looked for it once a frame: a Tab typed after the field had it
        // but before that frame was held, and a held Tab cannot be handed on from script, so it was refused
        // and the field kept the keyboard. On main it did so whenever no frame fell between the two. Here
        // no frame falls until the Tab is typed, so that is the case every time.
        test('FD-1/ADR-0240: a Tab typed once Find\'s field holds the keyboard moves on in the panel, before any frame has looked', async ({ page }) => {
            await settled(grid(page));
            const letGo = await holdFrames(page);
            await page.keyboard.press('ControlOrMeta+f');
            const panel = grid(page).locator('.ex-popover-find');
            const field = panel.locator('input').first();
            await expect(field).toBeFocused();

            await page.keyboard.press('Tab');
            await letGo();
            await settled(grid(page));
            await expect(field).not.toBeFocused();
            await expect(panel).toBeVisible();
            expect(await panel.evaluate((p) => p.contains(document.activeElement)), 'the keyboard is still the panel\'s').toBe(true);
        });

        test('a capital and a Shift+Enter typed straight after Ctrl+F keep their meaning (ADR-0055/0010, ED-22)', async ({ page }) => {
            // All of it before the panel can have taken DOM focus on a circuit: the keys are
            // held and handed to the field. A Shift pressed for the capital, and for the
            // Shift+Enter, is a keydown of its own and must not stop the replay there. Backward
            // from the first cell wraps to the last Novak.
            await page.keyboard.press('ControlOrMeta+f');
            await page.keyboard.type('Novak');
            await page.keyboard.press('Shift+Enter');

            await expect.poll(() => focusedCell(page)).toMatch(/r399c1$/);
            await expect(grid(page).locator('.ex-popover-find input').first()).toHaveValue('Novak');
        });

        test('a match beyond the painted rows is reached and revealed (FD-5, ADR-0055)', async ({ page }) => {
            // Row 350's notional: 1,000,000 + 350 × 12,345.67, shown without separators.
            await page.keyboard.press('ControlOrMeta+f');
            const field = grid(page).locator('.ex-popover-find input').first();
            await expect(field).toBeFocused();
            await page.keyboard.type('5320984.5');
            await page.keyboard.press('Enter');

            await expect.poll(() => focusedCell(page)).toMatch(/r350c2$/);
            await expect(grid(page).locator("[id$='r350c2']")).toBeInViewport();
        });

        test('with a range selected, Find searches it and the selection stands (FD-4/FD-5, ADR-0055)', async ({ page }) => {
            await grid(page).locator("[id$='r0c1']").click({ force: true });
            await grid(page).locator("[id$='r8c1']").click({ force: true, modifiers: ['Shift'] });

            await page.keyboard.press('ControlOrMeta+f');
            await expect(grid(page).locator('.ex-popover-find input').first()).toBeFocused();
            await page.keyboard.type('novak');
            for (const row of [3, 4, 5, 3]) {
                await page.keyboard.press('Enter');
                await expect.poll(() => focusedCell(page)).toMatch(new RegExp(`r${row}c1$`));
            }
            // The selection is the nine cells it was: the Focus moved inside it.
            await expect(grid(page).locator('.ex-range')).toHaveCount(1);
        });

        test('nothing found is said, in a live region (FD-6, ADR-0055)', async ({ page }) => {
            await page.keyboard.press('ControlOrMeta+f');
            await expect(grid(page).locator('.ex-popover-find input').first()).toBeFocused();
            await page.keyboard.type('no such trader');
            await page.keyboard.press('Enter');

            const outcome = grid(page).locator('.ex-popover-find [role=status]');
            await expect(outcome).toHaveText('No match');
            await expect(page.locator('#find-refused-status')).toContainText('NotFound');
        });

        test('ADR-0240: a tick of Match case or Match entire cell contents and the step clicked straight after it are taken in the order they were made (find ticket 06)', async ({ page }) => {
            // Each phase is driven at machine speed, nothing awaited between the step, the tick and the
            // step after it: a tick is a click in the Input Order, so the step clicked before it searches
            // with the option as it stood and the one clicked after it with the option ticked, on every
            // host. The tick is a click on the option's words, which the browser passes on to the
            // checkbox as a second click; only the checkbox's is numbered.
            await page.keyboard.press('ControlOrMeta+f');
            const panel = grid(page).locator('.ex-popover-find');
            const field = panel.locator('input').first();
            await expect(field).toBeFocused();
            const next = panel.locator('button.ex-find-next, button.mud-ex-grid-find-next');
            const matchCase = panel.getByText('Match case', { exact: true });
            const wholeCell = panel.getByText('Match entire cell contents', { exact: true });
            const box = (option) => panel.locator(`input.ex-find-${option}, .mud-ex-grid-find-${option} input`);
            const outcome = panel.locator('[role=status]');

            // "NOVAK" is Novak's name without Match case, and no one's with it.
            await page.keyboard.type('NOVAK');
            await next.click();
            await matchCase.click();
            await next.click();
            await expect.poll(() => focusedCell(page)).toMatch(/r3c1$/);
            await expect(outcome).toHaveText('No match');
            await expect(box('match-case')).toBeChecked();

            // "nova" is in Novak's name, and is not the whole of it.
            await matchCase.click();
            await field.fill('nova');
            await next.click();
            await wholeCell.click();
            await next.click();
            await expect.poll(() => focusedCell(page)).toMatch(/r4c1$/);
            await expect(outcome).toHaveText('No match');
            await expect(box('match-case')).not.toBeChecked();
            await expect(box('whole-cell')).toBeChecked();
        });

        test('Ctrl+F in the find field selects its text, and the browser\'s find stays shut (FD-1, ADR-0055)', async ({ page }) => {
            await page.keyboard.press('ControlOrMeta+f');
            const field = grid(page).locator('.ex-popover-find input').first();
            await expect(field).toBeFocused();
            await page.keyboard.type('novak');

            await watchNextKey(page, ['f', 'F']);
            await page.keyboard.press('ControlOrMeta+f');

            // spec-lint: early-read: the keydown listener records the key inside the press's own dispatch, so the answer is final once the press returns
            expect(await keySeenUntouched(page), 'the grid took Ctrl+F in its own field').not.toBe(true);
            await expect(field).toBeFocused();
            await expect.poll(() => field.evaluate((el) => [el.selectionStart, el.selectionEnd])).toEqual([0, 5]);
        });

        test('Ctrl+F in a column\'s popover opens Find in its place (FD-1, ADR-0055)', async ({ page }) => {
            await page.keyboard.press('Alt+ArrowDown');
            const menu = grid(page).locator('.ex-popover [role=menu]').first();
            await expect(menu).toBeVisible();

            await watchNextKey(page, ['f', 'F']);
            await page.keyboard.press('ControlOrMeta+f');

            // spec-lint: early-read: the keydown listener records the key inside the press's own dispatch, so the answer is final once the press returns
            expect(await keySeenUntouched(page), 'the grid took Ctrl+F in its own popover').not.toBe(true);
            await expect(grid(page).locator('.ex-popover')).toHaveCount(1);
            await expect(grid(page).locator('.ex-popover-find input').first()).toBeFocused();
        });

        test('Ctrl+F in the Context Menu, and in the filter\'s text field, opens Find in its place (FD-1, ADR-0055)', async ({ page }) => {
            await page.keyboard.press('Shift+F10');
            await expect(grid(page).locator('.ex-popover[role=menu]')).toBeVisible();
            await page.keyboard.press('ControlOrMeta+f');
            await expect(grid(page).locator('.ex-popover')).toHaveCount(1);
            await expect(grid(page).locator('.ex-popover-find input').first()).toBeFocused();
            await page.keyboard.press('Escape');
            await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
            await expectKeyboardOn(grid(page));

            // Book's filter is a value list with a search box: E puts the keyboard in it.
            await page.keyboard.press('Alt+ArrowDown');
            await expect(grid(page).locator('.ex-popover [role=menu]').first()).toBeVisible();
            await page.keyboard.press('e');
            const search = grid(page).locator('.ex-popover-filter input').first();
            await expect(search).toBeFocused();
            await watchNextKey(page, ['f', 'F']);
            await page.keyboard.press('ControlOrMeta+f');
            // spec-lint: early-read: the keydown listener records the key inside the press's own dispatch, so the answer is final once the press returns
            expect(await keySeenUntouched(page), 'the grid took Ctrl+F in the filter\'s field').not.toBe(true);
            await expect(grid(page).locator('.ex-popover')).toHaveCount(1);
            await expect(grid(page).locator('.ex-popover-find input').first()).toBeFocused();
        });

        test('a Ctrl+F typed straight after Alt+Down opens Find, and the letters after it land in its field (FD-1, ED-22, ADR-0055)', async ({ page }) => {
            // Typed together: on a circuit the column's popover takes DOM focus a round trip
            // later, so the Ctrl+F and the letters are held, and the Ctrl+F must reach the core
            // rather than the menu, which would take a letter as a command.
            await page.keyboard.press('Alt+ArrowDown');
            await page.keyboard.press('ControlOrMeta+f');
            await page.keyboard.type('Osei');

            const field = grid(page).locator('.ex-popover-find input').first();
            await expect(field).toBeFocused();
            await expect(field).toHaveValue('Osei');
            await expect(grid(page).locator('.ex-popover')).toHaveCount(1);
        });

        test('Escape closes the panel and hands the keyboard back to the grid (FD-3, ADR-0055)', async ({ page }) => {
            await page.keyboard.press('ControlOrMeta+f');
            await expect(grid(page).locator('.ex-popover-find input').first()).toBeFocused();
            await page.keyboard.press('Escape');

            await expect(grid(page).locator('.ex-popover-find')).toHaveCount(0);
            await expectKeyboardOn(grid(page));
            await page.keyboard.press('ArrowDown');
            await expect.poll(() => focusedCell(page)).toMatch(/r1c0$/);
        });

        // The Match Mark (ADR-0332 §4): /features is bound to GridSource.From, so the reference rule
        // marks every painted match, and the current match apart, under either Chrome.
        test('every painted match is marked as it is typed, and the current match apart (ADR-0332)', async ({ page }) => {
            await page.keyboard.press('ControlOrMeta+f');
            await page.keyboard.type('novak');
            const field = grid(page).locator('.ex-popover-find input').first();
            await expect(field).toHaveValue('novak');

            for (const row of [3, 4, 5]) {
                await expect(grid(page).locator(`[id$='r${row}c1']`)).toHaveClass(/(^|\s)ex-match-mark(\s|$)/);
            }
            await expect(grid(page).locator("[id$='r2c1']")).not.toHaveClass(/ex-match-mark/);
            await expect(grid(page).locator('.ex-match-mark-current')).toHaveCount(0);

            await page.keyboard.press('Enter');
            await expect.poll(() => focusedCell(page)).toMatch(/r3c1$/);
            await expect(grid(page).locator("[id$='r3c1']")).toHaveClass(/ex-match-mark-current/);
            await expect(grid(page).locator('.ex-match-mark-current')).toHaveCount(1);

            // Painted, in a colour of its own: a match's ground and the current one's differ, and
            // neither is transparent. Read from the cell's ::before, which holds the mark.
            const ground = (id) => grid(page).locator(`[id$='${id}']`).evaluate((el) => getComputedStyle(el, '::before').backgroundColor);
            const [match, current] = [await ground('r4c1'), await ground('r3c1')];
            expect(match).not.toMatch(/^(transparent|rgba\(0, 0, 0, 0\))$/);
            expect(current).not.toMatch(/^(transparent|rgba\(0, 0, 0, 0\))$/);
            expect(current).not.toBe(match);

            await page.keyboard.press('Enter');
            await expect.poll(() => focusedCell(page)).toMatch(/r4c1$/);
            await expect(grid(page).locator("[id$='r4c1']")).toHaveClass(/ex-match-mark-current/);
            await expect(grid(page).locator("[id$='r3c1']")).not.toHaveClass(/ex-match-mark-current/);
            await expect(grid(page).locator("[id$='r3c1']")).toHaveClass(/ex-match-mark/);
        });

        test('a match scrolled into view is marked as it is painted, with no step asked (ADR-0332)', async ({ page }) => {
            await page.keyboard.press('ControlOrMeta+f');
            await page.keyboard.type('novak');
            await expect(grid(page).locator("[id$='r3c1']")).toHaveClass(/ex-match-mark/);

            // Rows 207-209 are Novaks far below the rows painted at the top, so none of them was
            // painted before the scroll.
            await expect(grid(page).locator("[id$='r207c1']")).toHaveCount(0);
            await scrollRowToTop(grid(page), 205);

            for (const row of [207, 208, 209]) {
                await expect(grid(page).locator(`[id$='r${row}c1']`)).toHaveClass(/(^|\s)ex-match-mark(\s|$)/);
            }
            await expect(grid(page).locator("[id$='r206c1']")).not.toHaveClass(/ex-match-mark/);
            // The panel stood through the scroll, holding its text.
            await expect(grid(page).locator('.ex-popover-find input').first()).toHaveValue('novak');
        });

        test('the marks clear when the text is emptied and when the panel closes (ADR-0332)', async ({ page }) => {
            await page.keyboard.press('ControlOrMeta+f');
            await page.keyboard.type('novak');
            await expect(grid(page).locator("[id$='r3c1']")).toHaveClass(/ex-match-mark/);

            const field = grid(page).locator('.ex-popover-find input').first();
            await field.press('ControlOrMeta+a');
            await field.press('Backspace');
            await expect(field).toHaveValue('');
            await expect(grid(page).locator('.ex-match-mark')).toHaveCount(0);

            await page.keyboard.type('novak');
            await expect(grid(page).locator("[id$='r3c1']")).toHaveClass(/ex-match-mark/);
            await page.keyboard.press('Escape');
            await expect(grid(page).locator('.ex-popover-find')).toHaveCount(0);
            await expect(grid(page).locator('.ex-match-mark')).toHaveCount(0);
        });

        // Replace (ADR-0332 §2/§3): /features passes OnReplace and its Trader and Notional are
        // Editable, so Ctrl+H opens the panel with Replace. Replace All is GridSource.From's answer,
        // applied as one history step. Book (0) is not Editable.
        const replaceParts = chrome === 'mud'
            ? { field: '.mud-ex-grid-find-replace-field input', one: 'button.mud-ex-grid-find-replace', all: 'button.mud-ex-grid-find-replace-all', outcome: '.mud-ex-grid-find-outcome' }
            : { field: 'input.ex-find-replace-field', one: 'button.ex-find-replace', all: 'button.ex-find-replace-all', outcome: '.ex-find-outcome' };

        async function openReplace(page, text, replacement) {
            await watchNextKey(page, ['h', 'H']);
            await page.keyboard.press('ControlOrMeta+h');
            // spec-lint: early-read: the keydown listener records the key inside the press's own dispatch, so the answer is final once the press returns
            expect(await keySeenUntouched(page), 'the grid took Ctrl+H').not.toBe(true);
            // Read before typing: the text may hold an h, which the field takes untouched.
            await page.keyboard.type(text);
            const panel = grid(page).locator('.ex-popover-find');
            await expect(panel.locator('input').first()).toHaveValue(text);
            const field = panel.locator(replaceParts.field);
            await field.click();
            await page.keyboard.type(replacement);
            await expect(field).toHaveValue(replacement);
            return panel;
        }

        test('Ctrl+H opens the panel with Replace, and Replace All replaces every match as one undo step (ADR-0332)', async ({ page }) => {
            const panel = await openReplace(page, 'novak', 'Rossi');

            await panel.locator(replaceParts.all).click();
            await expect(panel.locator(replaceParts.outcome)).toHaveText('Replaced 100 cells.');
            await expect(page.locator('#replace-status')).toHaveText('Replace All: 100 cells in 100 rows');
            for (const row of [3, 4, 5]) {
                await expect(grid(page).locator(`[id$='r${row}c1']`)).toHaveText('Rossi');
            }
            // Beyond the painted rows too: the whole match set was written, not the painted part.
            await scrollRowToTop(grid(page), 397);
            await expect(grid(page).locator("[id$='r399c1']")).toHaveText('Rossi');
            await scrollRowToTop(grid(page), 0);
            // Nothing left to mark: every match was written.
            await expect(grid(page).locator('.ex-match-mark')).toHaveCount(0);

            await page.keyboard.press('Escape');
            await expect(grid(page).locator('.ex-popover-find')).toHaveCount(0);
            await page.keyboard.press('ControlOrMeta+z');
            await expect(page.locator('#history-status')).toHaveText('History: 100 changes undone');
            for (const row of [3, 4, 5]) {
                await expect(grid(page).locator(`[id$='r${row}c1']`)).toHaveText('Novak');
            }
        });

        test('Replace writes the current match and moves to the next (ADR-0332)', async ({ page }) => {
            const panel = await openReplace(page, 'novak', 'Rossi');

            // No match is current yet: the first press finds one, and writes nothing.
            await panel.locator(replaceParts.one).click();
            await expect.poll(() => focusedCell(page)).toMatch(/r3c1$/);
            await expect(grid(page).locator("[id$='r3c1']")).toHaveText('Novak');

            await panel.locator(replaceParts.one).click();
            await expect(grid(page).locator("[id$='r3c1']")).toHaveText('Rossi');
            await expect.poll(() => focusedCell(page)).toMatch(/r4c1$/);
            await expect(grid(page).locator("[id$='r4c1']")).toHaveText('Novak');
            await expect(page.locator('#edit-status')).toContainText('Trader=Rossi on ');
            await expect(panel.locator(replaceParts.field)).toHaveValue('Rossi');
        });

        test('with Replace shown, Enter in either field finds the next match (ADR-0332, ADR-0055)', async ({ page }) => {
            const panel = await openReplace(page, 'novak', 'Rossi');

            // The keyboard is in the Replace field: the form's second field must not stop the step.
            await page.keyboard.press('Enter');
            await expect.poll(() => focusedCell(page)).toMatch(/r3c1$/);
            await panel.locator('input').first().focus();
            await page.keyboard.press('Enter');
            await expect.poll(() => focusedCell(page)).toMatch(/r4c1$/);
            // A step writes nothing.
            await expect(grid(page).locator("[id$='r3c1']")).toHaveText('Novak');
        });

        test('Ctrl+F in a field of the panel with Replace turns it back into Find alone (ADR-0332)', async ({ page }) => {
            const panel = await openReplace(page, 'novak', 'Rossi');

            await page.keyboard.press('ControlOrMeta+f');
            await expect(panel.locator(replaceParts.field)).toHaveCount(0);
            await expect(panel.locator(replaceParts.all)).toHaveCount(0);
            const field = panel.locator('input').first();
            await expect(field).toBeFocused();
            await expect(field).toHaveValue('novak');
        });

        test('a match in a column that cannot be edited refuses Replace All, naming the cells (ADR-0332)', async ({ page }) => {
            // Alpha is every fourth row's Book, which is not Editable: nothing is written.
            const panel = await openReplace(page, 'alpha', 'Omega');

            await panel.locator(replaceParts.all).click();
            await expect(panel.locator(replaceParts.outcome)).toHaveText(
                'Nothing was replaced. Row 1, Book: this column cannot be edited. Row 5, Book: this column cannot be edited. '
                + 'Row 9, Book: this column cannot be edited. Row 13, Book: this column cannot be edited. '
                + 'Row 17, Book: this column cannot be edited. And 95 more.');
            await expect(page.locator('#replace-status')).toHaveText('Replace All: refused, 100 cells');
            await expect(grid(page).locator("[id$='r0c0']")).toHaveText('Alpha');
            await expect(page.locator('#find-refused-status')).toContainText('ReplaceRefused');
        });

        // Find reaches the column headers (ADR-0333): /features's headers are Book, Trader, Notional,
        // Narrow, TradeDate and Confirmed, under the Header Groups Who (Book, Trader) and What. No cell
        // says "trade", so a step from r0c0 finds no row and the walk reaches the headers, which stand
        // before the first row; the Focus stays where it is.
        const header = (page, text) => grid(page).locator('.ex-header-cell, .ex-header-group').filter({ hasText: new RegExp(`^${text}$`) });

        test('ADR-0333: a column header Find reaches is the current match, marked over its own box, said in the panel, and the Focus stays', async ({ page }) => {
            const trader = header(page, 'Trader');
            await circuitQuiet();
            // Its edges on Device Pixels (ADR-0090), read before the mark and after it.
            const box = await onDevicePixels(page, trader);
            const unmarked = await acrossEdges(page, box);

            await page.keyboard.press('ControlOrMeta+f');
            await page.keyboard.type('trade');
            const field = grid(page).locator('.ex-popover-find input').first();
            await expect(field).toHaveValue('trade');
            // Every header that says it is marked as it is typed, no step asked.
            await expect(trader).toHaveClass(MARK);
            await expect(header(page, 'TradeDate')).toHaveClass(MARK);
            await expect(header(page, 'Notional')).not.toHaveClass(MARK);
            await expect(grid(page).locator('.ex-match-mark-current')).toHaveCount(0);

            await page.keyboard.press('Enter');
            await expect(trader).toHaveClass(/ex-match-mark-current/);
            await expect(grid(page).locator('.ex-popover-find [role=status]')).toHaveText('Found in the header Trader');
            await expect.poll(() => focusedCell(page)).toMatch(/r0c0$/);
            await expect(field).toBeFocused();

            // The current mark is laid over the header's own ground, up to its edges and not past them.
            await circuitQuiet();
            const fill = rgba(await trader.evaluate((el) => getComputedStyle(el, '::before').backgroundColor));
            expect(fill.alpha, 'the current fill is seen').toBeGreaterThan(0);
            const marked = await acrossEdges(page, box);
            for (const side of ['left', 'right']) {
                for (const index of [0, 1]) {
                    const expected = blend(fill.rgb, fill.alpha, unmarked.at(side, index));
                    expect(paints(marked.at(side, index), expected), `${side} ${index} paints ${expected.map((v) => v.toFixed(2))}, saw ${marked.at(side, index)}`).toBe(true);
                }
                expect(marked.at(side, -1), `${side} -1, outside the header, is not marked`).toEqual(unmarked.at(side, -1));
            }

            // The next step goes on from the header: to the next header that says it, then round the
            // rows, none of which does, and back to the first.
            await page.keyboard.press('Enter');
            await expect(header(page, 'TradeDate')).toHaveClass(/ex-match-mark-current/);
            await expect(trader).not.toHaveClass(/ex-match-mark-current/);
            await page.keyboard.press('Enter');
            await expect(trader).toHaveClass(/ex-match-mark-current/);
            await expect(grid(page).locator('.ex-match-mark-current')).toHaveCount(1);
            await expect.poll(() => focusedCell(page)).toMatch(/r0c0$/);
        });

        test('ADR-0333: a Header Group label is found before the column headers, and a step from it goes on to the rows', async ({ page }) => {
            await page.keyboard.press('ControlOrMeta+f');
            await page.keyboard.type('who');
            await expect(grid(page).locator('.ex-popover-find input').first()).toHaveValue('who');
            await expect(header(page, 'Who')).toHaveClass(MARK);

            await page.keyboard.press('Enter');
            await expect(header(page, 'Who')).toHaveClass(/ex-match-mark-current/);
            await expect(grid(page).locator('.ex-popover-find [role=status]')).toHaveText('Found in the header Who');

            // Novak is in Trader's rows: from the header the rows are searched from their start.
            await page.keyboard.press('ControlOrMeta+a');
            await page.keyboard.type('novak');
            await page.keyboard.press('Enter');
            await expect.poll(() => focusedCell(page)).toMatch(/r3c1$/);
            await expect(header(page, 'Who')).not.toHaveClass(MARK);
            await expect(grid(page).locator('.ex-popover-find [role=status]')).toHaveText('');
        });
    });
}

test('Tab stays inside the find panel (FD-8, ADR-0055/0039)', async ({ page }) => {
    await page.goto('/features');
    await expect(grid(page).locator('.ex-row').first()).toBeVisible();
    await grid(page).locator("[id$='r0c0']").click({ force: true });
    await page.keyboard.press('ControlOrMeta+f');
    const panel = grid(page).locator('.ex-popover-find');
    await expect(panel.locator('input').first()).toBeFocused();

    for (let i = 0; i < 8; i++) {
        await page.keyboard.press('Tab');
        await expect.poll(() => panel.evaluate((el) => el.contains(document.activeElement)), { message: `Tab ${i + 1} stayed inside the panel` }).toBe(true);
    }
});

test('the find panel stands inside the grid\'s box (FD-8, ADR-0055/0040)', async ({ page }) => {
    await page.goto('/features');
    await expect(grid(page).locator('.ex-row').first()).toBeVisible();
    await grid(page).locator("[id$='r0c0']").click({ force: true });
    await page.keyboard.press('ControlOrMeta+f');
    const panel = grid(page).locator('.ex-popover-find');
    await expect(panel).toBeVisible();

    const [inner, outer] = await Promise.all([panel.boundingBox(), grid(page).boundingBox()]);
    expect(inner.x).toBeGreaterThanOrEqual(outer.x);
    expect(inner.y).toBeGreaterThanOrEqual(outer.y);
    expect(inner.x + inner.width).toBeLessThanOrEqual(outer.x + outer.width + 0.5);
    expect(inner.y + inner.height).toBeLessThanOrEqual(outer.y + outer.height + 0.5);
    // One per instance: the second grid on the page opened nothing.
    await expect(page.locator('.ex-popover-find')).toHaveCount(1);
});

test('a grid with nothing to search takes Ctrl+F and opens nothing (FD-1/FD-2, ADR-0055)', async ({ page }) => {
    await page.goto('/cells');
    const cells = page.locator('.ex-grid').first();
    await expect(cells.locator('.ex-row').first()).toBeVisible();
    await cells.locator("[id$='r0c0']").click({ force: true });

    await watchNextKey(page, ['f', 'F']);
    await page.keyboard.press('ControlOrMeta+f');
    // spec-lint: early-read: the keydown listener records the key inside the press's own dispatch, so the answer is final once the press returns
    expect(await keySeenUntouched(page), 'the grid took Ctrl+F').not.toBe(true);
    await expect(cells.locator('.ex-popover')).toHaveCount(0);
    // The key after it is not held behind a panel that never comes.
    await page.keyboard.press('ArrowDown');
    await expect.poll(() => cells.getAttribute('aria-activedescendant')).toMatch(/r1c0$/);
});

test('in the Cell Editor Ctrl+F does nothing (FD-1, ADR-0055)', async ({ page }) => {
    await page.goto('/features');
    await expect(grid(page).locator('.ex-row').first()).toBeVisible();
    await grid(page).locator("[id$='r0c1']").click({ force: true });
    await page.keyboard.type('Ab');
    await page.keyboard.press('ControlOrMeta+f');

    await expect(grid(page).locator('.ex-popover-find')).toHaveCount(0);
    await expect(grid(page).locator('input.ex-editor')).toHaveValue('Ab');
});

test('a grid with nothing to replace takes Ctrl+H and opens nothing (ADR-0332)', async ({ page }) => {
    await page.goto('/cells');
    const cells = page.locator('.ex-grid').first();
    await expect(cells.locator('.ex-row').first()).toBeVisible();
    await cells.locator("[id$='r0c0']").click({ force: true });

    await watchNextKey(page, ['h', 'H']);
    await page.keyboard.press('ControlOrMeta+h');
    // spec-lint: early-read: the keydown listener records the key inside the press's own dispatch, so the answer is final once the press returns
    expect(await keySeenUntouched(page), 'the grid took Ctrl+H').not.toBe(true);
    await expect(cells.locator('.ex-popover')).toHaveCount(0);
    // The key after it is not held behind a panel that never comes.
    await page.keyboard.press('ArrowDown');
    await expect.poll(() => cells.getAttribute('aria-activedescendant')).toMatch(/r1c0$/);
});

test('in the Cell Editor Ctrl+H does nothing (ADR-0332)', async ({ page }) => {
    await page.goto('/features');
    await expect(grid(page).locator('.ex-row').first()).toBeVisible();
    await grid(page).locator("[id$='r0c1']").click({ force: true });
    await page.keyboard.type('Ab');
    await page.keyboard.press('ControlOrMeta+h');

    await expect(grid(page).locator('.ex-popover-find')).toHaveCount(0);
    await expect(grid(page).locator('input.ex-editor')).toHaveValue('Ab');
});
