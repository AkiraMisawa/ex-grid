import { test, expect, setRoundTrip } from './fixtures.mjs';
import { sheet, openSheet, cell, pressCell, expectFocusAt } from './sheet-helpers.mjs';

// Format Cells under the built-in Chrome (ADR-0063, ticket 52; SH-45, DC-60): a popover in the
// grid's frame (ADR-0050 item 16), opened from the Context Menu and from the page's own button,
// taking the keyboard and giving it back, inside the Sheet's box and closed as a Cancel when the
// box shrinks below one row, and independent per Sheet (ADR-0018). The Number Format is what is
// set here, because it is what the Sheet paints on this branch: C2 holds 0.5.

// Tall enough that the Sheet, Formula Bar to scrollbar, is inside the window.
test.use({ viewport: { width: 1280, height: 1000 } });

/** Format Cells' frame in a grid: the Consumer's popover (ADR-0050 item 16). */
function formatCells(grid) {
    return grid.locator('.ex-popover-consumer');
}

function tab(grid, name) {
    return grid.getByRole('tab', { name, exact: true });
}

/** A choice in Format Cells by its label, as the user picks it. */
function choice(grid, name) {
    return formatCells(grid).getByLabel(name, { exact: true });
}

async function openFromMenu(grid, address) {
    await pressCell(grid, address);
    await cell(grid, address).click({ force: true, button: 'right' });
    await grid.page().getByRole('menuitem', { name: 'Format Cells…' }).click();
    await expect(formatCells(grid)).toBeVisible();
}

async function expectInside(inner, outer) {
    const a = await inner.boundingBox();
    const b = await outer.boundingBox();
    expect(a.x).toBeGreaterThanOrEqual(b.x - 0.5);
    expect(a.y).toBeGreaterThanOrEqual(b.y - 0.5);
    expect(a.x + a.width).toBeLessThanOrEqual(b.x + b.width + 0.5);
    expect(a.y + a.height).toBeLessThanOrEqual(b.y + b.height + 0.5);
}

