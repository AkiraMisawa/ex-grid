import { test, expect, setRoundTrip, circuitQuiet } from './fixtures.mjs';
import { sheet, openSheet, cell, pressCell, expectFocusAt, toolbarItem, toolbarArrow, editor } from './sheet-helpers.mjs';
import { expectKeyboardOn } from './keyboard.mjs';
import { watchHiddenFocus } from './hidden-focus.mjs';

// The Sheet Toolbar (ADR-0100, ticket 54; SH-48 to SH-50), on /sheet under both Chromes: above the
// Formula Bar, inside the Sheet's box and outside the grid's root, the grid keeping the rows the page
// gave it; Bold shown pressed while the Focus cell is bold; a pointer press that leaves the keyboard
// on the Sheet, so the next key goes to the cell; every item greyed out while a cell is edited; and a
// colour chosen from the list painted and kept on the face. The press straight after Shift+ArrowDown
// on the Server host is format-keys.spec.mjs's.

// Tall enough that the Sheet, toolbar to scrollbar, is inside the window.
test.use({ viewport: { width: 1280, height: 1000 } });

/** The Sheet Toolbar's band. */
function toolbar(page) {
    return page.locator('.ex-sheet-toolbar').first();
}

/**
 * The name of the item the band names as active (aria-activedescendant), under either Chrome: the
 * built-in one puts the id on the control, MudBlazor's on the control's wrapper.
 */
function activeItemName(page) {
    return toolbar(page).evaluate((band) => {
        const id = band.getAttribute('aria-activedescendant');
        const item = id ? document.getElementById(id) : null;
        if (!item) {
            return null;
        }
        return item.getAttribute('aria-label') ?? item.querySelector('[aria-label]')?.getAttribute('aria-label') ?? null;
    });
}

/**
 * The toolbar's lists that stand open, under either Chrome: the built-in one in the grid's popover,
 * MudBlazor's in an open MudPopover.
 */
function openLists(page) {
    return page.locator('.ex-sheet-choices, .mud-popover-open .mud-ex-sheet-toolbar-choices');
}

/** Format Cells: the grid's popover under the built-in Chrome, a MudDialog under the other. */
function formatCellsOf(page, grid, chrome) {
    return chrome === 'mud' ? page.locator('.mud-dialog.mud-ex-sheet-format-cells') : grid.locator('.ex-popover-consumer');
}

/**
 * Types KeyTips: Alt, then each letter, at machine speed. The letters typed before the KeyTips have
 * taken the keyboard are held by the grid's key gate until the Sheet has answered Alt's release, and
 * then handed to the toolbar in order (ADR-0050 item 16 and ADR-0100, decided with the user on
 * 2026-10-12): nothing here waits for the toolbar.
 */
async function keyTips(page, ...letters) {
    await page.keyboard.press('Alt');
    for (const letter of letters) {
        await page.keyboard.press(letter);
    }
}

