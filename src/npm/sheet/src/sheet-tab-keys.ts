// Excel's keys for the Sheet Tabs, as ExSheet declares them to its grid (ADR-0331 §4), the counterpart of C#'s
// SheetTabKeys: Ctrl+PageUp shows the Sheet before the one shown, Ctrl+PageDown the one after it, and neither
// goes past the first or the last, as in Excel. Shift+F11 adds a Sheet before the one shown, as Excel's does
// (exsheet-workbook ticket 06). Declared, they are claimed from the browser, whose own meaning for them moves
// among its tabs.

/** Excel's keys for the Sheet Tabs, in the grid's canonical form. */
export const sheetTabKeys = Object.freeze({
    /** Shows the Sheet before the one shown. */
    previous: 'Control+PageUp',
    /** Shows the Sheet after the one shown. */
    next: 'Control+PageDown',
    /** Adds a Sheet before the one shown. */
    insert: 'Shift+F11',
} as const);

/** The keys ExSheet declares to its grid for the Sheet Tabs. */
export function sheetTabKeysDeclared(): readonly string[] {
    return Object.freeze([sheetTabKeys.previous, sheetTabKeys.next, sheetTabKeys.insert]);
}

/** How far the key moves among the Sheets: -1 or 1, or `undefined` for a key that is not one of these. */
export function sheetTabStepOf(key: string): -1 | 1 | undefined {
    if (key === sheetTabKeys.previous) return -1;
    if (key === sheetTabKeys.next) return 1;
    return undefined;
}
