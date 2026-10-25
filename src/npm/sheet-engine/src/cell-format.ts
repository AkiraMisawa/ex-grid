// A cell's Cell Format and the parts it is made of (C#'s CellColour.cs, CellFormat.cs, CellBorders.cs and
// CellDisplay.cs; CONTEXT.md; ADR-0047, ADR-0071). Each part is an immutable value with `equals`, and a `key` where
// a Map or a Set must tell two equal parts apart from two different ones.
import { hex } from './dotnet';
import type { NumberFormat, NumberFormatColour } from './number-format';

/**
 * A colour a Cell Format records (ADR-0071): `automatic`, or an RGB value. A colour picked from the theme part of a
 * palette is recorded as its RGB value. Automatic is not a recorded colour: a Font's Automatic is the Ink.
 */
export class CellColour {
    // -1 is Automatic, so 0x000000 stays black.
    readonly #value: number;

    private constructor(value: number) {
        this.#value = value;
    }

    /** Automatic: no colour recorded, so what is painted is decided where it is painted. */
    static readonly automatic = new CellColour(-1);

    /** An RGB colour, as `0xRRGGBB`. Throws a RangeError for a value outside `0x000000` to `0xFFFFFF`. */
    static fromRgb(rgb: number): CellColour {
        if (!Number.isInteger(rgb) || rgb < 0 || rgb > 0xFFFFFF) throw new RangeError(`An RGB colour is 0x000000 to 0xFFFFFF, not ${String(rgb)}.`);
        return new CellColour(rgb);
    }

    /** An RGB colour, from its red, green and blue, each 0 to 255. */
    static fromParts(red: number, green: number, blue: number): CellColour {
        for (const part of [red, green, blue])
            if (!Number.isInteger(part) || part < 0 || part > 255) throw new RangeError(`A colour's part is 0 to 255, not ${String(part)}.`);
        return new CellColour((red << 16) | (green << 8) | blue);
    }

    /** A colour written `#RRGGBB`, in either case, or `undefined` for any other text. @internal */
    static fromHex(text: string | undefined): CellColour | undefined {
        if (text === undefined || !/^#[0-9A-Fa-f]{6}$/u.test(text)) return undefined;
        return CellColour.fromRgb(Number.parseInt(text.slice(1), 16));
    }

    /** Whether the colour is Automatic. */
    get isAutomatic(): boolean {
        return this.#value < 0;
    }

    /** The RGB value, as `0xRRGGBB`. Throws for Automatic, which records no RGB value. */
    get rgb(): number {
        if (this.isAutomatic) throw new Error('An Automatic colour records no RGB value.');
        return this.#value;
    }

    /** @internal */
    get key(): number {
        return this.#value;
    }

    equals(other: CellColour): boolean {
        return this.#value === other.#value;
    }

    /** `Automatic`, or the RGB value as `#RRGGBB`. */
    toString(): string {
        return this.isAutomatic ? 'Automatic' : `#${hex(this.#value, 6)}`;
    }
}

/** A cell's horizontal alignment, settable per cell as in Excel (ADR-0046). `general` is Excel's default: numbers to the right, text to the left, booleans and Error Values centred. */
export type HorizontalAlignment = 'general' | 'left' | 'center' | 'right';

/**
 * A cell's Font (CONTEXT.md; ADR-0071): the colour and emphasis of its text. Not its size or typeface: every row
 * has one height, and one digit width decides what fits.
 */
export class CellFont {
    /** The text's colour; Automatic is the Ink. */
    readonly colour: CellColour;
    readonly bold: boolean;
    readonly italic: boolean;
    /** A single underline. */
    readonly underline: boolean;
    readonly strikethrough: boolean;

    constructor(parts: { colour?: CellColour; bold?: boolean; italic?: boolean; underline?: boolean; strikethrough?: boolean } = {}) {
        this.colour = parts.colour ?? CellColour.automatic;
        this.bold = parts.bold === true;
        this.italic = parts.italic === true;
        this.underline = parts.underline === true;
        this.strikethrough = parts.strikethrough === true;
    }

    /** What a cell's text shows where no level records a Font: Automatic, with no emphasis. */
    static readonly default = new CellFont();

