import {
    BorderLine, CellBorders, CellColour, CellFill, CellFont, CellFormat, NumberFormat,
    type BorderLineStyle, type HorizontalAlignment,
} from '@ex-grid/sheet-engine';
import {
    defaultAlignment,
    type XlsxBorderEdge, type XlsxBorderStyle, type XlsxCellFormat, type XlsxColor, type XlsxFont, type XlsxStyles, type XlsxTheme,
} from '@ex-grid/data-xlsx';
import { elementAt } from './must';
import { readNumberFormat } from './number-formats';
import type { NotCarriedKind } from './open-report';
import { equalsIgnoreCase } from './text-casing';

// Reads the styles part's cell formats into the engine's Cell Formats (ADR-0330 §4, §8, ADR-0071), each once, as
// ExSheet.Xlsx's StyleReading reads them. Carried: the Number Format, horizontal alignment (General, left, centre, right),
// the Font's colour, bold, italic, underline and strikethrough, a solid Fill, and the four sides' borders in Excel's thirteen
// line styles. A colour is the RGB the file's theme or palette gives it; the default font's own colour, where it is black,
// is Automatic, the Ink, as a plain Excel workbook shows its text. Not carried, and said: another typeface or size than the
// workbook's own, vertical alignment, wrapping, indent, rotation and shrinking, the other horizontal alignments, double and
// accounting underlines (a single one is carried), pattern and gradient fills, and diagonal borders.

/** A cell format of the file read into the engine's Cell Format: what crosses, what cannot, and what is not carried. */
export interface ReadFormat {
    /** The Cell Format, every part the file's, or its default where the part cannot cross. */
    readonly format: CellFormat;
    /** Why its Number Format cannot cross, or `undefined`; a level that records it refuses the file. */
    readonly refusal: string | undefined;
    /** What the refusal names as held: the code, or the id. */
    readonly held: string | undefined;
    /** What it holds that is not carried, each kind once. */
    readonly notCarried: readonly NotCarriedKind[];
    /** Whether a colour it carries names a theme slot: carried as the theme's RGB, the link not. */
    readonly namesTheme: boolean;
}

export class StyleReading {
    readonly #styles: XlsxStyles;
    readonly #theme: XlsxTheme;
    readonly #read = new Map<number, ReadFormat>();
    readonly #workbookFont: XlsxFont;

    constructor(styles: XlsxStyles, theme: XlsxTheme) {
        this.#styles = styles;
        this.#theme = theme;
        this.#workbookFont = elementAt(styles.fonts, 0);
    }

    /** The cell format at `index` of the styles' cell formats, read once. */
    at(index: number): ReadFormat {
        let format = this.#read.get(index);
        if (format === undefined) {
            format = this.#readFormat(elementAt(this.#styles.cellFormats, index));
            this.#read.set(index, format);
        }
        return format;
    }

    #readFormat(xf: XlsxCellFormat): ReadFormat {
        const styles = this.#styles;
        const notCarried: NotCarriedKind[] = [];
        let namesTheme = false;
        const note = (kind: NotCarriedKind): void => {
            if (!notCarried.includes(kind)) notCarried.push(kind);
        };
        const colour = (color: XlsxColor | null): CellColour => {
            if (color === null) return CellColour.automatic;
            const argb = this.#theme.resolve(color, styles.indexedColors);
            if (argb === undefined) return CellColour.automatic;
            namesTheme ||= color.kind === 'theme';
            return CellColour.fromRgb(argb & 0xFFFFFF);
        };

        let refusal: string | undefined;
        let held: string | undefined;
        let numberFormat = NumberFormat.general;
        const read = readNumberFormat(xf.numberFormatId, styles.numberFormats.get(xf.numberFormatId));
        if (read.format !== undefined) {
            numberFormat = read.format;
        } else {
            refusal = read.reason;
            held = read.held;
        }

        const a = xf.alignment === null ? defaultAlignment : xf.alignment;
        let alignment: HorizontalAlignment | undefined;
        switch (a.horizontal) {
            case 'general':
            case 'left':
            case 'center':
            case 'right':
                alignment = a.horizontal;
                break;
            case 'fill':
            case 'justify':
            case 'centerContinuous':
            case 'distributed':
                note('horizontalAlignment');
                break;
            default:
                return unreachableAlignment(a.horizontal);
        }
        if (a.vertical !== 'bottom') note('verticalAlignment');
        if (a.wrapText || a.indent !== 0 || a.textRotation !== 0 || a.shrinkToFit) note('textLayout');