test.describe('on /sheet', () => {
    test.beforeEach(async ({ page }) => {
        await openSheet(page);
    });

    test('SH-45/DC-60: the Context Menu opens Format Cells inside the Sheet\'s box, with the keyboard on its tab, and the arrows switch the tabs', async ({ page }) => {
        const grid = sheet(page);
        await openFromMenu(grid, 'C2');

        await expectInside(formatCells(grid), grid);
        await expect(tab(grid, 'Number')).toBeFocused();
        await expect(tab(grid, 'Number')).toHaveAttribute('aria-selected', 'true');

        await page.keyboard.press('ArrowRight');
        await expect(tab(grid, 'Alignment')).toBeFocused();
        await expect(tab(grid, 'Alignment')).toHaveAttribute('aria-selected', 'true');
        await page.keyboard.press('ArrowLeft');
        await page.keyboard.press('ArrowLeft');
        await expect(tab(grid, 'Fill')).toBeFocused();
        await page.keyboard.press('Home');
        await expect(tab(grid, 'Number')).toBeFocused();
        await expect(formatCells(grid).getByRole('radio', { name: 'Accounting' })).toBeDisabled();
    });

    test('SH-45: OK sets the part touched as one undo step, and the keyboard is the Sheet\'s again', async ({ page }) => {
        const grid = sheet(page);
        await pressCell(grid, 'C2');
        await page.locator('#sheet-format-cells').click();
        await expect(formatCells(grid)).toBeVisible();

        await choice(grid, 'Percentage').check();
        await formatCells(grid).getByRole('button', { name: 'OK' }).click();

        await expect(formatCells(grid)).toHaveCount(0);
        await expect(cell(grid, 'C2')).toHaveText('50.00%');
        // The page's button opened it, and closing it hands the keyboard to the Sheet.
        await expect(grid).toBeFocused();
        await page.keyboard.press('ArrowDown');
        await expectFocusAt(grid, 'C3');
        await page.keyboard.press('ControlOrMeta+Z');
        await expect(cell(grid, 'C2')).toHaveText('0.5');
    });

    test('SH-42/SH-45, case 22: Ctrl+1 opens Format Cells from the keyboard, on Number, and no tab of the browser is selected', async ({ page }) => {
        const grid = sheet(page);
        await pressCell(grid, 'C2');

        await page.keyboard.press('Control+1');

        await expect(formatCells(grid)).toBeVisible();
        await expect(tab(grid, 'Number')).toHaveAttribute('aria-selected', 'true');
        await page.keyboard.press('Escape');
        await expect(formatCells(grid)).toHaveCount(0);
        await expect(grid).toBeFocused();
    });

    test('SH-45/DC-60: Escape sets nothing, closes Format Cells, and the next arrow moves the Focus', async ({ page }) => {
        const grid = sheet(page);
        await openFromMenu(grid, 'C2');
        await choice(grid, 'Percentage').check();

        await page.keyboard.press('Escape');

        await expect(formatCells(grid)).toHaveCount(0);
        await expect(cell(grid, 'C2')).toHaveText('0.5');
        await expect(grid).toBeFocused();
        await page.keyboard.press('ArrowDown');
        await expectFocusAt(grid, 'C3');
    });

    test('SH-45: a Custom code ExSheet does not read is refused by name, and Format Cells stays open', async ({ page }) => {
        const grid = sheet(page);
        await openFromMenu(grid, 'C2');
        await choice(grid, 'Custom').check();
        const code = formatCells(grid).locator('.ex-format-cells-code');

        await code.fill('[<0]0');
        await code.press('Enter');

        await expect(formatCells(grid).getByRole('alert')).toContainText("The number format '[<0]0' is not one ExSheet reads");
        await expect(formatCells(grid)).toBeVisible();
        await expect(cell(grid, 'C2')).toHaveText('0.5');
    });

    test('SH-45/SRV-7: a Custom code typed at full speed arrives whole, and Enter is OK', async ({ page }) => {
        const grid = sheet(page);
        await openFromMenu(grid, 'C2');
        await choice(grid, 'Custom').check();
        const code = formatCells(grid).locator('.ex-format-cells-code');
        await code.fill('');

        await code.pressSequentially('#,##0.0000');
        await expect(code).toHaveValue('#,##0.0000');
        await code.press('Enter');

        await expect(formatCells(grid)).toHaveCount(0);
        await expect(cell(grid, 'C2')).toHaveText('0.5000');
    });

    test('DC-60: Tab and Shift+Tab wrap inside Format Cells', async ({ page }) => {
        const grid = sheet(page);
        await openFromMenu(grid, 'C2');
        const cancel = formatCells(grid).getByRole('button', { name: 'Cancel' });

        await page.keyboard.press('Shift+Tab');
        await expect(cancel).toBeFocused();
        await page.keyboard.press('Tab');
        await expect(tab(grid, 'Number')).toBeFocused();
        await expect(formatCells(grid)).toBeVisible();
    });

    test('SH-45, case 24: over cells that differ the Font style is empty and the Fill is No Colour', async ({ page }) => {
        const grid = sheet(page);
        // A1 bold with a red Fill, set through Format Cells itself; A2 plain.
        await openFromMenu(grid, 'A1');
        await tab(grid, 'Font').click();
        await choice(grid, 'Bold').check();
        await tab(grid, 'Fill').click();
        await formatCells(grid).getByRole('radio', { name: 'Red', exact: true }).check();
        await formatCells(grid).getByRole('button', { name: 'OK' }).click();
        await expect(formatCells(grid)).toHaveCount(0);

        await cell(grid, 'A2').click({ force: true, modifiers: ['Shift'] });
        await cell(grid, 'A2').click({ force: true, button: 'right' });
        await page.getByRole('menuitem', { name: 'Format Cells…' }).click();

        // It reopens on the last tab shown (case 22).
        await expect(tab(grid, 'Fill')).toHaveAttribute('aria-selected', 'true');
        await expect(choice(grid, 'No Colour')).toBeChecked();
        await tab(grid, 'Font').click();
        for (const style of ['Regular', 'Italic', 'Bold', 'Bold Italic']) {
            await expect(choice(grid, style)).not.toBeChecked();
        }
    });

    // On the Server host the grid raises a move a round trip after it, from after the render that
    // shows it (ADR-0050 item 14's note of 2026-10-01). Format Cells opens over the Selection the
    // grid holds when it is asked, and the move's notification, landing after, names that same
    // Selection and leaves it standing (ticket 56). On WebAssembly these are the cases without a
    // round trip.

    test('ticket 56: the page\'s Format Cells pressed straight after Shift+ArrowDown opens over the extended range, and stands when the move is heard', async ({ page }) => {
        const grid = sheet(page);
        await pressCell(grid, 'C2');
        await setRoundTrip(150);

        await page.keyboard.press('Shift+ArrowDown');
        await page.locator('#sheet-format-cells').click();
        await expect(formatCells(grid)).toBeVisible();
        // Past the move's notification, a round trip after the key's render.
        await page.waitForTimeout(600);
        await expect(formatCells(grid)).toBeVisible();
        await setRoundTrip(0);

        await choice(grid, 'Percentage').check();
        await formatCells(grid).getByRole('button', { name: 'OK' }).click();
        await expect(formatCells(grid)).toHaveCount(0);
        await expect(cell(grid, 'C2')).toHaveText('50.00%');
        await expect(cell(grid, 'C3')).toHaveText('75.00%');
    });

    test('ticket 56: the Context Menu\'s Format Cells…, chosen as soon as the menu opens on another cell, opens over that cell', async ({ page }) => {
        const grid = sheet(page);
        await pressCell(grid, 'C2');
        await setRoundTrip(150);

        // Outside the Selection, the secondary click moves it to C3 and opens the menu there.
        // This pins the outcome on a real circuit; it does not hold the order. At 150 ms and at
        // 600 ms this test passed with ExSheet still reading the Selection last heard: by the time
        // the click on the item reached the Sheet, the move had been heard (why was not traced).
        // The order in which it has not is staged in layer 2 (CommandSelectionTests).
        await cell(grid, 'C3').click({ force: true, button: 'right' });
        await page.getByRole('menuitem', { name: 'Format Cells…' }).click();
        await expect(formatCells(grid)).toBeVisible();
        await page.waitForTimeout(600);
        await expect(formatCells(grid)).toBeVisible();
        await setRoundTrip(0);

        await choice(grid, 'Percentage').check();
        await formatCells(grid).getByRole('button', { name: 'OK' }).click();
        await expect(formatCells(grid)).toHaveCount(0);
        await expect(cell(grid, 'C3')).toHaveText('75.00%');
        await expect(cell(grid, 'C2')).toHaveText('0.5');
    });
});

