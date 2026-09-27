import { test, expect, watchNextKey, keySeenUntouched } from './fixtures.mjs';

// Excel's editing keys with real keys on /features (ADR-0007/0035/0046): undo and redo
// forwarded to the Consumer's history, Delete's Clear Intent, Backspace, and the fill keys —
// and the keydown each one takes from the browser, or leaves it. The page's Consumer keeps
// an undo stack and applies every intent; the grid only asks.
//
// Columns: Book (0, not editable), Trader (1, editable), Notional (2, editable), Narrow (3).

test.beforeEach(async ({ page }) => {
    await page.goto('/features');
    await expect(grid(page).locator('.ex-row').first()).toBeVisible();
});

function grid(page) {
    return page.locator('.ex-grid').first();
}

function cell(page, row, column) {
    return grid(page).locator(`[id$='r${row}c${column}']`);
}

async function clickCell(page, row, column, modifiers = []) {
    // Cells are pointer-events: none by design (ADR-0004); the click lands on the Viewport.
    await cell(page, row, column).click({ force: true, modifiers });
}

test('Ctrl+Z and Ctrl+Y reach the Consumer\'s history, and the keydown is taken (KB-37, ADR-0007)', async ({ page }) => {
    const trader = cell(page, 0, 1);
    const before = await trader.textContent();
    await clickCell(page, 0, 1);
    await page.keyboard.type('Undone');
    await page.keyboard.press('Enter');
    await expect(trader).toHaveText('Undone');

    await clickCell(page, 0, 1);
    await watchNextKey(page, ['z', 'Z']);
    await page.keyboard.press('ControlOrMeta+z');
    await expect(page.locator('#history-status')).toContainText('1 changes undone');
    await expect(trader).toHaveText(before);
    expect(await keySeenUntouched(page), 'the grid took Ctrl+Z').not.toBe(true);

    await page.keyboard.press('ControlOrMeta+y');
    await expect(trader).toHaveText('Undone');
    await page.keyboard.press('ControlOrMeta+z');
    await page.keyboard.press('ControlOrMeta+Shift+Z');
    await expect(page.locator('#history-status')).toContainText('1 changes redone');
    await expect(trader).toHaveText('Undone');
});

test('inside the editor Ctrl+Z, Delete and Backspace are the input\'s own (ADR-0007, ED-25)', async ({ page }) => {
    const before = await cell(page, 0, 1).textContent();
    await clickCell(page, 0, 1);
    await page.keyboard.press('F2'); // Caret: the value stays, with the caret at its end
    const editor = grid(page).locator('input.ex-editor');
    await expect(editor).toHaveValue(before);

    await page.keyboard.press('Backspace');
    await page.keyboard.press('Home');
    await page.keyboard.press('Delete');
    await expect(editor).toHaveValue(before.slice(1, -1));

    await page.keyboard.press('ControlOrMeta+z');
    await expect(editor).toHaveCount(1);
    await expect(page.locator('#history-status')).toContainText('—');
});

test('Delete raises one Clear Intent, and the Consumer blanks what it can (ED-24, ADR-0046)', async ({ page }) => {
    await clickCell(page, 0, 1);
    await clickCell(page, 2, 1, ['Shift']);
    await watchNextKey(page, 'Delete');
    await page.keyboard.press('Delete');

    await expect(page.locator('#clear-status')).toContainText('3 cells, 3 applied');
    for (const row of [0, 1, 2]) {
        await expect(cell(page, row, 1)).toHaveText('');
    }
    expect(await keySeenUntouched(page), 'the grid took Delete').not.toBe(true);

    // One intent, so one Ctrl+Z puts all three back.
    await page.keyboard.press('ControlOrMeta+z');
    await expect(page.locator('#history-status')).toContainText('3 changes undone');
    await expect(cell(page, 1, 1)).not.toHaveText('');
});

