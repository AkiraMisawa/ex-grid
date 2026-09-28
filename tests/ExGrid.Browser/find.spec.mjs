import { test, expect, watchNextKey, keySeenUntouched } from './fixtures.mjs';

// Find with real keys (ADR-0055): Ctrl+F on a focused grid opens the grid's own panel — never
// the browser's find bar, which sees only the painted rows — the keys typed straight after it
// land in the field, Enter steps through every row including the unpainted ones, and Escape
// hands the keyboard back. /features is bound to GridSource.From, the reference search. Run once
// per Chrome: the built-in panel and ExGrid.MudBlazor's must give identical outcomes (WR-5).
//
// Its Trader column (1) repeats every three rows through four names: rows 3-5 are Novak, then
// rows 15-17, and so on to 397-399. Notional (2) is unique per row.

function grid(page) {
    return page.locator('.ex-grid').first();
}

async function focusedCell(page) {
    return grid(page).getAttribute('aria-activedescendant');
}

for (const chrome of ['builtin', 'mud']) {
    test.describe(`under the ${chrome} Chrome`, () => {
        test.beforeEach(async ({ page }) => {
            await page.goto(`/features?chrome=${chrome}`);
            await expect(grid(page).locator('.ex-row').first()).toBeVisible();
            await grid(page).locator("[id$='r0c0']").click({ force: true });
        });

        test('Ctrl+F opens the grid\'s panel, the keys typed after it land in its field, and Enter steps (FD-1/FD-3/FD-5, ADR-0055)', async ({ page }) => {
            await watchNextKey(page, ['f', 'F']);
            // Typed together: the panel takes DOM focus a render later, and the letters are
            // held until it has (ADR-0010/0039).
            await page.keyboard.press('ControlOrMeta+f');
            await page.keyboard.type('novak');
            expect(await keySeenUntouched(page), 'the grid took Ctrl+F').not.toBe(true);

            const panel = grid(page).locator('.ex-popover-find');
            await expect(panel).toBeVisible();
            const field = panel.locator('input').first();
            await expect(field).toBeFocused();
            await expect(field).toHaveValue('novak');

            await page.keyboard.press('Enter');
            await expect.poll(() => focusedCell(page)).toMatch(/r3c1$/);
            await page.keyboard.press('Enter');
            await expect.poll(() => focusedCell(page)).toMatch(/r4c1$/);
            await page.keyboard.press('Shift+Enter');
            await expect.poll(() => focusedCell(page)).toMatch(/r3c1$/);
            // The panel stands through the steps, and the field keeps the keyboard.
            await expect(field).toBeFocused();
        });

        test('a capital and a Shift+Enter typed straight after Ctrl+F keep their meaning (ADR-0055/0010, ED-22)', async ({ page }) => {
            // All of it before the panel can have taken DOM focus on a circuit: the keys are
            // held and handed to the field. A Shift pressed for the capital, and for the
            // Shift+Enter, is a keydown of its own and must not stop the replay there. Backward
            // from the first cell wraps to the last Novak.
            await page.keyboard.press('ControlOrMeta+f');
            await page.keyboard.type('Novak');
            await page.keyboard.press('Shift+Enter');

            await expect.poll(() => focusedCell(page)).toMatch(/r399c1$/);
            await expect(grid(page).locator('.ex-popover-find input').first()).toHaveValue('Novak');
        });

        test('a match beyond the painted rows is reached and revealed (FD-5, ADR-0055)', async ({ page }) => {
            // Row 350's notional: 1,000,000 + 350 × 12,345.67, shown without separators.
            await page.keyboard.press('ControlOrMeta+f');
            const field = grid(page).locator('.ex-popover-find input').first();
            await expect(field).toBeFocused();
            await page.keyboard.type('5320984.5');
            await page.keyboard.press('Enter');

            await expect.poll(() => focusedCell(page)).toMatch(/r350c2$/);
            await expect(grid(page).locator("[id$='r350c2']")).toBeInViewport();
        });

        test('with a range selected, Find searches it and the selection stands (FD-4/FD-5, ADR-0055)', async ({ page }) => {
            await grid(page).locator("[id$='r0c1']").click({ force: true });
            await grid(page).locator("[id$='r8c1']").click({ force: true, modifiers: ['Shift'] });

            await page.keyboard.press('ControlOrMeta+f');
            await expect(grid(page).locator('.ex-popover-find input').first()).toBeFocused();
            await page.keyboard.type('novak');
            for (const row of [3, 4, 5, 3]) {
                await page.keyboard.press('Enter');
                await expect.poll(() => focusedCell(page)).toMatch(new RegExp(`r${row}c1$`));
            }
            // The selection is the nine cells it was: the Focus moved inside it.
            await expect(grid(page).locator('.ex-range')).toHaveCount(1);
        });

        test('nothing found is said, in a live region (FD-6, ADR-0055)', async ({ page }) => {
            await page.keyboard.press('ControlOrMeta+f');
            await expect(grid(page).locator('.ex-popover-find input').first()).toBeFocused();
            await page.keyboard.type('no such trader');
            await page.keyboard.press('Enter');

            const outcome = grid(page).locator('.ex-popover-find [role=status]');
            await expect(outcome).toHaveText('No match');
            await expect(page.locator('#find-refused-status')).toContainText('NotFound');
        });

        test('Ctrl+F in the find field selects its text, and the browser\'s find stays shut (FD-1, ADR-0055)', async ({ page }) => {
            await page.keyboard.press('ControlOrMeta+f');
            const field = grid(page).locator('.ex-popover-find input').first();
            await expect(field).toBeFocused();
            await page.keyboard.type('novak');

            await watchNextKey(page, ['f', 'F']);
            await page.keyboard.press('ControlOrMeta+f');

            expect(await keySeenUntouched(page), 'the grid took Ctrl+F in its own field').not.toBe(true);
            await expect(field).toBeFocused();
            await expect.poll(() => field.evaluate((el) => [el.selectionStart, el.selectionEnd])).toEqual([0, 5]);
        });

        test('Ctrl+F in a column\'s popover opens Find in its place (FD-1, ADR-0055)', async ({ page }) => {
            await page.keyboard.press('Alt+ArrowDown');
            const menu = grid(page).locator('.ex-popover [role=menu]').first();
            await expect(menu).toBeVisible();

            await watchNextKey(page, ['f', 'F']);
            await page.keyboard.press('ControlOrMeta+f');

            expect(await keySeenUntouched(page), 'the grid took Ctrl+F in its own popover').not.toBe(true);
            await expect(grid(page).locator('.ex-popover')).toHaveCount(1);
            await expect(grid(page).locator('.ex-popover-find input').first()).toBeFocused();
        });

        test('Ctrl+F in the Context Menu, and in the filter\'s text field, opens Find in its place (FD-1, ADR-0055)', async ({ page }) => {
            await page.keyboard.press('Shift+F10');
            await expect(grid(page).locator('.ex-popover[role=menu]')).toBeVisible();
            await page.keyboard.press('ControlOrMeta+f');
            await expect(grid(page).locator('.ex-popover')).toHaveCount(1);
            await expect(grid(page).locator('.ex-popover-find input').first()).toBeFocused();
            await page.keyboard.press('Escape');
            await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
            await expect(grid(page)).toBeFocused();

            // Book's filter is a value list with a search box: E puts the keyboard in it.
            await page.keyboard.press('Alt+ArrowDown');
            await expect(grid(page).locator('.ex-popover [role=menu]').first()).toBeVisible();
            await page.keyboard.press('e');
            const search = grid(page).locator('.ex-popover-filter input').first();
            await expect(search).toBeFocused();
            await watchNextKey(page, ['f', 'F']);
            await page.keyboard.press('ControlOrMeta+f');
            expect(await keySeenUntouched(page), 'the grid took Ctrl+F in the filter\'s field').not.toBe(true);
            await expect(grid(page).locator('.ex-popover')).toHaveCount(1);
            await expect(grid(page).locator('.ex-popover-find input').first()).toBeFocused();
        });

        test('a Ctrl+F typed straight after Alt+Down opens Find, and the letters after it land in its field (FD-1, ED-22, ADR-0055)', async ({ page }) => {
            // Typed together: on a circuit the column's popover takes DOM focus a round trip
            // later, so the Ctrl+F and the letters are held, and the Ctrl+F must reach the core
            // rather than the menu, which would take a letter as a command.
            await page.keyboard.press('Alt+ArrowDown');
            await page.keyboard.press('ControlOrMeta+f');
            await page.keyboard.type('Osei');

            const field = grid(page).locator('.ex-popover-find input').first();
            await expect(field).toBeFocused();
            await expect(field).toHaveValue('Osei');
            await expect(grid(page).locator('.ex-popover')).toHaveCount(1);
        });

        test('Escape closes the panel and hands the keyboard back to the grid (FD-3, ADR-0055)', async ({ page }) => {
            await page.keyboard.press('ControlOrMeta+f');
            await expect(grid(page).locator('.ex-popover-find input').first()).toBeFocused();
            await page.keyboard.press('Escape');

            await expect(grid(page).locator('.ex-popover-find')).toHaveCount(0);
            await expect(grid(page)).toBeFocused();
            await page.keyboard.press('ArrowDown');
            await expect.poll(() => focusedCell(page)).toMatch(/r1c0$/);
        });
    });
}

