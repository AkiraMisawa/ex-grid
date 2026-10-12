import { test, expect, setRoundTrip } from './fixtures.mjs';
import { sheet, openSheet, cell, editor, nameBox, pressCell, toolbarItem, enter, boxOf } from './sheet-helpers.mjs';

// Excel's formatting keys on /sheet (ticket 51; ADR-0071 "Keys", ADR-0050 item 14), with real keys:
// each toggle and Number Format key reaching the Sheet before the browser's own meaning, and with
// an edit open changing nothing and saying why (SH-42, SH-43). The keys are ExGrid's declared
// keys: the capture listener takes them from the browser, in both states, from the list C# hands
// it. A Font is not painted yet (ticket 48), so the page's line under the Sheet reads the Focus
// cell's Cell Format back through CellFormatAt; the Values the cells show are the cells' own.
//
// Whether the browser would have opened the page source for Ctrl+U is not something a page can
// ask; what it can see is that the grid took the key — its default prevented — and that no page
// opened. The eleventh Windows run's Part B showed, with real input, that the page receives these
// keys before Chrome and Edge act on them (verification/2026-10-01-windows-browser-11/keys.md).
//
// The page's own formatting button acts on the Selection as the keys do, including straight after
// a move the Sheet has not heard yet (ticket 56; ADR-0050 item 14's note of 2026-10-01).

test.use({ viewport: { width: 1280, height: 1000 } });

test.beforeEach(async ({ page }) => {
    await openSheet(page);
});

/** The Focus cell's Font as the page reads it back: its emphases, or "regular". */
const font = (page) => page.locator('#sheet-focus-font');

/** The Focus cell's Number Format code as the page reads it back. */
const numberFormat = (page) => page.locator('#sheet-focus-number-format');

/**
 * Notes on the grid's own root whether it took each key: a capture listener registered after the
 * grid's, on the same element, runs after it and reads `defaultPrevented`. It goes with the root
 * when the page does.
 */
async function noteTakenKeys(grid) {
    await grid.evaluate((root) => root.addEventListener('keydown', (event) => {
        if (event.ctrlKey || event.metaKey) {
            root.dataset.testTaken = `${event.key}:${event.defaultPrevented}`;
        }
    }, true));
}

test('SH-42/ADR-0071: Ctrl+B, Ctrl+I, Ctrl+U, Ctrl+5 and Ctrl+2 to Ctrl+4 each toggle the Focus cell, taken from the browser, one Ctrl+Z a press', async ({ page }) => {
    const grid = sheet(page);
    await pressCell(grid, 'A2');
    await expect(font(page)).toHaveText('regular');
    await noteTakenKeys(grid);
    const pages = page.context().pages().length;

    for (const [key, emphasis] of [['b', 'bold'], ['2', 'bold'], ['i', 'italic'], ['3', 'italic'], ['u', 'underline'], ['4', 'underline'], ['5', 'strikethrough']]) {
        await page.keyboard.press(`ControlOrMeta+${key}`);
        await expect(font(page), `Ctrl+${key} sets ${emphasis}`).toHaveText(emphasis);
        await expect(grid).toHaveAttribute('data-test-taken', `${key}:true`);
        await page.keyboard.press(`ControlOrMeta+${key}`);
        await expect(font(page), `Ctrl+${key} again takes ${emphasis} off`).toHaveText('regular');
    }

    // Each press was one step on the Sheet's stack.
    await page.keyboard.press('ControlOrMeta+Z');
    await expect(font(page)).toHaveText('strikethrough');
    await page.keyboard.press('ControlOrMeta+Z');
    await expect(font(page)).toHaveText('regular');
    await page.keyboard.press('ControlOrMeta+Z');
    await expect(font(page)).toHaveText('underline');
    // The cell's Value and the Focus are as they were, and the browser opened nothing — no page
    // source for Ctrl+U, no tab for Ctrl+2.
    await expect(cell(grid, 'A2')).toHaveText('Apples');
    await expect(nameBox(grid)).toHaveValue('A2');
    expect(page.context().pages()).toHaveLength(pages);
});

