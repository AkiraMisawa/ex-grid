// The model of a workbook as its file writes it (ADR-0330 §9), as ExGrid.Data.Xlsx's records hold it: a number is
// its text, a Formula its stored text, a style its index. Nothing is interpreted.

import { formatCell, parseCell } from './cell-reference';
import { equalsIgnoreCase } from './invariant';

/** What a cell's value is, as its `t` attribute writes it (ECMA-376 `ST_CellType`). */
export type XlsxCellType =
    /** A number (`t="n"`, or no `t`): the value is its decimal text, invariant. A cell with no value is a number with none. */
    | 'number'
    /** Text held in the shared strings (`t="s"`): the value is its index, and the cell's `text` the text. */
    | 'sharedString'
    /** Text written in the cell itself (`t="inlineStr"`): the cell's `text` is the text. */
    | 'inlineString'
    /** A Formula's text result (`t="str"`): the value is the text. */
    | 'formulaString'
    /** A boolean (`t="b"`): the value is `0` or `1`. */
    | 'boolean'
    /** An Error Value (`t="e"`): the value is its text, as `#DIV/0!`. */
    | 'error'
    /** A date in ISO 8601 (`t="d"`), which Excel itself never writes. */
    | 'date';

/** How a Formula is written (ECMA-376 `ST_CellFormulaType`). */
export type XlsxFormulaKind =
    /** A Formula of the cell's own. */
    | 'normal'
    /** A shared formula: the master carries the text, the range it covers and its index; every other cell of the group
     * carries only the index, and its Formula is the master's moved to it. */
    | 'shared'
    /** An array formula over its range: a dynamic array that spills when it says so, and a legacy multi-cell array formula
     * otherwise. */
    | 'array'
    /** A What-If data table. */
    | 'dataTable';

/** A cell's Formula as the file writes it: its text, without the leading `=`, and how it is shared. */
export interface XlsxFormula {
    /** The Formula's text as stored, Excel's `_xlfn.` and `_xlws.` prefixes included; empty for a cell that takes a shared
     * formula from its master. */
    readonly text: string;
    /** How it is written. */
    readonly kind: XlsxFormulaKind;
    /** The range a shared formula's master or an array formula covers, as `A1:A10`; otherwise null. */
    readonly reference: string | null;
    /** A shared formula's group (`si`); otherwise null. */
    readonly sharedIndex: number | null;
    /** Whether an array formula is a dynamic array that spills (Excel 365), as the workbook's cell metadata marks one. */
    readonly isDynamicArray: boolean;
}

/** A Formula to write: its text, and how it is shared where it is not a cell's own. */
export interface XlsxFormulaInput {
    /** The Formula's text, without the leading `=`, Excel's prefixes included; empty for a cell that takes a shared formula. */
    readonly text: string;
    /** How it is written; `normal` when left out. */
    readonly kind?: XlsxFormulaKind;
    /** The range a shared formula's master or an array formula covers; an array formula left without one covers its cell. */
    readonly reference?: string | null;
    /** A shared formula's group. */
    readonly sharedIndex?: number | null;
    /** Whether an array formula is a dynamic array that spills. */
    readonly isDynamicArray?: boolean;
}

/**
 * One cell as a worksheet writes it (ADR-0330 §9): its place, its type, its value as written, its Formula and its
 * style. Nothing is interpreted: a number is its text, a Formula its stored text, a style its index into the styles'
 * cell formats.
 */
export interface XlsxCell {
    /** The cell's row, from zero (row 1 is 0). */
    readonly row: number;
    /** The cell's column, from zero (column A is 0). */
    readonly column: number;
    /** What the value is. */
    readonly type: XlsxCellType;
    /** The value as written (`<v>`), or null for a cell with none: a cell that carries only a style, or an inline string. */
    readonly value: string | null;
    /** The text of a shared or inline string; otherwise null. */
    readonly text: string | null;
    /** The Formula, or null. */
    readonly formula: XlsxFormula | null;
    /** The index into the styles' cell formats (`s`); 0 when the cell names none. */
    readonly styleIndex: number;
    /** Whether the text came in runs of their own formatting, which are not carried; the text is. */
    readonly isRichText: boolean;
}

