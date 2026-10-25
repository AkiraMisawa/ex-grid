// The grid of cells ExSheet holds (C#'s Sheet.cs and its partials; CONTEXT.md): Excel's extent, held sparsely, each
// cell holding an Entry, and a Value computed from it. After every change the Sheet recalculates only what depends
// on the change, and publishes the new Values only when that recalculation has completed (ADR-0047). Its declared
// culture decides how typed constants are read and how Values are displayed (ADR-0048). One class here holds what
// C#'s partial class spreads over its files, in their order: the cells and the dependency graph, the state an undo
// compares, Cell Formats, recalculation, spills, the volatile functions, the Sheet Day and the draws, Linked
// Tables, the name, the format a Formula gives on entry, column widths, insertion and deletion, steps, the
// clipboard and the fill.
import { CellAddress, columnCount, rowCount } from './cell-address';
import { BorderLine, CellBorders, CellFill, CellFont, CellFormat, type CellDisplay, type HorizontalAlignment, type RangeEdgeLines } from './cell-format';
import { AxisFormat, CellFormatChange, CellState, defaultCellFormat, own, PlaceInRange, samePart } from './cell-format-change';
import { CellRange } from './cell-range';
import { isPlainNumber, parseConstantWithFormat } from './constant-parser';
import { Culture } from './culture';
import { equalsIgnoreCase, formatExponential, formatRoundTrip, ignoreCaseKey, isDigit, toUpperInvariant, trimWhiteSpace } from './dotnet';
import { Entry } from './entry';
import { entryText } from './entry-text';
import { completeFormula, formulaReferences, type FormulaCompletion, type FormulaReference, type LinkedTableColumn } from './formula-entry';
import { FormulaSyntaxError } from './formula-syntax-error';
import { atFifteenDigits } from './formulas/arithmetic';
import type { CellReader, SheetDay, SheetMoment } from './formulas/cell-reader';
import { drawOf } from './formulas/draws';
import { Evaluator, isMulti } from './formulas/evaluator';
import { rewriteStructuredReferences } from './formulas/formula-text';
import { knownShape } from './formulas/known-shape';
import type { Node } from './formulas/nodes';
import { Operand } from './formulas/operand';
import { Area, type Reference } from './formulas/reference';
import { StructuralEdit } from './formulas/structural-edit';
import { textEqualityKey } from './formulas/text-order';
import { ValueArray } from './formulas/value-array';
import { defined, unreachable } from './must';
import { abbreviatedMonthNamesOf, NumberFormat } from './number-format';
import { generalFitted, generalLimit, isGeneralScientific } from './number-text';
import { SheetCell, SheetCellMap, SheetCellSet } from './sheet-cell';
import { distinctAddresses, distinctNumbers, RepeatedKeyError, SheetChange, SheetColumnWidth, SheetRefusal, SheetRefusedError } from './sheet-change';
import { SheetDocumentAxisFormat, SheetDocumentCell, SheetDocumentColumnWidth, SheetDocumentTable } from './sheet-document-parts';
import type { SheetEdit } from './sheet-edit';
import type { SheetDocument } from './sheet-document';
import { Value, type ErrorValue, type ValueKind } from './value';
import type { SheetRename, Workbook } from './workbook';

/** A typed text for a cell. */
export type TypedCell = readonly [CellAddress, string];

/** An Entry set on a cell, or cleared (`undefined`). */
export type EntryOfCell = readonly [CellAddress, Entry | undefined];

/** A cell's address with its state. @internal */
export interface AddressedState {
    readonly address: CellAddress;
    readonly state: CellState;
}

/** A Formula rewritten on a Sheet, as it was before. @internal */
export interface RewrittenEntry {
    readonly sheet: Sheet;
    readonly address: CellAddress;
    readonly entry: Entry;
}

/** Which way a fill extends its source (ADR-0050): along one axis, away from the source. */
export type FillDirection = 'down' | 'right' | 'up' | 'left';

/** A Linked Table as a Sheet knows it (CONTEXT.md, ADR-0049): its name, its columns, its key, and whether its data has arrived. */
export class LinkedTable {
    /** The name Formulas use, as declared; matched without regard to case. */
    readonly name: string;

    /** The column names, in order, as declared; matched without regard to case. */
    readonly columns: readonly string[];

    /** Whether no snapshot is held: none has arrived yet, or the last was refused because a key repeated in it. */
    readonly isWaiting: boolean;

    /** How many rows the current snapshot holds; 0 while waiting. */
    readonly rowCount: number;

    /** The key's columns, as declared there: one, several, or none for a table declared without a key (ADR-0058). */
    readonly keyColumns: readonly string[];

    constructor(
        name: string,
        columns: readonly string[],
        isWaiting: boolean,
        rows: number,
        keyColumns: readonly string[] = [],
    ) {
        this.name = name;
        this.columns = columns;
        this.isWaiting = isWaiting;
        this.rowCount = rows;
        this.keyColumns = keyColumns;
    }

    /** The key column when the key is one column; `undefined` for none, and for a key of several columns. */
    get key(): string | undefined {
        return this.keyColumns.length === 1 ? this.keyColumns[0] : undefined;
    }
}

/** A Linked Table's state, which every Sheet of the Workbook reads (ADR-0331). @internal */
export class TableState {
    /** The current snapshot, column by column; `undefined` until the first arrives, and after one is refused. */
    data: readonly (readonly (Value | undefined)[])[] | undefined;
    /** For each column of `data`, whether it holds a refusal. */
    refusing: readonly boolean[] = [];
    rowCount = 0;

    readonly name: string;

    readonly columns: readonly string[];

    /** Each column's index, by its name read without regard to case. */
    readonly columnIndex: ReadonlyMap<string, number>;

    /** The key's columns, as `columns` spells them; empty for none. */
    readonly key: readonly string[];

    readonly order: number;

    constructor(
        name: string,
        columns: readonly string[],
        columnIndex: ReadonlyMap<string, number>,
        key: readonly string[],
        order: number,
    ) {
        this.name = name;
        this.columns = columns;
        this.columnIndex = columnIndex;
        this.key = key;
        this.order = order;
    }
}

/** One cell the Sheet holds. @internal */
export class Cell {
    entry: Entry | undefined;
    value: Value | undefined;
    /** The cell's own Number Format; `undefined` when it takes its row's or column's (ADR-0047). */
    numberFormat: NumberFormat | undefined;
    alignment: HorizontalAlignment | undefined;
    font: CellFont | undefined;
    fill: CellFill | undefined;
    borders: CellBorders | undefined;
    /** For an Anchor, the array its Formula computed (ADR-0125); `undefined` for a result of one Value. */
    array: ValueArray | undefined;
    /** For an Anchor: whether the last layout found its Spill Range in the way, so that it shows `#SPILL!`. */
    spillBlocked = false;
    /** For a cell of a Spill Range, the Anchor whose array gives its Value; it holds no Entry of its own. */
    spilledFrom: CellAddress | undefined;

    readonly address: CellAddress;

    constructor(address: CellAddress) {
        this.address = address;
    }

    /** Whether the cell records any part of a Cell Format of its own. */
    get isFormatted(): boolean {
        return this.numberFormat !== undefined || this.alignment !== undefined || this.font !== undefined || this.fill !== undefined || this.borders !== undefined;
    }

    get isEmpty(): boolean {
        return this.entry === undefined && !this.isFormatted && this.spilledFrom === undefined;
    }

    /** What the cell shows over `inherited`: each part its own, else the level's. */
    over(inherited: CellFormat): CellFormat {
        return new CellFormat(this.numberFormat ?? inherited.numberFormat, this.alignment ?? inherited.alignment, this.font ?? inherited.font,
            this.fill ?? inherited.fill, this.borders ?? inherited.borders);
    }

    /** Records the Cell Format `other` records of its own, and nothing else of it. */
    takeFormatOf(other: Cell): void {
        this.numberFormat = other.numberFormat;
        this.alignment = other.alignment;
        this.font = other.font;
        this.fill = other.fill;
        this.borders = other.borders;
    }

    /** Records each part `change` sets as `shown` has it, and nothing where `inherited` gives the same; whether anything changed. */
    take(change: CellFormatChange, shown: CellFormat, inherited: CellFormat): boolean {
        const was = new CellState(undefined, undefined, this.numberFormat, this.alignment, this.font, this.fill, this.borders);
        if (change.numberFormat !== undefined) this.numberFormat = own(shown.numberFormat, inherited.numberFormat);
        if (change.alignment !== undefined) this.alignment = own(shown.alignment, inherited.alignment);
        if (change.setsFont) this.font = own(shown.font, inherited.font);
        if (change.fill !== undefined) this.fill = own(shown.fill, inherited.fill);
        if (change.setsBorders) this.borders = own(shown.borders, inherited.borders);
        return !was.sameFormat(this);
    }
}

/** What a formatting command did, and what undoing it puts back. @internal */
export interface CellFormatOutcome {
    readonly change: SheetChange;
    readonly before: readonly AddressedState[];
    readonly rowsBefore: ReadonlyMap<number, AxisFormat>;
    readonly columnsBefore: ReadonlyMap<number, AxisFormat>;
}

/** What a change set on several ranges did: each range's outcome, in the order they were set. @internal */
export interface CellFormatsOutcome {
    readonly change: SheetChange;
    readonly parts: readonly CellFormatOutcome[];
}

/** What a structural edit did, and what undoing it needs besides the inverse edit. @internal */
export interface StructuralOutcome {
    readonly change: SheetChange;
    readonly dropped: readonly AddressedState[];
    readonly rewritten: readonly { readonly address: CellAddress; readonly entry: Entry }[];
    readonly rowsBefore: ReadonlyMap<number, AxisFormat>;
    readonly columnsBefore: ReadonlyMap<number, AxisFormat>;
    readonly widthsBefore: ReadonlyMap<number, SheetColumnWidth>;
    readonly rewrittenElsewhere: readonly RewrittenEntry[];
}

/** The formats a fill from whole columns (`columns`) or whole rows writes on its target's columns (rows), by index. @internal */
export interface FillLevels {
    readonly columns: boolean;
    readonly levels: readonly { readonly index: number; readonly level: AxisFormat }[];
}

/** What a fill writes, or why it is refused, and then nothing. @internal */
export interface FillPlan {
    readonly refusal: SheetRefusal | undefined;
    readonly states: readonly AddressedState[];
    readonly levels: FillLevels | undefined;
}

/**
 * One operation a Sheet has done, as one step of ExSheet's one undo stack (ADR-0048): undoing it restores every
 * Entry, format and Reference it changed exactly, with one recalculation; redoing it does it again. Steps are
 * undone in the reverse of the order they were done, and redone in the order they were undone.
 */
export class SheetStep {
    readonly #undo: (sheet: Sheet) => SheetChange;
    readonly #redo: (sheet: Sheet) => SheetChange;
    #isUndone = false;

/** The Sheet the operation was done on. */
    readonly sheet: Sheet;

/** What doing the operation changed. */
    readonly change: SheetChange;

    /** @internal */
    constructor(sheet: Sheet, change: SheetChange, undo: (sheet: Sheet) => SheetChange, redo: (sheet: Sheet) => SheetChange) {
        this.sheet = sheet;
        this.change = change;
        this.#undo = undo;
        this.#redo = redo;
    }

    /** Whether the step is currently undone. */
    get isUndone(): boolean {
        return this.#isUndone;
    }

