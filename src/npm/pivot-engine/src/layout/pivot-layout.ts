// The Pivot Layout (ADR-0059): which Pivot Fields stand in which Areas, in what order, with each one's
// settings, and the report's form and totals. It is ExPivot's View State: it holds no value, it is
// serialisable (layout-json.ts), and the Consumer persists it. Frozen; every change is a new layout, which
// is also the change signal. The create functions fill every setting a caller leaves out with the C#
// engine's default, so a layout made here and one made in C# from the same choices are the same layout.

import { itemKeysEqual, pivotItemKey, type PivotItemKey } from '../items/item-key';
import type { PivotAggregation, PivotArea, PivotAxis, PivotReportForm, PivotShowValuesAs, PivotSortDirection } from './pivot-enums';

/** An Item order: by label, ascending or descending, or by a Value Field's value at each Item's total;
 * `(blank)` is last either way (ADR-0060). */
export interface PivotSort {
    /** Ascending or descending. */
    readonly direction: PivotSortDirection;
    /** The index in the layout's Values of the Value Field to order by; absent to order by label. */
    readonly byValue?: number;
}

/** A Pivot Field standing in Filters, Rows or Columns, with its settings, which travel with it when it
 * moves between those Areas (ADR-0060/0061). */
export interface PivotFieldPlacement {
    /** The Pivot Field's name. */
    readonly field: string;
    /** The Item order. */
    readonly sort: PivotSort;
    /** The Items the user unticked: every record carrying one is left out of the report, totals included. */
    readonly hiddenItems: readonly PivotItemKey[];
    /** Whether an outer Item of this field has a subtotal (Excel's Automatic) or none. */
    readonly subtotals: boolean;
    /** The field-wide collapse state: true when every Item is collapsed but the toggled ones. */
    readonly collapsed: boolean;
    /** The Items whose collapse state is the opposite of `collapsed`. */
    readonly toggledItems: readonly PivotItemKey[];
}

/** A Pivot Field standing in Values (ADR-0060). */
export interface PivotValueField {
    /** The Pivot Field's name. */
    readonly field: string;
    /** How the records at a cell are summarised. */
    readonly aggregation: PivotAggregation;
    /** A caption of the Value Field's own (Excel's Custom Name), or absent for `<Aggregation> of <field>`. */
    readonly caption?: string;
    /** How the aggregated values are shown. */
    readonly showValuesAs: PivotShowValuesAs;
    /** A .NET format string the values are shown in under the report's locale, or absent for General,
     * at most eleven characters besides a minus sign, or `0.00%` for a percentage. */
    readonly numberFormat?: string;
}

/** Which Pivot Fields stand in which Areas, and the report's form and totals (ADR-0059). */
export interface PivotLayout {
    /** The report filter's fields, in order. */
    readonly filters: readonly PivotFieldPlacement[];
    /** The row fields, outermost first. */
    readonly rows: readonly PivotFieldPlacement[];
    /** The column fields, outermost first. */
    readonly columns: readonly PivotFieldPlacement[];
    /** The Value Fields, in order. */
    readonly values: readonly PivotValueField[];
    /** Where Σ Values stands when there are two or more Value Fields. */
    readonly valuesAxis: PivotAxis;
    /** How the row labels are set out. */
    readonly form: PivotReportForm;
    /** Whether a subtotal is carried on its group row (Excel's default) or has a row of its own at the
     * bottom. The Tabular form always puts it at the bottom. */
    readonly subtotalsAtTop: boolean;
    /** Whether the `Grand Total` row is painted at the bottom: Excel's "grand totals for columns". */
    readonly grandTotalRow: boolean;
    /** Whether the `Grand Total` column is painted at the right: Excel's "grand totals for rows". */
    readonly grandTotalColumn: boolean;
    /** Whether every row of the Outline and Tabular forms repeats its outer Items' labels. */
    readonly repeatItemLabels: boolean;
}

/** A placement's settings, each its default when left out: by label ascending, nothing hidden, subtotals
 * Automatic, expanded. */
