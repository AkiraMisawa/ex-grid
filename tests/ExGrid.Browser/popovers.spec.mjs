import { test, expect, alterPage, twoFrames, holdAnswers, circuitQuiet, settled } from './fixtures.mjs';
import { expectKeyboardOn, keyboardCarrier, activeDescendant, expectActiveDescendant } from './keyboard.mjs';

// The popovers' behaviour on /features, run once per Chrome (WR-5, FN-17): the built-in
// Chrome and ExGrid.MudBlazor's (/features?chrome=mud) must give identical outcomes,
// because what a popover's contents do is the core's to decide (ADR-0010/0039). The
// dismissals (KB-17), the keyboard in and out (KB-28..32), the roles and names (A11Y-19),
// the scroll container not clipping (UX-11), the Context Menu (CTX-1..4), and the column's
// one popover — its commands above its filter, Excel's letters and Clear filter (FL-12,
// FL-15, FL-16, ADR-0044).

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

// Where DOM focus is: inside which popover, on what — or back on the grid, its Keyboard Field
// on this grid, which edits (expectKeyboardOn, ADR-0080). A popover takes the keyboard when it
// opens and gives it back when it closes (ADR-0039). A column's popover is a dialog holding a
// menu (ADR-0044): on a command the role is the menu's, on the filter the dialog's.
async function activeIsInPopover(page) {
    return page.evaluate(() => {
        const active = document.activeElement;
        const popover = active?.closest('.ex-popover');
        const within = active?.closest('[role=menu], [role=dialog]');
        return popover && within && popover.contains(within)
            ? { role: within.getAttribute('role'), tag: active.tagName, text: active.textContent }
            : null;
    });
}

async function enabledMenuItems(page) {
    const items = grid(page).locator('.ex-popover button[role=menuitem]:not([disabled])');
    // A menu whose commands are answered later, or drawn by a Wrapper, stands before its items do.
    await expect(items.first()).toBeVisible();
    return items.allTextContents();
}

async function activeText(page) {
    return page.evaluate(() => document.activeElement?.textContent?.trim() ?? null);
}

// Whether DOM focus is on the condition form's (first) operator: a native select in the
// built-in panel, and in the Wrapper's, where it is named Operator.
async function activeIsOperator(page) {
    return page.evaluate(() => {
        const active = document.activeElement;
        return !!active?.closest('.ex-popover[role=dialog]')
            && ((active.tagName === 'SELECT' && !active.classList.contains('ex-popover-second'))
                || (active.getAttribute('role') === 'combobox' && active.getAttribute('aria-label') === 'Operator'));
    });
}

// Notional's popover, and Tab from its commands into the filter below (ADR-0044).
async function openNotionalPanel(page) {
    await clickCell(page, 1, 2);
    await page.keyboard.press('Alt+ArrowDown');
    await expect.poll(() => activeText(page)).toBe('Sort ascending');
    const panel = grid(page).locator('.ex-popover[role=dialog]');
    await expect(panel).toBeVisible();
    await page.keyboard.press('Tab');
    await expect.poll(() => activeIsOperator(page)).toBe(true);
    return panel;
}

// Each Chrome's own controls for the same choice. The Wrapper's operator is a native select
// dressed as MudBlazor's, which draws no Inner Popup (ADR-0410, note of 2026-10-28).
const CONDITION = {
    builtin: {
        choose: (page, panel) => panel.locator('select').first().selectOption('GreaterThan'),
        operand: (panel) => panel.locator('input:not([type])'),
        apply: (panel) => panel.locator('button', { hasText: 'OK' }),
    },
    mud: {
        choose: (page, panel) => panel.getByRole('combobox', { name: 'Operator' }).selectOption('greaterThan'),
        operand: (panel) => panel.locator('.mud-ex-grid-filter-operand input'),
        apply: (panel) => panel.locator('.mud-ex-grid-filter-apply'),
    },
};

