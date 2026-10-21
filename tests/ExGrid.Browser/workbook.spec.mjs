import { test, expect, settled, alterPage } from './fixtures.mjs';
import { sheet, cell, clickCell, pressCell, goTo, expectFocusAt, editor, bar, typeIntoNameBox, nameBox } from './sheet-helpers.mjs';
import { expectKeyboardOn } from './keyboard.mjs';

// The Sheet Tabs (ADR-0331 §4; exsheet-workbook ticket 05), on /workbook under both Chromes: one
// ExSheet holding the Sheets Claim and Summary, a tab for each under the grid. Ctrl+PageDown and
// Ctrl+PageUp show the Sheet after and before; a key typed straight after the switch, at machine
// speed, lands on the Sheet shown, at its own Selection, though on a circuit it told the paint before
// the switch (numbered after it in the Input Order, ADR-0240); each Sheet keeps where it was scrolled;
// a press on a tab leaves the keyboard in the grid; and the keys typed straight after a press on a tab,
// or after Escape on the tab list, are taken in their place in the Input Order (input-order ticket 04).

test.use({ viewport: { width: 1280, height: 1000 } });

for (const chrome of ['builtin', 'mud']) {
    test.describe(`ADR-0331 Sheet Tabs (${chrome})`, () => {
        async function openWorkbook(page) {
            await page.goto(chrome === 'builtin' ? '/workbook' : `/workbook?chrome=${chrome}`);
            await expect(cell(sheet(page), 'A1')).toHaveText('Item');
        }

        const tab = (page, name) => page.locator('.ex-sheet-tab', { hasText: name });

        test('WB-1: Ctrl+PageDown and Ctrl+PageUp show the Sheet after and before, and stop at the last and the first', async ({ page }) => {
            await openWorkbook(page);
            const grid = sheet(page);
            await clickCell(grid, 'B2');

            await page.keyboard.press('Control+PageDown');
            await expect(cell(grid, 'A1')).toHaveText('Claimed');
            await expect(cell(grid, 'B1')).toHaveText('4561.25');
            await expect(tab(page, 'Summary')).toHaveAttribute('aria-selected', 'true');
            await expect(page.locator('#workbook-shown')).toHaveText('Summary');

            await page.keyboard.press('Control+PageDown');
            await page.keyboard.press('Control+PageUp');
            await expect(cell(grid, 'A1')).toHaveText('Item');
            await expect(tab(page, 'Claim')).toHaveAttribute('aria-selected', 'true');
            await expectFocusAt(grid, 'B2');
        });

        test('WB-2: keys typed at once after Ctrl+PageDown land on the Sheet shown, at its own Focus, and nothing on the Sheet left', async ({ page }) => {
            await openWorkbook(page);
            const grid = sheet(page);
            await clickCell(grid, 'A1');
            await page.keyboard.press('Control+PageDown');
            await expect(cell(grid, 'A1')).toHaveText('Claimed');
            await goTo(grid, 'C2');
            await page.keyboard.press('Control+PageUp');
            await expect(cell(grid, 'A1')).toHaveText('Item');
            await clickCell(grid, 'C3');
            await settled(grid);

            // At machine speed: the switch and the typing, with nothing awaited between them.
            await page.keyboard.press('Control+PageDown');
            await page.keyboard.type('77');
            await page.keyboard.press('Enter');
            await settled(grid);

            await expect(cell(grid, 'C2')).toHaveText('77');
            await expect(editor(grid)).toHaveCount(0);
            await page.keyboard.press('Control+PageUp');
            await expect(cell(grid, 'C3')).toHaveText('289.5');
        });

        test('WB-3: each Sheet keeps where it was scrolled', async ({ page }) => {
            await openWorkbook(page);
            const grid = sheet(page);
            await goTo(grid, 'A150');
            await expect(cell(grid, 'A150')).toHaveText('Row 150');

            await tab(page, 'Summary').click();
            await expect(cell(grid, 'A1')).toHaveText('Claimed');
            await expect(cell(grid, 'A1')).toBeInViewport();

            await tab(page, 'Claim').click();
            await expect(cell(grid, 'A150')).toHaveText('Row 150');
            await expect(cell(grid, 'A150')).toBeInViewport();
            await expectFocusAt(grid, 'A150');
        });

        test('WB-4: a press on a tab shows its Sheet and leaves the keyboard in the grid', async ({ page }) => {
            await openWorkbook(page);
            const grid = sheet(page);
            await clickCell(grid, 'B2');

            await tab(page, 'Summary').click();

            await expect(tab(page, 'Summary')).toHaveAttribute('aria-selected', 'true');
            await expect(cell(grid, 'A1')).toHaveText('Claimed');
            await expectKeyboardOn(grid);
            // A key typed straight after the press is WB-5's.
        });

        // Input-order ticket 04: a press on a tab is numbered as it happens (data-ex-claims), so the keys
        // typed straight after it are taken once the Sheet it shows is shown, at that Sheet's own Focus.
        test('WB-5/ADR-0240: keys typed at once after a press on a tab land on the Sheet shown, at its own Focus', async ({ page }) => {
            await openWorkbook(page);
            const grid = sheet(page);
            await clickCell(grid, 'A1');
            await page.keyboard.press('Control+PageDown');
            await expect(cell(grid, 'A1')).toHaveText('Claimed');
            await goTo(grid, 'C2');
            await page.keyboard.press('Control+PageUp');
            await expect(cell(grid, 'A1')).toHaveText('Item');
            await clickCell(grid, 'C3');
            await settled(grid);

            // At machine speed: the press and the typing, with nothing awaited between them.
            await tab(page, 'Summary').click();
            await page.keyboard.type('77');
            await page.keyboard.press('Enter');
            await settled(grid);

            await expect(tab(page, 'Summary')).toHaveAttribute('aria-selected', 'true');
            await expect(cell(grid, 'C2')).toHaveText('77');
            await expect(editor(grid)).toHaveCount(0);
            await page.keyboard.press('Control+PageUp');
            await expect(cell(grid, 'C3')).toHaveText('289.5');
        });

        // Input-order ticket 04: a key on the tab list is in the Sheet's order. Escape gives the keyboard
        // back to the grid, and the arrow typed straight after it is taken there, once Escape is answered.
        test('WB-6/ADR-0240: Escape on the tab list, then ↓ at once, moves the Focus in the grid', async ({ page }) => {
            await openWorkbook(page);
            const grid = sheet(page);
            await clickCell(grid, 'B2');
            await tab(page, 'Claim').focus();
            await expect(tab(page, 'Claim')).toBeFocused();

            await page.keyboard.press('Escape');
            await page.keyboard.press('ArrowDown');
            await page.keyboard.press('ArrowRight');

            await expectFocusAt(grid, 'C3');
            await settled(grid);
            await expect(tab(page, 'Claim')).toHaveAttribute('aria-selected', 'true');
            await expectKeyboardOn(grid);
        });

        // Input-order ticket 04's review: the arrow keys on the tab list give the keyboard to the tab they
        // select through the grid, in the Input Order, and the tab takes it where it stands, as its own focus
        // did before: the page does not scroll to bring it into view.
        test('WB-6/ADR-0240: → on the tab list selects the next tab without scrolling the page', async ({ page }) => {
            await openWorkbook(page);
            const grid = sheet(page);
            await clickCell(grid, 'B2');
            // Room below the Sheet, so the page can scroll; put back, and scrolled back, as the test ends.
            await alterPage(page, () => {
                const before = document.body.style.paddingBottom;
                document.body.style.paddingBottom = '2000px';
                return () => {
                    document.body.style.paddingBottom = before;
                    window.scrollTo(0, 0);
                };
            });
            await tab(page, 'Claim').focus();
            await expect(tab(page, 'Claim')).toBeFocused();
            // The page scrolled until the tabs' middle is at the window's top edge, half of them out of view.
            await tab(page, 'Claim').evaluate((element) => {
                const box = element.getBoundingClientRect();
                window.scrollBy(0, box.top + box.height / 2);
            });
            const scrolled = await page.evaluate(() => window.scrollY);
            expect(scrolled).toBeGreaterThan(0);

            await page.keyboard.press('ArrowRight');

            await expect(tab(page, 'Summary')).toHaveAttribute('aria-selected', 'true');
            await expect(tab(page, 'Summary')).toBeFocused();
            await settled(grid);
            expect(await page.evaluate(() => window.scrollY)).toBe(scrolled);
        });

        test('WB-5: the Name Box goes to a cell on another Sheet, shows that Sheet and scrolls the cell into view', async ({ page }) => {
            await openWorkbook(page);
            const grid = sheet(page);
            await tab(page, 'Summary').click();
            await expect(cell(grid, 'A1')).toHaveText('Claimed');

            await typeIntoNameBox(grid, 'Claim!A150');
            await nameBox(grid).press('Enter');

            await expect(tab(page, 'Claim')).toHaveAttribute('aria-selected', 'true');
            await expectFocusAt(grid, 'A150');
            await expect(cell(grid, 'A150')).toHaveText('Row 150');
            await expect(cell(grid, 'A150')).toBeInViewport();
            await expect(page.locator('#workbook-shown')).toHaveText('Claim');
        });

        test('WB-6: a Formula typed at once across Ctrl+PageUp points at the Sheet before, and Enter commits it on its own Sheet', async ({ page }) => {
            await openWorkbook(page);
            const grid = sheet(page);
            await pressCell(grid, 'B2');
            await tab(page, 'Summary').click();
            await expect(cell(grid, 'A1')).toHaveText('Claimed');
            await pressCell(grid, 'B3');
            await settled(grid);

            // At machine speed: the typing, the switch, the point and the commit, nothing awaited between.
            await page.keyboard.type('=');
            await page.keyboard.press('Control+PageUp');
            await page.keyboard.press('ArrowDown');
            await page.keyboard.press('Enter');
            await settled(grid);

            await expect(tab(page, 'Summary')).toHaveAttribute('aria-selected', 'true');
            await expect(cell(grid, 'B3')).toHaveText('6');
            await expectFocusAt(grid, 'B4');
            await expect(editor(grid)).toHaveCount(0);
            await pressCell(grid, 'B3');
            await expect(bar(grid)).toHaveValue('=Claim!B3');
        });

        test('WB-7: a press on a tab while a Formula is typed keeps the edit, and a press on a cell there writes its Reference', async ({ page }) => {
            await openWorkbook(page);
            const grid = sheet(page);
            await tab(page, 'Summary').click();
            await expect(cell(grid, 'A1')).toHaveText('Claimed');
            await pressCell(grid, 'B3');
            await page.keyboard.type('=');
            await expect(editor(grid)).toHaveValue('=');

            await tab(page, 'Claim').click();
            await expect(tab(page, 'Claim')).toHaveAttribute('aria-selected', 'true');
            await expect(bar(grid)).toHaveValue('=');
            await clickCell(grid, 'D5');
            await expect(bar(grid)).toHaveValue('=Claim!D5');
            await page.keyboard.press('Enter');

            await expect(tab(page, 'Summary')).toHaveAttribute('aria-selected', 'true');
            await expect(cell(grid, 'B3')).toHaveText('4561.25');
            await expectKeyboardOn(grid);
        });

        // Exsheet-workbook ticket 06: what the tabs do besides showing a Sheet. Each is an input in the
        // Sheet's order (ADR-0240 section 6), so a key typed straight after it, with nothing awaited between,
        // acts on what it left: Ctrl+PageUp or Ctrl+PageDown, which step from the Sheet shown, tell where.
        const tabNames = (page) => page.locator('.ex-sheet-tabs [role=tab]');
        const command = (page, label) => page.locator('.ex-sheet-tab-menu [role=menuitem]', { hasText: label });

        test('WB-8/ADR-0331: + adds a Sheet after the one shown and shows it, and Ctrl+PageDown typed at once steps from it', async ({ page }) => {
            await openWorkbook(page);
            const grid = sheet(page);
            await clickCell(grid, 'B2');
            await settled(grid);

            await page.locator('.ex-sheet-tab-add').click();
            await page.keyboard.press('Control+PageDown');
            await settled(grid);

            await expect(tabNames(page)).toHaveText(['Claim', 'Sheet1', 'Summary']);
            await expect(tab(page, 'Summary')).toHaveAttribute('aria-selected', 'true');
            await expect(page.locator('#workbook-shown')).toHaveText('Summary');
            await expectKeyboardOn(grid);
        });

        test('WB-8/ADR-0331: Shift+F11 adds a Sheet before the one shown, and Ctrl+PageDown typed at once steps from it', async ({ page }) => {
            await openWorkbook(page);
            const grid = sheet(page);
            await clickCell(grid, 'B2');
            await settled(grid);

            await page.keyboard.press('Shift+F11');
            await page.keyboard.press('Control+PageDown');
            await settled(grid);

            await expect(tabNames(page)).toHaveText(['Sheet1', 'Claim', 'Summary']);
            await expect(tab(page, 'Claim')).toHaveAttribute('aria-selected', 'true');
            await expect(cell(grid, 'A1')).toHaveText('Item');
        });

        test('WB-9/ADR-0331: a double click renames on the tab, typed and committed at machine speed, and the Formulas follow', async ({ page }) => {
            await openWorkbook(page);
            const grid = sheet(page);
            await clickCell(grid, 'B2');
            await settled(grid);

            await tab(page, 'Claim').dblclick();
            await page.keyboard.type('Costs');
            await page.keyboard.press('Enter');
            await page.keyboard.press('Control+PageDown');
            await settled(grid);

            await expect(tabNames(page)).toHaveText(['Costs', 'Summary']);
            await expect(page.locator('.ex-sheet-tab-name')).toHaveCount(0);
            await expect(tab(page, 'Summary')).toHaveAttribute('aria-selected', 'true');
            await expect(cell(grid, 'B1')).toHaveText('4561.25');
            await pressCell(grid, 'B1');
            await expect(bar(grid)).toHaveValue('=Costs!D5');
        });

        test('WB-9/ADR-0331: a name Excel refuses is refused by name and stays open; Escape keeps the old name and ↓ typed at once moves in the grid', async ({ page }) => {
            await openWorkbook(page);
            const grid = sheet(page);
            await clickCell(grid, 'B2');
            await settled(grid);

            await tab(page, 'Claim').dblclick();
            await page.keyboard.type('Q1/Q2');
            await page.keyboard.press('Enter');
            await settled(grid);

            await expect(page.locator('.ex-sheet-tab-name')).toHaveAttribute('aria-invalid', 'true');
            await expect(page.locator('.ex-sheet-notice')).toContainText('cannot contain any of');
            await expect(page.locator('.ex-sheet-tab-name')).toBeFocused();

            await page.keyboard.press('Escape');
            await page.keyboard.press('ArrowDown');
            await settled(grid);

            await expect(tabNames(page)).toHaveText(['Claim', 'Summary']);
            await expectFocusAt(grid, 'B3');
            await expectKeyboardOn(grid);
        });

        test('WB-10/ADR-0331: Delete on the tab\'s menu, from the keys, and Ctrl+Z typed at once gives the Sheet back', async ({ page }) => {
            await openWorkbook(page);
            const grid = sheet(page);
            await clickCell(grid, 'B2');
            await settled(grid);

            await tab(page, 'Claim').click({ button: 'right' });
            await page.keyboard.press('ArrowDown');
            await page.keyboard.press('Enter');
            await settled(grid);

            await expect(tabNames(page)).toHaveText(['Summary']);
            await expect(cell(grid, 'B1')).toHaveText('#REF!');
            await expect(page.locator('.ex-sheet-tab-menu')).toHaveCount(0);

            await tab(page, 'Summary').click({ button: 'right' });
            await command(page, 'Rename').waitFor();
            await page.keyboard.press('Escape');
            await page.keyboard.press('Control+z');
            await settled(grid);

            await expect(tabNames(page)).toHaveText(['Claim', 'Summary']);
            await expect(tab(page, 'Claim')).toHaveAttribute('aria-selected', 'true');
            await expect(cell(grid, 'A1')).toHaveText('Item');
        });

        test('WB-11/ADR-0331: Shift+F10 on a tab opens its menu, Move Right moves it, and Ctrl+PageUp typed at once steps from it', async ({ page }) => {
            await openWorkbook(page);
            const grid = sheet(page);
            await clickCell(grid, 'B2');
            await tab(page, 'Claim').focus();
            await expect(tab(page, 'Claim')).toBeFocused();
            await settled(grid);

            await page.keyboard.press('Shift+F10');
            // Rename, Delete, then Move Right: Move Left cannot be done on the first Sheet, and ↓ passes it.
            await page.keyboard.press('ArrowDown');
            await page.keyboard.press('ArrowDown');
            await page.keyboard.press('Enter');
            await page.keyboard.press('Control+PageUp');
            await settled(grid);

            await expect(tabNames(page)).toHaveText(['Summary', 'Claim']);
            await expect(tab(page, 'Summary')).toHaveAttribute('aria-selected', 'true');
            await expectKeyboardOn(grid);
        });

        test('WB-12/ADR-0331: Copy on the tab\'s menu places the copy after the Sheet and shows it, and Ctrl+PageUp typed at once steps from it', async ({ page }) => {
            await openWorkbook(page);
            const grid = sheet(page);
            await clickCell(grid, 'B2');
            await settled(grid);

            await tab(page, 'Claim').click({ button: 'right' });
            await command(page, 'Copy').click();
            await page.keyboard.press('Control+PageDown');
            await settled(grid);

            await expect(tabNames(page)).toHaveText(['Claim', 'Claim (2)', 'Summary']);
            await expect(tab(page, 'Summary')).toHaveAttribute('aria-selected', 'true');
            await page.keyboard.press('Control+PageUp');
            await expect(tab(page, 'Claim (2)')).toHaveAttribute('aria-selected', 'true');
            await expect(cell(grid, 'D5')).toHaveText('4561.25');
        });

        test('WB-13/ADR-0331: a tab dragged onto another takes its place, and Ctrl+PageUp typed at once steps from it', async ({ page }) => {
            await openWorkbook(page);
            const grid = sheet(page);
            await clickCell(grid, 'B2');
            await settled(grid);

            const from = await tab(page, 'Claim').boundingBox();
            const to = await tab(page, 'Summary').boundingBox();
            await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2);
            await page.mouse.down();
            await page.mouse.move(to.x + to.width / 2, to.y + to.height / 2, { steps: 4 });
            await page.mouse.up();
            await page.keyboard.press('Control+PageUp');
            await settled(grid);

            await expect(tabNames(page)).toHaveText(['Summary', 'Claim']);
            await expect(tab(page, 'Summary')).toHaveAttribute('aria-selected', 'true');
            await expectKeyboardOn(grid);
        });
    });
}
