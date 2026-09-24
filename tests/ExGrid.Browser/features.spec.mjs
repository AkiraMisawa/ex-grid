import { test, expect } from './fixtures.mjs';

// The interaction surface, driven with real keys and the real clipboard against the
// /features page: the Cell Editor's two states (ADR-0010), the clipboard's two formats
// and its refusals (ADR-0005/0014/0016), the keys the grid must NOT take (ADR-0012),
// and the one-tab-stop contract (ADR-0033). Console and page errors fail the run
// (CON-1/2, in fixtures.mjs): this component displays money, and something the browser
// is complaining about may be something the reader is already seeing wrong.

test.beforeEach(async ({ page, context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    await page.goto('/features');
    await expect(page.locator('.ex-grid').first().locator('.ex-row').first()).toBeVisible();
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

test('a paste covering a non-editable column is refused whole (CP-16, ADR-0035)', async ({ page }) => {
    await page.evaluate(() => navigator.clipboard.writeText('intruder'));
    const book = grid(page).locator("[id$='r0c0']"); // Book, never declared editable
    const before = await book.textContent();
    await clickCell(page, 0, 0);
    await page.keyboard.press('Shift+ArrowRight'); // ...and Trader, which is editable

    await page.keyboard.press('ControlOrMeta+V');

    await expect(page.locator('#paste-refused-status')).toContainText('TargetNotEditable');
    // The whole target is refused, not the editable half of it.
    await expect(book).toHaveText(before);
    await expect(grid(page).locator("[id$='r0c1']")).not.toHaveText('intruder');
});

test('Ctrl+Enter fill across a non-editable column is refused, and says so (CP-16, ADR-0035)', async ({ page }) => {
    const narrow = grid(page).locator("[id$='r0c3']");
    const notional = grid(page).locator("[id$='r0c2']");
    const narrowBefore = await narrow.textContent();
    const notionalBefore = await notional.textContent();
    await clickCell(page, 0, 3);                   // Narrow, which is not editable
    await page.keyboard.press('Shift+ArrowLeft');  // Focus lands on Notional, which is
    await page.keyboard.type('7');                 // — so the editor opens, over a selection covering Narrow

    await page.keyboard.press('ControlOrMeta+Enter');

    await expect(page.locator('#paste-refused-status')).toContainText('TargetNotEditable');
    // Nothing was written — neither the column that refused nor the one that would have
    // been allowed on its own.
    await expect(narrow).toHaveText(narrowBefore);
    await expect(notional).toHaveText(notionalBefore);
    // ED-19: the Refusal judged the operation, not the text, so the editor is still
    // standing with the typing in it.
    await expect(grid(page).locator('.ex-editor')).toHaveValue('7');
});

test('a refusal is announced, not only painted (A11Y-16, ADR-0035)', async ({ page }) => {
    // The grid has an enum and no sentence, so it cannot announce this one; whatever Chrome
    // renders a refusal into has to be a live region or the refusal is silent to a reader
    // who cannot see it. The reference Chrome meets its own contract here.
    await expect(page.locator('#paste-refused-status')).toHaveAttribute('role', 'status');

    await page.evaluate(() => navigator.clipboard.writeText('intruder'));
    await clickCell(page, 0, 0);                   // Book, never declared editable
    await page.keyboard.press('Shift+ArrowRight');
    await page.keyboard.press('ControlOrMeta+V');

    await expect(page.locator('#paste-refused-status')).toContainText('TargetNotEditable');

    // A live region announces on mutation, so the second refusal for the same reason
    // would be silent if Chrome wrote the same sentence again — and that is the one the
    // user needs, having just tried again.
    const first = await page.locator('#paste-refused-status').textContent();
    await page.keyboard.press('ControlOrMeta+V');
    await expect(page.locator('#paste-refused-status')).not.toHaveText(first);
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

test('a menu copy writes without a prompt, and with headers (CP-19/CP-17, ADR-0005/0036)', async ({ page, context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    await clickCell(page, 0, 1);
    await page.keyboard.press('Shift+ArrowDown');

    await grid(page).locator("[id$='r0c1']").click({ button: 'right', force: true });
    await grid(page).locator('[role=menu] button[role=menuitem]')
        .filter({ hasText: 'Copy with headers' }).click();

    // A menu item's click fires no copy event, so this went through the asynchronous
    // clipboard API. Chromium grants clipboard-write to the active tab, so no prompt is
    // expected — if one ever appears this test hangs, which is the finding, not a pass.
    await expect.poll(async () => page.evaluate(() => navigator.clipboard.readText()), { timeout: 5000 })
        .toMatch(/^Trader\r?\n/);
});

test('a Reject holds the editor with real keys, and Escape is the way out (ED-15, ADR-0034)', async ({ page }) => {
    await clickCell(page, 0, 2);                   // Notional, which carries the verdict
    await page.keyboard.type('abc');

    await page.keyboard.press('Enter');

    // Every commit gesture stops, and the editor is still standing with the text in it.
    await expect(grid(page).locator('.ex-editor')).toHaveValue('abc');
    await expect(grid(page).locator('.ex-editor')).toHaveAttribute('aria-invalid', 'true');
    await page.keyboard.press('Tab');
    await expect(grid(page).locator('.ex-editor')).toHaveValue('abc');
    // The Consumer's own sentence, relayed into the root's live region (A11Y-15).
    await expect(grid(page).locator('.ex-announce')).toContainText("'abc' is not a number");
    await expect(page.locator('#edit-status')).not.toContainText('Notional=abc');

    await page.keyboard.press('Escape');
    await expect(grid(page).locator('.ex-editor')).toHaveCount(0);
});

test('a Flag applies and is marked, and its message opens on the Focus (ED-16/ED-17, ADR-0034)', async ({ page }) => {
    await clickCell(page, 0, 2);
    await page.keyboard.type('-5');

    await page.keyboard.press('Enter');

    // Applied — the intent was byte-identical to an Accept's — and marked afterwards by
    // the Consumer's ruleset through the display channels.
    await expect(page.locator('#edit-status')).toContainText('Notional=-5');
    await expect(grid(page).locator("[id$='r0c2']")).toHaveClass(/ex-state-error/);

    // The popover follows the Focus after 300ms of stillness, and never before.
    await clickCell(page, 0, 2);
    await expect(grid(page).locator('.ex-message')).toHaveCount(0);
    await expect(grid(page).locator('.ex-message')).toContainText('approval', { timeout: 3000 });
});

test('a flagged cell says why when the pointer rests on it (ED-17b, ADR-0021/0034)', async ({ page }) => {
    // Flag a cell first: -5 is applied and marked by the Consumer's ruleset.
    await clickCell(page, 0, 2);
    await page.keyboard.type('-5');
    await page.keyboard.press('Enter');
    await expect(grid(page).locator("[id$='r0c2']")).toHaveClass(/ex-state-error/);
    // Park the Focus elsewhere so what follows can only be the hover.
    await clickCell(page, 4, 1);
    await expect(grid(page).locator('.ex-message')).toHaveCount(0);

    await grid(page).locator("[id$='r0c2']").hover({ force: true });

    // JS heard the moves; C# was told once, when the pointer stopped.
    await expect(grid(page).locator('.ex-message')).toContainText('approval', { timeout: 3000 });

    // And crossing back out takes it away.
    await page.mouse.move(5, 5);
    await expect(grid(page).locator('.ex-message')).toHaveCount(0);
});

test('a pasted value wears the same mark as a typed one (ADR-0034)', async ({ page }) => {
    await page.evaluate(() => navigator.clipboard.writeText('-5'));
    await clickCell(page, 1, 2);                   // Notional

    await page.keyboard.press('ControlOrMeta+V');

    // One ruleset, whether the change arrived by an edit or by a paste.
    await expect(grid(page).locator("[id$='r1c2']")).toHaveClass(/ex-state-error/);
});

test('Ctrl+PageDown is neither handled nor prevented (KB-15)', async ({ page }) => {
    await clickCell(page, 0, 1);
    const focusBefore = await grid(page).getAttribute('aria-activedescendant');

    // Registered and awaited before the key is sent, and NOT `once`: a chord arrives
    // as two keydowns — Control first, then PageDown — and a once-listener is spent
    // on the Control. This test used to pass only when its un-awaited registration
    // happened to land between the two keydowns, which read as "never arrived" about
    // half the time on Edge and looked like the browser swallowing the shortcut.
    await page.evaluate(() => {
        window.__kb15 = new Promise((resolve) => {
            document.addEventListener('keydown', (event) => {
                if (event.key === 'PageDown') resolve(event.defaultPrevented);
            }, { capture: false });
            setTimeout(() => resolve('never arrived'), 3000);
        });
    });
    await page.keyboard.press('ControlOrMeta+PageDown');

    expect(await page.evaluate(() => window.__kb15)).toBe(false);
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

// Entering a cell by key (ADR-0037), on /cells. Columns there: Book 0 (pinned), Close of
// business 1, Intraday 2, Limit used 3 (a Template), Note 4 (a Template with a real
// field), Actions 5 (one action), Review 6 (three actions). The Focus is placed by
// clicking the pinned Book cell and walked there by key, so no test depends on where a
// button happens to be painted.

async function openCellsAt(page, column) {
    await page.goto('/cells');
    const cells = page.locator('.ex-grid').first();
    await expect(cells.locator('.ex-row').first()).toBeVisible();
    await cells.locator("[id$='r0c0']").click({ force: true });
    for (let i = 0; i < column; i++) {
        await page.keyboard.press('ArrowRight');
    }
    await expect(cells).toHaveAttribute('aria-activedescendant', new RegExp(`r0c${column}$`));
    return cells;
}

test('Space enters a cell with several actions; the arrows choose and Space fires once (KB-20/KB-21, ADR-0037)', async ({ page }) => {
    const cells = await openCellsAt(page, 6);

    await page.keyboard.press(' ');
    const chosen = cells.locator('.ex-action-chosen');
    await expect(chosen).toHaveCount(1);
    await expect(chosen).toHaveText('Approve');
    // The keyboard never left the root; the root names the chosen button instead.
    await expect(cells).toBeFocused();
    const chosenId = await chosen.getAttribute('id');
    await expect(cells).toHaveAttribute('aria-activedescendant', chosenId);

    await page.keyboard.press('ArrowRight');
    await expect(chosen).toHaveText('Query');
    await page.keyboard.press('ArrowRight');
    await page.keyboard.press('ArrowRight'); // clamped at the end, still inside
    await expect(chosen).toHaveText('Escalate');
    await page.keyboard.press('ArrowLeft');

    await page.keyboard.press(' ');
    await expect(page.locator('#action-status')).toContainText("Pressed 'query' in column 'Review'");
    await expect(page.locator('#action-status')).toContainText('Presses so far: 1.');
    // Firing leaves.
    await expect(chosen).toHaveCount(0);
    await expect(cells).toHaveAttribute('aria-activedescendant', /r0c6$/);
});

test('inside a cell Enter never fires: it leaves and moves down (KB-22, ADR-0020/0037)', async ({ page }) => {
    const cells = await openCellsAt(page, 6);
    await page.keyboard.press(' ');
    await page.keyboard.press('ArrowRight');

    await page.keyboard.press('Enter');

    await expect(page.locator('#action-status')).toHaveText('No action pressed yet.');
    await expect(cells.locator('.ex-action-chosen')).toHaveCount(0);
    await expect(cells).toHaveAttribute('aria-activedescendant', /r1c6$/);
});

test('Escape leaves an Interactive cell and the grid keeps the keyboard (KB-22, ADR-0037)', async ({ page }) => {
    const cells = await openCellsAt(page, 6);
    await page.keyboard.press(' ');

    await page.keyboard.press('Escape');

    await expect(cells.locator('.ex-action-chosen')).toHaveCount(0);
    await expect(cells).toBeFocused();
    await expect(cells).toHaveAttribute('aria-activedescendant', /r0c6$/);
});

test('Space puts the caret in a Template cell\'s field, and Escape brings the keyboard back (KB-23/KB-24, ADR-0037)', async ({ page }) => {
    const cells = await openCellsAt(page, 4);

    await page.keyboard.press(' ');
    // The row's own note field — the grid asked, the Consumer's control focused itself.
    const note = cells.locator("[id$='r0c4'] input.demo-note");
    await expect(note).toBeFocused();
    await page.keyboard.type('memo');
    await expect(note).toHaveValue('memo');

    await page.keyboard.press('Escape');
    await expect(cells).toBeFocused();
    await expect(cells).toHaveAttribute('aria-activedescendant', /r0c4$/);
    // The text typed there is the field's, and it is still there.
    await expect(note).toHaveValue('memo');
});

test('a held Space fires a one-action cell once (KB-26, ADR-0037)', async ({ page }) => {
    await openCellsAt(page, 5);

    // Playwright marks every keydown after the first as a repeat until the key goes up.
    await page.keyboard.down(' ');
    await page.keyboard.down(' ');
    await page.keyboard.down(' ');
    await page.keyboard.down(' ');
    await page.keyboard.up(' ');

    await expect(page.locator('#action-status')).toContainText("Pressed 'open' in column 'Actions'");
    await expect(page.locator('#action-status')).toContainText('Presses so far: 1.');
});

test('holding Space to enter a cell fires nothing (KB-26, ADR-0037)', async ({ page }) => {
    const cells = await openCellsAt(page, 6);

    // Without the repeat filter the first repeat would reach an Interactive cell as a
    // second Space — and fire the action the user had not yet chosen.
    await page.keyboard.down(' ');
    await page.keyboard.down(' ');
    await page.keyboard.down(' ');
    await page.keyboard.up(' ');

    await expect(cells.locator('.ex-action-chosen')).toHaveText('Approve');
    await expect(page.locator('#action-status')).toHaveText('No action pressed yet.');
});

test('Shift+Tab from after the grid lands on the root, not on a button inside it (A11Y-17, ADR-0037)', async ({ page }) => {
    await page.goto('/cells');
    const cells = page.locator('.ex-grid').first();
    await expect(cells.locator('.ex-action').first()).toBeVisible();

    // Something focusable straight after the grid, as any page would have.
    await page.evaluate(() => {
        const after = document.createElement('button');
        after.id = 'after-grid';
        after.textContent = 'after';
        document.querySelector('.ex-grid').insertAdjacentElement('afterend', after);
    });
    await page.locator('#after-grid').focus();

    await page.keyboard.press('Shift+Tab');

    await expect(cells).toBeFocused();
});

test('the chosen action is outlined, with and without forced colors (UX-14, ADR-0029/0037)', async ({ page }) => {
    for (const forcedColors of ['none', 'active']) {
        await page.emulateMedia({ forcedColors });
        const cells = await openCellsAt(page, 6);
        await page.keyboard.press(' ');

        const outlines = await cells.locator("[id$='r0c6'] .ex-action").evaluateAll((buttons) =>
            buttons.map((b) => ({ chosen: b.classList.contains('ex-action-chosen'), style: getComputedStyle(b).outlineStyle })));
        expect(outlines.filter((o) => o.chosen).map((o) => o.style), forcedColors).toEqual(['solid']);
        expect(outlines.filter((o) => !o.chosen).map((o) => o.style), forcedColors).toEqual(['none', 'none']);
    }
});

test('a press dragged off an action leaves no button holding the keyboard, and Enter fires nothing (KB-27, ADR-0020/0037)', async ({ page }) => {
    await page.goto('/cells');
    const cells = page.locator('.ex-grid').first();
    const open = cells.locator("[id$='r0c5'] .ex-action");
    await expect(open).toBeVisible();
    const box = await open.boundingBox();

    // Press, then leave the button before letting go — the platform's cancel.
    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
    await page.mouse.down();
    await page.mouse.move(box.x + box.width / 2, 5, { steps: 5 });
    await page.mouse.up();
    await expect(page.locator('#action-status')).toHaveText('No action pressed yet.');

    const held = await page.evaluate(() => document.activeElement?.classList.contains('ex-action') ?? false);
    expect(held, 'no grid button holds the keyboard').toBe(false);
    await page.keyboard.press('Enter');
    await expect(page.locator('#action-status')).toHaveText('No action pressed yet.');

    // And an ordinary click still lands: only the press's default was prevented.
    await open.click();
    await expect(page.locator('#action-status')).toContainText("Pressed 'open' in column 'Actions'");
    await expect(page.locator('#action-status')).toContainText('Presses so far: 1.');
});

// A popover takes the keyboard when it opens and gives it back when it closes
// (ADR-0039): before it, nothing opened the column menu from the keyboard, and no
// popover's items or controls could be reached without a pointer.

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

// Inside a popover (ADR-0039's table): the keys are the contents' once they hold DOM
// focus, and what each means is fixed. KB-30 for the menus, KB-31 for the filter panel;
// these run against the built-in Chrome, and WR-5 runs the same outcomes against the
// Wrapper's.

async function enabledMenuItems(page) {
    return grid(page).locator('.ex-popover[role=menu] button[role=menuitem]:not([disabled])').allTextContents();
}

async function activeText(page) {
    return page.evaluate(() => document.activeElement?.textContent ?? null);
}

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

async function openNotionalPanel(page) {
    await clickCell(page, 1, 2);
    await page.keyboard.press('Alt+ArrowDown');
    await expect.poll(() => activeText(page)).toBe('Sort ascending');
    await grid(page).locator('.ex-popover button[role=menuitem]', { hasText: 'Filter' }).click();
    const panel = grid(page).locator('.ex-popover[role=dialog]');
    await expect(panel).toBeVisible();
    await expect.poll(() => activeIsInPopover(page)).toMatchObject({ role: 'dialog', tag: 'SELECT' });
    return panel;
}

test('in the filter panel Tab and Shift+Tab wrap inside it (KB-31, ADR-0039)', async ({ page }) => {
    await openNotionalPanel(page);

    // Operator, value, OK, Cancel, Clear — and round to the operator: twice over, and
    // DOM focus is never anywhere but the panel.
    const seen = [];
    for (let i = 0; i < 10; i++) {
        await page.keyboard.press('Tab');
        await expect.poll(() => activeIsInPopover(page)).toMatchObject({ role: 'dialog' });
        seen.push((await activeIsInPopover(page)).tag + ':' + (await activeText(page)));
    }
    await expect.poll(() => activeIsInPopover(page)).toMatchObject({ tag: 'SELECT' });
    expect(seen.filter((s) => s.startsWith('SELECT')).length, 'the wrap reached the operator again').toBeGreaterThan(0);

    // Shift+Tab off the operator goes to the last control, not out of the panel.
    await page.keyboard.press('Shift+Tab');
    await expect.poll(() => activeText(page)).toBe('Clear');
    await expect(grid(page).locator('.ex-popover[role=dialog]')).toBeVisible();
});

// The rows left after a Notional > 3,000,000 condition (the page's notionals run from
// 1,000,000 up), applied by `apply`. The source re-answers after the panel closes, so the
// count is waited for, not read at once.
async function rowCountAfterFilter(page, apply) {
    const before = await grid(page).getAttribute('aria-rowcount');
    const panel = await openNotionalPanel(page);
    await panel.locator('select').selectOption('GreaterThan');
    await panel.locator('input:not([type])').fill('3000000');
    await apply(panel);
    await expect(grid(page).locator('.ex-popover')).toHaveCount(0);
    await expect.poll(() => activeIsRoot(page)).toBe(true);
    await expect.poll(() => grid(page).getAttribute('aria-rowcount'), 'the condition filters something out')
        .not.toBe(before);
    return grid(page).getAttribute('aria-rowcount');
}

test('Enter in the value field applies exactly what OK applies (KB-31, ADR-0039)', async ({ page }) => {
    const byOk = await rowCountAfterFilter(page, (panel) => panel.locator('button', { hasText: 'OK' }).click());

    await page.reload();
    await expect(grid(page).locator('.ex-row').first()).toBeVisible();
    const byEnter = await rowCountAfterFilter(page, (panel) => panel.locator('input:not([type])').press('Enter'));
    expect(byEnter).toBe(byOk);
});
