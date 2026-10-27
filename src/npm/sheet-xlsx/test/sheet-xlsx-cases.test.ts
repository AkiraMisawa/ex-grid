// Runs the shared cases of opening and saving a Workbook in tests/cases/sheet-xlsx/ (ADR-0330, ADR-0370 section 2), which
// xUnit runs against ExSheet.Xlsx as well: a file written as the .xlsx cases write one, edited part by part, opened, and what
// the Workbook Document and its Open Report say or how the file was refused; a Workbook built as the Sheet cases build one,
// saved, and the parts it made or how the save was refused; and the internal pieces both cores share, called one by one. A
// reader and a writer of .xlsx have no differential run, so what holds this package to ExSheet.Xlsx is these cases, answered
// by ExSheet.Xlsx. tests/cases/README.md says what a file holds.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { isDeepStrictEqual } from 'node:util';
import { describe, expect, it } from 'vitest';
import {
    CellAddress, CellFormat, CellRange, Culture, Entry, FormulaSyntaxError, NumberFormat, Sheet, SheetEdit, Value, Workbook, WorkbookDocument,
    type BorderLineStyle, type CellDisplay, type SheetColumnWidth,
} from '@ex-grid/sheet-engine';
import { createXlsxWriter, type XlsxCellInput, type XlsxRow, type XlsxSheetVisibility } from '@ex-grid/data-xlsx';
import { type Json, type JsonObject, flag, isObject, key, known, list, number, object, string, text } from '../../data-xlsx/test/case-json';
import { fileOf as xlsxFileOf, zipOf } from '../../data-xlsx/test/files';
import { cellInput, columnInput, holdsOf, partsOf, read, textsOf, writeOptions } from '../../data-xlsx/test/xlsx-case-io';
import { Run, cellFormatJson, cultureOf } from '../../sheet-engine/test/sheet-case';
import { openXlsxWorkbook, saveXlsxWorkbook, XlsxOpenError, XlsxSaveError, type XlsxSaveOptions } from '../src/index';
import { widthFromFile, widthToFile } from '../src/column-width-unit';
import { codeShowsDate, showsDate } from '../src/date-formats';
import { isoDateSerial } from '../src/iso-date';
import { builtInId } from '../src/number-formats';
import { readStoredFormula } from '../src/stored-formula';
import { fileLineStyle } from '../src/style-reading';
import { equalsIgnoreCase } from '../src/text-casing';
import { writeFormula } from '../src/written-formula';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../tests/cases/sheet-xlsx');

const kinds = ['sheetXlsx', 'sheetXlsxCall'];

// The steps this runner takes itself; every other step is a Sheet case's, taken by its runner.
const verbs = ['open', 'save', 'workbook', 'entries', 'keep', 'sameAs'];

const files = fs.readdirSync(root)
    .filter(name => name.endsWith('.json'))
    .toSorted()
    .map(name => ({ name, json: object(JSON.parse(fs.readFileSync(path.join(root, name), 'utf8')) as Json) }));

for (const { name, json } of files) {
    const kind = string(key(json, 'kind'));
    if (!kinds.includes(kind)) throw new Error(`${name} is of no kind this runner reads.`);
    describe(name, () => {
        for (const entry of list(key(json, 'cases')).map(object)) {
            // A case may write a row format over all 1,048,576 rows, which takes seconds, not milliseconds.
            it(string(key(entry, 'name')), { timeout: 60_000 }, async () => {
                expect.hasAssertions();
                if (kind === 'sheetXlsxCall') {
                    expect(escaped(call(entry))).toStrictEqual(key(entry, 'expect'));
                    return;
                }
                expect(await run(json, entry)).toStrictEqual([]);
            });
        }
    });
}

