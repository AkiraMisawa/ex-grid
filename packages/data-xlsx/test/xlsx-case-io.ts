// What the .xlsx cases of tests/cases/xlsx/ read and answer, shared by this package's runner and by @ex-grid/sheet-xlsx's,
// whose cases write their files and read them back as these do (tests/cases/README.md).
import { expect } from 'vitest';
import {
    XlsxError, automaticColor, defaultStyles, formatCell, indexedColor, openXlsx, parseCell, rgbColor, themeColor,
    type XlsxAlignment, type XlsxBorderEdge, type XlsxBorderStyle, type XlsxCell, type XlsxCellInput, type XlsxCellType, type XlsxColor,
    type XlsxColumn, type XlsxColumnInput, type XlsxElement, type XlsxFormulaInput, type XlsxFormulaKind, type XlsxHorizontalAlignment,
    type XlsxReadOptions, type XlsxStyles, type XlsxTable, type XlsxTheme, type XlsxUnderline, type XlsxVerticalAlignment,
    type XlsxWriteOptions,
} from '../src/index';
import { byteSource, dataOffset, readCentralDirectory } from '../src/zip-read';
import {
    type Json, type JsonObject, flag, hex, key, known, list, number, object, optional, orNull, parseHex, string, text,
} from './case-json';

export const aspects = ['workbook', 'sharedStrings', 'styles', 'theme', 'sheets', 'batches'];

/** The aspects of a read a case holds, `sheets` when it names none. */
export function holdsOf(listed: Json | undefined): readonly string[] {
    const holds = listed === undefined ? ['sheets'] : list(listed).map(string);
    const unknown = holds.find(aspect => !aspects.includes(aspect));
    if (unknown !== undefined) throw new Error(`'${unknown}' is not an aspect of a read.`);
    return holds;
}

/** What reading `file` gives, in the aspects `holds` names, or how it was refused. */
export async function read(file: Uint8Array | Blob, holds: readonly string[], options: XlsxReadOptions, saying: string | undefined): Promise<Json> {
    try {
        const reader = await openXlsx(file, options);
        const result: { [key: string]: Json } = {};
        if (holds.includes('workbook')) {
            result['workbook'] = {
                part: reader.workbookPart,
                sheets: reader.sheets.map(s => ({ name: s.name, sheetId: s.sheetId, visibility: s.visibility, part: s.part, kind: s.kind })),
                activeSheet: reader.activeSheet,
                date1904: reader.date1904,
                definedNames: reader.definedNames.map(n => ({ name: n.name, text: n.text, localSheet: n.localSheet, hidden: n.hidden })),
                hasTheme: reader.hasTheme,
                parts: [...reader.parts].toSorted(),
            };
        }
        if (holds.includes('sharedStrings')) {
            const strings = reader.sharedStrings;
            result['sharedStrings'] = strings.texts.map((t, i) => [t, strings.isRich(i)]);
        }
        if (holds.includes('styles')) result['styles'] = stylesAnswer(reader.styles, reader.theme);
        if (holds.includes('theme')) result['theme'] = reader.theme.schemeColors.map(hex);
        if (holds.includes('sheets') || holds.includes('batches')) {
            const sheets: Json[] = [];
            const everyBatch: Json[] = [];
            for (let i = 0; i < reader.sheets.length; i++) {
                if (reader.sheets[i]?.part === null) {
                    sheets.push(null);
                    everyBatch.push(null);
                    continue;
                }
                const sheet = await reader.openSheet(i);
                const cells: Json[] = [];
                const rows: Json[] = [];
                const batches: Json[] = [];
                while (await sheet.read()) {
                    batches.push([sheet.cells.length, sheet.rows.length]);
                    for (const cell of sheet.cells) cells.push(cellAnswer(cell));
                    for (const row of sheet.rows) rows.push([row.row, row.styleIndex]);
                }
                sheets.push({
                    columns: sheet.columns.map(columnAnswer),
                    rows,
                    cells,
                    otherElements: sheet.otherElements.map(elementAnswer),
                    customRowHeights: sheet.customRowHeights,
                    hiddenRows: sheet.hiddenRows,
                    frozenPanes: sheet.frozenPanes,
                });
                everyBatch.push(batches);
                await sheet.close();
            }
            if (holds.includes('sheets')) result['sheets'] = sheets;
            if (holds.includes('batches')) result['batches'] = everyBatch;
        }
        if (saying !== undefined) throw new Error(`The case expects a refusal saying "${saying}", and the file was read.`);
        return result;
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a refusal is the case's answer, and anything else is rethrown
        if (!(error instanceof XlsxError)) throw error;
        let message = error.message;
        if (saying !== undefined) {
            expect(message.startsWith(saying), `"${message}" starts with "${saying}"`).toBe(true);
            message = saying;
        }
        return { refused: { reason: error.reason, part: orNull(error.part), sheet: orNull(error.sheet), cell: orNull(error.cell), message } };
    }
}

