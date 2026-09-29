import { test, expect, alterPage, setRoundTrip, record, watchNextKey, keySeenUntouched } from './fixtures.mjs';
import { SERVER } from './hosting.mjs';
import {
    sheet, cell, clickCell, clickBarEnd, editor, bar, nameBox, expectFocusAt, goTo, enter,
    expectCovers, boxOf, readClipboard, candidates, typeSteadily, pressCell,
} from './sheet-helpers.mjs';

// The ExGrid declarations of ADR-0050 and ADR-0051 (§26, DC-*), as ExSheet declares them on
// /sheet, driven with real keys, the real mouse and the real clipboard: completion, Point, the
// Formula Bar under a delayed circuit, F4 cycling the Reference at the caret, the fill handle, a spilling paste, copy and paste inside
// the Sheet, undo and redo, the resize grips. The positions grid beside the Sheet declares
// nothing, and is the "one not declaring" of DC-25. Two ExSheets on one page are on /sheets.

// Tall enough that every Sheet on the page, Formula Bar to horizontal scrollbar, is inside the
// window: a pointer below the window's edge reaches nothing, and the edge band sits there.
test.use({ viewport: { width: 1280, height: 1000 } });

test.beforeEach(async ({ page, context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    await page.goto('/sheet');
    // A WebAssembly page boots the runtime on every navigation, which can take longer than an
    // assertion's default wait on a loaded machine.
    await expect(page.locator('#demo-interactive')).toBeAttached({ timeout: 30_000 });
    await expect(cell(sheet(page), 'A1')).toHaveText('Item');
    // The page pushes the Linked Table's first snapshot 1.5 s after the Sheet opens. Every
    // change to the Sheet clears ExSheet's notice, a Consumer's push included
    // (ExSheet.ChangedAsync), so a refusal read before the push lands can be wiped by it.
    await expect(cell(sheet(page), 'B12')).toHaveText('318.25', { timeout: 10_000 });
});

// Reopens /sheet under ExGrid.MudBlazor's Chrome; the built-in one is what beforeEach opened.
async function underChrome(page, chrome) {
    if (chrome === 'builtin') {
        return;
    }
    await page.goto(`/sheet?chrome=${chrome}`);
    await expect(page.locator('#demo-interactive')).toBeAttached({ timeout: 30_000 });
    await expect(cell(sheet(page), 'A1')).toHaveText('Item');
    await expect(page.locator('.mud-ex-formula-bar-text, .mud-ex-name-box').first()).toBeAttached();
    await expect(cell(sheet(page), 'B12')).toHaveText('318.25', { timeout: 10_000 });
}

const completion = (grid) => grid.locator('.ex-completion');
const items = candidates;
const caret = (locator) => locator.evaluate((input) => input.selectionStart);

// ---------------------------------------------------------------------------------------------
// Completion (DC-17, DC-18, DC-31)

// Under the built-in Chrome and ExGrid.MudBlazor's (/sheet?chrome=mud): the Chrome paints the
// list and the fields, the core decides the keys and the box, so every outcome is the same
// (ADR-0010/0030, DC-17's "under both Chromes").
for (const chrome of ['builtin', 'mud']) {
    test.describe(`under the ${chrome} Chrome`, () => {
        test.beforeEach(async ({ page }) => {
            await underChrome(page, chrome);
        });

        test('DC-17: the candidate list opens under the editor, inside the grid\'s box; Tab accepts', async ({ page }) => {
            const grid = sheet(page);
            await clickCell(grid, 'F2');
            await page.keyboard.type('=SU');
            await expect(items(grid).first()).toHaveText('SUM');
            // Inside the grid's box (ADR-0040), and beneath the cell being edited.
            const list = await boxOf(completion(grid));
            const root = await boxOf(grid);
            const edited = await boxOf(editor(grid));
            expect(list.x).toBeGreaterThanOrEqual(root.x - 0.5);
            expect(list.y).toBeGreaterThanOrEqual(root.y - 0.5);
            expect(list.x + list.width).toBeLessThanOrEqual(root.x + root.width + 0.5);
            expect(list.y + list.height).toBeLessThanOrEqual(root.y + root.height + 0.5);
            expect(list.y).toBeGreaterThanOrEqual(edited.y + edited.height - 1);

            // ↓ chooses the next candidate, ↑ the one before; the Focus does not move.
            const count = await items(grid).count();
            if (count > 1) {
                await page.keyboard.press('ArrowDown');
                await expect(items(grid).nth(1)).toHaveAttribute('aria-selected', 'true');
                await page.keyboard.press('ArrowUp');
                await expect(items(grid).nth(0)).toHaveAttribute('aria-selected', 'true');
            }
            await expect(nameBox(grid)).toHaveValue('F2');

            // Tab accepts: the function and its bracket, the caret after them, the hint beneath.
            await page.keyboard.press('Tab');
            await expect(editor(grid)).toHaveValue('=SUM(');
            await expect(bar(grid)).toHaveValue('=SUM(');
            expect(await caret(editor(grid))).toBe(5);
            await expect(completion(grid)).toContainText('number1');
            await typeSteadily(page, editor(grid), 'B2:B4)');
            await page.keyboard.press('Enter');
            await expect(cell(grid, 'F2')).toHaveText('39');
        });

        test('DC-17: Escape closes the list and leaves the edit open; the next Escape cancels', async ({ page }) => {
            const grid = sheet(page);
            await clickCell(grid, 'F2');
            await page.keyboard.type('=SU');
            await expect(items(grid).first()).toBeVisible();
            await page.keyboard.press('Escape');
            await expect(completion(grid)).toHaveCount(0);
            await expect(editor(grid)).toHaveValue('=SU');
            await page.keyboard.press('Escape');
            await expect(editor(grid)).toHaveCount(0);
            await expect(cell(grid, 'F2')).toHaveText('');
        });

        test('DC-31: with the list open, ← and → move the caret and the list stays', async ({ page }) => {
            const grid = sheet(page);
            await clickCell(grid, 'F2');
            await page.keyboard.type('=SU');
            await expect(items(grid).first()).toHaveText('SUM');
            await page.waitForTimeout(150); // the gate is told a message after the list is painted
            // ← is not claimed while the list is open: it moves the caret, the edit stays open, and the
            // list answers the text before the caret.
            await page.keyboard.press('ArrowLeft');
            await expect.poll(() => caret(editor(grid))).toBe(2);
            await expect(editor(grid)).toHaveValue('=SU');
            await expect(nameBox(grid)).toHaveValue('F2');
            await expect(items(grid).first()).toBeVisible();
            // → too.
            await page.keyboard.press('ArrowRight');
            await expect.poll(() => caret(editor(grid))).toBe(3);
            await expect(editor(grid)).toHaveValue('=SU');
            await expect(items(grid).first()).toHaveText('SUM');
            // ↓ is still the list's, and Tab still accepts.
            await page.keyboard.press('Tab');
            await expect(editor(grid)).toHaveValue('=SUM(');
            await page.keyboard.press('Escape');
            await page.keyboard.press('Escape');
            await expect(editor(grid)).toHaveCount(0);
        });

        test('DC-31: a repeated letter (=SS) completes the span at the caret, and the caret sits after it', async ({ page }) => {
            const grid = sheet(page);
            await clickCell(grid, 'F2');
            await page.keyboard.type('=S');
            await expect(items(grid).first()).toBeVisible();
            // The list is painted by one message and the key listener told it is open by the
            // next; on a circuit a ← pressed between the two is still gated as Overwrite's and
            // is swallowed (found by this suite, 2026-09-27; reported, not pinned here).
            await page.waitForTimeout(150);
            await page.keyboard.press('ArrowLeft');
            await expect.poll(() => caret(editor(grid))).toBe(1);
            // A second S typed before the first: the text is =SS, and the new S is at the caret, 2.
            // Inferred from the change alone, the new S could as well be the last one.
            await page.keyboard.type('S');
            await expect(editor(grid)).toHaveValue('=SS');
            await expect.poll(() => caret(editor(grid))).toBe(2);
            await expect(items(grid).first()).toHaveText('SUM');
            // The list offers SUM: it answered the prefix before the caret, "S". Had the caret been
            // inferred as 3, the prefix would be "SS", which names nothing, and no list would show.
            // Tab replaces the name the caret stands in — ExSheet's span is the whole name token
            // (FormulaEntry.Complete) — and puts the caret after the inserted text.
            await page.keyboard.press('Tab');
            await expect(editor(grid)).toHaveValue('=SUM(');
            await expect(bar(grid)).toHaveValue('=SUM(');
            await expect.poll(() => caret(editor(grid))).toBe(5);
            // And typing goes on at that caret.
            await typeSteadily(page, editor(grid), '1,');
            await expect(editor(grid)).toHaveValue('=SUM(1,');
            await page.keyboard.press('Escape');
            await page.keyboard.press('Escape');
            await expect(editor(grid)).toHaveCount(0);
        });

        test('DC-17: completion works from the Formula Bar, its list inside the grid\'s box', async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'F2');
            await clickBarEnd(grid);
            await typeSteadily(page, bar(grid), '=RO');
            await expect(items(grid).first()).toHaveText('ROUND');
            const list = await boxOf(completion(grid));
            const root = await boxOf(grid);
            const field = await boxOf(bar(grid));
            expect(list.y).toBeGreaterThanOrEqual(field.y + field.height - 1);
            expect(list.y + list.height).toBeLessThanOrEqual(root.y + root.height + 0.5);
            await page.keyboard.press('Tab');
            await expect(bar(grid)).toHaveValue('=ROUND(');
            await expect(editor(grid)).toHaveValue('=ROUND(');
            await expect(completion(grid)).toContainText('num_digits');
            await page.keyboard.press('Escape');
            await expect(editor(grid)).toHaveCount(0);
        });
    });
}

