// The styles part as far as ADR-0330 §4 carries it, as ExGrid.Data.Xlsx's records hold it: the custom Number Formats,
// the fonts, fills and borders, and the cell formats that cells point at.

/** What a colour names (ECMA-376 `CT_Color`). */
export type XlsxColorKind =
    /** The automatic colour (`auto="1"`): the window's text or background, as the place it colours decides. */
    | 'automatic'
    /** An ARGB value (`rgb`). */
    | 'rgb'
    /** A slot of the workbook's theme (`theme`), resolved through `XlsxTheme`. */
    | 'theme'
    /** An entry of the legacy indexed palette (`indexed`). */
    | 'indexed';

/** A colour as the file writes it: an ARGB value, a theme slot, an indexed entry or the automatic colour, each with a
 * tint. `XlsxTheme.resolve` turns it into the RGB Excel paints (ADR-0330 §8). */
export interface XlsxColor {
    /** What the colour names. */
    readonly kind: XlsxColorKind;
    /** The ARGB value for `rgb` (alpha in the high byte, unsigned), the theme slot for `theme`, the palette index for
     * `indexed`; 0 for `automatic`. */
    readonly value: number;
    /** How far the colour is lightened (positive, up to 1) or darkened (negative, down to -1); 0 for none. */
    readonly tint: number;
}

/** How a font underlines (ECMA-376 `ST_UnderlineValues`). */
export type XlsxUnderline = 'none' | 'single' | 'double' | 'singleAccounting' | 'doubleAccounting';

/** A font of the styles part. */
export interface XlsxFont {
    /** The typeface, as `Calibri`, or null. */
    readonly name: string | null;
    /** The size in points, or null. */
    readonly size: number | null;
    /** Whether it is bold. */
    readonly bold: boolean;
    /** Whether it is italic. */
    readonly italic: boolean;
    /** How it is underlined. */
    readonly underline: XlsxUnderline;
    /** Whether it is struck through. */
    readonly strike: boolean;
    /** Its colour, or null for none written. */
    readonly color: XlsxColor | null;
}

/** A fill of the styles part. */
export interface XlsxFill {
    /** The pattern, as ECMA-376 `ST_PatternType` names it: `none`, `solid`, `gray125`, …; null for a gradient fill. */
    readonly pattern: string | null;
    /** The pattern's foreground colour — a solid fill's colour — or null. */
    readonly foreground: XlsxColor | null;
    /** The pattern's background colour, or null. */
    readonly background: XlsxColor | null;
    /** Whether it is a gradient fill, which carries no pattern. */
    readonly isGradient: boolean;
}

/** A border line's style: none, or one of Excel's thirteen (ECMA-376 `ST_BorderStyle`). */
export type XlsxBorderStyle =
    | 'none' | 'thin' | 'medium' | 'dashed' | 'dotted' | 'thick' | 'double' | 'hair'
    | 'mediumDashed' | 'dashDot' | 'mediumDashDot' | 'dashDotDot' | 'mediumDashDotDot' | 'slantDashDot';

/** One edge of a border: its line style and colour. */
export interface XlsxBorderEdge {
    /** The line's style; `none` for no line. */
    readonly style: XlsxBorderStyle;
    /** The line's colour, or null for none written. */
    readonly color: XlsxColor | null;
}

/** A border of the styles part: four edges and a diagonal. */
export interface XlsxBorder {
    /** The left edge. */
    readonly left: XlsxBorderEdge;
    /** The right edge. */
    readonly right: XlsxBorderEdge;
    /** The top edge. */
    readonly top: XlsxBorderEdge;
    /** The bottom edge. */
    readonly bottom: XlsxBorderEdge;
    /** The diagonal line, drawn where `diagonalUp` or `diagonalDown` says. */
    readonly diagonal: XlsxBorderEdge;
    /** Whether the diagonal rises from bottom left to top right. */
    readonly diagonalUp: boolean;
    /** Whether the diagonal falls from top left to bottom right. */
    readonly diagonalDown: boolean;
}

/** A cell's horizontal alignment (ECMA-376 `ST_HorizontalAlignment`). */
export type XlsxHorizontalAlignment = 'general' | 'left' | 'center' | 'right' | 'fill' | 'justify' | 'centerContinuous' | 'distributed';

/** A cell's vertical alignment (ECMA-376 `ST_VerticalAlignment`). */
export type XlsxVerticalAlignment = 'bottom' | 'top' | 'center' | 'justify' | 'distributed';

/** A cell's alignment, as an `<alignment>` writes it. */
export interface XlsxAlignment {
    /** Where the text lies across the cell. */
    readonly horizontal: XlsxHorizontalAlignment;
    /** Where the text lies up and down the cell. */
    readonly vertical: XlsxVerticalAlignment;
    /** Whether the text wraps onto more lines. */
    readonly wrapText: boolean;
    /** The indent, in steps. */
    readonly indent: number;
    /** The rotation, as the file writes it: 0 to 180, or 255 for stacked text. */
    readonly textRotation: number;
    /** Whether the text shrinks to fit the cell's width. */
    readonly shrinkToFit: boolean;
}

/** A cell format (`<xf>` of `cellXfs`): what a cell's style index points at. */
export interface XlsxCellFormat {
    /** The Number Format's id: a built-in one below 164, or one of the styles' own. */
    readonly numberFormatId: number;
    /** The index of its font in the styles' fonts. */
    readonly fontId: number;
    /** The index of its fill in the styles' fills. */
    readonly fillId: number;
    /** The index of its border in the styles' borders. */
    readonly borderId: number;
    /** The alignment, or null when the format writes none. */
    readonly alignment: XlsxAlignment | null;
}

