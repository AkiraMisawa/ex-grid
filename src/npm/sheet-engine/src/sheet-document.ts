// The serialisable form of a Sheet (C#'s SheetDocument.cs; CONTEXT.md, ADR-0048), read and written as the C#
// engine reads and writes it, so a document written by either opens in the other.
import { CellAddress } from './cell-address';
import { BorderLine, borderLineStyles, CellBorders, CellColour, CellFill, CellFont, type BorderLineStyle, type HorizontalAlignment } from './cell-format';
import { CellRange } from './cell-range';
import { Culture } from './culture';
import { equalsIgnoreCase, hex } from './dotnet';
import { Entry } from './entry';
import { elementAt } from './must';
import { FormulaSyntaxError } from './formula-syntax-error';
import { JsonSyntaxError, JsonWriter, jsonDouble, jsonInt32, jsonText, parseJson, type JsonElement } from './json-element';
import { NumberFormat } from './number-format';
import { Sheet } from './sheet';
import type { SheetColumnWidthKind } from './sheet-change';
import { SheetDocumentAxisFormat, SheetDocumentCell, SheetDocumentColumnWidth, SheetDocumentError, SheetDocumentTable } from './sheet-document-parts';
import { parseTypedError, Value } from './value';

/** The parts of a Sheet Document besides its culture, name, Linked Tables and cells. */
export interface SheetDocumentLayout {
    /** The Cell Formats recorded on whole columns, in column order, adjacent columns recorded alike as one run (ADR-0047, ADR-0071). */
    readonly columns?: readonly SheetDocumentAxisFormat[];
    /** The Cell Formats recorded on whole rows, in row order, adjacent rows recorded alike as one run. */
    readonly rows?: readonly SheetDocumentAxisFormat[];
    /** The widths recorded on columns, in column order, adjacent columns of one width and one kind as one run (ADR-0046). */
    readonly columnWidths?: readonly SheetDocumentColumnWidth[];
}

/**
 * The serialisable form of a Sheet (CONTEXT.md, ADR-0048): its culture, its name (ADR-0046), the Linked Tables declared
 * on it — names, columns and keys, never rows (ADR-0049) — its Entries, the Cell Formats recorded on its columns, rows
 * and cells (ADR-0047, ADR-0071) and the widths set on its columns (ADR-0046), and never a Value: opening one computes
 * every Value again. The Consumer persists it; ExSheet never does.
 *
 * The document is a format with a version. A reader meeting a version it does not know refuses the document rather
 * than guess at it (ADR-0048); so does a reader meeting anything it does not understand. This engine writes version 9
 * and reads versions 1 to 9, each as the C# engine's remarks on SheetDocument describe.
 */
export class SheetDocument {
    /** The version this engine writes. */
    static readonly currentVersion = 9;

    /** The oldest version this engine reads. */
    static readonly oldestReadableVersion = 1;

    /** The cells that hold something, in row-major order. */
    readonly cells: readonly SheetDocumentCell[];

    /** The Cell Formats recorded on whole columns, in column order. Empty for a document read from version 1 or 2. */
    readonly columns: readonly SheetDocumentAxisFormat[];

    /** The Cell Formats recorded on whole rows, in row order. Empty for a document read from version 1 or 2. */
    readonly rows: readonly SheetDocumentAxisFormat[];

    /** The widths recorded on columns, in column order. Empty for a document read from versions 1 to 3. */
    readonly columnWidths: readonly SheetDocumentColumnWidth[];

    /** The name of the Sheet's declared culture, such as `en-US`; empty for the invariant culture, as .NET names it. */
    readonly culture: string;

    /** The Sheet's name (ADR-0046); `Sheet1` for a document read from version 1. */
    readonly name: string;

    /** The Linked Tables declared on the Sheet, in the order they were declared (ADR-0049). */
    readonly linkedTables: readonly SheetDocumentTable[];

