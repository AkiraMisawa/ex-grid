import { test, expect, scrollRowToTop } from './fixtures.mjs';
import { chooseNumberFormat, expectCovers, expectSelectionIsCell } from './sheet-helpers.mjs';

// Excel's behaviours, observed beside ExSheet (docs/specs/exsheet/excel-behaviours.md). Each test
// is one item of that list, driven with real keys and the real mouse on /sheet, and its
// expectation is what a real Excel did: Microsoft 365, 16.0.20326.20158, driven the same way on
// 2026-09-27 (verification/2026-09-27-windows-excel/behaviours.md, with the screenshots). Where
// ExSheet differs from Excel by decision, the test asserts the decision and names its ADR; the
// comment says what Excel does.
//
// A probe whose feature is not built yet (its ticket's Status: is not done) is test.fixme, with
// the ticket named. EXGRID_SHEET_UNBUILT=1 runs those too, to see what ExSheet does today.
//
// /sheet opens with data in A1:D13 (SheetPage.razor's Opening()), so the probes write into E and F,
// which are empty and on screen.

const UNBUILT = process.env.EXGRID_SHEET_UNBUILT === '1';
/** A test whose feature waits on a ticket that is not done: fixme, unless asked to run anyway. */
const waitsOn = (ticket) => (UNBUILT ? test : test.fixme);

const COLUMNS = (n) => {
    let s = '';
    for (n += 1; n > 0; n = Math.floor((n - 1) / 26)) s = String.fromCharCode(65 + ((n - 1) % 26)) + s;
    return s;
};
function parse(a1) {
    const [, letters, digits] = /^([A-Z]+)(\d+)$/.exec(a1);
    let column = 0;
    for (const ch of letters) column = column * 26 + (ch.charCodeAt(0) - 64);
    return { row: Number(digits) - 1, column: column - 1 };
}

const sheet = (page) => page.locator('.ex-grid').first();
const cell = (page, a1) => {
    const { row, column } = parse(a1);
    return sheet(page).locator(`[id$='-r${row}c${column}']`);
};
const bar = (page) => sheet(page).locator('input.ex-formula-bar-text');
const nameBox = (page) => sheet(page).locator('input.ex-name-box');
const cellEditor = (page) => sheet(page).locator('input.ex-editor:not(.ex-formula-bar-text)');
const announced = (page) => sheet(page).locator('.ex-announce');

// Cells are pointer-events: none by design (ADR-0004): the click goes through to the Viewport,
// as a user's does. A plain click waits until the Name Box names the cell, as a user waits for
// the screen: a click that follows an Enter at once can land before the Enter's move does (found
// on the Server host, verification/2026-09-27-windows-excel/typing-probe-2.mjs), and these
// probes ask about Excel's behaviours, not that race.
async function click(page, a1, options = {}) {
    await cell(page, a1).click({ force: true, ...options });
    await page.waitForTimeout(PACE_MS);
    if (Object.keys(options).length === 0) await expect(nameBox(page)).toHaveValue(a1);
}

/**
 * The cell Excel calls the active cell, as the user reads it here: the Name Box's address.
 * ExGrid's Name Box names its Focus, which is Excel's active cell: the fixed end of a
 * Shift-extension (ADR-0052).
 */
async function expectActive(page, a1) {
    // Soft: where the Name Box names the other end, the rest of the item still runs.
    await expect.soft(nameBox(page)).toHaveValue(a1);
}

/** A multi-cell Selection, as the live region names its corners (ADR-0033). */
async function expectSelection(page, from, to) {
    const a = parse(from);
    const b = parse(to);
    const rows = b.row - a.row + 1;
    const columns = b.column - a.column + 1;
    await expect(announced(page)).toContainText(
        `${rows} rows by ${columns} columns selected, ${COLUMNS(a.column)} ${a.row + 1} to ${COLUMNS(b.column)} ${b.row + 1}`);
}

/** Types into a cell and commits with Enter, as a user does. */
async function enter(page, a1, typed) {
    await click(page, a1);
    await page.keyboard.type(typed);
    await page.keyboard.press('Enter');
    // Enter commits and moves down: wait for the move, as the next click would otherwise race it.
    const { row, column } = parse(a1);
    await expect(nameBox(page)).toHaveValue(`${COLUMNS(column)}${row + 2}`);
}

/** The Entry a cell holds, read from the Formula Bar with the cell selected. */
async function entryOf(page, a1) {
    await click(page, a1);
    return bar(page).inputValue();
}

async function dragFillHandle(page, toA1) {
    await page.waitForTimeout(PACE_MS);
    // The drag ends where a user could see it: a target below the window's bottom edge is
    // scrolled into view first, as a user scrolls before dragging. Nothing past that edge
    // receives the drag (/sheet's prose above the Sheet grew with ADR-0058, and E10 fell past it).
    await cell(page, toA1).scrollIntoViewIfNeeded();
    const handle = await sheet(page).locator('.ex-fill-handle').first().boundingBox();
    const target = await cell(page, toA1).boundingBox();
    await page.mouse.move(handle.x + handle.width / 2, handle.y + handle.height / 2);
    await page.mouse.down();
    await page.mouse.move(target.x + target.width / 2, target.y + target.height / 2, { steps: 12 });
    await page.mouse.up();
    await page.waitForTimeout(PACE_MS);
}

