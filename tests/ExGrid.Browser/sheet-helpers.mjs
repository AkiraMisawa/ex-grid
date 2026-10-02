import { expect, circuitQuiet } from './fixtures.mjs';
import { expectActiveDescendant } from './keyboard.mjs';

// What the Sheet's specs share — sheet, declarations, sheets, edit-stands and pointing-scope:
// opening /sheet, finding an ExSheet's grid, a cell of it by its A1 address, and the surfaces
// ADR-0051 adds — the Cell Editor, the Formula Bar's text and the Name Box. An ExSheet is the ExGrid
// that has a Formula Bar; the positions grid beside it on /sheet has none.

/** The index-th ExSheet on the page: the grid that paints a Formula Bar. */
export function sheet(page, index = 0) {
    return page.locator('.ex-grid:has(> .ex-formula-bar)').nth(index);
}

/**
 * The positions grid beside the Sheet on /sheet: an ExGrid that declares nothing of a Sheet's (DC-25),
 * registered in the Sheet's Pointing Scope (ADR-0058).
 */
export function positions(page) {
    return page.locator('#sheet-positions .ex-grid');
}

/**
 * Opens /sheet, under ExGrid.MudBlazor's Chrome when `chrome` is 'mud', and waits for the Sheet
 * and its Linked Table. `page.goto` (fixtures.mjs) returns once the page is interactive: the
 * file's first navigation boots the app at the index, and every later one is an in-app
 * navigation (ADR-0056). The page pushes the Linked Table's first snapshot 1.5 s after the Sheet
 * opens, and every change to the Sheet clears ExSheet's notice, a Consumer's push included
 * (ExSheet.ChangedAsync), so a refusal read before the push lands can be wiped by it: B12 shows
 * its value once the push has landed.
 */
export async function openSheet(page, chrome = 'builtin') {
    await page.goto(chrome === 'builtin' ? '/sheet' : `/sheet?chrome=${chrome}`);
    await expect(cell(sheet(page), 'A1')).toHaveText('Item');
    if (chrome !== 'builtin') {
        await expect(page.locator('.mud-ex-formula-bar-text, .mud-ex-name-box').first()).toBeAttached();
    }
    await expect(cell(sheet(page), 'B12')).toHaveText('318.25', { timeout: 10_000 });
}

/** 'B7' → { row: 6, column: 1 }, zero-based, as the grid's cell ids count. */
export function at(address) {
    const match = /^([A-Z]+)(\d+)$/.exec(address);
    if (!match) {
        throw new Error(`not an A1 address: ${address}`);
    }
    let column = 0;
    for (const letter of match[1]) {
        column = column * 26 + (letter.charCodeAt(0) - 64);
    }
    return { row: Number(match[2]) - 1, column: column - 1 };
}

/** The painted cell at an A1 address. The id ends in -r{row}c{column} (CellIds). */
export function cell(grid, address) {
    const { row, column } = at(address);
    return grid.locator(`[id$='-r${row}c${column}']`);
}

/**
 * Presses a cell. Cells are pointer-events: none by design — the Viewport is the delegated
 * target (ADR-0004) — so the actionability check is bypassed and the browser hit-tests the
 * press through to the Viewport, as a user's does.
 */
export async function clickCell(grid, address, options = {}) {
    // Painted first: force skips the actionability checks, and a cell caught between two
    // renders has no box to press.
    await expect(cell(grid, address)).toBeVisible();
    await cell(grid, address).click({ force: true, ...options });
}

/**
 * Presses a cell and waits until the grid has answered: the Focus is there, and the Name Box
 * and the Formula Bar — painted in the same render — show that cell. For a test that goes on to
 * read what the bar shows or to press into it. On the Server host the answer is a round trip
 * away: read before it lands, the bar still shows the cell the Focus left, and a test that takes
 * that text as the start of what it types expects text the user never saw (found on Windows,
 * 2026-09-27: DC-19/DC-34 and SH-18/DC-22 expected `=B2*C2=` and `*`, where the grid rightly
 * held `=` and `=B3+1*`).
 */