test('SH-42/ADR-0071: the toggle follows the Focus cell over a range', async ({ page }) => {
    const grid = sheet(page);
    await pressCell(grid, 'A2');
    await page.keyboard.press('ControlOrMeta+b');
    await expect(font(page)).toHaveText('bold');

    // Focus A2 (bold) over A2:A3: both become plain.
    await page.keyboard.press('Shift+ArrowDown');
    await page.keyboard.press('ControlOrMeta+b');
    await expect(font(page)).toHaveText('regular');
    await pressCell(grid, 'A3');
    await expect(font(page)).toHaveText('regular');

    // Focus A3 (plain) over A2:A3: both become bold.
    await page.keyboard.press('Shift+ArrowUp');
    await page.keyboard.press('ControlOrMeta+b');
    await expect(font(page)).toHaveText('bold');
    await pressCell(grid, 'A2');
    await expect(font(page)).toHaveText('bold');
});

test('ADR-0050 item 14 (note of 2026-10-01)/ticket 56/ADR-0100: a Toolbar Item pressed straight after Shift+ArrowDown formats the extended range, as one step', async ({ page }) => {
    const grid = sheet(page);
    await pressCell(grid, 'B2');
    await expect(cell(grid, 'B3')).toHaveText('7');
    await setRoundTrip(150);

    // On the Server host the grid raises the move a round trip after the key, from after the render
    // that shows it; the item's click, sent straight after the key, reaches the Sheet first. On
    // WebAssembly this is the case without a round trip.
    await page.keyboard.press('Shift+ArrowDown');
    await toolbarItem(page, 'Percent Style').click();

    await expect(cell(grid, 'B2')).toHaveText('1200%');
    await expect(cell(grid, 'B3')).toHaveText('700%');
    await expect(cell(grid, 'B4')).toHaveText('20');
    await setRoundTrip(0);
    await page.locator('#sheet-undo').click();
    await expect(cell(grid, 'B2')).toHaveText('12');
    await expect(cell(grid, 'B3')).toHaveText('7');
});

test('SH-42/ADR-0071: Ctrl+Shift with ~ ! @ # $ % ^ applies Excel\'s Number Formats under en-US, by the character typed', async ({ page }) => {
    const grid = sheet(page);
    await pressCell(grid, 'C2');
    await expect(numberFormat(page)).toHaveText('General');
    await noteTakenKeys(grid);

    // Typed as characters with Shift, as a US layout types them; and # without Shift, as a UK
    // layout types it (the eleventh Windows run, case 19).
    for (const [keys, character, code] of [
        ['Shift+Digit1', '!', '#,##0.00'],
        ['Shift+Digit2', '@', 'h:mm AM/PM'],
        ['Shift+Digit3', '#', 'd-mmm-yy'],
        ['Shift+Digit5', '%', '0%'],
        ['#', '#', 'd-mmm-yy'],
        ['Shift+Digit4', '$', '$#,##0.00_);[Red]($#,##0.00)'],
        ['Shift+Digit6', '^', '0.00E+00'],
        ['Shift+Backquote', '~', 'General'],
    ]) {
        await page.keyboard.press(`ControlOrMeta+${keys}`);
        await expect(numberFormat(page), `Ctrl+${keys}`).toHaveText(code);
        await expect(grid).toHaveAttribute('data-test-taken', `${character}:true`);
    }
    await page.keyboard.press('ControlOrMeta+Shift+Digit4');
    await expect(cell(grid, 'C2')).toHaveText(/^\$0\.50\s*$/);
});

test('SH-43/ADR-0071: with an edit open, Ctrl+U changes nothing, is taken from the browser, and says why', async ({ page }) => {
    const grid = sheet(page);
    await pressCell(grid, 'F2');
    await page.keyboard.type('abc');
    await expect(editor(grid)).toHaveValue('abc');
    await noteTakenKeys(grid);
    const pages = page.context().pages().length;

    await page.keyboard.press('ControlOrMeta+u');

    await expect(grid).toHaveAttribute('data-test-taken', 'u:true');
    await expect(page.locator('.ex-sheet-notice')).toHaveText(/^Nothing was formatted: a cell is being edited/);
    // Raised to the Consumer: the page says it in its status line, as it says a refused command.
    await expect(page.locator('#sheet-status')).toHaveText(/^Nothing was formatted/);
    await expect(editor(grid)).toHaveValue('abc');
    await expect(editor(grid)).toBeFocused();
    expect(page.context().pages()).toHaveLength(pages);

    // Ctrl+B and Ctrl+Shift+$ the same, and the edit still takes typing and commits as typed.
    await page.keyboard.press('ControlOrMeta+b');
    await page.keyboard.press('ControlOrMeta+Shift+Digit4');
    await page.keyboard.type('d');
    await expect(editor(grid)).toHaveValue('abcd');
    await page.keyboard.press('Enter');
    await expect(editor(grid)).toHaveCount(0);
    await expect(cell(grid, 'F2')).toHaveText('abcd');
    await pressCell(grid, 'F2');
    await expect(font(page)).toHaveText('regular');
    await expect(numberFormat(page)).toHaveText('General');
});

