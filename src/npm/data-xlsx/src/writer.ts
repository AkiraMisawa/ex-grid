// An .xlsx workbook written as a stream (ADR-0330 §1), as ExGrid.Data.Xlsx's XlsxWriter writes it, part for part and
// character for character: each worksheet's cells go into the package as they are written, row by row, and the parts
// that need every sheet — the shared strings, the styles, the workbook part, the relationships and the content types —
// at `complete`. Each part is deflated as it is written, so the package waits in memory compressed.

import { formatCell, maxColumns, maxRows } from './cell-reference';
import { equalsIgnoreCase, grouped, roundTrip } from './invariant';
import {
    isValidTableName, tableReference,
    type XlsxCellInput, type XlsxColumnInput, type XlsxFormulaInput, type XlsxRow, type XlsxSheetVisibility, type XlsxTable,
} from './model';
import {
    contentTypesNamespace, dynamicArrayExtension, mainNamespace, officeDocumentType, packageRelationshipsNamespace, relationshipsContent,
    relationshipsNamespace, sharedStringsContent, sharedStringsType, sheetMetadataContent, sheetMetadataType, stylesContent, stylesType,
    tableContent, tableType, workbookContent, worksheetContent, worksheetType,
} from './names';
import { defaultMaxUncompressedBytes } from './pacer';
import { defaultStyles, firstCustomNumberFormat, type XlsxStyles } from './styles';
import { writeStyles } from './styles-writer';
import { carries, encodeXString } from './x-string';
import { XlsxError } from './xlsx-error';
import { XmlTextWriter } from './xml-writer';
import { ZipWriter, type ZipEntryWriter } from './zip-write';
import { foldCase } from './text-casing';
import { elementAt } from './must';

/** A defined name to write. */
export interface XlsxDefinedNameInput {
    /** The name, as `Rate`, or a built-in one such as `_xlnm.Print_Area`. */
    readonly name: string;
    /** What it refers to, as a Formula's text. */
    readonly text: string;
    /** The index of the sheet it is local to; none, for a name of the whole workbook, when left out. */
    readonly localSheet?: number | null;
    /** Whether it is hidden; false when left out. */
    readonly hidden?: boolean;
}

/** What a written workbook says of itself, beside its sheets. A key left out takes the default its comment names. */
export interface XlsxWriteOptions {
    /** The styles the cells' style indexes point into; `defaultStyles` when left out. */
    readonly styles?: XlsxStyles;
    /** The index of the sheet the workbook shows when it opens; 0 when left out. */
    readonly activeSheet?: number;
    /** Whether the workbook counts dates from 1904; the serials written are the caller's. False when left out. */
    readonly date1904?: boolean;
    /** Whether the workbook asks to be calculated in full when Excel opens it (`fullCalcOnLoad`): every Formula is written
     * with its Value, and Excel computes them again (ADR-0330 §6). False when left out. */
    readonly fullCalculationOnLoad?: boolean;
    /** The defined names the workbook part writes; none when left out. */
    readonly definedNames?: readonly XlsxDefinedNameInput[];
    /**
     * The uncompressed bytes the package's parts may hold together, 1 to 1 GiB; 1 GiB, the most a file is read with by
     * default, when left out, so that what is written opens again. A write that would go past it is refused by name
     * (`XlsxError`, `tooLarge`), naming the part, and the sheet, it met; the workbook is then abandoned.
     */
    readonly maxUncompressedBytes?: number;
}

/** The text a part's writer gathers before it is counted and deflated. */
const pieceLength = 64 * 1024;

/** The cells `writeAll` writes between two flushes. */
const writeBatch = 4096;

const encoder = new TextEncoder();

/** Whether `text` holds a character below U+0020. */
function hasControl(text: string): boolean {
    for (let i = 0; i < text.length; i++) if (text.charCodeAt(i) < 0x20) return true;
    return false;
}

/** `char.IsWhiteSpace`: Unicode's White_Space, which is not JavaScript's `\s`. */
function isWhiteSpace(c: number): boolean {
    return (c >= 0x09 && c <= 0x0D) || c === 0x20 || c === 0x85 || c === 0xA0 || c === 0x1680 || (c >= 0x2000 && c <= 0x200A)
        || c === 0x2028 || c === 0x2029 || c === 0x202F || c === 0x205F || c === 0x3000;
}

/** A string item's text (`<t>`), keeping its edges' white space, a line feed and a tab where it has them. */
function writeText(w: XmlTextWriter, text: string): void {
    w.start('t');
    if (text.length > 0 && (isWhiteSpace(text.charCodeAt(0)) || isWhiteSpace(text.charCodeAt(text.length - 1)) || text.includes('\n') || text.includes('\t'))) {
        w.attribute('xml:space', 'preserve');
    }
    w.text(encodeXString(text));
    w.end();
}

/** What a sheet's writer asks of the workbook's. */
interface WorkbookSide {
    readonly styles: XlsxStyles;
    share(text: string): number;
    dynamicArray(): void;
    addTable(sheet: number, table: XlsxTable): string;
    /** Counts and deflates a piece of a part's text, or refuses it past the limit. */
    put(entry: ZipEntryWriter, text: string, part: string, sheet: string | undefined): void;
    checkUsable(): void;
}

