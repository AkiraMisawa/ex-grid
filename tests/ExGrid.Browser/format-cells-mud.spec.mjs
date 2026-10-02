import { test, expect } from './fixtures.mjs';
import { sheet, openSheet, cell, pressCell, expectFocusAt } from './sheet-helpers.mjs';
import { expectKeyboardOn } from './keyboard.mjs';

// Format Cells under ExSheet.MudBlazor's Chrome (ADR-0071, ticket 53; SH-45, SH-47): a MudDialog at
// page level, outside the Sheet's root and its box, which MudBlazor draws. What it offers and what
// OK sets are ExSheet's, as under the built-in Chrome (format-cells.spec.mjs); what this file holds
// the Chrome to is the frame: the keyboard taken and handed back however the dialog closes, the
// tabs switching with the arrow keys, Tab kept inside, MudBlazor's colour picker for More Colours,
// and two Sheets each with its own. The Number Format is what is set and read back on the page,
// because it is what the Sheet paints on this branch: C2 holds 0.5.

// Tall enough that the Sheet, Formula Bar to scrollbar, is inside the window.
test.use({ viewport: { width: 1280, height: 1000 } });

/** Format Cells' dialog: MudBlazor's, at page level. */
function formatCells(page) {
    return page.locator('.mud-dialog.mud-ex-sheet-format-cells');
}

function tab(page, name) {
    return formatCells(page).getByRole('tab', { name, exact: true });
}

/** A choice in Format Cells by its label, as the user picks it. */
function choice(page, name) {
    return formatCells(page).getByLabel(name, { exact: true });
}

async function openFromMenu(page, grid, address) {
    await pressCell(grid, address);
    await cell(grid, address).click({ force: true, button: 'right' });
    await page.getByRole('menuitem', { name: 'Format Cells…' }).click();
    await expect(formatCells(page)).toBeVisible();
}

