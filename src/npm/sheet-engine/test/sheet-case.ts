// Runs a shared case of tests/cases/sheet/ (ADR-0370 sections 2 and 5) against the ported engine, as
// tests/ExSheet.Engine.Tests/Cases/SheetCases.cs runs it against ExSheet.Engine: a Workbook made under a culture,
// steps taken on it in turn, and what each step reads back. tests/cases/README.md says what a file holds.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { CellAddress } from '../src/cell-address';
import { BorderLine, CellColour, CellFill, type BorderLineStyle, type CellBorders, type CellFont, type HorizontalAlignment } from '../src/cell-format';
import { BorderChange, CellFormatChange } from '../src/cell-format-change';
import { CellRange } from '../src/cell-range';
import { Culture } from '../src/culture';
import { formatRoundTrip } from '../src/dotnet';
import { Entry } from '../src/entry';
import { FormatError } from '../src/format-error';
import { FormulaSyntaxError } from '../src/formula-syntax-error';
import { NumberFormat } from '../src/number-format';
import type { FillDirection, Sheet, SheetCopy } from '../src/sheet';
import { RepeatedKeyError, SheetRefusedError, type SheetChange, type SheetRefusal } from '../src/sheet-change';
import { SheetDocumentError, type SheetDocumentAxisFormat } from '../src/sheet-document-parts';
import type { SheetDocument } from '../src/sheet-document';
import { SheetEdit } from '../src/sheet-edit';
import { parseWrittenError, Value } from '../src/value';
import { Workbook, type WorkbookStep } from '../src/workbook';
import { WorkbookDocument } from '../src/workbook-document';

/** Where the shared cases of the engine live. */
export const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../tests/cases/sheet');

/** A value of a case, as JSON holds it. */
export type Json = null | boolean | number | string | readonly Json[] | { readonly [key: string]: Json };
/** An object of a case. */
export type JsonObject = { readonly [key: string]: Json };

