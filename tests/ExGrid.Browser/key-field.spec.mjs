import { test, expect, setRoundTrip, alterPage } from './fixtures.mjs';
import { SERVER } from './hosting.mjs';
import {
    sheet, openSheet, cell, clickCell, editor, nameBox, at,
} from './sheet-helpers.mjs';

// The Keyboard Field (ADR-0080). The fifteenth Windows run found that Japanese typed onto a selected
// cell could not compose: DOM focus was on the root, which is not editable, and Chrome and Edge give
// it no input context. On a grid that edits, the keyboard with no edit open is held by a text field
// of the grid's own inside the root. An IME composes there; the field is drawn over the Focus cell
// while it does; and the composition's text opens the Cell Editor in Overwrite holding it (ED-30).
// The field is the grid's one tab stop (A11Y-4) and carries aria-activedescendant (A11Y-5, A11Y-21);
// the root's ring is drawn from the script's mark (KB-12); and the release of Tab ends when DOM focus
// leaves the grid (KB-8, ADR-0012).
//
// The composition here is Chrome's own, made through the DevTools protocol (Input.imeSetComposition
// and Input.insertText, as DC-47 makes it): a real composition in the focused field, with Chrome's
// compositionstart, compositionupdate, input and compositionend — what no synthesised event can
// give, and what the root could not receive at all. What a real IME does with its own keys is a
// Windows run's (docs/specs/exsheet/verify-on-windows-16.md).

test.use({ viewport: { width: 1280, height: 1000 } });

/** This grid's own Keyboard Field, never one of a grid nested in its cells. */
const keyField = (grid) => grid.locator(':scope > .ex-scroller > .ex-spacer > .ex-viewport > .ex-key-field-layer > input.ex-key-field');

/** Composes `steps` in whatever field holds the keyboard, as an IME does, one update at a time. */
async function compose(client, ...steps) {
    for (const text of steps) {
        await client.send('Input.imeSetComposition', { text, selectionStart: text.length, selectionEnd: text.length });
    }
}

/** What the Keyboard Field shows: its value, whether it is drawn, and whether it has the keyboard. */
const fieldState = (grid) => keyField(grid).evaluate((field) => ({
    value: field.value,
    drawn: getComputedStyle(field).opacity === '1',
    focused: document.activeElement === field,
}));

/** Whether two boxes are one, to the pixel. */
const sameBox = (a, b) => Math.abs(a.x - b.x) <= 1 && Math.abs(a.y - b.y) <= 1
    && Math.abs(a.width - b.width) <= 1 && Math.abs(a.height - b.height) <= 1;

/**
 * Where the Focus is, read where ADR-0080 puts it on a grid that edits: the field's
 * aria-activedescendant, with the root naming nothing (A11Y-5), and the Name Box.
 */
async function expectFieldFocusAt(grid, address) {
    const { row, column } = at(address);
    await expect(keyField(grid)).toHaveAttribute('aria-activedescendant', new RegExp(`-r${row}c${column}$`));
    await expect(grid).not.toHaveAttribute('aria-activedescendant', /./);
    await expect(nameBox(grid)).toHaveValue(address);
}

/** Presses a cell and waits until the grid has answered: the Focus is there. */
async function pressAt(grid, address) {
    await clickCell(grid, address);
    await expectFieldFocusAt(grid, address);
}

/**
 * Puts a button straight before and straight after the `index`-th grid root `rootSelector` finds, so
 * a Tab or Shift+Tab out of the grid has somewhere to land that is known; alterPage takes them out as
 * the test ends (ADR-0056).
 */
async function surround(page, rootSelector, index = 0) {
    await alterPage(page, ({ selector, at }) => {
        const root = document.querySelectorAll(selector)[at];
        const before = document.createElement('button');
        before.id = 'before-grid';
        before.textContent = 'before';
        const after = document.createElement('button');
        after.id = 'after-grid';
        after.textContent = 'after';
        root.insertAdjacentElement('beforebegin', before);
        root.insertAdjacentElement('afterend', after);
        return () => {
            before.remove();
            after.remove();
        };
    }, { selector: rootSelector, at: index });
}