        const f = elementAt(styles.fonts, xf.fontId);
        if (!sameFace(f, this.#workbookFont)) note('fontNamesAndSizes');
        if (f.underline !== 'none' && f.underline !== 'single') note('underline');
        const font = new CellFont({ colour: this.#fontColour(f.color, colour), bold: f.bold, italic: f.italic, underline: f.underline !== 'none', strikethrough: f.strike });

        let fill = CellFill.none;
        const x = elementAt(styles.fills, xf.fillId);
        if (x.isGradient || (x.pattern !== 'none' && x.pattern !== 'solid')) {
            note('patternFills');
        } else if (x.pattern === 'solid') {
            // A solid fill whose colour is automatic, or names no colour the palette holds, has no RGB to carry.
            const solid = colour(x.foreground);
            if (solid.isAutomatic) note('patternFills');
            else fill = CellFill.solid(solid);
        }

        const b = elementAt(styles.borders, xf.borderId);
        if ((b.diagonalUp || b.diagonalDown) && b.diagonal.style !== 'none') note('diagonalBorders');
        const line = (edge: XlsxBorderEdge): BorderLine => (edge.style === 'none' ? BorderLine.none : new BorderLine(lineStyle(edge.style), colour(edge.color)));
        const borders = new CellBorders({ top: line(b.top), bottom: line(b.bottom), left: line(b.left), right: line(b.right) });

        return {
            format: new CellFormat(numberFormat, alignment === undefined ? 'general' : alignment, font, fill, borders),
            refusal, held, notCarried, namesTheme,
        };
    }

    /**
     * A Font's colour: the workbook font's own colour, where it paints black, is Automatic, the Ink, as Excel's own default
     * text (Text 1 of the Office theme) is; any other is the RGB it resolves to.
     */
    #fontColour(color: XlsxColor | null, colour: (color: XlsxColor | null) => CellColour): CellColour {
        const own = this.#workbookFont.color;
        if (color !== null && own !== null && sameColor(color, own)) {
            const argb = this.#theme.resolve(color, this.#styles.indexedColors);
            if (argb === undefined || argb >>> 0 === 0xFF000000 || argb === 0) return CellColour.automatic;
        }
        return colour(color);
    }
}

function unreachableAlignment(value: never): never {
    throw new Error(`Internal: ${String(value)} is not a horizontal alignment.`);
}

/** Whether two colours are the same as the C# record compares them. */
function sameColor(a: XlsxColor, b: XlsxColor): boolean {
    return a.kind === b.kind && a.value === b.value && a.tint === b.tint;
}

/** Whether a font has the workbook font's typeface and size; one that names none takes the workbook's. */
function sameFace(font: XlsxFont, workbook: XlsxFont): boolean {
    return (font.name === null || (workbook.name !== null && equalsIgnoreCase(font.name, workbook.name)))
        && (font.size === null || font.size === workbook.size);
}

const fromFile: ReadonlyMap<XlsxBorderStyle, BorderLineStyle> = new Map<XlsxBorderStyle, BorderLineStyle>([
    ['thin', 'thin'], ['medium', 'medium'], ['dashed', 'dashed'], ['dotted', 'dotted'], ['thick', 'thick'], ['double', 'double'],
    ['hair', 'hair'], ['mediumDashed', 'mediumDashed'], ['dashDot', 'dashDot'], ['mediumDashDot', 'mediumDashDot'],
    ['dashDotDot', 'dashDotDot'], ['mediumDashDotDot', 'mediumDashDotDot'], ['slantDashDot', 'slantedDashDot'],
]);

/** The engine's line style for the file's: the same thirteen (ADR-0071). */
export function lineStyle(style: XlsxBorderStyle): BorderLineStyle {
    const engine = fromFile.get(style);
    return engine === undefined ? 'none' : engine;
}

/** The file's line style for the engine's. */
export function fileLineStyle(style: BorderLineStyle): XlsxBorderStyle {
    for (const [file, engine] of fromFile) {
        if (engine === style) return file;
    }
    return 'none';
}
