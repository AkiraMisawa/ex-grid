import { test, expect, alterPage, setRoundTrip } from './fixtures.mjs';
import { pixelsApart, stillPictures } from './pixels.mjs';
import { SERVER } from './hosting.mjs';
import { sheet, cell, pressCell, editor, bar, clickBarEnd, boxOf, typeSteadily, stretchesOf } from './sheet-helpers.mjs';

// The coloured text in the editor (ADR-0057, "The coloured text is a layer that shows only while it
// is up to date"; DC-47, DC-48), as ExSheet declares its References on /sheet, under the built-in
// Chrome and ExGrid.MudBlazor's. Beneath each editor surface the core renders the text with each
// Reference in its colour; the field's own text turns transparent only in the surface the edit is
// in — the one holding DOM focus; Excel colours the cell's text or the Formula Bar's, never both
// (the eighth Windows run, range-finder cases 1, 21 and 1fb) — and only while the layer holds the
// field's value. The criterion is a frame: no painted frame may show transparent field text over a
// layer that differs, so the typing burst is sampled every animation frame in the page.

// Tall enough that the Sheet, Formula Bar to horizontal scrollbar, is inside the window.
test.use({ viewport: { width: 1280, height: 1000 } });

test.beforeEach(async ({ page }) => {
    await page.goto('/sheet');
    await expect(page.locator('#demo-interactive')).toBeAttached({ timeout: 30_000 });
    await expect(cell(sheet(page), 'A1')).toHaveText('Item');
    // The Linked Table's first snapshot lands 1.5 s after the Sheet opens, and re-renders it.
    await expect(cell(sheet(page), 'B12')).toHaveText('318.25', { timeout: 10_000 });
});

// Reopens /sheet under ExGrid.MudBlazor's Chrome; the built-in one is what beforeEach opened.
async function underChrome(page, chrome) {
    if (chrome === 'builtin') {
        return;
    }
    await page.goto(`/sheet?chrome=${chrome}`);
    await expect(page.locator('#demo-interactive')).toBeAttached({ timeout: 30_000 });
    await expect(cell(sheet(page), 'A1')).toHaveText('Item');
    await expect(page.locator('.mud-ex-formula-bar-text, .mud-ex-name-box').first()).toBeAttached();
    await expect(cell(sheet(page), 'B12')).toHaveText('318.25', { timeout: 10_000 });
}

/** What a field and the layer beneath it show: the field's value, the layer's text, whether the
 * listener has let the layer show, and what the stylesheet made of that. */
const colouring = (field) => field.evaluate((input) => {
    const layer = input.previousElementSibling?.classList.contains('ex-reference-text') ? input.previousElementSibling : null;
    return {
        value: input.value,
        text: layer?.getAttribute('data-ex-text') ?? null,
        drawn: layer?.textContent ?? null,
        shown: input.classList.contains('ex-reference-text-shown'),
        fill: getComputedStyle(input).webkitTextFillColor,
        layer: layer ? getComputedStyle(layer).visibility : null,
    };
});

const TRANSPARENT = 'rgba(0, 0, 0, 0)';

/** The field's text is the layer's, coloured: the layer shows and the field's own text is transparent. */
async function expectColoured(field, text) {
    await expect.poll(() => colouring(field))
        .toEqual({ value: text, text, drawn: text, shown: true, fill: TRANSPARENT, layer: 'visible' });
}

/** The field's own plain text shows, and its layer does not: no edit is open, the edit is in the
 * other surface, or the field is ahead of its layer. */
async function expectPlain(field, text) {
    await expect(field).toHaveValue(text);
    await expect.poll(async () => {
        const now = await colouring(field);
        return { shown: now.shown, own: now.fill !== TRANSPARENT, layer: now.layer };
    }).toEqual({ shown: false, own: true, layer: 'hidden' });
}

/**
 * Samples every editor surface of the page in every animation frame from now to the end of the
 * test: whenever the field's text is transparent or the layer shows, the layer's text — as written
 * on it, and as drawn — must be the field's value, and every highlight over the layer's run must
 * cover exactly the characters the core named for it (DC-47; ADR-0057, note of 2026-10-01). A
 * highlight over a hidden layer is a stretch coloured where nothing is shown, and is wrong too. The
 * page is left as it was.
 */
