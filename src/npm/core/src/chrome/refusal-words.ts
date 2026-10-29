import type { CopyRefusalReason, PasteRefusalReason } from '../clipboard/clipboard-rules';
import { defined } from '../must';

/**
 * The built-in Chrome's words for a refused copy, as the C# core's built-in labels say them (ADR-0005,
 * ADR-0203): a copy that landed nothing and said nothing is the silence ADR-0005 refuses, so the grid
 * announces these through its live region as it raises the refusal. Past the cap the words point at export,
 * never at a smaller selection: a part of the rows pasted elsewhere is the quietly wrong total the cap
 * exists to prevent.
 */
export const copyRefusalWords: Readonly<Record<CopyRefusalReason, string>> = Object.freeze({
    emptySelection: 'Nothing was copied: no cell is selected. Select the cells to copy.',
    misalignedShape: 'Nothing was copied: the selected ranges do not line up into one block. Select ranges that share their rows or their columns.',
    tooLarge: 'Nothing was copied: the selection holds more cells than one copy carries. Export the data instead.',
    rowsUnavailable: 'Nothing was copied: the rows of the selection could not all be read.',
    clipboardUnavailable: 'Nothing was copied: the browser refused the clipboard, which still holds what it held before.',
    refusedByConsumer: 'Nothing was copied.',
});

/** Where the words for `targetNotEditable` name the column that refused: its header is written in its place, or
 * its name where the header is blank, as a Mark or an Action Column's is. */
export const columnPlaceholder = '{column}';

/**
 * The built-in Chrome's words for a refused paste, as the C# core's built-in labels say them (ADR-0035,
 * ADR-0203). A paste, a fill and Delete go through one gate, so a reason any of them can meet is worded for
 * all three; one only a paste or a fill meets says which.
 */
export const pasteRefusalWords: Readonly<Record<PasteRefusalReason, string>> = Object.freeze({
    emptySelection: 'Nothing was changed: no cell is selected. Select the cells first.',
    singleCellTarget: 'Nothing was pasted: the copied block would spill out of the one selected cell. Select a range of the copied block\'s shape.',
    shapeMismatch: 'Nothing was pasted: the selection is not a whole multiple of the copied block. Select a range of the copied block\'s shape, '
        + 'or one it repeats over exactly.',
    disjointTarget: 'Nothing was pasted: a block of several cells goes into one range, not several. Select one range.',
    targetNotEditable: `Nothing was changed: the selection covers ${columnPlaceholder}, which cannot be edited. Select cells only in columns that can be edited.`,
    tooLarge: 'Nothing was changed: that is more than the grid reads at once. Use a smaller block.',
    spillPastExtent: 'Nothing was pasted: the copied block would run past the last row or column. Select a cell further from the edge.',
    nothingToFillFrom: 'Nothing was filled: there is no row above the selection, or column to its left, to fill from. '
        + 'Select the cells to fill from together with the cells to fill.',
    multipleRanges: 'Nothing was filled: a fill works on one range. Select one range.',
    sourceUnavailable: 'Nothing was filled: the cells to fill from could not all be read.',
    orderMoved: 'Nothing was changed: the rows moved after the cells were selected. Select the cells again.',
    sourceChanged: 'Nothing was changed: the data was replaced after the cells were selected. Select the cells again.',
    unreadable: 'Nothing was pasted: the clipboard could not be read. Paste again.',
    cancelled: 'Nothing was pasted: the paste was cancelled.',
});

/** Where a paste may spill, one cell takes the block from its top-left, and a shape mismatch says so
 * (ADR-0050 item 3, ADR-0203). */
export const shapeMismatchMaySpillWords = 'Nothing was pasted: the selection is not a whole multiple of the copied block. '
    + 'Select a range of the copied block\'s shape, one it repeats over exactly, or a single cell.';

/**
 * The ids a refused copy's words are asked for by, through the grid's `commandLabel`, as the C# core's
 * RefusalLabelIds names them (ADR-0203): words answered there are said in place of the built-in English, the
 * empty string silences the reason, and `undefined` keeps the built-in words.
 */
export const copyRefusalLabelIds: Readonly<Record<CopyRefusalReason, string>> = Object.freeze({
    emptySelection: 'refused-copy-empty-selection',
    misalignedShape: 'refused-copy-misaligned-shape',
    tooLarge: 'refused-copy-too-large',
    rowsUnavailable: 'refused-copy-rows-unavailable',
    clipboardUnavailable: 'refused-copy-clipboard-unavailable',
    refusedByConsumer: 'refused-copy-by-consumer',
});

/**
 * The ids a refused paste, fill or Delete's words are asked for by, through the grid's `commandLabel`, as the C#
 * core's RefusalLabelIds names them (ADR-0203). Words for `targetNotEditable` may hold `columnPlaceholder`, where
 * the column that refused is written in.
 */
