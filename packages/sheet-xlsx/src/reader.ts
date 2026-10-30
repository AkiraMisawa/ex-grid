import {
    CellAddress, CellBorders, CellFill, CellFont, CellRange, Culture, Entry, FormulaSyntaxError, NumberFormat, Sheet, SheetDocument,
    SheetDocumentAxisFormat, SheetDocumentCell, SheetDocumentColumnWidth, Value, WorkbookDocument,
} from '@ex-grid/sheet-engine';
import {
    openXlsx, parseCell, formatCell, XlsxError,
    type XlsxCell, type XlsxColumn, type XlsxDefinedName, type XlsxFormula, type XlsxReader, type XlsxReadOptions, type XlsxRow,
    type XlsxSheetReader,
} from '@ex-grid/data-xlsx';
import { readWidth } from './column-width-unit';
import { showsDate } from './date-formats';
import { defaultFormat, FormatParts } from './format-parts';
import { parseDouble } from './invariant';
import { isoDateSerial } from './iso-date';
import { defined, elementAt, unreachable } from './must';
import { XlsxOpenError } from './open-error';
import { OpenReport, type NotCarried, type NotCarriedKind, type UnsupportedFunctionCell, type XlsxOpenResult } from './open-report';
import { partsOf } from './sheet-format-plan';
import { elementNotCarried } from './sheet-elements';
import { readStoredFormula, stripPrefixes } from './stored-formula';
import { StyleReading, type ReadFormat } from './style-reading';
import { equalsIgnoreCase, foldCase, startsWithIgnoreCase } from './text-casing';

// Reads an .xlsx workbook into a Workbook Document and its Open Report (ADR-0330 §3, §4, §5, §7), as ExSheet.Xlsx's
// XlsxWorkbookReader reads it. Every worksheet becomes a Sheet, in the file's order, and the worksheet shown is the Sheet
// shown. Constants — numbers, text, booleans, Error Values — and Formulas are carried, never a Value the file stored: opening
// the document computes every Value again. Anything that would change a Value is refused by name, naming the Sheet, the cell
// and what it held, and nothing opens. What changes only how a Sheet looks, or what else the file held, is listed in the
// Open Report. Cell Formats cross at the levels the file records them — cell, row and column — with the column widths, in
// Excel's own unit (ADR-0071); a colour that names the theme is the RGB the file's theme gives it (ADR-0330 §8).

/**
 * Reads the workbook in `file` into a Workbook Document under `culture`, with its Open Report, or refuses it by name
 * (`XlsxOpenError`). The read goes a batch of cells at a time, reporting progress and yielding between slices as `options`
 * says, so a browser keeps painting while a large file is read.
 *
 * @param file - The file's bytes.
 * @param culture - The Workbook's culture, declared by the application as a CSV's encoding is (ADR-0330 §7): how typing is
 *   read and Values are shown from here on. No Value read from the file depends on it, and nothing in the file is taken for
 *   it. A locale the engine holds no data for throws a `RangeError` before the file is read.
 * @param options - The file layer's limits, progress, pacing and cancellation (ADR-0330 §9); its defaults when left out. An
 *   aborted `signal` is looked at between slices, and a cancelled read opens nothing.
 * @returns The Workbook Document and its Open Report.
 * @throws XlsxOpenError - The workbook is refused, and nothing opens.
 */
export async function openXlsxWorkbook(file: Uint8Array | ArrayBuffer | Blob, culture: Culture | string, options?: XlsxReadOptions): Promise<XlsxOpenResult> {
    // Refused before the file is read: a document under a culture the engine cannot name could not be opened.
    const workbookCulture = typeof culture === 'string' ? Culture.of(culture) : culture;
    let reader: XlsxReader;
    try {
        reader = await openXlsx(file, options);
    } catch (e) {
        if (e instanceof XlsxError) throw XlsxOpenError.fromFile(e);
        throw e;
    }
    try {
        return await new Mapping(reader, workbookCulture, options?.signal).read();
    } catch (e) {
        if (e instanceof XlsxError) throw XlsxOpenError.fromFile(e);
        throw e;
    }
}

