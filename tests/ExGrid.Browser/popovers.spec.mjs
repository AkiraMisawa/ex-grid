import { test, expect } from './fixtures.mjs';

// The popovers' behaviour on /features, run once per Chrome (WR-5, FN-17): the built-in
// Chrome and ExGrid.MudBlazor's (/features?chrome=mud) must give identical outcomes,
// because what a popover's contents do is the core's to decide (ADR-0010/0039). The
// dismissals (KB-17), the keyboard in and out (KB-28..32), the roles and names (A11Y-19),
// the scroll container not clipping (UX-11) and the Context Menu (CTX-1..4). The
// filter panel is the built-in one under both until the Wrapper fills it.

const CHROMES = ['builtin', 'mud'];

function grid(page) {
    return page.locator('.ex-grid').first();
}

async function clickCell(page, row, column) {
    // Cells are pointer-events: none by design — the Viewport is the delegated target
    // (ADR-0004) — so Playwright's actionability check is bypassed and the browser
    // hit-tests the click through to the Viewport, exactly as a user's does.
    await grid(page).locator(`[id$='r${row}c${column}']`).click({ force: true });
}

// Where DOM focus is: inside which popover, on what — or on the root. A popover takes the
// keyboard when it opens and gives it back when it closes (ADR-0039).
async function activeIsInPopover(page) {
    return page.evaluate(() => {
        const active = document.activeElement;
        const popover = active?.closest('.ex-popover');
        return popover ? { role: popover.getAttribute('role'), tag: active.tagName, text: active.textContent } : null;
    });
}

async function activeIsRoot(page) {
    return page.evaluate(() => document.activeElement === document.querySelector('.ex-grid'));
}

async function enabledMenuItems(page) {
    return grid(page).locator('.ex-popover[role=menu] button[role=menuitem]:not([disabled])').allTextContents();
}

async function activeText(page) {
    return page.evaluate(() => document.activeElement?.textContent?.trim() ?? null);
}

// Whether DOM focus is on the condition form's operator: a native select in the built-in
// panel, a MudSelect's combobox in the Wrapper's.
async function activeIsOperator(page) {
    return page.evaluate(() => {
        const active = document.activeElement;
        return !!active?.closest('.ex-popover[role=dialog]')
            && (active.tagName === 'SELECT' || active.getAttribute('role') === 'combobox');
    });
}

async function openNotionalPanel(page) {
    await clickCell(page, 1, 2);
    await page.keyboard.press('Alt+ArrowDown');
    await expect.poll(() => activeText(page)).toBe('Sort ascending');
    await grid(page).locator('.ex-popover button[role=menuitem]', { hasText: 'Filter' }).click();
    const panel = grid(page).locator('.ex-popover[role=dialog]');
    await expect(panel).toBeVisible();
    await expect.poll(() => activeIsOperator(page)).toBe(true);
    return panel;
}

// Each Chrome's own controls for the same choice. The Wrapper's operator is a MudSelect,
// whose list MudBlazor draws outside the grid's root — an Inner Popup (ADR-0039) — and
// whose word for "greater than" is MudBlazor's.
const CONDITION = {
    builtin: {
        choose: (page, panel) => panel.locator('select').selectOption('GreaterThan'),
        operand: (panel) => panel.locator('input:not([type])'),
        apply: (panel) => panel.locator('button', { hasText: 'OK' }),
    },
    mud: {
        choose: async (page, panel) => {
            await panel.getByRole('combobox', { name: 'Operator' }).click();
            await page.locator('.mud-popover-open .mud-list-item', { hasText: /^>$/ }).click();
            await expect(page.locator('.mud-popover-open .mud-list-item')).toHaveCount(0);
        },
        operand: (panel) => panel.locator('.mud-ex-grid-filter-operand input'),
        apply: (panel) => panel.locator('.mud-ex-grid-filter-apply'),
    },
};

// The rows left after a Notional > 3,000,000 condition (the page's notionals run from
// 1,000,000 up), applied by `apply`. The source re-answers after the panel closes, so the
// count is waited for, not read at once.
async function rowCountAfterFilter(page, chrome, apply) {
    const before = await grid(page).getAttribute('aria-rowcount');
    const panel = await openNotionalPanel(page);
    await CONDITION[chrome].choose(page, panel);
    await CONDITION[chrome].operand(panel).fill('3000000');
    await apply(panel);
    await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
    await expect.poll(() => activeIsRoot(page)).toBe(true);
    await expect.poll(() => grid(page).getAttribute('aria-rowcount'), 'the condition filters something out')
        .not.toBe(before);
    return grid(page).getAttribute('aria-rowcount');
}