export interface PivotFieldPlacementInput {
    /** The Pivot Field's name. */
    readonly field: string;
    /** The Item order. */
    readonly sort?: PivotSort;
    /** The Items unticked. */
    readonly hiddenItems?: readonly PivotItemKey[];
    /** Whether an outer Item has a subtotal. */
    readonly subtotals?: boolean;
    /** The field-wide collapse state. */
    readonly collapsed?: boolean;
    /** The Items whose collapse state is the opposite of `collapsed`. */
    readonly toggledItems?: readonly PivotItemKey[];
}

/** A Value Field's settings, each its default when left out: Sum, the default caption, no calculation,
 * General. */
export interface PivotValueFieldInput {
    /** The Pivot Field's name. */
    readonly field: string;
    /** How the records at a cell are summarised. */
    readonly aggregation?: PivotAggregation;
    /** A caption of the Value Field's own. */
    readonly caption?: string;
    /** How the aggregated values are shown. */
    readonly showValuesAs?: PivotShowValuesAs;
    /** A .NET format string the values are shown in. */
    readonly numberFormat?: string;
}

/** A layout's settings, each its default when left out: no field anywhere, Σ Values in Columns, the
 * Compact form, subtotals at the top, both grand totals, labels not repeated. A placement or a Value Field
 * may be given by its field's name alone. */
export interface PivotLayoutInput {
    /** The report filter's fields. */
    readonly filters?: readonly (string | PivotFieldPlacementInput)[];
    /** The row fields, outermost first. */
    readonly rows?: readonly (string | PivotFieldPlacementInput)[];
    /** The column fields, outermost first. */
    readonly columns?: readonly (string | PivotFieldPlacementInput)[];
    /** The Value Fields. */
    readonly values?: readonly (string | PivotValueFieldInput)[];
    /** Where Σ Values stands. */
    readonly valuesAxis?: PivotAxis;
    /** How the row labels are set out. */
    readonly form?: PivotReportForm;
    /** Whether a subtotal is carried on its group row. */
    readonly subtotalsAtTop?: boolean;
    /** Whether the `Grand Total` row is painted. */
    readonly grandTotalRow?: boolean;
    /** Whether the `Grand Total` column is painted. */
    readonly grandTotalColumn?: boolean;
    /** Whether the Outline and Tabular forms repeat outer labels. */
    readonly repeatItemLabels?: boolean;
}

const ascending: PivotSort = Object.freeze({ direction: 'ascending' });

function fieldName(field: string): string {
    if (typeof field !== 'string' || field.length === 0) throw new TypeError('A placed field is named.');
    return field;
}

const sortDirections: readonly string[] = ['ascending', 'descending'];

/** A sort, checked and frozen. */
export function pivotSort(direction: PivotSortDirection = 'ascending', byValue?: number): PivotSort {
    if (!sortDirections.includes(direction)) throw new TypeError(`'${direction}' is not a sort direction.`);
    if (byValue === undefined) return direction === 'descending' ? Object.freeze({ direction }) : ascending;
    if (!Number.isInteger(byValue)) throw new TypeError(`A sort by value names a Value Field by its index, not ${byValue}.`);
    return Object.freeze({ direction, byValue });
}

/** Whether two sorts are one. */
export function sortsEqual(a: PivotSort, b: PivotSort): boolean {
    return a.direction === b.direction && a.byValue === b.byValue;
}

/** Whether two Value Fields are one, member by member, as the C# engine's record equality says. */
export function valueFieldsEqual(a: PivotValueField, b: PivotValueField): boolean {
    return a.field === b.field && a.aggregation === b.aggregation && a.caption === b.caption && a.showValuesAs === b.showValuesAs
        && a.numberFormat === b.numberFormat;
}

function keys(list: readonly PivotItemKey[] | undefined): readonly PivotItemKey[] {
    if (list === undefined) return Object.freeze([]);
    return Object.freeze(list.map(key => pivotItemKey(key.kind, key.value)));
}