function isObject(value: Json | undefined): value is JsonObject {
    return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function object(value: Json | undefined, what: string): JsonObject {
    if (!isObject(value)) throw new CaseError(`${what} is not an object.`);
    return value;
}

function text(value: Json | undefined, what: string): string {
    if (typeof value !== 'string') throw new CaseError(`${what} is not text.`);
    return value;
}

function list(value: Json | undefined, what: string): readonly Json[] {
    if (!Array.isArray(value)) throw new CaseError(`${what} is not a list.`);
    return value as readonly Json[];
}

function integer(value: Json | undefined, what: string): number {
    if (typeof value !== 'number' || !Number.isInteger(value)) throw new CaseError(`${what} is not a whole number.`);
    return value;
}

/** Reads a file of the shared cases. */
export function readFile(file: string): JsonObject {
    return object(JSON.parse(fs.readFileSync(path.join(root, file), 'utf8')) as Json, file);
}

/** Every case of every file of the kind, as [file, name] pairs. */
export function casesOf(kind: string): [string, string][] {
    const rows: [string, string][] = [];
    for (const file of fs.readdirSync(root).filter(f => f.endsWith('.json')).toSorted()) {
        const document = readFile(file);
        if (document['kind'] !== kind) continue;
        const names = new Set<string>();
        for (const entry of list(document['cases'], `${file}'s cases`)) {
            const name = text(object(entry, 'a case')['name'], 'a case name');
            if (names.has(name)) throw new CaseError(`sheet/${file} holds two cases named "${name}".`);
            names.add(name);
            rows.push([file, name]);
        }
    }
    return rows;
}

/** The case of the file named so, and the file it is in. */
export function findCase(file: string, name: string): { file: JsonObject; entry: JsonObject } {
    const document = readFile(file);
    for (const entry of list(document['cases'], `${file}'s cases`)) {
        if (object(entry, 'a case')['name'] === name) return { file: document, entry: object(entry, 'a case') };
    }
    throw new CaseError(`sheet/${file} holds no case named "${name}".`);
}

/** A number a case writes: a JSON number, or `"Infinity"`, `"-Infinity"`, `"NaN"`. */
export function caseNumber(value: Json | undefined, what: string): number {
    if (typeof value === 'number') return value;
    if (value === 'Infinity') return Infinity;
    if (value === '-Infinity') return -Infinity;
    if (value === 'NaN') return NaN;
    throw new CaseError(`${what} is no number.`);
}

/** A Value as a case writes one: a number, text, a Boolean, `null` for a blank, and an Error Value as `{"error": "#N/A"}`. */
export function encode(value: Value | undefined): Json {
    if (value === undefined) return null;
    switch (value.kind) {
        case 'number':
            return value.number;
        case 'text':
            return value.text;
        case 'boolean':
            return value.boolean;
        case 'error':
            return value.isRefused ? { error: value.error, refused: true } : { error: value.error };
        default:
            throw new CaseError('Internal: no kind of Value.');
    }
}

/** A Value a case writes, read back; the inverse of `encode`. */
export function decode(node: Json | undefined): Value | undefined {
    if (node === null || node === undefined) return undefined;
    if (typeof node === 'string') return Value.fromText(node);
    if (typeof node === 'boolean') return Value.fromBoolean(node);
    if (typeof node === 'number') return Value.fromNumber(node);
    if (isObject(node)) {
        // The two states of a computation are no Error Value typed, but a case may give one to be refused.
        const written = node['error'];
        const error = written === '#CIRC!' || written === '#GETTING_DATA' ? written : typeof written === 'string' ? parseWrittenError(written) : undefined;
        if (error === undefined || Object.keys(node).length !== 1) throw new CaseError(`${JSON.stringify(node)} is no Error Value a case gives.`);
        return Value.fromError(error);
    }
    throw new CaseError(`${JSON.stringify(node)} is no Value.`);
}

function colourOf(node: Json | undefined): CellColour {
    const written = text(node, 'a colour');
    if (written === 'Automatic') return CellColour.automatic;
    const colour = CellColour.fromHex(written);
    if (colour === undefined) throw new CaseError(`'${written}' is no colour.`);
    return colour;
}

function font(of: CellFont): JsonObject {
    return { colour: of.colour.toString(), bold: of.bold, italic: of.italic, underline: of.underline, strikethrough: of.strikethrough };
}

function fill(of: CellFill): Json {
    return of.colour === undefined ? 'none' : of.colour.toString();
}

function fillOf(node: Json | undefined): CellFill {
    return node === 'none' ? CellFill.none : CellFill.solid(colourOf(node));
}

function line(of: BorderLine): Json {
    return of.isNone ? 'none' : { style: of.style, colour: of.colour.toString() };
}

function lineOf(node: Json | undefined): BorderLine {
    if (node === 'none') return BorderLine.none;
    const written = object(node, 'a line');
    return new BorderLine(text(written['style'], 'style') as BorderLineStyle, written['colour'] === undefined ? CellColour.automatic : colourOf(written['colour']));
}

function borders(of: CellBorders): JsonObject {
    return { top: line(of.top), bottom: line(of.bottom), left: line(of.left), right: line(of.right) };
}

function entryJson(entry: Entry | undefined): Json {
    if (entry === undefined) return null;
    return entry.formula === undefined ? { constant: encode(entry.constant) } : { formula: entry.formula };
}

const changeParts = new Set(['numberFormat', 'alignment', 'fontColour', 'bold', 'italic', 'underline', 'strikethrough', 'fill', 'borders']);
const edges = new Set(['top', 'bottom', 'left', 'right', 'insideHorizontal', 'insideVertical']);

function flag(node: Json | undefined, what: string): boolean | undefined {
    if (node === undefined) return undefined;
    if (typeof node !== 'boolean') throw new CaseError(`${what} is not true or false.`);
    return node;
}

/** A Cell Format change as a case writes one: each part it sets, by the Script Core's name. */
function changeOf(node: Json | undefined): CellFormatChange {
    const parts = object(node, 'a Cell Format change');
    for (const key of Object.keys(parts)) {
        if (!changeParts.has(key)) throw new CaseError(`'${key}' is no part of a Cell Format change.`);
    }
    return new CellFormatChange({
        numberFormat: parts['numberFormat'] === undefined ? undefined : NumberFormat.parse(text(parts['numberFormat'], 'numberFormat')),
        alignment: parts['alignment'] === undefined ? undefined : text(parts['alignment'], 'alignment') as HorizontalAlignment,
        fontColour: parts['fontColour'] === undefined ? undefined : colourOf(parts['fontColour']),
        bold: flag(parts['bold'], 'bold'),
        italic: flag(parts['italic'], 'italic'),
        underline: flag(parts['underline'], 'underline'),
        strikethrough: flag(parts['strikethrough'], 'strikethrough'),
        fill: parts['fill'] === undefined ? undefined : fillOf(parts['fill']),
        borders: parts['borders'] === undefined ? undefined : borderChangeOf(parts['borders']),
    });
}

function borderChangeOf(node: Json): BorderChange {
    if (node === 'none') return BorderChange.none;
    const given = object(node, 'a Border change');
    const keys = Object.keys(given);
    if (keys.length === 1 && given['outline'] !== undefined) return BorderChange.outline(lineOf(given['outline']));
    if (keys.length === 1 && given['inside'] !== undefined) return BorderChange.inside(lineOf(given['inside']));
    for (const key of keys) {
        if (!edges.has(key)) throw new CaseError(`'${key}' is no edge of a Border change.`);
    }
    const edge = (key: string): BorderLine | undefined => given[key] === undefined ? undefined : lineOf(given[key]);
    return new BorderChange({
        top: edge('top'), bottom: edge('bottom'), left: edge('left'), right: edge('right'),
        insideHorizontal: edge('insideHorizontal'), insideVertical: edge('insideVertical'),
    });
}

/** A run of columns or rows formatted alike, as a case reads one. */
function axisFormatJson(of: SheetDocumentAxisFormat): JsonObject {
    return {
        first: of.first,
        last: of.last,
        numberFormat: orNull(of.numberFormat?.code),
        alignment: orNull(of.alignment),
        font: of.font === undefined ? null : font(of.font),
        fill: of.fill === undefined ? null : fill(of.fill),
        borders: of.borders === undefined ? null : borders(of.borders),
    };
}

/** A Sheet Document as a case reads one: its cells, its column and row formats and its column widths. */
function documentJson(document: SheetDocument): JsonObject {
    return {
        cells: document.cells.map(c => ({
            address: c.address.toString(),
            entry: entryJson(c.entry),
            numberFormat: orNull(c.numberFormat?.code),
            alignment: orNull(c.alignment),
            font: c.font === undefined ? null : font(c.font),
            fill: c.fill === undefined ? null : fill(c.fill),
            borders: c.borders === undefined ? null : borders(c.borders),
        })),
        cellAddresses: document.cells.map(c => c.address.toString()),
        columns: document.columns.map(axisFormatJson),
        rows: document.rows.map(axisFormatJson),
        columnWidths: document.columnWidths.map(w => ({ first: w.first, last: w.last, width: w.width, kind: w.kind })),
    };
}

/** What a case writes for a reading the engine leaves out: `null`, as the C# runner reads a missing key. */
function orNull<T extends Json>(value: T | undefined): T | null {
    return value === undefined ? null : value;
}

function show(node: Json | undefined): string {
    if (node === undefined) return 'null';
    if (typeof node === 'number') return formatRoundTrip(node);
    return JSON.stringify(node);
}

/**
 * Whether `actual` is what `expected` says, and where not, how: an object is compared by the keys the case writes,
 * a key the engine leaves out reading as `null`; a list, element by element; a number, exactly, or within
 * `{"near": n, "within": e}`.
 */
export function compare(where: string, expected: Json | undefined, actual: Json | undefined, differences: string[]): void {
    if (isObject(expected) && 'near' in expected && 'within' in expected && Object.keys(expected).length === 2) {
        const centre = caseNumber(expected['near'], 'near');
        const within = caseNumber(expected['within'], 'within');
        if (typeof actual !== 'number' || Math.abs(actual - centre) > within) {
            differences.push(`${where}: expected ${formatRoundTrip(centre)} within ${formatRoundTrip(within)}, got ${show(actual)}`);
        }
        return;
    }
    if (isObject(expected) && isObject(actual)) {
        for (const [key, value] of Object.entries(expected)) compare(`${where}.${key}`, value, orNull(actual[key]), differences);
        return;
    }
    if (Array.isArray(expected) && Array.isArray(actual)) {
        const e = expected as readonly Json[];
        const a = actual as readonly Json[];
        if (e.length !== a.length) {
            differences.push(`${where}: expected ${JSON.stringify(e)}, got ${JSON.stringify(a)}`);
            return;
        }
        for (let i = 0; i < e.length; i++) compare(`${where}[${String(i)}]`, e[i], a[i], differences);
        return;
    }
    if (typeof expected === 'number' && typeof actual === 'number') {
        if (!Object.is(expected, actual) && !(expected === actual && expected !== 0)) {
            differences.push(`${where}: expected ${show(expected)}, got ${show(actual)}`);
        }
        return;
    }
    if (JSON.stringify(orNull(expected)) !== JSON.stringify(orNull(actual))) {
        differences.push(`${where}: expected ${show(expected)}, got ${show(actual)}`);
    }
}

function cultureOf(name: string): Culture {
    return name === 'invariant' ? Culture.invariant : Culture.of(name);
}

const verbs = new Set([
    'enter', 'enterAll', 'enterRun', 'do', 'check', 'undo', 'redo', 'reopen', 'remember', 'read', 'copy',
    'setNumberFormat', 'setAlignment', 'declareTable', 'pushTable', 'setToday', 'setSeed', 'recalculateVolatile',
]);
const modifiers = new Set(['on', 'expect', 'note']);

/** What a step did, for its readings: the change it reported, the refusal it was checked for, its step. */
interface Outcome {
    sheet: Sheet;
    change: SheetChange | undefined;
    checked: boolean;
    refusal: SheetRefusal | undefined;
    step: WorkbookStep | undefined;
}

function addresses(of: Iterable<CellAddress>): string[] {
    return [...of].map(a => a.toString());
}

/** A case the runner cannot read: the case's defect, never a refusal it can expect. */
class CaseError extends Error {}

/**
 * A refusal as a case expects it, in the words the C# runner uses for .NET's exception types. What is not the
 * engine refusing is thrown on: the runner's own `CaseError`, an engine defect (`Internal: …`), and what
 * JavaScript throws by itself (a plain `TypeError`, the call stack running out).
 */
function thrown(e: unknown): JsonObject {
    if (e instanceof CaseError) throw e;
    if (e instanceof FormulaSyntaxError) return { refusal: 'formulaSyntax', saying: e.message };
    if (e instanceof SheetRefusedError) return { refusal: 'refused', reason: e.refusal.reason, saying: e.refusal.message };
    if (e instanceof RepeatedKeyError) return { refusal: 'repeatedKey', saying: e.message };
    if (e instanceof SheetDocumentError) return { refusal: 'document', saying: e.message };
    if (e instanceof FormatError) return { refusal: 'format', saying: e.message };
    if (e instanceof RangeError && e.message !== 'Maximum call stack size exceeded') return { refusal: 'argument', saying: e.message };
    if (e instanceof TypeError || e instanceof RangeError) throw e;
    if (e instanceof Error && !e.message.startsWith('Internal')) return { refusal: 'invalidOperation', saying: e.message };
    throw e;
}

/** One case being run: its Workbook, the steps it has done, and what it remembered. */
class Run {
    readonly culture: Culture;
    workbook: Workbook;
    readonly differences: string[] = [];
    readonly #done: WorkbookStep[] = [];
    readonly #pictures = new Map<string, string>();
    readonly #copies = new Map<string, SheetCopy>();

    constructor(culture: Culture) {
        this.culture = culture;
        this.workbook = new Workbook(culture);
    }

    take(step: JsonObject, where: string): void {
        for (const key of Object.keys(step)) {
            if (!verbs.has(key) && !modifiers.has(key)) throw new CaseError(`${where}: '${key}' is no step or modifier.`);
        }
        const named = Object.keys(step).filter(k => verbs.has(k));
        const verb = named[0];
        if (named.length !== 1 || verb === undefined) throw new CaseError(`${where}: a step takes one verb, not ${String(named.length)}.`);
        const argument = orNull(step[verb]);
        const on = step['on'];
        let sheet = this.workbook.shown;
        if (on !== undefined) {
            const found = this.workbook.find(text(on, 'on'));
            if (found === undefined) throw new CaseError(`${where}: the Workbook has no Sheet '${JSON.stringify(on)}'.`);
            sheet = found;
        }
        const expect = step['expect'] === undefined ? undefined : object(step['expect'], 'expect');
        const outcome: Outcome = { sheet, change: undefined, checked: false, refusal: undefined, step: undefined };
        try {
            this.#act(verb, argument, sheet, outcome);
        } catch (e) { // oxlint-disable-line ex-grid/no-catch -- a refusal a case expects is read, not rethrown
            if (!(e instanceof Error) || e.message.startsWith('Internal')) throw e;
            const throws = expect?.['throws'];
            if (throws === undefined) {
                this.differences.push(`${where} (${verb}) threw ${e.name}: ${e.message}`);
                return;
            }
            compare(`${where}.throws`, throws, thrown(e), this.differences);
            for (const [key, value] of Object.entries(expect === undefined ? {} : expect)) {
                if (key !== 'throws') this.#read(where, key, value, outcome);
            }
            return;
        }
        if (expect === undefined) return;
        if (expect['throws'] !== undefined) {
            this.differences.push(`${where} (${verb}): expected it to throw ${JSON.stringify(expect['throws'])}, but it did not`);
            return;
        }
        for (const [key, value] of Object.entries(expect)) this.#read(where, key, value, outcome);
    }

    #act(verb: string, argument: Json, sheet: Sheet, outcome: Outcome): void {
        switch (verb) {
            case 'enter':
                for (const [address, typed] of Object.entries(object(argument, 'enter'))) {
                    outcome.change = sheet.enter(CellAddress.parse(address), text(typed, address));
                }
                break;
            case 'enterAll':
                outcome.change = sheet.enterMany(Object.entries(object(argument, 'enterAll')).map(([a, t]) => [CellAddress.parse(a), text(t, a)] as const));
                break;
            case 'enterRun': {
                // One batch over a range, each cell typed from a template naming its row: {row} or {row-1}.
                const run = object(argument, 'enterRun');
                const typed = text(run['typed'], 'enterRun.typed');
                outcome.change = sheet.enterMany([...CellRange.parse(text(run['range'], 'enterRun.range')).cells()].map(a =>
                    [a, typed.replaceAll('{row-1}', String(a.row)).replaceAll('{row}', String(a.row + 1))] as const));
                break;
            }
            case 'do': {
                const step = this.workbook.do(sheet.name, this.#edit(argument));
                this.#done.push(step);
                outcome.step = step;
                outcome.sheet = step.sheet;
                outcome.change = step.change.changesSheets && !this.workbook.sheets.includes(step.sheet) ? undefined : step.change.for(step.sheet);
                break;
            }
            case 'check':
                outcome.checked = true;
                outcome.refusal = this.workbook.check(sheet.name, this.#edit(argument));
                break;
            case 'undo':
            case 'redo': {
                const done = this.#stepOf(argument);
                outcome.step = done;
                const change = verb === 'undo' ? done.undo() : done.redo();
                outcome.change = this.workbook.sheets.includes(done.sheet) ? change.for(done.sheet) : undefined;
                break;
            }
            case 'reopen':
                if (argument === 'document') this.workbook = Workbook.open(this.workbook.toDocument());
                else if (argument === 'json') this.workbook = Workbook.open(WorkbookDocument.fromJson(this.workbook.toDocument().toJson()));
                else throw new CaseError(`'${JSON.stringify(argument)}' is no way to reopen a Workbook.`);
                outcome.sheet = this.workbook.shown;
                break;
            case 'remember':
                this.#pictures.set(text(argument, 'remember'), this.#picture());
                break;
            case 'read':
                if (argument !== true) throw new CaseError("'read' takes true.");
                break;
            case 'copy': {
                const copy = object(argument, 'copy');
                this.#copies.set(text(copy['as'], 'as'), sheet.copy(CellRange.parse(text(copy['range'], 'range'))));
                break;
            }
            case 'setNumberFormat':
                for (const [at, code] of Object.entries(object(argument, 'setNumberFormat'))) {
                    outcome.change = sheet.setNumberFormat(CellRange.parse(at), code === null ? undefined : NumberFormat.parse(text(code, at)));
                }
                break;
            case 'setAlignment':
                for (const [at, alignment] of Object.entries(object(argument, 'setAlignment'))) {
                    outcome.change = sheet.setAlignment(CellRange.parse(at), text(alignment, at) as HorizontalAlignment);
                }
                break;
            case 'declareTable': {
                const table = object(argument, 'declareTable');
                const columns = list(table['columns'], 'columns').map(c => text(c, 'a column'));
                const key = table['key'];
                const name = text(table['name'], 'name');
                const change = key === undefined || key === null
                    ? this.workbook.declareLinkedTable(name, columns)
                    : this.workbook.declareLinkedTable(name, columns, Array.isArray(key) ? list(key, 'key').map(k => text(k, 'a key column')) : text(key, 'key'));
                outcome.change = change.for(sheet);
                break;
            }
            case 'pushTable': {
                const table = object(argument, 'pushTable');
                const rows = list(table['rows'], 'rows').map(r => list(r, 'a row').map(decode));
                outcome.change = this.workbook.pushLinkedTable(text(table['name'], 'name'), rows).for(sheet);
                break;
            }
            case 'setToday': {
                if (argument === null) {
                    outcome.change = this.workbook.setToday(undefined).for(sheet);
                    break;
                }
                const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(text(argument, 'setToday'));
                if (match === null) throw new CaseError(`'${JSON.stringify(argument)}' is no day.`);
                outcome.change = this.workbook.setToday({ year: Number(match[1]), month: Number(match[2]), day: Number(match[3]) }).for(sheet);
                break;
            }
            case 'setSeed':
                outcome.change = this.workbook.setSeed(argument === null ? undefined : BigInt(typeof argument === 'number' ? argument : text(argument, 'setSeed'))).for(sheet);
                break;
            case 'recalculateVolatile':
                outcome.change = this.workbook.recalculateVolatile().for(sheet);
                break;
            default:
                throw new CaseError(`'${verb}' is no step.`);
        }
    }

    #stepOf(argument: Json): WorkbookStep {
        if (argument === 'last') {
            const last = this.#done.at(-1);
            if (last === undefined) throw new CaseError('No step has been done.');
            return last;
        }
        const step = this.#done[integer(argument, 'a step')];
        if (step === undefined) throw new CaseError(`No step ${JSON.stringify(argument)} has been done.`);
        return step;
    }

    /** A Sheet Edit as a case writes one: an object of one key, the factory, and its arguments. */
    #edit(node: Json): SheetEdit {
        const edit = object(node, 'an edit');
        const keys = Object.keys(edit);
        const name = keys[0];
        if (keys.length !== 1 || name === undefined) throw new CaseError(`An edit names one operation: ${JSON.stringify(edit)}.`);
        const argument = orNull(edit[name]);
        const args = (): JsonObject => object(argument, name);
        switch (name) {
            case 'enter':
                return SheetEdit.enterMany(Object.entries(args()).map(([a, t]) => [CellAddress.parse(a), text(t, a)] as const));
            case 'setEntries':
                return SheetEdit.setEntries(Object.entries(args()).map(([a, e]) => [CellAddress.parse(a), entryOf(e)] as const));
            case 'setNumberFormat': {
                const code = args()['format'];
                const format = code === undefined || code === null ? undefined : NumberFormat.parse(text(code, 'format'));
                const at = args()['at'];
                return Array.isArray(at)
                    ? SheetEdit.setNumberFormatOf(list(at, 'at').map(c => CellAddress.parse(text(c, 'a cell'))), format)
                    : SheetEdit.setNumberFormat(CellRange.parse(text(at, 'at')), format);
            }
            case 'setAlignment': {
                const alignment = text(args()['alignment'], 'alignment') as HorizontalAlignment;
                const at = args()['at'];
                return Array.isArray(at)
                    ? SheetEdit.setAlignmentOf(list(at, 'at').map(c => CellAddress.parse(text(c, 'a cell'))), alignment)
                    : SheetEdit.setAlignment(CellRange.parse(text(at, 'at')), alignment);
            }
            case 'setCellFormat':
                return SheetEdit.setCellFormat(list(args()['ranges'], 'ranges').map(r => CellRange.parse(text(r, 'a range'))), changeOf(args()['change']));
            case 'insertRows':
                return SheetEdit.insertRows(...placeAndCount(argument));
            case 'deleteRows':
                return SheetEdit.deleteRows(...placeAndCount(argument));
            case 'insertColumns':
                return SheetEdit.insertColumns(...placeAndCount(argument));
            case 'deleteColumns':
                return SheetEdit.deleteColumns(...placeAndCount(argument));
            case 'setColumnWidth': {
                const width = args()['width'];
                return SheetEdit.setColumnWidth(CellRange.parse(text(args()['columns'], 'columns')), width === undefined || width === null ? undefined : caseNumber(width, 'width'));
            }
            case 'setAutomaticColumnWidth':
                return SheetEdit.setAutomaticColumnWidth(CellRange.parse(text(args()['columns'], 'columns')), caseNumber(args()['width'], 'width'));
            case 'fill':
            case 'fillCopy': {
                const source = CellRange.parse(text(args()['source'], 'source'));
                const target = CellRange.parse(text(args()['target'], 'target'));
                const direction = text(args()['direction'], 'direction') as FillDirection;
                return name === 'fill' ? SheetEdit.fill(source, target, direction) : SheetEdit.fillCopy(source, target, direction);
            }
            case 'paste': {
                const copy = this.#copies.get(text(args()['copy'], 'copy'));
                const block = copy?.block;
                if (block === undefined) throw new CaseError('The copy pasted was refused, or never taken.');
                const at = args()['at'];
                if (Array.isArray(at)) return SheetEdit.paste(block, list(at, 'at').map(t => CellRange.parse(text(t, 'a target'))));
                const target = text(at, 'at');
                return SheetEdit.paste(block, target.includes(':') ? CellRange.parse(target) : CellAddress.parse(target));
            }
            case 'pasteText':
                return SheetEdit.pasteText(list(args()['fields'], 'fields').map(r => list(r, 'a row').map(f => text(f, 'a field'))),
                    CellAddress.parse(text(args()['at'], 'at')));
            case 'enterInto':
                return SheetEdit.enterInto(list(args()['targets'], 'targets').map(t => CellRange.parse(text(t, 'a target'))),
                    CellAddress.parse(text(args()['at'], 'at')), text(args()['typed'], 'typed'));
            case 'rename':
                return SheetEdit.rename(text(argument, 'rename'));
            case 'insertSheet': {
                const sheetName = args()['name'];
                return SheetEdit.insertSheet(sheetName === undefined || sheetName === null ? undefined : text(sheetName, 'name'), args()['before'] === true);
            }
            case 'deleteSheet':
                return SheetEdit.deleteSheet();
            case 'moveSheet':
                return SheetEdit.moveSheet(integer(argument, 'moveSheet'));
            case 'copySheet':
                return SheetEdit.copySheet(typeof argument === 'number' ? argument : undefined);
            default:
                throw new CaseError(`'${name}' is no Sheet Edit.`);
        }
    }

    /** Everything the Workbook records and shows: its document, and every held cell's Value on every Sheet. */
    #picture(): string {
        const values = this.workbook.sheets.flatMap(s => s.entryAddresses.map(a => `${s.name}!${a.toString()}=${valueText(s.getValue(a))}`));
        return `${this.workbook.toDocument().toJson()}\n${values.join('\n')}`;
    }

    #read(where: string, key: string, expected: Json, outcome: Outcome): void {
        const sheet = outcome.sheet;
        const perCell = (read: (address: CellAddress) => Json): JsonObject =>
            Object.fromEntries(Object.keys(object(expected, key)).map(a => [a, read(CellAddress.parse(a))]));
        let actual: Json;
        switch (key) {
            case 'values':
                actual = perCell(a => encode(sheet.getValue(a)));
                break;
            case 'displays':
                actual = Object.fromEntries(Object.entries(object(expected, key)).map(([address, wanted]) => {
                    const width = isObject(wanted) ? wanted['width'] : undefined;
                    const at = CellAddress.parse(address);
                    const display = width === undefined || width === null ? sheet.getDisplay(at) : sheet.getDisplay(at, caseNumber(width, 'width'));
                    return [address, {
                        width: orNull(width),
                        text: display.text,
                        cannotShow: display.cannotShow,
                        alignment: display.alignment,
                        colour: orNull(display.colour),
                        isNumber: display.isNumber,
                    }];
                }));
                break;
            case 'texts':
                actual = perCell(a => sheet.getDisplay(a).text);
                break;
            case 'fonts':
                actual = perCell(a => font(sheet.getFont(a)));
                break;
            case 'fills':
                actual = perCell(a => fill(sheet.getFill(a)));
                break;
            case 'borders':
                actual = perCell(a => borders(sheet.getBorders(a)));
                break;
            case 'document':
                actual = documentJson(this.workbook.toSheetDocument(sheet.name));
                break;
            case 'formulas':
                actual = perCell(a => orNull(sheet.getEntry(a)?.formula));
                break;
            case 'entryTexts':
                actual = perCell(a => sheet.getEntryText(a));
                break;
            case 'entries':
                actual = perCell(a => entryJson(sheet.getEntry(a)));
                break;
            case 'numberFormats':
                actual = perCell(a => sheet.getNumberFormat(a).code);
                break;
            case 'alignments':
                actual = perCell(a => sheet.getAlignment(a));
                break;
            case 'widthOnEntry':
                actual = perCell(a => orNull(sheet.getWidthOnEntry(a)));
                break;
            case 'spilledFrom':
                actual = perCell(a => orNull(sheet.spilledFrom(a)?.toString()));
                break;
            case 'columnWidths':
                actual = Object.fromEntries(Object.keys(object(expected, key)).map(column => {
                    const index = CellAddress.tryParseColumn(column);
                    if (index === undefined) throw new CaseError(`'${column}' is no column.`);
                    const width = sheet.getColumnWidth(index);
                    return [column, width === undefined ? null : { width: width.width, kind: width.kind }];
                }));
                break;
            case 'change': {
                const change = outcome.change;
                actual = change === undefined ? null : {
                    valueChanges: addresses(change.valueChanges),
                    recalculated: addresses(change.recalculated),
                    recalculatedCount: change.recalculated.length,
                    rows: [...change.rows],
                    columns: [...change.columns],
                    reformatsUnnamedRows: change.reformatsUnnamedRows,
                };
                break;
            }
            case 'refusal':
                if (!outcome.checked) throw new CaseError(`${where}: only a check reads a refusal.`);
                actual = outcome.refusal === undefined ? null : { reason: outcome.refusal.reason, saying: outcome.refusal.message };
                break;
            case 'isUndone':
                if (outcome.step === undefined) throw new CaseError(`${where}: no step to read.`);
                actual = outcome.step.isUndone;
                break;
            case 'ranges':
                if (outcome.step === undefined) throw new CaseError(`${where}: no step to read.`);
                actual = outcome.step.ranges.map(r => r.toString());
                break;
            case 'picture':
                actual = this.#pictures.get(text(expected, key)) === this.#picture() ? expected : 'a different picture';
                break;
            case 'pictureNot': {
                const remembered = this.#pictures.get(text(expected, key));
                actual = remembered !== undefined && remembered !== this.#picture() ? expected : 'the same picture';
                break;
            }
            case 'entryAddresses':
                actual = addresses(sheet.entryAddresses);
                break;
            case 'valueAddresses':
                actual = addresses(sheet.valueAddresses);
                break;
            case 'sheets':
                actual = this.workbook.sheets.map(s => s.name);
                break;
            case 'shown':
                actual = this.workbook.shown.name;
                break;
            default:
                throw new CaseError(`${where}: '${key}' is no reading.`);
        }
        compare(`${where}.${key}`, expected, actual, this.differences);
    }
}

