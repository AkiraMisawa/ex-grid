// A chart made from a grid selection (ADR-0258): the set of Row Keys and columns it was made from, never the
// positions it was painted at. The C# records are plain frozen data here, as the rest of this package's
// vocabulary is (model.ts).
import type { RowKey } from '@ex-grid/core';

/**
 * Whether a selection's series run down its columns or along its rows (ADR-0258). It comes from the grid's
 * declarations, never from the selection's shape: `seriesInColumns` is a history, each column a series along
 * an axis column (dates or numbers); `seriesInRows` is a curve, each row a series across columns at declared
 * positions (tenors).
 */
export type ChartOrientation = 'seriesInColumns' | 'seriesInRows';

/** A row a series was made from, by its Row Key, with the axis value it had then. */
export interface KeyedPosition {
    /** The row's Row Key (ADR-0140). */
    readonly key: RowKey;
    /** The row's axis value when the chart was made, which places its gap once the row is gone. */
    readonly x: number;
}

/** A column, by its name, at the rows it was made from (series in columns). */
export interface ColumnSeries {
    /** Says the series is a column. */
    readonly type: 'column';
    /** The series' identity in the Chart Definition: the column's name. */
    readonly id: string;
    /** The panel it was given when it was made: its Header Group, or a panel of its own for a Diff (ADR-0251). */
    readonly panel: string;
    /** The column's name. */
    readonly column: string;
    /** The rows it is charted at, by Row Key: its own, as every Excel series has its own ranges (U8). */
    readonly rows: readonly KeyedPosition[];
}

/** A row, by its Row Key, across columns at declared positions (series in rows). */
export interface RowSeries {
    /** Says the series is a row. */
    readonly type: 'row';
    /** The series' identity in the Chart Definition: the row's key and its Header Group. */
    readonly id: string;
    /** The panel it was given when it was made. */
    readonly panel: string;
    /** The row's Row Key (ADR-0140). */
    readonly rowKey: RowKey;
    /** The row's name when the chart was made, kept for the legend once the row is gone. */
    readonly name: string;
    /** The Header Group the columns stand under, or empty. */
    readonly group: string;
    /** The columns it is charted across, by name, in their positions' order. */
    readonly columns: readonly string[];
}

/** One series of a {@link GridChart}, in the data's terms (ADR-0251). */
export type GridChartSeries = ColumnSeries | RowSeries;

/**
 * A chart made from a grid selection: the set of Row Keys and columns it was made from, never the positions
 * it was painted at (ADR-0258). A recalculated value is redrawn, a row that no longer exists becomes a gap,
 * and a new row is never added. Hold it as it was handed over: the bridge knows a chart it drew before by
 * its identity.
 */
export interface GridChart {
    /** Whether its series are columns or rows. */
    readonly orientation: ChartOrientation;
    /** The axis column of a chart of series in columns; absent for series in rows, whose axis is the declared positions. */
    readonly axisColumn?: string;
    /** Its series, in the order they were made and added. */
    readonly series: readonly GridChartSeries[];
}

/** A chart the user registered as a target of "Add to chart" (ADR-0258, U7). */
export interface ChartTarget {
    /** The chart's name, as the menu item says it. */
    readonly name: string;
    /** The chart as it stands. */
    readonly chart: GridChart;
}

/** Where a click on a chart's point lands in the grid (ADR-0258, M3-Q6). */
export interface GridCell {
    /** The point's row, by its Row Key. */
    readonly rowKey: RowKey;
    /** The point's column, by its name. */
    readonly column: string;
}

/** Every Row Key some series of `chart` is charted at. */
export function gridChartRowKeys(chart: GridChart): ReadonlySet<RowKey> {
    const keys = new Set<RowKey>();
    for (const s of chart.series) {
        if (s.type === 'column') {
            for (const r of s.rows) keys.add(r.key);
        } else {
            keys.add(s.rowKey);
        }
    }
    return keys;
}
