import { test, expect, setRoundTrip, alterPage } from './fixtures.mjs';
import { SERVER } from './hosting.mjs';
import { expectKeyboardOn, expectActiveDescendant } from './keyboard.mjs';
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
// the keyboard back to the grid — its Keyboard Field, since a Sheet edits (ADR-0080) — as if
// the keyboard had never left.
//
// /sheet: the positions grid beside the Sheet is in its Pointing Scope (ADR-0058), and is an
// ordinary grid once the keyboard has left the Sheet; a page button stands for a control of the
// Consumer's. /sheets: two Sheets, each able to hold an edit of its own.

// Tall enough that every Sheet on the page, Formula Bar to horizontal scrollbar, is inside the
// window: a pointer below the window's edge reaches nothing, and the edge band sits there.
test.use({ viewport: { width: 1280, height: 1000 } });

/**
 * Presses the positions grid's FX cell: the keyboard is that grid's once its root has it. The
 * positions grid is in the Sheet's Pointing Scope (ADR-0058): while the Sheet points, a press on it
 * would point, and the keyboard would stay in the Sheet. So the keyboard first leaves the Sheet for a
 * control on the page, and once the Scope has stopped pointing the grid is an ordinary grid, as any
 * other grid on the page is.
 */
async function pressPositions(page) {
    // Revalue replaces every position's row, and the render that lands it replaces the cells: a
    // press aimed at FX's cell while it lands found that cell detached (found on CI, the Server
    // host, 2026-10-01). So the press waits until FX's row shows its new PV.
    const pv = positions(page).locator('.ex-row', { has: page.locator('.ex-cell', { hasText: /^FX$/ }) })
        .first().locator('.ex-cell').nth(2);
    const before = await pv.textContent();
    await page.locator('#sheet-revalue').click();
    await expect(positions(page)).not.toHaveClass(/\bex-pointed-at\b/);
    await expect(pv).not.toHaveText(before);
    const fx = positions(page).locator('.ex-cell', { hasText: /^FX$/ }).first();
    await expect(fx).toBeVisible();
    // Cells are pointer-events: none; the press lands on the Viewport (ADR-0004).
    await fx.click({ force: true });
    await expectKeyboardOn(positions(page));
}

