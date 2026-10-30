import {
    CellAddress, Sheet,
    type Culture, type Entry, type LinkedTable, type SheetDocument, type Value, type Workbook,
} from '@ex-grid/sheet-engine';
import {
    createXlsxWriter, defaultMaxCells, defaultMaxUncompressedBytes, formatCell, isValidTableName, maxColumns, maxRows, XlsxError,
    type XlsxCellInput, type XlsxSheetWriter, type XlsxWriter,
} from '@ex-grid/data-xlsx';
import { FormatParts } from './format-parts';
import { grouped, roundTrip } from './invariant';
import { elementAt } from './must';
import { XlsxSaveError, type XlsxSaveOptions } from './save-error';
import { SavePacer } from './save-pacer';
import { SheetFormatPlan } from './sheet-format-plan';
import { StyleTable } from './style-table';
import { foldCase } from './text-casing';
import { maxArguments, maxDepth, maxText, writeFormula } from './written-formula';

// Writes a Workbook as it stands to an .xlsx (ADR-0330 §6), as ExSheet.Xlsx's XlsxWorkbookWriter writes it: every Sheet, in
// order, as a worksheet of its name, the Sheet shown shown; every constant; every Formula with the Value it shows, Excel's
// stored prefixes put back; the cells an array spills into with their Values; the Cell Formats at cell, row and column level
// and the column widths in Excel's unit. Each Linked Table is written after the Sheets as an Excel Table of its name, on a
// worksheet of its name, holding its rows as they stand, so that a Formula that reads it (`Positions[PV]`) reads it in Excel
// too. The workbook is marked to be calculated in full when Excel opens it, and a program that does not calculate reads the
// Values the user saw, `RAND()`'s included. What cannot be written faithfully is refused by name, and nothing is returned.
//
// Every Formula is written as a dynamic array (`t="array"`, marked by the workbook's cell metadata as Excel 365 marks one),
// over the cells it spills into or its own cell alone: written plainly, Excel reads it as one of an earlier version, where a
// range an operator or a function's argument takes is implicitly intersected, and its Value would change (ADR-0125).

/** The longest Formula Excel holds, in characters, without its `=`. */
export const maxFormulaLength = 8_192;

/** The longest text a cell of Excel holds, in characters. */
export const maxTextLength = 32_767;

/** The most rows a Linked Table written as an Excel Table holds: a worksheet's 1,048,576, less its header's. */
export const maxLinkedTableRows = maxRows - 1;

/**
 * Writes `workbook` as it stands as an .xlsx, or refuses it by name (`XlsxSaveError`), returning nothing.
 *
 * The Workbook's snapshot is taken before the write first yields — every Sheet's document, every Value it shows, its spills
 * and each Linked Table's rows — and the file is built from that snapshot while yielding (ADR-0330 §9): the file is the
 * Workbook as it stood when the call was made, and the Workbook may change once the returned promise is under way. A Linked
 * Table's worksheet is written for Excel, and is not read back as a Linked Table (ADR-0330 §6): opened again, it is a
 * worksheet of the table's cells.
 *
 * @param workbook - The Workbook.
 * @param options - What the write may produce, and how it paces itself; the defaults when left out.
 * @returns The file, as a Blob of the spreadsheet's media type.
 * @throws XlsxSaveError - The Workbook is refused, and nothing was written.
 * @throws RangeError - `maxUncompressedBytes` lies outside 1 to `defaultMaxUncompressedBytes`, or `maxCells` is not a number.
 */
