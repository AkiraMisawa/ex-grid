/** The ids of the words the status line says about a Selection off screen (ADR-0014, ADR-0015, ADR-0370 section
 * 10), resolved through the grid's `commandLabel`, as the menus' words are. */
export const selectionLabelIds = Object.freeze({
    /** The off-screen indicator: how many cells are selected, at `{count}`, none of them in view. */
    selectedOutsideView: 'selected-outside-view',
    /** The button that brings the Selection's Focus back into view. */
    goToSelection: 'go-to-selection',
} as const);

/** The built-in English for those ids, as the C# core says them. */
export const selectionWords: readonly (readonly [string, string])[] = Object.freeze([
    [selectionLabelIds.selectedOutsideView, '{count} cells selected (outside the visible range)'],
    [selectionLabelIds.goToSelection, 'Go to selection'],
]);