    /** @internal */
    constructor(
        culture: string,
        name: string,
        linkedTables: readonly SheetDocumentTable[],
        cells: readonly SheetDocumentCell[],
        layout: SheetDocumentLayout = {},
    ) {
        this.culture = culture;
        this.name = name;
        this.linkedTables = linkedTables;
        this.cells = cells;
        this.columns = layout.columns ?? []; // oxlint-disable-line ex-grid/no-stand-in-default -- a document that records no column format has none, as C#'s init default
        this.rows = layout.rows ?? []; // oxlint-disable-line ex-grid/no-stand-in-default -- none recorded is none
        this.columnWidths = layout.columnWidths ?? []; // oxlint-disable-line ex-grid/no-stand-in-default -- none recorded is none
    }

    /**
     * A Sheet Document built from its parts, as a reader of another format builds one (ADR-0330): a Sheet named `name`
     * holding `cells` under `culture`, declaring no Linked Table. The cells are recorded in row-major order whatever
     * order they come in. Throws a RangeError for a culture this engine does not know, a name a Sheet cannot have, or a
     * cell that appears twice. Nothing is guessed.
     */
    static of(culture: Culture, name: string, cells: Iterable<SheetDocumentCell>, layout: SheetDocumentLayout = {}): SheetDocument {
        Sheet.checkName(name);
        const list: SheetDocumentCell[] = [];
        const seen = new Set<number>();
        for (const cell of cells) {
            if (seen.has(cell.address.key)) throw new RangeError(`The cell ${cell.address.toString()} appears twice.`);
            seen.add(cell.address.key);
            list.push(cell);
        }
        list.sort((a, b) => a.address.compareTo(b.address));
        return new SheetDocument(culture.name, name, [], list, layout);
    }

    /** The format version the document is written in: `currentVersion`, whatever version it was read from. */
    get version(): number {
        return SheetDocument.currentVersion;
    }

    /** Writes the document as JSON, as the C# engine writes it. */
    toJson(): string {
        const json = new JsonWriter();
        json.startObject();
        json.number('version', SheetDocument.currentVersion);
        json.string('culture', this.culture);
        json.string('name', this.name);
        SheetDocument.writeTables(json, this.linkedTables);
        this.writeBody(json);
        json.endObject();
        return json.toString();
    }

    /** The Linked Tables' declarations, as a Sheet Document and a Workbook Document both write them; nothing for none. @internal */
    static writeTables(json: JsonWriter, tables: readonly SheetDocumentTable[]): void {
        if (tables.length === 0) return;
        json.startArray('linkedTables');
        for (const table of tables) {
            json.startObject();
            json.string('name', table.name);
            json.startArray('columns');
            for (const column of table.columns) json.string(undefined, column);
            json.endArray();
            // One key column is written as its name, as version 7 wrote it; several as a list (version 9).
            if (table.keyColumns.length === 1) {
                json.string('key', elementAt(table.keyColumns, 0));
            } else if (table.keyColumns.length > 1) {
                json.startArray('key');
                for (const column of table.keyColumns) json.string(undefined, column);
                json.endArray();
            }
            json.endObject();
        }
        json.endArray();
    }

    /** What a Sheet records of its own: the formats on its columns and rows, its column widths and its cells. @internal */
    writeBody(json: JsonWriter): void {
        writeAxis(json, 'columns', this.columns, run => `${CellAddress.columnName(run.first)}:${CellAddress.columnName(run.last)}`);
        writeAxis(json, 'rows', this.rows, run => `${String(run.first + 1)}:${String(run.last + 1)}`);
        if (this.columnWidths.length > 0) {
            json.startArray('columnWidths');
            for (const run of this.columnWidths) {
                json.startObject();
                json.string('at', `${CellAddress.columnName(run.first)}:${CellAddress.columnName(run.last)}`);
                json.number('width', run.width);
                json.string('kind', run.kind);
                json.endObject();
            }
            json.endArray();
        }
        json.startArray('cells');
        for (const cell of this.cells) {
            json.startObject();
            json.string('at', cell.address.toString());
            const entry = cell.entry;
            if (entry !== undefined) {
                if (entry.formula !== undefined) {
                    json.string('formula', entry.formula);
                } else {
                    const constant = entry.constant;
                    if (constant === undefined) throw new Error('Internal: an Entry holds neither a Formula nor a constant.');
                    switch (constant.kind) {
                        case 'number': json.number('number', constant.number); break;
                        case 'text': json.string('text', constant.text); break;
                        case 'boolean': json.boolean('boolean', constant.boolean); break;
                        case 'error': json.string('error', constant.error); break;
                        default: throw new Error(`Internal: ${String(constant.kind satisfies never)} is not a constant's kind.`);
                    }
                }
            }
            writeCellFormat(json, cell.numberFormat, cell.alignment, cell.font, cell.fill, cell.borders);
            json.endObject();
        }
        json.endArray();
    }

