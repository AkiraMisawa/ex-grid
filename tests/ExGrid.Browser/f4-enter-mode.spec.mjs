import { test, expect, circuitQuiet } from './fixtures.mjs';
import {
    sheet, cell, openSheet, clickBarEnd, editor, bar, nameBox, expectFocusAt, goTo, expectCovers, pressCell,
    expectSelectionIsCell, spanOf,
} from './sheet-helpers.mjs';

// What run 18a read of Excel's F4 and Enter mode (docs/specs/exsheet/run-18/f4-enter-mode.md;
// verification/2026-10-14-windows-readings-18a/report.md, "f4-enter-mode.md"), typed into /sheet as
// the run typed it into a blank workbook: where F4 leaves a selection and where a caret (ADR-0051,
// answers 1, 2 and 4), the Primary Modifier's arrows pointing at the data edge after = (answer 5),
// Shift+Home extending a pointed range and pointing from the edited cell (answers 3 and 8), and the
// directions R1 (vii) did not ask, which commit and move or extend the Selection (ADR-0010, ED-2;
// answer 7). Keys go in at machine speed; what they leave is read once it is there, and on the
// Server host once the circuit has gone quiet (ADR-0220, ADR-0056). Column D of /sheet holds
// nothing below D5, as Excel's column held nothing.

test.use({ viewport: { width: 1280, height: 1000 } });

test.beforeEach(async ({ page }) => {
    await openSheet(page);
});

const selectionOf = (locator) => locator.evaluate((input) => [input.selectionStart, input.selectionEnd]);

/** Waits for the field to hold `text` with `selection` in it, and for the circuit to fall quiet. */
async function expectSettled(field, text, selection) {
    await expect(field).toHaveValue(text);
    await expect.poll(() => selectionOf(field)).toEqual(selection);
    await circuitQuiet();
    await expect(field).toHaveValue(text);
    expect(await selectionOf(field)).toEqual(selection);
}

/**
 * Waits until the pointing outline covers from `from` to `to`. /sheet pins column A, and an outline
 * across the pinned boundary is drawn in each layer it reaches, the pinned one outside
 * `.ex-selection` (ADR-0206): their union is read.
 */
async function expectPointCovers(grid, from, to) {
    const want = await spanOf(grid, from, to);
    await expect.poll(async () => {
        const boxes = (await grid.locator('.ex-point').evaluateAll((all) => all.map((e) => {
            const r = e.getBoundingClientRect();
            return { left: r.left, top: r.top, right: r.right, bottom: r.bottom };
        }))).filter((b) => b.right > b.left && b.bottom > b.top);
        if (boxes.length === 0) {
            return 'not painted';
        }
        const union = {
            x: Math.min(...boxes.map((b) => b.left)), y: Math.min(...boxes.map((b) => b.top)),
            right: Math.max(...boxes.map((b) => b.right)), bottom: Math.max(...boxes.map((b) => b.bottom)),
        };
        const near = (p, q) => Math.abs(p - q) <= 1.5;
        return near(union.x, want.x) && near(union.y, want.y) && near(union.right, want.x + want.width)
            && near(union.bottom, want.y + want.height)
            ? 'covers'
            : JSON.stringify({ union, want });
    }).toBe('covers');
}

/** Ends the open edit with Escape, the Focus back on `address`. */
async function cancel(page, grid, address) {
    await page.keyboard.press('Escape');
    await expect(editor(grid)).toHaveCount(0);
    await expectFocusAt(grid, address);
}

/**
 * Presses the Formula Bar's text just after its first `characters` characters, as a user presses
 * between two of them. Where that is is measured here, in the field's own font, on a canvas: the
 * grid itself measures nothing (ADR-0021), and nothing is added to the page.
 */
async function pressBarAfter(grid, characters) {
    const field = bar(grid);
    const x = await field.evaluate((input, count) => {
        const style = getComputedStyle(input);
        const context = document.createElement('canvas').getContext('2d');
        context.font = style.font;
        return parseFloat(style.borderLeftWidth) + parseFloat(style.paddingLeft)
            + context.measureText(input.value.slice(0, count)).width - input.scrollLeft;
    }, characters);
    const box = await field.boundingBox();
    // A pixel past the boundary, well inside the half of the next character that places the caret
    // before it.
    await field.click({ position: { x: x + 1, y: box.height / 2 } });
}

