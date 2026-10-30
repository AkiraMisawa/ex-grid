// Which Headings the Heading Mark marks (ADR-0008, 2026-10-13): every column and every row that holds part
// of the Selection, for every Selection, as Excel marks its Headings. Answered as runs, each a stretch of
// consecutive marked columns or rows, so the overlay draws one rectangle per run: what the union costs
// depends on how many ranges there are, never on how many columns or rows they cover (ADR-0008's economy).
// A run is the union of the ranges' spans on that axis, spans that overlap or touch joined, so a line runs
// on unbroken across neighbouring marked Headings, as Excel's ran on under A and B for A1:B2 (the readings
// of 2026-10-12, R4 (2)). The Focus marks nothing of its own: it is always inside a range (ADR-0052).
// The C# core's HeadingMarks.

import type { ColumnRange } from '../columns/column-geometry';
import type { RowRange } from '../viewport/row-range';
import { elementAt } from '../must';
import type { GridExtent } from './cell';
import type { SelectionRange } from './range';
import type { Selection } from './selection';

/** A run of marked Headings on one axis: its first index and how many it holds. */
type Run = ColumnRange & RowRange;

const runOf = (start: number, count: number): Run => Object.freeze({ start, count });

/** The marked columns, as runs in column order, or none for an empty Selection. */
export function markedColumns(selection: Selection): readonly ColumnRange[] {
    return union(selection.ranges.map(range => [range.leftColumn, range.leftColumn + range.columnCount - 1] as const));
}

/** The marked rows, as runs in row order, or none for an empty Selection. */
export function markedRows(selection: Selection): readonly RowRange[] {
    return rowRunsOf(selection.ranges);
}

/** The rows the given ranges cover, as runs in row order: ranges that overlap or touch are one run. */
export function rowRunsOf(ranges: readonly SelectionRange[]): readonly RowRange[] {
    return union(ranges.map(range => [range.topRow, range.topRow + range.rowCount - 1] as const));
}

/** The columns covered in full by the Selection's union, as ordered runs (ADR-0008, D1). */
export function wholeColumnRuns(selection: Selection, extent: GridExtent): readonly ColumnRange[] {
    return wholeAxis(selection, extent.columnCount, extent.rowCount, true);
}

/** The rows covered in full by the Selection's union, as ordered runs (ADR-0008, D1). */
export function wholeRowRuns(selection: Selection, extent: GridExtent): readonly RowRange[] {
    return wholeAxis(selection, extent.rowCount, extent.columnCount, false);
}

/** The union of inclusive spans, as the fewest disjoint runs in order: two spans that overlap, or that
 * touch with no index between them, are one. */
function union(spans: readonly (readonly [number, number])[]): readonly Run[] {
    const sorted = spans.toSorted((a, b) => (a[0] === b[0] ? a[1] - b[1] : a[0] - b[0]));
    const runs: [number, number][] = [];
    for (const [first, last] of sorted) {
        const previous = runs.at(-1);
        if (previous !== undefined && first <= previous[1] + 1) {
            previous[1] = Math.max(previous[1], last);
        } else {
            runs.push([first, last]);
        }
    }
    return Object.freeze(runs.map(([first, last]) => runOf(first, last - first + 1)));
}

interface CoverageEvent {
    readonly position: number;
    readonly crossStart: number;
    readonly crossEnd: number;
    readonly delta: number;
}

// Sweep one axis at rectangle boundaries. Between two boundaries, every cross-section has the same selected
// intervals on the other axis. Its minimum coverage is positive exactly when the union covers that whole
// axis: overlaps cannot make up for a hole. Compressed coordinates keep both time and storage dependent on
// ranges, never on the grid's extent.
function wholeAxis(selection: Selection, axisCount: number, crossCount: number, columns: boolean): readonly Run[] {
    if (selection.isEmpty || axisCount <= 0 || crossCount <= 0) return Object.freeze([]);
    const events: CoverageEvent[] = [];
    const boundaries = [0, crossCount];
    for (const range of selection.ranges) {
        const start = columns ? range.leftColumn : range.topRow;
        const crossStart = columns ? range.topRow : range.leftColumn;
        const end = Math.min(axisCount, start + (columns ? range.columnCount : range.rowCount));
        const crossEnd = Math.min(crossCount, crossStart + (columns ? range.rowCount : range.columnCount));
        if (start >= end || crossStart >= crossEnd) continue;
        events.push({ position: start, crossStart, crossEnd, delta: 1 }, { position: end, crossStart, crossEnd, delta: -1 });
        boundaries.push(crossStart, crossEnd);
    }
    if (events.length === 0) return Object.freeze([]);

    const ordered = events.toSorted((a, b) => a.position - b.position);
    const coordinates = new Map<number, number>();
    for (const boundary of boundaries.toSorted((a, b) => a - b)) {
        if (!coordinates.has(boundary)) coordinates.set(boundary, coordinates.size);
    }
    const indexOf = (boundary: number): number => {
        const index = coordinates.get(boundary);
        if (index === undefined) throw new Error(`Internal: ${boundary} is not a compressed boundary.`);
        return index;
    };
    const coverage = new CoverageMinimum(coordinates.size - 1);
    const runs: [number, number][] = [];
    let previous = elementAt(ordered, 0).position;
    let index = 0;
    while (index < ordered.length) {
        const { position } = elementAt(ordered, index);
        if (previous < position && coverage.allCovered) {
            const last = runs.at(-1);
            if (last !== undefined && last[1] === previous) {
                last[1] = position;
            } else {
                runs.push([previous, position]);
            }
        }
        // Apply every start and end at this coordinate before examining the next span; an adjacent pair of
        // rectangles leaves no gap, whatever the sort's tie order.
        do {
            const change = elementAt(ordered, index++);
            coverage.add(indexOf(change.crossStart), indexOf(change.crossEnd), change.delta);
        } while (index < ordered.length && elementAt(ordered, index).position === position);
        previous = position;
    }
    return Object.freeze(runs.map(([start, end]) => runOf(start, end - start)));
}

// Range addition and the global minimum on compressed intervals. Each node's minimum includes its own
// pending addition but not its ancestors', so a partial update needs no propagation; recomputing the parent
// adds its pending value to its children's minimum.
class CoverageMinimum {
    readonly #count: number;
    readonly #minimum: Int32Array;
    readonly #added: Int32Array;

    constructor(count: number) {
        this.#count = count;
        this.#minimum = new Int32Array(count * 4);
        this.#added = new Int32Array(count * 4);
    }

    get allCovered(): boolean {
        return this.#at(this.#minimum, 1) > 0;
    }

    add(start: number, end: number, delta: number): void {
        this.#add(1, 0, this.#count, start, end, delta);
    }

    #add(node: number, left: number, right: number, start: number, end: number, delta: number): void {
        if (start <= left && right <= end) {
            this.#minimum[node] = this.#at(this.#minimum, node) + delta;
            this.#added[node] = this.#at(this.#added, node) + delta;
            return;
        }
        const middle = left + Math.floor((right - left) / 2);
        if (start < middle) this.#add(node * 2, left, middle, start, end, delta);
        if (end > middle) this.#add(node * 2 + 1, middle, right, start, end, delta);
        this.#minimum[node] = this.#at(this.#added, node)
            + Math.min(this.#at(this.#minimum, node * 2), this.#at(this.#minimum, node * 2 + 1));
    }

    #at(array: Int32Array, node: number): number {
        const value = array[node];
        if (value === undefined) throw new Error(`Internal: node ${node} is outside the coverage tree.`);
        return value;
    }
}