// ---------------------------------------------------------------------------------------------
// Point (DC-19, DC-20, DC-31, DC-34)

test('DC-19: = ↓ ↓ points at F4, Shift+arrows extend, the Selection and the Focus stay put', async ({ page }) => {
    const grid = sheet(page);
    await clickCell(grid, 'F2');
    await page.keyboard.type('=');
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('ArrowDown');
    await expect(editor(grid)).toHaveValue('=F4');
    await expect(bar(grid)).toHaveValue('=F4');
    const point = grid.locator('.ex-selection .ex-point');
    await expectCovers(point, grid, 'F4', 'F4');
    // The Focus is still the cell being edited; the Name Box names the pointed cell, as Excel's
    // does (ADR-0051, observed 2026-09-27).
    await expect(nameBox(grid)).toHaveValue('F4');
    await expect(grid).toHaveAttribute('aria-activedescendant', /-r1c5$/);
    await expectCovers(grid.locator('.ex-selection .ex-focus'), grid, 'F2', 'F2');

    // Shift+↓ and Shift+→ extend the outline, and the Reference follows it.
    await page.keyboard.press('Shift+ArrowDown');
    await expect(editor(grid)).toHaveValue('=F4:F5');
    await page.keyboard.press('Shift+ArrowRight');
    await expect(editor(grid)).toHaveValue('=F4:G5');
    await expectCovers(point, grid, 'F4', 'G5');
    // The input selected no text: the whole Reference is there and the caret after it.
    await expect.poll(() => caret(editor(grid))).toBe(6);

    // An operator ends pointing; the next arrow points afresh after it.
    await page.keyboard.type('+');
    await expect(point).toHaveCount(0);
    await expect(nameBox(grid)).toHaveValue('F2');
    await page.keyboard.press('ArrowRight');
    await expect(editor(grid)).toHaveValue('=F4:G5+G2');
    // Enter commits the Formula into F2, where the edit began.
    await page.keyboard.press('Enter');
    await expect(editor(grid)).toHaveCount(0);
    await clickCell(grid, 'F2');
    await expect(bar(grid)).toHaveValue('=F4:G5+G2');
});

test('DC-19: a click points at the clicked cell; Shift+click extends', async ({ page }) => {
    const grid = sheet(page);
    await clickCell(grid, 'F2');
    await page.keyboard.type('=');
    await clickCell(grid, 'C3');
    await expect(editor(grid)).toHaveValue('=C3');
    await expectCovers(grid.locator('.ex-selection .ex-point'), grid, 'C3', 'C3');
    await expect(nameBox(grid)).toHaveValue('C3');
    await clickCell(grid, 'C4', { modifiers: ['Shift'] });
    await expect(editor(grid)).toHaveValue('=C3:C4');
    await page.keyboard.type('*');
    await clickCell(grid, 'B2');
    await expect(editor(grid)).toHaveValue('=C3:C4*B2');
    await expect(editor(grid)).toBeFocused();
    await page.keyboard.press('Escape');
    await expect(editor(grid)).toHaveCount(0);
});

test('DC-19: F2 switches from pointing to moving the caret', async ({ page }) => {
    const grid = sheet(page);
    await clickCell(grid, 'F2');
    await page.keyboard.type('=');
    await page.keyboard.press('ArrowDown');
    await expect(editor(grid)).toHaveValue('=F3');
    await expect(grid.locator('.ex-point')).toHaveCount(1);
    // F2: Caret. ← and → move the caret and point at nothing; the edit stays open where it was.
    await page.keyboard.press('F2');
    await page.keyboard.press('ArrowLeft');
    await expect.poll(() => caret(editor(grid))).toBe(2);
    await page.keyboard.press('ArrowLeft');
    await expect.poll(() => caret(editor(grid))).toBe(1);
    await page.keyboard.press('ArrowRight');
    await expect.poll(() => caret(editor(grid))).toBe(2);
    await expect(editor(grid)).toHaveValue('=F3');
    await expect(grid.locator('.ex-point')).toHaveCount(0);
    await expect(nameBox(grid)).toHaveValue('F2');
    // F2 back, with the caret after the =, points again (the other way is DC-31's test below).
    await page.keyboard.press('Escape');
    await expect(editor(grid)).toHaveCount(0);
});

test('DC-31: a Reference written mid-text lands at the caret and the caret sits after it', async ({ page }) => {
    const grid = sheet(page);
    await clickCell(grid, 'F2');
    await page.keyboard.type('=+1');
    // F2 into Caret, the caret back to just after the =.
    await page.keyboard.press('F2');
    await page.keyboard.press('ArrowLeft');
    await page.keyboard.press('ArrowLeft');
    await expect.poll(() => caret(editor(grid))).toBe(1);
    // F2 again: pointing, since a Reference can go after the =.
    await page.keyboard.press('F2');
    await page.keyboard.press('ArrowDown');
    await expect(editor(grid)).toHaveValue('=F3+1');
    await expect.poll(() => caret(editor(grid))).toBe(3);
    await page.keyboard.press('ArrowDown');
    await expect(editor(grid)).toHaveValue('=F4+1');
    await expect.poll(() => caret(editor(grid))).toBe(3);
    await page.keyboard.press('Escape');
});

for (const chrome of ['builtin', 'mud']) {
    test(`DC-19/DC-34: pointing from the Formula Bar; a press into the bar keeps its caret; moving the caret enters Caret (${chrome} Chrome)`, async ({ page }) => {
        // Under ExGrid.MudBlazor's Chrome a press into the bar once opened the edit with DOM
        // focus in the Cell Editor, not the bar (found by this suite, 2026-09-27): MudCellEditor
        // mounted with no request seen yet and took any FocusRequest as a new one. An edit the
        // bar opens now hands the cell's editor a request of zero, which asks nothing
        // (CellEditorContext.FocusRequest, ADR-0051/0030).
        await underChrome(page, chrome);
        const grid = sheet(page);
        // D2 holds =B2*C2. A press near the start of the bar's text leaves the caret there.
        await pressCell(grid, 'D2');
        const field = await boxOf(bar(grid));
        await bar(grid).click({ position: { x: 6, y: field.height / 2 } });
        await expect(editor(grid)).toHaveValue('=B2*C2');
        const at = await caret(bar(grid));
        expect(at).toBeLessThan(3);

        // From the bar, pointing works as it does from the cell: after an operator typed at the
        // end, ↓ writes a Reference into both surfaces while the bar keeps the keyboard.
        await page.keyboard.press('Escape');
        await pressCell(grid, 'F2');
        await clickBarEnd(grid);
        await typeSteadily(page, bar(grid), '=SUM(');
        // A press into the bar opens Caret (ADR-0051), where arrows move the caret; F2 points.
        await page.keyboard.press('F2');
        await page.keyboard.press('ArrowDown');
        await expect(bar(grid)).toHaveValue('=SUM(F3');
        await expect(editor(grid)).toHaveValue('=SUM(F3');
        await expect(bar(grid)).toBeFocused();
        await expectCovers(grid.locator('.ex-selection .ex-point'), grid, 'F3', 'F3');

        // A press in the text while pointing moves the caret there, ends pointing and enters Caret
        // (ADR-0051, third round): the outline goes, and ↓ then points at nothing.
        await bar(grid).click({ position: { x: 6, y: field.height / 2 } });
        await expect(grid.locator('.ex-point')).toHaveCount(0);
        await expect.poll(() => caret(bar(grid))).toBeLessThan(3);
        await page.keyboard.press('ArrowDown');
        await expect(bar(grid)).toHaveValue('=SUM(F3');
        await expect(grid.locator('.ex-point')).toHaveCount(0);
        await expect(editor(grid)).toHaveCount(1);
        await page.keyboard.press('Escape');
        await expect(editor(grid)).toHaveCount(0);
    });
}