/** How many days the 1904 date system's serials lie below 1900's for the same day. */
const days1904 = 1462;

const filterDatabase = '_xlnm._FilterDatabase';
const printArea = '_xlnm.Print_Area';
const printTitles = '_xlnm.Print_Titles';

/** A format not carried, by kind, with the first place it was met. */
interface FormatNotCarried {
    readonly kind: NotCarriedKind;
    readonly detail: string;
}

/**
 * The formats the columns record, by column, with the parts any of them records: a formatted row records those parts even
 * at their defaults, because in the file a row's format stands in place of its columns', where in a Sheet each part of a
 * row's level that records nothing takes the column's.
 */
interface ColumnLevels {
    readonly byColumn: ReadonlyMap<number, FormatParts>;
    readonly runs: readonly SheetDocumentAxisFormat[];
    readonly recorded: FormatParts;
}

/** A shared formula's master: its Entry where it stands, and what it names. */
interface SharedMaster {
    readonly entry: Entry;
    readonly row: number;
    readonly column: number;
    readonly unsupported: readonly string[];
}

/** One workbook's mapping, Sheet by Sheet. */
class Mapping {
    readonly #reader: XlsxReader;
    readonly #culture: Culture;
    readonly #signal: AbortSignal | undefined;
    readonly #notCarried: NotCarried[] = [];
    readonly #unsupported: UnsupportedFunctionCell[] = [];
    readonly #definedNames: ReadonlySet<string>;
    readonly #hasExcelTables: boolean;
    readonly #dateStyles = new Map<number, boolean>();
    readonly #styles: StyleReading;
    #namesTheme = false;

    constructor(reader: XlsxReader, culture: Culture, signal: AbortSignal | undefined) {
        this.#reader = reader;
        this.#culture = culture;
        this.#signal = signal;
        this.#definedNames = new Set(reader.definedNames.map(n => foldCase(n.name)));
        this.#hasExcelTables = reader.parts.some(p => startsWithIgnoreCase(p, 0, 'xl/tables/'));
        this.#styles = new StyleReading(reader.styles, reader.theme);
    }

