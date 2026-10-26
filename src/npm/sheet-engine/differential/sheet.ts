// The Script Core's side of a differential run: the Sheet a request describes, built and read as
// tools/sheet-oracle builds and reads it, answered in the oracle's own shape.

import { appendFileSync } from 'node:fs';
import { CellAddress } from '../src/cell-address';
import { CellRange } from '../src/cell-range';
import { Culture } from '../src/culture';
import { FormulaSyntaxError } from '../src/formula-syntax-error';
import { NumberFormat } from '../src/number-format';
import { SheetRefusedError } from '../src/sheet-change';
import { SheetEdit } from '../src/sheet-edit';
import type { Value } from '../src/value';
import { Workbook } from '../src/workbook';
import { comparable } from './oracle';

/** A Sheet to build and read: its culture, the texts typed into it in order, the format codes set on ranges, the cells to read. */
export interface SheetRequest {
    readonly culture: string;
    readonly entries: readonly (readonly [string, string])[];
    readonly formats?: readonly (readonly [string, string])[];
    readonly read: readonly string[];
    /** A column width in characters to read each display at; none reads it at no particular width. */
    readonly width?: number;
}

/** A number's 64 bits, as the oracle writes them: sixteen hexadecimal digits. */
export function bits(value: number): string {
    const view = new DataView(new ArrayBuffer(8));
    view.setFloat64(0, value);
    return view.getBigUint64(0).toString(16).padStart(16, '0');
}

function encode(value: Value | undefined): unknown {
    if (value === undefined) return null;
    switch (value.kind) {
        case 'number': return { number: bits(value.number), shown: String(value.number) };
        case 'text': return { text: value.text };
        case 'boolean': return { boolean: value.boolean };
        case 'error': return { error: value.error, refused: value.isRefused };
        default: throw new Error(`${String(value.kind)} is no kind of Value.`);
    }
}

/** What the Script Core answers for `request`, field for field as the oracle does. */
export function answer(request: SheetRequest): unknown {
    const workbook = new Workbook(request.culture === 'invariant' ? Culture.invariant : Culture.of(request.culture));
    const sheet = workbook.shown;
    const refusals: Record<string, unknown> = {};
    for (const [address, typed] of request.entries) {
        try {
            workbook.do(sheet.name, SheetEdit.enter(CellAddress.parse(address), typed));
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a refused entry is an answer the oracle gives too
            if (error instanceof FormulaSyntaxError) refusals[address] = { refusal: 'formulaSyntax', reason: error.reason };
            else if (error instanceof SheetRefusedError) refusals[address] = { refusal: 'refused', reason: error.refusal.reason };
            else throw error;
        }
    }
    // A format code the engine does not read is answered by its reason under the code, and a range the Sheet refuses
    // to format by its refusal under the range, as the oracle answers them.
    const formatRefusals: Record<string, unknown> = {};
    for (const [range, code] of request.formats === undefined ? [] : request.formats) {
        const reading = NumberFormat.tryParse(code);
        if (reading.format === undefined) {
            formatRefusals[code] = reading.reason;
            continue;
        }
        try {
            workbook.do(sheet.name, SheetEdit.setNumberFormat(CellRange.parse(range), reading.format));
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a refused range is an answer the oracle gives too
            if (error instanceof SheetRefusedError) formatRefusals[range] = { refusal: 'refused', reason: error.refusal.reason };
            else throw error;
        }
    }
    const cells: Record<string, unknown> = {};
    for (const read of request.read) {
        const at = CellAddress.parse(read);
        const display = sheet.getDisplay(at, request.width);
        cells[read] = { value: encode(sheet.getValue(at)), text: display.text, cannotShow: display.cannotShow };
    }
    return { cells, refusals, formatRefusals };
}

function field(from: unknown, ...path: string[]): unknown {
    let at: unknown = from;
    for (const key of path) at = typeof at === 'object' && at !== null ? Reflect.get(at, key) : undefined;
    return at;
}

/** The keys of an answer's part; a part neither side holds has none. */
function keysOf(part: unknown): string[] {
    return typeof part === 'object' && part !== null ? Object.keys(part) : [];
}

/**
 * Where the Script Core parts from the oracle on each request, a line for each cell, refused entry or format
 * code that differs, naming what was typed there and what each side answered; or the whole answer, when the
 * oracle threw.
 */
export function differences(requests: readonly SheetRequest[], answers: readonly unknown[]): string[] {
    const found: string[] = [];
    answers.forEach((theirs, i) => {
        const asked = requests[i];
        if (asked === undefined) throw new Error('Internal: an answer without its request.');
        const where = `${asked.culture}${asked.width === undefined ? '' : ` width ${String(asked.width)}`}`;
        let ours: unknown;
        try {
            ours = answer(asked);
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- an engine that throws is a difference the run lists with the rest
            found.push(`${where}: ours threw ${String(error)}, C# ${JSON.stringify(field(theirs, 'error') === undefined ? 'answered' : theirs)}, on ${JSON.stringify(asked)}`);
            return;
        }
        if (comparable(theirs) === comparable(ours)) return;
        if (field(theirs, 'error') !== undefined) {
            found.push(`${where}: C# threw ${JSON.stringify(theirs)} on ${JSON.stringify(asked)}`);
            return;
        }
        const typed = new Map(asked.entries);
        for (const part of ['refusals', 'formatRefusals', 'cells']) {
            const keys = new Set([...keysOf(field(theirs, part)), ...keysOf(field(ours, part))]);
            for (const key of keys) {
                const [a, b] = [field(theirs, part, key), field(ours, part, key)];
                if (comparable(a) === comparable(b)) continue;
                const format = asked.formats?.find(([range]) => range === key)?.[1];
                found.push(`${where} ${key} ${typed.has(key) ? JSON.stringify(typed.get(key)) : '(not typed)'}${format === undefined ? '' : ` as ${JSON.stringify(format)}`}: C# ${JSON.stringify(a)}, ours ${JSON.stringify(b)}`);
            }
        }
    });
    // Every difference, a line each, where EXGRID_SHEET_DIFF_OUT names a file: the test's own message shows the first few.
    const out = process.env['EXGRID_SHEET_DIFF_OUT'];
    if (out !== undefined && found.length > 0) appendFileSync(out, found.map((line) => `${line}\n`).join(''));
    return found;
}
