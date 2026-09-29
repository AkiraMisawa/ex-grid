import { test, expect, setRoundTrip } from './fixtures.mjs';
import {
    sheet, positions, openSheet, cell, clickCell, clickBarEnd, editor, bar, expectFocusAt, pressCell, typeSteadily,
} from './sheet-helpers.mjs';

// An edit stands when the keyboard leaves the grid, and a press brings it back (ED-26,
// ADR-0018 section 6, ADR-0021's note of 2026-09-29, ticket 25 of docs/specs/exsheet). Found on
// /sheet: `=` typed into a cell, then a click on the positions grid beside it. The Sheet's edit
// stayed open and nothing reached it again — Escape went to the positions grid, and a click
// back on the Sheet's rows pointed while the keyboard stayed with the positions grid. Losing
// DOM focus neither commits nor discards an edit; a key pressed in another grid is that
// grid's; and a press on the Sheet's rows or headings puts the keyboard back into the surface
// that last held it before the press is handled, so the press points, or commits and hands
// the keyboard to the root, as if the keyboard had never left.
//
// /sheet: the positions grid beside the Sheet declares nothing, and a page button stands for a
// control of the Consumer's. /sheets: two Sheets, each able to hold an edit of its own.

// Tall enough that every Sheet on the page, Formula Bar to horizontal scrollbar, is inside the
// window: a pointer below the window's edge reaches nothing, and the edge band sits there.
test.use({ viewport: { width: 1280, height: 1000 } });

/** Presses the positions grid's FX cell: the keyboard is that grid's once its root has it. */
async function pressPositions(page) {
    const fx = positions(page).locator('.ex-cell', { hasText: /^FX$/ }).first();
    await expect(fx).toBeVisible();
    // Cells are pointer-events: none; the press lands on the Viewport (ADR-0004).
    await fx.click({ force: true });
    await expect(positions(page)).toBeFocused();
}

