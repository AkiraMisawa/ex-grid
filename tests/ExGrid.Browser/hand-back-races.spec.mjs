import { test, expect, circuitQuiet } from './fixtures.mjs';
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
