// A link's narrowing (ADR-0340, Q5): C#'s LinkCondition and Narrowing. A row is let through when its tuple of
// values in the link's columns is one the selection holds, text compared as the grid's value list compares it.

import { foldCase } from '@ex-grid/core';
import { createSnapshot, decimalText, type Snapshot, type SnapshotColumn, type SnapshotColumnInput, type SnapshotDecimal } from '@ex-grid/data';

/** A value as a Snapshot hands it out: text, a Decimal, a double, an Integer or a Date's ticks as a `bigint`, a
 * boolean, or `null` for a Blank. */
export type BoardValue = string | SnapshotDecimal | number | bigint | boolean | null;

/**
 * What a link's selection narrows to (ADR-0340, Q5): the columns it compares, and each tuple of their values the
 * selection holds, once, in the order first selected. A row is let through when its own tuple is one of them:
 * text compared as the grid's value list compares it, without regard to case, every other value by its type and
 * value, and a Blank only to a Blank.
 *
 * It is the tuples, never a list of values per column: two selected rows (Rates, Jan) and (Credit, Feb) let
 * through no (Rates, Feb), which per-column lists would let in quietly (spine rule 1). It is not capped (spine
 * rule 5).
 */
export class LinkCondition {
    readonly #keys: ReadonlySet<string>;

    /** The columns compared. */
    readonly on: readonly string[];

    /** The tuples let through, each once, in the order first selected. */
    readonly values: readonly (readonly BoardValue[])[];

    /** The condition on `on` that lets through `values`, each a tuple in the order of `on`; a tuple given twice is
     * kept once. No column, or a tuple whose length is not the columns', is refused with a `RangeError`. */
    constructor(on: readonly string[], values: Iterable<readonly BoardValue[]>) {
        if (on.length === 0) throw new RangeError('A link compares at least one column.');
        this.on = Object.freeze([...on]);
        const keys = new Set<string>();
        const kept: (readonly BoardValue[])[] = [];
        for (const value of values) {
            if (value.length !== on.length) throw new RangeError(`A tuple holds one value per column; ${on.length} are compared.`);
            const key = tupleKey(value);
            if (keys.has(key)) continue;
            keys.add(key);
            kept.push(Object.freeze([...value]));
        }
        this.#keys = keys;
        this.values = Object.freeze(kept);
        Object.freeze(this);
    }

    /** Whether `other` compares the same columns and lets through the same tuples, in any order. */
    letsThroughAs(other: LinkCondition | undefined): boolean {
        if (other === undefined || other.on.length !== this.on.length || other.#keys.size !== this.#keys.size) return false;
        if (this.on.some((name, i) => other.on[i] !== name)) return false;
        for (const key of this.#keys) if (!other.#keys.has(key)) return false;
        return true;
    }

    /** Whether `tuple`, the values of {@link on} in their order, is let through. */
    matches(tuple: readonly BoardValue[]): boolean {
        return this.#keys.has(tupleKey(tuple));
    }
}

/** A tuple as one string, equal for two tuples the condition holds alike: text folded as OrdinalIgnoreCase folds
 * it, a Decimal by its canonical text, which a Snapshot hands out, every other value by its type and value. */
function tupleKey(tuple: readonly BoardValue[]): string {
    return tuple.map(valueKey).join('\u001f');
}

function valueKey(value: BoardValue): string {
    if (value === null) return 'n';
    switch (typeof value) {
        case 'string': return `t${foldCase(value).replaceAll('\\', '\\\\').replaceAll('\u001f', '\\u')}`;
        // C#'s double.Equals: a NaN is the same as a NaN, and 0 the same as -0, which String() writes alike.
        case 'number': return `d${String(value)}`;
        case 'bigint': return `i${value}`;
        case 'boolean': return value ? 'b1' : 'b0';
        default: return `m${decimalText(value)}`;
    }
}

/** The value of `column` at `row`. */
export function valueAt(column: SnapshotColumn, row: number): BoardValue {
    return column.valueAt(row);
}

/** The positions in `snapshot`'s order of the rows every one of `conditions` lets through. */
export function narrowedPositions(snapshot: Snapshot, conditions: readonly LinkCondition[]): number[] {
    const tests = conditions.map(condition => ({ condition, columns: condition.on.map(name => snapshot.column(name)) }));
    const kept: number[] = [];
    for (let row = 0; row < snapshot.rowCount; row++) {
        if (tests.every(({ condition, columns }) => condition.matches(columns.map(column => valueAt(column, row))))) kept.push(row);
    }
    return kept;
}

/** A Snapshot of the rows of `snapshot` at `positions`, in that order, its columns and version as they are. */
export function narrowedSnapshot(snapshot: Snapshot, positions: readonly number[]): Snapshot {
    return createSnapshot({
        version: snapshot.version,
        columns: snapshot.columns.map(column => copied(column, positions)),
    });
}

function copied(column: SnapshotColumn, positions: readonly number[]): SnapshotColumnInput {
    const named = { name: column.name, caption: column.caption };
    switch (column.kind) {
        case 'text': return { ...named, kind: column.kind, values: positions.map(p => column.valueAt(p)) };
        case 'decimal': return { ...named, kind: column.kind, values: positions.map(p => column.valueAt(p)) };
        case 'double': return { ...named, kind: column.kind, values: positions.map(p => column.valueAt(p)) };
        case 'integer': return { ...named, kind: column.kind, values: positions.map(p => column.valueAt(p)) };
        case 'date': return { ...named, kind: column.kind, values: positions.map(p => column.valueAt(p)) };
        case 'boolean': return { ...named, kind: column.kind, values: positions.map(p => column.valueAt(p)) };
        default: throw new Error(`Internal: ${String(column satisfies never)} is not a column.`);
    }
}
