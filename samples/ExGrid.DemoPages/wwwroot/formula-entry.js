// The page's own module, which ExGrid.Script.ExGrid on /script-formula-entry imports in the browser and calls as it
// decides each key in the editor (ADR-0431). It ships in this Razor class library, at _content/ExGrid.DemoPages/, as a
// Consumer's library would. The grid knows positions; what a Reference is called, and where one may go, is the page's:
// here the A1 names of the grid's four columns, A to D.

const letters = ['A', 'B', 'C', 'D'];
const reference = /(\$?)([A-D])(\$?)([1-9][0-9]*)/g;

/** Whether a Reference can go at the caret: in a Formula, after =, an operator, ( or a comma. */
export function pointAt(text, caret) {
    return text.startsWith('=') && /[=+\-*/(,]$/.test(text.slice(0, caret));
}

/** The pointed range's A1 name: B2, or A1:C3. */
export function referenceText(range) {
    const name = (row, column) => {
        if (column >= letters.length) throw new RangeError(`This page's References name columns A to D, not column ${column + 1}.`);
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

/** The References in a Formula, each with the cells it names, which the grid outlines in their colours. */
export function referencesIn(text) {
    if (!text.startsWith('=')) return null;
    return [...text.matchAll(reference)].map(match => ({
        start: match.index,
        length: match[0].length,
        range: { topRow: Number(match[4]) - 1, leftColumn: letters.indexOf(match[2]), rowCount: 1, columnCount: 1 },
    }));
}
