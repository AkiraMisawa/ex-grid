// Runs the Excel case corpus, dotnet/tests/ExSheet.Engine.Tests/ExcelCases/*.json, against the ported engine as
// ExcelCorpus.cs runs it against ExSheet.Engine (ADR-0047, ADR-0370 section 2): the same files, read as they are,
// each case's entries typed, its actions done, and its checked cell compared with what Excel answered. Excel is not
// asked again: the corpus's `expect` was written from ExcelOracle's records, in every locale they were recorded
// in, and xUnit reads the corpus and not the raw records, so this runner reads what xUnit reads.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { CellAddress } from '../src/cell-address';
import type { HorizontalAlignment } from '../src/cell-format';
import { CellFormatChange } from '../src/cell-format-change';
import { CellRange } from '../src/cell-range';
import { Culture } from '../src/culture';
import { formatRoundTrip, roundHalfToEven } from '../src/dotnet';
import { Entry } from '../src/entry';
import { FormulaSyntaxError } from '../src/formula-syntax-error';
import { NumberFormat } from '../src/number-format';
import { Sheet, type FillDirection } from '../src/sheet';
import { SheetRefusedError, type SheetRefusalReason } from '../src/sheet-change';
import { SheetEdit } from '../src/sheet-edit';
import { errorValues, Value, type ErrorValue } from '../src/value';
import { Workbook, type WorkbookStep } from '../src/workbook';

const directory = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../dotnet/tests/ExSheet.Engine.Tests/ExcelCases');

/** The Workbook Seed every case's Sheet is given (ADR-0180), as ExcelCorpus gives it. */
const corpusSeed = 20261011n;

/** A value of a case, as JSON holds it. */
type Json = null | boolean | number | string | readonly Json[] | { readonly [key: string]: Json };
type JsonObject = { readonly [key: string]: Json };

