// What Format Cells opens on, and what the user has touched since (ADR-0071, SH-45), the counterpart of C#'s
// FormatCellsDraft and FormatCellsSpread: the rules every Chrome's Format Cells sets its controls by, so that
// what a choice means and what OK sets are ExSheet's under every Chrome.
//
// The draft is the dialog's state while it is open, changed in place by each choice as the C# draft is: a
// Chrome reads it after each call. What it hands out — the change OK sets, the lists it offers — is frozen.

import {
    BorderChange, BorderLine, CellAddress, CellFill, CellFormatChange, CellRange, NumberFormat,
    type BorderLineStyle, type CellColour, type CellFont, type CellFormat, type Culture, type HorizontalAlignment,
    type NumberFormatReading, type Sheet,
} from '@ex-grid/sheet-engine';
import { automaticColour, colourFromRgb } from './cell-colour';
import { currencySymbolOf } from './culture-facts';
import {
    formatCellsOffer, isDisabledCategory,
    type BorderEdge, type BorderPreset, type ColourTarget, type FontStyle, type NumberFormatCategory,
} from './format-cells-offer';
import { unreachable } from './must';
import {
    currencyCode, currencyNegativeStyles, customTypesIn, dateTypes, defaultPlaces, mostDecimalPlaces, numberCode,
    numberNegativeStyles, percentageCode, recogniseNumberFormat, scientificCode, timeTypes, type RecognisedNumberFormat,
} from './number-format-codes';
import { SheetWords, type FormatCellsRefusal } from './sheet-words';

/** Each edge's one line across the Selection: `undefined` where it differs, drawn as a grey dotted line. */
type EdgeLines = Record<BorderEdge, BorderLine | undefined>;

/**
 * What differs across a Selection, as Format Cells shows it (ADR-0071): whether bold or italic differs, whether
 * the Fill does, each edge's one line, or `undefined` where it differs. An outer edge is read as it is drawn, so
 * a neighbour's line shows where the cell records none (the fourteenth Windows run, case 13); an edge inside a
 * range is read from its cells' own sides, so a thick bottom over a plain cell differs (the eleventh run, case
 * 24).
 */
interface FormatCellsSpread {
    readonly fontStyleDiffers: boolean;
    readonly fillDiffers: boolean;
    readonly edges: Readonly<EdgeLines>;
    readonly insideHorizontal: boolean;
    readonly insideVertical: boolean;
}

function distinct<T extends { equals(other: T): boolean }>(items: readonly T[]): T[] {
    const kept: T[] = [];
    for (const item of items) {
        if (!kept.some(other => other.equals(item))) kept.push(item);
    }
    return kept;
}

function spreadOf(sheet: Sheet, ranges: readonly CellRange[]): FormatCellsSpread {
    const shown = ranges.flatMap(range => sheet.getCellFormats(range));
    const sides: Record<BorderEdge, BorderLine[]> = { top: [], insideHorizontal: [], bottom: [], left: [], insideVertical: [], right: [] };
    const add = (edge: BorderEdge, part: CellRange, side: (format: CellFormat) => BorderLine): void => {
        for (const format of sheet.getCellFormats(part)) sides[edge].push(side(format));
    };
    for (const range of ranges) {
        const { first, last } = range;
        // An outer edge shows as it is drawn: a neighbour's line where the cell records none (the fourteenth
        // Windows run, case 13).
        const outer = sheet.getEdgeLines(range);
        sides.top.push(...outer.top);
        sides.bottom.push(...outer.bottom);
        sides.left.push(...outer.left);
        sides.right.push(...outer.right);
        // An edge inside the range compares the cells' own sides (the eleventh run, case 24): the lower side of
        // every row but the last and the upper side of every row but the first; and the same across columns.
        if (range.rowCount > 1) {
            add('insideHorizontal', new CellRange(first, new CellAddress(last.row - 1, last.column)), format => format.borders.bottom);
            add('insideHorizontal', new CellRange(new CellAddress(first.row + 1, first.column), last), format => format.borders.top);
        }
        if (range.columnCount > 1) {
            add('insideVertical', new CellRange(first, new CellAddress(last.row, last.column - 1)), format => format.borders.right);
            add('insideVertical', new CellRange(new CellAddress(first.row, first.column + 1), last), format => format.borders.left);
        }
    }
    const edgeLine = (edge: BorderEdge): BorderLine | undefined => {
        const lines = distinct(sides[edge]);
        if (lines.length === 0) return BorderLine.none;
        return lines.length === 1 ? lines[0] : undefined;
    };
    return {
        fontStyleDiffers: new Set(shown.map(format => `${format.font.bold}|${format.font.italic}`)).size > 1,
        fillDiffers: distinct(shown.map(format => format.fill)).length > 1,
        edges: {
            top: edgeLine('top'), insideHorizontal: edgeLine('insideHorizontal'), bottom: edgeLine('bottom'),
            left: edgeLine('left'), insideVertical: edgeLine('insideVertical'), right: edgeLine('right'),
        },
        insideHorizontal: ranges.some(range => range.rowCount > 1),
        insideVertical: ranges.some(range => range.columnCount > 1),
    };
}

