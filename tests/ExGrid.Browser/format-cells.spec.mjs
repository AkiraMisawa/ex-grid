import { test, expect, setRoundTrip, circuitQuiet } from './fixtures.mjs';
import { SERVER } from './hosting.mjs';
import { sheet, openSheet, cell, pressCell, expectFocusAt, pressAt, boxOf, columnHeader, hashesAtTheStandardWidth } from './sheet-helpers.mjs';
import { expectKeyboardOn } from './keyboard.mjs';

// Format Cells under the built-in Chrome (ADR-0071, ticket 52; SH-45, DC-60): a popover in the
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
    await pressAt(cell(grid, address), { button: 'right' });
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
        await expectKeyboardOn(grid);
        await page.keyboard.press('ArrowDown');
        await expectFocusAt(grid, 'C3');
        await page.keyboard.press('ControlOrMeta+Z');
        await expect(cell(grid, 'C2')).toHaveText('0.5');
    });

    // OK closes Format Cells, a round trip later on a circuit, and the keys typed meanwhile are held and
    // handed on where the keyboard lands, as behind a command run (ADR-0010's rule for a key that closes
    // a popover; ADR-0044's note of 2026-10-12): OK carries ex-popover-closes.
    test('SH-45/ADR-0044: keys typed while Enter on OK closes Format Cells are the Sheet\'s, in order: ↓, ↓ typed at once move the Focus (2026-10-12)', async ({ page }) => {
        const grid = sheet(page);
        await pressCell(grid, 'C2');
        await page.locator('#sheet-format-cells').click();
        await expect(formatCells(grid)).toBeVisible();
        await choice(grid, 'Percentage').check();
        const ok = formatCells(grid).getByRole('button', { name: 'OK' });
        await ok.focus();
        const circuit = await setRoundTrip(80);
        try {
            await page.keyboard.press('Enter');
            await page.keyboard.press('ArrowDown');
            await page.keyboard.press('ArrowDown');

            await expect(formatCells(grid)).toHaveCount(0);
            await circuitQuiet();
            await expect(cell(grid, 'C2')).toHaveText('50.00%');
            await expectKeyboardOn(grid);
            await expectFocusAt(grid, 'C4');
        } finally {
            if (circuit) await setRoundTrip(0);
        }
    });

    test('SH-42/SH-45, case 22: Ctrl+1 opens Format Cells from the keyboard, on Number, and no tab of the browser is selected', async ({ page }) => {
        const grid = sheet(page);
        await pressCell(grid, 'C2');

        await page.keyboard.press('Control+1');

        await expect(formatCells(grid)).toBeVisible();
        await expect(tab(grid, 'Number')).toHaveAttribute('aria-selected', 'true');
        await page.keyboard.press('Escape');
        await expect(formatCells(grid)).toHaveCount(0);
        await expectKeyboardOn(grid);
    });

    test('SH-45/DC-60: Escape sets nothing, closes Format Cells, and the next arrow moves the Focus', async ({ page }) => {
        const grid = sheet(page);
        await openFromMenu(grid, 'C2');
        await choice(grid, 'Percentage').check();

        await page.keyboard.press('Escape');

        await expect(formatCells(grid)).toHaveCount(0);
        await expect(cell(grid, 'C2')).toHaveText('0.5');
        await expectKeyboardOn(grid);
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

    test('DC-60/ADR-0039: Tab and Shift+Tab wrap inside Format Cells once it holds the keyboard', async ({ page }) => {
        const grid = sheet(page);
        await openFromMenu(grid, 'C2');
        const cancel = formatCells(grid).getByRole('button', { name: 'Cancel' });
        // Pressed once its tab holds the keyboard: one typed while it opens is held and dropped (below).
        await expect(tab(grid, 'Number')).toBeFocused();

        await page.keyboard.press('Shift+Tab');
        await expect(cancel).toBeFocused();
        await page.keyboard.press('Tab');
        await expect(tab(grid, 'Number')).toBeFocused();
        await expect(formatCells(grid)).toBeVisible();
    });

    // ADR-0039's note of 2026-10-13. Format Cells replaces the Context Menu in one render, and its tab
    // takes the keyboard a round trip after it on a circuit. The Sheet's root has the keyboard in
    // between, and holds what is typed there for Format Cells; a Shift+Tab among it is dropped with
    // the keys behind it (ADR-0010). Before, the keyboard was on nothing in that gap, and a Shift+Tab
    // reached Format Cells' trailing sentinel from behind and entered at Cancel — DC-60's earlier test
    // passed or not as the round trip fell. The keys are typed once the render that swaps the menu for
    // Format Cells has landed, looked for frame by frame, inside the 150 ms the tab's focus is behind it.
    test('DC-60/ADR-0039: with a 150 ms round trip, a Shift+Tab typed while Format Cells opens from the menu is dropped with the keys behind it', async ({ page }) => {
        test.skip(!SERVER, 'WebAssembly hands Format Cells the keyboard in the task that draws it: nothing is typed in between');
        const grid = sheet(page);
        const item = page.getByRole('menuitem', { name: 'Format Cells…' });
        await pressCell(grid, 'C2');
        await pressAt(cell(grid, 'C2'), { button: 'right' });
        await expect(item).toBeVisible();
        await setRoundTrip(150);

        await item.click();
        await page.waitForFunction(() => document.querySelector('.ex-popover-consumer') !== null, null, { polling: 'raf' });
        await page.keyboard.press('Shift+Tab');
        await page.keyboard.press('ArrowRight');

        // The tab takes the keyboard on Format Cells' own opening, and neither key moved it: not to
        // Cancel, nor on to the Alignment tab.
        await expect(tab(grid, 'Number')).toBeFocused();
        await circuitQuiet();
        await expect(tab(grid, 'Number')).toBeFocused();
        await expect(tab(grid, 'Number')).toHaveAttribute('aria-selected', 'true');
        await expect(grid.locator('.ex-viewport .ex-editor')).toHaveCount(0);

        // From here the keyboard is Format Cells', and Shift+Tab wraps as ever.
        await page.keyboard.press('Shift+Tab');
        await expect(formatCells(grid).getByRole('button', { name: 'Cancel' })).toBeFocused();
        await page.keyboard.press('Tab');
        await expect(tab(grid, 'Number')).toBeFocused();
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

        await pressAt(cell(grid, 'A2'), { modifiers: ['Shift'] });
        await pressAt(cell(grid, 'A2'), { button: 'right' });
        await page.getByRole('menuitem', { name: 'Format Cells…' }).click();

        // It reopens on the last tab shown (case 22).
        await expect(tab(grid, 'Fill')).toHaveAttribute('aria-selected', 'true');
        await expect(choice(grid, 'No Colour')).toBeChecked();
        await tab(grid, 'Font').click();
        for (const style of ['Regular', 'Italic', 'Bold', 'Bold Italic']) {
            await expect(choice(grid, style)).not.toBeChecked();
        }
    });

    test('SH-45, case 14-13: A2 under A1\'s thick bottom opens with a thick top, its button pressed', async ({ page }) => {
        const grid = sheet(page);
        // A1's bottom thick, set through Format Cells itself; A2 records nothing.
        await openFromMenu(grid, 'A1');
        await tab(grid, 'Border').click();
        await choice(grid, 'Thick').check();
        await formatCells(grid).getByRole('button', { name: 'Bottom', exact: true }).click();
        await formatCells(grid).getByRole('button', { name: 'OK' }).click();
        await expect(formatCells(grid)).toHaveCount(0);

        await openFromMenu(grid, 'A2');

        // It reopens on the last tab shown (case 22).
        await expect(tab(grid, 'Border')).toHaveAttribute('aria-selected', 'true');
        await expect(formatCells(grid).getByRole('button', { name: 'Top', exact: true })).toHaveAttribute('aria-pressed', 'true');
        await expect(formatCells(grid).locator('.ex-format-cells-preview line[data-edge=Top]')).toHaveAttribute('stroke-width', '3');
        await expect(formatCells(grid).getByRole('button', { name: 'Bottom', exact: true })).toHaveAttribute('aria-pressed', 'false');
    });

    test('SH-45, run 17 h10: over A2 under A1\'s thick bottom the Border tab opens on Thick, and the Top button alone takes the line away on both sides', async ({ page }) => {
        const grid = sheet(page);
        const edge = (name) => formatCells(grid).getByRole('button', { name, exact: true });
        // A1's bottom thick, set through Format Cells itself. Over A1, with no line, it opens on Thin.
        await openFromMenu(grid, 'A1');
        await tab(grid, 'Border').click();
        await expect(choice(grid, 'Thin')).toBeChecked();
        await choice(grid, 'Thick').check();
        await edge('Bottom').click();
        await formatCells(grid).getByRole('button', { name: 'OK' }).click();
        await expect(formatCells(grid)).toHaveCount(0);

        // Over A2 it opens on the one line A2 shows, as Excel's did.
        await openFromMenu(grid, 'A2');
        await expect(tab(grid, 'Border')).toHaveAttribute('aria-selected', 'true');
        await expect(choice(grid, 'Thick')).toBeChecked();
        await expect(edge('Top')).toHaveAttribute('aria-pressed', 'true');

        // What the run pressed: the Top button, then OK.
        await edge('Top').click();
        await expect(edge('Top')).toHaveAttribute('aria-pressed', 'false');
        await formatCells(grid).getByRole('button', { name: 'OK' }).click();
        await expect(formatCells(grid)).toHaveCount(0);

        // Neither cell keeps a line there: A2 shows no top, and A1 no bottom.
        await openFromMenu(grid, 'A2');
        await expect(edge('Top')).toHaveAttribute('aria-pressed', 'false');
        await expect(choice(grid, 'Thin')).toBeChecked();
        await formatCells(grid).getByRole('button', { name: 'Cancel' }).click();
        await expect(formatCells(grid)).toHaveCount(0);
        await openFromMenu(grid, 'A1');
        await expect(edge('Bottom')).toHaveAttribute('aria-pressed', 'false');
    });

    test('SH-45, run 18a format-cells 2: over B2 with a thick top and a medium right the Border tab opens on the top\'s Thick (ADR-0071)', async ({ page }) => {
        const grid = sheet(page);
        const edge = (name) => formatCells(grid).getByRole('button', { name, exact: true });
        // B2's top thick and its right medium, set through Format Cells itself.
        await openFromMenu(grid, 'B2');
        await tab(grid, 'Border').click();
        await choice(grid, 'Thick').check();
        await edge('Top').click();
        await choice(grid, 'Medium').check();
        await edge('Right').click();
        await formatCells(grid).getByRole('button', { name: 'OK' }).click();
        await expect(formatCells(grid)).toHaveCount(0);

        // Reopened, it is on Thick, the top's line, as Excel's was; before run 18a it opened on Thin.
        await openFromMenu(grid, 'B2');
        await expect(tab(grid, 'Border')).toHaveAttribute('aria-selected', 'true');
        await expect(choice(grid, 'Thick')).toBeChecked();
        await expect(edge('Top')).toHaveAttribute('aria-pressed', 'true');
        await expect(edge('Right')).toHaveAttribute('aria-pressed', 'true');
        await formatCells(grid).getByRole('button', { name: 'Cancel' }).click();
        await expect(formatCells(grid)).toHaveCount(0);
    });

    // Decided with the user on 2026-10-12 (ADR-0071): Format Cells' OK widens a column over ####
    // for any part of the Font it sets, as Excel's Font tab widened column A for Bold in the
    // seventeenth Windows run's h7, where Ctrl+B does not; its Fill tab widens nothing.
    test('ADR-0071 (decided 2026-10-12; the seventeenth run, h7): OK with Bold over #### widens a column at the standard width and leaves a sized one; OK with a Fill widens nothing; one Ctrl+Z takes back the bold and the width', async ({ page }) => {
        const grid = sheet(page);
        const { standard, sized } = await hashesAtTheStandardWidth(page, grid);
        const width = async (name) => (await boxOf(columnHeader(grid, name))).width;

        await page.keyboard.press('Control+1');
        await expect(formatCells(grid)).toBeVisible();
        await tab(grid, 'Fill').click();
        await formatCells(grid).getByRole('radio', { name: 'Yellow', exact: true }).check();
        await formatCells(grid).getByRole('button', { name: 'OK' }).click();
        await expect(formatCells(grid)).toHaveCount(0);
        await expectKeyboardOn(grid);
        await expect(cell(grid, 'E1')).toHaveText(/^#+$/);
        expect(await width('E')).toBeCloseTo(standard, 0);

        await page.keyboard.press('Control+1');
        await expect(formatCells(grid)).toBeVisible();
        await tab(grid, 'Font').click();
        await choice(grid, 'Bold').check();
        await formatCells(grid).getByRole('button', { name: 'OK' }).click();

        await expect(formatCells(grid)).toHaveCount(0);
        await expect(page.locator('#sheet-focus-font')).toHaveText('bold');
        await expect(cell(grid, 'E1')).toHaveText('123,456,789.12');
        await expect.poll(() => width('E')).toBeGreaterThan(standard + 10);
        await expect(cell(grid, 'F1')).toHaveText(/^#+$/);
        expect(await width('F')).toBeCloseTo(sized, 0);

        // The width is a step of OK's own: one Ctrl+Z takes back the bold and the width.
        await expectKeyboardOn(grid);
        await page.keyboard.press('ControlOrMeta+Z');
        await expect(page.locator('#sheet-focus-font')).toHaveText('regular');
        await expect(cell(grid, 'E1')).toHaveText(/^#+$/);
        await expect.poll(() => width('E')).toBeCloseTo(standard, 0);
        expect(await width('F')).toBeCloseTo(sized, 0);
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
        // Past the move's notification, a round trip after the key's render: once the circuit is quiet.
        await circuitQuiet();
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
        await pressAt(cell(grid, 'C3'), { button: 'right' });
        await page.getByRole('menuitem', { name: 'Format Cells…' }).click();
        await expect(formatCells(grid)).toBeVisible();
        // Past the move's notification: once the circuit is quiet.
        await circuitQuiet();
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
        await expectKeyboardOn(grid);
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
        await expectKeyboardOn(right);
        await expect(formatCells(left)).toBeVisible();
    });
});