/** The differences a case of opening and saving finds, each a line naming its step. */
async function run(file: JsonObject, entry: JsonObject): Promise<string[]> {
    const name = string(key(entry, 'name'));
    const named = entry['culture'] === undefined ? file['culture'] : entry['culture'];
    if (named === undefined) throw new Error('Neither the case nor its file names a culture.');
    const culture = cultureOf(string(named));
    const sheetRun = new Run(culture);
    if (entry['sheets'] !== undefined) sheetRun.workbook = new Workbook(culture, list(entry['sheets']).map(text));
    const kept = new Map<string, Workbook>();
    let saved: Uint8Array | undefined;
    const steps = list(key(entry, 'steps')).map(object);
    for (let i = 0; i < steps.length; i++) {
        const step = elementOf(steps, i);
        const where = `${name}, step ${String(i + 1)}`;
        const taken = Object.keys(step).filter(k => verbs.includes(k));
        if (taken.length === 0) {
            sheetRun.take(step, where);
            continue;
        }
        if (taken.length > 1) throw new Error(`${where}: a step takes one verb, not ${taken.join(', ')}.`);
        let answer: Json;
        switch (elementOf(taken, 0)) {
            case 'open':
                known(step, 'open', 'culture', 'options', 'document', 'expect', 'note');
                answer = await open(step, sheetRun, saved, culture);
                break;
            case 'save': {
                known(step, 'save', 'parts', 'file', 'expect', 'note');
                const done = await save(step, sheetRun.workbook);
                answer = done.answer;
                if (done.saved !== undefined) saved = done.saved;
                break;
            }
            case 'workbook':
                known(step, 'workbook', 'note');
                sheetRun.workbook = Workbook.open(WorkbookDocument.fromJson(JSON.stringify(key(step, 'workbook'))));
                continue;
            case 'entries': {
                known(step, 'entries', 'on', 'note');
                const on = step['on'] === undefined ? sheetRun.workbook.shown.name : string(step['on']);
                // Text as the .xlsx cases write it, which can hold what a JSON string cannot, a constant or {"formula": text}.
                const entries = Object.entries(object(key(step, 'entries'))).map(([at, value]) => [CellAddress.parse(at),
                    isObject(value) && Object.keys(value).length === 1 && value['formula'] !== undefined
                        ? Entry.parse(text(value['formula']), culture)
                        : Entry.fromValue(Value.fromText(text(value)))] as const);
                sheetRun.workbook.do(on, SheetEdit.setEntries(entries));
                continue;
            }
            case 'keep':
                known(step, 'keep', 'note');
                kept.set(string(key(step, 'keep')), sheetRun.workbook);
                continue;
            default:
                known(step, 'sameAs', 'note');
                sameAs(object(key(step, 'sameAs')), kept, sheetRun.workbook, where, sheetRun.differences);
                continue;
        }
        answer = escaped(answer);
        const expected = step['expect'];
        if (expected === undefined) sheetRun.differences.push(`${where}: the step has no answer: write it with EXGRID_WRITE_SHEET_XLSX_CASES=1 on the .NET runner.`);
        else if (!isDeepStrictEqual(expected, answer)) sheetRun.differences.push(`${where}: expected ${JSON.stringify(expected)}, answered ${JSON.stringify(answer)}.`);
    }
    return sheetRun.differences;
}

function elementOf<T>(items: readonly T[], index: number): T {
    const item = items[index];
    if (item === undefined) throw new Error(`No item at ${String(index)}.`);
    return item;
}

// ---- open -------------------------------------------------------------------------------------------

async function open(step: JsonObject, sheetRun: Run, saved: Uint8Array | undefined, culture: Culture): Promise<Json> {
    const source = key(step, 'open');
    let bytes: Uint8Array;
    if (source === 'saved') {
        if (saved === undefined) throw new Error('The case opens what it saved before it saves.');
        bytes = saved;
    } else {
        bytes = await fileOf(object(source));
    }
    const given = step['options'] === undefined ? {} : object(step['options']);
    known(given, 'maxUncompressedBytes', 'maxCells');
    const options = {
        ...(given['maxUncompressedBytes'] === undefined ? {} : { maxUncompressedBytes: number(given['maxUncompressedBytes']) }),
        ...(given['maxCells'] === undefined ? {} : { maxCells: number(given['maxCells']) }),
    };
    const opening = step['culture'] === undefined ? culture : cultureOf(string(step['culture']));
    try {
        const { document, report } = await openXlsxWorkbook(bytes, opening, options);
        sheetRun.workbook = Workbook.open(document);
        const answer: { [key: string]: Json } = {};
        if (step['document'] === undefined || flag(step['document'])) answer['document'] = JSON.parse(document.toJson()) as Json;
        answer['report'] = {
            isEmpty: report.isEmpty,
            notCarried: report.notCarried.map(n => ({ kind: n.kind, sheet: n.sheet, detail: n.detail, description: n.description })),
            unsupportedFunctions: report.unsupportedFunctions.map(u => ({ sheet: u.sheet, cell: u.cell, functions: [...u.functions] })),
        };
        return answer;
    } catch (e) { // oxlint-disable-line ex-grid/no-catch -- how the file was refused is the case's answer; anything else is rethrown
        if (!(e instanceof XlsxOpenError)) throw e;
        return {
            refused: {
                reason: e.reason, sheet: orNull(e.sheet), cell: orNull(e.cell), held: orNull(e.held), message: e.message, fileRefusal: orNull(e.fileRefusal),
            },
        };
    }
}