/**
 * The styles part as far as ADR-0330 §4 carries it: the custom Number Formats, the fonts, fills and borders, and the cell
 * formats that cells point at. What else the part holds (named cell styles, differential formats, table styles) is not
 * read.
 */
export interface XlsxStyles {
    /** The custom Number Formats by id (164 and above, or a built-in id written over), each its format code. */
    readonly numberFormats: ReadonlyMap<number, string>;
    /** The fonts, which a cell format names by index. */
    readonly fonts: readonly XlsxFont[];
    /** The fills, the first two of which Excel reserves: `none` and `gray125`. */
    readonly fills: readonly XlsxFill[];
    /** The borders, which a cell format names by index. */
    readonly borders: readonly XlsxBorder[];
    /** The cell formats, which a cell's style index names; the first is the one a cell of no style takes. */
    readonly cellFormats: readonly XlsxCellFormat[];
    /** The indexed palette written over (`<colors><indexedColors>`), as ARGB values, or null when the file keeps the default one. */
    readonly indexedColors: readonly number[] | null;
}

/** The automatic colour. */
export const automaticColor: XlsxColor = Object.freeze({ kind: 'automatic', value: 0, tint: 0 });

/** An ARGB value, as `0xFF4472C4`. */
export function rgbColor(argb: number, tint = 0): XlsxColor {
    return Object.freeze({ kind: 'rgb', value: argb >>> 0, tint });
}

/** A theme slot, with a tint: 0 Background 1, 1 Text 1, 2 Background 2, 3 Text 2, 4 to 9 Accents 1 to 6, 10 Hyperlink,
 * 11 Followed Hyperlink. */
export function themeColor(slot: number, tint = 0): XlsxColor {
    return Object.freeze({ kind: 'theme', value: slot >>> 0, tint });
}

/** An entry of the indexed palette, 0 to 65. */
export function indexedColor(index: number, tint = 0): XlsxColor {
    return Object.freeze({ kind: 'indexed', value: index >>> 0, tint });
}

/** No line. */
export const noBorderEdge: XlsxBorderEdge = Object.freeze({ style: 'none', color: null });

/** No border: the first border of every styles part. */
export const noBorder: XlsxBorder = Object.freeze({
    left: noBorderEdge, right: noBorderEdge, top: noBorderEdge, bottom: noBorderEdge, diagonal: noBorderEdge, diagonalUp: false, diagonalDown: false,
});

/** No fill: the first fill of every styles part. */
export const noFill: XlsxFill = Object.freeze({ pattern: 'none', foreground: null, background: null, isGradient: false });

/** The 12.5% grey pattern Excel reserves as every styles part's second fill. */
export const gray125Fill: XlsxFill = Object.freeze({ pattern: 'gray125', foreground: null, background: null, isGradient: false });

/** A solid fill of `color`. */
export function solidFill(color: XlsxColor): XlsxFill {
    return Object.freeze({ pattern: 'solid', foreground: color, background: null, isGradient: false });
}

/** The alignment of a cell that writes none. */
export const defaultAlignment: XlsxAlignment = Object.freeze({
    horizontal: 'general', vertical: 'bottom', wrapText: false, indent: 0, textRotation: 0, shrinkToFit: false,
});

/** A map nobody can change: the default styles are shared by every workbook that takes them. */
class FrozenMap<K, V> extends Map<K, V> {
    constructor(entries: readonly (readonly [K, V])[]) {
        super(entries);
        Object.freeze(this);
    }

    override set(key: K, value: V): this {
        if (Object.isFrozen(this)) throw new TypeError('The default styles cannot be changed.');
        return super.set(key, value);
    }

    override delete(): boolean {
        throw new TypeError('The default styles cannot be changed.');
    }

    override clear(): void {
        throw new TypeError('The default styles cannot be changed.');
    }
}

/** The styles of a workbook that styles nothing: Calibri 11, no fill (and the reserved grey), no border, and one cell
 * format of all four. */
export const defaultStyles: XlsxStyles = Object.freeze({
    numberFormats: new FrozenMap<number, string>([]),
    fonts: Object.freeze([Object.freeze({
        name: 'Calibri', size: 11, bold: false, italic: false, underline: 'none', strike: false, color: themeColor(1),
    } satisfies XlsxFont)]),
    fills: Object.freeze([noFill, gray125Fill]),
    borders: Object.freeze([noBorder]),
    cellFormats: Object.freeze([Object.freeze({ numberFormatId: 0, fontId: 0, fillId: 0, borderId: 0, alignment: null } satisfies XlsxCellFormat)]),
    indexedColors: null,
});

/** The first Number Format id a styles part defines for itself; the ones below it are built in. */
export const firstCustomNumberFormat = 164;

export const borderStyleNames: readonly XlsxBorderStyle[] = [
    'none', 'thin', 'medium', 'dashed', 'dotted', 'thick', 'double', 'hair',
    'mediumDashed', 'dashDot', 'mediumDashDot', 'dashDotDot', 'mediumDashDotDot', 'slantDashDot',
];

export const horizontalNames: readonly XlsxHorizontalAlignment[] = ['general', 'left', 'center', 'right', 'fill', 'justify', 'centerContinuous', 'distributed'];

export const verticalNames: readonly XlsxVerticalAlignment[] = ['bottom', 'top', 'center', 'justify', 'distributed'];