    /**
     * Reads a document written by `toJson`, or by the C# engine. Throws a SheetDocumentError when the version is not
     * one this engine reads (`oldestReadableVersion` to `currentVersion`), or the document holds anything its version
     * does not define. Nothing is guessed.
     */
    static fromJson(text: string): SheetDocument {
        const root = parseDocument(text, 'Sheet Document');
        if (root.kind !== 'object') throw new SheetDocumentError('A Sheet Document is a JSON object.');
        // The version is read before anything else: a document of another version is refused whatever else it holds (ADR-0048).
        const version = property(root, 'version');
        const number = version === undefined ? undefined : jsonInt32(version);
        if (number === undefined) throw new SheetDocumentError('The Sheet Document has no version.');
        if (number < SheetDocument.oldestReadableVersion || number > SheetDocument.currentVersion) {
            throw new SheetDocumentError(
                `The Sheet Document is version ${String(number)}; this engine reads versions ${String(SheetDocument.oldestReadableVersion)} to ${String(SheetDocument.currentVersion)}.`,
                { documentVersion: number });
        }
        return read(root, number, undefined);
    }

    /**
     * Reads one Sheet of a Workbook Document (ADR-0331): its name and what it records of its own, in the current
     * version, under the Workbook's culture. The version, the culture and the Linked Tables are the Workbook's, and are
     * refused here. @internal
     */
    static readSheetOfWorkbook(element: JsonElement, culture: string): SheetDocument {
        if (element.kind !== 'object') throw new SheetDocumentError('A Sheet of a Workbook Document is a JSON object.');
        return read(element, SheetDocument.currentVersion, culture);
    }

    /** The Linked Tables' declarations, as a Sheet Document and a Workbook Document both read them; none declared twice. @internal */
    static readTables(array: JsonElement, version: number): SheetDocumentTable[] {
        if (array.kind !== 'array') throw new SheetDocumentError('The Linked Tables are not an array.');
        const tables: SheetDocumentTable[] = [];
        for (const element of array.items) {
            const table = readTable(element, version);
            if (tables.some(t => equalsIgnoreCase(t.name, table.name))) throw new SheetDocumentError(`The Linked Table '${table.name}' is declared twice.`);
            tables.push(table);
        }
        return tables;
    }

    /** The culture a document names: `''` is the invariant culture, as .NET names it. Throws a SheetDocumentError for one this engine does not know. @internal */
    static resolveCulture(name: string): Culture {
        if (name === '') return Culture.invariant;
        try {
            return Culture.of(name);
        } catch (e) {
            throw new SheetDocumentError(`The culture '${name}' is not known here.`, { cause: e });
        }
    }
}

/** Parses a document's text, refusing text that is not JSON as the document's refusal. @internal */
export function parseDocument(text: string, what: string): JsonElement {
    try {
        return parseJson(text);
    } catch (e) {
        if (e instanceof JsonSyntaxError) throw new SheetDocumentError(`The ${what} is not JSON.`, { cause: e });
        throw e;
    }
}

/** The first property of `object` named `name`, as `TryGetProperty` finds it; `undefined` for none. @internal */
export function property(object: JsonElement & { kind: 'object' }, name: string): JsonElement | undefined {
    return object.properties.find(([n]) => n === name)?.[1];
}

/** The Sheet as a Sheet Document declaring `tables`: none for a Sheet of a Workbook Document, which records them once (ADR-0331). @internal */
export function sheetDocumentOf(sheet: Sheet, tables: readonly SheetDocumentTable[] = sheet.tableDeclarations): SheetDocument {
    const parts = sheet.toDocumentParts();
    return new SheetDocument(sheet.culture.name, sheet.name, tables, parts.cells, parts);
}

