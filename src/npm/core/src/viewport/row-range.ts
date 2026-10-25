import { wholeAtLeast } from './checks';

/**
 * The contiguous range a Range Request names (ADR-0001): a non-empty run of rows at non-negative
 * positions. A range beyond the rows currently known is legal (requests race with data updates, and
 * answering nothing is the correct answer), but a negative or empty range is a caller's defect and is
 * refused, so every Grid Source faces the same, already-checked shape.
 */
export interface RowRange {
    /** The first row's position in the whole result, from 0. */
    readonly start: number;
    /** How many rows the range holds: at least one. */
    readonly count: number;
}

/** A run of `count` rows from position `start`. A negative start or a count below one is refused. */
export function rowRange(start: number, count: number): RowRange {
    return Object.freeze({ start: wholeAtLeast(start, 0, 'start'), count: wholeAtLeast(count, 1, 'count') });
}