export async function saveXlsxWorkbook(workbook: Workbook, options: XlsxSaveOptions = {}): Promise<Blob> {
    options.signal?.throwIfAborted();
    const maxBytes = options.maxUncompressedBytes === undefined ? defaultMaxUncompressedBytes : options.maxUncompressedBytes;
    if (!(maxBytes >= 1 && maxBytes <= defaultMaxUncompressedBytes)) {
        throw new RangeError(`MaxUncompressedBytes is 1 to ${grouped(defaultMaxUncompressedBytes)}.`);
    }
    const maxCells = options.maxCells === undefined ? defaultMaxCells : options.maxCells;
    if (typeof maxCells !== 'number' || Number.isNaN(maxCells)) throw new RangeError(`MaxCells is a number of elements, not ${String(maxCells)}.`);

    // Everything the file says is taken here, synchronously, before the write first yields; the rest reads only this.
    const snapshot = snapshotOf(workbook);
    const pacer = new SavePacer(options);
    let writer: XlsxWriter | undefined;
    try {
        writer = await build(snapshot, maxCells, maxBytes, pacer);
        // The parts written last are written here; a write cancelled meanwhile returns nothing.
        const file = await writer.complete();
        options.signal?.throwIfAborted();
        return file;
    } catch (e) {
        await writer?.abandon();
        if (e instanceof XlsxError && e.reason === 'tooLarge') throw tooLarge(e, maxBytes);
        throw e;
    }
}

function tooLarge(e: XlsxError, maxBytes: number): XlsxSaveError {
    const part = e.part === undefined ? '' : e.part;
    return new XlsxSaveError('tooLarge',
        `The workbook would hold more than the ${grouped(maxBytes)} uncompressed bytes a file is written with, and read with by default; the part '${part}' went past it.`,
        { sheet: e.sheet });
}

/** A Sheet as it stood: its document, the Value of every cell holding one, and the Anchor each spilled cell spills from. */
interface SheetSnapshot {
    readonly name: string;
    readonly document: SheetDocument;
    /** By the cell's key. */
    readonly values: ReadonlyMap<number, Value>;
    /** The Anchor of each spilled cell, by the cell's key, in the order the Sheet lists them. */
    readonly spilledFrom: ReadonlyMap<number, { readonly at: CellAddress; readonly anchor: CellAddress }>;
}

/** A Linked Table as it stood: its declaration, and its rows column by column, or `undefined` while it waits. */
interface TableSnapshot {
    readonly table: LinkedTable;
    readonly columns: readonly (readonly (Value | undefined)[])[] | undefined;
}

/**
 * The Workbook as it stood when the write was called: all the write reads, taken synchronously, in time that grows with the
 * cells holding a Value and not with what the file writes for them.
 */
interface Snapshot {
    readonly culture: Culture;
    readonly shown: number;
    readonly sheets: readonly SheetSnapshot[];
    /** Each Sheet's name, by its name folded. */
    readonly sheetNames: ReadonlyMap<string, string>;
    readonly tables: readonly TableSnapshot[];
}

function snapshotOf(workbook: Workbook): Snapshot {
    const document = workbook.toDocument();
    const sheets: SheetSnapshot[] = [];
    const names = new Map<string, string>();
    workbook.sheets.forEach((sheet, i) => {
        const values = new Map<number, Value>();
        const spilledFrom = new Map<number, { readonly at: CellAddress; readonly anchor: CellAddress }>();
        for (const at of sheet.valueAddresses) {
            const value = sheet.getValue(at);
            if (value !== undefined) values.set(at.key, value);
            const anchor = sheet.spilledFrom(at);
            if (anchor !== undefined) spilledFrom.set(at.key, { at, anchor });
        }
        sheets.push({ name: sheet.name, document: elementAt(document.sheets, i), values, spilledFrom });
        if (!names.has(foldCase(sheet.name))) names.set(foldCase(sheet.name), sheet.name);
    });
    // A table's rows are read-only, and replaced whole by a later snapshot: holding the reference holds them as they are.
    const tables = workbook.linkedTables.map(table => ({ table, columns: table.isWaiting ? undefined : workbook.linkedTableColumns(table.name) }));
    return { culture: workbook.culture, shown: indexOf(workbook, workbook.shown), sheets, sheetNames: names, tables };
}

function indexOf(workbook: Workbook, sheet: Sheet): number {
    const sheets = workbook.sheets;
    for (let i = 0; i < sheets.length; i++) {
        if (sheets[i] === sheet) return i;
    }
    return 0;
}

/** One cell element a Sheet writes of its own: an Entry, a spilled Value, or a format. */
interface Recorded {
    readonly at: CellAddress;
    readonly entry: Entry | undefined;
    readonly style: number;
}

interface OwnCell {
    readonly at: CellAddress;
    readonly entry: Entry | undefined;
    readonly own: FormatParts;
}