/**
 * The line the Border tab opens on (ADR-0071): the line an outer edge of the Selection shows, the first of the
 * top, the bottom, the left and the right that shows one, in its style and colour; with none, Thin and Automatic.
 * Excel was seen to open on the one line shown over one cell, on a top, a bottom, a left and a right edge alike,
 * its own or a neighbour's as drawn (the seventeenth Windows run, h10; the fourteenth run, case 13; run 18a,
 * format-cells 1, 6, 7 and 8); on it over a range and over several ranges whose top all show it (run 18a, 4 and
 * 5); and, with two lines shown, on the top's (run 18a, 2 and 3; the readings of 2026-10-12 (R8)). That the
 * bottom comes before the left and the right, and the left before the right, no run has read: it is the engine's
 * order, by generalisation, as an edge whose line differs along it is passed over.
 */
function openingPen(edges: Readonly<EdgeLines>): BorderLine {
    for (const edge of ['top', 'bottom', 'left', 'right'] as const) {
        const line = edges[edge];
        if (line !== undefined && !line.isNone) return line;
    }
    return new BorderLine('thin');
}

function styleOf(font: CellFont): FontStyle {
    if (font.bold) return font.italic ? 'boldItalic' : 'bold';
    return font.italic ? 'italic' : 'regular';
}

function emphasisOf(style: FontStyle): { readonly bold: boolean; readonly italic: boolean } {
    switch (style) {
        case 'boldItalic': return { bold: true, italic: true };
        case 'bold': return { bold: true, italic: false };
        case 'italic': return { bold: false, italic: true };
        case 'regular': return { bold: false, italic: false };
        default: return unreachable(style);
    }
}

/**
 * What Format Cells opens on, and what the user has touched since (ADR-0071, SH-45).
 *
 * It opens on the Focus cell's Cell Format. A part that differs across the Selection is shown as Excel shows it
 * (the eleventh Windows run, case 24): the Font style empty (`fontStyle` is `undefined`), a Fill as No Colour,
 * and an edge as a grey dotted line (`edgeLine` is `undefined`). Every other part shows the Focus cell's. A
 * range's outer edges show as they are drawn, a neighbour's line where the cell records none (the fourteenth run,
 * case 13); an edge inside a range shows the cells' own sides.
 *
 * `change` names only the parts the user touched, so OK leaves every other part as each cell has it; with
 * nothing touched it is empty, and OK sets nothing. An edge shown from a neighbour and left alone writes nothing;
 * taken away, it is cleared on both sides, as clearing an edge is (SH-45; the seventeenth Windows run, h10). A
 * choice that cannot be set — a Custom code ExSheet does not read, More Colours text that is not a colour — is
 * held as `refusal`, by name, and OK sets nothing until it is put right.
 */
export class FormatCellsDraft {
    readonly #culture: Culture;
    readonly #openingNumber: RecognisedNumberFormat;
    readonly #edges: EdgeLines;
    readonly #edgesTouched = new Set<BorderEdge>();
    readonly #colourText = new Map<ColourTarget, string>();
    readonly #colourRefusals = new Map<ColourTarget, FormatCellsRefusal>();
    #customTypes: readonly string[] | undefined;