export async function pressCell(grid, address) {
    await clickCell(grid, address);
    await expectFocusAt(grid, address);
}

// Each field is the core's box: the built-in Chrome's input wears the class itself, a
// substituted Chrome's control sits inside it (ADR-0010/0051), so both are looked for.

/** The Cell Editor's field: the one .ex-editor over the rows (the Formula Bar's is the other). */
export function editor(grid) {
    return grid.locator('.ex-viewport input.ex-editor, .ex-viewport .ex-editor input');
}

/** The Formula Bar's text field — the Cell Editor's second surface (ADR-0051). */
export function bar(grid) {
    return grid.locator('input.ex-formula-bar-text, .ex-formula-bar-text input');
}

/**
 * Presses the Formula Bar's text past its end, so the caret lands after the last character —
 * where a user who clicks into the bar to add to a Formula clicks. A press keeps the caret where
 * it lands (ADR-0051, third round), and no key is needed to put it there: End would be the
 * grid's own key in Overwrite (ADR-0012).
 */
export async function clickBarEnd(grid) {
    const field = bar(grid);
    const box = await field.boundingBox();
    await field.click({ position: { x: box.width - 4, y: box.height / 2 } });
}

/** The Name Box. */
export function nameBox(grid) {
    return grid.locator('input.ex-name-box, .ex-name-box input');
}

/**
 * Where the Focus is, read from aria-activedescendant on the element that carries it: a Sheet's
 * Keyboard Field, since a Sheet edits (ADR-0033, ADR-0080).
 */
export async function expectFocusAt(grid, address) {
    const { row, column } = at(address);
    await expectActiveDescendant(grid, new RegExp(`-r${row}c${column}$`));
    await expect(nameBox(grid)).toHaveValue(address);
}

/** The completion list's candidates, as either Chrome paints them (ADR-0051). */
export function candidates(grid) {
    return grid.locator('.ex-completion [role=option]');
}

/** Moves the Focus with the Name Box, as a user does: press it, type, Enter (DC-11). */
export async function goTo(grid, address) {
    await typeIntoNameBox(grid, address);
    await nameBox(grid).press('Enter');
    await expectFocusAt(grid, address);
}

/** Presses the Name Box, empties it and types into it, steadily (see typeSteadily). */
export async function typeIntoNameBox(grid, text) {
    const page = grid.page();
    // The Name Box names the Focus as the host last said. A render renaming it that lands after
    // Ctrl+A writes over the selection, and Backspace then took only the last character: a press
    // on A1 straight before left "A" (CI, Server host, chrome, SH-2, 2026-10-02). So the box is
    // pressed once the host has said all it will (ADR-0056, note of 2026-10-02).
    await circuitQuiet();
    await nameBox(grid).click();
    await nameBox(grid).press('ControlOrMeta+A');
    await nameBox(grid).press('Backspace');
    await expect(nameBox(grid)).toHaveValue('');
    await typeSteadily(page, nameBox(grid), text);
}

/**
 * Types into the field that holds DOM focus one character at a time, each one waited for in the
 * field before the next. For a test whose subject is not typing speed: on the Server host a
 * render answering one input writes its text back over characters typed since (the SRV-5 test
 * in declarations.spec.mjs pins that defect by name), and a test about the Name Box or the
 * Formula Bar should not fail for it at random.
 */
export async function typeSteadily(page, field, text) {
    const start = await field.inputValue();
    const caretAt = await field.evaluate((input) => input.selectionStart ?? input.value.length);
    for (let i = 1; i <= text.length; i++) {
        await page.keyboard.type(text[i - 1]);
        await expect(field).toHaveValue(start.slice(0, caretAt) + text.slice(0, i) + start.slice(caretAt));
    }
}

/**
 * The /sheet toolbar's commands that change the Sheet: the ones the page greys out while an edit
 * is open, from ExSheet's notification (ADR-0048, SH-29). Revalue, a Linked Table push, is not
 * among them.
 */
