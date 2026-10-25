// What a formatting command sets, and what a row, a column or a cell records of a Cell Format (C#'s
// CellFormatChange.cs, the AxisFormat of Sheet.CellFormat.cs and the CellState of Sheet.State.cs; ADR-0047,
// ADR-0071).
import { CellAddress, columnCount, rowCount } from './cell-address';
import { BorderLine, CellBorders, CellFill, CellFont, CellFormat, type CellColour, type HorizontalAlignment } from './cell-format';
import { CellRange } from './cell-range';
import type { Entry } from './entry';
import { NumberFormat } from './number-format';
import type { Value } from './value';

/** What a cell shows where no level records anything: General, General alignment, the default Font, no Fill and no Borders. */
export const defaultCellFormat = new CellFormat(NumberFormat.general, 'general', CellFont.default, CellFill.none, CellBorders.none);

/** A part of a Cell Format: what `own` compares. */
type Part = NumberFormat | CellFont | CellFill | CellBorders | BorderLine;

/** Whether two parts, recorded or not, are the same. @internal */
export function samePart<T extends Part | HorizontalAlignment>(a: T | undefined, b: T | undefined): boolean {
    if (a === undefined || b === undefined) return a === b;
    if (typeof a === 'string' || typeof b === 'string') return a === b;
    return a.equals(b as never); // oxlint-disable-line typescript/no-unsafe-type-assertion -- both are the same kind of part, which T says
}

/** `value`, or `undefined` where it equals what shows under it anyway. @internal */
export function own<T extends Part | HorizontalAlignment>(value: T, under: T): T | undefined {
    return samePart(value, under) ? undefined : value;
}

/** The parts of a Cell Format recorded on a whole row or a whole column: `undefined` where that level records nothing. @internal */
export class AxisFormat {
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

    /** The level that records nothing. */
    static readonly empty = new AxisFormat(undefined, undefined, undefined, undefined, undefined);

    get isEmpty(): boolean {
        return this.numberFormat === undefined && this.alignment === undefined && this.font === undefined && this.fill === undefined && this.borders === undefined;
    }

    /** What the level shows over `under`: each part it records, else `under`'s. */
    over(under: CellFormat): CellFormat {
        return new CellFormat(this.numberFormat ?? under.numberFormat, this.alignment ?? under.alignment, this.font ?? under.font,
            this.fill ?? under.fill, this.borders ?? under.borders);
    }

    /** The level with each part `change` sets taken from `next`, and recorded as nothing where it equals what shows under the level anyway. */
    with(change: CellFormatChange, next: CellFormat, under: CellFormat): AxisFormat {
        return new AxisFormat(
            change.numberFormat === undefined ? this.numberFormat : own(next.numberFormat, under.numberFormat),
            change.alignment === undefined ? this.alignment : own(next.alignment, under.alignment),
            change.setsFont ? own(next.font, under.font) : this.font,
            change.fill === undefined ? this.fill : own(next.fill, under.fill),
            change.setsBorders ? own(next.borders, under.borders) : this.borders);
    }

    /** This level with the parts given replaced; a part given as `undefined` records nothing. */
    replacing(parts: { numberFormat?: NumberFormat | undefined; alignment?: HorizontalAlignment | undefined; fill?: CellFill | undefined; borders?: CellBorders | undefined }): AxisFormat {
        return new AxisFormat(
            'numberFormat' in parts ? parts.numberFormat : this.numberFormat,
            'alignment' in parts ? parts.alignment : this.alignment,
            this.font,
            'fill' in parts ? parts.fill : this.fill,
            'borders' in parts ? parts.borders : this.borders);
    }

    /** A key two equal levels share and two different ones do not. */
    get key(): string {
        return `${this.numberFormat === undefined ? '-' : `n${this.numberFormat.code}`}\u0000${this.alignment ?? '-'}\u0000${this.font?.key ?? '-'}\u0000${this.fill === undefined ? '-' : String(this.fill.key)}\u0000${this.borders?.key ?? '-'}`; // oxlint-disable-line ex-grid/no-stand-in-default -- '-' writes a part not recorded in the key
    }

    equals(other: AxisFormat): boolean {
        return samePart(this.numberFormat, other.numberFormat) && this.alignment === other.alignment && samePart(this.font, other.font)
            && samePart(this.fill, other.fill) && samePart(this.borders, other.borders);
    }
}