async function recordFrames(page) {
    await alterPage(page, () => {
        const frames = { sampled: 0, shown: 0, coloured: 0, wrong: [] };
        let request = 0;
        const highlighted = (run) => {
            const ranges = [];
            CSS.highlights.forEach((highlight, name) => {
                for (const range of highlight) {
                    if (range.startContainer === run) {
                        ranges.push(`${range.startOffset},${range.endOffset - range.startOffset},${name}`);
                    }
                }
            });
            return ranges.sort();
        };
        const sample = () => {
            for (const layer of document.querySelectorAll('.ex-reference-text')) {
                const field = layer.nextElementSibling;
                if (!(field instanceof HTMLInputElement)) {
                    continue;
                }
                const transparent = getComputedStyle(field).webkitTextFillColor === 'rgba(0, 0, 0, 0)';
                const visible = getComputedStyle(layer).visibility === 'visible';
                const run = layer.firstElementChild?.firstChild ?? null;
                const ranges = run === null ? [] : highlighted(run);
                if (transparent || visible) {
                    frames.shown++;
                    const text = layer.getAttribute('data-ex-text');
                    const named = (layer.getAttribute('data-ex-colours') ?? '').split(' ').filter((entry) => entry !== '').sort();
                    frames.coloured += ranges.length > 0 ? 1 : 0;
                    if (text !== field.value || layer.textContent !== field.value || JSON.stringify(ranges) !== JSON.stringify(named)) {
                        frames.wrong.push({ value: field.value, text, drawn: layer.textContent, transparent, visible, ranges, named });
                    }
                } else if (ranges.length > 0) {
                    frames.wrong.push({ value: field.value, hidden: true, ranges });
                }
            }
            frames.sampled++;
            request = requestAnimationFrame(sample);
        };
        request = requestAnimationFrame(sample);
        window.__referenceTextFrames = frames;
        return () => {
            cancelAnimationFrame(request);
            delete window.__referenceTextFrames;
        };
    });
}

const framesRecorded = (page) => page.evaluate(() => window.__referenceTextFrames);

// ---------------------------------------------------------------------------------------------
// Only while it is the field's text (DC-47)