// A Reference written by pointing is followed by the core placing the caret after it, a round trip
// after the render that carries the text on a circuit. A press in the text in that gap is the
// user's and newer: it ends pointing and its caret stands (ADR-0051, third round). Found on the
// Server host (Windows, fourth run, DC-19/DC-34 above, 5 of 10): the late placement put the caret
// back after the Reference, the core had set the press's report aside as the browser's own caret,
// and pointing went on.
for (const chrome of ['builtin', 'mud']) {
    test(`DC-19/DC-34: a press in the Formula Bar's text before a 150 ms circuit has placed the caret after a pointed Reference ends pointing (${chrome} Chrome)`, async ({ page }) => {
        await underChrome(page, chrome);
        const grid = sheet(page);
        await pressCell(grid, 'F2');
        await clickBarEnd(grid);
        await typeSteadily(page, bar(grid), '=SUM(');
        await page.keyboard.press('F2');
        const field = await boxOf(bar(grid));
        await setRoundTrip(150);
        await page.keyboard.press('ArrowDown');
        await expect(bar(grid)).toHaveValue('=SUM(F3');
        // No wait for the placement: the press follows what the user sees.
        await bar(grid).click({ position: { x: 6, y: field.height / 2 } });
        await expect(grid.locator('.ex-point')).toHaveCount(0);
        await expect.poll(() => caret(bar(grid))).toBeLessThan(3);
        // Long past the placement's round trip, the caret is still where the press put it.
        await page.waitForTimeout(500);
        expect(await caret(bar(grid))).toBeLessThan(3);
        await expect(grid.locator('.ex-point')).toHaveCount(0);
        await page.keyboard.press('ArrowDown');
        await expect(bar(grid)).toHaveValue('=SUM(F3');
        await expect(grid.locator('.ex-point')).toHaveCount(0);
        await page.keyboard.press('Escape');
        await expect(editor(grid)).toHaveCount(0);
        await setRoundTrip(0);
    });
}

// A press into the Formula Bar opens an edit (ADR-0051) — a change of editing mode, which the
// key gate hears a round trip later on a circuit. Found on the Server host (2026-09-27, Windows,
// second run, DC-19/DC-34; reproduced on Linux at 120 ms): F2 and ↓ typed in that gap were
// gated against "not editing", where the bar's keys are the browser's, so F2 did nothing and ↓
// only moved the caret — nothing pointed. The keys typed after the press are held until the
// core has answered it, as after any change of mode (ADR-0010).
for (const chrome of ['builtin', 'mud']) {
    test(`DC-19/ADR-0010: keys typed into the Formula Bar before a 150 ms circuit has answered the press into it keep their meaning (${chrome} Chrome)`, async ({ page }) => {
        await underChrome(page, chrome);
        const grid = sheet(page);
        await pressCell(grid, 'F2');
        await expect(bar(grid)).toHaveValue('');
        await setRoundTrip(150);
        await clickBarEnd(grid);
        // No wait for anything: the keys follow the press as a user's do.
        await page.keyboard.type('=SUM(');
        await page.keyboard.press('F2');
        await page.keyboard.press('ArrowDown');
        await expect(bar(grid)).toHaveValue('=SUM(F3');
        await expect(editor(grid)).toHaveValue('=SUM(F3');
        await expect(bar(grid)).toBeFocused();
        await expectCovers(grid.locator('.ex-selection .ex-point'), grid, 'F3', 'F3');
        // Escape typed straight after a press cancels the edit that press opened.
        await page.keyboard.press('Escape');
        await expect(editor(grid)).toHaveCount(0);
        await clickBarEnd(grid);
        await page.keyboard.type('9');
        await page.keyboard.press('Escape');
        await expect(editor(grid)).toHaveCount(0);
        await expect(grid).toBeFocused();
        await expect(cell(grid, 'F2')).toHaveText('');
        await expect(bar(grid)).toHaveValue('');
        // A press into the bar while a key is still held — a character typed onto the cell,
        // which opens an edit there — takes its place behind it: the edit stays one text, and
        // the key typed after the press lands in the bar.
        await page.keyboard.type('x');
        await clickBarEnd(grid);
        await page.keyboard.type('5');
        await expect(bar(grid)).toHaveValue('x5');
        await expect(editor(grid)).toHaveValue('x5');
        await expect(bar(grid)).toBeFocused();
        await page.keyboard.press('Enter');
        await expect(cell(grid, 'F2')).toHaveText('x5');
        await expectFocusAt(grid, 'F3');
        await setRoundTrip(0);
    });
}

// A press on the rows asks for the keyboard back at the root, and on a circuit that request
// lands a round trip late — after a press into the Formula Bar or the Name Box that followed it.
// Before ADR-0021's narrowed hand-back (2026-09-28) it took the keyboard from the field the user
// had just pressed, and the keys typed there went to the grid instead (found on the Server host,
// Windows, second run). The fields beside the rows keep their focus. The bar's press reaches the
// core ahead of the row press held before it, which ends the edit that press joined: in its
// turn among the held keys the press is answered again, and the key typed after it edits the
// cell the row press moved to, in the bar.
for (const chrome of ['builtin', 'mud']) {
    test(`ADR-0021: a row press's late hand-back does not take the keyboard from the Formula Bar or the Name Box pressed after it, on a 150 ms circuit (${chrome} Chrome)`, async ({ page }) => {
        await underChrome(page, chrome);
        const grid = sheet(page);
        await pressCell(grid, 'F2');
        await expect(bar(grid)).toHaveValue('');
        await setRoundTrip(150);
        // No wait for anything: a character onto the cell opens an edit there, a press on
        // another row commits it, and the press into the bar follows as a user's does.
        await page.keyboard.type('x');
        await clickCell(grid, 'F5');
        await clickBarEnd(grid);
        await page.keyboard.type('7');
        await expect(bar(grid)).toHaveValue('7');
        await expect(editor(grid)).toHaveValue('7');
        await expect(bar(grid)).toBeFocused();
        await page.keyboard.press('Enter');
        await expect(cell(grid, 'F2')).toHaveText('x');
        await expect(cell(grid, 'F5')).toHaveText('7');
        await expectFocusAt(grid, 'F6');
        // The same for the Name Box, pressed straight after a row that nothing held: the row's
        // focus asks for the keyboard back a round trip later. The Name Box's press is not held
        // among the keys, so the render answering the row press can still rewrite its text:
        // what is typed waits until the round trips have landed — the hand-back among them —
        // and the Name Box has kept the keyboard through them.
        await clickCell(grid, 'F8');
        await nameBox(grid).click();
        await expect(nameBox(grid)).toHaveValue('F8');
        await page.waitForTimeout(600);
        await expect(nameBox(grid)).toBeFocused();
        await page.keyboard.press('ControlOrMeta+A');
        await page.keyboard.type('D4');
        await expect(nameBox(grid)).toHaveValue('D4');
        await page.keyboard.press('Enter');
        await expectFocusAt(grid, 'D4');
        await expect(grid).toBeFocused();
        await setRoundTrip(0);
    });
}