    async read(): Promise<XlsxOpenResult> {
        const reader = this.#reader;
        const worksheets: number[] = [];
        const names = new Map<string, string>();
        for (let i = 0; i < reader.sheets.length; i++) {
            const info = elementAt(reader.sheets, i);
            if (info.kind !== 'worksheet' || info.part === null) {
                this.#notCarried.push({
                    kind: 'chartSheet', sheet: info.name, detail: null,
                    description: `The sheet '${info.name}' is not a worksheet (a chart sheet, or another that holds no cells), and is not opened.`,
                });
                continue;
            }
            const why = Sheet.whyNotAName(info.name);
            if (why !== undefined) throw new XlsxOpenError('sheets', `The worksheet's name is not one a Sheet can have: ${why}`, { sheet: info.name });
            const same = names.get(foldCase(info.name));
            if (same !== undefined) {
                throw new XlsxOpenError('sheets',
                    `The worksheets '${same}' and '${info.name}' have names that differ only in case, which two Sheets of one Workbook cannot have.`, { sheet: info.name });
            }
            names.set(foldCase(info.name), info.name);
            worksheets.push(i);
        }
        if (worksheets.length === 0) throw new XlsxOpenError('sheets', 'The workbook holds no worksheet, so it holds no cells to open.');

        this.#workbookParts();
        for (const name of reader.definedNames) {
            // A name local to a worksheet is said with that Sheet's entries; one of the workbook's, or of a chart sheet, here.
            const local = name.localSheet;
            if (local !== null && worksheets.includes(local)) continue;
            const shown = builtInName(name);
            if (shown === undefined) continue;
            const sheet = local !== null && local >= 0 && local < reader.sheets.length ? elementAt(reader.sheets, local).name : null;
            this.#notCarried.push({ kind: 'definedName', sheet, detail: shown, description: describeName(name, shown, sheet) });
        }
        const workbookEntries = this.#notCarried.length;
        const sheets: SheetDocument[] = [];
        for (const index of worksheets) {
            const info = elementAt(reader.sheets, index);
            if (info.visibility !== 'visible') {
                this.#notCarried.push({ kind: 'hiddenSheet', sheet: info.name, detail: null, description: `The Sheet '${info.name}' is hidden in the file, and opens shown.` });
            }
            let printNames = false;
            for (const name of reader.definedNames) {
                if (name.localSheet !== index) continue;
                if (equalsIgnoreCase(name.name, printArea) || equalsIgnoreCase(name.name, printTitles)) {
                    printNames = true;
                } else {
                    const shown = builtInName(name);
                    if (shown !== undefined) {
                        this.#notCarried.push({ kind: 'definedName', sheet: info.name, detail: shown, description: describeName(name, shown, info.name) });
                    }
                }
            }
            sheets.push(await this.#readSheet(index, printNames));
        }
        if (this.#namesTheme) {
            // Said of the whole workbook, with what else it holds, before each Sheet's entries.
            this.#notCarried.splice(workbookEntries, 0, {
                kind: 'themeColours', sheet: null, detail: null,
                description: 'Colours of the workbook name its theme: each is carried as the RGB value the file\'s theme gives it, and the link to the theme is not, so another theme will not recolour them.',
            });
        }
        const shownIndex = worksheets.includes(reader.activeSheet) ? reader.activeSheet : elementAt(worksheets, 0);
        const document = WorkbookDocument.of(this.#culture, elementAt(reader.sheets, shownIndex).name, sheets);
        return { document, report: new OpenReport(this.#notCarried, this.#unsupported) };
    }

    /** What the workbook as a whole holds, beside its sheets, as its parts say. */
    #workbookParts(): void {
        const parts = this.#reader.parts;
        const has = (folder: string): boolean => parts.some(p => startsWithIgnoreCase(p, 0, folder));
        const add = (kind: NotCarriedKind, description: string): void => {
            this.#notCarried.push({ kind, sheet: null, detail: null, description });
        };
        if (has('xl/pivotTables/')) add('pivotTables', 'The workbook holds PivotTables: the cells they show are carried as constants, the PivotTables are not.');
        if (has('xl/externalLinks/')) add('externalLinks', 'The workbook holds links to other workbooks, which no Formula carried reads; they are not carried.');
        if (has('xl/connections.xml') || has('xl/queryTables/')) {
            add('dataConnections', 'The workbook holds connections to outside data, which are not carried; the cells they filled are carried as constants.');
        }
        if (has('xl/slicers/') || has('xl/slicerCaches/') || has('xl/timelines/') || has('xl/timelineCaches/')) {
            add('slicers', 'The workbook holds slicers or timelines, which are not carried.');
        }
    }

    async #readSheet(index: number, printNames: boolean): Promise<SheetDocument> {
        const sheet = await this.#reader.openSheet(index);
        try {
            const name = sheet.sheet.name;
            const cells: SheetDocumentCell[] = [];
            const shared = new Map<number, SharedMaster>();
            let spills: CellRange[] = [];
            let spillRow = -1;
            let richCells = 0;
            let firstRich: string | undefined;
            const formats: FormatNotCarried[] = [];
            let columns: ColumnLevels | undefined;
            const rowRuns: SheetDocumentAxisFormat[] = [];
            const rowLevels = new Map<number, FormatParts>();
            let lastRow: { readonly row: number; readonly level: FormatParts } | undefined;
            while (await sheet.read()) {
                this.#signal?.throwIfAborted();
                // The columns come before the cells, complete once the first batch is read.
                columns ??= this.#readColumns(name, sheet.columns, formats);
                // A row is begun in the batch of its first cell or an earlier one; only the last row begun can hold cells of a later batch.
                rowLevels.clear();
                if (lastRow !== undefined) rowLevels.set(lastRow.row, lastRow.level);
                for (const row of sheet.rows) {
                    const level = this.#readRow(name, row, columns, formats);
                    rowLevels.set(row.row, level);
                    lastRow = { row: row.row, level };
                    addRun(rowRuns, row.row, level);
                }
                for (const cell of sheet.cells) {
                    const at = formatCell(cell.row, cell.column);
                    if (cell.isRichText) {
                        richCells++;
                        firstRich ??= at;
                    }
                    if (cell.row !== spillRow) {
                        // Cells come row by row: a Spill Range whose last row lies above this one is behind the reader for good.
                        spillRow = cell.row;
                        if (spills.length > 0) spills = spills.filter(s => s.last.row >= spillRow);
                    }
                    const address = new CellAddress(cell.row, cell.column);
                    const entry = cell.formula !== null
                        ? this.#formulaEntry(name, cell, at, cell.formula, shared, spills)
                        : insideSpill(spills, address) ? undefined : this.#constant(name, cell, at);
                    const rowLevel = rowLevels.get(cell.row);
                    const own = this.#readCell(name, cell, at, columns, rowLevel === undefined ? FormatParts.empty : rowLevel, formats);
                    if (entry !== undefined || !own.isEmpty) {
                        cells.push(new SheetDocumentCell(address, entry, own.numberFormat, own.alignment, own.font, own.fill, own.borders));
                    }
                }
            }
            columns ??= this.#readColumns(name, sheet.columns, formats);
            this.#sheetElements(name, sheet, richCells, firstRich, printNames, formats);
            return SheetDocument.of(this.#culture, name, cells, { columns: columns.runs, rows: rowRuns, columnWidths: widths(sheet.columns) });
        } finally {
            await sheet.close();
        }
    }

    /** The cell format at `style`, refusing the file where a level records a Number Format that cannot cross. */
    #format(sheet: string, style: number, place: string, formats: FormatNotCarried[]): ReadFormat {
        const format = this.#styles.at(style);
        if (format.refusal !== undefined) throw new XlsxOpenError('numberFormat', format.refusal, { sheet, cell: place, held: format.held });
        this.#namesTheme ||= format.namesTheme;
        for (const kind of format.notCarried) {
            if (!formats.some(f => f.kind === kind)) formats.push({ kind, detail: place });
        }
        return format;
    }

    #readColumns(sheet: string, columns: readonly XlsxColumn[], formats: FormatNotCarried[]): ColumnLevels {
        const byColumn = new Map<number, FormatParts>();
        const runs: SheetDocumentAxisFormat[] = [];
        for (const range of byFirst(columns)) {
            if (range.styleIndex === 0) continue;
            const place = `${CellAddress.columnName(range.first)}:${CellAddress.columnName(range.last)}`;
            const level = FormatParts.differing(this.#format(sheet, range.styleIndex, place, formats).format, defaultFormat);
            if (level.isEmpty) continue;
            for (let c = range.first; c <= range.last; c++) byColumn.set(c, level);
            const last = runs.at(-1);
            if (last !== undefined && last.last === range.first - 1 && partsOf(last).equals(level)) {
                runs[runs.length - 1] = axisFormat(last.first, range.last, level);
            } else {
                runs.push(axisFormat(range.first, range.last, level));
            }
        }
        const recorded = new FormatParts(
            runs.some(r => r.numberFormat !== undefined) ? NumberFormat.general : undefined,
            runs.some(r => r.alignment !== undefined) ? 'general' : undefined,
            runs.some(r => r.font !== undefined) ? CellFont.default : undefined,
            runs.some(r => r.fill !== undefined) ? CellFill.none : undefined,
            runs.some(r => r.borders !== undefined) ? CellBorders.none : undefined);
        return { byColumn, runs, recorded };
    }

    /**
     * A formatted row's level: each part that differs from the default, and each part a column records, so that the row
     * stands in place of its columns as in the file.
     */
    #readRow(sheet: string, row: XlsxRow, columns: ColumnLevels, formats: FormatNotCarried[]): FormatParts {
        const number = String(row.row + 1);
        const format = this.#format(sheet, row.styleIndex, `${number}:${number}`, formats).format;
        const differing = FormatParts.differing(format, defaultFormat);
        const recorded = columns.recorded;
        return new FormatParts(
            differing.numberFormat ?? (recorded.numberFormat === undefined ? undefined : format.numberFormat),
            differing.alignment ?? (recorded.alignment === undefined ? undefined : format.alignment),
            differing.font ?? (recorded.font === undefined ? undefined : format.font),
            differing.fill ?? (recorded.fill === undefined ? undefined : format.fill),
            differing.borders ?? (recorded.borders === undefined ? undefined : format.borders));
    }

    /** A cell's own level: the parts of its format that differ from what its row and column give it. In the file a cell's format stands alone. */
    #readCell(sheet: string, cell: XlsxCell, at: string, columns: ColumnLevels, row: FormatParts, formats: FormatNotCarried[]): FormatParts {
        const format = this.#format(sheet, cell.styleIndex, at, formats).format;
        const level = columns.byColumn.get(cell.column);
        const column = level === undefined ? FormatParts.empty : level;
        return FormatParts.differing(format, row.over(column.over(defaultFormat)));
    }

    #formulaEntry(sheet: string, cell: XlsxCell, at: string, formula: XlsxFormula, shared: Map<number, SharedMaster>, spills: CellRange[]): Entry {
        switch (formula.kind) {
            case 'dataTable':
                throw new XlsxOpenError('unreadableFormula', 'The cell is part of a What-If data table, which the engine does not compute.',
                    { sheet, cell: at, held: held(formula.text) });
            case 'array':
                if (!formula.isDynamicArray) {
                    throw new XlsxOpenError('legacyArrayFormula',
                        'The cell holds a legacy array formula (entered with Ctrl+Shift+Enter), which Excel computes as one over its cells and ExSheet would spill instead.',
                        { sheet, cell: at, held: `{${held(formula.text)}}` });
                }
                break;
            case 'shared':
                if (formula.text.length === 0) {
                    const master = formula.sharedIndex === null ? undefined : shared.get(formula.sharedIndex);
                    if (master === undefined) {
                        const index = formula.sharedIndex === null ? '' : String(formula.sharedIndex);
                        throw new XlsxOpenError('unreadableFormula', `The cell takes the shared formula ${index}, whose first cell does not come before it.`, { sheet, cell: at });
                    }
                    const moved = master.entry.movedBy(cell.row - master.row, cell.column - master.column);
                    this.#report(sheet, at, master.unsupported);
                    return moved;
                }
                break;
            case 'normal':
                break;
        }
        const { entry, unsupported } = this.#readFormula(sheet, at, formula.text);
        this.#report(sheet, at, unsupported);
        if (formula.kind === 'shared' && formula.sharedIndex !== null) {
            shared.set(formula.sharedIndex, { entry, row: cell.row, column: cell.column, unsupported });
        }
        if (formula.kind === 'array' && formula.reference !== null) {
            const spill = rangeOf(formula.reference);
            if (spill !== undefined) spills.push(spill);
        }
        return entry;
    }

    /** Reads a Formula's stored text into its Entry, refusing what would read wrongly, with the functions it names that ExSheet does not support. */
    #readFormula(sheet: string, at: string, stored: string): { readonly entry: Entry; readonly unsupported: readonly string[] } {
        const { read: text, problem } = readStoredFormula(stored);
        const where = { sheet, cell: at, held: text };
        switch (problem) {
            case 'otherWorkbook':
                throw new XlsxOpenError('otherWorkbook', 'The Formula reads another workbook, which ExSheet cannot reach.', where);
            case 'excelTable':
                throw new XlsxOpenError('excelTable', 'The Formula reads an Excel Table, which ExSheet does not read.', where);
            case 'implicitIntersection':
                throw new XlsxOpenError('unreadableFormula', 'The Formula uses the implicit intersection operator (@), which the engine does not read.', where);
            case 'spillRange':
                throw new XlsxOpenError('unreadableFormula', 'The Formula reads a Spill Range in a form the engine does not read.', where);
            case 'none':
                break;
        }
        let entry: Entry;
        try {
            entry = defined(Entry.parse(text, Culture.invariant), 'the Entry of a Formula');
        } catch (e) {
            if (e instanceof FormulaSyntaxError) throw new XlsxOpenError('unreadableFormula', `The engine cannot read the Formula: ${e.message}`, where, { cause: e });
            throw e;
        }
        for (const name of entry.unboundNames) {
            if (this.#definedNames.has(foldCase(name))) {
                throw new XlsxOpenError('definedName', `The Formula reads the defined name '${name}', which ExSheet does not define.`, where);
            }
            if (this.#hasExcelTables) {
                throw new XlsxOpenError('excelTable',
                    `The Formula reads the name '${name}', which the workbook does not define; it holds Excel Tables, which a name can read, and ExSheet does not read them.`,
                    where);
            }
        }
        const functions = entry.undeclaredFunctions;
        const lambda = functions.find(f => this.#definedNames.has(foldCase(f)));
        if (lambda !== undefined) throw new XlsxOpenError('definedName', `The Formula calls the defined name '${lambda}', which ExSheet does not define.`, where);
        return { entry, unsupported: functions };
    }

    #report(sheet: string, cell: string, functions: readonly string[]): void {
        if (functions.length > 0) this.#unsupported.push({ sheet, cell, functions });
    }

    /** A constant's Entry, or `undefined` for a cell that holds none (a cell with a style only). */
    #constant(sheet: string, cell: XlsxCell, at: string): Entry | undefined {
        const value = cell.value;
        switch (cell.type) {
            case 'sharedString':
            case 'inlineString':
                // A string cell with no text is the empty text, as the file layer and ExSheet.Xlsx read it.
                return Entry.fromValue(Value.fromText(cell.text === null ? '' : cell.text));
            case 'formulaString':
                return value === null ? undefined : Entry.fromValue(Value.fromText(value));
            case 'number': {
                if (value === null) return undefined;
                let number = parseDouble(value);
                if (number === undefined || !Number.isFinite(number)) {
                    throw new XlsxOpenError('unreadableConstant', 'The cell\'s number cannot be read.', { sheet, cell: at, held: value });
                }
                if (this.#reader.date1904 && this.#showsDate(cell.styleIndex)) number += days1904;
                return Entry.fromValue(Value.fromNumber(number));
            }
            case 'boolean':
                switch (value) {
                    case '1':
                    case 'true':
                        return Entry.fromValue(Value.fromBoolean(true));
                    case '0':
                    case 'false':
                        return Entry.fromValue(Value.fromBoolean(false));
                    case null:
                        return undefined;
                    default:
                        throw new XlsxOpenError('unreadableConstant', 'The cell\'s boolean cannot be read.', { sheet, cell: at, held: value });
                }
            case 'error': {
                if (value === null) return undefined;
                const error = Entry.fromErrorText(value);
                if (error !== undefined) return error;
                throw new XlsxOpenError('unreadableConstant', 'The cell holds an Error Value a cell of ExSheet cannot hold.', { sheet, cell: at, held: value });
            }
            case 'date': {
                if (value === null) return undefined;
                const serial = isoDateSerial(value);
                if (serial === undefined) {
                    throw new XlsxOpenError('unreadableConstant',
                        'The cell\'s date is not an ISO 8601 date or time, or lies outside the dates a serial counts.', { sheet, cell: at, held: value });
                }
                return Entry.fromValue(Value.fromNumber(serial));
            }
            default:
                return unreachable(cell.type);
        }
    }

    /** Whether the cell format at `style` shows a date (its serial moves from 1904's to 1900's). */
    #showsDate(style: number): boolean {
        const known = this.#dateStyles.get(style);
        if (known !== undefined) return known;
        const styles = this.#reader.styles;
        const id = elementAt(styles.cellFormats, style).numberFormatId;
        const shows = showsDate(id, styles.numberFormats.get(id));
        this.#dateStyles.set(style, shows);
        return shows;
    }

    /** What else a worksheet held, as the file layer reports it, said once per Sheet and kind. */
    #sheetElements(sheet: string, read: XlsxSheetReader, richCells: number, firstRich: string | undefined, printNames: boolean, formats: readonly FormatNotCarried[]): void {
        const kinds: { readonly kind: NotCarriedKind; readonly detail: string | undefined }[] = [];
        const add = (kind: NotCarriedKind, detail?: string): void => {
            if (!kinds.some(k => k.kind === kind && k.detail === detail)) kinds.push({ kind, detail });
        };
        if (read.customRowHeights) add('rowHeights');
        if (read.hiddenRows) add('hiddenRows');
        if (read.columns.some(c => c.hidden || (c.width !== null && c.width <= 0))) add('hiddenColumns');
        if (read.frozenPanes) add('frozenPanes');
        for (const element of read.otherElements) {
            for (const { kind, detail } of elementNotCarried(element)) add(kind, detail);
        }
        if (printNames) add('printSettings');
        if (richCells > 0) add('richText', firstRich);
        for (const { kind, detail } of formats) add(kind, detail);
        for (const { kind, detail } of kinds) {
            this.#notCarried.push({ kind, sheet, detail: detail === undefined ? null : detail, description: describe(kind, sheet, detail, richCells) });
        }
    }
}