for (const chrome of ['builtin', 'mud']) {
    // On a circuit the colours answer a keystroke a round trip after it: a burst types past them,
    // and the field's own text shows until the typing pauses. On WebAssembly the same burst runs
    // as the case without a round trip.
    test(`DC-47: a burst of typing on a 150 ms circuit never shows transparent field text over a layer that differs, and the colours come back when it pauses (${chrome} Chrome)`, async ({ page }) => {
        await underChrome(page, chrome);
        const grid = sheet(page);
        await pressCell(grid, 'F3');
        await page.keyboard.type('=');
        await expect(editor(grid)).toHaveValue('=');
        await recordFrames(page);
        await setRoundTrip(150);

        await page.keyboard.type('SUM(A1,B2)+C3*D4-A1', { delay: 25 });

        const typed = '=SUM(A1,B2)+C3*D4-A1';
        await expect(editor(grid)).toHaveValue(typed);
        await expectColoured(editor(grid), typed);
        // The edit is in the cell: the Formula Bar shows the same text, plain.
        await expectPlain(bar(grid), typed);
        const frames = await framesRecorded(page);
        expect(frames.wrong).toEqual([]);
        expect(frames.sampled).toBeGreaterThan(10);
        expect(frames.shown).toBeGreaterThan(0);
        await setRoundTrip(0);
        await page.keyboard.press('Escape');
        await expect(editor(grid)).toHaveCount(0);
    });

    test(`DC-47: on WebAssembly the colours follow each keystroke, each Reference in a colour of its own (${chrome} Chrome)`, async ({ page }) => {
        test.skip(SERVER, 'on a circuit a keystroke is a round trip from its colours: the burst above is that case');
        await underChrome(page, chrome);
        const grid = sheet(page);
        await pressCell(grid, 'F3');
        await recordFrames(page);

        const formula = '=A1+B2*SUM(C3:D4)';
        for (let i = 1; i <= formula.length; i++) {
            await page.keyboard.type(formula[i - 1]);
            await expectColoured(editor(grid), formula.slice(0, i));
        }
        await expectPlain(bar(grid), formula);

        // The layer's text is one run, and its References are coloured over it (ADR-0057, note of
        // 2026-10-01).
        expect(await editor(grid).evaluate((input) => input.previousElementSibling.firstElementChild.childNodes.length)).toBe(1);
        const references = await stretchesOf(editor(grid));
        expect(references.map((reference) => [reference.text, reference.place]))
            .toEqual([['A1', 1], ['B2', 2], ['C3:D4', 3]]);
        expect(new Set(references.map((reference) => reference.ink)).size).toBe(3);
        expect((await framesRecorded(page)).wrong).toEqual([]);
        await page.keyboard.press('Escape');
        await expect(editor(grid)).toHaveCount(0);
    });

    // A layer that has just appeared beneath its field is not a change of its text, and an edit
    // opened on the text the field already shows changes nothing in the field: the listener hears
    // the Cell Editor take DOM focus, and the Formula Bar's layer, which stands empty while no edit
    // is open, being given the edit's text.
    test(`DC-47: an edit opened on a Formula, by F2 or by a press into the Formula Bar, is coloured in that surface before anything is typed, and the other stays plain (${chrome} Chrome)`, async ({ page }) => {
        await underChrome(page, chrome);
        const grid = sheet(page);
        await pressCell(grid, 'D2');
        // No edit open: nothing is coloured.
        await expectPlain(bar(grid), '=B2*C2');

        await page.keyboard.press('F2');
        await expectColoured(editor(grid), '=B2*C2');
        await expectPlain(bar(grid), '=B2*C2');
        // The caret keeps the field's colour, and selected text is drawn by the field.
        const kept = await editor(grid).evaluate((input) => ({
            caret: getComputedStyle(input).caretColor,
            selected: getComputedStyle(input, '::selection').webkitTextFillColor,
        }));
        expect(kept.caret).not.toBe(TRANSPARENT);
        expect(kept.selected).not.toBe(TRANSPARENT);
        await page.keyboard.press('Escape');
        await expect(editor(grid)).toHaveCount(0);
        await expectPlain(bar(grid), '=B2*C2');

        const box = await boxOf(bar(grid));
        await bar(grid).click({ position: { x: box.width / 2, y: box.height / 2 } });
        await expect(editor(grid)).toHaveCount(1);
        await expectColoured(bar(grid), '=B2*C2');
        await expectPlain(editor(grid), '=B2*C2');
        await page.keyboard.press('Escape');
        await expect(editor(grid)).toHaveCount(0);
        await expectPlain(bar(grid), '=B2*C2');
    });

    // The edit is in whichever surface holds DOM focus, and a press moves it from one to the other
    // mid-edit: the colours go with it, at once, and what is typed in the bar is coloured there.
    test(`DC-47: the colours follow the edit from the Cell Editor into the Formula Bar and back, the other surface plain (${chrome} Chrome)`, async ({ page }) => {
        await underChrome(page, chrome);
        const grid = sheet(page);
        await pressCell(grid, 'F3');
        await page.keyboard.type('=A1+B2');
        await expectColoured(editor(grid), '=A1+B2');
        await expectPlain(bar(grid), '=A1+B2');

        await clickBarEnd(grid);
        await expect(bar(grid)).toBeFocused();
        await expectColoured(bar(grid), '=A1+B2');
        await expectPlain(editor(grid), '=A1+B2');
        await typeSteadily(page, bar(grid), '+C3');
        await expectColoured(bar(grid), '=A1+B2+C3');
        await expectPlain(editor(grid), '=A1+B2+C3');

        // Back into the cell, by a press in the Cell Editor's text.
        await editor(grid).click();
        await expect(editor(grid)).toBeFocused();
        await expectColoured(editor(grid), '=A1+B2+C3');
        await expectPlain(bar(grid), '=A1+B2+C3');
        await page.keyboard.press('Escape');
        await expect(editor(grid)).toHaveCount(0);
    });

    // An IME holds its composition in the field, ahead of anything the core has rendered — on
    // WebAssembly the core renders the composing text straight back, and the texts agree, but the
    // composition is still the field's to draw, underline and all. What Chrome sends, checked on
    // 2026-09-30: every input of a composition, its last included, carries isComposing, and
    // compositionend follows with no input after it. So the listener hears compositionend too, and
    // the colours come back when the composition ends, once the layer holds its text, with no
    // keystroke after it (ticket 29, decided with the user 2026-09-30).
    test(`DC-47: an IME composition shows the field's own text while it lasts, never the layer over it, and the colours come back when it ends (${chrome} Chrome)`, async ({ page }) => {
        await underChrome(page, chrome);
        const grid = sheet(page);
        await pressCell(grid, 'F3');
        await page.keyboard.type('=A1&');
        await expectColoured(editor(grid), '=A1&');
        await recordFrames(page);
        await setRoundTrip(150);
        const client = await page.context().newCDPSession(page);

        await client.send('Input.imeSetComposition', { text: 'に', selectionStart: 1, selectionEnd: 1 });
        await client.send('Input.imeSetComposition', { text: 'にほ', selectionStart: 2, selectionEnd: 2 });

        await expect(editor(grid)).toHaveValue('=A1&にほ');
        // Long past the round trip, the core has rendered the composing text, and the field still
        // draws it itself: no highlight is left over the hidden layer.
        await expect.poll(async () => (await colouring(editor(grid))).text).toBe('=A1&にほ');
        await page.waitForTimeout(400);
        await expectPlain(editor(grid), '=A1&にほ');
        expect(await stretchesOf(editor(grid))).toEqual([]);

        // The composition ends, and nothing is typed after it: compositionend shows the layer, and
        // its Reference is coloured again over the one run (ADR-0057, note of 2026-10-01).
        await client.send('Input.insertText', { text: '日本' });
        await expect(editor(grid)).toHaveValue('=A1&日本');
        await expectColoured(editor(grid), '=A1&日本');
        await expect.poll(async () => (await stretchesOf(editor(grid))).map((stretch) => [stretch.text, stretch.place, stretch.ink === stretch.colour]))
            .toEqual([['A1', 1, true]]);
        expect((await framesRecorded(page)).wrong).toEqual([]);
        await client.detach();
        await setRoundTrip(0);
        await page.keyboard.press('Escape');
        await expect(editor(grid)).toHaveCount(0);
    });
}

