// A cell's appearance as a Consumer supplies it (ADR-0050, item 15; ADR-0071): its Font, its Fill and the
// Border on each side, painted as Excel paints them. Document data, not Visual Tokens: a Sheet's red for a
// breach is the user's choice and reads as chosen, so it is the one kind of colour that reaches the paint
// from the Consumer, and it reaches it only as the six hex digits of a class the core generates.

/** Excel's thirteen line styles for a Border (ADR-0071), and `none`, named as `@ex-grid/sheet-engine` names
 * them. Each is drawn at the eleventh Windows run's geometry: centred on the gridline, in device pixels. */
export type BorderStyle = 'none' | 'hair' | 'thin' | 'medium' | 'thick' | 'double' | 'dotted' | 'dashed' | 'dashDot'
    | 'dashDotDot' | 'mediumDashed' | 'mediumDashDot' | 'mediumDashDotDot' | 'slantedDashDot';

/** A Border (`CONTEXT.md`): the line on one side of a cell, in one of Excel's line styles and a colour. */
export interface CellBorder {
    /** The line style; `none` is no line. */
    readonly style: BorderStyle;
    /** The line's colour as `0xRRGGBB`; black unless given. A `none` line keeps no colour. */
    readonly colour?: number;
}

/**
 * A cell's appearance (ADR-0050, item 15): its Font, its Fill, and the Border on each side. A part not given
 * paints as a grid without the declaration does.
 *
 * Each part is painted as Excel paints it (ADR-0071). The Font gives way on a Stale or Error cell, whose
 * state must never be the one that disappears (ADR-0006). A Fill covers the gridlines at the cell's edges. A
 * Border is centred on its gridline, a thick line reaching into both cells; the line between two cells is
 * drawn once, and where both record different lines the grid's `edgeBorder` chooses. A bold cell is judged
 * by the bold widths (ADR-0016).
 */
export interface CellAppearance {
    /** The text's colour as `0xRRGGBB`; the grid's own unless given. */
    readonly fontColour?: number;
    /** Bold text, judged by the bold widths. */
    readonly bold?: boolean;
    /** Italic text, judged by the regular widths. */
    readonly italic?: boolean;
    /** A single underline. */
    readonly underline?: boolean;
    /** A line through the text. */
    readonly strikethrough?: boolean;
    /** The one solid colour behind the text as `0xRRGGBB`; none unless given. */
    readonly fill?: number;
    /** The line on the top side; none unless given. */
    readonly top?: CellBorder;
    /** The line on the right side; none unless given. */
    readonly right?: CellBorder;
    /** The line on the bottom side; none unless given. */
    readonly bottom?: CellBorder;
    /** The line on the left side; none unless given. */
    readonly left?: CellBorder;
}

/** Which line is drawn on an edge whose two cells record different lines (ADR-0050, item 15): asked with the
 * upper (left) cell's line and the lower (right) cell's, never when only one records a line or both record
 * the same. */
export type EdgeBorderOf = (upperOrLeft: CellBorder, lowerOrRight: CellBorder) => CellBorder;

/**
 * The widths a Consumer's painted text is fitted with (ADR-0050, item 11): the Cell Metrics the grid judges
 * `####` by, so a text fitted to them is a text the grid shows whole.
 */
export interface CellTextMeasure {
    /** What a tabular digit costs: Excel's unit of column width. */
    readonly digitWidthPx: number;
    /** Padding on one side; a cell pays it twice. */
    readonly cellHorizontalPaddingPx: number;
    /** The metrics a bold cell is judged by. */
    readonly bold: CellTextMeasure;
    /** The width a column of `columnWidthPx` leaves for its text. */
    contentWidthPx(columnWidthPx: number): number;
    /** What `text` costs, each glyph charged its class's or its own width. */
    textWidthPx(text: string): number;
}
