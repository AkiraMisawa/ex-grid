import { elementAt } from '../must';
import { invariantMarker, markAttribute } from './clipboard-data';

// Turns what the `paste` event handed over into a rectangular block of cell values, or `undefined` when the
// clipboard holds nothing tabular: the paste rules are never consulted then (ADR-0014).
//
// The HTML flavour is preferred when it holds a table: Excel puts one on the clipboard when copying, and
// its `x:num` attribute carries the raw value at full precision, so reading it preserves what the TSV's
// display format has already rounded (ADR-0005). Anything else falls back to the plain text, read as
// Excel's TSV: tab-separated cells, lines as rows, quotes around a cell that holds a tab or a line break.

/**
 * Where one pasted field came from, which says how it is read (ADR-0050, item 10).
 *
 * - `shownText`: text as it was shown where it was copied, the plain-text flavour or an HTML cell's text
 *   content. Its numbers are written in the source's culture (`1.234,5` under `de-DE`), so a Consumer reads
 *   it as typed under its own culture.
 * - `invariant`: Excel's `x:num` attribute, the raw value at full precision in invariant notation, or a
 *   field of ExGrid's own unformatted HTML, which is locale-free throughout (ADR-0005). A Consumer reads it
 *   under the invariant culture, never its own: otherwise `1234.5` from Excel would be misread under
 *   `de-DE`.
 */
export type PasteFieldOrigin = 'shownText' | 'invariant';

/** A parsed clipboard block (ADR-0005, ADR-0050): the values, rectangular by construction, and beside
 * each one where it came from. */
export interface ClipboardBlock {
    /**
     * Whether the block was read from a table in the `text/html` flavour (a copy from the grid, from
     * ExSheet or from Excel) rather than from plain text alone. HTML that holds no table counts as plain
     * text, since the block was read from the text flavour. It decides where a single value goes over a
     * range (ADR-0014, amended 2026-09-29): from a table it fills the range; as plain text it goes into one
     * cell alone.
     */
    readonly isFromTable: boolean;
    /** The fields, row by row; every row is as wide as the widest. */
    readonly values: readonly (readonly string[])[];
    /** Where each field of `values` came from, at the same position. */
    readonly origins: readonly (readonly PasteFieldOrigin[])[];
    /** Each field's {@link markAttribute}, at the same position, or `undefined` when no field carries one. */
    readonly marks: readonly (readonly (string | undefined)[])[] | undefined;
}

interface Field {
    readonly value: string;
    readonly origin: PasteFieldOrigin;
    readonly mark: string | undefined;
}

/** The parsed block's values, rectangular by construction: a short line is padded with empty cells, as
 * Excel pads. `undefined` when neither flavour holds anything. */
export function parseClipboard(html: string | undefined, text: string | undefined): readonly (readonly string[])[] | undefined {
    return parseClipboardBlock(html, text)?.values;
}

/**
 * The parsed block with where each field came from (ADR-0050, item 10): a field is `invariant` when
 * Excel's `x:num="…"` supplied it, or when the whole table is ExGrid's own unformatted HTML (marked by
 * {@link invariantMarker}); every other field (the text flavour, a cell's text content, Excel's bare
 * `x:num`) is `shownText`, written in the source's culture. `undefined` when neither flavour holds
 * anything.
 */
export function parseClipboardBlock(html: string | undefined, text: string | undefined): ClipboardBlock | undefined {
    // What the browser's clipboard hands over is checked here, where it arrives untyped.
    for (const [name, flavour] of [['html', html], ['text', text]] as const) {
        if (flavour !== undefined && typeof flavour !== 'string') {
            throw new TypeError(`A clipboard flavour is text or absent. (${name}: ${typeof flavour})`);
        }
    }
    if (html !== undefined && html !== '') {
        const table = parseHtmlTable(html);
        if (table !== undefined && table.length > 0) return rectangular(table, true);
    }
    if (text !== undefined && text !== '') {
        const block = parseTsv(text);
        if (block.length > 0) {
            return rectangular(block.map(row => row.map(value => ({ value, origin: 'shownText' as const, mark: undefined }))), false);
        }
    }
    return undefined;
}

// What ends an unquoted cell's run of text.
const cellEnds = /[\t\r\n]/g;

/**
 * Excel-shaped TSV: cells split on tabs, rows on line breaks, and a cell that was quoted (because it holds
 * a tab, a break or a quote) read back as one cell with inner doubled quotes undone. One trailing line
 * break, which Excel always appends, does not become an extra empty row.
 */