/**
 * Writes an `.xlsx` workbook as a stream (ADR-0330 §1): each worksheet's cells go into the package as they are written,
 * and the parts that need every sheet at `complete`. Made by `createXlsxWriter`.
 */
export interface XlsxWriter {
    /**
     * Begins the next worksheet, after the ones before it, ending the one being written. Its cells are then written in the
     * order of the sheet: row by row, left to right.
     * @param name - The sheet's name: 1 to 31 characters, none of `[ ] : * ? / \` and no control character or half of a
     * surrogate pair, not beginning or ending with `'`, and no other sheet's in any case.
     * @param columns - The column widths: ranges that do not overlap, each naming a cell format the styles hold.
     * @param visibility - Whether the sheet is shown; `visible` when left out.
     */
    addSheet(name: string, columns?: readonly XlsxColumnInput[], visibility?: XlsxSheetVisibility): XlsxSheetWriter;
    /** Ends the last worksheet, writes the parts that need every sheet, and returns the package. The workbook needs a sheet,
     * and the options' active sheet must name one of them. */
    complete(): Promise<Blob>;
    /** Abandons a workbook not completed, writing nothing more. */
    abandon(): Promise<void>;
}

/**
 * One worksheet being written (`XlsxWriter.addSheet`): its cells, row by row and left to right. A cell out of that order,
 * or one written twice, is refused.
 */
export interface XlsxSheetWriter {
    /** The sheet's name. */
    readonly name: string;
    /**
     * Adds an Excel Table over cells of this sheet (`tableParts`, and a table part of its own), before the first cell of its
     * header's row is written. Each cell of its header must then be written, as text equal to its column's name (a shared
     * or inline string, no Formula), or the cell, or the sheet's end, is refused; its rows of data hold whatever is written
     * there. A table whose name is another table's or a defined name's in the workbook, in any case, or that overlaps
     * another table of the sheet, is refused.
     */
    addTable(table: XlsxTable): void;
    /** Begins `row` with the cell format of its own that its empty cells take (`customFormat`), after every row written
     * before it; the row's cells, if any, are then written as any others. A row written before, or one that already holds
     * a cell, is refused. */
    writeRow(row: XlsxRow): void;
    /** Writes `cell`, after every cell written before it in the sheet's order. A shared string is written from its `text`
     * into the workbook's shared strings, whatever its `value` says. */
    write(cell: XlsxCellInput): void;
    /** Writes every cell of `cells` in turn, flushing between batches. */
    writeAll(cells: Iterable<XlsxCellInput>): Promise<void>;
    /** Awaits the deflating of what is written so far, so that memory holds it compressed: call it between batches of a
     * large sheet. */
    flush(): Promise<void>;
}

class WorkbookWriter implements XlsxWriter {
    readonly #zip = new ZipWriter();
    readonly #styles: XlsxStyles;
    readonly #activeSheet: number;
    readonly #date1904: boolean;
    readonly #fullCalculation: boolean;
    readonly #definedNames: readonly XlsxDefinedNameInput[];
    readonly #max: number;
    readonly #sheets: { readonly name: string; readonly visibility: XlsxSheetVisibility }[] = [];
    readonly #sharedIndex = new Map<string, number>();
    readonly #shared: string[] = [];
    readonly #tables: { readonly sheet: number; readonly table: XlsxTable }[] = [];
    readonly #closing: Promise<void>[] = [];
    readonly #side: WorkbookSide;
    #current: SheetWriter | undefined;
    #completed = false;
    #refused = false;
    #sharedReferences = 0;
    #uncompressed = 0;
    #dynamic = false;