for (const chrome of ['builtin', 'mud']) {
    test.describe(`under the ${chrome} Chrome`, () => {
        test.beforeEach(async ({ page }) => {
            await openSheet(page, chrome);
        });

        test(`SH-48/ADR-0100: the toolbar stands above the Formula Bar, outside the grid's root, and the grid below it (${chrome})`, async ({ page }) => {
            const grid = sheet(page);
            const band = toolbar(page);
            await expect(band).toBeVisible();
            await expect(band).toHaveAttribute('role', 'toolbar');
            // Outside the grid's root: the grid's key listener never hears a key pressed on it.
            await expect(grid.locator('.ex-sheet-toolbar')).toHaveCount(0);
            const bandBox = await band.boundingBox();
            const barBox = await grid.locator('.ex-formula-bar').boundingBox();
            const gridBox = await grid.boundingBox();
            // The toolbar's bottom meets the grid's top, within a device pixel, and it is as wide.
            expect(Math.abs(bandBox.y + bandBox.height - gridBox.y)).toBeLessThanOrEqual(1);
            expect(barBox.y).toBeGreaterThanOrEqual(bandBox.y + bandBox.height - 1);
            expect(Math.abs(bandBox.width - gridBox.width)).toBeLessThanOrEqual(1);
            // Two Toolbar Rows, the page's formatting and its own, each the same height.
            const rows = band.locator('.ex-sheet-toolbar-row');
            await expect(rows).toHaveCount(2);
            const first = await rows.nth(0).boundingBox();
            const second = await rows.nth(1).boundingBox();
            expect(Math.abs(first.height - second.height)).toBeLessThanOrEqual(0.5);
            expect(Math.abs(bandBox.height - first.height - second.height)).toBeLessThanOrEqual(1);
        });

        test(`SH-50/ADR-0100: Bold sets bold, is shown pressed over a bold Focus, and the keyboard stays on the Sheet (${chrome})`, async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'A2');
            const bold = toolbarItem(page, 'Bold');
            await expect(bold).toHaveAttribute('aria-pressed', 'false');

            await bold.click();

            await expect(page.locator('#sheet-focus-font')).toHaveText('bold');
            await expect(bold).toHaveAttribute('aria-pressed', 'true');
            // The press did not take DOM focus: the next arrow moves the Focus, as after Excel's ribbon.
            await expectKeyboardOn(grid);
            await page.keyboard.press('ArrowDown');
            await expectFocusAt(grid, 'A3');
            await expect(bold).toHaveAttribute('aria-pressed', 'false');
            await page.keyboard.press('ArrowUp');
            await expect(bold).toHaveAttribute('aria-pressed', 'true');
            await page.keyboard.press('Control+z');
            await expect(page.locator('#sheet-focus-font')).toHaveText('regular');
        });

        test(`SH-50/ADR-0100: while a cell is edited every item is greyed out, the page's own included (${chrome})`, async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'C4');
            await expect(toolbarItem(page, 'Bold')).toBeEnabled();
            await expect(toolbarItem(page, 'Mark reviewed')).toBeEnabled();

            await page.keyboard.type('9');
            await expect(editor(grid)).toBeVisible();

            await expect(toolbarItem(page, 'Bold')).toBeDisabled();
            await expect(toolbarItem(page, 'Number Format')).toBeDisabled();
            await expect(toolbarItem(page, 'Mark reviewed')).toBeDisabled();
            await page.keyboard.press('Escape');
            await expect(toolbarItem(page, 'Bold')).toBeEnabled();
            await expect(toolbarItem(page, 'Mark reviewed')).toBeEnabled();
        });

        test(`ADR-0100: a Fill chosen from the palette is painted, and the face then sets it again (${chrome})`, async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'C2');

            await toolbarArrow(page, 'Fill Colour').click();
            await page.locator('[role="menuitemradio"][aria-label="Light Blue"]').first().click();

            await expect(cell(grid, 'C2')).toHaveCSS('background-color', 'rgb(0, 176, 240)');
            await expectKeyboardOn(grid);
            await page.keyboard.press('ArrowDown');
            await expectFocusAt(grid, 'C3');
            await toolbarItem(page, 'Fill Colour').click();
            await expect(cell(grid, 'C3')).toHaveCSS('background-color', 'rgb(0, 176, 240)');
        });

        test(`ADR-0100: the page's own action runs over the Selection (${chrome})`, async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'B3');

            await toolbarItem(page, 'Mark reviewed').click();

            await expect(cell(grid, 'B3')).toHaveCSS('background-color', 'rgb(226, 239, 218)');
        });
    });
}

test('SH-48/ADR-0100: without the toolbar the Sheet is as it was, and the page can hide it', async ({ page }) => {
    await page.goto('/sheet?toolbar=off');
    await expect(cell(sheet(page), 'A1')).toHaveText('Item');
    await expect(page.locator('.ex-sheet-toolbar')).toHaveCount(0);
    await expect(page.locator('.ex-sheet')).toHaveCSS('display', 'contents');
});

test('SH-50/ADR-0100: the toolbar is one tab stop; the arrows move among its items, Enter presses one, and the keyboard goes back to the Sheet', async ({ page }) => {
    await openSheet(page, 'builtin');
    const grid = sheet(page);
    await pressCell(grid, 'A3');
    const band = toolbar(page);

    await band.focus();
    await expect(band).toHaveAttribute('aria-activedescendant', await toolbarItem(page, 'Bold').getAttribute('id'));
    await page.keyboard.press('ArrowRight');
    await expect(band).toHaveAttribute('aria-activedescendant', await toolbarItem(page, 'Italic').getAttribute('id'));
    await page.keyboard.press('Enter');

    await expect(page.locator('#sheet-focus-font')).toHaveText('italic');
    await expectKeyboardOn(grid);
});