// A page that loses the Wrapper's shape: the Chrome's fields are then the browser's own inputs, with
// a ground of their own and not the core's box's width. The core makes a shown field see-through,
// so the layer's text shows, in colour, over the ground the core's box paints, rather than nothing
// at all (ADR-0057, "The Wrapper's shape is required…"). /sheet?chrome=mud has the shape; taking
// the paper's class away takes every rule of mud-ex-grid.css with it.
test("ADR-0057: under the Mud Chrome without the Wrapper's stylesheet, the Cell Editor's text still shows, in the layer's colours", async ({ page }) => {
    await underChrome(page, 'mud');
    await alterPage(page, () => {
        const paper = document.querySelector('.mud-ex-grid:has(.ex-formula-bar)');
        paper.classList.remove('mud-ex-grid');
        return () => paper.classList.add('mud-ex-grid');
    });
    const grid = sheet(page);
    await pressCell(grid, 'F3');
    await page.keyboard.type('=A1+B2');

    await expectColoured(editor(grid), '=A1+B2');
    const grounds = await editor(grid).evaluate((input) => ({
        field: getComputedStyle(input).backgroundColor,
        box: getComputedStyle(input.closest('.ex-editor')).backgroundColor,
    }));
    expect(grounds.field).toBe(TRANSPARENT);
    expect(grounds.box).not.toBe(TRANSPARENT);
    // What is seen is the layer's text, through the field: hiding the layer takes it away.
    const box = editor(grid).locator('xpath=..');
    await alterPage(page, () => {
        const style = document.createElement('style');
        style.textContent = '[data-layer="hidden"] .ex-reference-text { visibility: hidden !important; }';
        document.head.append(style);
        return () => style.remove();
    });
    const { shown, hidden } = await stillPictures(box, { mark: 'data-layer', ways: ['shown', 'hidden'] });
    expect(pixelsApart(shown, hidden, 96).apart, 'pixels the layer puts in the Cell Editor').toBeGreaterThan(20);
    await page.keyboard.press('Escape');
    await expect(editor(grid)).toHaveCount(0);
});