    constructor(options: XlsxWriteOptions = {}) {
        this.#styles = options.styles === undefined ? defaultStyles : options.styles;
        this.#activeSheet = options.activeSheet === undefined ? 0 : whole(options.activeSheet, 'The active sheet');
        this.#date1904 = options.date1904 === true;
        this.#fullCalculation = options.fullCalculationOnLoad === true;
        this.#definedNames = options.definedNames === undefined ? [] : [...options.definedNames];
        this.#max = options.maxUncompressedBytes === undefined ? defaultMaxUncompressedBytes : options.maxUncompressedBytes;
        if (!Number.isInteger(this.#max) || this.#max < 1 || this.#max > defaultMaxUncompressedBytes) {
            throw new RangeError(`MaxUncompressedBytes is 1 to ${grouped(defaultMaxUncompressedBytes)}.`);
        }
        validateStyles(this.#styles);
        validateNames(this.#definedNames);
        this.#side = {
            styles: this.#styles,
            share: text => {
                this.#sharedReferences++;
                let at = this.#sharedIndex.get(text);
                if (at === undefined) {
                    at = this.#shared.length;
                    this.#shared.push(text);
                    this.#sharedIndex.set(text, at);
                }
                return at;
            },
            dynamicArray: () => {
                this.#dynamic = true;
            },
            addTable: (sheet, table) => this.#addTable(sheet, table),
            put: (entry, text, part, sheet) => this.#put(entry, text, part, sheet),
            checkUsable: () => {
                if (this.#refused) throw new Error('The workbook was refused as too large and is abandoned: nothing more is written to it.');
            },
        };
    }

    /**
     * Begins the next worksheet, after the ones before it, ending the one being written. Its cells are then written in the
     * order of the sheet: row by row, left to right.
     * @param name - The sheet's name: 1 to 31 characters, none of `[ ] : * ? / \` and no control character or half of a
     * surrogate pair, not beginning or ending with `'`, and no other sheet's in any case.
     * @param columns - The column widths: ranges that do not overlap, each naming a cell format the styles hold.
     * @param visibility - Whether the sheet is shown; `visible` when left out.
     */
    addSheet(name: string, columns: readonly XlsxColumnInput[] = [], visibility: XlsxSheetVisibility = 'visible'): XlsxSheetWriter {
        this.#checkOpen();
        this.#checkName(name);
        const sorted = columns.toSorted((a, b) => a.first - b.first);
        let last = -1;
        for (const c of sorted) {
            const style = c.styleIndex === undefined ? 0 : c.styleIndex;
            const range = `${String(c.first)} to ${String(c.last)}`;
            if (!Number.isInteger(c.first) || !Number.isInteger(c.last) || c.first < 0 || c.last < c.first || c.last >= maxColumns) {
                throw new TypeError(`The column range ${range} lies outside the sheet's columns.`);
            }
            if (c.first <= last) throw new TypeError(`The column range ${range} overlaps another, which ends at ${String(last)}.`);
            if (!Number.isInteger(style) || style < 0 || style >= this.#styles.cellFormats.length) {
                throw new TypeError(`The column range ${range} names the cell format ${String(style)}, which the styles do not hold.`);
            }
            last = c.last;
        }
        this.#endCurrent();
        const index = this.#sheets.length;
        this.#sheets.push({ name, visibility });
        const part = `xl/worksheets/sheet${String(index + 1)}.xml`;
        this.#current = new SheetWriter(this.#side, index, name, part, this.#zip.entry(part), sorted, index === this.#activeSheet,
            closing => this.#closing.push(closing));
        return this.#current;
    }

    /**
     * Ends the last worksheet, writes the parts that need every sheet, and returns the package. The workbook needs a sheet,
     * and the options' active sheet must name one of them.
     */
    async complete(): Promise<Blob> {
        this.#checkOpen();
        if (this.#sheets.length === 0) throw new Error('A workbook holds at least one sheet; add one before completing it.');
        if (this.#activeSheet < 0 || this.#activeSheet >= this.#sheets.length) {
            throw new Error(`The active sheet ${String(this.#activeSheet)} is not one of the workbook's ${String(this.#sheets.length)} sheets.`);
        }
        // Completed from here, whatever happens: nothing more is written.
        this.#completed = true;
        this.#endCurrent();
        this.#current = undefined;
        const dynamic = this.#dynamic;
        if (this.#shared.length > 0) this.#part('xl/sharedStrings.xml', w => this.#writeSharedStrings(w));
        this.#part('xl/styles.xml', w => writeStyles(w, this.#styles));
        if (dynamic) this.#part('xl/metadata.xml', writeMetadata);
        this.#tables.forEach(({ table }, i) => {
            const id = i + 1;
            this.#part(`xl/tables/table${String(id)}.xml`, w => writeTable(w, table, id));
        });
        for (const sheet of new Set(this.#tables.map(t => t.sheet))) {
            this.#part(`xl/worksheets/_rels/sheet${String(sheet + 1)}.xml.rels`, w => this.#writeSheetRelationships(w, sheet));
        }
        this.#part('xl/workbook.xml', w => this.#writeWorkbook(w));
        this.#part('xl/_rels/workbook.xml.rels', w => this.#writeWorkbookRelationships(w, dynamic));
        this.#part('_rels/.rels', writeRootRelationships);
        this.#part('[Content_Types].xml', w => this.#writeContentTypes(w, dynamic));
        await Promise.all(this.#closing);
        return this.#zip.finish();
    }

    /** Abandons a workbook not completed, writing nothing more. */
    async abandon(): Promise<void> {
        this.#completed = true;
        this.#current?.abandon();
        this.#current = undefined;
        // What was being deflated is let finish; it is dropped with the writer.
        await Promise.allSettled(this.#closing);
    }

    #checkOpen(): void {
        this.#side.checkUsable();
        if (this.#completed) throw new Error('The workbook is completed or abandoned: nothing more is written to it.');
    }

    #endCurrent(): void {
        this.#current?.end();
    }

    #put(entry: ZipEntryWriter, text: string, part: string, sheet: string | undefined): void {
        this.#side.checkUsable();
        const bytes = encoder.encode(text);
        this.#uncompressed += bytes.length;
        if (this.#uncompressed > this.#max) {
            this.#refused = true;
            throw new XlsxError('tooLarge',
                `The workbook would hold more than the ${grouped(this.#max)} uncompressed bytes a file is written with (XlsxWriteOptions.MaxUncompressedBytes).`,
                { part, sheet });
        }
        entry.write(bytes);
    }

    #part(name: string, write: (w: XmlTextWriter) => void): void {
        const entry = this.#zip.entry(name);
        const w = new XmlTextWriter();
        w.startDocument();
        write(w);
        this.#put(entry, w.take(), name, undefined);
        this.#closing.push(entry.close());
    }

    /** Records a table on the sheet `sheet`, checked against every other table and defined name of the workbook: the id of
     * the relationship the sheet's `tablePart` names. */
    #addTable(sheet: number, table: XlsxTable): string {
        checkTable(table);
        if (this.#tables.some(t => equalsIgnoreCase(t.table.name, table.name)) || this.#definedNames.some(n => equalsIgnoreCase(n.name, table.name))) {
            throw new TypeError(`The workbook already has a table or a defined name named '${table.name}', in some case.`);
        }
        const own = this.#tables.filter(t => t.sheet === sheet).map(t => t.table);
        const other = own.find(t => overlaps(t, table));
        if (other !== undefined) {
            throw new TypeError(`The table '${table.name}' (${tableReference(table)}) overlaps the table '${other.name}' (${tableReference(other)}).`);
        }
        this.#tables.push({ sheet, table });
        return `rId${String(own.length + 1)}`;
    }

    #checkName(name: string): void {
        if (typeof name !== 'string') throw new TypeError("A sheet's name is text.");
        if (name.length === 0 || name.length > 31) throw new TypeError(`A sheet's name is 1 to 31 characters; '${name}' is ${String(name.length)}.`);
        if (!carries(name, false) || hasControl(name)) {
            throw new TypeError("A sheet's name holds no control character and no half of a surrogate pair, which XML cannot carry.");
        }
        if (/[[\]:*?/\\]/.test(name)) throw new TypeError(`A sheet's name holds none of [ ] : * ? / \\; '${name}' does.`);
        if (name.startsWith("'") || name.endsWith("'")) throw new TypeError(`A sheet's name does not begin or end with an apostrophe; '${name}' does.`);
        if (this.#sheets.some(s => equalsIgnoreCase(s.name, name))) throw new TypeError(`The workbook already has a sheet named '${name}', in some case.`);
    }

    #writeSharedStrings(w: XmlTextWriter): void {
        w.start('sst', mainNamespace);
        w.attribute('count', String(this.#sharedReferences));
        w.attribute('uniqueCount', String(this.#shared.length));
        for (const text of this.#shared) {
            w.start('si');
            writeText(w, text);
            w.end();
        }
        w.end();
    }

    #writeSheetRelationships(w: XmlTextWriter, sheet: number): void {
        w.start('Relationships', packageRelationshipsNamespace);
        let id = 1;
        this.#tables.forEach((t, i) => {
            if (t.sheet !== sheet) return;
            w.start('Relationship');
            w.attribute('Id', `rId${String(id++)}`);
            w.attribute('Type', tableType);
            w.attribute('Target', `../tables/table${String(i + 1)}.xml`);
            w.end();
        });
        w.end();
    }

    #writeWorkbook(w: XmlTextWriter): void {
        w.start('workbook', mainNamespace);
        w.attribute('xmlns:r', relationshipsNamespace);
        if (this.#date1904) {
            w.start('workbookPr');
            w.attribute('date1904', '1');
            w.end();
        }
        w.start('bookViews');
        w.start('workbookView');
        if (this.#activeSheet !== 0) w.attribute('activeTab', String(this.#activeSheet));
        w.end();
        w.end();
        w.start('sheets');
        this.#sheets.forEach((s, i) => {
            w.start('sheet');
            w.attribute('name', s.name);
            w.attribute('sheetId', String(i + 1));
            if (s.visibility !== 'visible') w.attribute('state', s.visibility);
            w.attribute('r:id', `rId${String(i + 1)}`);
            w.end();
        });
        w.end();
        if (this.#definedNames.length > 0) {
            w.start('definedNames');
            for (const n of this.#definedNames) {
                w.start('definedName');
                w.attribute('name', n.name);
                if (n.localSheet !== undefined && n.localSheet !== null) w.attribute('localSheetId', String(n.localSheet));
                if (n.hidden === true) w.attribute('hidden', '1');
                w.text(encodeXString(n.text));
                w.end();
            }
            w.end();
        }
        w.start('calcPr');
        w.attribute('calcId', '191029');
        if (this.#fullCalculation) w.attribute('fullCalcOnLoad', '1');
        w.end();
        w.end();
    }

    #writeWorkbookRelationships(w: XmlTextWriter, dynamic: boolean): void {
        w.start('Relationships', packageRelationshipsNamespace);
        let id = 1;
        const relationship = (type: string, target: string): void => {
            w.start('Relationship');
            w.attribute('Id', `rId${String(id++)}`);
            w.attribute('Type', type);
            w.attribute('Target', target);
            w.end();
        };
        this.#sheets.forEach((_, i) => relationship(worksheetType, `worksheets/sheet${String(i + 1)}.xml`));
        relationship(stylesType, 'styles.xml');
        if (this.#shared.length > 0) relationship(sharedStringsType, 'sharedStrings.xml');
        if (dynamic) relationship(sheetMetadataType, 'metadata.xml');
        w.end();
    }

    #writeContentTypes(w: XmlTextWriter, dynamic: boolean): void {
        w.start('Types', contentTypesNamespace);
        const entry = (element: string, key: string, value: string, type: string): void => {
            w.start(element);
            w.attribute(key, value);
            w.attribute('ContentType', type);
            w.end();
        };
        entry('Default', 'Extension', 'rels', relationshipsContent);
        entry('Default', 'Extension', 'xml', 'application/xml');
        entry('Override', 'PartName', '/xl/workbook.xml', workbookContent);
        this.#sheets.forEach((_, i) => entry('Override', 'PartName', `/xl/worksheets/sheet${String(i + 1)}.xml`, worksheetContent));
        entry('Override', 'PartName', '/xl/styles.xml', stylesContent);
        if (this.#shared.length > 0) entry('Override', 'PartName', '/xl/sharedStrings.xml', sharedStringsContent);
        if (dynamic) entry('Override', 'PartName', '/xl/metadata.xml', sheetMetadataContent);
        this.#tables.forEach((_, i) => entry('Override', 'PartName', `/xl/tables/table${String(i + 1)}.xml`, tableContent));
        w.end();
    }
}

/** A writer of an `.xlsx` workbook (`XlsxWriter`).
 * @param options - What the workbook says of itself; the defaults when left out. */
export function createXlsxWriter(options?: XlsxWriteOptions): XlsxWriter {
    return new WorkbookWriter(options);
}

function overlaps(a: XlsxTable, b: XlsxTable): boolean {
    return a.headerRow <= b.headerRow + b.dataRows && b.headerRow <= a.headerRow + a.dataRows
        && a.firstColumn < b.firstColumn + b.columns.length && b.firstColumn < a.firstColumn + a.columns.length;
}

function writeRootRelationships(w: XmlTextWriter): void {
    w.start('Relationships', packageRelationshipsNamespace);
    w.start('Relationship');
    w.attribute('Id', 'rId1');
    w.attribute('Type', officeDocumentType);
    w.attribute('Target', 'xl/workbook.xml');
    w.end();
    w.end();
}

function writeMetadata(w: XmlTextWriter): void {
    const xda = 'http://schemas.microsoft.com/office/spreadsheetml/2017/dynamicarray';
    w.start('metadata', mainNamespace);
    w.attribute('xmlns:xda', xda);
    w.start('metadataTypes');
    w.attribute('count', '1');
    w.start('metadataType');
    for (const [name, value] of [
        ['name', 'XLDAPR'], ['minSupportedVersion', '120000'], ['copy', '1'], ['pasteAll', '1'], ['pasteValues', '1'],
        ['merge', '1'], ['splitFirst', '1'], ['rowColShift', '1'], ['clearFormats', '1'], ['clearComments', '1'],
        ['assign', '1'], ['coerce', '1'], ['cellMeta', '1'],
    ] as const) {
        w.attribute(name, value);
    }
    w.end();
    w.end();
    w.start('futureMetadata');
    w.attribute('name', 'XLDAPR');
    w.attribute('count', '1');
    w.start('bk');
    w.start('extLst');
    w.start('ext');
    w.attribute('uri', dynamicArrayExtension);
    w.start('xda:dynamicArrayProperties');
    w.attribute('fDynamic', '1');
    w.attribute('fCollapsed', '0');
    w.end();
    w.end();
    w.end();
    w.end();
    w.end();
    w.start('cellMetadata');
    w.attribute('count', '1');
    w.start('bk');
    w.start('rc');
    w.attribute('t', '1');
    w.attribute('v', '0');
    w.end();
    w.end();
    w.end();
    w.end();
}

/** A table part (CT_Table): its range, an AutoFilter over it, its columns and Excel's default style. */
function writeTable(w: XmlTextWriter, table: XlsxTable, id: number): void {
    const reference = tableReference(table);
    w.start('table', mainNamespace);
    w.attribute('id', String(id));
    w.attribute('name', table.name);
    w.attribute('displayName', table.name);
    w.attribute('ref', reference);
    w.attribute('totalsRowShown', '0');
    if (table.autoFilter !== false) {
        w.start('autoFilter');
        w.attribute('ref', reference);
        w.end();
    }
    w.start('tableColumns');
    w.attribute('count', String(table.columns.length));
    table.columns.forEach((column, i) => {
        w.start('tableColumn');
        w.attribute('id', String(i + 1));
        w.attribute('name', encodeXString(column));
        w.end();
    });
    w.end();
    w.start('tableStyleInfo');
    w.attribute('name', 'TableStyleMedium2');
    w.attribute('showFirstColumn', '0');
    w.attribute('showLastColumn', '0');
    w.attribute('showRowStripes', '1');
    w.attribute('showColumnStripes', '0');
    w.end();
    w.end();
}

/** A table's own shape: its name as Excel names a Table, its columns, and its range within a sheet. */
function checkTable(table: XlsxTable): void {
    if (!isValidTableName(table.name)) {
        throw new TypeError(
            `'${table.name}' is not a name a table can have: a letter, _ or \\, then letters, digits, _ and ., at most 255 characters, and not a cell's address.`);
    }
    const name = table.name;
    if (table.columns.length === 0) throw new TypeError(`The table '${name}' has at least one column.`);
    const seen = new Set<string>();
    for (const column of table.columns) {
        if (typeof column !== 'string' || column === '') throw new TypeError(`A column of the table '${name}' has no name.`);
        if (!carries(column, true)) throw new TypeError(`A column's name in the table '${name}' holds half of a surrogate pair, which XML cannot carry.`);
        const folded = foldCase(column);
        if (seen.has(folded)) throw new TypeError(`The table '${name}' names the column '${column}' twice, in some case.`);
        seen.add(folded);
    }
    if (!Number.isInteger(table.dataRows) || table.dataRows < 1) {
        throw new TypeError(`The table '${name}' has at least one row of data; Excel holds a table of none as one empty row.`);
    }
    if (!Number.isInteger(table.headerRow) || !Number.isInteger(table.firstColumn) || table.headerRow < 0 || table.firstColumn < 0
        || table.headerRow + table.dataRows >= maxRows || table.firstColumn + table.columns.length > maxColumns) {
        throw new TypeError(`The table '${name}' lies outside the sheet.`);
    }
}

/** `value`, which a whole number's place in the file needs; a fraction is refused, where .NET's `int` cannot hold one. */
function whole(value: number, what: string): number {
    if (!Number.isInteger(value)) throw new TypeError(`${what} is a whole number, not ${String(value)}.`);
    return value;
}

function validateStyles(styles: XlsxStyles): void {
    if (styles.fonts.length === 0 || styles.fills.length < 2 || styles.borders.length === 0 || styles.cellFormats.length === 0) {
        throw new TypeError('A styles part has at least one font, two fills (none and gray125, which Excel reserves), one border and one cell format.');
    }
    styles.cellFormats.forEach((f, i) => {
        for (const id of [f.numberFormatId, f.fontId, f.fillId, f.borderId]) whole(id, `An index of the cell format ${String(i)}`);
        if (f.alignment !== null) {
            whole(f.alignment.indent, `The indent of the cell format ${String(i)}`);
            whole(f.alignment.textRotation, `The text rotation of the cell format ${String(i)}`);
        }
        if (f.fontId < 0 || f.fontId >= styles.fonts.length || f.fillId < 0 || f.fillId >= styles.fills.length
            || f.borderId < 0 || f.borderId >= styles.borders.length || f.numberFormatId < 0
            || (f.numberFormatId >= firstCustomNumberFormat && !styles.numberFormats.has(f.numberFormatId))) {
            throw new TypeError(`The cell format ${String(i)} names a font, fill, border or Number Format the styles do not hold.`);
        }
    });
    // What the styles part writes as text is checked here, so that completing the workbook cannot fail on it.
    if ([...styles.numberFormats.values()].some(code => typeof code !== 'string' || !carries(code, true))
        || styles.fonts.some(f => f.name !== null && !carries(f.name, false))
        || styles.fills.some(f => f.pattern !== null && !carries(f.pattern, false))) {
        throw new TypeError("A Number Format, a font's name or a fill's pattern holds text XML cannot carry: a control character, or half of a surrogate pair.");
    }
}

function validateNames(names: readonly XlsxDefinedNameInput[]): void {
    for (const n of names) {
        if (typeof n.name !== 'string' || n.name === '' || typeof n.text !== 'string') throw new TypeError('A defined name has a name and a text.');
        if (n.localSheet !== undefined && n.localSheet !== null) whole(n.localSheet, `The defined name '${n.name}''s sheet`);
        if (!carries(n.name, false) || hasControl(n.name) || !carries(n.text, true)) {
            throw new TypeError(`The defined name '${n.name}' holds a control character or half of a surrogate pair, which XML cannot carry.`);
        }
    }
}

class SheetWriter implements XlsxSheetWriter {
    readonly #owner: WorkbookSide;
    readonly #index: number;
    readonly #part: string;
    readonly #entry: ZipEntryWriter;
    readonly #xml = new XmlTextWriter();
    readonly #closed: (closing: Promise<void>) => void;
    readonly #tables: { readonly table: XlsxTable; readonly id: string; written: number }[] = [];
    #rowOpen = false;
    #ended = false;
    #row = -1;
    #column = -1;