function parseTsv(text: string): string[][] {
    const rows: string[][] = [];
    let row: string[] = [];
    let cell = '';
    let quoted = false;
    let cellStart = true;

    for (let i = 0; i < text.length; i++) {
        const c = text[i];
        if (quoted) {
            if (c === '"') {
                if (text[i + 1] === '"') {
                    cell += '"';
                    i++;
                } else {
                    quoted = false;
                }
            } else {
                // Up to the next quote in one append: a paste of megabytes is read in runs, not a character
                // at a time (PST-5).
                const quote = text.indexOf('"', i);
                const end = quote < 0 ? text.length : quote;
                cell += text.slice(i, end);
                i = end - 1;
            }
            continue;
        }

        if (c === '"' && cellStart) {
            quoted = true;
            cellStart = false;
        } else if (c === '\t') {
            row.push(cell);
            cell = '';
            cellStart = true;
        } else if (c === '\n' || c === '\r') {
            if (c === '\r' && text[i + 1] === '\n') i++;
            row.push(cell);
            cell = '';
            cellStart = true;
            rows.push(row);
            row = [];
        } else {
            // Past a cell's first character a quote is only text, so the run goes on to the next tab or
            // break, appended at once (PST-5).
            cellEnds.lastIndex = i + 1;
            const next = cellEnds.exec(text);
            const end = next === null ? text.length : next.index;
            cell += text.slice(i, end);
            i = end - 1;
            cellStart = false;
        }
    }

    // Whatever follows the last break is a final row, unless it is nothing at all, which is the trailing
    // break Excel appends, not an empty row the user copied.
    if (cell.length > 0 || row.length > 0) {
        row.push(cell);
        rows.push(row);
    }
    return rows;
}

/** Where `needle`, ASCII, first stands in `text` at or after `from`, in any case, or -1. Lower-casing the
 * whole text would move the indexes wherever a character's lower case is longer than itself. */