interface Planned {
    readonly sheet: SheetSnapshot;
    readonly plan: SheetFormatPlan;
    readonly cells: readonly OwnCell[];
}

/** A Linked Table to write as an Excel Table: its declaration, and its snapshot column by column. */
interface PlannedTable {
    readonly table: LinkedTable;
    readonly columns: readonly (readonly (Value | undefined)[])[];
}

/**
 * The snapshot written into a package held in memory, every part but those written on completion: first every element
 * counted against the limit, then written, yielding between slices.
 */
async function build(snapshot: Snapshot, maxCells: number, maxBytes: number, pacer: SavePacer): Promise<XlsxWriter> {
    pacer.sheets = snapshot.sheets.length + snapshot.tables.length;
    const tables = await tablesOf(snapshot, pacer);
    const planned: Planned[] = [];
    let cells = 0;
    for (const sheet of snapshot.sheets) {
        if (!carries(sheet.name, true)) {
            throw new XlsxSaveError('text',
                'The Sheet\'s name holds a character a file cannot carry in a worksheet\'s name: a control character below U+0020, U+FFFE or U+FFFF, or half of a surrogate pair.',
                { sheet: sheet.name });
        }
        const own = ownCells(sheet);
        const plan = new SheetFormatPlan(sheet.document);
        const crossings = plan.crossings(own.map(c => c.at));
        const rows = plan.formattedRows;
        cells += own.length + crossings + rows;
        if (cells > maxCells) {
            throw new XlsxSaveError('tooManyCells',
                `The workbook would write more than the ${grouped(maxCells)} elements it may (XlsxSaveOptions.MaxCells, which counts the cells written and each row a row format writes; by default the cells a file is read with): this Sheet writes ${grouped(own.length)} cells of its own, ${grouped(crossings)} where its formatted rows cross its formatted columns, and ${grouped(rows)} rows its row formats write.`,
                { sheet: sheet.name });
        }
        planned.push({ sheet, plan, cells: own });
        await pacer.checkpoint();
    }
    for (const { table, columns } of tables) {
        cells += table.columns.length;
        for (const column of columns) {
            for (const value of column) if (value !== undefined) cells++;
        }
        if (cells > maxCells) {
            throw new XlsxSaveError('tooManyCells',
                `The workbook would write more than the ${grouped(maxCells)} elements it may (XlsxSaveOptions.MaxCells, which counts the cells written and each row a row format writes; by default the cells a file is read with): the Linked Table '${table.name}' writes ${grouped(table.rowCount)} rows of ${grouped(table.columns.length)} columns under its header.`,
                { sheet: table.name });
        }
    }

    const styles = new StyleTable(snapshot.culture);
    const sheets: { readonly sheet: SheetSnapshot; readonly plan: SheetFormatPlan; readonly cells: readonly Recorded[]; readonly spills: ReadonlyMap<number, CellAddress> }[] = [];
    for (const { sheet, plan, cells: own } of planned) {
        plan.build(styles);
        const recorded: Recorded[] = [];
        for (const { at, entry, own: parts } of own) {
            recorded.push({ at, entry, style: plan.style(styles, parts, at) });
            const step = pacer.step(0);
            if (step !== undefined) await step;
        }
        sheets.push({ sheet, plan, cells: recorded, spills: spillsOf(sheet) });
    }

    pacer.total = cells;
    pacer.sheet = 0;
    const writer = createXlsxWriter({
        styles: styles.build(),
        activeSheet: snapshot.shown,
        fullCalculationOnLoad: true,
        maxUncompressedBytes: maxBytes,
    });
    const declared = new Map<string, readonly string[]>();
    for (const { table } of snapshot.tables) {
        if (!declared.has(foldCase(table.name))) declared.set(foldCase(table.name), table.columns);
    }
    const columnsOf = (name: string): readonly string[] | undefined => declared.get(foldCase(name));
    const hasSheet = (name: string): boolean => snapshot.sheetNames.has(foldCase(name));
    try {
        for (const { sheet, plan, cells: recorded, spills } of sheets) {
            const out = new Paced(writer.addSheet(sheet.name, plan.columns), pacer);
            await writeSheet(out, sheet, plan, recorded, spills, hasSheet, columnsOf);
            pacer.sheet++;
        }
        for (const table of tables) {
            await writeTable(new Paced(writer.addSheet(table.table.name), pacer), table);
            pacer.sheet++;
        }
        pacer.sheet = Math.max(0, pacer.sheets - 1);
        pacer.report();
        return writer;
    } catch (e) {
        await writer.abandon();
        throw e;
    }
}

