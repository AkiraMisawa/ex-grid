// What a user types into the Name Box, read as a place on the Sheet (ADR-0051), the counterpart of C#'s
// NameBoxText: a cell (`D200`), a range (`A1:C3`), whole columns (`B:D`) or whole rows (`5:9`), in either case,
// with or without `$` markers, as Excel's Name Box takes them. Anything else is not an address, and nothing
// moves. A Sheet's name may come before the address, as a Reference qualifies one (`Sheet2!B5`,
// `'Q1 Sales'!A1:C3`), and the Name Box then goes to that Sheet (ADR-0331 §5).

import { CellAddress, CellRange, columnCount, rowCount } from '@ex-grid/sheet-engine';

/** A place the Name Box reads: a range, and the name of the Sheet it is on when one was typed before it. */
export interface NameBoxPlace {
    /** The Sheet's name as typed, a quoted one unquoted; `undefined` when none was typed, and the range is then
     * on the Sheet shown. */
    readonly sheet: string | undefined;
    /** The range. */
    readonly range: CellRange;
}

/**
 * Reads what was typed as a range, with the name of the Sheet it is on when one is typed before it; `undefined`
 * when it is not a place. A name in quotes doubles a quote inside it, as a Reference writes one.
 */
export function readNameBoxPlace(typed: string): NameBoxPlace | undefined {
    let text = typed.trim();
    let sheet: string | undefined;
    if (text.startsWith('\'')) {
        let name = '';
        let i = 1;
        for (;;) {
            if (i >= text.length) return undefined;
            if (text[i] === '\'') {
                if (text[i + 1] === '\'') {
                    name += '\'';
                    i += 2;
                    continue;
                }
                break;
            }
            name += text.charAt(i);
            i++;
        }
        if (name.length === 0 || text[i + 1] !== '!') return undefined;
        sheet = name;
        text = text.slice(i + 2);
    } else {
        const bang = text.lastIndexOf('!');
        if (bang === 0) return undefined;
        if (bang > 0) {
            sheet = text.slice(0, bang);
            text = text.slice(bang + 1);
        }
    }
    const range = readNameBoxRange(text);
    return range === undefined ? undefined : Object.freeze({ sheet, range });
}

/** Reads what was typed as a range of the Sheet, or `undefined` when it is not one. */
export function readNameBoxRange(typed: string): CellRange | undefined {
    const text = typed.trim().replaceAll('$', '');
    if (text.length === 0) return undefined;
    const range = CellRange.tryParse(text);
    if (range !== undefined) return range;
    const colon = text.indexOf(':');
    if (colon <= 0 || colon === text.length - 1) return undefined;
    const a = text.slice(0, colon);
    const b = text.slice(colon + 1);
    const firstColumn = CellAddress.tryParseColumn(a);
    const lastColumn = CellAddress.tryParseColumn(b);
    if (firstColumn !== undefined && lastColumn !== undefined) {
        return new CellRange(new CellAddress(0, firstColumn), new CellAddress(rowCount - 1, lastColumn));
    }
    const firstRow = rowOf(a);
    const lastRow = rowOf(b);
    if (firstRow !== undefined && lastRow !== undefined) {
        return new CellRange(new CellAddress(firstRow, 0), new CellAddress(lastRow, columnCount - 1));
    }
    return undefined;
}

function rowOf(text: string): number | undefined {
    if (!/^[1-9][0-9]*$/u.test(text)) return undefined;
    const number = Number(text);
    return number > rowCount ? undefined : number - 1;
}
