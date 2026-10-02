import { test, expect } from './fixtures.mjs';
import { sheet, openSheet, cell, pressCell, expectFocusAt, toolbarItem, toolbarArrow, editor } from './sheet-helpers.mjs';
import { expectKeyboardOn } from './keyboard.mjs';

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
