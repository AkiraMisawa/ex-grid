import { test, expect, alterPage, setRoundTrip, circuitQuiet, settled } from './fixtures.mjs';
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
    // host, and the grid used to drop DOM focus there: read once it has landed. A Tab typed
    // before the answer is held behind the Escape and dropped, since script cannot move DOM focus
    // for it (ADR-0010/0021), so each Tab here waits for its Escape's answer.
    await page.keyboard.press('Escape');
    await circuitQuiet();
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
    await circuitQuiet();
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
    await circuitQuiet();
    await clickCell(page, 1, 1);
    await expectActiveDescendant(grid(page), /r1c1$/);

    await page.keyboard.press('Tab');
    await expectKeyboardOn(grid(page));
    await expectActiveDescendant(grid(page), /r1c2$/);
});

test('with a 150 ms round trip, Escape and Tab typed at once leave the grid once every input is answered; behind an unanswered Enter the Tab is refused aloud, and a character opens an edit (KB-8, ADR-0230, ADR-0010, ADR-0080)', async ({ page }) => {
    test.skip(!SERVER, 'WebAssembly answers before the next key: nothing is held long enough to see');
    await aButtonEitherSide(page);
    await clickCell(page, 0, 1);
    await expectActiveDescendant(grid(page), /r0c1$/);
    await settled(grid(page));
    await setRoundTrip(150);

    // Every input answered, and nothing for an Escape to peel: the gate takes the release at the
    // Escape's keydown, and the Tab typed at once leaves, as one typed after the answer does
    // (ADR-0230 section 1). The page's next element after the grid: the header's ▾ buttons are not
    // tab stops (ADR-0080).
    await page.keyboard.press('Escape');
    await page.keyboard.press('Tab');
    await expect(page.locator('#after-grid')).toBeFocused();

    // Behind an Enter not yet answered, which could have added a layer, the Escape is held, and the
    // Tab behind it. Released then, the Tab is the browser's, which script cannot carry out
    // (ADR-0021): it is refused aloud, the grid keeps the keyboard, and the release stands for the
    // Tab pressed again (ADR-0230 section 1 (b)).
    await grid(page).focus();
    await expectKeyboardOn(grid(page));
    await settled(grid(page));
    await page.keyboard.press('Enter');
    await page.keyboard.press('Escape');
    await page.keyboard.press('Tab');
    await expect(grid(page).locator('.ex-announce')).toContainText('Tab was pressed before Escape had let the keyboard leave the grid');
    await circuitQuiet();
    await expectKeyboardOn(grid(page));
    await expectActiveDescendant(grid(page), /r1c1$/);
    await page.keyboard.press('Tab');
    await expect(page.locator('#after-grid')).toBeFocused();

    // A character typed before the answer opens an edit in the selected cell once it lands.
    await grid(page).focus();
    await page.keyboard.press('Escape');
    await page.keyboard.type('x');
    const editor = grid(page).locator('input.ex-editor');
    await expect(editor).toHaveValue('x');
    await expectActiveDescendant(grid(page), /r1c1$/);
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
    // Every answer has landed: on the Server host once the circuit is quiet (ADR-0056); WebAssembly
    // answers before the next key.
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

test('a refusal is announced in the root\'s live region in the built-in words, and again when repeated (A11Y-16, ADR-0203, ERR-2)', async ({ page }) => {
    // The built-in Chrome words every refusal, and the grid's one live region says it (ADR-0203). The
    // page shows the reason by name beside it, and is no live region of its own: the refusal is said
    // once.
    const region = grid(page).locator('.ex-announce');
    await expect(region).toHaveAttribute('role', 'status');
    await expect(page.locator('#paste-refused-status')).not.toHaveAttribute('role', /.+/);
    // Every change to the region, counted: identical text written again would be no change, and the
    // second refusal is the one the user needs, having just tried again.
    await alterPage(page, () => {
        const said = document.querySelector('.ex-grid .ex-announce');
        const seen = { changes: 0 };
        const observer = new MutationObserver((records) => { seen.changes += records.length; });
        observer.observe(said, { childList: true, characterData: true, subtree: true });
        window.__exRefusalChanges = seen;
        return () => {
            observer.disconnect();
            delete window.__exRefusalChanges;
        };
    });
    const changes = () => page.evaluate(() => window.__exRefusalChanges.changes);

    await page.evaluate(() => navigator.clipboard.writeText('intruder'));
    await clickCell(page, 0, 0);                   // Book, never declared editable
    await page.keyboard.press('Shift+ArrowRight');
    // Straight on, while the range's description is still settling: the refusal is newer, and the
    // description stands down rather than replacing it when the settle runs out (principle 6).
    await page.keyboard.press('ControlOrMeta+V');

    const words = 'Nothing was changed: the selection covers Book, which cannot be edited. Select cells only in columns that can be edited.';
    await expect(region).toHaveText(words);
    await expect(page.locator('#paste-refused-status')).toContainText('TargetNotEditable');
    await circuitQuiet();
    const first = await changes();

    await page.keyboard.press('ControlOrMeta+V');
    await expect.poll(changes).toBeGreaterThan(first);
    await expect(region).toHaveText(words);
});

// What a copy did, read from beside the grid (ADR-0005's routes): every `copy` event the document
// heard after the grid's own listener — what it carried and whether the grid took it — and every
// call to the asynchronous Clipboard API, with how its promise settled. A write the browser would
// only make after asking leaves its promise pending, or rejects it: either is a prompt, and neither
// settles as written. Through alterPage, since it wraps a native and listens on document (ADR-0056).
async function watchCopyRoutes(page) {
    await alterPage(page, () => {
        const seen = { events: [], writes: [] };
        window.__copyRoutes = seen;
        const write = navigator.clipboard.write;
        const writeText = navigator.clipboard.writeText;
        const watched = (name, original) => function (...args) {
            const call = { name, state: 'pending' };
            seen.writes.push(call);
            const promise = original.apply(this, args);
            promise.then(() => { call.state = 'written'; }, (error) => { call.state = `rejected: ${error?.name ?? error}`; });
            return promise;
        };
        navigator.clipboard.write = watched('write', write);
        navigator.clipboard.writeText = watched('writeText', writeText);
        const onCopy = (event) => seen.events.push({
            taken: event.defaultPrevented,
            plain: event.clipboardData?.getData('text/plain') ?? '',
        });
        document.addEventListener('copy', onCopy);
        return () => {
            navigator.clipboard.write = write;
            navigator.clipboard.writeText = writeText;
            document.removeEventListener('copy', onCopy);
            delete window.__copyRoutes;
        };
    });
}
const copyRoutes = (page) => page.evaluate(() => JSON.parse(JSON.stringify(window.__copyRoutes)));

// The state the browser holds for a clipboard permission, as the page would ask it.
const permission = (page, name) => page.evaluate(async (n) => (await navigator.permissions.query({ name: n })).state, name);

// Takes back the clipboard permissions the file's beforeEach granted, after leaving a sentinel, so
// the copy that follows is made as a user's page makes it: with nothing the test granted, under the
// browser's own defaults (CP-6, CP-19). Chromium's default holds a sanitised write granted to the
// focused tab, which is the property under test, and a read at "prompt": a read made now would wait
// on a prompt, so "prompt" here says the test's grants are gone. Granted, `clipboard-read` would
// have let a write through on its own.
async function grantsTakenBack(page, context) {
    await page.evaluate(() => navigator.clipboard.writeText('SENTINEL'));
    await context.clearPermissions();
    expect(await permission(page, 'clipboard-read'), 'the test\'s grants are taken back before the copy').toBe('prompt');
}

// Reads the clipboard once the copy has been made, granting the read only then.
async function clipboardAfterwards(page, context) {
    await context.grantPermissions(['clipboard-read']);
    return (await page.evaluate(() => navigator.clipboard.readText())).replace(/\r\n/g, '\n').trimEnd();
}

const cellText = async (page, row, column) => (await grid(page).locator(`[id$='r${row}c${column}']`).textContent()).trim();

test('CP-6 / ADR-0005: a Ctrl+C inside the Window writes with no permission granted and no prompt, by the copy event on WebAssembly', async ({ page, context }) => {
    const expected = `${await cellText(page, 0, 1)}\n${await cellText(page, 1, 1)}`;
    await grantsTakenBack(page, context);
    await watchCopyRoutes(page);
    await clickCell(page, 0, 1);
    await page.keyboard.press('Shift+ArrowDown');
    await page.keyboard.press('ControlOrMeta+C');

    if (SERVER) {
        // No synchronous channel on a circuit: every copy takes the asynchronous route, and what
        // CP-6 protects there is the missing prompt (ADR-0005, SRV-2). The grid takes the event and
        // starts its write in it; the write settles as written, unasked.
        await expect.poll(async () => (await copyRoutes(page)).writes, { timeout: 5000 })
            .toEqual([{ name: 'write', state: 'written' }]);
        expect((await copyRoutes(page)).events.map((e) => e.taken)).toEqual([true]);
    } else {
        // The copy event route: the grid set both flavours on the event itself, and the
        // asynchronous Clipboard API was never called.
        const { events, writes } = await copyRoutes(page);
        expect(events, 'the grid took the copy event and set its data on it').toEqual([{ taken: true, plain: expect.any(String) }]);
        expect(events[0].plain.replace(/\r\n/g, '\n').trimEnd()).toBe(expected);
        expect(writes, 'navigator.clipboard was not called: the copy took the event route').toEqual([]);
    }
    await circuitQuiet();
    await expect(page.locator('#copy-status')).toHaveText('Copy refused: —');
    expect(await clipboardAfterwards(page, context), 'the copy landed on the clipboard').toBe(expected);
});

test('CP-19 / ADR-0005/0036: a menu copy writes with no permission granted and no prompt, by the asynchronous route, with headers (CP-17)', async ({ page, context }) => {
    await grantsTakenBack(page, context);
    await watchCopyRoutes(page);
    await clickCell(page, 0, 1);

    await grid(page).locator("[id$='r0c1']").click({ button: 'right', force: true });
    await grid(page).locator('[role=menu] button[role=menuitem]')
        .filter({ hasText: 'Copy with headers' }).click();

    // A menu item's click fires no copy event, so a one-cell selection takes the asynchronous
    // Clipboard API too (ADR-0005, "A menu copy has no copy event"). Chromium grants a write to the
    // focused tab, so it settles as written; a prompt would leave it pending or reject it, and
    // this fails then, which is the finding ADR-0005 asks layer 3 to record.
    await expect.poll(async () => (await copyRoutes(page)).writes, { timeout: 5000 })
        .toEqual([{ name: 'write', state: 'written' }]);
    expect((await copyRoutes(page)).events, 'no copy event was fired or needed').toEqual([]);
    await expect(page.locator('#copy-status')).toHaveText('Copy refused: —');
    expect(await clipboardAfterwards(page, context)).toBe(`Trader\n${await cellText(page, 0, 1)}`);
});

// CP-10 (ADR-0005): the page's text, selected with the mouse, is never what Ctrl+C on the grid
// copies. The browser aims a copy at the element holding the document's selection, and at the
// focused element only while there is none, so a selection left standing outside the grid would
// take the command from it. Dragged across the paragraph above the second instance, then a grid
// takes the keyboard: by a press on a cell of the grid that edits (its Keyboard Field, ADR-0080),
// and on the display-only second instance by a press, and by Tab, which leaves the page's
// selection standing as Ctrl+C arrives.
async function selectTextWithMouse(page, paragraph) {
    const box = await paragraph.boundingBox();
    await page.mouse.move(box.x + 2, box.y + box.height / 2);
    await page.mouse.down();
    await page.mouse.move(box.x + box.width - 2, box.y + box.height / 2, { steps: 8 });
    await page.mouse.up();
    // spec-lint: early-read: the document's text selection is the browser's own, made by the drag across a paragraph outside every grid, and no answer of a grid's changes it
    const selected = await page.evaluate(() => document.getSelection().toString());
    expect(selected.length, 'the mouse selected the paragraph\'s text').toBeGreaterThan(10);
    return selected;
}

for (const way of ['a press on the grid that edits', 'a press on the display-only grid', 'Tab back to the display-only grid']) {
    test(`CP-10 / ADR-0005: text selected with the mouse, then ${way}: Ctrl+C copies the grid's cell, not the text`, async ({ page }) => {
        const second = way !== 'a press on the grid that edits';
        const target = second ? page.locator('.ex-grid').nth(1) : grid(page);
        const cell = target.locator("[id$='r0c1']");
        const expected = (await cell.textContent()).trim();
        await page.evaluate(() => navigator.clipboard.writeText('SENTINEL'));

        if (way.startsWith('Tab')) {
            // The Focus first, then the text: Tab brings the keyboard back to the grid with its
            // Focus where it was, and Ctrl+C is the first key the grid hears.
            await cell.click({ force: true });
            await expectKeyboardOn(target);
            const selected = await selectTextWithMouse(page, page.locator('h2 + p'));
            await page.keyboard.press('Tab');
            await expectKeyboardOn(target);
            // The page's selection still stands as Ctrl+C arrives: it is the grid's to drop.
            expect(await page.evaluate(() => document.getSelection().toString())).toBe(selected);
        } else {
            await selectTextWithMouse(page, page.locator('h2 + p'));
            await cell.click({ force: true });
            await expectKeyboardOn(target);
        }
        await page.keyboard.press('ControlOrMeta+C');

        // On the Server host the copy lands a round trip later (ADR-0005).
        await expect.poll(async () => (await page.evaluate(() => navigator.clipboard.readText())).trimEnd(), { timeout: 5000 })
            .toBe(expected);
    });
}

// CP-14 (ADR-0005, ADR-0050 item 10): a paste reads text/html before text/plain, and from Excel's
// markup a number's x:num, the raw value at full precision, where the shown text and the plain
// flavour hold the display, already rounded. The fields arrive typed: an x:num field as the raw
// number under the invariant culture, a cell's shown text as text in the source's culture. The
// payload is Excel's shape as its HTML export writes it; the paste event's data is the browser's
// own, from the clipboard, through the real Ctrl+V.
test('CP-14 / ADR-0005: an Excel-shaped paste arrives typed and at full precision, from x:num and not the shown text', async ({ page }) => {
    const html = [
        '<html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:x="urn:schemas-microsoft-com:office:excel"',
        ' xmlns="http://www.w3.org/TR/REC-html40"><head><meta name=ProgId content=Excel.Sheet>',
        '<meta name=Generator content="Microsoft Excel 15"></head><body>',
        '<table border=0 cellpadding=0 cellspacing=0><!--StartFragment-->',
        '<tr height=19><td height=19>Ishida</td>',
        '<td class=xl66 align=right x:num="1234567.891234">1,234,567.89</td></tr>',
        '<tr height=19><td height=19>Mori</td>',
        '<td class=xl66 align=right x:num="0.1234567890123">0.12</td></tr>',
        '<!--EndFragment--></table></body></html>',
    ].join('');
    await page.evaluate((markup) => navigator.clipboard.write([new ClipboardItem({
        'text/html': new Blob([markup], { type: 'text/html' }),
        'text/plain': new Blob(['Ishida\t1,234,567.89\r\nMori\t0.12\r\n'], { type: 'text/plain' }),
    })]), html);
    await clickCell(page, 0, 1); // Trader; Notional to its right
    await page.keyboard.press('Shift+ArrowDown');
    await page.keyboard.press('Shift+ArrowRight');

    await page.keyboard.press('ControlOrMeta+V');

    await expect(page.locator('#paste-status')).toContainText('4 cells from 2x2');
    await expect(page.locator('#paste-fields')).toHaveText(
        'Fields: Ishida:ShownText | 1234567.891234:Invariant | Mori:ShownText | 0.1234567890123:Invariant');
});

// CP-14's second half (ADR-0005): desktop Excel's own copy carries no x:num. Its HTML holds each
// cell's shown text, as text/plain does (verification/2026-09-27-windows-excel-2/
// clipboard-probe-chrome.json, the shape declarations.spec.mjs pastes on /sheet). Then the shown
// text is what arrives, marked as shown text to be read in the source's culture, and precision is
// what was shown: here the demo's Consumer reads every field as a number, 1,234.50 among them.
const excelShownTextColumn = (cells) => [
    '<html xmlns:v="urn:schemas-microsoft-com:vml"\r\nxmlns:o="urn:schemas-microsoft-com:office:office"\r\n',
    'xmlns:x="urn:schemas-microsoft-com:office:excel"\r\nxmlns="http://www.w3.org/TR/REC-html40">\r\n\r\n<head>\r\n',
    '<meta http-equiv=Content-Type content="text/html; charset=utf-8">\r\n<meta name=ProgId content=Excel.Sheet>\r\n',
    '<meta name=Generator content="Microsoft Excel 15">\r\n<style>\r\n<!--table\r\n\t{mso-displayed-decimal-separator:"\\.";\r\n',
    '\tmso-displayed-thousand-separator:"\\,";}\r\ntd\r\n\t{mso-number-format:General;\r\n\twhite-space:nowrap;}\r\n',
    '.xl66\r\n\t{mso-number-format:Standard;}\r\n-->\r\n</style>\r\n</head>\r\n\r\n<body link="#467886" vlink="#96607D">\r\n\r\n',
    "<table border=0 cellpadding=0 cellspacing=0 width=87 style='border-collapse:\r\n collapse'>\r\n<!--StartFragment-->\r\n",
    " <col width=87 style='mso-width-source:userset'>\r\n",
    ...cells.map((text) => ` <tr height=19 style='height:14.5pt'>\r\n  <td height=19 class=xl66 align=right style='height:14.5pt'>${text}</td>\r\n </tr>\r\n`),
    '<!--EndFragment-->\r\n</table>\r\n\r\n</body>\r\n\r\n</html>\r\n',
].join('');

test('CP-14 / ADR-0005: desktop Excel\'s paste, shown text with no x:num, arrives as the shown text and is read as numbers', async ({ page }) => {
    const shown = ['6.00', '1,234.50', '0.12'];
    await page.evaluate(({ markup, text }) => navigator.clipboard.write([new ClipboardItem({
        'text/html': new Blob([markup], { type: 'text/html' }),
        'text/plain': new Blob([text], { type: 'text/plain' }),
    })]), { markup: excelShownTextColumn(shown), text: shown.map((t) => `${t}\r\n`).join('') });
    await clickCell(page, 0, 2); // Notional
    await page.keyboard.press('Shift+ArrowDown');
    await page.keyboard.press('Shift+ArrowDown');

    await page.keyboard.press('ControlOrMeta+V');

    await expect(page.locator('#paste-fields')).toHaveText('Fields: 6.00:ShownText | 1,234.50:ShownText | 0.12:ShownText');
    // Each shown text read as a number by the Consumer, as typed: all three written, and painted
    // back as the numbers they are, at the precision they were shown.
    await expect(page.locator('#paste-status')).toContainText('3 cells from 3x1, 3 applied');
    await expect(grid(page).locator("[id$='r0c2']")).toHaveText('6.00');
    await expect(grid(page).locator("[id$='r1c2']")).toHaveText('1234.50');
    await expect(grid(page).locator("[id$='r2c2']")).toHaveText('0.12');
});

// CON-8 (ADR-0005): a copy that fails in .NET is never silent. A Consumer's CopyAnswer that throws
// (/features?copythrows=1) fails the build of the payload. On WebAssembly a Ctrl+C asks for it
// synchronously, through the channel the probe at attach found, and the failure is said on the
// console and not run again down the asynchronous route, as a missing channel would be: nothing is
// written. A menu copy takes the asynchronous route on every host, and on the Server host so does
// every copy; its failure is said too. The clipboard keeps what it held either way.
test.describe(() => {
    test.use({ expectedErrors: [/\[ex-grid\] the grid failed to build a copy/] });

    test('CON-8 / ADR-0005: a copy whose payload fails to build says so on the console on either route, and writes nothing', async ({ page }) => {
        await page.goto('/features?copythrows=1');
        await expect(grid(page).locator('.ex-row').first()).toBeVisible();
        await page.evaluate(() => navigator.clipboard.writeText('SENTINEL'));
        await watchCopyRoutes(page);
        const said = () => page.waitForEvent('console', {
            predicate: (m) => m.type() === 'error' && m.text().includes('[ex-grid] the grid failed to build a copy'),
            timeout: 5000,
        });

        await clickCell(page, 0, 1);
        let failure = said();
        await page.keyboard.press('ControlOrMeta+C');
        await failure;
        await circuitQuiet();
        if (!SERVER) {
            expect((await copyRoutes(page)).writes, 'the failure was not run again down the asynchronous route').toEqual([]);
        }
        expect(await page.evaluate(() => navigator.clipboard.readText())).toBe('SENTINEL');

        failure = said();
        await grid(page).locator("[id$='r0c1']").click({ button: 'right', force: true });
        await grid(page).locator('[role=menu] button[role=menuitem]').filter({ hasText: /^Copy$/ }).click();
        await failure;
        await circuitQuiet();
        expect(await page.evaluate(() => navigator.clipboard.readText())).toBe('SENTINEL');
    });
});

/** Waits until the first grid's selected range covers the cells from [row, column] to [row, column], whole. */
async function expectRangeCovers(page, [fromRow, fromColumn], [toRow, toColumn]) {
    const from = await grid(page).locator(`[id$='r${fromRow}c${fromColumn}']`).boundingBox();
    const to = await grid(page).locator(`[id$='r${toRow}c${toColumn}']`).boundingBox();
    const want = { x: from.x, y: from.y, right: to.x + to.width, bottom: to.y + to.height };
    await expect.poll(async () => {
        const box = await grid(page).locator('.ex-selection .ex-range').boundingBox();
        if (!box) {
            return 'not painted';
        }
        const near = (p, q) => Math.abs(p - q) <= 1.5;
        return near(box.x, want.x) && near(box.y, want.y) && near(box.x + box.width, want.right) && near(box.y + box.height, want.bottom)
            ? 'covers'
            : JSON.stringify({ box, want });
    }).toBe('covers');
}

// Overwrite is Excel's Enter mode (ADR-0010): the Shift+arrows and Shift+Home commit the entry and
// then extend the Selection, as they do with no edit open, where they once selected the editor's
// text (the seventeenth Windows run, Part C, case 7). Caret keeps them the editor's.
test('in Overwrite Shift+Home and Shift+↓ commit and extend the Selection; in Caret Shift+Home selects the text (ED-2, ADR-0010)', async ({ page }) => {
    const editor = grid(page).locator('input.ex-editor');
    await clickCell(page, 2, 1); // Trader, editable
    await page.keyboard.type('AB');
    await expect(editor).toHaveValue('AB');

    await page.keyboard.press('Shift+Home');

    await expect(editor).toHaveCount(0);
    await expect(page.locator('#edit-status')).toContainText('Trader=AB');
    await expectRangeCovers(page, [2, 0], [2, 1]);
    expect(await activeDescendant(grid(page))).toMatch(/r2c1$/);

    // Shift+↓ the same way: typed onto the Focus, which the extension left where it was, and the
    // range the Selection holds grows a row, as Shift+↓ grows it with no edit open (ADR-0012).
    await page.keyboard.type('CD');
    await expect(editor).toHaveValue('CD');
    await page.keyboard.press('Shift+ArrowDown');
    await expect(editor).toHaveCount(0);
    await expect(page.locator('#edit-status')).toContainText('Trader=CD');
    await expectRangeCovers(page, [2, 0], [3, 1]);
    expect(await activeDescendant(grid(page))).toMatch(/r2c1$/);

    // In Caret (F2) the same key is the editor's: it selects the text, and nothing commits.
    await page.keyboard.press('F2');
    await expect(editor).toBeVisible();
    await page.keyboard.press('End');
    await page.keyboard.press('Shift+Home');
    await expect.poll(() => editor.evaluate((input) => [input.selectionStart, input.selectionEnd]))
        .toEqual([0, (await editor.inputValue()).length]);
    await expect(editor).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(editor).toHaveCount(0);
});

// The Primary Modifier's arrows the same way (ADR-0010, ED-2): Excel's Enter mode committed on Ctrl+→
// and moved, and on Ctrl+Shift+→ committed and extended (the readings of 2026-10-12, R1 (vii)); the
// other directions follow by generalisation. In Caret Ctrl+← is the editor's.
test('in Overwrite Ctrl+← commits and moves to the row\'s first column, Ctrl+Shift+← commits and extends to it; in Caret Ctrl+← moves the caret (ED-2, ADR-0010)', async ({ page }) => {
    const editor = grid(page).locator('input.ex-editor');
    await clickCell(page, 2, 1); // Trader, editable
    await page.keyboard.type('EF');
    await expect(editor).toHaveValue('EF');

    await page.keyboard.press('Control+ArrowLeft');

    await expect(editor).toHaveCount(0);
    await expect(page.locator('#edit-status')).toContainText('Trader=EF');
    await expect.poll(() => activeDescendant(grid(page))).toMatch(/r2c0$/);

    await clickCell(page, 2, 1);
    await page.keyboard.type('GH');
    await expect(editor).toHaveValue('GH');
    await page.keyboard.press('Control+Shift+ArrowLeft');
    await expect(editor).toHaveCount(0);
    await expect(page.locator('#edit-status')).toContainText('Trader=GH');
    await expectRangeCovers(page, [2, 0], [2, 1]);
    expect(await activeDescendant(grid(page))).toMatch(/r2c1$/);

    // In Caret (F2) the same key is the editor's: the caret moves, and nothing commits.
    await page.keyboard.press('F2');
    await expect(editor).toBeVisible();
    // Typed behind F2, End and Ctrl+← may reach the edit before or after F2's answer: held, they are
    // replayed as the field would have handled them, so the caret lands at 0 either way (ADR-0010).
    await page.keyboard.press('End');
    await page.keyboard.press('Control+ArrowLeft');
    await expect.poll(() => editor.evaluate((input) => input.selectionStart)).toBe(0);
    await expect(editor).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(editor).toHaveCount(0);
});

// A word move, and Shift's extension, in Caret on a value of two words. Where the field's own word
// moves land, on the platform the browser runs on: back, at the start of a word; forward, at its end,
// or on Windows at the start of the next word. The modifier is Ctrl, and Option on an Apple platform,
// where ⌘'s arrows go to the text's ends instead.
async function wordKeys(page) {
    const platform = await page.evaluate(() => navigator.userAgentData?.platform ?? navigator.platform ?? '');
    return {
        word: /mac|iphone|ipad|ipod/i.test(platform) ? 'Alt' : 'Control',
        // 'alpha beta': from the start, forward a word.
        forward: /^win/i.test(platform) ? 6 : 5,
    };
}

/** Commits 'alpha beta' into Trader on row 2 and leaves the Focus on that cell, no edit open. */
async function alphaBetaInTrader(page) {
    const editor = grid(page).locator('input.ex-editor');
    await clickCell(page, 2, 1); // Trader, editable
    await page.keyboard.type('alpha beta');
    await expect(editor).toHaveValue('alpha beta');
    await page.keyboard.press('Enter');
    await expect(editor).toHaveCount(0);
    await expect(page.locator('#edit-status')).toContainText('Trader=alpha beta');
    await clickCell(page, 2, 1);
    await expectActiveDescendant(grid(page), /r2c1$/);
    return editor;
}

const selectionOf = (editor) => editor.evaluate((input) => [input.selectionStart, input.selectionEnd]);
const extensionOf = (editor) => editor.evaluate((input) => [input.selectionStart, input.selectionEnd, input.selectionDirection]);

// The field's own word moves, typed behind no key the grid is answering: the edit stands open and
// holds the keyboard before the first of them, so the browser carries them out itself. What the held
// keys of the next test are measured against.
test('in Caret Ctrl+← and Ctrl+→ move the caret by a word, with Shift extend the selection, and Ctrl+Backspace and Ctrl+Delete delete a word, in the field itself (ED-2, ADR-0010)', async ({ page }) => {
    const { word, forward } = await wordKeys(page);
    const editor = await alphaBetaInTrader(page);
    await page.keyboard.press('F2');
    await expect(editor).toBeFocused();
    await circuitQuiet();

    await page.keyboard.press('End');
    await page.keyboard.press(`${word}+ArrowLeft`);
    await expect.poll(() => selectionOf(editor)).toEqual([6, 6]);
    await page.keyboard.press('Shift+End');
    await expect.poll(() => extensionOf(editor)).toEqual([6, 10, 'forward']);

    await page.keyboard.press('Home');
    await page.keyboard.press(`${word}+ArrowRight`);
    await expect.poll(() => selectionOf(editor)).toEqual([forward, forward]);
    await page.keyboard.press(`${word}+Shift+ArrowRight`);
    await expect.poll(() => extensionOf(editor)).toEqual([forward, 10, 'forward']);

    await page.keyboard.press('End');
    await page.keyboard.press(`${word}+Backspace`);
    await expect(editor).toHaveValue('alpha ');
    await expect.poll(() => selectionOf(editor)).toEqual([6, 6]);

    // Escape gives the text back; the edit reopened holds the keyboard before the next key.
    await page.keyboard.press('Escape');
    await expect(editor).toHaveCount(0);
    await page.keyboard.press('F2');
    await expect(editor).toBeFocused();
    await expect(editor).toHaveValue('alpha beta');
    await circuitQuiet();
    await page.keyboard.press('Home');
    await page.keyboard.press(`${word}+Delete`);
    await expect(editor).toHaveValue('alpha beta'.slice(forward));
    await expect.poll(() => selectionOf(editor)).toEqual([0, 0]);

    await page.keyboard.press('Escape');
    await expect(editor).toHaveCount(0);
    await expect(grid(page).locator("[id$='r2c1']")).toHaveText('alpha beta');
});

// The same keys held for certain: typed straight after F2 (or Escape and F2) over a 150 ms round trip,
// every one waits for the edit to open, and is then replayed into it. A held key the field would have
// handled itself is handled as it would have (ADR-0010): Ctrl+← and Shift+End were once dropped and
// collapsed there, so where the caret landed turned on whether the keys arrived before F2's answer —
// Ctrl+← stood at 2 in 'GH' on Edge, WebAssembly, where the test above it waits for 0 (ED-2).
test('with a 150 ms round trip, End, Ctrl+←, Shift+End typed straight after F2 move and extend as the field would; so do Ctrl+→, Ctrl+Shift+→, Ctrl+Backspace and Ctrl+Delete (ED-2, ADR-0010)', async ({ page }) => {
    test.skip(!SERVER, 'WebAssembly answers F2 before most keys: nothing is held for certain');
    const { word, forward } = await wordKeys(page);
    const editor = await alphaBetaInTrader(page);
    await setRoundTrip(150);

    await page.keyboard.press('F2');
    await page.keyboard.press('End');
    await page.keyboard.press(`${word}+ArrowLeft`);
    await page.keyboard.press('Shift+End');
    await expect.poll(() => extensionOf(editor)).toEqual([6, 10, 'forward']);
    await expect(editor).toHaveValue('alpha beta');

    await page.keyboard.press('Escape');
    await page.keyboard.press('F2');
    await page.keyboard.press('Home');
    await page.keyboard.press(`${word}+ArrowRight`);
    await page.keyboard.press(`${word}+Shift+ArrowRight`);
    await expect.poll(() => extensionOf(editor)).toEqual([forward, 10, 'forward']);

    await page.keyboard.press('Escape');
    await page.keyboard.press('F2');
    await page.keyboard.press(`${word}+Backspace`);
    await expect(editor).toHaveValue('alpha ');
    await expect.poll(() => selectionOf(editor)).toEqual([6, 6]);

    await page.keyboard.press('Escape');
    await page.keyboard.press('F2');
    await page.keyboard.press('Home');
    await page.keyboard.press(`${word}+Delete`);
    await expect(editor).toHaveValue('alpha beta'.slice(forward));
    await expect.poll(() => selectionOf(editor)).toEqual([0, 0]);

    await page.keyboard.press('Escape');
    await expect(editor).toHaveCount(0);
    await expect(grid(page).locator("[id$='r2c1']")).toHaveText('alpha beta');
});

test('a Reject holds the commit Shift+↓ asks for in Overwrite, and the Selection stays (ED-2, ED-15, ADR-0034)', async ({ page }) => {
    const editor = grid(page).locator('input.ex-editor');
    await clickCell(page, 0, 2); // Notional, which carries the verdict
    await page.keyboard.type('abc');

    await page.keyboard.press('Shift+ArrowDown');

    await expect(editor).toHaveValue('abc');
    await expect(editor).toHaveAttribute('aria-invalid', 'true');
    await expect(page.locator('#edit-status')).not.toContainText('Notional=abc');
    await expect(grid(page).locator('.ex-selection .ex-range')).toHaveCount(0);
    await page.keyboard.press('Escape');
    await expect(editor).toHaveCount(0);
    expect(await activeDescendant(grid(page))).toMatch(/r0c2$/);
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
            // spec-lint: fixed-wait: a cap inside the page, not a wait: the promise resolves when PageDown's keydown reaches document, and this only names a key that never came
            timer = setTimeout(() => resolve('never arrived'), 3000);
        });
        return () => {
            document.removeEventListener('keydown', listener);
            clearTimeout(timer);
            delete window.__kb15;
        };
    });
    await page.keyboard.press('ControlOrMeta+PageDown');

    // spec-lint: early-read: awaits the listener's promise, settled by the keydown inside the press itself, so it cannot be read before the key is heard
    expect(await page.evaluate(() => window.__kb15)).toBe(false);
    // spec-lint: early-read: the assertion that the Focus did NOT move; no state marks that nothing came, and the Settled Mark (PR #134, settled(grid)) will stand before it
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
    // Escape releases Tab (ADR-0012, KB-8), and the next Tab leaves the grid entirely: after a Tab,
    // which only moves the Focus, the release is taken at the Escape's keydown, and a Tab typed at
    // once leaves (ADR-0230 section 1).
    await page.keyboard.press('Escape');
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