/** Whether DOM focus is anywhere inside `grid`. */
const holdsFocus = (grid) => grid.evaluate((root) => root.contains(document.activeElement));

/** The root's ring (KB-12): its computed outline, and the script's mark. */
const ring = (grid) => grid.evaluate((root) => ({
    outline: getComputedStyle(root).outlineStyle !== 'none',
    marked: root.hasAttribute('data-ex-focus-visible'),
}));

/**
 * What Chromium's accessibility tree says of the element holding DOM focus (A11Y-21), over the
 * DevTools protocol: its role, whether it is focused, and the node its aria-activedescendant
 * resolves to — that node's role, and whether it is the element the attribute names.
 */
async function accessibleFocus(page) {
    const client = await page.context().newCDPSession(page);
    try {
        await client.send('DOM.enable');
        await client.send('Accessibility.enable');
        const backendOf = async (expression) => {
            const { result } = await client.send('Runtime.evaluate', { expression });
            if (!result.objectId) {
                return null;
            }
            const { node } = await client.send('DOM.describeNode', { objectId: result.objectId });
            return node.backendNodeId;
        };
        const axNodeOf = async (backendNodeId) => {
            const { nodes } = await client.send('Accessibility.getPartialAXTree', { backendNodeId, fetchRelatives: false });
            return nodes.find((n) => n.backendDOMNodeId === backendNodeId) ?? nodes[0];
        };
        const property = (node, name) => (node.properties ?? []).find((p) => p.name === name);

        const focused = await backendOf('document.activeElement');
        const named = await backendOf("document.getElementById(document.activeElement.getAttribute('aria-activedescendant'))");
        const node = await axNodeOf(focused);
        const related = property(node, 'activedescendant')?.value?.relatedNodes?.[0] ?? null;
        const target = related?.backendDOMNodeId ? await axNodeOf(related.backendDOMNodeId) : null;
        return {
            role: node.role?.value ?? null,
            focused: property(node, 'focused')?.value?.value === true,
            descendantRole: target?.role?.value ?? null,
            descendantIsNamed: related !== null && named !== null && related.backendDOMNodeId === named,
        };
    } finally {
        await client.detach();
    }
}