// ---------------------------------------------------------------------------------------------
// The Reference Point is writing, shown selected (ADR-0051; ADR-0057, "What cases 24–32 settled")

/** The field's selection: the look is never one. */
const selectionOf = (field) => field.evaluate((input) => [input.selectionStart, input.selectionEnd]);

// Excel's grey, #c6c6c6, over the light ground /sheet has (the default of
// --ex-reference-pointed-background).
const POINTED_GROUND = 'rgb(198, 198, 198)';

/** The one stretch pointed, on the grey, its ink a shade of its colour and not the colour itself. */
async function expectPointedLook(field, text) {
    await expect.poll(async () => (await stretchesOf(field)).filter((span) => span.pointed).map((span) => span.text)).toEqual([text]);
    const span = (await stretchesOf(field)).find((one) => one.pointed);
    expect(span.ground).toBe(POINTED_GROUND);
    expect(span.ink).not.toBe(span.colour);
    expect(span.ink).not.toBe(TRANSPARENT);
}

// The palette's first two colours, #326ac7 and #c0353e, and Excel's shade of each for the pointed
// Reference's text, #0401a2 and #630101 (Part B of the eighth Windows run, cases 20 and 20x): the
// light defaults of --ex-reference-1-pointed and --ex-reference-2-pointed (DC-56).
const FIRST_COLOUR = 'rgb(50, 106, 199)';
const SECOND_COLOUR = 'rgb(192, 53, 62)';
const FIRST_POINTED = 'rgb(4, 1, 162)';
const SECOND_POINTED = 'rgb(99, 1, 1)';

/** The one stretch pointed wears this colour, and on the grey its text is this shade of it. */
async function expectPointedShade(field, text, colour, ink) {
    await expectPointedLook(field, text);
    const span = (await stretchesOf(field)).find((one) => one.pointed);
    expect({ colour: span.colour, ink: span.ink, ground: span.ground }).toEqual({ colour, ink, ground: POINTED_GROUND });
}

