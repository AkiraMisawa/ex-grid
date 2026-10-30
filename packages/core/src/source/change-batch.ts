// A Change Batch (ADR-0141): what a live source's rows did since its last version, applied as one, so the
// grid shows the version before it or the one after it and never half of it.

import type { RowKey } from './row-key';

/**
 * The rows added, the rows changed and the Row Keys removed since the source's last version. A changed row
 * is found by its key, is a new object under that key (identity, not a change in place, says a row changed;
 * ADR-0003), and keeps the place in the source's order of the row it replaces; an added row goes at the
 * end, in this order. Any of the three may be left out. The lists are read when the batch is applied, and
 * not kept.
 */
export interface GridChangeBatch<Row> {
    /** The rows the batch adds. */
    readonly added?: readonly Row[];
    /** The rows the batch changes, each a new object under a key the source holds. */
    readonly changed?: readonly Row[];
    /** The keys of the rows the batch removes. */
    readonly removedKeys?: readonly RowKey[];
}
