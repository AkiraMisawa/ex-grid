// A Row Key (ADR-0140, ADR-0141): what tells one row from every other, through all its versions. The
// Consumer's rows are its own objects, and a changed row is a new object, so a row's versions are paired
// by its key, never by its identity.

/** A Row Key: a string, a number or a bigint. Two keys are the same key when they are the same value, as
 * a `Map` compares them; an object would be the same key only as itself, so a key rebuilt from the same
 * data would name another row, and it is refused. */
export type RowKey = string | number | bigint;

/** Reads a row's Row Key. */
export type RowKeyOf<Row> = (row: Row) => RowKey;

/** The row's key, refused by name when it names no row. `which` says which row, for the message. */
export function keyOf<Row>(rowKey: RowKeyOf<Row>, row: Row, which: string): RowKey {
    return checkedKey(rowKey(row), which);
}

/** A key, refused by name when it names no row: `null` or `undefined`, `NaN`, or a value that is not a
 * string, a number or a bigint, which only an untyped caller can pass. */
export function checkedKey(key: unknown, which: string): RowKey {
    if (key === undefined || key === null) {
        throw new TypeError(`${which} has a null Row Key. A Row Key names a row, and no row is named by nothing (ADR-0140).`);
    }
    if (typeof key === 'string' || typeof key === 'bigint' || (typeof key === 'number' && !Number.isNaN(key))) return key;
    throw new TypeError(`${which} has a Row Key that is ${typeof key === 'number' ? 'NaN' : `a ${typeof key}`}. A Row Key is a `
        + 'string, a number or a bigint, which is the same key whenever it is the same value (ADR-0140).');
}

/** A key as a message quotes it. */
export function quoted(key: RowKey): string {
    return `'${key.toString()}'`;
}
