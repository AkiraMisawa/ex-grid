import type { CopyRefusalReason } from '../clipboard/clipboard-rules';

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
