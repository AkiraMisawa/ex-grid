// Turns a grid selection into a GridChart, and a GridChart into a Chart Definition (ADR-0251, ADR-0258): the
// counterpart of C#'s SelectionReader. What it cannot make sense of it refuses by name, rather than drawing
// something plausible (spine rule 1). Nothing here is exported from the package: the bridge is its surface.
import type { GridColumn, GridHeaderGroup, RowKey, RowRange, SelectionRange } from '@ex-grid/core';
import { ChartDefinition } from '../definition';
import { dayNumber } from '../day-numbers';
import type { AppearanceKeys, ChartAxisOptions, ChartPoint, ChartSeriesOptions, ChartValue, SeriesKind } from '../model';
import { defined, elementAt, itemAt } from '../must';
import { ChartRefusedError, toNumber, writeObject } from '../values';
import type { ChartOrientation, ChartTarget, ColumnSeries, GridChart, GridChartSeries, KeyedPosition, RowSeries } from './grid-chart';
import type { GridChartDeclarations } from './grid-chart-declarations';

/** What the bridge reads of a grid: its columns, groups and order, and the rows at positions in it. */
export interface BridgeGridState<Row> {
    readonly columns: readonly GridColumn<Row>[];
    readonly groups: readonly GridHeaderGroup[];
    readonly rowSequenceVersion: number;
    readonly window: readonly Row[];
    readonly windowStart: number;
    /** The rows of a run outside the Window, as many as there are; absent where nothing can be fetched. */
    readonly fetch?: (range: RowRange) => Promise<readonly Row[]>;
    /** The source's Row Sequence Version as it stands now, read again once a fetch has answered. */
    readonly versionNow: () => number;
}

/** A selection read: the chart it makes, or the sentence that says why it makes none. */
export type SelectionReading =
    | { readonly chart: GridChart; readonly refusal?: never }
    | { readonly chart?: never; readonly refusal: string };

/** The refusal of a selection taken under another Row Sequence Version (ADR-0251, spine rule 6). */
export const stale = 'The rows were reordered after the selection was made. Select again.';

const refused = (refusal: string): SelectionReading => Object.freeze({ refusal });

function isNumber<Row>(c: GridColumn<Row>): boolean {
    // A Mark Column carries no value (`queryable: false`), as an Action Column does (ADR-0020, ADR-0043).
    return c.type === 'number' && c.queryable !== false && (c.actions === undefined || c.actions.length === 0);
}

function axisColumnsOf<Row>(d: GridChartDeclarations<Row>): ReadonlySet<string> {
    const { axisColumns = [] } = d;
    return axisColumns instanceof Set ? axisColumns : new Set(axisColumns);
}

function isAxis<Row>(c: GridColumn<Row>, d: GridChartDeclarations<Row>): boolean {
    return c.type === 'date' || (isNumber(c) && axisColumnsOf(d).has(c.name));
}

function positionOf<Row>(c: GridColumn<Row>, d: GridChartDeclarations<Row>): number | undefined {
    return isNumber(c) && d.positionOf !== undefined ? d.positionOf(c.name) : undefined;
}

function headerOf<Row>(c: GridColumn<Row>): string {
    return c.header ?? c.name;
}

function headers<Row>(columns: readonly GridColumn<Row>[]): string {
    return columns.map(headerOf).join(', ');
}

const range = (start: number, count: number): number[] => Array.from({ length: count }, (_, i) => start + i);

function distinctOrdered(values: readonly number[]): number[] {
    return [...new Set(values)].toSorted((a, b) => a - b);
}

/** The orientations the selection's columns declare, before any orientation the Consumer fixed. */
export function declaredOrientations<Row>(
    g: BridgeGridState<Row>, ranges: readonly SelectionRange[], d: GridChartDeclarations<Row>): readonly ChartOrientation[] {
    const covered = coveredColumns(g, whole(g, ranges));
    const found: ChartOrientation[] = [];
    if (covered.some(c => isAxis(c, d))) found.push('seriesInColumns');
    if (covered.some(c => positionOf(c, d) !== undefined)) found.push('seriesInRows');
    return found;
}

