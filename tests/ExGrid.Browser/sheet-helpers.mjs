import { expect } from './fixtures.mjs';

// What sheet.spec.mjs and declarations.spec.mjs share: finding an ExSheet's grid, a cell of it
// by its A1 address, and the surfaces ADR-0051 adds — the Cell Editor, the Formula Bar's text
// and the Name Box. An ExSheet is the ExGrid that has a Formula Bar; the positions grid beside
// it on /sheet declares nothing and has none.

/** The index-th ExSheet on the page: the grid that paints a Formula Bar. */
export function sheet(page, index = 0) {
    return page.locator('.ex-grid:has(> .ex-formula-bar)').nth(index);
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

/** Where the Focus is, read from the root's aria-activedescendant (ADR-0033). */
export async function expectFocusAt(grid, address) {
    const { row, column } = at(address);
    await expect(grid).toHaveAttribute('aria-activedescendant', new RegExp(`-r${row}c${column}$`));
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
