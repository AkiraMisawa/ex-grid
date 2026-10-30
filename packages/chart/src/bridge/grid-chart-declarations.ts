// What the Consumer declares for a grid's selections to be charted (ADR-0251, ADR-0258).
import type { RowKey, RowKeyOf } from '@ex-grid/core';
import type { AxisScale, SeriesKind } from '../model';
import type { ChartOrientation } from './grid-chart';

/**
 * What the Consumer declares for a grid's selections to be charted (ADR-0251, ADR-0258). Nothing here is read
 * off a header or guessed from a selection's shape: "6m" is a header's text, 0.5 years is what the Consumer
 * says it stands for.
 */
export interface GridChartDeclarations<Row> {
    /** A row's identity (ADR-0140), which a chart follows through a sort, a filter and a recalculation. */
    readonly rowKey: RowKeyOf<Row>;
    /**
     * The row a Row Key names in the Consumer's data now, or `undefined` once it no longer exists. A chart reads
     * its values here, never from the grid's Window, so a row only filtered out of the grid is not reported as
     * gone (ADR-0258, M3-Q7).
     */
    readonly rowOf: (key: RowKey) => Row | undefined;
    /** Each column's Series Kind by its name (ADR-0253); `undefined` for none. A column with none declared is
     * refused when it is charted. */
    readonly kindOf?: (column: string) => SeriesKind | undefined;
    /**
     * Whether each column's values are read from zero, by its name (ADR-0253, ADR-0258): false for a level read
     * away from zero, such as rates between 3.0% and 3.6%; `undefined`, or no function, leaves the Series Kind's
     * default. A series in rows takes its columns' declaration; they must read zero alike once each is resolved
     * by its Series Kind's default (`undefined` beside true is alike for a Level), or the curve is refused by
     * name.
     */
    readonly fromZeroOf?: (column: string) => boolean | undefined;
    /** The number columns that are an axis, by name. A date column is always one; a number column is one only
     * when it is named here. None unless given. */
    readonly axisColumns?: ReadonlySet<string> | readonly string[];
    /** A column's declared position along a curve's axis (a tenor's years), or `undefined` for a column that has none. */
    readonly positionOf?: (column: string) => number | undefined;
    /** The title of the declared positions' axis, such as "Tenor (years)"; empty unless given. */
    readonly positionTitle?: string;
    /** Whether the declared positions are drawn proportional to their values or as categories (ADR-0251);
     * `proportional` unless given. */
    readonly positionScale?: AxisScale;
    /** A row's name in the legend and the tooltip, for series in rows; the Row Key's text unless given. */
    readonly rowName?: (row: Row) => string;
    /** The orientation every selection is charted in, when the Consumer fixes it; absent lets the grid's
     * declarations decide (ADR-0258). */
    readonly orientation?: ChartOrientation;
}