/**
 * The name a defined name is listed under, or `undefined` when it is not listed: `_xlnm._FilterDatabase` is the range an
 * AutoFilter keeps, which the AutoFilter's own entry says. A print area or titles is a print setting of its Sheet's.
 */
function builtInName(name: XlsxDefinedName): string | undefined {
    return equalsIgnoreCase(name.name, filterDatabase) ? undefined : name.name;
}

function describeName(name: XlsxDefinedName, shown: string, sheet: string | null): string {
    return sheet === null
        ? `The defined name '${shown}' (${name.text}) is not carried; no Formula carried reads it.`
        : `The defined name '${shown}' of the sheet '${sheet}' (${name.text}) is not carried; no Formula carried reads it.`;
}

/** The column ranges in the order of their first column, as LINQ's stable `OrderBy` gives them. */
function byFirst(columns: readonly XlsxColumn[]): XlsxColumn[] {
    return columns.toSorted((a, b) => a.first - b.first);
}

function axisFormat(first: number, last: number, level: FormatParts): SheetDocumentAxisFormat {
    return new SheetDocumentAxisFormat(first, last, level.numberFormat, level.alignment, level.font, level.fill, level.borders);
}

/** Adds a formatted row to the runs, joining the run before it when it is the next row, formatted alike. */
function addRun(runs: SheetDocumentAxisFormat[], row: number, level: FormatParts): void {
    if (level.isEmpty) return;
    const last = runs.at(-1);
    if (last !== undefined && last.last === row - 1 && partsOf(last).equals(level)) {
        runs[runs.length - 1] = axisFormat(last.first, row, level);
    } else {
        runs.push(axisFormat(row, row, level));
    }
}