/** The elements written between two flushes of a sheet's text, so that memory holds it compressed. */
const flushEvery = 4096;

/** A worksheet's writer that counts each element against the pacer, and flushes its text between batches. */
class Paced {
    readonly sheet: XlsxSheetWriter;
    readonly #pacer: SavePacer;
    #since = 0;

    constructor(sheet: XlsxSheetWriter, pacer: SavePacer) {
        this.sheet = sheet;
        this.#pacer = pacer;
    }

    async row(row: number, style: number): Promise<void> {
        this.sheet.writeRow({ row, styleIndex: style });
        await this.#step();
    }

    async cell(cell: XlsxCellInput): Promise<void> {
        this.sheet.write(cell);
        await this.#step();
    }

    async #step(): Promise<void> {
        if (++this.#since >= flushEvery) {
            this.#since = 0;
            await this.sheet.flush();
        }
        const step = this.#pacer.step();
        if (step !== undefined) await step;
    }
}

/**
 * Each Linked Table with its snapshot, in the order they were declared, or the refusal of the first that cannot be written
 * as an Excel Table on a worksheet of its name (ADR-0330 §6).
 */
async function tablesOf(snapshot: Snapshot, pacer: SavePacer): Promise<PlannedTable[]> {
    const tables: PlannedTable[] = [];
    for (const { table, columns } of snapshot.tables) {
        const name = table.name;
        const where = { sheet: name };
        if (table.isWaiting || columns === undefined) {
            throw new XlsxSaveError('linkedTable',
                `The Linked Table '${name}' has not answered: it holds no rows yet, or its last snapshot was refused, and every Formula that reads it shows #GETTING_DATA. Its rows are written as they stand, and it has none to write.`, where);
        }
        const sheet = snapshot.sheetNames.get(foldCase(name));
        if (sheet !== undefined) {
            throw new XlsxSaveError('linkedTable', `The Linked Table '${name}' is written on a worksheet of its name, and the Sheet '${sheet}' already has it.`, where);
        }
        if (!isValidTableName(name)) {
            // The engine takes some names Excel gives no Table, such as A01, which a file reads as the cell A1.
            throw new XlsxSaveError('linkedTable',
                `The Linked Table '${name}' is written as an Excel Table of its name, and Excel gives a Table no such name: it reads as a cell's address, or holds a character a Table's name does not.`, where);
        }
        const why = Sheet.whyNotAName(name);
        if (why !== undefined) {
            throw new XlsxSaveError('linkedTable', `The Linked Table '${name}' is written on a worksheet of its name, and a worksheet cannot have it: ${why}`, where);
        }
        if (table.columns.length > maxColumns) {
            throw new XlsxSaveError('linkedTable',
                `The Linked Table '${name}' has ${grouped(table.columns.length)} columns, and a worksheet of Excel holds ${grouped(maxColumns)}.`, where);
        }
        if (table.rowCount === 0) {
            // ROWS(Positions[PV]) is 0 in ExSheet, and 1 in Excel, which holds a table of no rows as one empty row.
            throw new XlsxSaveError('linkedTable',
                `The Linked Table '${name}' holds no rows, and an Excel Table holds at least one: written with one empty row, its Formulas would answer otherwise in Excel (ROWS gives 1, COUNTBLANK 1, the column itself 0 where ExSheet shows #VALUE!).`, where);
        }
        if (table.rowCount > maxLinkedTableRows) {
            throw new XlsxSaveError('linkedTable',
                `The Linked Table '${name}' holds ${grouped(table.rowCount)} rows, and a worksheet of Excel holds ${grouped(maxRows)}, its header's included.`, where);
        }
        for (let c = 0; c < columns.length; c++) {
            const columnName = elementAt(table.columns, c);
            checkTableText(name, columnName, 0, c, 'as a column\'s name');
            const column = elementAt(columns, c);
            for (let r = 0; r < column.length; r++) {
                const value = column[r];
                if (value !== undefined && value.kind === 'text') {
                    checkTableText(name, value.text, r + 1, c, `in its column '${columnName}', row ${grouped(r + 1)},`);
                }
                const step = pacer.step(0);
                if (step !== undefined) await step;
            }
        }
        tables.push({ table, columns });
    }
    return tables;
}

