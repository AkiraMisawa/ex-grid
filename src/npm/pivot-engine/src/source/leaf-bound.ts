// The most leaves a layout can make, from each field's count of Items (ADR-0370 section 3): the hint the
// Field List marks a drop target with while a field is dragged over the leaf cap. The C# engine's
// PivotLeafBound.

import { publicKeyId } from '../items/item-key';
import type { PivotLayout } from '../layout/pivot-layout';
import type { PivotItemCounts } from './pivot-source';

/** Where a bound stops growing: 2^53 − 1, the largest whole number held exactly, as the C# engine stops. */
export const pivotLeafBoundMost = Number.MAX_SAFE_INTEGER;

/**
 * The most leaves `layout` can make, at most {@link pivotLeafBoundMost}: the product, over the fields in
 * Rows and Columns, of each field's count less the Items the layout hides of it. The leaves are the
 * combinations of those Items that some record carries, so they are never more while the counts are the
 * data's and every Hidden Item is among them; Filters and the Values add none. `undefined` when a field the
 * layout places in Rows or Columns was not counted. The bound is a hint for the drop target's mark only: a
 * count gone stale, or a Hidden Item the data no longer holds, moves the mark, never the answer, and the
 * question is asked with its `maxLeaves` all the same.
 */
export function pivotLeafBound(layout: PivotLayout, counts: PivotItemCounts): number | undefined {
    let bound = 1;
    for (const placement of [...layout.rows, ...layout.columns]) {
        const count = counts.countOf(placement.field);
        if (count === undefined) return undefined;
        const hidden = new Set(placement.hiddenItems.map(publicKeyId)).size;
        const shown = Math.max(0, count - hidden);
        bound = shown === 0 ? 0 : bound > Math.floor(pivotLeafBoundMost / shown) ? pivotLeafBoundMost : bound * shown;
    }
    return bound;
}
