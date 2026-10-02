import { test, expect, alterPage, setRoundTrip, circuitQuiet } from './fixtures.mjs';
import { SERVER } from './hosting.mjs';
import {
    expectKeyboardOn, keyboardIsOn, keyField, activeDescendant, expectActiveDescendant,
} from './keyboard.mjs';
import { expectSelectionIsCell } from './sheet-helpers.mjs';

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
    // Enter moved the Focus down: the activedescendant names row 1, on the grid's Keyboard Field
    // (ADR-0080).
    const active = await activeDescendant(grid(page));
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
    // The grid still holds the keyboard: its Keyboard Field has DOM focus again (ADR-0080).
    await expectKeyboardOn(grid(page));
});

// KB-8 (ADR-0012, rewritten 2026-10-01): Escape with nothing left to dismiss releases Tab and keeps
// DOM focus. A button either side of the grid, so the page's next and previous elements are known;
// beside the grid is #app on WebAssembly and body on the Server host, so alterPage takes them out
// as the test ends (ADR-0056).
async function aButtonEitherSide(page) {
    await alterPage(page, () => {
        const root = document.querySelector('.ex-grid');
        const before = document.createElement('button');
        before.id = 'before-grid';
        before.textContent = 'before';
        const after = document.createElement('button');
        after.id = 'after-grid';
        after.textContent = 'after';
        root.insertAdjacentElement('beforebegin', before);
        root.insertAdjacentElement('afterend', after);
        return () => { before.remove(); after.remove(); };
    });
}

test('Escape with nothing to dismiss keeps the keyboard, and the next Tab or Shift+Tab leaves for the page (KB-8, ADR-0080)', async ({ page }) => {
    await aButtonEitherSide(page);
    await clickCell(page, 0, 1);
    await page.keyboard.type('Q');
    await page.keyboard.press('Escape');
    await expect(grid(page).locator('input.ex-editor')).toHaveCount(0);

    // The second Escape has nothing to dismiss. Its answer is a round trip away on the Server
    // host, and the grid used to drop DOM focus there: read once it has long landed. A Tab typed
    // before the answer is held behind the Escape and dropped, since script cannot move DOM focus
    // for it (ADR-0010/0021), so each Tab here waits for its Escape's answer.
    await page.keyboard.press('Escape');
    await page.waitForTimeout(500);
    await expectKeyboardOn(grid(page));
    await expectActiveDescendant(grid(page), /r0c1$/);

    // The browser's next element from where the keyboard is, this grid's Keyboard Field: the page's
    // next element after the grid. The header's ▾ buttons are not tab stops, on any grid (ADR-0080,
    // 2026-10-02), so none is reached on the way. No cell takes it, and nothing traps it.
    await page.keyboard.press('Tab');
    await expect(page.locator('#after-grid')).toBeFocused();

    // Shift+Tab from the element after the grid lands in the grid, on its Keyboard Field, the one
    // tab stop: no ▾ is reached on the way in either (A11Y-4, ADR-0080).
    await page.keyboard.press('Shift+Tab');
    await expect(keyField(grid(page))).toBeFocused();

    // Back in the grid the release is spent — it ended when DOM focus left the grid — and Tab
    // cycles inside the selection again (ADR-0012, ADR-0080).
    await page.keyboard.press('Tab');
    await expectKeyboardOn(grid(page));
    await expectActiveDescendant(grid(page), /r0c2$/);

    // Shift+Tab after an Escape goes to the page's previous element, the one before the grid: no
    // ▾ is reached, and the root is no tab stop, so nothing on the way out hands the keyboard back
    // to the field (ADR-0080).
    await page.keyboard.press('Escape');
    await page.waitForTimeout(500);
    await page.keyboard.press('Shift+Tab');
    await expect(page.locator('#before-grid')).toBeFocused();
});

test('Escape, Escape, then a character opens an edit in the selected cell (KB-8)', async ({ page }) => {
    await clickCell(page, 0, 1);
    await page.keyboard.type('Q');
    await page.keyboard.press('Escape');
    await expect(grid(page).locator('input.ex-editor')).toHaveCount(0);
    await page.keyboard.press('Escape');
    await page.keyboard.type('x');

    const editor = grid(page).locator('input.ex-editor');
    await expect(editor).toHaveValue('x');
    await expect(editor).toBeFocused();
    await expectActiveDescendant(grid(page), /r0c1$/);
    await page.keyboard.press('Escape');
    await expect(editor).toHaveCount(0);
    await expect(page.locator('#edit-status')).toContainText('Edited: —');
});

