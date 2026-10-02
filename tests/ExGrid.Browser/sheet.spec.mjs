import { test, expect, setRoundTrip, alterPage } from './fixtures.mjs';
import { SERVER } from './hosting.mjs';
import { expectKeyboardOn, expectActiveDescendant } from './keyboard.mjs';
import {
    sheet, openSheet, cell, clickCell, clickBarEnd, editor, bar, nameBox, expectFocusAt, goTo, enter, expectCovers, boxOf, spanOf, typeSteadily, typeIntoNameBox, pressCell,
    expectCommandsGreyedOut, expectCommandsOffered, expectSelectionIsCell,
    pressAt,
} from './sheet-helpers.mjs';

// ExSheet on the DemoHost's /sheet, driven with real keys and the real mouse (SH-18, ticket 18):
// typing Entries and Formulas, Ctrl+arrow, the Headings, the Name Box, the Formula Bar,
// insertion, the Linked Table, the extent. The Formula-entry aids (completion, Point), the fill
// handle, the clipboard and undo are in declarations.spec.mjs, beside the ExGrid declarations
// of ADR-0050/0051 that they exercise. Every test takes its page from fixtures.mjs, so a
// console message or an exception on either host fails it (CON-*).
//
// The page's Sheet: A1:D5 a small table with Formulas (D2 =B2*C2 … B5 =SUM(B2:B4)), A7:B8 a
// date and a Formula over it, A10:B13 the Linked Table readers. Column A is pinned.

// Tall enough that every Sheet on the page, Formula Bar to horizontal scrollbar, is inside the
// window: a pointer below the window's edge reaches nothing, and the edge band sits there.
test.use({ viewport: { width: 1280, height: 1000 } });

test.beforeEach(async ({ page }) => {
    await openSheet(page);
});

test('SH-18: an Entry and a Formula typed into cells commit, compute and move the Focus down', async ({ page }) => {
    const grid = sheet(page);
    await enter(page, grid, 'E2', '=D2*2');
    await expect(cell(grid, 'E2')).toHaveText('12');
    await expectFocusAt(grid, 'E3');

    // A constant, then a Formula over it: the dependent recalculates as the constant changes.
    await enter(page, grid, 'F2', '5');
    await enter(page, grid, 'F3', '=F2+E2');
    await expect(cell(grid, 'F3')).toHaveText('17');
    await enter(page, grid, 'F2', '8');
    await expect(cell(grid, 'F3')).toHaveText('20');

    // A Formula's cell shows its Value; F2 opens the Entry (DC-16), in both surfaces.
    await clickCell(grid, 'D2');
    await expect(cell(grid, 'D2')).toHaveText('6');
    await expect(bar(grid)).toHaveValue('=B2*C2');
    await page.keyboard.press('F2');
    await expect(editor(grid)).toHaveValue('=B2*C2');
    await page.keyboard.press('Escape');
    await expect(editor(grid)).toHaveCount(0);
    await expect(cell(grid, 'D2')).toHaveText('6');
});

test('SH-18/DC-7: Ctrl+arrow stops at the end of each block, Ctrl+Shift+arrow extends to it', async ({ page }) => {
    const grid = sheet(page);
    await clickCell(grid, 'A1');
    await expectFocusAt(grid, 'A1');
    // A1:A5 is a block, A7:A8 the next, A10:A13 the last; below that, the Sheet's edge.
    for (const stop of ['A5', 'A7', 'A8', 'A10', 'A13', 'A1048576']) {
        await page.keyboard.press('ControlOrMeta+ArrowDown');
        await expectFocusAt(grid, stop);
    }
    await page.keyboard.press('ControlOrMeta+ArrowUp');
    await expectFocusAt(grid, 'A13');
    await page.keyboard.press('ControlOrMeta+ArrowRight');
    await expectFocusAt(grid, 'B13');
    await page.keyboard.press('ControlOrMeta+ArrowRight');
    await expectFocusAt(grid, 'XFD13');
    await page.keyboard.press('ControlOrMeta+ArrowLeft');
    await expectFocusAt(grid, 'B13');

    // Ctrl+Shift+arrow runs the Extent to where Ctrl+arrow would stop; the Focus stays (ADR-0052).
    await goTo(grid, 'B2');
    await page.keyboard.press('ControlOrMeta+Shift+ArrowDown');
    await expectFocusAt(grid, 'B2');
    await expectCovers(grid.locator('.ex-selection .ex-range'), grid, 'B2', 'B5');
});