for (const chrome of ['builtin', 'mud']) {
    test.describe(`/sheet under the ${chrome} Chrome`, () => {
        test.beforeEach(async ({ page }) => {
            await openSheet(page, chrome);
        });

        test('ADR-0080: with a cell selected and no edit open, the keyboard is the Keyboard Field\'s, inside the root, unseen, over the Focus cell', async ({ page }) => {
            const grid = sheet(page);
            await pressAt(grid, 'D10');

            await expect(keyField(grid)).toBeFocused();
            expect(await fieldState(grid)).toEqual({ value: '', drawn: false, focused: true });
            await expect.poll(async () => sameBox(await keyField(grid).boundingBox(), await cell(grid, 'D10').boundingBox()))
                .toBe(true);

            // The keys the core takes still reach the capture-phase listener on the root: the arrow
            // moves the Focus, and the field follows it.
            await page.keyboard.press('ArrowDown');
            await expectFieldFocusAt(grid, 'D11');
            await expect(keyField(grid)).toBeFocused();
            await expect.poll(async () => sameBox(await keyField(grid).boundingBox(), await cell(grid, 'D11').boundingBox()))
                .toBe(true);

            // A character typed without an IME opens Overwrite as before (ADR-0010).
            await page.keyboard.type('12');
            await expect(editor(grid)).toHaveValue('12');
            await expect(editor(grid)).toBeFocused();
            expect((await fieldState(grid)).value).toBe('');
            await page.keyboard.press('Escape');
            await expect(editor(grid)).toHaveCount(0);
            await expect(keyField(grid)).toBeFocused();
        });

        test('ED-30 (i1): a composition on a selected cell is drawn over the cell, nothing moves while it lasts, and its end opens the Cell Editor holding it; Enter commits and moves', async ({ page }) => {
            const grid = sheet(page);
            await pressAt(grid, 'D10');
            const before = await cell(grid, 'D10').textContent();
            const client = await page.context().newCDPSession(page);
            try {
                await compose(client, 'ｋ', 'か', 'かｎ', 'かな');

                await expect.poll(() => fieldState(grid)).toEqual({ value: 'かな', drawn: true, focused: true });
                expect(sameBox(await keyField(grid).boundingBox(), await cell(grid, 'D10').boundingBox())).toBe(true);
                // No edit is open while it lasts, and the Focus has not moved.
                await expect(editor(grid)).toHaveCount(0);
                await expectFieldFocusAt(grid, 'D10');

                // The IME commits: the composition ends, and the Cell Editor opens holding it, in
                // Overwrite, with the keyboard.
                await client.send('Input.insertText', { text: 'かな' });
                await expect(editor(grid)).toHaveValue('かな');
                await expect(editor(grid)).toBeFocused();
                await expect.poll(() => fieldState(grid)).toEqual({ value: '', drawn: false, focused: false });
            } finally {
                await client.detach();
            }

            // Enter commits and moves (ED-30, the fifteenth run's i1).
            await page.keyboard.press('Enter');
            await expect(editor(grid)).toHaveCount(0);
            await expect(cell(grid, 'D10')).toHaveText('かな');
            await expectFieldFocusAt(grid, 'D11');
            await expect(keyField(grid)).toBeFocused();

            // Put back, so the next test finds D10 as this one did.
            await pressAt(grid, 'D10');
            await page.keyboard.press('Delete');
            await expect(cell(grid, 'D10')).toHaveText(before ?? '');
        });

        test('ED-30 (i2): a composition the IME cancels leaves an edit open and empty, as Excel\'s does; Escape then cancels it', async ({ page }) => {
            const grid = sheet(page);
            await pressAt(grid, 'D10');
            const before = await cell(grid, 'D10').textContent();
            const client = await page.context().newCDPSession(page);
            try {
                await compose(client, 'か', 'かな');
                await expect.poll(() => fieldState(grid)).toEqual({ value: 'かな', drawn: true, focused: true });

                // An empty composition is the IME cancelling it.
                await compose(client, '');
                await expect(editor(grid)).toHaveValue('');
                await expect(editor(grid)).toBeFocused();
            } finally {
                await client.detach();
            }

            await page.keyboard.press('Escape');
            await expect(editor(grid)).toHaveCount(0);
            await expect(cell(grid, 'D10')).toHaveText(before ?? '');
            await expectFieldFocusAt(grid, 'D10');
        });

        test('ED-30: a press on another cell while composing puts the composition in the cell it was composed on, commits it there and selects the pressed cell', async ({ page }) => {
            const grid = sheet(page);
            await pressAt(grid, 'D10');
            const before = await cell(grid, 'D10').textContent();
            const client = await page.context().newCDPSession(page);
            try {
                await compose(client, 'か', 'かな');
                await expect.poll(() => fieldState(grid)).toEqual({ value: 'かな', drawn: true, focused: true });

                await clickCell(grid, 'D12');
            } finally {
                await client.detach();
            }

            await expect(cell(grid, 'D10')).toHaveText('かな');
            await expectFieldFocusAt(grid, 'D12');
            await expect(editor(grid)).toHaveCount(0);
            await expect(keyField(grid)).toBeFocused();
            expect(await fieldState(grid)).toEqual({ value: '', drawn: false, focused: true });

            await pressAt(grid, 'D10');
            await page.keyboard.press('Delete');
            await expect(cell(grid, 'D10')).toHaveText(before ?? '');
        });

        test('ED-30 / ADR-0010 (the hold): a second composition ended before the first one\'s editor has the keyboard is appended to it, and a key typed at once waits for both (150 ms on the Server host)', async ({ page }) => {
            const grid = sheet(page);
            await pressAt(grid, 'D10');
            const before = await cell(grid, 'D10').textContent();
            await setRoundTrip(150);
            const client = await page.context().newCDPSession(page);
            try {
                await compose(client, 'か', 'かな');
                await client.send('Input.insertText', { text: 'かな' });
                // At once, inside the round trip on a circuit.
                await compose(client, 'で', 'です');
                await client.send('Input.insertText', { text: 'です' });
                await page.keyboard.press('Enter');
            } finally {
                await client.detach();
            }

            await expect(cell(grid, 'D10')).toHaveText('かなです');
            await expectFieldFocusAt(grid, 'D11');
            await expect(editor(grid)).toHaveCount(0);
            await expect(keyField(grid)).toBeFocused();
            expect(await fieldState(grid)).toEqual({ value: '', drawn: false, focused: true });
            await setRoundTrip(0);

            await pressAt(grid, 'D10');
            await page.keyboard.press('Delete');
            await expect(cell(grid, 'D10')).toHaveText(before ?? '');
        });

        test('ADR-0080 / CP-6: Ctrl+C and Ctrl+V from the Keyboard Field are the grid\'s copy and paste, and type nothing into it', async ({ page, context }) => {
            await context.grantPermissions(['clipboard-read', 'clipboard-write']);
            const grid = sheet(page);
            await page.evaluate(() => navigator.clipboard.writeText('SENTINEL'));
            await pressAt(grid, 'B2');
            await expect(keyField(grid)).toBeFocused();
            await page.keyboard.press('ControlOrMeta+C');
            // On the Server host every copy takes the asynchronous route (ADR-0005).
            await expect.poll(async () => (await page.evaluate(() => navigator.clipboard.readText())).trimEnd(), { timeout: 5000 })
                .toBe('12');

            await page.evaluate(() => navigator.clipboard.writeText('42'));
            await pressAt(grid, 'E8');
            const before = await cell(grid, 'E8').textContent();
            await page.keyboard.press('ControlOrMeta+V');
            await expect(cell(grid, 'E8')).toHaveText('42');
            expect((await fieldState(grid)).value).toBe('');

            await page.keyboard.press('Delete');
            await expect(cell(grid, 'E8')).toHaveText(before ?? '');
        });

        test('A11Y-4 / ADR-0080: the Keyboard Field is the Sheet\'s one tab stop — Tab in from before it and out, Shift+Tab in from after it and out', async ({ page }) => {
            const grid = sheet(page);
            await surround(page, '.ex-grid:has(> .ex-formula-bar)');
            await expect(grid).toHaveAttribute('tabindex', '-1');
            await expect(keyField(grid)).toHaveAttribute('tabindex', '0');

            await page.locator('#before-grid').focus();
            await page.keyboard.press('Tab');
            await expect(keyField(grid)).toBeFocused();
            await page.keyboard.press('Escape');
            await page.waitForTimeout(500);
            await page.keyboard.press('Tab');
            await expect(page.locator('#after-grid')).toBeFocused();

            await page.keyboard.press('Shift+Tab');
            await expect(keyField(grid)).toBeFocused();
            await page.keyboard.press('Escape');
            await page.waitForTimeout(500);
            // From the field, Shift+Tab leaves the grid: the root is no tab stop to land on and pass
            // the keyboard straight back (ADR-0080).
            await page.keyboard.press('Shift+Tab');
            await expect(page.locator('#before-grid')).toBeFocused();
            expect(await holdsFocus(grid)).toBe(false);
        });

        test('KB-8 / ADR-0012 / ADR-0080: Escape, Escape, a press on a control elsewhere on the page, then Tab back into the Sheet — the release has ended, and Tab cycles inside the selection', async ({ page }) => {
            const grid = sheet(page);
            await surround(page, '.ex-grid:has(> .ex-formula-bar)');
            await pressAt(grid, 'D10');
            await page.keyboard.type('k');
            await expect(editor(grid)).toHaveValue('k');
            await page.keyboard.press('Escape');
            await expect(editor(grid)).toHaveCount(0);
            await page.keyboard.press('Escape');
            await page.waitForTimeout(500);
            await expect(keyField(grid)).toBeFocused();

            // The keyboard leaves the grid for a control of the page's, and comes back by Tab.
            await page.locator('#before-grid').click();
            await expect(page.locator('#before-grid')).toBeFocused();
            await page.keyboard.press('Tab');
            await expect(keyField(grid)).toBeFocused();
            await expectFieldFocusAt(grid, 'D10');

            // The release ended as DOM focus left: Tab is the grid's again.
            await page.keyboard.press('Tab');
            await expectFieldFocusAt(grid, 'E10');
            await expect(keyField(grid)).toBeFocused();
        });

        test('KB-8 / ADR-0012 / ADR-0080: Escape, the released Tab out of the Sheet, Shift+Tab back in — Tab cycles inside the selection again', async ({ page }) => {
            const grid = sheet(page);
            await surround(page, '.ex-grid:has(> .ex-formula-bar)');
            await pressAt(grid, 'D10');
            await page.keyboard.press('Escape');
            await page.waitForTimeout(500);
            await page.keyboard.press('Tab');
            await expect(page.locator('#after-grid')).toBeFocused();

            await page.keyboard.press('Shift+Tab');
            await expect(keyField(grid)).toBeFocused();
            await page.keyboard.press('Tab');
            await expectFieldFocusAt(grid, 'E10');
            await expect(keyField(grid)).toBeFocused();
        });

        test('KB-12 / ADR-0080: the root\'s ring shows when the keyboard arrives by Tab, and not after a click', async ({ page }) => {
            const grid = sheet(page);
            await surround(page, '.ex-grid:has(> .ex-formula-bar)');

            await page.locator('#before-grid').focus();
            await page.keyboard.press('Tab');
            await expect(keyField(grid)).toBeFocused();
            await expect.poll(() => ring(grid)).toEqual({ outline: true, marked: true });
            // An arrow moves the Focus; the keyboard stays, and so does the ring.
            await page.keyboard.press('ArrowDown');
            expect(await ring(grid)).toEqual({ outline: true, marked: true });

            // A click on the grid: the keyboard comes back to the field by a press, and no ring.
            await pressAt(grid, 'C3');
            await expect(keyField(grid)).toBeFocused();
            await expect.poll(() => ring(grid)).toEqual({ outline: false, marked: false });

            // Out by a click elsewhere and back by Tab: the ring again.
            await page.locator('#before-grid').click();
            expect(await ring(grid)).toEqual({ outline: false, marked: false });
            await page.keyboard.press('Tab');
            await expect(keyField(grid)).toBeFocused();
            await expect.poll(() => ring(grid)).toEqual({ outline: true, marked: true });

            // An edit holds the keyboard: the field's ring goes with it, and the root's comes back
            // with the keyboard after Escape.
            await page.keyboard.type('x');
            await expect(editor(grid)).toBeFocused();
            await expect.poll(() => ring(grid)).toEqual({ outline: false, marked: false });
            await page.keyboard.press('Escape');
            await expect(keyField(grid)).toBeFocused();
            await expect.poll(() => ring(grid)).toEqual({ outline: true, marked: true });
        });

        test('A11Y-21 / ADR-0080: over the DevTools protocol, the focused node is the Keyboard Field and its active descendant resolves to the Focus cell', async ({ page }) => {
            const grid = sheet(page);
            await pressAt(grid, 'D10');
            await expect(keyField(grid)).toBeFocused();

            const ax = await accessibleFocus(page);

            expect(ax).toEqual({ role: 'textbox', focused: true, descendantRole: 'gridcell', descendantIsNamed: true });
        });
    });
}