/** A cell to write. A shared string is written from its `text`, whatever its `value` says. */
export interface XlsxCellInput {
    /** The cell's row, from zero. */
    readonly row: number;
    /** The cell's column, from zero. */
    readonly column: number;
    /** What the value is. */
    readonly type: XlsxCellType;
    /** The value as written; none when left out. */
    readonly value?: string | null;
    /** The text of a shared or inline string. */
    readonly text?: string | null;
    /** The Formula. */
    readonly formula?: XlsxFormulaInput | null;
    /** The index into the styles' cell formats; 0 when left out. */
    readonly styleIndex?: number;
}

/** The cell's place as `B7`. */
export function referenceOf(cell: { readonly row: number; readonly column: number }): string {
    return formatCell(cell.row, cell.column);
}

/** A column range's width, as `<col>` writes it. */
export interface XlsxColumn {
    /** The first column of the range, from zero. */
    readonly first: number;
    /** The last column of the range, from zero. */
    readonly last: number;
    /** The width in Excel's own unit (characters of the default font's widest digit, with padding), or null when the range
     * sets none. */
    readonly width: number | null;
    /** Whether the columns are hidden. */
    readonly hidden: boolean;
    /** The style the columns' empty cells take, or 0. */
    readonly styleIndex: number;
    /** Whether the width is marked as fitted to what the columns hold (`bestFit`): where a typed entry widened the column,
     * or where it was fitted on purpose; the file does not say which. */
    readonly bestFit: boolean;
    /** Whether the width is one set for the columns (`customWidth`), rather than the default width written only so that a
     * range that records a style has a width. */
    readonly customWidth: boolean;
}

/** A column range to write; a key left out is the record's default. */
export interface XlsxColumnInput {
    /** The first column of the range, from zero. */
    readonly first: number;
    /** The last column of the range, from zero. */
    readonly last: number;
    /** The width in Excel's own unit; none when left out. */
    readonly width?: number | null;
    /** Whether the columns are hidden; false when left out. */
    readonly hidden?: boolean;
    /** The style the columns' empty cells take; 0 when left out. */
    readonly styleIndex?: number;
    /** Whether the width is marked as fitted; false when left out. */
    readonly bestFit?: boolean;
    /** Whether the width is one set for the columns; true when left out. */
    readonly customWidth?: boolean;
}

/** A row that records a cell format of its own (`<row s="…" customFormat="1">`): the format its cells take where the row
 * holds no cell, in place of their columns'. */
export interface XlsxRow {
    /** The row, from zero (row 1 is 0). */
    readonly row: number;
    /** The index into the styles' cell formats its empty cells take; 0 is the default format, which still stands in place
     * of the columns'. */
    readonly styleIndex: number;
}

/** Whether a sheet is shown. */
export type XlsxSheetVisibility =
    /** Shown. */
    | 'visible'
    /** Hidden, and listed in Excel's Unhide. */
    | 'hidden'
    /** Hidden, and not listed in Excel's Unhide. */
    | 'veryHidden';

/** What kind of sheet a workbook lists. */
export type XlsxSheetKind =
    /** A worksheet of cells. */
    | 'worksheet'
    /** A chart sheet, or any other sheet that holds no cells. */
    | 'other';

/** A worksheet as the workbook part lists it. */
export interface XlsxSheetInfo {
    /** Its name, as its tab shows it. */
    readonly name: string;
    /** Its `sheetId`. */
    readonly sheetId: number;
    /** Whether it is shown. */
    readonly visibility: XlsxSheetVisibility;
    /** The package part that holds it, as `xl/worksheets/sheet1.xml`, or null for a sheet that is not a worksheet. */
    readonly part: string | null;
    /** What kind of sheet it is. */
    readonly kind: XlsxSheetKind;
}

