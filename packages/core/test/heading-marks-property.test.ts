// ADR-0008, D1: the Heading Mark's whole runs, found by sweeping the ranges' boundaries, are what a
// cell-by-cell union answers, over random mixes of Ctrl+clicks on Headings and on cells with drags from
// them. The Script Core's half of dotnet/tests/ExGrid.Tests/HeadingMarksPropertyTests.cs: the check a single case
// cannot state (the cases are tests/cases/selection/heading-marks.json). The mix of gestures is the C#
// half's, drawn by a generator and seed of its own, so a failing sample is replayed here, where it was
// found. The seed is fixed, and every failure names the sample and the step.
import { describe, expect, it } from 'vitest';
import { cell, type GridExtent } from '../src/selection/cell';
import { wholeColumnRuns, wholeRowRuns } from '../src/selection/heading-marks';
import { Selection } from '../src/selection/selection';
import { runPairs } from './cases';
import { Random } from './random';

const seed = 20261025;
const samples = 200;
const steps = 25;

/** The runs of positions in [0, count) that `covered` holds, walked one by one. */
function bruteRuns(count: number, covered: (position: number) => boolean): readonly (readonly [number, number])[] {
    const runs: [number, number][] = [];
    for (let position = 0; position < count; position++) {
        if (!covered(position)) continue;
        const last = runs.at(-1);
        if (last !== undefined && last[0] + last[1] === position) {
            last[1] += 1;
        } else {
            runs.push([position, 1]);
        }
    }
    return runs;
}

const indices = (count: number): readonly number[] => Array.from({ length: count }, (_, index) => index);

describe('Heading Mark whole runs against a cell-by-cell union', () => {
    it('ADR-0008, D1: the sweep answers what a cell-by-cell union does, after mixed gestures', () => {
        const random = new Random(seed);
        for (let sample = 0; sample < samples; sample++) {
            const extent: GridExtent = { rowCount: random.next(1, 9), columnCount: random.next(1, 9) };
            let selection = Selection.empty;
            for (let step = 0; step < steps; step++) {
                const row = random.below(extent.rowCount);
                const column = random.below(extent.columnCount);
                switch (step % 5) {
                    case 0: selection = selection.toggleColumn(column, extent, row); break;
                    case 1: selection = selection.toggleRow(row, extent, column); break;
                    default:
                        selection = selection.toggleRange(cell(row, column), extent)
                            .extendTo(cell(random.below(extent.rowCount), random.below(extent.columnCount)), extent);
                }
                const where = `sample ${sample.toString()}, step ${step.toString()}`;
                const current = selection;
                expect(runPairs(wholeColumnRuns(current, extent)), `${where}: whole columns`).toEqual(bruteRuns(extent.columnCount,
                    columnIndex => indices(extent.rowCount).every(rowIndex => current.contains(cell(rowIndex, columnIndex)))));
                expect(runPairs(wholeRowRuns(current, extent)), `${where}: whole rows`).toEqual(bruteRuns(extent.rowCount,
                    rowIndex => indices(extent.columnCount).every(columnIndex => current.contains(cell(rowIndex, columnIndex)))));
            }
        }
    });
});