test.describe('in a box that follows the window', () => {
    test('DC-60/ADR-0040: Format Cells scrolls inside a small box, and closes as a Cancel when the box shrinks below one row', async ({ page }) => {
        await page.goto('/sheet?box=window');
        const grid = sheet(page);
        await expect(cell(grid, 'A1')).toHaveText('Item');
        // The page's Linked Table lands 1.5 s after the Sheet opens; Format Cells is opened after it.
        await expect(cell(grid, 'B12')).toHaveText('318.25', { timeout: 10_000 });
        await openFromMenu(grid, 'C2');
        await choice(grid, 'Percentage').check();

        // The box is the window less 360px: at 560 it is 200px, under a Formula Bar and a header.
        // The browser reports the new box, and the frame's bound follows it in the render after.
        await page.setViewportSize({ width: 1280, height: 560 });
        await expect(async () => expectInside(formatCells(grid), grid)).toPass();
        expect(await formatCells(grid).evaluate((frame) => frame.scrollHeight > frame.clientHeight)).toBe(true);

        // At 400 the box is 40px: less than a row under the Formula Bar and the header.
        await page.setViewportSize({ width: 1280, height: 400 });
        await expect(formatCells(grid)).toHaveCount(0);
        await expect(grid).toBeFocused();
        await page.setViewportSize({ width: 1280, height: 1000 });
        await expect(cell(grid, 'C2')).toHaveText('0.5');
    });
});

test.describe('two Sheets on /sheets', () => {
    test.beforeEach(async ({ page }) => {
        await page.goto('/sheets');
        await expect(page.locator('#demo-interactive')).toBeAttached({ timeout: 30_000 });
        await expect(cell(sheet(page, 0), 'A1')).toHaveText('Left');
        await expect(cell(sheet(page, 1), 'A1')).toHaveText('Right');
    });

    test('DC-60/ADR-0018: each Sheet\'s Format Cells is its own', async ({ page }) => {
        const left = sheet(page, 0);
        const right = sheet(page, 1);
        await openFromMenu(left, 'B1');
        await expectInside(formatCells(left), left);

        await openFromMenu(right, 'B1');
        await expectInside(formatCells(right), right);
        await expect(formatCells(left)).toBeVisible();
        await expect(tab(right, 'Number')).toBeFocused();

        // The tab shown is each Sheet's own, and Escape closes the one it is pressed in.
        await page.keyboard.press('End');
        await expect(tab(right, 'Fill')).toHaveAttribute('aria-selected', 'true');
        await expect(tab(left, 'Number')).toHaveAttribute('aria-selected', 'true');
        await page.keyboard.press('Escape');
        await expect(formatCells(right)).toHaveCount(0);
        await expect(right).toBeFocused();
        await expect(formatCells(left)).toBeVisible();
    });
});
