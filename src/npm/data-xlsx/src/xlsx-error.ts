// A file refused by name (ADR-0330 §4, §9), as ExGrid.Data.Xlsx's XlsxException refuses it: the reason, and the
// part, the Sheet and the cell it met, worded as that package words it, so a refusal reads alike in both.

/** Why a file was refused (ADR-0330 §4, §9). A refused file is never read in part. */
export type XlsxRefusal =
    /** The bytes are not a zip package, or the zip is damaged. */
    | 'notAZip'
    /** A zip package, but not a spreadsheet: it has no workbook part (a `.docx`, a `.pptx`, any other zip). */
    | 'notAWorkbook'
    /** A workbook with macros (`.xlsm`, `.xltm`, `.xlam`): its content types name a macro-enabled workbook or a VBA project. */
    | 'macroEnabled'
    /** A binary workbook (`.xlsb`). */
    | 'binary'
    /** A legacy Excel 97–2003 workbook (`.xls`): an OLE compound file that holds no encrypted package. */
    | 'legacy'
    /** An encrypted (password-protected) workbook: an OLE compound file holding an `EncryptedPackage`. */
    | 'encrypted'
    /** A workbook written as Strict Open XML, whose parts use other namespaces than the transitional ones Excel saves by default. */
    | 'strictOpenXml'
    /** A part declares a document type (`<!DOCTYPE`), which a workbook never needs and an attack uses. */
    | 'dtd'
    /** A part is not well-formed XML, or does not hold what its place in the package says it does. */
    | 'malformed'
    /** The file holds more uncompressed bytes than a read allows; or a workbook written would hold more than a write allows. */
    | 'tooLarge'
    /** The workbook holds more cells than a read allows. */
    | 'tooManyCells';

/** Where a refusal met what it refuses: the part, the Sheet and the cell, as far as it is about one. */
export interface XlsxPlace {
    /** The package part, as `xl/worksheets/sheet1.xml`. */
    readonly part?: string | undefined;
    /** The Sheet's name. */
    readonly sheet?: string | undefined;
    /** The cell, as `B7`. */
    readonly cell?: string | undefined;
}

/**
 * A file refused by name (ADR-0330 §4, §9): the reason, and the part, the Sheet and the cell it met, as far as the
 * refusal is about one. Nothing is read from a refused file: `Sheet 'Trades', cell B7: …`.
 */
export class XlsxError extends Error {
    /** Why the file was refused. */
    readonly reason: XlsxRefusal;
    /** The package part the refusal met, as `xl/worksheets/sheet1.xml`; `undefined` when it is about the file as a whole. */
    readonly part: string | undefined;
    /** The name of the Sheet the refusal met; `undefined` when it is not about one Sheet. */
    readonly sheet: string | undefined;
    /** The cell the refusal met, as `B7`; `undefined` when it is not about one cell. */
    readonly cell: string | undefined;

    /**
     * A refusal naming what it met.
     * @param reason - Why the file is refused.
     * @param message - What was wrong, as a sentence that follows the Sheet and the cell.
     * @param where - The part, the Sheet and the cell it met, as far as it is about one.
     * @param options - What caused it, when it was another error.
     */
    constructor(reason: XlsxRefusal, message: string, where: XlsxPlace = {}, options?: ErrorOptions) {
        super(formatted(where.sheet, where.cell, where.part, message), options);
        this.name = 'XlsxError';
        this.reason = reason;
        this.part = where.part;
        this.sheet = where.sheet;
        this.cell = where.cell;
    }
}

function formatted(sheet: string | undefined, cell: string | undefined, part: string | undefined, message: string): string {
    if (sheet !== undefined) return cell !== undefined ? `Sheet '${sheet}', cell ${cell}: ${message}` : `Sheet '${sheet}': ${message}`;
    if (part !== undefined) return `Part '${part}': ${message}`;
    return message;
}