test('SH-18/DC-2/DC-3: a column heading, a Row Heading and the corner select, and nothing sorts', async ({ page }) => {
    const grid = sheet(page);
    const header = (letter) => grid.locator('.ex-header-cell', { hasText: new RegExp(`^${letter}$`) });

    // A plain click on C's heading selects the whole column; the Focus goes to its first row.
    await header('C').click({ force: true });
    await expectFocusAt(grid, 'C1');
    // The range starts at row 1 and runs past every painted row. It is painted only as far as
    // the rows are, one row beyond (ADR-0053): laid out a million rows tall, it was clamped by
    // the browser's Layout Ceiling like any other length.
    const range = grid.locator('.ex-selection .ex-range');
    const readableBottom = () => grid.locator('.ex-scroller').evaluate((scroller) =>
        scroller.getBoundingClientRect().top + scroller.clientTop + scroller.clientHeight);
    const coversColumns = async (from, to) => {
        const want = await spanOf(grid, from, to);
        const box = await boxOf(range);
        expect(Math.abs(box.x - want.x)).toBeLessThanOrEqual(1);
        expect(Math.abs(box.width - want.width)).toBeLessThanOrEqual(1);
        expect(Math.abs(box.y - want.y)).toBeLessThanOrEqual(1);
        expect(box.y + box.height).toBeGreaterThanOrEqual(await readableBottom());
    };
    await coversColumns('C1', 'C1');
    // Shift+click extends from the Focus's column, and the Focus stays in it (ADR-0052).
    await header('E').click({ force: true, modifiers: ['Shift'] });
    await expect.poll(async () => (await boxOf(range)).width).toBeGreaterThan(200);
    await coversColumns('C1', 'E1');
    // Nothing sorted: the rows are where they were.
    await expect(cell(grid, 'A2')).toHaveText('Apples');
    await expect(cell(grid, 'A4')).toHaveText('Plums');

    // A Row Heading selects its row, across every column: the range starts at the pinned A and
    // runs past the right edge of what is painted.
    const heading = (n) => grid.locator('.ex-row .ex-row-heading', { hasText: new RegExp(`^${n}$`) });
    await pressAt(heading(3));
    await expectFocusAt(grid, 'A3');
    const rowRange = await boxOf(grid.locator('.ex-selection-pinned .ex-range'));
    const a3 = await boxOf(cell(grid, 'A3'));
    expect(Math.abs(rowRange.y - a3.y)).toBeLessThanOrEqual(1);
    expect(Math.abs(rowRange.height - a3.height)).toBeLessThanOrEqual(1);
    // Shift+click extends by rows.
    await pressAt(heading(5), { modifiers: ['Shift'] });
    await expect.poll(async () => Math.round((await boxOf(grid.locator('.ex-selection-pinned .ex-range'))).height))
        .toBe(Math.round(3 * a3.height));

    // The corner selects all: one range from A1 across every painted row and column.
    await grid.locator('.ex-headings-corner').click({ force: true });
    await expect.poll(async () => {
        const box = await boxOf(grid.locator('.ex-selection-pinned .ex-range'));
        return box.y + box.height >= await readableBottom();
    }).toBe(true);
    const all = await boxOf(grid.locator('.ex-selection-pinned .ex-range'));
    const a1 = await boxOf(cell(grid, 'A1'));
    expect(Math.abs(all.x - a1.x)).toBeLessThanOrEqual(1);
    expect(Math.abs(all.y - a1.y)).toBeLessThanOrEqual(1);
    await expect(grid.locator('.ex-announce')).not.toBeEmpty();
});

test('DC-3: the Row Headings stay at the left edge while the Sheet scrolls sideways', async ({ page }) => {
    const grid = sheet(page);
    const scroller = grid.locator('.ex-scroller');
    const heading = grid.locator('.ex-row .ex-row-heading').first();
    const before = await boxOf(heading);
    const rootBox = await boxOf(grid);
    expect(Math.abs(before.x - rootBox.x)).toBeLessThanOrEqual(2);

    for (const left of [300, 5000, 400_000]) {
        await scroller.evaluate((el, x) => { el.scrollLeft = x; }, left);
        // The scroll lands, the columns under it move, and the band does not.
        await expect(grid.locator('.ex-header-cell').nth(1)).not.toHaveText('B');
        const after = await boxOf(heading);
        expect(Math.abs(after.x - before.x)).toBeLessThanOrEqual(1);
        expect(Math.abs(after.width - before.width)).toBeLessThanOrEqual(1);
        // The pinned column A stays right beside it, and the corner above it.
        const a = await boxOf(cell(grid, 'A1'));
        expect(Math.abs(a.x - (after.x + after.width))).toBeLessThanOrEqual(1);
        const corner = await boxOf(grid.locator('.ex-headings-corner'));
        expect(Math.abs(corner.x - after.x)).toBeLessThanOrEqual(1);
        // Nothing scrolled under the band shows through it: what is painted at its middle is
        // the heading itself.
        // Cells are pointer-events: none (ADR-0004), so hit-testing is switched on for them
        // alone while asking: it changes what the browser reports, not what it paints.
        const hit = await page.evaluate(({ x, y }) => {
            const style = document.createElement('style');
            style.textContent = '.ex-row-heading, .ex-cell { pointer-events: auto !important; }';
            document.head.append(style);
            const el = document.elementFromPoint(x, y);
            style.remove();
            return el?.className ?? '';
        }, { x: after.x + after.width / 2, y: after.y + after.height / 2 });
        expect(hit).toContain('ex-row-heading');
    }
});

