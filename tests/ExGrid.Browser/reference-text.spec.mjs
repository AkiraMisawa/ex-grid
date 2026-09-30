import { test, expect, alterPage, setRoundTrip, twoFrames } from './fixtures.mjs';
import { SERVER } from './hosting.mjs';
import { sheet, cell, pressCell, editor, bar, clickBarEnd, boxOf, typeSteadily } from './sheet-helpers.mjs';

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
 * on it, and as drawn — must be the field's value (DC-47). The page is left as it was.
 */
async function recordFrames(page) {
    await alterPage(page, () => {
        const frames = { sampled: 0, shown: 0, wrong: [] };
        let request = 0;
        const sample = () => {
            for (const layer of document.querySelectorAll('.ex-reference-text')) {
                const field = layer.nextElementSibling;
                if (!(field instanceof HTMLInputElement)) {
                    continue;
                }
                const transparent = getComputedStyle(field).webkitTextFillColor === 'rgba(0, 0, 0, 0)';
                const visible = getComputedStyle(layer).visibility === 'visible';
                if (transparent || visible) {
                    frames.shown++;
                    const text = layer.getAttribute('data-ex-text');
                    if (text !== field.value || layer.textContent !== field.value) {
                        frames.wrong.push({ value: field.value, text, drawn: layer.textContent, transparent, visible });
                    }
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

        const spans = await editor(grid).evaluate((input) => [...input.previousElementSibling.querySelectorAll('span')]
            .map((span) => ({ text: span.textContent, class: span.className, colour: getComputedStyle(span).color })));
        expect(spans.map((span) => [span.text, span.class]))
            .toEqual([['A1', 'ex-reference-1'], ['B2', 'ex-reference-2'], ['C3:D4', 'ex-reference-3']]);
        expect(new Set(spans.map((span) => span.colour)).size).toBe(3);
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
    // compositionend follows with no input after it. The listener hears inputs and the layer's
    // text, not compositionend (ADR-0021's note of ADR-0057), so after the composition ends the
    // colours come back with the next keystroke rather than at once: uncoloured for a moment, never
    // coloured on the wrong characters.
    test(`DC-47: an IME composition shows the field's own text while it lasts, and never the layer over it (${chrome} Chrome)`, async ({ page }) => {
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
        // draws it itself.
        await expect.poll(async () => (await colouring(editor(grid))).text).toBe('=A1&にほ');
        await page.waitForTimeout(400);
        await expectPlain(editor(grid), '=A1&にほ');

        await client.send('Input.insertText', { text: '日本' });
        await expect(editor(grid)).toHaveValue('=A1&日本');
        await expect.poll(async () => (await colouring(editor(grid))).text).toBe('=A1&日本');
        await page.keyboard.type('&');
        await expectColoured(editor(grid), '=A1&日本&');
        expect((await framesRecorded(page)).wrong).toEqual([]);
        await client.detach();
        await setRoundTrip(0);
        await page.keyboard.press('Escape');
        await expect(editor(grid)).toHaveCount(0);
    });
}

// ---------------------------------------------------------------------------------------------
// Over the right characters (DC-48)

/** Differing pixels between two PNG screenshots of one element, compared in the page. */
function pixelsApart(page, a, b) {
    return page.evaluate(async ([a, b]) => {
        const load = (bytes) => new Promise((resolve, reject) => {
            const image = new Image();
            image.onload = () => resolve(image);
            image.onerror = reject;
            image.src = `data:image/png;base64,${bytes}`;
        });
        const [first, second] = await Promise.all([load(a), load(b)]);
        const canvas = document.createElement('canvas');
        canvas.width = first.width;
        canvas.height = first.height;
        const context = canvas.getContext('2d', { willReadFrequently: true });
        context.drawImage(first, 0, 0);
        const one = context.getImageData(0, 0, canvas.width, canvas.height).data;
        context.clearRect(0, 0, canvas.width, canvas.height);
        context.drawImage(second, 0, 0);
        const other = context.getImageData(0, 0, canvas.width, canvas.height).data;
        let differing = 0;
        let apart = 0;
        for (let i = 0; i < one.length; i += 4) {
            const d = Math.max(Math.abs(one[i] - other[i]), Math.abs(one[i + 1] - other[i + 1]), Math.abs(one[i + 2] - other[i + 2]));
            differing += d > 0 ? 1 : 0;
            apart += d > 96 ? 1 : 0;
        }
        return { differing, apart, sizes: [first.width, first.height, second.width, second.height] };
    }, [a.toString('base64'), b.toString('base64')]);
}

/**
 * A stylesheet over the page that draws a field one of two ways, by a mark the test sets on the
 * field: `own` — by its own text, the layer hidden, as it is while the layer is behind; `layer` —
 * by the layer, as the listener has it, with the colours taken off so the ink is the field's. A
 * word the spelling check marks is drawn by the field in its highlight's colour, which the
 * stylesheet takes away only while the layer shows, so `own` gives it back.
 */
async function overlayDrawingWays(page) {
    await alterPage(page, () => {
        const style = document.createElement('style');
        style.textContent = `
            .ex-reference-text:has(+ [data-drawn="own"]) { visibility: hidden !important; }
            .ex-reference-text + [data-drawn="own"] { -webkit-text-fill-color: currentColor !important; }
            .ex-reference-text + [data-drawn="own"]::spelling-error,
            .ex-reference-text + [data-drawn="own"]::grammar-error { color: inherit !important; }
            .ex-reference-text + input.ex-editor[data-drawn="own"] { background: var(--ex-editor-background, Canvas) !important; }
            .ex-reference-text:has(+ [data-drawn="layer"]) span { color: inherit !important; }`;
        document.head.append(style);
        return () => style.remove();
    });
}

/** The field drawn both ways. Where the layer's characters stand where the field's do, the two
 * are one picture. */
async function drawnBothWays(page, field) {
    const shoot = async (way) => {
        await field.evaluate((input, drawn) => input.setAttribute('data-drawn', drawn), way);
        await twoFrames(page);
        return field.screenshot();
    };
    const own = await shoot('own');
    const layer = await shoot('layer');
    await field.evaluate((input) => input.removeAttribute('data-drawn'));
    return { own, layer };
}

// Longer than either surface on /sheet, with a handful of References, as a Formula a user writes
// is. The layer draws each span as a text of its own, and the browser snaps each one's width to its
// layout unit, where the field's text is one run: measured on 2026-09-30, the layer's text runs
// about 1/128 px long per span, so at the far end of a Formula the error is 0.1 px with ten
// References, 0.6 px with forty, 1.3 px with eighty — never a character, but past twenty
// References no longer the same picture. This Formula is the same picture.
const FORMULA = '=IF(AND(B2>0,C2>0),ROUND(B2*C2*(1+D2),2),"Enter both the quantity and the price '
    + 'before the amount of this line is worked out, and check the discount in the next column")'
    + '&" as of "&TEXT(B7,"yyyy-mm-dd")&", due "&TEXT(B8,"yyyy-mm-dd")';

// The keys that take a text field's caret to either end of its line. On macOS, End and Home
// scroll the document instead (scrollToEndOfDocument:, as the platform binds them and Playwright
// sends them): there they scroll the grid to its last row, the Cell Editor's cell is no longer
// painted, and the editor goes with it. Command+→ and Command+← are the platform's line ends, and
// reach the field in Caret as End and Home do elsewhere (ADR-0010).
const LINE_END = process.platform === 'darwin'
    ? { End: 'Meta+ArrowRight', Home: 'Meta+ArrowLeft' }
    : { End: 'End', Home: 'Home' };

for (const chrome of ['builtin', 'mud']) {
    for (const surface of ['cell', 'bar']) {
        test(`DC-48: a Formula longer than the ${surface === 'cell' ? 'Cell Editor' : 'Formula Bar'} keeps its colours over the right characters at either end (${chrome} Chrome)`, async ({ page }) => {
            await underChrome(page, chrome);
            const grid = sheet(page);
            await pressCell(grid, 'F3');
            const field = surface === 'cell' ? editor(grid) : bar(grid);
            // Caret, where Home and End move the caret rather than the Focus (ADR-0010).
            if (surface === 'cell') {
                await page.keyboard.press('F2');
            } else {
                await clickBarEnd(grid);
            }
            await expect(field).toBeFocused();
            await page.keyboard.insertText(FORMULA);
            await expectColoured(field, FORMULA);
            await overlayDrawingWays(page);

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
                await page.keyboard.press(LINE_END[end]);
                // The field scrolled to show its caret — past the start at the end, back to it at the
                // start — and the layer's line with it (DC-48).
                await expect.poll(() => field.evaluate((input, at) => {
                    const line = input.previousElementSibling.firstElementChild;
                    const scrolled = at === 'End' ? input.scrollLeft > 0 : input.scrollLeft === 0;
                    return scrolled && line.scrollLeft === input.scrollLeft ? 'with the field' : `field ${input.scrollLeft}, layer ${line.scrollLeft}`;
                }, end)).toBe('with the field');
                await expectColoured(field, FORMULA);
                const { own, layer } = await drawnBothWays(page, field);
                const apart = await pixelsApart(page, own, layer);
                test.info().annotations.push({ type: `DC-48 ${end}`, description: JSON.stringify(apart) });
                expect(apart.apart, `pixels the layer's text puts somewhere the field's is not, at ${end}`).toBe(0);
            }

            await page.keyboard.press('Escape');
            await expect(editor(grid)).toHaveCount(0);
        });
    }
}
