import type { HeaderGroupLayout } from './header-groups';

// The reorder gesture's arithmetic (ADR-0011, ADR-0032): what you grab is the unit that moves, and a drag
// never crosses an edge that carries meaning, the pinned boundary or a Header Group's edge. Pure, so the
// clamping is pinned in layer 1 and the gesture only translates pixels to columns.

function checkUnit(unitFirst: number, unitCount: number, columnCount: number): void {
    if (!Number.isSafeInteger(unitFirst) || unitFirst < 0) {
        throw new RangeError(`A unit starts at a column, from 0. (unitFirst: ${unitFirst})`);
    }
    if (!Number.isSafeInteger(unitCount) || unitCount < 1) {
        throw new RangeError(`A unit holds at least one column. (unitCount: ${unitCount})`);
    }
    if (unitFirst + unitCount > columnCount) {
        throw new RangeError(`A unit ends within the columns. (unitFirst: ${unitFirst}, unitCount: ${unitCount}, columns: ${columnCount})`);
    }
}

/**
 * The insertion boundaries a grabbed unit may drop at, as column indexes (0 to `columnCount`). A leaf inside
 * a group clamps at the group's edges; a group moves among its peer units; everything clamps at the pinned
 * boundary. The grabbed unit's own edges are included: dropping there is the drag that changes nothing.
 */
export function allowedBoundaries(options: {
    readonly layout: HeaderGroupLayout;
    readonly columnCount: number;
    readonly pinnedCount: number;
    readonly unitFirst: number;
    readonly unitCount: number;
}): number[] {
    const { layout, columnCount, pinnedCount, unitFirst, unitCount } = options;
    checkUnit(unitFirst, unitCount, columnCount);
    const unitEnd = unitFirst + unitCount;

    // The pinned boundary first (ADR-0011): position and pinned-ness are the same fact, so a drop across it
    // would change a column the user never touched.
    let blockStart = unitFirst < pinnedCount ? 0 : pinnedCount;
    let blockEnd = unitFirst < pinnedCount ? pinnedCount : columnCount;

    // Then the smallest group strictly containing the unit: a leaf clamps at its group's edge, a group at its
    // container's (ADR-0032).
    for (const group of layout.groups) {
        const start = group.firstColumn;
        const end = group.firstColumn + group.memberCount;
        if (start <= unitFirst && unitEnd <= end && !(start === unitFirst && end === unitEnd)
            && start >= blockStart && end <= blockEnd) {
            blockStart = Math.max(blockStart, start);
            blockEnd = Math.min(blockEnd, end);
        }
    }

    // Peer units inside the block: the outermost group spans, and each uncovered column on its own. A
    // boundary is any edge between peers, never the middle of one, which would split it.
    const boundaries = [blockStart];
    let column = blockStart;
    while (column < blockEnd) {
        let next = column + 1;
        for (const group of layout.groups) {
            const start = group.firstColumn;
            const end = group.firstColumn + group.memberCount;
            // Outermost within the block, covering this column, but never the container itself, whose span is
            // the whole block.
            if (start <= column && column < end && start >= blockStart && end <= blockEnd
                && !(start === blockStart && end === blockEnd)) {
                next = Math.max(next, end);
            }
        }
        boundaries.push(next);
        column = next;
    }
    return boundaries;
}

/** The order after dropping the unit at one allowed boundary: the block moves whole, members in their
 * current order. Membership never changes by gesture (ADR-0032). */
export function reorderColumns(options: {
    readonly names: readonly string[];
    readonly unitFirst: number;
    readonly unitCount: number;
    readonly insertAt: number;
}): string[] {
    const { names, unitFirst, unitCount, insertAt } = options;
    checkUnit(unitFirst, unitCount, names.length);
    if (!Number.isSafeInteger(insertAt) || insertAt < 0 || insertAt > names.length) {
        throw new RangeError(`A unit is inserted at a boundary, 0 to ${names.length}. (insertAt: ${insertAt})`);
    }
    const unit = names.slice(unitFirst, unitFirst + unitCount);
    const rest = [...names.slice(0, unitFirst), ...names.slice(unitFirst + unitCount)];
    // The insertion point in the list with the unit removed.
    let target = unitFirst;
    if (insertAt <= unitFirst) target = insertAt;
    else if (insertAt >= unitFirst + unitCount) target = insertAt - unitCount;
    return [...rest.slice(0, target), ...unit, ...rest.slice(target)];
}
