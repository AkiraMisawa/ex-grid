import { test, expect, circuitQuiet, setRoundTrip } from './fixtures.mjs';
import {
    sheet, cell, openSheet, clickCell, editor, bar, nameBox, expectFocusAt, pressCell, typeIntoNameBox,
} from './sheet-helpers.mjs';

// Gestures that hand the keyboard or the Focus on, followed at once by the next input, as a user
// typing quickly or a script drives them (ADR-0220, the spine's sixth principle). Inputs go in at
// machine speed, with no wait between them; what they leave is read once it is there, and on the
// Server host once the circuit has gone quiet (ADR-0056). Column D of /sheet holds nothing below D5.

test.use({ viewport: { width: 1280, height: 1000 } });

test.beforeEach(async ({ page }) => {
    await openSheet(page);
});

test('ADR-0010/ADR-0051: keys typed straight after the Name Box\'s Enter names the Focus open an edit on it', async ({ page }) => {
    const grid = sheet(page);
    await pressCell(grid, 'D10');

    await typeIntoNameBox(grid, 'D10');
    await nameBox(grid).press('Enter');
    await page.keyboard.type('abc');

    await expect(editor(grid)).toHaveValue('abc');
    await circuitQuiet();
    await expect(editor(grid)).toHaveValue('abc');
    await expect(nameBox(grid)).toHaveValue('D10');
    await page.keyboard.press('Enter');
    await expectFocusAt(grid, 'D11');
    await expect(cell(grid, 'D10')).toHaveText('abc');
});

test('ADR-0010/ADR-0051: keys typed straight after the Name Box\'s Enter names another cell open an edit on that cell', async ({ page }) => {
    const grid = sheet(page);
    await pressCell(grid, 'D10');

    await typeIntoNameBox(grid, 'D12');
    await nameBox(grid).press('Enter');
    await page.keyboard.type('abc');

    await expect(editor(grid)).toHaveValue('abc');
    await circuitQuiet();
    await expect(editor(grid)).toHaveValue('abc');
    await expect(nameBox(grid)).toHaveValue('D12');
    await page.keyboard.press('Enter');
    await expectFocusAt(grid, 'D13');
    await expect(cell(grid, 'D12')).toHaveText('abc');
    await expect(cell(grid, 'D10')).toHaveText('');
});

test('ADR-0010: a press straight after the Enter that commits a Formula lands on the cell pressed', async ({ page }) => {
    const grid = sheet(page);
    await pressCell(grid, 'D10');

    await page.keyboard.type('=A1+B2');
    await page.keyboard.press('Enter');
    await clickCell(grid, 'D10');

    await expectFocusAt(grid, 'D10');
    await circuitQuiet();
    await expectFocusAt(grid, 'D10');
    await expect(editor(grid)).toHaveCount(0);
    await expect(bar(grid)).toHaveValue('=A1+B2');
});

/**
 * Presses the Formula Bar's text just after the first `characters` characters of `text`, the text
 * the bar shows as the press is made. Where that is is worked out here, in the field's own font, on
 * a canvas, from the text given rather than from the field's value, which a render in flight may be
 * rewriting: the press goes in at machine speed, as a user's who reads the bar does (ADR-0220).
 */
async function pressBarTextAfter(grid, text, characters) {
    const field = bar(grid);
    const x = await field.evaluate((input, [shown, count]) => {
        const style = getComputedStyle(input);
        const context = document.createElement('canvas').getContext('2d');
        context.font = style.font;
        return parseFloat(style.borderLeftWidth) + parseFloat(style.paddingLeft)
            + context.measureText(shown.slice(0, count)).width;
    }, [text, characters]);
    const box = await field.boundingBox();
    await field.click({ position: { x: x + 1, y: box.height / 2 } });
}

const selectionOf = (locator) => locator.evaluate((input) => [input.selectionStart, input.selectionEnd]);

// The bar mirrors the edit a round trip behind the typing on a circuit, so the test reads it showing
// =A1+B2 before Enter: the press into the bar is aimed at that text, as a user's who reads it is. On
// a 150 ms circuit, Enter, the press on D10 and the press into the bar then go in before the renders
// they cause land, which rewrite the bar under the caret: the window ticket 202 needs, made wide on
// purpose (ADR-0220, rule 5). WebAssembly runs it as the case without one.
test('ADR-0051/ADR-0010: on a 150 ms circuit, a press into the Formula Bar\'s text straight after Enter and a press on the cell keeps its caret, and F4 cycles the Reference pressed', async ({ page }) => {
    const grid = sheet(page);
    await pressCell(grid, 'D10');
    await setRoundTrip(150);
    try {
        await page.keyboard.type('=A1+B2');
        await expect(bar(grid)).toHaveValue('=A1+B2');
        await page.keyboard.press('Enter');
        await clickCell(grid, 'D10');
        await pressBarTextAfter(grid, '=A1+B2', 3);
        await page.keyboard.press('F4');

        await expect(bar(grid)).toHaveValue('=$A$1+B2');
        await expect.poll(() => selectionOf(bar(grid))).toEqual([1, 5]);
        await circuitQuiet();
        await expect(bar(grid)).toHaveValue('=$A$1+B2');
        expect(await selectionOf(bar(grid))).toEqual([1, 5]);
        await expectFocusAt(grid, 'D10');
    } finally {
        await setRoundTrip(0);
    }
});