test('Escape, a press on a cell, then Tab cycles inside it (KB-8)', async ({ page }) => {
    await clickCell(page, 0, 1);
    await page.keyboard.press('Escape');
    // Released once the answer has landed; the user who presses the grid has come back to it.
    await page.waitForTimeout(500);
    await clickCell(page, 1, 1);
    await expectActiveDescendant(grid(page), /r1c1$/);

    await page.keyboard.press('Tab');
    await expectKeyboardOn(grid(page));
    await expectActiveDescendant(grid(page), /r1c2$/);
});

test('with a 150 ms round trip, the keys straight after Escape wait for its answer: a Tab is dropped, a character opens an edit (KB-8, ADR-0010, ADR-0080)', async ({ page }) => {
    test.skip(!SERVER, 'WebAssembly answers before the next key: nothing is held long enough to see');
    await aButtonEitherSide(page);
    await clickCell(page, 0, 1);
    await expectActiveDescendant(grid(page), /r0c1$/);
    await setRoundTrip(150);

    // A Tab typed before the Escape's answer is held behind it; released then, it is the browser's,
    // which script cannot carry out (ADR-0021), so it is dropped: the grid keeps the keyboard and
    // the Focus does not move. The release stands for the Tab pressed again.
    await page.keyboard.press('Escape');
    await page.keyboard.press('Tab');
    await page.waitForTimeout(1000);
    await expectKeyboardOn(grid(page));
    await expectActiveDescendant(grid(page), /r0c1$/);
    // The page's next element after the grid: the header's ▾ buttons are not tab stops (ADR-0080).
    await page.keyboard.press('Tab');
    await expect(page.locator('#after-grid')).toBeFocused();

    // A character typed before the answer opens an edit in the selected cell once it lands.
    await grid(page).focus();
    await page.keyboard.press('Escape');
    await page.keyboard.type('x');
    const editor = grid(page).locator('input.ex-editor');
    await expect(editor).toHaveValue('x');
    await expectActiveDescendant(grid(page), /r0c1$/);
    await page.keyboard.press('Escape');
    await expect(editor).toHaveCount(0);
});

test('Escape, an arrow, then Tab cycles inside the selection (KB-8)', async ({ page }) => {
    await clickCell(page, 0, 1);
    await page.keyboard.press('Escape');
    await page.keyboard.press('ArrowDown');
    await expectActiveDescendant(grid(page), /r1c1$/);

    await page.keyboard.press('Tab');
    await expectKeyboardOn(grid(page));
    await expectActiveDescendant(grid(page), /r1c2$/);
});

// KB-44 with KB-8: a held Escape is one press. Its repeats are not another key, so the Tab its press
// released stays released (ADR-0012, rewritten 2026-10-01 and refined the same day with ADR-0070).
test('Escape held with nothing to dismiss, then Tab leaves for the page (KB-8, KB-44)', async ({ page }) => {
    await aButtonEitherSide(page);
    await clickCell(page, 0, 1);

    // Held: the first keydown is the press, and Playwright sends the ones after it as the browser
    // sends a held key's repeats. They wait behind the press for its answer, which released Tab.
    await page.keyboard.down('Escape');
    await page.keyboard.down('Escape');
    await page.keyboard.down('Escape');
    await page.keyboard.up('Escape');
    // Every answer has landed: on the Server host once the circuit is quiet (ADR-0056); the fixed
    // wait is the page's own time, all there is on WebAssembly.
    await page.waitForTimeout(500);
    await circuitQuiet();
    await expectKeyboardOn(grid(page));
    await expectActiveDescendant(grid(page), /r0c1$/);

    // Released, the Tab leaves for the page's next element, the Focus where it was (ADR-0080: the
    // header's ▾ buttons are not tab stops).
    await page.keyboard.press('Tab');
    await expect(page.locator('#after-grid')).toBeFocused();
    await expectActiveDescendant(grid(page), /r0c1$/);
});