/** A placement of the field, its settings checked and frozen. */
export function createPivotFieldPlacement(input: string | PivotFieldPlacementInput): PivotFieldPlacement {
    const { field, sort, hiddenItems, subtotals = true, collapsed = false, toggledItems }: PivotFieldPlacementInput =
        typeof input === 'string' ? { field: input } : input;
    return Object.freeze({
        field: fieldName(field),
        sort: sort === undefined ? ascending : pivotSort(sort.direction, sort.byValue),
        hiddenItems: keys(hiddenItems),
        subtotals,
        collapsed,
        toggledItems: keys(toggledItems),
    });
}

/** A Value Field of the field, its settings checked and frozen. */
export function createPivotValueField(input: string | PivotValueFieldInput): PivotValueField {
    const { field, aggregation = 'sum', showValuesAs = 'noCalculation', caption, numberFormat }: PivotValueFieldInput =
        typeof input === 'string' ? { field: input } : input;
    const value: { -readonly [K in keyof PivotValueField]: PivotValueField[K] } = { field: fieldName(field), aggregation, showValuesAs };
    if (caption !== undefined) value.caption = caption;
    if (numberFormat !== undefined) value.numberFormat = numberFormat;
    return Object.freeze(value);
}

/** A layout, every setting left out taking its default, and every placement and Value Field made by
 * {@link createPivotFieldPlacement} and {@link createPivotValueField}. */
export function createPivotLayout({
    filters = [], rows = [], columns = [], values = [], valuesAxis = 'columns', form = 'compact', subtotalsAtTop = true,
    grandTotalRow = true, grandTotalColumn = true, repeatItemLabels = false,
}: PivotLayoutInput = {}): PivotLayout {
    return Object.freeze({
        filters: Object.freeze(filters.map(createPivotFieldPlacement)),
        rows: Object.freeze(rows.map(createPivotFieldPlacement)),
        columns: Object.freeze(columns.map(createPivotFieldPlacement)),
        values: Object.freeze(values.map(createPivotValueField)),
        valuesAxis,
        form,
        subtotalsAtTop,
        grandTotalRow,
        grandTotalColumn,
        repeatItemLabels,
    });
}

/** The layout with some of its settings changed, as C#'s `with` makes a new record. */
export function withLayout(layout: PivotLayout, changes: Partial<PivotLayout>): PivotLayout {
    return Object.freeze({ ...layout, ...changes });
}

/** The placement with some of its settings changed. */
export function withPlacement(placement: PivotFieldPlacement, changes: Partial<PivotFieldPlacement>): PivotFieldPlacement {
    return Object.freeze({ ...placement, ...changes });
}

/** No field anywhere: the report is empty and asks for fields. */
export const emptyPivotLayout: PivotLayout = createPivotLayout();

/** Whether the layout places no field at all. */
export function isEmptyLayout(layout: PivotLayout): boolean {
    return layout.filters.length === 0 && layout.rows.length === 0 && layout.columns.length === 0 && layout.values.length === 0;
}

/** The placements of Filters, Rows or Columns. */
export function placementsIn(layout: PivotLayout, area: PivotArea): readonly PivotFieldPlacement[] {
    switch (area) {
        case 'filters': return layout.filters;
        case 'rows': return layout.rows;
        case 'columns': return layout.columns;
        case 'values': throw new TypeError('Values holds Value Fields; read the layout\'s values.');
        default: throw new RangeError(`Unknown PivotArea '${String(area satisfies never)}'.`);
    }
}

/** Where a field stands among Filters, Rows and Columns, or `undefined` when it stands in none of them. */
export function placementOf(layout: PivotLayout, field: string): { readonly area: PivotArea; readonly index: number } | undefined {
    for (const area of ['filters', 'columns', 'rows'] as const) {
        const index = placementsIn(layout, area).findIndex(placement => placement.field === field);
        if (index >= 0) return { area, index };
    }
    return undefined;
}

/** Whether the field stands anywhere, Values included: a ticked field. */
export function placesField(layout: PivotLayout, field: string): boolean {
    return placementOf(layout, field) !== undefined || layout.values.some(value => value.field === field);
}

/** Whether the placement's Item is collapsed. */
export function isItemCollapsed(placement: PivotFieldPlacement, item: PivotItemKey): boolean {
    return placement.collapsed !== placement.toggledItems.some(key => itemKeysEqual(key, item));
}
