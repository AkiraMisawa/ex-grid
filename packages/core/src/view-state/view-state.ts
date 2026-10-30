// The View State (CONTEXT.md) as one value, so that one call saves a user's view and one restores it
// (ADR-0207): which columns show, in which order, at which widths and whether each is pinned, and the sort
// and the filter. The grid holds none of it (ADR-0001); this is the Consumer's record of it, which
// GridViewStateJson writes as JSON and reads back. The C# core's GridViewState, ColumnViewState and
// FilterEquality, transcribed.

import type { SnapshotDecimal } from '@ex-grid/data';
import type { FilterClause, FilterSpec, GridFilter } from '../query/filter';
import type { SortSpec } from '../query/sort';
import type { GridQuery } from '../source/fetching-source';
import type { GridColumnWidth } from '../dom/grid-column';

/** A column's width as a View State keeps it: the intent, Auto or a Fixed number of pixels, never the width
 * an Auto column was painted at (ADR-0016). */
export type ColumnViewStateWidth = { readonly auto: true } | { readonly px: number };

/** One column of a View State: which column, named as the grid names it, its width and whether it is
 * pinned. Its place in the View State's list is its place in the column order (ADR-0207). */
export interface ColumnViewState {
    /** The column's name, as the grid's column names it; never empty. */
    readonly column: string;
    /** The width intent: Auto, or a Fixed, finite and positive number of pixels (ADR-0016). */
    readonly width: ColumnViewStateWidth;
    /** Whether the column is pinned: held against the Viewport's leading edge with the columns pinned before
     * it (ADR-0004). */
    readonly pinned: boolean;
}

/** The kind a DateTime operand names, as .NET's DateTimeKind does. It is kept, and does not take part in
 * equality: a DateTime compares by its wall-clock ticks (ADR-0023). */
export type ViewStateDateTimeKind = 'unspecified' | 'utc' | 'local';

/** The whole-number operand types of the View State's JSON, each the .NET type of that name. */
export type ViewStateIntegerType = 'int64' | 'int32' | 'int16' | 'sByte' | 'uInt64' | 'uInt32' | 'uInt16' | 'byte';

/**
 * A filter operand as the View State's JSON carries it: with its type, one of those the filter model admits
 * (ADR-0023, ADR-0207), so that reading never guesses one. A Decimal keeps the places it was written with; a
 * whole number is a `bigint` held to its type's range; a date is its ticks, 100 ns since 0001-01-01, as
 * `@ex-grid/data` holds one. A DateTimeOffset's ticks are its clock's, and its offset is in minutes.
 */
export type ViewStateOperand =
    | { readonly type: 'string'; readonly value: string }
    | { readonly type: 'boolean'; readonly value: boolean }
    | { readonly type: 'decimal'; readonly value: SnapshotDecimal }
    | { readonly type: 'double' | 'single'; readonly value: number }
    | { readonly type: ViewStateIntegerType; readonly value: bigint }
    | { readonly type: 'dateTime'; readonly ticks: bigint; readonly kind: ViewStateDateTimeKind }
    | { readonly type: 'dateOnly'; readonly ticks: bigint }
    | { readonly type: 'dateTimeOffset'; readonly ticks: bigint; readonly offsetMinutes: number };

/** What a View State is made of. */
export interface GridViewStateOptions {
    /** The columns shown, in display order, the pinned ones leading. A hidden column is one this leaves
     * out. */
    readonly columns: readonly ColumnViewState[];
    /** The sort, outermost first; empty for none (ADR-0001). */
    readonly sorts: readonly SortSpec[];
    /** The filter in force; absent for none (ADR-0002). Its operands are {@link ViewStateOperand}s where it
     * is to be written as JSON. */
    readonly filter?: GridFilter | undefined;
}

/** A column a grid is shown with, as {@link GridViewState.of} reads it: its name and its declared width. */
export interface ViewStateColumnSource {
    /** The column's name. */
    readonly name: string;
    /** The declared width; Auto where absent. */
    readonly width?: GridColumnWidth;
}