for (const chrome of ['builtin', 'mud']) {
    test(`ADR-0051/0057: after =SUM( the Reference Point writes is shown selected in the Cell Editor, and the Formula Bar stays plain (${chrome} Chrome)`, async ({ page }) => {
        await underChrome(page, chrome);
        const grid = sheet(page);
        await pressCell(grid, 'F3');
        await page.keyboard.type('=SUM(');
        await expect(editor(grid)).toHaveValue('=SUM(');

        await page.keyboard.press('ArrowDown');

        await expectColoured(editor(grid), '=SUM(F4');
        await expectPointedLook(editor(grid), 'F4');
        // A look on the layer, not a selection of the field's text: the caret after the Reference.
        expect(await selectionOf(editor(grid))).toEqual([7, 7]);
        await expectPlain(bar(grid), '=SUM(F4');
        await page.keyboard.press('Escape');
        await expect(editor(grid)).toHaveCount(0);
    });

    test(`ADR-0051/0057: pointed from the Formula Bar after =SUM(, the Reference is shown selected there, and the Cell Editor stays plain (${chrome} Chrome)`, async ({ page }) => {
        await underChrome(page, chrome);
        const grid = sheet(page);
        await pressCell(grid, 'F3');
        await clickBarEnd(grid);
        await expect(bar(grid)).toBeFocused();
        await typeSteadily(page, bar(grid), '=SUM(');
        // Caret, as a press into the bar leaves it: F2 points (ADR-0051).
        await page.keyboard.press('F2');

        await page.keyboard.press('ArrowDown');

        await expectColoured(bar(grid), '=SUM(F4');
        await expectPointedLook(bar(grid), 'F4');
        expect(await selectionOf(bar(grid))).toEqual([7, 7]);
        await expectPlain(editor(grid), '=SUM(F4');
        await page.keyboard.press('Escape');
        await expect(editor(grid)).toHaveCount(0);
    });

    test(`ADR-0051/0057: = ↓ ↓ writes =F5 with no grey, however often it is pointed (${chrome} Chrome)`, async ({ page }) => {
        await underChrome(page, chrome);
        const grid = sheet(page);
        await pressCell(grid, 'F3');
        await page.keyboard.type('=');
        await expect(editor(grid)).toHaveValue('=');

        await page.keyboard.press('ArrowDown');
        await page.keyboard.press('ArrowDown');

        await expectColoured(editor(grid), '=F5');
        await expect(grid.locator('.ex-selection .ex-point')).toHaveCount(1);
        const spans = await stretchesOf(editor(grid));
        expect(spans.map((span) => [span.text, span.pointed])).toEqual([['F5', false]]);
        expect(spans[0].ground).not.toBe(POINTED_GROUND);
        expect(spans[0].ink).toBe(spans[0].colour);
        await page.keyboard.press('Escape');
        await expect(editor(grid)).toHaveCount(0);
    });

    // Excel's case 32: the grey is not a selection that typing replaces. The digit follows the
    // Reference, and Point ends.
    test(`ADR-0051/0057: 5 typed after =D11+ ↓ ↓ follows the Reference, and the grey goes with Point (${chrome} Chrome)`, async ({ page }) => {
        await underChrome(page, chrome);
        const grid = sheet(page);
        await pressCell(grid, 'F3');
        await page.keyboard.type('=D11+');
        await expect(editor(grid)).toHaveValue('=D11+');
        await page.keyboard.press('ArrowDown');
        await page.keyboard.press('ArrowDown');
        await expectColoured(editor(grid), '=D11+F5');
        await expectPointedLook(editor(grid), 'F5');

        await page.keyboard.type('5');

        await expectColoured(editor(grid), '=D11+F55');
        await expect(grid.locator('.ex-selection .ex-point')).toHaveCount(0);
        expect((await stretchesOf(editor(grid))).map((span) => [span.text, span.pointed])).toEqual([['D11', false], ['F55', false]]);
        await page.keyboard.press('Escape');
        await expect(editor(grid)).toHaveCount(0);
    });

    // Excel's cases 20 and 20x, typed from D10 as the run typed them: ↓ after =D11+ points D11
    // again, which shares the first Reference's colour; ↓ once more points D12, a second colour.
    test(`DC-56: the pointed Reference's text is Excel's shade of its colour — #0401a2 for the first, #630101 for the second — on #c6c6c6 (${chrome} Chrome)`, async ({ page }) => {
        await underChrome(page, chrome);
        const grid = sheet(page);
        await pressCell(grid, 'D10');
        await page.keyboard.type('=D11+');
        await expect(editor(grid)).toHaveValue('=D11+');

        await page.keyboard.press('ArrowDown');
        await expectColoured(editor(grid), '=D11+D11');
        await expectPointedShade(editor(grid), 'D11', FIRST_COLOUR, FIRST_POINTED);
        await page.keyboard.press('ArrowDown');
        await expectColoured(editor(grid), '=D11+D12');
        await expectPointedShade(editor(grid), 'D12', SECOND_COLOUR, SECOND_POINTED);
        await page.keyboard.press('Escape');
        await expect(editor(grid)).toHaveCount(0);

        // One Reference, pointed after =SUM( and moved on: the first colour's shade.
        await pressCell(grid, 'D10');
        await page.keyboard.type('=SUM(');
        await expect(editor(grid)).toHaveValue('=SUM(');
        await page.keyboard.press('ArrowDown');
        await page.keyboard.press('ArrowDown');
        await expectColoured(editor(grid), '=SUM(D12');
        await expectPointedShade(editor(grid), 'D12', FIRST_COLOUR, FIRST_POINTED);
        await page.keyboard.press('Escape');
        await expect(editor(grid)).toHaveCount(0);
        await expect(cell(grid, 'D10')).toHaveText('');
    });
}

// ---------------------------------------------------------------------------------------------
// Over the right characters (DC-48)