test('ADR-0080 / ADR-0018: on /sheets each Sheet has a Keyboard Field of its own, and a composition in one opens an edit in that one only', async ({ page }) => {
    await page.goto('/sheets');
    const left = sheet(page, 0);
    const right = sheet(page, 1);
    await expect(left.locator('.ex-row').first()).toBeVisible();
    await expect(right.locator('.ex-row').first()).toBeVisible();
    await expect(keyField(left)).toHaveCount(1);
    await expect(keyField(right)).toHaveCount(1);

    await pressAt(left, 'B2');
    await expect(keyField(left)).toBeFocused();
    const client = await page.context().newCDPSession(page);
    try {
        await compose(client, 'か', 'かな');
        await expect.poll(() => fieldState(left)).toEqual({ value: 'かな', drawn: true, focused: true });
        expect(await fieldState(right)).toEqual({ value: '', drawn: false, focused: false });
        await client.send('Input.insertText', { text: 'かな' });
    } finally {
        await client.detach();
    }
    await expect(editor(left)).toHaveValue('かな');
    await expect(editor(right)).toHaveCount(0);

    // The keyboard goes to the other Sheet: its own field takes it, and the edit here stands
    // (ADR-0018 section 6).
    await pressAt(right, 'B2');
    await expect(keyField(right)).toBeFocused();
    await expect(editor(left)).toHaveValue('かな');
    await page.keyboard.press('Escape');
    await expect(editor(left)).toHaveValue('かな');

    // A press back on the left Sheet's rows puts the keyboard back in its edit, which Escape then
    // cancels.
    await clickCell(left, 'B2');
    await expect(editor(left)).toBeFocused();
    await page.keyboard.press('Escape');
    await expect(editor(left)).toHaveCount(0);
    await expect(keyField(left)).toBeFocused();
});