test.describe('on /sheet?chrome=mud', () => {
    test.beforeEach(async ({ page }) => {
        await openSheet(page, 'mud');
    });

    test('SH-45/ADR-0071: the Context Menu opens Format Cells as a MudDialog outside the Sheet, with the keyboard on its tab, and the arrows switch the tabs', async ({ page }) => {
        const grid = sheet(page);
        await openFromMenu(page, grid, 'C2');

        // At page level: nothing of it is inside the grid, and it is not bounded by the Sheet's box.
        await expect(grid.locator('.mud-ex-sheet-format-cells, .ex-popover-consumer')).toHaveCount(0);
        await expect(formatCells(page)).toHaveAttribute('role', 'dialog');
        await expect(tab(page, 'Number')).toBeFocused();
        await expect(tab(page, 'Number')).toHaveAttribute('aria-selected', 'true');

        await page.keyboard.press('ArrowRight');
        await expect(tab(page, 'Alignment')).toBeFocused();
        await expect(tab(page, 'Alignment')).toHaveAttribute('aria-selected', 'true');
        await page.keyboard.press('ArrowLeft');
        await page.keyboard.press('ArrowLeft');
        await expect(tab(page, 'Fill')).toBeFocused();
        await expect(tab(page, 'Fill')).toHaveAttribute('aria-selected', 'true');
        await page.keyboard.press('Home');
        await expect(tab(page, 'Number')).toBeFocused();
        await expect(choice(page, 'Accounting')).toBeDisabled();
    });

    test('SH-45: OK sets the part touched as one undo step, and the keyboard is the Sheet\'s again', async ({ page }) => {
        const grid = sheet(page);
        await pressCell(grid, 'C2');
        await page.locator('#sheet-format-cells').click();
        await expect(formatCells(page)).toBeVisible();
        await expect(tab(page, 'Number')).toBeFocused();

        await choice(page, 'Percentage').check();
        await formatCells(page).getByRole('button', { name: 'OK', exact: true }).click();

        await expect(formatCells(page)).toHaveCount(0);
        await expect(cell(grid, 'C2')).toHaveText('50.00%');
        // The page's button opened it, and closing it hands the keyboard to the Sheet, not back to
        // the button: MudBlazor's dialog returns it to what held it, and the frame saw to it that
        // nothing on the page did.
        await expectKeyboardOn(grid);
        await page.keyboard.press('ArrowDown');
        await expectFocusAt(grid, 'C3');
        await page.keyboard.press('ControlOrMeta+Z');
        await expect(cell(grid, 'C2')).toHaveText('0.5');
    });

    test('SH-42/SH-45: Ctrl+1 opens Format Cells on Number, and Escape closes it with the keyboard the Sheet\'s', async ({ page }) => {
        const grid = sheet(page);
        await pressCell(grid, 'C2');

        await page.keyboard.press('Control+1');

        await expect(formatCells(page)).toBeVisible();
        await expect(tab(page, 'Number')).toHaveAttribute('aria-selected', 'true');
        await expect(tab(page, 'Number')).toBeFocused();
        await page.keyboard.press('Escape');
        await expect(formatCells(page)).toHaveCount(0);
        await expectKeyboardOn(grid);
    });

    test('SH-45 (Part C): opened again on Border, every tab is in view and the strip does not scroll (ticket 147)', async ({ page }) => {
        const grid = sheet(page);
        await pressCell(grid, 'C2');
        await page.keyboard.press('Control+1');
        await expect(formatCells(page)).toBeVisible();
        await tab(page, 'Border').click();
        await expect(tab(page, 'Border')).toHaveAttribute('aria-selected', 'true');
        await page.keyboard.press('Escape');
        await expect(formatCells(page)).toHaveCount(0);

        // It opens on the tab shown last in this Sheet: Border, the fourth.
        await page.keyboard.press('Control+1');
        await expect(tab(page, 'Border')).toHaveAttribute('aria-selected', 'true');
        // Every tab inside the tab bar's own box, read at one moment (the dialog may still be moving
        // in), and the bar not scrolled: a tab scrolled out of it lies a whole tab away.
        const bar = await formatCells(page).locator('.mud-tabs-tabbar').evaluate((el) => {
            const box = el.getBoundingClientRect();
            const tabs = [...el.querySelectorAll('[role="tab"]')].map((t) => {
                const r = t.getBoundingClientRect();
                return { name: t.textContent.trim(), left: r.left, right: r.right };
            });
            const wrapper = el.querySelector('.mud-tabs-tabbar-wrapper');
            return { left: box.left, right: box.right, tabs, transform: wrapper ? getComputedStyle(wrapper).transform : 'none' };
        });
        expect(bar.tabs.map((t) => t.name), JSON.stringify(bar)).toEqual(['Number', 'Alignment', 'Font', 'Border', 'Fill']);
        for (const t of bar.tabs) {
            expect(t.left, `${t.name} starts inside the bar: ${JSON.stringify(bar)}`).toBeGreaterThanOrEqual(bar.left - 1);
            expect(t.right, `${t.name} ends inside the bar: ${JSON.stringify(bar)}`).toBeLessThanOrEqual(bar.right + 1);
        }
        expect(['none', 'matrix(1, 0, 0, 1, 0, 0)']).toContain(bar.transform);
        await expect(formatCells(page).locator('.mud-tabs-scroll-button:visible')).toHaveCount(0);
        await page.keyboard.press('Escape');
        await expect(formatCells(page)).toHaveCount(0);
    });

    test('SH-45: Escape sets nothing, and the next arrow moves the Focus', async ({ page }) => {
        const grid = sheet(page);
        await openFromMenu(page, grid, 'C2');
        await choice(page, 'Percentage').check();

        await page.keyboard.press('Escape');

        await expect(formatCells(page)).toHaveCount(0);
        await expect(cell(grid, 'C2')).toHaveText('0.5');
        await expectKeyboardOn(grid);
        await page.keyboard.press('ArrowDown');
        await expectFocusAt(grid, 'C3');
    });

    test('SH-45/ADR-0071: Escape in an open dropdown closes only its list; the next Escape cancels Format Cells', async ({ page }) => {
        const grid = sheet(page);
        await openFromMenu(page, grid, 'C2');
        // Touched, so that the Cancel below has something it must not set.
        await choice(page, 'Percentage').check();
        await tab(page, 'Alignment').click();
        const horizontal = formatCells(page).locator('.mud-ex-sheet-format-cells-alignment');
        const list = page.locator('.mud-popover-open .mud-list-item');

        // Horizontal is a dropdown, as Excel's.
        await horizontal.click();
        await expect(list).toHaveText(['General', 'Left', 'Centre', 'Right']);
        await page.keyboard.press('Escape');

        // The list closed; the dialog stands, the alignment as it was.
        await expect(list).toHaveCount(0);
        await expect(formatCells(page)).toBeVisible();
        await expect(horizontal.locator('input')).toHaveValue('General');

        // The next Escape, from the select the keyboard is back on, is the dialog's Cancel: the
        // Percentage touched before is not set.
        await page.keyboard.press('Escape');

        await expect(formatCells(page)).toHaveCount(0);
        await expect(cell(grid, 'C2')).toHaveText('0.5');
        await expectKeyboardOn(grid);
        await page.keyboard.press('ArrowDown');
        await expectFocusAt(grid, 'C3');
    });

    test('SH-45: a press on the dialog\'s backdrop is a Cancel, and the keyboard is the Sheet\'s again', async ({ page }) => {
        const grid = sheet(page);
        await openFromMenu(page, grid, 'C2');
        await choice(page, 'Percentage').check();

        await page.locator('.mud-overlay-dialog').click({ position: { x: 5, y: 5 } });

        await expect(formatCells(page)).toHaveCount(0);
        await expect(cell(grid, 'C2')).toHaveText('0.5');
        await expectKeyboardOn(grid);
        await page.keyboard.press('ArrowDown');
        await expectFocusAt(grid, 'C3');
    });

    test('SH-45: a Custom code ExSheet does not read is refused by name, and the dialog stays open', async ({ page }) => {
        const grid = sheet(page);
        await openFromMenu(page, grid, 'C2');
        await choice(page, 'Custom').check();
        const code = formatCells(page).locator('.mud-ex-sheet-format-cells-code input');

        await code.fill('[<0]0');
        await code.press('Enter');

        await expect(formatCells(page).locator('.mud-ex-sheet-format-cells-refusal')).toContainText("The number format '[<0]0' is not one ExSheet reads");
        await expect(formatCells(page)).toBeVisible();
        await expect(cell(grid, 'C2')).toHaveText('0.5');
    });

    test('SH-45/SRV-7: a Custom code typed at full speed arrives whole, and Enter is OK', async ({ page }) => {
        const grid = sheet(page);
        await openFromMenu(page, grid, 'C2');
        await choice(page, 'Custom').check();
        const code = formatCells(page).locator('.mud-ex-sheet-format-cells-code input');
        await code.fill('');

        await code.pressSequentially('#,##0.0000');
        await expect(code).toHaveValue('#,##0.0000');
        await code.press('Enter');

        await expect(formatCells(page)).toHaveCount(0);
        await expect(cell(grid, 'C2')).toHaveText('0.5000');
        await expectKeyboardOn(grid);
    });

    test('SH-45: Tab and Shift+Tab stay inside the dialog', async ({ page }) => {
        const grid = sheet(page);
        await openFromMenu(page, grid, 'C2');
        await expect(tab(page, 'Number')).toBeFocused();
        const cancel = formatCells(page).getByRole('button', { name: 'Cancel', exact: true });

        await page.keyboard.press('Shift+Tab');
        await expect(cancel).toBeFocused();
        await page.keyboard.press('Tab');
        await expect(tab(page, 'Number')).toBeFocused();
        await expect(formatCells(page)).toBeVisible();
    });

    test('SH-45/ADR-0071: More Colours is MudBlazor\'s colour picker, and the colour it picks is what OK sets', async ({ page }) => {
        const grid = sheet(page);
        await openFromMenu(page, grid, 'C2');
        await tab(page, 'Fill').click();

        await formatCells(page).locator('.mud-ex-sheet-more-colours input').click();
        await expect(page.locator('.mud-popover-open .mud-picker-color-picker')).toBeVisible();
        // MudBlazor's own hex field, in its picker's popover: a change is read on Enter.
        const hex = page.locator('.mud-popover-open .mud-picker-color-inputfield input');
        await hex.fill('#FF0000');
        await hex.press('Enter');
        // A press outside the picker closes it; the dialog stays.
        const title = await formatCells(page).locator('.mud-dialog-title').boundingBox();
        await page.mouse.click(title.x + 10, title.y + 10);
        await expect(page.locator('.mud-popover-open .mud-picker-color-picker')).toHaveCount(0);
        await expect(formatCells(page)).toBeVisible();
        await expect(formatCells(page).getByRole('radio', { name: 'Red', exact: true })).toBeChecked();
        await formatCells(page).getByRole('button', { name: 'OK', exact: true }).click();
        await expect(formatCells(page)).toHaveCount(0);

        // Read back through Format Cells itself: the Fill set is case 23's Red.
        await openFromMenu(page, grid, 'C2');
        await expect(tab(page, 'Fill')).toHaveAttribute('aria-selected', 'true');
        await expect(formatCells(page).getByRole('radio', { name: 'Red', exact: true })).toBeChecked();
        await page.keyboard.press('Escape');
        await expect(formatCells(page)).toHaveCount(0);
    });
});