export function cellAnswer(cell: XlsxCell): Json {
    const shown: { [key: string]: Json } = { at: formatCell(cell.row, cell.column), type: cell.type, value: cell.value };
    if (cell.text !== null) shown['text'] = cell.text;
    if (cell.formula !== null) {
        const formula: { [key: string]: Json } = { text: cell.formula.text };
        if (cell.formula.kind !== 'normal') formula['kind'] = cell.formula.kind;
        if (cell.formula.reference !== null) formula['ref'] = cell.formula.reference;
        if (cell.formula.sharedIndex !== null) formula['si'] = cell.formula.sharedIndex;
        if (cell.formula.isDynamicArray) formula['dynamic'] = true;
        shown['formula'] = formula;
    }
    if (cell.styleIndex !== 0) shown['style'] = cell.styleIndex;
    if (cell.isRichText) shown['rich'] = true;
    return shown;
}

function columnAnswer(column: XlsxColumn): Json {
    return {
        first: column.first, last: column.last, width: column.width, hidden: column.hidden, style: column.styleIndex,
        bestFit: column.bestFit, customWidth: column.customWidth,
    };
}

function elementAnswer(element: XlsxElement): Json {
    const shown: { [key: string]: Json } = { name: element.name };
    if (element.attributes.size > 0) shown['attributes'] = Object.fromEntries(element.attributes);
    if (element.children.length > 0) shown['children'] = element.children.map(elementAnswer);
    if (element.text !== null) shown['text'] = element.text;
    if (element.childrenCut) shown['cut'] = true;
    return shown;
}

export function stylesAnswer(styles: XlsxStyles, theme: XlsxTheme): Json {
    const color = (c: XlsxColor): Json => {
        const shown: { [key: string]: Json } = { kind: c.kind };
        if (c.kind === 'rgb') shown['value'] = hex(c.value);
        else if (c.kind !== 'automatic') shown['value'] = c.value;
        if (c.tint !== 0) shown['tint'] = c.tint;
        const resolved = theme.resolve(c, styles.indexedColors);
        shown['resolved'] = resolved === undefined ? null : hex(resolved);
        return shown;
    };
    const edge = (e: XlsxBorderEdge): Json => (e.color === null ? { style: e.style } : { style: e.style, color: color(e.color) });
    return {
        numberFormats: Object.fromEntries([...styles.numberFormats].toSorted(([a], [b]) => a - b).map(([id, code]) => [String(id), code])),
        fonts: styles.fonts.map(font => {
            const shown: { [key: string]: Json } = {};
            if (font.name !== null) shown['name'] = font.name;
            if (font.size !== null) shown['size'] = font.size;
            if (font.bold) shown['bold'] = true;
            if (font.italic) shown['italic'] = true;
            if (font.underline !== 'none') shown['underline'] = font.underline;
            if (font.strike) shown['strike'] = true;
            if (font.color !== null) shown['color'] = color(font.color);
            return shown;
        }),
        fills: styles.fills.map(fill => {
            const shown: { [key: string]: Json } = { pattern: fill.pattern };
            if (fill.foreground !== null) shown['foreground'] = color(fill.foreground);
            if (fill.background !== null) shown['background'] = color(fill.background);
            if (fill.isGradient) shown['gradient'] = true;
            return shown;
        }),
        borders: styles.borders.map(border => {
            const shown: { [key: string]: Json } = {
                left: edge(border.left), right: edge(border.right), top: edge(border.top), bottom: edge(border.bottom), diagonal: edge(border.diagonal),
            };
            if (border.diagonalUp) shown['diagonalUp'] = true;
            if (border.diagonalDown) shown['diagonalDown'] = true;
            return shown;
        }),
        cellFormats: styles.cellFormats.map(format => {
            const shown: { [key: string]: Json } = { numberFormat: format.numberFormatId, font: format.fontId, fill: format.fillId, border: format.borderId };
            if (format.alignment !== null) {
                const a = format.alignment;
                const alignment: { [key: string]: Json } = {};
                if (a.horizontal !== 'general') alignment['horizontal'] = a.horizontal;
                if (a.vertical !== 'bottom') alignment['vertical'] = a.vertical;
                if (a.wrapText) alignment['wrapText'] = true;
                if (a.indent !== 0) alignment['indent'] = a.indent;
                if (a.textRotation !== 0) alignment['textRotation'] = a.textRotation;
                if (a.shrinkToFit) alignment['shrinkToFit'] = true;
                shown['alignment'] = alignment;
            }
            return shown;
        }),
        indexedColors: styles.indexedColors === null ? null : styles.indexedColors.map(hex),
    };
}