    /** The sheet's name. */
    readonly name: string;

    constructor(owner: WorkbookSide, index: number, name: string, part: string, entry: ZipEntryWriter, columns: readonly XlsxColumnInput[],
        active: boolean, closed: (closing: Promise<void>) => void) {
        this.#owner = owner;
        this.#index = index;
        this.name = name;
        this.#part = part;
        this.#entry = entry;
        this.#closed = closed;
        const xml = this.#xml;
        xml.startDocument();
        xml.start('worksheet', mainNamespace);
        xml.attribute('xmlns:r', relationshipsNamespace);
        xml.start('sheetViews');
        xml.start('sheetView');
        if (active) xml.attribute('tabSelected', '1');
        xml.attribute('workbookViewId', '0');
        xml.end();
        xml.end();
        xml.start('sheetFormatPr');
        xml.attribute('defaultRowHeight', '15');
        xml.end();
        if (columns.length > 0) {
            xml.start('cols');
            for (const c of columns) {
                xml.start('col');
                xml.attribute('min', String(c.first + 1));
                xml.attribute('max', String(c.last + 1));
                if (c.width !== undefined && c.width !== null) {
                    xml.attribute('width', roundTrip(c.width));
                    if (c.customWidth !== false) xml.attribute('customWidth', '1');
                    if (c.bestFit === true) xml.attribute('bestFit', '1');
                }
                if (c.styleIndex !== undefined && c.styleIndex !== 0) xml.attribute('style', String(c.styleIndex));
                if (c.hidden === true) xml.attribute('hidden', '1');
                xml.end();
            }
            xml.end();
        }
        xml.start('sheetData');
    }

