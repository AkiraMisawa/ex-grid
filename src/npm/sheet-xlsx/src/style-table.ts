import type { BorderLine, CellColour, CellFormat, Culture } from '@ex-grid/sheet-engine';
import {
    defaultAlignment, firstCustomNumberFormat, gray125Fill, noBorder, noBorderEdge, noFill, rgbColor, solidFill,
    type XlsxAlignment, type XlsxBorder, type XlsxBorderEdge, type XlsxCellFormat, type XlsxColor, type XlsxFill, type XlsxFont, type XlsxStyles,
} from '@ex-grid/data-xlsx';
import { defaultFormat } from './format-parts';
import { builtInId, writtenCode } from './number-formats';
import { fileLineStyle } from './style-reading';

// The styles part a written workbook's Cell Formats need (ADR-0330 §4, §8, ADR-0071), as ExSheet.Xlsx's StyleTable builds
// it, as the formats are met: each distinct Cell Format one cell format of the file, its index what a cell, a row or a column
// names. Index 0 is the default Cell Format. Every font is the workbook's own, Calibri 11, with the Font's emphasis; a colour
// is written as its RGB value, and Automatic as none, which Excel paints as its automatic colour. A Number Format is its
// built-in id where it is one, and a format of the file's own from 164 otherwise, the engine's built-in currency spelled in
// the Workbook culture's currency.

/** The workbook's font: the one a written workbook's widths are counted in. */
const workbookFont: XlsxFont = Object.freeze({ name: 'Calibri', size: 11, bold: false, italic: false, underline: 'none', strike: false, color: null });

export class StyleTable {
    readonly #formats = new Map<string, number>();
    readonly #cellFormats: XlsxCellFormat[] = [];
    readonly #codes = new Map<string, number>();
    readonly #fonts = new Map<string, readonly [XlsxFont, number]>();
    readonly #fills = new Map<string, readonly [XlsxFill, number]>();
    readonly #borders = new Map<string, readonly [XlsxBorder, number]>();
    readonly #culture: Culture;

    /** A table for a Workbook of `culture`, holding the default Cell Format. */
    constructor(culture: Culture) {
        this.#culture = culture;
        this.#font(workbookFont);
        this.#fill(noFill);
        this.#fill(gray125Fill);
        this.#border(noBorder);
        this.index(defaultFormat);
    }

    /** The index of the cell format that writes `format`, added when it is new. */
    index(format: CellFormat): number {
        const known = this.#formats.get(format.key);
        if (known !== undefined) return known;
        const id = builtInId(format.numberFormat);
        const numberFormat = id === undefined ? this.#code(writtenCode(format.numberFormat, this.#culture)) : id;
        const f = format.font;
        const font = this.#font({
            ...workbookFont, bold: f.bold, italic: f.italic, underline: f.underline ? 'single' : 'none', strike: f.strikethrough, color: color(f.colour),
        });
        const fill = format.fill.colour === undefined ? 0 : this.#fill(solidFill(solidColor(format.fill.colour)));
        const b = format.borders;
        const border = this.#border({
            left: edge(b.left), right: edge(b.right), top: edge(b.top), bottom: edge(b.bottom), diagonal: noBorderEdge, diagonalUp: false, diagonalDown: false,
        });
        let alignment: XlsxAlignment | null;
        switch (format.alignment) {
            case 'left':
            case 'center':
            case 'right':
                alignment = { ...defaultAlignment, horizontal: format.alignment };
                break;
            case 'general':
                alignment = null;
                break;
            default:
                return unreachableAlignment(format.alignment);
        }
        const index = this.#cellFormats.length;
        this.#cellFormats.push({ numberFormatId: numberFormat, fontId: font, fillId: fill, borderId: border, alignment });
        this.#formats.set(format.key, index);
        return index;
    }

    /** The styles part, holding every format met so far. */
    build(): XlsxStyles {
        return {
            numberFormats: new Map([...this.#codes].map(([code, id]) => [id, code])),
            fonts: [...this.#fonts.values()].map(([font]) => font),
            fills: [...this.#fills.values()].map(([fill]) => fill),
            borders: [...this.#borders.values()].map(([border]) => border),
            cellFormats: [...this.#cellFormats],
            indexedColors: null,
        };
    }

    #code(code: string): number {
        let id = this.#codes.get(code);
        if (id === undefined) {
            id = firstCustomNumberFormat + this.#codes.size;
            this.#codes.set(code, id);
        }
        return id;
    }

    #font(font: XlsxFont): number {
        return add(this.#fonts, `${String(font.name)}|${String(font.size)}|${String(font.bold)}|${String(font.italic)}|${font.underline}|${String(font.strike)}|${colorKey(font.color)}`, font);
    }

    #fill(fill: XlsxFill): number {
        return add(this.#fills, `${String(fill.pattern)}|${colorKey(fill.foreground)}|${colorKey(fill.background)}|${String(fill.isGradient)}`, fill);
    }

    #border(border: XlsxBorder): number {
        return add(this.#borders, `${edgeKey(border.left)}|${edgeKey(border.right)}|${edgeKey(border.top)}|${edgeKey(border.bottom)}|${edgeKey(border.diagonal)}|${String(border.diagonalUp)}|${String(border.diagonalDown)}`, border);
    }
}

function add<T>(table: Map<string, readonly [T, number]>, key: string, item: T): number {
    const known = table.get(key);
    if (known !== undefined) return known[1];
    const index = table.size;
    table.set(key, [item, index]);
    return index;
}

function colorKey(c: XlsxColor | null): string {
    return c === null ? '-' : `${c.kind}:${String(c.value)}:${String(c.tint)}`;
}

function edgeKey(side: XlsxBorderEdge): string {
    return `${side.style}:${colorKey(side.color)}`;
}

function color(colour: CellColour): XlsxColor | null {
    return colour.isAutomatic ? null : rgbColor((0xFF000000 | colour.rgb) >>> 0);
}

/** A solid Fill's colour, which is never Automatic. */
function solidColor(colour: CellColour): XlsxColor {
    const written = color(colour);
    if (written === null) throw new Error('Internal: a solid Fill has no colour.');
    return written;
}

function edge(line: BorderLine): XlsxBorderEdge {
    return line.isNone ? noBorderEdge : { style: fileLineStyle(line.style), color: color(line.colour) };
}

function unreachableAlignment(value: never): never {
    throw new Error(`Internal: ${String(value)} is not a horizontal alignment.`);
}
