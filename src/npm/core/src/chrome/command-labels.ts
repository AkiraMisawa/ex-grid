import { gridCommandIds, gridLabelIds } from './command-ids';
import { findPanelWords } from './find-panel-labels';
import { markWords } from './mark-words';
import { errorStepWords, pendingCommitWords, rowCommandRefusalWords } from './refusal-words';
import { selectionWords } from './selection-words';
import { summaryWords } from './summary-words';

// The built-in Chrome's English for the core's commands, as the C# core's BuiltInCommandLabels says them
// (ADR-0036). A command is an id, whether it may run, and what running it does: what it is called is
// rendering, so it belongs to the Chrome and is resolved from the id. A Consumer who keeps the built-in menu
// and wants it in another language answers `commandLabel` first. The find panel's words are resolved the same
// way, by their ids (ADR-0055).
const words: ReadonlyMap<string, string> = new Map([
    [gridCommandIds.sortAscending, 'Sort ascending'],
    [gridCommandIds.sortDescending, 'Sort descending'],
    [gridCommandIds.clearFilter, 'Clear filter'],
    [gridCommandIds.hide, 'Hide this column'],
    [gridCommandIds.pin, 'Pin up to this column'],
    [gridCommandIds.unpin, 'Unpin all columns'],
    [gridCommandIds.sizeToFit, 'Size to fit'],
    [gridCommandIds.copy, 'Copy'],
    [gridCommandIds.copyWithHeaders, 'Copy with headers'],
    [gridLabelIds.nameBox, 'Name Box'],
    [gridLabelIds.formulaBar, 'Formula Bar'],
    ...findPanelWords,
    ...rowCommandRefusalWords,
    ...pendingCommitWords,
    ...errorStepWords,
    ...markWords,
    ...selectionWords,
    ...summaryWords,
]);

/**
 * What the built-in menu calls a command: the built-in English for one of the core's, and the id itself for
 * any other. A Consumer's command that reaches the built-in menu unlabelled then says what it is, rather than
 * showing a blank (ADR-0036).
 */
export function builtInCommandLabel(id: string): string {
    return words.get(id) ?? id;
}

/** Every id the built-in Chrome has English for: the ids a Consumer's `commandLabel` answers to give the core's own
 * words another language, as an adapter that cannot hand a function across asks them up front. */
export function builtInLabelIds(): readonly string[] {
    return Object.freeze([...words.keys()]);
}