// Each Chrome's own controls over a value list: "(Select All)" is a button with a checkbox's
// role in the built-in panel and a tri-state MudCheckBox in the Wrapper's.
const VALUE_LIST = {
    builtin: {
        selectAll: (popover) => popover.locator('.ex-select-all'),
        ticked: (popover) => popover.locator('.ex-popover-list label input[type=checkbox]:checked'),
        search: (popover) => popover.locator('input[type=search]'),
        apply: (popover) => popover.locator('.ex-popover-actions button', { hasText: 'OK' }),
    },
    mud: {
        selectAll: (popover) => popover.locator('.mud-ex-grid-filter-all input'),
        ticked: (popover) => popover.locator('.mud-ex-grid-filter-value input:checked'),
        search: (popover) => popover.locator('.mud-ex-grid-filter-search input'),
        apply: (popover) => popover.locator('.mud-ex-grid-filter-apply'),
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
    await expectKeyboardOn(grid(page));
    await expect.poll(() => grid(page).getAttribute('aria-rowcount'), 'the condition filters something out')
        .not.toBe(before);
    return grid(page).getAttribute('aria-rowcount');
}

// Where each key typed from now lands, by its keydown's target, read on the document in the capture
// phase, before any listener of the page's can stop it: in one of the grid's popovers, in the grid, or
// elsewhere. Only keys typed, not the grid's replays of held keys. Answers a reader of what has landed.
async function watchWhereKeysLand(page) {
    await alterPage(page, () => {
        window.__keysLanded = [];
        const listener = (e) => {
            if (!e.isTrusted) {
                return;
            }
            const t = e.target instanceof Element ? e.target : null;
            window.__keysLanded.push({ key: e.key, where: t?.closest('.ex-popover') ? 'popover' : t?.closest('.ex-grid') ? 'grid' : 'page' });
        };
        document.addEventListener('keydown', listener, true);
        return () => {
            document.removeEventListener('keydown', listener, true);
            delete window.__keysLanded;
        };
    });
    return () => page.evaluate(() => window.__keysLanded);
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
            // On window, which outlives the page: alterPage takes the listener off as the test
            // ends, if the secondary click never reached it (ADR-0056).
            await alterPage(page, () => {
                let listener;
                window.__contextMenuPrevented = new Promise((resolve) => {
                    listener = (e) => resolve(e.defaultPrevented);
                    window.addEventListener('contextmenu', listener, { once: true });
                });
                return () => {
                    window.removeEventListener('contextmenu', listener);
                    delete window.__contextMenuPrevented;
                };
            });
            const prevented = page.evaluate(() => window.__contextMenuPrevented);

            await grid(page).locator("[id$='r0c1']").click({ button: 'right', force: true });

            // The browser's own menu is suppressed declaratively on the element — no listener
            // the grid installed, so the allowlist stays at four (ADR-0021).
            expect(await prevented).toBe(true);
            const items = grid(page).locator('[role=menu] button[role=menuitem]');
            await expect(items).toHaveText(['Copy', 'Copy with headers', 'open-trade']);
        });

        test('a secondary click outside the selection moves it first (CTX-1, ADR-0036)', async ({ page }) => {
            await clickCell(page, 0, 1);
            await expectActiveDescendant(grid(page), /r0c1$/);
            const before = await activeDescendant(grid(page));

            await grid(page).locator("[id$='r3c2']").click({ button: 'right', force: true });

            // What a command will act on is what the user can see.
            await expect(await keyboardCarrier(grid(page))).not.toHaveAttribute('aria-activedescendant', before ?? '');
            await expectActiveDescendant(grid(page), /r3c2$/);
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

        test('a popover taller than its grid stays inside the grid\'s box and scrolls (UX-11, ADR-0040)', async ({ page }) => {
            // The second grid is 140px tall: its column menu does not fit, and is bounded.
            const short = page.locator('.ex-grid').nth(1);
            await short.locator('.ex-menu-button').first().click();
            const popover = short.locator('.ex-popover');
            await expect(popover.locator('[role=menuitem]').first()).toBeVisible();

            const measured = await popover.evaluate((p) => {
                const r = p.getBoundingClientRect();
                return { bottom: r.bottom, gridBottom: p.closest('.ex-grid').getBoundingClientRect().bottom, scrolls: p.scrollHeight > p.clientHeight };
            });
            expect(measured.bottom).toBeLessThanOrEqual(measured.gridBottom + 0.5);
            expect(measured.scrolls, 'the premise: the menu is taller than the grid gives it').toBe(true);
            const last = popover.locator('[role=menuitem]').last();
            await last.scrollIntoViewIfNeeded();
            await expect(last).toBeInViewport();
        });

        test('every popover takes the keyboard when it opens, by pointer or by key (KB-29, ADR-0039)', async ({ page }) => {
            // The column menu, by its ▾.
            await grid(page).locator('.ex-menu-button').first().click();
            await expect.poll(() => activeIsInPopover(page)).toMatchObject({ role: 'menu', tag: 'BUTTON' });

            // The filter below, by E from the commands: its search box, once the value list
            // has landed (ADR-0044).
            await expect(grid(page).locator('.ex-popover[role=dialog] input[type=search], .ex-popover[role=dialog] .mud-ex-grid-filter-search input')).toBeVisible();
            await page.keyboard.press('e');
            await expect.poll(() => activeIsInPopover(page)).toMatchObject({ role: 'dialog', tag: 'INPUT' });

            // Escape from inside closes it, and the keyboard is the grid's again.
            await page.keyboard.press('Escape');
            await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
            await expectKeyboardOn(grid(page));

            // The Context Menu, by key.
            await clickCell(page, 1, 1);
            await page.keyboard.press('Shift+F10');
            await expect.poll(() => activeIsInPopover(page)).toEqual({ role: 'menu', tag: 'BUTTON', text: 'Copy' });
        });

        test('a grid that goes while keys wait for its popover leaves nothing running (ADR-0010, CON-2)', async ({ page }) => {
            await clickCell(page, 0, 1);
            await expectActiveDescendant(grid(page), /r0c1$/);
            // The popover opens and never takes DOM focus, so what Alt+↓ started keeps waiting
            // for it (ADR-0010) — as it does for a round trip on a circuit, or for the frame
            // after the popover took it. The grid goes in that wait.
            await alterPage(page, () => {
                const focus = HTMLElement.prototype.focus;
                HTMLElement.prototype.focus = function () { };
                return () => { HTMLElement.prototype.focus = focus; };
            });
            await page.keyboard.press('Alt+ArrowDown');
            await expect(grid(page).locator('.ex-popover')).toBeVisible();
            // Away inside the app, so the grid is disposed rather than unloaded with its document.
            await page.evaluate(() => window.Blazor.navigateTo('/'));
            await expect(page.locator('#demo-index')).toBeAttached();
            // The frames in which a wait that outlived its grid would look for it again.
            await twoFrames(page);
        });

        test('however a popover closes, the keyboard is back on the grid and the arrows move the Focus (KB-32, ADR-0039)', async ({ page }) => {
            const focusAfterDown = async () => {
                const before = await activeDescendant(grid(page));
                await page.keyboard.press('ArrowDown');
                await expect.poll(() => activeDescendant(grid(page))).not.toBe(before);
            };

            // A command run.
            await clickCell(page, 1, 0);
            await page.keyboard.press('Alt+ArrowDown');
            await grid(page).locator('.ex-popover button[role=menuitem]', { hasText: 'Sort ascending' }).click();
            await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
            await expectKeyboardOn(grid(page));
            await clickCell(page, 1, 0);
            await focusAfterDown();

            // The filter's Cancel.
            await page.keyboard.press('Alt+ArrowDown');
            await grid(page).locator('.ex-popover[role=dialog] button', { hasText: 'Cancel' }).click();
            await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
            await expectKeyboardOn(grid(page));
            await focusAfterDown();

            // The ▾ pressed again.
            const button = grid(page).locator('.ex-menu-button').first();
            await button.click();
            await button.click();
            await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
            await expectKeyboardOn(grid(page));

            // Escape from inside a menu.
            await page.keyboard.press('Alt+ArrowDown');
            await expect.poll(() => activeIsInPopover(page)).toMatchObject({ role: 'menu' });
            await page.keyboard.press('Escape');
            await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
            await expectKeyboardOn(grid(page));
            await focusAfterDown();
        });

        // Decided with the user on 2026-10-12 (ADR-0044's note of that day): the keys typed while the
        // keyboard is in flight back from a closing popover are held, and handed on in order to where it
        // lands, so the outcome is the same at any speed (the spine's sixth principle). On the Server host
        // at an 80 ms round trip the Razor grid's popover stood a round trip after the key or press that
        // closed it, and a key typed then was the popover's: ↓ moved in a menu about to go, and was lost.
        // The Script grid closes a popover in the browser (ADR-0440), so the keys after the closing one land
        // on the grid itself, with the host's answers held across them to show none is waited on.
        test('keys typed while Escape closes a menu are the grid\'s, in order: ↓, ↓ typed at once move the Focus (ADR-0044, 2026-10-12)', async ({ page }) => {
            await clickCell(page, 1, 0);
            await page.keyboard.press('Alt+ArrowDown');
            await expect.poll(() => activeIsInPopover(page)).toMatchObject({ role: 'menu' });
            // The menu opened and answered: nothing the opening waits on still holds the keys, so where
            // they land says what the close itself does. A key typed while an answer is awaited is held
            // in its place, and lands where DOM focus stood (ADR-0220).
            await settled(grid(page));
            const landed = await watchWhereKeysLand(page);
            // The host's answers are held across the keys: nothing the close waits on may come from it (ADR-0220).
            await holdAnswers(true);
            try {
                await page.keyboard.press('Escape');
                await page.keyboard.press('ArrowDown');
                await page.keyboard.press('ArrowDown');
                await holdAnswers(false);

                await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
                await circuitQuiet();
                await expectKeyboardOn(grid(page));
                await expectActiveDescendant(grid(page), /r3c0$/);
                await expect(grid(page).locator('.ex-header-cell[aria-sort=ascending], .ex-header-cell[aria-sort=descending]')).toHaveCount(0);
                expect(await landed()).toEqual([
                    { key: 'Escape', where: 'popover' }, { key: 'ArrowDown', where: 'grid' }, { key: 'ArrowDown', where: 'grid' }]);
            } finally {
                await holdAnswers(false);
            }
        });

        test('keys typed while Escape closes a filter panel from its search box are the grid\'s, in order: ↓, ↓ typed at once move the Focus (ADR-0044, 2026-10-12)', async ({ page }) => {
            await clickCell(page, 1, 0);
            await page.keyboard.press('Alt+ArrowDown');
            await expect.poll(() => activeText(page)).toBe('Sort ascending');
            // E goes to the filter's search box (FL-15).
            await page.keyboard.press('e');
            await expect.poll(() => activeIsInPopover(page)).toMatchObject({ role: 'dialog', tag: 'INPUT' });
            const landed = await watchWhereKeysLand(page);
            // The host's answers are held across the keys: nothing the close waits on may come from it (ADR-0220).
            await holdAnswers(true);
            try {
                await page.keyboard.press('Escape');
                await page.keyboard.press('ArrowDown');
                await page.keyboard.press('ArrowDown');
                await holdAnswers(false);

                await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
                await circuitQuiet();
                await expectKeyboardOn(grid(page));
                await expectActiveDescendant(grid(page), /r3c0$/);
                expect(await landed()).toEqual([
                    { key: 'Escape', where: 'popover' }, { key: 'ArrowDown', where: 'grid' }, { key: 'ArrowDown', where: 'grid' }]);
            } finally {
                await holdAnswers(false);
            }
        });

        test('a key typed while a command pressed in a menu closes it is the grid\'s: ↓ typed at once moves the Focus (ADR-0044, 2026-10-12)', async ({ page }) => {
            await clickCell(page, 2, 1);
            await grid(page).locator("[id$='r2c1']").click({ button: 'right', force: true });
            const command = grid(page).locator('[role=menu] button[role=menuitem]').last();
            await expect(command).toBeVisible();
            // Finish opening the menu before holding answers for the closing gesture. Visibility
            // alone can precede its keyboard hand-off, which the hold would otherwise interrupt.
            await settled(grid(page));
            const landed = await watchWhereKeysLand(page);
            // The host's answers are held across the keys: nothing the close waits on may come from it (ADR-0220).
            await holdAnswers(true);
            try {
                await command.click();
                await page.keyboard.press('ArrowDown');
                await holdAnswers(false);

                await expect(grid(page).locator('[role=menu]')).toHaveCount(0);
                await expect(page.locator('#context-status')).toContainText('(1 cells selected');
                await circuitQuiet();
                await expectKeyboardOn(grid(page));
                await expectActiveDescendant(grid(page), /r3c1$/);
                expect(await landed()).toEqual([{ key: 'ArrowDown', where: 'grid' }]);
            } finally {
                await holdAnswers(false);
            }
        });

        // A panel's OK, Apply and Cancel close its popover as a command run does (ADR-0010's rule for a
        // key that closes a popover; ADR-0044's note of 2026-10-12): each Chrome marks them
        // ex-popover-closes. Notional > 0 excludes no row, so the Selection stands (ADR-0011) and the
        // Focus is where the panel was opened, r1c2.
        const closingButton = {
            builtin: {
                ok: (panel) => panel.locator('.ex-popover-actions button', { hasText: 'OK' }),
                cancel: (panel) => panel.locator('.ex-popover-actions button', { hasText: 'Cancel' }),
            },
            mud: {
                ok: (panel) => panel.locator('.mud-ex-grid-filter-apply'),
                cancel: (panel) => panel.locator('.mud-ex-grid-filter-cancel'),
            },
        };

        async function notionalAboveZero(page) {
            const panel = await openNotionalPanel(page);
            await CONDITION[chrome].choose(page, panel);
            await CONDITION[chrome].operand(panel).fill('0');
            await expect(closingButton[chrome].ok(panel)).toBeEnabled();
            return panel;
        }

        test('keys typed while Enter on OK closes a filter panel are the grid\'s, in order: ↓, ↓ typed at once move the Focus (ADR-0010, ADR-0044, 2026-10-12)', async ({ page }) => {
            const all = await grid(page).getAttribute('aria-rowcount');
            const panel = await notionalAboveZero(page);
            await closingButton[chrome].ok(panel).focus();
            const landed = await watchWhereKeysLand(page);
            // The host's answers are held across the keys: nothing the close waits on may come from it (ADR-0220).
            await holdAnswers(true);
            try {
                await page.keyboard.press('Enter');
                await page.keyboard.press('ArrowDown');
                await page.keyboard.press('ArrowDown');
                await holdAnswers(false);

                await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
                await circuitQuiet();
                await expectKeyboardOn(grid(page));
                await expectActiveDescendant(grid(page), /r3c2$/);
                expect(await grid(page).getAttribute('aria-rowcount')).toBe(all);
                expect(await landed()).toEqual([
                    { key: 'Enter', where: 'popover' }, { key: 'ArrowDown', where: 'grid' }, { key: 'ArrowDown', where: 'grid' }]);
            } finally {
                await holdAnswers(false);
            }
            // OK applied the condition: Clear filter is there to take it away.
            await page.keyboard.press('Alt+ArrowDown');
            await expect.poll(() => activeText(page)).toBe('Sort ascending');
            await expect(grid(page).locator('.ex-popover button[role=menuitem]', { hasText: /Clear filter/ })).toBeEnabled();
        });

        test('keys typed while Space on Cancel closes a filter panel are the grid\'s, in order: ↓, ↓ typed at once move the Focus (ADR-0010, ADR-0044, 2026-10-12)', async ({ page }) => {
            const panel = await notionalAboveZero(page);
            await closingButton[chrome].cancel(panel).focus();
            const landed = await watchWhereKeysLand(page);
            // The host's answers are held across the keys: nothing the close waits on may come from it (ADR-0220).
            await holdAnswers(true);
            try {
                await page.keyboard.press('Space');
                await page.keyboard.press('ArrowDown');
                await page.keyboard.press('ArrowDown');
                await holdAnswers(false);

                await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
                await circuitQuiet();
                await expectKeyboardOn(grid(page));
                await expectActiveDescendant(grid(page), /r3c2$/);
                expect(await landed()).toEqual([
                    { key: ' ', where: 'popover' }, { key: 'ArrowDown', where: 'grid' }, { key: 'ArrowDown', where: 'grid' }]);
            } finally {
                await holdAnswers(false);
            }
            // Cancel applied nothing.
            await page.keyboard.press('Alt+ArrowDown');
            await expect.poll(() => activeText(page)).toBe('Sort ascending');
            await expect(grid(page).locator('.ex-popover button[role=menuitem]', { hasText: /Clear filter/ })).toBeDisabled();
        });

        test('a key typed while a press on OK closes a filter panel is the grid\'s: ↓ typed at once moves the Focus (ADR-0039, ADR-0044, 2026-10-12)', async ({ page }) => {
            const panel = await notionalAboveZero(page);
            const landed = await watchWhereKeysLand(page);
            // The host's answers are held across the keys: nothing the close waits on may come from it (ADR-0220).
            await holdAnswers(true);
            try {
                await closingButton[chrome].ok(panel).click();
                await page.keyboard.press('ArrowDown');
                await holdAnswers(false);

                await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
                await circuitQuiet();
                await expectKeyboardOn(grid(page));
                await expectActiveDescendant(grid(page), /r2c2$/);
                expect(await landed()).toEqual([{ key: 'ArrowDown', where: 'grid' }]);
            } finally {
                await holdAnswers(false);
            }
        });

        test('in a menu the arrows, Home and End move among the enabled items and wrap (KB-30, ADR-0039)', async ({ page }) => {
            await clickCell(page, 1, 0);
            await page.keyboard.press('Alt+ArrowDown');
            // Read once it is drawn — a round trip away on the Server host.
            await expect(grid(page).locator('.ex-popover [role=menu]')).toBeVisible();
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
            await expect(grid(page).locator('.ex-popover [role=menu]')).toBeVisible();
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
                await expectKeyboardOn(grid(page));
            });
        }

        for (const [key, lands] of [['Tab', 'the search box'], ['Shift+Tab', 'Cancel']]) {
            test(`over a filter, ${key} on a command moves to ${lands} — nothing runs, nothing closes (FL-12, KB-30, ADR-0044)`, async ({ page }) => {
                await clickCell(page, 1, 0);
                await page.keyboard.press('Alt+ArrowDown');
                await page.keyboard.press('ArrowDown');
                await expect.poll(() => activeText(page)).toBe('Sort descending');
                await expect(grid(page).locator('.ex-popover[role=dialog] input[type=search], .ex-popover[role=dialog] .mud-ex-grid-filter-search input')).toBeVisible();

                await page.keyboard.press(key);

                if (key === 'Tab')
                    await expect.poll(() => activeIsInPopover(page)).toMatchObject({ role: 'dialog', tag: 'INPUT' });
                else
                    await expect.poll(() => activeText(page)).toBe('Cancel');
                await expect(grid(page).locator('.ex-popover')).toHaveCount(1);
                await expect(grid(page).locator('.ex-header-cell[aria-sort=descending]')).toHaveCount(0);
            });
        }

        test('the Context Menu answers the same keys, and Tab closes it (KB-30, ADR-0039)', async ({ page }) => {
            await clickCell(page, 1, 1);
            await page.keyboard.press('Shift+F10');
            await expect(grid(page).locator('.ex-popover[role=menu]')).toBeVisible();
            const items = await enabledMenuItems(page);
            await expect.poll(() => activeText(page)).toBe(items[0]);
            await page.keyboard.press('End');
            await expect.poll(() => activeText(page)).toBe(items[items.length - 1]);
            await page.keyboard.press('ArrowDown');
            await expect.poll(() => activeText(page)).toBe(items[0]);
            await page.keyboard.press('Tab');
            await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
            await expectKeyboardOn(grid(page));
        });

        test('Tab and Shift+Tab wrap through both halves of the popover (KB-31, FL-12, ADR-0044)', async ({ page }) => {
            await openNotionalPanel(page);

            // Forward from the operator through the filter — the value, the join, the second
            // condition, OK — to Cancel, its last control, one settled step at a time: DOM
            // focus is never anywhere but the popover.
            const active = () => page.evaluate(() => document.activeElement?.outerHTML);
            for (let i = 0; i < 10 && (await activeText(page)) !== 'Cancel'; i++) {
                const before = await active();
                await page.keyboard.press('Tab');
                await expect.poll(active).not.toBe(before);
                expect(await activeIsInPopover(page)).not.toBeNull();
            }
            await expect.poll(() => activeText(page)).toBe('Cancel');

            // Tab off Cancel wraps to the first command, and Tab from there is the filter's.
            await page.keyboard.press('Tab');
            await expect.poll(() => activeText(page)).toBe('Sort ascending');
            await page.keyboard.press('Tab');
            await expect.poll(() => activeIsOperator(page)).toBe(true);

            // Shift+Tab off the operator goes back to the first command, and Shift+Tab on a
            // command to the filter's last control.
            await page.keyboard.press('Shift+Tab');
            await expect.poll(() => activeText(page)).toBe('Sort ascending');
            await page.keyboard.press('Shift+Tab');
            await expect.poll(() => activeText(page)).toBe('Cancel');
            await expect(grid(page).locator('.ex-popover[role=dialog]')).toBeVisible();
        });

        test('the letters: S sorts, E goes to the search box where "e" is text, C clears (FL-15, FL-16, ADR-0044)', async ({ page }) => {
            // S from a command sorts ascending and closes.
            await clickCell(page, 1, 0);
            await page.keyboard.press('Alt+ArrowDown');
            await expect.poll(() => activeText(page)).toBe('Sort ascending');
            await page.keyboard.press('ArrowDown');
            await expect.poll(() => activeText(page)).toBe('Sort descending');
            await page.keyboard.press('s');
            await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
            await expect(grid(page).locator('.ex-header-cell[aria-sort=ascending]')).toHaveCount(1);
            await expectKeyboardOn(grid(page));
            // The Sort is the header's as it is asked for; a source that sorts elsewhere, .NET's on the Server
            // host, is loading until its rows land, and a click before then is taken against the order before.
            await expect(grid(page)).not.toHaveClass(/\bex-loading\b/);

            // E goes to the search box, and a letter typed there is text — "elta" searches, and
            // neither its E nor anything after it is a letter of the commands. The sort
            // dropped the selection (ADR-0011), so the Focus is given again first.
            const all = await grid(page).getAttribute('aria-rowcount');
            await clickCell(page, 1, 0);
            await page.keyboard.press('Alt+ArrowDown');
            await expect.poll(() => activeText(page)).toBe('Sort ascending');
            const search = grid(page).locator('.ex-popover input[type=search], .ex-popover .mud-ex-grid-filter-search input');
            await expect(search).toBeVisible();
            await expect(grid(page).locator('.ex-popover button[role=menuitem]', { hasText: /Clear filter/ })).toBeDisabled();
            await page.keyboard.press('e');
            await expect.poll(() => activeIsInPopover(page)).toMatchObject({ role: 'dialog', tag: 'INPUT' });
            await page.keyboard.type('elta');
            await expect(search).toHaveValue('elta');
            await expect(grid(page).locator('.ex-popover')).toHaveCount(1);
            // Enter applies the search's matches, Delta alone (FL-10).
            await page.keyboard.press('Enter');
            await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
            await expect.poll(() => grid(page).getAttribute('aria-rowcount')).not.toBe(all);

            // Clear filter is enabled now, and C clears it. The filter dropped the selection
            // too (FL-8).
            await clickCell(page, 1, 0);
            await page.keyboard.press('Alt+ArrowDown');
            await expect.poll(() => activeText(page)).toBe('Sort ascending');
            await expect(grid(page).locator('.ex-popover button[role=menuitem]', { hasText: /Clear filter/ })).toBeEnabled();
            await page.keyboard.press('c');
            await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
            await expect.poll(() => grid(page).getAttribute('aria-rowcount')).toBe(all);
            await expectKeyboardOn(grid(page));
        });

        test('on the value list the letters act too: E goes back to the search box, O sorts (FL-15, ADR-0044)', async ({ page }) => {
            await clickCell(page, 1, 0);
            await page.keyboard.press('Alt+ArrowDown');
            await expect.poll(() => activeText(page)).toBe('Sort ascending');
            await expect(grid(page).locator('.ex-popover-list, .mud-ex-grid-filter-values')).toBeVisible();
            const inList = () => page.evaluate(() => !!document.activeElement?.closest('.ex-popover-list, .mud-ex-grid-filter-values'));
            const active = () => page.evaluate(() => document.activeElement?.outerHTML);
            const tabIntoList = async () => {
                for (let i = 0; i < 4 && !(await inList()); i++) {
                    const before = await active();
                    await page.keyboard.press('Tab');
                    await expect.poll(active).not.toBe(before);
                }
                expect(await inList(), 'Tab from the search box reaches the value list').toBe(true);
            };

            // E to the search box, and Tab on to the list's first entry, "(Select All)".
            await page.keyboard.press('e');
            await expect.poll(() => activeIsInPopover(page)).toMatchObject({ role: 'dialog', tag: 'INPUT' });
            await tabIntoList();

            // E on the list goes back to the search box.
            await page.keyboard.press('e');
            await expect.poll(inList).toBe(false);
            await expect.poll(() => activeIsInPopover(page)).toMatchObject({ role: 'dialog', tag: 'INPUT' });
            expect(await grid(page).locator('.ex-popover input[type=search], .ex-popover .mud-ex-grid-filter-search input').inputValue()).toBe('');

            // O on the list sorts descending and closes, as it does on a command.
            await tabIntoList();
            await page.keyboard.press('o');
            await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
            await expect(grid(page).locator('.ex-header-cell[aria-sort=descending]')).toHaveCount(1);
            await expectKeyboardOn(grid(page));
        });

        test('where no value list stands, E goes to the condition\'s value, where a search is typed (FL-15, ADR-0044)', async ({ page }) => {
            await clickCell(page, 1, 2);
            await page.keyboard.press('Alt+ArrowDown');
            await expect.poll(() => activeText(page)).toBe('Sort ascending');
            await expect(grid(page).locator('.ex-popover[role=dialog]')).toBeVisible();

            await page.keyboard.press('e');

            await expect.poll(() => page.evaluate(() => {
                const active = document.activeElement;
                return !!active && (active.matches('.ex-popover form input:not([type])')
                    || active.closest('.mud-ex-grid-filter-operand') !== null);
            })).toBe(true);
            await page.keyboard.type('5');
            await expect.poll(() => page.evaluate(() => document.activeElement?.value)).toBe('5');
        });

        test('E on a checkbox outside the value list holds nothing: a Space after it ticks at once (ADR-0010/0044)', async ({ page }) => {
            // A filter in force on Book, so that a search offers "Add current selection".
            await clickCell(page, 1, 0);
            await page.keyboard.press('Alt+ArrowDown');
            await expect.poll(() => activeText(page)).toBe('Sort ascending');
            await expect(grid(page).locator('.ex-popover-list, .mud-ex-grid-filter-values')).toBeVisible();
            await page.keyboard.press('e');
            await expect.poll(() => activeIsInPopover(page)).toMatchObject({ role: 'dialog', tag: 'INPUT' });
            await page.keyboard.type('Alpha');
            await page.keyboard.press('Enter');
            await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
            // The filtered rows have come: a click before them is taken against the rows before, and
            // their change drops it (ADR-0440 section 6, ADR-0011).
            await expect(grid(page)).not.toHaveClass(/\bex-loading\b/);

            await clickCell(page, 1, 0);
            await page.keyboard.press('Alt+ArrowDown');
            await expect.poll(() => activeText(page)).toBe('Sort ascending');
            await expect(grid(page).locator('.ex-popover-list, .mud-ex-grid-filter-values')).toBeVisible();
            await page.keyboard.press('e');
            await expect.poll(() => activeIsInPopover(page)).toMatchObject({ role: 'dialog', tag: 'INPUT' });
            await page.keyboard.type('Be');
            const add = grid(page).locator('.ex-popover-add input, .mud-ex-grid-filter-add input');
            await expect(add).toBeVisible();
            await add.focus();

            await page.keyboard.press('e');
            await page.keyboard.press('Space');

            // Held behind an E the grid does not answer there, the Space would wait two
            // seconds and then be dropped.
            await expect(add).toBeChecked({ timeout: 700 });
        });

        test('Enter in the value field applies exactly what OK applies (KB-31, ADR-0039)', async ({ page }) => {
            const byOk = await rowCountAfterFilter(page, chrome, (panel) => CONDITION[chrome].apply(panel).click());

            await page.reload();
            await expect(grid(page).locator('.ex-row').first()).toBeVisible();
            const byEnter = await rowCountAfterFilter(page, chrome, (panel) => CONDITION[chrome].operand(panel).press('Enter'));
            expect(byEnter).toBe(byOk);
        });

        test('with nothing ticked the value filter is not applied: Apply shows it is unavailable, and neither Apply nor Enter applies it (ADR-0009/0023, WR-2, ticket 76)', async ({ page }) => {
            const all = await grid(page).getAttribute('aria-rowcount');
            const popover = grid(page).locator('.ex-popover');
            const values = VALUE_LIST[chrome];
            await clickCell(page, 1, 0);
            await page.keyboard.press('Alt+ArrowDown');
            await expect.poll(() => activeText(page)).toBe('Sort ascending');
            await expect(grid(page).locator('.ex-popover-list, .mud-ex-grid-filter-values')).toBeVisible();

            // "(Select All)" toggled from every value to none.
            await values.selectAll(popover).click();
            await expect(values.ticked(popover)).toHaveCount(0);

            // Enter in the search box submits the form whatever Apply's state (ADR-0039); an In
            // with no values is not a filter the engine takes (ADR-0023), so the panel stands.
            // Absence cannot be waited for, so the submission is given time to land.
            await values.search(popover).focus();
            await page.keyboard.press('Enter');
            // spec-lint: fixed-wait: Enter in the search box submits the popover's form, an event Blazor hears on its own, which the Settled Mark does not cover (ADR-0220, As built); circuitQuiet() proves it on the Server host, and on WebAssembly nothing marks that the panel will not close
            await page.waitForTimeout(600);
            await circuitQuiet();
            await expect(popover).toHaveCount(1);
            await expect(values.apply(popover)).toBeDisabled();
            // A press on the unavailable Apply does nothing either.
            await values.apply(popover).click({ force: true });
            // spec-lint: fixed-wait: the press on Apply is a click Blazor hears on its own, which the Settled Mark does not cover (ADR-0220, As built); circuitQuiet() proves it on the Server host, and on WebAssembly nothing marks that nothing will be applied
            await page.waitForTimeout(600);
            await circuitQuiet();
            await expect(popover).toHaveCount(1);
            expect(await grid(page).getAttribute('aria-rowcount')).toBe(all);

            // Escape closes it as a Cancel, and the rows are as they were.
            await page.keyboard.press('Escape');
            await expect(popover).toHaveCount(0);
            expect(await grid(page).getAttribute('aria-rowcount')).toBe(all);
        });

        test('a pointer-down elsewhere in the instance dismisses a popover and keeps its own meaning (KB-17, ADR-0010/0039)', async ({ page }) => {
            await clickCell(page, 1, 0);
            await page.keyboard.press('Alt+ArrowDown');
            await expect(grid(page).locator('.ex-popover [role=menu]')).toBeVisible();

            // Well clear of the menu, which stands under the first column's header: a press
            // that lands on an item runs it, which is not this test.
            await clickCell(page, 4, 4);

            // The menu is gone without running anything, the press selected the cell it
            // landed on, and the keyboard is the grid's.
            await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
            await expectActiveDescendant(grid(page), /r4c4$/);
            await expect(grid(page).locator('.ex-header-cell[aria-sort=ascending]')).toHaveCount(0);
            await expectKeyboardOn(grid(page));
        });
    });
}

