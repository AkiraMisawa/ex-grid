import { test, expect, setRoundTrip, holdAnswers, circuitQuiet, alterPage, settled } from './fixtures.mjs';
import { sheet, openSheet, cell, pressCell, expectFocusAt, toolbarItem, toolbarArrow, editor, boxOf, columnHeader, hashesAtTheStandardWidth } from './sheet-helpers.mjs';
import { expectKeyboardOn } from './keyboard.mjs';
import { watchHiddenFocus } from './hidden-focus.mjs';
import { resolvedColour } from './pixels.mjs';

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

/** The KeyTips the toolbar shows. */
function tips(page) {
    return toolbar(page).locator('.ex-sheet-keytip');
}

/**
 * Where each key typed from now lands, by its keydown's target, read on the document in the capture
 * phase, before any listener of the page's can stop it: in a toolbar list, on the element a list
 * leaves through, on the band, in a popover, in the grid, or elsewhere. Only keys typed, not the
 * grid's replays of held keys. Answers a reader of what has landed so far.
 */
async function watchWhereKeysLand(page) {
    await alterPage(page, () => {
        window.__keysLanded = [];
        const listener = (e) => {
            if (!e.isTrusted) {
                return;
            }
            const t = e.target instanceof Element ? e.target : null;
            const where = t?.closest('.ex-sheet-choices, .mud-ex-sheet-toolbar-choices') ? 'list'
                : t?.closest('.mud-ex-sheet-toolbar-leaving') ? 'leaving'
                    : t?.closest('.ex-sheet-toolbar') ? 'band'
                        : t?.closest('.ex-popover') ? 'popover'
                            : t?.closest('.ex-grid') ? 'grid' : 'page';
            window.__keysLanded.push({ key: e.key, where });
        };
        document.addEventListener('keydown', listener, true);
        return () => {
            document.removeEventListener('keydown', listener, true);
            delete window.__keysLanded;
        };
    });
    return () => page.evaluate(() => window.__keysLanded);
}