// Under the built-in Chrome and ExGrid.MudBlazor's: a Chrome paints the surfaces as boxes around
// controls of its own, and the keyboard comes back into the control (ADR-0010/0030).
for (const chrome of ['builtin', 'mud']) {
    test.describe(`/sheet under the ${chrome} Chrome`, () => {
        test.beforeEach(async ({ page }) => {
            await openSheet(page, chrome);
        });

        test('ED-26: an edit left for another grid stands, that grid\'s keys are its own, and a press back points with the keyboard in the edit', async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'C4');
            await page.keyboard.type('=');
            await expect(editor(grid)).toHaveValue('=');
            await expect(editor(grid)).toBeFocused();

            await pressPositions(page);
            // Neither committed nor discarded.
            await expect(editor(grid)).toHaveValue('=');
            await expect(bar(grid)).toHaveValue('=');
            // The positions grid's keys are its own (KB-1): ↓ moves its Focus, Escape lets its
            // keyboard go (ADR-0012), and the Sheet's edit stands through both.
            await expect(positions(page)).toHaveAttribute('aria-activedescendant', /-r2c1$/);
            await page.keyboard.press('ArrowDown');
            await expect(positions(page)).toHaveAttribute('aria-activedescendant', /-r3c1$/);
            await page.keyboard.press('Escape');
            await expect(positions(page)).not.toBeFocused();
            await expect(editor(grid)).toHaveValue('=');
            await expect(cell(grid, 'C4')).toHaveText('0.2');

            // The press back on the Sheet's rows finds the keyboard in the edit, so it points and
            // the keyboard stays there.
            await clickCell(grid, 'B2');
            await expect(editor(grid)).toHaveValue('=B2');
            await expect(grid.locator('.ex-point')).toHaveCount(1);
            await expect(editor(grid)).toBeFocused();
            // The next key is the Sheet's: Escape cancels its edit.
            await page.keyboard.press('Escape');
            await expect(editor(grid)).toHaveCount(0);
            await expect(cell(grid, 'C4')).toHaveText('0.2');
            await expect(grid).toBeFocused();
            await expect(positions(page)).toHaveAttribute('aria-activedescendant', /-r3c1$/);
        });

        test('ED-26: a press back where no Reference can go commits, moves, and gives the Sheet\'s root the keyboard', async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'C4');
            await page.keyboard.type('99');
            await expect(editor(grid)).toHaveValue('99');
            await pressPositions(page);
            await expect(editor(grid)).toHaveValue('99');

            await clickCell(grid, 'B2');

            await expect(cell(grid, 'C4')).toHaveText('99');
            await expect(editor(grid)).toHaveCount(0);
            await expectFocusAt(grid, 'B2');
            await expect(grid).toBeFocused();
            // The next keys are the Sheet's, at the Focus the press moved to.
            await page.keyboard.type('7');
            await expect(editor(grid)).toHaveValue('7');
            await page.keyboard.press('Enter');
            await expect(cell(grid, 'B2')).toHaveText('7');
            await expectFocusAt(grid, 'B3');
        });

        test('ED-26: an edit last typed in the Formula Bar gets the keyboard back in the bar', async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'E4');
            await clickBarEnd(grid);
            await typeSteadily(page, bar(grid), '=');
            await expect(editor(grid)).toHaveValue('=');
            await pressPositions(page);

            await clickCell(grid, 'B2');

            await expect(bar(grid)).toHaveValue('=B2');
            await expect(bar(grid)).toBeFocused();
            await typeSteadily(page, bar(grid), '+1');
            await page.keyboard.press('Enter');
            await expect(cell(grid, 'E4')).toHaveText('13');
            await expect(grid).toBeFocused();
        });

        test('ED-26: a press back that commits an edit last typed in the Formula Bar gives the root the keyboard, not the bar', async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'E4');
            await clickBarEnd(grid);
            await typeSteadily(page, bar(grid), '5');
            await pressPositions(page);

            await clickCell(grid, 'B2');

            await expect(cell(grid, 'E4')).toHaveText('5');
            await expectFocusAt(grid, 'B2');
            await expect(grid).toBeFocused();
            await page.keyboard.type('7');
            await page.keyboard.press('Enter');
            await expect(cell(grid, 'B2')).toHaveText('7');
        });

        // The same press without leaving: the core keeps DOM focus where it is through a press on
        // the rows while an edit is open (ADR-0051), so the bar kept it after the commit and the
        // hand-back left it there — typing went into a bar with no edit open, and nowhere else.
        // The bar's focus was left standing by the press, and the hand-back takes it (ADR-0021).
        test('ADR-0021/ED-26: a press on the rows that commits an edit typed in the Formula Bar gives the root the keyboard', async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'E4');
            await clickBarEnd(grid);
            await typeSteadily(page, bar(grid), '5');

            await clickCell(grid, 'B2');

            await expect(cell(grid, 'E4')).toHaveText('5');
            await expectFocusAt(grid, 'B2');
            await expect(grid).toBeFocused();
            await page.keyboard.type('7');
            await page.keyboard.press('Enter');
            await expect(cell(grid, 'B2')).toHaveText('7');
        });

        test('ED-26: an edit left for a control on the page stands, and a press back commits it', async ({ page }) => {
            const grid = sheet(page);
            const undo = page.locator('#sheet-undo');
            await pressCell(grid, 'C4');
            await page.keyboard.type('99');
            await expect(editor(grid)).toHaveValue('99');

            await undo.click();
            await expect(undo).toBeFocused();
            await expect(editor(grid)).toHaveValue('99');
            await expect(cell(grid, 'C4')).toHaveText('0.2');

            await clickCell(grid, 'B2');
            await expect(cell(grid, 'C4')).toHaveText('99');
            await expectFocusAt(grid, 'B2');
            await expect(grid).toBeFocused();
        });
    });
}

// On a circuit the answer to the press is a round trip away. Had the keyboard been handed back
// from C#, the key typed straight after the press would still reach the positions grid, and the
// Sheet's edit would stand with nothing able to reach it (ADR-0018 section 6). On WebAssembly
// there is no round trip, and this is the case without one.
test('ED-26/ADR-0021: with a 150 ms round trip, the key straight after the press back is the Sheet\'s', async ({ page }) => {
    await openSheet(page);
    const grid = sheet(page);
    await pressCell(grid, 'C4');
    await page.keyboard.type('=');
    await expect(editor(grid)).toBeFocused();
    await pressPositions(page);
    await expect(positions(page)).toHaveAttribute('aria-activedescendant', /-r2c1$/);
    await setRoundTrip(150);

    // No wait between the two.
    await clickCell(grid, 'B2');
    await page.keyboard.press('Escape');

    await expect(editor(grid)).toHaveCount(0);
    await expect(cell(grid, 'C4')).toHaveText('0.2');
    await expect(grid).toBeFocused();
    await expect(positions(page)).toHaveAttribute('aria-activedescendant', /-r2c1$/);
});