function isObject(value: Json | undefined): value is JsonObject {
    return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function object(value: Json | undefined, what: string): JsonObject {
    if (!isObject(value)) throw new Error(`${what} is not an object.`);
    return value;
}

function text(value: Json | undefined, what: string): string {
    if (typeof value !== 'string') throw new Error(`${what} is not text.`);
    return value;
}

function number(value: Json | undefined, what: string): number {
    if (typeof value !== 'number') throw new Error(`${what} is not a number.`);
    return value;
}

function list(value: Json | undefined, what: string): readonly Json[] {
    if (!Array.isArray(value)) throw new Error(`${what} is not a list.`);
    return value as readonly Json[];
}

function read(file: string): JsonObject {
    return object(JSON.parse(fs.readFileSync(path.join(directory, file), 'utf8')) as Json, file);
}

const fixtures = new Map<string, JsonObject>(Object.entries(object(read('fixtures.json')['fixtures'], 'fixtures'))
    .map(([name, fixture]) => [name, object(object(fixture, name)['cells'], `${name}'s cells`)]));

const areas = fs.readdirSync(directory).filter(f => f.endsWith('.json') && f !== 'fixtures.json').map(f => f.slice(0, -5)).toSorted();

/** The check column's width in characters, when the case sets one. */
function columnWidth(c: JsonObject): number | undefined {
    return c['columnWidth'] === undefined ? undefined : number(c['columnWidth'], 'columnWidth');
}

/** Whether the case asks how the typed entries widened the check column, as ExcelCorpus.TypedWidening does. */
function typedWidening(c: JsonObject): boolean {
    const expected = object(c['expect'], 'expect');
    return 'widens' in expected || 'widthAtMost' in expected || 'widthAtLeast' in expected;
}

/** The component's widening on entry, as ExcelCorpus.Widen does it. */
function widen(sheet: Sheet, address: CellAddress): void {
    const recorded = sheet.getColumnWidth(address.column);
    if (recorded?.isSetByUser === true) return;
    const needed = sheet.getWidthOnEntry(address);
    if (needed === undefined) return;
    if (needed <= (recorded?.width ?? Sheet.defaultColumnWidth)) return;
    sheet.setAutomaticColumnWidth(CellRange.wholeColumns(address.column, address.column), needed);
}

/** A value written in the corpus: a number, a boolean, null for a blank, or text — where text that is an Error Value's spelling is that Error Value. */
function toValue(e: Json | undefined): Value | undefined {
    if (e === null) return undefined;
    if (typeof e === 'number') return Value.fromNumber(e);
    if (typeof e === 'boolean') return Value.fromBoolean(e);
    if (typeof e === 'string') {
        const error = errorFromText(e);
        return error === undefined ? Value.fromText(e) : Value.fromError(error);
    }
    throw new Error(`Not a value: ${JSON.stringify(e)}`);
}

function errorFromText(t: string): ErrorValue | undefined {
    return errorValues.find(e => e === t);
}

function enterCells(sheet: Sheet, cells: JsonObject, widening = false): void {
    for (const [name, typed] of Object.entries(cells)) {
        const address = CellAddress.parse(name);
        sheet.enter(address, text(typed, name));
        if (widening) widen(sheet, address);
    }
}

function build(c: JsonObject, widening: boolean): Workbook {
    const culture = Culture.of(c['culture'] === undefined ? 'en-US' : text(c['culture'], 'culture'));
    const workbook = new Workbook(culture, c['sheetName'] === undefined ? undefined : [text(c['sheetName'], 'sheetName')]);
    const sheet = workbook.shown;
    workbook.setSeed(corpusSeed);
    if (c['tables'] !== undefined) {
        for (const [name, table] of Object.entries(object(c['tables'], 'tables'))) {
            const t = object(table, name);
            workbook.declareLinkedTable(name, list(t['columns'], 'columns').map(e => text(e, 'a column')));
            if (t['rows'] !== null) workbook.pushLinkedTable(name, list(t['rows'], 'rows').map(r => list(r, 'a row').map(toValue)));
        }
    }
    if (c['fixture'] !== undefined) {
        const fixture = fixtures.get(text(c['fixture'], 'fixture'));
        if (fixture === undefined) throw new Error(`No fixture ${JSON.stringify(c['fixture'])}.`);
        enterCells(sheet, fixture);
    }
    // Observed Value2 fixtures are already typed data, not strings to parse as entries.
    if (c['values'] !== undefined) {
        sheet.setEntries(Object.entries(object(c['values'], 'values')).map(([name, v]) => {
            const entry = v === null ? undefined
                : typeof v === 'string' ? Entry.fromValue(Value.fromText(v))
                : Entry.fromValue(toValue(v) as Value);
            return [CellAddress.parse(name), entry] as const;
        }));
    }
    enterCells(sheet, object(c['cells'], 'cells'), widening);
    if (c['formats'] !== undefined) {
        for (const [name, code] of Object.entries(object(c['formats'], 'formats'))) {
            sheet.setNumberFormatOf([CellAddress.parse(name)], NumberFormat.parse(text(code, name)));
        }
    }
    return workbook;
}

function edit(sheet: Sheet, a: JsonObject, what: string): SheetEdit {
    const count = (): number => (a['count'] === undefined ? 1 : number(a['count'], 'count'));
    const row = (): number => number(a['row'], 'row') - 1;
    const column = (): number => CellAddress.parse(`${text(a['column'], 'column')}1`).column;
    const range = (property: string): CellRange => CellRange.parse(text(a[property], property));
    switch (what) {
        case 'enter':
            return SheetEdit.enterMany(Object.entries(object(a['cells'], 'cells')).map(([name, typed]) => [CellAddress.parse(name), text(typed, name)] as const));
        case 'insertRows': return SheetEdit.insertRows(row(), count());
        case 'deleteRows': return SheetEdit.deleteRows(row(), count());
        case 'insertColumns': return SheetEdit.insertColumns(column(), count());
        case 'deleteColumns': return SheetEdit.deleteColumns(column(), count());
        case 'fill':
            return SheetEdit.fill(range('source'), range('target'), text(a['direction'], 'direction').toLowerCase() as FillDirection);
        case 'copy': {
            const block = sheet.copy(range('source')).block;
            if (block === undefined) throw new Error('The copy was refused.');
            const destination = range('destination');
            return destination.rowCount === 1 && destination.columnCount === 1 ? SheetEdit.paste(block, destination.first) : SheetEdit.paste(block, destination);
        }
        case 'pasteText':
            return SheetEdit.pasteText(list(a['rows'], 'rows').map(r => list(r, 'a row').map(f => text(f, 'a field'))), CellAddress.parse(text(a['at'], 'at')));
        case 'setColumnWidth': {
            const w = a['width'];
            // "automatic" is a width an entry widened the columns to (ADR-0046, 2026-09-28).
            if (a['automatic'] === true) return SheetEdit.setAutomaticColumnWidth(range('range'), number(w, 'width'));
            return SheetEdit.setColumnWidth(range('range'), w === null ? undefined : number(w, 'width'));
        }
        case 'rename': return SheetEdit.rename(text(a['name'], 'name'));
        case 'format':
        case 'align':
        case 'cellFormat': {
            // A range written with commas is several ranges formatted in one step (ADR-0046).
            const ranges = text(a['range'], 'range').split(',').map(r => CellRange.parse(r));
            const format = a['format'] === undefined ? undefined : NumberFormat.parse(text(a['format'], 'format'));
            const align = a['align'] === undefined ? undefined : text(a['align'], 'align').toLowerCase() as HorizontalAlignment;
            if (what === 'cellFormat' || ranges.length > 1) return SheetEdit.setCellFormat(ranges, new CellFormatChange({ numberFormat: format, alignment: align }));
            const first = ranges[0] as CellRange;
            if (what === 'format') return SheetEdit.setNumberFormat(first, format);
            if (align === undefined) throw new Error('An align action gives no alignment.');
            return SheetEdit.setAlignment(first, align);
        }
        default: throw new Error(`Unknown action "${what}".`);
    }
}

/** C#'s enum name for a refusal's reason, as the corpus writes it. */
function reasonName(reason: SheetRefusalReason): string {
    return reason.charAt(0).toUpperCase() + reason.slice(1);
}

/** Does the case's actions in order; the first refused one stops them, and its reason is returned. */
function act(workbook: Workbook, c: JsonObject): string | undefined {
    const action = c['action'];
    if (action === undefined) return undefined;
    const sheet = workbook.shown;
    const widening = typedWidening(c);
    const steps: WorkbookStep[] = [];
    for (const step of Array.isArray(action) ? (action as readonly Json[]) : [action]) {
        const a = object(step, 'an action');
        const what = text(a['do'], 'do');
        if (what === 'undo') {
            const last = steps.pop();
            if (last === undefined) throw new Error('Nothing to undo.');
            last.undo();
            continue;
        }
        const e = edit(sheet, a, what);
        const refusal = workbook.check(sheet.name, e);
        if (refusal !== undefined) {
            // Check and do agree: a refused edit throws with the same reason and changes nothing.
            let thrown: unknown;
            try {
                workbook.do(sheet.name, e);
            } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the refusal is caught to compare it with check's, and a wrong one fails the case below
                thrown = error;
            }
            if (!(thrown instanceof SheetRefusedError) || thrown.refusal.reason !== refusal.reason) {
                throw new Error(`check refused (${refusal.reason}) but do did not refuse alike.`);
            }
            return reasonName(refusal.reason);
        }
        steps.push(workbook.do(sheet.name, e));
        if (widening && what === 'enter') {
            for (const name of Object.keys(object(a['cells'], 'cells'))) widen(sheet, CellAddress.parse(name));
        }
    }
    return undefined;
}

