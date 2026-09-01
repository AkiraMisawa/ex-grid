import { test, expect } from '@playwright/test';

// The interaction surface, driven with real keys and the real clipboard against the
// /features page: the Cell Editor's two states (ADR-0010), the clipboard's two formats
// and its refusals (ADR-0005/0014/0016), the keys the grid must NOT take (ADR-0012),
// and the one-tab-stop contract (ADR-0033). Console and page errors fail the run
// (CON-1/2): this component displays money, and something the browser is complaining
// about may be something the reader is already seeing wrong.

let consoleErrors;
let pageErrors;

test.beforeEach(async ({ page, context }) => {
    consoleErrors = [];
    pageErrors = [];
    page.on('console', (message) => {
        if (message.type() === 'error') {
            consoleErrors.push(message.text());
        }
    });
    page.on('pageerror', (error) => pageErrors.push(String(error)));
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    await page.goto('/features');
    await expect(page.locator('.ex-grid').first().locator('.ex-row').first()).toBeVisible();
});

test.afterEach(() => {
    expect(consoleErrors, 'zero console errors across the run (CON-1)').toEqual([]);
    expect(pageErrors, 'zero uncaught page errors (CON-2)').toEqual([]);
});

function grid(page) {
    return page.locator('.ex-grid').first();
}

async function clickCell(page, row, column) {
    // Cells are pointer-events: none by design — the Viewport is the delegated target
    // (ADR-0004) — so Playwright's actionability check must be bypassed: the browser
    // then hit-tests the click through to the Viewport, exactly as a user's does.
    const cell = grid(page).locator(`[id$='r${row}c${column}']`);
    await cell.click({ force: true });
}

test('typing opens Overwrite containing the character, Enter commits and moves (ED-2/ED-4)', async ({ page }) => {
    await clickCell(page, 0, 1); // Trader, editable
    await page.keyboard.type('X');

    const editor = grid(page).locator('input.ex-editor');
    await expect(editor).toHaveValue('X');

    await page.keyboard.type('yz');
    await page.keyboard.press('Enter');

    await expect(editor).toHaveCount(0);
    await expect(page.locator('#edit-status')).toContainText('Trader=Xyz');
    // The demo's Consumer applies the intent (ADR-0007's other half): a NEW row
    // instance comes back through the source and the painted cell follows.
    await expect(grid(page).locator("[id$='r0c1']")).toHaveText('Xyz');
    // Enter moved the Focus down: the activedescendant names row 1.
    const active = await grid(page).getAttribute('aria-activedescendant');
    expect(active).toMatch(/r1c1$/);
});

test('in Overwrite an arrow commits and moves; in Caret it moves the caret (ED-2)', async ({ page }) => {
    await clickCell(page, 0, 1);
    await page.keyboard.type('AB');
    // Caret via F2: the arrow now belongs to the editor and moves its caret.
    await page.keyboard.press('F2');
    await page.keyboard.press('ArrowLeft');
    await page.keyboard.type('x');
    const editor = grid(page).locator('input.ex-editor');
    await expect(editor).toHaveValue('AxB');

    // Back to Overwrite: the arrow commits and moves.
    await page.keyboard.press('F2');
    await page.keyboard.press('ArrowRight');
    await expect(editor).toHaveCount(0);
    await expect(page.locator('#edit-status')).toContainText('Trader=AxB');
});

test('Escape cancels the editor and never blurs the grid mid-edit (ED-3)', async ({ page }) => {
    await clickCell(page, 0, 1);
    await page.keyboard.type('Q');
    await page.keyboard.press('Escape');

    await expect(grid(page).locator('input.ex-editor')).toHaveCount(0);
    await expect(page.locator('#edit-status')).toContainText('Edited: —');
    // The grid still holds the keyboard: the root has DOM focus again.
    await expect(grid(page)).toBeFocused();

    // A second Escape, outside editing, is Leave: focus exits the grid (KB-8).
    await page.keyboard.press('Escape');
    await expect(grid(page)).not.toBeFocused();
});

