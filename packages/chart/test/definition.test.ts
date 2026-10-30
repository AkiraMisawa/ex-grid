// Layer 1: what the shared cases cannot say, because C# has no counterpart. A series is handed back as options,
// changed, as C#'s `with` copies a record (ADR-0257's slices are made so): its points stand.
import { describe, expect, it } from 'vitest';
import { ChartDefinition } from '../src/definition';

describe('a series handed back as options', () => {
    it('ADR-0251: keeps its points, though its bars, empty, come with it', () => {
        const first = new ChartDefinition({
            axis: { values: 'numbers' },
            series: [{ id: 'a', name: 'A', kind: 'level', points: [{ x: 0, y: 1 }, { x: 1, y: 3 }] }],
        });
        const [series] = first.series;
        if (series === undefined) throw new Error('The definition holds no series.');
        const renamed = new ChartDefinition({ axis: first.axis, series: [{ ...series, name: 'B', keys: { colour: 'B' } }] });
        expect(renamed.series[0]?.points).toEqual([{ x: 0, y: 1 }, { x: 1, y: 3 }]);
        expect(renamed.series[0]?.name).toBe('B');
    });
});