test('SH-18/DC-11: the Name Box takes the Focus to an address, scrolled into view', async ({ page }) => {
    const grid = sheet(page);
    // The Linked Table's first snapshot is waited for: every change to the Sheet clears
    // ExSheet's notice, a Consumer's push included (ExSheet.ChangedAsync), and on a slow host
    // the push would land just after the refusal this test reads below.
    await expect(cell(grid, 'B12')).toHaveText('318.25', { timeout: 10_000 });
    await clickCell(grid, 'A1');
    await goTo(grid, 'D200');
    // The Focus cell is painted inside the scroller's box, and the keyboard is the grid's.
    const scroller = await boxOf(grid.locator('.ex-scroller'));
    const d200 = await boxOf(cell(grid, 'D200'));
    expect(d200.y).toBeGreaterThanOrEqual(scroller.y);
    expect(d200.y + d200.height).toBeLessThanOrEqual(scroller.y + scroller.height);
    await expectKeyboardOn(grid);
    await page.keyboard.press('ArrowDown');
    await expectFocusAt(grid, 'D201');

    // An address the Sheet cannot read moves nothing and is said so.
    await typeIntoNameBox(grid, 'nonsense');
    await nameBox(grid).press('Enter');
    await expect(page.locator('.ex-sheet-notice')).not.toBeEmpty();
    await expectActiveDescendant(grid, /-r200c3$/);
});

test('SH-18/DC-11: the Name Box pressed with an edit open commits it, then navigates', async ({ page }) => {
    const grid = sheet(page);
    await clickCell(grid, 'F2');
    await page.keyboard.type('42');
    await expect(editor(grid)).toHaveValue('42');
    await nameBox(grid).click();
    // The press committed the edit where it was, and the keyboard stayed in the Name Box.
    await expect(editor(grid)).toHaveCount(0);
    await expect(cell(grid, 'F2')).toHaveText('42');
    await expect(nameBox(grid)).toBeFocused();
    await nameBox(grid).press('ControlOrMeta+A');
    await nameBox(grid).press('Backspace');
    await typeSteadily(page, nameBox(grid), 'B7');
    await nameBox(grid).press('Enter');
    await expectFocusAt(grid, 'B7');

    // A Formula that cannot be read is Rejected on the press: the edit stays open with
    // everything typed, and the keyboard goes back to it (ADR-0050, fourth round).
    await clickCell(grid, 'F3');
    await page.keyboard.type('=SUM(');
    await nameBox(grid).click();
    await expect(editor(grid)).toHaveValue('=SUM(');
    await expect(editor(grid)).toBeFocused();
    await typeSteadily(page, editor(grid), '1)');
    await expect(editor(grid)).toHaveValue('=SUM(1)');
    await page.keyboard.press('Enter');
    await expect(cell(grid, 'F3')).toHaveText('1');
});

// A press into the Name Box selects its text, as Excel's does, so what is typed replaces the
// address shown (ADR-0051, decided with the user 2026-10-01; ticket 78). The fifteenth Windows run
// (i7) found the caret left after `D10`, and an IME's composition appended to it: `D10かな`. Only
// the press that gives the Name Box the keyboard selects; one into it while it holds the keyboard
// is the field's own. Under both Chromes: the built-in input, and ExGrid.MudBlazor's control inside
// the core's box.
const selectionOf = (field) => field.evaluate((input) => [input.selectionStart, input.selectionEnd]);

for (const chrome of ['builtin', 'mud']) {
    test.describe(`ticket 78 under the ${chrome} Chrome`, () => {
        test.beforeEach(async ({ page }) => {
            if (chrome !== 'builtin') {
                await openSheet(page, chrome);
            }
        });

        test('ticket 78/ADR-0051: a press into the Name Box selects its text, B2 typed replaces it and Enter goes there; a second press places the caret', async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'D10');

            await nameBox(grid).click();

            await expect(nameBox(grid)).toBeFocused();
            await expect.poll(() => selectionOf(nameBox(grid))).toEqual([0, 3]);
            await page.keyboard.type('B');
            await expect(nameBox(grid)).toHaveValue('B');
            await page.keyboard.type('2');
            await expect(nameBox(grid)).toHaveValue('B2');
            await page.keyboard.press('Enter');
            await expectFocusAt(grid, 'B2');
            await expectKeyboardOn(grid);

            // A second press, into the Name Box that holds the keyboard, places the caret where it
            // lands: here past the text's end.
            await nameBox(grid).click();
            await expect.poll(() => selectionOf(nameBox(grid))).toEqual([0, 2]);
            const box = await nameBox(grid).boundingBox();
            await nameBox(grid).click({ position: { x: box.width - 4, y: box.height / 2 } });
            await expect.poll(() => selectionOf(nameBox(grid))).toEqual([2, 2]);
            // Escape still gives the keyboard back, the Focus where it was.
            await page.keyboard.press('Escape');
            await expectKeyboardOn(grid);
            await expectFocusAt(grid, 'B2');
        });

        test('ticket 78/ADR-0051: the commit a press into the Name Box makes renames it, and what is typed replaces the new name; a Reject still takes the keyboard back', async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'F2');
            await page.keyboard.type('=1+');
            await expect(editor(grid)).toHaveValue('=1+');
            await page.keyboard.press('ArrowDown');
            await expect(editor(grid)).toHaveValue('=1+F3');
            await expect(nameBox(grid)).toHaveValue('F3');

            // The press selects F3; its commit names the Focus, F2, over that selection.
            await nameBox(grid).click();

            await expect(editor(grid)).toHaveCount(0);
            await expect(cell(grid, 'F2')).toHaveText('1');
            await expect(nameBox(grid)).toHaveValue('F2');
            await expect(nameBox(grid)).toBeFocused();
            await page.keyboard.type('B');
            await expect(nameBox(grid)).toHaveValue('B');
            await page.keyboard.type('2');
            await expect(nameBox(grid)).toHaveValue('B2');
            await page.keyboard.press('Enter');
            await expectFocusAt(grid, 'B2');

            // A Formula that cannot be read is Rejected on the press: the keyboard goes back to the
            // edit, and what is typed lands in the Formula (ADR-0050, fourth round; ADR-0021).
            await pressCell(grid, 'F3');
            await page.keyboard.type('=SUM(');
            await expect(editor(grid)).toHaveValue('=SUM(');
            await nameBox(grid).click();
            await expect(editor(grid)).toBeFocused();
            await typeSteadily(page, editor(grid), '1)');
            await expect(editor(grid)).toHaveValue('=SUM(1)');
            await page.keyboard.press('Enter');
            await expect(cell(grid, 'F3')).toHaveText('1');
        });

        // On a circuit the Name Box's press is not held among the keys, so the render answering the
        // row press before it lands after the press and writes its name over the selection.
        test('ticket 78/ADR-0051: on a 150 ms circuit, a render that renames the Name Box after the press leaves what is typed replacing the name', async ({ page }) => {
            test.skip(!SERVER, 'WebAssembly has no round trip: the row press is answered before the Name Box is pressed');
            const grid = sheet(page);
            await pressCell(grid, 'F2');
            await setRoundTrip(150);
            // No wait for anything: the press into the Name Box follows the row press as a user's does.
            await clickCell(grid, 'F8');
            await nameBox(grid).click();
            await expect(nameBox(grid)).toHaveValue('F8');
            // Every round trip has landed — the renames, and the row's hand-back, which leaves the
            // Name Box its keyboard (ADR-0021).
            await page.waitForTimeout(600);
            await expect(nameBox(grid)).toBeFocused();

            await page.keyboard.type('D');
            await expect(nameBox(grid)).toHaveValue('D');
            await page.keyboard.type('4');
            await expect(nameBox(grid)).toHaveValue('D4');
            await page.keyboard.press('Enter');
            await expectFocusAt(grid, 'D4');
            await setRoundTrip(0);
        });
    });
}

