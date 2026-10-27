import type { WorkbookDocument } from '@ex-grid/sheet-engine';

// What reading an .xlsx returns beside the Workbook Document (CONTEXT.md, ADR-0330 §5), as ExSheet.Xlsx's OpenReport says
// it: what the file held that ExSheet did not carry, and the cells whose Formulas name a function ExSheet does not support,
// each with its Sheet and place. The package shows nothing; the application decides how to tell the user.

/** What kind of thing an `OpenReport` says was not carried (ADR-0330 §4). */
export type NotCarriedKind =
    /** A worksheet that is hidden: it opens shown (ADR-0331 §4). */
    | 'hiddenSheet'
    /** A sheet that is not a worksheet — a chart sheet, a dialog or macro sheet — which holds no cells. */
    | 'chartSheet'
    /** A defined name, which no Formula carried reads; `detail` is its name, and `sheet` the sheet it is local to, or `null`
     * for a name of the whole workbook. */
    | 'definedName'
    /** Rows given heights of their own. */
    | 'rowHeights'
    /** Rows that are hidden: they open shown. */
    | 'hiddenRows'
    /** Columns that are hidden: they open shown. */
    | 'hiddenColumns'
    /** Panes frozen in the Sheet's view. */
    | 'frozenPanes'
    /** Merged cells: each cell is carried on its own. */
    | 'mergedCells'
    /** Conditional formatting. */
    | 'conditionalFormatting'
    /** Data validation. */
    | 'dataValidation'
    /** Notes and comments. */
    | 'notesAndComments'
    /** Hyperlinks: the cells' text is carried. */
    | 'hyperlinks'
    /** Pictures, charts, shapes and controls drawn over the Sheet. */
    | 'picturesAndCharts'
    /** The Sheet's protection. */
    | 'protection'
    /** Print settings beyond Excel's defaults: margins, a page set-up of its own, headers and footers with text, page breaks,
     * printed gridlines or headings, a fit to pages, a print area or print titles. */
    | 'printSettings'
    /** An AutoFilter, and the sort state it keeps. */
    | 'autoFilter'
    /** The structure of an Excel Table that no Formula reads: its cells are carried. */
    | 'excelTable'
    /** The Sheet's tab colour. */
    | 'sheetProperties'
    /** Rich-text runs inside cells: the text is carried, the runs' own formatting is not. */
    | 'richText'
    /** A PivotTable: the cells it shows are carried as constants, the PivotTable is not. */
    | 'pivotTables'
    /** Links to other workbooks that no Formula carried reads. */
    | 'externalLinks'
    /** Connections to outside data, and the queries that read through them. */
    | 'dataConnections'
    /** Slicers and timelines. */
    | 'slicers'
    /** View settings that change how the Sheet looks: hidden gridlines, headings or zeros, Formulas shown, right to left, a
     * zoom, a page layout. */
    | 'view'
    /** Anything else a worksheet holds; `detail` names its element, as `customSheetViews`. */
    | 'other'
    /** Colours that name the workbook's theme: each is carried as the RGB value the file's theme gives it, and the link to the
     * theme is not, so another theme does not recolour them (ADR-0330 §8). Said once, of the whole workbook. */
    | 'themeColours'
    /** A typeface or a size other than the workbook's own font's: the cells open in the Sheet's one font (ADR-0071). */
    | 'fontNamesAndSizes'
    /** Vertical alignment other than bottom. */
    | 'verticalAlignment'
    /** Wrapped, indented, rotated or shrunk text. */
    | 'textLayout'
    /** A horizontal alignment a Sheet does not have — fill, justify, distributed, centred across the selection: the cells open
     * aligned as General. */
    | 'horizontalAlignment'
    /** A double or accounting underline: the cells open with a single underline. */
    | 'underline'
    /** A pattern or gradient fill, or a solid one of no colour: the cells open with no Fill. */
    | 'patternFills'
    /** Diagonal borders: the cells keep their four sides. */
    | 'diagonalBorders'
    /** A default column width of the Sheet's own: its columns without a width of their own open at the default width. */
    | 'defaultColumnWidth';

/** One thing the file held that was not carried (ADR-0330 §4). */
export interface NotCarried {
    /** What it is. */
    readonly kind: NotCarriedKind;
    /** The Sheet it was on, or `null` for what the workbook as a whole held. */
    readonly sheet: string | null;
    /** What names it more closely, where something does: a defined name, the first cell of rich text or of a format not
     * carried (a cell as `B7`, columns as `B:C`, rows as `5:5`), an element's name; otherwise `null`. */
    readonly detail: string | null;
    /** One sentence an application can show as it stands. */
    readonly description: string;
}

/** A cell whose Formula names a function ExSheet does not support (ADR-0330 §4, §5): the Formula is carried, and shows
 * `#NAME?`. */
export interface UnsupportedFunctionCell {
    /** The Sheet's name. */
    readonly sheet: string;
    /** The cell, as `B7`. */
    readonly cell: string;
    /** The functions it names that are not supported, in the Formula's order. */
    readonly functions: readonly string[];
}

/** What reading an .xlsx returns beside the Workbook Document (ADR-0330 §5). */
export class OpenReport {
    /**
     * What the file held that changes how a Sheet looks, or what else the file held, and that ExSheet did not carry
     * (ADR-0330 §4's "Not carried"), in the workbook's order: what the workbook holds first, then each Sheet's in turn. Saving
     * the Workbook again will not keep any of it.
     */
    readonly notCarried: readonly NotCarried[];

    /**
     * The cells whose Formula names a function outside ExSheet's declared set (`DeclaredFunction`), in Sheet order and then
     * row by row: each is carried, and shows `#NAME?` (ADR-0047, ADR-0330 §4).
     */
    readonly unsupportedFunctions: readonly UnsupportedFunctionCell[];

    /** @internal */
    constructor(notCarried: readonly NotCarried[], unsupportedFunctions: readonly UnsupportedFunctionCell[]) {
        this.notCarried = Object.freeze(notCarried.map(n => Object.freeze({ ...n })));
        this.unsupportedFunctions = Object.freeze(unsupportedFunctions.map(u => Object.freeze({ ...u, functions: Object.freeze([...u.functions]) })));
        Object.freeze(this);
    }

    /** Whether the file was carried whole: nothing left behind, and every function it names supported. */
    get isEmpty(): boolean {
        return this.notCarried.length === 0 && this.unsupportedFunctions.length === 0;
    }
}

/** A Workbook Document read from an .xlsx, and its Open Report (ADR-0330 §5). */
export interface XlsxOpenResult {
    /** The Workbook Document: every worksheet a Sheet, in the file's order, the one shown shown. */
    readonly document: WorkbookDocument;
    /** What was not carried, and the cells naming functions ExSheet does not support. */
    readonly report: OpenReport;
}