test('ADR-0051/SH-28: F4 leaves a caret where the caret stood at the text\'s end, and selects what it rewrote where it stood inside the text (run 18a, answers 1 and 2)', async ({ page }) => {
    const grid = sheet(page);

    // 1. Typed into the bar, F2 (Enter mode), F4: a caret at the end.
    await pressCell(grid, 'D10');
    await clickBarEnd(grid);
    await page.keyboard.type('=A1+B2');
    await page.keyboard.press('F2');
    await page.keyboard.press('F4');
    await expectSettled(bar(grid), '=A1+$B$2', [8, 8]);
    await cancel(page, grid, 'D10');

    // 2 (a). ←, → puts the caret back at the end: F4 leaves a caret.
    await clickBarEnd(grid);
    await page.keyboard.type('=A1+B2');
    await page.keyboard.press('ArrowLeft');
    await page.keyboard.press('ArrowRight');
    await page.keyboard.press('F4');
    await expectSettled(bar(grid), '=A1+$B$2', [8, 8]);
    await cancel(page, grid, 'D10');

    // 2 (b). ←×3 and a digit typed there: the caret after A10, inside the text, and F4 selects $A$10.
    await clickBarEnd(grid);
    await page.keyboard.type('=A1+B2');
    await page.keyboard.press('ArrowLeft');
    await page.keyboard.press('ArrowLeft');
    await page.keyboard.press('ArrowLeft');
    await page.keyboard.type('0');
    await page.keyboard.press('F4');
    await expectSettled(bar(grid), '=$A$10+B2', [1, 6]);
    await cancel(page, grid, 'D10');

    // 2 (c). =A1+B2 committed into D10 and the cell pressed again at once, as the run did; then a
    // press into the Formula's text right after its first Reference, and F4: that Reference selected.
    await page.keyboard.type('=A1+B2');
    await page.keyboard.press('Enter');
    await pressCell(grid, 'D10');
    await expect(bar(grid)).toHaveValue('=A1+B2');
    // Where the press into the bar goes is read from the bar's text, so it is read once the grid
    // has settled: the Focus on D10 and the bar's text are also what the page showed before the
    // Enter's move was painted, and the move's render, landing between the read and the press,
    // would leave an empty bar to press into. (A press made on =A1+B2 keeps its caret through the
    // renders after it, ticket 202; hand-back-races.spec.mjs drives that at once.)
    await circuitQuiet();
    await expectFocusAt(grid, 'D10');
    await pressBarAfter(grid, 3);
    await page.keyboard.press('F4');
    await expectSettled(bar(grid), '=$A$1+B2', [1, 5]);
    await cancel(page, grid, 'D10');
});

test('ADR-0051/DC-19: after =, Ctrl+↓ points at the column\'s data edge, and Ctrl+Shift+↓ points from the edited cell to it; nothing commits (run 18a, answer 5)', async ({ page }) => {
    const grid = sheet(page);
    const point = grid.locator('.ex-selection .ex-point');

    await pressCell(grid, 'D10');
    await page.keyboard.type('=');
    await page.keyboard.press('Control+ArrowDown');

    await expect(editor(grid)).toHaveValue('=D1048576');
    await expect(bar(grid)).toHaveValue('=D1048576');
    await expect(nameBox(grid)).toHaveValue('D1048576');
    await expect(point.first()).toBeVisible();
    await circuitQuiet();
    await expect(editor(grid)).toHaveValue('=D1048576');
    // Pointing goes on: ↑ moves the outline on from where it went.
    await page.keyboard.press('ArrowUp');
    await expect(editor(grid)).toHaveValue('=D1048575');
    await page.keyboard.press('Escape');
    await expect(editor(grid)).toHaveCount(0);
    // The view stayed where the outline went; Ctrl+Backspace brings the Focus back into it.
    await page.keyboard.press('Control+Backspace');
    await expectFocusAt(grid, 'D10');
    await expect(bar(grid)).toHaveValue('');

    await page.keyboard.type('=');
    await page.keyboard.press('Control+Shift+ArrowDown');

    await expect(editor(grid)).toHaveValue('=D10:D1048576');
    await expect(nameBox(grid)).toHaveValue('D10');
    await expect(point.first()).toBeVisible();
    await circuitQuiet();
    await expect(editor(grid)).toHaveValue('=D10:D1048576');
    await page.keyboard.press('Escape');
    await expect(editor(grid)).toHaveCount(0);
    await page.keyboard.press('Control+Backspace');
    await expectFocusAt(grid, 'D10');
    await expect(bar(grid)).toHaveValue('');
});

