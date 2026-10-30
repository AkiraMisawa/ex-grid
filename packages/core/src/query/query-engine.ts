import { defined, elementAt, unreachable } from '../must';
import type { ColumnType, DateType, QueryColumn } from './columns';
import { dateKey, type DateKey } from './dates';
import { filterOperatorsFor, type FilterClause, type FilterCombinator, type FilterOperator, type GridFilter } from './filter';
import { compareNumbers, numberIdentity, numberKey, numberRefusal, type NumberKey } from './numbers';
import type { SortDirection, SortSpec } from './sort';
import { foldCase } from './text-casing';

// The reference implementation of a Query's semantics (ADR-0001, ADR-0023): what GridSource.from does
// with a Filter and a Sort, and what a server-side Source is held to. The whole Query is checked before any
// row is read, so a malformed one is refused whatever the rows hold; every cell is then held to its
// column's declared type as it is read, whichever operator reads it.

/** A cell as it is compared: a text folded as OrdinalIgnoreCase folds it, a Number key, a Date key or a
 * boolean, or `null` for a Blank. Each is read once, never again per comparison. */
type Key = string | NumberKey | DateKey | boolean | null;

const columnTypes: readonly ColumnType[] = Object.freeze(['text', 'number', 'date', 'boolean'] as const);
const dateTypes: readonly DateType[] = Object.freeze(['dateTime', 'dateOnly', 'dateTimeOffset'] as const);
const combinators: readonly FilterCombinator[] = Object.freeze(['and', 'or'] as const);
const directions: readonly SortDirection[] = Object.freeze(['ascending', 'descending'] as const);

/** A row the Filter kept, where it stood, and its sort keys once read. */
interface Kept<Row> {
    readonly row: Row;
    readonly position: number;
}

/** A clause checked once: `in` holds the identities of the listed values and whether a Blank is among
 * them, every other operator its operand. */
type PreparedClause =
    | { readonly operator: 'in'; readonly set: ReadonlySet<string>; readonly blankListed: boolean }
    | { readonly operator: Exclude<FilterOperator, 'in'>; readonly operand: Key };

interface PreparedColumn<Row> {
    readonly column: QueryColumn<Row>;
    readonly combinator: FilterCombinator;
    readonly clauses: readonly PreparedClause[];
}

interface Level<Row> {
    readonly column: QueryColumn<Row>;
    readonly direction: SortDirection;
}

/** The rows a Query keeps, in its order: filtered, then sorted stably, Blanks last in both directions. */
export function applyQuery<Row>(
    rows: readonly Row[], columns: readonly QueryColumn<Row>[], filter: GridFilter | undefined, sorts: readonly SortSpec[] | undefined,
): Row[] {
    return keptInOrder(rows, columns, filter, sorts).map(kept => kept.row);
}

/** The positions in `rows` of the rows a Query keeps, in its order. */
export function queryPositions<Row>(
    rows: readonly Row[], columns: readonly QueryColumn<Row>[], filter: GridFilter | undefined, sorts: readonly SortSpec[] | undefined,
): number[] {
    return keptInOrder(rows, columns, filter, sorts).map(kept => kept.position);
}

function keptInOrder<Row>(
    rows: readonly Row[], columns: readonly QueryColumn<Row>[], filter: GridFilter | undefined, sorts: readonly SortSpec[] | undefined,
): Kept<Row>[] {
    const byName = indexColumns(columns);
    const prepared = filter === undefined ? undefined : prepare(byName, filter);
    const levels = sorts === undefined || sorts.length === 0 ? undefined : levelsOf(byName, sorts);

    const kept: Kept<Row>[] = [];
    rows.forEach((row, position) => {
        if (prepared === undefined || matches(row, prepared)) kept.push({ row, position });
    });
    return levels === undefined ? kept : sortKept(kept, levels);
}

/** Whether a row passes a Filter, as a predicate: the Filter is checked and prepared once, here, so a
 * Source that tests rows one at a time (a row added, a row changed) does not prepare it per row. */
export function filterPredicate<Row>(columns: readonly QueryColumn<Row>[], filter: GridFilter): (row: Row) => boolean {
    const prepared = prepare(indexColumns(columns), filter);
    return row => matches(row, prepared);
}