for (const chrome of CHROMES) {
    test.describe(`under the ${chrome} Chrome`, () => {
        test.beforeEach(async ({ page, context }) => {
            await context.grantPermissions(['clipboard-read', 'clipboard-write']);
            await page.goto(`/features?chrome=${chrome}`);
            await expect(grid(page).locator('.ex-row').first()).toBeVisible();
        });

        test('a secondary click opens the grid\'s menu, not the browser\'s (CTX-1/CTX-5, ADR-0036)', async ({ page }) => {
            await clickCell(page, 0, 1);
            const prevented = page.evaluate(() => new Promise((resolve) => {
                window.addEventListener('contextmenu', (e) => resolve(e.defaultPrevented), { once: true });
            }));

            await grid(page).locator("[id$='r0c1']").click({ button: 'right', force: true });

            // The browser's own menu is suppressed declaratively on the element — no listener
            // the grid installed, so the allowlist stays at four (ADR-0021).
            expect(await prevented).toBe(true);
            const items = grid(page).locator('[role=menu] button[role=menuitem]');
            await expect(items).toHaveText(['Copy', 'Copy with headers', 'open-trade']);
        });

        test('a secondary click outside the selection moves it first (CTX-1, ADR-0036)', async ({ page }) => {
            await clickCell(page, 0, 1);
            const before = await grid(page).getAttribute('aria-activedescendant');

            await grid(page).locator("[id$='r3c2']").click({ button: 'right', force: true });

            // What a command will act on is what the user can see.
            await expect(grid(page)).not.toHaveAttribute('aria-activedescendant', before ?? '');
            await expect(grid(page)).toHaveAttribute('aria-activedescendant', /r3c2$/);
        });

        test('a Consumer command receives the clicked row and the selection (CTX-3, ADR-0036)', async ({ page }) => {
            await clickCell(page, 2, 1);
            await page.keyboard.press('Shift+ArrowDown');

            await grid(page).locator("[id$='r2c1']").click({ button: 'right', force: true });
            await grid(page).locator('[role=menu] button[role=menuitem]').last().click();

            await expect(page.locator('#context-status')).toContainText('2 cells selected');
        });

        test('the ContextMenu key opens it on the Focus (CTX-4, ADR-0036)', async ({ page }) => {
            await clickCell(page, 1, 1);

            await page.keyboard.press('ContextMenu');

            await expect(grid(page).locator('[role=menu] button[role=menuitem]').first()).toHaveText('Copy');
            // Escape peels the menu before it leaves the grid (ADR-0012's layering).
            await page.keyboard.press('Escape');
            await expect(grid(page).locator('[role=menu]')).toHaveCount(0);
        });

        test('popovers are not clipped by the scroll container and stay per instance (UX-11)', async ({ page }) => {
            // The rightmost column's menu of the first grid.
            const buttons = grid(page).locator('.ex-menu-button');
            await buttons.last().click();

            const popover = page.locator('.ex-popover');
            await expect(popover).toHaveCount(1);
            await expect(popover).toBeVisible();
            // Escape dismisses it and the grid keeps the keyboard; a second press on the
            // button would toggle it the same way.
            await popover.press('Escape');
            await expect(popover).toHaveCount(0);
            await buttons.last().click();
            await expect(page.locator('.ex-popover')).toHaveCount(1);
            const box = await popover.boundingBox();
            const gridBox = await grid(page).boundingBox();
            // Fully visible inside the viewport, not cut off at the scroller's edge.
            expect(box.x + box.width).toBeLessThanOrEqual(gridBox.x + gridBox.width + 1);
            // And it belongs to the first grid alone: the second grid shows none.
            expect(await page.locator('.ex-grid').nth(1).locator('.ex-popover').count()).toBe(0);
        });

        test('Alt+↓ opens the Focus column\'s menu, and the menu takes the keyboard (KB-28/KB-29, ADR-0039)', async ({ page }) => {
            await clickCell(page, 1, 0);

            await page.keyboard.press('Alt+ArrowDown');

            const menu = grid(page).locator('.ex-popover[role=menu]');
            await expect(menu).toBeVisible();
            await expect(menu).toHaveAttribute('aria-label', 'Book');
            await expect.poll(() => activeIsInPopover(page)).toEqual({ role: 'menu', tag: 'BUTTON', text: 'Sort ascending' });
        });

        test('every popover takes the keyboard when it opens, by pointer or by key (KB-29, ADR-0039)', async ({ page }) => {
            // The column menu, by its ▾.
            await grid(page).locator('.ex-menu-button').first().click();
            await expect.poll(() => activeIsInPopover(page)).toMatchObject({ role: 'menu', tag: 'BUTTON' });

            // The filter panel, from the menu: its first control once the value list has landed.
            await grid(page).locator('.ex-popover button[role=menuitem]', { hasText: 'Filter' }).click();
            await expect(grid(page).locator('.ex-popover[role=dialog]')).toBeVisible();
            await expect.poll(() => activeIsInPopover(page)).toMatchObject({ role: 'dialog', tag: 'INPUT' });

            // Escape from inside closes it, and the keyboard is the grid's again.
            await page.keyboard.press('Escape');
            await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
            await expect.poll(() => activeIsRoot(page)).toBe(true);

            // The Context Menu, by key.
            await clickCell(page, 1, 1);
            await page.keyboard.press('Shift+F10');
            await expect.poll(() => activeIsInPopover(page)).toEqual({ role: 'menu', tag: 'BUTTON', text: 'Copy' });
        });

        test('however a popover closes, the keyboard is back on the grid and the arrows move the Focus (KB-32, ADR-0039)', async ({ page }) => {
            const focusAfterDown = async () => {
                const before = await grid(page).getAttribute('aria-activedescendant');
                await page.keyboard.press('ArrowDown');
                await expect.poll(() => grid(page).getAttribute('aria-activedescendant')).not.toBe(before);
            };

            // A command run.
            await clickCell(page, 1, 0);
            await page.keyboard.press('Alt+ArrowDown');
            await grid(page).locator('.ex-popover button[role=menuitem]', { hasText: 'Sort ascending' }).click();
            await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
            await expect.poll(() => activeIsRoot(page)).toBe(true);
            await clickCell(page, 1, 0);
            await focusAfterDown();

            // The panel's Cancel.
            await page.keyboard.press('Alt+ArrowDown');
            await grid(page).locator('.ex-popover button[role=menuitem]', { hasText: 'Filter' }).click();
            await grid(page).locator('.ex-popover[role=dialog] button', { hasText: 'Cancel' }).click();
            await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
            await expect.poll(() => activeIsRoot(page)).toBe(true);
            await focusAfterDown();

            // The ▾ pressed again.
            const button = grid(page).locator('.ex-menu-button').first();
            await button.click();
            await button.click();
            await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
            await expect.poll(() => activeIsRoot(page)).toBe(true);

            // Escape from inside a menu.
            await page.keyboard.press('Alt+ArrowDown');
            await expect.poll(() => activeIsInPopover(page)).toMatchObject({ role: 'menu' });
            await page.keyboard.press('Escape');
            await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
            await expect.poll(() => activeIsRoot(page)).toBe(true);
            await focusAfterDown();
        });

        test('in a menu the arrows, Home and End move among the enabled items and wrap (KB-30, ADR-0039)', async ({ page }) => {
            await clickCell(page, 1, 0);
            await page.keyboard.press('Alt+ArrowDown');
            const items = await enabledMenuItems(page);
            expect(items.length, 'a menu with something to move among').toBeGreaterThan(2);
            await expect.poll(() => activeText(page)).toBe(items[0]);

            // Down through every enabled item, and round to the first again.
            for (const item of [...items.slice(1), items[0]]) {
                await page.keyboard.press('ArrowDown');
                await expect.poll(() => activeText(page)).toBe(item);
            }
            // Up from the first wraps to the last.
            await page.keyboard.press('ArrowUp');
            await expect.poll(() => activeText(page)).toBe(items[items.length - 1]);
            await page.keyboard.press('Home');
            await expect.poll(() => activeText(page)).toBe(items[0]);
            await page.keyboard.press('End');
            await expect.poll(() => activeText(page)).toBe(items[items.length - 1]);

            // None of it moved the page or the Focus underneath.
            expect(await page.evaluate(() => window.scrollY)).toBe(0);
            await expect(grid(page).locator('.ex-popover[role=menu]')).toBeVisible();
        });

        for (const key of ['Enter', ' ']) {
            test(`in a menu ${key === ' ' ? 'Space' : key} runs the item and closes it (KB-30, ADR-0039)`, async ({ page }) => {
                await clickCell(page, 1, 0);
                await page.keyboard.press('Alt+ArrowDown');
                await expect.poll(() => activeText(page)).toBe('Sort ascending');
                await page.keyboard.press('ArrowDown');
                await expect.poll(() => activeText(page)).toBe('Sort descending');

                await page.keyboard.press(key === ' ' ? 'Space' : key);

                await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
                await expect(grid(page).locator('.ex-header-cell[aria-sort=descending]')).toHaveCount(1);
                await expect.poll(() => activeIsRoot(page)).toBe(true);
            });
        }

        for (const key of ['Tab', 'Shift+Tab']) {
            test(`in a menu ${key} closes it as a Cancel (KB-30, ADR-0039)`, async ({ page }) => {
                await clickCell(page, 1, 0);
                await page.keyboard.press('Alt+ArrowDown');
                await page.keyboard.press('ArrowDown');
                await expect.poll(() => activeText(page)).toBe('Sort descending');

                await page.keyboard.press(key);

                await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
                await expect(grid(page).locator('.ex-header-cell[aria-sort=descending]')).toHaveCount(0);
                await expect.poll(() => activeIsRoot(page)).toBe(true);
            });
        }

        test('the Context Menu answers the same keys (KB-30, ADR-0039)', async ({ page }) => {
            await clickCell(page, 1, 1);
            await page.keyboard.press('Shift+F10');
            const items = await enabledMenuItems(page);
            await expect.poll(() => activeText(page)).toBe(items[0]);
            await page.keyboard.press('End');
            await expect.poll(() => activeText(page)).toBe(items[items.length - 1]);
            await page.keyboard.press('ArrowDown');
            await expect.poll(() => activeText(page)).toBe(items[0]);
            await page.keyboard.press('Tab');
            await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
            await expect.poll(() => activeIsRoot(page)).toBe(true);
        });

        test('in the filter panel Tab and Shift+Tab wrap inside it (KB-31, ADR-0039)', async ({ page }) => {
            await openNotionalPanel(page);

            // Operator, value, Apply (while it is available), Cancel, Clear — and round to
            // the operator: twice over, and DOM focus is never anywhere but the panel.
            let reachedOperator = 0;
            for (let i = 0; i < 10; i++) {
                await page.keyboard.press('Tab');
                await expect.poll(() => activeIsInPopover(page)).toMatchObject({ role: 'dialog' });
                if (await activeIsOperator(page))
                    reachedOperator++;
            }
            expect(reachedOperator, 'the wrap reached the operator again').toBeGreaterThan(0);

            // Shift+Tab off the operator goes to the last control, not out of the panel.
            while (!(await activeIsOperator(page)))
                await page.keyboard.press('Tab');
            await page.keyboard.press('Shift+Tab');
            await expect.poll(() => activeText(page)).toBe('Clear');
            await expect(grid(page).locator('.ex-popover[role=dialog]')).toBeVisible();
        });

        test('Enter in the value field applies exactly what OK applies (KB-31, ADR-0039)', async ({ page }) => {
            const byOk = await rowCountAfterFilter(page, chrome, (panel) => CONDITION[chrome].apply(panel).click());

            await page.reload();
            await expect(grid(page).locator('.ex-row').first()).toBeVisible();
            const byEnter = await rowCountAfterFilter(page, chrome, (panel) => CONDITION[chrome].operand(panel).press('Enter'));
            expect(byEnter).toBe(byOk);
        });

        test('a pointer-down elsewhere in the instance dismisses a popover and keeps its own meaning (KB-17, ADR-0010/0039)', async ({ page }) => {
            await clickCell(page, 1, 0);
            await page.keyboard.press('Alt+ArrowDown');
            await expect(grid(page).locator('.ex-popover[role=menu]')).toBeVisible();

            // Well clear of the menu, which stands under the first column's header: a press
            // that lands on an item runs it, which is not this test.
            await clickCell(page, 4, 4);

            // The menu is gone without running anything, the press selected the cell it
            // landed on, and the keyboard is the grid's.
            await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
            await expect(grid(page)).toHaveAttribute('aria-activedescendant', /r4c4$/);
            await expect(grid(page).locator('.ex-header-cell[aria-sort=ascending]')).toHaveCount(0);
            await expect.poll(() => activeIsRoot(page)).toBe(true);
        });

        test("the menus are menus and the panel a dialog, a column's named by its header (A11Y-19, ADR-0039)", async ({ page }) => {
            await grid(page).locator('.ex-menu-button').first().click();
            const menu = grid(page).locator('.ex-popover');
            await expect(menu).toHaveAttribute('role', 'menu');
            await expect(menu).toHaveAttribute('aria-label', 'Book');
            await expect(menu.locator('[role=menuitem]').first()).toBeVisible();

            await menu.locator('[role=menuitem]', { hasText: 'Filter' }).click();
            const panel = grid(page).locator('.ex-popover');
            await expect(panel).toHaveAttribute('role', 'dialog');
            await expect(panel).toHaveAttribute('aria-label', 'Book');
            await page.keyboard.press('Escape');

            await clickCell(page, 1, 1);
            await page.keyboard.press('Shift+F10');
            const context = grid(page).locator('.ex-popover');
            await expect(context).toHaveAttribute('role', 'menu');
            expect(await context.getAttribute('aria-label')).toBeNull();
        });
    });
}