/** The columns' widths in the engine's unit, a run wherever adjacent columns share one; a width fitted to an entry (`bestFit`) is one an entry widened. */
function widths(columns: readonly XlsxColumn[]): SheetDocumentColumnWidth[] {
    const runs: SheetDocumentColumnWidth[] = [];
    for (const range of byFirst(columns)) {
        if (range.width === null) continue;
        const characters = readWidth(range.width);
        if (characters === undefined) continue;
        const kind = range.bestFit ? 'widenedByEntry' : 'setByUser';
        const last = runs.at(-1);
        if (last !== undefined && last.last === range.first - 1 && last.width === characters && last.kind === kind) {
            runs[runs.length - 1] = new SheetDocumentColumnWidth(last.first, range.last, characters, kind);
        } else {
            runs.push(new SheetDocumentColumnWidth(range.first, range.last, characters, kind));
        }
    }
    return runs;
}

function held(stored: string): string {
    return `=${stripPrefixes(stored)}`;
}

/**
 * Whether a cell lies in a dynamic array's Spill Range, whose cells hold the Values Excel stored, not constants. The list
 * holds only the ranges not yet behind the reader, pruned as each row begins.
 */
function insideSpill(spills: readonly CellRange[], cell: CellAddress): boolean {
    return spills.some(s => s.contains(cell));
}

