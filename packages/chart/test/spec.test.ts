// What the Chart Spec reader does in TypeScript alone, where JavaScript's values differ from .NET's: a decimal,
// which a chart value has no type for, and a table that answers `undefined`. Everything the two readers share
// is in tests/cases/chart/spec*.json (ADR-0370, sections 2 and 3), run by cases.test.ts.
import { decimalFromText } from '@ex-grid/data';
import { describe, expect, it } from 'vitest';
import { chartDataOf, type ChartData } from '../src/chart-data';
import { ChartSpecRefusedError, readChartSpec } from '../src/spec';

const spec = JSON.stringify({
    version: 1, axis: { values: 'numbers' },
    series: [{ id: 'd', name: 'D', kind: 'level', data: 'd', x: 'x', y: 'y' }],
});

function read(y: unknown): ReturnType<typeof readChartSpec> {
    const table = chartDataOf([['x', [1]], ['y', [y]]]);
    return readChartSpec(spec, handle => (handle === 'd' ? table : undefined));
}

describe('a decimal read from a column', () => {
    it('ADR-0251: a whole decimal is held exactly, as a bigint', () => {
        expect(read(decimalFromText('79228162514264337593543950335')).definition.series[0]?.points[0]?.y).toBe(79228162514264337593543950335n);
    });

    it('ADR-0251: a decimal with places is the number that writes its digits', () => {
        expect(read(decimalFromText('-1234.5678')).definition.series[0]?.points[0]?.y).toBe(-1234.5678);
        expect(read({ mantissa: 150n, scale: 2 }).definition.series[0]?.points[0]?.y).toBe(1.5);
    });

    it('ADR-0251: a decimal no number writes digit for digit is refused, never rounded', () => {
        expect(() => read(decimalFromText('0.1234567890123456789'))).toThrow(ChartSpecRefusedError);
        expect(() => read(decimalFromText('0.1234567890123456789'))).toThrow('series[0].y: row 1 of "d" holds 0.1234567890123456789, which a chart value cannot hold digit for digit; a decimal is drawn with every digit it holds, or not at all.');
    });
});

describe('a table', () => {
    it('ADR-0320: a table that answers undefined has broken its contract, where a missing value is null', () => {
        const table: ChartData = { columns: ['x', 'y'], rowCount: 1, value: (column: string) => (column === 'x' ? 1 : undefined) };
        expect(() => readChartSpec(spec, () => table)).toThrow(TypeError);
    });

    it('ADR-0320: a reference table refuses columns of different lengths and two columns of one name', () => {
        expect(() => chartDataOf([['a', [1]], ['b', [1, 2]]])).toThrow(TypeError);
        expect(() => chartDataOf([['a', [1]], ['a', [2]]])).toThrow(TypeError);
    });

    it('ADR-0320: a reference table refuses a column it does not have and a row past its last', () => {
        const table = chartDataOf([['a', [1]]]);
        expect(table.value('a', 0)).toBe(1);
        expect(() => table.value('b', 0)).toThrow(RangeError);
        expect(() => table.value('a', 1)).toThrow(RangeError);
    });
});

describe('the JSON', () => {
    it('ADR-0320: a member written twice is refused as text that is not JSON, which System.Text.Json reads as its last', () => {
        const twice = '{ "version": 1, "version": 1, "axis": { "values": "numbers" }, "series": [] }';
        expect(() => readChartSpec(twice, () => undefined)).toThrow(new ChartSpecRefusedError('', 'The Chart Spec is not JSON: The member \'version\' is written twice in one object.'));
    });
});