// KeyTips and Ctrl+F1 (ADR-0100, ticket 160; SH-51, SH-52). The browser's own keys on Windows —
// whether Alt's release is kept from Chrome's and Edge's menu — are a Windows run's to read; here the
// keys reach the page and mean what ADR-0100 says.
for (const chrome of ['builtin', 'mud']) {
    test(`SH-52/ADR-0100: Alt released alone shows the rows' KeyTips, H shows Excel's letters, and 1 sets bold with the keyboard back on the Sheet (${chrome})`, async ({ page }) => {
        await openSheet(page, chrome);
        const grid = sheet(page);
        await pressCell(grid, 'A4');

        await page.keyboard.press('Alt');
        const tips = toolbar(page).locator('.ex-sheet-keytip');
        await expect(tips).toHaveText(['H', 'Y']);
        await page.keyboard.press('h');
        await expect(tips.filter({ hasText: /^1$/ })).toHaveCount(1);
        await expect(tips.filter({ hasText: /^FC$/ })).toHaveCount(1);
        await page.keyboard.press('1');

        await expect(page.locator('#sheet-focus-font')).toHaveText('bold');
        await expect(tips).toHaveCount(0);
        // The letters were the KeyTips', never the cell's: no edit opened, and the next arrow moves the Focus.
        await expect(cell(grid, 'A4')).toHaveText('Plums');
        await expectKeyboardOn(grid);
        await page.keyboard.press('ArrowDown');
        await expectFocusAt(grid, 'A5');
    });
}

// What the seventeenth Windows run found in Part D. Under the MudBlazor Chrome each list a KeyTip
// opened left the keyboard on the toolbar, so Escape gave it back to the Sheet and left the list
// standing, and the next letter's list opened beside it, four at once: under either Chrome the list
// takes the keyboard into its choices, and Escape closes it with the keyboard back on the Sheet.
// Excel reaches Format Cells… by O, E, through its Format menu. And the element the keyboard leaves
// the toolbar through was aria-hidden while it held focus, which Chromium says in the console.
for (const chrome of ['builtin', 'mud']) {
    test(`SH-52/ADR-0100/ADR-0039: Alt, H, F, C puts the keyboard in the Font Colour list; Escape closes it with the keyboard on the Sheet, and the next list is the only one (${chrome})`, async ({ page }) => {
        await openSheet(page, chrome);
        const grid = sheet(page);
        await pressCell(grid, 'B3');
        const fill = await cell(grid, 'B3').evaluate((element) => getComputedStyle(element).backgroundColor);

        await keyTips(page, 'h', 'f', 'c');

        await expect(openLists(page)).toHaveCount(1);
        // On the choice the Focus cell has: no colour of its own.
        await expect(page.locator(':focus')).toContainText('Automatic');
        await page.keyboard.press('Escape');
        await expect(openLists(page)).toHaveCount(0);
        await expectKeyboardOn(grid);

        await keyTips(page, 'h', 'h');
        await expect(openLists(page)).toHaveCount(1);
        await expect(page.locator(':focus')).toContainText('No Fill');
        await page.keyboard.press('ArrowDown');
        await expect(page.locator(':focus')).toHaveAttribute('aria-label', 'White, Background 1');
        await page.keyboard.press('Escape');

        await expect(openLists(page)).toHaveCount(0);
        await expectKeyboardOn(grid);
        // Escape chose nothing, and the next arrow moves the Focus.
        await expect(cell(grid, 'B3')).toHaveCSS('background-color', fill);
        await page.keyboard.press('ArrowDown');
        await expectFocusAt(grid, 'B4');
    });

    test(`SH-52/ADR-0100 (2026-10-12): Format Cells is Alt, H, O, E, as Excel's Format menu reaches it — O narrows the KeyTips, E opens it (${chrome})`, async ({ page }) => {
        await openSheet(page, chrome);
        const grid = sheet(page);
        await pressCell(grid, 'C2');
        const tips = toolbar(page).locator('.ex-sheet-keytip');
        const formatCells = formatCellsOf(page, grid, chrome);

        await keyTips(page, 'h', 'o');
        await expect(tips).toHaveText(['OE']);
        await expect(formatCells).toHaveCount(0);
        await page.keyboard.press('e');

        await expect(formatCells).toBeVisible();
        await expect(tips).toHaveCount(0);
        await page.keyboard.press('Escape');
        await expect(formatCells).toHaveCount(0);
        await expectKeyboardOn(grid);
    });

    test(`ADR-0100 (the seventeenth Windows run, Part D): the keyboard handed back from the toolbar leaves no console warning while the accessibility tree is on (${chrome})`, async ({ page }) => {
        await openSheet(page, chrome);
        const grid = sheet(page);
        await pressCell(grid, 'A4');
        const hidden = await watchHiddenFocus(page);
        try {
            // On a circuit the element the keyboard leaves through holds focus a round trip.
            await setRoundTrip(80);

            await keyTips(page, 'h', '1');
            await expect(page.locator('#sheet-focus-font')).toHaveText('bold');
            await expectKeyboardOn(grid);
            await page.keyboard.press('F10');
            await expect(toolbar(page).locator('.ex-sheet-keytip')).toHaveText(['H', 'Y']);
            await page.keyboard.press('Escape');
            await expectKeyboardOn(grid);

            expect(await hidden.read(), 'no element holding focus is aria-hidden').toEqual([]);
        } finally {
            await hidden.stop();
            await setRoundTrip(0);
        }
        await page.keyboard.press('Control+z');
        await expect(page.locator('#sheet-focus-font')).toHaveText('regular');
    });
}

