// Runs the shared .xlsx cases of tests/cases/xlsx/ (ADR-0330, ADR-0370 section 3), which xUnit runs against
// ExGrid.Data.Xlsx as well: a reader and a writer of .xlsx have no differential run, so what holds this package to
// ExGrid.Data.Xlsx is these cases, answered by ExGrid.Data.Xlsx. tests/cases/README.md says what a file holds.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import {
    XlsxError, XlsxTheme, applyTint, automaticColor, createXlsxWriter, defaultStyles, formatCell, indexedColor, openXlsx, parseCell,
    rgbColor, themeColor,
    type XlsxAlignment, type XlsxBorderEdge, type XlsxBorderStyle, type XlsxCell, type XlsxCellInput, type XlsxCellType, type XlsxColor,
    type XlsxColumn, type XlsxColumnInput, type XlsxElement, type XlsxFormulaInput, type XlsxFormulaKind, type XlsxHorizontalAlignment,
    type XlsxReadOptions, type XlsxSheetVisibility, type XlsxStyles, type XlsxTable, type XlsxUnderline,
    type XlsxVerticalAlignment, type XlsxWriteOptions, type XlsxWriter, type XlsxSheetWriter,
} from '../src/index';
import { decodeXString } from '../src/x-string';
import { byteSource, dataOffset, readCentralDirectory } from '../src/zip-read';
import {
    type Json, type JsonObject, flag, hex, isObject, orNull, key, known, list, number, object, optional, parseHex, string, text,
} from './case-json';
import { fileOf } from './files';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../tests/cases/xlsx');

const kinds = ['xlsxRead', 'xlsxWrite', 'xlsxReference', 'xlsxEscape', 'xlsxColor'];

const files = fs.readdirSync(root)
    .filter(name => name.endsWith('.json'))
    .toSorted()
    .map(name => ({ name, json: JSON.parse(fs.readFileSync(path.join(root, name), 'utf8')) as Json }));

for (const { name, json } of files) {
    const kind = string(key(json, 'kind'));
    if (!kinds.includes(kind)) throw new Error(`${name} is of no kind this runner reads.`);
    describe(name, () => {
        for (const entry of list(key(json, 'cases')).map(object)) {
            it(string(key(entry, 'name')), async () => {
                expect.hasAssertions();
                expect(await answer(kind, entry)).toStrictEqual(key(entry, 'expect'));
            });
        }
    });
}

async function answer(kind: string, entry: JsonObject): Promise<Json> {
    switch (kind) {
        case 'xlsxRead': return readAnswer(entry);
        case 'xlsxWrite': return writeAnswer(entry);
        case 'xlsxReference': return referenceAnswer(entry);
        case 'xlsxEscape': return decodeXString(text(key(entry, 'written')));
        case 'xlsxColor': return colorAnswer(entry);
        default: throw new Error(`'${kind}' is not a kind.`);
    }
}

// ---- xlsxRead -------------------------------------------------------------------------------------------

const aspects = ['workbook', 'sharedStrings', 'styles', 'theme', 'sheets', 'batches'];

async function readAnswer(entry: JsonObject): Promise<Json> {
    const given = optional(entry, 'options', object, {});
    known(given, 'maxUncompressedBytes', 'maxCells');
    const options: XlsxReadOptions = {
        ...(given['maxUncompressedBytes'] === undefined ? {} : { maxUncompressedBytes: number(given['maxUncompressedBytes']) }),
        ...(given['maxCells'] === undefined ? {} : { maxCells: number(given['maxCells']) }),
    };
    return read(await fileOf(key(entry, 'file')), holdsOf(entry['holds']), options, optional<string | undefined>(entry, 'saying', string, undefined));
}

/** The aspects of a read a case holds, `sheets` when it names none. */
function holdsOf(listed: Json | undefined): readonly string[] {
    const holds = listed === undefined ? ['sheets'] : list(listed).map(string);
    const unknown = holds.find(aspect => !aspects.includes(aspect));
    if (unknown !== undefined) throw new Error(`'${unknown}' is not an aspect of a read.`);
    return holds;
}

