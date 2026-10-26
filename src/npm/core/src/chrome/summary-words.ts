import type { SummaryFigure } from '../summary/summary-accumulator';

/** The ids of the words the Selection Summary shows (ADR-0130, ADR-0370 section 10), resolved through the grid's
 * `commandLabel`, as the menus' words are. */
export const summaryLabelIds = Object.freeze({
    /** Average. */
    average: 'summary-average',
    /** Count. */
    count: 'summary-count',
    /** Numerical Count. */
    numericalCount: 'summary-numerical-count',
    /** Min. */
    min: 'summary-min',
    /** Max. */
    max: 'summary-max',
    /** Sum. */
    sum: 'summary-sum',
    /** What the line says while the question is out. */
    pending: 'summary-pending',
} as const);

/** The id of a figure's name. */
export function summaryLabelIdOf(figure: SummaryFigure): string {
    return summaryLabelIds[figure];
}

/** The built-in English for the Selection Summary's ids, as the C# core says them. */
export const summaryWords: readonly (readonly [string, string])[] = Object.freeze([
    [summaryLabelIds.average, 'Average'],
    [summaryLabelIds.count, 'Count'],
    [summaryLabelIds.numericalCount, 'Numerical Count'],
    [summaryLabelIds.min, 'Min'],
    [summaryLabelIds.max, 'Max'],
    [summaryLabelIds.sum, 'Sum'],
    [summaryLabelIds.pending, 'Calculating…'],
]);
