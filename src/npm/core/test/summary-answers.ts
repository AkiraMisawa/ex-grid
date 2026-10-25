// The Selection Summary's requests and answers as a case writes them, read for the "summary" and "source"
// runners.
import { expect } from 'vitest';
import { unreachable } from '../src/must';
import { decimalText } from '../src/summary/exact-decimal';
import { summaryFigureText, type GridSummaryRequest, type GridSummaryResult } from '../src/summary/grid-summary';
import { isSummaryFigure, type AggregateResult, type SummaryFigure } from '../src/summary/summary-accumulator';
import { readRanges, type RangeText } from './cases';

/** A figure's answer as a case writes it. */
export type WrittenAnswer = 'empty' | { readonly exact: string } | { readonly double: number | string } | { readonly error: string };

/** A Selection Summary's request as a case writes it: the file's columns unless it names its own. */
export interface WrittenAsk {
    readonly ranges: readonly RangeText[];
    readonly columns?: readonly string[];
    readonly figures: readonly SummaryFigure[];
}

/** The request a case writes, under the version given. */
export function summaryRequestOf(written: WrittenAsk, columns: readonly string[], version: number): GridSummaryRequest {
    return { ranges: readRanges(written.ranges), columns: written.columns === undefined ? columns : written.columns, rowSequenceVersion: version, figures: written.figures };
}

/** A figure's answer as a case reads it. */
export function shownAnswer(result: AggregateResult): WrittenAnswer {
    switch (result.kind) {
        case 'empty': return 'empty';
        case 'error': return { error: result.error };
        case 'number': return result.exact === undefined ? { double: result.number } : { exact: decimalText(result.exact) };
        default: return unreachable(result);
    }
}

/** Holds an answered summary to what a case says of it: every figure it names, and no other. */
export function expectAnswers(
    result: GridSummaryResult, expected: Readonly<Record<string, WrittenAnswer>>, texts: Readonly<Record<string, string>> | undefined,
): void {
    if (result.kind !== 'answered') throw new Error(`Expected figures, got a decline: ${result.reason}`);
    expect(Object.keys(result.figures).toSorted(), 'the figures answered').toEqual(Object.keys(expected).toSorted());
    for (const [figure, written] of Object.entries(expected)) {
        if (!isSummaryFigure(figure)) throw new Error(`"${figure}" is not a figure.`);
        const answer = result.figures[figure];
        if (answer === undefined) throw new Error(`No answer for ${figure}.`);
        const wanted = typeof written === 'object' && 'double' in written ? { double: Number(written.double) } : written;
        expect(shownAnswer(answer), figure).toEqual(wanted);
    }
    for (const [figure, text] of Object.entries(texts === undefined ? {} : texts)) {
        if (!isSummaryFigure(figure)) throw new Error(`"${figure}" is not a figure.`);
        const answer = result.figures[figure];
        if (answer === undefined) throw new Error(`A text for ${figure}, which was not asked.`);
        expect(summaryFigureText(figure, answer), `the text of ${figure}`).toBe(text);
    }
}