// Home and End in Caret move the caret, in the Cell Editor and in the Formula Bar (ADR-0010). On
// macOS the browser binds them to scrolling the document instead, and PageUp and PageDown to
// scrolling a page: the grid scrolled away from the open edit, the edited cell left the painted
// rows, and the Cell Editor went, DOM focus with it (ticket 32). There the listener answers them.
// On Windows and Linux the browser does it, and this test holds as it stands.
const LONG_TEXT = 'The quantity and the price are both entered before the amount of this line is worked out, '
    + 'and the discount in the next column is checked against the terms agreed for this book';

/** Where the edit is: the field's selection and scroll, and whether the grid or the page moved. */
const whereTheEditIs = (field) => field.evaluate((input) => {
    const scroller = input.closest('.ex-grid').querySelector(':scope > .ex-scroller');
    return {
        focused: document.activeElement === input,
        selection: [input.selectionStart, input.selectionEnd],
        scrolled: input.scrollLeft,
        grid: [scroller.scrollTop, scroller.scrollLeft],
        page: document.scrollingElement.scrollTop,
    };
});

for (const surface of ['cell', 'bar']) {
    test(`ticket 32/ADR-0010: in Caret in the ${surface === 'cell' ? 'Cell Editor' : 'Formula Bar'}, Home and End move the caret, with Shift extend the selection, and nothing scrolls the grid away`, async ({ page }) => {
        const grid = sheet(page);
        // A long text far down the Sheet, where the grid has room to scroll either way.
        await goTo(grid, 'F200');
        await page.keyboard.press('F2');
        await expect(editor(grid)).toBeFocused();
        await page.keyboard.insertText(LONG_TEXT);
        await expect(editor(grid)).toHaveValue(LONG_TEXT);
        await page.keyboard.press('Enter');
        await expect(editor(grid)).toHaveCount(0);
        await goTo(grid, 'F200');
        let field;
        if (surface === 'cell') {
            await page.keyboard.press('F2');
            field = editor(grid);
        } else {
            await clickBarEnd(grid);
            field = bar(grid);
        }
        await expect(field).toBeFocused();
        await expect(field).toHaveValue(LONG_TEXT);
        // The edit is open before the first key. The bar shows the Focus cell's text before any
        // edit, so the value alone does not say so. On a circuit the edit a press into the bar
        // opens lands a round trip later, and writing the field then puts the caret at the end and
        // the view back at the start. End pressed before that measured the write, not the key
        // (seen in CI on the Server host, Linux, where the browser moves the caret itself).
        await expect(grid).toHaveClass(/ex-editing/);
        // F2 leaves the caret at the end; a press into the bar's text, where the press was.
        const end = LONG_TEXT.length;
        const { grid: gridAt, page: pageAt } = await whereTheEditIs(field);

        const expectAt = async (selection, scrolled) => {
            await expect.poll(async () => {
                const now = await whereTheEditIs(field);
                return {
                    focused: now.focused,
                    selection: now.selection,
                    scrolled: scrolled === 'start' ? now.scrolled === 0 : now.scrolled > 0,
                    grid: now.grid,
                    page: now.page,
                };
            }).toEqual({ focused: true, selection, scrolled: true, grid: gridAt, page: pageAt });
            await expect(grid).toHaveClass(/ex-editing/);
        };
        await page.keyboard.press('End');
        await expectAt([end, end], 'end');
        await page.keyboard.press('Home');
        await expectAt([0, 0], 'start');
        await page.keyboard.press('Shift+End');
        await expectAt([0, end], 'end');
        await page.keyboard.press('End');
        await page.keyboard.press('Shift+Home');
        await expectAt([0, end], 'start');
        await page.keyboard.press('End');
        await expectAt([end, end], 'end');
        // PageUp and PageDown move no caret in a one-line field, and a browser would scroll the
        // grid with them. The listener takes them while an edit is open, on every platform
        // (decided with the user, 2026-09-30), so nothing moves.
        await page.keyboard.press('PageUp');
        await page.keyboard.press('PageDown');
        await expectAt([end, end], 'end');

        // The edit is still the one opened: Escape cancels it where it stands.
        await page.keyboard.press('Escape');
        await expect(editor(grid)).toHaveCount(0);
        await expectFocusAt(grid, 'F200');
    });
}