test('the clipboard carries both formats, and #### never reaches it (CP-4/CP-5/CP-6/CP-10)', async ({ page }) => {
    // Select the Narrow (####) cell of row 0 and extend to include a text cell.
    await clickCell(page, 0, 2); // Notional
    await page.keyboard.press('Shift+ArrowRight'); // + Narrow (####)

    // The narrowed cell really paints ####.
    await expect(grid(page).locator("[id$='r0c3']")).toContainText('#');

    // A sentinel first, plain text only: a clipboard left holding both flavours by an
    // earlier test must not pass for this copy.
    await page.evaluate(() => navigator.clipboard.writeText('SENTINEL'));
    await page.keyboard.press('ControlOrMeta+C');

    // On WebAssembly this copy took the event route and has landed already; on the Server
    // host there is no synchronous channel, and every copy takes the asynchronous route
    // (ADR-0005) — the write lands a round trip later. Read once it has.
    // A read that overlaps the write is refused by the browser ("Clipboard data has
    // changed"); that is the write landing, not a result, so it reads as nothing yet.
    const readClipboard = () => page.evaluate(async () => {
        try {
            const items = await navigator.clipboard.read();
            const result = {};
            for (const item of items) {
                for (const type of item.types) {
                    result[type] = await (await item.getType(type)).text();
                }
            }
            return result;
        } catch (error) {
            if (error instanceof DOMException && error.name === 'InvalidStateError') {
                return {};
            }
            throw error;
        }
    });
    await expect.poll(async () => Object.keys(await readClipboard()), { timeout: 5000 })
        .toEqual(expect.arrayContaining(['text/plain', 'text/html']));
    const clipboard = await readClipboard();
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
    // Both flavours, as a spreadsheet puts one cell on the clipboard: a one-cell table fills
    // the whole range (ADR-0014).
    await page.evaluate(() => navigator.clipboard.write([new ClipboardItem({
        'text/html': new Blob(['<table><tr><td>fill</td></tr></table>'], { type: 'text/html' }),
        'text/plain': new Blob(['fill\r\n'], { type: 'text/plain' }),
    })]));
    await clickCell(page, 0, 1);
    await page.keyboard.press('Shift+ArrowDown');
    await page.keyboard.press('Shift+ArrowDown');

    await page.keyboard.press('ControlOrMeta+V');

    await expect(page.locator('#paste-status')).toContainText('3 cells from 1x1');
});

// An edit that ends leaves a collapsed caret where the Cell Editor stood, and the next press on
// the rows moves it to the nearest text the page can select, outside the grid. The browser aims
// Ctrl+C and Ctrl+V at the selection rather than at the focused root, so the grid heard neither
// until DOM focus left it and came back (found on /sheet, 2026-09-29). Enter, Tab and Escape each
// end an edit here.
test('CP-6/CP-10/CP-14: Ctrl+C and Ctrl+V reach the grid after an edit ends by Enter, Tab or Escape', async ({ page }) => {
    const copies = async (row, column) => {
        await page.evaluate(() => navigator.clipboard.writeText('SENTINEL'));
        const text = (await grid(page).locator(`[id$='r${row}c${column}']`).textContent()).trim();
        await clickCell(page, row, column);
        await page.keyboard.press('ControlOrMeta+C');
        // On the Server host the copy lands a round trip later (ADR-0005).
        await expect.poll(async () => (await page.evaluate(() => navigator.clipboard.readText())).trimEnd(), { timeout: 5000 })
            .toBe(text);
    };
    const editor = grid(page).locator('input.ex-editor');
    for (const key of ['Enter', 'Tab', 'Escape']) {
        await clickCell(page, 0, 1); // Trader, editable
        await page.keyboard.type('Q');
        await expect(editor).toHaveValue('Q');
        await page.keyboard.press(key);
        await expect(editor).toHaveCount(0);
        await expectKeyboardOn(grid(page));
        await copies(1, 1);
        await copies(2, 1);
    }
    await page.evaluate(() => navigator.clipboard.writeText('Pasted'));
    await clickCell(page, 1, 1);
    await page.keyboard.press('ControlOrMeta+V');
    await expect(page.locator('#paste-status')).toContainText('1 cells from 1x1');
});