test('ADR-0080: focus put on the root itself goes on to the Keyboard Field', async ({ page }) => {
    await openSheet(page);
    const grid = sheet(page);
    await pressAt(grid, 'C3');
    await page.locator('#sheet-revalue').focus();
    await grid.evaluate((root) => root.focus());
    await expect(keyField(grid)).toBeFocused();
});

test('ADR-0080 / ADR-0035 / ED-30: over a cell that does not edit the Keyboard Field is read-only, and no composition starts there', async ({ page }) => {
    await page.goto('/features');
    const grid = page.locator('.ex-grid').first();
    await expect(grid.locator('.ex-row').first()).toBeVisible();
    // Book does not edit; Trader does.
    const book = grid.locator("[id$='r1c0']");
    const trader = grid.locator("[id$='r1c1']");
    await book.click({ force: true });
    await expect(keyField(grid)).toBeFocused();
    await expect(keyField(grid)).toHaveAttribute('readonly', '');
    const client = await page.context().newCDPSession(page);
    try {
        await compose(client, 'か', 'かな');
        await page.waitForTimeout(300);
        expect(await fieldState(grid)).toEqual({ value: '', drawn: false, focused: true });
        await expect(grid.locator('.ex-viewport .ex-editor')).toHaveCount(0);

        await trader.click({ force: true });
        await expect(keyField(grid)).not.toHaveAttribute('readonly', '');
        await compose(client, 'か', 'かな');
        await expect.poll(() => fieldState(grid)).toEqual({ value: 'かな', drawn: true, focused: true });
        await compose(client, '');
    } finally {
        await client.detach();
    }
    await expect(grid.locator('.ex-viewport input.ex-editor')).toHaveValue('');
    await page.keyboard.press('Escape');
    await expect(grid.locator('.ex-viewport .ex-editor')).toHaveCount(0);
});