/**
 * A stylesheet over the page that draws a field one of two ways, by a mark the test sets on the
 * field: `own` — by its own text, the layer hidden, as it is while the layer is behind; `layer` —
 * by the layer, as the listener has it, with the colours taken off. A word the spelling check
 * marks is drawn by the field in its highlight's colour, which the stylesheet takes away only while
 * the layer shows, so `own` gives it back.
 *
 * Both ways draw in black, whatever ink the Chrome gives the field, on the white ground of the
 * light scheme the tests run in. A glyph moved half a pixel changes an edge pixel by about half the
 * ink's contrast with its ground: 128 levels in black, over DC-48's threshold of 96, but 95 in
 * MudBlazor's #424242, which the Formula Bar wears under ExGrid.MudBlazor, so the comparison could
 * not see a layer half a pixel out there. On Linux the self-test below found one only in the column
 * where the field's left edge cuts through a glyph, and found none on CI's Server host, where End
 * had left the field scrolled one pixel further: 876 px, against 875 in every local run (2026-10-01).
 */
async function overlayDrawingWays(page) {
    await alterPage(page, () => {
        const style = document.createElement('style');
        style.textContent = `
            .ex-reference-text:has(+ [data-drawn]), .ex-reference-text + [data-drawn] { color: #000 !important; }
            .ex-reference-text:has(+ [data-drawn="own"]) { visibility: hidden !important; }
            .ex-reference-text + [data-drawn="own"] { -webkit-text-fill-color: currentColor !important; }
            .ex-reference-text + [data-drawn="own"]::spelling-error,
            .ex-reference-text + [data-drawn="own"]::grammar-error { color: inherit !important; }
            .ex-reference-text + input.ex-editor[data-drawn="own"] { background: var(--ex-editor-background, Canvas) !important; }
            .ex-reference-text:has(+ [data-drawn="layer"]) {
                --ex-reference-text-1: currentColor; --ex-reference-text-2: currentColor; --ex-reference-text-3: currentColor;
                --ex-reference-text-4: currentColor; --ex-reference-text-5: currentColor; --ex-reference-text-6: currentColor;
                --ex-reference-text-7: currentColor;
            }`;
        document.head.append(style);
        return () => style.remove();
    });
}

/** The field drawn both ways, pictured while the page holds still (`stillPictures`). Where the
 * layer's characters stand where the field's do, the two are one picture. */
const drawnBothWays = (field) => stillPictures(field, { mark: 'data-drawn', ways: ['own', 'layer'] });

/** DC-48's comparison: pixels apart by more than 96 on some channel. */
const DC48_THRESHOLD = 96;

// Longer than either surface on /sheet, with a handful of References, as a Formula a user writes
// is. The layer once drew each Reference as a span, a run of text each, and the browser rounds each
// run's width up to its layout unit, 1/64 px, where the field's text is one run: at End of this
// Formula the layer stood 1/16 px right of the field, and under ExSheet.MudBlazor's Roboto one edge
// pixel crossed the threshold below (ticket 86). The layer is one run now, coloured by highlights
// (ADR-0057, note of 2026-10-01), so the two are one picture at both ends.
const FORMULA = '=IF(AND(B2>0,C2>0),ROUND(B2*C2*(1+D2),2),"Enter both the quantity and the price '
    + 'before the amount of this line is worked out, and check the discount in the next column")'
    + '&" as of "&TEXT(B7,"yyyy-mm-dd")&", due "&TEXT(B8,"yyyy-mm-dd")';

/** Opens an edit of FORMULA on F3 in one surface, in Caret, and lays the drawing ways over the page. */
async function editFormula(page, chrome, surface) {
    await underChrome(page, chrome);
    const grid = sheet(page);
    await pressCell(grid, 'F3');
    const field = surface === 'cell' ? editor(grid) : bar(grid);
    // Caret, where Home and End move the caret rather than the Focus (ADR-0010) — on macOS
    // too, where the listener answers them (ticket 32).
    if (surface === 'cell') {
        await page.keyboard.press('F2');
    } else {
        await clickBarEnd(grid);
    }
    await expect(field).toBeFocused();
    await page.keyboard.insertText(FORMULA);
    await expectColoured(field, FORMULA);
    await overlayDrawingWays(page);
    return { grid, field };
}

/** Moves the caret to an end, and waits until the field — and the layer's line with it — has
 * scrolled to show it: past the start at the end, back to it at the start (DC-48). */
