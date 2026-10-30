// The styles part read as far as ADR-0330 §4 carries it, and nothing past it, as ExGrid.Data.Xlsx's StylesReader
// reads it. The part is small and is read whole.

import { parseHex32 } from './invariant';
import { Package } from './package';
import {
    borderStyleNames, defaultStyles, firstCustomNumberFormat, horizontalNames, noBorderEdge, noFill, verticalNames,
    type XlsxAlignment, type XlsxBorder, type XlsxBorderEdge, type XlsxBorderStyle, type XlsxCellFormat, type XlsxColor,
    type XlsxFill, type XlsxFont, type XlsxStyles, type XlsxUnderline,
} from './styles';
import { decodeXString } from './x-string';
import { XlsxError } from './xlsx-error';
import { XmlError, type XmlReader } from './xml-reader';
import { bool, double, int } from './values';

export async function readStyles(pkg: Package, part: string): Promise<XlsxStyles> {
    const opened = await pkg.readXml(part);
    if (opened === undefined) return defaultStyles;
    const numberFormats = new Map<number, string>();
    const fonts: XlsxFont[] = [];
    const fills: XlsxFill[] = [];
    const borders: XlsxBorder[] = [];
    const cellFormats: XlsxCellFormat[] = [];
    let indexed: number[] | null = null;
    const xml = opened.xml;
    try {
        xml.moveToContent();
        if (xml.isEmptyElement()) return defaultStyles;
        const depth = xml.depth();
        xml.read();
        while (!xml.eof() && !(xml.nodeType() === 'endElement' && xml.depth() === depth)) {
            if (xml.nodeType() !== 'element') {
                xml.read();
                continue;
            }
            switch (xml.localName()) {
                case 'numFmts':
                    children(xml, 'numFmt', () => {
                        const id = xml.getAttribute('numFmtId');
                        const code = xml.getAttribute('formatCode');
                        if (id !== undefined && code !== undefined) numberFormats.set(int(id, { part }), decodeXString(code));
                    });
                    break;
                case 'fonts':
                    children(xml, 'font', () => fonts.push(readFont(xml, part)));
                    break;
                case 'fills':
                    children(xml, 'fill', () => fills.push(readFill(xml, part)));
                    break;
                case 'borders':
                    children(xml, 'border', () => borders.push(readBorder(xml, part)));
                    break;
                case 'cellXfs':
                    children(xml, 'xf', () => cellFormats.push(readCellFormat(xml, part)));
                    break;
                case 'colors':
                    indexed = readIndexedColors(xml, part);
                    break;
                default:
                    xml.skip();
                    break;
            }
        }
    } catch (error) {
        if (error instanceof XmlError) throw Package.refuse(error, opened, part);
        throw error;
    }
    if (fonts.length === 0) fonts.push(...defaultStyles.fonts);
    if (fills.length === 0) fills.push(...defaultStyles.fills);
    if (borders.length === 0) borders.push(...defaultStyles.borders);
    if (cellFormats.length === 0) cellFormats.push(...defaultStyles.cellFormats);
    cellFormats.forEach((f, i) => {
        const check = (what: string, id: number, count: number): void => {
            if (id < 0 || id >= count) {
                throw new XlsxError('malformed', `The cell format ${String(i)} names the ${what} ${String(id)}, which the styles do not hold.`, { part });
            }
        };
        check('font', f.fontId, fonts.length);
        check('fill', f.fillId, fills.length);
        check('border', f.borderId, borders.length);
        // Below 164 a Number Format is built in, or one the part may redefine; from 164 it is the part's own.
        if (f.numberFormatId < 0 || (f.numberFormatId >= firstCustomNumberFormat && !numberFormats.has(f.numberFormatId))) {
            throw new XlsxError('malformed',
                `The cell format ${String(i)} names the Number Format ${String(f.numberFormatId)}, which the styles do not hold.`, { part });
        }
    });
    return Object.freeze({
        numberFormats,
        fonts: Object.freeze(fonts),
        fills: Object.freeze(fills),
        borders: Object.freeze(borders),
        cellFormats: Object.freeze(cellFormats),
        indexedColors: indexed === null ? null : Object.freeze(indexed),
    });
}

/**
 * Walks the children of the element the reader is on, calling `each` on every one named `name` with the reader on its
 * start; the caller reads its attributes or its subtree, and the walk goes past it. Leaves the reader after the
 * element's end.
 */
