import { test, expect, setRoundTrip } from './fixtures.mjs';
import { SERVER } from './hosting.mjs';
import {
    sheet, positions, openSheet, cell, clickCell, editor, bar, nameBox, pressCell, boxOf, expectCovers,
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
// Id (A), Book (B) and PV (C). R-4471's PV is 318.25, and the PVs sum to 1189.4. Sorted by PV,
// ascending, R-4471 is the fourth.
//
// What the Scope draws in its grids (ADR-0058, "What is drawn"; ticket 38, SH-34): the Linked Table
// columns the Formula reads, outlined in the colours their References wear, with no OutlinedColumns
// wired by the page; the pressed cell or column dashed in the Focus outline's colour, a cell found
// by its row's key, until Point over what the press wrote ends; and the written lookup on the grey
// ground as a whole, unless it follows the = directly.

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

const TRANSPARENT = 'rgba(0, 0, 0, 0)';
// Excel's grey, #c6c6c6, over the light ground /sheet has (--ex-reference-pointed-background).
const POINTED_GROUND = 'rgb(198, 198, 198)';

/** Each span of the layer beneath a field (ADR-0057): its text, whether the core marked it as what
 * Point wrote, and how the stylesheet paints it — its ground, its ink and the colour it wears. */
const spansOf = (field) => field.evaluate((input) => [...input.previousElementSibling.querySelectorAll('span')]
    .map((span) => {
        const style = getComputedStyle(span);
        return {
            text: span.textContent,
            pointed: span.classList.contains('ex-reference-pointed'),
            ground: style.backgroundColor,
            ink: style.webkitTextFillColor,
            colour: style.color,
        };
    }));

/** The texts of the layer's spans marked as what Point wrote. */
const pointedTexts = async (field) => (await spansOf(field)).filter((span) => span.pointed).map((span) => span.text);

/** The line an outline or the dashes are drawn with. */
const lineOf = (locator) => locator.evaluate((element) => {
    const style = getComputedStyle(element);
    return { style: style.outlineStyle, colour: style.outlineColor };
});

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

    test('ADR-0058/SH-34: =SUM(1, and a press on a PV cell outline Id and PV in their text\'s colours, dash the cell, lay the lookup on the grey, and the dashes follow the row through a sort', async ({ page }) => {
        const grid = sheet(page);
        const table = positions(page);
        await pressCell(grid, 'F3');
        await page.keyboard.type('=SUM(1,');
        await expect(editor(grid)).toHaveValue('=SUM(1,');
        await expectPointedAt(table);

        await clickCell(table, 'C3');

        const lookup = LOOKUP_4471.slice(1);
        await expect(editor(grid)).toHaveValue(`=SUM(1,${lookup}`);
        // The Scope outlines the two columns the lookup reads, over their bodies: the page wires no
        // OutlinedColumns.
        const outlines = table.locator('.ex-reference-outline');
        const id = table.locator('.ex-reference-outline.ex-reference-1');
        const pv = table.locator('.ex-reference-outline.ex-reference-2');
        await expect(outlines).toHaveCount(2);
        await expectCovers(id, table, 'A1', 'A5');
        await expectCovers(pv, table, 'C1', 'C5');
        // In the colours their References wear in the Cell Editor.
        await expect.poll(() => pointedTexts(editor(grid))).toEqual([lookup]);
        const spans = await spansOf(editor(grid));
        const idText = spans.find((span) => span.text === 'Positions[Id]');
        const pvText = spans.find((span) => span.text === 'Positions[PV]');
        expect((await lineOf(id)).colour).toBe(idText.colour);
        expect((await lineOf(pv)).colour).toBe(pvText.colour);
        expect(idText.colour).not.toBe(pvText.colour);

        // The pressed cell: only dashes, in the Focus outline's colour, and no outline of its own.
        const dashes = table.locator('.ex-point-dashes');
        await expect(dashes).toHaveCount(1);
        await expectCovers(dashes, table, 'C3', 'C3');
        const dashed = await lineOf(dashes);
        expect(dashed.style).toBe('dashed');
        expect(dashed.colour).not.toBe(pvText.colour);

        // The whole lookup on the grey, what a further press would replace; the column references
        // inside it in darker shades of their colours.
        const pointed = spans.find((span) => span.pointed);
        expect(pointed.ground).toBe(POINTED_GROUND);
        for (const reference of [idText, pvText]) {
            expect(reference.pointed).toBe(false);
            expect(reference.ground).toBe(TRANSPARENT);
            expect(reference.ink).not.toBe(reference.colour);
            expect(reference.ink).not.toBe(TRANSPARENT);
        }
        expect(idText.ink).not.toBe(pvText.ink);

        // The keyboard leaves the Sheet, and the positions grid, ordinary again, is sorted by PV.
        await page.locator('h1').click();
        await expectPointedAt(table, false);
        await header(table, 'PV').click({ force: true });
        await expect(table.locator('.ex-header-cell[aria-sort=ascending]')).toHaveCount(1);
        await expect(cell(table, 'A4')).toHaveText('R-4471');

        // The dashes are over R-4471's PV, where the sort put it; Point over the lookup stands, and so
        // do the edit and the column outlines.
        await expectCovers(dashes, table, 'C4', 'C4');
        await expect(dashes).toHaveCount(1);
        await expectCovers(id, table, 'A1', 'A5');
        await expectCovers(pv, table, 'C1', 'C5');
        await expect(editor(grid)).toHaveValue(`=SUM(1,${lookup}`);
    });

    test('ADR-0058/SH-34: a press on the PV header dashes the column; an operator typed takes the dashes away, and the outline stays until the edit ends', async ({ page }) => {
        const grid = sheet(page);
        const table = positions(page);
        await pressCell(grid, 'F3');
        await page.keyboard.type('=1+');
        await expect(editor(grid)).toHaveValue('=1+');
        await expectPointedAt(table);

        await header(table, 'PV').click({ force: true });

        await expect(editor(grid)).toHaveValue('=1+Positions[PV]');
        const dashes = table.locator('.ex-point-dashes');
        const pv = table.locator('.ex-reference-outline.ex-reference-1');
        await expectCovers(dashes, table, 'C1', 'C5');
        await expectCovers(pv, table, 'C1', 'C5');
        // One Reference, written after an operator: it wears the grey itself.
        await expect.poll(() => pointedTexts(editor(grid))).toEqual(['Positions[PV]']);

        await page.keyboard.type('*2');

        await expect(editor(grid)).toHaveValue('=1+Positions[PV]*2');
        await expect(dashes).toHaveCount(0);
        await expect.poll(() => pointedTexts(editor(grid))).toEqual([]);
        await expect(table.locator('.ex-reference-outline')).toHaveCount(1);
        await expectCovers(pv, table, 'C1', 'C5');
        await page.keyboard.press('Escape');
        await expect(editor(grid)).toHaveCount(0);
        await expect(table.locator('.ex-reference-outline')).toHaveCount(0);
    });

    test('ADR-0058/SH-34: a lookup written straight after = is not on the grey; Enter takes the dashes and the outlines away', async ({ page }) => {
        const grid = sheet(page);
        const table = positions(page);
        await pressCell(grid, 'F3');
        await page.keyboard.type('=');
        await expect(editor(grid)).toHaveValue('=');
        await expectPointedAt(table);

        await clickCell(table, 'C3');

        await expect(editor(grid)).toHaveValue(LOOKUP_4471);
        const dashes = table.locator('.ex-point-dashes');
        await expectCovers(dashes, table, 'C3', 'C3');
        await expect(table.locator('.ex-reference-outline')).toHaveCount(2);
        // Coloured, and not shown selected: it follows the = directly (ADR-0057, cases 19 and 20x).
        await expect.poll(async () => (await spansOf(editor(grid))).map((span) => span.text))
            .toEqual(['Positions[Id]', 'Positions[PV]']);
        expect(await pointedTexts(editor(grid))).toEqual([]);

        await page.keyboard.press('Enter');

        await expect(cell(grid, 'F3')).toHaveText('318.25');
        await expect(dashes).toHaveCount(0);
        await expect(table.locator('.ex-reference-outline')).toHaveCount(0);
    });

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

// The arrow keys after a press on a registered grid (ADR-0058, "The keyboard", as the ninth Windows
// run settled it; ticket 41; SH-35, DC-55). /pointing puts a Sheet and a grid of 40 positions in one
// Scope. The Linked Table Positions has two columns, Id, its key, and PV; the grid shows Id (A),
// Book (B), which is the grid's own, and PV (C), and paints nine rows at a time. R-n's PV is 10n, and
// the PVs sum to 8200.
const lookup = (id, column = 'PV') => `=XLOOKUP("${id}", Positions[Id], Positions[${column}])`;
const table = (page) => page.locator('#pointing-positions .ex-grid');

/** `=` typed into the Sheet's C3, and a press on a positions cell, written. */
async function pointFrom(page, address, written) {
    const grid = sheet(page);
    await pressCell(grid, 'C3');
    await page.keyboard.type('=');
    await expect(editor(grid)).toHaveValue('=');
    await expectPointedAt(table(page));
    await clickCell(table(page), address);
    await expect(editor(grid)).toHaveValue(written);
}

test.describe('/pointing', () => {
    const refused = (page) => page.locator('#pointing-refused');

    test.beforeEach(async ({ page }) => {
        await page.goto('/pointing');
        // The table's snapshot has landed.
        await expect(cell(sheet(page), 'B1')).toHaveText('8200');
    });

    test('ADR-0058/SH-35: =, a press on R-1\'s PV, ↓ gives R-2\'s lookup and moves the dashes; ↑ goes back; Enter computes it', async ({ page }) => {
        const grid = sheet(page);
        const positions = table(page);
        await pointFrom(page, 'C1', lookup('R-1'));

        await page.keyboard.press('ArrowDown');

        await expect(editor(grid)).toHaveValue(lookup('R-2'));
        await expectCovers(positions.locator('.ex-point-dashes'), positions, 'C2', 'C2');
        // The keyboard stayed in the Sheet, and the positions grid kept no Selection for the key.
        await expect(editor(grid)).toBeFocused();
        await expect(positions.locator('.ex-focus, .ex-range')).toHaveCount(0);
        await expect(nameBox(grid)).toHaveValue('');

        await page.keyboard.press('ArrowUp');
        await expect(editor(grid)).toHaveValue(lookup('R-1'));
        await expectCovers(positions.locator('.ex-point-dashes'), positions, 'C1', 'C1');

        await page.keyboard.press('ArrowDown');
        await expect(editor(grid)).toHaveValue(lookup('R-2'));
        await page.keyboard.press('Enter');
        await expect(cell(grid, 'C3')).toHaveText('20');
        await expect(refused(page)).toHaveText('');
    });

    test('ADR-0058/SH-35: → from an Id cell passes over Book, the grid\'s own column, to PV; at the last column nothing moves; ← comes back over it', async ({ page }) => {
        const grid = sheet(page);
        const positions = table(page);
        await pointFrom(page, 'A1', lookup('R-1', 'Id'));

        await page.keyboard.press('ArrowRight');

        await expect(editor(grid)).toHaveValue(lookup('R-1'));
        await expectCovers(positions.locator('.ex-point-dashes'), positions, 'C1', 'C1');
        await page.keyboard.press('ArrowRight');
        await page.keyboard.press('ArrowLeft');
        await expect(editor(grid)).toHaveValue(lookup('R-1', 'Id'));
        await expectCovers(positions.locator('.ex-point-dashes'), positions, 'A1', 'A1');
        await expect(editor(grid)).toBeFocused();
        await expect(refused(page)).toHaveText('');
    });

    test('ADR-0058/SH-35/DC-55: ↓ past the painted rows scrolls the positions grid to keep the pointed cell in view', async ({ page }) => {
        const grid = sheet(page);
        const positions = table(page);
        const scroller = positions.locator('.ex-scroller');
        await expect.poll(() => scroller.evaluate((element) => element.scrollTop)).toBe(0);
        // R-9, the last row in view.
        await pointFrom(page, 'C9', lookup('R-9'));

        await page.keyboard.press('ArrowDown');

        await expect(editor(grid)).toHaveValue(lookup('R-10'));
        await expect.poll(() => scroller.evaluate((element) => element.scrollTop)).toBeGreaterThan(0);
        const dashes = positions.locator('.ex-point-dashes');
        await expectCovers(dashes, positions, 'C10', 'C10');
        // In view: inside the scroller, under its header.
        const view = await boxOf(scroller);
        const header = await boxOf(positions.locator('.ex-header'));
        const box = await boxOf(dashes);
        expect(box.y).toBeGreaterThanOrEqual(header.y + header.height - 0.5);
        expect(box.y + box.height).toBeLessThanOrEqual(view.y + view.height + 0.5);
        await expect(editor(grid)).toBeFocused();
    });

    test('ADR-0058/SH-35: Shift+↓ and Ctrl+↓ write nothing, leave the text as it was, and the page says why; ↓ still points', async ({ page }) => {
        const grid = sheet(page);
        await pointFrom(page, 'C1', lookup('R-1'));

        await page.keyboard.press('Shift+ArrowDown');
        await expect(refused(page)).toContainText('Shift+arrow');
        await expect(editor(grid)).toHaveValue(lookup('R-1'));
        await page.keyboard.press('Control+ArrowDown');
        await expect(refused(page)).toContainText('Ctrl+arrow');
        await expect(editor(grid)).toHaveValue(lookup('R-1'));
        await expect(editor(grid)).toBeFocused();

        await page.keyboard.press('ArrowDown');
        await expect(editor(grid)).toHaveValue(lookup('R-2'));
    });

    test('ADR-0058/SH-35: after a press on the PV header an arrow writes nothing, and the page says to press a cell (decided 2026-10-01)', async ({ page }) => {
        const grid = sheet(page);
        const positions = table(page);
        await pressCell(grid, 'C3');
        await page.keyboard.type('=SUM(');
        await expect(editor(grid)).toHaveValue('=SUM(');
        await expectPointedAt(positions);
        await header(positions, 'PV').click({ force: true });
        await expect(editor(grid)).toHaveValue('=SUM(Positions[PV]');

        await page.keyboard.press('ArrowDown');

        await expect(refused(page)).toContainText('Press a cell to point by keys');
        await expect(editor(grid)).toHaveValue('=SUM(Positions[PV]');
        await expect(editor(grid)).toBeFocused();
        await expect(positions.locator('.ex-point-dashes')).toHaveCount(1);
        await page.keyboard.type(')');
        await page.keyboard.press('Enter');
        await expect(cell(grid, 'C3')).toHaveText('8200');
    });

    // The keys typed after an arrow keep their place behind it (ADR-0010's hold): the arrow is answered
    // once the Scope has rewritten the text, and on a circuit that is a round trip after the key.
    test('ADR-0058/SH-35: with a 150 ms round trip, ↓ and *2 typed at once give R-2\'s lookup and the *2', async ({ page }) => {
        const grid = sheet(page);
        await pointFrom(page, 'C1', lookup('R-1'));
        await setRoundTrip(150);

        // No wait between them.
        await page.keyboard.press('ArrowDown');
        await page.keyboard.type('*2');

        await expect(editor(grid)).toHaveValue(`${lookup('R-2')}*2`);
        await page.keyboard.press('Enter');
        await expect(cell(grid, 'C3')).toHaveText('40');
    });
});

// The positions grid with its own scrollbars (DC-53; ticket 57). Part B of the ninth Windows run
// could not set this up: /pointing's grid is wider than its columns, so it has no horizontal
// scrollbar, and its last column ends 18 px short of the vertical one. ?narrow lays the grid out
// narrower than its columns. Scrolled to its last row and its last column, R-40's PV lies against
// both gutters, and what the Scope draws over it stops where they begin.
test.describe('/pointing?narrow', () => {
    // A rectangle flush with a gutter may round the wrong way by a hair; a gutter is 12 px.
    const SLACK_PX = 1;

    test.beforeEach(async ({ page }) => {
        await page.goto('/pointing?narrow');
        await expect(page.locator('#pointing-narrow')).toBeVisible();
        await expect(cell(sheet(page), 'B1')).toHaveText('8200');
    });

    /**
     * The scroller's client area — what its scrollbars leave readable, the browser's own answer —
     * and the strips they take, in the coordinates boundingBox gives, read at one instant.
     */
    const clientAreaOf = (scroller) => scroller.evaluate((element) => {
        const outer = element.getBoundingClientRect();
        return {
            left: outer.left + element.clientLeft,
            top: outer.top + element.clientTop,
            right: outer.left + element.clientLeft + element.clientWidth,
            bottom: outer.top + element.clientTop + element.clientHeight,
            gutterWidth: element.offsetWidth - element.clientWidth,
            gutterHeight: element.offsetHeight - element.clientHeight,
            overflows: element.scrollWidth > element.clientWidth && element.scrollHeight > element.clientHeight,
        };
    });

    test('ADR-0058/DC-53: scrolled to R-40\'s PV, =, a press on it: the dashes and both column outlines lie inside the client area, not under either gutter', async ({ page }, testInfo) => {
        const positions = table(page);
        const scroller = positions.locator('.ex-scroller');

        const before = await clientAreaOf(scroller);
        testInfo.annotations.push({ type: 'gutter', description: `${before.gutterWidth}x${before.gutterHeight} on ${process.platform}` });
        // Both of its own scrollbars: the columns overflow the box as the rows do.
        expect(before.overflows, 'the grid overflows on both axes').toBe(true);
        // Headless Chrome on macOS keeps overlay bars whatever the CSS asks (README.md; both strips
        // measured 0 there on 2026-10-01), so there the sides below are held to no gutter at all.
        // Where the bars occupy layout, as on CI's Linux, a 0 would leave them proving nothing.
        if (process.platform !== 'darwin') {
            expect(before.gutterWidth, 'the vertical scrollbar occupies layout').toBeGreaterThan(0);
            expect(before.gutterHeight, 'the horizontal scrollbar occupies layout').toBeGreaterThan(0);
        }
        // Until the grid is scrolled right, PV runs under the vertical gutter.
        const pvUnscrolled = await boxOf(cell(positions, 'C1'));
        expect(pvUnscrolled.x + pvUnscrolled.width, 'PV ends past the client area').toBeGreaterThan(before.right + SLACK_PX);

        // To the last row and the last column.
        await scroller.evaluate((element) => {
            element.scrollTop = element.scrollHeight;
            element.scrollLeft = element.scrollWidth;
        });
        await expect(cell(positions, 'C40')).toBeVisible();
        await pointFrom(page, 'C40', lookup('R-40'));

        const dashes = positions.locator('.ex-point-dashes');
        const outlines = positions.locator('.ex-reference-outline');
        await expectCovers(dashes, positions, 'C40', 'C40');
        await expect(outlines).toHaveCount(2);
        const client = await clientAreaOf(scroller);
        // The case this is about: R-40's PV lies against both gutters, its right side the client
        // area's right edge and its bottom the client area's bottom edge.
        const r40 = await boxOf(cell(positions, 'C40'));
        expect(Math.abs(r40.x + r40.width - client.right), 'PV ends at the vertical gutter').toBeLessThanOrEqual(SLACK_PX);
        expect(Math.abs(r40.y + r40.height - client.bottom), 'R-40 ends at the horizontal gutter').toBeLessThanOrEqual(SLACK_PX);

        // The dashes, whole inside the client area, under the header.
        const header = await boxOf(positions.locator('.ex-header'));
        const dashed = await boxOf(dashes);
        expect(dashed.x).toBeGreaterThanOrEqual(client.left - SLACK_PX);
        expect(dashed.y).toBeGreaterThanOrEqual(header.y + header.height - SLACK_PX);
        expect(dashed.x + dashed.width, 'the dashes run under the vertical gutter').toBeLessThanOrEqual(client.right + SLACK_PX);
        expect(dashed.y + dashed.height, 'the dashes run under the horizontal gutter').toBeLessThanOrEqual(client.bottom + SLACK_PX);

        // Id's outline and PV's, each to R-40's bottom and no further. Their tops run up under the
        // header, and Id's left side is scrolled out past the client area's left edge: those edges
        // are the Viewport's, not a gutter.
        for (let i = 0; i < 2; i++) {
            const outline = await boxOf(outlines.nth(i));
            expect(Math.abs(outline.y + outline.height - (r40.y + r40.height)), `outline ${i + 1} ends at R-40`).toBeLessThanOrEqual(SLACK_PX);
            expect(outline.x + outline.width, `outline ${i + 1} runs under the vertical gutter`).toBeLessThanOrEqual(client.right + SLACK_PX);
            expect(outline.y + outline.height, `outline ${i + 1} runs under the horizontal gutter`).toBeLessThanOrEqual(client.bottom + SLACK_PX);
        }
        await expect(editor(sheet(page))).toBeFocused();
    });
});