    /** This Font with the parts given replaced. */
    with(parts: { colour?: CellColour; bold?: boolean; italic?: boolean; underline?: boolean; strikethrough?: boolean }): CellFont {
        return new CellFont({
            colour: parts.colour ?? this.colour,
            bold: parts.bold ?? this.bold,
            italic: parts.italic ?? this.italic,
            underline: parts.underline ?? this.underline,
            strikethrough: parts.strikethrough ?? this.strikethrough,
        });
    }

    /** @internal */
    get key(): string {
        return `${String(this.colour.key)}${this.bold ? 'b' : ''}${this.italic ? 'i' : ''}${this.underline ? 'u' : ''}${this.strikethrough ? 's' : ''}`;
    }

    equals(other: CellFont): boolean {
        return this.colour.equals(other.colour) && this.bold === other.bold && this.italic === other.italic
            && this.underline === other.underline && this.strikethrough === other.strikethrough;
    }
}

/**
 * A cell's Fill (CONTEXT.md; ADR-0071): the one solid colour behind its text, or `none`, where the Paper shows. No
 * patterns and no gradients.
 */
export class CellFill {
    /** The Fill's colour, always an RGB value; `undefined` for `none`. */
    readonly colour: CellColour | undefined;

    private constructor(colour: CellColour | undefined) {
        this.colour = colour;
    }

    /** No Fill: the Paper shows. Recorded on a cell, it hides a Fill its row or column records. */
    static readonly none = new CellFill(undefined);

    /**
     * A solid Fill in `colour`. Throws for Automatic: Excel's Fill offers No Color and colours, never Automatic, so
     * there is nothing an Automatic Fill would mean.
     */
    static solid(colour: CellColour): CellFill {
        if (colour.isAutomatic) throw new RangeError("A Fill's colour is an RGB value, not Automatic.");
        return new CellFill(colour);
    }

    /** Whether this is `none`. */
    get isNone(): boolean {
        return this.colour === undefined;
    }

    /** @internal */
    get key(): number {
        return this.colour === undefined ? -2 : this.colour.key;
    }

    equals(other: CellFill): boolean {
        return this.key === other.key;
    }
}

/** Excel's thirteen line styles for a Border (ADR-0071), and `none`. No diagonals. */
export type BorderLineStyle = 'none' | 'hair' | 'thin' | 'medium' | 'thick' | 'double' | 'dotted' | 'dashed' | 'dashDot' | 'dashDotDot'
    | 'mediumDashed' | 'mediumDashDot' | 'mediumDashDotDot' | 'slantedDashDot';

/** The line styles in C#'s enum order, which Sheet Documents and keys rely on. @internal */
export const borderLineStyles: readonly BorderLineStyle[] = ['none', 'hair', 'thin', 'medium', 'thick', 'double', 'dotted', 'dashed', 'dashDot',
    'dashDotDot', 'mediumDashed', 'mediumDashDot', 'mediumDashDotDot', 'slantedDashDot'];

/** A Border (CONTEXT.md; ADR-0071): the line on one side of a cell, in one of Excel's thirteen line styles and a colour, or `none`. */
export class BorderLine {
    /** The line style; `none` for no line. */
    readonly style: BorderLineStyle;
    /** The line's colour; Automatic for no line. */
    readonly colour: CellColour;

    /** A line in `style` and `colour`. `none` is no line, and has no colour. Throws a RangeError for a style that is not one. */
    constructor(style: BorderLineStyle, colour: CellColour = CellColour.automatic) {
        if (!borderLineStyles.includes(style)) throw new RangeError(`Not a line style: ${style}.`);
        this.style = style;
        this.colour = style === 'none' ? CellColour.automatic : colour;
    }

    /** No line. */
    static readonly none = new BorderLine('none');

    /** Whether this is no line. */
    get isNone(): boolean {
        return this.style === 'none';
    }

    /** @internal */
    get key(): string {
        return `${this.style}:${String(this.colour.key)}`;
    }

    equals(other: BorderLine): boolean {
        return this.style === other.style && this.colour.equals(other.colour);
    }
}

/**
 * A cell's Borders (ADR-0071): the line on each of its four sides. Each cell records its own four sides, as Excel's
 * files do, and the edge two cells share shows one line from either side: the upper cell's, or the left cell's for
 * a vertical edge, where both record one, and otherwise whichever does (the twelfth Windows run). A border command
 * records the edge on the cells it sets and clears the neighbour's record of it, so the later setting wins from
 * either side (the eleventh run, case 7).
 */
export class CellBorders {
    readonly top: BorderLine;
    readonly bottom: BorderLine;
    readonly left: BorderLine;
    readonly right: BorderLine;