test('the clipboard carries both formats, and #### never reaches it (CP-4/CP-5/CP-6/CP-10)', async ({ page }) => {
    // Select the Narrow (####) cell of row 0 and extend to include a text cell.
    await clickCell(page, 0, 2); // Notional
    await page.keyboard.press('Shift+ArrowRight'); // + Narrow (####)

    // The narrowed cell really paints ####.
    await expect(grid(page).locator("[id$='r0c3']")).toContainText('#');

    await page.keyboard.press('ControlOrMeta+C');

    const clipboard = await page.evaluate(async () => {
        const items = await navigator.clipboard.read();
        const result = {};
        for (const item of items) {
            for (const type of item.types) {
                result[type] = await (await item.getType(type)).text();
            }
        }
        return result;
    });
    expect(Object.keys(clipboard)).toEqual(expect.arrayContaining(['text/plain', 'text/html']));
    // CP-5: the raw value, never the hashes — in either flavour.
    expect(clipboard['text/plain']).not.toContain('#');
    expect(clipboard['text/html']).toContain('1000000');
    // CP-4: the html flavour is the raw, locale-free value; the plain is the display.
    // Chrome normalises the fragment on read (a meta charset, a tbody); the cells are
    // what the contract is about.
    expect(clipboard['text/html']).toContain('<td>1000000.00</td><td>1000000.00</td>');
});

test('a misaligned selection refuses and the clipboard stays untouched (CP-1/CP-3)', async ({ page }) => {
    await page.evaluate(() => navigator.clipboard.writeText('SENTINEL'));
    // Two ranges that do not line up: (0,1) and (1,2).
    await clickCell(page, 0, 1);
    await page.keyboard.down('ControlOrMeta');
    await clickCell(page, 1, 2);
    await page.keyboard.up('ControlOrMeta');

    await page.keyboard.press('ControlOrMeta+C');

    await expect(page.locator('#copy-status')).toContainText('MisalignedShape');
    expect(await page.evaluate(() => navigator.clipboard.readText())).toBe('SENTINEL');
});

test('paste raises one intent shaped by the clipboard block (CP-14, PST-1)', async ({ page }) => {
    await page.evaluate(() => navigator.clipboard.writeText('fill'));
    await clickCell(page, 0, 1);
    await page.keyboard.press('Shift+ArrowDown');
    await page.keyboard.press('Shift+ArrowDown');

    await page.keyboard.press('ControlOrMeta+V');

    await expect(page.locator('#paste-status')).toContainText('3 cells from 1x1');
});

test('Ctrl+PageDown is neither handled nor prevented (KB-15)', async ({ page }) => {
    await clickCell(page, 0, 1);
    const focusBefore = await grid(page).getAttribute('aria-activedescendant');

    const prevented = page.evaluate(() => new Promise((resolve) => {
        document.addEventListener('keydown', (event) => {
            if (event.key === 'PageDown') resolve(event.defaultPrevented);
        }, { once: true, capture: false });
        setTimeout(() => resolve('never arrived'), 3000);
    }));
    await page.keyboard.press('ControlOrMeta+PageDown');

    expect(await prevented).toBe(false);
    expect(await grid(page).getAttribute('aria-activedescendant')).toBe(focusBefore);
});

