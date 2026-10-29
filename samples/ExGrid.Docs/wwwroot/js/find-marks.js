// A module of the page's own, which ExGrid's Blazor component over the TypeScript core imports in the browser and
// asks of each cell on screen while the Find panel holds a pattern (ADR-0440). The site serves it from wwwroot, as any
// other static file. It is the rule the Example's search finds by, GridFind.Matches over the columns that hold text,
// the only ones that search reads: a cell it marks is a cell the next step reaches.

const searched = new Set(['Book', 'Trader', 'Instrument']);

/** Whether a cell matches: called with the row's values by column name, the column's name and the pattern. */
export function match(values, column, pattern) {
    if (!searched.has(column) || pattern.text === '') return false;
    const text = values[column];
    if (typeof text !== 'string') throw new TypeError(`Column '${column}' holds text, and its cell held ${typeof text}.`);
    // Ordinal, ignoring case as .NET's OrdinalIgnoreCase does: by each character's simple upper case.
    const [cell, sought] = pattern.matchCase ? [text, pattern.text] : [upper(text), upper(pattern.text)];
    return pattern.wholeCell ? cell === sought : cell.includes(sought);
}

// One UTF-16 unit for one: a character whose upper case is longer ('ß' is 'SS') stays itself, as .NET keeps it, so
// 'ß' never matches 'ss'.
function upper(text) {
    let result = '';
    for (const unit of text.split('')) {
        const raised = unit.toUpperCase();
        result += raised.length === 1 ? raised : unit;
    }
    return result;
}
