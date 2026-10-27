import { formatted } from './open-error';

// A Workbook not written, refused by name (ADR-0330 §6), as ExSheet.Xlsx's XlsxSaveException refuses it: the reason, the
// Sheet and the cell, and what the cell held, as far as the refusal is about one — `Sheet 'Trades', cell B7 (=B6*2): …`, as
// a refused opening names them. Nothing was written.

/**
 * Why a Workbook was not written, and nothing was (ADR-0330 §6): what it holds cannot be written as a file Excel reads with
 * the Values the user saw, or would make a file too large to open again.
 */
export type XlsxSaveRefusal =
    /**
     * A Linked Table cannot be written as an Excel Table on a worksheet of its name, holding its rows as they stand
     * (ADR-0330 §6): it has not answered (every Formula that reads it shows `#GETTING_DATA`), it holds no rows (an Excel Table
     * holds at least one, and its Formulas would answer otherwise over one empty row), it holds more rows than a worksheet's
     * 1,048,576 less its header's or more columns than its 16,384, or its name is already a Sheet's or is one a worksheet
     * cannot have (longer than 31 characters, holding `\`, or `History`). Or a cell shows `#GETTING_DATA`. The refusal names
     * the table, and a cell's refusal the Sheet and the cell.
     */
    | 'linkedTable'
    /**
     * A cell shows `#CIRC!`: it is part of a circular reference, or depends on one. A file holds only Excel's Error Values, and
     * Excel shows 0 there, which ExSheet refuses to show (ADR-0047); no Value the file can hold is the one the user saw.
     */
    | 'circularReference'
    /**
     * A Formula that cannot be written as Excel stores it, or as Excel takes it: its text is longer than Excel's 8,192
     * characters, holds a text constant longer than 255, nests its calls deeper than 64 levels or makes one with more than 255
     * arguments; it holds half of a surrogate pair, which a file cannot carry; it reads a Sheet the Workbook does not have
     * (Excel would read another workbook's); it calls a name a `LET` binds or a `LAMBDA` takes, which ExSheet reads as an
     * unknown function and Excel would call; it reads, by a structured reference, a Linked Table or a column the Workbook does
     * not declare, or a column whose name begins or ends with a space or holds `@`, a tab, a line break or one of
     * `, : . " { } $ ^ & * + = - < > /`, whose form Excel has not been seen to write; it reads a Linked Table's name as a name
     * of its own (`=SUM(Positions)`), which ExSheet shows as `#NAME?` and Excel would read as the table; or it does not read
     * back as the Formula it is.
     */
    | 'formula'
    /**
     * A cell's text is longer than the 32,767 characters a cell of Excel holds, or holds half of a surrogate pair, which a file
     * cannot carry; or a Sheet's name holds a character a worksheet's name cannot carry in a file: a control character below
     * U+0020, U+FFFE or U+FFFF, or half of a surrogate pair. The refusal then names the Sheet and no cell.
     */
    | 'text'
    /**
     * The workbook would write more elements than `XlsxSaveOptions.maxCells`, which counts each cell written and each row a
     * row format writes. A Sheet formatted on many rows and many columns writes a cell wherever a formatted row crosses a
     * formatted column that records a part the row does not, and a row format writes a row element for every row it covers,
     * holding a cell or not. The default limit is the cells a file is read with (`defaultMaxCells`); the reader counts cells
     * only, so a file within it opens again, and rows are counted here so that a row format over every row is bounded too.
     */
    | 'tooManyCells'
    /**
     * The workbook would hold more uncompressed bytes than `XlsxSaveOptions.maxUncompressedBytes`: by default, more than a
     * file is read with (`defaultMaxUncompressedBytes`), so it would not open again. The refusal names the Sheet whose
     * worksheet went past it, when one did.
     */
    | 'tooLarge';