/** `Math.Round(x, digits)` as .NET computes it: scaled, rounded half to even, scaled back. */
/** `Math.Round(x, digits)`, as the C# runner compares: a number of 1e16 or more is already whole, and is left as it is. */
function roundTo(x: number, digits: number): number {
    if (Math.abs(x) >= 1e16) return x;
    const power = 10 ** digits;
    return roundHalfToEven(x * power) / power;
}

function sameValue(expected: Json, kind: string | undefined, digits: number | undefined, actual: Value | undefined): boolean {
    if (expected === null) return actual === undefined;
    if (typeof expected === 'boolean') return actual?.kind === 'boolean' && actual.boolean === expected;
    if (typeof expected === 'number') {
        if (actual?.kind !== 'number') return false;
        return digits === undefined ? expected === actual.number : roundTo(expected, digits) === roundTo(actual.number, digits);
    }
    if (typeof expected === 'string') {
        const error = kind === 'text' ? undefined : errorFromText(expected);
        if (error !== undefined) return actual?.kind === 'error' && actual.error === error;
        return actual?.kind === 'text' && actual.text === expected;
    }
    throw new Error(`Not a value2: ${JSON.stringify(expected)}`);
}

function describeValue(value: Value | undefined): string {
    if (value === undefined) return 'blank';
    switch (value.kind) {
        case 'number': return formatRoundTrip(value.number);
        case 'text': return `text "${value.text}"`;
        case 'boolean': return value.boolean ? 'TRUE' : 'FALSE';
        case 'error': return value.error;
        default: throw new Error(`Not a kind: ${String(value.kind satisfies never)}`);
    }
}

