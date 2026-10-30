import { XlsxError, type XlsxRefusal } from '@ex-grid/data-xlsx';

// A workbook refused by name, and nothing opened (ADR-0330 §4), as ExSheet.Xlsx's XlsxOpenException refuses it: the
// reason, the Sheet and the cell, and what the cell held, as far as the refusal is about one — `Sheet 'Trades', cell B7
// (=Rate*A7): …`, as the CSV reader names a row and a column.

/**
 * Why a workbook was refused, and nothing opened (ADR-0330 §4): what it held would change a Value, or the number a cell
 * shows, or it is not a file that can be read faithfully.
 */
export type XlsxOpenRefusal =
    /** The file layer refused the file: not a plain .xlsx, damaged, or past a limit. `XlsxOpenError.fileRefusal` says why. */
    | 'file'
    /** A Formula the engine cannot read: a syntax it does not implement (an array constant, the union operator, `@`), or a
     * What-If data table. */
    | 'unreadableFormula'
    /** A Formula reads a defined name, which ExSheet does not define, so it would show `#NAME?` where Excel shows a Value. */
    | 'definedName'
    /** A Formula reads another workbook, which ExSheet cannot reach. */
    | 'otherWorkbook'
    /** A Formula reads an Excel Table, which ExSheet does not read yet (ADR-0330 §6). */
    | 'excelTable'
    /** A legacy array formula (Ctrl+Shift+Enter), whose cells Excel computes as one, and ExSheet would spill otherwise. */
    | 'legacyArrayFormula'
    /** A constant a cell cannot hold in ExSheet, such as an Error Value the engine does not read. */
    | 'unreadableConstant'
    /** A worksheet's name is not one a Sheet can have, or the workbook holds no worksheet at all. */
    | 'sheets'
    /**
     * A cell, a row or a column takes a Number Format the engine does not read, or a built-in one whose code follows the
     * computer's locale and that the file does not spell out: shown in General instead, a date would show as a serial, or a
     * currency lose its symbol (ADR-0330 §4). `XlsxOpenError.held` is the format's code as the file spells it, or
     * `Number Format N`, naming its id, where the file spells none.
     */
    | 'numberFormat';

/** Where an open's refusal met what it refuses, as far as it is about one Sheet and cell. */
export interface XlsxOpenPlace {
    /** The name of the Sheet it met. */
    readonly sheet?: string | undefined;
    /** The cell it met, as `B7`. */
    readonly cell?: string | undefined;
    /** What the cell held, as a Formula's text with its `=` or a constant's. */
    readonly held?: string | undefined;
}

/** A workbook refused by name, and nothing opened (ADR-0330 §4). */
export class XlsxOpenError extends Error {
    /** Why the workbook was refused. */
    readonly reason: XlsxOpenRefusal;
    /** The name of the Sheet the refusal met, or `undefined` when it is not about one Sheet. */
    readonly sheet: string | undefined;
    /** The cell the refusal met, as `B7`, or `undefined` when it is not about one cell. */
    readonly cell: string | undefined;
    /** What the cell held — a Formula's text with its `=`, or a constant — or `undefined` when the refusal is not about one
     * cell. */
    readonly held: string | undefined;

    /**
     * A refusal naming what it met.
     * @param reason - Why the workbook is refused.
     * @param message - What was wrong, as a sentence that follows the Sheet and the cell.
     * @param where - The Sheet, the cell and what the cell held, as far as it is about one.
     * @param options - What caused it, when it was another error.
     */
    constructor(reason: XlsxOpenRefusal, message: string, where: XlsxOpenPlace = {}, options?: ErrorOptions) {
        super(formatted(where.sheet, where.cell, where.held, message), options);
        this.name = 'XlsxOpenError';
        this.reason = reason;
        this.sheet = where.sheet;
        this.cell = where.cell;
        this.held = where.held;
    }

    /** Why the file layer refused the file, when `reason` is `file`; otherwise `undefined`. */
    get fileRefusal(): XlsxRefusal | undefined {
        return this.reason === 'file' && this.cause instanceof XlsxError ? this.cause.reason : undefined;
    }

    /** The file layer's refusal, with its Sheet and cell. @internal */
    static fromFile(e: XlsxError): XlsxOpenError {
        const refusal = new XlsxOpenError('file', e.message, { sheet: e.sheet, cell: e.cell }, { cause: e });
        // The file layer has already written its Sheet and cell into its words.
        refusal.message = e.message;
        return refusal;
    }
}

/** The message with the Sheet, the cell and what it held before it, as ExSheet.Xlsx writes them. */
export function formatted(sheet: string | undefined, cell: string | undefined, held: string | undefined, message: string): string {
    const at = held === undefined ? cell : `${cell === undefined ? '' : cell} (${held})`;
    if (sheet === undefined) return message;
    return at === undefined ? `Sheet '${sheet}': ${message}` : `Sheet '${sheet}', cell ${at}: ${message}`;
}