test('DC-20: typed quickly on a 150 ms circuit, no arrow points where the text forbids it', async ({ page }) => {
    const grid = sheet(page);
    await clickCell(grid, 'F2');
    await setRoundTrip(150);
    // After an operand, ↓ commits and moves (Overwrite), even though the answer for "=" — where
    // it would point — is the last one the circuit has returned when the arrow is pressed.
    await page.keyboard.type('=1');
    await page.keyboard.press('ArrowDown');
    await expect(editor(grid)).toHaveCount(0);
    await expect(cell(grid, 'F2')).toHaveText('1');
    await expectFocusAt(grid, 'F3');
    // After an operator, ↓ points, typed just as fast: F4, and the 2 typed after it makes F42.
    await page.keyboard.type('=1+');
    await page.keyboard.press('ArrowDown');
    await page.keyboard.type('2');
    await page.keyboard.press('Enter');
    // Enter committed and moved: until it has, a press on F3 would point at it.
    await expectFocusAt(grid, 'F4');
    await expect(editor(grid)).toHaveCount(0);
    await clickCell(grid, 'F3');
    await expect(bar(grid)).toHaveValue('=1+F42');
});

test('DC-28: keys typed in the Formula Bar behind F2 on a 150 ms circuit land in the bar, in order', async ({ page }) => {
    const grid = sheet(page);
    await pressCell(grid, 'F2');
    await clickBarEnd(grid);
    await typeSteadily(page, bar(grid), '=+1');
    await expect(editor(grid)).toHaveValue('=+1');
    // The caret back after the =, in the bar: the cell's surface would have its caret elsewhere.
    await page.keyboard.press('ArrowLeft');
    await page.keyboard.press('ArrowLeft');
    await expect.poll(() => caret(bar(grid))).toBe(1);
    await setRoundTrip(150);
    // F2 is a mode change (Caret → Point): a round trip on a circuit. The keys typed behind it
    // are held, then typed into the surface that holds DOM focus — the bar, at its caret.
    await page.keyboard.press('F2');
    await page.keyboard.type('7*');
    await expect(bar(grid)).toHaveValue('=7*+1');
    await expect(editor(grid)).toHaveValue('=7*+1');
    await setRoundTrip(0);
    await page.keyboard.press('Escape');
    await expect(editor(grid)).toHaveCount(0);
});

// What DC-28 is for: the user goes on typing where the held keys left the caret. Found by this
// suite on the Server host with 150 ms (2026-09-27): every render answering one of the held
// keys writes that key's text back into the surface — first a text the user has already typed
// past (=7+1 over =7*+1), then the current one — and each write puts the browser's caret at the
// end. The characters held behind F2 land in order, but the next one lands at the end of the
// text: =7*+12 where the user typed =7*2+1. The same happens to any two characters typed
// mid-text within one round trip, in the Cell Editor as in the Formula Bar (abXYcdZ for
// ab|cd + XY + Z). Left failing on the Server host, by name, until the core stops writing a
// surface's own typing back into it (ADR-0051, ED-22).
test('DC-28/ED-22: after keys held behind F2 on a 150 ms circuit, the next key lands at the caret they left', async ({ page }) => {
    const grid = sheet(page);
    await pressCell(grid, 'F2');
    await clickBarEnd(grid);
    await typeSteadily(page, bar(grid), '=+1');
    await expect(editor(grid)).toHaveValue('=+1');
    await page.keyboard.press('ArrowLeft');
    await page.keyboard.press('ArrowLeft');
    await expect.poll(() => caret(bar(grid))).toBe(1);
    await setRoundTrip(150);
    await page.keyboard.press('F2');
    await page.keyboard.type('7*');
    await expect(bar(grid)).toHaveValue('=7*+1');
    // Every answer has landed: the text in both surfaces is the one typed.
    await expect(editor(grid)).toHaveValue('=7*+1');
    await page.waitForTimeout(1000);
    await expect.poll(() => caret(bar(grid))).toBe(3);
    await page.keyboard.type('2');
    await expect(bar(grid)).toHaveValue('=7*2+1');
    await setRoundTrip(0);
    await page.keyboard.press('Escape');
    await expect(editor(grid)).toHaveCount(0);
});

// ---------------------------------------------------------------------------------------------
// F4 cycles the Reference at the caret (DC-45; ADR-0051, 2026-09-29). Where each edge case lands
// is SH-28's, pinned in layer 1; what is asked here is that the real key reaches the grid only
// while an edit is open, that both surfaces show the rewrite and the caret lands after it, and
// that a burst on a circuit is decided press by press from the text each key carries.

const FORMS = ['=$B$2', '=B$2', '=$B2', '=B2'];
const selectionOf = (locator) => locator.evaluate((input) => [input.selectionStart, input.selectionEnd]);

/**
 * Records every text Blazor writes into the Sheet's Cell Editor from here on, however close
 * together — a value set by a render fires no event, and two can land within one frame — by
 * wrapping the one field's own value setter. The field is the test's own grid's; the wrapper is
 * taken off as the test ends.
 */
async function recordEditorWrites(page) {
    await alterPage(page, (selector) => {
        const input = document.querySelector(selector);
        const native = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value');
        const written = [];
        Object.defineProperty(input, 'value', {
            configurable: true,
            get() { return native.get.call(this); },
            set(value) { written.push(value); native.set.call(this, value); },
        });
        window.__editorWrites = written;
        return () => {
            delete input.value;
            delete window.__editorWrites;
        };
    }, '.ex-grid:has(> .ex-formula-bar) .ex-viewport input.ex-editor');
}

/** The texts written since recordEditorWrites, a repeat of the one before it dropped. */
const editorWrites = (page) => page.evaluate(() => window.__editorWrites.filter((text, i, all) => text !== all[i - 1]));

for (const chrome of ['builtin', 'mud']) {
    test(`DC-45: =B2 and four F4 presses in a cell give $B$2, B$2, $B2 and B2, the caret after each (${chrome} Chrome)`, async ({ page }) => {
        await underChrome(page, chrome);
        const grid = sheet(page);
        await pressCell(grid, 'F2');
        await page.keyboard.type('=B2');
        await expect(editor(grid)).toHaveValue('=B2');

        for (const form of FORMS) {
            await page.keyboard.press('F4');
            await expect(editor(grid)).toHaveValue(form);
            await expect(bar(grid)).toHaveValue(form);
            await expect.poll(() => caret(editor(grid))).toBe(form.length);
        }
        await expect(editor(grid)).toBeFocused();

        // What F4 wrote is what Enter commits.
        await page.keyboard.press('F4');
        await expect(editor(grid)).toHaveValue('=$B$2');
        await page.keyboard.press('Enter');
        await expect(editor(grid)).toHaveCount(0);
        await pressCell(grid, 'F2');
        await expect(bar(grid)).toHaveValue('=$B$2');
    });

    test(`DC-45: =B2 and four F4 presses in the Formula Bar give the same four forms, and the bar keeps the keyboard (${chrome} Chrome)`, async ({ page }) => {
        await underChrome(page, chrome);
        const grid = sheet(page);
        await pressCell(grid, 'F2');
        await clickBarEnd(grid);
        await typeSteadily(page, bar(grid), '=B2');

        for (const form of FORMS) {
            await page.keyboard.press('F4');
            await expect(bar(grid)).toHaveValue(form);
            await expect(editor(grid)).toHaveValue(form);
            await expect.poll(() => caret(bar(grid))).toBe(form.length);
        }
        await expect(bar(grid)).toBeFocused();
        await page.keyboard.press('Escape');
        await expect(editor(grid)).toHaveCount(0);
    });
}

test('DC-45: with no edit open, F4 is left to the browser and opens nothing', async ({ page }) => {
    const grid = sheet(page);
    await pressCell(grid, 'D2');
    // Prevented once the listener has seen it, so the browser does nothing with it either.
    await watchNextKey(page, 'F4', { preventAfter: true });

    await page.keyboard.press('F4');

    await expect.poll(() => keySeenUntouched(page), 'F4 reached the page untaken').toBe(true);
    await expect(editor(grid)).toHaveCount(0);
    await expectFocusAt(grid, 'D2');
    await expect(bar(grid)).toHaveValue('=B2*C2');
});