test('SH-52/ADR-0100: F10, H, A, C centres the Focus cell; Escape backs out a level; Alt with an arrow is a chord', async ({ page }) => {
    await openSheet(page, 'builtin');
    const grid = sheet(page);
    await pressCell(grid, 'C2');
    const tips = toolbar(page).locator('.ex-sheet-keytip');

    await page.keyboard.press('F10');
    await expect(tips).toHaveText(['H', 'Y']);
    await page.keyboard.press('h');
    await page.keyboard.press('Escape');
    await expect(tips).toHaveText(['H', 'Y']);
    await page.keyboard.press('h');
    await page.keyboard.press('a');
    await expect(tips).toHaveText(['AL', 'AC', 'AR']);
    await page.keyboard.press('c');
    await expect(toolbarItem(page, 'Centre')).toHaveAttribute('aria-pressed', 'true');
    await expectKeyboardOn(grid);

    await page.keyboard.down('Alt');
    await page.keyboard.press('ArrowRight');
    await page.keyboard.up('Alt');
    await expect(tips).toHaveCount(0);
});

test('SH-51/ADR-0100: Ctrl+F1 hides the toolbar the page binds, and shows it again', async ({ page }) => {
    await openSheet(page, 'builtin');
    const grid = sheet(page);
    await pressCell(grid, 'B2');

    await page.keyboard.press('Control+F1');
    await expect(page.locator('.ex-sheet-toolbar')).toHaveCount(0);
    await expectKeyboardOn(grid);
    await page.keyboard.press('Control+F1');
    await expect(toolbar(page)).toBeVisible();
});

