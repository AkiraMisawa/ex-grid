import { test, expect, setRoundTrip } from './fixtures.mjs';
import { SERVER } from './hosting.mjs';
import {
    sheet, positions, openSheet, cell, clickCell, editor, bar, nameBox, pressCell, boxOf,
} from './sheet-helpers.mjs';

// A Pointing Scope (ADR-0058, ticket 37; SH-32, SH-35): /sheet puts its Sheet and its positions grid
// in one Scope, and /sheets gives each side a Scope of its own. While a Sheet points — it holds the
// keyboard, has an edit open, and its caret stands where a Reference can go — a press on a registered
// grid writes what reads the pressed cell by key, or the pressed column, where Point writes; the grid
// keeps neither the keyboard nor a Selection. A press that stands for more than one cell writes
// nothing, and the page's status line says why. When the keyboard leaves the Sheet, the grid is an
// ordinary grid again, and the edit stands.
//
// /sheet's positions, in the grid's order: R-1102, R-2210, R-4471, R-5093, R-6120, with the columns
// Id (A), Book (B) and PV (C). R-4471's PV is 318.25, and the PVs sum to 1189.4.

// Tall enough that every grid on the page is inside the window.
test.use({ viewport: { width: 1280, height: 1000 } });

const LOOKUP_4471 = '=XLOOKUP("R-4471", Positions[Id], Positions[PV])';

/** Whether a grid is pointed at: ex-pointed-at on its root (DC-52). */
async function expectPointedAt(grid, pointed = true) {
    if (pointed) {
        await expect(grid).toHaveClass(/\bex-pointed-at\b/);
    } else {
        await expect(grid).not.toHaveClass(/\bex-pointed-at\b/);
    }
}

/**
 * Resolves as soon as the grid wears ex-pointed-at: heard from the class itself, in the task that
 * applies the render, rather than polled, so that a press can follow while keys typed before it
 * are still held by the Sheet's listener (DC-54). The observer is gone once it has answered.
 */
async function pointedAtNow(grid) {
    await grid.evaluate((root) => new Promise((resolve, reject) => {
        if (root.classList.contains('ex-pointed-at')) {
            resolve();
            return;
        }
        const observer = new MutationObserver(() => {
            if (root.classList.contains('ex-pointed-at')) {
                observer.disconnect();
                clearTimeout(timer);
                resolve();
            }
        });
        const timer = setTimeout(() => {
            observer.disconnect();
            reject(new Error('the grid was never pointed at'));
        }, 5000);
        observer.observe(root, { attributes: true, attributeFilter: ['class'] });
    }));
}

/** A column header of a grid, by its label. */
function header(grid, label) {
    return grid.locator('.ex-header-cell', { hasText: new RegExp(`^${label}$`) });
}