// Under the built-in Chrome and ExGrid.MudBlazor's: a Chrome paints the surfaces as boxes around
// controls of its own, and the keyboard comes back into the control (ADR-0010/0030).
for (const chrome of ['builtin', 'mud']) {
    test.describe(`/sheet under the ${chrome} Chrome`, () => {
        test.beforeEach(async ({ page }) => {
            await openSheet(page, chrome);
        });

        test('ADR-0018/ED-26: an edit left for another grid stands, that grid\'s keys are its own, and a press back points with the keyboard in the edit', async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'C4');
            await page.keyboard.type('=');
            await expect(editor(grid)).toHaveValue('=');
            await expect(editor(grid)).toBeFocused();

            await pressPositions(page);
            // Neither committed nor discarded.
            await expect(editor(grid)).toHaveValue('=');
            await expect(bar(grid)).toHaveValue('=');
            // The positions grid's keys are its own (KB-1): ↓ moves its Focus, Escape releases its
            // Tab and it keeps the keyboard (ADR-0012, rewritten 2026-10-01; KB-8), and the Sheet's
            // edit stands through both.
            await expect(positions(page)).toHaveAttribute('aria-activedescendant', /-r2c1$/);
            await page.keyboard.press('ArrowDown');
            await expect(positions(page)).toHaveAttribute('aria-activedescendant', /-r3c1$/);
            await page.keyboard.press('Escape');
            await expectKeyboardOn(positions(page));
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
            await expectKeyboardOn(grid);
            await expect(positions(page)).toHaveAttribute('aria-activedescendant', /-r3c1$/);
        });

        test('ADR-0018/ADR-0080/ED-26: a press back where no Reference can go commits, moves, and gives the Sheet the keyboard', async ({ page }) => {
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
            await expectKeyboardOn(grid);
            // The next keys are the Sheet's, at the Focus the press moved to.
            await page.keyboard.type('7');
            await expect(editor(grid)).toHaveValue('7');
            await page.keyboard.press('Enter');
            await expect(cell(grid, 'B2')).toHaveText('7');
            await expectFocusAt(grid, 'B3');
        });

        test('ADR-0018/ED-26: an edit last typed in the Formula Bar gets the keyboard back in the bar', async ({ page }) => {
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
            await expectKeyboardOn(grid);
        });

        test('ADR-0018/ADR-0021/ADR-0080/ED-26: a press back that commits an edit last typed in the Formula Bar gives the grid the keyboard, not the bar', async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'E4');
            await clickBarEnd(grid);
            await typeSteadily(page, bar(grid), '5');
            await pressPositions(page);

            await clickCell(grid, 'B2');

            await expect(cell(grid, 'E4')).toHaveText('5');
            await expectFocusAt(grid, 'B2');
            await expectKeyboardOn(grid);
            await page.keyboard.type('7');
            await page.keyboard.press('Enter');
            await expect(cell(grid, 'B2')).toHaveText('7');
        });

        // The same press without leaving: the core keeps DOM focus where it is through a press on
        // the rows while an edit is open (ADR-0051), so the bar kept it after the commit and the
        // hand-back left it there — typing went into a bar with no edit open, and nowhere else.
        // The bar's focus was left standing by the press, and the hand-back takes it (ADR-0021),
        // into the grid's Keyboard Field (ADR-0080).
        test('ADR-0021/ADR-0080/ED-26: a press on the rows that commits an edit typed in the Formula Bar gives the grid the keyboard', async ({ page }) => {
            const grid = sheet(page);
            await pressCell(grid, 'E4');
            await clickBarEnd(grid);
            await typeSteadily(page, bar(grid), '5');

            await clickCell(grid, 'B2');

            await expect(cell(grid, 'E4')).toHaveText('5');
            await expectFocusAt(grid, 'B2');
            await expectKeyboardOn(grid);
            await page.keyboard.type('7');
            await page.keyboard.press('Enter');
            await expect(cell(grid, 'B2')).toHaveText('7');
        });

        test('ADR-0018/ED-26: an edit left for a control on the page stands, and a press back commits it', async ({ page }) => {
            const grid = sheet(page);
            // A control the open edit leaves enabled: the page greys out its commands that
            // change the Sheet while an edit is open (ADR-0048, SH-29), and a Linked Table's
            // snapshot is data arriving, not one of them (ADR-0049).
            const control = page.locator('#sheet-revalue');
            await pressCell(grid, 'C4');
            await page.keyboard.type('99');
            await expect(editor(grid)).toHaveValue('99');

            await control.click();
            await expect(control).toBeFocused();
            await expect(editor(grid)).toHaveValue('99');
            await expect(cell(grid, 'C4')).toHaveText('0.2');

            await clickCell(grid, 'B2');
            await expect(cell(grid, 'C4')).toHaveText('99');
            await expectFocusAt(grid, 'B2');
            await expectKeyboardOn(grid);
        });
    });
}

// On a circuit the answer to the press is a round trip away. Had the keyboard been handed back
// from C#, the key typed straight after the press would still reach the positions grid, and the
// Sheet's edit would stand with nothing able to reach it (ADR-0018 section 6). On WebAssembly
// there is no round trip, and this is the case without one.
test('ADR-0018/ADR-0021/ED-26: with a 150 ms round trip, the key straight after the press back is the Sheet\'s', async ({ page }) => {
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
    await expectKeyboardOn(grid);
    await expect(positions(page)).toHaveAttribute('aria-activedescendant', /-r2c1$/);
});

