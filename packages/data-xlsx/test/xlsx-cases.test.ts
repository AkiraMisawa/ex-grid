// Runs the shared .xlsx cases of tests/cases/xlsx/ (ADR-0330, ADR-0370 section 2), which xUnit runs against
// ExGrid.Data.Xlsx as well: a reader and a writer of .xlsx have no differential run, so what holds this package to
// ExGrid.Data.Xlsx is these cases, answered by ExGrid.Data.Xlsx. tests/cases/README.md says what a file holds.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import {
    XlsxError, XlsxTheme, applyTint, createXlsxWriter, formatCell, parseCell,
    type XlsxColumnInput, type XlsxReadOptions, type XlsxSheetVisibility, type XlsxWriter, type XlsxSheetWriter,
} from '../src/index';
import { decodeXString } from '../src/x-string';
import { type Json, type JsonObject, flag, hex, isObject, key, known, list, number, object, optional, orNull, parseHex, string, text } from './case-json';
import { fileOf } from './files';
import { cellInput, colorInput, columnInput, holdsOf, partsOf, read, tableInput, writeOptions } from './xlsx-case-io';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../tests/cases/xlsx');

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

async function readAnswer(entry: JsonObject): Promise<Json> {
    const given = optional(entry, 'options', object, {});
    known(given, 'maxUncompressedBytes', 'maxCells');
    const options: XlsxReadOptions = {
        ...(given['maxUncompressedBytes'] === undefined ? {} : { maxUncompressedBytes: number(given['maxUncompressedBytes']) }),
        ...(given['maxCells'] === undefined ? {} : { maxCells: number(given['maxCells']) }),
    };
    return read(await fileOf(key(entry, 'file')), holdsOf(entry['holds']), options, optional<string | undefined>(entry, 'saying', string, undefined));
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