test('DC-45: F4 cycles the Reference the caret touches mid-text, and every Reference a selection covers', async ({ page }) => {
    const grid = sheet(page);
    await pressCell(grid, 'F2');
    await clickBarEnd(grid);
    await typeSteadily(page, bar(grid), '=A1+B2');
    // A press into the bar opens Caret: ← is the input's, and moves the caret to just after A1.
    for (let i = 0; i < 3; i++) {
        await page.keyboard.press('ArrowLeft');
    }
    await expect.poll(() => caret(bar(grid))).toBe(3);

    await page.keyboard.press('F4');
    await expect(bar(grid)).toHaveValue('=$A$1+B2');
    await expect.poll(() => caret(bar(grid))).toBe(5);

    // The whole text selected: both References go to the next form of the first, and the
    // selection covers what was rewritten.
    await page.keyboard.press('End');
    await page.keyboard.press('Shift+Home');
    await expect.poll(() => selectionOf(bar(grid))).toEqual([0, 8]);
    await page.keyboard.press('F4');
    await expect(bar(grid)).toHaveValue('=A$1+B$2');
    await expect(editor(grid)).toHaveValue('=A$1+B$2');
    await expect.poll(() => selectionOf(bar(grid))).toEqual([1, 8]);
    await page.keyboard.press('Escape');
    await expect(editor(grid)).toHaveCount(0);
});

test('DC-45: while pointing, F4 cycles the pointed Reference and pointing goes on; the next arrow writes the relative form', async ({ page }) => {
    const grid = sheet(page);
    await pressCell(grid, 'F2');
    await page.keyboard.type('=');
    await page.keyboard.press('ArrowDown');
    await expect(editor(grid)).toHaveValue('=F3');
    const point = grid.locator('.ex-selection .ex-point');

    await page.keyboard.press('F4');
    await expect(editor(grid)).toHaveValue('=$F$3');
    await expectCovers(point, grid, 'F3', 'F3');
    await expect(nameBox(grid)).toHaveValue('F3');
    await expect.poll(() => caret(editor(grid))).toBe(5);

    // The outline moves on, and writes its Reference as pointing writes it (ADR-0051's reading;
    // whether Excel keeps the $ form is asked in the sixth Windows run).
    await page.keyboard.press('ArrowDown');
    await expect(editor(grid)).toHaveValue('=F4');
    await expectCovers(point, grid, 'F4', 'F4');
    await page.keyboard.press('Escape');
    await expect(editor(grid)).toHaveCount(0);
});

// On a circuit each F4 is answered a round trip later, and the keys behind it are held until it
// is (ADR-0010), so each press carries the text the one before it left — never a text a round
// trip old. On WebAssembly the same burst runs as the case without a round trip.
test('DC-45: on a 150 ms circuit, four F4 presses in a burst give the four forms in order', async ({ page }) => {
    const grid = sheet(page);
    await pressCell(grid, 'F2');
    await page.keyboard.type('=B2');
    await expect(editor(grid)).toHaveValue('=B2');
    await recordEditorWrites(page);
    await setRoundTrip(150);

    for (let i = 0; i < 4; i++) {
        await page.keyboard.press('F4');
    }

    await expect.poll(async () => {
        const writes = await editorWrites(page);
        return writes.slice(writes.indexOf(FORMS[0]));
    }).toEqual(FORMS);
    await expect(editor(grid)).toHaveValue('=B2');
    await expect.poll(() => caret(editor(grid))).toBe(3);
    await setRoundTrip(0);
    await page.keyboard.press('Escape');
    await expect(editor(grid)).toHaveCount(0);
});

// The caret after a rewrite is placed a round trip after the render that carries the text, and
// setting the value leaves the browser's own caret at the end meanwhile. A second F4 in that gap
// carries that caret, which is not the user's: the Reference the first one rewrote cycles again,
// not the one at the end of the text.
test('DC-45: on a 150 ms circuit, a second F4 before the caret is placed cycles the same Reference', async ({ page }) => {
    const grid = sheet(page);
    await pressCell(grid, 'F2');
    await clickBarEnd(grid);
    await typeSteadily(page, bar(grid), '=A1+B2');
    for (let i = 0; i < 3; i++) {
        await page.keyboard.press('ArrowLeft');
    }
    await expect.poll(() => caret(bar(grid))).toBe(3);
    await setRoundTrip(150);

    await page.keyboard.press('F4');
    await page.keyboard.press('F4');

    await expect(bar(grid)).toHaveValue('=A$1+B2');
    await expect.poll(() => caret(bar(grid))).toBe(4);
    // Long past the last placement's round trip, nothing else has changed.
    await page.waitForTimeout(500);
    await expect(bar(grid)).toHaveValue('=A$1+B2');
    expect(await caret(bar(grid))).toBe(4);
    await setRoundTrip(0);
    await page.keyboard.press('Escape');
    await expect(editor(grid)).toHaveCount(0);
});

// ---------------------------------------------------------------------------------------------
// The fill handle (DC-13, DC-27)

async function dragHandle(page, grid, from, to) {
    // The handle stands at the bottom-right of the Selection's last range once the render that
    // placed the Selection has landed; grabbed before that, it would be the old Selection's.
    const corner = await boxOf(cell(grid, from));
    await expect.poll(async () => {
        const box = await grid.locator('.ex-fill-handle').boundingBox();
        return box !== null
            && Math.abs(box.x + box.width / 2 - (corner.x + corner.width)) <= 2
            && Math.abs(box.y + box.height / 2 - (corner.y + corner.height)) <= 2;
    }).toBe(true);
    const handle = await boxOf(grid.locator('.ex-fill-handle'));
    const target = await boxOf(cell(grid, to));
    await page.mouse.move(handle.x + handle.width / 2, handle.y + handle.height / 2);
    await page.mouse.down();
    // A few steps, as a hand moves; slightly off the axis, which the core ignores.
    const toX = target.x + target.width / 2;
    const toY = target.y + target.height / 2;
    await page.mouse.move(toX + 3, toY, { steps: 8 });
    await expect(grid.locator('.ex-fill-target')).toHaveCount(1);
    await page.mouse.up();
}

for (const chrome of ['builtin', 'mud']) {
    test(`DC-13/DC-27: dragging the fill handle continues a series; the Selection is source and target (${chrome} Chrome)`, async ({ page }) => {
        await underChrome(page, chrome);
        const grid = sheet(page);
        await enter(page, grid, 'F2', '1');
        await enter(page, grid, 'F3', '2');
        await clickCell(grid, 'F2');
        await clickCell(grid, 'F3', { modifiers: ['Shift'] });
        // The handle is painted at the bottom-right of the Selection, in the overlay (dragHandle
        // waits for it there, and grabs it where it is painted).
        await dragHandle(page, grid, 'F3', 'F6');
        await expect(cell(grid, 'F4')).toHaveText('3');
        await expect(cell(grid, 'F5')).toHaveText('4');
        await expect(cell(grid, 'F6')).toHaveText('5');
        // Only the one axis: nothing was written beside the column.
        await expect(cell(grid, 'G4')).toHaveText('');
        await expectCovers(grid.locator('.ex-selection .ex-range'), grid, 'F2', 'F6');
        await expect(grid.locator('.ex-fill-target')).toHaveCount(0);

        // A fill is one step: one Ctrl+Z takes it all back, the Selection is where the user left it.
        await page.keyboard.press('ControlOrMeta+Z');
        await expect(cell(grid, 'F4')).toHaveText('');
        await expect(cell(grid, 'F6')).toHaveText('');
        await expect(cell(grid, 'F3')).toHaveText('2');
    });
}

test('DC-13: the edge auto-scroll carries a fill past the bottom of the Viewport', async ({ page }) => {
    const grid = sheet(page);
    await enter(page, grid, 'E2', '1');
    await enter(page, grid, 'E3', '2');
    await clickCell(grid, 'E2');
    await clickCell(grid, 'E3', { modifiers: ['Shift'] });
    const corner = await boxOf(cell(grid, 'E3'));
    await expect.poll(async () => {
        const box = await grid.locator('.ex-fill-handle').boundingBox();
        return box !== null && Math.abs(box.y + box.height / 2 - (corner.y + corner.height)) <= 2;
    }).toBe(true);
    const handle = await boxOf(grid.locator('.ex-fill-handle'));
    const scroller = await boxOf(grid.locator('.ex-scroller'));
    await page.mouse.move(handle.x + handle.width / 2, handle.y + handle.height / 2);
    await page.mouse.down();
    // A row down first: the fill drag has begun once its target outline is painted.
    await page.mouse.move(handle.x + handle.width / 2, handle.y + 28, { steps: 3 });
    await expect(grid.locator('.ex-fill-target')).toHaveCount(1);
    // Held in the band at the Viewport's bottom edge (ADR-0008): the rows scroll under the
    // pointer and the target follows them.
    await page.mouse.move(handle.x + handle.width / 2, scroller.y + scroller.height - 22, { steps: 10 });
    // Read as the scroll offset: the band scrolls up to eight rows a tick, so any one row
    // passes through the Viewport faster than a locator polls for it.
    await expect.poll(() => grid.locator('.ex-scroller').evaluate((el) => el.scrollTop), { timeout: 10_000 })
        .toBeGreaterThan(30 * 28);
    await page.mouse.up();
    await expect(grid.locator('.ex-fill-target')).toHaveCount(0);
    // The series runs from E2 to wherever the drag was released, one number per row, and the
    // Selection is all of it: Ctrl+↓ from E2 finds its end.
    await goTo(grid, 'E2');
    await page.keyboard.press('ControlOrMeta+ArrowDown');
    // Waited for, not read once: the Focus moves when the key's answer lands, and on a circuit
    // that is a round trip — through the Consumer's edge answer (ADR-0050) — after the press
    // returns. Read at once, the Name Box still said E2 (the fifth Windows run, Server host).
    await expect.poll(async () => Number((await nameBox(grid).inputValue()).slice(1)))
        .toBeGreaterThan(20);
    const end = await nameBox(grid).inputValue();
    const last = Number(end.slice(1));
    await expectFocusAt(grid, end);
    await expect(cell(grid, end)).toHaveText(String(last - 1));
});