function children(xml: XmlReader, name: string, each: () => void): void {
    if (xml.isEmptyElement()) {
        xml.read();
        return;
    }
    const depth = xml.depth();
    xml.read();
    while (!xml.eof() && !(xml.nodeType() === 'endElement' && xml.depth() === depth)) {
        if (xml.nodeType() === 'element' && xml.depth() === depth + 1 && xml.localName() === name) {
            each();
            // The caller read the attributes or the subtree; the element and its content go now.
            xml.skip();
            continue;
        }
        if (xml.nodeType() === 'element') xml.skip();
        else xml.read();
    }
    xml.read();
}

/**
 * Walks the subtree of the element the reader is on, as `XmlReader.ReadSubtree` does: `visit` is called on each node
 * inside it with its depth below the element. Leaves the reader on the element's end, or on the element when it is
 * empty.
 */
function subtree(xml: XmlReader, visit: (depth: number) => void): void {
    if (xml.isEmptyElement()) return;
    const top = xml.depth();
    while (xml.read() && !(xml.nodeType() === 'endElement' && xml.depth() === top)) visit(xml.depth() - top);
}

function readFont(xml: XmlReader, part: string): XlsxFont {
    let name: string | null = null;
    let size: number | null = null;
    let bold = false;
    let italic = false;
    let strike = false;
    let underline: XlsxUnderline = 'none';
    let color: XlsxColor | null = null;
    subtree(xml, depth => {
        if (xml.nodeType() !== 'element' || depth !== 1) return;
        const val = xml.getAttribute('val');
        switch (xml.localName()) {
            case 'name':
                name = val === undefined ? null : val;
                break;
            case 'sz':
                size = val === undefined ? null : double(val, part);
                break;
            case 'b':
                bold = val === undefined || bool(val);
                break;
            case 'i':
                italic = val === undefined || bool(val);
                break;
            case 'strike':
                strike = val === undefined || bool(val);
                break;
            case 'u':
                underline = underlineOf(val, part);
                break;
            case 'color':
                color = readColor(xml, part);
                break;
            default:
                break;
        }
    });
    return Object.freeze({ name, size, bold, italic, underline, strike, color });
}

function underlineOf(val: string | undefined, part: string): XlsxUnderline {
    switch (val) {
        case undefined:
        case 'single': return 'single';
        case 'double': return 'double';
        case 'singleAccounting': return 'singleAccounting';
        case 'doubleAccounting': return 'doubleAccounting';
        case 'none': return 'none';
        default: throw new XlsxError('malformed', `'${val}' is not an underline.`, { part });
    }
}

function readFill(xml: XmlReader, part: string): XlsxFill {
    if (xml.isEmptyElement()) return noFill;
    const top = xml.depth();
    const fill = firstFill(xml, top, part);
    // The rest of the fill is not read, and the reader is left on its end, as the subtree's reader leaves it.
    while (!(xml.nodeType() === 'endElement' && xml.depth() === top) && xml.read());
    return fill;
}

function firstFill(xml: XmlReader, top: number, part: string): XlsxFill {
    while (xml.read() && !(xml.nodeType() === 'endElement' && xml.depth() === top)) {
        if (xml.nodeType() !== 'element' || xml.depth() !== top + 1) continue;
        if (xml.localName() === 'gradientFill') return Object.freeze({ pattern: null, foreground: null, background: null, isGradient: true });
        if (xml.localName() !== 'patternFill') continue;
        const patternType = xml.getAttribute('patternType');
        const pattern = patternType === undefined ? 'none' : patternType;
        let foreground: XlsxColor | null = null;
        let background: XlsxColor | null = null;
        if (!xml.isEmptyElement()) {
            const at = xml.depth();
            while (xml.read() && xml.depth() > at) {
                if (xml.nodeType() !== 'element') continue;
                if (xml.localName() === 'fgColor') foreground = readColor(xml, part);
                else if (xml.localName() === 'bgColor') background = readColor(xml, part);
            }
        }
        return Object.freeze({ pattern, foreground, background, isGradient: false });
    }
    return noFill;
}