/** What reading `file` gives, in the aspects `holds` names, or how it was refused. */
async function read(file: Uint8Array | Blob, holds: readonly string[], options: XlsxReadOptions, saying: string | undefined): Promise<Json> {
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

function cellAnswer(cell: XlsxCell): Json {
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

function stylesAnswer(styles: XlsxStyles, theme: XlsxTheme): Json {
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

// ---- xlsxWrite ------------------------------------------------------------------------------------------

async function writeAnswer(entry: JsonObject): Promise<Json> {
    let writer: XlsxWriter;
    try {
        writer = createXlsxWriter(writeOptions(optional(entry, 'options', object, {})));
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- how the writer refused is the case's answer; anything else is rethrown
        return { create: thrown(error) };
    }
    const outcomes: Json[] = [];
    let file: Blob | undefined;
    const sheets: XlsxSheetWriter[] = [];
    for (const step of list(key(entry, 'steps')).map(object)) {
        try {
            const written = await take(writer, sheets, step);
            if (written !== undefined) file = written;
            outcomes.push(null);
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- how a step was refused is the case's answer; anything else is rethrown
            outcomes.push(thrown(error));
        }
    }
    if (file === undefined) await writer.abandon();
    const result: { [key: string]: Json } = { create: null, outcomes };
    if (optional(entry, 'parts', flag, false)) result['parts'] = file === undefined ? null : await partsOf(file);
    if (entry['read'] !== undefined) result['read'] = file === undefined ? null : await read(file, holdsOf(entry['read']), {}, undefined);
    return result;
}

/** Takes one step of a write case: the workbook when the step completed it. */
async function take(writer: XlsxWriter, sheets: XlsxSheetWriter[], step: JsonObject): Promise<Blob | undefined> {
    const verbs = Object.keys(step).filter(name => name !== 'columns' && name !== 'visibility');
    const [verb] = verbs;
    if (verb === undefined || verbs.length !== 1) throw new Error(`${JSON.stringify(step)} is not one step.`);
    const argument = key(step, verb);
    const last = (): XlsxSheetWriter => {
        const sheet = sheets.at(-1);
        if (sheet === undefined) throw new Error('The case writes before it adds a sheet.');
        return sheet;
    };
    switch (verb) {
        case 'addSheet':
            sheets.push(writer.addSheet(text(argument), optional<readonly XlsxColumnInput[] | undefined>(step, 'columns', value => list(value).map(columnInput), undefined),
                optional<XlsxSheetVisibility>(step, 'visibility', value => string(value) as XlsxSheetVisibility, 'visible')));
            return undefined;
        case 'write':
            last().write(cellInput(argument));
            return undefined;
        case 'writeAll':
            await last().writeAll(list(argument).map(cellInput));
            return undefined;
        case 'writeRow': {
            const [row, style] = list(argument).map(number);
            if (row === undefined || style === undefined) throw new Error(`${JSON.stringify(argument)} is not a row and its style.`);
            last().writeRow({ row, styleIndex: style });
            return undefined;
        }
        case 'addTable':
            last().addTable(tableInput(argument));
            return undefined;
        case 'flush':
            await last().flush();
            return undefined;
        case 'complete':
            return writer.complete();
        default:
            throw new Error(`'${verb}' is not a write step.`);
    }
}

/** How a step threw, as tests/cases/README.md names it; anything no case expects is rethrown. */
function thrown(error: unknown): Json {
    if (error instanceof XlsxError) {
        return { kind: 'refused', reason: error.reason, part: orNull(error.part), sheet: orNull(error.sheet), message: error.message };
    }
    if (error instanceof RangeError) return { kind: 'outOfRange', message: error.message };
    if (error instanceof TypeError) return { kind: 'argument', message: error.message };
    if (error instanceof Error) return { kind: 'invalidOperation', message: error.message };
    throw error;
}

const decoder = new TextDecoder();

/** Each part of the workbook, in the zip's order, as UTF-8 text with its line ends written as `\n`. */
async function partsOf(file: Blob): Promise<Json> {
    const source = byteSource(file);
    const parts: Json[] = [];
    for (const entry of await readCentralDirectory(source)) {
        const at = await dataOffset(source, entry);
        const inflated = source.stream(at, entry.compressedSize).pipeThrough(new DecompressionStream('deflate-raw'));
        const content = decoder.decode(await new Response(inflated).arrayBuffer());
        parts.push([entry.name, content.replace(/\r\n|[\r\n\f\u0085\u2028\u2029]/g, '\n')]);
    }
    return parts;
}

function writeOptions(given: JsonObject): XlsxWriteOptions {
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

function cellInput(value: Json): XlsxCellInput {
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

function columnInput(value: Json): XlsxColumnInput {
    const column = object(value);
    return {
        first: number(key(column, 'first')),
        last: number(key(column, 'last')),
        width: column['width'] === null ? null : number(key(column, 'width')),
        hidden: optional(column, 'hidden', flag, false),
        styleIndex: optional(column, 'style', number, 0),
        bestFit: optional(column, 'bestFit', flag, false),
        customWidth: optional(column, 'customWidth', flag, true),
    };
}

function tableInput(value: Json): XlsxTable {
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

function colorInput(value: Json): XlsxColor {
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

// ---- xlsxReference, xlsxColor ---------------------------------------------------------------------------

function referenceAnswer(entry: JsonObject): Json {
    if (entry['place'] !== undefined) {
        const [row, column] = list(entry['place']).map(number);
        if (row === undefined || column === undefined) throw new Error('A place is a row and a column.');
        return formatCell(row, column);
    }
    const place = parseCell(text(key(entry, 'text')));
    return place === undefined ? false : [place.row, place.column];
}

function colorAnswer(entry: JsonObject): Json {
    if (entry['tint'] !== undefined && entry['argb'] !== undefined) return hex(applyTint(parseHex(entry['argb']), number(entry['tint'])));
    const theme = isObject(entry) && entry['theme'] !== undefined ? new XlsxTheme(list(entry['theme']).map(parseHex)) : XlsxTheme.office;
    const indexed = optional<readonly number[] | null>(entry, 'indexedColors', v => list(v).map(parseHex), null);
    const resolved = theme.resolve(colorInput(key(entry, 'color')), indexed);
    return resolved === undefined ? null : hex(resolved);
}