function orNull<T extends Json>(value: T | undefined): T | null {
    return value === undefined ? null : value;
}

/**
 * The file an open reads: one of the .xlsx cases' files, or `{"pages": […], "options": …}`, a workbook written by
 * @ex-grid/data-xlsx's writer a page at a time; either then edited as `"edits"` says.
 */
async function fileOf(file: JsonObject): Promise<Uint8Array> {
    const { edits: _, ...source } = file;
    const bytes = file['pages'] === undefined
        ? await xlsxFileOf(source)
        : await pages(file['options'] === undefined ? {} : object(file['options']), list(file['pages']));
    return file['edits'] === undefined ? bytes : edited(bytes, list(file['edits']));
}

/** A workbook of the pages, each a worksheet: its formatted rows and cells written in the sheet's order. */
async function pages(options: JsonObject, given: readonly Json[]): Promise<Uint8Array> {
    const writer = createXlsxWriter(writeOptions(options));
    for (const node of given) {
        const page = object(node);
        known(page, 'name', 'cells', 'columns', 'rows', 'visibility');
        const columns = page['columns'] === undefined ? undefined : list(page['columns']).map(columnInput);
        const visibility = page['visibility'] === undefined ? 'visible' : string(page['visibility']) as XlsxSheetVisibility;
        const sheet = writer.addSheet(text(key(page, 'name')), columns, visibility);
        const byRow = new Map<number, XlsxCellInput[]>();
        for (const cell of (page['cells'] === undefined ? [] : list(page['cells'])).flatMap(c => cellsOf(object(c)))) {
            const row = byRow.get(cell.row);
            if (row === undefined) byRow.set(cell.row, [cell]);
            else row.push(cell);
        }
        const rows = new Map<number, XlsxRow>();
        for (const r of page['rows'] === undefined ? [] : list(page['rows'])) {
            const [at, style] = list(r);
            if (at === undefined || style === undefined) throw new Error(`${JSON.stringify(r)} is not a row and its format.`);
            rows.set(number(at), { row: number(at), styleIndex: number(style) });
        }
        for (const row of [...new Set([...byRow.keys(), ...rows.keys()])].toSorted((a, b) => a - b)) {
            const formatted = rows.get(row);
            if (formatted !== undefined) sheet.writeRow(formatted);
            const cells = byRow.get(row);
            if (cells !== undefined) for (const cell of cells.toSorted((a, b) => a.column - b.column)) sheet.write(cell);
        }
    }
    return new Uint8Array(await (await writer.complete()).arrayBuffer());
}

/** A cell as the .xlsx cases write one, or `{"run": "A3:GR3", …}`, the same cell in each place of a range. */
function cellsOf(cell: JsonObject): XlsxCellInput[] {
    if (cell['run'] === undefined) return [cellInput(cell)];
    const range = CellRange.parse(string(cell['run']));
    const { run: _, ...rest } = cell;
    const input = cellInput({ ...rest, at: 'A1' });
    return Array.from(range.cells(), a => ({ ...input, row: a.row, column: a.column }));
}

/**
 * The file with each edit made in turn, each of which must change it: `{"part", "replace": [[old, new], …]}` replaces every
 * occurrence, `{"part", "regex": [[pattern, replacement], …]}` every match, and `{"add": part, "text"}` adds a part, as
 * another program would leave one.
 */
async function edited(file: Uint8Array, edits: readonly Json[]): Promise<Uint8Array> {
    const parts = await textsOf(file);
    for (const node of edits) {
        const edit = object(node);
        if (edit['add'] !== undefined) {
            known(edit, 'add', 'text');
            parts.push([text(edit['add']), text(key(edit, 'text'))]);
            continue;
        }
        known(edit, 'part', 'replace', 'regex');
        const name = string(key(edit, 'part'));
        const at = parts.findIndex(p => p[0] === name);
        const part = parts[at];
        if (part === undefined) throw new Error(`The file has no part '${name}' to edit.`);
        let content = part[1];
        for (const pair of edit['replace'] === undefined ? [] : list(edit['replace'])) {
            const [from, to] = list(pair);
            if (from === undefined || to === undefined) throw new Error(`${JSON.stringify(pair)} is not a replacement.`);
            content = content.split(text(from)).join(text(to));
        }
        for (const pair of edit['regex'] === undefined ? [] : list(edit['regex'])) {
            const [pattern, to] = list(pair);
            if (pattern === undefined || to === undefined) throw new Error(`${JSON.stringify(pair)} is not a replacement.`);
            content = content.replace(new RegExp(string(pattern), 'g'), text(to));
        }
        if (content === part[1]) throw new Error(`The edit ${JSON.stringify(edit)} changes nothing.`);
        parts[at] = [name, content];
    }
    return zipOf(parts);
}

