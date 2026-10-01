import { expect } from './fixtures.mjs';

// Where a grid's keyboard is (ADR-0080). On a grid with an editable column, the keyboard with no
// edit open is held by the grid's Keyboard Field — an unseen text field of its own inside the root,
// so that an IME can start on a selected cell — and not by the root: the field is the grid's one tab
// stop, with the root at -1, and carries aria-activedescendant, and focus that lands on the root
// itself, by a press or by script, is passed on to the field at once. A display-only grid has no
// field, and keeps all of that on its root, as before.
//
// So an assertion that a grid's root holds DOM focus means "the keyboard is this grid's", and reads
// "the root or its own Keyboard Field" (expectKeyboardOn); aria-activedescendant is read from the
// element that carries it (keyboardCarrier); and a grid's tab stop is taken where it stands
// (expectTabStopTaken). A grid's own field is the first in its markup: it stands at the start of the
// Viewport, ahead of every cell and so of any grid nested in one, whose field is that grid's
// (ADR-0018). Every read is made in the page, so it answers for the grid as it is at that moment.

/** This grid's own Keyboard Field (ADR-0080), on a grid that edits: a display-only grid has none. */
export function keyField(grid) {
    return grid.locator('input.ex-key-field').first();
}

/**
 * Whether the keyboard is this grid's with no edit open — DOM focus on its root, or on its own
 * Keyboard Field — at this moment: true, or where the keyboard is instead, for a failure to name.
 */
export function keyboardIsOn(grid) {
    return grid.evaluate((root) => {
        const active = document.activeElement;
        if (active === root || (active instanceof HTMLInputElement && active.classList.contains('ex-key-field')
            && active.closest('.ex-grid') === root)) {
            return true;
        }
        if (active === null || active === document.body) {
            return 'the page body';
        }
        const name = active.tagName.toLowerCase() + (active.id ? `#${active.id}` : '')
            + [...active.classList].map((c) => `.${c}`).join('');
        return root.contains(active) ? `${name}, inside this grid` : name;
    });
}

/**
 * Asserts the keyboard is this grid's with no edit open: DOM focus is on its root, or on its own
 * Keyboard Field, never a nested grid's (ADR-0080).
 */
export async function expectKeyboardOn(grid, message = 'the keyboard is this grid\'s: its root or its own Keyboard Field holds DOM focus (ADR-0080)') {
    await expect.poll(() => keyboardIsOn(grid), { message }).toBe(true);
}

/**
 * The element that carries the grid's aria-activedescendant (ADR-0033, ADR-0080): its own Keyboard
 * Field on a grid that edits, its root on a display-only grid. Chosen once the grid is no longer
 * Prerendered, since until then it has no field (A11Y-20).
 */
export async function keyboardCarrier(grid) {
    await expect(grid).not.toHaveAttribute('aria-busy', /.*/);
    const ownField = await grid.evaluate((root) => root.querySelector('input.ex-key-field')?.closest('.ex-grid') === root);
    return ownField ? keyField(grid) : grid;
}

/**
 * The grid's aria-activedescendant at this moment, read from the element that carries it
 * (keyboardCarrier): for `expect.poll`, and for a value kept to compare with a later one.
 */
export function activeDescendant(grid) {
    return grid.evaluate((root) => {
        const field = root.querySelector('input.ex-key-field');
        return (field !== null && field.closest('.ex-grid') === root ? field : root).getAttribute('aria-activedescendant');
    });
}

/** Asserts the grid's aria-activedescendant, on the element that carries it (keyboardCarrier). */
export async function expectActiveDescendant(grid, expected) {
    await expect(await keyboardCarrier(grid)).toHaveAttribute('aria-activedescendant', expected);
}

/**
 * Waits until a grid has taken its one tab stop, as it does once it is interactive and no longer
 * Prerendered (A11Y-20): `tabindex="0"` on its own Keyboard Field, with the root at -1, on a grid
 * that edits, and on its root on a display-only grid (A11Y-4, ADR-0080).
 */
export async function expectTabStopTaken(grid) {
    await expect.poll(() => grid.evaluate((root) => {
        const field = root.querySelector('input.ex-key-field');
        return field !== null && field.closest('.ex-grid') === root
            ? `field ${field.getAttribute('tabindex')}, root ${root.getAttribute('tabindex')}`
            : `root ${root.getAttribute('tabindex')}`;
    }), { message: 'the grid has taken its one tab stop: its Keyboard Field\'s where it has one, its root\'s otherwise (A11Y-4, A11Y-20, ADR-0080)' })
        .toMatch(/^(field 0, root -1|root 0)$/);
}