/**
 * Refuses text of a Linked Table that a cell of Excel cannot hold: longer than 32,767 characters, or holding half of a
 * surrogate pair.
 * @param table - The table's name, which its worksheet has.
 * @param text - The text.
 * @param row - The worksheet's row it is written in, from zero: the header's is 0.
 * @param column - The worksheet's column it is written in, from zero.
 * @param at - Where the table holds it, in words.
 */
function checkTableText(table: string, text: string, row: number, column: number, at: string): void {
    const cell = formatCell(row, column);
    if (text.length > maxTextLength) {
        throw new XlsxSaveError('text',
            `The Linked Table '${table}' holds, ${at} text ${grouped(text.length)} characters long, and a cell of Excel holds at most ${grouped(maxTextLength)}.`, { sheet: table, cell });
    }
    if (!carries(text, false)) {
        throw new XlsxSaveError('text', `The Linked Table '${table}' holds, ${at} text with half of a surrogate pair, which a file cannot carry.`, { sheet: table, cell });
    }
}

/** A Linked Table's worksheet: its header at A1, each column's name, and its rows below as they stand, as an Excel Table of its name. */
async function writeTable(out: Paced, { table, columns }: PlannedTable): Promise<void> {
    out.sheet.addTable({ name: table.name, headerRow: 0, firstColumn: 0, columns: table.columns, dataRows: table.rowCount });
    for (let c = 0; c < table.columns.length; c++) {
        await out.cell({ row: 0, column: c, type: 'sharedString', text: elementAt(table.columns, c) });
    }
    for (let r = 0; r < table.rowCount; r++) {
        for (let c = 0; c < columns.length; c++) {
            const value = elementAt(columns, c)[r];
            if (value === undefined) continue;
            const row = r + 1;
            switch (value.kind) {
                case 'number':
                    await out.cell({ row, column: c, type: 'number', value: roundTrip(value.number) });
                    break;
                case 'text':
                    await out.cell({ row, column: c, type: 'sharedString', text: value.text });
                    break;
                case 'boolean':
                    await out.cell({ row, column: c, type: 'boolean', value: value.boolean ? '1' : '0' });
                    break;
                case 'error':
                    await out.cell({ row, column: c, type: 'error', value: value.toString() });
                    break;
            }
        }
    }
}

/**
 * The cells a Sheet writes of its own, in the sheet's order: each the document records (an Entry, or a format of its own),
 * and each an array spills into.
 */
function ownCells(sheet: SheetSnapshot): OwnCell[] {
    const cells = new Map<number, OwnCell>();
    for (const cell of sheet.document.cells) {
        cells.set(cell.address.key, {
            at: cell.address, entry: cell.entry, own: new FormatParts(cell.numberFormat, cell.alignment, cell.font, cell.fill, cell.borders),
        });
    }
    for (const [key, { at }] of sheet.spilledFrom) {
        if (!cells.has(key)) cells.set(key, { at, entry: undefined, own: FormatParts.empty });
    }
    return [...cells.values()].toSorted((a, b) => a.at.compareTo(b.at));
}

/** Each spilling Anchor, by its key, with the last cell of its Spill Range. */
function spillsOf(sheet: SheetSnapshot): Map<number, CellAddress> {
    const spills = new Map<number, CellAddress>();
    for (const { at, anchor } of sheet.spilledFrom.values()) {
        const last = spills.get(anchor.key);
        spills.set(anchor.key, last === undefined ? at : new CellAddress(Math.max(last.row, at.row), Math.max(last.column, at.column)));
    }
    return spills;
}