// ---- save -------------------------------------------------------------------------------------------

async function save(step: JsonObject, workbook: Workbook): Promise<{ readonly answer: Json; readonly saved: Uint8Array | undefined }> {
    const given = step['save'] === undefined ? {} : object(step['save']);
    known(given, 'maxUncompressedBytes', 'maxCells');
    const options: XlsxSaveOptions = {
        ...(given['maxUncompressedBytes'] === undefined ? {} : { maxUncompressedBytes: number(given['maxUncompressedBytes']) }),
        ...(given['maxCells'] === undefined ? {} : { maxCells: number(given['maxCells']) }),
    };
    let file: Blob;
    try {
        file = await saveXlsxWorkbook(workbook, options);
    } catch (e) { // oxlint-disable-line ex-grid/no-catch -- how the save was refused is the case's answer; anything else is rethrown
        if (e instanceof XlsxSaveError) {
            return { answer: { refused: { reason: e.reason, sheet: orNull(e.sheet), cell: orNull(e.cell), held: orNull(e.held), message: e.message } }, saved: undefined };
        }
        if (e instanceof RangeError) return { answer: { outOfRange: e.message }, saved: undefined };
        throw e;
    }
    const all = list(await partsOf(file));
    const answer: { [key: string]: Json } = { partNames: all.map(nameOf) };
    const parts = step['parts'];
    if (parts !== undefined) {
        if (Array.isArray(parts)) answer['parts'] = list(parts).map(n => elementOf(all.filter(p => nameOf(p) === string(n)), 0));
        else if (parts === true) answer['parts'] = all;
        else throw new Error('\'parts\' is true or a list of parts.');
    }
    if (step['file'] !== undefined) answer['file'] = await read(file, holdsOf(step['file']), {}, undefined);
    return { answer, saved: new Uint8Array(await file.arrayBuffer()) };
}

/** A part's name, as `partsOf` lists the part with its text. */
function nameOf(part: Json): string {
    return string(elementOf(list(part), 0));
}

// ---- sameAs -----------------------------------------------------------------------------------------

/**
 * Whether the Workbook shows what a kept one showed, Sheet by Sheet: each cell of the range in the same Cell Format and the
 * same display, the range's columns at the same widths, and the last cell of the Sheet in the same Cell Format. With
 * `"currencyWritten"`, the kept Workbook's built-in currency is taken as written: a code of the file's own spelling the
 * Workbook's currency (ADR-0330 §4).
 */
function sameAs(given: JsonObject, kept: ReadonlyMap<string, Workbook>, after: Workbook, where: string, differences: string[]): void {
    known(given, 'kept', 'range', 'currencyWritten');
    const before = kept.get(string(key(given, 'kept')));
    if (before === undefined) throw new Error(`${where}: no Workbook is kept as ${JSON.stringify(given['kept'])}.`);
    const range = CellRange.parse(string(key(given, 'range')));
    const written = given['currencyWritten'] !== undefined && flag(given['currencyWritten']);
    if (before.sheets.length !== after.sheets.length) {
        differences.push(`${where}: ${String(after.sheets.length)} Sheets, not ${String(before.sheets.length)}.`);
        return;
    }
    const currency = NumberFormat.builtInCurrency(before.culture).code;
    for (let s = 0; s < before.sheets.length; s++) {
        const was = elementOf(before.sheets, s);
        const now = elementOf(after.sheets, s);
        for (const at of [...range.cells(), new CellAddress(Sheet.rowCount - 1, Sheet.columnCount - 1)]) {
            let format = was.getCellFormat(at);
            if (written && equalsIgnoreCase(format.numberFormat.code, currency)) {
                format = new CellFormat(NumberFormat.parse(format.numberFormat.localCode(before.culture)), format.alignment, format.font, format.fill, format.borders);
            }
            same(`${where}: ${was.name}!${at.toString()}'s Cell Format`, cellFormatJson(format), cellFormatJson(now.getCellFormat(at)), differences);
            same(`${where}: ${was.name}!${at.toString()}'s display`, display(was.getDisplay(at)), display(now.getDisplay(at)), differences);
        }
        for (let column = range.first.column; column <= range.last.column; column++) {
            same(`${where}: ${was.name}'s column ${String(column)}'s width`, width(was.getColumnWidth(column)), width(now.getColumnWidth(column)), differences);
        }
    }
}

