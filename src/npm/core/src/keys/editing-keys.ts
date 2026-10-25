/**
 * The keys the core claims while a cell is being edited, one set for each editing mode (ADR-0010,
 * ADR-0051, ADR-0058). The Script Core's listener looks a key up in the set of the mode the edit is in.
 */

/** Claimed in every mode: commit, cancel, Tab's moves and F2's switch, and Ctrl+F and Ctrl+H, taken and
 * answered with nothing, since Find and Replace are disabled while a cell is being edited (ADR-0055,
 * ADR-0332 §2) and the browser's own would search only the painted rows. */
const editing: readonly string[] = Object.freeze([
    'Escape', 'Enter', 'Shift+Enter', 'Control+Enter', 'Tab', 'Shift+Tab', 'F2',
    'Control+f', 'Control+F', 'Control+h', 'Control+H',
]);

/** Pointing (ADR-0051): the arrows, Home and End, the Shift+arrows and Shift+Home, and the Primary
 * Modifier's arrows with Shift or without, which move or extend the outline rather than the caret. */
const point: readonly string[] = Object.freeze([
    ...editing,
    'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Home', 'End',
    'Shift+ArrowUp', 'Shift+ArrowDown', 'Shift+ArrowLeft', 'Shift+ArrowRight', 'Shift+Home',
    'Control+ArrowUp', 'Control+ArrowDown', 'Control+ArrowLeft', 'Control+ArrowRight',
    'Control+Shift+ArrowUp', 'Control+Shift+ArrowDown', 'Control+Shift+ArrowLeft', 'Control+Shift+ArrowRight',
]);

/** While a completion list is open, only its ↑ and ↓ beside the editing keys (ADR-0051, ADR-0058). */
const completion: readonly string[] = Object.freeze([...editing, 'ArrowUp', 'ArrowDown']);

/** A list open over Point (ADR-0058, Q51): ←, →, Home, End and the four Shift+arrows beside the list's
 * keys, to point as they do without the list. */
const completionOverPoint: readonly string[] = Object.freeze([
    ...completion,
    'ArrowLeft', 'ArrowRight', 'Home', 'End',
    'Shift+ArrowUp', 'Shift+ArrowDown', 'Shift+ArrowLeft', 'Shift+ArrowRight',
]);

/** An editing mode, as far as its claimed keys go. Caret's mode claims `editing`'s. */
export type EditingKeyMode = 'editing' | 'overwrite' | 'point' | 'pointed' | 'completion' | 'completionOverPoint';

/**
 * The keys claimed in each editing mode. Overwrite, Excel's Enter mode (ADR-0010, ED-2), and pointing from
 * outside (ADR-0058) claim Point's: they commit and move, or point, rather than move the caret.
 */
export const editingKeysByMode: Readonly<Record<EditingKeyMode, readonly string[]>> = Object.freeze({
    editing,
    overwrite: point,
    point,
    pointed: point,
    completion,
    completionOverPoint,
});