test.describe('two Sheets on /sheets?chrome=mud', () => {
    test.beforeEach(async ({ page }) => {
        await page.goto('/sheets?chrome=mud');
        await expect(page.locator('#demo-interactive')).toBeAttached({ timeout: 30_000 });
        await expect(cell(sheet(page, 0), 'A1')).toHaveText('Left');
        await expect(cell(sheet(page, 1), 'A1')).toHaveText('Right');
        await expect(page.locator('.mud-ex-name-box')).toHaveCount(2);
    });

    test('ADR-0018/ADR-0071: each Sheet\'s Format Cells is its own, and closing it hands the keyboard to that Sheet', async ({ page }) => {
        const left = sheet(page, 0);
        const right = sheet(page, 1);

        await openFromMenu(page, left, 'B1');
        await page.keyboard.press('End');
        await expect(tab(page, 'Fill')).toHaveAttribute('aria-selected', 'true');
        await page.keyboard.press('Escape');
        await expect(formatCells(page)).toHaveCount(0);
        await expectKeyboardOn(left);

        // The tab shown is each Sheet's own: the right one opens on Number, its first opening.
        await openFromMenu(page, right, 'B1');
        await expect(tab(page, 'Number')).toHaveAttribute('aria-selected', 'true');
        await page.keyboard.press('Escape');
        await expect(formatCells(page)).toHaveCount(0);
        await expectKeyboardOn(right);

        await openFromMenu(page, left, 'B1');
        await expect(tab(page, 'Fill')).toHaveAttribute('aria-selected', 'true');
        await page.keyboard.press('Escape');
        await expectKeyboardOn(left);
    });
});