test('ADR-0014 (amended 2026-09-29): one value of plain text over a range goes into its top-left alone, and the Selection collapses to it', async ({ page }) => {
    // Text from a text editor: plain text only, no table.
    await page.evaluate(() => navigator.clipboard.writeText('Solo'));
    const below = [await grid(page).locator("[id$='r1c1']").textContent(), await grid(page).locator("[id$='r2c1']").textContent()];
    // Drawn from the bottom, so the Focus is not the top-left: the top-left is taken.
    await clickCell(page, 2, 1);
    await page.keyboard.press('Shift+ArrowUp');
    await page.keyboard.press('Shift+ArrowUp');
    await expect(grid(page).locator('.ex-announce')).toContainText('3 rows by 1 columns selected');

    await page.keyboard.press('ControlOrMeta+V');

    await expect(page.locator('#paste-status')).toContainText('1 cells from 1x1');
    await expect(grid(page).locator("[id$='r0c1']")).toHaveText('Solo');
    await expect(grid(page).locator("[id$='r1c1']")).toHaveText(below[0]);
    await expect(grid(page).locator("[id$='r2c1']")).toHaveText(below[1]);
    await expectActiveDescendant(grid(page), /r0c1$/);
    // That cell alone, row 0 and column 1: B1 in A1 terms (a 1×1 Selection is not announced:
    // ADR-0033).
    await expectSelectionIsCell(grid(page), 'B1');
    // ...and the live region no longer names the range the paste replaced (fifth Windows run).
    await expect(grid(page).locator('.ex-announce')).toHaveText('');
});