test('SH-18/DC-22: the Formula Bar and the Cell Editor are one text; each commits and cancels once', async ({ page }) => {
    const grid = sheet(page);
    await clickCell(grid, 'E3');
    await expect(bar(grid)).toHaveValue('');

    // Typed in the cell: the bar agrees after every keystroke.
    for (const [key, text] of [['=', '='], ['B', '=B'], ['3', '=B3']]) {
        await page.keyboard.type(key);
        await expect(editor(grid)).toHaveValue(text);
        await expect(bar(grid)).toHaveValue(text);
    }
    // Typed in the bar: the cell agrees. The press into the bar keeps the edit open.
    await clickBarEnd(grid);
    for (const [key, text] of [['+', '=B3+'], ['1', '=B3+1']]) {
        await page.keyboard.type(key);
        await expect(bar(grid)).toHaveValue(text);
        await expect(editor(grid)).toHaveValue(text);
    }
    // Enter in the bar commits once: the value lands, the Focus moves down one row, and one
    // Ctrl+Z takes the whole Entry away again.
    await page.keyboard.press('Enter');
    await expect(editor(grid)).toHaveCount(0);
    await expect(cell(grid, 'E3')).toHaveText('8');
    await expectFocusAt(grid, 'E4');
    await expectKeyboardOn(grid);
    await page.keyboard.press('ControlOrMeta+Z');
    await expect(cell(grid, 'E3')).toHaveText('');
    await page.keyboard.press('ControlOrMeta+Y');
    await expect(cell(grid, 'E3')).toHaveText('8');

    // Escape in the bar cancels once: both surfaces close, the cell is untouched, the grid
    // keeps the keyboard — the next Escape is the grid's own Leave.
    await pressCell(grid, 'E3');
    await clickBarEnd(grid);
    await typeSteadily(page, bar(grid), '*2');
    await expect(editor(grid)).toHaveValue('=B3+1*2');
    await page.keyboard.press('Escape');
    await expect(editor(grid)).toHaveCount(0);
    await expect(bar(grid)).toHaveValue('=B3+1');
    await expect(cell(grid, 'E3')).toHaveText('8');
    await expectKeyboardOn(grid);
});

test('SH-18: inserting a row keeps every Reference naming its cell, and one Ctrl+Z restores it', async ({ page }) => {
    const grid = sheet(page);
    await clickCell(grid, 'B5');
    await expect(bar(grid)).toHaveValue('=SUM(B2:B4)');
    await page.locator('#sheet-insert-row').click();
    await expect(cell(grid, 'A3')).toHaveText('Apples');
    await expect(cell(grid, 'A2')).toHaveText('');
    await expect(cell(grid, 'B6')).toHaveText('39');
    await clickCell(grid, 'B6');
    await expect(bar(grid)).toHaveValue('=SUM(B3:B5)');
    await clickCell(grid, 'D3');
    await expect(bar(grid)).toHaveValue('=B3*C3');

    // By the Context Menu on a cell: rows above the Selection.
    await pressAt(cell(grid, 'A3'), { button: 'right' });
    await page.getByRole('menuitem', { name: 'Insert rows above' }).click();
    await expect(cell(grid, 'A4')).toHaveText('Apples');
    await clickCell(grid, 'B7');
    await expect(bar(grid)).toHaveValue('=SUM(B4:B6)');

    // One Ctrl+Z per insertion restores the structure and every Reference.
    await page.keyboard.press('ControlOrMeta+Z');
    await expect(cell(grid, 'A3')).toHaveText('Apples');
    await page.keyboard.press('ControlOrMeta+Z');
    await expect(cell(grid, 'A2')).toHaveText('Apples');
    await clickCell(grid, 'B5');
    await expect(bar(grid)).toHaveValue('=SUM(B2:B4)');
    await expect(cell(grid, 'B5')).toHaveText('39');
});

// The other three structure commands, from the Context Menu (ticket 13): References rewritten,
// a deleted target written #REF!, the Selection left where it was (the Row Sequence Version does
// not move: rows and columns are places, ADR-0011/0046), and one Ctrl+Z per command restoring
// the structure and every Reference. The menu hands the keyboard back to the grid, so the
// Ctrl+Z is pressed straight after the command, with no press on a cell between.