test('DC-13: a Formula filled right shifts its References; a refused pattern leaves the Selection on the source', async ({ page }) => {
    const grid = sheet(page);
    // D2 =B2*C2 filled down to D2:D4's neighbours is already there; fill it right into E and F.
    await clickCell(grid, 'D2');
    await dragHandle(page, grid, 'D2', 'F2');
    await clickCell(grid, 'E2');
    await expect(bar(grid)).toHaveValue('=C2*D2');
    await clickCell(grid, 'F2');
    await expect(bar(grid)).toHaveValue('=D2*E2');

    // Text Excel would continue as a series (Item 3, Item 4) is a pattern ExSheet does not have:
    // refused, said so, nothing written — never copies — and the Selection
    // stays on the source.
    await enter(page, grid, 'E6', 'Item 1');
    await enter(page, grid, 'E7', 'Item 2');
    await clickCell(grid, 'E6');
    await clickCell(grid, 'E7', { modifiers: ['Shift'] });
    await dragHandle(page, grid, 'E7', 'E9');
    await expect(cell(grid, 'E8')).toHaveText('');
    await expectCovers(grid.locator('.ex-selection .ex-range'), grid, 'E6', 'E7');
    await expect(page.locator('.ex-sheet-notice')).not.toBeEmpty();
});

// ---------------------------------------------------------------------------------------------
// The clipboard (DC-8, DC-33, SH-14)

test('DC-8: a block pasted from the real clipboard onto one cell spills, and becomes the Selection', async ({ page }) => {
    const grid = sheet(page);
    await page.evaluate(() => navigator.clipboard.writeText('1\t2\r\n3\t=F2+G2\r\n'));
    await clickCell(grid, 'F2');
    await page.keyboard.press('ControlOrMeta+V');
    await expect(cell(grid, 'F2')).toHaveText('1');
    await expect(cell(grid, 'G2')).toHaveText('2');
    await expect(cell(grid, 'F3')).toHaveText('3');
    // Shown text is read as typed, so the Formula is a Formula.
    await expect(cell(grid, 'G3')).toHaveText('3');
    await expectCovers(grid.locator('.ex-selection .ex-range'), grid, 'F2', 'G3');
    await expect(nameBox(grid)).toHaveValue('F2');
    // One paste, one step.
    await page.keyboard.press('ControlOrMeta+Z');
    await expect(cell(grid, 'F2')).toHaveText('');
    await expect(cell(grid, 'G3')).toHaveText('');
});

// An edit that ends leaves a collapsed caret in the document where its field stood, while the
// root holds DOM focus again; the next press on the rows moves that caret to the nearest text
// the page can select, outside the grid. The browser aims Ctrl+C and Ctrl+V at the selection,
// not at the focused root, so after the first edit every copy and paste went past the grid until
// DOM focus left it and came back (found on /sheet, 2026-09-29). Every way out of an edit, under
// both Chromes, followed by a copy and a paste by keyboard.
for (const chrome of ['builtin', 'mud']) {
    test(`CP-6/CP-10/CP-14: Ctrl+C and Ctrl+V reach the grid after every way out of an edit (${chrome} Chrome)`, async ({ page }) => {
        await underChrome(page, chrome);
        const grid = sheet(page);
        const copied = async (address, text) => {
            await page.evaluate(() => navigator.clipboard.writeText('SENTINEL'));
            await clickCell(grid, address);
            await page.keyboard.press('ControlOrMeta+C');
            // On the Server host every copy takes the asynchronous route (ADR-0005).
            await expect.poll(async () => ((await readClipboard(page))['text/plain'] ?? '').trimEnd(), { timeout: 5000 })
                .toBe(text);
        };
        // Before any edit: the copy route as it always was.
        await copied('B2', '12');

        // Enter in the Cell Editor commits and moves.
        await enter(page, grid, 'E1', '5');
        await expect(cell(grid, 'E1')).toHaveText('5');
        await expect(grid).toBeFocused();
        await copied('C2', '0.5');
        await copied('B3', '7');

        // Tab commits and moves right.
        await clickCell(grid, 'E2');
        await page.keyboard.type('6');
        await expect(editor(grid)).toHaveValue('6');
        await page.keyboard.press('Tab');
        await expect(editor(grid)).toHaveCount(0);
        await expect(cell(grid, 'E2')).toHaveText('6');
        await copied('B2', '12');

        // Escape cancels.
        await clickCell(grid, 'E3');
        await page.keyboard.type('7');
        await expect(editor(grid)).toHaveValue('7');
        await page.keyboard.press('Escape');
        await expect(editor(grid)).toHaveCount(0);
        await expect(cell(grid, 'E3')).toHaveText('');
        await copied('C2', '0.5');

        // Enter and Escape in the Formula Bar, whose field stays in the page.
        await clickCell(grid, 'E4');
        await clickBarEnd(grid);
        await typeSteadily(page, bar(grid), '8');
        await page.keyboard.press('Enter');
        await expect(editor(grid)).toHaveCount(0);
        await expect(cell(grid, 'E4')).toHaveText('8');
        await expect(grid).toBeFocused();
        await copied('B3', '7');
        await clickCell(grid, 'E5');
        await clickBarEnd(grid);
        await typeSteadily(page, bar(grid), '9');
        await page.keyboard.press('Escape');
        await expect(editor(grid)).toHaveCount(0);
        await expect(cell(grid, 'E5')).toHaveText('');
        await expect(grid).toBeFocused();
        await copied('B2', '12');

        // And a paste after an edit lands where the Selection is.
        await enter(page, grid, 'E6', '1');
        await page.evaluate(() => navigator.clipboard.writeText('42'));
        await clickCell(grid, 'F6');
        await page.keyboard.press('ControlOrMeta+V');
        await expect(cell(grid, 'F6')).toHaveText('42');
    });
}

// Excel's HTML flavour for one column of three cells, as a paste event on Windows Chrome hands
// it over (verification/2026-09-27-windows-excel-2/clipboard-probe-chrome.json): Excel's head
// and style sheet, each cell's shown text, no x:num.
const excelColumnHtml = (width, cells) => [
    '<html xmlns:v="urn:schemas-microsoft-com:vml"\r\nxmlns:o="urn:schemas-microsoft-com:office:office"\r\n',
    'xmlns:x="urn:schemas-microsoft-com:office:excel"\r\nxmlns="http://www.w3.org/TR/REC-html40">\r\n\r\n<head>\r\n',
    '<meta http-equiv=Content-Type content="text/html; charset=utf-8">\r\n<meta name=ProgId content=Excel.Sheet>\r\n',
    '<meta name=Generator content="Microsoft Excel 15">\r\n<style>\r\n<!--table\r\n\t{mso-displayed-decimal-separator:"\\.";\r\n',
    '\tmso-displayed-thousand-separator:"\\,";}\r\ntd\r\n\t{mso-number-format:General;\r\n\twhite-space:nowrap;}\r\n',
    '.xl65\r\n\t{mso-number-format:"Short Date";}\r\n.xl66\r\n\t{mso-number-format:Standard;}\r\n-->\r\n</style>\r\n</head>\r\n\r\n',
    '<body link="#467886" vlink="#96607D">\r\n\r\n',
    `<table border=0 cellpadding=0 cellspacing=0 width=${width} style='border-collapse:\r\n collapse'>\r\n<!--StartFragment-->\r\n`,
    ` <col width=${width} style='mso-width-source:userset'>\r\n`,
    ...cells.map((text, i) => ` <tr height=19 style='height:14.5pt'>\r\n  <td height=19${i ? ` class=xl6${4 + i}` : ''} align=right style='height:14.5pt'>${text}</td>\r\n </tr>\r\n`),
    '<!--EndFragment-->\r\n</table>\r\n\r\n</body>\r\n\r\n</html>\r\n',
].join('');