// A person's pace: every key and click waits this long before the next. On the Server host a
// click or key that follows the previous one at once can land before that one's answer, and a
// value typed next then lands in the wrong cell (found in this run: typing-probe-2.mjs, wrong at
// 0–60 ms on a local circuit, right from 100 ms). These probes ask what Excel does, not that.
const PACE_MS = 150;

// The file's tests share one page (ADR-0056), so its keyboard is paced once, not once per test.
const paced = new WeakSet();

test.beforeEach(async ({ page, context }) => {
    if (!paced.has(page)) {
        paced.add(page);
        for (const name of ['press', 'type']) {
            const act = page.keyboard[name].bind(page.keyboard);
            page.keyboard[name] = async (...args) => { await act(...args); await page.waitForTimeout(PACE_MS); };
        }
    }
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    await page.goto('/sheet');
    await expect(cell(page, 'A1')).toHaveText('Item');
});

// ---- Moving and selecting (ADR-0012, ADR-0050) ----------------------------------------------

test('item 1: Ctrl+Down stops at each block edge, then the last row (ADR-0050 §2, ticket 07)', async ({ page }) => {
    for (const [a1, v] of [['F1', '1'], ['F2', '2'], ['F3', '3'], ['F7', '7'], ['F8', '8'], ['F9', '9']]) {
        await enter(page, a1, v);
    }
    // Excel: A1 -> A3 -> A7 -> A9 -> A1048576 (keys and Range.End(xlDown) alike).
    await click(page, 'F1');
    for (const stop of ['F3', 'F7', 'F9', 'F1048576']) {
        await page.keyboard.press('Control+ArrowDown');
        await expectActive(page, stop);
    }
    // From a blank cell: the start of the next block, then its end.
    await page.keyboard.press('Control+Home');
    await click(page, 'F5');
    for (const stop of ['F7', 'F9', 'F1048576']) {
        await page.keyboard.press('Control+ArrowDown');
        await expectActive(page, stop);
    }
    // In a column with nothing in it: the last row at once.
    await page.keyboard.press('Control+Home');
    await click(page, 'G1');
    await page.keyboard.press('Control+ArrowDown');
    await expectActive(page, 'G1048576');
    // And back up from the last value: A7, A3, A1 in Excel.
    await page.keyboard.press('Control+Home');
    await click(page, 'F9');
    for (const stop of ['F7', 'F3', 'F1']) {
        await page.keyboard.press('Control+ArrowUp');
        await expectActive(page, stop);
    }
});

test('item 2: Ctrl+Shift+Right from inside a row block selects to its end, then to the last column (ADR-0050 §2, ADR-0052, ticket 07)', async ({ page }) => {
    // Excel's layout was C2:G2 with the Focus on D2; the same block sits at E6:I6 here, in a row
    // /sheet leaves empty (row 1 holds headings in A1:D1, which would join the block).
    await click(page, 'E6');
    for (const v of ['a', 'b', 'c', 'd', 'e']) {
        await page.keyboard.type(v);
        await page.keyboard.press('Tab');
    }
    await page.keyboard.press('Enter');
    await click(page, 'F6');
    await page.keyboard.press('Control+Shift+ArrowRight');
    await expectSelection(page, 'F6', 'I6');
    await expectActive(page, 'F6');
    await page.keyboard.press('Control+Shift+ArrowRight');
    await expectSelection(page, 'F6', 'XFD6');
    // Excel: Ctrl+Shift+Left from D2 is C2:D2, the block's start.
    await page.keyboard.press('Control+Home');
    await click(page, 'F6');
    await page.keyboard.press('Control+Shift+ArrowLeft');
    await expectSelection(page, 'E6', 'F6');
});

test('item 3: a column letter selects the column, Shift+click extends, the Focus on the top visible row (ADR-0050 §1, ADR-0052, ticket 06)', async ({ page }) => {
    const header = (letter) => sheet(page).locator('.ex-header-cell', { hasText: new RegExp(`^${letter}$`) });
    await click(page, 'C3');
    await header('B').click({ force: true });
    await expectSelection(page, 'B1', 'B1048576');
    await expectActive(page, 'B1');
    await header('D').click({ force: true, modifiers: ['Shift'] });
    await expectSelection(page, 'B1', 'D1048576');
    await expectActive(page, 'B1');
    // The other way round, the Focus stays on the first column clicked: D1 in Excel.
    await header('D').click({ force: true });
    await header('B').click({ force: true, modifiers: ['Shift'] });
    await expectSelection(page, 'B1', 'D1048576');
    await expectActive(page, 'D1');
    // Scrolled to row 100, Excel's Focus is D100: the top visible row, not row 1.
    await scrollRowToTop(sheet(page), 99);
    await expect(cell(page, 'A100')).toBeVisible();
    await header('D').click({ force: true });
    await expectActive(page, 'D100');
});

test('item 4: a row number selects the row, Shift+click extends, the Focus in the first column on screen (ADR-0050 §1, ADR-0052, ticket 06)', async ({ page }) => {
    const heading = (n) => sheet(page).locator('.ex-row-heading', { hasText: new RegExp(`^${n}$`) }).first();
    await click(page, 'C3');
    await heading(2).click({ force: true });
    await expectSelection(page, 'A2', 'XFD2');
    await expectActive(page, 'A2');
    await heading(5).click({ force: true, modifiers: ['Shift'] });
    await expectSelection(page, 'A2', 'XFD5');
    await expectActive(page, 'A2');
    await heading(5).click({ force: true });
    await heading(2).click({ force: true, modifiers: ['Shift'] });
    await expectSelection(page, 'A2', 'XFD5');
    await expectActive(page, 'A5');
});

