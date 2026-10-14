import { test, expect, setRoundTrip, alterPage, circuitQuiet } from './fixtures.mjs';
import { sheet, openSheet, cell, pressCell, expectFocusAt, pressAt, editor, boxOf, columnHeader, hashesAtTheStandardWidth } from './sheet-helpers.mjs';
import { expectKeyboardOn } from './keyboard.mjs';
import { watchHiddenFocus } from './hidden-focus.mjs';

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
    await pressAt(cell(grid, address), { button: 'right' });
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

    test('SH-45, run 17 h10: over A2 under A1\'s thick bottom the Border tab opens on Thick, and the Top button alone takes the line away on both sides', async ({ page }) => {
        const grid = sheet(page);
        const edge = (name) => formatCells(page).getByRole('button', { name, exact: true });
        // A1's bottom thick, set through Format Cells itself. Over A1, with no line, it opens on Thin.
        await openFromMenu(page, grid, 'A1');
        await tab(page, 'Border').click();
        await expect(choice(page, 'Thin')).toBeChecked();
        await choice(page, 'Thick').check();
        await edge('Bottom').click();
        await formatCells(page).getByRole('button', { name: 'OK', exact: true }).click();
        await expect(formatCells(page)).toHaveCount(0);

        // Over A2 it opens on the one line A2 shows, as Excel's did: MudBlazor's radio group shows
        // the draft's choice from its first render.
        await openFromMenu(page, grid, 'A2');
        await expect(tab(page, 'Border')).toHaveAttribute('aria-selected', 'true');
        await expect(choice(page, 'Thick')).toBeChecked();
        await expect(edge('Top')).toHaveAttribute('aria-pressed', 'true');

        // What the run pressed: the Top button, then OK.
        await edge('Top').click();
        await expect(edge('Top')).toHaveAttribute('aria-pressed', 'false');
        await formatCells(page).getByRole('button', { name: 'OK', exact: true }).click();
        await expect(formatCells(page)).toHaveCount(0);

        // Neither cell keeps a line there: A2 shows no top, and A1 no bottom.
        await openFromMenu(page, grid, 'A2');
        await expect(edge('Top')).toHaveAttribute('aria-pressed', 'false');
        await expect(choice(page, 'Thin')).toBeChecked();
        await formatCells(page).getByRole('button', { name: 'Cancel', exact: true }).click();
        await expect(formatCells(page)).toHaveCount(0);
        await openFromMenu(page, grid, 'A1');
        await expect(edge('Bottom')).toHaveAttribute('aria-pressed', 'false');
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

    test('SH-45/ADR-0071 (the seventeenth Windows run, Part D): opened from the toolbar\'s keys, Alt, H, O, E, the frame hands the keyboard to the dialog with no console warning while the accessibility tree is on', async ({ page }) => {
        const grid = sheet(page);
        await pressCell(grid, 'C2');
        const hidden = await watchHiddenFocus(page);
        try {
            // On a circuit the frame's own element holds focus a round trip or more, while the dialog is drawn.
            await setRoundTrip(80);

            // At machine speed: the letters typed before the KeyTips have taken the keyboard are held by the
            // grid's key gate and handed to the toolbar in order (ADR-0100, ADR-0050 item 16; decided with
            // the user, 2026-10-12), and nothing here waits for the KeyTips.
            await page.keyboard.press('Alt');
            await page.keyboard.press('h');
            await page.keyboard.press('o');
            await page.keyboard.press('e');

            await expect(formatCells(page)).toBeVisible();
            await expect(tab(page, 'Number')).toBeFocused();
            // No letter reached the cell: no edit opened under the KeyTips.
            await expect(editor(grid)).toHaveCount(0);
            await page.keyboard.press('Escape');
            await expect(formatCells(page)).toHaveCount(0);
            await expectKeyboardOn(grid);
            await expect(cell(grid, 'C2')).toHaveText('0.5');

            expect(await hidden.read(), 'no element holding focus is aria-hidden').toEqual([]);
        } finally {
            await hidden.stop();
            await setRoundTrip(0);
        }
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
        await expectKeyboardOn(grid);

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

    test('ADR-0039: Escape on the dialog root hands the following arrows to the Sheet', async ({ page }) => {
        const grid = sheet(page);
        await openFromMenu(page, grid, 'C2');
        await formatCells(page).getByText('Format Cells', { exact: true }).click();
        await expect(formatCells(page)).toBeFocused();
        await setRoundTrip(80);

        await page.keyboard.press('Escape');
        await page.keyboard.press('ArrowDown');
        await page.keyboard.press('ArrowDown');

        await expect(formatCells(page)).toHaveCount(0);
        await expectFocusAt(grid, 'C4');
        await expectKeyboardOn(grid);
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

    test('SH-45/ADR-0071 (decided with the user, 2026-10-13): Escape, then ↓ and ↓ typed at once, cancels Format Cells and moves the Focus two rows, as typed slowly', async ({ page }) => {
        const grid = sheet(page);
        await openFromMenu(page, grid, 'C2');
        await expect(tab(page, 'Number')).toBeFocused();
        await choice(page, 'Percentage').check();
        await setRoundTrip(80);

        // On a circuit the dialog stands a round trip after the Escape: the arrows typed meanwhile are the
        // Sheet's, held and handed to it with the keyboard, never the closing dialog's or nobody's.
        await page.keyboard.press('Escape');
        await page.keyboard.press('ArrowDown');
        await page.keyboard.press('ArrowDown');

        await expectFocusAt(grid, 'C4');
        await expect(formatCells(page)).toHaveCount(0);
        await expect(cell(grid, 'C2')).toHaveText('0.5');
        await expectKeyboardOn(grid);
    });

    test('SH-45/ADR-0071 (decided with the user, 2026-10-13): OK, then ↓ typed at once, sets what was touched and moves the Focus, as typed slowly', async ({ page }) => {
        const grid = sheet(page);
        await openFromMenu(page, grid, 'C2');
        await choice(page, 'Percentage').check();
        await setRoundTrip(80);

        await formatCells(page).getByRole('button', { name: 'OK', exact: true }).click();
        await page.keyboard.press('ArrowDown');

        await expectFocusAt(grid, 'C3');
        await expect(formatCells(page)).toHaveCount(0);
        await expect(cell(grid, 'C2')).toHaveText('50.00%');
        await expect(cell(grid, 'C3')).not.toHaveText(/%$/);
        await expectKeyboardOn(grid);
        await page.keyboard.press('ControlOrMeta+Z');
        await expect(cell(grid, 'C2')).toHaveText('0.5');
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

    // Decided with the user on 2026-10-12 (ADR-0071): what OK sets is ExSheet's under every Chrome,
    // so under this one too OK widens a column over #### for any part of the Font it sets, as
    // Excel's Font tab widened column A for Bold in the seventeenth Windows run's h7; its Fill tab
    // widens nothing.
    test('ADR-0071 (decided 2026-10-12; the seventeenth run, h7): OK with Bold over #### widens a column at the standard width and leaves a sized one; OK with a Fill widens nothing; one Ctrl+Z takes back the bold and the width', async ({ page }) => {
        const grid = sheet(page);
        const { standard, sized } = await hashesAtTheStandardWidth(page, grid);
        const width = async (name) => (await boxOf(columnHeader(grid, name))).width;

        await page.keyboard.press('Control+1');
        await expect(formatCells(page)).toBeVisible();
        await tab(page, 'Fill').click();
        await formatCells(page).getByRole('radio', { name: 'Yellow', exact: true }).check();
        await formatCells(page).getByRole('button', { name: 'OK', exact: true }).click();
        await expect(formatCells(page)).toHaveCount(0);
        await expectKeyboardOn(grid);
        await expect(cell(grid, 'E1')).toHaveText(/^#+$/);
        expect(await width('E')).toBeCloseTo(standard, 0);

        await page.keyboard.press('Control+1');
        await expect(formatCells(page)).toBeVisible();
        await tab(page, 'Font').click();
        await choice(page, 'Bold').check();
        await formatCells(page).getByRole('button', { name: 'OK', exact: true }).click();

        await expect(formatCells(page)).toHaveCount(0);
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

    test('ADR-0039/ADR-0071: More Colours never takes the keyboard back from HEX selected as its popup appears', async ({ page }) => {
        await openFromMenu(page, sheet(page), 'C2');
        await tab(page, 'Fill').click();
        await setRoundTrip(150);
        await alterPage(page, () => {
            const state = window.__moreColoursEarlyFocus = { selected: false };
            const observer = new MutationObserver(() => {
                const hex = document.querySelector('.mud-popover-open .mud-picker-color-inputfield input');
                if (!hex || state.selected) return;
                // Choose the field in the render that reveals it, before an opening focus request
                // can return over the circuit. This exercises the early interaction deterministically.
                state.selected = true;
                hex.focus();
            });
            observer.observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['class'] });
            return () => {
                observer.disconnect();
                delete window.__moreColoursEarlyFocus;
            };
        });
        await formatCells(page).getByRole('button', { name: 'More Colours', exact: true }).click();
        await expect.poll(() => page.evaluate(() => window.__moreColoursEarlyFocus.selected)).toBe(true);
        // Read after every opening request has landed; this is not a wait before selecting HEX.
        await circuitQuiet();
        await expect(page.locator('.mud-popover-open .mud-picker-color-inputfield input')).toBeFocused();
    });

    for (const commit of ['Enter', 'blur']) {
        test(`ADR-0039/ADR-0071/ADR-0210: More Colours receives typing made with its opening click (${commit})`, async ({ page }) => {
            await openFromMenu(page, sheet(page), 'C2');
            await tab(page, 'Fill').click();
            await setRoundTrip(150);
            await formatCells(page).getByRole('button', { name: 'More Colours', exact: true }).click();
            // No observation between the opening gesture and typing: keys before HEX has the
            // keyboard must replace its initial value, in order, just as keys typed there do.
            await page.keyboard.type('#FF0000');
            if (commit === 'Enter') await page.keyboard.press('Enter');
            const hex = page.locator('.mud-popover-open .mud-picker-color-inputfield input');
            await expect(hex).toBeFocused();
            await expect(hex).toHaveValue(/^#ff0000$/i);
            if (commit === 'blur') {
                const title = await formatCells(page).locator('.mud-dialog-title').boundingBox();
                await page.mouse.click(title.x + 10, title.y + 10);
            }
            await expect(formatCells(page).getByRole('radio', { name: 'Red', exact: true })).toBeChecked();
            if (commit === 'blur') {
                await expect(hex).toHaveCount(0);
                // The outside press can leave focus on the dialog itself. Escape still
                // closes Format Cells, with the next key going to the Sheet in order.
                await page.keyboard.press('Escape');
                await page.keyboard.press('ArrowDown');
                await expect(formatCells(page)).toHaveCount(0);
                await expectFocusAt(sheet(page), 'C3');
            }
        });
    }

    for (const when of ['with opening', 'after HEX takes the keyboard']) {
        test(`ADR-0039/ADR-0071: Escape from More Colours closes its popup before Format Cells (${when})`, async ({ page }) => {
            await openFromMenu(page, sheet(page), 'C2');
            await tab(page, 'Fill').click();
            await setRoundTrip(150);
            await formatCells(page).getByRole('button', { name: 'More Colours', exact: true }).click();
            const hex = page.locator('.mud-popover-open .mud-picker-color-inputfield input');
            if (when === 'after HEX takes the keyboard') await expect(hex).toBeFocused();
            await page.keyboard.press('Escape');
            await expect(hex).toHaveCount(0);
            await expect(formatCells(page)).toBeVisible();
            await page.keyboard.press('Escape');
            await expect(formatCells(page)).toHaveCount(0);
            await expectKeyboardOn(sheet(page));
        });
    }

    test('ADR-0039/ADR-0071: two Escapes typed with More Colours opening close its two frames in order', async ({ page }) => {
        const grid = sheet(page);
        await openFromMenu(page, grid, 'C2');
        await tab(page, 'Fill').click();
        await setRoundTrip(150);
        await formatCells(page).getByRole('button', { name: 'More Colours', exact: true }).click();
        await page.keyboard.press('Escape');
        await page.keyboard.press('Escape');
        await page.keyboard.press('ArrowDown');
        await expect(page.locator('.mud-popover-open .mud-picker-color-picker')).toHaveCount(0);
        await expect(formatCells(page)).toHaveCount(0);
        await expectKeyboardOn(grid);
        await expectFocusAt(grid, 'C3');
    });

    for (const opening of ['Enter', 'Space']) {
        test(`ADR-0039/ADR-0071: More Colours opens from its button's ${opening} without typing that key into HEX and keeps Tab native`, async ({ page }) => {
            await openFromMenu(page, sheet(page), 'C2');
            await tab(page, 'Fill').click();
            await setRoundTrip(150);
            const opener = formatCells(page).getByRole('button', { name: 'More Colours', exact: true });
            await opener.focus();
            await page.keyboard.press(opening);
            await page.keyboard.type('#FF0000');
            await page.keyboard.press('Enter');
            const hex = page.locator('.mud-popover-open .mud-picker-color-inputfield input');
            await expect(hex).toBeFocused();
            await expect(hex).toHaveValue(/^#ff0000$/i);
            await expect(formatCells(page).getByRole('radio', { name: 'Red', exact: true })).toBeChecked();
            await page.keyboard.press('Escape');
            await expect(opener).toBeFocused();
            await page.keyboard.press('Tab');
            await expect(formatCells(page).getByRole('button', { name: 'OK', exact: true })).toBeFocused();
            await page.keyboard.press('Shift+Tab');
            await expect(opener).toBeFocused();
        });
    }

    for (const reopen of ['Enter', 'Space']) {
        test(`ADR-0039/ADR-0071: More Colours reopens from keys typed with its closing Escape (${reopen})`, async ({ page }) => {
            await openFromMenu(page, sheet(page), 'C2');
            await tab(page, 'Fill').click();
            await setRoundTrip(150);
            await formatCells(page).getByRole('button', { name: 'More Colours', exact: true }).click();
            const hex = page.locator('.mud-popover-open .mud-picker-color-inputfield input');
            await expect(hex).toBeFocused();
            // Enter/Space activates the returned opening button. The next characters belong
            // to the new HEX field, with no observation between close, reopen and typing.
            await page.keyboard.press('Escape');
            await page.keyboard.press(reopen);
            await page.keyboard.type('#FF0000');
            await page.keyboard.press('Enter');
            await circuitQuiet();
            await expect(hex).toBeFocused();
            await expect(hex).toHaveValue(/^#ff0000$/i);
            await expect(formatCells(page).getByRole('radio', { name: 'Red', exact: true })).toBeChecked();
        });
    }

    test('ADR-0039/ADR-0071: More Colours retains RGB and HSL controls and reopens in HEX', async ({ page }) => {
        await openFromMenu(page, sheet(page), 'C2');
        await tab(page, 'Fill').click();
        const opener = formatCells(page).getByRole('button', { name: 'More Colours', exact: true });
        await opener.click();
        const popup = page.locator('.mud-popover-open .mud-ex-sheet-colour-popup');
        await expect(popup.getByText('HEX', { exact: true })).toBeVisible();
        await popup.locator('.mud-picker-control-switch button').click();
        for (const label of ['R', 'G', 'B']) await expect(popup.getByText(label, { exact: true })).toBeVisible();
        await popup.locator('.mud-picker-control-switch button').click();
        for (const label of ['H', 'S', 'L']) await expect(popup.getByText(label, { exact: true })).toBeVisible();
        await page.keyboard.press('Escape');
        await expect(popup).toHaveCount(0);
        await setRoundTrip(150);
        await opener.click();
        await page.keyboard.type('#FF0000');
        await page.keyboard.press('Enter');
        await expect(popup.getByText('HEX', { exact: true })).toBeVisible();
        await expect(popup.locator('.mud-picker-color-inputfield input')).toBeFocused();
        await expect(formatCells(page).getByRole('radio', { name: 'Red', exact: true })).toBeChecked();
    });

    test('SH-45/ADR-0071: More Colours is MudBlazor\'s colour picker, and the colour it picks is what OK sets', async ({ page }) => {
        const grid = sheet(page);
        await openFromMenu(page, grid, 'C2');
        await tab(page, 'Fill').click();

        await formatCells(page).getByRole('button', { name: 'More Colours', exact: true }).click();
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
