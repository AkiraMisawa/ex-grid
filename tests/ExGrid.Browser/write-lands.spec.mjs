import { test, expect, alterPage, circuitQuiet, setRoundTrip } from './fixtures.mjs';
import { SERVER } from './hosting.mjs';
import { expectActiveDescendant, expectTabStopTaken } from './keyboard.mjs';

// A write lands as the user entered it, on the row the user aimed it at (ADR-0142, rewritten
// 2026-10-07; LV-11 to LV-14, LV-17), driven on /features?upstream=1. There F9, declared to the
// grid, moves the Notional of the first five rows up by one, as a live feed would: it reaches the
// host in its turn among the keys and presses, so on the Server host a gesture made straight after
// it is taken on the render from before the change and handled after it. Columns: Book 0, Trader 1,
// Notional 2, Act 3 (one action, Approve), Narrow 4, …
//
// What the user saw at a gesture is read by a capture listener on the document, ahead of the
// grid's own (seenAt): the text of the cell the gesture is about, as the page showed it then. A
// race test asserts what it saw before it asserts the outcome, so a run in which the change had
// already been painted says so by name rather than passing on the wrong branch. Through the
// latency proxy at 150 ms a gesture made by the next Playwright call is taken long before the
// change's render can come back.

function grid(page) {
    return page.locator('.ex-grid').first();
}

const NOTIONAL = 2;
const ACT = 3;

function cell(page, row, column) {
    return grid(page).locator(`[id$='r${row}c${column}']`);
}

async function clickCell(page, row, column) {
    // Cells are pointer-events: none — the Viewport is the delegated target (ADR-0004).
    await cell(page, row, column).click({ force: true });
}

async function boxOf(locator) {
    const box = await locator.boundingBox();
    expect(box).not.toBeNull();
    return box;
}