test('item 5: the corner selects every cell, the Focus on the top-left visible cell (ADR-0050 §1, ADR-0052, ticket 06)', async ({ page }) => {
    await click(page, 'C3');
    await sheet(page).locator('.ex-headings-corner').click({ force: true });
    await expectSelection(page, 'A1', 'XFD1048576');
    await expectActive(page, 'A1');
    // Scrolled to row 100 (and column C in Excel), Excel's Focus was the top-left visible cell.
    // Column A is pinned on /sheet, so that is A100 here.
    await click(page, 'C3');
    await scrollRowToTop(sheet(page), 99);
    await expect(cell(page, 'A100')).toBeVisible();
    await sheet(page).locator('.ex-headings-corner').click({ force: true });
    await expectSelection(page, 'A1', 'XFD1048576');
    await expectActive(page, 'A100');
});

// ---- The active cell (ADR-0052; verification/2026-09-27-windows-excel-2/active-cell.md) -----

/** That a cell is painted inside the scroller's box — not the page's, which /sheet overruns —
 * once the scroll the grid asked for has landed. */
async function expectInView(page, a1) {
    await expect(async () => {
        const scroller = await sheet(page).locator('.ex-scroller').boundingBox();
        const box = await cell(page, a1).boundingBox();
        expect(box).not.toBeNull();
        expect(box.y).toBeGreaterThanOrEqual(scroller.y);
        expect(box.y + box.height).toBeLessThanOrEqual(scroller.y + scroller.height);
    }).toPass({ timeout: 5000 });
}

// Excel's cases, driven with real keys and the real mouse. /sheet's data is A1:D13, so the probes
// that write use E:G, which are empty and on screen.

test('active cell, case 1: Shift+arrows move the far corner, the Focus stays, and the view follows the Extent (ADR-0052)', async ({ page }) => {
    await click(page, 'B2');
    await page.keyboard.press('Shift+ArrowDown');
    await page.keyboard.press('Shift+ArrowDown');
    await page.keyboard.press('Shift+ArrowRight');
    await expectSelection(page, 'B2', 'C4');
    await expectActive(page, 'B2');
    // Far enough down that the Extent leaves the view: the view scrolls to keep it, not B2.
    for (let i = 0; i < 40; i++) await page.keyboard.press('Shift+ArrowDown');
    await expectSelection(page, 'B2', 'C44');
    await expectActive(page, 'B2');
    await expectInView(page, 'C44');
    expect(await sheet(page).locator('.ex-scroller').evaluate((s) => s.scrollTop)).toBeGreaterThan(0);
});

test('active cell, case 2: while a drag is held the Name Box shows 4R x 3C, and D5 stays active after D5 to B2 (ADR-0052)', async ({ page }) => {
    await click(page, 'E1');
    const from = await cell(page, 'D5').boundingBox();
    const to = await cell(page, 'B2').boundingBox();
    await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2);
    await page.mouse.down();
    await page.mouse.move(to.x + to.width / 2, to.y + to.height / 2, { steps: 8 });
    await expect(nameBox(page)).toHaveValue('4R x 3C');
    await page.mouse.up();
    await expect(nameBox(page)).toHaveValue('D5');
    await expectSelection(page, 'B2', 'D5');
});

test('active cell, case 3: after Enter, Shift+arrow moves the edge opposite the Focus, and nothing from the middle (ADR-0052)', async ({ page }) => {
    await click(page, 'E1');
    await click(page, 'G3', { modifiers: ['Shift'] });
    await page.keyboard.press('Enter');
    await page.keyboard.press('Enter');
    await expectActive(page, 'E3');
    await page.keyboard.press('Shift+ArrowRight');
    await expectSelection(page, 'E1', 'H3');
    await page.keyboard.press('Shift+ArrowDown');
    await expectSelection(page, 'E2', 'H3');
    await expectActive(page, 'E3');
    // From the middle (F2 in E1:G3, by Tab then Enter), Shift+Left changes nothing: E1:G3 stays,
    // so typing and Ctrl+Enter fill exactly E1:G3.
    await click(page, 'E1');
    await click(page, 'G3', { modifiers: ['Shift'] });
    await page.keyboard.press('Tab');
    await page.keyboard.press('Enter');
    await expectActive(page, 'F2');
    await page.keyboard.press('Shift+ArrowLeft');
    await page.keyboard.type('m');
    await page.keyboard.press('Control+Enter');
    for (const a1 of ['E1', 'G3']) await expect(cell(page, a1)).toHaveText('m');
    await expect(cell(page, 'D1')).not.toHaveText('m');
    await expectActive(page, 'F2');
});

test('active cell, case 6: Ctrl+click on the active cell takes it out and the first remaining cell of the range made last is active (ADR-0052)', async ({ page }) => {
    await click(page, 'E1');
    await click(page, 'G3', { modifiers: ['Shift'] });
    await click(page, 'E1', { modifiers: ['ControlOrMeta'] });
    await expectActive(page, 'F1');
    await page.keyboard.press('Shift+ArrowDown');
    // F1:G1 grew to F1:G2; typing still enters F1.
    await page.keyboard.type('g');
    await page.keyboard.press('Enter');
    await expect(cell(page, 'F1')).toHaveText('g');
    await expect(cell(page, 'E1')).toHaveText('');
});

