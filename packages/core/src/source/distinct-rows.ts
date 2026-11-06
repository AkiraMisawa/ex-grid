// The grid's check that a Window holds no row twice (ADR-0003, ADR-0141): a row is told from every other by its
// Row Key, or without one by its identity, so a null row, a null key, or one that repeats would leave two places
// the grid cannot tell apart, and is refused by name, naming the positions, rather than painted.
import { checkedKey, quoted, type RowKey, type RowKeyOf } from './row-key';

/** The key the grid tells the row at `position` by: its Row Key, or without one the row itself. A null row, or a
 * key that names no row, is refused by name. */
export function distinctKeyOf<Row>(row: Row, position: number, rowKey: RowKeyOf<Row> | undefined): unknown {
    if (row === null || row === undefined) {
        throw new TypeError(`The row at position ${position.toString()} is ${row === null ? 'null' : 'undefined'}. Every row of `
            + 'a Window is a row the grid can name; a missing one cannot be painted, selected or copied (ADR-0003).');
    }
    if (rowKey === undefined) return row;
    // The message is made only for a key that is refused: a check of every row of a Window makes nothing else.
    const key: unknown = rowKey(row);
    if (typeof key === 'string' || typeof key === 'bigint' || (typeof key === 'number' && !Number.isNaN(key))) return key;
    return checkedKey(key, `The row at position ${position.toString()}`);
}

/** Refuses, by name, the row at `position`, which stands where the row at `first` already stood: the same Row
 * Key, or without one the same object. */
export function refuseRepeatedRow(first: number, position: number, key: unknown, keyed: boolean): never {
    throw new TypeError(keyed
        ? `The rows at positions ${first.toString()} and ${position.toString()} both have the Row Key `
            // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- a key distinctKeyOf returned under a Row Key is one
            + `${quoted(key as RowKey)}, so the grid could not tell the two apart (ADR-0140, ADR-0141).`
        : `The rows at positions ${first.toString()} and ${position.toString()} are the same object. Without a Row Key a row `
            + 'is told from every other by its identity, so the grid could not tell the two apart: hand over a copy, or name '
            + 'the rows by a Row Key (ADR-0003, ADR-0140).');
}

/** Refuses, by name, a null row in `rows`, a null key, or a row that stands twice: the same Row Key, or without one
 * the same object. `start` is the result position of `rows[0]`, which the message counts from. */
export function refuseRepeatedRows<Row>(rows: readonly Row[], start: number, rowKey: RowKeyOf<Row> | undefined): void {
    const seen = new Map<unknown, number>();
    rows.forEach((row, i) => {
        const position = start + i;
        const key = distinctKeyOf(row, position, rowKey);
        const first = seen.get(key);
        if (first !== undefined) refuseRepeatedRow(first, position, key, rowKey !== undefined);
        seen.set(key, position);
    });
}
