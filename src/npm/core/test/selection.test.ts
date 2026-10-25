// Layer 1: the selection's shared cases (tests/cases/selection), run against the Script Core.
import { describe, expect, it } from 'vitest';
import type { GridExtent } from '../src/selection/cell';
import { edgeBandRowsPerTick } from '../src/selection/edge-band';
import { HeldSelection } from '../src/selection/held';
import { markedColumns, markedRows, wholeColumnRuns, wholeRowRuns } from '../src/selection/heading-marks';
import { fromCorners, rangeCellCount, rangeContains, selectionRange, subtractArea, subtractCell } from '../src/selection/range';
import { Selection } from '../src/selection/selection';
import { defined } from '../src/must';
import {
    caseFiles, expectRefusal, readCell, readRange, readRanges, runPairs, type CellText, type RangeText, type Refusal,
} from './cases';
import { apply, type Step } from './gestures';

interface SelectionCase {
    readonly extent?: readonly [number, number];
    readonly steps: readonly Step[];
}

/** The saved selection a step names; a name never saved is a defect in the case. */
const savedAs = (saved: ReadonlyMap<string, Selection>, name: unknown): Selection =>
    defined(saved.get(name as string), `the selection saved as ${JSON.stringify(name)}`);

const nullable = <T>(value: unknown, read: (text: never) => T): T | undefined =>
    value === null ? undefined : read(value as never);

function holds(selection: Selection, expected: Record<string, unknown>, before: Selection, saved: Map<string, Selection>, extent: GridExtent): void {
    for (const [key, value] of Object.entries(expected)) {
        switch (key) {
            case 'empty': expect(selection.isEmpty, key).toBe(value); break;
            case 'ranges': expect(selection.ranges, key).toEqual(readRanges(value as string)); break;
            case 'rangeCount': expect(selection.ranges.length, key).toBe(value); break;
            case 'rangeAt': {
                const [index, range] = value as [number, RangeText];
                expect(selection.ranges[index], key).toEqual(readRange(range));
                break;
            }
            case 'includesRange': expect(selection.ranges, key).toContainEqual(readRange(value as RangeText)); break;
            case 'focus': expect(selection.focus, key).toEqual(nullable(value, readCell)); break;
            case 'extent': expect(selection.extent, key).toEqual(nullable(value, readCell)); break;
            case 'focusRange': expect(selection.focusRange, key).toEqual(nullable(value, readRange)); break;
            case 'cellCount': expect(selection.cellCount, key).toBe(value); break;
            case 'contains':
                for (const c of value as CellText[]) expect(selection.contains(readCell(c)), `contains ${JSON.stringify(c)}`).toBe(true);
                break;
            case 'notContains':
                for (const c of value as CellText[]) expect(selection.contains(readCell(c)), `contains ${JSON.stringify(c)}`).toBe(false);
                break;
            case 'coversColumn':
                for (const [column, covered] of value as [number, boolean][]) {
                    expect(selection.coversColumn(column, extent), `coversColumn ${column}`).toBe(covered);
                }
                break;
            case 'coversRow':
                for (const [row, covered] of value as [number, boolean][]) {
                    expect(selection.coversRow(row, extent), `coversRow ${row}`).toBe(covered);
                }
                break;
            case 'wholeColumns': expect(selection.wholeColumns(extent), key).toEqual(value); break;
            case 'headingMarks': holdsMarks(selection, value as Record<string, unknown>, extent); break;
            case 'topLeftOfRangeMadeLast': expect(selection.topLeftOfRangeMadeLast, key).toEqual(nullable(value, readCell)); break;
            case 'sameAs': expect(selection.equals(savedAs(saved, value)), `equal to ${String(value)}`).toBe(true); break;
            case 'differsFrom': expect(selection.equals(savedAs(saved, value)), `equal to ${String(value)}`).toBe(false); break;
            case 'unchanged': expect(selection.equals(before), 'unchanged').toBe(value); break;
            default: throw new Error(`The expectation "${key}" is not one this runner knows.`);
        }
    }
}

const extentOf = (pair: readonly [number, number]): GridExtent => ({ rowCount: pair[0], columnCount: pair[1] });


/** The Heading Mark's runs (ADR-0008): `columns` and `rows` it marks, and `wholeColumns` and `wholeRows` it
 * covers whole under the case's extent, or under `under` when the case names another. */
function holdsMarks(selection: Selection, expected: Record<string, unknown>, extent: GridExtent): void {
    const under = expected['under'] === undefined ? extent : extentOf(expected['under'] as [number, number]);
    for (const [key, value] of Object.entries(expected)) {
        switch (key) {
            case 'under': break;
            case 'columns': expect(runPairs(markedColumns(selection)), key).toEqual(value); break;
            case 'rows': expect(runPairs(markedRows(selection)), key).toEqual(value); break;
            case 'wholeColumns': expect(runPairs(wholeColumnRuns(selection, under)), key).toEqual(value); break;
            case 'wholeRows': expect(runPairs(wholeRowRuns(selection, under)), key).toEqual(value); break;
            default: throw new Error(`The Heading Mark expectation "${key}" is not one this runner knows.`);
        }
    }
}