test('active cell, cases 7 and 8: Ctrl+. walks the corners, Ctrl+Backspace scrolls back to the Focus, Shift+Backspace collapses (ADR-0052)', async ({ page }) => {
    await click(page, 'E1');
    await click(page, 'G3', { modifiers: ['Shift'] });
    for (const corner of ['G1', 'G3', 'E3', 'E1']) {
        await page.keyboard.press('ControlOrMeta+Period');
        await expectActive(page, corner);
    }
    await expectSelection(page, 'E1', 'G3');
    await scrollRowToTop(sheet(page), 299);
    await expect(cell(page, 'E300')).toBeVisible();
    await page.keyboard.press('Control+Backspace');
    await expectInView(page, 'E1');
    await expectSelection(page, 'E1', 'G3');
    await page.keyboard.press('Shift+Backspace');
    // Collapsed: Ctrl+Enter now fills E1 alone.
    await page.keyboard.type('c');
    await page.keyboard.press('Control+Enter');
    await expect(cell(page, 'E1')).toHaveText('c');
    await expect(cell(page, 'F1')).toHaveText('');
    await expectActive(page, 'E1');
});

test('active cell, cases 12 and 13: typing enters the Focus only, and Ctrl+Enter fills the Selection and moves nothing (ADR-0052)', async ({ page }) => {
    await click(page, 'E2');
    await click(page, 'G4', { modifiers: ['Shift'] });
    await page.keyboard.type('x');
    await page.keyboard.press('Enter');
    await expect(cell(page, 'E2')).toHaveText('x');
    await expect(cell(page, 'F2')).toHaveText('');
    await expectActive(page, 'E3');
    await expectSelection(page, 'E2', 'G4');

    await click(page, 'E6');
    await click(page, 'G8', { modifiers: ['Shift'] });
    await page.keyboard.type('y');
    await page.keyboard.press('Control+Enter');
    for (const a1 of ['E6', 'F7', 'G8']) await expect(cell(page, a1)).toHaveText('y');
    await expectActive(page, 'E6');
    await expectSelection(page, 'E6', 'G8');
});

test('item 6: the Name Box selects what it is given and scrolls only as far as it must (ADR-0050, ticket 09)', async ({ page }) => {
    const viewport = await sheet(page).locator('.ex-scroller').boundingBox();
    await nameBox(page).click();
    await nameBox(page).fill('D200');
    await page.keyboard.press('Enter');
    await expectActive(page, 'D200');
    // Excel scrolled from row 1 until D200 was the last whole row on screen (rows 144–201 of 57).
    const d200 = await cell(page, 'D200').boundingBox();
    expect(d200.y + d200.height).toBeLessThanOrEqual(viewport.y + viewport.height + 1);
    expect(d200.y + d200.height).toBeGreaterThan(viewport.y + viewport.height - 2 * d200.height);
    // Back up to B2:C5: Excel scrolled only until row 2 was the first row on screen.
    await nameBox(page).click();
    await nameBox(page).fill('B2:C5');
    await page.keyboard.press('Enter');
    await expectSelection(page, 'B2', 'C5');
    await expectActive(page, 'B2');
    const b2 = await cell(page, 'B2').boundingBox();
    const header = await sheet(page).locator('.ex-header-cell').first().boundingBox();
    expect(Math.abs(b2.y - (header.y + header.height))).toBeLessThanOrEqual(2);
});

// ---- Entering and editing (ADR-0051, ADR-0012) --------------------------------------------------

test('item 7: in Enter mode an arrow key commits and moves (ADR-0012, ticket 08)', async ({ page }) => {
    await click(page, 'E3');
    await page.keyboard.type('abc');
    await page.keyboard.press('ArrowRight');
    await expect(cell(page, 'E3')).toHaveText('abc');
    await expectActive(page, 'F3');
    await click(page, 'E4');
    await page.keyboard.type('5');
    await page.keyboard.press('ArrowDown');
    await expect(cell(page, 'E4')).toHaveText('5');
    await expectActive(page, 'E5');
});

test('item 8: F2 on a Formula shows it with the caret at the end, and the arrows move the caret (ADR-0051, ticket 08)', async ({ page }) => {
    await click(page, 'D2');
    await page.keyboard.press('F2');
    await expect(cellEditor(page)).toHaveValue('=B2*C2');
    const caret = () => cellEditor(page).evaluate((e) => e.selectionStart);
    expect(await caret()).toBe('=B2*C2'.length);
    await page.keyboard.press('ArrowLeft');
    await page.keyboard.press('ArrowLeft');
    expect(await caret()).toBe('=B2*C2'.length - 2);
    await expectActive(page, 'D2');
    // Excel: ↑ in Edit mode puts the caret at the start of the (single-line) text and stays.
    await page.keyboard.press('ArrowUp');
    await expect(cellEditor(page)).toHaveValue('=B2*C2');
    expect(await caret()).toBe(0);
    await page.keyboard.press('Escape');
    await expect(cellEditor(page)).toHaveCount(0);
    expect(await entryOf(page, 'D2')).toBe('=B2*C2');
});