function same(what: string, before: Json, after: Json, differences: string[]): void {
    if (!isDeepStrictEqual(before, after)) differences.push(`${what} was ${JSON.stringify(before)}, and is ${JSON.stringify(after)}.`);
}

function display(shown: CellDisplay): Json {
    return { text: shown.text, cannotShow: shown.cannotShow, alignment: shown.alignment, colour: orNull(shown.colour), isNumber: shown.isNumber };
}

function width(of: SheetColumnWidth | undefined): Json {
    return of === undefined ? null : { width: of.width, kind: of.kind };
}

/**
 * The answer with each text that holds half of a surrogate pair written `{"utf16": …}`, each half as `\uXXXX`, as the cases
 * write such text: JSON's writers here and in .NET cannot both carry it as a string.
 */
function escaped(answer: Json): Json {
    if (Array.isArray(answer)) return list(answer).map(escaped);
    if (isObject(answer)) return Object.fromEntries(Object.entries(answer).map(([k, v]) => [k, escaped(v)]));
    if (typeof answer === 'string') {
        const half = halfPair(answer);
        return half === undefined ? answer : { utf16: half };
    }
    return answer;
}

/** The text with each half of a surrogate pair written `\uXXXX`, or `undefined` when it holds none. */
function halfPair(value: string): string | undefined {
    let out = '';
    let any = false;
    for (let i = 0; i < value.length; i++) {
        const c = value.charCodeAt(i);
        const next = i + 1 < value.length ? value.charCodeAt(i + 1) : 0;
        if (c >= 0xD800 && c <= 0xDBFF && next >= 0xDC00 && next <= 0xDFFF) {
            out += value.slice(i, i + 2);
            i++;
        } else if (c >= 0xD800 && c <= 0xDFFF) {
            out += `\\u${c.toString(16).toUpperCase().padStart(4, '0')}`;
            any = true;
        } else {
            out += value.charAt(i);
        }
    }
    return any ? out : undefined;
}

// ---- calls ------------------------------------------------------------------------------------------

/** What one of the internal pieces both cores share answers, as tests/cases/README.md names them. */
function call(entry: JsonObject): Json {
    known(entry, 'name', 'adr', 'call', 'argument', 'expect', 'note');
    const argument = key(entry, 'argument');
    const called = string(key(entry, 'call'));
    switch (called) {
        case 'storedFormula': {
            const { read: formula, problem } = readStoredFormula(string(argument));
            return { read: formula, problem };
        }
        case 'writtenFormula': {
            const given = object(argument);
            known(given, 'formula', 'sheets', 'tables');
            let parsed: Entry | undefined;
            try {
                parsed = Entry.parse(string(key(given, 'formula')), Culture.invariant);
            } catch (e) { // oxlint-disable-line ex-grid/no-catch -- a Formula the engine refuses is the case's answer; anything else is rethrown
                if (e instanceof FormulaSyntaxError) return { refused: 'formulaSyntax', beyondLimit: e.isBeyondLimit };
                throw e;
            }
            if (parsed === undefined) throw new Error('The formula is no Entry.');
            const sheets = given['sheets'] === undefined ? undefined : list(given['sheets']).map(string);
            const tables = given['tables'] === undefined ? undefined : object(given['tables']);
            const { text: stored, problem } = writeFormula(parsed,
                sheets === undefined ? undefined : n => sheets.some(s => equalsIgnoreCase(s, n)),
                tables === undefined ? undefined : t => {
                    const found = Object.entries(tables).find(([k]) => equalsIgnoreCase(k, t));
                    return found !== undefined && Array.isArray(found[1]) ? list(found[1]).map(text) : undefined;
                });
            if (problem === 'none') {
                // What is written never reads back as another Formula, whatever the case's answer says it is written as: it
                // reads back as itself, or is refused by name (an Excel Table's reference, ADR-0330 §6, or a 3D Reference's Spill Range).
                const readBack = readStoredFormula(stored);
                if (readBack.problem === 'none') expect(readBack.read).toBe(parsed.formula);
            }
            return { stored: problem === 'none' ? stored : null, problem };
        }
        case 'isoDate':
            return orNull(isoDateSerial(text(argument)));
        case 'codeShowsDate':
            return codeShowsDate(string(argument));
        case 'builtInShowsDate':
            return showsDate(number(argument), undefined);
        case 'widthToFile':
            return widthToFile(number(argument));
        case 'widthFromFile':
            return widthFromFile(number(argument));
        case 'builtInId':
            return orNull(builtInId(NumberFormat.parse(string(argument))));
        case 'fileLineStyle':
            return fileLineStyle(string(argument) as BorderLineStyle);
        default:
            throw new Error(`'${called}' is not a call.`);
    }
}