/** A defined name, as the workbook part writes it. */
export interface XlsxDefinedName {
    /** The name, as `Rate`, or a built-in one such as `_xlnm.Print_Area`. */
    readonly name: string;
    /** What it refers to, as a Formula's text. */
    readonly text: string;
    /** The index of the sheet it is local to, or null for a name of the whole workbook. */
    readonly localSheet: number | null;
    /** Whether it is hidden. */
    readonly hidden: boolean;
}

/**
 * An Excel Table on a worksheet (ECMA-376 Part 1, §18.5), as a sheet writer's `addTable` writes it: a header row holding
 * each column's name, its rows of data below, an AutoFilter over them and Excel's default table style.
 */
export interface XlsxTable {
    /** The name a Formula reads it by (`Positions[PV]`), as Excel names a Table (`isValidTableName`), and no other table's or
     * defined name's in the workbook, in any case. */
    readonly name: string;
    /** The header's row, from zero. */
    readonly headerRow: number;
    /** The first column, from zero. */
    readonly firstColumn: number;
    /** The columns' names, in order: at least one, none empty, no two the same in any case. */
    readonly columns: readonly string[];
    /** The rows of data below the header: at least one, as Excel holds a table of none as one empty row. */
    readonly dataRows: number;
    /** Whether the header's cells carry an AutoFilter's buttons, as a table Excel makes does; true when left out. */
    readonly autoFilter?: boolean;
}

/**
 * Whether `name` is a name Excel gives a Table: a letter, `_` or `\`, then letters, digits, `_` and `.`; at most 255
 * characters; not a cell's address (`A1`, `A01`), not an R1C1 one, `R` or `C`, and not `TRUE` or `FALSE`. Whether another
 * table or a defined name has it is the workbook's to say.
 */
export function isValidTableName(name: string): boolean {
    // .NET's \p{L} reads UTF-16 units, and no half of a surrogate pair is a letter.
    return name.length > 0 && name.length <= 255 && !/[\uD800-\uDFFF]/.test(name) && /^[\p{L}_\\][\p{L}\p{Nd}_.]*$/u.test(name)
        && parseCell(name) === undefined && !/^[Rr][0-9]*[Cc][0-9]*$|^[RrCc]$/.test(name)
        && !equalsIgnoreCase(name, 'TRUE') && !equalsIgnoreCase(name, 'FALSE');
}

/** The cells the table covers, its header's included, as `A1:C4`. */
export function tableReference(table: XlsxTable): string {
    return `${formatCell(table.headerRow, table.firstColumn)}:${formatCell(table.headerRow + table.dataRows, table.firstColumn + table.columns.length - 1)}`;
}

/**
 * An element of a worksheet that the reader does not read, kept as far as a reader of the file needs to judge what it
 * holds: its name, its attributes, its text, and its children to `elementMaxDepth` levels, the first `elementMaxChildren`
 * of each. It is never the whole element, and is not written back.
 */
export interface XlsxElement {
    /** The element's local name, as `pageMargins`; an extension's without its prefix, as `conditionalFormattings`. */
    readonly name: string;
    /** Its attributes by local name (`r:id` is `id`), namespace declarations left out, in the file's order. */
    readonly attributes: ReadonlyMap<string, string>;
    /** Its child elements, the first `elementMaxChildren`, to `elementMaxDepth` levels below the worksheet's element. */
    readonly children: readonly XlsxElement[];
    /** Its own text, its first `elementMaxText` characters; null when it holds none. */
    readonly text: string | null;
    /** Whether it held children that `children` leaves out, past the limits. */
    readonly childrenCut: boolean;
}

/** The levels of children an `XlsxElement` keeps below a worksheet's element. */
export const elementMaxDepth = 3;

/** The children an `XlsxElement` keeps of each element, in the file's order. */
export const elementMaxChildren = 16;

/** The characters of an element's own text that an `XlsxElement` keeps. */
export const elementMaxText = 1024;