    #checkOpen(): void {
        this.#owner.checkUsable();
        if (this.#ended) throw new Error(`The sheet '${this.name}' is ended: another sheet was begun, or the workbook completed.`);
    }

    /**
     * Adds an Excel Table over cells of this sheet (`tableParts`, and a table part of its own), before the first cell of its
     * header's row is written. Each cell of its header must then be written, as text equal to its column's name (a shared
     * or inline string, no Formula), or the cell, or the sheet's end, is refused; its rows of data hold whatever is written
     * there. A table whose name is another table's or a defined name's in the workbook, in any case, or that overlaps
     * another table of the sheet, is refused.
     */
    addTable(table: XlsxTable): void {
        this.#checkOpen();
        // A copy, so that a list the caller changes afterwards cannot make the table part's names differ from the header written.
        const copy: XlsxTable = Object.freeze({ ...table, columns: Object.freeze([...table.columns]) });
        if (copy.headerRow <= this.#row) {
            throw new TypeError(`The table '${copy.name}' is added after row ${String(this.#row + 1)} was begun: a table is added before its header's row.`);
        }
        const id = this.#owner.addTable(this.#index, copy);
        this.#tables.push({ table: copy, id, written: 0 });
    }

    /**
     * Begins `row` with the cell format of its own that its empty cells take (`customFormat`), after every row written
     * before it; the row's cells, if any, are then written as any others. A row written before, or one that already holds
     * a cell, is refused.
     */
    writeRow(row: XlsxRow): void {
        this.#checkOpen();
        if (!Number.isInteger(row.row) || row.row < 0 || row.row >= maxRows) throw new TypeError(`The row ${String(row.row)} lies outside the sheet.`);
        if (row.row <= this.#row) {
            throw new TypeError(`The row ${String(row.row + 1)} is begun after row ${String(this.#row + 1)}: a sheet's rows are written in order, each once.`);
        }
        if (!Number.isInteger(row.styleIndex) || row.styleIndex < 0 || row.styleIndex >= this.#owner.styles.cellFormats.length) {
            throw new TypeError(`The row ${String(row.row + 1)} names the cell format ${String(row.styleIndex)}, which the styles do not hold.`);
        }
        const xml = this.#xml;
        if (this.#rowOpen) xml.end();
        xml.start('row');
        xml.attribute('r', String(row.row + 1));
        xml.attribute('s', String(row.styleIndex));
        xml.attribute('customFormat', '1');
        this.#rowOpen = true;
        this.#row = row.row;
        this.#column = -1;
        this.#spill();
    }

    /**
     * Writes `cell`, after every cell written before it in the sheet's order. A shared string is written from its `text`
     * into the workbook's shared strings, whatever its `value` says.
     */
    write(cell: XlsxCellInput): void {
        this.#checkOpen();
        if (!Number.isInteger(cell.row) || !Number.isInteger(cell.column) || cell.row < 0 || cell.row >= maxRows || cell.column < 0 || cell.column >= maxColumns) {
            throw new TypeError(`The cell at row ${String(cell.row)}, column ${String(cell.column)} lies outside the sheet.`);
        }
        const reference = formatCell(cell.row, cell.column);
        const type = typeAttribute(cell);
        if (cell.row < this.#row || (cell.row === this.#row && cell.column <= this.#column)) {
            const after = this.#column < 0 ? `row ${String(this.#row + 1)}` : formatCell(this.#row, this.#column);
            throw new TypeError(`The cell ${reference} is written after ${after}: a sheet's cells are written row by row, left to right, each once.`);
        }
        const styleIndex = cell.styleIndex === undefined ? 0 : cell.styleIndex;
        if (!Number.isInteger(styleIndex) || styleIndex < 0 || styleIndex >= this.#owner.styles.cellFormats.length) {
            throw new TypeError(`The cell ${reference} names the cell format ${String(styleIndex)}, which the styles do not hold.`);
        }
        const value = cell.value === undefined ? null : cell.value;
        const text = cell.text === undefined ? null : cell.text;
        const formula = cell.formula === undefined ? null : cell.formula;
        // Every argument is checked before anything is written, so that a refused cell leaves the sheet as it was.
        const kind = formula === null || formula.kind === undefined ? 'normal' : formula.kind;
        const sharedIndex = formula === null || formula.sharedIndex === undefined ? null : formula.sharedIndex;
        if (kind === 'shared' && sharedIndex === null) throw new TypeError(`The cell ${reference}'s shared formula names no group.`);
        if (sharedIndex !== null) whole(sharedIndex, `The cell ${reference}'s shared formula group`);
        if (formula !== null && (typeof formula.text !== 'string' || !carries(formula.text, true)
            || (formula.reference !== undefined && formula.reference !== null && !carries(formula.reference, false)))) {
            throw new TypeError(`The cell ${reference}'s Formula holds text XML cannot carry.`);
        }
        const carried = cell.type === 'sharedString' || cell.type === 'inlineString' ? text === null || carries(text, true)
            : cell.type === 'formulaString' ? value === null || carries(value, true)
                : value === null || carries(value, false);
        if (!carried) throw new TypeError(`The cell ${reference} holds text XML cannot carry: a control character, or half of a surrogate pair.`);
        const header = this.#headerOf(cell, reference, text, formula);
        const xml = this.#xml;
        if (cell.row !== this.#row) {
            if (this.#rowOpen) xml.end();
            xml.start('row');
            xml.attribute('r', String(cell.row + 1));
            this.#rowOpen = true;
            this.#row = cell.row;
        }
        this.#column = cell.column;
        if (header !== undefined) header.written++;
        xml.start('c');
        xml.attribute('r', reference);
        if (styleIndex !== 0) xml.attribute('s', String(styleIndex));
        if (type !== undefined) xml.attribute('t', type);
        if (formula !== null && formula.isDynamicArray === true) {
            xml.attribute('cm', '1');
            this.#owner.dynamicArray();
        }
        if (formula !== null) this.#writeFormula(formula, kind, sharedIndex, reference);
        switch (cell.type) {
            case 'sharedString':
                xml.element('v', String(this.#owner.share(text === null ? '' : text)));
                break;
            case 'inlineString':
                xml.start('is');
                writeText(xml, text === null ? '' : text);
                xml.end();
                break;
            case 'formulaString':
                if (value !== null) xml.element('v', encodeXString(value));
                break;
            case 'number':
            case 'boolean':
            case 'error':
            case 'date':
                if (value !== null) xml.element('v', value);
                break;
            default:
                throw new TypeError(`'${String(cell.type)}' is not a cell's type.`);
        }
        xml.end();
        this.#spill();
    }

    #writeFormula(formula: XlsxFormulaInput, kind: string, sharedIndex: number | null, reference: string): void {
        const xml = this.#xml;
        xml.start('f');
        if (kind !== 'normal') xml.attribute('t', kind);
        if (formula.reference !== undefined && formula.reference !== null) xml.attribute('ref', formula.reference);
        else if (kind === 'array') xml.attribute('ref', reference);
        if (sharedIndex !== null) xml.attribute('si', String(sharedIndex));
        if (formula.text.length > 0) xml.text(encodeXString(formula.text));
        xml.end();
    }

    /** Writes every cell of `cells` in turn, flushing between batches. */
    async writeAll(cells: Iterable<XlsxCellInput>): Promise<void> {
        let n = 0;
        for (const cell of cells) {
            this.write(cell);
            if (++n % writeBatch === 0) await this.flush();
        }
    }

    /** Awaits the deflating of what is written so far, so that memory holds it compressed: call it between batches of a
     * large sheet. */
    async flush(): Promise<void> {
        if (!this.#ended) this.#spill(true);
        await this.#entry.flush();
    }

    /** The count of header cells written of the table whose header holds `cell`, or undefined when none does; a header's
     * cell that does not hold its column's name as text is refused. */
    #headerOf(cell: XlsxCellInput, reference: string, text: string | null, formula: XlsxFormulaInput | null): { written: number } | undefined {
        for (const t of this.#tables) {
            const { table } = t;
            if (cell.row !== table.headerRow || cell.column < table.firstColumn || cell.column >= table.firstColumn + table.columns.length) continue;
            const name = elementAt(table.columns, cell.column - table.firstColumn);
            if ((cell.type !== 'sharedString' && cell.type !== 'inlineString') || formula !== null || text !== name) {
                throw new TypeError(
                    `The cell ${reference} is in the header of the table '${table.name}', and holds its column's name, '${name}', as text, with no Formula.`);
            }
            return t;
        }
        return undefined;
    }

    /** Hands the text gathered to the package once it is long enough, or whatever there is when `all`. */
    #spill(all = false): void {
        if (this.#xml.length === 0 || (!all && this.#xml.length < pieceLength)) return;
        this.#owner.put(this.#entry, this.#xml.take(), this.#part, this.name);
    }

    /** Ends the sheet. */
    end(): void {
        if (this.#ended) return;
        // A table whose header was not written whole would be repaired by Excel: the sheet is not ended, and the caller
        // abandons the workbook.
        for (const { table, written } of this.#tables) {
            if (written !== table.columns.length) {
                throw new Error(`The sheet '${this.name}' ends with ${String(written)} of the ${String(table.columns.length)} cells of the header of the table '${table.name}' written; each holds its column's name.`);
            }
        }
        this.#ended = true;
        const xml = this.#xml;
        if (this.#rowOpen) xml.end();
        xml.end();
        if (this.#tables.length > 0) {
            xml.start('tableParts');
            xml.attribute('count', String(this.#tables.length));
            for (const { id } of this.#tables) {
                xml.start('tablePart');
                xml.attribute('r:id', id);
                xml.end();
            }
            xml.end();
        }
        xml.end();
        this.#owner.put(this.#entry, xml.take(), this.#part, this.name);
        this.#closed(this.#entry.close());
    }

    /** Abandons the sheet. */
    abandon(): void {
        if (this.#ended) return;
        this.#ended = true;
        this.#closed(this.#entry.close());
    }
}

function typeAttribute(cell: XlsxCellInput): string | undefined {
    switch (cell.type) {
        case 'sharedString': return 's';
        case 'inlineString': return 'inlineStr';
        case 'formulaString': return 'str';
        case 'boolean': return 'b';
        case 'error': return 'e';
        case 'date': return 'd';
        case 'number': return undefined;
        default: throw new TypeError(`'${String(cell.type)}' is not a cell's type.`);
    }
}
