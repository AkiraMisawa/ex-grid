// Reads the shared cases of tests/cases/ (ADR-0350, section 5), which xUnit runs against the C# core as
// well. tests/cases/README.md says what a file holds.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { expect } from 'vitest';
import { cell, type CellPosition } from '../src/selection/cell';
import { fromCorners, selectionRange, type SelectionRange } from '../src/selection/range';
import { defined, unreachable } from '../src/must';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../tests/cases');

/** One file of cases. */
export interface CaseFile<T> {
    readonly file: string;
    readonly about: string;
    readonly kind: string;
    readonly extent?: readonly [number, number];
    readonly cases: readonly (T & { readonly name: string })[];
}

/** Every area of the cases: the directories under tests/cases/, by name. */
export function areas(): string[] {
    return fs.readdirSync(root, { withFileTypes: true })
        .filter(entry => entry.isDirectory())
        .map(entry => entry.name)
        .toSorted();
}

/** Every file of the area's directory whose kind is the one asked for, by name. */
export function caseFiles<T>(area: string, kind: string): CaseFile<T>[] {
    const dir = path.join(root, area);
    return fs.readdirSync(dir)
        .filter(name => name.endsWith('.json'))
        .toSorted()
        .map(name => ({ ...(JSON.parse(fs.readFileSync(path.join(dir, name), 'utf8')) as Omit<CaseFile<T>, 'file'>), file: name }))
        .filter(file => file.kind === kind);
}

/** A cell, as [row, column] or in A1 notation. */
export type CellText = readonly [number, number] | string;

/** A range, as [top, left, rows, columns] or in A1 notation. */
export type RangeText = readonly [number, number, number, number] | string;

export function readCell(text: CellText): CellPosition {
    if (typeof text !== 'string') {
        return cell(text[0], text[1]);
    }
    const match = /^([A-Z]+)(\d+)$/.exec(text);
    if (match === null) {
        throw new Error(`"${text}" is not a cell in A1 notation.`);
    }
    let column = 0;
    const [, letters, digits] = match;
    for (const letter of defined(letters, 'the column letters')) {
        column = column * 26 + (letter.charCodeAt(0) - 64);
    }
    return cell(Number(defined(digits, 'the row digits')) - 1, column - 1);
}

export function readRange(text: RangeText): SelectionRange {
    if (typeof text !== 'string') {
        return selectionRange(text[0], text[1], text[2], text[3]);
    }
    const [first, last, more] = text.split(':');
    if (first === undefined || more !== undefined) {
        throw new Error(`"${text}" is not a range in A1 notation.`);
    }
    return fromCorners(readCell(first), readCell(last ?? first));
}

/** A list of ranges, as an array of ranges or as Excel's Selection.Address. */
export function readRanges(text: readonly RangeText[] | string): SelectionRange[] {
    if (typeof text === 'string') {
        return text === '' ? [] : text.split(',').map(readRange);
    }
    return text.map(readRange);
}

/** What a refusal is named in the cases: an argument outside what the grid has, an argument of a kind the
 * member does not take, or anything else refused. */
/** Runs of Headings, rows or columns, as a case writes them: `[start, count]` each. */
export const runPairs = (runs: readonly { readonly start: number; readonly count: number }[]): readonly (readonly [number, number])[] =>
    runs.map(run => [run.start, run.count] as const);

export type Refusal = 'outOfRange' | 'argument' | 'invalidOperation';

/** Runs the action and holds it to the refusal named, and, with `saying`, to a message holding that
 * text, as the C# core's does. */
export function expectRefusal(action: () => unknown, refusal: Refusal, saying?: string): void {
    // Run once: an action may change what a second run would meet.
    let thrown: unknown;
    try {
        action();
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the refusal the case expects is held to it below
        thrown = error;
    }
    expectRefused(thrown, refusal, saying);
}

/** Holds what an action threw, or `undefined` when it threw nothing, to the refusal named. */
export function expectRefused(thrown: unknown, refusal: Refusal, saying?: string): void {
    expect(thrown, `expected a refusal (${refusal})`).toBeInstanceOf(Error);
    switch (refusal) {
        case 'outOfRange':
            expect(thrown, 'expected a refusal (outOfRange)').toBeInstanceOf(RangeError);
            break;
        case 'argument':
            expect(thrown, 'expected a refusal (argument)').toBeInstanceOf(TypeError);
            break;
        case 'invalidOperation':
            expect(thrown, 'expected a refusal (invalidOperation), not outOfRange').not.toBeInstanceOf(RangeError);
            expect(thrown, 'expected a refusal (invalidOperation), not argument').not.toBeInstanceOf(TypeError);
            break;
        default:
            unreachable(refusal);
    }
    if (saying !== undefined) {
        expect(thrown instanceof Error ? thrown.message : '', 'the refusal says').toContain(saying);
    }
}

/** The whole number a step names under `key`. A key that is missing or holds anything else is a defect in
 * the case, never read as 0. */
export function wholeNumber(step: Readonly<Record<string, unknown>>, key: string): number {
    const value = step[key];
    if (typeof value !== 'number' || !Number.isInteger(value)) {
        throw new Error(`The step ${JSON.stringify(step)} needs "${key}" as a whole number.`);
    }
    return value;
}