/**
 * The View State as one value (ADR-0207). The pinned columns are the leading ones (ADR-0004), and a pinned
 * column after one that is not, a column named twice, a column with no name and a width that is neither
 * Auto nor a finite, positive number of pixels are refused when one is made. Two View States are equal when
 * they hold the same view ({@link GridViewState.equals}), so one read back equals the one written.
 */
export class GridViewState {
    readonly #columns: readonly ColumnViewState[];
    readonly #sorts: readonly SortSpec[];
    readonly #filter: GridFilter | undefined;
    readonly #pinned: number;

    /** A View State of the columns, in display order, under the sorts and the filter. The lists are copied.
     * A column or a sort level that is missing, a column with no name, a column named twice and a pinned
     * column after one that is not are refused with a `TypeError`, and a width that is no width with a
     * `RangeError`. */
    constructor(options: GridViewStateOptions) {
        const named = new Set<string>();
        let pinned = 0;
        const columns = options.columns.map((given: ColumnViewState | null | undefined, i) => {
            if (given === null || given === undefined) throw new TypeError(`The View State's column ${i + 1} is null.`);
            const { column, width } = given;
            if (typeof column !== 'string' || column === '') throw new TypeError(`The View State's column ${i + 1} has no name.`);
            if (named.has(column)) throw new TypeError(`Column '${column}' appears twice in the View State; a column stands in one place.`);
            named.add(column);
            if (typeof given.pinned !== 'boolean') throw new TypeError(`Column '${column}': pinned is true or false.`);
            if (given.pinned && pinned < i) {
                throw new TypeError(`Column '${column}' is pinned after a column that is not; the pinned columns are the leading ones (ADR-0004).`);
            }
            if (given.pinned) pinned++;
            return Object.freeze({ column, width: widthOf(width, column), pinned: given.pinned });
        });
        options.sorts.forEach((sort: SortSpec | null | undefined, i) => {
            if (sort === null || sort === undefined) throw new TypeError(`The View State's sort level ${i + 1} is null.`);
        });
        this.#columns = Object.freeze(columns);
        this.#sorts = Object.freeze([...options.sorts]);
        this.#filter = options.filter;
        this.#pinned = pinned;
    }

    /**
     * The View State a grid is shown under: its columns, each with its declared width, the leading
     * `pinnedColumnCount` of them pinned, and the sort and the filter in force. A pinned count outside
     * `[0, columns.length]` is refused with a `RangeError`, as the grid refuses it (ADR-0004).
     */
    static of(
        columns: readonly ViewStateColumnSource[], pinnedColumnCount: number, sorts: readonly SortSpec[] = [], filter?: GridFilter,
    ): GridViewState {
        if (!Number.isInteger(pinnedColumnCount) || pinnedColumnCount < 0 || pinnedColumnCount > columns.length) {
            throw new RangeError(
                `PinnedColumnCount is the first N of ${columns.length} columns; there are not that many (ADR-0004). (pinnedColumnCount: ${pinnedColumnCount})`);
        }
        const shown = columns.map((column, i): ColumnViewState => ({
            column: column.name,
            width: declaredWidth(column.width, column.name),
            pinned: i < pinnedColumnCount,
        }));
        return new GridViewState(filter === undefined ? { columns: shown, sorts } : { columns: shown, sorts, filter });
    }

    /** The columns shown, in display order, the pinned ones leading. */
    get columns(): readonly ColumnViewState[] {
        return this.#columns;
    }

    /** The sort, outermost first; empty for none (ADR-0001). */
    get sorts(): readonly SortSpec[] {
        return this.#sorts;
    }

    /** The filter in force, or `undefined` for none (ADR-0002). */
    get filter(): GridFilter | undefined {
        return this.#filter;
    }

    /** How many leading columns are pinned: what the grid's pinned column count takes. */
    get pinnedColumnCount(): number {
        return this.#pinned;
    }

