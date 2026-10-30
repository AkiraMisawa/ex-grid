// The figures ExPivot's Selection Summary reads from the report source's answer (ADR-0130), by the rules C#'s
// accumulator reads merged parts by: what overflows, what is not finite, and what the source refused.

import { describe, expect, it } from 'vitest';
import type { AggregateResult, GridSummaryRequest, GridSummaryResult, SummaryFigure } from '@ex-grid/core';
import type { PivotNumber, PivotReportSource, PivotReportSummaryResult } from '@ex-grid/pivot-engine';
import { summarizeReport } from '../src/report-summary';
import { PivotScreenWords } from '../src/words';

const request: GridSummaryRequest = Object.freeze({
    ranges: Object.freeze([Object.freeze({ topRow: 0, leftColumn: 1, rowCount: 2, columnCount: 1 })]),
    columns: Object.freeze(['Row Labels', 'Sum of Amount']),
    rowSequenceVersion: 1,
    figures: Object.freeze(['average', 'count', 'numericalCount', 'min', 'max', 'sum'] as const),
});

const double = (value: number): PivotNumber => Object.freeze({ kind: 'double', value });
const exact = (mantissa: bigint, scale: number): PivotNumber => Object.freeze({ kind: 'exact', value: Object.freeze({ mantissa, scale }) });

function answering(answer: Partial<PivotReportSummaryResult>): PivotReportSource {
    // Only the summary is asked here.
    return { summary: () => Promise.resolve({ version: 'v1', values: 2, numbers: 2, hasError: false, locale: 'en-US', ...answer }) } as unknown as PivotReportSource;
}

function summarize(source: PivotReportSource): Promise<GridSummaryResult> {
    return summarizeReport(source, 'v1', request, new PivotScreenWords(undefined), new AbortController().signal);
}

async function figuresOf(source: PivotReportSource): Promise<Readonly<Partial<Record<SummaryFigure, AggregateResult>>>> {
    const result = await summarize(source);
    if (result.kind !== 'answered') throw new Error(`The summary was declined: ${result.reason}`);
    return result.figures;
}

describe('ExPivot\'s Selection Summary figures (ADR-0130)', () => {
    it('a sum of finite values that overflows is #NUM! with its average, and the extremes stand', async () => {
        const figures = await figuresOf(answering({ sum: double(Infinity), min: double(1e308), max: double(1.5e308) }));
        expect(figures.sum).toEqual({ kind: 'error', error: 'notANumber' });
        expect(figures.average).toEqual({ kind: 'error', error: 'notANumber' });
        expect(figures.min).toEqual({ kind: 'number', number: 1e308, exact: undefined });
        expect(figures.max).toEqual({ kind: 'number', number: 1.5e308, exact: undefined });
        expect(figures.count).toMatchObject({ kind: 'number', number: 2 });
    });

    it('a value that is no finite number makes every figure that reads numbers #NUM!, and the counts stand', async () => {
        const figures = await figuresOf(answering({ sum: double(Infinity), min: double(1), max: double(Infinity) }));
        for (const figure of ['average', 'min', 'max', 'sum'] as const) expect(figures[figure]).toEqual({ kind: 'error', error: 'notANumber' });
        expect(figures.numericalCount).toMatchObject({ kind: 'number', number: 2 });
    });

    it('an exact figure is handed without trailing zeros, as C#\'s accumulator finishes it', async () => {
        const figures = await figuresOf(answering({ sum: exact(4200n, 2), min: exact(1000n, 2), max: exact(3200n, 2) }));
        expect(figures.sum).toEqual({ kind: 'number', number: 42, exact: { mantissa: 42n, scale: 0 } });
        expect(figures.min).toEqual({ kind: 'number', number: 10, exact: { mantissa: 10n, scale: 0 } });
    });

    it('a report the source no longer holds is declined as data that moved on, in the pivot\'s words (ADR-0066)', async () => {
        const result = await summarize(answering({ values: 0, numbers: 0, refusal: Object.freeze({ kind: 'reportVersionNotHeld', message: 'Report v1 is not held.' }) }));
        expect(result).toEqual({ kind: 'declined', reason: 'The data has changed — refresh.' });
    });
});