test('SH-5/SH-18: deleting a row rewrites the References below it, a deleted target is #REF!, and one Ctrl+Z restores it', async ({ page }) => {
    const grid = sheet(page);
    // A Formula naming a cell of the row about to go.
    await enter(page, grid, 'F1', '=A3');
    await expect(cell(grid, 'F1')).toHaveText('Pears');

    await clickCell(grid, 'B3');
    await pressAt(cell(grid, 'B3'), { button: 'right' });
    await page.getByRole('menuitem', { name: 'Delete rows' }).click();
    await expect(cell(grid, 'A3')).toHaveText('Plums');
    // The Selection stays where it was: the same address, now over the row that moved up.
    await expectFocusAt(grid, 'B3');
    await expectSelectionIsCell(grid, 'B3');
    await expectKeyboardOn(grid);
    // The total moved up a row and shrank with its range; the row below kept its own cells.
    await expect(cell(grid, 'B4')).toHaveText('32');
    await expect(cell(grid, 'D3')).toHaveText('4');
    // The Reference to the deleted row is #REF!, in the Value and in the stored Formula.
    await expect(cell(grid, 'F1')).toHaveText('#REF!');

    // One Ctrl+Z restores the row and every Reference.
    await page.keyboard.press('ControlOrMeta+Z');
    await expect(cell(grid, 'A3')).toHaveText('Pears');
    await expect(cell(grid, 'F1')).toHaveText('Pears');
    await expect(cell(grid, 'B5')).toHaveText('39');

    // Read back after the undo, and, redone, the stored Formulas as the deletion wrote them.
    await clickCell(grid, 'F1');
    await expect(bar(grid)).toHaveValue('=A3');
    await clickCell(grid, 'B5');
    await expect(bar(grid)).toHaveValue('=SUM(B2:B4)');
    await page.keyboard.press('ControlOrMeta+Y');
    await expect(cell(grid, 'A3')).toHaveText('Plums');
    await clickCell(grid, 'F1');
    await expect(bar(grid)).toHaveValue('=#REF!');
    await clickCell(grid, 'B4');
    await expect(bar(grid)).toHaveValue('=SUM(B2:B3)');
});

test('SH-5/SH-18: inserting a column rewrites every Reference across it, and one Ctrl+Z restores it', async ({ page }) => {
    const grid = sheet(page);
    // Two rows of column C: the command acts on the columns the Selection spans, one here.
    await clickCell(grid, 'C2');
    await clickCell(grid, 'C3', { modifiers: ['Shift'] });
    await pressAt(cell(grid, 'C2'), { button: 'right' });
    await page.getByRole('menuitem', { name: 'Insert columns to the left' }).click();
    await expect(cell(grid, 'C1')).toHaveText('');
    await expect(cell(grid, 'D1')).toHaveText('Price');
    // The Selection stays over C2:C3, now the new blank column.
    await expectCovers(grid.locator('.ex-selection .ex-range'), grid, 'C2', 'C3');
    await expectKeyboardOn(grid);
    // Amount moved right and still multiplies Qty by Price.
    await expect(cell(grid, 'E2')).toHaveText('6');
    await expect(cell(grid, 'E5')).toHaveText('15.25');

    await page.keyboard.press('ControlOrMeta+Z');
    await expect(cell(grid, 'C1')).toHaveText('Price');
    await expect(cell(grid, 'D2')).toHaveText('6');

    await clickCell(grid, 'D2');
    await expect(bar(grid)).toHaveValue('=B2*C2');
    await clickCell(grid, 'D5');
    await expect(bar(grid)).toHaveValue('=SUM(D2:D4)');
    await page.keyboard.press('ControlOrMeta+Y');
    await expect(cell(grid, 'D1')).toHaveText('Price');
    await clickCell(grid, 'E2');
    await expect(bar(grid)).toHaveValue('=B2*D2');
    await clickCell(grid, 'E5');
    await expect(bar(grid)).toHaveValue('=SUM(E2:E4)');
});

test('SH-5/SH-18: deleting a column makes a Reference to it #REF!, and one Ctrl+Z restores it', async ({ page }) => {
    const grid = sheet(page);
    await clickCell(grid, 'C2');
    await pressAt(cell(grid, 'C2'), { button: 'right' });
    await page.getByRole('menuitem', { name: 'Delete columns' }).click();
    // Amount moved into C; its Price operand is gone.
    await expect(cell(grid, 'C1')).toHaveText('Amount');
    await expectFocusAt(grid, 'C2');
    await expectSelectionIsCell(grid, 'C2');
    await expectKeyboardOn(grid);
    await expect(cell(grid, 'C2')).toHaveText('#REF!');
    await expect(cell(grid, 'C5')).toHaveText('#REF!');
    // Qty, left of the deletion, is untouched, and so is its total.
    await expect(cell(grid, 'B5')).toHaveText('39');

    await page.keyboard.press('ControlOrMeta+Z');
    await expect(cell(grid, 'C1')).toHaveText('Price');
    await expect(cell(grid, 'D2')).toHaveText('6');
    await expect(cell(grid, 'D5')).toHaveText('15.25');

    await clickCell(grid, 'D2');
    await expect(bar(grid)).toHaveValue('=B2*C2');
    await page.keyboard.press('ControlOrMeta+Y');
    await expect(cell(grid, 'C1')).toHaveText('Amount');
    await clickCell(grid, 'C2');
    await expect(bar(grid)).toHaveValue('=B2*#REF!');
});