// A paste event carrying every flavour Excel's clipboard showed the page on Windows: text/plain
// with the values, text/html with the shown text, text/rtf, and a file (the picture of the
// range). Dispatched on the grid's focused root, where the browser's own paste lands.
async function pasteAsExcel(grid, cells, width) {
    await grid.evaluate((root, { text, html }) => {
        const data = new DataTransfer();
        data.setData('text/plain', text);
        data.setData('text/html', html);
        data.setData('text/rtf', '{\\rtf1\\ansi 6\\par}');
        data.items.add(new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47])], 'image.png', { type: 'image/png' }));
        root.dispatchEvent(new ClipboardEvent('paste', { clipboardData: data, bubbles: true, cancelable: true }));
    }, { text: '6\r\n26/09/2026\r\n1,234.50\r\n', html: excelColumnHtml(width, cells) });
}

test('DC-38/ADR-0048: Excel\'s too-narrow column pasted onto one cell is refused, the notice says why, and the Selection stays', async ({ page }) => {
    const grid = sheet(page);
    await clickCell(grid, 'F7');
    await pasteAsExcel(grid, ['6', '######', '######'], 49);
    const notice = page.locator('.ex-sheet-notice');
    await expect(notice).toContainText('source column was too narrow to show the value');
    await expect(notice).toContainText('F8');
    // The Sheet refused the spill (GridPasteIntent.Refuse), so the grid selects no block: the
    // Selection stays on F7, as Excel leaves it, and the notice stands.
    await page.waitForTimeout(500);
    await expect(notice).toContainText('too narrow');
    await expectFocusAt(grid, 'F7');
    await expectCovers(grid.locator('.ex-selection .ex-range'), grid, 'F7', 'F7');
    await expect(cell(grid, 'F7')).toHaveText('');
    await expect(cell(grid, 'F8')).toHaveText('');
    await expect(cell(grid, 'F9')).toHaveText('');
});

test('DC-8: Excel\'s column wide enough, with the same flavours, file and RTF included, pastes and spills', async ({ page }) => {
    const grid = sheet(page);
    await clickCell(grid, 'F7');
    await pasteAsExcel(grid, ['6', '26/09/2026', '1,234.50'], 74);
    await expect(cell(grid, 'F7')).toHaveText('6');
    await expect(cell(grid, 'F8')).not.toHaveText('');
    await expect(cell(grid, 'F9')).toHaveText('1,234.50');
    await expectCovers(grid.locator('.ex-selection .ex-range'), grid, 'F7', 'F9');
    await expect(page.locator('.ex-sheet-notice')).toBeEmpty();
});

// What the grid's own paste handler is given, read from a listener ahead of it: the
// ADR-0050 fourth-round question is what the browser hands a paste, not what a
// navigator.clipboard.read() — which sanitises again — would say.
async function capturePastes(page) {
    await alterPage(page, () => {
        window.__pastes = [];
        const listener = (event) => {
            window.__pastes.push({
                html: event.clipboardData.getData('text/html'),
                text: event.clipboardData.getData('text/plain'),
            });
        };
        document.addEventListener('paste', listener, true);
        return () => { document.removeEventListener('paste', listener, true); delete window.__pastes; };
    });
    return async () => page.evaluate(() => window.__pastes.at(-1));
}

test('SH-14/DC-33: a copy inside the Sheet carries Entries and shifts References; outward it carries Values', async ({ page }, testInfo) => {
    const grid = sheet(page);
    const lastPaste = await capturePastes(page);
    // Copy D2:D3 (=B2*C2, =B3*C3) by keyboard.
    await page.evaluate(() => navigator.clipboard.writeText('SENTINEL'));
    await clickCell(grid, 'D2');
    await clickCell(grid, 'D3', { modifiers: ['Shift'] });
    await page.keyboard.press('ControlOrMeta+C');
    // Outward: the Values, as shown, in both flavours.
    await expect.poll(async () => (await readClipboard(page))['text/plain'] ?? '', { timeout: 5000 }).toMatch(/^6\r?\n5\.25/);

    // Inward, two rows lower: the Entries, the relative References shifted.
    await clickCell(grid, 'F6');
    await page.keyboard.press('ControlOrMeta+V');
    await expect(cell(grid, 'F7')).not.toHaveText('');
    await clickCell(grid, 'F6');
    await expect(bar(grid)).toHaveValue('=D6*E6');
    await clickCell(grid, 'F7');
    await expect(bar(grid)).toHaveValue('=D7*E7');

    // The marker ExGrid writes on its own HTML (ADR-0050 fourth round), as the paste received it.
    const pasted = await lastPaste();
    const kept = /data-ex-grid="invariant"/.test(pasted?.html ?? '');
    const route = SERVER ? 'asynchronous (Server: navigator.clipboard.write)' : 'synchronous (copy event)';
    testInfo.annotations.push({ type: 'DC-33 marker', description: `${route}: ${kept ? 'kept' : 'stripped'}` });
    record(testInfo.project.name, { [`DC-33 marker, keyboard copy, ${SERVER ? 'server' : 'wasm'}`]: kept ? 'kept' : 'stripped' });
    console.log(`DC-33 marker on the ${route} route: ${kept ? 'kept' : 'stripped'}`);
    expect(pasted?.html ?? '').toContain('<table');
});

test('DC-33: whether the invariant marker survives the asynchronous clipboard route (a menu copy)', async ({ page }, testInfo) => {
    const grid = sheet(page);
    const lastPaste = await capturePastes(page);
    await page.evaluate(() => navigator.clipboard.writeText('SENTINEL'));
    await clickCell(grid, 'B2');
    await clickCell(grid, 'C3', { modifiers: ['Shift'] });
    // A copy invoked from a menu fires no copy event: it always takes navigator.clipboard.write,
    // on both hosts (ADR-0036).
    await cell(grid, 'B2').click({ force: true, button: 'right' });
    await page.getByRole('menuitem', { name: /^Copy$/ }).click();
    await expect.poll(async () => (await readClipboard(page))['text/plain'] ?? '', { timeout: 5000 }).toMatch(/^12\t0\.5/);
    // Pasted into a cell of another Sheet region: the paste is what the browser hands over.
    await clickCell(grid, 'F6');
    await page.keyboard.press('ControlOrMeta+V');
    await expect(cell(grid, 'F6')).toHaveText('12');
    await expect(cell(grid, 'G7')).toHaveText('0.75');
    const pasted = await lastPaste();
    const kept = /data-ex-grid="invariant"/.test(pasted?.html ?? '');
    testInfo.annotations.push({ type: 'DC-33 marker', description: `asynchronous (menu copy): ${kept ? 'kept' : 'stripped'}` });
    record(testInfo.project.name, { [`DC-33 marker, menu copy, ${SERVER ? 'server' : 'wasm'}`]: kept ? 'kept' : 'stripped' });
    console.log(`DC-33 marker on the asynchronous route (menu copy): ${kept ? 'kept' : 'stripped'}; html: ${(pasted?.html ?? '').slice(0, 300)}`);
    expect(pasted?.html ?? '').toContain('<table');
});

// ---------------------------------------------------------------------------------------------
// Undo and redo (DC-30)