/** A Sheet's rows, in order: each formatted row with its format, and every cell element in it, its own and its crossings'. */
async function writeSheet(
    out: Paced, sheet: SheetSnapshot, plan: SheetFormatPlan, cells: readonly Recorded[], spills: ReadonlyMap<number, CellAddress>,
    hasSheet: (name: string) => boolean, columnsOf: (name: string) => readonly string[] | undefined,
): Promise<void> {
    const write = (cell: Recorded): Promise<void> => out.cell(cellOf(sheet, cell, spills, hasSheet, columnsOf));
    const runs = plan.rows;
    let run = 0;
    let runRow = runs.length > 0 ? elementAt(runs, 0).first : Number.MAX_SAFE_INTEGER;
    let next = 0;
    while (next < cells.length || run < runs.length) {
        const cellRow = next < cells.length ? elementAt(cells, next).at.row : Number.MAX_SAFE_INTEGER;
        const row = Math.min(cellRow, runRow);
        let end = next;
        while (end < cells.length && elementAt(cells, end).at.row === row) end++;
        if (row === runRow) {
            const formatted = elementAt(runs, run);
            await out.row(row, formatted.style);
            let at = next;
            for (const crossing of formatted.crossings) {
                for (let column = crossing.first; column <= crossing.last; column++) {
                    while (at < end && elementAt(cells, at).at.column < column) await write(elementAt(cells, at++));
                    if (at < end && elementAt(cells, at).at.column === column) continue;
                    await out.cell({ row, column, type: 'number', value: null, styleIndex: crossing.style });
                }
            }
            while (at < end) await write(elementAt(cells, at++));
            runRow++;
            if (runRow > formatted.last) {
                run++;
                runRow = run < runs.length ? elementAt(runs, run).first : Number.MAX_SAFE_INTEGER;
            }
        } else {
            for (let at = next; at < end; at++) await write(elementAt(cells, at));
        }
        next = end;
    }
}

/** One cell of the Sheet's own: its constant, or its Formula with the Value it shows, or a spilled Value, or its format alone. */
function cellOf(
    sheet: SheetSnapshot, cell: Recorded, spills: ReadonlyMap<number, CellAddress>,
    hasSheet: (name: string) => boolean, columnsOf: (name: string) => readonly string[] | undefined,
): XlsxCellInput {
    const at = cell.at;
    const empty: XlsxCellInput = { row: at.row, column: at.column, type: 'number', value: null, styleIndex: cell.style };
    const entry = cell.entry;
    const constant = entry?.constant;
    if (constant !== undefined) {
        if (constant.kind !== 'text') return valued(sheet, at, constant, empty, undefined);
        checkText(sheet, at, constant.text, undefined);
        return { ...empty, type: 'sharedString', text: constant.text };
    }
    const value = sheet.values.get(at.key);
    const formula = entry?.formula;
    if (entry !== undefined && formula !== undefined) {
        const where = { sheet: sheet.name, cell: at.toString(), held: formula };
        if (formula.length - 1 > maxFormulaLength) {
            throw new XlsxSaveError('formula',
                `The Formula is ${grouped(formula.length - 1)} characters long, and Excel holds at most ${grouped(maxFormulaLength)}.`, { sheet: sheet.name, cell: at.toString() });
        }
        if (!carries(formula, false)) throw new XlsxSaveError('formula', 'The Formula holds half of a surrogate pair, which a file cannot carry.', where);
        const { text: stored, problem } = writeFormula(entry, hasSheet, columnsOf);
        switch (problem) {
            case 'boundCall':
                throw new XlsxSaveError('formula', 'The Formula calls a name a LET binds or a LAMBDA takes, which ExSheet reads as an unknown function and Excel would call.', where);
            case 'longText':
                throw new XlsxSaveError('formula', `The Formula holds a text constant longer than the ${String(maxText)} characters Excel takes in one.`, where);
            case 'tooDeep':
                throw new XlsxSaveError('formula', `The Formula nests its calls deeper than the ${String(maxDepth)} levels Excel takes.`, where);
            case 'tooManyArguments':
                throw new XlsxSaveError('formula', `The Formula makes a call with more than the ${String(maxArguments)} arguments Excel takes.`, where);
            case 'missingSheet':
                throw new XlsxSaveError('formula', 'The Formula reads a Sheet the Workbook does not have, which Excel would read as another workbook\'s.', where);
            case 'structuredReference':
                throw new XlsxSaveError('formula',
                    'The Formula reads, by a structured reference, a Linked Table or a column the Workbook does not declare; the file would hold no Excel Table of it for Excel to read.', where);
            case 'columnName':
                throw new XlsxSaveError('formula',
                    'The Formula reads a Linked Table\'s column whose name begins or ends with a space, which a structured reference in Excel does not keep, or holds @ or one of , : . " { } $ ^ & * + = - < > / or a tab or line break, whose form in a structured reference Excel has not been seen to write.', where);
            case 'tableName':
                throw new XlsxSaveError('formula',
                    'The Formula reads a Linked Table\'s name as a name of its own, which ExSheet shows as #NAME? and Excel would read as the Excel Table written.', where);
            case 'unreadable':
                throw new XlsxSaveError('formula', 'The Formula cannot be written as Excel stores it: its names do not bind as the engine binds them.', where);
            case 'none':
                break;
        }
        const last = spills.get(at.key);
        const range = last === undefined ? at.toString() : `${at.toString()}:${last.toString()}`;
        const written = { text: stored, kind: 'array' as const, reference: range, isDynamicArray: true };
        return { ...(value === undefined ? empty : valued(sheet, at, value, empty, formula)), formula: written };
    }
    return value === undefined ? empty : valued(sheet, at, value, empty, undefined);
}