// The header's ▾ buttons are not tab stops, on any grid, under either Chrome (ADR-0080, decided with
// the user 2026-10-02; A11Y-4): the field stands after the header in the markup, and a Tab into a grid
// that edits went through every column's ▾ before the first cell. /features' first grid edits and
// shows a ▾ per column; its second shows them and edits nothing. A press still opens the popover.
for (const chrome of ['builtin', 'mud']) {
    test.describe(`/features under the ${chrome} Chrome`, () => {
        const grids = (page) => page.locator('.ex-grid');

        test.beforeEach(async ({ page }) => {
            await page.goto(chrome === 'builtin' ? '/features' : `/features?chrome=${chrome}`);
            await expect(grids(page)).toHaveCount(2);
            await expect(grids(page).first().locator('.ex-row').first()).toBeVisible();
        });

        test('A11Y-4 / ADR-0080: Tab from before the first grid, which edits, reaches its field past every ▾, and the next Tab leaves the grid; Shift+Tab likewise', async ({ page }) => {
            const grid = grids(page).first();
            const menus = grid.locator('.ex-header .ex-menu-button');
            expect(await menus.count()).toBeGreaterThan(0);
            for (const tabindex of await menus.evaluateAll((buttons) => buttons.map((b) => b.getAttribute('tabindex')))) {
                expect(tabindex).toBe('-1');
            }
            await surround(page, '.ex-grid', 0);

            await page.locator('#before-grid').focus();
            await page.keyboard.press('Tab');
            await expect(keyField(grid)).toBeFocused();
            // Tab is the grid's until Escape releases it (ADR-0012); the released Tab leaves.
            await page.keyboard.press('Escape');
            await page.waitForTimeout(500);
            await page.keyboard.press('Tab');
            await expect(page.locator('#after-grid')).toBeFocused();

            await page.keyboard.press('Shift+Tab');
            await expect(keyField(grid)).toBeFocused();
            await page.keyboard.press('Escape');
            await page.waitForTimeout(500);
            await page.keyboard.press('Shift+Tab');
            await expect(page.locator('#before-grid')).toBeFocused();

            // Pressed, the ▾ still opens the column's popover.
            await menus.nth(1).click();
            await expect(grid.locator('.ex-popover [role=menu]')).toBeVisible();
            await page.keyboard.press('Escape');
            await expect(grid.locator('.ex-popover')).toHaveCount(0);
        });

        test('A11Y-4 / ADR-0080: on the second grid, which edits nothing, Tab and Shift+Tab reach its root and pass no ▾ on the way out', async ({ page }) => {
            const grid = grids(page).nth(1);
            await expect(grid.locator('.ex-header .ex-menu-button').first()).toBeAttached();
            await expect(keyField(grid)).toHaveCount(0);
            await surround(page, '.ex-grid', 1);

            await page.locator('#before-grid').focus();
            await page.keyboard.press('Tab');
            await expect(grid).toBeFocused();
            await page.keyboard.press('Escape');
            await page.waitForTimeout(500);
            await page.keyboard.press('Tab');
            await expect(page.locator('#after-grid')).toBeFocused();

            // From after the grid, Shift+Tab lands on the root, not on its last ▾.
            await page.keyboard.press('Shift+Tab');
            await expect(grid).toBeFocused();
            await page.keyboard.press('Escape');
            await page.waitForTimeout(500);
            await page.keyboard.press('Shift+Tab');
            await expect(page.locator('#before-grid')).toBeFocused();
        });
    });
}