function writeAxis(json: JsonWriter, name: string, runs: readonly SheetDocumentAxisFormat[], at: (run: SheetDocumentAxisFormat) => string): void {
    if (runs.length === 0) return;
    json.startArray(name);
    for (const run of runs) {
        json.startObject();
        json.string('at', at(run));
        writeCellFormat(json, run.numberFormat, run.alignment, run.font, run.fill, run.borders);
        json.endObject();
    }
    json.endArray();
}

/**
 * The parts of a Cell Format a cell, row or column records, each only where it records one: `"format"`, `"align"`,
 * `"font"` (only what differs from the default Font), `"fill"` (`"none"` or `"#RRGGBB"`) and `"borders"` (each side
 * that has a line, as `"style"` in Excel's file's name for it and `"color"`). An Automatic colour is not written.
 */
function writeCellFormat(
    json: JsonWriter, format: NumberFormat | undefined, alignment: HorizontalAlignment | undefined, font: CellFont | undefined,
    fill: CellFill | undefined, borders: CellBorders | undefined,
): void {
    if (format !== undefined) json.string('format', format.code);
    if (alignment !== undefined) json.string('align', alignment);
    if (font !== undefined) {
        json.startObject('font');
        if (!font.colour.isAutomatic) json.string('color', colourText(font.colour));
        if (font.bold) json.boolean('bold', true);
        if (font.italic) json.boolean('italic', true);
        if (font.underline) json.boolean('underline', true);
        if (font.strikethrough) json.boolean('strikethrough', true);
        json.endObject();
    }
    if (fill !== undefined) json.string('fill', fill.colour === undefined ? 'none' : colourText(fill.colour));
    if (borders !== undefined) {
        json.startObject('borders');
        writeSide(json, 'top', borders.top);
        writeSide(json, 'bottom', borders.bottom);
        writeSide(json, 'left', borders.left);
        writeSide(json, 'right', borders.right);
        json.endObject();
    }
}

function writeSide(json: JsonWriter, side: string, line: BorderLine): void {
    if (line.isNone) return;
    json.startObject(side);
    json.string('style', lineStyleName(line.style));
    if (!line.colour.isAutomatic) json.string('color', colourText(line.colour));
    json.endObject();
}

function colourText(colour: CellColour): string {
    return `#${hex(colour.rgb, 6)}`;
}

/** Reads a Sheet Document's object, or with `workbookCulture` one Sheet of a Workbook Document. */
function read(root: JsonElement & { kind: 'object' }, number: number, workbookCulture: string | undefined): SheetDocument {
    let culture = workbookCulture;
    let name: string | undefined;
    let tables: SheetDocumentTable[] = [];
    const cells: SheetDocumentCell[] = [];
    const seen = new Set<number>();
    let columns: SheetDocumentAxisFormat[] = [];
    let rows: SheetDocumentAxisFormat[] = [];
    let widths: SheetDocumentColumnWidth[] = [];
    for (const [key, value] of root.properties) {
        if (key === 'version' && workbookCulture === undefined) continue;
        if (key === 'culture' && workbookCulture === undefined) {
            if (value.kind !== 'string') throw new SheetDocumentError('The culture is not a string.');
            culture = value.value;
        } else if (key === 'name' && number >= 2) {
            if (value.kind !== 'string') throw new SheetDocumentError('The name is not a string.');
            name = value.value;
            const why = Sheet.whyNotAName(name);
            if (why !== undefined) throw new SheetDocumentError(`'${name}' is not a Sheet's name: ${why}`);
        } else if (key === 'linkedTables' && number >= 2 && workbookCulture === undefined) {
            tables = SheetDocument.readTables(value, number);
        } else if (key === 'columns' && number >= 3) {
            // The whole Sheet is recorded as whole columns (A:XFD), so a column run may span every row and every column.
            columns = readAxis(value, 'column', number, text => {
                const r = text === undefined ? undefined : CellRange.tryParse(text);
                return r?.isWholeColumns === true ? [r.first.column, r.last.column] : undefined;
            });
        } else if (key === 'rows' && number >= 3) {
            rows = readAxis(value, 'row', number, text => {
                const r = text === undefined ? undefined : CellRange.tryParse(text);
                return r?.isWholeRows === true && !r.isWholeColumns ? [r.first.row, r.last.row] : undefined;
            });
        } else if (key === 'columnWidths' && number >= 4) {
            widths = readWidths(value, number);
        } else if (key === 'cells') {
            if (value.kind !== 'array') throw new SheetDocumentError('The cells are not an array.');
            for (const element of value.items) {
                const cell = readCell(element, number);
                if (seen.has(cell.address.key)) throw new SheetDocumentError(`The cell ${cell.address.toString()} appears twice.`);
                seen.add(cell.address.key);
                cells.push(cell);
            }
        } else {
            throw new SheetDocumentError(workbookCulture === undefined
                ? `'${key}' is not part of a version ${String(number)} Sheet Document.`
                : `'${key}' is not part of a Sheet of a Workbook Document.`);
        }
    }
    if (culture === undefined) throw new SheetDocumentError('The Sheet Document records no culture.');
    if (number >= 2 && name === undefined) {
        throw new SheetDocumentError(workbookCulture === undefined ? 'The Sheet Document records no name.' : 'A Sheet of the Workbook Document records no name.');
    }
    SheetDocument.resolveCulture(culture);
    cells.sort((a, b) => a.address.compareTo(b.address));
    return new SheetDocument(culture, name ?? Sheet.defaultName, tables, cells, { columns, rows, columnWidths: widths });
}