/** A range written `A1:B3` or `A1`, or `undefined` when it is not one. */
function rangeOf(text: string): CellRange | undefined {
    const colon = text.indexOf(':');
    const first = parseCell(colon < 0 ? text : text.slice(0, colon));
    const last = parseCell(colon < 0 ? text : text.slice(colon + 1));
    if (first === undefined || last === undefined) return undefined;
    return new CellRange(new CellAddress(first.row, first.column), new CellAddress(last.row, last.column));
}

function describe(kind: NotCarriedKind, sheet: string, detail: string | undefined, richCells: number): string {
    const at = detail === undefined ? '' : detail;
    switch (kind) {
        case 'rowHeights': return `Rows of '${sheet}' have heights of their own, which are not carried.`;
        case 'hiddenRows': return `Rows of '${sheet}' are hidden in the file, and open shown.`;
        case 'hiddenColumns': return `Columns of '${sheet}' are hidden in the file, and open shown.`;
        case 'frozenPanes': return `'${sheet}' has frozen panes, which are not carried.`;
        case 'mergedCells': return `'${sheet}' has merged cells; each cell is carried on its own.`;
        case 'conditionalFormatting': return `'${sheet}' has conditional formatting, which is not carried.`;
        case 'dataValidation': return `'${sheet}' has data validation, which is not carried.`;
        case 'notesAndComments': return `'${sheet}' has notes or comments, which are not carried.`;
        case 'hyperlinks': return `'${sheet}' has hyperlinks; their cells' text is carried, the links are not.`;
        case 'picturesAndCharts': return `'${sheet}' has pictures, charts, shapes or controls, which are not carried.`;
        case 'protection': return `'${sheet}' is protected in the file; the protection is not carried.`;
        case 'printSettings':
            return `'${sheet}' has print settings beyond Excel's defaults (margins, a header or footer, page breaks, a print area, a scale or the like), which are not carried.`;
        case 'view':
            return `'${sheet}' has view settings (hidden gridlines, headings or zeros, Formulas shown, right to left, a zoom or a page layout), which are not carried.`;
        case 'autoFilter': return `'${sheet}' has an AutoFilter, which is not carried; every row opens shown.`;
        case 'excelTable': return `'${sheet}' has Excel Tables; their cells are carried, the Tables are not.`;
        case 'slicers': return `'${sheet}' has slicers or timelines, which are not carried.`;
        case 'sheetProperties': return `The tab colour of '${sheet}' is not carried.`;
        case 'richText':
            return `${String(richCells)} cell(s) of '${sheet}', the first ${at}, hold rich text: the text is carried, the runs' own formatting is not.`;
        case 'defaultColumnWidth':
            return `'${sheet}' has a default column width of its own, which is not carried; its columns without a width of their own open at the default width.`;
        case 'fontNamesAndSizes':
            return `Cells of '${sheet}', the first at ${at}, use a typeface or a size other than the workbook's own, which is not carried; they open in the Sheet's font.`;
        case 'verticalAlignment': return `Cells of '${sheet}', the first at ${at}, are aligned vertically, which is not carried.`;
        case 'textLayout': return `Cells of '${sheet}', the first at ${at}, wrap, indent, rotate or shrink their text, which is not carried.`;
        case 'horizontalAlignment':
            return `Cells of '${sheet}', the first at ${at}, are aligned to fill, justify, distribute or centre across the selection, which is not carried; they open aligned as General.`;
        case 'underline': return `Cells of '${sheet}', the first at ${at}, are underlined twice or for accounting; they open with a single underline.`;
        case 'patternFills':
            return `Cells of '${sheet}', the first at ${at}, have a pattern or gradient fill, or a solid fill of no colour, which is not carried; they open with no Fill.`;
        case 'diagonalBorders': return `Cells of '${sheet}', the first at ${at}, have diagonal borders, which are not carried; their four sides are.`;
        // Said of the workbook, never of a Sheet's elements; an element the file layer names is said by its name.
        case 'chartSheet':
        case 'dataConnections':
        case 'definedName':
        case 'externalLinks':
        case 'hiddenSheet':
        case 'other':
        case 'pivotTables':
        case 'themeColours':
        default:
            return `'${sheet}' holds <${at}>, which is not carried.`;
    }
}