test('ADR-0051/ED-2: Shift+Home extends a pointed range from its fixed end, and after = points from the edited cell, in Point (run 18a, answers 3 and 8)', async ({ page }) => {
    const grid = sheet(page);

    await pressCell(grid, 'D10');
    await page.keyboard.type('=');
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('Shift+ArrowDown');
    await page.keyboard.press('Shift+Home');

    await expect(editor(grid)).toHaveValue('=A11:D12');
    await expectPointCovers(grid, 'A11', 'D12');
    await expect(nameBox(grid)).toHaveValue('D11');
    await circuitQuiet();
    await expect(editor(grid)).toHaveValue('=A11:D12');
    // Still in Point: ↓ moves the outline on.
    await page.keyboard.press('ArrowDown');
    await expect(editor(grid)).toHaveValue('=D12');
    await cancel(page, grid, 'D10');

    await page.keyboard.type('=');
    await page.keyboard.press('Shift+Home');

    await expect(editor(grid)).toHaveValue('=A10:D10');
    await expectPointCovers(grid, 'A10', 'D10');
    await circuitQuiet();
    await expect(editor(grid)).toHaveValue('=A10:D10');
    await page.keyboard.press('ArrowDown');
    await expect(editor(grid)).toHaveValue('=D11');
    await cancel(page, grid, 'D10');
});

// Each from abc typed into D10, as run 18a typed it. /sheet's column D holds =SUM(D2:D4) in D5, so
// Ctrl+↑ stops there where Excel's empty column took it to D1: the data edge (ADR-0050, item 2), as
// with no edit open.
test('ADR-0010/ED-2: typed into a cell, Shift+↑, Ctrl+← and Ctrl+↑ commit and move or extend the Selection as with no edit open (run 18a, answer 7)', async ({ page }) => {
    const grid = sheet(page);
    const range = grid.locator('.ex-selection .ex-range');
    await pressCell(grid, 'D10');
    for (const [key, check] of [
        ['Shift+ArrowUp', async () => {
            await expectCovers(range, grid, 'D9', 'D10');
            await expect(nameBox(grid)).toHaveValue('D10');
        }],
        ['Control+ArrowLeft', async () => {
            await expect(nameBox(grid)).toHaveValue('A10');
            await expectSelectionIsCell(grid, 'A10');
        }],
        ['Control+ArrowUp', async () => {
            await expect(nameBox(grid)).toHaveValue('D5');
            await expectSelectionIsCell(grid, 'D5');
        }],
        ['Control+Shift+ArrowLeft', async () => {
            await expectCovers(range, grid, 'A10', 'D10');
            await expect(nameBox(grid)).toHaveValue('D10');
        }],
        ['Control+Shift+ArrowUp', async () => {
            await expectCovers(range, grid, 'D5', 'D10');
            await expect(nameBox(grid)).toHaveValue('D10');
        }],
    ]) {
        await page.keyboard.type('abc');
        await page.keyboard.press(key);

        await expect(editor(grid)).toHaveCount(0);
        await check();
        await circuitQuiet();
        // Back to D10 by the Name Box, as the run went back; after Shift+↑ and the extends, it names
        // the Focus the Selection already has, and collapses the Selection onto it.
        await goTo(grid, 'D10');
        await expect(bar(grid)).toHaveValue('abc');
    }
});

test('ADR-0010/ED-2: typed into a cell, Ctrl+↓ commits and moves to the column\'s last row, and Ctrl+Shift+↓ commits and selects to it (run 18a, answer 7)', async ({ page }) => {
    const grid = sheet(page);

    await pressCell(grid, 'D10');
    await page.keyboard.type('abc');
    await page.keyboard.press('Control+ArrowDown');

    await expect(editor(grid)).toHaveCount(0);
    await expect(nameBox(grid)).toHaveValue('D1048576');
    await expectSelectionIsCell(grid, 'D1048576');
    await circuitQuiet();
    await goTo(grid, 'D10');
    await expect(bar(grid)).toHaveValue('abc');

    await page.keyboard.type('xyz');
    await page.keyboard.press('Control+Shift+ArrowDown');

    await expect(editor(grid)).toHaveCount(0);
    await expect(nameBox(grid)).toHaveValue('D10');
    // The Extent is kept in view, at the column's last row, inside the range; the Focus, brought
    // back into view, is inside it too, and holds what was committed.
    await expectRangeHolds(grid, 'D1048576');
    await page.keyboard.press('Control+Backspace');
    await expectRangeHolds(grid, 'D10');
    await expectRangeHolds(grid, 'D11');
    await expect(nameBox(grid)).toHaveValue('D10');
    await expect(bar(grid)).toHaveValue('xyz');
});

/** Waits until the painted Selection range holds the cell at `address`, whole. */
async function expectRangeHolds(grid, address) {
    await expect.poll(async () => {
        const box = await grid.locator('.ex-selection .ex-range').boundingBox();
        const held = await cell(grid, address).boundingBox();
        if (!box || !held) {
            return 'not painted';
        }
        return box.x <= held.x + 1 && box.y <= held.y + 1 && box.x + box.width >= held.x + held.width - 1
            && box.y + box.height >= held.y + held.height - 1
            ? 'holds'
            : JSON.stringify({ box, held });
    }).toBe('holds');
}