function readAxis(
    array: JsonElement, what: string, version: number, parse: (text: string | undefined) => readonly [number, number] | undefined,
): SheetDocumentAxisFormat[] {
    if (array.kind !== 'array') throw new SheetDocumentError(`The ${what} formats are not an array.`);
    const runs: SheetDocumentAxisFormat[] = [];
    for (const element of array.items) {
        if (element.kind !== 'object') throw new SheetDocumentError(`A ${what} format is not a JSON object.`);
        let at: readonly [number, number] | undefined;
        let format: NumberFormat | undefined;
        let alignment: HorizontalAlignment | undefined;
        let font: CellFont | undefined;
        let fill: CellFill | undefined;
        let borders: CellBorders | undefined;
        for (const [key, value] of element.properties) {
            if (key === 'at') {
                at = value.kind === 'string' ? parse(value.value) : undefined;
                if (at === undefined) throw new SheetDocumentError(`'${jsonText(value)}' is not a ${what} or a run of them.`);
            } else if (key === 'format') {
                format = readFormat(value);
            } else if (key === 'align') {
                alignment = readAlignment(value, true);
            } else if (key === 'font' && version >= 8) {
                font = readFont(value);
            } else if (key === 'fill' && version >= 8) {
                fill = readFill(value);
            } else if (key === 'borders' && version >= 8) {
                borders = readBorders(value);
            } else {
                throw new SheetDocumentError(`'${key}' is not part of a version ${String(version)} ${what} format.`);
            }
        }
        if (at === undefined) throw new SheetDocumentError(`A ${what} format says no ${what}.`);
        const [first, last] = at;
        if (format === undefined && alignment === undefined && font === undefined && fill === undefined && borders === undefined) {
            throw new SheetDocumentError(`The ${what} format at ${String(first)} sets nothing.`);
        }
        if (runs.some(r => r.first <= last && first <= r.last)) throw new SheetDocumentError(`A ${what} is formatted twice.`);
        runs.push(new SheetDocumentAxisFormat(first, last, format, alignment, font, fill, borders));
    }
    runs.sort((a, b) => a.first - b.first);
    return runs;
}