/** A cell holding `value`: a number, a boolean or an Error Value in the cell, text in the cell as a Formula's result. */
function valued(sheet: SheetSnapshot, at: CellAddress, value: Value, cell: XlsxCellInput, formula: string | undefined): XlsxCellInput {
    switch (value.kind) {
        case 'number':
            return { ...cell, value: roundTrip(value.number) };
        case 'boolean':
            return { ...cell, type: 'boolean', value: value.boolean ? '1' : '0' };
        case 'text':
            checkText(sheet, at, value.text, formula);
            return { ...cell, type: 'formulaString', value: value.text };
        case 'error':
            break;
    }
    const where = { sheet: sheet.name, cell: at.toString(), held: formula };
    if (value.error === '#CIRC!') {
        throw new XlsxSaveError('circularReference',
            'The cell shows #CIRC!: it is part of a circular reference, or depends on one. A file holds no such Error Value, and Excel shows 0 there; no Value it can hold is the one shown.',
            where);
    }
    if (value.error === '#GETTING_DATA') {
        throw new XlsxSaveError('linkedTable', 'The cell shows #GETTING_DATA: it waits on a Linked Table that has not answered.', where);
    }
    return { ...cell, type: 'error', value: value.toString() };
}

function isHighSurrogate(c: number): boolean {
    return c >= 0xD800 && c <= 0xDBFF;
}

function isLowSurrogate(c: number): boolean {
    return c >= 0xDC00 && c <= 0xDFFF;
}

/**
 * Whether a file carries `text`, as the file layer writes it: no half of a surrogate pair anywhere, and in a worksheet's
 * name, which is written as it is, no control character below U+0020 and no U+FFFE or U+FFFF. Elsewhere the file layer
 * escapes those (`_x0001_`).
 */
function carries(text: string, inName: boolean): boolean {
    for (let i = 0; i < text.length; i++) {
        const c = text.charCodeAt(i);
        if (isHighSurrogate(c) && i + 1 < text.length && isLowSurrogate(text.charCodeAt(i + 1))) {
            i++;
            continue;
        }
        if (isHighSurrogate(c) || isLowSurrogate(c)) return false;
        if (inName && (c < 0x20 || c === 0xFFFE || c === 0xFFFF)) return false;
    }
    return true;
}

function checkText(sheet: SheetSnapshot, at: CellAddress, text: string, formula: string | undefined): void {
    const where = { sheet: sheet.name, cell: at.toString(), held: formula };
    if (text.length > maxTextLength) {
        throw new XlsxSaveError('text', `The cell's text is ${grouped(text.length)} characters long, and a cell of Excel holds at most ${grouped(maxTextLength)}.`, where);
    }
    if (!carries(text, false)) throw new XlsxSaveError('text', 'The cell\'s text holds half of a surrogate pair, which a file cannot carry.', where);
}
