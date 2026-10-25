// A column as the Consumer declares it to a grid (ADR-0350 section 16): a plain object, read once per
// `update`, and resolved here into what the paint reads. The query semantics read the same object, so
// a column has one shape for sorting, filtering and painting.

import type { EditVerdict } from '../cells/edit-intent';
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

/** A Fixed width with its bounds (ADR-0016): `px` wide, and dragged no narrower than `minPx`. A MaxWidth
 * bounds only what the grid computes, Size to fit among it, so a width dragged past it stands as it came. */
export interface FixedColumnWidth {
    /** The width in CSS pixels; at least `minPx`. */
    readonly px: number;
    /** The narrowest a drag makes the column; 40 unless given. */
    readonly minPx?: number;
    /** The widest Size to fit makes it; 400 unless given. */
    readonly maxPx?: number;
}

/** A column's declared width: a Fixed number of pixels, a Fixed width with its bounds, or Auto (ADR-0016). */
export type GridColumnWidth = number | FixedColumnWidth | AutoColumnWidth;

/** A column of a grid: what the query semantics read, and what the grid paints. */
export interface GridColumn<Row> extends QueryColumn<Row> {
    /** The header's text; the column's name unless given. */
    readonly header?: string;
    /** The declared width; Auto between the default bounds unless given. */
    readonly width?: GridColumnWidth;
    /** How the column's cells align; by their type unless given (ADR-0006). */
    readonly align?: CellAlign;
    /** Whether typing and a paste may write this column's cells (ADR-0007, ADR-0035). Off unless given: this
     * is a display-first grid, and a cell written where nobody hears the intent would be written into
     * nothing, so a grid with no `onEdit` opens no Cell Editor, and one with no `onPaste` takes a paste on no
     * column. The grid never holds the value either way: a write leaves as an intent. */
    readonly editable?: boolean;
    /** The Consumer's verdict on a commit to this column, asked as it commits (ADR-0034): accept, flag or
     * reject. None means every commit is accepted. It runs on the commit's path, so it is cheap. */
    readonly validate?: (row: Row, text: string) => EditVerdict;
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
    /** Whether a paste may write its cells (ADR-0035). */
    readonly editable: boolean;
}

const aligns: ReadonlySet<string> = new Set<CellAlign>(['auto', 'left', 'center', 'right']);

/** Resolves the declared columns, refusing by name what no grid could paint honestly. */
export function resolveColumns<Row>(columns: readonly GridColumn<Row>[]): readonly ResolvedColumn<Row>[] {
    const given: unknown = columns;
    if (!Array.isArray(given)) throw new TypeError('The columns are an array of columns, in display order.');
    const names = new Set<string>();
    return Object.freeze(columns.map((column, index) => {
        // Checked as an untyped caller may hand it, where it arrives (CONVENTIONS.md).
        const { name, value, align, header, type, editable, validate }: {
            readonly name: unknown; readonly value: unknown; readonly align?: unknown; readonly header?: unknown; readonly type: unknown;
            readonly editable?: unknown; readonly validate?: unknown;
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
        if (editable !== undefined && typeof editable !== 'boolean') {
            throw new TypeError(`Column '${name}': editable is true or false.`);
        }
        if (validate !== undefined && typeof validate !== 'function') {
            throw new TypeError(`Column '${name}': validate is a function of the row and the text (ADR-0034).`);
        }
        return Object.freeze({
            column,
            header: header ?? name,
            spec: specOf(column),
            numeric: hashesWhenOverflowing(type),
            editable: editable === true,
        });
    }));
}

function specOf<Row>(column: GridColumn<Row>): ColumnWidthSpec {
    const { width } = column;
    if (width === undefined) {
        return new ColumnWidthSpec({ width: ColumnWidth.auto });
    }
    const bounded = typeof width === 'number' ? { px: width } : width;
    const { auto, px }: { readonly auto?: unknown; readonly px?: unknown } = bounded;
    const bounds = {
        ...('minPx' in bounded ? { minWidthPx: bounded.minPx } : {}),
        ...('maxPx' in bounded ? { maxWidthPx: bounded.maxPx } : {}),
    };
    if (auto === true && px === undefined) return new ColumnWidthSpec({ width: ColumnWidth.auto, ...bounds });
    if (auto !== undefined || typeof px !== 'number') {
        throw new TypeError(`Column '${column.name}': a width is a number of pixels, { px, minPx, maxPx } or { auto: true, minPx, maxPx }.`);
    }
    const spec = new ColumnWidthSpec({ width: ColumnWidth.fixed(px), ...bounds });
    if (px < spec.minWidthPx) {
        // Refused, not clamped (ADR-0016, FN-12): a column narrower than its MinWidth could not be
        // dragged back to the width it was declared at.
        throw new RangeError(`Column '${column.name}': a Fixed width of ${px} is below its MinWidth (${spec.minWidthPx}) — refused, not clamped (ADR-0016).`);
    }
    return spec;
}