// The seventeenth Windows run's h7 (ADR-0071; ticket 100's path): over a number showing ####,
// Excel's Ctrl+U widened a column nobody had sized, from 8.09 to 11.73, and Ctrl+I did not; at a
// width set through COM, neither did. E1 and F1 read C9, so the number arrives by recalculation,
// which widens nothing, as Excel's set through COM did: typed into E1 it would have widened E.
test('ADR-0071/SH-42 (the seventeenth run, h7): Ctrl+U over #### widens a column at the standard width and leaves a sized one at its width; Ctrl+I widens neither; one Ctrl+Z takes back both', async ({ page }) => {
    const grid = sheet(page);
    const header = (name) => grid.locator('.ex-header-cell', { hasText: new RegExp(`^${name}$`) });
    // While C9 is empty each shows 89.12, which fits, so neither the Formulas nor the format widens.
    await enter(page, grid, 'E1', '=C9*100+89.12');
    await enter(page, grid, 'F1', '=E1');
    await pressCell(grid, 'E1');
    await page.keyboard.press('Shift+ArrowRight');
    await page.keyboard.press('ControlOrMeta+Shift+Digit1');
    await expect(numberFormat(page)).toHaveText('#,##0.00');
    await expect(cell(grid, 'F1')).toHaveText('89.12');
    const standard = (await boxOf(header('E'))).width;

    // F is sized narrower by its grip, as a user sizes it, to about h7's width 5.
    const grip = await boxOf(header('F').locator('.ex-resize-grip'));
    await page.mouse.move(grip.x + grip.width / 2, grip.y + grip.height / 2);
    await page.mouse.down();
    await page.mouse.move(grip.x + grip.width / 2 - 40, grip.y + grip.height / 2, { steps: 6 });
    await page.mouse.up();
    await expect.poll(async () => Math.round(standard - (await boxOf(header('F'))).width)).toBeGreaterThanOrEqual(35);
    const sized = (await boxOf(header('F'))).width;

    // 1234567 fits C in General, so C stays; E1 and F1 now hold 123456789.12, which shows ####.
    await enter(page, grid, 'C9', '1234567');
    await expect(cell(grid, 'E1')).toHaveText(/^#+$/);
    await expect(cell(grid, 'F1')).toHaveText(/^#+$/);
    expect((await boxOf(header('E'))).width).toBeCloseTo(standard, 0);

    await pressCell(grid, 'E1');
    await page.keyboard.press('Shift+ArrowRight');
    await page.keyboard.press('ControlOrMeta+i');
    await expect(font(page)).toHaveText('italic');
    await expect(cell(grid, 'E1')).toHaveText(/^#+$/);
    expect((await boxOf(header('E'))).width).toBeCloseTo(standard, 0);

    await page.keyboard.press('ControlOrMeta+u');
    await expect(font(page)).toHaveText('italic underline');
    await expect(cell(grid, 'E1')).toHaveText('123,456,789.12');
    await expect.poll(async () => (await boxOf(header('E'))).width).toBeGreaterThan(standard + 10);
    await expect(cell(grid, 'F1')).toHaveText(/^#+$/);
    expect((await boxOf(header('F'))).width).toBeCloseTo(sized, 0);

    // The width is a step of the key's own: one Ctrl+Z takes back the underline and the width.
    await page.keyboard.press('ControlOrMeta+Z');
    await expect(font(page)).toHaveText('italic');
    await expect(cell(grid, 'E1')).toHaveText(/^#+$/);
    await expect.poll(async () => (await boxOf(header('E'))).width).toBeCloseTo(standard, 0);
    expect((await boxOf(header('F'))).width).toBeCloseTo(sized, 0);
});