    /** Undoes the operation, and says what that changed. Throws when the step is already undone. */
    undo(): SheetChange {
        if (this.#isUndone) throw new Error('The step is already undone.');
        // An undo is one change, however many steps it takes (ADR-0180).
        const change = this.sheet.asOneChange(() => this.#undo(this.sheet));
        this.#isUndone = true;
        return change;
    }

    /** Does the operation again after `undo`, and says what that changed. Throws when the step is not undone. */
    redo(): SheetChange {
        if (!this.#isUndone) throw new Error('Only an undone step can be redone.');
        const change = this.sheet.asOneChange(() => this.#redo(this.sheet));
        this.#isUndone = false;
        return change;
    }
}

/**
 * A rectangle of cells copied inside ExSheet: each cell's Entry and the Cell Format it showed, as they were at the
 * copy, and where they came from (ADR-0048, ADR-0071). Pasting it writes the Entries with their relative References
 * shifted by the distance pasted, as Excel does, and the Cell Formats with them. Immutable.
 */
export class SheetBlock {
    readonly #cells: readonly CellState[];

/** The range the block was copied from. */
    readonly source: CellRange;

    /** @internal */
    constructor(source: CellRange, cells: readonly CellState[]) {
        this.source = source;
        this.#cells = cells;
    }

/** How many rows the block holds. */
    get rowCount(): number {
        return this.source.rowCount;
    }

/** How many columns the block holds. */
    get columnCount(): number {
        return this.source.columnCount;
    }

    /** The Entry at a position inside the block, as copied; `undefined` for a blank cell. */
    entryAt(row: number, column: number): Entry | undefined {
        return this.at(row, column).entry;
    }

    /** The Cell Format at a position inside the block: what the cell showed at the copy, its Borders its own four sides. */
    cellFormatAt(row: number, column: number): CellFormat {
        return this.at(row, column).cellFormat;
    }

    /** The cell at a position inside the block, as copied: its Entry and the Cell Format it showed. @internal */
    at(row: number, column: number): CellState {
        if (!Number.isInteger(row) || row < 0 || row >= this.rowCount) throw new RangeError(`The block has ${String(this.rowCount)} row(s).`);
        if (!Number.isInteger(column) || column < 0 || column >= this.columnCount) throw new RangeError(`The block has ${String(this.columnCount)} column(s).`);
        return defined(this.#cells[row * this.columnCount + column], 'a cell of the block');
    }
}

/**
 * A copy of a rectangle of cells, or its refusal (ADR-0048, ADR-0049): the Entries for a paste inside ExSheet, and
 * the Values for anywhere else in the two flavours ExGrid puts on the clipboard (ADR-0005).
 */
export class SheetCopy {
    /** Why the copy is refused — a cell in it is `#GETTING_DATA` (ADR-0049) — or `undefined`. A refused copy carries nothing. */
    readonly refusal: SheetRefusal | undefined;

    /** The Entries, for a paste inside ExSheet. */
    readonly block: SheetBlock | undefined;

    /** The `text/plain` flavour: each Value as the cell shows it, tab-separated, one line per row ending in CR LF. */
    readonly text: string | undefined;

    /** The `text/html` flavour: a table of the unformatted Values, invariant, in Excel's markup. */
    readonly html: string | undefined;

    /** @internal */
    constructor(
        refusal: SheetRefusal | undefined,
        block: SheetBlock | undefined,
        text: string | undefined,
        html: string | undefined,
    ) {
        this.refusal = refusal;
        this.block = block;
        this.text = text;
        this.html = html;
    }

/** Whether the copy is refused (`refusal`). */
    get isRefused(): boolean {
        return this.refusal !== undefined;
    }
}

/** The functions whose answer can change while no cell their Formula names changes (ADR-0124). */
const volatileFunctions = new Set(['NOW', 'OFFSET', 'INDIRECT', 'RAND', 'RANDBETWEEN']);

/** The most passes one recalculation makes before it stops, for spills that keep moving each other (ADR-0125). */
const mostPasses = 64;

/** The namespace Excel's own HTML declares for its `x:` attributes. */
const excelNamespace = 'xmlns:x="urn:schemas-microsoft-com:office:excel"';

/** The format a number constant with `%` gives a Formula's result beside an unformatted operand: `0.0%`, as Excel was observed to give `=10+50%`. */
const percentConstant = NumberFormat.parse('0.0%');

const tableNamePattern = /^[A-Za-z_\\][A-Za-z0-9_.]{0,254}$/u;
const rowColumnPattern = /^[Rr][0-9]*[Cc][0-9]*$|^[RrCc]$/u;

let nextSheetId = 0;

/** What a recalculation's reads have met for the Formula being computed, whichever Sheet they read. */
interface ReadState {
    /** `#CIRC!` for a cell in a cycle or still being computed; else `#GETTING_DATA` for a waiting cell; else `undefined`. */
    met: ErrorValue | undefined;
    /** Whether a read has met a refusal while the Formula being computed was evaluated. */
    metRefusal: boolean;
}

/** The cells of one Sheet as one recalculation reads them. */
class StagedReader implements CellReader {
    readonly sheet: Sheet;

    readonly staged: Map<number, Value | undefined>;

    readonly onDemand: (address: CellAddress) => Value | undefined;

    readonly arrays: Map<number, ValueArray | undefined>;

    readonly reads: ReadState;

    readonly other: (name: string) => StagedReader | undefined;

    readonly span: (first: string, last: string) => readonly CellReader[] | undefined;

    constructor(
        sheet: Sheet,
        staged: Map<number, Value | undefined>,
        onDemand: (address: CellAddress) => Value | undefined,
        arrays: Map<number, ValueArray | undefined>,
        reads: ReadState,
        other: (name: string) => StagedReader | undefined,
        span: (first: string, last: string) => readonly CellReader[] | undefined,
    ) {
        this.sheet = sheet;
        this.staged = staged;
        this.onDemand = onDemand;
        this.arrays = arrays;
        this.reads = reads;
        this.other = other;
        this.span = span;
    }

    /** A cell as this recalculation leaves it: staged, or computed now when it is still to be (ADR-0124), or as it stood. */
    read(address: CellAddress): Value | undefined {
        const key = address.key;
        const value = this.staged.has(key) ? this.staged.get(key) : this.onDemand(address) ?? this.sheet.getValue(address);
        if (value !== undefined && value.isError && (value.error === '#CIRC!' || value.error === '#GETTING_DATA') && this.reads.met !== '#CIRC!') {
            this.reads.met = value.error;
        }
        if (value?.isRefused === true) this.reads.metRefusal = true;
        return value;
    }

    nonBlankIn(area: Area): Iterable<CellAddress> {
        return [...this.sheet.cellsIn(area)].filter(a => this.read(a) !== undefined).toSorted((a, b) => a.compareTo(b));
    }

    /** A Linked Table's column, which waits as a named one does (ADR-0049, ADR-0124). */
    tableColumn(table: string, column: string): Operand {
        const read = this.sheet.tableColumn(table, column);
        if (read.isError && read.scalar?.error === '#GETTING_DATA' && this.reads.met !== '#CIRC!') this.reads.met = '#GETTING_DATA';
        if (this.sheet.columnHoldsRefusal(table, column)) this.reads.metRefusal = true;
        return read;
    }

    isLinkedTable(name: string): boolean {
        return this.sheet.workbook.tables.has(ignoreCaseKey(name));
    }

    formulaAt(address: CellAddress): Node | undefined {
        return this.sheet.formulaShownAt(address);
    }

    resolve(sheetName: string | undefined): CellReader | undefined {
        return this.sheet.isThisSheet(sheetName) ? this : this.other(defined(sheetName, 'a qualifier'));
    }

    resolveSpan(first: string, last: string): readonly CellReader[] | undefined {
        return this.span(first, last);
    }

    get today(): SheetDay | undefined {
        return this.sheet.workbook.today;
    }

    get now(): SheetMoment | undefined {
        return this.sheet.moment;
    }

    draw(cell: CellAddress, place: number): bigint | undefined {
        return this.sheet.draw(cell, place);
    }

    /** The Anchor's array as this pass computed it, or as it stood; `#REF!` when it does not spill (ADR-0125). */
    spill(anchor: CellAddress): Operand {
        this.read(anchor);
        const cell = this.sheet.cellAt(anchor);
        let array: ValueArray | undefined;
        if (this.arrays.has(anchor.key)) array = this.arrays.get(anchor.key);
        else if (cell !== undefined) array = cell.array;
        if (array === undefined || cell === undefined || cell.spillBlocked) return Operand.ofError('#REF!');
        const shown = new ValueArray(array.rows, array.columns);
        for (let r = 0; r < array.rows; r++) {
            for (let c = 0; c < array.columns; c++) shown.set(r, c, array.at(r, c) ?? Value.fromNumber(0));
        }
        return Operand.ofSpillRange(shown);
    }
}

/**
 * The grid of cells ExSheet holds: Excel's extent, held sparsely. Not shared between threads of work: one Sheet is
 * changed and read by one caller at a time. A Sheet belongs to a Workbook (ADR-0331); change it through its
 * Workbook's `do`.
 */
export class Sheet {
    /** Excel's row count: 1,048,576 (ADR-0046). */
    static readonly rowCount = rowCount;

    /** Excel's column count: 16,384, `A` to `XFD` (ADR-0046). */
    static readonly columnCount = columnCount;

    /** The name a new Sheet has, as Excel's first sheet does (ADR-0046). */
    static readonly defaultName = 'Sheet1';

    /** The longest name a Sheet can have: 31 characters, as in Excel. */
    static readonly maximumNameLength = 31;

    /** The widest a column can be, in characters: 255, Excel's limit. */
    static readonly maxColumnWidth = 255;

    /** Excel's default column width, in characters: 8.43 (ADR-0047). */
    static readonly defaultColumnWidth = 8.43;

    /** A number unique to this Sheet in this process, which keys its cells across a Workbook. @internal */
    readonly id = nextSheetId++;

    /** The Workbook the Sheet belongs to: one of its own, of this Sheet alone, for a Sheet created on its own (ADR-0331). */
    readonly workbook: Workbook;

    /** The Sheet's declared culture (ADR-0048). Formulas are in invariant syntax whatever it is (ADR-0047). */
    readonly culture: Culture;

    #name: string;

    readonly #cells = new Map<number, Cell>();

    /** For each cell, the Formula cells that name it by a single-cell Reference. */
    readonly #cellDependents = new Map<number, Set<number>>();

    /** For each Formula cell that reads a multi-cell rectangle, the rectangles. */
    readonly #areaPrecedents = new Map<number, Area[]>();

    /** For each Formula cell that names another Sheet's cells, those References (ADR-0331). */
    readonly #remoteReferences = new Map<number, Reference[]>();

    /** For each cell, the Formula cells on other Sheets that name it by a single-cell Reference (ADR-0331). */
    readonly #remoteCellDependents = new Map<number, SheetCellSet>();

    /** For each Formula cell on another Sheet that reads a multi-cell rectangle of this Sheet, the rectangles (ADR-0331). */
    readonly #remoteAreaDependents = new SheetCellMap<Area[]>();

    /** The Cell Formats recorded on whole rows, by row; a row absent records nothing (ADR-0047). */
    #rowFormats = new Map<number, AxisFormat>();

    /** The Cell Formats recorded on whole columns, by column; a column absent records nothing (ADR-0047). */
    #columnFormats = new Map<number, AxisFormat>();

    /** The widths recorded on columns, and the kind of each, by column (ADR-0046). */
    #columnWidths = new Map<number, SheetColumnWidth>();

    /** The Formula cells that call a volatile function. */
    readonly #volatileFormulas = new Set<number>();

    /** The moment read at the start of the recalculation under way. */
    #moment: SheetMoment | undefined;

    /** The Formula cells that call `TODAY`, recalculated when the Sheet Day changes. */
    readonly #todayReaders = new Set<number>();

    /** The Formula cells that call `RAND` or `RANDBETWEEN`. */
    readonly #randomReaders = new Set<number>();

    /** For each table name, declared or not, read without regard to case, the Formula cells that name it. */
    readonly #tableReaders = new Map<string, Set<number>>();

    #cultureListNames: Set<string> | undefined;

    /** How many passes the last recalculation made (ADR-0124). Layer 1 reads it. @internal */
    recalculationPasses = 0;

    /** A Sheet named `name` in `workbook`, under its culture (ADR-0331). Throws for a name a Sheet cannot have. @internal */
    constructor(workbook: Workbook, name: string) {
        this.#name = Sheet.checkName(name);
        this.workbook = workbook;
        this.culture = workbook.culture;
    }

    // ---- The cells and the dependency graph (Sheet.cs) ----

    /** Records what a document records on this Sheet's columns, rows and cells besides Entries. @internal */
    openFormats(document: SheetDocument): void {
        for (const run of document.columns) {
            for (let column = run.first; column <= run.last; column++) this.#columnFormats.set(column, this.#opened(run.level));
        }
        for (const run of document.columnWidths) {
            for (let column = run.first; column <= run.last; column++) this.#columnWidths.set(column, new SheetColumnWidth(run.width, run.kind));
        }
        for (const run of document.rows) {
            for (let row = run.first; row <= run.last; row++) this.#rowFormats.set(row, this.#opened(run.level));
        }
        for (const cell of document.cells) {
            if (cell.numberFormat === undefined && cell.alignment === undefined && cell.font === undefined && cell.fill === undefined && cell.borders === undefined) continue;
            const held = new Cell(cell.address);
            held.numberFormat = cell.numberFormat?.openedIn(this.culture);
            held.alignment = cell.alignment;
            held.font = cell.font;
            held.fill = cell.fill;
            held.borders = cell.borders;
            this.#cells.set(cell.address.key, held);
        }
    }

    /** Takes a document's Entries, computing every Value again. Opening is no change (ADR-0180). @internal */
    openEntries(document: SheetDocument): void {
        this.setEntries(document.cells.filter(c => c.entry !== undefined).map(c => [c.address, c.entry] as const));
        this.workbook.forgetChanges();
    }

    #opened(level: AxisFormat): AxisFormat {
        return level.numberFormat === undefined ? level : level.replacing({ numberFormat: level.numberFormat.openedIn(this.culture) });
    }

    /** The Sheet as a Sheet Document declaring `tables` (ADR-0046, ADR-0047, ADR-0048, ADR-0049, ADR-0071). @internal */
    toDocumentParts(): {
        cells: SheetDocumentCell[]; columns: SheetDocumentAxisFormat[]; rows: SheetDocumentAxisFormat[]; columnWidths: SheetDocumentColumnWidth[];
    } {
        const cells = [...this.#cells.values()]
            .filter(c => c.entry !== undefined || c.isFormatted)
            .toSorted((a, b) => a.address.compareTo(b.address))
            .map(c => new SheetDocumentCell(c.address, c.entry, c.numberFormat, c.alignment, c.font, c.fill, c.borders));
        return { cells, columns: runs(this.#columnFormats), rows: runs(this.#rowFormats), columnWidths: this.#widthRuns() };
    }

    /** The addresses of every cell holding an Entry, in row-major order. */
    get entryAddresses(): CellAddress[] {
        return [...this.#cells.values()].filter(c => c.entry !== undefined).map(c => c.address).toSorted((a, b) => a.compareTo(b));
    }

    /**
     * The text `number` would show in the cell at `address`, under the cell's Number Format and the Sheet's culture
     * (ADR-0130). `undefined` where the format cannot show it.
     */
    formatAs(address: CellAddress, number: number): string | undefined {
        if (!Number.isFinite(number)) return undefined;
        const { text, cannotShow } = this.getNumberFormat(address).format(Value.fromNumber(number), this.culture);
        return cannotShow ? undefined : text;
    }

    /** The addresses of every cell holding a Value — an Entry's, or an array's spilled into it (ADR-0125) — in no particular order. */
    get valueAddresses(): CellAddress[] {
        return [...this.#cells.values()].filter(c => c.value !== undefined).map(c => c.address);
    }

    /** The cell's Value, or `undefined` for a blank cell. */
    getValue(address: CellAddress): Value | undefined {
        return this.#cells.get(address.key)?.value;
    }

    /** The cell's Entry, or `undefined` for a blank cell. */
    getEntry(address: CellAddress): Entry | undefined {
        return this.#cells.get(address.key)?.entry;
    }

    /** The text the Cell Editor opens with: the Formula for a Formula, the constant written under the Sheet's culture otherwise. Empty for a blank cell. */
    getEntryText(address: CellAddress): string {
        const entry = this.#cells.get(address.key)?.entry;
        if (entry === undefined) return '';
        return entry.formula ?? entryText(defined(entry.constant, 'a constant Entry\'s Value'), this.getNumberFormat(address), this.culture);
    }

    /**
     * What the cell shows: its Value formatted by its number format under the Sheet's culture, with its alignment
     * resolved (ADR-0046/0047). Without a width, at no particular width — a number in General in full, at fifteen
     * significant digits. With a width, in a column `width` characters wide (ADR-0047, ADR-0016): General is fitted
     * as Excel's General fits it, and a number in any other format whose text is longer cannot be shown.
     */
    getDisplay(address: CellAddress, width?: number): CellDisplay {
        const characters = width === undefined ? undefined : charactersOf(width);
        const value = this.#cells.get(address.key)?.value;
        if (value === undefined) {
            return { text: '', alignment: resolveAlignment(this.getAlignment(address), undefined), isNumber: false, cannotShow: false, colour: undefined };
        }
        const format = this.getNumberFormat(address);
        const { text, cannotShow, colour } = characters === undefined ? format.format(value, this.culture) : format.formatIn(value, this.culture, characters);
        return { text: cannotShow ? '' : text, alignment: resolveAlignment(this.getAlignment(address), value.kind), isNumber: value.kind === 'number', cannotShow, colour };
    }

    /** Takes text as the user typed it into a cell, read under the Sheet's culture, and recalculates. @internal */
    enter(address: CellAddress, typed: string): SheetChange {
        return this.enterMany([[address, typed]]);
    }

    /**
     * Takes several typed texts as one change, with one recalculation. If any text is a Formula that cannot be
     * read, nothing changes. `pasted`: the texts were pasted from another program, and a text beginning with `=`
     * that cannot be read as a Formula is taken as text (ADR-0048). @internal
     */
    enterMany(typed: Iterable<TypedCell>, pasted = false): SheetChange {
        const entries: [CellAddress, Entry | undefined][] = [];
        const implied: { address: CellAddress; format: NumberFormat }[] = [];
        for (const [address, text] of typed) {
            const read = this.#readTyped(address, text, pasted);
            entries.push([address, read.entry]);
            if (read.implied !== undefined) implied.push({ address, format: read.implied });
        }
        return this.#writeTyped(entries, implied);
    }

    /** Excel's Ctrl+Enter (ADR-0050 item 5): `typed` read as entered in `enteredAt`, written into every cell of `cells`. @internal */
    enterInto(cells: readonly CellAddress[], enteredAt: CellAddress, typed: string): SheetChange {
        const entered = this.#readTyped(enteredAt, typed, false).entry;
        if (entered?.isFormula !== true) return this.enterMany(cells.map(cell => [cell, typed] as const), false);
        const entries: [CellAddress, Entry | undefined][] = [];
        for (const cell of cells) entries.push([cell, entered.movedBy(cell.row - enteredAt.row, cell.column - enteredAt.column)]);
        return this.#writeTyped(entries, []);
    }

    /** One typed text read into `address`: its Entry, and the format typing it implies for a General cell, if any. */
    #readTyped(address: CellAddress, text: string, pasted: boolean): { entry: Entry | undefined; implied: NumberFormat | undefined } {
        if (text.length === 0 || text.startsWith('=')) {
            let entry: Entry | undefined;
            try {
                entry = Entry.parse(text, this.culture);
            } catch (e) { // oxlint-disable-line ex-grid/no-catch -- a pasted Formula that cannot be read is text, as Excel takes a pasted =1+ (ADR-0048)
                if (!pasted || !(e instanceof FormulaSyntaxError)) throw e;
                entry = Entry.fromValue(Value.fromText(text));
            }
            return { entry: entry === undefined ? undefined : this.inDeclaredCase(this.inOwnName(entry)), implied: undefined };
        }
        // A date or a percentage typed into a General cell gives the cell its format, as in Excel.
        const parsed = parseConstantWithFormat(text, this.culture);
        let value = parsed.value;
        let signed: Entry | undefined;
        try {
            signed = value.kind === 'text' ? Entry.signedFormula(text) : undefined;
        } catch (e) { // oxlint-disable-line ex-grid/no-catch -- pasted text Excel would read as a Formula the engine does not implement is text (ADR-0048); typed, it is refused
            if (!pasted || !(e instanceof FormulaSyntaxError)) throw e;
            signed = undefined;
        }
        if (signed !== undefined) return { entry: this.inDeclaredCase(this.inOwnName(signed)), implied: undefined };
        // A plain number typed into a cell that shows percentages is read as a percentage (LVL-015, ADR-0047 second run). Only typed.
        if (!pasted && value.kind === 'number' && this.getNumberFormat(address).isPercent && isPlainNumber(text, this.culture)) {
            value = Value.fromNumber(value.number / 100);
        }
        return { entry: Entry.fromValue(value), implied: parsed.implied };
    }

    /** Typed Entries written as one change: the formats typing implied, then a Formula's format from what it reads, then the Entries. */
    #writeTyped(entries: [CellAddress, Entry | undefined][], implied: readonly { address: CellAddress; format: NumberFormat }[]): SheetChange {
        const rows = new Set<number>();
        for (const { address, format } of implied) {
            if (!this.getNumberFormat(address).isGeneral) continue;
            this.#cellFor(address).numberFormat = format;
            rows.add(address.row);
        }
        // A Formula entered into a General cell takes a format from what it reads, as in Excel (ADR-0047).
        for (const [address, entry] of entries) {
            const parsed = entry?.parsed;
            if (parsed === undefined || !this.getNumberFormat(address).isGeneral) continue;
            const inferred = this.#formatOnEntry(parsed);
            if (inferred === undefined) continue;
            this.#cellFor(address).numberFormat = inferred;
            rows.add(address.row);
        }
        // A 3D Reference is written with its ends in tab order, in their Sheets' own names (ADR-0331 §7).
        for (let i = 0; i < entries.length; i++) {
            const [address, typed] = defined(entries[i], 'an entry');
            if (typed === undefined) continue;
            const ordered = this.workbook.entryInTabOrder(typed);
            if (ordered !== typed) entries[i] = [address, ordered];
        }
        const change = this.setEntries(entries);
        if (rows.size === 0) return change;
        for (const row of change.rows) rows.add(row);
        return new SheetChange(change.valueChanges, change.recalculated, distinctNumbers(rows));
    }

    /** The cell held at `address`, made and held if there is none. */
    #cellFor(address: CellAddress): Cell {
        let cell = this.#cells.get(address.key);
        if (cell === undefined) {
            cell = new Cell(address);
            this.#cells.set(address.key, cell);
        }
        return cell;
    }

    /** Sets or clears one cell's Entry, and recalculates. @internal */
    setEntry(address: CellAddress, entry: Entry | undefined): SheetChange {
        return this.setEntries([[address, entry]]);
    }

    /** Sets or clears several cells' Entries as one change, with one recalculation. A later pair for the same cell wins. @internal */
    setEntries(entries: Iterable<EntryOfCell>): SheetChange {
        const changed = new Set<number>();
        const rows = new Set<number>();
        for (const [address, entry] of entries) {
            let cell = this.#cells.get(address.key);
            if (Entry.same(cell?.entry, entry)) continue;
            const old = cell?.entry?.parsed;
            if (old !== undefined) this.#unregister(address, old);
            if (entry === undefined) {
                defined(cell, 'the cell whose Entry is cleared').entry = undefined;
            } else {
                cell ??= this.#cellFor(address);
                cell.entry = entry;
                if (entry.parsed !== undefined) this.#register(address, entry.parsed);
            }
            changed.add(address.key);
            rows.add(address.row);
        }
        if (changed.size === 0) return SheetChange.none;
        this.countChange();
        return this.#recalculate(new SheetCellSet([...changed].map(key => new SheetCell(this, CellAddress.fromKey(key)))), rows);
    }

    #register(formulaCell: CellAddress, formula: Node): void {
        const areas: Area[] = [];
        const remote: Reference[] = [];
        for (const reference of formula.references()) {
            if (!this.isLocal(reference)) {
                remote.push(reference);
                continue;
            }
            const area = reference.area;
            if (area.isSingleCell) {
                const target = new CellAddress(area.row1, area.column1).key;
                let set = this.#cellDependents.get(target);
                if (set === undefined) {
                    set = new Set();
                    this.#cellDependents.set(target, set);
                }
                set.add(formulaCell.key);
            } else {
                areas.push(area);
            }
        }
        if (areas.length > 0) this.#areaPrecedents.set(formulaCell.key, areas);
        if (remote.length > 0) {
            this.#remoteReferences.set(formulaCell.key, remote);
            this.#registerRemote(formulaCell, remote);
        }
        this.#registerTableReaders(formulaCell, formula);
        if (callsToday(formula)) this.#todayReaders.add(formulaCell.key);
        if ([...formula.calls()].some(call => call.function !== undefined && volatileFunctions.has(call.name))) this.#volatileFormulas.add(formulaCell.key);
        if (callsRandom(formula)) this.#randomReaders.add(formulaCell.key);
    }

    #unregister(formulaCell: CellAddress, formula: Node): void {
        for (const reference of formula.references()) {
            const area = reference.area;
            if (this.isLocal(reference) && area.isSingleCell) {
                const target = new CellAddress(area.row1, area.column1).key;
                const set = this.#cellDependents.get(target);
                if (set?.delete(formulaCell.key) === true && set.size === 0) this.#cellDependents.delete(target);
            }
        }
        this.#areaPrecedents.delete(formulaCell.key);
        this.#unregisterRemote(formulaCell);
        this.#unregisterTableReaders(formulaCell, formula);
        this.#todayReaders.delete(formulaCell.key);
        this.#volatileFormulas.delete(formulaCell.key);
        this.#randomReaders.delete(formulaCell.key);
    }

    /** Records a Formula cell's References to other Sheets on the Sheets they name; a 3D Reference on every Sheet of its span (ADR-0331 §7). */
    #registerRemote(formulaCell: CellAddress, remote: readonly Reference[]): void {
        const dependent = new SheetCell(this, formulaCell);
        const groups = new Map<Sheet, Area[]>();
        for (const reference of remote) {
            for (const target of this.#targets(reference)) {
                let list = groups.get(target);
                if (list === undefined) {
                    list = [];
                    groups.set(target, list);
                }
                list.push(reference.area);
            }
        }
        for (const [target, group] of groups) {
            const areas: Area[] = [];
            for (const area of group) {
                if (!area.isSingleCell) {
                    areas.push(area);
                    continue;
                }
                const address = new CellAddress(area.row1, area.column1).key;
                let set = target.#remoteCellDependents.get(address);
                if (set === undefined) {
                    set = new SheetCellSet();
                    target.#remoteCellDependents.set(address, set);
                }
                set.add(dependent);
            }
            if (areas.length > 0) target.#remoteAreaDependents.set(dependent, areas);
        }
    }

    /** Forgets what `#registerRemote` recorded for a Formula cell. */
    #unregisterRemote(formulaCell: CellAddress): void {
        const remote = this.#remoteReferences.get(formulaCell.key);
        if (remote === undefined) return;
        this.#remoteReferences.delete(formulaCell.key);
        const dependent = new SheetCell(this, formulaCell);
        for (const reference of remote) {
            for (const target of this.#targets(reference)) {
                target.#remoteAreaDependents.delete(dependent);
                const area = reference.area;
                if (!area.isSingleCell) continue;
                const address = new CellAddress(area.row1, area.column1).key;
                const set = target.#remoteCellDependents.get(address);
                if (set?.delete(dependent) === true && set.size === 0) target.#remoteCellDependents.delete(address);
            }
        }
    }

    /** The Sheets a Reference to another Sheet, or a 3D Reference, reads, as the Workbook stands: none for a name no Sheet has. */
    #targets(reference: Reference): readonly Sheet[] {
        if (reference.isThreeD) return this.workbook.span(defined(reference.sheetName, 'a span\'s first Sheet'), defined(reference.lastSheetName, 'a span\'s last Sheet')) ?? []; // oxlint-disable-line ex-grid/no-stand-in-default -- a span an end of which no Sheet has spans no Sheet: it reads none (ADR-0331 §7)
        const target = this.workbook.find(defined(reference.sheetName, 'a qualifier'));
        return target === undefined ? [] : [target];
    }

    /** The Formula cells that read `address` directly. */
    *#dependents(address: CellAddress): Generator<CellAddress> {
        const set = this.#cellDependents.get(address.key);
        if (set !== undefined) for (const dependent of set) yield CellAddress.fromKey(dependent);
        for (const [formulaCell, areas] of this.#areaPrecedents) {
            if (areas.some(area => area.contains(address))) yield CellAddress.fromKey(formulaCell);
        }
    }

