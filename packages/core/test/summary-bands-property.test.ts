// ADR-0130: the Selection Summary's bands, found by sweeping the ranges' edges, are what the definition answers
// read one band at a time, over random ranges that overlap, touch, repeat and reach past the rows and columns.
// The sweep exists for a Selection of many ranges, which a Consumer places since ADR-0050 item 4's note of
// 2026-11-12: walking every range for every band cost the square of their number. The Script Core's half of
// dotnet/tests/ExGrid.Tests/SummaryBandsPropertyTests.cs. The seed is fixed, and every failure names the sample.
import { describe, expect, it } from 'vitest';
import type { SelectionRange } from '../src/selection/range';
import { summaryBands, type SummaryBand } from '../src/summary/grid-summary';
import { Random } from './random';

const seed = 20261112;
const samples = 400;

/** The definition: a band between each two neighbouring edges, with the columns of every range that covers it. */
function bandsByDefinition(ranges: readonly SelectionRange[], rowCount: number, columnCount: number): readonly SummaryBand[] {
    const clamp = (value: number): number => Math.min(Math.max(value, 0), rowCount);
    const edges = new Set<number>();
    for (const range of ranges) {
        const top = clamp(range.topRow);
        const bottom = clamp(range.topRow + range.rowCount);
        if (top < bottom) {
            edges.add(top);
            edges.add(bottom);
        }
    }
    const sorted = [...edges].toSorted((a, b) => a - b);
    const bands: SummaryBand[] = [];
    sorted.slice(1).forEach((bottom, i) => {
        const top = sorted[i] as number;
        const columns = new Set<number>();
        for (const range of ranges) {
            if (range.topRow <= top && range.topRow + range.rowCount > top) {
                const right = Math.min(range.leftColumn + range.columnCount, columnCount);
                for (let column = Math.max(range.leftColumn, 0); column < right; column++) columns.add(column);
            }
        }
        if (columns.size > 0) bands.push({ top, bottom, columns: [...columns].toSorted((a, b) => a - b) });
    });
    return bands;
}

describe('the Selection Summary\'s bands against their definition', () => {
    it('ADR-0130: the sweep answers the bands the definition does, for ranges that overlap, touch, repeat and reach outside', () => {
        const random = new Random(seed);
        for (let sample = 0; sample < samples; sample++) {
            const rowCount = random.next(0, 13);
            const columnCount = random.next(0, 7);
            const ranges: SelectionRange[] = Array.from({ length: random.next(0, 9) }, () => ({
                // Past the rows and the columns on purpose: such cells are no cells, and are summarised nowhere.
                topRow: random.below(15), leftColumn: random.below(8), rowCount: random.next(1, 6), columnCount: random.next(1, 5),
            }));
            expect(summaryBands(ranges, rowCount, columnCount), `sample ${sample.toString()}: ${JSON.stringify({ ranges, rowCount, columnCount })}`)
                .toEqual(bandsByDefinition(ranges, rowCount, columnCount));
        }
    });

    it('ADR-0130: rows that do not stand together are a band each, and the rows between them are in none', () => {
        const ranges = [0, 2, 4, 7].map(topRow => ({ topRow, leftColumn: 0, rowCount: 1, columnCount: 3 }));
        expect(summaryBands(ranges, 100, 3)).toEqual([0, 2, 4, 7].map(top => ({ top, bottom: top + 1, columns: [0, 1, 2] })));
    });
});