test('SH-16/SH-18: the Linked Table reads #GETTING_DATA until its first snapshot, then the values', async ({ page }) => {
    const grid = sheet(page);
    // The page pushes the first snapshot 1.5 s after the Sheet opens (SheetPage.razor).
    await page.goto('/sheet');
    await expect(page.locator('#demo-interactive')).toBeAttached({ timeout: 30_000 });
    await expect(cell(grid, 'B11')).toHaveText('#GETTING_DATA');
    await expect(cell(grid, 'B12')).toHaveText('#GETTING_DATA');
    // General, as Excel shows it: no thousands separator.
    await expect(cell(grid, 'B11')).toHaveText('1189.4', { timeout: 10_000 });
    await expect(cell(grid, 'B12')).toHaveText('318.25');
    await expect(cell(grid, 'B13')).toHaveText('5');
    await page.locator('#sheet-revalue').click();
    await expect(cell(grid, 'B12')).toHaveText('321.43');
});

// While an edit is open, the application's changes are refused, and the page greys out its
// buttons from ExSheet's notification (ADR-0048, ADR-0050 section 6; ticket 26). Found on this
// page: 99 typed over C4 (Plums), "Insert a row above row 2" pressed while the edit was open, and
// Enter wrote 99 into Pears' price — the Cell Editor had stayed on row 4 as Plums moved to row 5.

test('SH-29 (ADR-0048): the commands grey out while 99 is typed over C4, and Enter puts 99 in Plums\' row', async ({ page }) => {
    const grid = sheet(page);
    await expectCommandsOffered(page);

    await pressCell(grid, 'C4');
    await page.keyboard.type('99');
    await expect(editor(grid)).toHaveValue('99');
    await expectCommandsGreyedOut(page);
    // A Linked Table's snapshot is data arriving, not a command (ADR-0049).
    await expect(page.locator('#sheet-revalue')).toBeEnabled();

    await page.keyboard.press('Enter');
    await expect(editor(grid)).toHaveCount(0);
    await expect(cell(grid, 'C4')).toHaveText('99');
    await expect(cell(grid, 'A4')).toHaveText('Plums');
    await expectCommandsOffered(page);
});

// The press that reaches the refusal: on a circuit the button greys out a round trip after the
// first key, and a press inside that round trip lands on a button still offered. ExSheet refuses
// the insertion by name (the page shows the refusal) and nothing moves under the editor.
test('SH-29 (ADR-0048): the insert pressed on a 150 ms circuit before the button greys out is refused, and 99 lands in Plums\' row', async ({ page }) => {
    test.skip(!SERVER, 'WebAssembly has no round trip: the button greys out before a press can reach it');
    const grid = sheet(page);
    await pressCell(grid, 'C4');
    await setRoundTrip(150);
    // No wait for anything: the press follows the typing as a user's does.
    await page.keyboard.type('99');
    await page.locator('#sheet-insert-row').click();

    await expect(page.locator('#sheet-status')).toContainText('a cell is being edited');
    await expect(editor(grid)).toHaveValue('99');
    await expectCommandsGreyedOut(page);
    await expect(cell(grid, 'A4')).toHaveText('Plums');

    // The press took the keyboard to the button, and the edit stayed open (ADR-0018 section 6):
    // the user goes back to it and commits.
    await editor(grid).click();
    await page.keyboard.press('Enter');
    await expect(editor(grid)).toHaveCount(0);
    await expect(cell(grid, 'C4')).toHaveText('99');
    await expect(cell(grid, 'A4')).toHaveText('Plums');
    await expect(cell(grid, 'A3')).toHaveText('Pears');
    await expect(cell(grid, 'C3')).toHaveText('0.75');
    // 12 × 0.5 + 7 × 0.75 + 20 × 99. The defect painted 703, with 99 as Pears' price.
    await expect(cell(grid, 'D5')).toHaveText('1991.25');
    await expectCommandsOffered(page);
    await setRoundTrip(0);
});

test('SH-29 (ADR-0048/0049): the buttons follow each way an edit opens and ends, and a Linked Table push is taken meanwhile', async ({ page }) => {
    const grid = sheet(page);

    // Opened by typing. A snapshot pushed meanwhile recalculates its readers and leaves the typing.
    await pressCell(grid, 'C2');
    await page.keyboard.type('5');
    await expect(editor(grid)).toHaveValue('5');
    await expectCommandsGreyedOut(page);
    await expect(page.locator('#sheet-revalue')).toBeEnabled();
    await page.locator('#sheet-revalue').click();
    await expect(cell(grid, 'B12')).toHaveText('321.43');
    await expect(editor(grid)).toHaveValue('5');
    await expectCommandsGreyedOut(page);

    // Cancelled: the cell is as it was, and the buttons are back.
    await editor(grid).click();
    await page.keyboard.press('Escape');
    await expect(editor(grid)).toHaveCount(0);
    await expect(cell(grid, 'C2')).toHaveText('0.5');
    await expectCommandsOffered(page);

    // Opened by a press into the Formula Bar, and cancelled from there.
    await pressCell(grid, 'D2');
    await clickBarEnd(grid);
    await expectCommandsGreyedOut(page);
    await page.keyboard.press('Escape');
    await expect(editor(grid)).toHaveCount(0);
    await expectCommandsOffered(page);

    // Held open by a Reject: a Formula that cannot be read keeps the edit, and the buttons stay
    // grey until it is corrected and committed (ADR-0034).
    await pressCell(grid, 'F3');
    await page.keyboard.type('=SUM(');
    await page.keyboard.press('Enter');
    await expect(editor(grid)).toHaveValue('=SUM(');
    await expectCommandsGreyedOut(page);
    await typeSteadily(page, editor(grid), '1)');
    await page.keyboard.press('Enter');
    await expect(cell(grid, 'F3')).toHaveText('1');
    await expectCommandsOffered(page);
});