async function caretTo(page, field, end) {
    await page.keyboard.press(end);
    await expect.poll(() => field.evaluate((input, at) => {
        const line = input.previousElementSibling.firstElementChild;
        const scrolled = at === 'End' ? input.scrollLeft > 0 : input.scrollLeft === 0;
        return scrolled && line.scrollLeft === input.scrollLeft ? 'with the field' : `field ${input.scrollLeft}, layer ${line.scrollLeft}`;
    }, end)).toBe('with the field');
    await expectColoured(field, FORMULA);
}

for (const chrome of ['builtin', 'mud']) {
    for (const surface of ['cell', 'bar']) {
        test(`DC-48: a Formula longer than the ${surface === 'cell' ? 'Cell Editor' : 'Formula Bar'} keeps its colours over the right characters at either end (${chrome} Chrome)`, async ({ page }) => {
            const { grid, field } = await editFormula(page, chrome, surface);

            // The same font, size, padding and letter spacing (DC-48).
            const metrics = await field.evaluate((input) => {
                const pick = (element) => {
                    const style = getComputedStyle(element);
                    return [style.fontFamily, style.fontSize, style.fontWeight, style.lineHeight, style.paddingLeft,
                        style.paddingRight, style.letterSpacing, style.wordSpacing];
                };
                return { field: pick(input), layer: pick(input.previousElementSibling) };
            });
            expect(metrics.layer).toEqual(metrics.field);

            for (const end of ['End', 'Home']) {
                await caretTo(page, field, end);
                const { own, layer } = await drawnBothWays(field);
                const apart = pixelsApart(own, layer, DC48_THRESHOLD);
                test.info().annotations.push({ type: `DC-48 ${end}`, description: JSON.stringify(apart) });
                expect(apart.apart, `pixels the layer's text puts somewhere the field's is not, at ${end}`).toBe(0);
            }

            await page.keyboard.press('Escape');
            await expect(editor(grid)).toHaveCount(0);
        });
    }
}

// The comparison above finds a layer that stands where the field does not. Each misplacement here is
// a rule over the line of a layer whose field the test marks, drawn only while the layer draws the
// field (`data-drawn="layer"`), at End: half a pixel either way, another font, other letter spacing.
// Each is on the line, which the computed-style check above does not read, so only the pixels can
// tell. Were a rule not to apply, nothing would be found apart, and the test would fail. The threshold
// is DC-48's own, with no allowance: the layer is one run, as the field is (ADR-0057, note of
// 2026-10-01), and stands exactly where the field does.
const MISPLACED = {
    'half a pixel right': 'position: relative; left: 0.5px;',
    'half a pixel left': 'position: relative; left: -0.5px;',
    'in Georgia': 'font-family: Georgia, "Times New Roman", serif;',
    'with 0.5px letter spacing': 'letter-spacing: 0.5px;',
};

for (const chrome of ['builtin', 'mud']) {
    for (const surface of ['cell', 'bar']) {
        test(`DC-48: the comparison finds a layer half a pixel out, in another font or with other letter spacing, in the ${surface === 'cell' ? 'Cell Editor' : 'Formula Bar'} (${chrome} Chrome)`, async ({ page }) => {
            const { grid, field } = await editFormula(page, chrome, surface);
            await alterPage(page, (misplaced) => {
                const style = document.createElement('style');
                style.textContent = Object.entries(misplaced)
                    .map(([name, rule]) => `.ex-reference-text:has(+ [data-drawn="layer"][data-misplaced="${name}"]) .ex-reference-text-line { ${rule} }`)
                    .join('\n');
                document.head.append(style);
                return () => style.remove();
            }, MISPLACED);
            await caretTo(page, field, 'End');

            for (const misplacement of Object.keys(MISPLACED)) {
                await field.evaluate((input, name) => input.setAttribute('data-misplaced', name), misplacement);
                const { own, layer } = await drawnBothWays(field);
                await field.evaluate((input) => input.removeAttribute('data-misplaced'));
                const apart = pixelsApart(own, layer, DC48_THRESHOLD);
                test.info().annotations.push({ type: `DC-48 ${misplacement}`, description: JSON.stringify(apart) });
                expect(apart.apart, `pixels found apart with the layer ${misplacement}`).toBeGreaterThan(0);
            }

            await page.keyboard.press('Escape');
            await expect(editor(grid)).toHaveCount(0);
        });
    }
}