test.beforeEach(async ({ page, context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    await page.goto('/features?upstream=1');
    await expect(grid(page).locator('.ex-row').first()).toBeVisible();
    await expectTabStopTaken(grid(page));
});

/**
 * Notes, for each keydown, mousedown, mouseup and paste from now on, the text the cell at
 * (`row`, `column`) of the first grid showed and the render its Viewport named (data-ex-paint). A
 * listener on the document in the capture phase runs before the grid's own on its root.
 */
async function watchWhatIsSeen(page, row, column) {
    await alterPage(page, ({ row, column }) => {
        window.__seenAtGesture = [];
        const note = (event) => {
            const root = document.querySelector('.ex-grid');
            const shown = root?.querySelector(`[id$='r${row}c${column}']`);
            window.__seenAtGesture.push({
                type: event.type,
                key: event.key ?? null,
                text: shown ? shown.textContent : null,
                paint: root?.querySelector('.ex-viewport')?.getAttribute('data-ex-paint') ?? null,
            });
        };
        const types = ['keydown', 'mousedown', 'mouseup', 'paste'];
        for (const type of types) {
            document.addEventListener(type, note, true);
        }
        return () => {
            for (const type of types) {
                document.removeEventListener(type, note, true);
            }
            delete window.__seenAtGesture;
        };
    }, { row, column });
}

/** What the watched cell showed at the last gesture of `type` (and `key`, for a keydown). */
async function seenAt(page, type, key = null) {
    const seen = await page.evaluate(() => window.__seenAtGesture);
    // A key is matched without its case: a Ctrl+V reaches the page as `v` or `V`, as the platform has it.
    const matching = seen.filter((e) => e.type === type && (key === null || e.key?.toLowerCase() === key.toLowerCase()));
    expect(matching.length, `a ${type}${key ? ` of ${key}` : ''} was seen`).toBeGreaterThan(0);
    return matching[matching.length - 1];
}

test('LV-11/LV-21: a commit over a cell that changed under the editor lands, and the Overwrite Notice in the live region names what was seen and what was replaced (ADR-0142)', async ({ page }) => {
    const notice = page.locator('#overwrite-notice-status');
    await expect(notice).toHaveAttribute('role', 'status');
    const notional = cell(page, 0, NOTIONAL);
    const before = (await notional.textContent()).trim();
    await clickCell(page, 0, NOTIONAL);
    await expectActiveDescendant(grid(page), /r0c2$/);
    await setRoundTrip(150);

    await page.keyboard.type('1500000');
    // The data moves on while the editor covers the cell: F9 is raised with the edit open, and
    // the editor is left as it was (ADR-0050, item 14).
    await page.keyboard.press('F9');
    await expect(page.locator('#upstream-status')).toContainText('(×1)');
    await expect(notional).not.toHaveText(before);
    const moved = (await notional.textContent()).trim();
    await page.keyboard.press('Enter');

    await expect(page.locator('#edit-status')).toContainText('Notional=1500000');
    await expect(grid(page).locator('input.ex-editor')).toHaveCount(0);
    await expect(notional).toHaveText('1500000');
    await expect(notice).toContainText(`Notional written over ${moved}; it showed ${before} when you started typing`);
    await circuitQuiet();
    await expect(page.locator('#commit-refused-status')).toHaveText('Commit refused: —');
    await expect(notice).not.toContainText('×2');
});

test('LV-11: Escape over a cell that changed under the editor writes nothing, and tells nothing (ADR-0142)', async ({ page }) => {
    const notional = cell(page, 0, NOTIONAL);
    await clickCell(page, 0, NOTIONAL);
    await expectActiveDescendant(grid(page), /r0c2$/);
    await setRoundTrip(150);

    await page.keyboard.type('42');
    await page.keyboard.press('F9');
    await expect(page.locator('#upstream-status')).toContainText('(×1)');
    const moved = (await notional.textContent()).trim();

    await page.keyboard.press('Escape');
    await expect(grid(page).locator('input.ex-editor')).toHaveCount(0);
    await circuitQuiet();
    await expect(page.locator('#edit-status')).toHaveText('Edited: —');
    await expect(page.locator('#overwrite-notice-status')).toHaveText('Overwritten: —');
    await expect(notional).toHaveText(moved);
});

test('LV-11: a change to another cell of the row refuses nothing, and tells nothing (ADR-0142)', async ({ page }) => {
    await clickCell(page, 0, 1);                       // Trader, editable; F9 moves Notional
    await expectActiveDescendant(grid(page), /r0c1$/);
    await setRoundTrip(150);

    await page.keyboard.type('Osei');
    await page.keyboard.press('F9');
    await page.keyboard.press('Enter');

    await expect(page.locator('#edit-status')).toContainText('Trader=Osei');
    await expect(cell(page, 0, 1)).toHaveText('Osei');
    await circuitQuiet();
    await expect(page.locator('#commit-refused-status')).toHaveText('Commit refused: —');
    await expect(page.locator('#overwrite-notice-status')).toHaveText('Overwritten: —');
});

test('LV-12: an Action press taken on a render whose row has changed since acts on that row as it is now, once (ADR-0142, ADR-0020)', async ({ page }) => {
    test.skip(!SERVER, 'WebAssembly paints F9\'s change before the next press can be taken: no press can be taken on the render before it');
    await expect(page.locator('#action-refused-status')).toHaveAttribute('role', 'status');
    const notional = cell(page, 0, NOTIONAL);
    const before = (await notional.textContent()).trim();
    const button = await boxOf(cell(page, 0, ACT).locator('.ex-action'));
    // The keyboard on the grid, away from the row pressed.
    await clickCell(page, 6, 0);
    await expectActiveDescendant(grid(page), /r6c0$/);
    await watchWhatIsSeen(page, 0, NOTIONAL);
    await setRoundTrip(150);

    await page.keyboard.press('F9');
    await page.mouse.click(button.x + button.width / 2, button.y + button.height / 2);

    expect((await seenAt(page, 'mousedown')).text, 'the press was taken on the render before F9\'s change').toBe(before);
    await expect(page.locator('#upstream-status')).toContainText('(×1)');
    await expect(notional).not.toHaveText(before);
    const moved = (await notional.textContent()).trim();
    // Acted on the row as it is now: at the Notional F9 moved it to.
    await expect(page.locator('#action-status')).toContainText('approve Alpha/');
    await expect(page.locator('#action-status')).toContainText(moved.replace(/,/g, ''));
    await circuitQuiet();
    await expect(page.locator('#action-refused-status')).toHaveText('Action refused: —');
});

test('LV-13/LV-14: a Ctrl+V pressed on a render a newer one replaced before it landed, under the same order, pastes where it was aimed (ADR-0142)', async ({ page }) => {
    await page.evaluate(() => navigator.clipboard.write([new ClipboardItem({
        'text/html': new Blob(['<table><tr><td>5</td></tr></table>'], { type: 'text/html' }),
        'text/plain': new Blob(['5\r\n'], { type: 'text/plain' }),
    })]));
    const notional = cell(page, 0, NOTIONAL);
    const before = (await notional.textContent()).trim();
    await clickCell(page, 0, NOTIONAL);
    await page.keyboard.press('Shift+ArrowDown');     // Notional, rows 0 and 1 — both moved by F9
    await circuitQuiet();
    await watchWhatIsSeen(page, 0, NOTIONAL);
    await setRoundTrip(150);

    await page.keyboard.press('F9');
    await page.keyboard.press('ControlOrMeta+v');

    const atPaste = await seenAt(page, 'paste');
    if (SERVER) {
        expect(atPaste.text, 'on the Server host the paste is taken before the change\'s render comes back').toBe(before);
    }
    await expect(page.locator('#upstream-status')).toContainText('(×1)');
    await expect(page.locator('#paste-status')).toContainText('2 cells from 1x1');
    await expect(notional).toHaveText('5');
    await expect(cell(page, 1, NOTIONAL)).toHaveText('5');
    await circuitQuiet();
    await expect(page.locator('#paste-refused-status')).toHaveText('Paste refused: —');
    if (SERVER) {
        expect(await grid(page).locator('.ex-viewport').first().getAttribute('data-ex-paint'),
            'a newer render replaced the one the paste was taken on').not.toBe(atPaste.paint);
    }
});

test('LV-13: a Ctrl+Enter fill whose target changed since the key was pressed lands as entered (ADR-0142, ADR-0035)', async ({ page }) => {
    test.skip(!SERVER, 'WebAssembly paints F9\'s change before Ctrl+Enter can be taken on the render before it');
    // Notional rows 4 and 5, the Focus on 5 (ADR-0052): F9 moves row 4, not the edited cell.
    await clickCell(page, 5, NOTIONAL);
    await page.keyboard.press('Shift+ArrowUp');
    await page.keyboard.type('7');
    const editor = grid(page).locator('input.ex-editor');
    await expect(editor).toHaveValue('7');
    await circuitQuiet();
    const before = (await cell(page, 4, NOTIONAL).textContent()).trim();
    await watchWhatIsSeen(page, 4, NOTIONAL);
    await setRoundTrip(150);

    await page.keyboard.press('F9');
    await page.keyboard.press('ControlOrMeta+Enter');

    expect((await seenAt(page, 'keydown', 'Enter')).text, 'Ctrl+Enter was taken on the render before F9\'s change').toBe(before);
    await expect(page.locator('#paste-status')).toContainText('2 cells from 1x1');
    await expect(editor).toHaveCount(0);
    await expect(cell(page, 4, NOTIONAL)).toHaveText('7');
    await circuitQuiet();
    await expect(page.locator('#paste-refused-status')).toHaveText('Paste refused: —');
    await expect(page.locator('#overwrite-notice-status')).toHaveText('Overwritten: —');
});

test('LV-13: a fill-handle drag released on a render whose target has changed since lands (ADR-0142, ADR-0050 item 5)', async ({ page }) => {
    test.skip(!SERVER, 'WebAssembly paints F9\'s change before the release can be taken on the render before it');
    await clickCell(page, 0, NOTIONAL);
    await circuitQuiet();
    const corner = await boxOf(cell(page, 0, NOTIONAL));
    await expect.poll(async () => {
        const box = await grid(page).locator('.ex-fill-handle').boundingBox();
        return box !== null
            && Math.abs(box.x + box.width / 2 - (corner.x + corner.width)) <= 2
            && Math.abs(box.y + box.height / 2 - (corner.y + corner.height)) <= 2;
    }).toBe(true);
    const handle = await boxOf(grid(page).locator('.ex-fill-handle'));
    const target = await boxOf(cell(page, 2, NOTIONAL));
    const before = (await cell(page, 2, NOTIONAL).textContent()).trim();
    await page.mouse.move(handle.x + handle.width / 2, handle.y + handle.height / 2);
    await page.mouse.down();
    const toX = target.x + target.width / 2;
    const toY = target.y + target.height / 2;
    await page.mouse.move(toX + 3, toY, { steps: 8 });
    // The target is outlined once the drag's moves are heard (declarations.spec.mjs, fillDragHeard).
    let nudge = 0;
    await expect.poll(async () => {
        await page.mouse.move(toX + 3 + (nudge++ % 2), toY);
        return grid(page).locator('.ex-fill-target').count();
    }).toBe(1);
    await watchWhatIsSeen(page, 2, NOTIONAL);
    await setRoundTrip(150);

    await page.keyboard.press('F9');
    await page.mouse.up();

    expect((await seenAt(page, 'mouseup')).text, 'the release was taken on the render before F9\'s change').toBe(before);
    await expect(page.locator('#fill-status')).not.toHaveText('Filled: —');
    await circuitQuiet();
    await expect(page.locator('#paste-refused-status')).toHaveText('Paste refused: —');
});

// The same keys give the same outcome on both hosts (LV-17, principle 6). On the Server host the
// keys after Enter are taken on the render from before the commit was painted — at 150 ms always,
// at 0 ms as the wire allows. On WebAssembly the commit is painted before the next key is taken.
for (const rtt of [0, 150]) {
    test(`LV-17: 5 Enter ↑ Ctrl+V typed at once pastes over the cell the commit just wrote (ADR-0142, ${rtt} ms)`, async ({ page }) => {
        test.skip(!SERVER && rtt !== 0, 'WebAssembly has no round trip to set');
        await page.evaluate(() => navigator.clipboard.write([new ClipboardItem({
            'text/html': new Blob(['<table><tr><td>7</td></tr></table>'], { type: 'text/html' }),
            'text/plain': new Blob(['7\r\n'], { type: 'text/plain' }),
        })]));
        const notional = cell(page, 0, NOTIONAL);
        const before = (await notional.textContent()).trim();
        await clickCell(page, 0, NOTIONAL);
        await expectActiveDescendant(grid(page), /r0c2$/);
        await circuitQuiet();
        await watchWhatIsSeen(page, 0, NOTIONAL);
        await setRoundTrip(rtt);

        await page.keyboard.type('5');
        await page.keyboard.press('Enter');
        await page.keyboard.press('ArrowUp');
        await page.keyboard.press('ControlOrMeta+v');

        if (SERVER && rtt === 150) {
            expect((await seenAt(page, 'keydown', 'v')).text, 'Ctrl+V was taken on the render before the 5 was painted')
                .toBe(before);
        }
        await expect(page.locator('#paste-status')).toContainText('1 cells from 1x1');
        await expect(notional).toHaveText('7');
        await circuitQuiet();
        await expect(page.locator('#edit-status')).toContainText('Notional=5');
        await expect(page.locator('#paste-refused-status')).toHaveText('Paste refused: —');
    });

    test(`LV-11/LV-17: 1 Enter ↑ 2 Enter typed at once commits both, the second over the cell the first wrote, with no notice (ADR-0142 D1, ${rtt} ms)`, async ({ page }) => {
        test.skip(!SERVER && rtt !== 0, 'WebAssembly has no round trip to set');
        const notional = cell(page, 0, NOTIONAL);
        const before = (await notional.textContent()).trim();
        await clickCell(page, 0, NOTIONAL);
        await expectActiveDescendant(grid(page), /r0c2$/);
        await circuitQuiet();
        await watchWhatIsSeen(page, 0, NOTIONAL);
        await setRoundTrip(rtt);

        await page.keyboard.type('1');
        await page.keyboard.press('Enter');
        await page.keyboard.press('ArrowUp');
        await page.keyboard.type('2');
        await page.keyboard.press('Enter');

        if (SERVER && rtt === 150) {
            expect((await seenAt(page, 'keydown', '2')).text, 'the 2 was taken on the render before the 1 was painted')
                .toBe(before);
        }
        await expect(notional).toHaveText('2');
        await expect(grid(page).locator('input.ex-editor')).toHaveCount(0);
        await circuitQuiet();
        await expect(page.locator('#edit-status')).toContainText('Notional=2');
        await expect(page.locator('#commit-refused-status')).toHaveText('Commit refused: —');
        await expect(page.locator('#overwrite-notice-status')).toHaveText('Overwritten: —');
    });
}

test('LV-11: a change in the round trip between the key that opens the editor and the open is not told, and the commit lands (ADR-0142)', async ({ page }) => {
    test.skip(!SERVER, 'WebAssembly paints F9\'s change before the next key can be taken on the render before it');
    const notional = cell(page, 0, NOTIONAL);
    const before = (await notional.textContent()).trim();
    await clickCell(page, 0, NOTIONAL);
    await expectActiveDescendant(grid(page), /r0c2$/);
    await circuitQuiet();
    await watchWhatIsSeen(page, 0, NOTIONAL);
    await setRoundTrip(150);

    // F9 moves the cell upstream; the 5 that opens the editor over it is taken before that change
    // is painted. The editor opens over the moved cell: the round trip the rule accepts.
    await page.keyboard.press('F9');
    await page.keyboard.type('5');
    await page.keyboard.press('Enter');

    expect((await seenAt(page, 'keydown', '5')).text, 'the 5 was taken on the render before F9\'s change').toBe(before);
    await expect(page.locator('#upstream-status')).toContainText('(×1)');
    await expect(page.locator('#edit-status')).toContainText('Notional=5');
    await expect(notional).toHaveText('5');
    await expect(grid(page).locator('input.ex-editor')).toHaveCount(0);
    await circuitQuiet();
    await expect(page.locator('#commit-refused-status')).toHaveText('Commit refused: —');
    await expect(page.locator('#overwrite-notice-status')).toHaveText('Overwritten: —');
});

test('LV-13: a Delete whose target changed since the key was pressed clears it (ADR-0142, ADR-0054)', async ({ page }) => {
    test.skip(!SERVER, 'WebAssembly paints F9\'s change before Delete can be taken on the render before it');
    // Notional rows 0 and 1, both moved by F9.
    await clickCell(page, 0, NOTIONAL);
    await page.keyboard.press('Shift+ArrowDown');
    await circuitQuiet();
    const before = (await cell(page, 0, NOTIONAL).textContent()).trim();
    await watchWhatIsSeen(page, 0, NOTIONAL);
    await setRoundTrip(150);

    await page.keyboard.press('F9');
    await page.keyboard.press('Delete');

    expect((await seenAt(page, 'keydown', 'Delete')).text, 'Delete was taken on the render before F9\'s change').toBe(before);
    await expect(page.locator('#clear-status')).toContainText('2 cells');
    await circuitQuiet();
    await expect(page.locator('#paste-refused-status')).toHaveText('Paste refused: —');
});

test('LV-13: a Ctrl+R whose target changed since the key was pressed fills it (ADR-0142, ADR-0035)', async ({ page }) => {
    test.skip(!SERVER, 'WebAssembly paints F9\'s change before Ctrl+R can be taken on the render before it');
    // Row 0, Trader to Notional: Trader is the source, and F9 moves Notional, the target. Notional's
    // right is the Action column, which no fill can write (ADR-0035), so the fill runs rightward into it.
    await clickCell(page, 0, NOTIONAL - 1);
    await page.keyboard.press('Shift+ArrowRight');
    await circuitQuiet();
    const before = (await cell(page, 0, NOTIONAL).textContent()).trim();
    await watchWhatIsSeen(page, 0, NOTIONAL);
    await setRoundTrip(150);

    await page.keyboard.press('F9');
    await page.keyboard.press('ControlOrMeta+r');

    expect((await seenAt(page, 'keydown', 'r')).text, 'Ctrl+R was taken on the render before F9\'s change').toBe(before);
    await expect(page.locator('#paste-status')).toContainText('1 cells from 1x1');
    await circuitQuiet();
    await expect(page.locator('#paste-refused-status')).toHaveText('Paste refused: —');
});

test('LV-13: a Ctrl+D whose source changed since the key was pressed fills with the source as it is now (ADR-0142, ADR-0035)', async ({ page }) => {
    test.skip(!SERVER, 'WebAssembly paints F9\'s change before Ctrl+D can be taken on the render before it');
    // Notional rows 4 to 6: F9 moves row 4, the source, and not rows 5 and 6, the target.
    await clickCell(page, 4, NOTIONAL);
    await page.keyboard.press('Shift+ArrowDown');
    await page.keyboard.press('Shift+ArrowDown');
    await circuitQuiet();
    const before = (await cell(page, 4, NOTIONAL).textContent()).trim();
    await watchWhatIsSeen(page, 4, NOTIONAL);
    await setRoundTrip(150);

    await page.keyboard.press('F9');
    await page.keyboard.press('ControlOrMeta+d');

    expect((await seenAt(page, 'keydown', 'd')).text, 'Ctrl+D was taken on the render before F9\'s change').toBe(before);
    await expect(cell(page, 4, NOTIONAL)).not.toHaveText(before);
    const moved = (await cell(page, 4, NOTIONAL).textContent()).trim();
    await expect(page.locator('#paste-status')).toContainText('2 cells from 1x1');
    await expect(cell(page, 5, NOTIONAL)).toHaveText(moved);
    await expect(cell(page, 6, NOTIONAL)).toHaveText(moved);
    await circuitQuiet();
    await expect(page.locator('#paste-refused-status')).toHaveText('Paste refused: —');
});

test('ADR-0011 (note of 2026-10-07) / LV-20: with a Row Key, a live amendment that re-sorts the editor\'s row out of view takes the editor along without a scroll; the keys typed still reach it, and Enter lands on that row (ADR-0142)', async ({ page }) => {
    // F8, declared under ?rowkey=1, amends the top row's Notional past every other: under a sort by
    // Notional the row moves to the end, as a live feed's amendment can move it.
    await page.goto('/features?upstream=1&rowkey=1');
    await expect(grid(page).locator('.ex-row').first()).toBeVisible();
    await expectTabStopTaken(grid(page));
    const header = grid(page).locator('.ex-header-cell').nth(NOTIONAL);
    await header.click({ position: { x: 30, y: 14 }, force: true });
    await expect(header).toHaveAttribute('aria-sort', 'ascending');
    const last = 399;                                  // the page's 400 trades
    await clickCell(page, 0, 1);                       // Trader of trade #0, the smallest Notional
    await expectActiveDescendant(grid(page), /r0c1$/);
    const scroller = grid(page).locator('.ex-scroller');
    const scrollTop = await scroller.evaluate((el) => el.scrollTop);

    await page.keyboard.type('Zed');
    await page.keyboard.press('F8');
    await expect(page.locator('#upstream-status')).toContainText('trade #0 amended past every other (×1)');

    // The editor went with its row, out of view; nothing scrolled, and it still holds the keyboard.
    const editor = grid(page).locator('input.ex-editor');
    await expect(editor).toHaveClass(/ex-editor-away/);
    await expect(editor).toBeFocused();
    await circuitQuiet();
    expect(await scroller.evaluate((el) => el.scrollTop)).toBe(scrollTop);
    await page.keyboard.type('x');
    await expect(editor).toHaveValue('Zedx');
    await circuitQuiet();
    expect(await scroller.evaluate((el) => el.scrollTop)).toBe(scrollTop);

    await page.keyboard.press('Enter');

    await expect(page.locator('#edit-status')).toHaveText('Edited: Trader=Zedx on Alpha/Ishikawa #0');
    await expect(editor).toHaveCount(0);
    await circuitQuiet();
    await expect(page.locator('#commit-refused-status')).toHaveText('Commit refused: —');
    await expect(page.locator('#edit-discarded-status')).toHaveText('Edit discarded: —');
    await scroller.evaluate((el) => { el.scrollTop = el.scrollHeight; });
    await expect(cell(page, last, 1)).toHaveText('Zedx');
});