// One cell is Excel's "chart the table around it": every column of the Window (ADR-0251).
function isOneCell(ranges: readonly SelectionRange[]): boolean {
    return ranges.length === 1 && elementAt(ranges, 0).rowCount === 1 && elementAt(ranges, 0).columnCount === 1;
}

function whole<Row>(g: BridgeGridState<Row>, ranges: readonly SelectionRange[]): readonly SelectionRange[] {
    return isOneCell(ranges) && g.window.length > 0 && g.columns.length > 0
        ? [{ topRow: g.windowStart, leftColumn: 0, rowCount: g.window.length, columnCount: g.columns.length }]
        : ranges;
}

function coveredColumns<Row>(g: BridgeGridState<Row>, ranges: readonly SelectionRange[]): GridColumn<Row>[] {
    return distinctOrdered(ranges.flatMap(r => range(r.leftColumn, r.columnCount)).filter(i => i < g.columns.length))
        .map(i => elementAt(g.columns, i));
}

function isFirstTier(group: GridHeaderGroup): boolean {
    return group.tier === undefined || group.tier === 1;
}

// The Header Group a column stands under in the tier above the header row; empty for none, which is the
// contract of RowSeries.group and of a panel (ADR-0251).
function groupOf(groups: readonly GridHeaderGroup[], column: string): string {
    const group = groups.find(x => isFirstTier(x) && x.columns.includes(column));
    return group === undefined ? '' : group.label;
}

/**
 * Reads `selection`, taken under `version`, as `orientation`; into `target` when the series are added to a chart
 * already drawn (U7).
 */
export async function readSelection<Row>(
    g: BridgeGridState<Row>, selection: readonly SelectionRange[], version: number, d: GridChartDeclarations<Row>,
    orientation: ChartOrientation | undefined, target: ChartTarget | undefined): Promise<SelectionReading> {
    if (selection.length === 0) return refused('Nothing is selected.');
    // The gesture carries the order it was taken against; another order now means its positions name other
    // rows (spine rule 6).
    if (version !== g.rowSequenceVersion) return refused(stale);
    if (isOneCell(selection) && g.window.length === 0) return refused('The grid shows no rows to chart.');

    const oneCell = isOneCell(selection);
    const ranges = whole(g, selection);
    const covered = coveredColumns(g, ranges);
    const declared = declaredOrientations(g, ranges, d);

    let chosen: ChartOrientation;
    if (target !== undefined) {
        // An added series is read the target's way; a selection that declares only the other way is a curve
        // into a history, or a history into a curve, and is refused by name (ADR-0258).
        if (declared.length > 0 && !declared.includes(target.chart.orientation)) {
            return refused(target.chart.orientation === 'seriesInColumns'
                ? `The selection charts curves across declared positions, but '${target.name}' is drawn along ${axisHeader(g, target.chart)}.`
                : `The selection charts along ${headers(covered.filter(c => isAxis(c, d)))}, but '${target.name}' is drawn across declared positions.`);
        }
        chosen = target.chart.orientation;
    } else {
        const fixed = orientation ?? d.orientation ?? (declared.length === 1 ? elementAt(declared, 0) : undefined);
        if (fixed === undefined) {
            return refused(declared.length === 0
                ? 'The selection has no axis column (dates or numbers) and no columns with declared positions, so it has no horizontal axis.'
                : 'The selection can be charted with series in columns or in rows; choose one.');
        }
        chosen = fixed;
    }

    const positions = distinctOrdered(ranges.flatMap(r => range(r.topRow, r.rowCount)));
    const rows = await rowsAt(g, positions);
    if (rows === 'moved') return refused(stale);
    if (rows === undefined) return refused('The grid could not hand over every selected row; select fewer rows.');

    const made = chosen === 'seriesInColumns'
        ? columnsChart(g, ranges, covered, rows, d, oneCell, target)
        : rowsChart(g, ranges, covered, rows, d, oneCell, target);
    if (made.chart === undefined) return made;

    let chart = made.chart;
    if (target !== undefined) {
        chart = Object.freeze({ ...target.chart, series: Object.freeze([...target.chart.series, ...chart.series]) });
    }
    try {
        chart = ownPanelsForDiffs(g, chart, d, target === undefined ? 0 : target.chart.series.length);
        drawGridChart(g.columns, g.groups, chart, d);
    // oxlint-disable-next-line ex-grid/no-catch -- ADR-0258: a definition refused is the selection's refusal, said by name; anything else is rethrown
    } catch (error) {
        if (error instanceof ChartRefusedError) return refused(error.message);
        throw error;
    }
    return Object.freeze({ chart });
}

