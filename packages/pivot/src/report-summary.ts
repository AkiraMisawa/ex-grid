// The Selection Summary over the report (ADR-0130), the Razor component's `SummarizeReportAsync`: the report
// source sums the cells it laid out, under the Report Version the selection was made on, and the figures are
// read from its counts, sum and extremes as C#'s `GridSummaryCells` reads a server's parts. A figure that reads
// numbers is written in the Focus value's number format, as the report shows that value.

import { answeredSummary, declinedSummary, summaryFigureOrder, type AggregateResult, type GridSummaryRequest, type GridSummaryResult, type SummaryFigure } from '@ex-grid/core';
import { formatPivotFigure, pivotNumberAverage, pivotNumberCanonical, pivotNumberValue, type PivotNumber, type PivotReportSource, type PivotReportSummaryResult } from '@ex-grid/pivot-engine';
import { unreachable } from './must';
import type { PivotScreenWords } from './words';

const empty: AggregateResult = Object.freeze({ kind: 'empty' });

/** The figures `request` asks for over the report of `version`, or a decline in ExPivot's words (ADR-0060). */
export async function summarizeReport(
    source: PivotReportSource, version: string, request: GridSummaryRequest, words: PivotScreenWords, signal: AbortSignal,
): Promise<GridSummaryResult> {
    const { focus } = request;
    const query = Object.freeze({
        version,
        columns: request.columns,
        ranges: Object.freeze(request.ranges.map(range => Object.freeze({
            top: range.topRow, left: range.leftColumn, bottom: range.topRow + range.rowCount - 1, right: range.leftColumn + range.columnCount - 1,
        }))),
        ...(focus === undefined ? {} : { focusRow: focus.row, focusColumn: focus.column }),
    });
    const answer = await source.summary(query, signal);
    if (answer.version !== version) return declinedSummary(words.word('summary-another-version'));
    if (answer.refusal !== undefined) return declinedSummary(words.reportRefused(answer.refusal));
    // An error is counted among the values: an answer that says one was selected and counts no value contradicts itself.
    if (answer.hasError && answer.values <= 0) return declinedSummary(words.word('summary-inconsistent-errors'));
    return figuresOf(answer, request.figures);
}

// Each figure asked for, in Excel's order, by the rules C#'s accumulator reads merged parts by: a figure that reads
// numbers is empty where no cell holds one, an error among the values shows only the counts, a value that is no finite
// number makes every figure that reads numbers #NUM!, and a figure that overflows is #NUM! alone.
function figuresOf(answer: PivotReportSummaryResult, asked: readonly SummaryFigure[]): GridSummaryResult {
    const { values, numbers, min, max, hasError, numberFormat, locale } = answer;
    const figures: Partial<Record<SummaryFigure, AggregateResult>> = {};
    const texts: Partial<Record<SummaryFigure, string>> = {};
    // A value that is no finite number is one of the extremes; a sum of finite values may still overflow.
    const nonFinite = [min, max].some(number => number !== undefined && !finite(number));
    for (const figure of summaryFigureOrder) {
        if (!asked.includes(figure)) continue;
        const counts = figure === 'count' || figure === 'numericalCount';
        if (!counts && numbers === 0) figures[figure] = empty;
        else if (!counts && hasError) figures[figure] = Object.freeze({ kind: 'error', error: 'errorAmongValues' });
        else if (values === 0) figures[figure] = empty;
        else if (counts) figures[figure] = countOf(figure === 'count' ? values : numbers);
        else {
            const number = pivotNumberCanonical(numberOf(figure, answer));
            if (nonFinite || !finite(number)) {
                figures[figure] = Object.freeze({ kind: 'error', error: 'notANumber' });
            } else {
                figures[figure] = resultOf(number);
                if (numberFormat !== undefined) texts[figure] = formatPivotFigure(number, numberFormat, locale);
            }
        }
    }
    return answeredSummary(figures, texts);
}

function numberOf(figure: SummaryFigure, answer: PivotReportSummaryResult): PivotNumber {
    const { numbers, sum, min, max } = answer;
    switch (figure) {
        case 'sum': return held(sum, figure);
        case 'average': return pivotNumberAverage(held(sum, figure), numbers);
        case 'min': return held(min, figure);
        case 'max': return held(max, figure);
        case 'count':
        case 'numericalCount': throw new Error(`Internal: ${figure} is a count.`);
        default: return unreachable(figure);
    }
}

// The report source answers a sum and both extremes wherever it counted a number.
function held(number: PivotNumber | undefined, figure: SummaryFigure): PivotNumber {
    if (number === undefined) throw new Error(`The report source counted numbers and answered no ${figure} of them (ADR-0130).`);
    return number;
}

function finite(number: PivotNumber): boolean {
    return Number.isFinite(pivotNumberValue(number));
}

function countOf(count: number): AggregateResult {
    return Object.freeze({ kind: 'number', number: count, exact: Object.freeze({ mantissa: BigInt(count), scale: 0 }) });
}

function resultOf(number: PivotNumber): AggregateResult {
    return Object.freeze(number.kind === 'exact'
        ? { kind: 'number', number: pivotNumberValue(number), exact: number.value }
        : { kind: 'number', number: number.value, exact: undefined });
}