test('item 9: Point mode by keys writes each Reference as the pointer moves (ADR-0051, ticket 11)', async ({ page }) => {
    await click(page, 'E5');
    await page.keyboard.type('=');
    await expect(cellEditor(page)).toHaveValue('=');
    await page.keyboard.press('ArrowDown');
    await expect(cellEditor(page)).toHaveValue('=E6');
    // Excel's Name Box names the pointed cell while pointing (C6 in its screenshot).
    await expect.soft(nameBox(page)).toHaveValue('E6');
    await page.keyboard.press('ArrowDown');
    await expect(cellEditor(page)).toHaveValue('=E7');
    await page.keyboard.press('Shift+ArrowRight');
    await expect(cellEditor(page)).toHaveValue('=E7:F7');
    await page.keyboard.type('+');
    await expect(cellEditor(page)).toHaveValue('=E7:F7+');
    // Once the pointer ends, Excel's Name Box names the edited cell again (C5).
    await expect.soft(nameBox(page)).toHaveValue('E5');
});

test('item 10: Point mode by mouse writes the clicked cell, then the dragged range (ADR-0051, ticket 11)', async ({ page }) => {
    await click(page, 'E10');
    await page.keyboard.type('=');
    // A click while pointing: no waiting for the Name Box, which is the question here.
    await cell(page, 'C3').click({ force: true });
    await page.waitForTimeout(PACE_MS);
    await expect(cellEditor(page)).toHaveValue('=C3');
    // Excel's Name Box names the pointed cell while pointing (C3 in its screenshot).
    await expect.soft(nameBox(page)).toHaveValue('C3');
    const from = await cell(page, 'C3').boundingBox();
    const to = await cell(page, 'D4').boundingBox();
    await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2);
    await page.mouse.down();
    await page.mouse.move(to.x + to.width / 2, to.y + to.height / 2, { steps: 8 });
    await page.mouse.up();
    await expect(cellEditor(page)).toHaveValue('=C3:D4');
});

test('item 11: F2 while pointing turns the arrows back to the caret (ADR-0051, ticket 11)', async ({ page }) => {
    await click(page, 'E5');
    await page.keyboard.type('=');
    await page.keyboard.press('ArrowDown');
    await expect(cellEditor(page)).toHaveValue('=E6');
    await page.keyboard.press('F2');
    await page.keyboard.press('ArrowLeft');
    await page.keyboard.press('ArrowLeft');
    // Excel: the caret moved (=|E6); the pointer did not.
    await expect(cellEditor(page)).toHaveValue('=E6');
    expect(await cellEditor(page).evaluate((e) => e.selectionStart)).toBe(1);
});

test('item 12: typing = in the Formula Bar and pressing Down does not point (ADR-0051, ticket 11)', async ({ page }) => {
    await click(page, 'E5');
    await bar(page).click();
    await page.keyboard.type('=');
    await page.keyboard.press('ArrowDown');
    // Excel stayed in Edit mode with "=" alone.
    await expect(bar(page)).toHaveValue('=');
});

test('item 13: completion lists candidates, Tab takes one with its parenthesis, Escape closes only the list (ADR-0051, ticket 10)', async ({ page }) => {
    const list = sheet(page).locator('.ex-completion-list');
    await click(page, 'E5');
    await page.keyboard.type('=SUM');
    // Excel's list for =SUM: SUM (selected), SUMIF, SUMIFS, SUMPRODUCT, SUMSQ, SUMX2MY2, …;
    // ExSheet offers only its declared functions (ADR-0047), so the first is what is compared.
    await expect(list.locator('.ex-completion-item').first()).toHaveText('SUM');
    await expect(list.locator('.ex-completion-selected')).toHaveText('SUM');
    await page.keyboard.press('Tab');
    await expect(cellEditor(page)).toHaveValue('=SUM(');
    await page.keyboard.press('Escape');
    await expect(cellEditor(page)).toHaveCount(0);
    // Escape with the list open closes the list and keeps the edit; a second Escape cancels it.
    await click(page, 'E5');
    await page.keyboard.type('=SU');
    await expect(list).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(list).toHaveCount(0);
    await expect(cellEditor(page)).toHaveValue('=SU');
    await page.keyboard.press('Escape');
    await expect(cellEditor(page)).toHaveCount(0);
});

test('item 14: the argument hint names every argument and marks the current one (ADR-0051, ticket 10)', async ({ page }) => {
    const hint = sheet(page).locator('.ex-completion-hint');
    await click(page, 'E5');
    await page.keyboard.type('=XLOOKUP(');
    await expect(hint).toHaveText('XLOOKUP(lookup_value, lookup_array, return_array, [if_not_found], [match_mode], [search_mode])');
    await expect(hint.locator('strong, b, em').first()).toHaveText('lookup_value');
    await page.keyboard.type('1,A1:A3,');
    await expect(hint.locator('strong, b, em').first()).toHaveText('return_array');
    await page.keyboard.type('B1:B3,,');
    await expect(hint.locator('strong, b, em').first()).toHaveText('[match_mode]');
});

