// A column as the Consumer declares it to a grid (ADR-0350 section 16): a plain object, read once per
// `update`, and resolved here into what the paint reads. The query semantics read the same object, so
// a column has one shape for sorting, filtering and painting.

import type { CellAlign } from '../paint/cell-classes';
import { ColumnWidth, ColumnWidthSpec } from '../columns/column-width';
import { hashesWhenOverflowing } from '../columns/overflow-rules';
import type { QueryColumn } from '../query/columns';

/** An Auto width with its bounds (ADR-0016): computed from what is painted, between `minPx` and `maxPx`. */
export interface AutoColumnWidth {
    /** Says the width is computed from content. */
    readonly auto: true;
    /** The narrowest the column stands, and the narrowest a drag makes it; 40 unless given. */
    readonly minPx?: number;
    /** The widest the grid computes it, past which a number shows `####`; 400 unless given. */
    readonly maxPx?: number;
}

/** A column's declared width: a Fixed number of pixels, or Auto (ADR-0016). */
export type GridColumnWidth = number | AutoColumnWidth;

/** A column of a grid: what the query semantics read, and what the grid paints. */
export interface GridColumn<Row> extends QueryColumn<Row> {
    /** The header's text; the column's name unless given. */
    readonly header?: string;
    /** The declared width; Auto between the default bounds unless given. */
    readonly width?: GridColumnWidth;
    /** How the column's cells align; by their type unless given (ADR-0006). */
    readonly align?: CellAlign;
}

/** A column with what its declaration resolves to, checked once, where the Consumer handed it over. */
export interface ResolvedColumn<Row> {
    /** The column as declared. */
    readonly column: GridColumn<Row>;
    /** The header's text. */
    readonly header: string;
    /** The width and its bounds. */
    readonly spec: ColumnWidthSpec;
    /** Whether its cells are a numeric presentation, `####` when too narrow (ADR-0016). */
    readonly numeric: boolean;
}

const aligns: ReadonlySet<string> = new Set<CellAlign>(['auto', 'left', 'center', 'right']);

/** Resolves the declared columns, refusing by name what no grid could paint honestly. */
export function resolveColumns<Row>(columns: readonly GridColumn<Row>[]): readonly ResolvedColumn<Row>[] {
    const given: unknown = columns;
    if (!Array.isArray(given)) throw new TypeError('The columns are an array of columns, in display order.');
    const names = new Set<string>();
    return Object.freeze(columns.map((column, index) => {
        // Checked as an untyped caller may hand it, where it arrives (CONVENTIONS.md).
        const { name, value, align, header, type }: {
            readonly name: unknown; readonly value: unknown; readonly align?: unknown; readonly header?: unknown; readonly type: unknown;
        } = column;
        if (typeof name !== 'string' || name === '') {
            throw new TypeError(`The column at ${index} has no name: a column is named by a non-empty string.`);
        }
        if (names.has(name)) {
            throw new TypeError(`Two columns are named '${name}': a Sort, a Filter and the grid's state name a column by it.`);
        }
        names.add(name);
        if (typeof value !== 'function') {
            throw new TypeError(`Column '${name}' has no value: a column reads its value with a function of the row.`);
        }
        if (type !== 'text' && type !== 'number' && type !== 'date' && type !== 'boolean') {
            throw new TypeError(`Column '${name}': a type is text, number, date or boolean, not ${String(type)} (ADR-0002).`);
        }
        if (align !== undefined && (typeof align !== 'string' || !aligns.has(align))) {
            throw new RangeError(`Column '${name}': align is one of ${[...aligns].join(', ')}, not ${JSON.stringify(align)}.`);
        }
        if (header !== undefined && typeof header !== 'string') {
            throw new TypeError(`Column '${name}': a header is a string.`);
        }
        return Object.freeze({
            column,
            header: header ?? name,
            spec: specOf(column),
            numeric: hashesWhenOverflowing(type),
        });
    }));
}

function specOf<Row>(column: GridColumn<Row>): ColumnWidthSpec {
    const { width } = column;
    if (width === undefined) {
        return new ColumnWidthSpec({ width: ColumnWidth.auto });
    }
    if (typeof width === 'number') {
        const spec = new ColumnWidthSpec({ width: ColumnWidth.fixed(width) });
        if (width < spec.minWidthPx) {
            // Refused, not clamped (ADR-0016, FN-12): a column narrower than its MinWidth could not be
            // dragged back to the width it was declared at.
            throw new RangeError(`Column '${column.name}': a Fixed width of ${width} is below its MinWidth (${spec.minWidthPx}) — refused, not clamped (ADR-0016).`);
        }
        return spec;
    }
    const { auto }: { readonly auto: unknown } = width;
    if (auto !== true) {
        throw new TypeError(`Column '${column.name}': a width is a number of pixels or { auto: true, minPx, maxPx }.`);
    }
    return new ColumnWidthSpec({
        width: ColumnWidth.auto,
        ...(width.minPx === undefined ? {} : { minWidthPx: width.minPx }),
        ...(width.maxPx === undefined ? {} : { maxWidthPx: width.maxPx }),
    });
}
