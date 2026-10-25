import type { CommitRefusalReason } from '../cells/edit-intent';
import type { CopyRefusalReason, PasteRefusalReason } from '../clipboard/clipboard-rules';
import type { CellPosition } from '../selection/cell';

/**
 * A gesture the grid refused, said by what was refused and why (ADR-0005, ADR-0350 section 16): one callback
 * hears every refusal, and the `kind` tells them apart. Nothing a refused gesture would have done has been
 * done: a refused copy leaves the clipboard as it was, and a refused paste writes no cell.
 */
export type GridRefusal =
    /** A copy that wrote nothing: the reason names the rule it hit (ADR-0005, ADR-0011). */
    | { readonly kind: 'copy'; readonly reason: CopyRefusalReason }
    /** A paste that raised no intent, and so wrote nothing: the reason names the rule it hit (ADR-0014,
     * ADR-0035, ADR-0050 item 3). */
    | { readonly kind: 'paste'; readonly reason: PasteRefusalReason }
    /** A Cell Editor commit that raised no intent, because the row the editor was opened on is not where the
     * grid can find it (ADR-0142): the editor stands with the typing, at `cell`, in the column named. */
    | { readonly kind: 'commit'; readonly reason: CommitRefusalReason; readonly cell: CellPosition; readonly column: string };