test('item 15: Escape in the Formula Bar cancels the edit and gives the keys back to the grid (ADR-0051, ticket 09)', async ({ page }) => {
    await click(page, 'E5');
    await bar(page).click();
    await page.keyboard.type('xyz');
    await page.keyboard.press('Escape');
    await expect(cell(page, 'E5')).toHaveText('');
    await expectActive(page, 'E5');
    await page.keyboard.type('7');
    await page.keyboard.press('Enter');
    await expect(cell(page, 'E5')).toHaveText('7');
    await expectActive(page, 'E6');
});

// ---- Clipboard (ADR-0048, ADR-0050) ------------------------------------------------------------

waitsOn(14)('item 16: a copied Formula pastes with its relative References shifted (ADR-0048, ticket 14)', async ({ page }) => {
    await enter(page, 'E1', '=B2');
    await click(page, 'E1');
    await page.keyboard.press('Control+C');
    await click(page, 'E3');
    await page.keyboard.press('Control+V');
    // Excel: =A1 copied from B1 into B3 is =A3.
    expect(await entryOf(page, 'E3')).toBe('=B4');
});

waitsOn(14)('item 17: a 3×3 block pasted onto one cell fills 3×3 and becomes the Selection (ADR-0050, ticket 14)', async ({ page }) => {
    await click(page, 'B2');
    await click(page, 'D4', { modifiers: ['Shift'] });
    await page.keyboard.press('Control+C');
    await click(page, 'C6');
    await page.keyboard.press('Control+V');
    // Excel: E5:G7 selected with the Focus on E5, and B2's =A2*2 written as =E6*2 at F6.
    await expectSelection(page, 'C6', 'E8');
    await expectActive(page, 'C6');
    expect(await entryOf(page, 'E7')).toBe('=C7*D7');
});

// Not a probe a spec can run: it needs Excel on the other side of the clipboard.
test.fixme('item 18: cells from Excel, and to Excel (ADR-0048, ticket 14)', async () => {
    // Observed with clipboard-probe.mjs, which drives Excel too; a spec cannot. Excel → ExSheet:
    // a Formula arrives as its Value, a date as a date (its displayed text, which is ######## when
    // Excel's column was too narrow), and 1,234.50 as 1234.5 without its format. ExSheet → Excel:
    // Excel reads the HTML flavour, so a date arrives as the serial 46291 and #,##0.00 is lost.
});

async function pasteText(page, a1, text) {
    await page.evaluate((t) => navigator.clipboard.writeText(t), text);
    await click(page, a1);
    await page.keyboard.press('Control+V');
}

waitsOn(14)('item 19: pasted text =A1+1 is a Formula (ADR-0048, ticket 14)', async ({ page }) => {
    await pasteText(page, 'E5', '=A1+1');
    expect(await entryOf(page, 'E5')).toBe('=A1+1');
});

waitsOn(14)('item 19: pasted text 1,234 is 1234 formatted #,##0, as Excel takes it (ADR-0048, ticket 14)', async ({ page }) => {
    await pasteText(page, 'E5', '1,234');
    await expect(cell(page, 'E5')).toHaveText('1,234');
});

waitsOn(14)('item 19 and Part A item 10: pasted text =1+ is taken as that text (ADR-0048, ticket 14)', async ({ page }) => {
    await pasteText(page, 'E5', '=1+');
    // Excel took unreadable Formula text as the text itself, with no refusal and no dialog.
    await expect(cell(page, 'E5')).toHaveText('=1+');
});

test('fourth run, active cell item 3: one value of plain text over a range goes into its top-left alone, and the Selection collapses to it (ADR-0014, amended 2026-09-29)', async ({ page }) => {
    // Notepad's clipboard: the text alone, nothing of a spreadsheet's
    // (verification/2026-09-29-windows-excel-4/active-cell.md, item 3).
    await page.evaluate(() => navigator.clipboard.writeText('=A1'));
    await click(page, 'E2');
    await click(page, 'F3', { modifiers: ['Shift'] });
    await expectSelection(page, 'E2', 'F3');
    await page.keyboard.press('Control+V');
    // Excel: B2 alone holds the Formula =A1 (showing 0), the other three cells stay empty, and
    // the Selection is B2.
    await expect(cell(page, 'E2')).not.toHaveText('');
    await expect(nameBox(page)).toHaveValue('E2');
    // E2 alone (a 1×1 Selection is not announced: ADR-0033).
    await expectSelectionIsCell(sheet(page), 'E2');
    // ...and the live region no longer names E2:F3 (the fifth Windows run, Part B).
    await expect(announced(page)).toHaveText('');
    await expect(cell(page, 'F2')).toHaveText('');
    await expect(cell(page, 'E3')).toHaveText('');
    await expect(cell(page, 'F3')).toHaveText('');
    expect(await entryOf(page, 'E2')).toBe('=A1');
});

test('one cell copied inside the Sheet still fills the whole range, as Excel\'s own copy does (ADR-0014, amended 2026-09-29)', async ({ page }) => {
    // C2 of the opening data. Copied without an edit before it: a keyboard copy right after an
    // edit committed with Enter writes nothing on this page today, a separate defect.
    const value = await cell(page, 'C2').textContent();
    expect(value).not.toBe('');
    await page.evaluate(() => navigator.clipboard.writeText('SENTINEL'));
    await click(page, 'C2');
    await page.keyboard.press('Control+C');
    // On the Server host the copy lands a round trip later (ADR-0005).
    await expect.poll(() => page.evaluate(() => navigator.clipboard.readText()), { timeout: 5000 }).not.toBe('SENTINEL');
    await click(page, 'E3');
    await click(page, 'F4', { modifiers: ['Shift'] });
    await page.keyboard.press('Control+V');
    for (const a1 of ['E3', 'F3', 'E4', 'F4']) await expect(cell(page, a1)).toHaveText(value);
    await expectCovers(sheet(page).locator('.ex-selection .ex-range'), sheet(page), 'E3', 'F4');
    await expectActive(page, 'E3');
});

