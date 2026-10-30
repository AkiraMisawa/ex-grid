import type { ColumnType } from './columns';

// The Filter a Consumer applies (ADR-0009, ADR-0023): per column, clauses joined by And or Or, and the
// columns joined by And. The grid builds it and hands it over; it never runs it (spine rule 3), except in
// GridSource.from, the reference implementation.

/** A filter clause's operator (ADR-0023). There is no Between: it is And of two comparisons. */
export type FilterOperator =
    | 'equals' | 'notEquals'
    | 'greaterThan' | 'greaterThanOrEqual' | 'lessThan' | 'lessThanOrEqual'
    | 'contains' | 'doesNotContain' | 'startsWith' | 'endsWith'
    | 'in' | 'isBlank' | 'isNotBlank';

/** How a column's clauses combine (ADR-0009). Columns always combine with And. */
export type FilterCombinator = 'and' | 'or';

/** One condition on a column. `value` is the operand of a comparison or a text match; `values` the list of
 * `in`, where `null` stands for a Blank; `isBlank` and `isNotBlank` take neither. */
export interface FilterClause {
    /** What the clause tests. */
    readonly operator: FilterOperator;
    /** The operand of a comparison or a text match, held to the column's declared type (ADR-0023). */
    readonly value?: unknown;
    /** The list `in` matches, `null` standing for a Blank. */
    readonly values?: readonly unknown[];
}

/** One column's part of a Filter: its clauses, combined by `combinator`, And unless given. */
export interface FilterSpec {
    /** The column's conditions. */
    readonly clauses: readonly FilterClause[];
    /** How they combine; And unless given. */
    readonly combinator?: FilterCombinator;
}

/** The whole Filter: a spec per column, by name, and the Opaque Filter, whose meaning only the Consumer
 * knows and which the reference implementation ignores (ADR-0023). Two Opaque Filters are the same when
 * they hold the same data, arrays and plain objects compared member by member, so one built afresh on
 * every render is no change; any other object is the same only as itself. */
export interface GridFilter {
    /** Each filtered column's spec, by the column's name. */
    readonly columns: Readonly<Record<string, FilterSpec>>;
    /** The Opaque Filter, the Consumer's own. */
    readonly opaque?: unknown;
}

const textOperators: readonly FilterOperator[] = Object.freeze([
    'equals', 'notEquals', 'contains', 'doesNotContain', 'startsWith', 'endsWith', 'in', 'isBlank', 'isNotBlank',
] as const);
const comparableOperators: readonly FilterOperator[] = Object.freeze([
    'equals', 'notEquals', 'greaterThan', 'greaterThanOrEqual', 'lessThan', 'lessThanOrEqual', 'in', 'isBlank', 'isNotBlank',
] as const);
// `in` stays available: the value-list panel mode serialises as In on every column type, Boolean included
// (ADR-0009, ADR-0023).
const booleanOperators: readonly FilterOperator[] = Object.freeze(['equals', 'in', 'isBlank', 'isNotBlank'] as const);

/** The operators a column of `type` offers, which are the ones the reference implementation accepts on it
 * (ADR-0023). */
export function filterOperatorsFor(type: ColumnType): readonly FilterOperator[] {
    switch (type) {
        case 'text': return textOperators;
        case 'number':
        case 'date': return comparableOperators;
        case 'boolean': return booleanOperators;
        default: throw new RangeError(`A column's type is text, number, date or boolean, not ${JSON.stringify(type)}.`);
    }
}