/** The axis column a chart of series in columns is drawn along; one that names none is the caller's defect. */
function axisColumnOf(chart: GridChart): string {
    if (chart.axisColumn === undefined) {
        throw new TypeError('A chart of series in columns names its axis column (axisColumn), as the bridge makes one (ADR-0258).');
    }
    return chart.axisColumn;
}

function axisHeader<Row>(g: BridgeGridState<Row>, chart: GridChart): string {
    const name = axisColumnOf(chart);
    const column = g.columns.find(c => c.name === name);
    return column === undefined ? name : headerOf(column);
}

// The rows at `positions`, from the Window or fetched; `undefined` where the source cannot hand them all over, and
// `moved` where the order moved while a fetch was out, so the rows fetched and the Window's stand under two
// orders and name rows the user never selected (spine rule 6).
async function rowsAt<Row>(g: BridgeGridState<Row>, positions: readonly number[]): Promise<HeldRows<Row> | 'moved' | undefined> {
    const rows = new Map<number, { readonly row: Row }>();
    const end = g.windowStart + g.window.length;
    for (const run of runs(positions)) {
        if (run.start >= g.windowStart && run.start + run.count <= end) {
            for (let i = 0; i < run.count; i++) rows.set(run.start + i, { row: itemAt(g.window, run.start + i - g.windowStart) });
            continue;
        }
        if (g.fetch === undefined) return undefined;
        const fetched = await g.fetch(run);
        if (g.versionNow() !== g.rowSequenceVersion) return 'moved';
        if (fetched.length < run.count) return undefined;
        for (let i = 0; i < run.count; i++) rows.set(run.start + i, { row: itemAt(fetched, i) });
    }
    return rows;
}

// The rows handed over, by position: each in a box, so a Consumer's row that is itself undefined is not a miss.
type HeldRows<Row> = ReadonlyMap<number, { readonly row: Row }>;

function rowAt<Row>(rows: HeldRows<Row>, position: number): Row {
    return defined(rows.get(position), `the row at ${position}`).row;
}

function runs(positions: readonly number[]): RowRange[] {
    const found: RowRange[] = [];
    for (let i = 0; i < positions.length;) {
        let j = i;
        while (j + 1 < positions.length && elementAt(positions, j + 1) === elementAt(positions, j) + 1) j++;
        found.push({ start: elementAt(positions, i), count: j - i + 1 });
        i = j + 1;
    }
    return found;
}

