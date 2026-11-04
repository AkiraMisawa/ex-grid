// A module of the page's own, which ExGrid's Blazor component over the TypeScript core imports in the browser and
// calls as it decides each key in the editor (ADR-0431). The site serves it from wwwroot, as any other static file.
// The grid knows positions; what a Reference is called, and where one may go, is the page's: here the A1 names of the
// Example's four columns, A to D.

const letters = ['A', 'B', 'C', 'D'];
const reference = /(\$?)([A-D])(\$?)([1-9][0-9]*)/g;

/** Whether a Reference can go at the caret: in a Formula, after =, an operator, ( or a comma. */
export function pointAt(text, caret) {
    return text.startsWith('=') && /[=+\-*/(,]$/.test(text.slice(0, caret));
}

/** The pointed range's A1 name: B2, or A1:C3. */
export function referenceText(range) {
    const name = (row, column) => {
        if (column >= letters.length) throw new RangeError(`This Example's References name columns A to D, not column ${column + 1}.`);
        return `${letters[column]}${row + 1}`;
    };
    const first = name(range.topRow, range.leftColumn);
    return range.rowCount === 1 && range.columnCount === 1
        ? first
        : `${first}:${name(range.topRow + range.rowCount - 1, range.leftColumn + range.columnCount - 1)}`;
}

/** F4: the Reference at the caret through B2, $B$2, B$2 and $B2. */
export function cycleReference(text, start, end) {
    for (const match of text.matchAll(reference)) {
        const from = match.index;
        const to = from + match[0].length;
        if (start < from || end > to) continue;
        const [, column$, column, row$, row] = match;
        const next = column$ && row$ ? `${column}$${row}` : row$ ? `$${column}${row}` : column$ ? `${column}${row}` : `$${column}$${row}`;
        return { text: text.slice(0, from) + next + text.slice(to), selectionStart: from + next.length, selectionEnd: from + next.length };
    }
    return null;
}

const cellOrRange = /(\$?)([A-D])(\$?)([1-9][0-9]*)(?::(\$?)([A-D])(\$?)([1-9][0-9]*))?/g;

/** The References in a Formula, each with the cells it names, which the grid outlines in their colours. */
export function referencesIn(text) {
    if (!text.startsWith('=')) return null;
    return [...text.matchAll(cellOrRange)].map(match => {
        const top = Number(match[4]) - 1;
        const left = letters.indexOf(match[2]);
        const bottom = match[8] === undefined ? top : Number(match[8]) - 1;
        const right = match[6] === undefined ? left : letters.indexOf(match[6]);
        return {
            start: match.index,
            length: match[0].length,
            range: {
                topRow: Math.min(top, bottom), leftColumn: Math.min(left, right),
                rowCount: Math.abs(bottom - top) + 1, columnCount: Math.abs(right - left) + 1,
            },
        };
    });
}

/**
 * A Reference dragged by its outline's edge or corner square, rewritten to name `range` as it was written: each end
 * keeps its `$`, and a single cell made a range writes both ends in its form.
 */
export function moveReference(text, range) {
    const match = new RegExp(`^${cellOrRange.source}$`).exec(text);
    if (match === null) throw new RangeError(`'${text}' is not a Reference this Example reads.`);
    // A single cell lends its own form to the end a resize gives it.
    const [, column1$, , row1$, , column2$ = column1$, , row2$ = row1$] = match;
    const name = (row, column, column$, row$) => {
        if (column >= letters.length) throw new RangeError(`This Example's References name columns A to D, not column ${column + 1}.`);
        return `${column$}${letters[column]}${row$}${row + 1}`;
    };
    const first = name(range.topRow, range.leftColumn, column1$, row1$);
    return range.rowCount === 1 && range.columnCount === 1
        ? first
        : `${first}:${name(range.topRow + range.rowCount - 1, range.leftColumn + range.columnCount - 1, column2$, row2$)}`;
}