/** A row's sort keys, one per level of a Sort, read and held to their columns' types once. */
export type SortKeys = readonly Key[];

/** The order a Sort puts rows in, as one comparison of a row with another's keys: the sort's own, level
 * by level, without the tie-break by position that makes the sort stable, so rows it calls equal tie and
 * the caller breaks the tie. What a live source merges its changed rows into the previous result with
 * (ADR-0141), so the merge and `applyQuery` order rows alike. */
export interface RowOrder<Row> {
    /** A row's keys, one per level. */
    keysOf(row: Row): SortKeys;
    /** Negative when `row` goes before the row whose keys these are, zero when they tie. The row's keys are
     * read level by level, only as far as the first that differs. */
    compare(row: Row, keys: SortKeys): number;
}

/** The order of `sorts`, checked whole before any row is read; no Sort puts every row level. */
export function rowOrder<Row>(columns: readonly QueryColumn<Row>[], sorts: readonly SortSpec[]): RowOrder<Row> {
    const levels = sorts.length === 0 ? [] : levelsOf(indexColumns(columns), sorts);
    return Object.freeze({
        keysOf: (row: Row) => Object.freeze(levels.map(({ column }) => keyOf(column, column.value(row), false))),
        compare: (row: Row, keys: SortKeys) => {
            for (const [level, { column, direction }] of levels.entries()) {
                const result = compareKeys(column.type, keyOf(column, column.value(row), false), elementAt(keys, level), direction);
                if (result !== 0) return result;
            }
            return 0;
        },
    });
}

function indexColumns<Row>(columns: readonly QueryColumn<Row>[]): ReadonlyMap<string, QueryColumn<Row>> {
    const byName = new Map<string, QueryColumn<Row>>();
    for (const column of columns) {
        if (byName.has(column.name)) throw new Error(`Two columns are named '${column.name}'.`);
        if (column.dateType !== undefined && column.type !== 'date') {
            throw new TypeError(`Column '${column.name}' (${column.type}) declares a date type; only a Date column holds dates (ADR-0023).`);
        }
        byName.set(column.name, column);
    }
    return byName;
}

function resolve<Row>(columns: ReadonlyMap<string, QueryColumn<Row>>, name: string): QueryColumn<Row> {
    const column = columns.get(name);
    if (column === undefined) throw new Error(`Unknown column '${name}'.`);
    // Checked here, before any row is read, so a type outside the vocabulary is refused whatever the rows.
    if (!columnTypes.includes(column.type)) {
        throw new RangeError(`Column '${name}' has an unknown type (${JSON.stringify(column.type)}).`);
    }
    if (column.type === 'date' && column.dateType !== undefined && !dateTypes.includes(column.dateType)) {
        throw new RangeError(`Column '${name}' has an unknown date type (${JSON.stringify(column.dateType)}).`);
    }
    // An Action Column carries no value, so every row would compare equal and a sort would shuffle the
    // result into an order nobody could account for (ADR-0020).
    if (column.queryable === false) {
        throw new Error(`Column '${name}' carries actions only and has no value, so it cannot be sorted or filtered.`);
    }
    return column;
}

// ---- Filter -------------------------------------------------------------------------------------

function prepare<Row>(columns: ReadonlyMap<string, QueryColumn<Row>>, filter: GridFilter): PreparedColumn<Row>[] {
    // Columns combine with And (ADR-0009). The Opaque Filter is ignored (ADR-0023).
    return Object.entries(filter.columns).map(([name, spec]) => {
        const column = resolve(columns, name);
        // A Query that came through JSON writes an absent member as null.
        const { clauses: written } = spec;
        if (isBlank(written) || written.length === 0) {
            throw new Error(`The filter on column '${name}' has no clauses. Remove the column from the Filter instead.`);
        }
        const combinator = isBlank(spec.combinator) ? 'and' : spec.combinator;
        if (!combinators.includes(combinator)) {
            throw new Error(`The filter on column '${name}' has an unknown combinator (${JSON.stringify(combinator)}).`);
        }
        const clauses = written.map(clause => prepareClause(column, clause));
        return { column, combinator, clauses };
    });
}