test('SH-2: the Focus reaches XFD1048576 and the DOM does not grow with the extent', async ({ page }) => {
    const grid = sheet(page);
    const count = () => grid.evaluate((root) => root.querySelectorAll('*').length);
    await clickCell(grid, 'A1');
    const atTop = await count();
    await goTo(grid, 'XFD1048576');
    await expect(cell(grid, 'XFD1048576')).toBeVisible();
    await expect(grid.locator('.ex-row .ex-row-heading').last()).toHaveText('1048576');
    await expect(grid.locator('.ex-header-cell').last()).toHaveText('XFD');
    // The same bound as at the top (§18): the far corner paints no more elements than A1 did,
    // give or take the Focus's own rectangle and a partly painted row or column.
    const atEnd = await count();
    expect(atEnd).toBeLessThanOrEqual(atTop + 20);
    // And it is a cell like any other: an Entry typed there commits.
    await page.keyboard.type('end');
    await page.keyboard.press('Tab');
    await expect(cell(grid, 'XFD1048576')).toHaveText('end');
    // Back to the top: Ctrl+Home.
    await page.keyboard.press('ControlOrMeta+Home');
    await expectFocusAt(grid, 'A1');
    expect(await count()).toBeLessThanOrEqual(atTop + 20);
});

// KB-8 (ADR-0012, rewritten 2026-10-01; ticket 77): Escape with nothing left to dismiss releases Tab
// and keeps DOM focus. The fifteenth Windows run's case i2: an edit opened by a character on D10,
// Escape, Escape, and the second sent the keyboard to body while D10 still looked selected. Under
// both Chromes: the Chrome draws the Cell Editor, the core decides the keys (ADR-0010).
for (const chrome of ['builtin', 'mud']) {
    test(`KB-8 under the ${chrome} Chrome: a character, Escape, Escape, a character opens an edit in the selected cell`, async ({ page }) => {
        if (chrome !== 'builtin') {
            await openSheet(page, chrome);
        }
        const grid = sheet(page);
        await pressCell(grid, 'D10');
        await page.keyboard.type('k');
        await expect(editor(grid)).toHaveValue('k');
        await page.keyboard.press('Escape');
        await expect(editor(grid)).toHaveCount(0);

        // Read once the second Escape's answer has long landed: the keyboard left a round trip
        // after it on the Server host.
        await page.keyboard.press('Escape');
        await page.waitForTimeout(500);
        await expectKeyboardOn(grid);
        await expectFocusAt(grid, 'D10');

        await page.keyboard.type('x');
        await expect(editor(grid)).toHaveValue('x');
        await expect(editor(grid)).toBeFocused();
        await expectActiveDescendant(grid, /-r9c3$/);
        await page.keyboard.press('Escape');
        await expect(editor(grid)).toHaveCount(0);
        await expect(cell(grid, 'D10')).toHaveText('');
        await expectFocusAt(grid, 'D10');
    });

    test(`KB-8 under the ${chrome} Chrome: after Escape and an arrow, or a press, Tab cycles; after Escape alone it leaves the Sheet`, async ({ page }) => {
        if (chrome !== 'builtin') {
            await openSheet(page, chrome);
        }
        const grid = sheet(page);
        // Something focusable straight after the Sheet, which the page's own markup holds;
        // alterPage takes it out as the test ends (ADR-0056).
        await alterPage(page, () => {
            const after = document.createElement('button');
            after.id = 'after-sheet';
            after.textContent = 'after';
            document.querySelector('.ex-grid:has(> .ex-formula-bar)').insertAdjacentElement('afterend', after);
            return () => after.remove();
        });
        await pressCell(grid, 'D10');

        // Any other key ends the release: the arrow moves, and Tab cycles again.
        await page.keyboard.press('Escape');
        await page.keyboard.press('ArrowDown');
        await expectFocusAt(grid, 'D11');
        await page.keyboard.press('Tab');
        await expectFocusAt(grid, 'E11');
        await expectKeyboardOn(grid);

        // So does a press on the grid, made once the release has landed.
        await page.keyboard.press('Escape');
        await page.waitForTimeout(500);
        await pressCell(grid, 'E12');
        await page.keyboard.press('Tab');
        await expectFocusAt(grid, 'F12');
        await expectKeyboardOn(grid);
        await page.keyboard.press('ArrowUp');
        await expectFocusAt(grid, 'F11');
        await page.keyboard.press('ArrowLeft');
        await expectFocusAt(grid, 'E11');

        // A Tab typed before the Escape's answer is held and dropped (ADR-0010/0021): wait for it.
        await page.keyboard.press('Escape');
        await page.waitForTimeout(500);
        await page.keyboard.press('Tab');
        await expect(page.locator('#after-sheet')).toBeFocused();
        await expectFocusAt(grid, 'E11');
    });
}