function columnsChart<Row>(
    g: BridgeGridState<Row>, ranges: readonly SelectionRange[], covered: readonly GridColumn<Row>[], rows: HeldRows<Row>,
    d: GridChartDeclarations<Row>, oneCell: boolean, target: ChartTarget | undefined): SelectionReading {
    const axes = covered.filter(c => isAxis(c, d));
    let axis: GridColumn<Row>;
    if (target !== undefined) {
        const name = axisColumnOf(target.chart);
        const found = g.columns.find(c => c.name === name);
        if (found === undefined) return refused(`'${target.name}' is drawn along ${name}, which is no longer in the grid.`);
        axis = found;
        const others = axes.filter(c => c.name !== found.name);
        if (others.length > 0) {
            return refused(`The selection charts along ${headers(others)}, but '${target.name}' is drawn along ${headerOf(found)}.`);
        }
    } else {
        if (axes.length === 0) return refused('The selection has no axis column (dates or numbers) to put along the horizontal axis.');
        if (axes.length > 1) return refused(`The selection has ${axes.length} axis columns (${headers(axes)}); choose one for the horizontal axis.`);
        axis = elementAt(axes, 0);
    }

    const others = covered.filter(c => c.name !== axis.name);
    const notNumbers = others.filter(c => !isNumber(c));
    if (notNumbers.length > 0 && !oneCell) return refused(`${headers(notNumbers)} cannot be plotted: not a number column.`);
    const figures = others.filter(c => isNumber(c));
    if (figures.length === 0) return refused(`The selection has only ${headerOf(axis)}; select the figures to plot beside it.`);

    const series: GridChartSeries[] = [];
    for (const column of figures) {
        if (target?.chart.series.some(s => s.type === 'column' && s.column === column.name) === true) {
            return refused(`${headerOf(column)} is already in '${target.name}'.`);
        }
        const index = g.columns.indexOf(column);
        const keyed: KeyedPosition[] = [];
        const seen = new Set<RowKey>();
        const at = distinctOrdered(ranges.filter(r => index >= r.leftColumn && index <= r.leftColumn + r.columnCount - 1)
            .flatMap(r => range(r.topRow, r.rowCount)));
        for (const position of at) {
            const row = rowAt(rows, position);
            const key = d.rowKey(row);
            if (seen.has(key)) continue;
            seen.add(key);
            const x = axisX(axis.value(row));
            if (x === undefined) return refused(`A selected row has no ${headerOf(axis)}, so it has no place along the axis.`);
            keyed.push(Object.freeze({ key, x }));
        }
        const made: ColumnSeries = Object.freeze({
            type: 'column', id: column.name, panel: groupOf(g.groups, column.name), column: column.name, rows: Object.freeze(keyed),
        });
        series.push(made);
    }
    return Object.freeze({ chart: Object.freeze({ orientation: 'seriesInColumns', axisColumn: axis.name, series: Object.freeze(series) }) });
}

function rowsChart<Row>(
    g: BridgeGridState<Row>, ranges: readonly SelectionRange[], covered: readonly GridColumn<Row>[], rows: HeldRows<Row>,
    d: GridChartDeclarations<Row>, oneCell: boolean, target: ChartTarget | undefined): SelectionReading {
    // A number column with no position was chosen and cannot be drawn on this axis: refused by name, not
    // dropped (ADR-0251). A text or date column names the row, and is not charted.
    const unpositioned = covered.filter(c => isNumber(c) && positionOf(c, d) === undefined);
    if (unpositioned.length > 0 && !oneCell) {
        return refused(`${headers(unpositioned)} has no declared position on the ${title(d)} axis, so it cannot be drawn here.`);
    }

    const all = g.columns.filter(c => positionOf(c, d) !== undefined);
    const chosen = covered.filter(c => positionOf(c, d) !== undefined);
    if (chosen.length === 0) return refused(`The selection covers no column with a position on the ${title(d)} axis.`);
    // A curve needs two points: a selection over one positioned column stands for its rows' whole curves.
    const wholeCurves = chosen.length <= 1;
    const groups = [...new Set(all.map(c => groupOf(g.groups, c.name)))];

    const series: GridChartSeries[] = [];
    const seen = new Set<RowKey>();
    const curves = new Set<string>();
    for (const position of distinctOrdered(ranges.flatMap(r => range(r.topRow, r.rowCount)))) {
        const row = rowAt(rows, position);
        const key = d.rowKey(row);
        if (seen.has(key)) continue;
        seen.add(key);
        const name = d.rowName === undefined ? writeObject(key) : d.rowName(row);
        for (const group of groups) {
            let inGroup = (wholeCurves ? all : chosen).filter(c => groupOf(g.groups, c.name) === group);
            if (inGroup.length === 0) continue;
            if (inGroup.length === 1 && !wholeCurves) inGroup = all.filter(c => groupOf(g.groups, c.name) === group);
            if (target?.chart.series.some(s => s.type === 'row' && s.rowKey === key && s.group === group) === true) {
                return refused(`${label(name, group)} is already in '${target.name}'.`);
            }
            // What makes a group one curve does not depend on the row: checked once a group.
            if (!curves.has(group)) {
                curves.add(group);
                const notOne = curveRefusal(inGroup, group, d);
                if (notOne !== undefined) return refused(notOne);
            }
            const ordered = inGroup
                .map(c => ({ name: c.name, at: definedPosition(c, d) }))
                .toSorted((a, b) => a.at - b.at)
                .map(c => c.name);
            const id = group.length === 0 ? writeObject(key) : `${writeObject(key)} · ${group}`;
            const made: RowSeries = Object.freeze({ type: 'row', id, panel: '', rowKey: key, name, group, columns: Object.freeze(ordered) });
            series.push(made);
        }
    }
    return Object.freeze({ chart: Object.freeze({ orientation: 'seriesInRows', series: Object.freeze(series) }) });
}

