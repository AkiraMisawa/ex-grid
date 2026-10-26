// The closed sets of names a Pivot Layout and a report are written in (ADR-0059/0060/0061), spelt as
// the layout's JSON spells them (PivotLayoutJson, version 1), so a layout read from JSON is the layout.

/** The type a Consumer declares for a Pivot Field (ADR-0060). It decides the defaults only: where a
 * ticked field goes, which Aggregation a new Value Field takes. Each Item's kind comes from the data. */
export type PivotFieldType = 'text' | 'number' | 'date' | 'boolean';

/** The four places a Pivot Field stands, in Excel's words (ADR-0061). */
export type PivotArea = 'filters' | 'columns' | 'rows' | 'values';

/** Where Σ Values stands, when there are two or more Value Fields: innermost in Columns, Excel's default,
 * or in Rows (ADR-0060). */
export type PivotAxis = 'columns' | 'rows';

/** How a Value Field summarises the records at a cell: Excel's "Summarize Values By" (ADR-0060). */
export type PivotAggregation =
    | 'sum' | 'count' | 'average' | 'max' | 'min' | 'product' | 'countNumbers' | 'stdDev' | 'stdDevp' | 'var' | 'varp';

/** Every Aggregation, in the C# engine's order. */
export const pivotAggregations: readonly PivotAggregation[] = Object.freeze(
    ['sum', 'count', 'average', 'max', 'min', 'product', 'countNumbers', 'stdDev', 'stdDevp', 'var', 'varp']);

/** How a Value Field's values are shown after aggregation (ADR-0060). */
export type PivotShowValuesAs = 'noCalculation' | 'percentOfGrandTotal' | 'percentOfColumnTotal' | 'percentOfRowTotal';

/** Every Show Values As, in the C# engine's order: what Value Field Settings… offers. */
export const pivotShowValuesAsList: readonly PivotShowValuesAs[] = Object.freeze(
    ['noCalculation', 'percentOfGrandTotal', 'percentOfColumnTotal', 'percentOfRowTotal']);

/** How a report sets out its row labels: Excel's "Report Layout" (ADR-0060). */
export type PivotReportForm = 'compact' | 'outline' | 'tabular';

/** Ascending or descending, for an Item order; `(blank)` is last either way (ADR-0060). */
export type PivotSortDirection = 'ascending' | 'descending';

/** The kind of an Item, which comes from its value (ADR-0060). {@link pivotItemKinds} is the ascending
 * order across kinds. */
export type PivotItemKind = 'number' | 'date' | 'text' | 'boolean' | 'error' | 'blank';

/** Every Item kind, in the ascending order across kinds: numbers, dates, text, Booleans, `#NUM!`, then
 * `(blank)`. */
export const pivotItemKinds: readonly PivotItemKind[] = Object.freeze(['number', 'date', 'text', 'boolean', 'error', 'blank']);

/** A part of a Date column a Pivot Field can be declared as: its year, quarter or month (ADR-0060). */
export type PivotDatePart = 'year' | 'quarter' | 'month';

/** What a row of a report stands for (ADR-0059/0060): an Item of the innermost field, a group row, a
 * subtotal, or the grand total. */
export type PivotRowRole = 'item' | 'group' | 'subtotal' | 'grandTotal';

/** What a value column of a report stands for (ADR-0060). */
export type PivotColumnRole = 'item' | 'subtotal' | 'grandTotal';

/** The C# engine's name of an Aggregation, as its messages write it: `Sum`, `CountNumbers`, `StdDevp`. */
export function aggregationTitle(aggregation: PivotAggregation): string {
    return aggregation.charAt(0).toUpperCase() + aggregation.slice(1);
}

/** The C# engine's name of an Item kind, as a key's text and its messages write it: `Text`, `Number`. */
export function itemKindTitle(kind: PivotItemKind): string {
    return kind.charAt(0).toUpperCase() + kind.slice(1);
}

/** The order of an Item kind among the kinds, ascending. */
export function itemKindOrder(kind: PivotItemKind): number {
    return pivotItemKinds.indexOf(kind);
}
