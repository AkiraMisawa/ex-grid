import { defined, elementAt, unreachable } from '../must';
import type { ColumnType, QueryColumn } from './columns';
import { filterOperatorsFor, type FilterClause, type FilterCombinator, type FilterOperator, type GridFilter } from './filter';
import { compareNumbers, numberIdentity, numberKey, numberRefusal, type NumberKey } from './numbers';
import type { SortDirection, SortSpec } from './sort';
import { compareIgnoringCase, foldCase } from './text-casing';

// The reference implementation of a Query's semantics (ADR-0001, ADR-0023): what GridSource.from does
// with a Filter and a Sort, and what a server-side Source is held to. The whole Query is checked before any
// row is read, so a malformed one is refused whatever the rows hold; every cell is then held to its
// column's declared type as it is read, whichever operator reads it.

/** A cell as it is compared: a string, a Number key or a boolean, or `null` for a Blank. */
type Key = string | NumberKey | boolean | null;

const combinators: readonly FilterCombinator[] = Object.freeze(['and', 'or'] as const);
const directions: readonly SortDirection[] = Object.freeze(['ascending', 'descending'] as const);

/** A row the Filter kept, where it stood, and its sort keys once read. */
interface Kept<Row> {
    readonly row: Row;
    readonly position: number;
}

interface PreparedClause {
    readonly operator: FilterOperator;
    readonly operand: Key;
    /** For `in`: the identities of the listed values, and whether a Blank is among them. */
    readonly set?: ReadonlySet<string>;
    readonly blankListed?: boolean;
}

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

/** Whether one row passes a Filter. */
export function matchesFilter<Row>(row: Row, columns: readonly QueryColumn<Row>[], filter: GridFilter): boolean {
    return matches(row, prepare(indexColumns(columns), filter));
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
    // An Action Column carries no value, so every row would compare equal and a sort would shuffle the
    // result into an order nobody could account for (ADR-0020).
    if (column.queryable === false) {
        throw new Error(`Column '${name}' carries actions only and has no value, so it cannot be sorted or filtered.`);
    }
    if (column.type === 'date') {
        // The Script Core's date values wait on the decision of their type (the API memo of 2026-10-25);
        // until then a Date column is refused rather than compared by a guess.
        throw new Error(`Column '${name}' is a Date column, which the Script Core does not sort or filter yet.`);
    }
    return column;
}

// ---- Filter -------------------------------------------------------------------------------------

function prepare<Row>(columns: ReadonlyMap<string, QueryColumn<Row>>, filter: GridFilter): PreparedColumn<Row>[] {
    // Columns combine with And (ADR-0009). The Opaque Filter is ignored (ADR-0023).
    return Object.entries(filter.columns).map(([name, spec]) => {
        const column = resolve(columns, name);
        if (spec.clauses.length === 0) {
            throw new Error(`The filter on column '${name}' has no clauses. Remove the column from the Filter instead.`);
        }
        const { combinator = 'and' } = spec;
        if (!combinators.includes(combinator)) {
            throw new Error(`The filter on column '${name}' has an unknown combinator (${JSON.stringify(combinator)}).`);
        }
        const clauses = spec.clauses.map(clause => prepareClause(column, clause));
        return { column, combinator, clauses };
    });
}