function prepareClause<Row>(column: QueryColumn<Row>, clause: FilterClause): PreparedClause {
    const { operator } = clause;
    if (!filterOperatorsFor(column.type).includes(operator)) {
        throw new Error(`Operator ${JSON.stringify(operator)} is not allowed on column '${column.name}' (${column.type}).`);
    }
    // A Query that came through JSON writes an absent member as null.
    const { values, value } = clause;
    if (operator === 'in') {
        if (isBlank(values) || values.length === 0) {
            throw new Error(`Operator in on column '${column.name}' requires a non-empty values list.`);
        }
        if (!isBlank(value)) throw new Error(`Operator in on column '${column.name}' does not take a value.`);
        const keys = values.map(listed => keyOf(column, listed, true));
        // A value list can hold tens of thousands of entries (ADR-0009): membership is a set built once.
        const set = new Set<string>();
        for (const key of keys) if (key !== null) set.add(identity(column.type, key));
        return { operator, set, blankListed: keys.includes(null) };
    }
    if (!isBlank(values)) {
        throw new Error(`Operator ${operator} on column '${column.name}' does not take a values list.`);
    }
    const wantsValue = operator !== 'isBlank' && operator !== 'isNotBlank';
    if (wantsValue && isBlank(value)) {
        throw new Error(`Operator ${operator} on column '${column.name}' requires a value. To select Blanks, use isBlank.`);
    }
    if (!wantsValue && !isBlank(value)) {
        throw new Error(`Operator ${operator} on column '${column.name}' does not take a value.`);
    }
    return { operator, operand: wantsValue ? keyOf(column, value, true) : null };
}

/** Absent, as a Query that came through JSON writes it: `undefined`, or `null`. */
export function isBlank(value: unknown): value is null | undefined {
    return value === undefined || value === null;
}

function matches<Row>(row: Row, prepared: readonly PreparedColumn<Row>[]): boolean {
    return prepared.every(({ column, combinator, clauses }) => {
        // Held to the declared type as it is read, not inside whichever operator compares, so the same data
        // is accepted or refused whatever the operator (ADR-0023).
        const cell = keyOf(column, column.value(row), false);
        return combinator === 'and'
            ? clauses.every(clause => matchesClause(column.type, cell, clause))
            : clauses.some(clause => matchesClause(column.type, cell, clause));
    });
}

function matchesClause(type: ColumnType, cell: Key, clause: PreparedClause): boolean {
    // A Blank matches only isBlank, and an in list that names a Blank (ADR-0023).
    if (clause.operator === 'in') return cell === null ? clause.blankListed : clause.set.has(identity(type, cell));
    const { operator, operand } = clause;
    if (cell === null) return operator === 'isBlank';
    switch (operator) {
        case 'isBlank': return false;
        case 'isNotBlank': return true;
        case 'equals': return compare(type, cell, operand) === 0;
        case 'notEquals': return compare(type, cell, operand) !== 0;
        case 'greaterThan': return compare(type, cell, operand) > 0;
        case 'greaterThanOrEqual': return compare(type, cell, operand) >= 0;
        case 'lessThan': return compare(type, cell, operand) < 0;
        case 'lessThanOrEqual': return compare(type, cell, operand) <= 0;
        case 'contains': return text(cell).includes(text(operand));
        case 'doesNotContain': return !text(cell).includes(text(operand));
        case 'startsWith': return text(cell).startsWith(text(operand));
        case 'endsWith': return text(cell).endsWith(text(operand));
        default: return unreachable(operator);
    }
}

// ---- Sort ---------------------------------------------------------------------------------------

function levelsOf<Row>(columns: ReadonlyMap<string, QueryColumn<Row>>, sorts: readonly SortSpec[]): Level<Row>[] {
    // The whole list is checked before any row is read: an unknown direction must not sort plausibly.
    return sorts.map(({ column, direction }) => {
        if (!directions.includes(direction)) {
            throw new Error(`Sort on column '${column}' has an unknown direction (${JSON.stringify(direction)}).`);
        }
        return { column: resolve(columns, column), direction };
    });
}