// A display-only grid has no field, and keeps the keyboard, the tab stop, the ring and
// aria-activedescendant on its root, as before (ADR-0080). /wide edits nothing and shows no menu
// button, so its root is its one tab stop.
test.describe('/wide, a display-only grid', () => {
    const wide = (page) => page.locator('.ex-grid').first();

    test.beforeEach(async ({ page }) => {
        await page.goto('/wide');
        await expect(wide(page).locator('.ex-row').first()).toBeVisible();
        await expect(wide(page).locator('.ex-menu-button')).toHaveCount(0);
    });

    test('ADR-0080 / A11Y-5: a display-only grid has no Keyboard Field and keeps the keyboard and aria-activedescendant on its root', async ({ page }) => {
        const grid = wide(page);
        await expect(grid.locator('input.ex-key-field')).toHaveCount(0);
        await expect(grid).toHaveAttribute('tabindex', '0');
        await grid.locator("[id$='r1c3']").click({ force: true });
        await expect(grid).toBeFocused();
        await expect(grid).toHaveAttribute('aria-activedescendant', /-r1c3$/);
    });

    test('A11Y-4 / ADR-0080: the root is the one tab stop of a display-only grid — Tab in from before it and out, Shift+Tab in from after it and out', async ({ page }) => {
        const grid = wide(page);
        await surround(page, '.ex-grid');

        await page.locator('#before-grid').focus();
        await page.keyboard.press('Tab');
        await expect(grid).toBeFocused();
        await page.keyboard.press('Escape');
        await page.waitForTimeout(500);
        await page.keyboard.press('Tab');
        await expect(page.locator('#after-grid')).toBeFocused();

        await page.keyboard.press('Shift+Tab');
        await expect(grid).toBeFocused();
        await page.keyboard.press('Escape');
        await page.waitForTimeout(500);
        await page.keyboard.press('Shift+Tab');
        await expect(page.locator('#before-grid')).toBeFocused();
    });

    test('KB-8 / ADR-0012 / ADR-0080: on a grid without a field too, a press elsewhere on the page ends the release, and Tab back in cycles inside the selection', async ({ page }) => {
        const grid = wide(page);
        await surround(page, '.ex-grid');
        await grid.locator("[id$='r1c3']").click({ force: true });
        await expect(grid).toHaveAttribute('aria-activedescendant', /-r1c3$/);
        await page.keyboard.press('Escape');
        await page.waitForTimeout(500);

        await page.locator('#before-grid').click();
        await page.keyboard.press('Tab');
        await expect(grid).toBeFocused();
        await page.keyboard.press('Tab');
        await expect(grid).toHaveAttribute('aria-activedescendant', /-r1c4$/);
        await expect(grid).toBeFocused();
    });

    test('KB-12 / ADR-0080: a display-only grid keeps :focus-visible on its root — the ring after Tab, none after a click, and no mark', async ({ page }) => {
        const grid = wide(page);
        await surround(page, '.ex-grid');

        await page.locator('#before-grid').focus();
        await page.keyboard.press('Tab');
        await expect(grid).toBeFocused();
        expect(await ring(grid)).toEqual({ outline: true, marked: false });

        await page.locator('#before-grid').click();
        await grid.locator("[id$='r1c3']").click({ force: true });
        await expect(grid).toBeFocused();
        expect(await ring(grid)).toEqual({ outline: false, marked: false });
    });
});

