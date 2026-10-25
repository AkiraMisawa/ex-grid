// A Filter as a Grid Source keeps it (ADR-0023): copied when it is applied, so the Consumer's own objects
// behind it can change without reaching the source, compared by value, so the same Filter applied again
// is no change, and without one column's own condition, for that column's value list (ADR-0009).

import type { FilterClause, FilterSpec, GridFilter } from '../query/filter';

/** A frozen copy of the Filter, down to its value lists. */
export function snapshotFilter(filter: GridFilter | undefined): GridFilter | undefined {
    if (filter === undefined) return undefined;
    const columns = Object.fromEntries(Object.entries(filter.columns).map(([name, spec]) => [name, snapshotSpec(spec)]));
    return Object.freeze({ ...filter, columns: Object.freeze(columns) });
}

function snapshotSpec(spec: FilterSpec): FilterSpec {
    return Object.freeze({ ...spec, clauses: Object.freeze(spec.clauses.map(snapshotClause)) });
}

function snapshotClause(clause: FilterClause): FilterClause {
    return Object.freeze(clause.values === undefined ? { ...clause } : { ...clause, values: Object.freeze([...clause.values]) });
}

/** Whether two Filters say the same: the same columns, combinators, operators and operands. */
export function filtersEqual(a: GridFilter | undefined, b: GridFilter | undefined): boolean {
    if (a === b) return true;
    if (a === undefined || b === undefined) return false;
    if (!sameValue(a.opaque, b.opaque)) return false;
    const names = Object.keys(a.columns);
    if (names.length !== Object.keys(b.columns).length) return false;
    return names.every(name => {
        const x = a.columns[name];
        const y = b.columns[name];
        return x !== undefined && y !== undefined && specsEqual(x, y);
    });
}

function specsEqual(x: FilterSpec, y: FilterSpec): boolean {
    const { combinator: left = 'and' } = x;
    const { combinator: right = 'and' } = y;
    return left === right && x.clauses.length === y.clauses.length
        && x.clauses.every((clause, index) => {
            const other = y.clauses[index];
            return other !== undefined && clausesEqual(clause, other);
        });
}

function clausesEqual(x: FilterClause, y: FilterClause): boolean {
    if (x.operator !== y.operator || !sameValue(x.value, y.value)) return false;
    if (x.values === undefined || y.values === undefined) return x.values === y.values;
    const other = y.values;
    return x.values.length === other.length && x.values.every((value, index) => sameValue(value, other[index]));
}

/** The Filter without one column's own condition; `undefined` when nothing is left. */
export function filterWithout(filter: GridFilter | undefined, column: string): GridFilter | undefined {
    if (filter === undefined) return undefined;
    const remaining = Object.fromEntries(Object.entries(filter.columns).filter(([name]) => name !== column));
    if (Object.keys(remaining).length === 0 && filter.opaque === undefined) return undefined;
    return { ...filter, columns: remaining };
}

/** Two operands that are the same value: equal primitives, the same object, or a date that names the
 * same date (two Temporal values are equal when their text is, a `Date` when its time is). */
export function sameValue(a: unknown, b: unknown): boolean {
    if (Object.is(a, b) || (a === 0 && b === 0)) return true;
    const x = dateIdentity(a);
    return x !== undefined && x === dateIdentity(b);
}

/** A text that is the same for two dates exactly when they are the same value, or `undefined` for a value
 * that is not a date. */
export function dateIdentity(value: unknown): string | undefined {
    if (typeof value !== 'object' || value === null) return undefined;
    const tag = Object.prototype.toString.call(value);
    if (tag === '[object Date]') {
        const getTime: unknown = Reflect.get(value, 'getTime');
        return typeof getTime === 'function' ? `${tag}${String(Reflect.apply(getTime, value, []))}` : undefined;
    }
    if (!tag.startsWith('[object Temporal.')) return undefined;
    const toString: unknown = Reflect.get(value, 'toString');
    return typeof toString === 'function' ? `${tag}${String(Reflect.apply(toString, value, []))}` : undefined;
}