    #numberTouched = false;
    #alignmentTouched = false;
    #fontStyleTouched = false;
    #underlineTouched = false;
    #strikethroughTouched = false;
    #fontColourTouched = false;
    #fillTouched = false;

    #category: NumberFormatCategory = 'general';
    #decimalPlaces = 2;
    #thousandsSeparator = false;
    #negativeNumber = 0;
    #typeCode = '';
    #alignment: HorizontalAlignment;
    #fontStyle: FontStyle | undefined;
    #underline: boolean;
    #strikethrough: boolean;
    #fontColour: CellColour;
    #fill: CellFill;
    #lineStyle: BorderLineStyle;
    #lineColour: CellColour;

    /** The Focus cell's Cell Format, which Format Cells opens on. */
    readonly opening: CellFormat;
    /** Whether a selected range spans more than one row, so that it has an edge inside it between rows. */
    readonly hasInsideHorizontal: boolean;
    /** Whether a selected range spans more than one column, so that it has an edge inside it between columns. */
    readonly hasInsideVertical: boolean;

    private constructor(opening: CellFormat, spread: FormatCellsSpread, culture: Culture) {
        this.#culture = culture;
        this.opening = opening;
        this.hasInsideHorizontal = spread.insideHorizontal;
        this.hasInsideVertical = spread.insideVertical;
        this.#openingNumber = recogniseNumberFormat(opening.numberFormat, culture);
        this.#restoreNumber(this.#openingNumber);
        this.#alignment = opening.alignment;
        this.#fontStyle = spread.fontStyleDiffers ? undefined : styleOf(opening.font);
        this.#underline = opening.font.underline;
        this.#strikethrough = opening.font.strikethrough;
        this.#fontColour = opening.font.colour;
        this.#fill = spread.fillDiffers ? CellFill.none : opening.fill;
        this.#edges = { ...spread.edges };
        const pen = openingPen(this.#edges);
        this.#lineStyle = pen.style;
        this.#lineColour = pen.colour;
    }

    /**
     * Format Cells' draft over the ranges, opening on the Focus cell (ADR-0071): what differs across the ranges is
     * read from what the Sheet records (`Sheet.getCellFormats`), so a Selection of whole columns opens as quickly
     * as one cell.
     */
    static open(sheet: Sheet, focus: CellAddress, ranges: readonly CellRange[]): FormatCellsDraft {
        return new FormatCellsDraft(sheet.getCellFormat(focus), spreadOf(sheet, ranges), sheet.culture);
    }

    // ---- Number ----

    /** The Number Format category chosen. */
    get category(): NumberFormatCategory {
        return this.#category;
    }

    /** The decimal places, under Number, Currency, Percentage and Scientific. */
    get decimalPlaces(): number {
        return this.#decimalPlaces;
    }

    /** Whether Number groups thousands. */
    get thousandsSeparator(): boolean {
        return this.#thousandsSeparator;
    }

    /** Which of `negativeNumbers` is chosen. */
    get negativeNumber(): number {
        return this.#negativeNumber;
    }

    /** The Date or Time format chosen, or the Custom code as typed; empty under any other category. */
    get typeCode(): string {
        return this.#typeCode;
    }

    /** The ways a negative number shows under the chosen category, each as its code. */
    get negativeNumbers(): readonly string[] {
        switch (this.#category) {
            case 'number':
                return Object.freeze(Array.from({ length: numberNegativeStyles },
                    (_, n) => numberCode(this.#decimalPlaces, this.#thousandsSeparator, n)));
            case 'currency':
                return Object.freeze(Array.from({ length: currencyNegativeStyles }, (_, n) => currencyCode(this.#decimalPlaces, n, this.#culture)));
            case 'general': case 'accounting': case 'date': case 'time': case 'percentage': case 'fraction': case 'scientific':
            case 'text': case 'special': case 'custom':
                return Object.freeze([]);
            default:
                return unreachable(this.#category);
        }
    }

    /** The formats the chosen category lists: Date's, Time's, or the codes Custom starts from. */
    get types(): readonly string[] {
        switch (this.#category) {
            case 'date': return dateTypes;
            case 'time': return timeTypes;
            case 'custom': {
                const listed = this.#listedCustomTypes();
                const opening = this.#openingNumber;
                return opening.category === 'custom' && !listed.includes(opening.type) ? Object.freeze([opening.type, ...listed]) : listed;
            }
            case 'general': case 'number': case 'currency': case 'accounting': case 'percentage': case 'fraction':
            case 'scientific': case 'text': case 'special':
                return Object.freeze([]);
            default: return unreachable(this.#category);
        }
    }

    /** The codes Custom lists, as the Sheet's culture spells them. */
    #listedCustomTypes(): readonly string[] {
        if (this.#customTypes === undefined) this.#customTypes = customTypesIn(this.#culture);
        return this.#customTypes;
    }

    /** The Sheet culture's currency symbol, which Currency writes. */
    get currencySymbol(): string {
        return currencySymbolOf(this.#culture);
    }

    /** Whether the chosen category takes decimal places. */
    get takesDecimalPlaces(): boolean {
        return this.#category === 'number' || this.#category === 'currency' || this.#category === 'percentage' || this.#category === 'scientific';
    }

    /**
     * The code the Number tab shows now. Under Custom it is the code as typed, which is read as the Sheet's culture
     * spells codes (`NumberFormat.tryParseLocal`): a built-in only where it spells that built-in's code under the
     * culture (the fourteenth Windows run, case 11).
     */
    get numberFormatCode(): string {
        switch (this.#category) {
            case 'general': return NumberFormat.general.code;
            case 'number': return numberCode(this.#decimalPlaces, this.#thousandsSeparator, this.#negativeNumber);
            case 'currency': return currencyCode(this.#decimalPlaces, this.#negativeNumber, this.#culture);
            case 'percentage': return percentageCode(this.#decimalPlaces);
            case 'scientific': return scientificCode(this.#decimalPlaces);
            case 'text': return '@';
            case 'accounting':
            case 'date':
            case 'time':
            case 'fraction':
            case 'special':
            case 'custom':
                return this.#typeCode;
            default: return unreachable(this.#category);
        }
    }

    /** Why the Number tab's code cannot be set, or `undefined` when it can. */
    get numberFormatRefusal(): FormatCellsRefusal | undefined {
        const read = this.#readNumberFormat();
        return 'refusal' in read ? read.refusal : undefined;
    }

    /**
     * Chooses a category, with its options as they opened when it is the one the Focus cell's code is, and the
     * category's defaults otherwise. A category shown disabled is refused, with its reason.
     */
    selectCategory(category: NumberFormatCategory): void {
        if (!formatCellsOffer.categories.some(offer => offer.category === category)) {
            throw new TypeError(`'${category}' is not a Number Format category.`);
        }
        if (isDisabledCategory(category)) throw new TypeError(SheetWords.english.categoryDisabled(category));
        this.#numberTouched = true;
        if (category === this.#openingNumber.category) {
            this.#restoreNumber(this.#openingNumber);
            return;
        }
        // Custom starts from the code shown, as Excel's does, spelled as the Custom box reads it; a code already in
        // the box is spelled so.
        const code = this.numberFormatCode;
        const shownFormat = this.#category === 'custom' ? undefined : NumberFormat.tryParse(code).format;
        const shown = shownFormat === undefined ? code : shownFormat.localCode(this.#culture);
        this.#category = category;
        this.#decimalPlaces = defaultPlaces(category, this.#culture);
        this.#thousandsSeparator = false;
        this.#negativeNumber = 0;
        this.#typeCode = category === 'date' ? firstOf(dateTypes)
            : category === 'time' ? firstOf(timeTypes)
                : category === 'custom' ? shown : '';
    }

    /** Sets the decimal places, 0 to 30. */
    setDecimalPlaces(places: number): void {
        if (!Number.isInteger(places) || places < 0 || places > mostDecimalPlaces) {
            throw new RangeError(`Decimal places are 0 to ${mostDecimalPlaces}, not ${places}.`);
        }
        this.#numberTouched = true;
        this.#decimalPlaces = places;
    }

    /** Sets whether Number groups thousands. */
    setThousandsSeparator(separator: boolean): void {
        this.#numberTouched = true;
        this.#thousandsSeparator = separator;
    }

    /** Chooses one of `negativeNumbers`. */
    setNegativeNumber(index: number): void {
        if (!Number.isInteger(index) || index < 0 || index >= this.negativeNumbers.length) {
            throw new RangeError(`${index} is not one of the ${this.negativeNumbers.length} ways a negative number shows here.`);
        }
        this.#numberTouched = true;
        this.#negativeNumber = index;
    }

    /** Chooses a Date or Time format, or types the Custom code. */
    setType(code: string): void {
        if (typeof code !== 'string') throw new TypeError('A format code is text.');
        this.#numberTouched = true;
        this.#typeCode = code;
    }

    // ---- Alignment ----

    /** The horizontal alignment. */
    get alignment(): HorizontalAlignment {
        return this.#alignment;
    }

    /** Sets the horizontal alignment. */
    setAlignment(alignment: HorizontalAlignment): void {
        if (!formatCellsOffer.alignments.includes(alignment)) throw new TypeError(`'${alignment}' is not an alignment.`);
        this.#alignmentTouched = true;
        this.#alignment = alignment;
    }

    // ---- Font ----

    /** The Font style; `undefined`, shown empty, while bold or italic differs across the Selection. */
    get fontStyle(): FontStyle | undefined {
        return this.#fontStyle;
    }

    /** Whether the text is underlined. */
    get underline(): boolean {
        return this.#underline;
    }

    /** Whether the text is struck through. */
    get strikethrough(): boolean {
        return this.#strikethrough;
    }

    /** The Font's colour; Automatic is the Ink. */
    get fontColour(): CellColour {
        return this.#fontColour;
    }

    /** Sets the Font style: bold and italic together. */
    setFontStyle(style: FontStyle): void {
        if (!formatCellsOffer.fontStyles.includes(style)) throw new TypeError(`'${style}' is not a font style.`);
        this.#fontStyleTouched = true;
        this.#fontStyle = style;
    }

    /** Sets the single underline on or off. */
    setUnderline(underline: boolean): void {
        this.#underlineTouched = true;
        this.#underline = underline;
    }

    /** Sets strikethrough on or off. */
    setStrikethrough(strikethrough: boolean): void {
        this.#strikethroughTouched = true;
        this.#strikethrough = strikethrough;
    }

    /** Sets the Font's colour: Automatic, or a colour of the palette or More Colours. */
    setFontColour(colour: CellColour): void {
        this.#fontColourTouched = true;
        this.#fontColour = colour;
        this.#forgetColourText('font');
    }

    /**
     * Whether the Font is Excel's Normal font as ExSheet has it — the colour Automatic, and neither bold, italic,
     * underline nor strikethrough — which the Font tab's Normal font box shows checked (ADR-0071, 2026-10-02).
     * False while bold or italic differs across the Selection.
     */
    get isNormalFont(): boolean {
        return this.#fontStyle === 'regular' && !this.#underline && !this.#strikethrough && this.#fontColour.isAutomatic;
    }

    /**
     * The Font tab's Normal font box, checked (ADR-0071, 2026-10-02): every part of the Font set to its default and
     * touched, so OK records them on each selected cell over a row's or a column's Font, as Excel writes its Normal
     * style's font. ExSheet has no Normal style: a typeface and a size stay out.
     */
    setNormalFont(): void {
        this.setFontStyle('regular');
        this.setUnderline(false);
        this.setStrikethrough(false);
        this.setFontColour(automaticColour());
    }

    // ---- Fill ----

    /** The Fill; No Colour while the Fill differs across the Selection. */
    get fill(): CellFill {
        return this.#fill;
    }

    /** Sets the Fill: No Colour, or a colour of the palette or More Colours. */
    setFill(fill: CellFill): void {
        this.#fillTouched = true;
        this.#fill = fill;
        this.#forgetColourText('fill');
    }

    // ---- Border ----

    /**
     * The line style an edge or a preset sets. Format Cells opens on the line an outer edge shows, and on Thin
     * where none shows one (`openingPen`).
     */
    get lineStyle(): BorderLineStyle {
        return this.#lineStyle;
    }

    /** The line colour an edge or a preset sets: the colour of the line the style opens on, else Automatic. */
    get lineColour(): CellColour {
        return this.#lineColour;
    }

    /** Chooses the line style. */
    setLineStyle(style: BorderLineStyle): void {
        if (!formatCellsOffer.lineStyleColumns.some(column => column.includes(style))) {
            throw new TypeError(`'${style}' is not a line style.`);
        }
        this.#lineStyle = style;
    }

    /** Chooses the line colour. */
    setLineColour(colour: CellColour): void {
        this.#lineColour = colour;
        this.#forgetColourText('border');
    }

    /** The line on an edge; `undefined`, drawn as a grey dotted line, while it differs across the Selection. */
    edgeLine(edge: BorderEdge): BorderLine | undefined {
        return this.#edges[edge];
    }

    /** Whether an edge can be set: an edge inside the range only where a range has more than one row or column. */
    canSet(edge: BorderEdge): boolean {
        switch (edge) {
            case 'insideHorizontal': return this.hasInsideHorizontal;
            case 'insideVertical': return this.hasInsideVertical;
            case 'top':
            case 'bottom':
            case 'left':
            case 'right':
                return true;
            default: return unreachable(edge);
        }
    }

    /** Whether a preset is offered: Inside only where a range has an edge inside it. */
    canApply(preset: BorderPreset): boolean {
        return preset !== 'inside' || this.hasInsideHorizontal || this.hasInsideVertical;
    }

    /**
     * An edge's button, as Excel's: an edge that shows the chosen line loses it, and any other takes it. A line
     * style of None takes the line away. An edge that cannot be set for this Selection is refused.
     */
    toggleEdge(edge: BorderEdge): void {
        if (!this.canSet(edge)) {
            throw new Error(`The ${SheetWords.english.edgeName(edge)} edge lies inside a range, and no selected range has one.`);
        }
        const pen = this.#pen();
        const shown = this.#edges[edge];
        this.#setEdge(edge, shown !== undefined && shown.equals(pen) ? BorderLine.none : pen);
    }

    /** A preset: None takes every line of the range away; Outline and Inside set the chosen line there. Inside,
     * where no selected range has an edge inside it, is refused. */
    applyPreset(preset: BorderPreset): void {
        if (!this.canApply(preset)) throw new Error('Inside sets the edges inside a range, and no selected range has one.');
        const line = preset === 'none' ? BorderLine.none : this.#pen();
        for (const edge of formatCellsOffer.edges) {
            const outer = edge !== 'insideHorizontal' && edge !== 'insideVertical';
            const sets = preset === 'outline' ? outer : preset === 'inside' ? !outer : true;
            if (sets && this.canSet(edge)) this.#setEdge(edge, line);
        }
    }

    #pen(): BorderLine {
        return new BorderLine(this.#lineStyle, this.#lineColour);
    }

    #setEdge(edge: BorderEdge, line: BorderLine): void {
        this.#edges[edge] = line;
        this.#edgesTouched.add(edge);
    }

    // ---- More Colours ----

    /** The text typed under More Colours for a colour, or empty. */
    colourText(target: ColourTarget): string {
        const text = this.#colourText.get(target);
        return text === undefined ? '' : text;
    }

    /**
     * Reads text typed under More Colours: `#` and six hex digits, such as `#1F4E79`, sets the colour for the
     * target. Any other text stands as a `refusal` until it is corrected, cleared, or a colour is chosen from the
     * palette.
     */
    setColourText(target: ColourTarget, text: string): void {
        if (typeof text !== 'string') throw new TypeError('More Colours text is text.');
        const colour = FormatCellsDraft.tryReadHex(text);
        if (colour !== undefined) {
            switch (target) {
                case 'font': this.setFontColour(colour); break;
                case 'fill': this.setFill(CellFill.solid(colour)); break;
                case 'border': this.setLineColour(colour); break;
                default: unreachable(target);
            }
            this.#colourText.set(target, text);
            return;
        }
        this.#colourText.set(target, text);
        if (text.trim().length === 0) this.#colourRefusals.delete(target);
        else this.#colourRefusals.set(target, Object.freeze({ kind: 'notAColour', target, text }));
    }

    /** A colour written `#RRGGBB`, or `RRGGBB`, in either case; `undefined` for any other text. */
    static tryReadHex(text: string): CellColour | undefined {
        const digits = text.trim().replace(/^#+/u, '');
        return /^[0-9A-Fa-f]{6}$/u.test(digits) ? colourFromRgb(Number.parseInt(digits, 16)) : undefined;
    }

    #forgetColourText(target: ColourTarget): void {
        this.#colourText.delete(target);
        this.#colourRefusals.delete(target);
    }

    // ---- OK ----

    /**
     * Why OK cannot set what Format Cells shows, by name, or `undefined` when it can: a Custom code ExSheet does
     * not read, or More Colours text that is not a colour. `SheetWords.formatCellsRefusal` words it.
     */
    get refusal(): FormatCellsRefusal | undefined {
        const number = this.#numberTouched ? this.numberFormatRefusal : undefined;
        if (number !== undefined) return number;
        for (const refusal of this.#colourRefusals.values()) return refusal;
        return undefined;
    }

    /** Whether the user has touched anything that OK would set. */
    get isTouched(): boolean {
        return !this.change.isEmpty;
    }

    /** The parts the user touched, as one change; empty when nothing was. */
    get change(): CellFormatChange {
        const style = this.#fontStyleTouched ? this.#fontStyle : undefined;
        const emphasis = style === undefined ? undefined : emphasisOf(style);
        const touched = (edge: BorderEdge): BorderLine | undefined => (this.#edgesTouched.has(edge) ? this.#edges[edge] : undefined);
        const read = this.#numberTouched ? this.#readNumberFormat() : undefined;
        return new CellFormatChange({
            numberFormat: read !== undefined && 'format' in read ? read.format : undefined,
            alignment: this.#alignmentTouched ? this.#alignment : undefined,
            bold: emphasis?.bold,
            italic: emphasis?.italic,
            underline: this.#underlineTouched ? this.#underline : undefined,
            strikethrough: this.#strikethroughTouched ? this.#strikethrough : undefined,
            fontColour: this.#fontColourTouched ? this.#fontColour : undefined,
            fill: this.#fillTouched ? this.#fill : undefined,
            borders: this.#edgesTouched.size === 0 ? undefined : new BorderChange({
                top: touched('top'),
                bottom: touched('bottom'),
                left: touched('left'),
                right: touched('right'),
                insideHorizontal: touched('insideHorizontal'),
                insideVertical: touched('insideVertical'),
            }),
        });
    }

    /**
     * The Number tab's code as the engine reads it — a Custom code as the Sheet's culture spells codes, any other
     * as written — or the refusal, by name.
     */
    #readNumberFormat(): { readonly format: NumberFormat } | { readonly refusal: FormatCellsRefusal } {
        const code = this.numberFormatCode;
        if (code.trim().length === 0) return { refusal: Object.freeze({ kind: 'noNumberFormatCode' }) };
        const reading: NumberFormatReading = this.#category === 'custom'
            ? NumberFormat.tryParseLocal(code, this.#culture)
            : NumberFormat.tryParse(code);
        if (reading.format !== undefined) return { format: reading.format };
        return { refusal: Object.freeze({ kind: 'numberFormatNotRead', code, reason: reading.reason }) };
    }

    #restoreNumber(number: RecognisedNumberFormat): void {
        this.#category = number.category;
        this.#decimalPlaces = number.places;
        this.#thousandsSeparator = number.separator;
        this.#negativeNumber = number.negative;
        this.#typeCode = number.category === 'date' || number.category === 'time' || number.category === 'custom' ? number.type : '';
    }
}

function firstOf(list: readonly string[]): string {
    const first = list[0];
    if (first === undefined) throw new Error('Internal: a category lists no format.');
    return first;
}