for (const file of caseFiles<SelectionCase>('selection', 'selection')) {
    describe(`selection/${file.file}`, () => {
        for (const c of file.cases) {
            it(c.name, () => {
                expect.hasAssertions();
                const extent = extentOf(c.extent ?? defined(file.extent, `the extent of ${file.file}`));
                const saved = new Map<string, Selection>();
                let selection = Selection.empty;
                let before = selection;
                for (const step of c.steps) {
                    if ('expect' in step) {
                        holds(selection, step['expect'] as Record<string, unknown>, before, saved, extent);
                    } else if ('save' in step) {
                        saved.set(step['save'] as string, selection);
                    } else if ('restore' in step) {
                        selection = savedAs(saved, step['restore']);
                    } else {
                        const stepExtent = step['extent'] === undefined ? extent : extentOf(step['extent'] as [number, number]);
                        const refusal = step['throws'] as Refusal | undefined;
                        before = selection;
                        if (refusal === undefined) {
                            selection = apply(selection, step, stepExtent);
                        } else {
                            expectRefusal(() => apply(selection, step, stepExtent), refusal);
                        }
                    }
                }
            });
        }
    });
}

interface RangeCase {
    readonly fromCorners?: readonly [CellText, CellText];
    readonly create?: readonly [number, number, number, number];
    readonly contains?: { readonly range: RangeText; readonly cells: readonly (readonly [CellText, boolean])[] };
    readonly cellCount?: RangeText;
    readonly subtract?: { readonly range: RangeText; readonly cell: CellText };
    readonly subtractArea?: { readonly range: RangeText; readonly area: RangeText };
    readonly expect?: unknown;
    readonly throws?: Refusal;
}

for (const file of caseFiles<RangeCase>('selection', 'range')) {
    describe(`selection/${file.file}`, () => {
        for (const c of file.cases) {
            it(c.name, () => {
                expect.hasAssertions();
                if (c.fromCorners !== undefined) {
                    const [a, b] = c.fromCorners;
                    expect(fromCorners(readCell(a), readCell(b))).toEqual(readRange(c.expect as RangeText));
                } else if (c.create !== undefined) {
                    const [top, left, rows, columns] = c.create;
                    if (c.throws !== undefined) {
                        expectRefusal(() => selectionRange(top, left, rows, columns), c.throws);
                    } else {
                        expect(selectionRange(top, left, rows, columns)).toEqual(readRange(c.expect as RangeText));
                    }
                } else if (c.contains !== undefined) {
                    const range = readRange(c.contains.range);
                    for (const [position, inside] of c.contains.cells) {
                        expect(rangeContains(range, readCell(position)), JSON.stringify(position)).toBe(inside);
                    }
                } else if (c.cellCount !== undefined) {
                    expect(rangeCellCount(readRange(c.cellCount))).toBe(c.expect);
                } else if (c.subtract !== undefined) {
                    expect(subtractCell(readRange(c.subtract.range), readCell(c.subtract.cell)))
                        .toEqual(readRanges(c.expect as string));
                } else if (c.subtractArea !== undefined) {
                    expect(subtractArea(readRange(c.subtractArea.range), readRange(c.subtractArea.area)))
                        .toEqual(readRanges(c.expect as string));
                } else {
                    throw new Error(`The case "${c.name}" names nothing this runner knows.`);
                }
            });
        }
    });
}

interface EdgeBandCase {
    readonly positionPx: number;
    readonly lengthPx: number;
    readonly rowsPerViewport: number;
    readonly expect: number;
}

for (const file of caseFiles<EdgeBandCase>('selection', 'edgeBand')) {
    describe(`selection/${file.file}`, () => {
        for (const c of file.cases) {
            it(c.name, () => {
                expect.hasAssertions();
                expect(edgeBandRowsPerTick(c.positionPx, c.lengthPx, c.rowsPerViewport)).toBe(c.expect);
            });
        }
    });
}

interface HeldCase {
    readonly version: number;
    readonly steps: readonly Step[];
}

for (const file of caseFiles<HeldCase>('selection', 'held')) {
    describe(`selection/${file.file}`, () => {
        for (const c of file.cases) {
            it(c.name, () => {
                expect.hasAssertions();
                const extent = extentOf(defined(file.extent, `the extent of ${file.file}`));
                let held = HeldSelection.empty(c.version);
                let before = held;
                for (const step of c.steps) {
                    if ('click' in step) {
                        before = held;
                        held = held.with(held.selection.click(readCell(step['click'] as CellText), extent));
                    } else if ('under' in step) {
                        before = held;
                        held = held.under(step['under'] as number);
                    } else {
                        const expected = step['expect'] as { version?: number; empty?: boolean; sameAsBefore?: boolean };
                        for (const key of Object.keys(expected)) {
                            if (!['version', 'empty', 'sameAsBefore'].includes(key)) {
                                throw new Error(`A held case expects "${key}", which this runner does not know.`);
                            }
                        }
                        if (expected.version !== undefined) expect(held.rowSequenceVersion).toBe(expected.version);
                        if (expected.empty !== undefined) expect(held.selection.isEmpty).toBe(expected.empty);
                        if (expected.sameAsBefore !== undefined) expect(held === before).toBe(expected.sameAsBefore);
                    }
                }
            });
        }
    });
}