const decoder = new TextDecoder();

/** Each part of the workbook, in the zip's order, as UTF-8 text with its line ends written as `\n`. */
export async function partsOf(file: Blob): Promise<Json> {
    return (await textsOf(file)).map(([name, content]) => [name, content.replace(/\r\n|[\r\n\f\u0085\u2028\u2029]/g, '\n')]);
}

/** Each part of the zip, in its order, as UTF-8 text as it stands, stored or deflated. */
export async function textsOf(file: Uint8Array | Blob): Promise<[string, string][]> {
    const source = byteSource(file);
    const parts: [string, string][] = [];
    for (const entry of await readCentralDirectory(source)) {
        const at = await dataOffset(source, entry);
        const body = source.stream(at, entry.compressedSize);
        const bytes = entry.method === 0 ? body : body.pipeThrough(new DecompressionStream('deflate-raw'));
        parts.push([entry.name, decoder.decode(await new Response(bytes).arrayBuffer())]);
    }
    return parts;
}

export function writeOptions(given: JsonObject): XlsxWriteOptions {
    known(given, 'styles', 'activeSheet', 'date1904', 'fullCalculationOnLoad', 'definedNames', 'maxUncompressedBytes');
    return {
        ...(given['styles'] === undefined ? {} : { styles: stylesInput(object(given['styles'])) }),
        ...(given['activeSheet'] === undefined ? {} : { activeSheet: number(given['activeSheet']) }),
        ...(given['date1904'] === undefined ? {} : { date1904: flag(given['date1904']) }),
        ...(given['fullCalculationOnLoad'] === undefined ? {} : { fullCalculationOnLoad: flag(given['fullCalculationOnLoad']) }),
        ...(given['definedNames'] === undefined ? {} : {
            definedNames: list(given['definedNames']).map(object).map(n => ({
                name: text(key(n, 'name')), text: text(key(n, 'text')),
                localSheet: optional<number | null>(n, 'localSheet', number, null), hidden: optional(n, 'hidden', flag, false),
            })),
        }),
        ...(given['maxUncompressedBytes'] === undefined ? {} : { maxUncompressedBytes: number(given['maxUncompressedBytes']) }),
    };
}

export function cellInput(value: Json): XlsxCellInput {
    const cell = object(value);
    known(cell, 'at', 'type', 'value', 'text', 'formula', 'style');
    const place = parseCell(string(key(cell, 'at')));
    if (place === undefined) throw new Error(`'${JSON.stringify(cell['at'])}' is not a cell.`);
    return {
        row: place.row,
        column: place.column,
        type: string(key(cell, 'type')) as XlsxCellType,
        value: optional<string | null>(cell, 'value', text, null),
        text: optional<string | null>(cell, 'text', text, null),
        formula: optional<XlsxFormulaInput | null>(cell, 'formula', formulaInput, null),
        styleIndex: optional(cell, 'style', number, 0),
    };
}

function formulaInput(value: Json): XlsxFormulaInput {
    const formula = object(value);
    return {
        text: text(key(formula, 'text')),
        kind: optional<XlsxFormulaKind>(formula, 'kind', v => string(v) as XlsxFormulaKind, 'normal'),
        reference: optional<string | null>(formula, 'ref', text, null),
        sharedIndex: optional<number | null>(formula, 'si', number, null),
        isDynamicArray: optional(formula, 'dynamic', flag, false),
    };
}

export function columnInput(value: Json): XlsxColumnInput {
    const column = object(value);
    return {
        first: number(key(column, 'first')),
        last: number(key(column, 'last')),
        width: optional<number | null>(column, 'width', w => (w === null ? null : number(w)), null),
        hidden: optional(column, 'hidden', flag, false),
        styleIndex: optional(column, 'style', number, 0),
        bestFit: optional(column, 'bestFit', flag, false),
        customWidth: optional(column, 'customWidth', flag, true),
    };
}