    /** The addresses inside `area` that hold something, in no particular order. @internal */
    *cellsIn(area: Area): Generator<CellAddress> {
        if (area.cellCount <= this.#cells.size) {
            for (let row = area.row1; row <= area.row2; row++) {
                for (let column = area.column1; column <= area.column2; column++) {
                    const address = new CellAddress(row, column);
                    if (this.#cells.has(address.key)) yield address;
                }
            }
        } else {
            for (const cell of this.#cells.values()) {
                if (area.contains(cell.address)) yield cell.address;
            }
        }
    }

    /** The cell held at `address`. @internal */
    cellAt(address: CellAddress): Cell | undefined {
        return this.#cells.get(address.key);
    }

    /** The Formula whose result a cell shows: its own, or for a cell of a Spill Range its Anchor's (ADR-0125). @internal */
    formulaShownAt(address: CellAddress): Node | undefined {
        const cell = this.#cells.get(address.key);
        if (cell === undefined) return undefined;
        if (cell.entry !== undefined) return cell.entry.parsed;
        return cell.spilledFrom === undefined ? undefined : this.#cells.get(cell.spilledFrom.key)?.entry?.parsed;
    }

    // ---- The state an undo compares (Sheet.State.cs) ----

    /** Every held cell's state, keyed by address. @internal */
    snapshot(): Map<number, CellState> {
        const states = new Map<number, CellState>();
        for (const cell of this.#cells.values()) states.set(cell.address.key, stateOf(cell));
        return states;
    }

    #stateAt(address: CellAddress): CellState {
        const cell = this.#cells.get(address.key);
        return cell === undefined ? CellState.blank : stateOf(cell);
    }

    /** The cell as a copy or a fill carries it: its Entry, and every part of the Cell Format it records, from whichever level records it. */
    #carriedState(address: CellAddress): CellState {
        const shown = this.#ownFormat(address);
        return this.#stateAt(address).recorded.withFormat(shown);
    }