/** The parts a Cell Format change may set. */
export interface CellFormatChangeParts {
    /** The Number Format to set; `NumberFormat.general` sets General. */
    readonly numberFormat?: NumberFormat | undefined;
    /** The horizontal alignment to set; `general` sets General. */
    readonly alignment?: HorizontalAlignment | undefined;
    /** The Font colour to set; Automatic sets the Ink. */
    readonly fontColour?: CellColour | undefined;
    readonly bold?: boolean | undefined;
    readonly italic?: boolean | undefined;
    /** A single underline set on, or off. */
    readonly underline?: boolean | undefined;
    readonly strikethrough?: boolean | undefined;
    /** The Fill to set; `CellFill.none` takes it away. */
    readonly fill?: CellFill | undefined;
    /** The Borders to set, relative to each range the change is set on. */
    readonly borders?: BorderChange | undefined;
}

/**
 * What a formatting command sets on the Cell Format of ranges (ADR-0071): every part that is given, each Font
 * emphasis on its own, and every other part left as each cell has it. Its `borders` are relative to each range it
 * is set on, so several ranges each get their own outline, as in Excel.
 */
export class CellFormatChange implements CellFormatChangeParts {
    readonly numberFormat: NumberFormat | undefined;
    readonly alignment: HorizontalAlignment | undefined;
    readonly fontColour: CellColour | undefined;
    readonly bold: boolean | undefined;
    readonly italic: boolean | undefined;
    readonly underline: boolean | undefined;
    readonly strikethrough: boolean | undefined;
    readonly fill: CellFill | undefined;
    readonly borders: BorderChange | undefined;

    constructor(parts: CellFormatChangeParts) {
        this.numberFormat = parts.numberFormat;
        this.alignment = parts.alignment;
        this.fontColour = parts.fontColour;
        this.bold = parts.bold;
        this.italic = parts.italic;
        this.underline = parts.underline;
        this.strikethrough = parts.strikethrough;
        this.fill = parts.fill;
        this.borders = parts.borders;
    }

    /** This change with its Borders replaced. @internal */
    withBorders(borders: BorderChange): CellFormatChange {
        return new CellFormatChange({ ...this.#parts(), borders });
    }

    #parts(): CellFormatChangeParts {
        return {
            numberFormat: this.numberFormat, alignment: this.alignment, fontColour: this.fontColour, bold: this.bold, italic: this.italic,
            underline: this.underline, strikethrough: this.strikethrough, fill: this.fill, borders: this.borders,
        };
    }

    /** Whether the change sets no part at all. */
    get isEmpty(): boolean {
        return this.numberFormat === undefined && this.alignment === undefined && !this.setsFont && this.fill === undefined && !this.setsBorders;
    }

    /** Whether the change sets any part of the Font. @internal */
    get setsFont(): boolean {
        return this.fontColour !== undefined || this.bold !== undefined || this.italic !== undefined || this.underline !== undefined || this.strikethrough !== undefined;
    }

    /** Whether the change sets any side. @internal */
    get setsBorders(): boolean {
        return this.borders !== undefined && !this.borders.isEmpty;
    }

    /** Whether the change sets a part that `level` records. @internal */
    touches(level: AxisFormat): boolean {
        return (this.numberFormat !== undefined && level.numberFormat !== undefined)
            || (this.alignment !== undefined && level.alignment !== undefined)
            || (this.setsFont && level.font !== undefined)
            || (this.fill !== undefined && level.fill !== undefined)
            || (this.setsBorders && level.borders !== undefined);
    }

    /** What a cell that showed `shown` shows once the change is set on it, at `place` in its range. @internal */
    applyTo(shown: CellFormat, place: PlaceInRange): CellFormat {
        return new CellFormat(
            this.numberFormat ?? shown.numberFormat,
            this.alignment ?? shown.alignment,
            this.applyToFont(shown.font),
            this.fill ?? shown.fill,
            this.borders === undefined ? shown.borders : this.borders.applyTo(shown.borders, place));
    }

    /** `font` with the emphases and the colour the change sets. @internal */
    applyToFont(font: CellFont): CellFont {
        return new CellFont({
            colour: this.fontColour ?? font.colour,
            bold: this.bold ?? font.bold,
            italic: this.italic ?? font.italic,
            underline: this.underline ?? font.underline,
            strikethrough: this.strikethrough ?? font.strikethrough,
        });
    }
}

/** The edges a Border change may set. */
export interface BorderChangeParts {
    /** The range's top edge: the top side of its first row's cells. The cells above them lose their record of it. */
    readonly top?: BorderLine | undefined;
    /** The range's bottom edge: the bottom side of its last row's cells. The cells below them lose their record of it. */
    readonly bottom?: BorderLine | undefined;
    /** The range's left edge: the left side of its first column's cells. The cells to their left lose their record of it. */
    readonly left?: BorderLine | undefined;
    /** The range's right edge: the right side of its last column's cells. The cells to their right lose their record of it. */
    readonly right?: BorderLine | undefined;
    /** Every edge between two of the range's rows: the bottom side of the upper cell and the top side of the lower. */
    readonly insideHorizontal?: BorderLine | undefined;
    /** Every edge between two of the range's columns: the right side of the left cell and the left side of the right. */
    readonly insideVertical?: BorderLine | undefined;
}

/**
 * The Borders a change sets, relative to each range it is set on (ADR-0071), as Excel's Format Cells sets them:
 * each edge given is set — to a line, or to `BorderLine.none` to take the line away — and every other edge is left
 * as each cell has it. `outline`, `inside` and `none` are Excel's presets. As Excel keeps the line between two
 * cells (the twelfth Windows run): an outer edge is recorded on the range's own cells, and the cells beside it lose
 * their record of the same edge.
 */
export class BorderChange implements BorderChangeParts {
    readonly top: BorderLine | undefined;
    readonly bottom: BorderLine | undefined;
    readonly left: BorderLine | undefined;
    readonly right: BorderLine | undefined;
    readonly insideHorizontal: BorderLine | undefined;
    readonly insideVertical: BorderLine | undefined;