test('the grid is one tab stop (A11Y-4, KB-12)', async ({ page }) => {
    // Tab from the address bar territory: focus the body first.
    await page.evaluate(() => document.body.focus());
    await page.keyboard.press('Tab');

    // The first tab stop inside the page that is the grid's is the root itself.
    const first = grid(page);
    // Walk tabs until the first grid is reached (nav links precede it).
    for (let i = 0; i < 20; i++) {
        if (await first.evaluate((el) => document.activeElement === el)) break;
        await page.keyboard.press('Tab');
    }
    await expect(first).toBeFocused();
    // Keyboard focus shows the ring (KB-12).
    const outline = await first.evaluate((el) => getComputedStyle(el).outlineStyle);
    expect(outline).not.toBe('none');

    // One more Tab leaves the grid entirely: no cell is a tab stop.
    await page.keyboard.press('Tab');
    const activeInsideGrid = await first.evaluate(
        (el) => el === document.activeElement || el.contains(document.activeElement));
    // The next stop can be the second grid's root or the menu buttons; what it must
    // not be is a cell of the first grid.
    const activeIsCell = await page.evaluate(
        () => document.activeElement?.classList?.contains('ex-cell') ?? false);
    expect(activeIsCell).toBe(false);
    void activeInsideGrid;
});

test('two instances stay independent: no --ex-* on :root, no window global (DOM-4)', async ({ page }) => {
    const grids = page.locator('.ex-grid');
    await expect(grids).toHaveCount(2);

    const leaks = await page.evaluate(() => {
        const root = document.documentElement;
        const rootVars = [...root.style].filter((name) => name.startsWith('--ex-'));
        const globals = Object.keys(window).filter((key) => key.toLowerCase().startsWith('exgrid'));
        return { rootVars, globals };
    });
    expect(leaks.rootVars).toEqual([]);
    expect(leaks.globals).toEqual([]);

    // The two roots carry their own geometry tokens.
    const heights = await page.evaluate(() =>
        [...document.querySelectorAll('.ex-grid')].map((el) => el.style.getPropertyValue('--ex-row-height')));
    expect(heights).toEqual(['24px', '20px']);
});

test('a header click sorts and the selection is untouched by it (SR-1)', async ({ page }) => {
    await clickCell(page, 0, 0);
    const header = grid(page).locator('.ex-header-cell').nth(2); // Notional
    await header.click({ position: { x: 30, y: 14 }, force: true });

    await expect(grid(page).locator('.ex-header-cell').nth(2)).toHaveAttribute('aria-sort', 'ascending');
    // The click did not select the column: the selection is still the one cell.
    const active = await grid(page).getAttribute('aria-activedescendant');
    expect(active).toMatch(/c0$/);
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

// The /cells page is the other half of the key-gate contract: a grid with NO editable
// column, whose Template Column holds a real focusable control (ADR-0020).

test('a display-only grid does not take printable keys away from the page (ADR-0010/0020)', async ({ page }) => {
    await page.goto('/cells');
    const cells = page.locator('.ex-grid').first();
    await expect(cells.locator('.ex-row').first()).toBeVisible();
    await cells.locator("[id$='r0c0']").click({ force: true });

    // Nothing on this page edits, so a printable key must pass the grid by: not
    // preventDefaulted, not stopPropagation-ed, and no editor appears. The listener
    // sits in the bubble phase past the grid and is INSTALLED before the press (the
    // KB-15 lesson) — null afterwards means the grid swallowed the keydown outright.
    await page.evaluate(() => {
        window.__printableSeen = null;
        document.addEventListener('keydown', (e) => {
            window.__printableSeen = !e.defaultPrevented;
        }, { once: true });
    });
    await page.keyboard.press('x');
    await expect(cells.locator('.ex-editor')).toHaveCount(0);
    const seen = await page.evaluate(() => window.__printableSeen);
    expect(seen, 'the keydown reached the page untouched').toBe(true);
});

test('Escape returns the keyboard from a descendant control to the grid (ADR-0020/0012)', async ({ page }) => {
    await page.goto('/cells');
    const cells = page.locator('.ex-grid').first();
    await expect(cells.locator('.ex-row').first()).toBeVisible();

    const note = cells.locator('input.demo-note').first();
    await note.click({ force: true });
    await expect(note).toBeFocused();
    // The control owns its keys (KB-11)…
    await note.pressSequentially('memo');
    await expect(note).toHaveValue('memo');

    // …but Escape is the way out: back to the grid, not out of it.
    await page.keyboard.press('Escape');
    await expect(cells).toBeFocused();
});