function readWidths(array: JsonElement, version: number): SheetDocumentColumnWidth[] {
    if (array.kind !== 'array') throw new SheetDocumentError('The column widths are not an array.');
    const runs: SheetDocumentColumnWidth[] = [];
    for (const element of array.items) {
        if (element.kind !== 'object') throw new SheetDocumentError('A column width is not a JSON object.');
        let at: readonly [number, number] | undefined;
        let width: number | undefined;
        // Version 4 recorded only widths the user set (ADR-0046).
        let kind: SheetColumnWidthKind | undefined = version < 5 ? 'setByUser' : undefined;
        for (const [key, value] of element.properties) {
            if (key === 'at') {
                const r = value.kind === 'string' ? CellRange.tryParse(value.value) : undefined;
                if (r?.isWholeColumns !== true) throw new SheetDocumentError(`'${jsonText(value)}' is not a column or a run of them.`);
                at = [r.first.column, r.last.column];
            } else if (key === 'width') {
                const w = jsonDouble(value);
                if (w === undefined || !(w > 0 && w <= Sheet.maxColumnWidth)) {
                    throw new SheetDocumentError(`'${jsonText(value)}' is not a column's width: more than 0 and at most ${String(Sheet.maxColumnWidth)} characters.`);
                }
                width = w;
            } else if (key === 'custom' && version === 5) {
                // Version 5's custom width stopped widening; any other was widened again (ADR-0046, 2026-09-28).
                if (value.kind === 'true') kind = 'setByUser';
                else if (value.kind === 'false') kind = 'widenedByEntry';
                else throw new SheetDocumentError(`'${jsonText(value)}' does not say whether a column's width is custom.`);
            } else if (key === 'kind' && version >= 6) {
                if (value.kind !== 'string' || (value.value !== 'setByUser' && value.value !== 'widenedByEntry')) {
                    throw new SheetDocumentError(`'${jsonText(value)}' is not a kind of column width: "widenedByEntry" or "setByUser".`);
                }
                kind = value.value;
            } else {
                throw new SheetDocumentError(`'${key}' is not part of a version ${String(version)} column width.`);
            }
        }
        if (at === undefined) throw new SheetDocumentError('A column width says no column.');
        const [first, last] = at;
        if (width === undefined) throw new SheetDocumentError(`The column width at ${CellAddress.columnName(first)} gives no width.`);
        if (kind === undefined) throw new SheetDocumentError(`The column width at ${CellAddress.columnName(first)} does not say its kind.`);
        if (runs.some(r => r.first <= last && first <= r.last)) throw new SheetDocumentError('A column is given a width twice.');
        runs.push(new SheetDocumentColumnWidth(first, last, width, kind));
    }
    runs.sort((a, b) => a.first - b.first);
    return runs;
}

function readFormat(value: JsonElement): NumberFormat {
    const reading = value.kind === 'string' ? NumberFormat.tryParse(value.value) : undefined;
    if (reading?.format === undefined) throw new SheetDocumentError(`'${jsonText(value)}' is not a number format this version reads.`);
    return reading.format;
}

/**
 * A colour as version 8 records it: `"automatic"` or `"#RRGGBB"`. Any other kind — an `.xlsx` theme colour, a colour's
 * name — is refused by name, never guessed at (ADR-0048, 2026-09-30).
 */
function readColour(value: JsonElement): CellColour {
    if (value.kind === 'string') {
        if (value.value === 'automatic') return CellColour.automatic;
        const rgb = CellColour.fromHex(value.value);
        if (rgb !== undefined) return rgb;
    }
    throw new SheetDocumentError(`'${jsonText(value)}' is not a colour a Sheet Document records: a colour is "automatic" or "#RRGGBB", and no other kind is read.`);
}

function readFont(value: JsonElement): CellFont {
    if (value.kind !== 'object') throw new SheetDocumentError(`'${jsonText(value)}' is not a Font.`);
    let font = CellFont.default;
    for (const [key, element] of value.properties) {
        switch (key) {
            case 'color': font = font.with({ colour: readColour(element) }); break;
            case 'bold': font = font.with({ bold: readEmphasis(key, element) }); break;
            case 'italic': font = font.with({ italic: readEmphasis(key, element) }); break;
            case 'underline': font = font.with({ underline: readEmphasis(key, element) }); break;
            case 'strikethrough': font = font.with({ strikethrough: readEmphasis(key, element) }); break;
            default: throw new SheetDocumentError(`'${key}' is not part of a Font: its colour, bold, italic, a single underline and strikethrough.`);
        }
    }
    return font;
}

function readEmphasis(name: string, element: JsonElement): boolean {
    if (element.kind === 'true') return true;
    if (element.kind === 'false') return false;
    throw new SheetDocumentError(`'${jsonText(element)}' does not say whether a Font is ${name}.`);
}

