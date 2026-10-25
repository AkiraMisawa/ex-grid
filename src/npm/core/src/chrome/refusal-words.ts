import type { CopyRefusalReason, PasteRefusalReason } from '../clipboard/clipboard-rules';

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

/** Where the words for `targetNotEditable` name the column that refused: its header is written in its place. */
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
