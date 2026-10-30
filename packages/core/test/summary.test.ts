// Layer 1: the reference Selection Summary (tests/cases/summary, kind "summary"), run against the Script
// Core's `summaryOf`. A row is the values of its cells in the file's columns, written as a query case
// writes them; a column the request names and the file does not is one with no value. The C# runner is
// dotnet/tests/ExGrid.Tests/Cases/SummaryCaseTests.cs.
import { describe, expect, it } from 'vitest';
import { answeredSummary, declinedSummary, summaryFigureText, summaryOf } from '../src/summary/grid-summary';
import { SummaryAccumulator, type AggregateResult, type SummaryFigure } from '../src/summary/summary-accumulator';
import { caseFiles } from './cases';
import { value } from './query-values';
import { expectAnswers, summaryRequestOf, type WrittenAnswer, type WrittenAsk } from './summary-answers';

interface SummaryCase {
    readonly rows?: readonly (readonly unknown[])[];
    readonly columns?: readonly string[];
    readonly repeat?: number;
    readonly ask: WrittenAsk;
    readonly is: Readonly<Record<string, WrittenAnswer>>;
    readonly texts?: Readonly<Record<string, string>>;
}

for (const file of caseFiles<SummaryCase>('summary', 'summary')) {
    const { columns: fileColumns, rows: fileRows } = file as unknown as { readonly columns: readonly string[]; readonly rows: readonly (readonly unknown[])[] };
    describe(`summary/${file.file}`, () => {
        for (const c of file.cases) {
            it(c.name, () => {
                expect.hasAssertions();
                const columns = c.columns === undefined ? fileColumns : c.columns;
                const written = (c.rows === undefined ? fileRows : c.rows).map(row => row.map(value));
                const rows = c.repeat === undefined ? written : Array.from({ length: c.repeat }, () => written).flat();
                const result = summaryOf(rows, summaryRequestOf(c.ask, columns, 0), name => {
                    const at = columns.indexOf(name);
                    return at < 0 ? undefined : (row: readonly unknown[]) => row[at];
                });
                expectAnswers(result, c.is, c.texts);
            });
        }
    });
}

describe('summary: an answer is refused where it would say nothing true (ADR-0130)', () => {
    const one: AggregateResult = { kind: 'number', number: 1, exact: { mantissa: 1n, scale: 0 } };
    it('ADR-0130: a text for a figure that was not answered is refused by name', () => {
        expect(() => answeredSummary({ sum: one }, { average: '1' })).toThrow(/has a text and no answer/u);
    });
    it('ADR-0130: an answer names figures, never another key', () => {
        expect(() => answeredSummary({ ['total' as SummaryFigure]: one })).toThrow(/not a figure/u);
    });
    it('ADR-0130: a decline says why', () => {
        expect(() => declinedSummary(' ')).toThrow(TypeError);
        expect(declinedSummary('Too many rows to sum on the server.')).toEqual({ kind: 'declined', reason: 'Too many rows to sum on the server.' });
    });
    it('ADR-0130: a figure that is not a number has no text', () => {
        expect(summaryFigureText('sum', { kind: 'error', error: 'notANumber' })).toBeUndefined();
    });
    it('ADR-0130: a number that is not finite, or an exact value no decimal holds, is refused as an answer', () => {
        expect(() => answeredSummary({ sum: { kind: 'number', number: Number.NaN, exact: undefined } })).toThrow(RangeError);
        expect(() => answeredSummary({ sum: { kind: 'number', number: 1, exact: { mantissa: 1n << 96n, scale: 0 } } })).toThrow(RangeError);
        expect(() => answeredSummary({ sum: { kind: 'number', number: 1, exact: { mantissa: 1n, scale: 29 } } })).toThrow(RangeError);
    });
});

describe('summary: a figure\'s own text is C#\'s GridSummary.FigureText (ADR-0130)', () => {
    it('ADR-0130: a count answered as a double is written as the whole number C# writes', () => {
        expect(summaryFigureText('count', { kind: 'number', number: 3, exact: undefined })).toBe('3');
    });
    it('ADR-0130: an exact figure is written without trailing zeros, however the answerer wrote its scale', () => {
        expect(summaryFigureText('sum', { kind: 'number', number: 1.5, exact: { mantissa: 150n, scale: 2 } })).toBe('1.5');
    });
});

describe('summary: an error among the cells (ADR-0130, SM-7)', () => {
    it('ADR-0130 / SM-7: an error among the cells leaves Count and Numerical Count, as Excel\'s status bar (run 17, b5)', () => {
        const cells = new SummaryAccumulator();
        cells.add(1);
        cells.add(2);
        cells.addError();
        cells.add('x');
        cells.add(null);
        expect(cells.read('count')).toMatchObject({ kind: 'number', number: 4 });
        expect(cells.read('numericalCount')).toMatchObject({ kind: 'number', number: 2 });
        for (const figure of ['average', 'min', 'max', 'sum'] as const) {
            expect(cells.read(figure), figure).toEqual({ kind: 'error', error: 'errorAmongValues' });
        }
    });
});
