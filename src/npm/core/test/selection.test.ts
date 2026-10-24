// Layer 1: the selection's shared cases (tests/cases/selection), run against the Script Core.
import { describe, expect, it } from 'vitest';
import type { GridDirection, GridExtent } from '../src/selection/cell';
import { edgeBandRowsPerTick } from '../src/selection/edge-band';
import { HeldSelection } from '../src/selection/held';
import { fromCorners, rangeCellCount, rangeContains, selectionRange, subtractArea, subtractCell } from '../src/selection/range';
import { Selection } from '../src/selection/selection';
import {
    caseFiles, expectRefusal, readCell, readRange, readRanges, type CellText, type RangeText, type Refusal,
} from './cases';

type Step = Record<string, unknown>;

interface SelectionCase {
    readonly extent?: readonly [number, number];
    readonly steps: readonly Step[];
}

const cycleKeys = {
    'enter': ['columnMajor', false],
    'shift+enter': ['columnMajor', true],
    'tab': ['rowMajor', false],
    'shift+tab': ['rowMajor', true],
} as const;

/** The gesture a step names, applied to the selection. */
function gesture(selection: Selection, step: Step, extent: GridExtent): Selection {
    const at = (key: string) => readCell(step[key] as CellText);
    const answer = step['answer'] === undefined
        ? undefined
        : (() => readCell(step['answer'] as CellText));
    const focusRow = (step['focusRow'] as number | undefined) ?? 0;
    const focusColumn = (step['focusColumn'] as number | undefined) ?? 0;
    if ('click' in step) return selection.click(at('click'), extent);
    if ('extendTo' in step) return selection.extendTo(at('extendTo'), extent);
    if ('toggleRange' in step) return selection.toggleRange(at('toggleRange'), extent);
    if ('toggleColumn' in step) return selection.toggleColumn(step['toggleColumn'] as number, extent, focusRow);
    if ('toggleRow' in step) return selection.toggleRow(step['toggleRow'] as number, extent, focusColumn);
    if ('move' in step) return selection.move(step['move'] as GridDirection, extent);
    if ('extend' in step) return selection.extend(step['extend'] as GridDirection, extent);
    if ('moveToEdge' in step) return selection.moveToEdge(step['moveToEdge'] as GridDirection, extent, answer);
    if ('extendToEdge' in step) return selection.extendToEdge(step['extendToEdge'] as GridDirection, extent, answer);
    if ('moveByViewport' in step) return selection.moveByViewport(step['moveByViewport'] as number, extent);
    if ('extendByViewport' in step) return selection.extendByViewport(step['extendByViewport'] as number, extent);
    if ('selectAll' in step) {
        const page = step['selectAll'];
        return page === true
            ? selection.selectAll(extent)
            : selection.selectAll(extent, page as { firstRow: number; rowCount: number });
    }
    if ('selectWholeColumns' in step) return selection.selectWholeColumns(extent);
    if ('selectWholeRows' in step) return selection.selectWholeRows(extent);
    if ('collapseToFocus' in step) return selection.collapseToFocus(extent);
    if ('focusOn' in step) return selection.focusOn(at('focusOn'));
    if ('extendToColumn' in step) {
        return selection.extendToColumn(step['extendToColumn'] as number, extent, (step['focusRowIfEmpty'] as number | undefined) ?? 0);
    }
    if ('extendToRow' in step) {
        return selection.extendToRow(step['extendToRow'] as number, extent, (step['focusColumnIfEmpty'] as number | undefined) ?? 0);
    }
    if ('place' in step) return selection.place(readRange(step['place'] as RangeText), at('focus'), extent);
    if ('selectColumn' in step) return selection.selectColumn(step['selectColumn'] as number, extent, focusRow);
    if ('selectRow' in step) return selection.selectRow(step['selectRow'] as number, extent, focusColumn);
    throw new Error(`The step ${JSON.stringify(step)} names no gesture this runner knows.`);
}

/** A key that is pressed `times` times, or once for each Focus in `focuses`, which it is held to. */
function presses(selection: Selection, step: Step, extent: GridExtent, press: (s: Selection) => Selection): Selection {
    const focuses = step['focuses'] as CellText[] | undefined;
    const times = (step['times'] as number | undefined) ?? focuses?.length ?? 1;
    for (let i = 0; i < times; i++) {
        selection = press(selection);
        const expected = focuses?.[i];
        if (expected !== undefined) {
            expect(selection.focus, `the Focus after press ${i + 1}`).toEqual(readCell(expected));
        }
    }
    return selection;
}

function apply(selection: Selection, step: Step, extent: GridExtent): Selection {
    if ('cycle' in step) {
        const [order, backward] = cycleKeys[step['cycle'] as keyof typeof cycleKeys];
        return presses(selection, step, extent, s => s.cycleFocus(order, backward, extent));
    }
    if ('nextCorner' in step) {
        return presses(selection, step, extent, s => s.moveFocusToNextCorner(extent));
    }
    return gesture(selection, step, extent);
}

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
            case 'topLeftOfRangeMadeLast': expect(selection.topLeftOfRangeMadeLast, key).toEqual(nullable(value, readCell)); break;
            case 'sameAs': expect(selection.equals(saved.get(value as string)!), `equal to ${String(value)}`).toBe(true); break;
            case 'differsFrom': expect(selection.equals(saved.get(value as string)!), `equal to ${String(value)}`).toBe(false); break;
            case 'unchanged': expect(selection.equals(before), 'unchanged').toBe(value); break;
            default: throw new Error(`The expectation "${key}" is not one this runner knows.`);
        }
    }
}

const extentOf = (pair: readonly [number, number]): GridExtent => ({ rowCount: pair[0], columnCount: pair[1] });

for (const file of caseFiles<SelectionCase>('selection', 'selection')) {
    describe(`selection/${file.file}`, () => {
        for (const c of file.cases) {
            it(c.name, () => {
                const extent = extentOf(c.extent ?? file.extent!);
                const saved = new Map<string, Selection>();
                let selection = Selection.empty;
                let before = selection;
                for (const step of c.steps) {
                    if ('expect' in step) {
                        holds(selection, step['expect'] as Record<string, unknown>, before, saved, extent);
                    } else if ('save' in step) {
                        saved.set(step['save'] as string, selection);
                    } else if ('restore' in step) {
                        selection = saved.get(step['restore'] as string)!;
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
                const extent = extentOf(file.extent!);
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
                        if (expected.version !== undefined) expect(held.rowSequenceVersion).toBe(expected.version);
                        if (expected.empty !== undefined) expect(held.selection.isEmpty).toBe(expected.empty);
                        if (expected.sameAsBefore !== undefined) expect(held === before).toBe(expected.sameAsBefore);
                    }
                }
            });
        }
    });
}