export const pasteRefusalLabelIds: Readonly<Record<PasteRefusalReason, string>> = Object.freeze({
    emptySelection: 'refused-paste-empty-selection',
    singleCellTarget: 'refused-paste-single-cell-target',
    shapeMismatch: 'refused-paste-shape-mismatch',
    disjointTarget: 'refused-paste-disjoint-target',
    targetNotEditable: 'refused-paste-target-not-editable',
    tooLarge: 'refused-paste-too-large',
    spillPastExtent: 'refused-paste-spill-past-extent',
    nothingToFillFrom: 'refused-paste-nothing-to-fill-from',
    multipleRanges: 'refused-paste-multiple-ranges',
    sourceUnavailable: 'refused-paste-source-unavailable',
    orderMoved: 'refused-paste-order-moved',
    sourceChanged: 'refused-paste-source-changed',
    unreadable: 'refused-paste-unreadable',
    cancelled: 'refused-paste-cancelled',
});

/** The id a shape mismatch's words are asked for by where a paste may spill (`pasteMaySpill`): one cell is a
 * target there, and the words say so (ADR-0050 item 3, ADR-0203). */
export const shapeMismatchMaySpillLabelId = 'refused-paste-shape-mismatch-may-spill';

/** The built-in English for every refusal id above, as the C# core's built-in labels say them: resolved through the
 * grid's `commandLabel` by the reason's id, as the menus' words are (ADR-0203). */
export const refusalWords: readonly (readonly [string, string])[] = Object.freeze([
    ...byId(copyRefusalLabelIds, copyRefusalWords),
    ...byId(pasteRefusalLabelIds, pasteRefusalWords),
    Object.freeze([shapeMismatchMaySpillLabelId, shapeMismatchMaySpillWords] as const),
]);

// A reason's words keyed by its id, from the two tables keyed by the reason.
function byId(ids: Readonly<Record<string, string>>, words: Readonly<Record<string, string>>): readonly (readonly [string, string])[] {
    const english = new Map(Object.entries(words));
    return Object.entries(ids).map(([reason, id]) => Object.freeze([id, defined(english.get(reason), `the words for ${reason}`)] as const));
}

/** The id of what an action says when the row it was pressed on is gone (ADR-0142, A11Y-16), as the C# core's
 * KeyRefusalLabelIds names it: resolved through the grid's `commandLabel`, as the menus' words are. */
export const rowCommandRefusalId = 'key-refusal-row-command-target-left';

/** The built-in English for {@link rowCommandRefusalId}, as the C# core's built-in labels say it. */
export const rowCommandRefusalWords: readonly (readonly [string, string])[] = Object.freeze([
    [rowCommandRefusalId, 'The row is no longer available. Repeat the command on the current data.'],
]);

/** The id of what a commit says when the user cancelled it while its verdict or its `onEdit` was still answering
 * (ADR-0380 section 3), resolved through the grid's `commandLabel` as {@link rowCommandRefusalId} is. A Replace
 * cancelled so names its cell with these words. */
export const commitCancelledId = 'edit-commit-cancelled';

/** The id of what a Replace says when its cell changed while its verdict was answering (ADR-0380 section 3): the
 * text it would write was made from the entry as it stood before. */
export const replaceCellChangedId = 'replace-cell-changed';

/** The id of what the grid says when an input held while a commit was pending could not be taken again at its turn
 * (ADR-0380 section 3): a press whose control is gone, a Tab past the page's last control, a key an IME was composing.
 * Said once for the inputs of one answer. */
export const heldInputLostId = 'pending-input-lost';

/** The id of what the grid says once as a paste, or a fill, waits for the Consumer's answer (ADR-0240 section 12,
 * ADR-0380 section 3): it is under way, and an Escape cancels it. The C# core's `GridLabelIds.PasteUnderWay`. */
export const pasteUnderWayId = 'paste-under-way';

/** The error step found no cell with an error anywhere the Consumer looked (ADR-0381 section 3). */
export const errorStepNoneId = 'error-step-none';

/** The error step's answer came back about rows or columns that moved while it was out: nothing moved (ADR-0381,
 * ADR-0011). */
export const errorStepMovedId = 'error-step-moved';

/** The built-in English for the error step's words (ADR-0381 section 3). */
export const errorStepWords: readonly (readonly [string, string])[] = Object.freeze([
    [errorStepNoneId, 'No error among the rows and columns shown.'],
    [errorStepMovedId, 'The rows moved while the next error was looked for, so the step was set aside. Press F8 again.'],
]);

/** The built-in English for a Pending Commit's words. */
export const pendingCommitWords: readonly (readonly [string, string])[] = Object.freeze([
    [commitCancelledId, 'Nothing was changed: the edit was cancelled while it was being checked.'],
    [replaceCellChangedId, 'Nothing was replaced: the cell changed while the replacement was being checked.'],
    [heldInputLostId, 'An input made while an edit was being checked could not be taken. Make it again.'],
    [pasteUnderWayId, 'Pasting. Press Escape to cancel.'],
]);