export function tableInput(value: Json): XlsxTable {
    const table = object(value);
    return {
        name: text(key(table, 'name')),
        headerRow: number(key(table, 'headerRow')),
        firstColumn: number(key(table, 'firstColumn')),
        columns: list(key(table, 'columns')).map(text),
        dataRows: number(key(table, 'dataRows')),
        autoFilter: optional(table, 'autoFilter', flag, true),
    };
}

/** Styles as a case writes them, a list it leaves out taken from `defaultStyles`. */
function stylesInput(styles: JsonObject): XlsxStyles {
    known(styles, 'numberFormats', 'fonts', 'fills', 'borders', 'cellFormats', 'indexedColors');
    return {
        numberFormats: optional(styles, 'numberFormats', v => new Map(Object.entries(object(v)).map(([id, code]) => [Number(id), text(code)])), defaultStyles.numberFormats),
        fonts: optional(styles, 'fonts', v => list(v).map(object).map(f => ({
            name: optional<string | null>(f, 'name', text, null),
            size: optional<number | null>(f, 'size', number, null),
            bold: optional(f, 'bold', flag, false),
            italic: optional(f, 'italic', flag, false),
            underline: optional<XlsxUnderline>(f, 'underline', u => string(u) as XlsxUnderline, 'none'),
            strike: optional(f, 'strike', flag, false),
            color: optional<XlsxColor | null>(f, 'color', colorInput, null),
        })), defaultStyles.fonts),
        fills: optional(styles, 'fills', v => list(v).map(object).map(f => ({
            pattern: key(f, 'pattern') === null ? null : string(key(f, 'pattern')),
            foreground: optional<XlsxColor | null>(f, 'foreground', colorInput, null),
            background: optional<XlsxColor | null>(f, 'background', colorInput, null),
            isGradient: optional(f, 'gradient', flag, false),
        })), defaultStyles.fills),
        borders: optional(styles, 'borders', v => list(v).map(object).map(b => ({
            left: edgeInput(key(b, 'left')), right: edgeInput(key(b, 'right')), top: edgeInput(key(b, 'top')),
            bottom: edgeInput(key(b, 'bottom')), diagonal: edgeInput(key(b, 'diagonal')),
            diagonalUp: optional(b, 'diagonalUp', flag, false), diagonalDown: optional(b, 'diagonalDown', flag, false),
        })), defaultStyles.borders),
        cellFormats: optional(styles, 'cellFormats', v => list(v).map(object).map(x => ({
            numberFormatId: optional(x, 'numberFormat', number, 0),
            fontId: optional(x, 'font', number, 0),
            fillId: optional(x, 'fill', number, 0),
            borderId: optional(x, 'border', number, 0),
            alignment: optional<XlsxAlignment | null>(x, 'alignment', a => alignmentInput(object(a)), null),
        })), defaultStyles.cellFormats),
        indexedColors: optional<readonly number[] | null>(styles, 'indexedColors', v => (v === null ? null : list(v).map(parseHex)), defaultStyles.indexedColors),
    };
}

function alignmentInput(a: JsonObject): XlsxAlignment {
    return {
        horizontal: optional<XlsxHorizontalAlignment>(a, 'horizontal', h => string(h) as XlsxHorizontalAlignment, 'general'),
        vertical: optional<XlsxVerticalAlignment>(a, 'vertical', v => string(v) as XlsxVerticalAlignment, 'bottom'),
        wrapText: optional(a, 'wrapText', flag, false),
        indent: optional(a, 'indent', number, 0),
        textRotation: optional(a, 'textRotation', number, 0),
        shrinkToFit: optional(a, 'shrinkToFit', flag, false),
    };
}

function edgeInput(value: Json): XlsxBorderEdge {
    const edge = object(value);
    return { style: string(key(edge, 'style')) as XlsxBorderStyle, color: optional<XlsxColor | null>(edge, 'color', colorInput, null) };
}

export function colorInput(value: Json): XlsxColor {
    const color = object(value);
    const tint = optional(color, 'tint', number, 0);
    const kind = string(key(color, 'kind'));
    switch (kind) {
        case 'automatic': return automaticColor;
        case 'rgb': return rgbColor(parseHex(key(color, 'value')), tint);
        case 'theme': return themeColor(number(key(color, 'value')), tint);
        case 'indexed': return indexedColor(number(key(color, 'value')), tint);
        default: throw new Error(`'${kind}' is not a colour's kind.`);
    }
}
