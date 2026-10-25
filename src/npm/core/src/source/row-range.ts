// A Range Request's rows (ADR-0001): where they start in the result and how many there are.

/** Rows of the result, from `start`, `count` of them: what a Range Request names. */
export interface RowRange {
    readonly start: number;
    readonly count: number;
}

/** Refuses a range that names no rows: a start before the first, or fewer than one row. */
export function checkRange(range: RowRange): void {
    if (!Number.isSafeInteger(range.start) || range.start < 0) {
        throw new RangeError(`A row range starts at a whole number from 0, not ${range.start.toString()}.`);
    }
    if (!Number.isSafeInteger(range.count) || range.count < 1) {
        throw new RangeError(`A row range holds at least one row, not ${range.count.toString()}.`);
    }
}