function sortKept<Row>(kept: readonly Kept<Row>[], levels: readonly Level<Row>[]): Kept<Row>[] {
    // Every key is read before the sort, so the declared-type refusal fires for the same Query and data
    // whatever ties with what (ADR-0023), and no comparison throws midway.
    const keyed = kept.map(entry => ({ entry, keys: levels.map(({ column }) => keyOf(column, column.value(entry.row), false)) }));
    // toSorted is stable, so equal keys keep the filtered order, which is the input order (ADR-0023).
    return keyed.toSorted((a, b) => {
        for (const [level, { column, direction }] of levels.entries()) {
            const result = compareKeys(column.type, elementAt(a.keys, level), elementAt(b.keys, level), direction);
            if (result !== 0) return result;
        }
        return 0;
    }).map(({ entry }) => entry);
}

function compareKeys(type: ColumnType, x: Key, y: Key, direction: SortDirection): number {
    // Blanks last in both directions (ADR-0023): outside the direction's inversion.
    if (x === null || y === null) return (x === null ? 1 : 0) - (y === null ? 1 : 0);
    const result = compare(type, x, y);
    return direction === 'ascending' ? result : -result;
}

// ---- Values -------------------------------------------------------------------------------------

function compare(type: ColumnType, x: Key, y: Key): number {
    switch (type) {
        case 'text': return compareFolded(text(x), text(y));
        case 'number': return compareNumbers(ordinal(x), ordinal(y));
        case 'boolean': return Number(bool(x)) - Number(bool(y));
        case 'date': return compareNumbers(ordinal(x), ordinal(y));
        default: return unreachable(type);
    }
}

/** A key that is the same for two values exactly when they compare equal, for an `in` set. */
function identity(type: ColumnType, key: NonNullable<Key>): string {
    switch (type) {
        case 'text': return text(key);
        case 'number': return numberIdentity(ordinal(key));
        case 'boolean': return String(bool(key));
        case 'date': return ordinal(key).toString();
        default: return unreachable(type);
    }
}

/** A cell or an operand held to its column's declared type (ADR-0002, ADR-0023). */
function keyOf<Row>(column: QueryColumn<Row>, value: unknown, isOperand: boolean): Key {
    if (isBlank(value)) return null;
    const refuse = (reason: string): never => {
        const role = isOperand ? 'the filter value' : 'the value accessor returned';
        throw new Error(`Column '${column.name}' (${column.type}): ${role} ${shown(value)} ${reason}.`);
    };
    switch (column.type) {
        case 'text': return typeof value === 'string' ? foldCase(value) : refuse('contradicts the declared type');
        case 'boolean': return typeof value === 'boolean' ? value : refuse('contradicts the declared type');
        case 'number': {
            if (typeof value !== 'number' && typeof value !== 'bigint') return refuse('contradicts the declared type');
            const key = numberKey(value);
            return key === undefined ? refuse(defined(numberRefusal(value), 'the reason a Number was refused')) : key;
        }
        case 'date': {
            const { dateType = 'dateTime' } = column;
            const key = dateKey(dateType, value);
            return typeof key === 'string' ? refuse(key) : key;
        }
        default: return unreachable(column.type);
    }
}

function shown(value: unknown): string {
    const kind = typeof value;
    switch (kind) {
        case 'string':
        case 'bigint':
        case 'number':
        case 'boolean': return `'${String(value)}' (${kind})`;
        // An object is named by its tag (a Temporal.PlainDate, a Date), as C# names a value's type.
        case 'object': return value === null ? '(null)' : `(${Object.prototype.toString.call(value).slice('[object '.length, -1)})`;
        case 'function':
        case 'symbol':
        case 'undefined': return `(${kind})`;
        default: return unreachable(kind);
    }
}

/** Orders two folded texts as OrdinalIgnoreCase orders them: by their UTF-16 code units. */
function compareFolded(x: string, y: string): number {
    if (x === y) return 0;
    return x < y ? -1 : 1;
}

function text(key: Key): string {
    if (typeof key !== 'string') throw new Error('Internal: a text comparison read a key that is not text.');
    return key;
}

/** A Number or Date key, both of which order on one line. */
function ordinal(key: Key): NumberKey {
    if (typeof key !== 'number' && typeof key !== 'bigint') throw new Error('Internal: a comparison read a key that is not ordinal.');
    return key;
}

function bool(key: Key): boolean {
    if (typeof key !== 'boolean') throw new Error('Internal: a boolean comparison read a key that is not a boolean.');
    return key;
}