function readBorder(xml: XmlReader, part: string): XlsxBorder {
    const diagonalUp = bool(xml.getAttribute('diagonalUp'));
    const diagonalDown = bool(xml.getAttribute('diagonalDown'));
    let left = noBorderEdge;
    let right = noBorderEdge;
    let top = noBorderEdge;
    let bottom = noBorderEdge;
    let diagonal = noBorderEdge;
    subtree(xml, depth => {
        if (xml.nodeType() !== 'element' || depth !== 1) return;
        const name = xml.localName();
        const style = borderStyle(xml.getAttribute('style'), part);
        let color: XlsxColor | null = null;
        if (!xml.isEmptyElement()) {
            const at = xml.depth();
            while (xml.read() && xml.depth() > at) {
                if (xml.nodeType() === 'element' && xml.localName() === 'color') color = readColor(xml, part);
            }
        }
        const edge: XlsxBorderEdge = style === 'none' && color === null ? noBorderEdge : Object.freeze({ style, color });
        switch (name) {
            case 'left':
            case 'start':
                left = edge;
                break;
            case 'right':
            case 'end':
                right = edge;
                break;
            case 'top':
                top = edge;
                break;
            case 'bottom':
                bottom = edge;
                break;
            case 'diagonal':
                diagonal = edge;
                break;
            default:
                break;
        }
    });
    return Object.freeze({ left, right, top, bottom, diagonal, diagonalUp, diagonalDown });
}

function borderStyle(name: string | undefined, part: string): XlsxBorderStyle {
    if (name === undefined) return 'none';
    const style = borderStyleNames.find(s => s === name);
    if (style === undefined) throw new XlsxError('malformed', `'${name}' is not a border style.`, { part });
    return style;
}

function readCellFormat(xml: XmlReader, part: string): XlsxCellFormat {
    const id = (attribute: string): number => {
        const v = xml.getAttribute(attribute);
        return v === undefined ? 0 : int(v, { part });
    };
    const numberFormatId = id('numFmtId');
    const fontId = id('fontId');
    const fillId = id('fillId');
    const borderId = id('borderId');
    let alignment: XlsxAlignment | null = null;
    subtree(xml, depth => {
        if (xml.nodeType() === 'element' && depth === 1 && xml.localName() === 'alignment') alignment = readAlignment(xml, part);
    });
    return Object.freeze({ numberFormatId, fontId, fillId, borderId, alignment });
}

function readAlignment(xml: XmlReader, part: string): XlsxAlignment {
    const hv = xml.getAttribute('horizontal');
    const vv = xml.getAttribute('vertical');
    const horizontal = hv === undefined ? 'general' : horizontalNames.find(n => n === hv);
    const vertical = vv === undefined ? 'bottom' : verticalNames.find(n => n === vv);
    if (horizontal === undefined || vertical === undefined) throw new XlsxError('malformed', 'A cell format names an alignment that is not one.', { part });
    const indent = xml.getAttribute('indent');
    const rotation = xml.getAttribute('textRotation');
    return Object.freeze({
        horizontal,
        vertical,
        wrapText: bool(xml.getAttribute('wrapText')),
        indent: indent === undefined ? 0 : int(indent, { part }),
        textRotation: rotation === undefined ? 0 : int(rotation, { part }),
        shrinkToFit: bool(xml.getAttribute('shrinkToFit')),
    });
}

function readIndexedColors(xml: XmlReader, part: string): number[] | null {
    let colors: number[] | null = null;
    subtree(xml, () => {
        if (xml.nodeType() !== 'element') return;
        if (xml.localName() === 'indexedColors') {
            colors = [];
            return;
        }
        const rgb = xml.getAttribute('rgb');
        if (xml.localName() === 'rgbColor' && colors !== null && rgb !== undefined) colors.push(argb(rgb, part));
    });
    return colors;
}

/** The colour the element the reader is on writes (`CT_Color`). */
export function readColor(xml: XmlReader, part: string): XlsxColor {
    const t = xml.getAttribute('tint');
    const tint = t === undefined ? 0 : double(t, part);
    if (bool(xml.getAttribute('auto'))) return Object.freeze({ kind: 'automatic', value: 0, tint });
    const rgb = xml.getAttribute('rgb');
    if (rgb !== undefined) return Object.freeze({ kind: 'rgb', value: argb(rgb, part), tint });
    const theme = xml.getAttribute('theme');
    if (theme !== undefined) return Object.freeze({ kind: 'theme', value: int(theme, { part }) >>> 0, tint });
    const indexed = xml.getAttribute('indexed');
    if (indexed !== undefined) return Object.freeze({ kind: 'indexed', value: int(indexed, { part }) >>> 0, tint });
    return Object.freeze({ kind: 'automatic', value: 0, tint });
}

function argb(text: string, part: string): number {
    const value = parseHex32(text);
    if (value === undefined || (text.length !== 6 && text.length !== 8)) throw new XlsxError('malformed', `'${text}' is not an RGB colour.`, { part });
    return text.length === 6 ? (0xFF000000 | value) >>> 0 : value;
}