// A positioned column's position: only columns with one reach here.
function definedPosition<Row>(c: GridColumn<Row>, d: GridChartDeclarations<Row>): number {
    const at = positionOf(c, d);
    if (at === undefined) throw new Error(`Internal: column '${c.name}' was taken as positioned and has no position.`);
    return at;
}

// Why a group's columns are not one curve, or undefined: they mix Series Kinds, or read zero differently
// (ADR-0253, ADR-0258), each column's declaration resolved by its Series Kind's default.
function curveRefusal<Row>(columns: readonly GridColumn<Row>[], group: string, d: GridChartDeclarations<Row>): string | undefined {
    const name = group.length === 0 ? 'The positioned columns' : group;
    if (new Set(columns.map(c => kindOf(c.name, d))).size > 1) {
        return `${name} mix Series Kinds (${headers(columns)}), so they are not one curve.`;
    }
    const fromZero = columns.filter(c => reachesZero(c.name, d));
    const awayFromZero = columns.filter(c => !reachesZero(c.name, d));
    if (fromZero.length > 0 && awayFromZero.length > 0) {
        return `${name} are declared both from zero (${headers(fromZero)}) and away from it (${headers(awayFromZero)}), so they are not one curve.`;
    }
    return undefined;
}

// A column's Series Kind, or undefined where the Consumer declared none (C#'s SeriesKind.Undeclared).
function kindOf<Row>(column: string, d: GridChartDeclarations<Row>): SeriesKind | undefined {
    return d.kindOf === undefined ? undefined : d.kindOf(column);
}

function fromZeroOf<Row>(column: string, d: GridChartDeclarations<Row>): boolean | undefined {
    return d.fromZeroOf === undefined ? undefined : d.fromZeroOf(column);
}

// Whether a column's values are read from zero: its declaration, or its Series Kind's default (ADR-0253), as
// a ChartSeries resolves it; a kind not declared takes a Level's.
function reachesZero<Row>(column: string, d: GridChartDeclarations<Row>): boolean {
    const kind = kindOf(column, d);
    const fromZero = fromZeroOf(column, d);
    return kind === 'amountPerPeriod'
        || (kind === 'observation' || kind === 'priceRange' ? fromZero === true : fromZero !== false);
}

function title<Row>(d: GridChartDeclarations<Row>): string {
    const { positionTitle = '' } = d;
    return positionTitle.length === 0 ? 'positions\'' : positionTitle;
}

function label(name: string, group: string): string {
    return group.length === 0 ? name : `${name} · ${group}`;
}

// A series with negative values beside positive ones (a Diff) goes to a panel of its own by default, because
// aligning zeros for it costs the other series half the panel (ADR-0251). Decided once, when the series is
// made, so a recalculation never moves it between panels.
function ownPanelsForDiffs<Row>(g: BridgeGridState<Row>, chart: GridChart, d: GridChartDeclarations<Row>, kept: number): GridChart {
    const definition = drawGridChart(g.columns, g.groups, chart, d, false);
    const mixed = (i: number): boolean => {
        const values = elementAt(definition.series, i).points.flatMap(p => (p.y === null ? [] : [toNumber(p.y)]));
        return values.some(v => v < 0) && values.some(v => v > 0);
    };
    const series = [...chart.series];
    for (let i = kept; i < series.length; i++) {
        const panel = elementAt(series, i).panel;
        if (!mixed(i) || !series.some((s, j) => j !== i && s.panel === panel)) continue;
        series[i] = Object.freeze({ ...elementAt(series, i), panel: elementAt(definition.series, i).name });
    }
    return Object.freeze({ ...chart, series: Object.freeze(series) });
}

