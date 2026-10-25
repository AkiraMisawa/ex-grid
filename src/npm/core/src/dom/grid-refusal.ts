import type { CopyRefusalReason } from '../clipboard/clipboard-rules';

/**
 * A gesture the grid refused, said by what was refused and why (ADR-0005, ADR-0350 section 16): one callback
 * hears every refusal, and the `kind` tells them apart. Nothing a refused gesture would have done has been
 * done: a refused copy leaves the clipboard as it was.
 */
export type GridRefusal =
    /** A copy that wrote nothing: the reason names the rule it hit (ADR-0005, ADR-0011). */
    | { readonly kind: 'copy'; readonly reason: CopyRefusalReason };