// A press on the rows of a grid nested in the Sheet is that grid's (ADR-0018): it neither brings
// the keyboard back to the Sheet's edit nor starts the Sheet's hold behind a press (ADR-0010), and
// the keys typed after it reach the nested grid. A stand-in stands for the nested grid — its own
// root, scroller and rows inside the Sheet's root, taking the keyboard as a grid does and noting
// the keys it hears — so the Sheet's listener sees the press and the keys as it would a real one's.
// On the Server host at 150 ms, a hold of the Sheet's would still stand as the key is typed.
test('ADR-0010/ADR-0018/ED-22: a press on the rows of a grid nested in the Sheet starts no hold of the Sheet\'s', async ({ page }) => {
    await openSheet(page);
    const grid = sheet(page);
    await pressCell(grid, 'C4');
    await page.keyboard.type('=');
    await expect(editor(grid)).toHaveValue('=');
    await grid.evaluate((root) => {
        const nested = document.createElement('div');
        nested.className = 'ex-grid';
        nested.id = 'edit-stands-nested';
        nested.tabIndex = 0;
        nested.style.cssText = 'position: absolute; right: 24px; bottom: 24px; width: 120px; height: 60px; z-index: 20; background: Canvas';
        nested.innerHTML = '<div class="ex-scroller" style="height: 60px"><div class="ex-spacer" style="height: 60px">'
            + '<div class="ex-viewport" style="top: 0; height: 60px"></div></div></div>';
        nested.dataset.heard = '';
        // A key the Sheet's listener held and handed on later arrives dispatched from script,
        // untrusted: only the browser's own keydown counts as heard.
        nested.addEventListener('keydown', (event) => {
            nested.dataset.heard += event.isTrusted ? event.key : `(handed on: ${event.key})`;
        });
        root.append(nested);
    });
    const nested = page.locator('#edit-stands-nested');
    await expect(nested.locator('.ex-viewport')).toBeVisible();
    // A hold of the Sheet's would last until the Sheet's core had answered: on a circuit a round
    // trip, wide enough to type into.
    await setRoundTrip(150);

    // No wait between the two.
    await nested.locator('.ex-viewport').click();
    await page.keyboard.type('x');

    await expect(nested).toBeFocused();
    await expect(nested).toHaveAttribute('data-heard', 'x');
    await expect(editor(grid)).toHaveValue('=');
    await nested.evaluate((element) => element.remove());
});

test.describe('/sheets', () => {
    test.beforeEach(async ({ page }) => {
        await page.goto('/sheets');
        await expect(cell(sheet(page, 0), 'A1')).toHaveText('Left');
        await expect(cell(sheet(page, 1), 'A1')).toHaveText('Right');
    });

    test('ADR-0018/ED-26: two Sheets each hold an edit, and a press back on the rows gives each its keyboard', async ({ page }) => {
        const left = sheet(page, 0);
        const right = sheet(page, 1);
        await pressCell(left, 'D1');
        await page.keyboard.type('=');
        await expect(editor(left)).toHaveValue('=');
        await pressCell(right, 'D2');
        await expectKeyboardOn(right);
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
        await expectKeyboardOn(left);
        await expect(editor(right)).toHaveValue('5');

        // Back on the right's rows, where no Reference can go after 5: it commits, and the right
        // has the keyboard.
        await clickCell(right, 'E3');
        await expect(cell(right, 'D2')).toHaveText('5');
        await expectFocusAt(right, 'E3');
        await expectKeyboardOn(right);
        await page.keyboard.type('7');
        await page.keyboard.press('Enter');
        await expect(cell(right, 'E3')).toHaveText('7');
        await expect(cell(left, 'E3')).toHaveText('');
    });

    test('ADR-0018/ADR-0080/ED-26: a press back on a column heading or a Row Heading commits the edit left standing, and the Sheet has the keyboard', async ({ page }) => {
        const left = sheet(page, 0);
        const right = sheet(page, 1);
        await pressCell(left, 'D3');
        await page.keyboard.type('4');
        await expect(editor(left)).toHaveValue('4');
        await pressCell(right, 'A5');
        await expectKeyboardOn(right);
        await expect(editor(left)).toHaveValue('4');

        await left.locator('.ex-header-cell', { hasText: /^C$/ }).click({ force: true });
        await expect(cell(left, 'D3')).toHaveText('4');
        await expectFocusAt(left, 'C1');
        await expectKeyboardOn(left);

        await pressCell(left, 'D4');
        await page.keyboard.type('6');
        await expect(editor(left)).toHaveValue('6');
        await clickCell(right, 'A6');
        await expectKeyboardOn(right);

        await left.locator('.ex-row .ex-row-heading', { hasText: /^6$/ }).click({ force: true });
        await expect(cell(left, 'D4')).toHaveText('6');
        await expectFocusAt(left, 'A6');
        await expectKeyboardOn(left);
    });

    test('ADR-0018/ADR-0021/ED-26: with a 150 ms round trip, the key straight after the press back is that Sheet\'s', async ({ page }) => {
        const left = sheet(page, 0);
        const right = sheet(page, 1);
        await pressCell(left, 'D1');
        await page.keyboard.type('=');
        await expect(editor(left)).toBeFocused();
        await pressCell(right, 'D2');
        await expectKeyboardOn(right);
        await setRoundTrip(150);

        // No wait between the two: Escape in the right Sheet would release its Tab (ADR-0012).
        await clickCell(left, 'B1');
        await page.keyboard.press('Escape');

        await expect(editor(left)).toHaveCount(0);
        await expect(cell(left, 'D1')).toHaveText('');
        await expectKeyboardOn(left);
        await expect(editor(right)).toHaveCount(0);
        await expectFocusAt(right, 'D2');
    });
});