test.describe('/sheet', () => {
    test.beforeEach(async ({ page }) => {
        await openSheet(page);
    });

    test('ADR-0058/SH-32: =, a press on a PV cell, *2 and Enter show that row\'s PV doubled', async ({ page }) => {
        const grid = sheet(page);
        const table = positions(page);
        await pressCell(grid, 'F3');
        await page.keyboard.type('=');
        await expect(editor(grid)).toHaveValue('=');
        await expectPointedAt(table);

        await clickCell(table, 'C3');

        await expect(editor(grid)).toHaveValue(LOOKUP_4471);
        // The keyboard stayed in the Sheet, and the positions grid kept no Selection for the press.
        await expect(editor(grid)).toBeFocused();
        await expect(table.locator('.ex-focus, .ex-range')).toHaveCount(0);
        // The pressed cell has no name in the Sheet's words (the ninth Windows run, corrected).
        await expect(nameBox(grid)).toHaveValue('');

        await page.keyboard.type('*2');
        await expect(editor(grid)).toHaveValue(`${LOOKUP_4471}*2`);
        await page.keyboard.press('Enter');
        await expect(cell(grid, 'F3')).toHaveText('636.5');
        await expectPointedAt(table, false);
    });

    test('ADR-0058/SH-32: =SUM(, a press on the PV header, ) and Enter show the column\'s sum', async ({ page }) => {
        const grid = sheet(page);
        const table = positions(page);
        await pressCell(grid, 'F3');
        await page.keyboard.type('=SUM(');
        await expect(editor(grid)).toHaveValue('=SUM(');
        await expectPointedAt(table);

        await header(table, 'PV').click({ force: true });

        await expect(editor(grid)).toHaveValue('=SUM(Positions[PV]');
        await expect(editor(grid)).toBeFocused();
        // Nothing of the header's own ran: no sort, no Selection.
        await expect(table.locator('.ex-header-cell[aria-sort=ascending], .ex-header-cell[aria-sort=descending]')).toHaveCount(0);
        await expect(table.locator('.ex-focus, .ex-range')).toHaveCount(0);
        await page.keyboard.type(')');
        await expect(editor(grid)).toHaveValue('=SUM(Positions[PV])');
        await page.keyboard.press('Enter');
        await expect(cell(grid, 'F3')).toHaveText('1189.4');
    });

    test('ADR-0058/SH-32: a further press replaces what this Point wrote, on the positions grid or on the Sheet', async ({ page }) => {
        const grid = sheet(page);
        const table = positions(page);
        await pressCell(grid, 'F3');
        await page.keyboard.type('=1+');
        // The text the press lands after, as the user sees it: a press made while keys typed before
        // it are still on their way is DC-54's, below.
        await expect(editor(grid)).toHaveValue('=1+');
        await expectPointedAt(table);

        await clickCell(table, 'C3');
        await expect(editor(grid)).toHaveValue(`=1+${LOOKUP_4471.slice(1)}`);
        await header(table, 'Id').click({ force: true });
        await expect(editor(grid)).toHaveValue('=1+Positions[Id]');
        await clickCell(grid, 'B2');
        await expect(editor(grid)).toHaveValue('=1+B2');
        await expect(nameBox(grid)).toHaveValue('B2');
        await clickCell(table, 'C1');
        await expect(editor(grid)).toHaveValue('=1+XLOOKUP("R-1102", Positions[Id], Positions[PV])');
        await expect(editor(grid)).toBeFocused();
    });

    test('ADR-0058/SH-35: after a press on the positions grid, F4 changes nothing and the Name Box is empty', async ({ page }) => {
        const grid = sheet(page);
        const table = positions(page);
        await pressCell(grid, 'F3');
        await page.keyboard.type('=');
        await expectPointedAt(table);
        await clickCell(table, 'C3');
        await expect(editor(grid)).toHaveValue(LOOKUP_4471);

        await page.keyboard.press('F4');

        await expect(editor(grid)).toHaveValue(LOOKUP_4471);
        await expect(nameBox(grid)).toHaveValue('');
        await expect(editor(grid)).toBeFocused();
    });

    test('ADR-0058/SH-32: a Shift+press writes nothing, and the status line says why', async ({ page }) => {
        const grid = sheet(page);
        const table = positions(page);
        await pressCell(grid, 'F3');
        await page.keyboard.type('=SUM(');
        await expect(editor(grid)).toHaveValue('=SUM(');
        await expectPointedAt(table);

        await clickCell(table, 'C3', { modifiers: ['Shift'] });

        await expect(page.locator('#sheet-pointing')).toContainText('more than one cell');
        await expect(editor(grid)).toHaveValue('=SUM(');
        await expect(editor(grid)).toBeFocused();
    });

    test('ADR-0058/SH-32: a drag that reaches another cell takes back what its press wrote, and the status line says why', async ({ page }) => {
        const grid = sheet(page);
        const table = positions(page);
        await pressCell(grid, 'F3');
        await page.keyboard.type('=SUM(');
        await expect(editor(grid)).toHaveValue('=SUM(');
        await expectPointedAt(table);
        const from = await boxOf(cell(table, 'C1'));
        const to = await boxOf(cell(table, 'C3'));

        await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2);
        await page.mouse.down();
        // The press writes at once, before the release (ADR-0058, "What is written").
        await expect(editor(grid)).toHaveValue('=SUM(XLOOKUP("R-1102", Positions[Id], Positions[PV])');
        await page.mouse.move(to.x + to.width / 2, to.y + to.height / 2, { steps: 4 });
        await expect(editor(grid)).toHaveValue('=SUM(');
        await page.mouse.up();

        await expect(page.locator('#sheet-pointing')).toContainText('taken back');
        await expect(editor(grid)).toBeFocused();
        await expect(table.locator('.ex-focus, .ex-range')).toHaveCount(0);
    });

    test('ADR-0058/SH-35/ED-26: when the keyboard leaves the Sheet the positions grid is ordinary, the edit stands, and pointing goes on once it is back', async ({ page }) => {
        const grid = sheet(page);
        const table = positions(page);
        await pressCell(grid, 'F3');
        await page.keyboard.type('=');
        await expectPointedAt(table);

        // A control on the page takes the keyboard: the Scope stops pointing.
        await page.locator('#sheet-revalue').click();
        await expectPointedAt(table, false);
        // A press on the positions grid is its own now.
        await clickCell(table, 'B2');
        await expect(table).toBeFocused();
        await expect(table).toHaveAttribute('aria-activedescendant', /-r1c1$/);
        await expect(editor(grid)).toHaveValue('=');

        // A press back on the Sheet's rows brings the keyboard back, and points.
        await clickCell(grid, 'B2');
        await expect(editor(grid)).toHaveValue('=B2');
        await expect(editor(grid)).toBeFocused();
        await expectPointedAt(table);
        // The first press on the positions grid points again.
        await clickCell(table, 'C3');
        await expect(editor(grid)).toHaveValue(LOOKUP_4471);
        await page.keyboard.press('Escape');
        await expect(editor(grid)).toHaveCount(0);
        await expect(cell(grid, 'F3')).toHaveText('');
    });

    // The second gap of ADR-0058, "On a circuit" (DC-54; ADR-0021's note of 2026-09-30): a press on
    // the positions grid reaches the Sheet's field a round trip later, as the text written for it. The
    // positions grid tells the Sheet's root of the press in script, and the Sheet holds the keys typed
    // after it until it has been answered, and hands on the keys typed before it first.
    test('ADR-0058/DC-54: with a 150 ms round trip, =, a press on a PV cell and * at once give the lookup and the *', async ({ page }) => {
        const grid = sheet(page);
        const table = positions(page);
        await pressCell(grid, 'F3');
        await page.keyboard.type('=');
        await expect(editor(grid)).toHaveValue('=');
        await expect(editor(grid)).toBeFocused();
        await expectPointedAt(table);
        await setRoundTrip(150);

        // No wait between the two.
        await clickCell(table, 'C3');
        await page.keyboard.type('*');

        await expect(editor(grid)).toHaveValue(`${LOOKUP_4471}*`);
        await expect(editor(grid)).toBeFocused();
        await expect(table.locator('.ex-focus, .ex-range')).toHaveCount(0);
    });

    test('ADR-0058/DC-54: with a 150 ms round trip, =SUM(1,, a press, then ) and Enter at once commit the lookup inside the SUM', async ({ page }) => {
        const grid = sheet(page);
        const table = positions(page);
        await pressCell(grid, 'F3');
        await page.keyboard.type('=SUM(1,');
        await expect(editor(grid)).toHaveValue('=SUM(1,');
        await expectPointedAt(table);
        await setRoundTrip(150);

        // No wait between the three.
        await clickCell(table, 'C3');
        await page.keyboard.type(')');
        await page.keyboard.press('Enter');

        // 1 + R-4471's PV. `=SUM(1,)`, the press lost, would show 1.
        await expect(cell(grid, 'F3')).toHaveText('319.25');
        await expect(editor(grid)).toHaveCount(0);
        await setRoundTrip(0);
        await pressCell(grid, 'F3');
        await expect(bar(grid)).toHaveValue(`=SUM(1,${LOOKUP_4471.slice(1)})`);
    });

    // Found while building ticket 37: typed at once with the press, `1+` was still held behind the `=`
    // by the Sheet's listener, and the press, which the positions grid sends to its own core, reached
    // the Sheet first (`=XLOOKUP(...)` in 2 runs of 4 at 0 ms). The press is made the moment the
    // positions grid is painted pointed at, which is before the Cell Editor has taken the keyboard
    // and the held keys have gone into it. Four rounds each, since at 0 ms the gap is a race.
    for (const ms of [0, 150]) {
        test(`ADR-0058/DC-54: with a ${ms} ms round trip, =1+ typed and a press at once give =1+ and the lookup, never the lookup alone`, async ({ page }) => {
            const grid = sheet(page);
            const table = positions(page);
            await pressCell(grid, 'F3');
            const target = await boxOf(cell(table, 'C3'));
            await setRoundTrip(ms);
            for (let round = 0; round < 4; round++) {
                await page.keyboard.type('=1+');
                await pointedAtNow(table);
                await page.mouse.click(target.x + target.width / 2, target.y + target.height / 2);

                await expect(editor(grid), `round ${round + 1}`).toHaveValue(`=1+${LOOKUP_4471.slice(1)}`);
                await expect(editor(grid)).toBeFocused();
                await page.keyboard.press('Escape');
                await expect(editor(grid)).toHaveCount(0);
                await expect(grid).toBeFocused();
                await expectPointedAt(table, false);
            }
            await expect(table.locator('.ex-focus, .ex-range')).toHaveCount(0);
        });
    }

    // The first gap of ADR-0058, "On a circuit": the positions grid learns that the Sheet points from
    // the render that follows `=`, a round trip later. A press before that render is an ordinary
    // press, and the edit stands. On WebAssembly there is no round trip to press within.
    test('ADR-0058/SH-35: with a 150 ms round trip, a press within the round trip after = is an ordinary press, and the edit stands', async ({ page }) => {
        test.skip(!SERVER, 'WebAssembly has no round trip: the grid is pointed at before any press can land');
        const grid = sheet(page);
        const table = positions(page);
        await pressCell(grid, 'F3');
        await expect(grid).toBeFocused();
        await setRoundTrip(150);

        // No wait between the two.
        await page.keyboard.type('=');
        await clickCell(table, 'C3');

        await expect(table).toBeFocused();
        await expect(table).toHaveAttribute('aria-activedescendant', /-r2c2$/);
        await expect(editor(grid)).toHaveValue('=');
        // The keyboard is the positions grid's, so the Sheet does not point.
        await expectPointedAt(table, false);
        await expect(page.locator('#sheet-pointing')).toHaveText('');
        await setRoundTrip(0);
        // The edit stands, and a press back points.
        await clickCell(grid, 'B2');
        await expect(editor(grid)).toHaveValue('=B2');
        await expect(editor(grid)).toBeFocused();
    });
});

