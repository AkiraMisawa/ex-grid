import type { CommitRefusalReason } from '../cells/edit-intent';
import type { CopyRefusalReason, PasteRefusalReason } from '../clipboard/clipboard-rules';
import type { MarkRefusalReason } from '../rows/row-marks';
import type { CellPosition } from '../selection/cell';

/**
 * Why a Find, or a Replace, moved or wrote nothing (ADR-0055, ADR-0332): nothing can search, so Ctrl+F opened
 * nothing; nothing matched; the order or the source moved while the step was out, so its answer was set
 * aside; no Replace can land here, so Ctrl+H opened nothing; or a Replace could not write the cells the panel
 * names, and wrote none.
 */
export type FindRefusalReason = 'unavailable' | 'notFound' | 'orderChanged' | 'sourceChanged' | 'replaceUnavailable' | 'replaceRefused';

/**
 * Why an action press raised nothing (ADR-0142, LV-12): the Window no longer holds the row it was pressed on;
 * nothing but a position under an order that has moved since names that row; or it was made on what a source
 * since replaced painted, whatever the new source holds there.
 */
export type ActionRefusalReason = 'rowLeftTheWindow' | 'orderMoved' | 'sourceChanged';

/**
 * Why a Consumer's placement of the Selection placed nothing (ADR-0050 item 4, ADR-0011): it was asked under
 * another Row Sequence Version than the grid's, or the commit of an open edit it caused brought in another
 * source (ADR-0142).
 */
export type PlacementRefusalReason = 'orderMoved' | 'sourceChanged';

/**
 * A gesture the grid refused, said by what was refused and why (ADR-0005, ADR-0350 section 16): one callback
 * hears every refusal, and the `kind` tells them apart. Nothing a refused gesture would have done has been
 * done: a refused copy leaves the clipboard as it was, and a refused paste writes no cell.
 */
export type GridRefusal<Row = unknown> =
    /** A copy that wrote nothing: the reason names the rule it hit (ADR-0005, ADR-0011). */
    | { readonly kind: 'copy'; readonly reason: CopyRefusalReason }
    /** A paste that raised no intent, and so wrote nothing: the reason names the rule it hit (ADR-0014,
     * ADR-0035, ADR-0050 item 3). */
    | { readonly kind: 'paste'; readonly reason: PasteRefusalReason }
    /** A Cell Editor commit that raised no intent, because the row the editor was opened on is not where the
     * grid can find it (ADR-0142): the editor stands with the typing, at `cell`, in the column named. */
    | { readonly kind: 'commit'; readonly reason: CommitRefusalReason; readonly cell: CellPosition; readonly column: string }
    /** A Find step or a Replace that moved or wrote nothing, or a Ctrl+F or Ctrl+H that opened nothing (ADR-0055,
     * ADR-0332): the find panel says why where it stands. */
    | { readonly kind: 'find'; readonly reason: FindRefusalReason }
    /** An action press that raised nothing (ADR-0020, ADR-0142): the action named, in the column named, on
     * `row` while the Window still holds it and `undefined` where it does not (ADR-0160). */
    | {
        readonly kind: 'action'; readonly reason: ActionRefusalReason; readonly row: Row | undefined; readonly column: string;
        readonly action: string;
    }
    /** A press on a mark that marked nothing and raised no Row Mark intent (ADR-0043's note of 2026-10-09, MK-9):
     * a row's checkbox whose row the Window no longer holds, or that only a position under a moved order names;
     * the header's checkbox pressed under an order that has moved since; or either, made on what a source since
     * replaced painted. */
    | { readonly kind: 'mark'; readonly reason: MarkRefusalReason }
    /** A Consumer's `placeSelection` that placed nothing, because its positions no longer name the rows they were
     * written for (ADR-0050 item 4, ADR-0011). */
    | { readonly kind: 'placement'; readonly reason: PlacementRefusalReason };
