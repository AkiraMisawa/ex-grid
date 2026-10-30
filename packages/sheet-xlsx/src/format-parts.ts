import { CellBorders, CellFill, CellFont, CellFormat, NumberFormat, type HorizontalAlignment } from '@ex-grid/sheet-engine';

// The parts of a Cell Format one level records — a cell, a row or a column (ADR-0047, ADR-0071) — each `undefined` where the
// level takes it from the level beneath, as ExSheet.Xlsx's FormatParts holds them.

/** The Cell Format a Sheet shows where no level records a part. */
export const defaultFormat = new CellFormat(NumberFormat.general, 'general', CellFont.default, CellFill.none, CellBorders.none);

export class FormatParts {
    static readonly empty = new FormatParts(undefined, undefined, undefined, undefined, undefined);

    readonly numberFormat: NumberFormat | undefined;
    readonly alignment: HorizontalAlignment | undefined;
    readonly font: CellFont | undefined;
    readonly fill: CellFill | undefined;
    readonly borders: CellBorders | undefined;

    constructor(
        numberFormat: NumberFormat | undefined,
        alignment: HorizontalAlignment | undefined,
        font: CellFont | undefined,
        fill: CellFill | undefined,
        borders: CellBorders | undefined,
    ) {
        this.numberFormat = numberFormat;
        this.alignment = alignment;
        this.font = font;
        this.fill = fill;
        this.borders = borders;
    }

    get isEmpty(): boolean {
        return this.numberFormat === undefined && this.alignment === undefined && this.font === undefined && this.fill === undefined && this.borders === undefined;
    }

    /** This level over `under`, part by part. */
    over(under: CellFormat): CellFormat {
        return new CellFormat(
            this.numberFormat === undefined ? under.numberFormat : this.numberFormat,
            this.alignment === undefined ? under.alignment : this.alignment,
            this.font === undefined ? under.font : this.font,
            this.fill === undefined ? under.fill : this.fill,
            this.borders === undefined ? under.borders : this.borders);
    }

    /** The parts of `format` that differ from `under`: what a level records so that it shows `format` over it. */
    static differing(format: CellFormat, under: CellFormat): FormatParts {
        return new FormatParts(
            format.numberFormat.equals(under.numberFormat) ? undefined : format.numberFormat,
            format.alignment === under.alignment ? undefined : format.alignment,
            format.font.equals(under.font) ? undefined : format.font,
            format.fill.equals(under.fill) ? undefined : format.fill,
            format.borders.equals(under.borders) ? undefined : format.borders);
    }

    /** Whether the two levels record the same parts alike, as the C# record compares. */
    equals(other: FormatParts): boolean {
        return same(this.numberFormat, other.numberFormat, (a, b) => a.equals(b))
            && this.alignment === other.alignment
            && same(this.font, other.font, (a, b) => a.equals(b))
            && same(this.fill, other.fill, (a, b) => a.equals(b))
            && same(this.borders, other.borders, (a, b) => a.equals(b));
    }


    /** The parts a level records, as a mask: Number Format, alignment, Font, Fill, borders. */
    get mask(): number {
        return (this.numberFormat === undefined ? 0 : 1) | (this.alignment === undefined ? 0 : 2) | (this.font === undefined ? 0 : 4)
            | (this.fill === undefined ? 0 : 8) | (this.borders === undefined ? 0 : 16);
    }
}

function same<T>(a: T | undefined, b: T | undefined, equal: (a: T, b: T) => boolean): boolean {
    return a === undefined || b === undefined ? a === b : equal(a, b);
}
