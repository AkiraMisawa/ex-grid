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

// An Apple platform, where Meta is the Primary Modifier and Option types characters of its own.
const onApple = (page) => page.evaluate(() => /mac|iphone|ipad|ipod/i.test(navigator.userAgentData?.platform ?? navigator.platform ?? ''));

// An AltGr character reports Control and Alt held together, and is how several layouts type @ { [ €:
// the script's gate passes it as typing, where Control or Alt alone is a shortcut the browser keeps
// (ED-13, ADR-0010). The C# half is CellEditorTests'; this is the script's, with real keys.
test('ED-13: Control and Alt held together open the editor with the character; either alone opens nothing (ADR-0010)', async ({ page }) => {
    test.skip(await onApple(page), 'On an Apple platform Option types a character of its own (Option+q is œ), and AltGr is not a key there');
    const editor = grid(page).locator('input.ex-editor');
    await clickCell(page, 0, 1); // Trader, editable
    await expectActiveDescendant(grid(page), /r0c1$/);

    await page.keyboard.press('Alt+q');
    await page.keyboard.press('Control+q');
    await settled(grid(page));
    await expect(editor).toHaveCount(0);

    await page.keyboard.press('Control+Alt+q');
    await expect(editor).toHaveValue('q');
    await page.keyboard.press('Escape');
    await expect(editor).toHaveCount(0);
    await expect(grid(page).locator("[id$='r0c1']")).not.toHaveText('q');
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

test('with a 150 ms round trip, Escape and Tab typed at once leave the grid, behind an Enter too, and a character opens an edit (KB-8, ADR-0230, ADR-0010, ADR-0080)', async ({ page }) => {
    test.skip(!SERVER, 'WebAssembly answers before the next key: nothing is held long enough to see');
    await aButtonEitherSide(page);
    await clickCell(page, 0, 1);
    await expectActiveDescendant(grid(page), /r0c1$/);
    await settled(grid(page));
    await setRoundTrip(150);

    // Nothing for an Escape to peel: the release is taken at the Escape's keydown, and the Tab
    // typed at once leaves, as one typed after a round trip does (ADR-0230 section 1). The page's next element after the grid: the header's ▾ buttons are not
    // tab stops (ADR-0080).
    await page.keyboard.press('Escape');
    await page.keyboard.press('Tab');
    await expect(page.locator('#after-grid')).toBeFocused();

    // Behind an Enter too: the Script Core decides each key in the browser as it is typed, so no
    // answer is awaited from .NET, the Enter has moved the Focus by the time the Escape is judged,
    // and the Tab leaves. The refusal aloud of ADR-0230 section 1 (b) was for a key held behind an
    // answer of the Razor grid's, which nothing here waits for.
    await grid(page).focus();
    await expectKeyboardOn(grid(page));
    await settled(grid(page));
    await page.keyboard.press('Enter');
    await page.keyboard.press('Escape');
    await page.keyboard.press('Tab');
    await expect(page.locator('#after-grid')).toBeFocused();
    await expectActiveDescendant(grid(page), /r1c1$/);

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
    // what the contract is about. The raw value is the value's own invariant text: the
    // Script Core's number, a C# decimal included once it crossed, keeps no trailing places,
    // so it is the shortest text of the same number (ADR-0350's values).
    expect(clipboard['text/html']).toContain('<td>1000000</td><td>1000000</td>');
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

test('ED-19: after a refused fill the editor stands, and Enter then raises exactly one Edit Intent, for the one cell; the fill raised none (ADR-0035)', async ({ page }) => {
    const counts = page.locator('#intent-count');
    await expect(counts).toHaveText('Intents: 0 edits, 0 pastes');
    await clickCell(page, 0, 2);                   // Notional, editable
    await page.keyboard.press('Shift+ArrowRight'); // over Narrow, which is not
    await page.keyboard.type('7');
    await page.keyboard.press('ControlOrMeta+Enter');
    await expect(page.locator('#paste-refused-status')).toContainText('TargetNotEditable');
    await expect(grid(page).locator('.ex-editor')).toHaveValue('7');

    await page.keyboard.press('Enter');

    await expect(grid(page).locator('.ex-editor')).toHaveCount(0);
    await expect(page.locator('#edit-status')).toContainText('Notional=7');
    await settled(grid(page));
    await expect(counts).toHaveText('Intents: 1 edits, 0 pastes');
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

test('CP-6 / ADR-0005: a Ctrl+C inside the Window writes with no permission granted and no prompt, by the copy event', async ({ page, context }) => {
    const expected = `${await cellText(page, 0, 1)}\n${await cellText(page, 1, 1)}`;
    await grantsTakenBack(page, context);
    await watchCopyRoutes(page);
    await clickCell(page, 0, 1);
    await page.keyboard.press('Shift+ArrowDown');
    await page.keyboard.press('ControlOrMeta+C');

    // The copy event route on both hosts: the Script Core holds the Window's rows in the browser, so
    // even on a circuit the grid set both flavours on the event itself, and the asynchronous
    // Clipboard API was never called (ADR-0005, ADR-0350 section 7).
    const { events, writes } = await copyRoutes(page);
    expect(events, 'the grid took the copy event and set its data on it').toEqual([{ taken: true, plain: expect.any(String) }]);
    expect(events[0].plain.replace(/\r\n/g, '\n').trimEnd()).toBe(expected);
    expect(writes, 'navigator.clipboard was not called: the copy took the event route').toEqual([]);
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
// (/features?copythrows=1) fails the build of the payload: .NET answers that it failed, naming the
// exception, and the grid refuses the copy and raises the failure on the page, by a Ctrl+C or the
// menu's Copy alike. The clipboard keeps what it held either way, and the host logs the Consumer's exception: in the
// browser's console on WebAssembly, in the host's log on Server.
test.describe(() => {
    const logged = /the Consumer's copy answer threw, and the copy wrote nothing/;
    test.use({
        expectedPageErrors: [/\[ex-grid\] the grid failed to build a copy/],
        ...(SERVER ? { expectedHostLog: [logged] } : { expectedErrors: [logged] }),
    });

    test('CON-8 / ADR-0005: a copy whose payload fails to build says so on the page by either gesture, and writes nothing', async ({ page }) => {
        await page.goto('/features?copythrows=1');
        await expect(grid(page).locator('.ex-row').first()).toBeVisible();
        await page.evaluate(() => navigator.clipboard.writeText('SENTINEL'));
        const said = () => page.waitForEvent('pageerror', {
            predicate: (error) => error.message.includes('[ex-grid] the grid failed to build a copy'),
            timeout: 5000,
        });

        await clickCell(page, 0, 1);
        let failure = said();
        await page.keyboard.press('ControlOrMeta+C');
        await failure;
        await circuitQuiet();
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

// On an Apple platform ⌘+← and ⌘+→ go to the text's ends in a field, where elsewhere Ctrl's arrows
// move by a word (wordKeys). In Caret they are the field's, as the word moves are: the edit stands
// and the caret lands at the start and the end. Pressed only where the platform gives them that
// meaning; KB-3's macOS row runs it (ticket 280).
test('on an Apple platform, in Caret ⌘+← and ⌘+→ move the caret to the text\'s start and end, in the field itself (ED-2, KB-3, ticket 280)', async ({ page }) => {
    test.skip(!(await onApple(page)), '⌘+←/→ go to the text\'s ends only on an Apple platform; elsewhere Ctrl\'s word moves are pressed above');
    const editor = await alphaBetaInTrader(page);
    await page.keyboard.press('F2');
    await expect(editor).toBeFocused();
    await circuitQuiet();

    await page.keyboard.press('Meta+ArrowLeft');
    await expect.poll(() => selectionOf(editor)).toEqual([0, 0]);
    await page.keyboard.press('Meta+ArrowRight');
    await expect.poll(() => selectionOf(editor)).toEqual([10, 10]);
    await page.keyboard.press('Meta+Shift+ArrowLeft');
    await expect.poll(() => extensionOf(editor)).toEqual([0, 10, 'backward']);

    await page.keyboard.press('Escape');
    await expect(editor).toHaveCount(0);
    await expect(grid(page).locator("[id$='r2c1']")).toHaveText('alpha beta');
});

test('on an Apple platform, with a 150 ms round trip, ⌘+← and ⌘+→ typed straight after F2 land the caret at the text\'s start and end, as typed slowly (ED-2, KB-3, ADR-0010, ticket 280)', async ({ page }) => {
    test.skip(!(await onApple(page)), '⌘+←/→ go to the text\'s ends only on an Apple platform');
    test.skip(!SERVER, 'WebAssembly answers F2 before most keys: nothing is held for certain');
    const editor = await alphaBetaInTrader(page);
    await setRoundTrip(150);

    await page.keyboard.press('F2');
    await page.keyboard.press('Meta+ArrowLeft');
    await expect.poll(() => selectionOf(editor)).toEqual([0, 0]);
    await expect(editor).toHaveValue('alpha beta');

    await page.keyboard.press('Escape');
    await page.keyboard.press('F2');
    await page.keyboard.press('Home');
    await page.keyboard.press('Meta+ArrowRight');
    await expect.poll(() => selectionOf(editor)).toEqual([10, 10]);

    await page.keyboard.press('Escape');
    await expect(editor).toHaveCount(0);
    await expect(grid(page).locator("[id$='r2c1']")).toHaveText('alpha beta');
});

// Ctrl+., Ctrl+Backspace and Shift+Backspace are the active cell's keys with no edit open (ADR-0052);
// with one open, none is taken from it: the field answers Backspace's forms as a field does, Ctrl+.
// does nothing there, and the range and its Focus stand (KB-42).
test('KB-42: with an edit open, Ctrl+. walks no corner, and Ctrl+Backspace and Shift+Backspace delete in the field; the range stands (ADR-0052)', async ({ page }) => {
    const { word } = await wordKeys(page);
    const editor = grid(page).locator('input.ex-editor');
    await clickCell(page, 0, 1); // Trader, editable
    await page.keyboard.press('Shift+ArrowDown');
    await expectActiveDescendant(grid(page), /r0c1$/);
    await page.keyboard.type('alpha beta');
    await expect(editor).toHaveValue('alpha beta');

    await page.keyboard.press('ControlOrMeta+Period');
    await page.keyboard.press('Shift+Backspace');
    await expect(editor).toHaveValue('alpha bet');
    await page.keyboard.press(`${word}+Backspace`);
    await expect(editor).toHaveValue('alpha ');
    await expectActiveDescendant(grid(page), /r0c1$/);

    // The range stood under the edit: with it gone, Ctrl+. walks to its next corner.
    await page.keyboard.press('Escape');
    await expect(editor).toHaveCount(0);
    await page.keyboard.press('ControlOrMeta+Period');
    await expectActiveDescendant(grid(page), /r1c1$/);
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

test('KB-11: arrows typed in a Template Column\'s input move its caret, and the grid\'s Selection stays where it was (ADR-0010/0020)', async ({ page }) => {
    await page.goto('/cells');
    const cells = page.locator('.ex-grid').first();
    await expect(cells.locator('.ex-row').first()).toBeVisible();
    const note = cells.locator('input.demo-note').first();
    await note.click({ force: true });
    await expect(note).toBeFocused();
    await note.pressSequentially('memo');
    await expect(note).toHaveValue('memo');
    const focus = await activeDescendant(cells);
    const caret = () => note.evaluate((input) => [input.selectionStart, input.selectionEnd]);

    await page.keyboard.press('ArrowLeft');
    await page.keyboard.press('ArrowLeft');
    await expect.poll(caret).toEqual([2, 2]);
    await page.keyboard.press('Shift+ArrowLeft');
    await expect.poll(caret).toEqual([1, 2]);
    await page.keyboard.press('ArrowRight');
    await page.keyboard.press('ArrowRight');
    await expect.poll(caret).toEqual([3, 3]);
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('ArrowUp');
    await settled(cells);

    await expect(note).toBeFocused();
    await expect(note).toHaveValue('memo');
    expect(await activeDescendant(cells)).toBe(focus);
});

// DC-63 on /cells?dblclick=1, where the page listens to a double click and adds a Mark Column
// (column 7) after the others: Book 0 (Editable under ?editable=1), Close of business 1,
// Intraday 2, Limit used 3 (a Template with no control), Note 4 (a Template with a field),
// Actions 5, Review 6. Row 0 is a Group row, rows 1 to 5 its Details, and a Mark is painted on a
// Detail row only (ADR-0043). What was heard is written below the grid with a count.

test('DC-63: a double click where no edit opens is heard once with its cell and the Focus placed there; an Editable cell opens its edit, an open edit and a control in an Action, Template or Mark cell raise nothing (ADR-0063)', async ({ page }) => {
    await page.goto('/cells?editable=1&dblclick=1');
    const cells = page.locator('.ex-grid').first();
    await expect(cells.locator('.ex-row').first()).toBeVisible();
    const heard = page.locator('#double-click-status');
    const editor = cells.locator('input.ex-editor');
    await expect(heard).toHaveText('No double click heard yet.');

    await cells.locator("[id$='r2c2']").dblclick({ force: true });
    await expect(heard).toHaveText('Double click heard on row 2, column 2. Heard so far: 1.');
    await expectActiveDescendant(cells, /r2c2$/);
    await expect(editor).toHaveCount(0);

    // The empty part of a Template cell names that cell.
    await cells.locator("[id$='r3c3']").dblclick({ force: true });
    await expect(heard).toHaveText('Double click heard on row 3, column 3. Heard so far: 2.');
    await expectActiveDescendant(cells, /r3c3$/);

    // Where an edit opens, the edit is the double click's meaning; while it is open, a double
    // click is the editor's.
    await cells.locator("[id$='r2c0']").dblclick({ force: true });
    await expect(editor).toBeVisible();
    await editor.dblclick();
    await expect(editor).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(editor).toHaveCount(0);

    // A control inside a cell takes its own presses and the double click stops at its cell:
    // the action fires twice, the Mark is pressed, and no cell is named.
    await cells.locator("[id$='r2c4'] input.demo-note").dblclick();
    await cells.locator("[id$='r2c5'] .ex-action").dblclick();
    await expect(page.locator('#action-status')).toHaveText(/Presses so far: 2\.$/);
    // The Mark Column lies past the Viewport's right edge, and a column is painted once it is in view.
    await cells.locator('.ex-scroller').evaluate((scroller) => { scroller.scrollLeft = scroller.scrollWidth; });
    await cells.locator("[id$='r4c7'] .ex-mark").dblclick();
    await expect(page.locator('#action-status')).toHaveText(`Mark pressed on ${await cells.locator("[id$='r4c0']").innerText()}.`);
    await settled(cells);
    await circuitQuiet();
    await expect(heard).toHaveText('Double click heard on row 3, column 3. Heard so far: 2.');
    await expect(editor).toHaveCount(0);
});

test('DC-63: without a listener, a double click where no edit opens places the Focus and changes nothing else (ADR-0063, DC-1)', async ({ page }) => {
    await page.goto('/cells?editable=1');
    const cells = page.locator('.ex-grid').first();
    await expect(cells.locator('.ex-row').first()).toBeVisible();
    const before = await cells.locator('.ex-viewport').innerText();

    await cells.locator("[id$='r2c2']").dblclick({ force: true });
    await expectActiveDescendant(cells, /r2c2$/);
    await settled(cells);
    await circuitQuiet();

    await expect(cells.locator('.ex-editor')).toHaveCount(0);
    await expect(page.locator('#double-click-status')).toHaveCount(0);
    await expect(page.locator('#action-status')).toHaveText('No action pressed yet.');
    expect(await cells.locator('.ex-viewport').innerText()).toBe(before);
});

// DC-62 on /cells?editable=1&leave=1, where the page counts OnLeave below the grid and does
// nothing else with it. A button either side of the grid shows where Tab goes once released.

test('DC-62: with OnLeave declared, Escape with nothing left to dismiss raises it once, keeps the DOM focus and releases Tab; an Escape that cancels an edit, closes a popover, leaves an Interactive cell or returns from a control raises nothing, and a held one raises once (ADR-0070, KB-8)', async ({ page }) => {
    await page.goto('/cells?editable=1&leave=1');
    const cells = page.locator('.ex-grid').first();
    await expect(cells.locator('.ex-row').first()).toBeVisible();
    await aButtonEitherSide(page);
    const heard = page.locator('#leave-status');
    await expect(heard).toHaveText('No leave heard yet.');

    // An edit cancelled.
    await cells.locator("[id$='r2c0']").click({ force: true });
    await page.keyboard.type('Q');
    await expect(cells.locator('input.ex-editor')).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(cells.locator('.ex-editor')).toHaveCount(0);

    // The Context Menu closed.
    await page.keyboard.press('ArrowRight');
    await page.keyboard.press('Shift+F10');
    await expect(cells.locator('.ex-popover')).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(cells.locator('.ex-popover')).toHaveCount(0);

    // An Interactive cell left: Space enters Review (column 6), Escape leaves it.
    for (let i = 1; i < 6; i++) {
        await page.keyboard.press('ArrowRight');
    }
    await expectActiveDescendant(cells, /r2c6$/);
    await page.keyboard.press('Space');
    await expect(cells.locator('.ex-action-chosen')).toHaveCount(1);
    await page.keyboard.press('Escape');
    await expect(cells.locator('.ex-action-chosen')).toHaveCount(0);

    // A control inside a cell returned from.
    const note = cells.locator("[id$='r2c4'] input.demo-note");
    await note.click();
    await expect(note).toBeFocused();
    await page.keyboard.press('Escape');
    await expectKeyboardOn(cells);

    await settled(cells);
    await circuitQuiet();
    await expect(heard).toHaveText('No leave heard yet.');

    // Nothing left to dismiss: heard once, and the grid keeps the keyboard with its Focus.
    const focus = await activeDescendant(cells);
    await page.keyboard.press('Escape');
    await expect(heard).toHaveText('Leave heard. Heard so far: 1.');
    await circuitQuiet();
    await expectKeyboardOn(cells);
    expect(await activeDescendant(cells)).toBe(focus);
    // Tab was released with it: the next Tab is the page's.
    await page.keyboard.press('Tab');
    await expect(page.locator('#after-grid')).toBeFocused();

    // Back in the grid, a held Escape raises once for its press, and its repeats nothing more.
    await page.keyboard.press('Shift+Tab');
    await expectKeyboardOn(cells);
    await page.keyboard.down('Escape');
    await page.keyboard.down('Escape');
    await page.keyboard.down('Escape');
    await page.keyboard.up('Escape');
    await expect(heard).toHaveText('Leave heard. Heard so far: 2.');
    await settled(cells);
    await circuitQuiet();
    await expect(heard).toHaveText('Leave heard. Heard so far: 2.');
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

// The live region's writes, counted from now: each change of its text, in order, kept on the region
// element itself (inside this test's own grid, gone with it) until writesTo reads and stops it.
async function countWrites(region) {
    await region.evaluate((el) => {
        el.exWrites = [];
        el.exWritesObserver = new MutationObserver(() => {
            if (el.exWrites.at(-1) !== el.textContent) {
                el.exWrites.push(el.textContent);
            }
        });
        el.exWritesObserver.observe(el, { childList: true, characterData: true, subtree: true });
    });
}
const writesTo = (region) => region.evaluate((el) => {
    el.exWritesObserver.disconnect();
    return el.exWrites;
});

test('A11Y-10: arrows with no Shift move the Focus and leave the live region unwritten (ADR-0033)', async ({ page }) => {
    const region = grid(page).locator('.ex-announce');
    await clickCell(page, 2, 1);
    await expectActiveDescendant(grid(page), /r2c1$/);
    await expect(region).toHaveText('');
    await countWrites(region);

    for (const key of ['ArrowDown', 'ArrowRight', 'ArrowUp', 'ArrowLeft', 'ArrowDown']) {
        await page.keyboard.press(key);
    }
    await expectActiveDescendant(grid(page), /r3c1$/);
    await settled(grid(page));

    expect(await writesTo(region)).toEqual([]);
    await expect(region).toHaveText('');
});

test('A11Y-9: a drag across 200 rows writes the live region once, after it settles (ADR-0033)', async ({ page }) => {
    const region = grid(page).locator('.ex-announce');
    const scroller = grid(page).locator('.ex-scroller');
    await clickCell(page, 0, 1);
    await expectActiveDescendant(grid(page), /r0c1$/);
    await countWrites(region);

    const start = await grid(page).locator("[id$='r0c1']").boundingBox();
    const box = await scroller.boundingBox();
    await page.mouse.move(start.x + start.width / 2, start.y + start.height / 2);
    await page.mouse.down();
    // Held in the band at the Viewport's bottom edge (ADR-0008): the rows scroll under the pointer
    // and the Extent follows them, past row 200 of 24px.
    await page.mouse.move(start.x + start.width / 2, box.y + box.height - 18, { steps: 10 });
    await expect.poll(() => scroller.evaluate((el) => el.scrollTop), { timeout: 20_000 }).toBeGreaterThan(200 * 24);
    await page.mouse.up();
    await settled(grid(page));
    await expect(region).toContainText('rows by 1 columns selected');

    const writes = await writesTo(region);
    expect(writes).toHaveLength(1);
    expect(Number(writes[0].match(/^(\d+) rows/)?.[1])).toBeGreaterThan(200);
});

test('A11Y-6: a wheel scroll that takes the Focus out of the paint clears aria-activedescendant, and scrolling back restores it, never scrolling to the Focus itself (ADR-0033)', async ({ page }) => {
    const scroller = grid(page).locator('.ex-scroller');
    await clickCell(page, 1, 1);
    await expectActiveDescendant(grid(page), /r1c1$/);
    const box = await scroller.boundingBox();
    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);

    await page.mouse.wheel(0, 60 * 24);
    await expect(grid(page).locator("[id$='r1c1']")).toHaveCount(0);
    await settled(grid(page));
    // Cleared, never naming an id that is not in the document.
    expect(await activeDescendant(grid(page))).toBeFalsy();
    // And the grid did not bring the Focus back: the page stays where the wheel left it.
    const after = await scroller.evaluate((el) => el.scrollTop);
    expect(after).toBeGreaterThan(24 * 10);

    await page.mouse.wheel(0, -after);
    await expect(grid(page).locator("[id$='r1c1']")).toHaveCount(1);
    await expectActiveDescendant(grid(page), /r1c1$/);
});

// Holds a drag from the cell at (row, 1) in the edge band of the Viewport's top or bottom (ADR-0008)
// until the edge scroll has carried the Viewport to that end of the page and the Selection has
// reached `cells`; then moves once more inside the band, so the boundary is pressed against after it
// is reached, and lets go. The count is read whole, so a Selection that ran past the page fails.
async function dragIntoBand(page, row, edge, cells) {
    const status = grid(page).locator('.ex-status');
    const scroller = grid(page).locator('.ex-scroller');
    const start = await grid(page).locator(`[id$='r${row}c1']`).boundingBox();
    const box = await scroller.boundingBox();
    await page.mouse.move(start.x + start.width / 2, start.y + start.height / 2);
    await page.mouse.down();
    const y = edge === 'bottom' ? box.y + box.height - 18 : box.y + 28 + 6;
    await page.mouse.move(start.x + start.width / 2, y, { steps: 10 });
    await expect(status).toContainText(`${cells} cells selected`, { timeout: 20_000 });
    await expect.poll(() => scroller.evaluate((el, end) => end === 'bottom'
        ? el.scrollTop + el.clientHeight >= el.scrollHeight - 1
        : el.scrollTop === 0, edge), { timeout: 20_000 }).toBe(true);
    await page.mouse.move(start.x + start.width / 2 + 4, y + (edge === 'bottom' ? 2 : -2));
    await settled(grid(page));
    await page.mouse.up();
    await settled(grid(page));
    await expect(status).toContainText(new RegExp(`(^|\\D)${cells} cells selected`));
}

test('SL-11: a drag held against either boundary of a page does not turn it, and the Selection stops at the page (ADR-0015)', async ({ page }) => {
    await page.goto('/features?paged=1');
    const pager = grid(page).locator('.ex-pager');
    await expect(pager).toContainText('1 / 8');

    // The first page's last row, from its first.
    await clickCell(page, 0, 1);
    await dragIntoBand(page, 0, 'bottom', 50);
    await expect(pager).toContainText('1 / 8');
    await expect(grid(page).locator('.ex-status')).toContainText(/(^|\D)50 cells selected/);

    // The second page's first row, from a row below it.
    await pager.locator('button').nth(1).click();
    await expect(pager).toContainText('2 / 8');
    await expect(grid(page).locator("[id$='r55c1']")).toBeVisible();
    await clickCell(page, 55, 1);
    await dragIntoBand(page, 55, 'top', 6);
    await expect(pager).toContainText('2 / 8');
    await expect(grid(page).locator('.ex-status')).toContainText(/(^|\D)6 cells selected/);
});

// Off an Apple platform Meta is the OS's own key, not the Primary Modifier: a key held with it stays
// out of the table on every path, the editor's included (KB-3, ADR-0012). Win+↓ is a window manager's
// chord, and Meta+Enter must not fill as Ctrl+Enter does. Where Meta is Command, KB-3's macOS row
// presses Cmd+Enter instead (sheet-vs-excel.spec.mjs's ControlOrMeta+Enter).
test('KB-3: off an Apple platform, Meta+↓ moves no Focus, and Meta+Enter in an open edit over a range fills nothing (ADR-0012)', async ({ page }) => {
    test.skip(await onApple(page), 'Meta is Command on an Apple platform, and the Primary Modifier');
    const editor = grid(page).locator('input.ex-editor');
    await clickCell(page, 2, 2); // Notional, editable
    await expectActiveDescendant(grid(page), /r2c2$/);

    await page.keyboard.press('Meta+ArrowDown');
    await settled(grid(page));
    await expectActiveDescendant(grid(page), /r2c2$/);

    await page.keyboard.press('Shift+ArrowDown');
    await page.keyboard.type('7');
    await expect(editor).toHaveValue('7');
    await page.keyboard.press('Meta+Enter');
    await settled(grid(page));
    await expect(editor).toHaveValue('7');
    await expect(page.locator('#intent-count')).toHaveText('Intents: 0 edits, 0 pastes');

    // Ctrl+Enter is the key that fills.
    await page.keyboard.press('Control+Enter');
    await expect(page.locator('#intent-count')).toHaveText('Intents: 0 edits, 1 pastes');
});

// A key that names the start of the row or of the result puts the Viewport's left edge at the start,
// with Pinned Columns or without (KB-10, ADR-0012's note on pinned columns): /wide pins two of its
// hundred columns, and its button unpins them.
test('KB-10: Home and Ctrl+Home return the Viewport to the start of the row and the result, pinned and unpinned alike (ADR-0012)', async ({ page }) => {
    for (const pinned of [2, 0]) {
        await page.goto('/wide?rows=1000');
        const wide = page.locator('.ex-grid').first();
        const scroller = wide.locator('.ex-scroller');
        const left = () => scroller.evaluate((el) => el.scrollLeft);
        const top = () => scroller.evaluate((el) => el.scrollTop);
        if (pinned === 0) {
            await page.locator('#toggle-pinned').click();
            await expect(page.locator('#toggle-pinned')).toContainText('Pinned columns: 0');
        }
        await wide.locator("[id$='r3c4']").click({ force: true });
        await expectActiveDescendant(wide, /r3c4$/);

        await page.keyboard.press('End');
        await expectActiveDescendant(wide, /r3c99$/);
        await expect.poll(left).toBeGreaterThan(0);
        await page.keyboard.press('Home');
        await expectActiveDescendant(wide, /r3c0$/);
        await expect.poll(left).toBe(0);

        await page.keyboard.press('ControlOrMeta+End');
        await expectActiveDescendant(wide, /r999c99$/);
        await expect.poll(left).toBeGreaterThan(0);
        await expect.poll(top).toBeGreaterThan(0);
        await page.keyboard.press('ControlOrMeta+Home');
        await expectActiveDescendant(wide, /r0c0$/);
        await expect.poll(left).toBe(0);
        await expect.poll(top).toBe(0);
    }
});

// While a range is extended the grid keeps the Extent in view; moved back inside the view, the Extent
// scrolls nothing (KB-40, ADR-0052). The Focus's half of the clause is layer 2's and the loop above.
test('KB-40: an Extent carried below the view scrolls it, and moved back inside the view scrolls nothing (ADR-0052)', async ({ page }) => {
    const scroller = grid(page).locator('.ex-scroller');
    const top = () => scroller.evaluate((el) => el.scrollTop);
    await clickCell(page, 1, 1);
    await expectActiveDescendant(grid(page), /r1c1$/);

    for (let i = 0; i < 15; i++) {
        await page.keyboard.press('Shift+ArrowDown');
    }
    await expect(grid(page).locator('.ex-announce')).toContainText('16 rows by 1 columns selected');
    await expect.poll(top).toBeGreaterThan(0);
    await settled(grid(page));
    const carried = await top();

    for (let i = 0; i < 3; i++) {
        await page.keyboard.press('Shift+ArrowUp');
    }
    await expect(grid(page).locator('.ex-announce')).toContainText('13 rows by 1 columns selected');
    await settled(grid(page));
    expect(await top()).toBe(carried);
});

// A selection beyond the Window takes the asynchronous route: the grid asks for the rows it has not
// got, and the copy holds every one of them (CP-7, ADR-0005). /fetch holds a Window of the rows on
// screen and sixty either side, of a hundred thousand.
test('CP-7: a copy of rows beyond the Window takes the asynchronous route, and the clipboard holds every row the source was asked for (ADR-0005)', async ({ page }) => {
    await page.goto('/fetch');
    const fetchGrid = grid(page);
    await expect(fetchGrid.locator("[id$='r0c0']")).toHaveText('0');
    await fetchGrid.locator("[id$='r0c0']").click({ force: true });
    await expectActiveDescendant(fetchGrid, /r0c0$/);
    await page.evaluate(() => navigator.clipboard.writeText('SENTINEL'));

    for (let i = 0; i < 12; i++) {
        await page.keyboard.press('Shift+PageDown');
    }
    const announced = fetchGrid.locator('.ex-announce');
    await expect(announced).toContainText('rows by 1 columns selected');
    await settled(fetchGrid);
    const rows = Number((await announced.textContent()).match(/(\d+) rows/)[1]);
    expect(rows, 'the premise: the Selection runs past the Window\'s sixty rows of read-ahead').toBeGreaterThan(200);

    await page.keyboard.press('ControlOrMeta+C');

    await expect.poll(() => page.evaluate(() => navigator.clipboard.readText()), { timeout: 15_000 }).not.toBe('SENTINEL');
    const lines = (await page.evaluate(() => navigator.clipboard.readText())).replace(/\r?\n$/, '').split(/\r?\n/);
    expect(lines).toHaveLength(rows);
    expect(lines[0]).toBe('0');
    expect(lines.at(-1).replace(/,/g, '')).toBe(String(rows - 1));
});

// A grid disposed while its calls are still in flight — keys forwarded and a scroll reported, a round
// trip from being answered on the Server host — leaves no exception and nothing on the console, and
// the next grid mounted on the page works (ERR-7, ADR-0018). The console-clean fixture fails the test
// on anything the browser or the circuit says.
test('ERR-7: disposing a grid while its interop calls are in flight leaves the console clean, and a grid mounted after it works (ADR-0018)', async ({ page }) => {
    await page.goto('/lifecycle');
    const toggle = page.locator('#toggle-grid');
    const mounted = page.locator('.ex-grid');
    await toggle.click();
    await expect(mounted.locator('.ex-row').first()).toBeVisible();
    await expect(mounted).not.toHaveAttribute('aria-busy', /.*/);
    await mounted.locator("[id$='r1c1']").click({ force: true });
    await expectActiveDescendant(mounted, /r1c1$/);
    await setRoundTrip(150);

    const box = await mounted.locator('.ex-scroller').boundingBox();
    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
    await page.mouse.wheel(0, 200);
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('ArrowDown');
    await toggle.click();
    await expect(mounted).toHaveCount(0);
    await setRoundTrip(0);
    await circuitQuiet();

    await toggle.click();
    await expect(mounted.locator('.ex-row').first()).toBeVisible();
    await expect(mounted).not.toHaveAttribute('aria-busy', /.*/);
    await mounted.locator("[id$='r1c1']").click({ force: true });
    await page.keyboard.press('ArrowDown');
    await expectActiveDescendant(mounted, /r2c1$/);
});