/** One series as the bridge draws it, before its Series Kind is checked: `undefined` where none is declared. */
export interface DrawnSeries {
    readonly id: string;
    readonly name: string;
    readonly kind: SeriesKind | undefined;
    readonly fromZero: boolean | undefined;
    readonly panel: string;
    readonly keys: AppearanceKeys;
    readonly points: readonly ChartPoint[];
    readonly format: ((value: unknown) => string) | undefined;
}

/** What a GridChart stands for now, before it is made a Chart Definition: what two drawings are compared by. */
export interface Drawing {
    readonly axis: ChartAxisOptions;
    readonly series: readonly DrawnSeries[];
    readonly notes: readonly string[];
}

/**
 * What `chart` stands for now: each value read by Row Key from the Consumer's data (ADR-0258), a row that no
 * longer exists a gap, and a Note that says how many.
 */
export function drawingOf<Row>(
    columns: readonly GridColumn<Row>[], groups: readonly GridHeaderGroup[], chart: GridChart, d: GridChartDeclarations<Row>): Drawing {
    const byName = new Map(columns.map(c => [c.name, c] as const));
    const gone = new Set<RowKey>();
    const missing: string[] = [];
    const rowOf = (key: RowKey): Row | undefined => {
        const row = d.rowOf(key);
        if (row === undefined) gone.add(key);
        return row;
    };

    let axis: ChartAxisOptions;
    const series: DrawnSeries[] = [];
    if (chart.orientation === 'seriesInColumns') {
        const name = axisColumnOf(chart);
        const x = byName.get(name);
        if (x === undefined) throw new ChartRefusedError(`The chart is drawn along ${name}, which is no longer in the grid.`);
        axis = { values: x.type === 'date' ? 'dates' : 'numbers', title: headerOf(x) };
        for (const s of chart.series) {
            if (s.type !== 'column') continue;
            const column = byName.get(s.column);
            if (column === undefined) missing.push(s.column);
            const points = s.rows.map((r): ChartPoint => {
                const row = rowOf(r.key);
                if (row === undefined) return Object.freeze({ x: r.x, y: null });
                return Object.freeze({ x: axisX(x.value(row)) ?? r.x, y: column === undefined ? null : valueOf(column.value(row)) });
            }).toSorted((a, b) => a.x - b.x);
            const path = groups.filter(gr => gr.columns.includes(s.column))
                .toSorted((a, b) => tierOf(b) - tierOf(a))
                .map(gr => gr.label);
            const header = column === undefined ? s.column : headerOf(column);
            const group = groupOf(groups, s.column);
            series.push(Object.freeze({
                id: s.id,
                name: [...path, header].join(' · '),
                kind: kindOf(s.column, d),
                fromZero: fromZeroOf(s.column, d),
                panel: s.panel,
                keys: Object.freeze(group.length === 0 ? { colour: header } : { colour: header, lineStyle: group }),
                points: Object.freeze(points),
                format: column?.format,
            }));
        }
    } else {
        const used = new Map<string, readonly [number, string]>();
        for (const s of chart.series) {
            if (s.type !== 'row') continue;
            for (const n of s.columns) {
                const c = byName.get(n);
                if (c === undefined) continue;
                const named = [declaredPosition(c.name, d), headerOf(c)] as const;
                used.set(`${named[0]}\u0000${named[1]}`, named);
            }
        }
        const { positionTitle = '', positionScale = 'proportional' } = d;
        axis = { values: 'numbers', title: positionTitle, scale: positionScale, named: [...used.values()].toSorted((a, b) => a[0] - b[0]) };
        for (const s of chart.series) {
            if (s.type !== 'row') continue;
            const row = rowOf(s.rowKey);
            const cols: GridColumn<Row>[] = [];
            for (const n of s.columns) {
                const c = byName.get(n);
                if (c === undefined) missing.push(n);
                else cols.push(c);
            }
            const points = cols
                .map((c): ChartPoint => Object.freeze({ x: declaredPosition(c.name, d), y: row === undefined ? null : valueOf(c.value(row)) }))
                .toSorted((a, b) => a.x - b.x);
            const name = row === undefined || d.rowName === undefined ? s.name : d.rowName(row);
            const first = cols[0];
            // Declarations are the Consumer's and may change after the chart was made: a curve whose columns no
            // longer read zero alike is refused, never drawn by one column's (ADR-0258).
            if (new Set(cols.map(c => reachesZero(c.name, d))).size > 1) {
                throw new ChartRefusedError(`${label(s.name, s.group)}: its columns are now declared both from zero and away from it, so they are not one curve.`);
            }
            series.push(Object.freeze({
                id: s.id,
                name: label(name, s.group),
                kind: first === undefined ? undefined : kindOf(first.name, d),
                fromZero: first === undefined ? undefined : fromZeroOf(first.name, d),
                panel: s.panel,
                keys: Object.freeze(s.group.length === 0 ? { colour: name } : { colour: name, lineStyle: s.group }),
                points: Object.freeze(points),
                format: first?.format,
            }));
        }
    }

    const notes: string[] = [];
    // A row that no longer exists is a gap, never zero, and the chart says so (ADR-0258, M3-Q3).
    if (gone.size === 1) notes.push('1 of the selected rows no longer exists.');
    else if (gone.size > 1) notes.push(`${gone.size} of the selected rows no longer exist.`);
    for (const name of new Set(missing)) notes.push(`${name} is no longer in the grid, so it is drawn without values.`);
    return Object.freeze({ axis, series: Object.freeze(series), notes: Object.freeze(notes) });
}

