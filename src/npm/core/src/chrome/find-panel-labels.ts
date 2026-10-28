// The words of the find panel (ADR-0055) and its Replace (ADR-0332 §2), by id, as the C# core's
// FindPanelLabelIds names them. The built-in panel resolves them as the built-in menus resolve theirs:
// through the grid's `commandLabel`, falling back to the built-in English. The grid holds no sentence: which
// one a step's outcome calls for is `findOutcomeText`'s.

import { unreachable } from '../must';
import { filledWords } from './filled-words';

/** The find panel's label ids. */
export const findPanelLabelIds = Object.freeze({
    /** The panel's accessible name. */
    title: 'find',
    /** The text field's label. */
    field: 'find-field',
    /** The Match case option. */
    matchCase: 'find-match-case',
    /** The Match entire cell contents option. */
    wholeCell: 'find-whole-cell',
    /** The step forward. */
    next: 'find-next',
    /** The step backward. */
    previous: 'find-previous',
    /** What a step that matched nothing says, and a Replace All that replaced none. */
    notFound: 'find-not-found',
    /** What a step that found its match in a header says (ADR-0333), the header at `{column}`. */
    foundInHeader: 'find-found-in-header',
    /** What a step whose answer arrived under another order says. */
    orderChanged: 'find-order-changed',
    /** What a step whose answer came from a source since replaced by another says. */
    sourceChanged: 'find-source-changed',
    /** The panel's accessible name while it offers Replace (ADR-0332 §2). */
    replaceTitle: 'find-replace-title',
    /** The Replace field's label. */
    replaceField: 'find-replace-field',
    /** The Replace button: the current match, then the next. */
    replace: 'find-replace',
    /** The Replace All button. */
    replaceAll: 'find-replace-all',
    /** What a Replace All that replaced several cells says, their count at `{count}`. */
    replaced: 'find-replaced',
    /** What a Replace All that replaced one cell says. */
    replacedOne: 'find-replaced-one',
    /** What a Replace that wrote nothing says first, before the cells it names. */
    replaceRefused: 'find-replace-refused',
    /** One cell a Replace could not write: `{row}`, `{column}` and `{reason}`. */
    replaceRefusedCell: 'find-replace-refused-cell',
    /** The refused cells past those named, their count at `{count}`. */
    replaceRefusedMore: 'find-replace-refused-more',
    /** Why a match in a column that is not Editable cannot be written. */
    notEditable: 'find-not-editable',
} as const);

/** The built-in English for the find panel's ids, as the C# core's BuiltInCommandLabels says them. */
export const findPanelWords: readonly (readonly [string, string])[] = Object.freeze([
    [findPanelLabelIds.title, 'Find'],
    [findPanelLabelIds.field, 'Find what'],
    [findPanelLabelIds.matchCase, 'Match case'],
    [findPanelLabelIds.wholeCell, 'Match entire cell contents'],
    [findPanelLabelIds.next, 'Find next'],
    [findPanelLabelIds.previous, 'Find previous'],
    [findPanelLabelIds.notFound, 'No match'],
    [findPanelLabelIds.foundInHeader, 'Found in the header {column}'],
    [findPanelLabelIds.orderChanged, 'The rows moved; find again'],
    [findPanelLabelIds.sourceChanged, 'The data was replaced; find again'],
    [findPanelLabelIds.replaceTitle, 'Find and Replace'],
    [findPanelLabelIds.replaceField, 'Replace with'],
    [findPanelLabelIds.replace, 'Replace'],
    [findPanelLabelIds.replaceAll, 'Replace All'],
    [findPanelLabelIds.replaced, 'Replaced {count} cells.'],
    [findPanelLabelIds.replacedOne, 'Replaced 1 cell.'],
    [findPanelLabelIds.replaceRefused, 'Nothing was replaced.'],
    [findPanelLabelIds.replaceRefusedCell, 'Row {row}, {column}: {reason}.'],
    [findPanelLabelIds.replaceRefusedMore, 'And {count} more.'],
    [findPanelLabelIds.notEditable, 'this column cannot be edited'],
] as const);

/** How many refused cells the panel names before it counts the rest. */
export const refusedCellsNamed = 5;

/** One cell a Replace could not write, as the panel names it: its row's number from 1, its column's header,
 * and why in the Consumer's words, or `undefined` where the column is not Editable. */
export interface FindPanelRefusedCell {
    /** The cell's row, counted from 1 in the order shown. */
    readonly rowNumber: number;
    /** The header of the cell's column. */
    readonly column: string;
    /** Why the cell was not written, in the Consumer's words, or `undefined` where its column is not Editable. */
    readonly reason: string | undefined;
}

/**
 * What the last step or Replace came to (ADR-0055, ADR-0332, ADR-0333): nothing yet, or a match in a cell,
 * which the moved Focus shows; a match in a header, named; nothing found; an answer the order or the
 * source outlived; a Replace All's count; or the cells a Replace could not write.
 */
export type FindOutcome =
    | { readonly kind: 'none' | 'found' | 'notFound' | 'orderChanged' | 'sourceChanged' }
    | { readonly kind: 'foundInHeader'; readonly header: string }
    | { readonly kind: 'replaced'; readonly count: number }
    | { readonly kind: 'replaceRefused'; readonly cells: readonly FindPanelRefusedCell[] };

/**
 * The whole sentence the panel shows for `outcome`, in `words`, or the empty string for an outcome that says
 * nothing. A refused Replace names its first cells, as many as the panel names, and counts the rest (ADR-0332
 * §2). A number is written as the invariant culture writes it, as the C# core's built-in panel does under
 * the hosts' culture.
 */
export function findOutcomeText(outcome: FindOutcome, words: (id: string) => string): string {
    switch (outcome.kind) {
        case 'none':
        case 'found':
            return '';
        case 'notFound':
            return words(findPanelLabelIds.notFound);
        case 'orderChanged':
            return words(findPanelLabelIds.orderChanged);
        case 'sourceChanged':
            return words(findPanelLabelIds.sourceChanged);
        case 'foundInHeader':
            return filledWords(words(findPanelLabelIds.foundInHeader), { column: outcome.header });
        case 'replaced':
            return outcome.count === 1
                ? words(findPanelLabelIds.replacedOne)
                : filledWords(words(findPanelLabelIds.replaced), { count: String(outcome.count) });
        case 'replaceRefused': {
            const { cells } = outcome;
            const named = cells.slice(0, refusedCellsNamed).map(cell => filledWords(words(findPanelLabelIds.replaceRefusedCell), {
                row: String(cell.rowNumber),
                column: cell.column,
                reason: trimmedReason(cell.reason === undefined ? words(findPanelLabelIds.notEditable) : cell.reason),
            }));
            const more = cells.length > refusedCellsNamed
                ? [filledWords(words(findPanelLabelIds.replaceRefusedMore), { count: String(cells.length - refusedCellsNamed) })]
                : [];
            return [words(findPanelLabelIds.replaceRefused), ...named, ...more].join(' ');
        }
        default:
            return unreachable(outcome);
    }
}

// A reason ends where its sentence's own full stop is put: trailing white space and stops are dropped, as
// the C# core's TrimEnd().TrimEnd('.') drops them.
function trimmedReason(reason: string): string {
    return reason.trimEnd().replace(/\.+$/u, '');
}