test('Tab stays inside the find panel (FD-8, ADR-0055/0039)', async ({ page }) => {
    await page.goto('/features');
    await expect(grid(page).locator('.ex-row').first()).toBeVisible();
    await grid(page).locator("[id$='r0c0']").click({ force: true });
    await page.keyboard.press('ControlOrMeta+f');
    const panel = grid(page).locator('.ex-popover-find');
    await expect(panel.locator('input').first()).toBeFocused();

    for (let i = 0; i < 8; i++) {
        await page.keyboard.press('Tab');
        const inside = await panel.evaluate((el) => el.contains(document.activeElement));
        expect(inside, `Tab ${i + 1} stayed inside the panel`).toBe(true);
    }
});

test('the find panel stands inside the grid\'s box (FD-8, ADR-0055/0040)', async ({ page }) => {
    await page.goto('/features');
    await expect(grid(page).locator('.ex-row').first()).toBeVisible();
    await grid(page).locator("[id$='r0c0']").click({ force: true });
    await page.keyboard.press('ControlOrMeta+f');
    const panel = grid(page).locator('.ex-popover-find');
    await expect(panel).toBeVisible();

    const [inner, outer] = await Promise.all([panel.boundingBox(), grid(page).boundingBox()]);
    expect(inner.x).toBeGreaterThanOrEqual(outer.x);
    expect(inner.y).toBeGreaterThanOrEqual(outer.y);
    expect(inner.x + inner.width).toBeLessThanOrEqual(outer.x + outer.width + 0.5);
    expect(inner.y + inner.height).toBeLessThanOrEqual(outer.y + outer.height + 0.5);
    // One per instance: the second grid on the page opened nothing.
    await expect(page.locator('.ex-popover-find')).toHaveCount(1);
});