function tierOf(group: GridHeaderGroup): number {
    const { tier = 1 } = group;
    return tier;
}

// A column's declared position along a curve's axis. C#'s SelectionReader reads a position the Consumer no
// longer declares as 0 (`PositionOf?.Invoke(c.Name) ?? 0`); the two cores draw alike, and the bridge's
// report names it as a defect to settle rather than a rule (spine rule 1).
function declaredPosition<Row>(column: string, d: GridChartDeclarations<Row>): number {
    // oxlint-disable-next-line ex-grid/no-stand-in-default -- parity with C#'s SelectionReader.Draw, a reported defect (see above)
    return d.positionOf?.(column) ?? 0;
}

function seriesOptions(s: DrawnSeries, kind: SeriesKind): ChartSeriesOptions {
    return {
        id: s.id, name: s.name, kind, panel: s.panel, keys: s.keys, points: s.points,
        ...(s.fromZero === undefined ? {} : { fromZero: s.fromZero }),
        ...(s.format === undefined ? {} : { format: s.format }),
    };
}

/**
 * The Chart Definition a drawing stands for. Read with `validate` false, a series with no Series Kind is read
 * as a Level, to see its values' signs before any is checked; otherwise it is refused by name, as the
 * definition refuses one, after every series before it was checked.
 */
export function definitionOf(drawing: Drawing, validate: boolean): ChartDefinition {
    const undeclared = validate ? drawing.series.findIndex(s => s.kind === undefined) : -1;
    if (undeclared >= 0) {
        // The series before it are checked first, as the definition checks them in order; then it is refused
        // in the definition's own words (definition.ts).
        const before = drawing.series.slice(0, undeclared);
        // oxlint-disable-next-line eslint/no-new -- made only for what it refuses of the series before the undeclared one
        new ChartDefinition({ axis: drawing.axis, series: before.map(s => seriesOptions(s, kindOrLevel(s))), notes: drawing.notes });
        const s = elementAt(drawing.series, undeclared);
        if (before.some(b => b.id === s.id)) {
            throw new ChartRefusedError(`Two series are named '${s.id}'; each series needs an identity of its own.`);
        }
        throw new ChartRefusedError(`'${s.name}' declares no Series Kind: say whether it is a Level, a Rate over an interval or an Amount per period.`);
    }
    return new ChartDefinition({ axis: drawing.axis, series: drawing.series.map(s => seriesOptions(s, kindOrLevel(s))), notes: drawing.notes });
}

// Only reached for a series whose kind is declared, or where an undeclared one is read as a Level to see its
// signs (ADR-0251).
function kindOrLevel(s: DrawnSeries): SeriesKind {
    return s.kind === undefined ? 'level' : s.kind;
}

/** The Chart Definition `chart` stands for now (ADR-0258). */
export function drawGridChart<Row>(
    columns: readonly GridColumn<Row>[], groups: readonly GridHeaderGroup[], chart: GridChart, d: GridChartDeclarations<Row>,
    validate = true): ChartDefinition {
    return definitionOf(drawingOf(columns, groups, chart, d), validate);
}