// Decided with the user on 2026-10-12 (ADR-0100, ADR-0050 item 16): an outcome on the toolbar never
// depends on when a key was typed (the spine's sixth principle). On the Server host at an 80 ms round
// trip, where every gap is widest, and on WebAssembly, where there is none:
//  - the letters typed after Alt's release, before the KeyTips hold the keyboard, are held by the key
//    gate and handed to the toolbar (they reached the cell, which opened an edit under the KeyTips);
//  - Escape typed on the toolbar before the list a KeyTip opened has taken the keyboard closes the
//    list, and the keyboard is on the Sheet (CloseRequest);
//  - a key typed while the keyboard is on its way back from the toolbar, on the band or on the element
//    a Chrome put it on inside it, reaches the Sheet in order (it pressed the last item again).
for (const chrome of ['builtin', 'mud']) {
    test.describe(`keys typed at machine speed under the ${chrome} Chrome (2026-10-12)`, () => {
        test.beforeEach(async ({ page }) => {
            await openSheet(page, chrome);
        });

        test.afterEach(async () => {
            await setRoundTrip(0);
        });

        test(`SH-52/ADR-0100/ADR-0050: Alt, H, 1 typed at once sets bold, with no edit opened and the KeyTips gone (${chrome})`, async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'A4');
            await setRoundTrip(80);

            await page.keyboard.press('Alt');
            await page.keyboard.press('h');
            await page.keyboard.press('1');

            await expect(page.locator('#sheet-focus-font')).toHaveText('bold');
            await circuitQuiet();
            await expect(toolbar(page).locator('.ex-sheet-keytip')).toHaveCount(0);
            await expect(editor(grid)).toHaveCount(0);
            await expect(cell(grid, 'A4')).toHaveText('Plums');
            await expectKeyboardOn(grid);
            await page.keyboard.press('ArrowDown');
            await expectFocusAt(grid, 'A5');
            await page.keyboard.press('Control+z');
            await expect(page.locator('#sheet-focus-font')).toHaveText('regular');
        });

        test(`SH-52/ADR-0100: Alt, H, F, C, Escape typed at once closes the list, with the keyboard on the Sheet (${chrome})`, async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'B3');
            await setRoundTrip(80);

            await keyTips(page, 'h', 'f', 'c', 'Escape');

            await circuitQuiet();
            await expect(openLists(page)).toHaveCount(0);
            await expect(toolbar(page).locator('.ex-sheet-keytip')).toHaveCount(0);
            await expectKeyboardOn(grid);
            await page.keyboard.press('ArrowDown');
            await expectFocusAt(grid, 'B4');
        });

        test(`SH-52/ADR-0100/ADR-0050: Escape in a list, then Alt, H, H typed at once opens the next list, the only one (${chrome})`, async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'B3');
            await keyTips(page, 'h', 'f', 'c');
            await expect(openLists(page)).toHaveCount(1);
            await expect(page.locator(':focus')).toContainText('Automatic');
            await setRoundTrip(80);

            await page.keyboard.press('Escape');
            // A key typed in a list while it closes is still the list's under either Chrome, and an Alt
            // typed on the way back is not heard as alone (stop 4 in
            // docs/agents/proposals/run17cf-toolbar-mud.md): the Alt goes down once the keyboard is back
            // on the Sheet, while the toolbar's hand-back may still be standing, and the letters at once.
            await expectKeyboardOn(grid);
            await keyTips(page, 'h', 'h');

            await expect(openLists(page)).toHaveCount(1);
            await expect(page.locator(':focus')).toContainText('No Fill');
            await circuitQuiet();
            await expect(openLists(page)).toHaveCount(1);
            await expect(editor(grid)).toHaveCount(0);
            await page.keyboard.press('Escape');
            await expect(openLists(page)).toHaveCount(0);
            await expectKeyboardOn(grid);
        });

        test(`ADR-0100/ADR-0050: Enter typed at once after a KeyTip's command reaches the Sheet, not the toolbar (${chrome})`, async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'A4');
            await setRoundTrip(80);

            await keyTips(page, 'h', '1', 'Enter');

            await expectFocusAt(grid, 'A5');
            await circuitQuiet();
            // Bold was set once, on A4, and pressed no second time.
            await expect(page.locator('#sheet-focus-font')).toHaveText('regular');
            await expect(editor(grid)).toHaveCount(0);
            await expectKeyboardOn(grid);
            await page.keyboard.press('ArrowUp');
            await expect(page.locator('#sheet-focus-font')).toHaveText('bold');
        });

        test(`ADR-0100/ADR-0050: Enter typed at once after Enter on a toolbar item reaches the Sheet, not the item (${chrome})`, async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'A3');
            const band = toolbar(page);
            await band.focus();
            await expect.poll(() => activeItemName(page)).toBe('Bold');
            await page.keyboard.press('ArrowRight');
            await expect.poll(() => activeItemName(page)).toBe('Italic');
            await setRoundTrip(80);

            await page.keyboard.press('Enter');
            await page.keyboard.press('Enter');

            await expectFocusAt(grid, 'A4');
            await circuitQuiet();
            await expect(page.locator('#sheet-focus-font')).toHaveText('regular');
            await expectKeyboardOn(grid);
            await page.keyboard.press('ArrowUp');
            await expect(page.locator('#sheet-focus-font')).toHaveText('italic');
        });

        test(`ADR-0100/ADR-0050: a colour chosen in a list, then Enter typed before the keyboard is back, fills the cell and moves the Focus (${chrome})`, async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'B3');
            await keyTips(page, 'h', 'h');
            await expect(openLists(page)).toHaveCount(1);
            await expect(page.locator(':focus')).toContainText('No Fill');
            await page.keyboard.press('ArrowDown');
            await page.keyboard.press('ArrowRight');
            await expect(page.locator(':focus')).toHaveAttribute('aria-label', 'Black, Text 1');
            await setRoundTrip(80);

            await page.keyboard.press('Enter');
            if (chrome === 'mud') {
                // Under MudBlazor's Chrome a key typed in the list itself while it closes is the list's
                // (a stop in docs/agents/proposals/run17cf-toolbar-mud.md): the second Enter is typed
                // once the keyboard has left the list, on the element it goes back through or beyond.
                await expect.poll(() => page.evaluate(() => document.activeElement?.closest('.mud-popover') == null)).toBe(true);
            }
            await page.keyboard.press('Enter');

            await expectFocusAt(grid, 'B4');
            await circuitQuiet();
            await expect(openLists(page)).toHaveCount(0);
            await expect(cell(grid, 'B3')).toHaveCSS('background-color', 'rgb(0, 0, 0)');
            await expectKeyboardOn(grid);
        });
    });
}