test.describe('/sheets', () => {
    test.beforeEach(async ({ page }) => {
        await page.goto('/sheets');
        await expect(cell(sheet(page, 0), 'A1')).toHaveText('Left');
        await expect(cell(sheet(page, 1), 'A1')).toHaveText('Right');
    });

    test('ADR-0058/SH-32: each side is a Scope of its own: a press on the right\'s grid while the left Sheet points is an ordinary press', async ({ page }) => {
        const left = sheet(page, 0);
        const right = sheet(page, 1);
        const leftPositions = page.locator('#sheet-left-positions .ex-grid');
        const rightPositions = page.locator('#sheet-right-positions .ex-grid');
        await pressCell(left, 'D1');
        await page.keyboard.type('=');
        await expect(editor(left)).toHaveValue('=');
        await expectPointedAt(leftPositions);
        await expectPointedAt(rightPositions, false);

        // The left's own grid points: R-4471 is its second row.
        await clickCell(leftPositions, 'C2');
        await expect(editor(left)).toHaveValue(LOOKUP_4471);
        await expect(editor(left)).toBeFocused();

        await clickCell(rightPositions, 'C2');

        await expect(rightPositions).toBeFocused();
        await expect(rightPositions).toHaveAttribute('aria-activedescendant', /-r1c2$/);
        await expect(editor(left)).toHaveValue(LOOKUP_4471);
        await expect(editor(right)).toHaveCount(0);
        // The left no longer holds the keyboard, so its grid is ordinary too.
        await expectPointedAt(leftPositions, false);
        await expect(page.locator('#sheets-pointing')).toHaveText('');
    });
});
