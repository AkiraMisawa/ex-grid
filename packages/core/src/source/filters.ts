// A Filter as a Grid Source keeps it (ADR-0023): copied when it is applied, so the Consumer's own objects
// behind it can change without reaching the source, compared by value, so the same Filter applied again
// is no change, and without one column's own condition, for that column's value list (ADR-0009). A member
// written as `null`, as a Query that came through JSON writes an absent one, is absent here as it is to
// the engine.

import { dateIdentity } from '../query/dates';
import type { FilterClause, FilterSpec, GridFilter } from '../query/filter';
import { isBlank } from '../query/query-engine';

/** A frozen copy of the Filter, down to its value lists, with no member written as `null`. */
export function snapshotFilter(filter: GridFilter | undefined): GridFilter | undefined {
    if (filter === undefined) return undefined;
    const columns = Object.fromEntries(Object.entries(filter.columns).map(([name, spec]) => [name, snapshotSpec(spec)]));
    return Object.freeze({ ...filter, columns: Object.freeze(columns) });
}

function snapshotSpec(spec: FilterSpec): FilterSpec {
    const { combinator, clauses } = spec;
    const copied = Object.freeze(clauses.map(snapshotClause));
    return Object.freeze(isBlank(combinator) ? { clauses: copied } : { clauses: copied, combinator });
}

function snapshotClause(clause: FilterClause): FilterClause {
    const { values, ...rest } = clause;
    return Object.freeze(isBlank(values) ? rest : { ...rest, values: Object.freeze([...values]) });
}

/** Whether two Filters say the same: the same columns, combinators, operators and operands, and Opaque
 * Filters that hold the same data. */
export function filtersEqual(a: GridFilter | undefined, b: GridFilter | undefined): boolean {
    if (a === b) return true;
    if (a === undefined || b === undefined) return false;
    if (!sameData(a.opaque, b.opaque)) return false;
    const names = Object.keys(a.columns);
    if (names.length !== Object.keys(b.columns).length) return false;
    return names.every(name => {
        const x = a.columns[name];
        const y = b.columns[name];
        return x !== undefined && y !== undefined && specsEqual(x, y);
    });
}

function specsEqual(x: FilterSpec, y: FilterSpec): boolean {
    // A spec the engine refuses is never the one in force, so it is a change, and the engine says why.
    if (isBlank(x.clauses) || isBlank(y.clauses)) return false;
    const other = y.clauses;
    return combinatorOf(x) === combinatorOf(y) && x.clauses.length === other.length
        && x.clauses.every((clause, index) => {
            const that = other[index];
            return that !== undefined && clausesEqual(clause, that);
        });
}

function combinatorOf(spec: FilterSpec): string {
    return isBlank(spec.combinator) ? 'and' : spec.combinator;
}

function clausesEqual(x: FilterClause, y: FilterClause): boolean {
    if (x.operator !== y.operator || !sameValue(operandOf(x.value), operandOf(y.value))) return false;
    const { values: left } = x;
    const { values: right } = y;
    if (isBlank(left) || isBlank(right)) return isBlank(left) && isBlank(right);
    return left.length === right.length && left.every((value, index) => sameValue(value, right[index]));
}

/** An operand, `null` and `undefined` being the same absence, as they are to C#'s `null`. */
function operandOf(value: unknown): unknown {
    return isBlank(value) ? undefined : value;
}

/** The Filter without one column's own condition; `undefined` when nothing is left. */
export function filterWithout(filter: GridFilter | undefined, column: string): GridFilter | undefined {
    if (filter === undefined) return undefined;
    const remaining = Object.fromEntries(Object.entries(filter.columns).filter(([name]) => name !== column));
    if (Object.keys(remaining).length === 0 && filter.opaque === undefined) return undefined;
    return { ...filter, columns: remaining };
}

/** Two operands that are the same value: equal primitives, the same object, or dates C# holds equal. */
export function sameValue(a: unknown, b: unknown): boolean {
    if (Object.is(a, b) || (a === 0 && b === 0)) return true;
    const x = dateIdentity(a);
    return x !== undefined && x === dateIdentity(b);
}

/**
 * Two Opaque Filters that hold the same data: the same values, and arrays and plain objects whose members
 * hold the same data, as C# compares two records. An adapter that builds its Opaque Filter afresh on every
 * render therefore pushes no change, and does not render again for it (ADR-0023). Any other object is
 * itself only.
 */
function sameData(a: unknown, b: unknown): boolean {
    if (sameValue(a, b)) return true;
    if (Array.isArray(a) || Array.isArray(b)) {
        return Array.isArray(a) && Array.isArray(b) && a.length === b.length
            && a.every((value: unknown, index) => sameData(value, b[index]));
    }
    if (!isPlainObject(a) || !isPlainObject(b)) return false;
    const names = Object.keys(a);
    return names.length === Object.keys(b).length
        && names.every(name => Object.hasOwn(b, name) && sameData(Reflect.get(a, name), Reflect.get(b, name)));
}

function isPlainObject(value: unknown): value is object {
    if (typeof value !== 'object' || value === null) return false;
    const prototype: unknown = Object.getPrototypeOf(value);
    return prototype === Object.prototype || prototype === null;
}