test('Delete over a non-editable column is refused whole, and says so (ED-24, ADR-0046)', async ({ page }) => {
    const trader = await cell(page, 0, 1).textContent();
    await clickCell(page, 0, 0);
    await page.keyboard.press('Shift+ArrowRight');
    await page.keyboard.press('Delete');

    await expect(page.locator('#paste-refused-status')).toContainText('TargetNotEditable');
    await expect(page.locator('#clear-status')).toContainText('—');
    await expect(cell(page, 0, 1)).toHaveText(trader);
});

test('Backspace opens an empty editor, and Escape leaves the cell as it was (ED-23, ADR-0035)', async ({ page }) => {
    const trader = cell(page, 3, 1);
    const before = await trader.textContent();
    await clickCell(page, 3, 1);
    await page.keyboard.press('Backspace');

    const editor = grid(page).locator('input.ex-editor');
    await expect(editor).toHaveValue('');
    await page.keyboard.press('Escape');
    await expect(editor).toHaveCount(0);
    await expect(trader).toHaveText(before);

    // Typed straight after it, the keys land in the editor Backspace opened (ED-22's hold).
    await page.keyboard.press('Backspace');
    await page.keyboard.type('Novak');
    await page.keyboard.press('Enter');
    await expect(page.locator('#edit-status')).toContainText('Trader=Novak');
});

test('Ctrl+D fills down and Ctrl+R fills right, and the browser keeps neither key (CP-24, ADR-0035)', async ({ page }) => {
    const top = await cell(page, 4, 1).textContent();
    await clickCell(page, 4, 1);
    await clickCell(page, 6, 1, ['Shift']);
    await watchNextKey(page, ['d', 'D']);
    await page.keyboard.press('ControlOrMeta+d');

    await expect(page.locator('#paste-status')).toContainText('2 cells from 1x1, 2 applied');
    await expect(cell(page, 5, 1)).toHaveText(top);
    await expect(cell(page, 6, 1)).toHaveText(top);
    expect(await keySeenUntouched(page), 'the grid took Ctrl+D').not.toBe(true);

    // Ctrl+R on one Notional cell reads the Trader to its left — a name, which this
    // Consumer cannot parse as a notional, so nothing applies; the intent was raised.
    await clickCell(page, 7, 2);
    await watchNextKey(page, ['r', 'R']);
    await page.keyboard.press('ControlOrMeta+r');
    await expect(page.locator('#paste-status')).toContainText('1 cells from 1x1, 0 applied');
    expect(await keySeenUntouched(page), 'the grid took Ctrl+R — the page did not reload').not.toBe(true);
});

test('Ctrl+D on the first row is refused by name (CP-25, ADR-0035)', async ({ page }) => {
    await clickCell(page, 0, 1);
    await page.keyboard.press('ControlOrMeta+d');
    await expect(page.locator('#paste-refused-status')).toContainText('NothingToFillFrom');
});

test('a display-only grid leaves Delete, Backspace, Ctrl+Z, Ctrl+D and Ctrl+R to the page (ED-25, KB-37, ADR-0046/0035/0007)', async ({ page }) => {
    await page.goto('/cells');
    const cells = page.locator('.ex-grid').first();
    await expect(cells.locator('.ex-row').first()).toBeVisible();
    await cells.locator("[id$='r0c0']").click({ force: true });

    const keys = [
        ['Delete', 'Delete'], ['Backspace', 'Backspace'], ['ControlOrMeta+z', ['z', 'Z']],
        ['ControlOrMeta+d', ['d', 'D']], ['ControlOrMeta+r', ['r', 'R']],
    ];
    for (const [press, name] of keys) {
        // Stopped by the test once the page has seen it, so Ctrl+R does not reload the page
        // under it and Ctrl+D opens no bookmark bubble.
        await watchNextKey(page, name, { preventAfter: true });
        await page.keyboard.press(press);
        expect(await keySeenUntouched(page), `${press} reached the page untouched`).toBe(true);
    }
    await expect(cells.locator('.ex-editor')).toHaveCount(0);
});
