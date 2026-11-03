import { test, expect, alterPage, setRoundTrip, settled, chromeHost } from './fixtures.mjs';
import { SERVER } from './hosting.mjs';
import { sheet, openSheet, cell, pressCell, pressAt, editor, bar, columnHeader, expectFocusAt } from './sheet-helpers.mjs';
import { keyField, expectKeyboardOn, expectActiveDescendant } from './keyboard.mjs';
import { onWindowsDesktop, keysSeenBy, osJapanese, osTyped, osPressed } from './os-input.mjs';

// ED-30 with a real IME (ADR-0450): the Microsoft IME, Japanese, on CI's Windows runner, where the
// Windows runs once took it by hand (docs/specs/exsheet/verify-on-windows-16.md, Part A, the cases k1 to
// k16, and the seventeenth run's DC-47 case). key-field.spec.mjs and reference-text.spec.mjs make the
// composition through the DevTools protocol, which says what the page does with a composition it is
// told; what they cannot say is what a real IME tells it — which keys it takes, in what order its
// events come against the keys', when a composition ends on a key that starts the next, and what a press
// the browser meets first does to it. Here every key and every press during a composition goes through
// Windows' own input queue (os-input.mjs), so the IME meets it as it meets a person's.
//
// The IME's Predictive input is off on the runner (windows/japanese-input.ps1): with it on, a list of the
// IME's own stands under the composition and takes a press that falls on it (the sixteenth and seventeenth
// runs' k8). What the IME converts a reading to is its own, so a case reads what it committed from the
// composition's own end (compositionend) and holds the grid to that. Keys that set up a case and that no
// IME is to see (=A1+, 150) are Playwright's, which reach the page alone.
//
// k15 (Alt+Tab mid-composition) is a case to record, not a clause: which of two ways it ends changed
// from run to run (the nineteenth run's report), and key-field.spec.mjs holds what ADR-0080 asks of it.

// The OS draws the window; Playwright's viewport would emulate one.
test.use({ viewport: null });

test.beforeEach(() => {
    test.skip(!onWindowsDesktop || process.env.EXGRID_JAPANESE !== '1',
        'A real IME needs a Windows desktop with Japanese installed (windows/japanese-input.ps1, which CI\'s step marks with EXGRID_JAPANESE=1) (ADR-0450)');
});

/** What a composition left in a cell: no Latin letter, as a key the IME did not take would leave. */
const COMPOSED = /^[^A-Za-zＡ-Ｚａ-ｚ]+$/u;

/** What the grid's Keyboard Field shows: its value, whether it is drawn, and whether it has the keyboard. */
const fieldState = (grid) => keyField(grid).evaluate((field) => ({
    value: field.value,
    drawn: getComputedStyle(field).opacity === '1',
    focused: document.activeElement === field,
}));

/** Whether two boxes are one, to the pixel. */
const sameBox = (a, b) => Math.abs(a.x - b.x) <= 1 && Math.abs(a.y - b.y) <= 1
    && Math.abs(a.width - b.width) <= 1 && Math.abs(a.height - b.height) <= 1;

let recorders = 0;

/**
 * Starts recording what each composition the page sees ends with (compositionend's data), in the
 * capture phase on its window. Answers a function that reads them.
 */
async function compositionEnds(page) {
    const name = `exImeEnds${++recorders}`;
    await alterPage(page, ({ record }) => {
        const ends = [];
        window[record] = ends;
        const note = (event) => ends.push(event.data);
        window.addEventListener('compositionend', note, true);
        return () => {
            window.removeEventListener('compositionend', note, true);
            delete window[record];
        };
    }, { record: name });
    return () => page.evaluate((record) => window[record], name);
}

/** Waits until `count` compositions have ended, and answers what they ended with. */
async function ended(ends, count) {
    await expect.poll(async () => (await ends()).length, { message: `${count} composition(s) ended` }).toBe(count);
    return ends();
}

/** What the Cell Editor's field and the layer beneath it show (DC-47), as reference-text.spec.mjs reads them. */
const colouring = (field) => field.evaluate((input) => {
    const layer = input.previousElementSibling?.classList.contains('ex-reference-text') ? input.previousElementSibling : null;
    return {
        value: input.value,
        text: layer?.getAttribute('data-ex-text') ?? null,
        drawn: layer?.textContent ?? null,
        shown: input.classList.contains('ex-reference-text-shown'),
        transparent: getComputedStyle(input).webkitTextFillColor === 'rgba(0, 0, 0, 0)',
        layer: layer ? getComputedStyle(layer).visibility : null,
        colours: layer?.getAttribute('data-ex-colours') ?? null,
    };
});