    constructor(parts: BorderChangeParts) {
        this.top = parts.top;
        this.bottom = parts.bottom;
        this.left = parts.left;
        this.right = parts.right;
        this.insideHorizontal = parts.insideHorizontal;
        this.insideVertical = parts.insideVertical;
    }

    /** Excel's Outline: `line` on the range's four outer edges. */
    static outline(line: BorderLine): BorderChange {
        return new BorderChange({ top: line, bottom: line, left: line, right: line });
    }

    /** Excel's Inside: `line` on every edge inside the range. */
    static inside(line: BorderLine): BorderChange {
        return new BorderChange({ insideHorizontal: line, insideVertical: line });
    }

    /** Excel's None: every edge of the range, outer and inner, without a line. */
    static readonly none = new BorderChange({
        top: BorderLine.none, bottom: BorderLine.none, left: BorderLine.none, right: BorderLine.none,
        insideHorizontal: BorderLine.none, insideVertical: BorderLine.none,
    });

    /** Whether the change sets no edge. @internal */
    get isEmpty(): boolean {
        return this.top === undefined && this.bottom === undefined && this.left === undefined && this.right === undefined
            && this.insideHorizontal === undefined && this.insideVertical === undefined;
    }

    /** The four sides a cell at `place` in its range shows once the change is set, from `borders`. @internal */
    applyTo(borders: CellBorders, place: PlaceInRange): CellBorders {
        return new CellBorders({
            top: (place.firstRow ? this.top : this.insideHorizontal) ?? borders.top,
            bottom: (place.lastRow ? this.bottom : this.insideHorizontal) ?? borders.bottom,
            left: (place.firstColumn ? this.left : this.insideVertical) ?? borders.left,
            right: (place.lastColumn ? this.right : this.insideVertical) ?? borders.right,
        });
    }

    /**
     * The change as it is set on `range`. Over whole rows short of the whole Sheet, the left of column A is the
     * range's left edge, and where the change sets no left edge of its own it takes the inside vertical line (the
     * fourteenth Windows run, case 14). Over any other range the change is as it is. @internal
     */
    on(range: CellRange): BorderChange {
        return this.left === undefined && this.insideVertical !== undefined && range.isWholeRows && !range.isWholeColumns
            ? new BorderChange({ ...this.#parts(), left: this.insideVertical })
            : this;
    }

    #parts(): BorderChangeParts {
        return { top: this.top, bottom: this.bottom, left: this.left, right: this.right, insideHorizontal: this.insideHorizontal, insideVertical: this.insideVertical };
    }

    /**
     * The cells beside `range` across each outer edge the change sets, with the change that clears their record of
     * it (the twelfth Windows run). An edge on the Sheet's outer edge has none. @internal
     */
    *beside(range: CellRange): Generator<{ range: CellRange; borders: BorderChange }> {
        const { first, last } = range;
        if (this.top !== undefined && first.row > 0) {
            yield { range: new CellRange(new CellAddress(first.row - 1, first.column), new CellAddress(first.row - 1, last.column)), borders: new BorderChange({ bottom: BorderLine.none }) };
        }
        if (this.bottom !== undefined && last.row < rowCount - 1) {
            yield { range: new CellRange(new CellAddress(last.row + 1, first.column), new CellAddress(last.row + 1, last.column)), borders: new BorderChange({ top: BorderLine.none }) };
        }
        if (this.left !== undefined && first.column > 0) {
            yield { range: new CellRange(new CellAddress(first.row, first.column - 1), new CellAddress(last.row, first.column - 1)), borders: new BorderChange({ right: BorderLine.none }) };
        }
        if (this.right !== undefined && last.column < columnCount - 1) {
            yield { range: new CellRange(new CellAddress(first.row, last.column + 1), new CellAddress(last.row, last.column + 1)), borders: new BorderChange({ left: BorderLine.none }) };
        }
    }
}

/** Where a cell lies in a range a change is set on: which of the range's outer edges its sides are. @internal */
export class PlaceInRange {
    readonly firstRow: boolean;