function indexOfIgnoringCase(text: string, needle: string, from: number): number {
    const pattern = new RegExp(needle.replaceAll(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'gi');
    pattern.lastIndex = from;
    const found = pattern.exec(text);
    return found === null ? -1 : found.index;
}

/**
 * A deliberately small scan for the one shape that matters (the table Excel and this grid put on the
 * clipboard), not an HTML parser. A cell's value is its `x:num` attribute when Excel supplied one (the raw
 * number, full precision), otherwise its text content with tags stripped and entities decoded. In ExGrid's
 * own table ({@link invariantMarker}) a cell's {@link markAttribute} is read beside its value.
 */
function parseHtmlTable(html: string): Field[][] | undefined {
    const tableStart = indexOfIgnoringCase(html, '<table', 0);
    if (tableStart < 0) return undefined;
    let tableEnd = indexOfIgnoringCase(html, '</table', tableStart);
    if (tableEnd < 0) tableEnd = html.length;
    // ExGrid's own flavour is raw and locale-free throughout (ADR-0005), so every field of it is invariant;
    // it says so on the table, and nothing else on a clipboard does.
    const tableOpenEnd = html.indexOf('>', tableStart);
    const ownInvariant = tableOpenEnd > 0 && tableOpenEnd < tableEnd
        && indexOfIgnoringCase(html.slice(tableStart, tableOpenEnd), invariantMarker, 0) >= 0;

    const rows: Field[][] = [];
    let position = tableStart;
    for (;;) {
        const rowStart = indexOfTag(html, 'tr', position, tableEnd);
        if (rowStart < 0) break;
        let rowEnd = indexOfIgnoringCase(html, '</tr', rowStart);
        if (rowEnd < 0 || rowEnd > tableEnd) rowEnd = tableEnd;

        const cells: Field[] = [];
        let cellPosition = rowStart;
        for (;;) {
            let cellStart = indexOfTag(html, 'td', cellPosition, rowEnd);
            if (cellStart < 0) cellStart = indexOfTag(html, 'th', cellPosition, rowEnd);
            if (cellStart < 0) break;
            const openEnd = html.indexOf('>', cellStart);
            if (openEnd < 0 || openEnd > rowEnd) break;
            let closeStart = indexOfIgnoringCase(html, '</t', openEnd);
            if (closeStart < 0 || closeStart > rowEnd) closeStart = rowEnd;

            const attributes = html.slice(cellStart, openEnd);
            const content = html.slice(openEnd + 1, closeStart);
            const mark = ownInvariant ? quotedAttribute(attributes, markAttribute) : undefined;
            // Excel's x:num="…" is the raw number behind the displayed text. The bare form (x:num with no
            // value) says only "this is a number", so the text content stands.
            const number = quotedAttribute(attributes, 'x:num');
            cells.push(number === undefined
                ? { value: decodeText(content), origin: ownInvariant ? 'invariant' : 'shownText', mark }
                : { value: number, origin: 'invariant', mark });
            cellPosition = closeStart + 1;
        }
        if (cells.length > 0) rows.push(cells);
        position = rowEnd + 1;
    }
    return rows;
}

/** Finds `<tag` as a real tag open, followed by whitespace, `>` or `/`, between `from` and `until`. */
function indexOfTag(html: string, tag: string, from: number, until: number): number {
    const needle = `<${tag}`;
    let i = from;
    while (i >= 0 && i < until) {
        i = indexOfIgnoringCase(html, needle, i);
        if (i < 0 || i >= until) return -1;
        const after = html.charAt(i + needle.length);
        if (/\s/.test(after) || after === '>' || after === '/') return i;
        i += needle.length;
    }
    return -1;
}

/** The value of `name="…"` among a tag's attributes, as written, or `undefined`. */
function quotedAttribute(attributes: string, name: string): string | undefined {
    const needle = `${name}="`;
    const i = indexOfIgnoringCase(attributes, needle, 0);
    if (i < 0) return undefined;
    const start = i + needle.length;
    const end = attributes.indexOf('"', start);
    return end < 0 ? undefined : attributes.slice(start, end);
}

const entities: Readonly<Record<string, string>> = {
    amp: '&',
    lt: '<',
    gt: '>',
    quot: '"',
    apos: "'",
    '#39': "'",
    // Kept distinct from a plain space until after the trim below: &nbsp; is how Excel's HTML flavour
    // preserves a value's own leading and trailing spaces, so trimming it would silently alter the copied
    // value (ADR-0005's principle).
    nbsp: ' ',
    '#160': ' ',
};

function decodeText(content: string): string {
    let text = '';
    for (let i = 0; i < content.length; i++) {
        const c = content.charAt(i);
        if (c === '<') {
            const end = content.indexOf('>', i);
            if (end < 0) break;
            // A break inside a cell is part of its value; every other tag only wraps.
            const tag = content.slice(i + 1, end).replace(/^\/+/, '').replace(/\/+$/, '').trim();
            if (tag.slice(0, 2).toLowerCase() === 'br') text += '\n';
            i = end;
            continue;
        }
        if (c === '&') {
            const end = content.indexOf(';', i);
            if (end > i && end - i <= 8) {
                const entity = content.slice(i + 1, end);
                const decoded = Object.hasOwn(entities, entity) ? entities[entity] : undefined;
                if (decoded !== undefined) {
                    text += decoded;
                    i = end;
                    continue;
                }
            }
        }
        text += c;
    }
    // Trim only the markup's own pretty-printing whitespace; a no-break space is value, not markup, and
    // becomes an ordinary space only once it is safe.
    return text.replace(/^[ \t\n\r]+|[ \t\n\r]+$/g, '').replaceAll(' ', ' ');
}

function rectangular(rows: readonly (readonly Field[])[], isFromTable: boolean): ClipboardBlock {
    // Not Math.max(...rows): a paste of a hundred thousand lines would pass as many arguments.
    const width = rows.reduce((widest, row) => Math.max(widest, row.length), 0);
    const marked = rows.some(row => row.some(field => field.mark !== undefined));
    // A short line is padded with empty cells, as Excel pads; an empty field reads the same under any
    // culture.
    const padded = rows.map(row => Array.from({ length: width }, (_, c): Field =>
        c < row.length ? elementAt(row, c) : { value: '', origin: 'shownText', mark: undefined }));
    return Object.freeze({
        isFromTable,
        values: Object.freeze(padded.map(row => Object.freeze(row.map(field => field.value)))),
        origins: Object.freeze(padded.map(row => Object.freeze(row.map(field => field.origin)))),
        marks: marked ? Object.freeze(padded.map(row => Object.freeze(row.map(field => field.mark)))) : undefined,
    });
}