// ED-27 (ADR-0018 section 6, 2026-09-29): an edit whose keyboard is elsewhere is told apart. While
// DOM focus is outside a Sheet's root, its Cell Editor's outline is 1px wide, in the style and colour
// --ex-editor-outline gives it, and at the token's full width again once the keyboard is back —
// under the core's Chrome, and under ExGrid.MudBlazor's, whose token is 2px of the palette's
// primary, on the Wrapper's paper. The stylesheet alone does it (`:focus-within` on the root).
for (const chrome of ['builtin', 'mud']) {
    test(`ADR-0018/ED-27: an edit whose keyboard is elsewhere is outlined 1px wide, and at full width once it returns (${chrome} Chrome)`, async ({ page }) => {
        await page.goto(chrome === 'builtin' ? '/sheets' : `/sheets?chrome=${chrome}`);
        const left = sheet(page, 0);
        const right = sheet(page, 1);
        await expect(cell(left, 'A1')).toHaveText('Left');
        await expect(cell(right, 'A1')).toHaveText('Right');
        if (chrome !== 'builtin') {
            await expect(left.locator('.mud-ex-editor, .mud-ex-formula-bar-text').first()).toBeAttached();
        }
        // The outline of the box the core floats over the cell, as the browser computes it.
        const outline = (grid) => editor(grid).evaluate((field) => {
            const style = getComputedStyle(field.closest('.ex-editor'));
            return { width: style.outlineWidth, style: style.outlineStyle, color: style.outlineColor };
        });

        await pressCell(left, 'D1');
        await page.keyboard.type('=');
        await expect(editor(left)).toBeFocused();
        const full = await outline(left);
        expect(full.style).toBe('solid');
        expect(full.width).toBe('2px');
        if (chrome !== 'builtin') {
            // The Wrapper's token, not the core's default: the palette's primary.
            const primary = await left.evaluate((root) => {
                const probe = document.createElement('span');
                probe.style.color = getComputedStyle(root).getPropertyValue('--mud-palette-primary');
                root.append(probe);
                const color = getComputedStyle(probe).color;
                probe.remove();
                return color;
            });
            expect(full.color).toBe(primary);
        }

        // The keyboard goes to the other Sheet: the left edit stands, outlined 1px, same colour.
        await pressCell(right, 'D2');
        await expectKeyboardOn(right);
        await expect.poll(() => outline(left)).toEqual({ ...full, width: '1px' });
        await page.keyboard.type('5');
        await expect(editor(right)).toBeFocused();
        await expect.poll(() => outline(right)).toEqual(full);
        await expect.poll(() => outline(left)).toEqual({ ...full, width: '1px' });

        // Back on the left's rows: the keyboard returns to its edit, and the widths swap.
        await clickCell(left, 'B1');
        await expect(editor(left)).toHaveValue('=B1');
        await expect(editor(left)).toBeFocused();
        await expect.poll(() => outline(left)).toEqual(full);
        await expect.poll(() => outline(right)).toEqual({ ...full, width: '1px' });

        // To nothing at all: Escape in the right would end its edit, so the page takes focus.
        await page.locator('h1').click();
        await expect.poll(() => outline(left)).toEqual({ ...full, width: '1px' });
        await expect.poll(() => outline(right)).toEqual({ ...full, width: '1px' });
    });
}