function readFill(value: JsonElement): CellFill {
    if (value.kind === 'string') {
        if (value.value === 'none') return CellFill.none;
        const rgb = CellColour.fromHex(value.value);
        if (rgb !== undefined) return CellFill.solid(rgb);
    }
    throw new SheetDocumentError(`'${jsonText(value)}' is not a Fill a Sheet Document records: a Fill is "none" or "#RRGGBB", and no other kind of colour is read.`);
}

function readBorders(value: JsonElement): CellBorders {
    if (value.kind !== 'object') throw new SheetDocumentError(`'${jsonText(value)}' is not a cell's Borders.`);
    let borders = CellBorders.none;
    for (const [key, element] of value.properties) {
        switch (key) {
            case 'top': borders = borders.with({ top: readSide(key, element) }); break;
            case 'bottom': borders = borders.with({ bottom: readSide(key, element) }); break;
            case 'left': borders = borders.with({ left: readSide(key, element) }); break;
            case 'right': borders = borders.with({ right: readSide(key, element) }); break;
            default: throw new SheetDocumentError(`'${key}' is not a side of a cell: top, bottom, left or right.`);
        }
    }
    return borders;
}

function readSide(side: string, value: JsonElement): BorderLine {
    if (value.kind !== 'object') throw new SheetDocumentError(`'${jsonText(value)}' is not the line on a cell's ${side} side.`);
    let style: BorderLineStyle | undefined;
    let colour = CellColour.automatic;
    for (const [key, element] of value.properties) {
        if (key === 'style') {
            style = element.kind === 'string' ? lineStyleOf(element.value) : undefined;
            if (style === undefined) throw new SheetDocumentError(`'${jsonText(element)}' is not one of Excel's thirteen line styles.`);
        } else if (key === 'color') {
            colour = readColour(element);
        } else {
            throw new SheetDocumentError(`'${key}' is not part of a Border: its line style and its colour.`);
        }
    }
    if (style === undefined) throw new SheetDocumentError(`The line on a cell's ${side} side says no line style.`);
    return new BorderLine(style, colour);
}

/** The names Excel's own file gives the thirteen line styles (`ST_BorderStyle`), where they differ from the style's own. */
const fileNames: Partial<Record<BorderLineStyle, string>> = { slantedDashDot: 'slantDashDot' };

function lineStyleName(style: BorderLineStyle): string {
    return fileNames[style] ?? style;
}

function lineStyleOf(name: string): BorderLineStyle | undefined {
    return borderLineStyles.find(s => s !== 'none' && lineStyleName(s) === name);
}

function readAlignment(value: JsonElement, allowGeneral: boolean): HorizontalAlignment {
    if (value.kind === 'string') {
        switch (value.value) {
            case 'general':
                if (allowGeneral) return 'general';
                break;
            case 'left':
            case 'center':
            case 'right':
                return value.value;
            default:
                break;
        }
    }
    throw new SheetDocumentError(`'${jsonText(value)}' is not an alignment.`);
}

function readTable(element: JsonElement, version: number): SheetDocumentTable {
    if (element.kind !== 'object') throw new SheetDocumentError('A Linked Table is not a JSON object.');
    let name: string | undefined;
    let columns: string[] | undefined;
    let key: string[] = [];
    for (const [property_, value] of element.properties) {
        if (property_ === 'name') {
            if (value.kind !== 'string') throw new SheetDocumentError(`'${jsonText(value)}' is not a Linked Table's name.`);
            name = value.value;
        } else if (property_ === 'columns') {
            if (value.kind !== 'array') throw new SheetDocumentError("A Linked Table's columns are not an array.");
            columns = value.items.map(c => {
                if (c.kind !== 'string') throw new SheetDocumentError(`'${jsonText(c)}' is not a column name.`);
                return c.value;
            });
        } else if (property_ === 'key' && version >= 7 && value.kind === 'string') {
            key = [value.value];
        } else if (property_ === 'key' && version >= 9 && value.kind === 'array') {
            key = value.items.map(c => {
                if (c.kind !== 'string') throw new SheetDocumentError(`'${jsonText(c)}' is not a Linked Table's key column.`);
                return c.value;
            });
            if (key.length < 2) throw new SheetDocumentError(`A key of several columns lists at least two; '${jsonText(value)}' does not.`);
        } else if (property_ === 'key' && version >= 7) {
            throw new SheetDocumentError(`'${jsonText(value)}' is not a Linked Table's key column.`);
        } else {
            throw new SheetDocumentError(`'${property_}' is not part of a version ${String(version)} Linked Table's declaration.`);
        }
    }
    if (name === undefined) throw new SheetDocumentError('A Linked Table has no name.');
    if (columns === undefined) throw new SheetDocumentError(`The Linked Table '${name}' has no columns.`);
    const why = Sheet.whyNotALinkedTable(name, columns);
    if (why !== undefined) throw new SheetDocumentError(why);
    const whyNotKey = Sheet.whyNotAKey(name, columns, key);
    if (whyNotKey !== undefined) throw new SheetDocumentError(whyNotKey);
    return new SheetDocumentTable(name, columns, key);
}