    readonly lastRow: boolean;

    readonly firstColumn: boolean;

    readonly lastColumn: boolean;

    constructor(firstRow: boolean, lastRow: boolean, firstColumn: boolean, lastColumn: boolean) {
        this.firstRow = firstRow;
        this.lastRow = lastRow;
        this.firstColumn = firstColumn;
        this.lastColumn = lastColumn;
    }

    /** A cell set on its own: all four sides are outer edges. */
    static readonly alone = new PlaceInRange(true, true, true, true);

    /** A cell none of whose sides is an outer edge. */
    static readonly inside = new PlaceInRange(false, false, false, false);

    /**
     * Where `at` lies in `range`, as Excel places an outline: whole columns have no top or bottom edge, whole rows
     * no right edge, and the whole Sheet no outer edge (the eleventh and twelfth Windows runs).
     */
    static of(range: CellRange, at: CellAddress): PlaceInRange {
        const columns = range.isWholeColumns;
        const rows = range.isWholeRows;
        return new PlaceInRange(
            !columns && at.row === range.first.row,
            !columns && at.row === range.last.row,
            !(columns && rows) && at.column === range.first.column,
            !rows && at.column === range.last.column);
    }
}

/**
 * What one cell holds and shows, compared before and after an operation. The parts of its Cell Format are the
 * cell's own; `undefined` takes the row's or the column's (ADR-0047, ADR-0071). @internal
 */
export class CellState {
    readonly entry: Entry | undefined;

    readonly value: Value | undefined;

    readonly numberFormat: NumberFormat | undefined;

    readonly alignment: HorizontalAlignment | undefined;

    readonly font: CellFont | undefined;

    readonly fill: CellFill | undefined;

    readonly borders: CellBorders | undefined;

    constructor(
        entry: Entry | undefined,
        value: Value | undefined,
        numberFormat: NumberFormat | undefined,
        alignment: HorizontalAlignment | undefined,
        font: CellFont | undefined,
        fill: CellFill | undefined,
        borders: CellBorders | undefined,
    ) {
        this.entry = entry;
        this.value = value;
        this.numberFormat = numberFormat;
        this.alignment = alignment;
        this.font = font;
        this.fill = fill;
        this.borders = borders;
    }

    static readonly blank = new CellState(undefined, undefined, undefined, undefined, undefined, undefined, undefined);

    /** The same cell as recorded: Entry and Cell Format, the Value left out. */
    get recorded(): CellState {
        return this.value === undefined ? this : new CellState(this.entry, undefined, this.numberFormat, this.alignment, this.font, this.fill, this.borders);
    }

    /** This state with its Entry replaced. */
    withEntry(entry: Entry | undefined): CellState {
        return new CellState(entry, this.value, this.numberFormat, this.alignment, this.font, this.fill, this.borders);
    }

    /** This state with its Cell Format replaced, part by part. */
    withFormat(format: { numberFormat: NumberFormat | undefined; alignment: HorizontalAlignment | undefined; font: CellFont | undefined; fill: CellFill | undefined; borders: CellBorders | undefined }): CellState {
        return new CellState(this.entry, this.value, format.numberFormat, format.alignment, format.font, format.fill, format.borders);
    }

    sameRecord(other: CellState): boolean {
        return sameEntry(this.entry, other.entry) && this.sameFormat(other);
    }

    /** Whether the two record the same Cell Format of their own. */
    sameFormat(other: { numberFormat: NumberFormat | undefined; alignment: HorizontalAlignment | undefined; font: CellFont | undefined; fill: CellFill | undefined; borders: CellBorders | undefined }): boolean {
        return samePart(this.numberFormat, other.numberFormat) && this.alignment === other.alignment && samePart(this.font, other.font)
            && samePart(this.fill, other.fill) && samePart(this.borders, other.borders);
    }

    /** The Cell Format the state shows, where it records nothing of a part, the part's default. */
    get cellFormat(): CellFormat {
        return new CellFormat(
            this.numberFormat ?? NumberFormat.general,
            this.alignment ?? 'general', // oxlint-disable-line ex-grid/no-stand-in-default -- a part recorded nowhere shows its default
            this.font ?? CellFont.default,
            this.fill ?? CellFill.none,
            this.borders ?? CellBorders.none);
    }
}

function sameEntry(a: Entry | undefined, b: Entry | undefined): boolean {
    if (a === undefined || b === undefined) return a === b;
    return a.equals(b);
}