// ED-28 (ADR-0021's note of 2026-09-30, ADR-0018 section 6): an edit that opens takes the keyboard
// only while the keyboard is still this grid's. The Cell Editor is focused after the render that
// paints it, a round trip later on a circuit; a press on the other Sheet in between keeps its
// keyboard, the left edit stands with what was typed, and a press back on the left's rows brings the
// keyboard to it. Found by CI on the Server host, in ED-26's /sheets test above: the left editor's
// focus landed after the press on the right and took the keyboard back. The race failed 9 runs in 40
// on the base with 40 ms injected. WebAssembly has no round trip for a press to land in.
const inRoot = (page, index) => page.evaluate((i) => {
    const grids = [...document.querySelectorAll('.ex-grid')].filter((g) => g.querySelector(':scope > .ex-formula-bar'));
    return grids[i].contains(document.activeElement);
}, index);

for (const chrome of ['builtin', 'mud']) {
    test.describe(`ADR-0021/ED-28: an edit opening on the left Sheet, and a press on the right at once, 40 ms round trip (${chrome} Chrome)`, () => {
        test.beforeEach(async ({ page }) => {
            test.skip(!SERVER, 'WebAssembly has no round trip: the editor is focused before any press can land');
            await page.goto(chrome === 'builtin' ? '/sheets' : `/sheets?chrome=${chrome}`);
            await expect(cell(sheet(page, 0), 'A1')).toHaveText('Left');
            await expect(cell(sheet(page, 1), 'A1')).toHaveText('Right');
            if (chrome !== 'builtin') {
                await expect(sheet(page, 0).locator('.mud-ex-formula-bar-text').first()).toBeAttached();
            }
        });

        test('= typed on the left and the right pressed at once, thirty times: the right keeps the keyboard, and the left edit stands', async ({ page }) => {
            const left = sheet(page, 0);
            const right = sheet(page, 1);
            await setRoundTrip(40);
            for (let round = 0; round < 30; round++) {
                await pressCell(left, 'D1');
                // No wait between the two: the press lands before the left's editor has the keyboard.
                await page.keyboard.type('=');
                await pressCell(right, 'D2');
                await expect(editor(left)).toHaveValue('=');
                // Long past the round trip in which the left's editor would take focus: still the right's.
                // Soft, so a run says how many of the thirty rounds lost it.
                await page.waitForTimeout(200);
                expect.soft(await inRoot(page, 1), `round ${round}: the right holds the keyboard`).toBe(true);
                await expect(editor(left)).toHaveValue('=');

                // The press back brings the keyboard to the edit standing there (ED-26), and points.
                await clickCell(left, 'B1');
                await expect(editor(left)).toHaveValue('=B1');
                await expect(editor(left)).toBeFocused();
                await page.keyboard.press('Escape');
                await expect(editor(left)).toHaveCount(0);
            }
        });

        test('=1 typed at full speed and the right pressed at once: the 1 held behind the = goes into the left edit, not the right', async ({ page }) => {
            const left = sheet(page, 0);
            const right = sheet(page, 1);
            await setRoundTrip(40);
            await pressCell(left, 'D1');
            await page.keyboard.type('=1');
            await pressCell(right, 'D2');
            await expectKeyboardOn(right);
            await expect(editor(left)).toHaveValue('=1');
            await page.waitForTimeout(200);
            expect(await inRoot(page, 1)).toBe(true);
            await expect(editor(left)).toHaveValue('=1');
            // Nothing reached the right: it opened no edit, and its cell is as it was.
            await expect(editor(right)).toHaveCount(0);
            await expect(cell(right, 'D2')).toHaveText('');

            // The press back: no Reference can go after =1, so it commits, and the left has the keyboard.
            await clickCell(left, 'B1');
            await expect(cell(left, 'D1')).toHaveText('1');
            await expectKeyboardOn(left);
        });
    });
}