// /sheet under each Chrome, and the Blazor one behind 150 ms on the Server host, as the Windows runs took
// ED-30. Each test has a window of its own (viewport: null), so it starts on a fresh page with D10
// selected, the window on the Japanese keyboard, and the IME off.
for (const [chrome, ms] of [['builtin', 0], ['mud', 0], ['mud', 150]]) {
    const named = `(${chrome}${ms > 0 ? `, ${ms} ms` : ''}) ${chromeHost(chrome)}`;

    test.describe(`/sheet under the ${chrome} Chrome${ms > 0 ? `, ${ms} ms behind` : ''}`, () => {
        test.beforeEach(async ({ page }) => {
            test.skip(ms > 0 && !SERVER, 'a round trip exists only on a circuit');
            await openSheet(page, chrome);
            await pressCell(sheet(page), 'D10');
            await osJapanese(page);
            await setRoundTrip(ms);
        });

        test(`ED-30 (k1, k2): the IME switched on over a selected cell composes from the first key in the Keyboard Field drawn over it; nothing moves while it lasts; its end opens the Cell Editor holding it; Enter commits ${named}`, async ({ page }) => {
            const grid = sheet(page);
            const ends = await compositionEnds(page);
            const pressed = await keysSeenBy(page);
            await osTyped(page, 'imeon', 'k', 'a', 'n', 'a');
            expect((await pressed()).slice(-4), 'the IME took every key').toEqual(['Process', 'Process', 'Process', 'Process']);
            await expect.poll(() => fieldState(grid)).toEqual({ value: 'かな', drawn: true, focused: true });
            await expect.poll(async () => sameBox(await keyField(grid).boundingBox(), await cell(grid, 'D10').boundingBox()), 'the composition is drawn over D10').toBe(true);
            await expect(editor(grid)).toHaveCount(0);
            await expectFocusAt(grid, 'D10');

            // Space converts; the composition goes on.
            await osTyped(page, 'space');
            await expect.poll(() => fieldState(grid)).toMatchObject({ drawn: true, focused: true });
            await expect(editor(grid)).toHaveCount(0);

            // Enter ends it, and the Cell Editor opens holding what it ended with, with the keyboard.
            await osTyped(page, 'enter');
            const [typed] = await ended(ends, 1);
            expect(typed).toMatch(COMPOSED);
            await expect(editor(grid)).toHaveValue(typed);
            await expect(editor(grid)).toBeFocused();
            await expect.poll(() => fieldState(grid)).toEqual({ value: '', drawn: false, focused: false });

            await osTyped(page, 'enter');
            await expect(cell(grid, 'D10')).toHaveText(typed);
            await expectFocusAt(grid, 'D11');
            await expect(editor(grid)).toHaveCount(0);
            await expectKeyboardOn(grid);
        });

        test(`ED-30 (k3): Escape during a composition ends it empty and leaves the edit open and empty; Escape again cancels it ${named}`, async ({ page }) => {
            const grid = sheet(page);
            const ends = await compositionEnds(page);
            await osTyped(page, 'imeon', 'k', 'a', 'n', 'a');
            await expect.poll(() => fieldState(grid)).toEqual({ value: 'かな', drawn: true, focused: true });

            await osTyped(page, 'escape');
            expect(await ended(ends, 1)).toEqual(['']);
            await expect(editor(grid)).toHaveValue('');
            await expect(editor(grid)).toBeFocused();

            await osTyped(page, 'escape');
            await expect(editor(grid)).toHaveCount(0);
            await expect(cell(grid, 'D10')).toHaveText('');
            await expectFocusAt(grid, 'D10');
            await expectKeyboardOn(grid);
        });

        test(`ED-30 (k4): Down while a composition is converted is the IME's: the Focus stays and nothing is written ${named}`, async ({ page }) => {
            const grid = sheet(page);
            const ends = await compositionEnds(page);
            await osTyped(page, 'imeon', 'k', 'a', 'n', 'a', 'space', 'down', 'down');
            await expect.poll(() => fieldState(grid)).toMatchObject({ drawn: true, focused: true });
            await expect(editor(grid)).toHaveCount(0);
            await expectFocusAt(grid, 'D10');
            await expect(cell(grid, 'D10')).toHaveText('');

            await osTyped(page, 'enter');
            const [typed] = await ended(ends, 1);
            expect(typed).toMatch(COMPOSED);
            await expect(editor(grid)).toHaveValue(typed);
            await osTyped(page, 'enter');
            await expect(cell(grid, 'D10')).toHaveText(typed);
            await expectFocusAt(grid, 'D11');
        });

        test(`ED-30 (k5): with the IME left on by an earlier edit, a press on the cell, then keys at machine speed, compose from the first key ${named}`, async ({ page }) => {
            const grid = sheet(page);
            const ends = await compositionEnds(page);
            await osTyped(page, 'f2');
            await expect(editor(grid)).toBeFocused();
            await osTyped(page, 'imeon', 'escape');
            await expect(editor(grid)).toHaveCount(0);
            await expectKeyboardOn(grid);

            await osPressed(cell(grid, 'D10'));
            await expectFocusAt(grid, 'D10');
            await osTyped(page, 'k', 'a', 'n', 'a', 'space', 'enter', 'enter');
            const [typed] = await ended(ends, 1);
            expect(typed, 'the first key composed, rather than standing in the cell as a letter (the fifteenth run\'s k穴)').toMatch(COMPOSED);
            await expect(cell(grid, 'D10')).toHaveText(typed);
            await expectFocusAt(grid, 'D11');
        });

        test(`ED-30 (k6, k6y): a second clause typed after the first was converted is one entry with it, read key by key and typed at machine speed alike ${named}`, async ({ page }) => {
            const grid = sheet(page);
            const ends = await compositionEnds(page);
            await osTyped(page, 'imeon', 'k', 'a', 'n', 'a', 'space');
            await expect.poll(() => fieldState(grid)).toMatchObject({ drawn: true, focused: true });
            await osTyped(page, 'k', 'a', 'n', 'j', 'i');
            await expect.poll(() => keyField(grid).inputValue(), 'no key of the second clause was lost').toMatch(/かんじ$/u);
            await osTyped(page, 'space', 'enter');
            await expect(editor(grid)).toBeFocused();
            await expect.poll(() => fieldState(grid)).toEqual({ value: '', drawn: false, focused: false });
            const typed = (await ends()).join('');
            expect(typed).toMatch(COMPOSED);
            await expect(editor(grid)).toHaveValue(typed);
            await osTyped(page, 'enter');
            await expect(cell(grid, 'D10')).toHaveText(typed);
            await expectFocusAt(grid, 'D11');

            // The same keys at once, in D11: the same entry (k6y). The IME converts as it did, its input
            // history being off.
            await osTyped(page, 'k', 'a', 'n', 'a', 'space', 'k', 'a', 'n', 'j', 'i', 'space', 'enter', 'enter');
            await expect(cell(grid, 'D11')).toHaveText(typed);
            await expectFocusAt(grid, 'D12');
            await expect(editor(grid)).toHaveCount(0);
        });

        test(`ED-30 (k7): two compositions and the keys after them at machine speed are typed in order, nothing lost, doubled or reordered ${named}`, async ({ page }) => {
            const grid = sheet(page);
            const ends = await compositionEnds(page);
            await osTyped(page, 'imeon', 'k', 'a', 'n', 'a', 'space', 'enter', 'd', 'e', 's', 'u', 'space', 'enter', 'enter');
            const both = await ended(ends, 2);
            for (const typed of both) expect(typed).toMatch(COMPOSED);
            await expect(cell(grid, 'D10')).toHaveText(both.join(''));
            await expectFocusAt(grid, 'D11');
            await expect(editor(grid)).toHaveCount(0);
            await expectKeyboardOn(grid);
        });

        for (const [id, pressed] of [['k8', 'D12'], ['k8x', 'D8']]) {
            test(`ED-30 (${id}): a press on ${pressed} during a composition puts it in D10, commits it there and selects ${pressed} ${named}`, async ({ page }) => {
                const grid = sheet(page);
                const ends = await compositionEnds(page);
                await osTyped(page, 'imeon', 'k', 'a', 'n', 'a');
                await expect.poll(() => fieldState(grid)).toEqual({ value: 'かな', drawn: true, focused: true });

                await osPressed(cell(grid, pressed));
                expect(await ended(ends, 1)).toEqual(['かな']);
                await expect(cell(grid, 'D10')).toHaveText('かな');
                await expectFocusAt(grid, pressed);
                await expect(editor(grid)).toHaveCount(0);
                await expect.poll(() => fieldState(grid)).toEqual({ value: '', drawn: false, focused: true });
            });
        }

        test(`ED-30 (k9): a press on the Formula Bar during a composition puts it in the edit, and the bar takes the keyboard holding it ${named}`, async ({ page }) => {
            const grid = sheet(page);
            const ends = await compositionEnds(page);
            await osTyped(page, 'imeon', 'k', 'a', 'n', 'a');
            await expect.poll(() => fieldState(grid)).toEqual({ value: 'かな', drawn: true, focused: true });

            const box = await bar(grid).boundingBox();
            await osPressed(bar(grid), { x: box.width - 8, y: box.height / 2 });
            expect(await ended(ends, 1)).toEqual(['かな']);
            await expect(bar(grid)).toBeFocused();
            await expect(bar(grid)).toHaveValue('かな');
            await expect(cell(grid, 'D10')).toHaveText('');

            await osTyped(page, 'enter');
            await expect(cell(grid, 'D10')).toHaveText('かな');
            await expect(editor(grid)).toHaveCount(0);
        });

        test(`ED-30 (k10): a press on a column's header during a composition ends it in D10 first, then selects the column ${named}`, async ({ page }) => {
            const grid = sheet(page);
            const ends = await compositionEnds(page);
            await osTyped(page, 'imeon', 'k', 'a', 'n', 'a');
            await expect.poll(() => fieldState(grid)).toEqual({ value: 'かな', drawn: true, focused: true });

            // A third of the way across, clear of the header's menu button.
            const header = columnHeader(grid, 'D');
            const box = await header.boundingBox();
            await osPressed(header, { x: box.width / 3, y: box.height / 2 });
            expect(await ended(ends, 1)).toEqual(['かな']);
            await expect(cell(grid, 'D10')).toHaveText('かな');
            await expectActiveDescendant(grid, /-r0c3$/);
            await expect(editor(grid)).toHaveCount(0);
            await expectKeyboardOn(grid);
        });

        test(`ED-30 (k12): Space with the IME on and nothing composed is the IME's ideographic space, and opens an edit holding it ${named}`, async ({ page }) => {
            const grid = sheet(page);
            const ends = await compositionEnds(page);
            await osTyped(page, 'imeon', 'space');
            expect(await ended(ends, 1)).toEqual(['　']);
            await expect(editor(grid)).toHaveValue('　');
            await expect(editor(grid)).toBeFocused();
            await expectFocusAt(grid, 'D10');
        });

        test(`ED-30 (k13, k13x): with the IME on and nothing composed, the arrows, Tab, Enter, Delete, and the copy and the paste are the grid's ${named}`, async ({ page }) => {
            const grid = sheet(page);
            const ends = await compositionEnds(page);
            await page.keyboard.type('150');
            await page.keyboard.press('Enter');
            await expect(cell(grid, 'D10')).toHaveText('150');
            await expectFocusAt(grid, 'D11');

            await osTyped(page, 'imeon', 'up');
            await expectFocusAt(grid, 'D10');
            await osTyped(page, 'down', 'right', 'tab', 'enter');
            await expectFocusAt(grid, 'F12');
            await expect(editor(grid)).toHaveCount(0);
            await expect.poll(() => fieldState(grid)).toEqual({ value: '', drawn: false, focused: true });

            await pressCell(grid, 'D10');
            await osTyped(page, 'ctrl+c');
            await pressCell(grid, 'D12');
            await osTyped(page, 'ctrl+v');
            await expect(cell(grid, 'D12')).toHaveText('150');
            await osTyped(page, 'delete');
            await expect(cell(grid, 'D12')).toHaveText('');
            await expect(cell(grid, 'D10')).toHaveText('150');
            expect(await ends(), 'nothing was composed').toEqual([]);
        });

        test(`ED-30 (k16): a press on the composition's own text loses nothing: what it composed reaches D10 ${named}`, async ({ page }) => {
            const grid = sheet(page);
            const ends = await compositionEnds(page);
            await osTyped(page, 'imeon', 'k', 'a', 'n', 'a');
            await expect.poll(() => fieldState(grid)).toEqual({ value: 'かな', drawn: true, focused: true });

            // 14 CSS px into D10, on the composition's text: the field takes no pointer, so the press is
            // D10's. Whether it ends the composition or the composition goes on (the runs recorded it going
            // on), Enter, Enter puts it in D10.
            const box = await cell(grid, 'D10').boundingBox();
            await osPressed(cell(grid, 'D10'), { x: 14, y: box.height / 2 });
            await osTyped(page, 'enter', 'enter');
            expect(await ended(ends, 1)).toEqual(['かな']);
            await expect(cell(grid, 'D10')).toHaveText('かな');
            await expect(editor(grid)).toHaveCount(0);
            await expectKeyboardOn(grid);
        });

        test(`DC-47/ED-30: a composition after =A1+ shows the field's own text while it lasts, and A1's colour comes back when it ends ${named}`, async ({ page }) => {
            const grid = sheet(page);
            const ends = await compositionEnds(page);
            await page.keyboard.type('=A1+');
            await expect(editor(grid)).toHaveValue('=A1+');
            await expect.poll(() => colouring(editor(grid))).toMatchObject({ shown: true, layer: 'visible' });

            await osTyped(page, 'imeon', 'k', 'a', 'n', 'a');
            await expect(editor(grid)).toHaveValue('=A1+かな');
            await expect.poll(() => colouring(editor(grid)), 'while it composes, the field is ahead of its layer and its own text shows')
                .toMatchObject({ shown: false, transparent: false, layer: 'hidden' });

            await osTyped(page, 'space', 'enter');
            const [typed] = await ended(ends, 1);
            const text = `=A1+${typed}`;
            await expect.poll(() => colouring(editor(grid)), 'the composition over, the layer holds the field\'s text, coloured')
                .toMatchObject({ value: text, text, drawn: text, shown: true, transparent: true, layer: 'visible' });
            expect((await colouring(editor(grid))).colours, 'A1, the first two characters after =, in its Reference colour').toMatch(/^1,2,\S+-reference-\d+$/);
        });
    });
}