test('a grid with nothing to search takes Ctrl+F and opens nothing (FD-1/FD-2, ADR-0055)', async ({ page }) => {
    await page.goto('/cells');
    const cells = page.locator('.ex-grid').first();
    await expect(cells.locator('.ex-row').first()).toBeVisible();
    await cells.locator("[id$='r0c0']").click({ force: true });

    await watchNextKey(page, ['f', 'F']);
    await page.keyboard.press('ControlOrMeta+f');
    expect(await keySeenUntouched(page), 'the grid took Ctrl+F').not.toBe(true);
    await expect(cells.locator('.ex-popover')).toHaveCount(0);
    // The key after it is not held behind a panel that never comes.
    await page.keyboard.press('ArrowDown');
    await expect.poll(() => cells.getAttribute('aria-activedescendant')).toMatch(/r1c0$/);
});

test('in the Cell Editor Ctrl+F does nothing (FD-1, ADR-0055)', async ({ page }) => {
    await page.goto('/features');
    await expect(grid(page).locator('.ex-row').first()).toBeVisible();
    await grid(page).locator("[id$='r0c1']").click({ force: true });
    await page.keyboard.type('Ab');
    await page.keyboard.press('ControlOrMeta+f');

    await expect(grid(page).locator('.ex-popover-find')).toHaveCount(0);
    await expect(grid(page).locator('input.ex-editor')).toHaveValue('Ab');
});