/** Where a save's refusal met what it refuses, as far as it is about one Sheet and cell. */
export interface XlsxSavePlace {
    /** The name of the Sheet it met. */
    readonly sheet?: string | undefined;
    /** The cell it met, as `B7`. */
    readonly cell?: string | undefined;
    /** What the cell held, as a Formula's text with its `=`. */
    readonly held?: string | undefined;
}

/** A Workbook not written, refused by name (ADR-0330 §6). */
export class XlsxSaveError extends Error {
    /** Why the Workbook was not written. */
    readonly reason: XlsxSaveRefusal;
    /** The name of the Sheet the refusal met, or `undefined` when it is not about one Sheet. */
    readonly sheet: string | undefined;
    /** The cell the refusal met, as `B7`, or `undefined` when it is not about one cell. */
    readonly cell: string | undefined;
    /** What the cell held — a Formula's text with its `=` — or `undefined`. */
    readonly held: string | undefined;

    /**
     * A refusal naming what it met.
     * @param reason - Why the Workbook is not written.
     * @param message - What was wrong, as a sentence that follows the Sheet and the cell.
     * @param where - The Sheet, the cell and what the cell held, as far as it is about one.
     */
    constructor(reason: XlsxSaveRefusal, message: string, where: XlsxSavePlace = {}) {
        super(formatted(where.sheet, where.cell, where.held, message));
        this.name = 'XlsxSaveError';
        this.reason = reason;
        this.sheet = where.sheet;
        this.cell = where.cell;
        this.held = where.held;
    }
}

/** How far a write has come (`XlsxSaveOptions.onProgress`): the Workbook's snapshot being planned, then the worksheets being
 * written. */
export interface XlsxSaveProgress {
    /** The index of the worksheet being written, in the file's order, from zero, a Linked Table's after the Sheets'; -1 while
     * the snapshot is planned: its cells counted against the limit and checked. */
    readonly sheet: number;
    /** The worksheets the file holds: one for each Sheet and one for each Linked Table. */
    readonly sheets: number;
    /** The elements written so far, counted as `maxCells` counts them; 0 while the snapshot is planned. */
    readonly elements: number;
    /** The elements the file writes, counted as `maxCells` counts them; 0 while the snapshot is planned. */
    readonly totalElements: number;
}

/**
 * What a write may produce, and how it paces itself (ADR-0330 §6, §9). Beyond either limit the Workbook is refused by name,
 * never written in part. The write takes its snapshot of the Workbook before it first yields, then builds the file from it in
 * slices sized by time, yielding between them so that a browser keeps painting, and reports its progress after each, as a
 * read does. A key left out takes the default its comment names.
 */
export interface XlsxSaveOptions {
    /**
     * The cells the whole workbook may write, every worksheet counted, each element a cell: the Sheets' own cells, the cells an
     * array spills into, the cells of no value written where a formatted row crosses a formatted column, a Linked Table's cells
     * and its header's, and each row a row format writes, which is an element of its own whether or not it holds a cell (a row
     * format over every row writes 1,048,576). `defaultMaxCells` when left out, the most a file is read with by default, so
     * that what is written opens again. Beyond it the write is refused by name (`tooManyCells`) before anything is written.
     */
    readonly maxCells?: number;
    /**
     * The uncompressed bytes the file's parts may hold together: 1 to `defaultMaxUncompressedBytes`, which it is when left
     * out, the most a file is read with by default. Beyond it the write is refused by name (`tooLarge`).
     */
    readonly maxUncompressedBytes?: number;
    /** Told how far the write has come after every slice, and once more when every element is written. */
    readonly onProgress?: (progress: XlsxSaveProgress) => void;
    /** How long one slice works, in milliseconds, before the write yields; 30 when left out, a read's. A slice always does
     * some work, so 0 yields after every batch. */
    readonly sliceBudgetMs?: number;
    /** What the write awaits between slices; a macrotask (`setTimeout(…, 0)`) when left out, as a read's. */
    readonly yield?: () => Promise<void>;
    /** Cancels the write: it rejects with the signal's reason, looked at between slices. Cancelled, it writes nothing. */
    readonly signal?: AbortSignal;
}