// ED-22, widened (ADR-0010, 2026-09-29): a press on the rows while an edit is open is a mode change
// too, and the keys typed after it are held until the core has answered it, then handed on against
// the mode the answer leaves (ADR-0021's note of the same day). Found building this file: `99` over
// C4, a press on B2, `7` at once, and the `7` was lost — on the Sheet, where the core keeps DOM focus
// in the editor through the press (ADR-0051), it went into the editor the commit was removing. On
// the Server host at 150 ms and without injected latency; the case without one runs on both hosts.
for (const rtt of [0, 150]) {
    test.describe(`ADR-0010/ED-22: keys straight after a press on the rows while an edit is open, ${rtt ? `${rtt} ms round trip` : 'no injected latency'}`, () => {
        test.beforeEach(() => {
            test.skip(rtt > 0 && !SERVER, 'a round trip is injected on the Server host only; the case without one runs here');
        });

        test('ADR-0010/ED-22: on a plain editable grid, the key after a press that commits opens an edit on the pressed cell', async ({ page }) => {
            await page.goto('/features');
            const grid = page.locator('.ex-grid').first();
            const at = (row, column) => grid.locator(`[id$='-r${row}c${column}']`);
            await expect(at(2, 1)).toBeVisible();
            await at(0, 1).click({ force: true });                // Trader, editable
            await expectActiveDescendant(grid, /-r0c1$/);
            await page.keyboard.type('99');
            await expect(grid.locator('input.ex-editor')).toHaveValue('99');
            await setRoundTrip(rtt);

            // No wait between the two.
            await at(2, 1).click({ force: true });
            await page.keyboard.type('7');

            await expect(page.locator('#edit-status')).toContainText('Trader=99');
            await expect(grid.locator('input.ex-editor')).toHaveValue('7');
            await expectActiveDescendant(grid, /-r2c1$/);
            await page.keyboard.press('Enter');
            await expect(at(0, 1)).toHaveText('99');
            await expect(at(2, 1)).toHaveText('7');
        });

        test('ADR-0010/ADR-0021/ED-22: on a Sheet, the key after a press that commits opens an edit on the pressed cell', async ({ page }) => {
            await openSheet(page);
            const grid = sheet(page);
            await pressCell(grid, 'C4');
            await page.keyboard.type('99');
            await expect(editor(grid)).toHaveValue('99');
            await setRoundTrip(rtt);

            await clickCell(grid, 'B2');
            await page.keyboard.type('7');

            await expect(cell(grid, 'C4')).toHaveText('99');
            await expect(editor(grid)).toHaveValue('7');
            await expectFocusAt(grid, 'B2');
            await page.keyboard.press('Enter');
            await expect(cell(grid, 'B2')).toHaveText('7');
            await expectFocusAt(grid, 'B3');
        });

        test('ADR-0010/ADR-0051/ED-22: on a Sheet, the keys after a press that points are typed after the Reference it wrote', async ({ page }) => {
            await openSheet(page);
            const grid = sheet(page);
            await pressCell(grid, 'C4');
            await page.keyboard.type('=');
            await expect(editor(grid)).toHaveValue('=');
            await setRoundTrip(rtt);

            await clickCell(grid, 'B2');
            await page.keyboard.type('+1');
            await page.keyboard.press('Enter');

            await expect(cell(grid, 'C4')).toHaveText('13');
            await expect(editor(grid)).toHaveCount(0);
            await expectFocusAt(grid, 'C5');
        });

        // What the hold must not change: the second press of a double click reaches the core ahead
        // of the double click, so a double click on another cell still commits the open edit and
        // opens the clicked cell's text in Caret (ADR-0010).
        // Outside an edit a press on the rows and an arrow change no mode, and start no hold
        // (ADR-0010: plain navigation is never paced to one round trip). What the grid holds never
        // reaches the page, so the page's own listener tells: a key the grid leaves to the browser
        // (Shift) typed straight after them arrives at once, and while an edit is open, behind a
        // press on the rows, it does not.
        test('ADR-0010/ED-22: outside an edit, a press on the rows and an arrow hold nothing', async ({ page }) => {
            await page.goto('/features');
            const grid = page.locator('.ex-grid').first();
            const at = (row, column) => grid.locator(`[id$='-r${row}c${column}']`);
            await expect(at(2, 1)).toBeVisible();
            await alterPage(page, () => {
                const heard = (window.__editStandsHeard = []);
                const listener = (event) => heard.push(event.key);
                window.addEventListener('keydown', listener);
                return () => {
                    window.removeEventListener('keydown', listener);
                    delete window.__editStandsHeard;
                };
            });
            const heard = () => page.evaluate(() => [...window.__editStandsHeard]);
            await setRoundTrip(rtt);

            await at(0, 0).click({ force: true });               // Book: not editable, no edit
            await page.keyboard.press('ArrowDown');
            await page.keyboard.press('Shift');

            await expect.poll(heard).toEqual(['Shift']);
            await expectActiveDescendant(grid, /-r1c0$/);

            // What the listener would see of a hold: behind a press that commits an edit, the
            // Shift is held, and a modifier alone is dropped when the hold is handed on. Only a
            // round trip makes that window wide enough to type into on purpose.
            if (rtt > 0) {
                await page.evaluate(() => { window.__editStandsHeard.length = 0; });
                await at(0, 1).click({ force: true });           // Trader, editable
                await page.keyboard.type('9');
                await expect(grid.locator('input.ex-editor')).toHaveValue('9');
                await page.evaluate(() => { window.__editStandsHeard.length = 0; });
                await at(2, 1).click({ force: true });
                await page.keyboard.press('Shift');
                await expect(page.locator('#edit-status')).toContainText('Trader=9');
                await expectActiveDescendant(grid, /-r2c1$/);
                expect(await heard()).toEqual([]);
            }
        });

        // The same double click on a plain grid, whose press on the rows keeps its default: DOM
        // focus goes to the rows, and on through the root to its Keyboard Field (ADR-0080), while
        // the hold stands.
        test('ADR-0010/ED-22: on a plain editable grid, a double click on another cell commits the edit and opens that cell\'s text', async ({ page }) => {
            await page.goto('/features');
            const grid = page.locator('.ex-grid').first();
            const at = (row, column) => grid.locator(`[id$='-r${row}c${column}']`);
            const field = grid.locator('input.ex-editor');
            await expect(at(2, 1)).toBeVisible();
            const trader = (await at(2, 1).textContent()).trim();
            await at(0, 1).click({ force: true });                // Trader, editable
            await expectActiveDescendant(grid, /-r0c1$/);
            await page.keyboard.type('99');
            await expect(field).toHaveValue('99');
            await setRoundTrip(rtt);

            await at(2, 1).dblclick({ force: true });

            await expect(page.locator('#edit-status')).toContainText('Trader=99');
            await expect(field).toHaveValue(trader);
            await expect(field).toBeFocused();
            await expectActiveDescendant(grid, /-r2c1$/);
            await page.keyboard.press('Escape');
            await expect(field).toHaveCount(0);
            await expect(at(2, 1)).toHaveText(trader);
        });

        test('ADR-0010/ED-22: on a Sheet, a double click on another cell commits the edit and opens that cell\'s text', async ({ page }) => {
            await openSheet(page);
            const grid = sheet(page);
            await pressCell(grid, 'C4');
            await page.keyboard.type('99');
            await expect(editor(grid)).toHaveValue('99');
            await setRoundTrip(rtt);

            await expect(cell(grid, 'B2')).toBeVisible();
            await cell(grid, 'B2').dblclick({ force: true });

            await expect(cell(grid, 'C4')).toHaveText('99');
            await expect(editor(grid)).toHaveValue('12');
            await expect(editor(grid)).toBeFocused();
            await expectFocusAt(grid, 'B2');
            await page.keyboard.press('Escape');
            await expect(editor(grid)).toHaveCount(0);
            await expect(cell(grid, 'B2')).toHaveText('12');
        });
    });
}