/** The colour a cell's text is painted in, as sRGB. */
async function inkOf(locator) {
    return resolvedColour(locator.page(), await locator.evaluate((el) => getComputedStyle(el).color));
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

        // Decided with the user on 2026-10-12 (ADR-0071): each toggle widens a column over #### as
        // the key it names does — Underline as Ctrl+U (the seventeenth Windows run's h7),
        // Strikethrough as Ctrl+5 (the fourteenth's case 7) — and Bold and Italic widen nothing, as
        // Ctrl+B and Ctrl+I do not.
        test(`ADR-0100/ADR-0071 (decided 2026-10-12): over #### Italic and Bold widen no column; Underline and Strikethrough widen one at the standard width, never one sized by its grip, each taken back by one Ctrl+Z (${chrome})`, async ({ page }) => {
            const grid = sheet(page);
            const { standard, sized } = await hashesAtTheStandardWidth(page, grid);
            const width = async (name) => (await boxOf(columnHeader(grid, name))).width;
            const focusFont = page.locator('#sheet-focus-font');

            await toolbarItem(page, 'Italic').click();
            await expect(focusFont).toHaveText('italic');
            await toolbarItem(page, 'Bold').click();
            await expect(focusFont).toHaveText('bold italic');
            // That nothing widened is read once every round trip has landed (ADR-0056).
            await circuitQuiet();
            await expect(cell(grid, 'E1')).toHaveText(/^#+$/);
            expect(await width('E')).toBeCloseTo(standard, 0);

            for (const [item, shown] of [['Underline', 'bold italic underline'], ['Strikethrough', 'bold italic strikethrough']]) {
                await toolbarItem(page, item).click();
                await expect(focusFont).toHaveText(shown);
                await expect(cell(grid, 'E1')).toHaveText('123,456,789.12');
                await expect.poll(() => width('E')).toBeGreaterThan(standard + 10);
                await circuitQuiet();
                await expect(cell(grid, 'F1')).toHaveText(/^#+$/);
                expect(await width('F')).toBeCloseTo(sized, 0);

                // The width is a step of the press's own: one Ctrl+Z takes back the emphasis and the width.
                await expectKeyboardOn(grid);
                await page.keyboard.press('ControlOrMeta+Z');
                await expect(focusFont).toHaveText('bold italic');
                await expect(cell(grid, 'E1')).toHaveText(/^#+$/);
                await expect.poll(() => width('E')).toBeCloseTo(standard, 0);
            }
        });

        // Decided with the user on 2026-10-14 (ADR-0071, ADR-0100): the Font Colour widens a column over
        // #### as Excel's Home tab Font Color button did, by its face and from its list (the readings of
        // 2026-10-14, run 18a, widening case 5), never one sized by its grip, each in its press's one undo
        // step; SetCellFormatAsync keeps widening as Font.Color through COM, which does not widen. The Fill
        // Colour face widens nothing, as the fourteenth Windows run's Fill Colour button did not (case 7).
        test(`ADR-0100/ADR-0071 (decided with the user, 2026-10-14): over #### the Font Colour, by its face and from its list, widens the column at the standard width and leaves a sized one, each taken back by one Ctrl+Z; the Fill Colour face widens no column (${chrome})`, async ({ page }) => {
            const grid = sheet(page);
            const { standard, sized } = await hashesAtTheStandardWidth(page, grid);
            const width = async (name) => (await boxOf(columnHeader(grid, name))).width;

            await toolbarItem(page, 'Fill Colour').click();
            await expect(cell(grid, 'E1')).toHaveCSS('background-color', 'rgb(255, 255, 0)');
            // That nothing widened is read once every round trip has landed (ADR-0056).
            await circuitQuiet();
            await expect(cell(grid, 'E1')).toHaveText(/^#+$/);
            expect(await width('E')).toBeCloseTo(standard, 0);

            // Its face, red as Excel's starts; then a colour from its list, Blue as run 18a chose.
            const faceAndList = [
                [async () => toolbarItem(page, 'Font Colour').click(), [255, 0, 0]],
                [async () => {
                    await toolbarArrow(page, 'Font Colour').click();
                    await page.locator('[role="menuitemradio"][aria-label="Blue"]').first().click();
                }, [0x00, 0x70, 0xc0]],
            ];
            for (const [press, ink] of faceAndList) {
                await press();
                await expect.poll(() => inkOf(cell(grid, 'E1'))).toEqual(ink);
                await expect(cell(grid, 'E1')).toHaveText('123,456,789.12');
                await expect.poll(() => width('E')).toBeGreaterThan(standard + 10);
                await circuitQuiet();
                await expect(cell(grid, 'F1')).toHaveText(/^#+$/);
                expect(await width('F')).toBeCloseTo(sized, 0);

                // The width is a step of the press's own: one Ctrl+Z takes back the colour and the width.
                await expectKeyboardOn(grid);
                await page.keyboard.press('ControlOrMeta+Z');
                await expect(cell(grid, 'E1')).toHaveText(/^#+$/);
                await expect.poll(() => width('E')).toBeCloseTo(standard, 0);
                await expect(cell(grid, 'E1')).toHaveCSS('background-color', 'rgb(255, 255, 0)');
            }
        });

        test(`ADR-0100/ADR-0044: Ctrl+Z typed straight after a Font Colour choice undoes its colour and width while the list closes (${chrome})`, async ({ page }, testInfo) => {
            const grid = sheet(page);
            const { standard, sized } = await hashesAtTheStandardWidth(page, grid);
            const before = await inkOf(cell(grid, 'E1'));
            const keysLanded = await watchWhereKeysLand(page);
            await setRoundTrip(150);
            await toolbarArrow(page, 'Font Colour').click();

            // The undo belongs after the choice even while its list still holds the keyboard.
            // No reading between the click and the key waits for the colour, width or focus.
            await page.locator('[role="menuitemradio"][aria-label="Blue"]').first().click();
            await page.keyboard.press('ControlOrMeta+Z');

            await circuitQuiet();
            await testInfo.attach('keys-after-font-colour', { body: JSON.stringify(await keysLanded()), contentType: 'application/json' });
            await expect(cell(grid, 'E1')).toHaveText(/^#+$/);
            await expect.poll(() => inkOf(cell(grid, 'E1'))).toEqual(before);
            expect((await boxOf(columnHeader(grid, 'E'))).width).toBeCloseTo(standard, 0);
            expect((await boxOf(columnHeader(grid, 'F'))).width).toBeCloseTo(sized, 0);
            await expectKeyboardOn(grid);
            await expect(openLists(page)).toHaveCount(0);
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
// takes the keyboard into its choices, and Escape closes it. Where the keyboard goes then is Excel's
// as the Windows readings of 2026-10-12 observed it (R2 (b)): back to the Home row's KeyTips. Excel
// reaches Format Cells… by O, E, through its Format menu (observed, R2 (a)). And the element the
// keyboard leaves the toolbar through was aria-hidden while it held focus, which Chromium says in the
// console.
for (const chrome of ['builtin', 'mud']) {
    test(`SH-52/ADR-0100/ADR-0039 (R2 (b), 2026-10-12): Alt, H, F, C puts the keyboard in the Font Colour list; Escape goes back to the Home row's KeyTips, Escape to the rows', Escape to the Sheet, and the next list is the only one (${chrome})`, async ({ page }) => {
        // R2 (b) of the Windows readings of 2026-10-12, observed: after Alt, H, F, C one Escape showed
        // the Home tab's KeyTips with the keyboard not back in the cell, a second the tabs', a third none.
        // Each step waited for here; typed at once below.
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
        await expect(tips(page).filter({ hasText: /^FC$/ })).toHaveCount(1);
        await expect(tips(page).filter({ hasText: /^1$/ })).toHaveCount(1);
        await expect(toolbar(page)).toBeFocused();
        await page.keyboard.press('Escape');
        await expect(tips(page)).toHaveText(['H', 'Y']);
        await expect(toolbar(page)).toBeFocused();
        await page.keyboard.press('Escape');
        await expect(tips(page)).toHaveCount(0);
        await expectKeyboardOn(grid);

        await keyTips(page, 'h', 'h');
        await expect(openLists(page)).toHaveCount(1);
        await expect(page.locator(':focus')).toContainText('No Fill');
        await page.keyboard.press('ArrowDown');
        await expect(page.locator(':focus')).toHaveAttribute('aria-label', 'White, Background 1');
        await page.keyboard.press('Escape');
        await expect(openLists(page)).toHaveCount(0);
        await expect(tips(page).filter({ hasText: /^FC$/ })).toHaveCount(1);
        // From the Home row's KeyTips, 1 sets bold and gives the keyboard back to the Sheet.
        await page.keyboard.press('1');
        await expect(page.locator('#sheet-focus-font')).toHaveText('bold');
        await expect(tips(page)).toHaveCount(0);
        await expectKeyboardOn(grid);
        // Escape chose nothing, and the next arrow moves the Focus.
        await expect(cell(grid, 'B3')).toHaveCSS('background-color', fill);
        await page.keyboard.press('ArrowDown');
        await expectFocusAt(grid, 'B4');
        await page.keyboard.press('Control+z');
    });

    test(`SH-52/ADR-0100 (2026-10-12; R2 (a), observed): Format Cells is Alt, H, O, E, as Excel's Format menu reaches it — O narrows the KeyTips, E opens it (${chrome})`, async ({ page }) => {
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

    test(`ADR-0039 (2026-10-13)/ADR-0100: More Number Formats… opens Format Cells with the keyboard in it, and Escape typed straight after the choice closes it, as typed slowly (${chrome})`, async ({ page }) => {
        await openSheet(page, chrome);
        const grid = sheet(page);
        await pressCell(grid, 'C2');
        const formatCells = formatCellsOf(page, grid, chrome);
        const before = await page.locator('#sheet-focus-number-format').textContent();
        // The Number Format's list from its KeyTip, holding the keyboard on its last choice.
        const onMoreNumberFormats = async () => {
            await keyTips(page, 'h', 'n');
            await expect(openLists(page).locator(':focus')).toHaveCount(1);
            await page.keyboard.press('End');
            await expect(openLists(page).locator(':focus')).toContainText('More Number Formats');
        };

        // Slowly: Enter opens Format Cells, which takes the keyboard; Escape closes it.
        await onMoreNumberFormats();
        await page.keyboard.press('Enter');
        await expect(formatCells).toBeVisible();
        await expect(openLists(page)).toHaveCount(0);
        await expect.poll(() => formatCells.evaluate((f) => f.contains(document.activeElement))).toBe(true);
        await page.keyboard.press('Escape');
        await expect(formatCells).toHaveCount(0);
        await expectKeyboardOn(grid);

        // At once, at an 80 ms round trip on the Server host: the Escape typed while the list closes and
        // Format Cells opens is Format Cells', and closes it, never the grid's.
        await onMoreNumberFormats();
        await setRoundTrip(80);
        await page.keyboard.press('Enter');
        await page.keyboard.press('Escape');

        await expect(openLists(page)).toHaveCount(0);
        await circuitQuiet();
        await expect(formatCells).toHaveCount(0);
        await expectKeyboardOn(grid);
        await setRoundTrip(0);
        // Format Cells closed by Escape set nothing.
        await expect(page.locator('#sheet-focus-number-format')).toHaveText(before ?? '');
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

// The verifier's round-2 finding 1: under MudSheetChrome the toolbar held every key typed on a control
// inside it while its band did not hold the keyboard, taking MudMenu's buttons, which a Tab reached, for
// the element a list leaves through, and never let go. A key is held only while a hand-back is under way,
// and since 2026-10-13 (decided with the user) no control inside the band takes Tab under either Chrome.
for (const chrome of ['builtin', 'mud']) {
    test(`ADR-0100: Tab goes through every control of the toolbar and out of it; the keys typed on each are that control's, nothing is held, and the band's own keys work after (${chrome})`, async ({ page }) => {
        await openSheet(page, chrome);
        const grid = sheet(page);
        await pressCell(grid, 'B3');
        const before = (await cell(grid, 'B3').textContent()).trim();
        const band = toolbar(page);
        await band.focus();
        await expect(band).toBeFocused();
        const where = () => page.evaluate(() => {
            const active = document.activeElement;
            return active?.closest('.ex-sheet-toolbar')
                ? (active.classList.contains('ex-sheet-toolbar') ? 'band' : active.getAttribute('aria-label') ?? active.className)
                : null;
        });

        // Forward through the toolbar's tab stops, a letter typed on each.
        const stops = [];
        for (let i = 0; i < 16; i++) {
            await page.keyboard.press('Tab');
            const at = await where();
            if (at === null) {
                break;
            }
            stops.push(at);
            await page.keyboard.press('x');
            await expect.poll(where).toBe(at);
        }
        // The band is the one stop under either Chrome: MudBlazor's lists draw no button that takes Tab
        // (SH-50; decided with the user, 2026-10-13). Tab from the band leaves the toolbar.
        expect(stops).toEqual([]);
        await expect(openLists(page)).toHaveCount(0);
        await expect(cell(grid, 'B3')).toHaveText(before);

        // And back to the band through the same stops, a letter typed on each again. The way back starts
        // inside the toolbar: the keyboard leaves it for the grid, which keeps Tab and Shift+Tab as its own
        // keys until Escape releases them (KB-8). On the band Escape gives the keyboard to the Sheet.
        await band.focus();
        await expect.poll(where).toBe('band');
        for (const at of stops) {
            await page.keyboard.press('Tab');
            await expect.poll(where).toBe(at);
        }
        for (let i = stops.length - 1; i >= 0; i--) {
            await page.keyboard.press('x');
            await expect.poll(where).toBe(stops[i]);
            await page.keyboard.press('Shift+Tab');
        }
        await expect.poll(where).toBe('band');
        await expect(openLists(page)).toHaveCount(0);
        await expect(cell(grid, 'B3')).toHaveText(before);
        await page.keyboard.press('Escape');
        await expectKeyboardOn(grid);
        await page.keyboard.press('ArrowDown');
        await expectFocusAt(grid, 'B4');
        await expect(tips(page)).toHaveCount(0);
        await expect(editor(grid)).toHaveCount(0);
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

        test(`SH-52/ADR-0100/ADR-0050: F10, H, 2 typed at once sets italic, with no edit opened and the KeyTips gone (${chrome})`, async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'A4');
            await setRoundTrip(80);

            // F10 is a declared key: the keys after it wait for the Sheet's answer, which hands the keyboard
            // to the toolbar (ADR-0050 item 16), as the keys after Alt's release do.
            await page.keyboard.press('F10');
            await page.keyboard.press('h');
            await page.keyboard.press('2');

            await expect(page.locator('#sheet-focus-font')).toHaveText('italic');
            await circuitQuiet();
            await expect(tips(page)).toHaveCount(0);
            await expect(editor(grid)).toHaveCount(0);
            await expect(cell(grid, 'A4')).toHaveText('Plums');
            await expectKeyboardOn(grid);
            await page.keyboard.press('ArrowDown');
            await expectFocusAt(grid, 'A5');
            await page.keyboard.press('Control+z');
            await expect(page.locator('#sheet-focus-font')).toHaveText('regular');
        });

        // SH-52's sequence (F10, H, Escape, H, A, C centres the Focus cell), typed at machine speed. The keys
        // after F10 are held by the key gate until the band holds the keyboard, and those typed once it does
        // reach it straight; either way they are the KeyTips', in the order typed. The gate gives the band the
        // keyboard and hands it the keys it held for it in the same task (ADR-0050 item 16: a control takes
        // the keyboard in turn), so a key typed on the band never overtakes one still held. Until 2026-10-16 the
        // gate asked the core an ordered question after a held Escape before handing on the keys behind it,
        // and an A typed on the band meanwhile reached the KeyTips ahead of the held H: it ended them, and the
        // H opened an edit in C2. With no round trip added the band takes the keyboard while these keys are
        // being typed, which is where that happened (six runs in ten); at 80 ms every key is held.
        for (const rtt of [0, 80]) {
            test(`SH-52/ADR-0100/ADR-0050: F10, H, Escape, H, A, C typed at once centres the Focus cell, with no edit opened and the KeyTips gone (${chrome}, ${rtt} ms)`, async ({ page }) => {
                const grid = sheet(page);
                await pressCell(grid, 'C2');
                const before = await cell(grid, 'C2').textContent();
                await setRoundTrip(rtt);

                for (const key of ['F10', 'h', 'Escape', 'h', 'a', 'c']) {
                    await page.keyboard.press(key);
                }

                // The toolbar's answer is the Consumer's, seen by an expectation; the keyboard back on the
                // Sheet is the hand-back's. From there every key the grid was handed is its own, and the
                // grid is read once it says it has settled (ADR-0220): nothing held, nothing owed.
                await expect(toolbarItem(page, 'Centre')).toHaveAttribute('aria-pressed', 'true');
                await expectKeyboardOn(grid);
                await settled(grid);
                expect(await tips(page).count()).toBe(0);
                expect(await editor(grid).count()).toBe(0);
                expect(await cell(grid, 'C2').textContent()).toBe(before);
                await page.keyboard.press('ArrowDown');
                await settled(grid);
                await expectFocusAt(grid, 'C3');
            });
        }

        test(`SH-52/ADR-0100 (R2 (b)): Alt, H, F, C, Escape, Escape, Escape typed at once leaves no list and no KeyTips, with the keyboard on the Sheet (${chrome})`, async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'B3');
            await setRoundTrip(80);

            await keyTips(page, 'h', 'f', 'c', 'Escape', 'Escape', 'Escape');

            await circuitQuiet();
            await expect(openLists(page)).toHaveCount(0);
            await expect(tips(page)).toHaveCount(0);
            await expect(editor(grid)).toHaveCount(0);
            await expectKeyboardOn(grid);
            await page.keyboard.press('ArrowDown');
            await expectFocusAt(grid, 'B4');
        });

        test(`SH-52/ADR-0100 (R2 (b)): Alt, H, F, C, Escape, 1 typed at once sets bold from the Home row's KeyTips, as typed slowly (${chrome})`, async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'A4');
            await setRoundTrip(80);

            await keyTips(page, 'h', 'f', 'c', 'Escape', '1');

            await expect(page.locator('#sheet-focus-font')).toHaveText('bold');
            await circuitQuiet();
            await expect(openLists(page)).toHaveCount(0);
            await expect(tips(page)).toHaveCount(0);
            await expect(editor(grid)).toHaveCount(0);
            await expect(cell(grid, 'A4')).toHaveText('Plums');
            await expectKeyboardOn(grid);
            await page.keyboard.press('Control+z');
            await expect(page.locator('#sheet-focus-font')).toHaveText('regular');
        });

        test(`SH-52/ADR-0100 (R2 (b), decided with the user 2026-10-12): Escape typed in a list a KeyTip opened, then 1 at once, sets bold: the 1 typed while the list closes is the KeyTips' (${chrome})`, async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'A4');
            await keyTips(page, 'h', 'h');
            await expect(openLists(page)).toHaveCount(1);
            await expect(page.locator(':focus')).toContainText('No Fill');
            const landed = await watchWhereKeysLand(page);
            const circuit = await setRoundTrip(80);

            await page.keyboard.press('Escape');
            await page.keyboard.press('1');

            await expect(page.locator('#sheet-focus-font')).toHaveText('bold');
            await circuitQuiet();
            await expect(openLists(page)).toHaveCount(0);
            await expect(tips(page)).toHaveCount(0);
            await expect(editor(grid)).toHaveCount(0);
            await expect(cell(grid, 'A4')).toHaveText('Plums');
            await expectKeyboardOn(grid);
            if (circuit) {
                // On a circuit the 1 was typed while the list still stood: the case the hold is for.
                expect(await landed()).toEqual([{ key: 'Escape', where: 'list' }, { key: '1', where: 'list' }]);
            }
            await page.keyboard.press('Control+z');
            await expect(page.locator('#sheet-focus-font')).toHaveText('regular');
        });

        test(`SH-52/ADR-0100/ADR-0050: Escape three times in a list a KeyTip opened, then Alt, H, H, all typed at once, opens the next list, the only one (${chrome})`, async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'B3');
            await keyTips(page, 'h', 'f', 'c');
            await expect(openLists(page)).toHaveCount(1);
            await expect(page.locator(':focus')).toContainText('Automatic');
            await setRoundTrip(80);

            // In the list, then on its way to the band and back to the Sheet: each Escape goes back a level.
            // The Alt and the letters at once too: its press and its release are held in order with the
            // keys (decided with the user, 2026-10-13), so it is heard alone wherever the keyboard is.
            await page.keyboard.press('Escape');
            await page.keyboard.press('Escape');
            await page.keyboard.press('Escape');
            await keyTips(page, 'h', 'h');

            await expect(openLists(page)).toHaveCount(1);
            await expect(page.locator(':focus')).toContainText('No Fill');
            await circuitQuiet();
            await expect(openLists(page)).toHaveCount(1);
            await expect(editor(grid)).toHaveCount(0);
            await page.keyboard.press('Escape');
            await expect(openLists(page)).toHaveCount(0);
            await expect(tips(page).filter({ hasText: /^FC$/ })).toHaveCount(1);
        });

        // Ticket 07, found by CI on 2026-10-18: the Alt key went down while the Escapes before it were
        // held, they took the keyboard to the toolbar and back to the Sheet, and only then was the Alt
        // released. The key gate had forgotten its press when the keyboard left the grid, so the release
        // started no KeyTips, and H, H opened an edit in the cell.
        test(`SH-52/ADR-0100/ADR-0050 (ticket 07): Escape three times in a list a KeyTip opened, then Alt held down until the keyboard is back on the Sheet, then H, H, opens the next list, as typed slowly (${chrome})`, async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'B3');
            await keyTips(page, 'h', 'f', 'c');
            await expect(openLists(page)).toHaveCount(1);
            await expect(page.locator(':focus')).toContainText('Automatic');
            await setRoundTrip(80);

            // The Escapes and the Alt's press in one burst, so that they reach the page before the list's
            // Escape has been answered.
            const client = await page.context().newCDPSession(page);
            const escape = { key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 };
            const alt = { key: 'Alt', code: 'AltLeft', windowsVirtualKeyCode: 18 };
            await Promise.all([
                ...[1, 2, 3].flatMap(() => [
                    client.send('Input.dispatchKeyEvent', { type: 'rawKeyDown', ...escape }),
                    client.send('Input.dispatchKeyEvent', { type: 'keyUp', ...escape }),
                ]),
                client.send('Input.dispatchKeyEvent', { type: 'rawKeyDown', modifiers: 1, ...alt }),
            ]);
            await expectKeyboardOn(grid);
            await expect(tips(page)).toHaveCount(0);
            await circuitQuiet();
            await client.send('Input.dispatchKeyEvent', { type: 'keyUp', ...alt });
            await page.keyboard.press('h');
            await page.keyboard.press('h');

            await expect(openLists(page)).toHaveCount(1);
            await expect(page.locator(':focus')).toContainText('No Fill');
            await circuitQuiet();
            await expect(openLists(page)).toHaveCount(1);
            await expect(editor(grid)).toHaveCount(0);
            await page.keyboard.press('Escape');
            await expect(openLists(page)).toHaveCount(0);
        });

        test(`SH-52/ADR-0100 (decided with the user, 2026-10-13): Enter on an edit, then Alt, H, 1, all typed at once, commits the edit and sets bold below it, as typed slowly (${chrome})`, async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'A4');
            await page.keyboard.press('F2');
            await expect(editor(grid)).toHaveCount(1);
            await setRoundTrip(80);

            // The Alt goes down and comes up while the grid still holds the keys behind Enter: its press and
            // its release reach the Sheet in their turn, the release after the press, and the KeyTips show.
            await page.keyboard.press('Enter');
            await keyTips(page, 'h', '1');

            await expectFocusAt(grid, 'A5');
            await expect(page.locator('#sheet-focus-font')).toHaveText('bold');
            await circuitQuiet();
            await expect(tips(page)).toHaveCount(0);
            await expect(editor(grid)).toHaveCount(0);
            await expect(cell(grid, 'A4')).toHaveText('Plums');
            await expectKeyboardOn(grid);
            await page.keyboard.press('Control+z');
            await expect(page.locator('#sheet-focus-font')).toHaveText('regular');
        });

        test(`SH-52/ADR-0100 (decided with the user, 2026-10-13): Alt, Alt, H, 1 typed at once turns the KeyTips on and off and types h1 into the cell, as typed slowly (${chrome})`, async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'A4');

            // Slowly first: the second Alt, on the toolbar, ends the KeyTips, and the letters are the cell's.
            await page.keyboard.press('Alt');
            await expect(tips(page)).not.toHaveCount(0);
            await page.keyboard.press('Alt');
            await expectKeyboardOn(grid);
            await expect(tips(page)).toHaveCount(0);
            await page.keyboard.press('h');
            await page.keyboard.press('1');
            await expect(editor(grid)).toHaveCount(1);
            await expect(editor(grid)).toHaveValue('h1');
            await page.keyboard.press('Escape');
            await expect(editor(grid)).toHaveCount(0);

            // At once, on a circuit: the same.
            await setRoundTrip(80);
            await keyTips(page, 'Alt', 'h', '1');

            await expect(editor(grid)).toHaveCount(1);
            await expect(editor(grid)).toHaveValue('h1');
            await circuitQuiet();
            await expect(tips(page)).toHaveCount(0);
            await expect(page.locator('#sheet-focus-font')).toHaveText('regular');
            await page.keyboard.press('Escape');
            await expect(editor(grid)).toHaveCount(0);
            await expect(cell(grid, 'A4')).toHaveText('Plums');
        });

        test(`ADR-0100 (decided with the user, 2026-10-13): Alt, H, H, then ↓, →, Enter typed at once fills the cell from the list, the keys typed before it had the keyboard being its own (${chrome})`, async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'B3');
            const landed = await watchWhereKeysLand(page);
            const circuit = await setRoundTrip(80);

            await keyTips(page, 'h', 'h', 'ArrowDown', 'ArrowRight', 'Enter');

            await expect(cell(grid, 'B3')).toHaveCSS('background-color', 'rgb(0, 0, 0)');
            await circuitQuiet();
            await expect(openLists(page)).toHaveCount(0);
            await expect(tips(page)).toHaveCount(0);
            await expect(editor(grid)).toHaveCount(0);
            await expectKeyboardOn(grid);
            await page.keyboard.press('ArrowDown');
            await expectFocusAt(grid, 'B4');
            if (circuit) {
                // On a circuit the list took the keyboard a round trip after the second H: the arrows and Enter
                // were typed before it had, outside the list.
                const keys = await landed();
                expect(keys.find((k) => k.key === 'ArrowDown')?.where).not.toBe('list');
            }
        });

        test(`ADR-0100 (decided with the user, 2026-10-13): Alt, H, N, then End, Enter typed at once open Format Cells from More Number Formats…, as typed slowly (${chrome})`, async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'C2');
            const formatCells = formatCellsOf(page, grid, chrome);
            const before = await page.locator('#sheet-focus-number-format').textContent();
            await setRoundTrip(80);

            // End and Enter are the list's however soon they come: End reaches its last choice and Enter
            // makes it, where the list answers them, not the browser's press of a button.
            await keyTips(page, 'h', 'n', 'End', 'Enter');

            await expect(formatCells).toBeVisible();
            await circuitQuiet();
            await expect(openLists(page)).toHaveCount(0);
            await expect(tips(page)).toHaveCount(0);
            await expect.poll(() => formatCells.evaluate((f) => f.contains(document.activeElement))).toBe(true);
            await page.keyboard.press('Escape');
            await expect(formatCells).toHaveCount(0);
            await expectKeyboardOn(grid);
            await expect(page.locator('#sheet-focus-number-format')).toHaveText(before ?? '');
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

        test(`ADR-0100/ADR-0050 (decided with the user, 2026-10-12): a colour chosen in a list, then Enter and ↓ typed in the list as it closes, fills the cell once and moves the Focus twice (${chrome})`, async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'B3');
            await keyTips(page, 'h', 'h');
            await expect(openLists(page)).toHaveCount(1);
            await expect(page.locator(':focus')).toContainText('No Fill');
            await page.keyboard.press('ArrowDown');
            await page.keyboard.press('ArrowRight');
            await expect(page.locator(':focus')).toHaveAttribute('aria-label', 'Black, Text 1');
            const landed = await watchWhereKeysLand(page);
            // The host's answers are held across the three keys, so the list the first Enter closes still
            // stands when the other two are typed however slowly the runner types them (ADR-0220); a round
            // trip of 80ms only made that likely, and a slow runner typed the ↓ after the list had gone.
            const circuit = await holdAnswers(true);

            // The second Enter and the ↓ at once, while the list the first Enter closes still stands: under
            // either Chrome they are held, from the list itself, and are the Sheet's once the keyboard is back.
            await page.keyboard.press('Enter');
            await page.keyboard.press('Enter');
            await page.keyboard.press('ArrowDown');
            await holdAnswers(false);

            await expectFocusAt(grid, 'B5');
            await circuitQuiet();
            await expect(openLists(page)).toHaveCount(0);
            await expect(cell(grid, 'B3')).toHaveCSS('background-color', 'rgb(0, 0, 0)');
            await expect(cell(grid, 'B4')).not.toHaveCSS('background-color', 'rgb(0, 0, 0)');
            await expect(cell(grid, 'B5')).not.toHaveCSS('background-color', 'rgb(0, 0, 0)');
            await expectKeyboardOn(grid);
            if (circuit) {
                // On a circuit all three were typed in the list, a round trip before it could close: without
                // the hold the second Enter chose again or was lost, the ↓ moved in the list, and the Focus
                // stayed on B3.
                expect(await landed()).toEqual([
                    { key: 'Enter', where: 'list' }, { key: 'Enter', where: 'list' }, { key: 'ArrowDown', where: 'list' }]);
            }
        });
    });
}
