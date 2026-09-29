import { test, expect, setRoundTrip } from './fixtures.mjs';
import {
    sheet, cell, clickCell, clickBarEnd, editor, bar, nameBox, expectFocusAt, enter, candidates, typeSteadily,
    expectCovers,
} from './sheet-helpers.mjs';

// Two ExSheets on /sheets (ADR-0018, SH-13, DC-25, ticket 18's second criterion): keys,
// popovers, undo and the Formula Bar stay with the Sheet that has the keyboard. Each Sheet
// names itself in A1 and holds B1 and C1 =B1*2, so anything that crossed would show. Both read
// the page's positions as the Linked Table Positions, and beneath each is a grid of them that the
// page outlines from that Sheet's word alone (SH-31).

// Tall enough that every Sheet on the page, Formula Bar to horizontal scrollbar, is inside the
// window: a pointer below the window's edge reaches nothing, and the edge band sits there.
test.use({ viewport: { width: 1280, height: 1000 } });

test.beforeEach(async ({ page, context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    await page.goto('/sheets');
    // A WebAssembly page boots the runtime on every navigation, which can take longer than an
    // assertion's default wait on a loaded machine.
    await expect(page.locator('#demo-interactive')).toBeAttached({ timeout: 30_000 });
    await expect(cell(sheet(page, 0), 'A1')).toHaveText('Left');
    await expect(cell(sheet(page, 1), 'A1')).toHaveText('Right');
});

test('SH-13/DC-25: typing and Formulas stay in the Sheet that has the keyboard', async ({ page }) => {
    const left = sheet(page, 0);
    const right = sheet(page, 1);
    await enter(page, left, 'B1', '11');
    await expect(cell(left, 'C1')).toHaveText('22');
    await expect(cell(right, 'B1')).toHaveText('20');
    await expect(cell(right, 'C1')).toHaveText('40');
    await expect(right.locator('.ex-editor input, input.ex-editor').first()).not.toBeFocused();

    await enter(page, right, 'B1', '21');
    await expect(cell(right, 'C1')).toHaveText('42');
    await expect(cell(left, 'C1')).toHaveText('22');
    // Each Name Box names its own Focus.
    await expect(nameBox(left)).toHaveValue('B2');
    await expect(nameBox(right)).toHaveValue('B2');
    await clickCell(right, 'C1');
    await expect(nameBox(right)).toHaveValue('C1');
    await expect(nameBox(left)).toHaveValue('B2');
    await expect(bar(right)).toHaveValue('=B1*2');
    await expect(bar(left)).toHaveValue('');
});

test('SH-13: each Sheet keeps its own undo stack', async ({ page }) => {
    const left = sheet(page, 0);
    const right = sheet(page, 1);
    await enter(page, left, 'B1', '11');
    await enter(page, right, 'B1', '21');
    // Ctrl+Z in the right Sheet undoes the right Sheet's step only.
    await page.keyboard.press('ControlOrMeta+Z');
    await expect(cell(right, 'B1')).toHaveText('20');
    await expect(cell(left, 'B1')).toHaveText('11');
    // A second Ctrl+Z there has nothing left to undo, and the left Sheet still holds its edit.
    await page.keyboard.press('ControlOrMeta+Z');
    await expect(cell(right, 'B1')).toHaveText('20');
    await expect(cell(left, 'B1')).toHaveText('11');
    // In the left Sheet, its own step.
    await clickCell(left, 'D4');
    await page.keyboard.press('ControlOrMeta+Z');
    await expect(cell(left, 'B1')).toHaveText('10');
    await page.keyboard.press('ControlOrMeta+Y');
    await expect(cell(left, 'B1')).toHaveText('11');
    await expect(cell(right, 'B1')).toHaveText('20');
});

test('ADR-0018: a completion list, a pointing outline and the Formula Bar belong to one Sheet', async ({ page }) => {
    const left = sheet(page, 0);
    const right = sheet(page, 1);
    await clickCell(left, 'D1');
    await page.keyboard.type('=SU');
    await expect(candidates(left).first()).toHaveText('SUM');
    await expect(right.locator('.ex-completion')).toHaveCount(0);
    // The editor's text shows in its own bar only.
    await expect(bar(left)).toHaveValue('=SU');
    await expect(bar(right)).toHaveValue('');
    await page.keyboard.press('Escape');
    await page.keyboard.press('Escape');
    // The cancel hands the keyboard back to the left root a round trip later; a press on the
    // other Sheet before that is the race the last test in this file pins.
    await expect(editor(left)).toHaveCount(0);
    await expect(left).toBeFocused();

    // Pointing in the right Sheet paints its outline there only.
    await clickCell(right, 'D1');
    await expectFocusAt(right, 'D1');
    await expect(right).toBeFocused();
    await page.keyboard.type('=');
    await page.keyboard.press('ArrowLeft');
    await expect(editor(right)).toHaveValue('=C1');
    await expect(right.locator('.ex-point')).toHaveCount(1);
    await expect(left.locator('.ex-point')).toHaveCount(0);
    await page.keyboard.press('Enter');
    await expect(cell(right, 'D1')).toHaveText('40');
    await expect(cell(left, 'D1')).toHaveText('');

    // Typing into the left Sheet's Formula Bar edits the left Sheet only.
    await clickCell(left, 'E1');
    await clickBarEnd(left);
    await typeSteadily(page, bar(left), '=C1+1');
    await expect(editor(left)).toHaveValue('=C1+1');
    await expect(editor(right)).toHaveCount(0);
    await page.keyboard.press('Enter');
    await expect(cell(left, 'E1')).toHaveText('21');
    await expect(cell(right, 'E1')).toHaveText('');
    await expectFocusAt(left, 'E2');
});

test('ADR-0018: a Context Menu opens in the Sheet it was asked of, and its command acts there', async ({ page }) => {
    const left = sheet(page, 0);
    const right = sheet(page, 1);
    await cell(right, 'A1').click({ force: true, button: 'right' });
    await expect(right.locator('.ex-popover')).toHaveCount(1);
    await expect(left.locator('.ex-popover')).toHaveCount(0);
    await page.getByRole('menuitem', { name: 'Insert rows above' }).click();
    await expect(cell(right, 'A2')).toHaveText('Right');
    await expect(cell(left, 'A1')).toHaveText('Left');
    await expect(cell(left, 'A2')).toHaveText('');
});

// Found by this suite on the Server host (2026-09-27): Escape cancels an edit, and the core
// hands DOM focus back to that grid's root a round trip later (ReclaimFocusAsync). A press on
// another Sheet inside that round trip gives the keyboard to the other Sheet — and the late
// focus call took it back, so the keys typed next went to the Sheet the user left. At 150 ms
// it happened every time. ADR-0018: keys must never cross instances. The hand-back now lands
// only while DOM focus is still inside that grid's root or on nothing (ADR-0021, added
// 2026-09-27).
test('ADR-0018: a Sheet the user has pressed keeps the keyboard when the other one finishes a cancel late', async ({ page }) => {
    const left = sheet(page, 0);
    const right = sheet(page, 1);
    await clickCell(left, 'D1');
    await page.keyboard.type('5');
    await expect(editor(left)).toHaveValue('5');
    await setRoundTrip(150);
    await page.keyboard.press('Escape');
    await clickCell(right, 'D1');
    await expectFocusAt(right, 'D1');
    // Every answer in flight has landed.
    await page.waitForTimeout(1000);
    await expect(right).toBeFocused();
    await page.keyboard.type('7');
    await page.keyboard.press('Enter');
    await expect(cell(right, 'D1')).toHaveText('7');
    await expect(cell(left, 'D1')).toHaveText('');
});

// ExSheet never reaches another instance (ADR-0018, ADR-0049): it tells its own Consumer which
// Linked Table columns its Formula reads, and the page passes each Sheet's word to the grid beneath
// that Sheet only (SH-31, DC-25, ADR-0057).
test('SH-31/DC-25: each Sheet outlines a Linked Table\'s column only in the grid its page wired to it', async ({ page }) => {
    const left = sheet(page, 0);
    const right = sheet(page, 1);
    const leftPositions = page.locator('#sheet-left-positions .ex-grid');
    const rightPositions = page.locator('#sheet-right-positions .ex-grid');

    await clickCell(left, 'D1');
    await page.keyboard.type('=SUM(Positions[PV])');
    await expect(editor(left)).toHaveValue('=SUM(Positions[PV])');
    const leftPv = leftPositions.locator('.ex-reference-outline');
    await expect(leftPv).toHaveCount(1);
    await expect(leftPv).toHaveClass(/\bex-reference-1\b/);
    await expectCovers(leftPv, leftPositions, 'C1', 'C3');
    await expect(rightPositions.locator('.ex-reference-outline')).toHaveCount(0);
    await page.keyboard.press('Enter');
    await expect(editor(left)).toHaveCount(0);
    await expect(leftPositions.locator('.ex-reference-outline')).toHaveCount(0);
    await expectFocusAt(left, 'D2');

    await clickCell(right, 'D1');
    await expectFocusAt(right, 'D1');
    await expect(right).toBeFocused();
    await page.keyboard.type('=B1+COUNTA(Positions[Id])');
    await expect(editor(right)).toHaveValue('=B1+COUNTA(Positions[Id])');
    const rightId = rightPositions.locator('.ex-reference-outline');
    await expect(rightId).toHaveCount(1);
    await expect(rightId).toHaveClass(/\bex-reference-2\b/);
    await expectCovers(rightId, rightPositions, 'A1', 'A3');
    await expect(leftPositions.locator('.ex-reference-outline')).toHaveCount(0);
    await page.keyboard.press('Escape');
    await expect(editor(right)).toHaveCount(0);
    await expect(rightPositions.locator('.ex-reference-outline')).toHaveCount(0);
    await expect(leftPositions.locator('.ex-reference-outline')).toHaveCount(0);
});