test.describe('/sheets', () => {
    test.beforeEach(async ({ page }) => {
        await page.goto('/sheets');
        await expect(cell(sheet(page, 0), 'A1')).toHaveText('Left');
        await expect(cell(sheet(page, 1), 'A1')).toHaveText('Right');
    });

    test('ED-26/ADR-0018: two Sheets each hold an edit, and a press back on the rows gives each its keyboard', async ({ page }) => {
        const left = sheet(page, 0);
        const right = sheet(page, 1);
        await pressCell(left, 'D1');
        await page.keyboard.type('=');
        await expect(editor(left)).toHaveValue('=');
        await pressCell(right, 'D2');
        await expect(right).toBeFocused();
        await page.keyboard.type('5');
        await expect(editor(right)).toHaveValue('5');
        await expect(editor(left)).toHaveValue('=');

        // Back on the left's rows: the keyboard is the left's edit's, and the press points.
        await clickCell(left, 'B1');
        await expect(editor(left)).toHaveValue('=B1');
        await expect(editor(left)).toBeFocused();
        await expect(editor(right)).toHaveValue('5');
        // Escape is the left's, and cancels the left's edit only.
        await page.keyboard.press('Escape');
        await expect(editor(left)).toHaveCount(0);
        await expect(cell(left, 'D1')).toHaveText('');
        await expect(left).toBeFocused();
        await expect(editor(right)).toHaveValue('5');

        // Back on the right's rows, where no Reference can go after 5: it commits, and the right
        // has the keyboard.
        await clickCell(right, 'E3');
        await expect(cell(right, 'D2')).toHaveText('5');
        await expectFocusAt(right, 'E3');
        await expect(right).toBeFocused();
        await page.keyboard.type('7');
        await page.keyboard.press('Enter');
        await expect(cell(right, 'E3')).toHaveText('7');
        await expect(cell(left, 'E3')).toHaveText('');
    });

    test('ED-26: a press back on a column heading or a Row Heading commits the edit left standing, and the root has the keyboard', async ({ page }) => {
        const left = sheet(page, 0);
        const right = sheet(page, 1);
        await pressCell(left, 'D3');
        await page.keyboard.type('4');
        await expect(editor(left)).toHaveValue('4');
        await pressCell(right, 'A5');
        await expect(right).toBeFocused();
        await expect(editor(left)).toHaveValue('4');

        await left.locator('.ex-header-cell', { hasText: /^C$/ }).click({ force: true });
        await expect(cell(left, 'D3')).toHaveText('4');
        await expectFocusAt(left, 'C1');
        await expect(left).toBeFocused();

        await pressCell(left, 'D4');
        await page.keyboard.type('6');
        await expect(editor(left)).toHaveValue('6');
        await clickCell(right, 'A6');
        await expect(right).toBeFocused();

        await left.locator('.ex-row .ex-row-heading', { hasText: /^6$/ }).click({ force: true });
        await expect(cell(left, 'D4')).toHaveText('6');
        await expectFocusAt(left, 'A6');
        await expect(left).toBeFocused();
    });

    test('ED-26/ADR-0018: with a 150 ms round trip, the key straight after the press back is that Sheet\'s', async ({ page }) => {
        const left = sheet(page, 0);
        const right = sheet(page, 1);
        await pressCell(left, 'D1');
        await page.keyboard.type('=');
        await expect(editor(left)).toBeFocused();
        await pressCell(right, 'D2');
        await expect(right).toBeFocused();
        await setRoundTrip(150);

        // No wait between the two: Escape in the right Sheet would release it (ADR-0012).
        await clickCell(left, 'B1');
        await page.keyboard.press('Escape');

        await expect(editor(left)).toHaveCount(0);
        await expect(cell(left, 'D1')).toHaveText('');
        await expect(left).toBeFocused();
        await expect(editor(right)).toHaveCount(0);
        await expectFocusAt(right, 'D2');
    });
});
