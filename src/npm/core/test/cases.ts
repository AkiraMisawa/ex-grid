// Reads the shared cases of tests/cases/ (ADR-0350, section 5), which xUnit runs against the C# core as
// well. tests/cases/README.md says what a file holds.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { expect } from 'vitest';
import { cell, type CellPosition } from '../src/selection/cell';
import { fromCorners, selectionRange, type SelectionRange } from '../src/selection/range';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../tests/cases');

/** One file of cases. */
export interface CaseFile<T> {
    readonly file: string;
    readonly about: string;
    readonly kind: string;
    readonly extent?: readonly [number, number];
    readonly cases: readonly (T & { readonly name: string })[];
}

/** Every file of the area's directory whose kind is the one asked for, by name. */
export function caseFiles<T>(area: string, kind: string): CaseFile<T>[] {
    const dir = path.join(root, area);
    return fs.readdirSync(dir)
        .filter(name => name.endsWith('.json'))
        .sort()
        .map(name => ({ file: name, ...JSON.parse(fs.readFileSync(path.join(dir, name), 'utf8')) }) as CaseFile<T>)
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
    for (const letter of match[1]!) {
        column = column * 26 + (letter.charCodeAt(0) - 64);
    }
    return cell(Number(match[2]) - 1, column - 1);
}

export function readRange(text: RangeText): SelectionRange {
    if (typeof text !== 'string') {
        return selectionRange(text[0], text[1], text[2], text[3]);
    }
    const [first, last] = text.split(':');
    return fromCorners(readCell(first!), readCell(last ?? first!));
}

/** A list of ranges, as an array of ranges or as Excel's Selection.Address. */
export function readRanges(text: readonly RangeText[] | string): SelectionRange[] {
    if (typeof text === 'string') {
        return text === '' ? [] : text.split(',').map(readRange);
    }
    return text.map(readRange);
}

/** What a refusal is named in the cases: an argument outside what the grid has, or anything else. */
export type Refusal = 'outOfRange' | 'invalidOperation';

/** Runs the action and holds it to the refusal named. */
export function expectRefusal(action: () => unknown, refusal: Refusal): void {
    let thrown: unknown;
    try {
        action();
    } catch (error) {
        thrown = error;
    }
    expect(thrown, `expected a refusal (${refusal})`).toBeInstanceOf(Error);
    if (refusal === 'outOfRange') {
        expect(thrown).toBeInstanceOf(RangeError);
    } else {
        expect(thrown).not.toBeInstanceOf(RangeError);
    }
}