    /**
     * Whether `other` is the same View State, by what it holds rather than by which lists hold it (ADR-0207,
     * ST-4): the same columns in the same order, each with the same width intent and pinning; the same sort
     * levels in the same order; and the same filter ({@link sameGridFilter}).
     */
    equals(other: GridViewState): boolean {
        return this === other || (
            sameList(this.#columns, other.#columns, sameColumn)
            && sameList(this.#sorts, other.#sorts, sameSort)
            && sameGridFilter(this.#filter, other.#filter));
    }
}

function widthOf(width: ColumnViewStateWidth, column: string): ColumnViewStateWidth {
    if ('px' in width) return fixedWidth(width.px, column);
    // Checked as a value an untyped caller may pass: { auto: false } is no width.
    const auto: unknown = Reflect.get(width, 'auto');
    if (auto === true) return autoWidth;
    throw new TypeError(`Column '${column}': a width is { auto: true } or { px }.`);
}

const autoWidth: ColumnViewStateWidth = Object.freeze({ auto: true });

function fixedWidth(px: unknown, column: string): ColumnViewStateWidth {
    if (typeof px !== 'number' || !Number.isFinite(px) || px <= 0) {
        throw new RangeError(`Column '${column}': a Fixed width is a finite, positive number of pixels. (px: ${String(px)})`);
    }
    return Object.freeze({ px });
}

function declaredWidth(width: GridColumnWidth | undefined, column: string): ColumnViewStateWidth {
    if (width === undefined) return autoWidth;
    if (typeof width === 'number') return fixedWidth(width, column);
    if ('px' in width) return fixedWidth(width.px, column);
    const auto: unknown = Reflect.get(width, 'auto');
    if (auto === true) return autoWidth;
    throw new TypeError(`Column '${column}': a width is a number of pixels, { px, minPx, maxPx } or { auto: true, minPx, maxPx }.`);
}

function sameColumn(a: ColumnViewState, b: ColumnViewState): boolean {
    return a.column === b.column && a.pinned === b.pinned && sameWidth(a.width, b.width);
}

function sameWidth(a: ColumnViewStateWidth, b: ColumnViewStateWidth): boolean {
    return 'px' in a ? 'px' in b && a.px === b.px : !('px' in b);
}

function sameSort(a: SortSpec, b: SortSpec): boolean {
    return a.column === b.column && a.direction === b.direction;
}

/**
 * Whether two Queries ask for the same thing, as the C# core's `GridQuery.Equals` says (FN-13, ADR-0207): the
 * same range, the same sort levels in the same order, and the same filter ({@link sameGridFilter}). So a
 * Query read back from the View State's JSON is the same as the one written.
 */
export function sameGridQuery(a: GridQuery, b: GridQuery): boolean {
    return a === b || (
        a.range.start === b.range.start && a.range.count === b.range.count
        && sameList(a.sorts, b.sorts, sameSort)
        && sameGridFilter(a.filter, b.filter));
}

/**
 * Whether two filters are the same condition, by what they hold rather than by which objects hold it
 * (ADR-0207; FN-13, ST-4): the same columns, in any order, each with the same combinator and the same clauses
 * in the same order, each with the same operator and the same operands, and the same Opaque Filter. Two
 * {@link ViewStateOperand}s are the same when they are of the same type and equal by that type's own
 * equality, as the filter compares them (ADR-0023): a Decimal 1.50 and 1.5 are the same, a Decimal and a
 * Double of one value are not, a DateTime compares by its ticks whatever its kind, and a DateTimeOffset by
 * its instant. Any other operand, and the Opaque Filter, is the same only as an equal primitive or the same
 * object. `null` and an absent operand are one absence.
 */
export function sameGridFilter(a: GridFilter | undefined, b: GridFilter | undefined): boolean {
    if (a === undefined || b === undefined) return a === b;
    if (a === b) return true;
    const names = Object.keys(a.columns);
    if (names.length !== Object.keys(b.columns).length || !sameOperand(a.opaque, b.opaque)) return false;
    return names.every(name => Object.hasOwn(b.columns, name) && sameSpec(a.columns[name], b.columns[name]));
}

function sameSpec(a: FilterSpec | null | undefined, b: FilterSpec | null | undefined): boolean {
    if (isAbsent(a) || isAbsent(b)) return isAbsent(a) && isAbsent(b);
    return combinatorOf(a) === combinatorOf(b) && sameList(a.clauses, b.clauses, sameClause);
}

function sameClause(a: FilterClause | null, b: FilterClause | null): boolean {
    if (a === null || b === null) return a === b;
    return a.operator === b.operator && sameOperand(a.value, b.value) && sameOptionalList(a.values, b.values);
}

function sameOptionalList(a: readonly unknown[] | null | undefined, b: readonly unknown[] | null | undefined): boolean {
    if (isAbsent(a) || isAbsent(b)) return isAbsent(a) && isAbsent(b);
    return sameList(a, b, sameOperand);
}

function sameList<T>(a: readonly T[] | null | undefined, b: readonly T[] | null | undefined, same: (x: T, y: T) => boolean): boolean {
    if (isAbsent(a) || isAbsent(b)) return isAbsent(a) && isAbsent(b);
    return a.length === b.length && a.every((x, i) => same(x, b[i] as T)); // oxlint-disable-line typescript/no-unsafe-type-assertion -- the lengths are equal, so b holds an item at every index of a
}

/** Two operands of one type and one value, as the filter compares them (ADR-0023). */
function sameOperand(a: unknown, b: unknown): boolean {
    if (isAbsent(a) || isAbsent(b)) return isAbsent(a) && isAbsent(b);
    if (Object.is(a, b)) return true;
    if (!isTagged(a) || !isTagged(b) || a.type !== b.type) return false;
    switch (a.type) {
        case 'string':
        case 'boolean':
            return a.value === Reflect.get(b, 'value');
        case 'decimal':
            return sameDecimal(a.value, Reflect.get(b, 'value'));
        case 'double':
        case 'single': {
            const other: unknown = Reflect.get(b, 'value');
            // .NET's Equals: 0 and -0 are one value, and NaN is NaN.
            return a.value === other || (Number.isNaN(a.value) && Number.isNaN(other));
        }
        case 'int64': case 'int32': case 'int16': case 'sByte':
        case 'uInt64': case 'uInt32': case 'uInt16': case 'byte':
            return a.value === Reflect.get(b, 'value');
        case 'dateTime':
        case 'dateOnly':
            return a.ticks === Reflect.get(b, 'ticks');
        case 'dateTimeOffset': {
            const ticks: unknown = Reflect.get(b, 'ticks');
            const offset: unknown = Reflect.get(b, 'offsetMinutes');
            return typeof ticks === 'bigint' && typeof offset === 'number'
                && a.ticks - BigInt(a.offsetMinutes) * ticksPerMinute === ticks - BigInt(offset) * ticksPerMinute;
        }
        default:
            return false;
    }
}

const ticksPerMinute = 600_000_000n;

/** `null` or `undefined`: the one absence C#'s `null` is. */
function isAbsent(value: unknown): value is null | undefined {
    return value === null || value === undefined;
}

/** A spec's combinator, And where it names none. */
export function combinatorOf(spec: FilterSpec): unknown {
    return spec.combinator === undefined ? 'and' : spec.combinator;
}

function sameDecimal(a: SnapshotDecimal, b: unknown): boolean {
    if (typeof b !== 'object' || b === null) return false;
    const mantissa: unknown = Reflect.get(b, 'mantissa');
    const scale: unknown = Reflect.get(b, 'scale');
    if (typeof mantissa !== 'bigint' || typeof scale !== 'number') return false;
    return a.scale >= scale
        ? a.mantissa === mantissa * 10n ** BigInt(a.scale - scale)
        : a.mantissa * 10n ** BigInt(scale - a.scale) === mantissa;
}

/** Whether a value is shaped as a {@link ViewStateOperand}: an object whose `type` is one of the tags. Its
 * members are checked where it is written. */
export function isTagged(value: unknown): value is ViewStateOperand {
    if (typeof value !== 'object' || value === null) return false;
    const type: unknown = Reflect.get(value, 'type');
    return typeof type === 'string' && (operandTypes as readonly string[]).includes(type);
}

/** Every operand type, as the View State's operands name it. */
export const operandTypes: readonly ViewStateOperand['type'][] = Object.freeze([
    'string', 'boolean', 'decimal', 'double', 'single',
    'int64', 'int32', 'int16', 'sByte', 'uInt64', 'uInt32', 'uInt16', 'byte',
    'dateTime', 'dateOnly', 'dateTimeOffset',
] as const);
