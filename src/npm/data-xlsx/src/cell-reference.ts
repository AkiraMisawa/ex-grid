// A cell's place written as `B7`, and read from it, within Excel's grid of 1,048,576 rows and 16,384 columns, as
// ExGrid.Data.Xlsx's XlsxCellReference writes and reads it.

/** The rows a worksheet has: 1,048,576. */
export const maxRows = 1_048_576;

/** The columns a worksheet has: 16,384 (A to XFD). */
export const maxColumns = 16_384;

/** The name of the column `column`, from zero: 0 is `A`, 26 is `AA`. */
export function columnName(column: number): string {
    if (!Number.isInteger(column) || column < 0) throw new RangeError(`The column ${String(column)} is not a column from zero.`);
    let letters = '';
    let n = column + 1;
    while (n > 0) {
        const rem = (n - 1) % 26;
        letters = String.fromCharCode(0x41 + rem) + letters;
        n = Math.floor((n - 1) / 26);
    }
    return letters;
}

/** The cell at `row` and `column`, both from zero, as `B7`. */
export function formatCell(row: number, column: number): string {
    if (!Number.isInteger(row) || row < 0) throw new RangeError(`The row ${String(row)} is not a row from zero.`);
    return columnName(column) + String(row + 1);
}

/** A cell's place, both from zero. */
export interface XlsxPlaceOfCell {
    /** The row, from zero (row 1 is 0). */
    readonly row: number;
    /** The column, from zero (column A is 0). */
    readonly column: number;
}

/** Reads `B7` into its row and column, from zero; `undefined` for text that is not a cell inside the grid. `$` marks are
 * allowed. */
export function parseCell(text: string): XlsxPlaceOfCell | undefined {
    let i = 0;
    if (text.charAt(i) === '$') i++;
    let col = 0;
    let letters = 0;
    for (; i < text.length; i++) {
        const c = text.charCodeAt(i);
        const upper = c >= 0x61 && c <= 0x7a ? c - 0x20 : c;
        if (upper < 0x41 || upper > 0x5a) break;
        col = col * 26 + (upper - 0x41 + 1);
        if (++letters > 3) return undefined;
    }
    if (letters === 0 || col > maxColumns) return undefined;
    if (text.charAt(i) === '$') i++;
    let r = 0;
    let digits = 0;
    for (; i < text.length; i++) {
        const c = text.charCodeAt(i);
        if (c < 0x30 || c > 0x39) break;
        r = r * 10 + (c - 0x30);
        if (++digits > 7) return undefined;
    }
    if (digits === 0 || i !== text.length || r < 1 || r > maxRows) return undefined;
    return { row: r - 1, column: col - 1 };
}