/** A Value as C#'s Value.ToString writes it, for a picture. */
function valueText(value: Value | undefined): string {
    return value === undefined ? '' : value.toString();
}

function entryOf(node: Json | undefined): Entry | undefined {
    if (node === null || node === undefined) return undefined;
    const entry = object(node, 'an Entry');
    if (typeof entry['formula'] === 'string') return Entry.fromFormula(entry['formula']);
    if ('constant' in entry) {
        const constant = decode(entry['constant']);
        if (constant === undefined) throw new CaseError('A constant is not blank.');
        return Entry.fromValue(constant);
    }
    throw new CaseError(`${JSON.stringify(node)} is no Entry.`);
}

/** A row or column and a count, as `[2]` or `[2, 3]`; the count left out is one. */
function placeAndCount(argument: Json): [number, number] {
    const pair = list(argument, 'a place and count');
    if (pair.length < 1 || pair.length > 2) throw new CaseError(`${JSON.stringify(argument)} is no place and count.`);
    return [integer(pair[0], 'a place'), pair.length === 2 ? integer(pair[1], 'a count') : 1];
}

/** Runs the case, and answers every way the engine differs from what it expects; empty when it agrees. */
export function runSheetCase(file: JsonObject, entry: JsonObject): string[] {
    const culture = entry['culture'] ?? file['culture'];
    if (culture === undefined) throw new CaseError('Neither the case nor its file names a culture.');
    const run = new Run(cultureOf(text(culture, 'culture')));
    const sheets = entry['sheets'];
    if (sheets !== undefined) run.workbook = new Workbook(run.culture, list(sheets, 'sheets').map(s => text(s, 'a Sheet name')));
    const setup = entry['setup'];
    if (setup !== undefined) {
        const setups = file['setups'];
        const named = isObject(setups) ? setups[text(setup, 'setup')] : undefined;
        if (named === undefined) throw new CaseError(`The file names no setup '${JSON.stringify(setup)}'.`);
        list(named, 'a setup').forEach((step, i) => {
            run.take(object(step, 'a step'), `setup step ${String(i + 1)}`);
        });
    }
    const steps = list(entry['steps'], 'steps');
    if (steps.length === 0) throw new CaseError('The case takes no steps.');
    steps.forEach((step, i) => {
        run.take(object(step, 'a step'), `step ${String(i + 1)}`);
    });
    return run.differences;
}