function readCell(element: JsonElement, version: number): SheetDocumentCell {
    if (element.kind !== 'object') throw new SheetDocumentError('A cell is not a JSON object.');
    let address: CellAddress | undefined;
    let entry: Entry | undefined;
    let format: NumberFormat | undefined;
    let alignment: HorizontalAlignment | undefined;
    let font: CellFont | undefined;
    let fill: CellFill | undefined;
    let borders: CellBorders | undefined;
    const single = (next: Entry): Entry => {
        if (entry !== undefined) throw new SheetDocumentError('A cell holds more than one Entry.');
        return next;
    };
    for (const [key, value] of element.properties) {
        switch (key) {
            case 'at': {
                const at = value.kind === 'string' ? CellAddress.tryParse(value.value) : undefined;
                if (at === undefined) throw new SheetDocumentError(`'${jsonText(value)}' is not a cell address.`);
                address = at;
                break;
            }
            case 'number': {
                const n = jsonDouble(value);
                if (n === undefined) throw new SheetDocumentError(`'${jsonText(value)}' is not a number.`);
                entry = single(Entry.fromValue(Value.fromNumber(n)));
                break;
            }
            case 'text':
                if (value.kind !== 'string') throw new SheetDocumentError(`'${jsonText(value)}' is not text.`);
                entry = single(Entry.fromValue(Value.fromText(value.value)));
                break;
            case 'boolean':
                if (value.kind !== 'true' && value.kind !== 'false') throw new SheetDocumentError(`'${jsonText(value)}' is not a boolean.`);
                entry = single(Entry.fromValue(Value.fromBoolean(value.kind === 'true')));
                break;
            case 'error': {
                const error = value.kind === 'string' ? parseTypedError(value.value) : undefined;
                if (error === undefined) throw new SheetDocumentError(`'${jsonText(value)}' is not an Error Value a cell can hold.`);
                entry = single(Entry.fromValue(Value.fromError(error)));
                break;
            }
            case 'formula': {
                if (value.kind !== 'string') throw new SheetDocumentError(`'${jsonText(value)}' is not a Formula.`);
                let formula: Entry;
                try {
                    formula = Entry.fromFormula(value.value);
                } catch (e) {
                    if (e instanceof FormulaSyntaxError) throw new SheetDocumentError(e.message, { cause: e });
                    throw e;
                }
                entry = single(formula);
                break;
            }
            case 'format':
                format = readFormat(value);
                // Before version 3 a cell's General was no format of its own: no level could show through it.
                if (version < 3 && format.isGeneral) format = undefined;
                break;
            case 'align':
                alignment = readAlignment(value, version >= 3);
                break;
            default:
                if (key === 'font' && version >= 8) font = readFont(value);
                else if (key === 'fill' && version >= 8) fill = readFill(value);
                else if (key === 'borders' && version >= 8) borders = readBorders(value);
                else throw new SheetDocumentError(`'${key}' is not part of a version ${String(version)} cell.`);
        }
    }
    if (address === undefined) throw new SheetDocumentError('A cell has no address.');
    if (entry === undefined && format === undefined && alignment === undefined && font === undefined && fill === undefined && borders === undefined) {
        throw new SheetDocumentError(`The cell ${address.toString()} holds nothing.`);
    }
    return new SheetDocumentCell(address, entry, format, alignment, font, fill, borders);
}