    /** States about to be written whole, each part kept only where it differs from what its row or column gives it. @internal */
    settle(states: Iterable<AddressedState>): AddressedState[] {
        return [...states].map(p => ({ address: p.address, state: settled(p.state, this.#inherited(p.address)) }));
    }

    /** The Cell Format every held cell shows, keyed by address. */
    #shownSnapshot(): Map<number, CellFormat> {
        const shown = new Map<number, CellFormat>();
        for (const cell of this.#cells.values()) shown.set(cell.address.key, this.getCellFormat(cell.address));
        return shown;
    }

    /** The change between `before` and now, address by address. @internal */
    diff(before: ReadonlyMap<number, CellState>, recalculated: Iterable<CellAddress>, shownBefore?: ReadonlyMap<number, CellFormat>): SheetChange {
        const valueChanges: CellAddress[] = [];
        const rows = new Set<number>();
        const compare = (address: CellAddress, old: CellState): void => {
            const now = this.#stateAt(address);
            const valueChanged = !Value.unchanged(old.value, now.value);
            if (valueChanged) valueChanges.push(address);
            const shown = shownBefore === undefined || (shownBefore.get(address.key) ?? defaultCellFormat).equals(this.getCellFormat(address));
            if (valueChanged || !old.sameRecord(now) || !shown) rows.add(address.row);
        };
        for (const [key, old] of before) compare(CellAddress.fromKey(key), old);
        for (const cell of this.#cells.values()) {
            if (!before.has(cell.address.key)) compare(cell.address, CellState.blank);
        }
        valueChanges.sort((a, b) => a.compareTo(b));
        const recomputed = distinctAddresses(recalculated);
        return valueChanges.length === 0 && rows.size === 0 && recomputed.length === 0
            ? SheetChange.none
            : new SheetChange(valueChanges, recomputed, distinctNumbers(rows));
    }

    /** Forgets and rebuilds the dependency graph from every Formula held. @internal */
    rebuildDependencies(): void {
        this.#cellDependents.clear();
        this.#areaPrecedents.clear();
        for (const formulaCell of this.#remoteReferences.keys()) this.#unregisterRemote(CellAddress.fromKey(formulaCell));
        this.#tableReaders.clear();
        this.#todayReaders.clear();
        this.#volatileFormulas.clear();
        this.#randomReaders.clear();
        for (const cell of this.#cells.values()) {
            const parsed = cell.entry?.parsed;
            if (parsed !== undefined) this.#register(cell.address, parsed);
        }
    }

    // ---- Cell Formats (Sheet.CellFormat.cs) ----

    /** The cell's Cell Format as it shows: each part the cell's own, else its row's, else its column's, else the part's default (ADR-0047, ADR-0071, SH-38). */
    getCellFormat(address: CellAddress): CellFormat {
        const recorded = this.#ownFormat(address);
        return new CellFormat(recorded.numberFormat, recorded.alignment, recorded.font, recorded.fill, this.getBorders(address));
    }

    /** The cell's Cell Format as it records it, cell over row over column, its Borders its own four sides. */
    #ownFormat(address: CellAddress): CellFormat {
        const cell = this.#cells.get(address.key);
        return cell === undefined ? this.#inherited(address) : cell.over(this.#inherited(address));
    }

    /** The cell's Number Format, as it takes effect: cell over row over column, else General (ADR-0047). */
    getNumberFormat(address: CellAddress): NumberFormat {
        return this.#cells.get(address.key)?.numberFormat ?? this.#rowFormats.get(address.row)?.numberFormat
            ?? this.#columnFormats.get(address.column)?.numberFormat ?? NumberFormat.general;
    }

    /** The cell's horizontal alignment setting, as it takes effect: cell over row over column (ADR-0047). */
    getAlignment(address: CellAddress): HorizontalAlignment {
        // oxlint-disable-next-line ex-grid/no-stand-in-default -- no level recording one is General, Excel's default (ADR-0047)
        return this.#cells.get(address.key)?.alignment ?? this.#rowFormats.get(address.row)?.alignment
            ?? this.#columnFormats.get(address.column)?.alignment ?? 'general';
    }

    /** The cell's Font, as it takes effect: cell over row over column (ADR-0071, SH-38). */
    getFont(address: CellAddress): CellFont {
        return this.#cells.get(address.key)?.font ?? this.#rowFormats.get(address.row)?.font ?? this.#columnFormats.get(address.column)?.font ?? CellFont.default;
    }

    /** The cell's Fill, as it takes effect: cell over row over column (ADR-0071, SH-38). */
    getFill(address: CellAddress): CellFill {
        return this.#cells.get(address.key)?.fill ?? this.#rowFormats.get(address.row)?.fill ?? this.#columnFormats.get(address.column)?.fill ?? CellFill.none;
    }

    /**
     * The line on each of the cell's four sides as it shows, which is the line on the edge it shares with the cell
     * beside it, read the same from either cell (ADR-0071; the twelfth Windows run): the upper cell's, or the left
     * cell's, where both record one; where only one does, that one.
     */
    getBorders(address: CellAddress): CellBorders {
        const sides = this.#ownSides(address);
        return new CellBorders({
            top: address.row > 0 ? edge(this.#ownSides(new CellAddress(address.row - 1, address.column)).bottom, sides.top) : sides.top,
            bottom: sides.bottom.isNone && address.row < rowCount - 1 ? this.#ownSides(new CellAddress(address.row + 1, address.column)).top : sides.bottom,
            left: address.column > 0 ? edge(this.#ownSides(new CellAddress(address.row, address.column - 1)).right, sides.left) : sides.left,
            right: sides.right.isNone && address.column < columnCount - 1 ? this.#ownSides(new CellAddress(address.row, address.column + 1)).left : sides.right,
        });
    }

    /** The cell's own four sides, as it records them: cell over row over column (ADR-0071, SH-38). */
    #ownSides(address: CellAddress): CellBorders {
        return this.#cells.get(address.key)?.borders ?? this.#rowFormats.get(address.row)?.borders
            ?? this.#columnFormats.get(address.column)?.borders ?? CellBorders.none;
    }

    /** The Number Format recorded on the whole row, or `undefined` when none is (ADR-0047). */
    getRowNumberFormat(row: number): NumberFormat | undefined {
        return this.#rowFormats.get(checkRow(row))?.numberFormat;
    }

    /** The Number Format recorded on the whole column, or `undefined` when none is (ADR-0047). */
    getColumnNumberFormat(column: number): NumberFormat | undefined {
        return this.#columnFormats.get(checkColumn(column))?.numberFormat;
    }

    /** The alignment recorded on the whole row, or `undefined` when none is (ADR-0047). */
    getRowAlignment(row: number): HorizontalAlignment | undefined {
        return this.#rowFormats.get(checkRow(row))?.alignment;
    }

    /** The alignment recorded on the whole column, or `undefined` when none is (ADR-0047). */
    getColumnAlignment(column: number): HorizontalAlignment | undefined {
        return this.#columnFormats.get(checkColumn(column))?.alignment;
    }

    /** Sets the Number Format of cells; `undefined` is General. No Value changes. @internal */
    setNumberFormatOf(addresses: Iterable<CellAddress>, format: NumberFormat | undefined): SheetChange {
        return this.#formatCells(addresses, new CellFormatChange({ numberFormat: format ?? NumberFormat.general }), undefined, undefined, undefined).change;
    }

    /** Sets the Number Format of a range; `undefined` is General. Whole columns and whole rows record one entry each (ADR-0047). @internal */
    setNumberFormat(range: CellRange, format: NumberFormat | undefined): SheetChange {
        return this.applyCellFormat(range, new CellFormatChange({ numberFormat: format ?? NumberFormat.general })).change;
    }

    /** Sets the horizontal alignment of cells. No Value changes. @internal */
    setAlignmentOf(addresses: Iterable<CellAddress>, alignment: HorizontalAlignment): SheetChange {
        return this.#formatCells(addresses, new CellFormatChange({ alignment: checkAlignment(alignment) }), undefined, undefined, undefined).change;
    }

    /** Sets the horizontal alignment of a range, recording whole columns and whole rows as one entry each. @internal */
    setAlignment(range: CellRange, alignment: HorizontalAlignment): SheetChange {
        return this.applyCellFormat(range, new CellFormatChange({ alignment: checkAlignment(alignment) })).change;
    }

    /** Sets the parts `change` names on several ranges at once (ADR-0046, ADR-0071). @internal */
    setCellFormat(ranges: Iterable<CellRange>, change: CellFormatChange): SheetChange {
        return this.applyCellFormats(Sheet.checkCellFormat(ranges, change), change).change;
    }

    /** The ranges a change is set on, checked with the change. Throws for no range, a change that sets nothing, or an alignment that is not one. @internal */
    static checkCellFormat(ranges: Iterable<CellRange>, change: CellFormatChange): CellRange[] {
        const list = [...ranges];
        if (list.length === 0) throw new RangeError('A Cell Format is set on at least one range.');
        if (change.isEmpty) throw new RangeError('The change sets no part of a Cell Format.');
        if (change.alignment !== undefined) checkAlignment(change.alignment);
        return list;
    }

    /** Sets `change` on each range in turn, as one change. @internal */
    applyCellFormats(ranges: readonly CellRange[], change: CellFormatChange): CellFormatsOutcome {
        const parts = ranges.map(range => this.applyCellFormat(range, change));
        return { change: SheetChange.merge(parts.map(p => p.change)), parts };
    }

    /** Undoes a change set on several ranges, each range's part in the reverse of the order it was set. @internal */
    undoCellFormats(outcome: CellFormatsOutcome): SheetChange {
        const changes: SheetChange[] = [];
        for (let i = outcome.parts.length - 1; i >= 0; i--) changes.push(this.undoCellFormat(defined(outcome.parts[i], 'a part')));
        return SheetChange.merge(changes);
    }

    /** What a cell shows where it records nothing of its own: its row's, else its column's, else the defaults. */
    #inherited(address: CellAddress): CellFormat {
        return (this.#rowFormats.get(address.row) ?? AxisFormat.empty).over((this.#columnFormats.get(address.column) ?? AxisFormat.empty).over(defaultCellFormat));
    }

    /**
     * A change set on a range, as Excel sets it (ADR-0047, ADR-0071): on the range, and, across each outer edge
     * the change sets, on the cells beside it, whose record of that edge it clears. Undoing it puts back both. @internal
     */
    applyCellFormat(range: CellRange, change: CellFormatChange): CellFormatOutcome {
        let set = change;
        if (set.borders !== undefined) set = set.withBorders(set.borders.on(range));
        const mine = this.#applyCellFormatOn(range, set);
        if (!set.setsBorders) return mine;
        const parts: CellFormatOutcome[] = [mine];
        for (const { range: beside, borders } of defined(set.borders, 'the Borders set').beside(range)) {
            parts.push(this.#applyCellFormatOn(beside, new CellFormatChange({ borders })));
        }
        return combined(parts);
    }

    /**
     * A change set on the range alone. Whole columns record it on the column level; whole rows on the row level.
     * The whole Sheet is whole columns. A cell a level alone would not show what it should is given it as its own.
     * Any other range sets its cells.
     */
    #applyCellFormatOn(range: CellRange, change: CellFormatChange): CellFormatOutcome {
        const rowsBefore = new Map(this.#rowFormats);
        const columnsBefore = new Map(this.#columnFormats);
        const wholeColumns = range.isWholeColumns;
        const wholeRows = range.isWholeRows;
        if (!wholeColumns && !wholeRows) return this.#formatCells(range.cells(), change, range, undefined, undefined);

        const touched = this.#cellsGivenTheirOwn(range, change);
        // What each of them records before the levels change under it: a Font or Borders is patched from that.
        const shownBefore = new Map<number, CellFormat>();
        for (const address of touched) if (!shownBefore.has(address.key)) shownBefore.set(address.key, this.#ownFormat(address));
        if (wholeColumns) {
            if (wholeRows) {
                for (const row of this.#rowFormats.keys()) setAxis(this.#rowFormats, row, underWholeSheet(defined(this.#rowFormats.get(row), 'a row\'s level'), change));
            }
            for (let column = range.first.column; column <= range.last.column; column++) {
                // Nothing lies under a column: a part at its default records nothing. The whole Sheet has no outer edge (the twelfth run, case 15).
                const level = this.#columnFormats.get(column) ?? AxisFormat.empty;
                const next = change.applyTo(level.over(defaultCellFormat), wholeRows ? PlaceInRange.inside : new PlaceInRange(false, false, column === range.first.column, column === range.last.column));
                setAxis(this.#columnFormats, column, level.with(change, next, defaultCellFormat));
            }
        } else {
            // General, General alignment or no Fill on a row is recorded only where a column's would otherwise show through.
            const columnLevels = [...this.#columnFormats.values()];
            const formatted = change.numberFormat !== undefined && columnLevels.some(f => f.numberFormat !== undefined);
            const aligned = change.alignment !== undefined && columnLevels.some(f => f.alignment !== undefined);
            const filled = change.fill !== undefined && columnLevels.some(f => f.fill !== undefined);
            for (let row = range.first.row; row <= range.last.row; row++) {
                const level = this.#rowFormats.get(row) ?? AxisFormat.empty;
                const next = change.applyTo(level.over(defaultCellFormat), new PlaceInRange(row === range.first.row, row === range.last.row, false, false));
                const recorded = level.with(change, next, defaultCellFormat);
                setAxis(this.#rowFormats, row, new AxisFormat(
                    formatted ? next.numberFormat : recorded.numberFormat,
                    aligned ? next.alignment : recorded.alignment,
                    recorded.font,
                    filled ? next.fill : recorded.fill,
                    recorded.borders));
            }
        }
        return this.#formatCells(touched, change, range, shownBefore, { rows: rowsBefore, columns: columnsBefore });
    }

    /** The cells a change set on whole columns or whole rows gives a record of their own, because the levels alone would not show them what they should. */
    #cellsGivenTheirOwn(range: CellRange, change: CellFormatChange): CellAddress[] {
        const touched: CellAddress[] = [];
        const borders = change.setsBorders ? change.borders : undefined;
        if (range.isWholeColumns) {
            for (const cell of this.#cells.values()) if (cell.address.column >= range.first.column && cell.address.column <= range.last.column) touched.push(cell.address);
            if (range.isWholeRows) return touched;
            for (const [row, level] of this.#rowFormats) {
                if (!change.touches(level)) continue;
                for (let column = range.first.column; column <= range.last.column; column++) touched.push(new CellAddress(row, column));
            }
        } else {
            for (const cell of this.#cells.values()) if (cell.address.row >= range.first.row && cell.address.row <= range.last.row) touched.push(cell.address);
            for (const [column, level] of this.#columnFormats) {
                if (!(change.setsFont && level.font !== undefined) && !(borders !== undefined && level.borders !== undefined)) continue;
                for (let row = range.first.row; row <= range.last.row; row++) touched.push(new CellAddress(row, column));
            }
            if (borders !== undefined && !samePart(borders.left, borders.insideVertical)) {
                for (let row = range.first.row; row <= range.last.row; row++) touched.push(new CellAddress(row, range.first.column));
            }
        }
        return touched;
    }

    /**
     * Gives each cell what it records once `change` is set on it at its place in `range`, patched from what it
     * recorded before, and recorded on the cell only where it differs from what its row or column gives it. A
     * changed top or bottom side names the row across it too.
     */
    #formatCells(addresses: Iterable<CellAddress>, change: CellFormatChange, range: CellRange | undefined,
        shownBefore: ReadonlyMap<number, CellFormat> | undefined,
        axesBefore: { rows: ReadonlyMap<number, AxisFormat>; columns: ReadonlyMap<number, AxisFormat> } | undefined): CellFormatOutcome {
        const before = new Map<number, AddressedState>();
        const rows = new Set<number>(axesBefore === undefined ? [] : this.#rowsAffectedByAxes(axesBefore.rows, axesBefore.columns));
        for (const address of addresses) {
            if (before.has(address.key)) continue;
            before.set(address.key, { address, state: this.#stateAt(address).recorded });
            const shown = shownBefore?.get(address.key) ?? this.#ownFormat(address);
            const next = change.applyTo(shown, range === undefined ? PlaceInRange.alone : PlaceInRange.of(range, address));
            let cell = this.#cells.get(address.key);
            const existing = cell !== undefined;
            cell ??= new Cell(address);
            const changed = cell.take(change, next, this.#inherited(address));
            if (cell.isEmpty && cell.value === undefined) this.#cells.delete(address.key);
            else if (!existing) this.#cells.set(address.key, cell);
            if (changed || (existing && axesBefore !== undefined)) rows.add(address.row);
            for (const row of rowsAcross(address, shown.borders, next.borders)) rows.add(row);
        }
        // A level shows in the cells that hold nothing too, and rows names only those that hold one.
        const levels = axesBefore !== undefined && this.#levelsDiffer(axesBefore.rows, axesBefore.columns);
        const sheetChange = rows.size === 0 && !levels ? SheetChange.none : new SheetChange([], [], distinctNumbers(rows), [], levels);
        return {
            change: sheetChange,
            before: [...before.values()],
            rowsBefore: axesBefore?.rows ?? new Map(this.#rowFormats),
            columnsBefore: axesBefore?.columns ?? new Map(this.#columnFormats),
        };
    }

    /** The rows holding a cell that shows under a row or column level that differs from `rows` and `columns`. */
    #rowsAffectedByAxes(rows: ReadonlyMap<number, AxisFormat>, columns: ReadonlyMap<number, AxisFormat>): number[] {
        const changedRows = new Set([...rows.keys(), ...this.#rowFormats.keys()].filter(r => !sameLevel(rows.get(r), this.#rowFormats.get(r))));
        const changedColumns = new Set([...columns.keys(), ...this.#columnFormats.keys()].filter(c => !sameLevel(columns.get(c), this.#columnFormats.get(c))));
        if (changedRows.size === 0 && changedColumns.size === 0) return [];
        return [...this.#cells.values()].filter(c => changedRows.has(c.address.row) || changedColumns.has(c.address.column)).map(c => c.address.row);
    }

    /** Undoes a formatting command: the row and column levels, then the cells, exactly as they were. @internal */
    undoCellFormat(outcome: CellFormatOutcome): SheetChange {
        const rowsNow = this.#rowFormats;
        const columnsNow = this.#columnFormats;
        this.#rowFormats = new Map(outcome.rowsBefore);
        this.#columnFormats = new Map(outcome.columnsBefore);
        const rows = new Set(this.#rowsAffectedByAxes(rowsNow, columnsNow));
        const levels = this.#levelsDiffer(rowsNow, columnsNow);
        const change = this.restore(outcome.before);
        for (const row of change.rows) rows.add(row);
        return rows.size === 0 && !levels
            ? SheetChange.none
            : new SheetChange(change.valueChanges, change.recalculated, distinctNumbers(rows), [], levels);
    }

    /** Whether the row or column levels now differ from `rows` and `columns`. */
    #levelsDiffer(rows: ReadonlyMap<number, AxisFormat>, columns: ReadonlyMap<number, AxisFormat>): boolean {
        return levelsDiffer(rows, this.#rowFormats) || levelsDiffer(columns, this.#columnFormats);
    }

    /**
     * The row (or column) levels after a structural edit: moved with their rows (columns), those deleted or pushed
     * off the edge dropped, and inserted rows (columns) given the level of the one before them, all but its
     * Borders, when `formatInserted` (ADR-0046, ADR-0071).
     */
    #shiftAxisFormats(edit: StructuralEdit, formatInserted: boolean): void {
        const rows = edit.axis === 'rows';
        const levels = rows ? this.#rowFormats : this.#columnFormats;
        const shifted = new Map<number, AxisFormat>();
        for (const [index, level] of levels) {
            const to = edit.move(rows ? new CellAddress(index, 0) : new CellAddress(0, index));
            if (to !== undefined) shifted.set(rows ? to.row : to.column, level);
        }
        const above = edit.start > 0 ? levels.get(edit.start - 1) : undefined;
        if (edit.isInsert && formatInserted && above !== undefined) {
            const taken = above.replacing({ borders: undefined });
            if (!taken.isEmpty) {
                for (let i = 0; i < edit.count; i++) shifted.set(edit.start + i, taken);
            }
        }
        if (rows) this.#rowFormats = shifted;
        else this.#columnFormats = shifted;
    }

    // ---- What a range of cells shows (Sheet.CellFormatSpread.cs) ----

    /**
     * The Cell Formats the cells of `range` show, each once (ADR-0071): what Format Cells reads to show a part that
     * differs across the Selection. Its Borders are each cell's own four sides. Read from what the Sheet records,
     * never cell by cell (principle 5).
     */
    getCellFormats(range: CellRange): CellFormat[] {
        const shown = new Map<string, CellFormat>();
        const add = (format: CellFormat): void => {
            if (!shown.has(format.key)) shown.set(format.key, format);
        };
        // How many cells recording a Cell Format of their own lie in each block of a row group and a column group.
        const recorded = new Map<string, number>();
        for (const cell of this.#cells.values()) {
            if (!cell.isFormatted || !range.contains(cell.address)) continue;
            add(this.#ownFormat(cell.address));
            const block = `${(this.#rowFormats.get(cell.address.row) ?? AxisFormat.empty).key}|${(this.#columnFormats.get(cell.address.column) ?? AxisFormat.empty).key}`;
            recorded.set(block, (recorded.get(block) ?? 0) + 1); // oxlint-disable-line ex-grid/no-stand-in-default -- a block no cell has counted yet holds none
        }
        const rows = levelGroups(this.#rowFormats, range.first.row, range.last.row);
        const columns = levelGroups(this.#columnFormats, range.first.column, range.last.column);
        for (const row of rows.values()) {
            for (const column of columns.values()) {
                if (row.count * column.count > (recorded.get(`${row.level.key}|${column.level.key}`) ?? 0)) add(row.level.over(column.level.over(defaultCellFormat))); // oxlint-disable-line ex-grid/no-stand-in-default -- a block no cell records holds none
            }
        }
        return [...shown.values()];
    }

    /** The lines drawn along each outer edge of `range`, each once per edge (ADR-0071; the fourteenth Windows run, case 13). */
    getEdgeLines(range: CellRange): RangeEdgeLines {
        const { first, last } = range;
        return {
            top: this.#linesBetween(true, first.row - 1, first.row, first.column, last.column),
            bottom: this.#linesBetween(true, last.row, last.row + 1, first.column, last.column),
            left: this.#linesBetween(false, first.column - 1, first.column, first.row, last.row),
            right: this.#linesBetween(false, last.column, last.column + 1, first.row, last.row),
        };
    }

    /** The lines drawn on the edges between two neighbouring lines of cells, each once. A line beyond the Sheet's edge records nothing. */
    #linesBetween(horizontal: boolean, before: number, after: number, first: number, last: number): BorderLine[] {
        const lines = new Map<string, BorderLine>();
        const addLine = (line: BorderLine): void => {
            if (!lines.has(line.key)) lines.set(line.key, line);
        };
        const count = horizontal ? rowCount : columnCount;
        const hasBefore = before >= 0;
        const hasAfter = after < count;
        const across = horizontal ? this.#columnFormats : this.#rowFormats;
        const groups = levelGroups(across, first, last);
        const shownOn = (upperOrLeft: CellBorders, lowerOrRight: CellBorders): BorderLine => horizontal
            ? edge(hasBefore ? upperOrLeft.bottom : BorderLine.none, hasAfter ? lowerOrRight.top : BorderLine.none)
            : edge(hasBefore ? upperOrLeft.right : BorderLine.none, hasAfter ? lowerOrRight.left : BorderLine.none);
        const ownAt = (line: number, position: number): CellBorders =>
            line < 0 || line >= count ? CellBorders.none : this.#ownSides(horizontal ? new CellAddress(line, position) : new CellAddress(position, line));
        const levelled = (line: number, level: AxisFormat): CellBorders => horizontal
            ? this.#rowFormats.get(line)?.borders ?? level.borders ?? CellBorders.none
            : level.borders ?? this.#columnFormats.get(line)?.borders ?? CellBorders.none;
        // Where a cell on either side records Borders of its own, the edge is read there, and that place leaves its level's group.
        const read = new Set<number>();
        for (const cell of this.#cells.values()) {
            if (cell.borders === undefined) continue;
            const line = horizontal ? cell.address.row : cell.address.column;
            const position = horizontal ? cell.address.column : cell.address.row;
            if ((line !== before && line !== after) || position < first || position > last || read.has(position)) continue;
            read.add(position);
            addLine(shownOn(ownAt(before, position), ownAt(after, position)));
            const group = defined(groups.get((across.get(position) ?? AxisFormat.empty).key), 'the level\'s group');
            group.count--;
        }
        // Every other place shows what the levels record: each cell's row over its column.
        for (const { level, count: places } of groups.values()) {
            if (places > 0) addLine(shownOn(levelled(before, level), levelled(after, level)));
        }
        return [...lines.values()];
    }

    // ---- Recalculation (Sheet.Recalculation.cs) ----

    /** The Formula cells, on any Sheet of the Workbook, that read `address` of this Sheet directly. @internal */
    *dependentsInWorkbook(address: CellAddress): Generator<SheetCell> {
        for (const dependent of this.#dependents(address)) yield new SheetCell(this, dependent);
        const set = this.#remoteCellDependents.get(address.key);
        if (set !== undefined) yield* set;
        for (const [dependent, areas] of this.#remoteAreaDependents) {
            if (areas.some(area => area.contains(address))) yield dependent;
        }
    }

    /** The Sheet a Reference's qualifier names: this one for none or its own name, another of the Workbook for its name; `undefined` for a name no Sheet has. @internal */
    sheetNamed(qualifier: string | undefined): Sheet | undefined {
        return this.isThisSheet(qualifier) ? this : this.workbook.find(defined(qualifier, 'a qualifier'));
    }

    /** One recalculation of cells on any Sheet of the Workbook, started from this Sheet: what it changed here. */
    #recalculate(changed: SheetCellSet, rows: Set<number>): SheetChange {
        const changes = Sheet.recalculateWorkbook(this.workbook, changed, this, rows);
        let mine: SheetChange | undefined;
        for (const [sheet, change] of changes) {
            if (sheet === this) mine = change;
            else this.workbook.collect(sheet, change);
        }
        return mine ?? SheetChange.none;
    }

    /**
     * One recalculation: what `changed` reaches and every volatile Formula of the Workbook, the moment read once
     * (ADR-0124); then the spills laid out, Sheet by Sheet, and what their layout changed recalculated with every
     * volatile Formula, until a layout changes nothing. What it changed on each Sheet it reached; `initiator`'s
     * always. @internal
     */
    static recalculateWorkbook(workbook: Workbook, changed: SheetCellSet, initiator: Sheet, initiatorRows: Set<number>): Map<Sheet, SheetChange> {
        // Read once, so every NOW of the Workbook in one recalculation answers the same moment.
        const moment = workbook.nowSource?.();
        for (const sheet of workbook.sheets) sheet.#moment = moment;
        const rows = new Map<Sheet, Set<number>>(workbook.sheets.map(s => [s, s === initiator ? initiatorRows : new Set<number>()]));
        const changes = new Map<Sheet, SheetChange[]>(workbook.sheets.map(s => [s, []]));
        const add = (pass: Map<Sheet, SheetChange>): void => {
            for (const [sheet, change] of pass) defined(changes.get(sheet), 'a Sheet\'s changes').push(change);
        };
        add(Sheet.#recalculatePass(workbook, changed, rows, true));
        let passes = 1;
        for (;;) {
            const laidOut: SheetCell[] = [];
            for (const sheet of workbook.sheets) {
                const cells = sheet.#layOutSpills();
                if (cells.length === 0) continue;
                const sheetRows = defined(rows.get(sheet), 'a Sheet\'s rows');
                for (const address of cells) sheetRows.add(address.row);
                defined(changes.get(sheet), 'a Sheet\'s changes').push(new SheetChange(cells, [], distinctNumbers(cells.map(a => a.row))));
                for (const address of cells) laidOut.push(new SheetCell(sheet, address));
            }
            if (laidOut.length === 0) break;
            const readers = new SheetCellSet();
            for (const cell of laidOut) readers.addAll(cell.sheet.dependentsInWorkbook(cell.address));
            if ((readers.size === 0 && !workbook.sheets.some(s => s.hasVolatileFormulas)) || passes === mostPasses) break;
            add(Sheet.#recalculatePass(workbook, readers, rows, true));
            passes++;
        }
        initiator.recalculationPasses = passes;
        const result = new Map<Sheet, SheetChange>();
        for (const [sheet, list] of changes) {
            if (sheet === initiator || list.length > 0) result.set(sheet, SheetChange.merge(list));
        }
        return result;
    }

    /**
     * Recomputes the changed cells and everything that depends on them, on any Sheet, in dependency order, into a
     * staging set; a cycle, and everything downstream of one, is `#CIRC!` (ADR-0047). Only when every Value is
     * computed are they published.
     */
    static #recalculatePass(workbook: Workbook, changed: SheetCellSet, rows: Map<Sheet, Set<number>>, withVolatile: boolean): Map<Sheet, SheetChange> {
        // 1. Everything the change can reach, and every volatile Formula, whatever the change (ADR-0124).
        const dirty = new SheetCellSet(changed);
        if (withVolatile) {
            for (const sheet of workbook.sheets) for (const key of sheet.#volatileFormulas) dirty.add(new SheetCell(sheet, CellAddress.fromKey(key)));
        }
        const queue: SheetCell[] = [...dirty];
        for (let head = 0; head < queue.length; head++) {
            const next = defined(queue[head], 'a queued cell');
            for (const dependent of next.sheet.dependentsInWorkbook(next.address)) {
                if (dirty.add(dependent)) queue.push(dependent);
            }
        }

        const staged = new Map<Sheet, Map<number, Value | undefined>>(workbook.sheets.map(s => [s, new Map()]));
        const arrays = new Map<Sheet, Map<number, ValueArray | undefined>>(workbook.sheets.map(s => [s, new Map()]));
        const recalculated = new Map<Sheet, CellAddress[]>(workbook.sheets.map(s => [s, []]));
        const stagedOf = (sheet: Sheet): Map<number, Value | undefined> => defined(staged.get(sheet), 'a Sheet\'s staged Values');
        const arraysOf = (sheet: Sheet): Map<number, ValueArray | undefined> => defined(arrays.get(sheet), 'a Sheet\'s arrays');
        const recalculatedOf = (sheet: Sheet): CellAddress[] => defined(recalculated.get(sheet), 'a Sheet\'s recalculated cells');
        const formulas = new SheetCellSet();
        for (const target of dirty) {
            const cell = target.sheet.#cells.get(target.address.key);
            const entry = cell?.entry;
            if (entry?.parsed !== undefined) formulas.add(target);
            // A spilled cell with no Entry keeps its Value: the spill's layout gives it, after the pass (ADR-0125).
            else if (entry !== undefined || cell?.spilledFrom === undefined) stagedOf(target.sheet).set(target.address.key, entry?.constant);
        }

        // 2. Dependency order among the Formulas to recompute (Kahn). What never becomes ready is in a cycle or downstream of one.
        const edges = new SheetCellMap<SheetCell[]>();
        const waiting = new SheetCellMap<number>();
        for (const formula of formulas) waiting.set(formula, 0);
        for (const precedent of formulas) {
            const targets: SheetCell[] = [];
            const seen = new SheetCellSet();
            for (const dependent of precedent.sheet.dependentsInWorkbook(precedent.address)) {
                if (!seen.add(dependent) || !formulas.has(dependent)) continue;
                targets.push(dependent);
                waiting.set(dependent, defined(waiting.get(dependent), 'a waiting count') + 1);
            }
            edges.set(precedent, targets);
        }

        // A Reference a volatile function computes is read as it stands after this recalculation: a cell still to
        // compute is computed first, on demand, and a Formula that reaches itself that way is #CIRC! (ADR-0124).
        const inProgress = new SheetCellSet();
        const reads: ReadState = { met: undefined, metRefusal: false };
        const readers = new Map<Sheet, { reader: StagedReader; evaluator: Evaluator }>();

        // One Formula, once: in dependency order, or first, on demand, when a computed Reference reads it before its turn.
        const compute = (target: SheetCell): Value | undefined => {
            const { sheet, address } = target;
            const sheetStaged = stagedOf(sheet);
            if (sheetStaged.has(address.key)) return sheetStaged.get(address.key);
            if (inProgress.has(target)) return Value.fromError('#CIRC!');
            inProgress.add(target);
            const { reader, evaluator } = defined(readers.get(sheet), 'a Sheet\'s reader');
            const anchor = defined(sheet.#cells.get(address.key), 'the Formula\'s cell');
            const node = defined(anchor.entry?.parsed, 'the cell\'s Formula');
            const outer = evaluator.self;
            evaluator.self = address;
            // The outer Formula's reads so far are its own; this one's start from none.
            const outerMet = reads.met;
            const outerMetRefusal = reads.metRefusal;
            reads.met = undefined;
            reads.metRefusal = false;
            let value: Value;
            const tainted = sheet.#taint(node, reader);
            const sheetArrays = arraysOf(sheet);
            if (tainted !== undefined) {
                value = tainted;
                sheetArrays.set(address.key, undefined);
            } else {
                // A result of more than one Value spills (ADR-0125): the anchor holds the array, and shows its first
                // Value until the layout after the pass says whether it spills.
                const computed = evaluator.evaluateResult(node);
                const array = isMulti(computed) ? evaluator.toArray(computed) : undefined;
                const scalar = array === undefined ? evaluator.scalarOf(computed) : undefined;
                const met = reads.met as ErrorValue | undefined;
                if (met !== undefined) {
                    value = Value.fromError(met);
                    sheetArrays.set(address.key, undefined);
                } else if (reads.metRefusal as boolean) {
                    value = Value.refusal;
                    sheetArrays.set(address.key, undefined);
                } else {
                    sheetArrays.set(address.key, array);
                    value = array === undefined ? scalar ?? Value.fromNumber(0)
                        : stillBlocked(anchor, array) ? Value.fromError('#SPILL!')
                        : array.at(0, 0) ?? Value.fromNumber(0);
                }
            }
            // A refused Anchor whose array's shape its text settles spills refusals over that shape (2026-10-13, decided with the user).
            if (value.isRefused) {
                const shape = knownShape(node, r => sheet.sheetNamed(r.sheetName) !== undefined);
                if (shape !== undefined && (shape.rows > 1 || shape.columns > 1)) {
                    const refusals = new ValueArray(shape.rows, shape.columns);
                    for (let r = 0; r < shape.rows; r++) {
                        for (let c = 0; c < shape.columns; c++) refusals.set(r, c, Value.refusal);
                    }
                    sheetArrays.set(address.key, refusals);
                    if (stillBlocked(anchor, refusals)) value = Value.fromError('#SPILL!');
                }
            }
            reads.met = outerMet;
            reads.metRefusal = outerMetRefusal;
            evaluator.self = outer;
            inProgress.delete(target);
            sheetStaged.set(address.key, value);
            recalculatedOf(sheet).push(address);
            return value;
        };

        for (const sheet of workbook.sheets) {
            const reader = new StagedReader(
                sheet, stagedOf(sheet),
                address => {
                    const cell = new SheetCell(sheet, address);
                    return formulas.has(cell) ? compute(cell) : undefined;
                },
                arraysOf(sheet), reads,
                name => {
                    const other = workbook.find(name);
                    return other === undefined ? undefined : readers.get(other)?.reader;
                },
                (first, last) => workbook.span(first, last)?.map(s => defined(readers.get(s), 'a Sheet\'s reader').reader));
            readers.set(sheet, { reader, evaluator: new Evaluator(reader, sheet.culture) });
        }
        const ready: SheetCell[] = [];
        for (const [cell, count] of waiting) if (count === 0) ready.push(cell);
        for (let head = 0; head < ready.length; head++) {
            const target = defined(ready[head], 'a ready cell');
            if (!stagedOf(target.sheet).has(target.address.key)) compute(target);
            for (const dependent of defined(edges.get(target), 'a cell\'s edges')) {
                const left = defined(waiting.get(dependent), 'a waiting count') - 1;
                waiting.set(dependent, left);
                if (left === 0) ready.push(dependent);
            }
        }
        for (const [target, count] of waiting) {
            if (count > 0) {
                const sheetStaged = stagedOf(target.sheet);
                if (!sheetStaged.has(target.address.key)) recalculatedOf(target.sheet).push(target.address);
                sheetStaged.set(target.address.key, Value.fromError('#CIRC!'));
            }
        }

        // 3. Publish, all at once.
        const result = new Map<Sheet, SheetChange>();
        for (const sheet of workbook.sheets) {
            const change = sheet.#publish(stagedOf(sheet), arraysOf(sheet), recalculatedOf(sheet), defined(rows.get(sheet), 'a Sheet\'s rows'));
            if (change !== undefined) result.set(sheet, change);
        }
        return result;
    }

    /** Publishes what a pass computed on this Sheet: the Anchors' arrays, then every staged Value. `undefined` when the pass neither reached the Sheet nor holds rows for it. */
    #publish(staged: Map<number, Value | undefined>, arrays: Map<number, ValueArray | undefined>, recalculated: CellAddress[], rows: Set<number>): SheetChange | undefined {
        if (staged.size === 0 && arrays.size === 0 && recalculated.length === 0 && rows.size === 0) return undefined;
        for (const [key, array] of arrays) {
            const anchor = this.#cells.get(key);
            if (anchor !== undefined) {
                anchor.array = array;
                if (array === undefined) anchor.spillBlocked = false;
            }
        }
        const valueChanges: CellAddress[] = [];
        for (const [key, value] of staged) {
            const cell = this.#cells.get(key);
            const old = cell?.value;
            if (cell !== undefined) {
                cell.value = value;
                if (cell.isEmpty) this.#cells.delete(key);
            }
            if (!Value.unchanged(old, value)) {
                const address = CellAddress.fromKey(key);
                valueChanges.push(address);
                rows.add(address.row);
            }
        }
        valueChanges.sort((a, b) => a.compareTo(b));
        recalculated.sort((a, b) => a.compareTo(b));
        return new SheetChange(valueChanges, recalculated, distinctNumbers(rows));
    }

    /**
     * A Formula that reads a cell in a cycle is `#CIRC!` whatever it computes; one that reads a waiting cell, or
     * calls `TODAY` before the Sheet Day is known, or draws before the Sheet has a seed, waits; and one that reads a
     * refused cell, or a Linked Table's column that holds a refusal, is refused (ADR-0047, ADR-0049, ADR-0121,
     * ADR-0180). A cycle comes before a wait, and a wait before a refusal.
     */
    #taint(node: Node, reader: StagedReader): Value | undefined {
        let gettingData = this.#readsWaitingTable(node) || this.#waitsForToday(node) || this.#waitsForNow(node) || this.#waitsForSeed(node);
        let refused = this.#readsRefusingTable(node);
        for (const reference of node.references()) {
            // A 3D Reference reads its rectangle on every Sheet of its span (ADR-0331 §7).
            let targets: readonly CellReader[];
            if (reference.isThreeD) {
                targets = reader.resolveSpan(defined(reference.sheetName, 'a span\'s first Sheet'), defined(reference.lastSheetName, 'a span\'s last Sheet')) ?? []; // oxlint-disable-line ex-grid/no-stand-in-default -- a span an end of which no Sheet has reads no Sheet (ADR-0331 §7)
            } else {
                const one = reader.resolve(reference.sheetName);
                targets = one === undefined ? [] : [one];
            }
            for (const target of targets) {
                if (!(target instanceof StagedReader)) continue;
                for (const address of target.sheet.cellsIn(reference.area)) {
                    const value = target.read(address);
                    if (value?.isError !== true) continue;
                    if (value.error === '#CIRC!') return value;
                    if (value.error === '#GETTING_DATA') gettingData = true;
                    if (value.isRefused) refused = true;
                }
            }
        }
        return gettingData ? Value.fromError('#GETTING_DATA') : refused ? Value.refusal : undefined;
    }

    // ---- Spilled arrays (Sheet.Spill.cs) ----

    /**
     * Decides where every array spills, Anchors in address order: an Anchor whose Spill Range passes the Sheet's
     * edge, or covers a cell holding an Entry or another Anchor's spill, shows `#SPILL!` and spills nothing. Cells
     * no Anchor spills into any more are released. The cells whose Value the layout changed, Anchors included.
     */
    #layOutSpills(): CellAddress[] {
        const changed: CellAddress[] = [];
        const set = (cell: Cell, value: Value | undefined): void => {
            if (Value.unchanged(cell.value, value)) return;
            cell.value = value;
            changed.push(cell.address);
        };
        const claimed = new Map<number, { anchor: CellAddress; value: Value }>();
        const anchors = [...this.#cells.values()].filter(c => c.array !== undefined).toSorted((a, b) => a.address.compareTo(b.address));
        for (const anchor of anchors) {
            if (anchor.entry?.parsed === undefined) {
                anchor.array = undefined;
                anchor.spillBlocked = false;
                continue;
            }
            const array = defined(anchor.array, 'the Anchor\'s array');
            const origin = anchor.address;
            let blocked = origin.row + array.rows > rowCount || origin.column + array.columns > columnCount;
            for (let r = 0; r < array.rows && !blocked; r++) {
                for (let c = 0; c < array.columns && !blocked; c++) {
                    if (r === 0 && c === 0) continue;
                    const key = new CellAddress(origin.row + r, origin.column + c).key;
                    blocked = claimed.has(key) || this.#cells.get(key)?.entry !== undefined;
                }
            }
            // Whether it spills is what A1# reads: a change of it is a change, whatever the Value shows.
            if (anchor.spillBlocked !== blocked) changed.push(anchor.address);
            anchor.spillBlocked = blocked;
            set(anchor, blocked ? Value.fromError('#SPILL!') : array.at(0, 0) ?? Value.fromNumber(0));
            if (blocked) continue;
            for (let r = 0; r < array.rows; r++) {
                for (let c = 0; c < array.columns; c++) {
                    if (r === 0 && c === 0) continue;
                    claimed.set(new CellAddress(origin.row + r, origin.column + c).key, { anchor: origin, value: array.at(r, c) ?? Value.fromNumber(0) });
                }
            }
        }
        for (const cell of [...this.#cells.values()].filter(c => c.spilledFrom !== undefined && !claimed.has(c.address.key))) {
            cell.spilledFrom = undefined;
            if (cell.entry === undefined) set(cell, undefined);
            if (cell.isEmpty) this.#cells.delete(cell.address.key);
        }
        for (const [key, { anchor, value }] of claimed) {
            const cell = this.#cellFor(CellAddress.fromKey(key));
            cell.spilledFrom = anchor;
            set(cell, value);
        }
        changed.sort((a, b) => a.compareTo(b));
        return changed;
    }

    /** The Anchor whose array gives a cell its Value, or `undefined` for a cell no array spills into (ADR-0125). */
    spilledFrom(address: CellAddress): CellAddress | undefined {
        return this.#cells.get(address.key)?.spilledFrom;
    }

    // ---- Volatile functions, the Sheet Day and the draws (Sheet.Volatile.cs, Sheet.Today.cs, Sheet.Random.cs) ----

    /** The moment read at the start of the recalculation under way. @internal */
    get moment(): SheetMoment | undefined {
        return this.#moment;
    }

    /** Whether any Formula of this Sheet calls a volatile function. @internal */
    get hasVolatileFormulas(): boolean {
        return this.#volatileFormulas.size > 0;
    }

    /** One recalculation of `cells`, on any Sheet of the Workbook, started from this Sheet, with every volatile Formula: what it changed here. @internal */
    recalculateCells(cells: SheetCellSet): SheetChange {
        return this.#recalculate(cells, new Set());
    }

    #waitsForNow(node: Node): boolean {
        return this.#moment === undefined && [...node.calls()].some(call => call.function !== undefined && call.name === 'NOW');
    }

    /** The Formula cells of this Sheet that call `TODAY`. @internal */
    get todayReaders(): SheetCell[] {
        return [...this.#todayReaders].map(key => new SheetCell(this, CellAddress.fromKey(key)));
    }

    #waitsForToday(node: Node): boolean {
        return this.workbook.today === undefined && callsToday(node);
    }

    /** The Formula cells of this Sheet that call `RAND` or `RANDBETWEEN`. @internal */
    get randomReaders(): SheetCell[] {
        return [...this.#randomReaders].map(key => new SheetCell(this, CellAddress.fromKey(key)));
    }

    /** Does `operation` as one operation of the Workbook. @internal */
    asOneChange<T>(operation: () => T): T {
        return this.workbook.asOneChange(operation);
    }

    /** Counts a change of the Workbook. @internal */
    countChange(): void {
        this.workbook.countChange();
    }

    /** The draw for the call at `place` in the Formula of `cell`, or `undefined` without a seed (ADR-0180). @internal */
    draw(cell: CellAddress, place: number): bigint | undefined {
        const seed = this.workbook.seed;
        return seed === undefined ? undefined : drawOf(seed, this.#name, cell, place, BigInt(this.workbook.changes));
    }

    #waitsForSeed(node: Node): boolean {
        return this.workbook.seed === undefined && callsRandom(node);
    }

    // ---- Typing aids (FormulaEntry.cs) ----

    /**
     * Completion for the Formula being typed (ADR-0051, ADR-0058): the declared functions and the Workbook's Linked
     * Tables whose names begin with the name at the caret, the columns of one of its tables after `Table[`, and an
     * argument's values where it takes one of a fixed list (`completeFormula`).
     */
    complete(text: string, caret: number): FormulaCompletion | undefined {
        const tables = [...this.workbook.tables.values()].toSorted((a, b) => a.order - b.order);
        return completeFormula(text, caret, tables.map(t => t.name),
            name => this.workbook.tables.get(ignoreCaseKey(name))?.columns);
    }

    /**
     * The References in the Formula being edited (ADR-0057), a qualifier naming this Sheet by its name as it is now
     * (`formulaReferences`). A structured reference is kept only when its Linked Table is declared with that
     * column: one naming a table or a column that is not names nothing, as a Reference to another Sheet does not. A
     * declared table still waiting for its data is kept.
     */
    references(text: string): readonly FormulaReference[] {
        return formulaReferences(text, this.#name).filter(r => r.linkedColumn === undefined || this.#isDeclared(r.linkedColumn));
    }

    #isDeclared(column: LinkedTableColumn): boolean {
        return this.workbook.tables.get(ignoreCaseKey(column.table))?.columnIndex.has(ignoreCaseKey(column.column)) === true;
    }

    // ---- Linked Tables (Sheet.LinkedTables.cs) ----

    /** The Linked Tables declared on the Sheet's Workbook, in the order they were declared. @internal */
    get linkedTables(): LinkedTable[] {
        return [...this.workbook.tables.values()].toSorted((a, b) => a.order - b.order)
            .map(t => new LinkedTable(t.name, t.columns, t.data === undefined, t.rowCount, t.key));
    }

    /** Declares a Linked Table on the Workbook: what it changed on this Sheet. @internal */
    declareLinkedTable(name: string, columns: readonly string[], key: readonly string[] = []): SheetChange {
        const why = Sheet.whyNotALinkedTable(name, columns);
        if (why !== undefined) throw new RangeError(why);
        const whyNotKey = Sheet.whyNotAKey(name, columns, key);
        if (whyNotKey !== undefined) throw new RangeError(whyNotKey);
        const tables = this.workbook.tables;
        const held = tables.get(ignoreCaseKey(name));
        if (held !== undefined) {
            // The same declaration again changes nothing; other columns or another key replace it (ADR-0049).
            if (sameStrings(held.columns, columns) && sameStrings(held.key, keyAsDeclared(columns, key))) return SheetChange.none;
            tables.delete(ignoreCaseKey(name));
            this.declare(name, columns, key, held.order);
        } else {
            this.declare(name, columns, key);
        }
        // A declaration that takes effect is a change, as a snapshot is (ADR-0180, 2026-10-11).
        this.countChange();
        return this.#recalculateReaders(name);
    }

    /** Records a Linked Table's declaration on the Workbook, checked already. @internal */
    declare(name: string, columns: readonly string[], key: readonly string[], order?: number): void {
        const tables = this.workbook.tables;
        const index = new Map<string, number>();
        columns.forEach((column, i) => index.set(ignoreCaseKey(column), i));
        const next = tables.size === 0 ? 0 : Math.max(...[...tables.values()].map(t => t.order)) + 1;
        tables.set(ignoreCaseKey(name), new TableState(name, [...columns], index, keyAsDeclared(columns, key), order ?? next));
    }

    /** Why a name and columns cannot be declared as a Linked Table, or `undefined` when they can. @internal */
    static whyNotALinkedTable(name: string, columns: readonly string[]): string | undefined {
        if (!isTableName(name)) return `'${name}' is not a name a Linked Table can have.`;
        if (columns.length === 0) return 'A Linked Table has at least one column.';
        const seen = new Set<string>();
        for (const column of columns) {
            if (column.length === 0) return 'A column name is empty.';
            const key = ignoreCaseKey(column);
            if (seen.has(key)) return `The column '${column}' is declared twice.`;
            seen.add(key);
        }
        return undefined;
    }

    /** Why a key cannot be declared with a table's columns, or `undefined` when it can (ADR-0049, 2026-09-30). @internal */
    static whyNotAKey(name: string, columns: readonly string[], key: readonly string[]): string | undefined {
        const seen = new Set<string>();
        for (const column of key) {
            if (!columns.some(c => equalsIgnoreCase(c, column))) return `The key '${column}' is not one of the columns of '${name}'.`;
            const k = ignoreCaseKey(column);
            if (seen.has(k)) return `The key column '${column}' of '${name}' is named twice.`;
            seen.add(k);
        }
        return undefined;
    }

    /** The declarations, as a Sheet Document records them (ADR-0049). @internal */
    get tableDeclarations(): SheetDocumentTable[] {
        return [...this.workbook.tables.values()].toSorted((a, b) => a.order - b.order).map(t => new SheetDocumentTable(t.name, t.columns, t.key));
    }

    /** Takes a Linked Table's snapshot: what it changed on this Sheet. @internal */
    pushLinkedTable(name: string, rows: Iterable<readonly (Value | undefined)[]>): SheetChange {
        const table = this.workbook.tables.get(ignoreCaseKey(name));
        if (table === undefined) throw new Error(`No Linked Table named '${name}' is declared.`);
        const width = table.columns.length;
        const columns: (Value | undefined)[][] = Array.from({ length: width }, () => []);
        let count = 0;
        for (const row of rows) {
            if (row.length !== width) throw new RangeError(`Row ${String(count)} does not hold one Value for each of the ${String(width)} columns of '${table.name}'.`);
            for (let c = 0; c < width; c++) {
                const value = row[c];
                if (value?.isError === true && (value.error === '#GETTING_DATA' || value.error === '#CIRC!')) {
                    throw new RangeError(`${value.error} is not data a Linked Table can hold.`);
                }
                defined(columns[c], 'a column').push(value);
            }
            count++;
        }
        // A snapshot is a change, refused or taken: what the Sheet's Formulas read changes (ADR-0180).
        this.countChange();
        if (table.key.length > 0) {
            const parts = table.key.map(k => defined(columns[defined(table.columnIndex.get(ignoreCaseKey(k)), 'a key column\'s index')], 'a key column'));
            const repeat = firstRepeat(parts);
            if (repeat !== undefined) {
                // The table waits again; the previous snapshot is not kept: it would show an older value (ADR-0049, 2026-09-30).
                table.data = undefined;
                table.rowCount = 0;
                const waiting = this.#recalculateReaders(table.name);
                throw new RepeatedKeyError(table.name, table.key, parts.map(p => defined(p[repeat.first], 'a key')), repeat.first,
                    parts.map(p => defined(p[repeat.second], 'a key')), repeat.second, waiting);
            }
        }
        // The snapshot is built whole before it replaces the old one.
        table.data = columns;
        table.refusing = columns.map(c => c.some(v => v?.isRefused === true));
        table.rowCount = count;
        return this.#recalculateReaders(table.name);
    }

    /** Recalculates every Formula that reads the table, on any Sheet of the Workbook (ADR-0331). */
    #recalculateReaders(name: string): SheetChange {
        const readers = new SheetCellSet();
        for (const sheet of this.workbook.sheets) {
            const cells = sheet.#tableReaders.get(ignoreCaseKey(name));
            if (cells !== undefined) for (const key of cells) readers.add(new SheetCell(sheet, CellAddress.fromKey(key)));
        }
        // Nothing reads the table, but a volatile Formula is recalculated whatever the change (ADR-0124).
        if (readers.size === 0 && !this.workbook.sheets.some(s => s.hasVolatileFormulas)) return SheetChange.none;
        return this.#recalculate(readers, new Set());
    }

    /** What a structured reference reads (ADR-0049). @internal */
    tableColumn(name: string, column: string): Operand {
        const table = this.workbook.tables.get(ignoreCaseKey(name));
        if (table === undefined) return Operand.ofError('#NAME?');
        if (table.data === undefined) return Operand.ofError('#GETTING_DATA');
        const index = table.columnIndex.get(ignoreCaseKey(column));
        return index === undefined ? Operand.ofError('#REF!') : Operand.ofColumn(defined(table.data[index], 'a column'));
    }

    /** Whether a Linked Table's column holds a refusal (ADR-0047; 2026-10-12, decided with the user). @internal */
    columnHoldsRefusal(name: string, column: string): boolean {
        const table = this.workbook.tables.get(ignoreCaseKey(name));
        if (table?.data === undefined) return false;
        const index = table.columnIndex.get(ignoreCaseKey(column));
        return index !== undefined && table.refusing[index] === true;
    }

    #readsRefusingTable(node: Node): boolean {
        for (const reference of node.structuredReferences()) {
            if (this.columnHoldsRefusal(reference.table, reference.column)) return true;
        }
        return false;
    }

    #readsWaitingTable(node: Node): boolean {
        for (const reference of node.structuredReferences()) {
            const table = this.workbook.tables.get(ignoreCaseKey(reference.table));
            if (table !== undefined && table.data === undefined) return true;
        }
        return false;
    }

    #registerTableReaders(formulaCell: CellAddress, formula: Node): void {
        for (const table of tablesRead(formula)) {
            let set = this.#tableReaders.get(table);
            if (set === undefined) {
                set = new Set();
                this.#tableReaders.set(table, set);
            }
            set.add(formulaCell.key);
        }
    }

    #unregisterTableReaders(formulaCell: CellAddress, formula: Node): void {
        for (const table of tablesRead(formula)) {
            const set = this.#tableReaders.get(table);
            if (set?.delete(formulaCell.key) === true && set.size === 0) this.#tableReaders.delete(table);
        }
    }

    // ---- The name (Sheet.Name.cs) ----

    /**
     * The Sheet's name (ADR-0046). A Reference qualified with it (`Sheet1!A1`, matched without regard to case) names
     * this Sheet's cells.
     */
    get name(): string {
        return this.#name;
    }

    /** Gives the Sheet a new name; the Workbook checks it and rewrites the References that name the Sheet. @internal */
    setName(name: string): void {
        this.#name = name;
    }

    /**
     * Why `name` is not a name a Sheet can have, as Excel decides one — 1 to 31 characters, none of `: \ / ? * [ ]`,
     * not beginning or ending with `'`, and not `History` — or `undefined` when it is one.
     */
    static whyNotAName(name: string): string | undefined {
        if (name.length === 0) return "A Sheet's name cannot be empty.";
        if (name.length > Sheet.maximumNameLength) return `A Sheet's name is at most ${String(Sheet.maximumNameLength)} characters.`;
        if (/[:\\/?*[\]]/u.test(name)) return "A Sheet's name cannot contain any of : \\ / ? * [ ].";
        if (name.startsWith("'") || name.endsWith("'")) return "A Sheet's name cannot begin or end with an apostrophe.";
        if (equalsIgnoreCase(name, 'History')) return "'History' is a name Excel reserves.";
        return undefined;
    }

    /** Whether `name` is a name a Sheet can have (`whyNotAName`). */
    static isValidName(name: string): boolean {
        return Sheet.whyNotAName(name) === undefined;
    }

    /** `name`, checked: throws a RangeError for a name a Sheet cannot have. @internal */
    static checkName(name: string): string {
        const reason = Sheet.whyNotAName(name);
        if (reason !== undefined) throw new RangeError(reason);
        return name;
    }

    /** Renames the Sheet (ADR-0046) through its Workbook. @internal */
    rename(name: string): SheetChange {
        return this.applyRename(name).change;
    }

    /** Whether `qualifier`, a Reference's Sheet name, names this Sheet: no qualifier, or this Sheet's name without regard to case. @internal */
    isThisSheet(qualifier: string | undefined): boolean {
        return Sheet.names(qualifier, this.#name);
    }

    /** Whether `qualifier` names the Sheet called `name`: no qualifier, or that name without regard to case. @internal */
    static names(qualifier: string | undefined, name: string): boolean {
        return qualifier === undefined || equalsIgnoreCase(qualifier, name);
    }

    /** Whether a Reference names this Sheet's cells alone: a 3D Reference never does (ADR-0331 §7). @internal */
    isLocal(reference: Reference): boolean {
        return !reference.isThreeD && this.isThisSheet(reference.sheetName);
    }

    /** A typed Formula with every qualifier that names this Sheet written in the Sheet's own name (TEXT-083). The same instance when nothing changes. @internal */
    inOwnName(entry: Entry): Entry {
        if (entry.parsed === undefined || entry.formula?.includes('!') !== true) return entry;
        const other = (qualifier: string): boolean => this.isThisSheet(qualifier) && qualifier !== this.#name;
        return Entry.rewrite(entry,
            r => r.with({
                sheetName: r.sheetName !== undefined && other(r.sheetName) ? this.#name : r.sheetName,
                lastSheetName: r.lastSheetName !== undefined && other(r.lastSheetName) ? this.#name : r.lastSheetName,
            }),
            q => (other(q) ? this.#name : q));
    }

    /** A typed Formula with every structured reference to a declared Linked Table written in the declared case (ticket 180). The same instance when nothing changes. @internal */
    inDeclaredCase(entry: Entry): Entry {
        const formula = entry.formula;
        if (entry.parsed === undefined || formula?.includes('[') !== true) return entry;
        const rewritten = rewriteStructuredReferences(formula, (table, column) => {
            const declared = this.workbook.tables.get(ignoreCaseKey(table));
            const index = declared?.columnIndex.get(ignoreCaseKey(column));
            return declared === undefined || index === undefined ? undefined : { table: declared.name, column: defined(declared.columns[index], 'a column') };
        });
        return rewritten === formula ? entry : Entry.fromFormula(rewritten);
    }

    /** Renames the Sheet through its Workbook, which rewrites every Sheet's References to it (ADR-0331). @internal */
    applyRename(name: string): SheetRename {
        Sheet.checkName(name);
        return this.workbook.renameSheet(this, name);
    }

    /** Undoes a rename. @internal */
    undoRename(rename: SheetRename): SheetChange {
        return this.workbook.undoRename(this, rename);
    }

    /** Every Formula cell and its Entry. @internal */
    formulaEntries(): { address: CellAddress; entry: Entry }[] {
        const list: { address: CellAddress; entry: Entry }[] = [];
        for (const cell of this.#cells.values()) {
            if (cell.entry?.parsed !== undefined) list.push({ address: cell.address, entry: cell.entry });
        }
        return list;
    }

    /** The Formula cells with a qualified Reference. @internal */
    qualifiedFormulas(): CellAddress[] {
        const list: CellAddress[] = [];
        for (const cell of this.#cells.values()) {
            const parsed = cell.entry?.parsed;
            if (parsed !== undefined && [...parsed.references()].some(r => r.sheetName !== undefined)) list.push(cell.address);
        }
        return list;
    }

    /** Puts an Entry in a cell without recalculating or registering it. @internal */
    replaceEntry(address: CellAddress, entry: Entry): void {
        this.#cellFor(address).entry = entry;
    }

    /** Forgets every Reference this Sheet's Formulas make to another Sheet, on the Sheets they name. @internal */
    unregisterAllRemote(): void {
        for (const formulaCell of this.#remoteReferences.keys()) this.#unregisterRemote(CellAddress.fromKey(formulaCell));
    }

    // ---- The format a Formula gives on entry (Sheet.FormulaFormat.cs) ----

    /**
     * The format a Formula gives a General cell when it is entered, as Excel's does (ADR-0047), or `undefined` for
     * none: the smallest rule consistent with every case the corpus has observed Excel to answer.
     */
    #formatOnEntry(formula: Node): NumberFormat | undefined {
        const operand = (node: Node): NumberFormat | undefined => {
            switch (node.kind) {
                case 'reference': {
                    // A 3D Reference gives none (ADR-0331 §7).
                    if (node.reference.isThreeD) return undefined;
                    const owner = this.sheetNamed(node.reference.sheetName);
                    if (owner === undefined) return undefined;
                    const given = owner.getNumberFormat(new CellAddress(node.reference.row1, node.reference.column1));
                    return given.showsNumbersAsGeneral ? undefined : given;
                }
                case 'parentheses':
                    return operand(node.inner);
                case 'unary':
                    return operand(node.operand);
                case 'binary': {
                    const spine = node.spine;
                    let left = operand(node.leftmost);
                    for (const b of spine) left = binary(b.operator, b.left, b.right, left, operand(b.right));
                    return left;
                }
                case 'function':
                    if (node.name === 'DATE' || node.name === 'TODAY') return NumberFormat.shortDate;
                    if (node.name === 'NOW') return NumberFormat.shortDateTime;
                    for (const argument of node.arguments) {
                        const format = operand(argument);
                        if (format !== undefined) return format;
                    }
                    return undefined;
                case 'boolean':
                case 'error':
                case 'intersection':
                case 'let':
                case 'missing':
                case 'name':
                case 'number':
                case 'percent':
                case 'structuredReference':
                case 'text':
                    return undefined;
                default:
                    return unreachable(node);
            }
        };
        return simple(formula) ? operand(formula) : undefined;
    }

    // ---- Column widths (Sheet.Width.cs) ----

    /** The width recorded on the column, and its kind, or `undefined` when none is (ADR-0046). Throws for a column outside `A` to `XFD`. */
    getColumnWidth(column: number): SheetColumnWidth | undefined {
        return this.#columnWidths.get(checkColumn(column));
    }

    /** Sets the width of every column `columns` spans, as a width the user set; `undefined` puts them back at the default width (ADR-0046). @internal */
    setColumnWidth(columns: CellRange, width: number | undefined): SheetChange {
        const checked = Sheet.checkColumnWidth(width);
        return this.applyColumnWidth(columns, checked === undefined ? undefined : new SheetColumnWidth(checked, 'setByUser')).change;
    }

    /** Sets the width of every column `columns` spans, as a width an entry widened them to (ADR-0046, 2026-09-28). @internal */
    setAutomaticColumnWidth(columns: CellRange, width: number): SheetChange {
        return this.applyColumnWidth(columns, new SheetColumnWidth(defined(Sheet.checkColumnWidth(width), 'a width'), 'widenedByEntry')).change;
    }

    /** `width`, checked: more than 0 and at most `maxColumnWidth`, or `undefined`. @internal */
    static checkColumnWidth(width: number | undefined): number | undefined {
        if (width === undefined || (width > 0 && width <= Sheet.maxColumnWidth)) return width;
        throw new RangeError(`A column's width is more than 0 and at most ${String(Sheet.maxColumnWidth)} characters; a hidden column (width 0) is not supported.`);
    }

    /** A width set on columns, and the widths before it for undoing it. @internal */
    applyColumnWidth(columns: CellRange, width: SheetColumnWidth | undefined): { change: SheetChange; before: Map<number, SheetColumnWidth> } {
        const before = new Map(this.#columnWidths);
        for (let column = columns.first.column; column <= columns.last.column; column++) {
            if (width === undefined) this.#columnWidths.delete(column);
            else this.#columnWidths.set(column, width);
        }
        return { change: new SheetChange([], [], [], this.#widthsChangedSince(before)), before };
    }

    /** Puts every column's width back to `widths`, and says which columns that changed. @internal */
    restoreColumnWidths(widths: ReadonlyMap<number, SheetColumnWidth>): SheetChange {
        const now = this.#columnWidths;
        this.#columnWidths = new Map(widths);
        const columns = this.#widthsChangedSince(now);
        return columns.length === 0 ? SheetChange.none : new SheetChange([], [], [], columns);
    }

    /** A copy of the widths recorded now. @internal */
    columnWidthsNow(): Map<number, SheetColumnWidth> {
        return new Map(this.#columnWidths);
    }

    /** The columns whose width or its kind differs between `before` and now, ascending. */
    #widthsChangedSince(before: ReadonlyMap<number, SheetColumnWidth>): number[] {
        return distinctNumbers([...before.keys(), ...this.#columnWidths.keys()].filter(c => {
            const was = before.get(c);
            const now = this.#columnWidths.get(c);
            return !(was !== undefined && now !== undefined && was.equals(now));
        }));
    }

    /** The widths after an insertion or deletion of columns (ADR-0046; the seventeenth Windows run, b10). */
    #shiftColumnWidths(edit: StructuralEdit, formatInserted: boolean): void {
        if (edit.axis !== 'columns') return;
        const shifted = new Map<number, SheetColumnWidth>();
        for (const [column, width] of this.#columnWidths) {
            const moved = edit.move(new CellAddress(0, column));
            if (moved !== undefined) shifted.set(moved.column, width);
        }
        const left = edit.start > 0 ? this.#columnWidths.get(edit.start - 1) : undefined;
        if (edit.isInsert && formatInserted && left !== undefined) {
            const taken = new SheetColumnWidth(left.width, 'setByUser');
            for (let i = 0; i < edit.count; i++) shifted.set(edit.start + i, taken);
        }
        this.#columnWidths = shifted;
    }

    /** The widths as a Sheet Document records them: adjacent columns of one width and one kind are one run. */
    #widthRuns(): SheetDocumentColumnWidth[] {
        const list: SheetDocumentColumnWidth[] = [];
        for (const column of [...this.#columnWidths.keys()].toSorted((a, b) => a - b)) {
            const width = defined(this.#columnWidths.get(column), 'a width');
            const last = list.at(-1);
            if (last !== undefined && last.last === column - 1 && last.width === width.width && last.kind === width.kind) {
                list[list.length - 1] = new SheetDocumentColumnWidth(last.first, column, last.width, last.kind);
            } else {
                list.push(new SheetDocumentColumnWidth(column, column, width.width, width.kind));
            }
        }
        return list;
    }

    /**
     * The width, in characters, that the number typed into this cell needs, for widening a column whose width the
     * user has not set when the number is typed, as Excel does (ADR-0047); `undefined` when what the cell holds never
     * widens a column.
     */
    getWidthOnEntry(address: CellAddress): number | undefined {
        const cell = this.#cells.get(address.key);
        const entry = cell?.entry;
        if (cell === undefined || entry === undefined) return undefined;
        // A Formula's number widens the column as a typed one does (WD-007).
        const shown = entry.isFormula ? cell.value : entry.constant;
        if (shown?.kind !== 'number') return undefined;
        const number = shown.number;
        const format = this.getNumberFormat(address);
        if (!format.showsNumbersAsGeneral) {
            const { text, cannotShow } = format.format(shown, this.culture);
            return cannotShow ? undefined : text.length;
        }
        const widest = generalLimit + 1;
        if (isGeneralScientific(number, widest) === true) return defined(generalFitted(number, widest, this.culture), 'the scientific form').length;
        for (let characters = 1; characters <= widest; characters++) {
            if (isGeneralScientific(number, characters) === false) return characters;
        }
        return undefined;
    }

    /** The cells in `range` that hold an Entry, in no particular order (ADR-0071, case 17). */
    entryAddressesIn(range: CellRange): CellAddress[] {
        return [...this.cellsIn(new Area(range.first.row, range.first.column, range.last.row, range.last.column))]
            .filter(address => this.#cells.get(address.key)?.entry !== undefined);
    }

    // ---- Insertion and deletion (Sheet.Structure.cs) ----

    /** Inserts `count` rows at `row` (ADR-0046/0047). @internal */
    insertRows(row: number, count = 1): SheetChange {
        return this.restructure(new StructuralEdit('rows', row, count, true)).change;
    }

    /** Deletes `count` rows from `row` (ADR-0047). @internal */
    deleteRows(row: number, count = 1): SheetChange {
        return this.restructure(new StructuralEdit('rows', row, count, false)).change;
    }

    /** Inserts columns, as `insertRows` does rows (ADR-0046, ADR-0047, ADR-0071). @internal */
    insertColumns(column: number, count = 1): SheetChange {
        return this.restructure(new StructuralEdit('columns', column, count, true)).change;
    }

    /** Deletes columns, as `deleteRows` does rows (ADR-0046). @internal */
    deleteColumns(column: number, count = 1): SheetChange {
        return this.restructure(new StructuralEdit('columns', column, count, false)).change;
    }

    /** Whether `edit` would be refused, and why; nothing changes either way. @internal */
    checkStructural(edit: StructuralEdit): SheetRefusal | undefined {
        edit.validate();
        if (!edit.isInsert) return undefined;
        for (const cell of this.#cells.values()) {
            // Only an Entry stops an insertion, as in Excel.
            if (cell.entry !== undefined && edit.move(cell.address) === undefined) {
                return new SheetRefusal('entriesWouldLeaveSheet', `${capitalise(edit.describe())} would push ${cell.address.toString()}, which holds an Entry, off the Sheet.`);
            }
        }
        return undefined;
    }

    /** Gives each cell of the inserted rows (columns) the Cell Format of the cell before the insertion, all but its Borders (ADR-0046, ADR-0071). */
    #formatInserted(edit: StructuralEdit, moved: readonly Cell[]): void {
        const rows = edit.axis === 'rows';
        for (const source of moved) {
            if ((rows ? source.address.row : source.address.column) !== edit.start - 1) continue;
            for (let i = 0; i < edit.count; i++) {
                const at = rows ? new CellAddress(edit.start + i, source.address.column) : new CellAddress(source.address.row, edit.start + i);
                const cell = new Cell(at);
                cell.takeFormatOf(source);
                cell.borders = undefined;
                if (cell.isFormatted) this.#cells.set(at.key, cell);
            }
        }
    }

    /** An insertion or deletion. `formatInserted`: whether inserted rows or columns take the Cell Format of the one before them (ADR-0046). @internal */
    restructure(edit: StructuralEdit, formatInserted = true): StructuralOutcome {
        const refusal = this.checkStructural(edit);
        if (refusal !== undefined) throw new SheetRefusedError(refusal);
        this.countChange();

        const before = this.snapshot();
        const shownBefore = this.#shownSnapshot();
        const rowsBefore = new Map(this.#rowFormats);
        const columnsBefore = new Map(this.#columnFormats);
        const widthsBefore = this.columnWidthsNow();
        const dropped: AddressedState[] = [];
        const rewritten: { address: CellAddress; entry: Entry }[] = [];
        const moved: Cell[] = [];
        const dirty = new Set<number>();
        for (const cell of this.#cells.values()) {
            // A spilled Value is its Anchor's to lay out again from wherever the Anchor moves (ADR-0125).
            if (cell.entry === undefined && cell.spilledFrom !== undefined && !cell.isFormatted) continue;
            const to = edit.move(cell.address);
            if (to === undefined) {
                dropped.push({ address: cell.address, state: defined(before.get(cell.address.key), 'the cell\'s state') });
                continue;
            }
            let entry = cell.entry;
            const parsed = entry?.parsed;
            // ROW() and COLUMN() read their own cell's place, which a move changes without a word of the Formula's.
            if (parsed !== undefined && !to.equals(cell.address) && readsOwnPlace(parsed)) dirty.add(to.key);
            if (entry !== undefined && parsed !== undefined) {
                const mapped = Entry.rewrite(entry, r => edit.map(r, this));
                if (mapped !== entry) {
                    rewritten.push({ address: cell.address, entry });
                    dirty.add(to.key);
                } else if ([...parsed.references()].some(r => edit.reaches(r, this) || this.workbook.spans(r, this))) {
                    // A:A kept its text, but what it covers moved underneath it; so did what a 3D Reference spanning this Sheet reads here (ADR-0331 §7).
                    dirty.add(to.key);
                }
                entry = mapped;
            }
            const movedCell = new Cell(to);
            movedCell.entry = entry;
            movedCell.value = entry === undefined ? undefined : cell.value;
            if (cell.array !== undefined) dirty.add(to.key);
            movedCell.takeFormatOf(cell);
            moved.push(movedCell);
        }

        const elsewhere = this.#rewriteElsewhere(edit);
        const elsewhereBefore = new Map<Sheet, Map<number, CellState>>();
        for (const r of elsewhere.rewritten) if (!elsewhereBefore.has(r.sheet)) elsewhereBefore.set(r.sheet, r.sheet.snapshot());

        this.#cells.clear();
        for (const cell of moved) this.#cells.set(cell.address.key, cell);
        if (edit.isInsert && formatInserted && edit.start > 0) this.#formatInserted(edit, moved);
        this.#shiftAxisFormats(edit, formatInserted);
        this.#shiftColumnWidths(edit, formatInserted);
        this.rebuildDependencies();
        for (const r of elsewhere.rewritten) r.sheet.#reregister(r.address, r.entry);
        const changed = new SheetCellSet([...dirty].map(key => new SheetCell(this, CellAddress.fromKey(key))));
        changed.addAll(elsewhere.dirty);
        // A volatile Formula is recalculated whatever the change (ADR-0124).
        const recalculated = changed.size === 0 && !this.workbook.sheets.some(s => s.hasVolatileFormulas) ? [] : this.#recalculate(changed, new Set()).recalculated;
        // The Formulas rewritten on another Sheet changed there, whatever their Values did.
        for (const [other, snapshot] of elsewhereBefore) this.workbook.collect(other, other.diff(snapshot, []));
        // Rows and columns moved: two cells' sides meet on a new edge, from cells that may hold nothing.
        const change = SheetChange.merge([this.diff(before, recalculated, shownBefore), new SheetChange([], [], [], this.#widthsChangedSince(widthsBefore))])
            .reformattingUnnamedRows();
        return {
            change, dropped, rewritten, rowsBefore, columnsBefore, widthsBefore,
            rewrittenElsewhere: elsewhere.rewritten.map(r => ({ sheet: r.sheet, address: r.address, entry: r.before })),
        };
    }

    /** What an insertion or deletion on this Sheet does to the Formulas of the Workbook's other Sheets (ADR-0331, ADR-0046). Nothing changes yet. */
    #rewriteElsewhere(edit: StructuralEdit): { rewritten: { sheet: Sheet; address: CellAddress; entry: Entry; before: Entry }[]; dirty: SheetCellSet } {
        const rewritten: { sheet: Sheet; address: CellAddress; entry: Entry; before: Entry }[] = [];
        const dirty = new SheetCellSet();
        const mine = (r: Reference): boolean => r.sheetName !== undefined && this.isThisSheet(r.sheetName);
        for (const other of this.workbook.sheets) {
            if (other === this) continue;
            for (const { address, entry } of other.formulaEntries()) {
                if (entry.formula?.includes('!') !== true) continue;
                const mapped = Entry.rewrite(entry, r => (mine(r) ? edit.map(r, this) : r));
                if (mapped !== entry) {
                    rewritten.push({ sheet: other, address, entry: mapped, before: entry });
                    dirty.add(new SheetCell(other, address));
                } else if ([...defined(entry.parsed, 'a Formula').references()].some(r => (mine(r) && edit.reaches(r, this)) || this.workbook.spans(r, this))) {
                    dirty.add(new SheetCell(other, address));
                }
            }
        }
        return { rewritten, dirty };
    }

    /** Puts a rewritten Formula in a cell of this Sheet, its References registered again; nothing is recalculated. */
    #reregister(address: CellAddress, entry: Entry): void {
        const cell = defined(this.#cells.get(address.key), 'the rewritten cell');
        const old = cell.entry?.parsed;
        if (old !== undefined) this.#unregister(address, old);
        cell.entry = entry;
        if (entry.parsed !== undefined) this.#register(address, entry.parsed);
    }

    /** Puts back, on the Workbook's other Sheets, the Formulas an insertion or deletion on this Sheet rewrote, and recalculates them: what it changed here. */
    #putBackElsewhere(rewritten: readonly RewrittenEntry[]): SheetChange {
        if (rewritten.length === 0) return SheetChange.none;
        const before = new Map<Sheet, Map<number, CellState>>();
        for (const r of rewritten) if (!before.has(r.sheet)) before.set(r.sheet, r.sheet.snapshot());
        for (const r of rewritten) r.sheet.#reregister(r.address, r.entry);
        const change = this.#recalculate(new SheetCellSet(rewritten.map(r => new SheetCell(r.sheet, r.address))), new Set());
        for (const [other, snapshot] of before) this.workbook.collect(other, other.diff(snapshot, []));
        return change;
    }

    // ---- Steps (Sheet.Steps.cs) ----

    /** Does `edit` as one operation and returns the step that undoes it (ADR-0048). @internal */
    do(edit: SheetEdit): SheetStep {
        const refusal = edit.check(this);
        if (refusal !== undefined) throw new SheetRefusedError(refusal);
        // However many steps the edit takes, it is one change (ADR-0180).
        return this.asOneChange(() => edit.apply(this));
    }

    /** Whether `do` would refuse `edit` as the Sheet stands, and why. @internal */
    check(edit: SheetEdit): SheetRefusal | undefined {
        return edit.check(this);
    }

    /** What each of `addresses` records: Entry and Cell Format, never a Value. @internal */
    record(addresses: Iterable<CellAddress>): AddressedState[] {
        return [...addresses].map(address => ({ address, state: this.#stateAt(address).recorded }));
    }

    /** Puts cells back to what they recorded, as one change with one recalculation. A changed top or bottom side names the row across it too. @internal */
    restore(states: Iterable<AddressedState>): SheetChange {
        const list = [...states];
        const rows = new Set<number>();
        for (const { address, state } of list) {
            let cell = this.#cells.get(address.key);
            const existing = cell !== undefined;
            cell ??= new Cell(address);
            if (state.sameFormat(cell)) continue;
            if (!samePart(cell.borders, state.borders)) {
                const inherited = this.#inherited(address).borders;
                for (const row of rowsAcross(address, cell.borders ?? inherited, state.borders ?? inherited)) rows.add(row);
            }
            cell.numberFormat = state.numberFormat;
            cell.alignment = state.alignment;
            cell.font = state.font;
            cell.fill = state.fill;
            cell.borders = state.borders;
            rows.add(address.row);
            if (!existing) this.#cells.set(address.key, cell);
            else if (cell.isEmpty && cell.value === undefined) this.#cells.delete(address.key);
        }
        const change = this.setEntries(list.map(p => [p.address, p.state.entry] as const));
        // A cell given formatting only for the moment of the swap, and left with nothing, goes.
        for (const { address } of list) {
            const cell = this.#cells.get(address.key);
            if (cell?.isEmpty === true && cell.value === undefined) this.#cells.delete(address.key);
        }
        if (rows.size === 0) return change;
        for (const row of change.rows) rows.add(row);
        return new SheetChange(change.valueChanges, change.recalculated, distinctNumbers(rows));
    }

    /** Undoes a structural edit: the inverse edit, then the rewritten Formulas and the dropped cells put back. @internal */
    unrestructure(edit: StructuralEdit, outcome: StructuralOutcome): SheetChange {
        const before = this.snapshot();
        const shownBefore = this.#shownSnapshot();
        const widthsBefore = this.columnWidthsNow();
        const recalculated = [...this.restructure(edit.inverse, false).change.recalculated];
        this.#rowFormats = new Map(outcome.rowsBefore);
        this.#columnFormats = new Map(outcome.columnsBefore);
        this.#columnWidths = new Map(outcome.widthsBefore);
        const states: AddressedState[] = [];
        for (const { address, entry } of outcome.rewritten) states.push({ address, state: this.#stateAt(address).recorded.withEntry(entry) });
        for (const d of outcome.dropped) states.push({ address: d.address, state: d.state.recorded });
        recalculated.push(...this.restore(states).recalculated);
        recalculated.push(...this.#putBackElsewhere(outcome.rewrittenElsewhere).recalculated);
        return SheetChange.merge([this.diff(before, recalculated, shownBefore), new SheetChange([], [], [], this.#widthsChangedSince(widthsBefore))])
            .reformattingUnnamedRows();
    }

    // ---- The clipboard (Sheet.Clipboard.cs) ----

    /** Copies a rectangle (ADR-0048): its Entries for a paste inside ExSheet, and its Values for anywhere else. A rectangle holding a `#GETTING_DATA` Value is refused whole (ADR-0049). */
    copy(range: CellRange): SheetCopy {
        const area = new Area(range.first.row, range.first.column, range.last.row, range.last.column);
        let waiting: CellAddress | undefined;
        for (const address of this.cellsIn(area)) {
            const value = this.getValue(address);
            if (value?.isError === true && value.error === '#GETTING_DATA' && (waiting === undefined || address.compareTo(waiting) < 0)) waiting = address;
        }
        if (waiting !== undefined) {
            return new SheetCopy(new SheetRefusal('waitingForData',
                `${waiting.toString()} is waiting for a Linked Table's data (#GETTING_DATA); a copy would carry a wait that means nothing where it lands.`), undefined, undefined, undefined);
        }
        const states: CellState[] = [];
        let text = '';
        let html = `<table ${excelNamespace}>`;
        for (let row = range.first.row; row <= range.last.row; row++) {
            html += '<tr>';
            for (let column = range.first.column; column <= range.last.column; column++) {
                const address = new CellAddress(row, column);
                states.push(this.#spilledState(address, range) ?? this.#carriedState(address));
                if (column > range.first.column) text += '\t';
                const value = this.getValue(address);
                const raw = value?.toString() ?? ''; // oxlint-disable-line ex-grid/no-stand-in-default -- a blank cell copies as no text, as Excel's does (ADR-0005)
                const display = this.getDisplay(address);
                // What a cell that cannot be shown holds is its Value, never the #### it paints (ADR-0016).
                text += quoteForTsv(display.cannotShow ? raw : display.text);
                html += cellHtml(value, raw, this.getNumberFormat(address));
            }
            text += '\r\n';
            html += '</tr>';
        }
        html += '</table>';
        return new SheetCopy(undefined, new SheetBlock(range, states), text, html);
    }

    /** A spilled cell as a copy carries it (ADR-0125); `undefined` for a cell no array spills into. */
    #spilledState(address: CellAddress, range: CellRange): CellState | undefined {
        const anchor = this.spilledFrom(address);
        if (anchor === undefined) return undefined;
        const carried = this.#carriedState(address);
        const inside = range.contains(anchor);
        const value = this.getValue(address);
        if (inside || value === undefined || (value.isError && (value.error === '#GETTING_DATA' || value.error === '#CIRC!')) || value.isRefused) return carried;
        return carried.withEntry(Entry.fromValue(value));
    }

    /** The states a block pasted at `origin` writes; a blank cell of the block clears the cell it lands on. @internal */
    static *place(block: SheetBlock, origin: CellAddress): Generator<AddressedState> {
        const rows = origin.row - block.source.first.row;
        const columns = origin.column - block.source.first.column;
        for (let r = 0; r < block.rowCount; r++) {
            for (let c = 0; c < block.columnCount; c++) {
                const state = block.at(r, c);
                yield { address: new CellAddress(origin.row + r, origin.column + c), state: state.withEntry(state.entry?.movedBy(rows, columns)) };
            }
        }
    }

    /** A block of `rows` × `columns` at `origin` that would run past the Sheet's edge. @internal */
    static offSheet(origin: CellAddress, rows: number, columns: number): SheetRefusal | undefined {
        if (origin.row + rows <= rowCount && origin.column + columns <= columnCount) return undefined;
        return new SheetRefusal('blockWouldLeaveSheet', `A block of ${String(rows)} × ${String(columns)} at ${origin.toString()} would run past the Sheet's edge (XFD1048576).`);
    }

    // ---- The fill (Sheet.Fill.cs) ----

    /** What a Fill Intent writes, as Excel fills (ADR-0050, item 5), or why it is refused. `copyOnly`: Excel's fill keys. @internal */
    planFill(source: CellRange, target: CellRange, direction: FillDirection, copyOnly = false): FillPlan {
        const vertical = direction === 'down' || direction === 'up';
        const forward = direction === 'down' || direction === 'right';
        const states: AddressedState[] = [];
        let fillTarget = target;
        // The target may be given as the extension alone, or as the whole extended range.
        if (fillTarget.contains(source.first) && fillTarget.contains(source.last) && !fillTarget.equals(source)) {
            fillTarget = forward
                ? new CellRange(vertical ? new CellAddress(source.last.row + 1, fillTarget.first.column) : new CellAddress(fillTarget.first.row, source.last.column + 1), fillTarget.last)
                : new CellRange(fillTarget.first, vertical ? new CellAddress(source.first.row - 1, fillTarget.last.column) : new CellAddress(fillTarget.last.row, source.first.column - 1));
        }
        const adjacent = vertical
            ? fillTarget.first.column === source.first.column && fillTarget.last.column === source.last.column
              && (forward ? fillTarget.first.row === source.last.row + 1 : fillTarget.last.row === source.first.row - 1)
            : fillTarget.first.row === source.first.row && fillTarget.last.row === source.last.row
              && (forward ? fillTarget.first.column === source.last.column + 1 : fillTarget.last.column === source.first.column - 1);
        if (!adjacent) {
            return {
                refusal: new SheetRefusal('fillShapeNotSupported', `A fill ${direction} from ${source.toString()} extends it along one axis; ${fillTarget.toString()} does not.`),
                states, levels: undefined,
            };
        }

        const length = vertical ? source.rowCount : source.columnCount;
        const count = vertical ? fillTarget.rowCount : fillTarget.columnCount;
        // Lines that run the whole axis: a fill down from whole rows, or across from whole columns.
        const wholeAxis = vertical ? source.isWholeRows : source.isWholeColumns;
        const single = source.first.equals(source.last);
        const held: Cell[] = [];
        for (const cell of this.#cells.values()) {
            if (cell.entry !== undefined && source.contains(cell.address)) {
                held.push(cell);
                if (held.length === 2) break;
            }
        }
        // The source's one Entry, a number: it goes on as a series from a source of several cells.
        const loneNumber = !single && held.length === 1 && held[0]?.entry?.constant?.kind === 'number';
        // Every cell of a source of several cells holds a number, so a line of one cell copies its own.
        let allNumbers = !single && !wholeAxis;
        for (let r = 0; r < source.rowCount && allNumbers; r++) {
            for (let c = 0; c < source.columnCount && allNumbers; c++) {
                allNumbers = this.#stateAt(new CellAddress(source.first.row + r, source.first.column + c)).recorded.entry?.constant?.kind === 'number';
            }
        }
        for (const line of this.#linesToFill(source, fillTarget, vertical, wholeAxis)) {
            const sourceAt = (i: number): CellAddress => vertical
                ? new CellAddress(source.first.row + i, source.first.column + line)
                : new CellAddress(source.first.row + line, source.first.column + i);
            // Position along the axis relative to the source's first cell: 0..length-1 is the source.
            const cells = Array.from({ length }, (_, i) => this.#carriedState(sourceAt(i)));
            const { rule, refusal } = this.#fillRule(cells, sourceAt, copyOnly, wholeAxis, single, loneNumber, allNumbers);
            if (refusal !== undefined) return { refusal, states: [], levels: undefined };
            for (let k = 0; k < count; k++) {
                const x = forward ? length + k : -1 - k;
                const at = vertical
                    ? new CellAddress(source.first.row + x, source.first.column + line)
                    : new CellAddress(source.first.row + line, source.first.column + x);
                const i = ((x % length) + length) % length;
                states.push({ address: at, state: defined(cells[i], 'a source cell').withEntry(rule(x, i, at)) });
            }
        }
        return { refusal: undefined, states, levels: wholeAxis ? this.#levelsOf(source, vertical, forward, length, count) : undefined };
    }

    /** The level each column (row) of a fill from whole columns (whole rows) takes: the one recorded on the source column (row) it repeats. */
    #levelsOf(source: CellRange, vertical: boolean, forward: boolean, length: number, count: number): FillLevels {
        const along = vertical ? this.#rowFormats : this.#columnFormats;
        const first = vertical ? source.first.row : source.first.column;
        const levels: { index: number; level: AxisFormat }[] = [];
        for (let k = 0; k < count; k++) {
            const x = forward ? length + k : -1 - k;
            const i = ((x % length) + length) % length;
            levels.push({ index: first + x, level: along.get(first + i) ?? AxisFormat.empty });
        }
        return { columns: !vertical, levels };
    }

    /** Does a fill planned with levels: the target columns' (rows') formats first, then each cell's state settled against them, as one step. @internal */
    doFillWithLevels(plan: FillPlan): SheetStep {
        const levels = defined(plan.levels, 'the fill\'s levels');
        const addresses = distinctAddresses(plan.states.map(p => p.address));
        const before = this.record(addresses);
        const rowsBefore = new Map(this.#rowFormats);
        const columnsBefore = new Map(this.#columnFormats);
        const rowsAfter = new Map(this.#rowFormats);
        const columnsAfter = new Map(this.#columnFormats);
        for (const { index, level } of levels.levels) setAxis(levels.columns ? columnsAfter : rowsAfter, index, level);
        const change = this.#putLevelsAndCells(rowsAfter, columnsAfter, plan.states, true);
        const after = this.record(addresses);
        return new SheetStep(this, change,
            s => s.#putLevelsAndCells(rowsBefore, columnsBefore, before, false),
            s => s.#putLevelsAndCells(rowsAfter, columnsAfter, after, false));
    }

    /** Puts the row and column levels as given, then the cells, each settled against those levels when `settle`, as one change. */
    #putLevelsAndCells(rows: ReadonlyMap<number, AxisFormat>, columns: ReadonlyMap<number, AxisFormat>, cells: Iterable<AddressedState>, settleCells: boolean): SheetChange {
        const rowsNow = this.#rowFormats;
        const columnsNow = this.#columnFormats;
        this.#rowFormats = new Map(rows);
        this.#columnFormats = new Map(columns);
        const affected = new Set(this.#rowsAffectedByAxes(rowsNow, columnsNow));
        const levels = this.#levelsDiffer(rowsNow, columnsNow);
        const change = this.restore(settleCells ? this.settle(cells) : cells);
        if (affected.size === 0 && !levels) return change;
        for (const row of change.rows) affected.add(row);
        return new SheetChange(change.valueChanges, change.recalculated, distinctNumbers(affected), [], levels);
    }

    /** The lines of a fill that can write anything, ascending, counted from the source's first. */
    #linesToFill(source: CellRange, target: CellRange, vertical: boolean, wholeAxis: boolean): number[] {
        const lines = vertical ? source.columnCount : source.rowCount;
        const firstLine = vertical ? source.first.column : source.first.row;
        const alongFirst = Math.min(vertical ? source.first.row : source.first.column, vertical ? target.first.row : target.first.column);
        const alongLast = Math.max(vertical ? source.last.row : source.last.column, vertical ? target.last.row : target.last.column);
        const along = vertical ? this.#rowFormats : this.#columnFormats;
        const across = vertical ? this.#columnFormats : this.#rowFormats;
        // A format on a row (a column) the fill runs along reaches every line, unless the fill writes it on the target's own rows (columns).
        if (!wholeAxis && [...along.keys()].some(i => i >= alongFirst && i <= alongLast)) return Array.from({ length: lines }, (_, i) => i);
        const filled = new Set<number>();
        if (!wholeAxis) {
            for (const i of across.keys()) if (i >= firstLine && i < firstLine + lines) filled.add(i - firstLine);
        }
        for (const cell of this.#cells.values()) {
            const lineAt = vertical ? cell.address.column : cell.address.row;
            const alongAt = vertical ? cell.address.row : cell.address.column;
            if (lineAt >= firstLine && lineAt < firstLine + lines && alongAt >= alongFirst && alongAt <= alongLast) filled.add(lineAt - firstLine);
        }
        return distinctNumbers(filled);
    }

    /** The rule for one line: given a position along the axis, the source cell it repeats and its address, the Entry written there. */
    #fillRule(cells: readonly CellState[], sourceAt: (i: number) => CellAddress, copyOnly: boolean, wholeAxis: boolean, single: boolean, loneNumber: boolean,
        allNumbers: boolean): { rule: (x: number, i: number, at: CellAddress) => Entry | undefined; refusal: SheetRefusal | undefined } {
        const copy = (_x: number, i: number, at: CellAddress): Entry | undefined => {
            const from = sourceAt(i);
            return defined(cells[i], 'a source cell').entry?.movedBy(at.row - from.row, at.column - from.column);
        };
        const refuse = (refusal: SheetRefusal): { rule: typeof copy; refusal: SheetRefusal } => ({ rule: copy, refusal });
        if (copyOnly) return { rule: copy, refusal: undefined };

        let numbers = 0;
        let dates = 0;
        for (let i = 0; i < cells.length; i++) {
            const cell = defined(cells[i], 'a source cell');
            const constant = cell.entry?.constant;
            if (constant === undefined) continue;
            if (constant.kind === 'number') {
                numbers++;
                const format = defined(cell.numberFormat, 'a carried Number Format');
                if (format.isDate) {
                    if (!format.isDateOnly || cells.length > 1) {
                        return refuse(fillRefusal(sourceAt(i), format.isDateOnly
                            ? 'two or more dates, which Excel may step by month or year'
                            : 'a time of day, which Excel steps by the hour'));
                    }
                    dates++;
                }
            } else if (constant.kind === 'text' && this.#continuesAsSeries(constant.text)) {
                return refuse(fillRefusal(sourceAt(i), `'${constant.text}', text Excel would continue as a series`));
            }
        }

        if (numbers === 0) return { rule: copy, refusal: undefined };
        const numberAt = (i: number): number => defined(defined(cells[i], 'a source cell').entry?.constant, 'a number').number;
        if (cells.length === 1) {
            const number = numberAt(0);
            if (dates === 1) return { rule: x => series(number + x), refusal: undefined };
            // A single number is copied from one cell's handle and goes on by one from a whole column's (R5), and from a
            // source of several cells that holds nothing else (run 18a, 4); among numbers in every cell it is copied.
            if (single || allNumbers) return { rule: copy, refusal: undefined };
            if (wholeAxis || loneNumber) return { rule: x => series(trendStep(number, 1, x)), refusal: undefined };
            return refuse(fillRefusal(sourceAt(0), 'a single number in a line of a source of several cells, which Excel was not read to copy or continue there'));
        }
        if (loneNumber && numbers === 1 && dates === 0) {
            // The source's one number and the blanks beside it in its line (run 18a, 5).
            const at = cells.findIndex(c => c.entry !== undefined);
            const number = numberAt(at);
            const length = cells.length;
            return { rule: (x, i) => (i === at ? series(trendStep(number, 1, Math.floor(x / length))) : undefined), refusal: undefined };
        }
        if (numbers !== cells.length) {
            return refuse(fillRefusal(sourceAt(cells.findIndex(c => c.entry?.constant?.kind === 'number')), 'numbers mixed with other cells, which Excel continues in ways ExSheet does not yet'));
        }

        // Excel's linear trend: the least-squares line through (0, y0) .. (n-1, yn-1).
        const ys = cells.map((_, i) => numberAt(i));
        const meanX = (ys.length - 1) / 2;
        const meanY = average(ys);
        let sxy = 0;
        let sxx = 0;
        for (let i = 0; i < ys.length; i++) {
            sxy += (i - meanX) * (defined(ys[i], 'a number') - meanY);
            sxx += (i - meanX) * (i - meanX);
        }
        const slope = sxy / sxx;
        // Excel stores a trend at 15 significant digits (verification/2026-09-27-windows-excel item 4).
        const step = trendRound(slope);
        const ahead = trendRound(meanY + slope * (ys.length - meanX));
        const behind = trendRound(meanY + slope * (-1 - meanX));
        return { rule: x => series(x >= 0 ? trendStep(ahead, step, x - ys.length) : trendStep(behind, -step, -1 - x)), refusal: undefined };
    }

    /** Text Excel's fill would not simply copy: text holding a digit, and the day and month names of its built-in lists, in English and in the Sheet's culture. */
    #continuesAsSeries(text: string): boolean {
        for (let i = 0; i < text.length; i++) if (isDigit(text.charCodeAt(i))) return true;
        const trimmed = ignoreCaseKey(trimWhiteSpace(text));
        this.#cultureListNames ??= cultureListNames(this.culture);
        return invariantListNames().has(trimmed) || this.#cultureListNames.has(trimmed);
    }
}

// ---- Helpers ----

function stateOf(c: Cell): CellState {
    return new CellState(c.entry, c.value, c.numberFormat, c.alignment, c.font, c.fill, c.borders);
}

function settled(state: CellState, inherited: CellFormat): CellState {
    return state.withFormat({
        numberFormat: state.numberFormat !== undefined && state.numberFormat.equals(inherited.numberFormat) ? undefined : state.numberFormat,
        alignment: state.alignment !== undefined && state.alignment === inherited.alignment ? undefined : state.alignment,
        font: state.font !== undefined && state.font.equals(inherited.font) ? undefined : state.font,
        fill: state.fill !== undefined && state.fill.equals(inherited.fill) ? undefined : state.fill,
        borders: state.borders !== undefined && state.borders.equals(inherited.borders) ? undefined : state.borders,
    });
}

function resolveAlignment(alignment: HorizontalAlignment, kind: ValueKind | undefined): Exclude<HorizontalAlignment, 'general'> {
    if (alignment !== 'general') return alignment;
    switch (kind) {
        case 'number': return 'right';
        case 'boolean':
        case 'error':
            return 'center';
        case 'text':
        case undefined:
            return 'left';
        default: throw new Error(`Internal: ${String(kind)} is no kind of Value.`);
    }
}

/** Whether an Anchor whose spill was blocked computed an array of the same shape again: it is still blocked until the layout says otherwise. */
function stillBlocked(anchor: Cell, array: ValueArray): boolean {
    return anchor.spillBlocked && anchor.array !== undefined && anchor.array.rows === array.rows && anchor.array.columns === array.columns;
}

/** The line shown on an edge two cells record a side of: the upper (left) cell's where it records one, else the lower (right) cell's. */
function edge(upperOrLeft: BorderLine, lowerOrRight: BorderLine): BorderLine {
    return upperOrLeft.isNone ? lowerOrRight : upperOrLeft;
}

const horizontalAlignments: readonly HorizontalAlignment[] = ['general', 'left', 'center', 'right'];

function checkAlignment(alignment: HorizontalAlignment): HorizontalAlignment {
    if (!horizontalAlignments.includes(alignment)) throw new RangeError(`Not an alignment: ${alignment}.`);
    return alignment;
}

function checkRow(row: number): number {
    if (!Number.isInteger(row) || row < 0 || row >= rowCount) throw new RangeError(`A row is 0 to ${String(rowCount - 1)}.`);
    return row;
}

function checkColumn(column: number): number {
    if (!Number.isInteger(column) || column < 0 || column >= columnCount) throw new RangeError(`A column is 0 to ${String(columnCount - 1)}.`);
    return column;
}

/** The parts of one change as one outcome: the levels as they were before the first part, and each cell as it was before the first part that touched it. */
function combined(parts: readonly CellFormatOutcome[]): CellFormatOutcome {
    const seen = new Set<number>();
    const first = defined(parts[0], 'the first part');
    return {
        change: SheetChange.merge(parts.map(p => p.change)),
        before: parts.flatMap(p => p.before).filter(b => {
            if (seen.has(b.address.key)) return false;
            seen.add(b.address.key);
            return true;
        }),
        rowsBefore: first.rowsBefore,
        columnsBefore: first.columnsBefore,
    };
}

/** A row's level when a change is set on the whole Sheet (the twelfth Windows run, case 15). */
function underWholeSheet(level: AxisFormat, change: CellFormatChange): AxisFormat {
    return new AxisFormat(
        change.numberFormat === undefined ? level.numberFormat : undefined,
        change.alignment === undefined ? level.alignment : undefined,
        change.setsFont && level.font !== undefined ? change.applyToFont(level.font) : level.font,
        change.fill === undefined ? level.fill : undefined,
        change.setsBorders && level.borders !== undefined ? defined(change.borders, 'the Borders set').applyTo(level.borders, PlaceInRange.inside) : level.borders);
}

function setAxis(levels: Map<number, AxisFormat>, index: number, level: AxisFormat): void {
    if (level.isEmpty) levels.delete(index);
    else levels.set(index, level);
}

function sameLevel(a: AxisFormat | undefined, b: AxisFormat | undefined): boolean {
    return (a ?? AxisFormat.empty).equals(b ?? AxisFormat.empty);
}

function levelsDiffer(a: ReadonlyMap<number, AxisFormat>, b: ReadonlyMap<number, AxisFormat>): boolean {
    if (a.size !== b.size) return true;
    for (const [key, level] of a) {
        const other = b.get(key);
        if (other === undefined || !other.equals(level)) return true;
    }
    return false;
}

/** The rows beside a cell whose painted edge changes with the cell's own sides. */
function rowsAcross(address: CellAddress, before: CellBorders, after: CellBorders): number[] {
    const rows: number[] = [];
    if (!before.top.equals(after.top) && address.row > 0) rows.push(address.row - 1);
    if (!before.bottom.equals(after.bottom) && address.row < rowCount - 1) rows.push(address.row + 1);
    return rows;
}

/** The row or column levels as a Sheet Document records them: adjacent indexes recorded alike are one run. */
function runs(levels: ReadonlyMap<number, AxisFormat>): SheetDocumentAxisFormat[] {
    const list: { first: number; last: number; level: AxisFormat }[] = [];
    for (const index of [...levels.keys()].toSorted((a, b) => a - b)) {
        const level = defined(levels.get(index), 'a level');
        const last = list.at(-1);
        if (last !== undefined && last.last === index - 1 && last.level.equals(level)) last.last = index;
        else list.push({ first: index, last: index, level });
    }
    return list.map(r => new SheetDocumentAxisFormat(r.first, r.last, r.level.numberFormat, r.level.alignment, r.level.font, r.level.fill, r.level.borders));
}

/** The levels recorded on the rows or columns `first` to `last`, each with how many of them record it; those recording nothing are counted under the empty level. */
function levelGroups(levels: ReadonlyMap<number, AxisFormat>, first: number, last: number): Map<string, { level: AxisFormat; count: number }> {
    const groups = new Map<string, { level: AxisFormat; count: number }>();
    let counted = 0;
    for (const [index, level] of levels) {
        if (index < first || index > last) continue;
        const group = groups.get(level.key);
        if (group === undefined) groups.set(level.key, { level, count: 1 });
        else group.count++;
        counted++;
    }
    const unrecorded = last - first + 1 - counted;
    if (unrecorded > 0) {
        const group = groups.get(AxisFormat.empty.key);
        if (group === undefined) groups.set(AxisFormat.empty.key, { level: AxisFormat.empty, count: unrecorded });
        else group.count += unrecorded;
    }
    return groups;
}

function charactersOf(width: number): number {
    if (!Number.isFinite(width) || width < 0) throw new RangeError('A column width is a finite number of characters, zero or more.');
    // A width typed as 8 may arrive as 7.9999999 after a round trip through pixels.
    return Math.min(2_147_483_647, Math.floor(width + 1e-9));
}

function callsToday(node: Node): boolean {
    return [...node.calls()].some(call => call.function !== undefined && call.name === 'TODAY');
}

function callsRandom(node: Node): boolean {
    return [...node.calls()].some(call => call.function !== undefined && (call.name === 'RAND' || call.name === 'RANDBETWEEN'));
}

function sameStrings(a: readonly string[], b: readonly string[]): boolean {
    return a.length === b.length && a.every((s, i) => s === b[i]);
}

/** The key's columns as the columns spell them, so that `id` and `Id` declare the same key. */
function keyAsDeclared(columns: readonly string[], key: readonly string[]): string[] {
    return key.map(k => defined(columns.find(column => equalsIgnoreCase(column, k)), 'a key column'));
}

/**
 * The first two rows whose keys the lookup a Pointing Scope writes cannot tell apart, or `undefined` (ADR-0058). A
 * row whose key, or any part of it, is blank or an Error Value is passed over.
 */
function firstRepeat(parts: readonly (readonly (Value | undefined)[])[]): { first: number; second: number } | undefined {
    const several = parts.length > 1;
    const seen = new Map<string, number>();
    const rows = defined(parts[0], 'a key column').length;
    for (let row = 0; row < rows; row++) {
        let identity = '';
        let isKey = true;
        for (const part of parts) {
            const key = part[row];
            if (key === undefined || key.isError) {
                isKey = false;
                break;
            }
            const exact = exactMatchIdentity(key);
            const number = several && exact.kind === 'number' ? atFifteenDigits(exact.number) : exact.number;
            identity += `${exact.kind}:${formatRoundTrip(number)}:${String(exact.text?.length ?? -1)}:${exact.text ?? ''}|`; // oxlint-disable-line ex-grid/no-stand-in-default -- a key with no text writes its length as -1 and its text as nothing, as the C# identity does
        }
        if (!isKey) continue;
        const first = seen.get(identity);
        if (first !== undefined) return { first, second: row };
        seen.set(identity, row);
    }
    return undefined;
}

/** What `XLOOKUP`'s exact match compares of a Value: its kind, and then its number, its text in Excel's collation, or its boolean. */
function exactMatchIdentity(value: Value): { kind: ValueKind; number: number; text: string | undefined } {
    switch (value.kind) {
        case 'text': return { kind: 'text', number: 0, text: textEqualityKey(value.text) };
        case 'number': return { kind: 'number', number: value.number, text: undefined };
        case 'boolean':
        case 'error':
            return { kind: 'boolean', number: value.boolean ? 1 : 0, text: undefined };
        default: throw new Error(`Internal: ${String(value.kind)} is no kind of Value.`);
    }
}

/** The tables a Formula reads: those its structured references name, and every bare name it holds (TABLE-024), each once without regard to case. */
function tablesRead(formula: Node): string[] {
    const names = new Set<string>();
    for (const reference of formula.structuredReferences()) names.add(ignoreCaseKey(reference.table));
    for (const name of bareNames(formula)) names.add(ignoreCaseKey(name));
    return [...names];
}

/** The names a Formula holds that no `LET` binds, which a Linked Table's name could be. */
function bareNames(node: Node): string[] {
    switch (node.kind) {
        case 'name': return node.binding === undefined ? [node.name] : [];
        case 'intersection': return [...bareNames(node.left), ...bareNames(node.right)];
        case 'parentheses': return bareNames(node.inner);
        case 'unary': return bareNames(node.operand);
        case 'percent': return bareNames(node.operand);
        case 'binary': return [...node.operands()].flatMap(bareNames);
        case 'function': return node.arguments.flatMap(bareNames);
        case 'let': return [...node.bindings.flatMap(b => bareNames(b.value)), ...bareNames(node.calculation)];
        case 'number':
        case 'text':
        case 'boolean':
        case 'error':
        case 'missing':
        case 'reference':
        case 'structuredReference':
            return [];
        default: throw new Error(`Internal: ${String((node as { kind: unknown }).kind)} is no kind of node.`);
    }
}

function isTableName(name: string): boolean {
    return tableNamePattern.test(name)
        && CellAddress.tryParse(name) === undefined
        && !rowColumnPattern.test(name)
        && !equalsIgnoreCase(name, 'TRUE')
        && !equalsIgnoreCase(name, 'FALSE');
}

/** Whether a Formula is one the format-on-entry rule reads: References to single cells, numbers, `+ - * /`, parentheses, and a few functions. */
function simple(node: Node): boolean {
    switch (node.kind) {
        case 'number': return true;
        case 'reference': return node.reference.shape === 'cell';
        case 'parentheses': return simple(node.inner);
        case 'unary': return simple(node.operand);
        case 'percent': return node.operand.kind === 'number';
        case 'binary': return node.spine.every(s => s.operator === '+' || s.operator === '-' || s.operator === '*' || s.operator === '/') && [...node.operands()].every(simple);
        case 'function':
            if (node.function === undefined) return false;
            if (node.name === 'DATE' || node.name === 'TODAY' || node.name === 'NOW') return true;
            return (node.name === 'MIN' || node.name === 'MAX' || node.name === 'SUM') && node.arguments.every(a => a.kind === 'reference' || a.kind === 'number');
        case 'boolean':
        case 'error':
        case 'intersection':
        case 'let':
        case 'missing':
        case 'name':
        case 'structuredReference':
        case 'text':
            return false;
        default:
            return unreachable(node);
    }
}

/** A number constant with `%`, possibly in parentheses or under a sign. */
function isPercentConstant(node: Node): boolean {
    switch (node.kind) {
        case 'percent': return true;
        case 'parentheses': return isPercentConstant(node.inner);
        case 'unary': return isPercentConstant(node.operand);
        case 'binary':
        case 'boolean':
        case 'error':
        case 'function':
        case 'intersection':
        case 'let':
        case 'missing':
        case 'name':
        case 'number':
        case 'reference':
        case 'structuredReference':
        case 'text':
            return false;
        default:
            return unreachable(node);
    }
}

/** The format one operator gives, from the formats of its operands. */
function binary(operator: string, leftNode: Node, rightNode: Node, left: NumberFormat | undefined, right: NumberFormat | undefined): NumberFormat | undefined {
    if (operator !== '+' && operator !== '-') {
        const product = left ?? right;
        return product !== undefined && !product.isPercent ? product : undefined;
    }
    if (isPercentConstant(leftNode) || isPercentConstant(rightNode)) {
        const other = isPercentConstant(leftNode) ? right : left;
        return other === undefined ? percentConstant : undefined;
    }
    if (operator === '-' && left?.isDate === true && right?.isDate === true) return undefined;
    return left ?? right;
}

function capitalise(text: string): string {
    return toUpperInvariant(text.charAt(0)) + text.slice(1);
}

/** Whether a Formula calls `ROW()` or `COLUMN()` with no argument: its Value is its own cell's place. */
function readsOwnPlace(formula: Node): boolean {
    return [...formula.calls()].some(call => call.function !== undefined && (call.name === 'ROW' || call.name === 'COLUMN') && call.arguments.length === 0);
}

/**
 * One cell of the `text/html` flavour, in Excel's own markup (ADR-0048): a number carries its unformatted Value in
 * `x:num`, and a cell whose format is not General carries the format code in `mso-number-format`.
 */
function cellHtml(value: Value | undefined, raw: string, format: NumberFormat): string {
    let html = '<td';
    if (value?.kind === 'number') html += ` x:num="${raw}"`;
    if (!format.isGeneral) html += ` style='mso-number-format:"${cssFormatCode(format.code)}"'`;
    return `${html}>${escapeHtml(raw)}</td>`;
}

/**
 * A format code as Excel's HTML writes it inside `mso-number-format:"…"`: letters, digits, characters outside
 * ASCII, the space and `_ * ? % -` as they are; every other ASCII character escaped with a backslash; and the
 * characters that would end the CSS string or the attribute around it as CSS hex escapes. @internal
 */
export function cssFormatCode(code: string): string {
    let css = '';
    for (let i = 0; i < code.length; i++) {
        const c = code.charAt(i);
        const n = code.charCodeAt(i);
        if (/[A-Za-z0-9]/u.test(c) || n > 0x7f || c === '_' || c === '*' || c === '?' || c === '%' || c === '-' || c === ' ') {
            css += c;
        } else if (c === '"' || c === "'" || c === '&' || c === '<' || c === '>') {
            css += `\\${n.toString(16).toUpperCase().padStart(4, '0')}`;
            // A hex escape runs on through hex digits and swallows one space after it.
            const next = code.charAt(i + 1);
            if (i + 1 < code.length && (/[0-9A-Fa-f]/u.test(next) || next === ' ')) css += ' ';
        } else {
            css += `\\${c}`;
        }
    }
    return css;
}

/** Excel's TSV: a field holding a tab, a line break or a quote is quoted, inner quotes doubled. */
function quoteForTsv(value: string): string {
    return /[\t\n\r"]/u.test(value) ? `"${value.replaceAll('"', '""')}"` : value;
}

function escapeHtml(value: string): string {
    return value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
}

function fillRefusal(at: CellAddress, what: string): SheetRefusal {
    return new SheetRefusal('fillPatternNotSupported', `${at.toString()} holds ${what}; ExSheet refuses the fill rather than fill it with copies.`);
}

function series(number: number): Entry {
    return Entry.fromValue(Number.isFinite(number) ? Value.fromNumber(number) : Value.fromError('#NUM!'));
}

/** `Enumerable.Average`: the sum in order, divided by the count. */
function average(values: readonly number[]): number {
    let sum = 0;
    for (const v of values) sum += v;
    return sum / values.length;
}

// Excel's 15 significant digits, for a fill's linear trend (ADR-0050, ticket 15). C# counts the steps in `decimal`;
// here a decimal is a whole number of units of 10^exponent, held in a bigint, which counts them exactly.

const trendDigits = 15;

/** `number` rounded to 15 significant digits, as the nearest double. */
function trendRound(number: number): number {
    return Number.isFinite(number) && number !== 0 ? Number(formatExponential(number, trendDigits - 1)) : number;
}

/** A decimal: `units` × 10^`exponent`. */
interface Decimal {
    readonly units: bigint;
    readonly exponent: number;
}

/** Decimal keeps 15 significant digits from 1e-13 (its 28 places) up to 1e15 (a whole number of 15 digits). */
function insideDecimal(number: number): boolean {
    return number === 0 || (Math.abs(number) >= 1e-13 && Math.abs(number) < 1e15);
}

/** The number at 15 significant digits, exactly, as C# reads its `E14` text into a decimal; `undefined` outside decimal's range. */
function exactDecimal(number: number): Decimal | undefined {
    if (!insideDecimal(number)) return undefined;
    if (number === 0) return { units: 0n, exponent: 0 };
    const text = formatExponential(number, trendDigits - 1);
    const e = text.indexOf('E');
    const mantissa = text.slice(0, e).replace('.', '');
    return { units: BigInt(mantissa), exponent: Number(text.slice(e + 1)) - (trendDigits - 1) };
}

/** `first` plus `count` steps, rounded to 15 significant digits: counted exactly where both are inside decimal's range, and in doubles otherwise. */
function trendStep(first: number, step: number, count: number): number {
    if (count === 0) return first;
    const a = exactDecimal(first);
    const s = exactDecimal(step);
    if (a !== undefined && s !== undefined) {
        const exponent = Math.min(a.exponent, s.exponent);
        const units = a.units * 10n ** BigInt(a.exponent - exponent) + BigInt(count) * s.units * 10n ** BigInt(s.exponent - exponent);
        const rounded = roundDecimal({ units, exponent });
        if (rounded !== undefined) return Number(`${String(rounded.units)}e${String(rounded.exponent)}`);
    }
    return trendRound(first + count * step);
}

/** A decimal rounded to 15 significant digits, half away from zero; `undefined` outside decimal's range of 1e-13 to 1e15. */
function roundDecimal(number: Decimal): Decimal | undefined {
    if (number.units === 0n) return { units: 0n, exponent: 0 };
    const negative = number.units < 0n;
    const magnitude = negative ? -number.units : number.units;
    const digits = magnitude.toString().length;
    // The decimal exponent of the leading digit: 10^(digits - 1 + exponent).
    const leading = digits - 1 + number.exponent;
    if (leading < -13 || leading >= 15) return undefined;
    const drop = digits - trendDigits;
    if (drop <= 0) return number;
    const divisor = 10n ** BigInt(drop);
    let kept = magnitude / divisor;
    if ((magnitude % divisor) * 2n >= divisor) kept += 1n;
    return { units: negative ? -kept : kept, exponent: number.exponent + drop };
}

let invariantNames: Set<string> | undefined;

function invariantListNames(): Set<string> {
    invariantNames ??= cultureListNames(Culture.invariant);
    return invariantNames;
}

function cultureListNames(culture: Culture): Set<string> {
    const data = culture.data;
    return new Set([...data.dayNames, ...data.abbreviatedDayNames, ...data.monthNames, ...abbreviatedMonthNamesOf(culture)]
        .filter(n => n.length > 0).map(ignoreCaseKey));
}

