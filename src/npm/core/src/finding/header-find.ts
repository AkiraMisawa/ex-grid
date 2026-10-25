// The headers a Find step walks (ADR-0333), the counterpart of C#'s FindHeaders: the grid searches its
// visible column headers and Header Group labels itself, by the reference rule over the text it shows, and
// asks the Consumer only about rows. The header rows come before the data rows in the walk
// (header-find-walk.ts), each left to right in the current column order.

import { itemAt } from '../must';
import type { HeaderGroupLayout, ResolvedHeaderGroup } from '../columns/header-groups';
import { matcherOf, type GridFindPattern } from './find';

/** One header a step may stop on: where it stands, what it says, and which header it is. */
export interface FindHeader {
    /** Its header row, from the top: the Header Group tiers first, the column header row last. */
    readonly headerRow: number;
    /** The first column under it. */
    readonly firstColumn: number;
    /** The last column under it: the first, for a column's own header. */
    readonly lastColumn: number;
    /** The text it shows, which is what is matched. */
    readonly text: string;
    /** Which header it is, wherever the columns move it: {@link columnHeaderId} or {@link groupHeaderId}. */
    readonly id: string;
}

/** Which header a column's header is (ADR-0333): its column's name. */
export function columnHeaderId(name: string): string {
    return JSON.stringify(['column', name]);
}

/** Which header a Header Group's label is (ADR-0333): its declared top tier and the first member column it
 * declares. */
export function groupHeaderId(group: ResolvedHeaderGroup): string {
    return JSON.stringify(['group', group.group.tier, group.group.columns[0]]);
}

/**
 * The headers in walk order (ADR-0333 §3): each Header Group row from the top, then the column header row,
 * each left to right in the current column order. `columns` are the visible columns' names and headers in
 * that order.
 */
export function findHeaders(
    columns: readonly { readonly name: string; readonly header: string }[], layout: HeaderGroupLayout,
): readonly FindHeader[] {
    const headers: FindHeader[] = [];
    for (let headerRow = 0; headerRow < layout.tierCount; headerRow++) {
        const tier = layout.tierCount - headerRow;
        // Declared in any order; walked left to right.
        const inTier = layout.groups.filter(group => group.topTier === tier).toSorted((a, b) => a.firstColumn - b.firstColumn);
        for (const group of inTier) {
            headers.push({
                headerRow, firstColumn: group.firstColumn, lastColumn: group.firstColumn + group.memberCount - 1,
                text: group.label, id: groupHeaderId(group),
            });
        }
    }
    columns.forEach(({ name, header }, c) => {
        headers.push({ headerRow: layout.tierCount, firstColumn: c, lastColumn: c, text: header, id: columnHeaderId(name) });
    });
    return headers;
}

/** The indices of the headers that match `pattern`, ascending. */
export function matchingHeaders(headers: readonly FindHeader[], pattern: GridFindPattern): readonly number[] {
    const matches = matcherOf(pattern);
    return headers.flatMap((header, i) => (matches(header.text) ? [i] : []));
}

/** Whether two walks hold the same headers in the same places, whatever their text: what a step read before
 * it asked still stands where it stood. */
export function sameHeaders(read: readonly FindHeader[], now: readonly FindHeader[]): boolean {
    return read.length === now.length && read.every((header, i) => {
        const other = itemAt(now, i);
        return header.id === other.id && header.headerRow === other.headerRow
            && header.firstColumn === other.firstColumn && header.lastColumn === other.lastColumn;
    });
}
