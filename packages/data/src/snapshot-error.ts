// A load or a write refused (ADR-0064), as ExGrid.Data's SnapshotException says it: a load that meets a
// value it cannot read fails whole, naming the row and the column, and yields no Snapshot. The message is
// worded as C# words it, so the shared cases hold both languages to one text.

/** What a {@link SnapshotError} names, beside its reason. */
export interface SnapshotErrorPlace {
    /** The 1-based number of the row refused. */
    readonly row?: number;
    /** The name of the column refused. */
    readonly column?: string;
    /** The Record Key refused. */
    readonly key?: string | bigint;
}

/**
 * A Snapshot refused whole (ADR-0064): a read that meets a value its column's kind cannot hold, a type
 * outside ADR-0065's table or bytes that are not a whole Arrow stream; a write that meets a value its
 * Arrow type cannot hold exactly; a Record Key Blank or carried twice. Nothing is yielded, or written.
 * Its message names the row and the column as ExGrid.Data's `SnapshotException` does:
 * `Row 12,345, column 'Notional': …`.
 */
export class SnapshotError extends Error {
    /** The 1-based number of the row refused, or `undefined` when the refusal is not about one row. */
    readonly row: number | undefined;
    /** The name of the column refused, or `undefined` when the refusal is not about one column. A refusal
     * about a Record Key names the key's column. */
    readonly column: string | undefined;
    /** The Record Key refused, or `undefined` when the refusal is not about a key. */
    readonly key: string | bigint | undefined;

    /** A refusal for `reason`, prefixed with the row and the column `place` names, when it names them. */
    constructor(reason: string, place: SnapshotErrorPlace = {}) {
        super(SnapshotError.#format(reason, place));
        this.name = 'SnapshotError';
        this.row = place.row;
        this.column = place.column;
        this.key = place.key;
    }

    static #format(reason: string, place: SnapshotErrorPlace): string {
        if (place.row !== undefined && place.column !== undefined) return `Row ${grouped(place.row)}, column '${place.column}': ${reason}`;
        if (place.row !== undefined) return `Row ${grouped(place.row)}: ${reason}`;
        if (place.column !== undefined) return `Column '${place.column}': ${reason}`;
        return reason;
    }
}

/** A whole number with its thousands grouped by commas, as .NET's invariant `N0` writes it. */
export function grouped(value: number | bigint): string {
    const digits = (value < 0 ? -BigInt(value) : BigInt(value)).toString();
    const groups: string[] = [];
    for (let end = digits.length; end > 0; end -= 3) groups.unshift(digits.slice(Math.max(0, end - 3), end));
    return (value < 0 ? '-' : '') + groups.join(',');
}
