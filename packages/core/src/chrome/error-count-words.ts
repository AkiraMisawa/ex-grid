/** The ids of the words the status line says about the Error Count (ADR-0381 section 3's note of 2026-11-01, ADR-0370
 * section 10), resolved through the grid's `commandLabel`, as the menus' words are. */
export const errorCountLabelIds = Object.freeze({
    /** Every error cell at `{count}`, the step reaching them all. */
    count: 'error-count',
    /** Every error cell at `{count}`, and at `{hidden}` how many of them the step cannot reach. */
    countHidden: 'error-count-hidden',
    /** What the line says while the question is out. */
    pending: 'error-count-pending',
    /** What pressing the count does, said as its title. */
    step: 'error-count-step',
} as const);

/** The built-in English for those ids. */
export const errorCountWords: readonly (readonly [string, string])[] = Object.freeze([
    [errorCountLabelIds.count, 'Errors: {count}'],
    [errorCountLabelIds.countHidden, 'Errors: {count} ({hidden} hidden)'],
    [errorCountLabelIds.pending, 'Counting errors…'],
    [errorCountLabelIds.step, 'Go to the next error (F8)'],
]);