export function sheetCommands(page) {
    return ['#sheet-undo', '#sheet-redo', '#sheet-money', '#sheet-format-cells', '#sheet-insert-row'].map((id) => page.locator(id));
}

/** Every command that changes the Sheet is greyed out: an edit is open. */
export async function expectCommandsGreyedOut(page) {
    for (const button of sheetCommands(page)) {
        await expect(button).toBeDisabled();
    }
}

/** Every command that changes the Sheet is offered again: no edit is open. */
export async function expectCommandsOffered(page) {
    for (const button of sheetCommands(page)) {
        await expect(button).toBeEnabled();
    }
}

/**
 * Types an Entry into a cell and commits it with Enter. The cell is pressed first so the grid
 * holds the keyboard.
 */
export async function enter(page, grid, address, typed) {
    await clickCell(grid, address);
    await expectFocusAt(grid, address);
    await page.keyboard.type(typed);
    await expect(editor(grid)).toHaveValue(typed);
    await page.keyboard.press('Enter');
    await expect(editor(grid)).toHaveCount(0);
}

/**
 * The painted rectangles of a layer of the selection overlay, as boxes in cell units relative
 * to a cell's box: what the Selection, the pointing outline or the fill target covers.
 */
export async function boxOf(locator) {
    const box = await locator.boundingBox();
    if (!box) {
        throw new Error('the element is not painted');
    }
    return box;
}

/** The union of the painted boxes of a set of cells — what a range over them should cover. */
export async function spanOf(grid, from, to) {
    const a = await boxOf(cell(grid, from));
    const b = await boxOf(cell(grid, to));
    return { x: a.x, y: a.y, width: b.x + b.width - a.x, height: b.y + b.height - a.y };
}

/** Asserts one painted rectangle covers exactly the cells from..to, to within a pixel. */
export async function expectCovers(locator, grid, from, to) {
    const want = await spanOf(grid, from, to);
    await expect.poll(async () => {
        const box = await locator.boundingBox();
        if (!box) {
            return 'not painted';
        }
        const near = (p, q) => Math.abs(p - q) <= 1.5;
        return near(box.x, want.x) && near(box.y, want.y) && near(box.width, want.width) && near(box.height, want.height)
            ? 'covers'
            : JSON.stringify({ box, want });
    }).toBe('covers');
}

/**
 * Asserts the Selection is the one cell at an A1 address: no range is tinted, and the Focus outline
 * lies over that cell alone. One cell selected is the Focus alone (ADR-0008, 2026-09-29).
 */
export async function expectSelectionIsCell(grid, address) {
    await expect(grid.locator('.ex-range')).toHaveCount(0);
    await expectCovers(grid.locator('.ex-focus'), grid, address, address);
}

/**
 * Where an editor field's caret stands across its visible width, and how far the field and the
 * line of its coloured layer (ADR-0057) are scrolled. `x` is the caret's distance from the left of
 * the field's content box, in CSS px, and `width` is that box's width, so the caret can be seen
 * while 0 ≤ x ≤ width. Measured here, by a copy of the text before the caret set in the field's own
 * font: the grid itself measures nothing (ADR-0021). The copy is gone before the call returns.
 */
export function caretInField(field) {
    return field.evaluate((input) => {
        const style = getComputedStyle(input);
        const copy = document.createElement('span');
        for (const property of ['fontFamily', 'fontSize', 'fontWeight', 'fontStyle', 'fontStretch', 'fontVariant',
            'fontFeatureSettings', 'fontKerning', 'letterSpacing', 'wordSpacing', 'textTransform']) {
            copy.style[property] = style[property];
        }
        copy.style.whiteSpace = 'pre';
        copy.style.position = 'absolute';
        copy.style.visibility = 'hidden';
        copy.textContent = input.value.slice(0, input.selectionEnd);
        document.body.append(copy);
        const before = copy.getBoundingClientRect().width;
        copy.remove();
        const layer = input.previousElementSibling;
        return {
            x: before - input.scrollLeft,
            width: input.clientWidth - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight),
            scrolled: input.scrollLeft,
            layer: layer !== null && layer.classList.contains('ex-reference-text') ? layer.firstElementChild.scrollLeft : null,
        };
    });
}