function prepareClause<Row>(column: QueryColumn<Row>, clause: FilterClause): PreparedClause {
    const { operator } = clause;
    if (!filterOperatorsFor(column.type).includes(operator)) {
        throw new Error(`Operator ${JSON.stringify(operator)} is not allowed on column '${column.name}' (${column.type}).`);
    }
    const wantsValues = operator === 'in';
    const wantsValue = !wantsValues && operator !== 'isBlank' && operator !== 'isNotBlank';
    if (wantsValues && (clause.values === undefined || clause.values.length === 0)) {
        throw new Error(`Operator in on column '${column.name}' requires a non-empty values list.`);
    }
    if (!wantsValues && clause.values !== undefined) {
        throw new Error(`Operator ${operator} on column '${column.name}' does not take a values list.`);
    }
    if (wantsValue && isBlank(clause.value)) {
        throw new Error(`Operator ${operator} on column '${column.name}' requires a value. To select Blanks, use isBlank.`);
    }
    if (!wantsValue && !isBlank(clause.value)) {
        throw new Error(`Operator ${operator} on column '${column.name}' does not take a value.`);
    }

    if (wantsValues) {
        const keys = defined(clause.values, 'the checked values list').map(value => keyOf(column, value, true));
        // A value list can hold tens of thousands of entries (ADR-0009): membership is a set built once.
        const set = new Set<string>();
        for (const key of keys) if (key !== null) set.add(identity(column.type, key));
        return { operator, operand: null, set, blankListed: keys.includes(null) };
    }
    return { operator, operand: wantsValue ? keyOf(column, clause.value, true) : null };
}

function isBlank(value: unknown): boolean {
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
    const { operator, operand } = clause;
    // A Blank matches only isBlank, and an in list that names a Blank (ADR-0023).
    if (cell === null) return operator === 'isBlank' || (operator === 'in' && clause.blankListed === true);
    switch (operator) {
        case 'isBlank': return false;
        case 'isNotBlank': return true;
        case 'in': return clause.set?.has(identity(type, cell)) === true;
        case 'equals': return compare(type, cell, operand) === 0;
        case 'notEquals': return compare(type, cell, operand) !== 0;
        case 'greaterThan': return compare(type, cell, operand) > 0;
        case 'greaterThanOrEqual': return compare(type, cell, operand) >= 0;
        case 'lessThan': return compare(type, cell, operand) < 0;
        case 'lessThanOrEqual': return compare(type, cell, operand) <= 0;
        case 'contains': return foldCase(text(cell)).includes(foldCase(text(operand)));
        case 'doesNotContain': return !foldCase(text(cell)).includes(foldCase(text(operand)));
        case 'startsWith': return foldCase(text(cell)).startsWith(foldCase(text(operand)));
        case 'endsWith': return foldCase(text(cell)).endsWith(foldCase(text(operand)));
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
        case 'text': return compareIgnoringCase(text(x), text(y));
        case 'number': return compareNumbers(number(x), number(y));
        case 'boolean': return Number(bool(x)) - Number(bool(y));
        case 'date': throw new Error('Internal: a Date column reached a comparison.');
        default: return unreachable(type);
    }
}

/** A key that is the same for two values exactly when they compare equal, for an `in` set. */
function identity(type: ColumnType, key: NonNullable<Key>): string {
    switch (type) {
        case 'text': return foldCase(text(key));
        case 'number': return numberIdentity(number(key));
        case 'boolean': return String(bool(key));
        case 'date': throw new Error('Internal: a Date column reached a value list.');
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
        case 'text': return typeof value === 'string' ? value : refuse('contradicts the declared type');
        case 'boolean': return typeof value === 'boolean' ? value : refuse('contradicts the declared type');
        case 'number': {
            if (typeof value !== 'number' && typeof value !== 'bigint') return refuse('contradicts the declared type');
            const refusal = numberRefusal(value);
            return refusal === undefined ? numberKey(value) : refuse(refusal);
        }
        case 'date': throw new Error('Internal: a Date column reached a value.');
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
        case 'object':
        case 'function':
        case 'symbol':
        case 'undefined': return `(${kind})`;
        default: return unreachable(kind);
    }
}

function text(key: Key): string {
    if (typeof key !== 'string') throw new Error('Internal: a text comparison read a key that is not text.');
    return key;
}

function number(key: Key): NumberKey {
    if (typeof key !== 'number' && typeof key !== 'bigint') throw new Error('Internal: a number comparison read a key that is not a number.');
    return key;
}

function bool(key: Key): boolean {
    if (typeof key !== 'boolean') throw new Error('Internal: a boolean comparison read a key that is not a boolean.');
    return key;
}