// Alt+↓ is the grid's where the Focus's column has a menu: the keydown's default is prevented, so the
// browser does nothing of its own with it, and the page does not move. On a grid with no column menu
// it opens nothing, and the Focus stays (KB-28, ADR-0039).
async function hearAltDown(page) {
    await alterPage(page, () => {
        // Heard first, in the window's capture phase (the grid stops the key's propagation at its
        // root), and kept: its defaultPrevented is read once the dispatch is over.
        window.__altDown = null;
        const listener = (e) => {
            if (e.key === 'ArrowDown' && e.altKey) {
                window.__altDown = e;
            }
        };
        window.addEventListener('keydown', listener, true);
        return () => {
            window.removeEventListener('keydown', listener, true);
            delete window.__altDown;
        };
    });
}

test('KB-28: Alt+↓ is taken from the browser where the column has a menu, the page unmoved; on a grid with no column menu it opens nothing (ADR-0039)', async ({ page }) => {
    await page.goto('/features');
    await expect(grid(page).locator('.ex-row').first()).toBeVisible();
    await hearAltDown(page);
    await clickCell(page, 1, 0);
    const scrolled = () => page.evaluate(() => [window.scrollX, window.scrollY]);
    const before = await scrolled();

    await page.keyboard.press('Alt+ArrowDown');
    await expect(grid(page).locator('.ex-popover [role=menu]')).toHaveAttribute('aria-label', 'Book');
    expect(await page.evaluate(() => window.__altDown?.defaultPrevented)).toBe(true);
    expect(await scrolled()).toEqual(before);

    // /wide hides its column menus (HideColumnMenu): its columns have no menu.
    await page.goto('/wide?rows=1000');
    const wide = grid(page);
    await expect(wide.locator('.ex-row').first()).toBeVisible();
    await expect(wide.locator('.ex-menu-button')).toHaveCount(0);
    await wide.locator("[id$='r2c3']").click({ force: true });
    await expectActiveDescendant(wide, /r2c3$/);
    await page.keyboard.press('Alt+ArrowDown');
    await settled(wide);
    await expect(wide.locator('.ex-popover')).toHaveCount(0);
    await expectActiveDescendant(wide, /r2c3$/);
    await expectKeyboardOn(wide);
});