test('DC-30: Ctrl+Z / Ctrl+Y / Ctrl+Shift+Z walk the Sheet\'s stack; with an edit open they are the editor\'s', async ({ page }) => {
    const grid = sheet(page);
    await enter(page, grid, 'F2', '5');
    await enter(page, grid, 'F3', '6');
    await page.keyboard.press('ControlOrMeta+Z');
    await expect(cell(grid, 'F3')).toHaveText('');
    await page.keyboard.press('ControlOrMeta+Z');
    await expect(cell(grid, 'F2')).toHaveText('');
    await page.keyboard.press('ControlOrMeta+Y');
    await expect(cell(grid, 'F2')).toHaveText('5');
    await page.keyboard.press('ControlOrMeta+Shift+Z');
    await expect(cell(grid, 'F3')).toHaveText('6');

    // With an edit open, the keys are the editor's: the core does not claim them, the Sheet's
    // stack is left alone, and the edit stays open.
    await clickCell(grid, 'F4');
    await page.keyboard.type('abc');
    await expect(editor(grid)).toHaveValue('abc');
    await page.keyboard.press('ControlOrMeta+Z');
    await page.keyboard.press('ControlOrMeta+Y');
    await page.keyboard.press('ControlOrMeta+Shift+Z');
    await expect(editor(grid)).toHaveCount(1);
    await expect(nameBox(grid)).toHaveValue('F4');
    await expect(cell(grid, 'F3')).toHaveText('6');
    await expect(cell(grid, 'F2')).toHaveText('5');
    await page.keyboard.press('Escape');
    await expect(editor(grid)).toHaveCount(0);
    await expect(cell(grid, 'F3')).toHaveText('6');
    await expect(cell(grid, 'F2')).toHaveText('5');
    // And the stack still walks from where it was.
    await page.keyboard.press('ControlOrMeta+Z');
    await expect(cell(grid, 'F3')).toHaveText('');
});

// ADR-0007's last bullet: the editor's own keys "undo uncommitted typing". The first key opens
// the editor on its character (a render writes that value); what is typed after it is the
// input's own history. On the Server host every input's render writes the surface's text back
// into it, which empties the browser's undo history (found by this suite, 2026-09-27; the same
// write-back that loses characters in the SRV-5 test below), so there Ctrl+Z undoes nothing.
test('DC-30/ADR-0007: with an edit open, Ctrl+Z undoes uncommitted typing', async ({ page }) => {
    const grid = sheet(page);
    await clickCell(grid, 'F4');
    await page.keyboard.type('abc');
    await expect(editor(grid)).toHaveValue('abc');
    // At an ordinary round trip, so that the renders answering these two inputs land after
    // both were typed, as they do on any real network; every answer is waited for.
    await setRoundTrip(150);
    await page.keyboard.type('de');
    await expect(editor(grid)).toHaveValue('abcde');
    await page.waitForTimeout(1000);
    await expect(editor(grid)).toHaveValue('abcde');
    await page.keyboard.press('ControlOrMeta+Z');
    await expect(editor(grid)).toHaveCount(1);
    await expect(editor(grid)).not.toHaveValue('abcde');
    await expect(editor(grid)).toHaveValue(/^abc/);
    await setRoundTrip(0);
    await page.keyboard.press('Escape');
});

// SRV-5 / ED-22 as the Sheet meets them: a Formula typed into an open editor at an ordinary
// typing speed, on a 150 ms circuit, arrives whole. Found by this suite (2026-09-27): each input
// raises a render, and each render writes the text as it stood at that input back into the
// field — over characters typed since. Characters vanish: `=SUM(1,2,3,4,5)` typed at 10 keys a
// second arrives as `=SM1234,)`; at 25 a second, `=S,,)`. The plain grid on /features loses
// them the same way (`Xabcdefghij` → `Xabdfhj`), so this is the core's editor, not ExSheet's.
// ED-22's own cases pass because every key there is held behind the key that opened the editor
// and replayed by the listener. Left failing on the Server host until the core stops writing a
// surface's own typing back into it.
test('SRV-5/ED-22: a Formula typed into an open editor at 10 keys a second on a 150 ms circuit loses nothing', async ({ page }) => {
    const grid = sheet(page);
    await clickCell(grid, 'F3');
    await page.keyboard.type('=');
    await expect(editor(grid)).toHaveValue('=');
    await setRoundTrip(150);
    await page.keyboard.type('SUM(1,2,3,4,5)', { delay: 100 });
    await expect(editor(grid)).toHaveValue('=SUM(1,2,3,4,5)');
    await expect(bar(grid)).toHaveValue('=SUM(1,2,3,4,5)');
    await page.keyboard.press('Enter');
    await expect(cell(grid, 'F3')).toHaveText('15');
});

test('DC-30/DC-25: on the grid that declares no undo, Ctrl+Z stays the browser\'s', async ({ page }) => {
    const positions = page.locator('#sheet-positions .ex-grid');
    await positions.locator("[id$='-r1c1']").click({ force: true });
    await expect(positions).toBeFocused();
    await alterPage(page, () => {
        window.__undoPrevented = null;
        const listener = (event) => {
            if (event.key === 'z' || event.key === 'Z') {
                window.__undoPrevented = event.defaultPrevented;
            }
        };
        document.addEventListener('keydown', listener);
        return () => { document.removeEventListener('keydown', listener); delete window.__undoPrevented; };
    });
    await page.keyboard.press('ControlOrMeta+Z');
    await expect.poll(() => page.evaluate(() => window.__undoPrevented)).toBe(false);
    // The Sheet beside it saw nothing either.
    await expect(cell(sheet(page), 'D2')).toHaveText('6');
});

// ---------------------------------------------------------------------------------------------
// Resize grips without the menu (DC-36), and a grid that declares nothing beside one that
// declares everything (DC-25)

test('DC-36: the grips resize a column and size it to fit, and no header carries a menu button', async ({ page }) => {
    const grid = sheet(page);
    await expect(grid.locator('.ex-menu-button')).toHaveCount(0);
    const headerB = grid.locator('.ex-header-cell', { hasText: /^B$/ });
    const before = await boxOf(headerB);
    const grip = await boxOf(headerB.locator('.ex-resize-grip'));
    await page.mouse.move(grip.x + grip.width / 2, grip.y + grip.height / 2);
    await page.mouse.down();
    await page.mouse.move(grip.x + grip.width / 2 + 60, grip.y + grip.height / 2, { steps: 6 });
    await page.mouse.up();
    await expect.poll(async () => Math.round((await boxOf(headerB)).width - before.width)).toBeGreaterThanOrEqual(55);
    // The cells follow the header.
    const b2 = await boxOf(cell(grid, 'B2'));
    expect(Math.abs(b2.width - (await boxOf(headerB)).width)).toBeLessThanOrEqual(1);

    // A double-click on A's edge sizes A to fit its longest text ("PV of R-4471").
    const headerA = grid.locator('.ex-header-cell', { hasText: /^A$/ });
    const aBefore = await boxOf(headerA);
    await headerA.locator('.ex-resize-grip').dblclick({ force: true });
    await expect.poll(async () => (await boxOf(headerA)).width).not.toBe(aBefore.width);
    const a12 = cell(grid, 'A12');
    const fits = await a12.evaluate((el) => el.scrollWidth <= el.clientWidth);
    expect(fits).toBe(true);
});

test('DC-25: the positions grid, declaring nothing, keeps ExGrid\'s own behaviour beside the Sheet', async ({ page }) => {
    const positions = page.locator('#sheet-positions .ex-grid');
    await expect(positions.locator('.ex-formula-bar, .ex-row-heading, .ex-headings-corner, .ex-fill-handle')).toHaveCount(0);
    // Ctrl+↓ goes to the grid's edge, not to a block's end: it has no edge answer.
    await positions.locator("[id$='-r0c0']").click({ force: true });
    await page.keyboard.press('ControlOrMeta+ArrowDown');
    await expect(positions).toHaveAttribute('aria-activedescendant', /-r4c0$/);
    await expect(positions.locator('.ex-fill-handle')).toHaveCount(0);
    // A block pasted onto one cell is refused, as ADR-0014 says; nothing lands in the Sheet.
    const grid = sheet(page);
    await page.evaluate(() => navigator.clipboard.writeText('a\tb\r\nc\td\r\n'));
    await page.keyboard.press('ControlOrMeta+V');
    await expect(cell(grid, 'A5')).toHaveText('Total');
    // And the Sheet's keys stay the Sheet's: typing into the Sheet does not reach the positions.
    await clickCell(grid, 'F2');
    await page.keyboard.type('=SU');
    await expect(completion(grid)).toHaveCount(1);
    await expect(positions.locator('.ex-completion, .ex-editor')).toHaveCount(0);
    await page.keyboard.press('Escape');
    await page.keyboard.press('Escape');
});