// A cell that does not edit, and two Sheets on one page: k11 on /features and k14 on /sheets, under each
// Chrome, as the Windows runs took them.
for (const chrome of ['builtin', 'mud']) {
    test(`ED-30 (k11): over a cell that does not edit nothing composes and nothing opens; over one that does, the IME composes (${chrome}) ${chromeHost(chrome)}`, async ({ page }) => {
        await page.goto(chrome === 'builtin' ? '/features' : `/features?chrome=${chrome}`);
        const grid = page.locator('.ex-grid').first();
        await expect(grid.locator('.ex-row').first()).toBeVisible();
        // Book does not edit; Trader does.
        const book = grid.locator("[id$='r1c0']");
        const trader = grid.locator("[id$='r1c1']");
        await expect(book).not.toHaveText('');
        const shown = await book.textContent();
        await pressAt(book);
        await expect(keyField(grid)).toHaveAttribute('readonly', '');
        await osJapanese(page);
        const ends = await compositionEnds(page);

        await osTyped(page, 'imeon', 'k', 'a', 'n', 'a');
        await settled(grid);
        expect(await fieldState(grid)).toEqual({ value: '', drawn: false, focused: true });
        await expect(grid.locator('.ex-viewport .ex-editor')).toHaveCount(0);
        await expect(book).toHaveText(shown);
        expect(await ends(), 'nothing composed').toEqual([]);

        await pressAt(trader);
        await expect(keyField(grid)).not.toHaveAttribute('readonly', '');
        await osTyped(page, 'imeon', 'k', 'a', 'n', 'a', 'space', 'enter', 'enter');
        const [typed] = await ended(ends, 1);
        expect(typed).toMatch(COMPOSED);
        await expect(trader).toHaveText(typed);
        await expect(grid.locator('.ex-viewport .ex-editor')).toHaveCount(0);
    });

    test(`ED-30 (k14): a composition in the second Sheet is its own: the first Sheet's open edit stands, and nothing reaches it (${chrome}) ${chromeHost(chrome)}`, async ({ page }) => {
        await page.goto(chrome === 'builtin' ? '/sheets' : `/sheets?chrome=${chrome}`);
        await expect(cell(sheet(page, 0), 'A1')).toHaveText('Left');
        await expect(cell(sheet(page, 1), 'A1')).toHaveText('Right');
        const left = sheet(page, 0);
        const right = sheet(page, 1);
        await pressCell(left, 'B2');
        await osJapanese(page);
        const ends = await compositionEnds(page);

        await osTyped(page, 'imeon', 'k', 'a', 'n', 'a', 'enter');
        expect(await ended(ends, 1)).toEqual(['かな']);
        await expect(editor(left)).toHaveValue('かな');
        await expect(editor(left)).toBeFocused();

        await pressCell(right, 'B2');
        await expectKeyboardOn(right);
        await osTyped(page, 'k', 'a', 'n', 'a');
        await expect.poll(() => fieldState(right)).toEqual({ value: 'かな', drawn: true, focused: true });
        await expect(editor(left)).toHaveValue('かな');
        await expect(editor(right)).toHaveCount(0);
        await expect(cell(left, 'B2')).toHaveText('');
    });
}