test('ADR-0014 (amended 2026-09-29): one cell copied inside the grid still fills the whole range', async ({ page }) => {
    await page.evaluate(() => navigator.clipboard.writeText('SENTINEL'));
    const source = await grid(page).locator("[id$='r0c1']").textContent();
    await clickCell(page, 0, 1);
    await page.keyboard.press('ControlOrMeta+C');
    // On the Server host the copy lands a round trip later (ADR-0005).
    await expect.poll(() => page.evaluate(() => navigator.clipboard.readText()), { timeout: 5000 }).not.toBe('SENTINEL');
    await clickCell(page, 2, 1);
    await page.keyboard.press('Shift+ArrowDown');
    await page.keyboard.press('Shift+ArrowDown');

    await page.keyboard.press('ControlOrMeta+V');

    await expect(page.locator('#paste-status')).toContainText('3 cells from 1x1');
    for (const row of [2, 3, 4]) await expect(grid(page).locator(`[id$='r${row}c1']`)).toHaveText(source);
    await expect(grid(page).locator('.ex-announce')).toContainText('3 rows by 1 columns selected');
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

test('Ctrl+Enter fill across a non-editable column is refused, and says so (CP-16, ADR-0035, ADR-0052)', async ({ page }) => {
    const narrow = grid(page).locator("[id$='r0c3']");
    const notional = grid(page).locator("[id$='r0c2']");
    const narrowBefore = await narrow.textContent();
    const notionalBefore = await notional.textContent();
    await clickCell(page, 0, 2);                   // Notional, which is editable
    await page.keyboard.press('Shift+ArrowRight'); // the Focus stays on Notional (ADR-0052)
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
    // The click's Focus is painted by the render it asked for — a round trip away on the
    // Server host.
    await expectActiveDescendant(grid(page), /r0c1$/);
    const focusBefore = await activeDescendant(grid(page));

    // Registered and awaited before the key is sent, and NOT `once`: a chord arrives
    // as two keydowns — Control first, then PageDown — and a once-listener is spent
    // on the Control. This test used to pass only when its un-awaited registration
    // happened to land between the two keydowns, which read as "never arrived" about
    // half the time on Edge and looked like the browser swallowing the shortcut. Through
    // alterPage, which takes the listener off as the test ends (ADR-0056).
    await alterPage(page, () => {
        let listener;
        let timer;
        window.__kb15 = new Promise((resolve) => {
            listener = (event) => {
                if (event.key === 'PageDown') resolve(event.defaultPrevented);
            };
            document.addEventListener('keydown', listener, { capture: false });
            timer = setTimeout(() => resolve('never arrived'), 3000);
        });
        return () => {
            document.removeEventListener('keydown', listener);
            clearTimeout(timer);
            delete window.__kb15;
        };
    });
    await page.keyboard.press('ControlOrMeta+PageDown');

    expect(await page.evaluate(() => window.__kb15)).toBe(false);
    expect(await activeDescendant(grid(page))).toBe(focusBefore);
});

test('the grid is one tab stop, its Keyboard Field on a grid that edits (A11Y-4, KB-12, ADR-0080)', async ({ page }) => {
    await aButtonEitherSide(page);
    await page.locator('#before-grid').focus();

    // One Tab from the element before the grid reaches its tab stop. The first grid edits, so that
    // is its Keyboard Field, and its root is not one; and the header's ▾ buttons are not tab stops,
    // on any grid, so none comes first (ADR-0080, 2026-10-02).
    const first = grid(page);
    await page.keyboard.press('Tab');
    await expect(keyField(first)).toBeFocused();
    await expect(keyField(first)).toHaveAttribute('tabindex', '0');
    await expect(first).toHaveAttribute('tabindex', '-1');
    // Keyboard focus shows the ring (KB-12). The field matches :focus-visible on every focus, a
    // click's too, so the root's ring is drawn from the script's mark of a keyboard that did not
    // arrive by a press: an attribute, which a render rewriting the root's classes leaves standing
    // (ADR-0080).
    await expect(first).toHaveAttribute('data-ex-focus-visible', /.*/);
    await expect.poll(() => first.evaluate((el) => getComputedStyle(el).outlineStyle)).not.toBe('none');

    // One more Tab is the grid's own key (ADR-0012's cycle): it moves the Focus, and the keyboard
    // stays in the field. No cell and no ▾ takes DOM focus. (A11Y-4's reading, corrected with the
    // user on 2026-10-02: it said this Tab left the grid, which ADR-0012 never did. The sixteenth
    // Windows run, Part C.)
    await page.keyboard.press('Tab');
    await expectKeyboardOn(first);
    const activeIsCell = await page.evaluate(
        () => document.activeElement?.classList?.contains('ex-cell') ?? false);
    expect(activeIsCell).toBe(false);
    // Escape releases Tab (ADR-0012, KB-8), and the next Tab leaves the grid entirely. On a circuit
    // the release is the Escape's answer, a round trip away, and a Tab typed before it is held behind
    // the Escape and dropped (script cannot move DOM focus for it, ADR-0010/0021): wait for it.
    await page.keyboard.press('Escape');
    await circuitQuiet();
    await page.keyboard.press('Tab');
    await expect(page.locator('#after-grid')).toBeFocused();
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
    const active = await activeDescendant(grid(page));
    expect(active).toMatch(/c0$/);
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
    await alterPage(page, () => {
        window.__printableSeen = null;
        const listener = (e) => {
            window.__printableSeen = !e.defaultPrevented;
        };
        document.addEventListener('keydown', listener, { once: true });
        return () => {
            document.removeEventListener('keydown', listener);
            delete window.__printableSeen;
        };
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
    await expectKeyboardOn(cells);
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
    // The keyboard never left the grid, whose root names the chosen button instead: this grid
    // edits nothing, so it has no Keyboard Field (ADR-0080).
    await expectKeyboardOn(cells);
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
    await expectKeyboardOn(cells);
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
    await expectKeyboardOn(cells);
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

    // Something focusable straight after the grid, as any page would have. Beside the grid is
    // #app on WebAssembly and body on the Server host, which leaving the page does not clear:
    // alterPage takes the button out as the test ends (ADR-0056).
    await alterPage(page, () => {
        const after = document.createElement('button');
        after.id = 'after-grid';
        after.textContent = 'after';
        document.querySelector('.ex-grid').insertAdjacentElement('afterend', after);
        return () => after.remove();
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
        // The chosen button is painted by the render Space asked for — a round trip away
        // on the Server host, so read once it has landed.
        await expect(cells.locator("[id$='r0c6'] .ex-action-chosen")).toHaveCount(1);

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