// The chosen action's button (ADR-0037) named by the field: /cells with Book made editable
// (?editable=1), so the grid holds the keyboard in its Keyboard Field beside the Review column's
// Interactive cells.
for (const chrome of ['builtin', 'mud']) {
    test(`A11Y-21 / ADR-0080 / ADR-0037 under the ${chrome} Chrome: on /cells, while Interactive, the field's active descendant resolves to the chosen action's button`, async ({ page }) => {
        await page.goto(chrome === 'builtin' ? '/cells?editable=1' : `/cells?editable=1&chrome=${chrome}`);
        const grid = page.locator('.ex-grid').first();
        await expect(grid.locator('.ex-row').first()).toBeVisible();
        await expect(keyField(grid)).toHaveCount(1);

        // Review is the seventh column, reached by key from the pinned Book cell, as features.spec
        // reaches it, so nothing depends on where a button is painted. Space enters it, and the
        // first action is chosen.
        await grid.locator("[id$='-r0c0']").click({ force: true });
        await expect(keyField(grid)).toBeFocused();
        for (let i = 0; i < 6; i++) {
            await page.keyboard.press('ArrowRight');
        }
        await expect(keyField(grid)).toHaveAttribute('aria-activedescendant', /-r0c6$/);
        await page.keyboard.press(' ');
        await expect(keyField(grid)).toHaveAttribute('aria-activedescendant', /-r0c6a0$/);
        await expect(grid).not.toHaveAttribute('aria-activedescendant', /./);
        await expect(keyField(grid)).toBeFocused();

        const ax = await accessibleFocus(page);
        expect(ax).toEqual({ role: 'textbox', focused: true, descendantRole: 'button', descendantIsNamed: true });

        // Leaving the cell names it again, and the field still holds the keyboard.
        await page.keyboard.press('Escape');
        await expect(keyField(grid)).toHaveAttribute('aria-activedescendant', /-r0c6$/);
        const back = await accessibleFocus(page);
        expect(back).toEqual({ role: 'textbox', focused: true, descendantRole: 'gridcell', descendantIsNamed: true });
    });
}

test.describe('the Server host only', () => {
    test.skip(!SERVER, 'a round trip exists only on a circuit');

    test('ED-30 / ADR-0010 (the hold): Enter typed straight after a composition ends waits for the editor it opens, and commits it (150 ms)', async ({ page }) => {
        await openSheet(page);
        const grid = sheet(page);
        await pressAt(grid, 'D10');
        const before = await cell(grid, 'D10').textContent();
        await setRoundTrip(150);
        const client = await page.context().newCDPSession(page);
        try {
            await compose(client, 'か', 'かな');
            await client.send('Input.insertText', { text: 'かな' });
            await page.keyboard.press('Enter');
            await page.keyboard.press('Enter');
        } finally {
            await client.detach();
        }
        await expect(cell(grid, 'D10')).toHaveText('かな');
        await expectFieldFocusAt(grid, 'D12');
        await setRoundTrip(0);
        await pressAt(grid, 'D10');
        await page.keyboard.press('Delete');
        await expect(cell(grid, 'D10')).toHaveText(before ?? '');
    });
});