/** Whether two drawings are the same: what a chart is handed again unchanged, so it skips its render (ADR-0252). */
export function sameDrawing(a: Drawing, b: Drawing): boolean {
    return sameAxis(a.axis, b.axis)
        && a.series.length === b.series.length && a.series.every((s, i) => sameSeries(s, elementAt(b.series, i)))
        && a.notes.length === b.notes.length && a.notes.every((n, i) => n === b.notes[i]);
}

function sameAxis(a: ChartAxisOptions, b: ChartAxisOptions): boolean {
    const an = a.named === undefined ? [] : a.named;
    const bn = b.named === undefined ? [] : b.named;
    return a.values === b.values && a.title === b.title && a.scale === b.scale
        && an.length === bn.length && an.every((n, i) => n[0] === elementAt(bn, i)[0] && n[1] === elementAt(bn, i)[1]);
}

function sameSeries(a: DrawnSeries, b: DrawnSeries): boolean {
    return a.id === b.id && a.name === b.name && a.kind === b.kind && a.fromZero === b.fromZero && a.panel === b.panel
        && a.keys.colour === b.keys.colour && a.keys.lineStyle === b.keys.lineStyle && a.format === b.format
        && a.points.length === b.points.length
        && a.points.every((p, i) => p.x === elementAt(b.points, i).x && p.y === elementAt(b.points, i).y);
}

/** A cell's value as a chart value, in the type it came in (ADR-0251); null for an empty or non-numeric value. */
export function valueOf(value: unknown): ChartValue | null {
    if (typeof value === 'bigint') return value;
    return typeof value === 'number' && Number.isFinite(value) ? value : null;
}

// The day number of 1970-01-01, and a day's length, for an instant read in UTC, as the grid shows one.
const epochDay = dayNumber(1970, 1, 1);
const msPerDay = 86_400_000;

/**
 * An axis cell's value as a position: a date's day number, or a number; `undefined` for one that has no place.
 * A `Temporal.PlainDate`, `PlainDateTime` or `ZonedDateTime` is its own ISO day, as C#'s `DateOnly`, `DateTime`
 * and `DateTimeOffset` are; a `Temporal.Instant` or a `Date` holds no offset, so it is its UTC day, the day the
 * grid shows it on (display-text.ts).
 */
export function axisX(value: unknown): number | undefined {
    if (typeof value === 'object' && value !== null) {
        switch (Object.prototype.toString.call(value)) {
            case '[object Temporal.PlainDate]':
            case '[object Temporal.PlainDateTime]':
            case '[object Temporal.ZonedDateTime]':
                return isoDayNumber(value);
            case '[object Temporal.Instant]':
                return utcDayNumber(Reflect.get(value, 'epochMilliseconds'));
            case '[object Date]':
                return value instanceof Date ? utcDayNumber(value.getTime()) : undefined;
            default:
                return undefined;
        }
    }
    const v = valueOf(value);
    return v === null ? undefined : toNumber(v);
}

function isoDayNumber(date: object): number | undefined {
    const iso = Reflect.get(date, 'calendarId') === 'iso8601' ? date : inIsoCalendar(date);
    if (iso === undefined) return undefined;
    const [year, month, day] = ['year', 'month', 'day'].map((name): unknown => Reflect.get(iso, name));
    return typeof year === 'number' && typeof month === 'number' && typeof day === 'number'
        && Number.isSafeInteger(year) && Number.isSafeInteger(month) && Number.isSafeInteger(day)
        ? dayNumber(year, month, day)
        : undefined;
}

function inIsoCalendar(date: object): object | undefined {
    const convert: unknown = Reflect.get(date, 'withCalendar');
    if (typeof convert !== 'function') return undefined;
    const iso: unknown = convert.call(date, 'iso8601');
    return typeof iso === 'object' && iso !== null ? iso : undefined;
}

function utcDayNumber(milliseconds: unknown): number | undefined {
    return typeof milliseconds === 'number' && Number.isFinite(milliseconds) ? Math.floor(milliseconds / msPerDay) + epochDay : undefined;
}