/**
 * Asserts an editor field shows its caret, within a pixel either side, and that the line of its
 * coloured layer is scrolled as the field is (DC-48). `scrolled` says whether the field must be
 * scrolled past its start to show it — text wider than the field, with the caret beyond its first
 * width — so that a field that never moved cannot pass by showing its start.
 */
export async function expectCaretShown(field, { scrolled }, what) {
    await expect.poll(async () => {
        const at = await caretInField(field);
        const shown = at.x >= -1 && at.x <= at.width + 1 && (at.scrolled > 0) === scrolled && at.layer === at.scrolled;
        return shown ? 'shown' : JSON.stringify(at);
    }, { message: what }).toBe('shown');
}

/** Reads what the clipboard holds, both flavours; an overlapping write reads as nothing yet. */
export function readClipboard(page) {
    return page.evaluate(async () => {
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
}

/**
 * What the highlights over the layer beneath a field paint (ADR-0057, note of 2026-10-01), in the
 * order of the text. The layer's text is one run, and the editor listener colours its References with
 * the CSS Custom Highlight API, under names of the grid's own; this reads the ranges it registered
 * over that run, so a stretch is here only if the listener coloured it. Each Reference: its text,
 * `pointed: false`, its `place` in the palette, the `colour` of that place, the `ink` it is painted
 * in (its pointed shade on the grey) and the `ground` under it. The stretch Point wrote, on the grey
 * ground: `pointed: true`, its text and its `ground`, and the `colour` and `ink` of a Reference that
 * stands exactly there. `name` is the highlight's name past the grid's `prefix`. Nothing while the
 * layer is hidden.
 */
export function stretchesOf(field) {
    return field.evaluate((input) => {
        const line = input.previousElementSibling?.firstElementChild;
        const run = line?.firstChild;
        if (!(run instanceof Text)) {
            return [];
        }
        const ranges = [];
        CSS.highlights.forEach((highlight, name) => {
            const named = /^(ex\d+-reference-)(.+)$/.exec(name);
            if (named === null) {
                return;
            }
            for (const range of highlight) {
                if (range.startContainer === run) {
                    ranges.push({ start: range.startOffset, end: range.endOffset, prefix: named[1], name: named[2] });
                }
            }
        });
        const paint = (name) => getComputedStyle(line, `::highlight(${name})`);
        const grounds = ranges.filter((range) => range.name === 'pointed');
        const groundUnder = (range) => grounds.find((ground) => ground.start <= range.start && range.end <= ground.end);
        const references = ranges.filter((range) => range.name !== 'pointed').map((range) => {
            const place = Number(/^\d+/.exec(range.name)[0]);
            const ground = groundUnder(range);
            return {
                start: range.start,
                end: range.end,
                text: run.data.slice(range.start, range.end),
                prefix: range.prefix,
                name: range.name,
                pointed: false,
                place,
                colour: paint(`${range.prefix}${place}`).color,
                ink: paint(`${range.prefix}${range.name}`).color,
                ground: ground === undefined ? 'rgba(0, 0, 0, 0)' : paint(`${ground.prefix}pointed`).backgroundColor,
            };
        });
        const pointed = grounds.map((range) => {
            const exact = references.find((reference) => reference.start === range.start && reference.end === range.end);
            return {
                start: range.start,
                end: range.end,
                text: run.data.slice(range.start, range.end),
                prefix: range.prefix,
                name: range.name,
                pointed: true,
                place: exact?.place ?? null,
                colour: exact?.colour ?? null,
                ink: exact?.ink ?? null,
                ground: paint(`${range.prefix}pointed`).backgroundColor,
            };
        });
        return [...pointed, ...references].sort((a, b) => a.start - b.start || Number(b.pointed) - Number(a.pointed));
    });
}