// ---- Fill (ADR-0050, item 5) --------------------------------------------------------------------

test('item 20: the fill handle continues a series, and leaves source and target selected (ADR-0050 item 5, ADR-0052, ticket 15)', async ({ page }) => {
    await enter(page, 'E1', '1');
    await enter(page, 'E2', '3');
    await click(page, 'E1');
    await click(page, 'E2', { modifiers: ['Shift'] });
    await dragFillHandle(page, 'E6');
    await expect(cell(page, 'E6')).toHaveText('11');
    await expectSelection(page, 'E1', 'E6');
    await expectActive(page, 'E1');
});

test('item 20: one number is copied, one date goes on by day, a Formula shifts (ADR-0050, ticket 15)', async ({ page }) => {
    await enter(page, 'E1', '5');
    await click(page, 'E1');
    await dragFillHandle(page, 'E4');
    await expect(cell(page, 'E4')).toHaveText('5');
    await enter(page, 'F1', '9/26/2026');
    await click(page, 'F1');
    await dragFillHandle(page, 'F4');
    await expect(cell(page, 'F4')).toHaveText('9/29/2026');
    await enter(page, 'E8', '=B2*2');
    await click(page, 'E8');
    await dragFillHandle(page, 'E10');
    expect(await entryOf(page, 'E10')).toBe('=B4*2');
});

test('item 20: 1, 2, 4 continue as Excel\'s trend, to the 15 digits Excel keeps (ADR-0050, ticket 15)', async ({ page }) => {
    await enter(page, 'E1', '1');
    await enter(page, 'E2', '2');
    await enter(page, 'E3', '4');
    await click(page, 'E1');
    await click(page, 'E3', { modifiers: ['Shift'] });
    await dragFillHandle(page, 'E5');
    // Excel's Value2 is 5.33333333333333 exactly (rounded to 15 digits), and its bar shows that.
    expect(await entryOf(page, 'E4')).toBe('5.33333333333333');
    expect(await entryOf(page, 'E5')).toBe('6.83333333333333');
});

test('item 20: a series filled up goes backwards, and the Focus stays on the source (ADR-0050, ADR-0052, ticket 15)', async ({ page }) => {
    await enter(page, 'E5', '1');
    await enter(page, 'E6', '3');
    await click(page, 'E5');
    await click(page, 'E6', { modifiers: ['Shift'] });
    await dragFillHandle(page, 'E2');
    await expect(cell(page, 'E4')).toHaveText('-1');
    await expect(cell(page, 'E2')).toHaveText('-5');
    // Excel: A2:A6 selected, the Focus on A5, the source's first cell.
    await expectSelection(page, 'E2', 'E6');
    await expectActive(page, 'E5');
});

test('item 20: text in two cells filled left repeats backwards (ADR-0050, ticket 15)', async ({ page }) => {
    await enter(page, 'D10', 'a');
    await enter(page, 'E10', 'b');
    await click(page, 'D10');
    await click(page, 'E10', { modifiers: ['Shift'] });
    await dragFillHandle(page, 'B10');
    // Excel, from E1:F1 = a, b: D1 = b, C1 = a, B1 = b.
    await expect(cell(page, 'C10')).toHaveText('b');
    await expect(cell(page, 'B10')).toHaveText('a');
});

test('item 20: Item 1, Mon and a date with a time are refused, by decision (ADR-0050)', async ({ page }) => {
    // Excel continues them (Item 2, Tue, the next day at 10:00, losing about 5 ms); ExSheet
    // refuses a pattern it has not implemented rather than fill it with copies.
    for (const typed of ['Item 1', 'Mon', '9/26/2026 10:00']) {
        await enter(page, 'E1', typed);
        await click(page, 'E1');
        await dragFillHandle(page, 'E3');
        await expect(cell(page, 'E2')).toHaveText('');
        await expect(page.locator('.ex-sheet-notice')).not.toHaveText('');
    }
});

test('item 21: with several ranges selected there is no fill handle (ADR-0050, ticket 15)', async ({ page }) => {
    await click(page, 'A1');
    await click(page, 'B2', { modifiers: ['Shift'] });
    await expect(sheet(page).locator('.ex-fill-handle')).toHaveCount(1);
    await click(page, 'D4', { modifiers: ['ControlOrMeta'] });
    await expect(sheet(page).locator('.ex-fill-handle')).toHaveCount(0);
});

// ---- Structure (ADR-0046) -----------------------------------------------------------------------