function numberText(n: number | undefined): string {
    return n === undefined ? 'null' : formatRoundTrip(n);
}

/** Runs the case and returns every way the engine's answer differs from `expect`; empty when it agrees. */
function run(c: JsonObject): string[] {
    const expected = object(c['expect'], 'expect');
    const refusesEntry = expected['refusedEntry'] === true;
    let workbook: Workbook;
    try {
        workbook = build(c, typedWidening(c));
    } catch (e) { // oxlint-disable-line ex-grid/no-catch -- an entry refused is the case's answer when it expects one, and named as a difference when it does not; anything else is thrown on
        if (e instanceof FormulaSyntaxError) return refusesEntry ? [] : [`an entry was refused: ${e.message}`];
        // A format code refused, as Excel refuses one it will not take (the oracle counts it as a refused entry).
        if (e instanceof TypeError && e.message.startsWith('The number format')) return refusesEntry ? [] : [`a format was refused: ${e.message}`];
        throw e;
    }
    if (refusesEntry) return ['expected an entry refused, but every entry was taken'];
    const refusal = act(workbook, c);
    const sheet = workbook.shown;
    const at = CellAddress.parse(text(c['check'], 'check'));
    const differences: string[] = [];

    if (expected['refused'] !== undefined) {
        const refused = text(expected['refused'], 'refused');
        if (refusal !== refused) differences.push(`expected the action refused (${refused}), but it was ${refusal === undefined ? 'done' : `refused (${refusal})`}`);
    } else if (refusal !== undefined) {
        differences.push(`the action was refused (${refusal})`);
    }

    if (expected['value2'] !== undefined) {
        const kind = expected['kind'] === undefined ? undefined : text(expected['kind'], 'kind');
        const digits = expected['digits'] === undefined ? undefined : number(expected['digits'], 'digits');
        const actual = sheet.getValue(at);
        if (!sameValue(expected['value2'], kind, digits, actual)) {
            differences.push(`value2: expected ${JSON.stringify(expected['value2'])}${kind === undefined ? '' : ` (${kind})`}, got ${describeValue(actual)}`);
        }
    }

    if (expected['text'] !== undefined) {
        // The oracle reads Range.Text in a column 100 characters wide unless the case gives the column's width.
        const display = sheet.getDisplay(at, columnWidth(c) ?? 100); // oxlint-disable-line ex-grid/no-stand-in-default -- the oracle reads Range.Text 100 characters wide unless the case gives a width
        const shown = display.cannotShow ? '####' : display.text;
        if (shown !== expected['text']) differences.push(`text: expected "${text(expected['text'], 'text')}", got "${shown}"`);
    }

    if (expected['widthOnEntry'] !== undefined) {
        const wanted = expected['widthOnEntry'] === null ? undefined : number(expected['widthOnEntry'], 'widthOnEntry');
        const actual = sheet.getWidthOnEntry(at);
        if (actual !== wanted) differences.push(`widthOnEntry: expected ${numberText(wanted)}, got ${numberText(actual)}`);
    }

    if (expected['widens'] !== undefined) {
        const recorded = sheet.getColumnWidth(at.column);
        const actual = recorded !== undefined && recorded.width > Sheet.defaultColumnWidth;
        if (actual !== expected['widens']) differences.push(`widens: expected ${JSON.stringify(expected['widens'])}, got ${String(actual)}`);
    }

    if (expected['widthAtMost'] !== undefined) {
        const actual = sheet.getColumnWidth(at.column)?.width ?? Sheet.defaultColumnWidth;
        if (actual > number(expected['widthAtMost'], 'widthAtMost')) differences.push(`widthAtMost: expected at most ${JSON.stringify(expected['widthAtMost'])}, got ${formatRoundTrip(actual)}`);
    }

    if (expected['widthAtLeast'] !== undefined) {
        const actual = sheet.getColumnWidth(at.column)?.width ?? Sheet.defaultColumnWidth;
        if (actual < number(expected['widthAtLeast'], 'widthAtLeast')) differences.push(`widthAtLeast: expected at least ${JSON.stringify(expected['widthAtLeast'])}, got ${formatRoundTrip(actual)}`);
    }

    if (expected['width'] !== undefined) {
        const wanted = expected['width'] === null ? undefined : number(expected['width'], 'width');
        const actual = sheet.getColumnWidth(at.column)?.width;
        if (actual !== wanted) differences.push(`width: expected ${numberText(wanted)}, got ${numberText(actual)}`);
    }

    if (expected['custom'] !== undefined) {
        const actual = sheet.getColumnWidth(at.column)?.isCustom === true;
        if (actual !== expected['custom']) differences.push(`custom: expected ${JSON.stringify(expected['custom'])}, got ${String(actual)}`);
    }

    if (expected['formula'] !== undefined) {
        const entry = sheet.getEntry(at);
        const written = entry === undefined ? '' : entry.formula ?? sheet.getEntryText(at);
        if (written !== expected['formula']) differences.push(`formula: expected "${text(expected['formula'], 'formula')}", got "${written}"`);
    }

    if (expected['numberFormat'] !== undefined) {
        const code = sheet.getNumberFormat(at).code;
        if (code !== expected['numberFormat']) differences.push(`numberFormat: expected "${text(expected['numberFormat'], 'numberFormat')}", got "${code}"`);
    }

    if (expected['alignment'] !== undefined) {
        const setting = sheet.getAlignment(at);
        if (setting !== expected['alignment']) differences.push(`alignment: expected "${text(expected['alignment'], 'alignment')}", got "${setting}"`);
    }
    return differences;
}

describe('Excel case corpus (ADR-0047)', () => {
    for (const area of areas) {
        const document = read(`${area}.json`);
        if (document['area'] !== area) throw new Error(`${area}.json names another area.`);
        const cases = list(document['cases'], 'cases').map(c => object(c, 'a case'));
        describe(area, () => {
            it.each(cases.map(c => [text(c['id'], 'id'), c] as const))('%s', (id, c) => {
                const differences = run(c);
                expect(differences, `${id} (${text(c['description'], 'description')})`).toEqual([]);
            });
        });
    }
});