    constructor(sides: { top?: BorderLine; bottom?: BorderLine; left?: BorderLine; right?: BorderLine } = {}) {
        this.top = sides.top ?? BorderLine.none;
        this.bottom = sides.bottom ?? BorderLine.none;
        this.left = sides.left ?? BorderLine.none;
        this.right = sides.right ?? BorderLine.none;
    }

    /** No line on any side. */
    static readonly none = new CellBorders();

    /** These Borders with the sides given replaced. */
    with(sides: { top?: BorderLine; bottom?: BorderLine; left?: BorderLine; right?: BorderLine }): CellBorders {
        return new CellBorders({
            top: sides.top ?? this.top,
            bottom: sides.bottom ?? this.bottom,
            left: sides.left ?? this.left,
            right: sides.right ?? this.right,
        });
    }

    /** @internal */
    get key(): string {
        return `${this.top.key}|${this.bottom.key}|${this.left.key}|${this.right.key}`;
    }

    equals(other: CellBorders): boolean {
        return this.top.equals(other.top) && this.bottom.equals(other.bottom) && this.left.equals(other.left) && this.right.equals(other.right);
    }
}

/**
 * The lines drawn along a range's four outer edges, each line once per edge (`Sheet.getEdgeLines`): what Format
 * Cells shows on a range's outline (ADR-0071; the fourteenth Windows run, case 13). Each list holds distinct lines,
 * in the order met.
 */
export interface RangeEdgeLines {
    readonly top: readonly BorderLine[];
    readonly bottom: readonly BorderLine[];
    readonly left: readonly BorderLine[];
    readonly right: readonly BorderLine[];
}

/**
 * A cell's Cell Format as it shows (CONTEXT.md; ADR-0047, ADR-0071): each part from the level that records it, cell
 * over row over column, and the part's default where no level does.
 */
export class CellFormat {
    readonly numberFormat: NumberFormat;

    readonly alignment: HorizontalAlignment;

    readonly font: CellFont;

    readonly fill: CellFill;

    readonly borders: CellBorders;

    constructor(
        numberFormat: NumberFormat,
        alignment: HorizontalAlignment,
        font: CellFont,
        fill: CellFill,
        borders: CellBorders,
    ) {
        this.numberFormat = numberFormat;
        this.alignment = alignment;
        this.font = font;
        this.fill = fill;
        this.borders = borders;
    }

    /** @internal */
    get key(): string {
        return `${this.numberFormat.code}\u0000${this.alignment}\u0000${this.font.key}\u0000${String(this.fill.key)}\u0000${this.borders.key}`;
    }

    equals(other: CellFormat): boolean {
        return this.numberFormat.equals(other.numberFormat) && this.alignment === other.alignment && this.font.equals(other.font)
            && this.fill.equals(other.fill) && this.borders.equals(other.borders);
    }
}

/**
 * What a cell shows: its Value formatted by the cell's number format under the Sheet's culture, and where it sits.
 * Whether it fits its column is the grid's to decide (ADR-0016): a number that does not fit becomes `####`, never a
 * shorter number.
 */
export interface CellDisplay {
    /** The formatted text; empty for a blank cell, and empty when `cannotShow` is set. */
    readonly text: string;
    /** The alignment resolved from the cell's setting: never `general`. */
    readonly alignment: Exclude<HorizontalAlignment, 'general'>;
    /** Whether the Value is a number (a date included). A number too wide for its column is shown as `####`; text is not (ADR-0016). */
    readonly isNumber: boolean;
    /**
     * The number cannot be shown in its format at any width — a negative date, or a date after 31 December 9999 —
     * or, from `Sheet.getDisplay` with a width, not in the width asked, and the cell shows `####`, as Excel's does.
     */
    readonly cannotShow: boolean;
    /**
     * The colour the Number Format names for the section that showed the Value, kept where the number cannot be
     * shown; `undefined` where that section names none, or where no section showed it (ADR-0071, SH-40). It takes
     * precedence over the cell's Font colour, as Excel's does.
     */
    readonly colour: NumberFormatColour | undefined;
}