test('item 22: inserting rows above a selected range keeps the Selection where it was, and the Focus (ADR-0046, ADR-0052, ticket 13)', async ({ page }) => {
    await click(page, 'B3');
    await click(page, 'C4', { modifiers: ['Shift'] });
    await click(page, 'B3', { button: 'right' });
    await page.getByRole('menuitem', { name: /insert rows above/i }).click();
    // Excel: two rows inserted above row 3, the Selection still B3:C4 (now the new rows), the
    // Focus B3, and the new rows formatted as the row above.
    await expectSelection(page, 'B3', 'C4');
    await expectActive(page, 'B3');
    await expect(cell(page, 'A5')).toHaveText('Pears');
});

test('item 23: deleting a row a Formula references writes #REF! in its place (ADR-0046, ticket 13)', async ({ page }) => {
    await enter(page, 'E1', '=B3*2');
    await enter(page, 'F1', '=SUM(B2:B4)');
    await click(page, 'B3', { button: 'right' });
    await page.getByRole('menuitem', { name: /delete rows/i }).click();
    // The deletion lands a round trip after the click on the Server host, and the render that
    // brings it replaces row 1's cells: a click on E1 made in between reached for an element
    // being taken out ("Element is not visible", Windows, fourth run), and one made earlier
    // still would read the Entry from before the deletion. Pears was row 3; Plums moves up.
    await expect(cell(page, 'A3')).toHaveText('Plums');
    // Excel: =A5*2 became =#REF!*2, and =SUM(A4:A6) became =SUM(A4:A5).
    expect(await entryOf(page, 'E1')).toBe('=#REF!*2');
    expect(await entryOf(page, 'F1')).toBe('=SUM(B2:B3)');
});

// Nothing to drive yet: /sheet offers the user no rename.
test.fixme('item 24: a rename is undone by Ctrl+Z, as Excel undoes it (ADR-0048, ticket 12)', async () => {
    // Excel: renamed to Data through Home > Format > Rename Sheet, then Ctrl+Z: Sheet1 again.
    // ExSheet undoes a rename by decision (ADR-0048), so the two agree. /sheet has no rename
    // command for the user to drive yet.
});

// ---- Display (ADR-0016, ADR-0046) ---------------------------------------------------------------

test('item 25: a long text is cut with an ellipsis, by decision; Excel lets it run over (ADR-0046)', async ({ page }) => {
    await enter(page, 'E1', 'A long text that runs well past its column');
    const overflow = await cell(page, 'E1').evaluate((e) => ({
        ellipsis: getComputedStyle(e).textOverflow, clipped: e.scrollWidth > e.clientWidth,
    }));
    expect(overflow).toEqual({ ellipsis: 'ellipsis', clipped: true });
});

test('item 26: a number or a date too wide for a column the user sized shows #### (ADR-0016, SH-26)', async ({ page }) => {
    // Excel, at width 4: 123456 in General, 12345 as 0.00 and a date all showed ####. The item's
    // point is ####, not the format, so the column is sized first, by its grip, as a user sizes it:
    // a column the user sized is never widened, by an entry (SH-26) or by a Number Format (ADR-0071,
    // "Readings until the fourteenth Windows run": Format Cells' OK and SetCellFormatAsync widen as
    // a formatting key does, and only a column the user has not sized). This item used to type a
    // number wider than the default column and format it with the page's #,##0.00 button, which the Sheet Toolbar replaced (ADR-0100). Since ticket 58
    // that format widens the column, as the reading says, and the number shows; it was not ####.
    // Half the default width is about Excel's four characters. The format is the Sheet Toolbar's
    // Number, 0.00, as Excel's case used (ADR-0100); it does not fit.
    const header = sheet(page).locator('.ex-header-cell', { hasText: /^E$/ });
    const before = await header.boundingBox();
    const grip = await header.locator('.ex-resize-grip').boundingBox();
    await page.mouse.move(grip.x + grip.width / 2, grip.y + grip.height / 2);
    await page.mouse.down();
    await page.mouse.move(grip.x + grip.width / 2 - before.width / 2, grip.y + grip.height / 2, { steps: 6 });
    await page.mouse.up();
    await expect.poll(async () => Math.round(before.width - (await header.boundingBox()).width)).toBeGreaterThanOrEqual(Math.round(before.width / 2) - 2);
    const sized = (await header.boundingBox()).width;

    await enter(page, 'E1', '123456');
    await enter(page, 'E2', '12345');
    await click(page, 'E2');
    await chooseNumberFormat(page, 'Number');
    await enter(page, 'E3', '9/26/2026');
    await expect(cell(page, 'E1')).toHaveText(/^#+$/);
    await expect(cell(page, 'E2')).toHaveText(/^#+$/);
    await expect(cell(page, 'E3')).toHaveText(/^#+$/);
    // Nothing widened the column: the entries and the format left it where the user put it.
    expect((await header.boundingBox()).width).toBeCloseTo(sized, 0);
    expect(await entryOf(page, 'E2')).toBe('12345');
});

test('item 27: every cell of a cycle and its dependents is #CIRC!, by decision; Excel shows 0 (ADR-0047)', async ({ page }) => {
    // Excel: one warning dialog when the cycle closes, 0 in all three, and "Circular References: A1"
    // in the status bar.
    await enter(page, 'E1', '=F1');
    await enter(page, 'F1', '=E1');
    await enter(page, 'E2', '=IFERROR(E1,0)');
    await expect(cell(page, 'E1')).toHaveText('#CIRC!');
    await expect(cell(page, 'F1')).toHaveText('#CIRC!');
    await expect(cell(page, 'E2')).toHaveText('#CIRC!');
});
