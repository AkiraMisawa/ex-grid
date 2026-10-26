// The Chart Spec (ADR-0320): a chart said in JSON, as an AI writes it and as a Board holds it as one of its
// views (ADR-0340). It names Data Handles and their columns and never holds a number; each series says its
// Series Kind; what cannot be drawn is refused with the path and the reason. The port of the C# engine's
// `ChartSpec.Read`, held to it by the shared cases under tests/cases/chart/ (ADR-0370, sections 2 and 3):
// the same refusals, at the same paths, in the same words, checked in the same order.
import {
    JsonNumber, JsonTextError, decimalText, isJsonArray, isJsonObject, jsonMember, jsonMemberNames, jsonRawText, readJsonText, ticksOfTemporal,
    type JsonObject, type JsonValue, type SnapshotDecimal,
} from '@ex-grid/data';
import type { ChartData } from './chart-data';
import { dateOf, isoDate } from './day-numbers';
import { ChartDefinition } from './definition';
import { roundTrip } from './invariant-text';
import type {
    AxisScale, AxisSide, AxisValues, ChartAxisOptions, ChartEdge, ChartPoint, ChartSeriesOptions, ChartValue, IntervalBound, IntervalStyle,
    PointColour, PointDetail, PriceBar, SeriesInterval, SeriesKind, StepSide,
} from './model';
import { elementAt } from './must';
import { SurfaceDefinition, type SurfaceNode, type SurfaceTriangle, type SurfaceValue } from './surface';
import { ChartRefusedError, toNumber } from './values';

/**
 * A Chart Spec that cannot be drawn (ADR-0320): its message names the path in the spec and the reason, in
 * one sentence a host returns to the AI that wrote it, so the AI can correct the spec. The counterpart of
 * C#'s `ChartSpecRefusedException`.
 */
export class ChartSpecRefusedError extends Error {
    /** Where in the spec it was refused, such as `series[1].kind`; empty for the spec as a whole. */
    readonly path: string;

    /** A refusal at `path`, said by `message`. */
    constructor(path: string, message: string) {
        super(message);
        this.name = 'ChartSpecRefusedError';
        this.path = path;
    }
}

/** A Chart Spec read: its title, and the Chart Definition or the 3D surface it describes. */
export type ChartSpecReadingOptions =
    | {
        /** The chart's title, or empty when the spec gives none. */
        readonly title: string;
        /** The chart, its points read from the tables the spec names. */
        readonly definition: ChartDefinition;
    }
    | {
        /** The surface's title, or empty when the spec gives none. */
        readonly title: string;
        /** The surface, its points read from the tables the spec names (ADR-0274). */
        readonly surface: SurfaceDefinition;
    };

/** A Chart Spec read: its title, and the Chart Definition or the 3D surface it describes. */
export class ChartSpecReading {
    readonly #title: string;
    readonly #definition: ChartDefinition | undefined;
    readonly #surface: SurfaceDefinition | undefined;

    /** A spec of series, or of a 3D surface, read. */
    constructor(options: ChartSpecReadingOptions) {
        this.#title = options.title;
        if ('surface' in options) {
            this.#definition = undefined;
            this.#surface = options.surface;
        } else {
            this.#definition = options.definition;
            this.#surface = undefined;
        }
    }

    /** The chart's title, or empty when the spec gives none. */
    get title(): string {
        return this.#title;
    }

    /**
     * The chart of series, its points read from the tables the spec names. A spec of a 3D surface, which
     * {@link ChartSpecReading.surface} holds, throws an `Error`: a chart of series read from it would be no
     * chart at all (spine rule 1).
     */
    get definition(): ChartDefinition {
        if (this.#definition === undefined) {
            throw new Error('This Chart Spec is of a 3D surface: draw its Surface with ExSurface; it has no chart of series.');
        }
        return this.#definition;
    }

    /** The 3D surface a spec of one describes (ADR-0274); `undefined` for a spec of series. */
    get surface(): SurfaceDefinition | undefined {
        return this.#surface;
    }
}

/** The version of the Chart Spec this reader reads. */
export const chartSpecVersion = 1;

const rootKeys = ['version', 'title', 'axis', 'series', 'surface', 'notes'];
const surfaceKeys = ['data', 'x', 'y', 'z', 'xTitle', 'yTitle', 'zTitle', 'xScale', 'yScale', 'zScale', 'centre', 'method', 'interpolated', 'key', 'triangles'];
const triangleKeys = ['data', 'a', 'b', 'c'];
const axisKeys = ['values', 'title', 'scale', 'packed'];
const seriesKeys = [
    'id', 'name', 'kind', 'data', 'x', 'y', 'open', 'high', 'low', 'close', 'volume', 'label', 'panel', 'axis', 'fromZero', 'step', 'colour', 'size',
    'key', 'edges', 'tooltip', 'interval', 'xInterval',
];
const edgeKeys = ['data', 'from', 'to'];
const colourKeys = ['column', 'scale', 'centre', 'title'];
const sizeKeys = ['column', 'title'];
const intervalKeys = ['lower', 'upper', 'meaning', 'style'];
const priceColumns = ['open', 'high', 'low', 'close', 'volume'] as const;

// The choices a key takes, in the order a refusal lists them.
const kinds: ReadonlyMap<string, SeriesKind> = new Map([
    ['level', 'level'], ['rateOverInterval', 'rateOverInterval'], ['amountPerPeriod', 'amountPerPeriod'], ['observation', 'observation'],
    ['priceRange', 'priceRange'],
]);
const axisValueChoices: ReadonlyMap<string, AxisValues> = new Map([['dates', 'dates'], ['numbers', 'numbers'], ['labels', 'labels']]);
const axisScaleChoices: ReadonlyMap<string, AxisScale> = new Map([['proportional', 'proportional'], ['categories', 'categories'], ['logarithmic', 'logarithmic']]);
const surfaceScaleChoices: ReadonlyMap<string, AxisScale> = new Map([['categories', 'categories'], ['proportional', 'proportional'], ['logarithmic', 'logarithmic']]);
const zScaleChoices: ReadonlyMap<string, AxisScale> = new Map([['proportional', 'proportional'], ['logarithmic', 'logarithmic']]);
const sideChoices: ReadonlyMap<string, AxisSide> = new Map([['left', 'left'], ['right', 'right']]);
const stepChoices: ReadonlyMap<string, StepSide> = new Map([['preceding', 'preceding'], ['following', 'following']]);
const colourScaleChoices: ReadonlyMap<string, boolean> = new Map([['sequential', false], ['diverging', true]]);
const intervalStyleChoices: ReadonlyMap<string, IntervalStyle> = new Map([['whiskers', 'whiskers'], ['band', 'band']]);

/**
 * Reads `text`, a Chart Spec, into a Chart Definition or a 3D surface, taking each Data Handle's table from
 * `tables`, which answers `undefined` for a handle it does not know: the counterpart of C#'s
 * `ChartSpec.Read`. A spec that cannot be drawn throws a {@link ChartSpecRefusedError}, whose message says
 * where and why.
 */
export function readChartSpec(text: string, tables: (handle: string) => ChartData | undefined): ChartSpecReading {
    let root: JsonValue;
    try {
        root = readJsonText(text);
    } catch (error) {
        if (error instanceof JsonTextError) {
            throw new ChartSpecRefusedError('', `The Chart Spec is not JSON: ${error.message}`);
        }
        throw error;
    }
    return new Reader(tables).read(root);
}

// What a grid axis places: numbers, dates as their day numbers, or labels.
type PlacedKind = 'numbers' | 'dates' | 'labels';

interface GridValues {
    // The axis' values: numbers and dates in order, labels as the data first shows them.
    readonly values: readonly (number | string)[];
    // Each row's value among them.
    readonly of: readonly (number | string)[];
    readonly kind: PlacedKind | undefined;
}

class Reader {
    readonly #tables: (handle: string) => ChartData | undefined;
    // A label axis' categories, in the order the data first shows them (ADR-0251).
    readonly #labels: string[] = [];
    readonly #labelIndex = new Map<string, number>();

    constructor(tables: (handle: string) => ChartData | undefined) {
        this.#tables = tables;
    }

    read(root: JsonValue): ChartSpecReading {
        if (!isJsonObject(root)) {
            throw refuse('', 'The Chart Spec is a JSON object.');
        }
        known(root, '', rootKeys);
        const version = jsonMember(root, 'version');
        if (version === undefined) {
            throw refuse('version', `version is missing: a Chart Spec says which version it is written in; this reader reads version ${String(chartSpecVersion)}.`);
        }
        if (!(version instanceof JsonNumber) || version.int32 !== chartSpecVersion) {
            throw refuse('version', `version ${jsonRawText(version)} is not known: this reader reads version ${String(chartSpecVersion)}, and never reads a spec as the nearest version.`);
        }
        const title = optionalString(root, 'title', '');
        const surfaceElement = jsonMember(root, 'surface');
        if (surfaceElement !== undefined) {
            for (const other of ['axis', 'series']) {
                if (jsonMember(root, other) !== undefined) {
                    throw refuse(other, `${other} belongs to a chart of series; a spec draws a surface or series, and its surface names its own x, y and z.`);
                }
            }
            // The notes are read before the surface, as C# reads its arguments.
            const notes = notesOf(root);
            return new ChartSpecReading({ title: title === undefined ? '' : title, surface: this.#surface(surfaceElement, 'surface', notes) });
        }
        const axisElement = objectMember(root, 'axis', '');
        known(axisElement, 'axis', axisKeys);
        const values = choice(axisElement, 'values', 'axis', axisValueChoices, 'what the positions across are');
        const scale = jsonMember(axisElement, 'scale') === undefined
            ? undefined
            : choice(axisElement, 'scale', 'axis', axisScaleChoices, 'how the positions are placed');
        const seriesArray = jsonMember(root, 'series');
        if (!isJsonArray(seriesArray) || seriesArray.length === 0) {
            throw refuse('series', 'series is missing: a Chart Spec lists at least one series.');
        }
        const series = seriesArray.map((s, i) => this.#series(s, `series[${String(i)}]`, values));
        const notes = notesOf(root);
        const axisTitle = optionalString(axisElement, 'title', 'axis');
        const packed = optionalBool(axisElement, 'packed', 'axis');
        const axis: ChartAxisOptions = {
            values,
            title: axisTitle === undefined ? '' : axisTitle,
            ...(scale === undefined ? {} : { scale }),
            labels: [...this.#labels],
            packed: packed === undefined ? false : packed,
        };
        // Each series is checked on its own first, so a refusal names the series it belongs to.
        for (let n = 0; n < series.length; n++) {
            const own = elementAt(series, n);
            const duplicate = series.findIndex(s => s.id === own.id);
            if (duplicate < n) {
                throw refuse(`series[${String(n)}].id`, `series[${String(n)}].id "${own.id}" is series[${String(duplicate)}]'s too: each series needs an identity of its own.`);
            }
            defined(`series[${String(n)}]`, () => new ChartDefinition({ axis, series: [own] }));
        }
        return new ChartSpecReading({
            title: title === undefined ? '' : title,
            definition: defined('', () => new ChartDefinition({ axis, series, notes })),
        });
    }

    // A 3D surface (ADR-0274): a grid its rows lie on, or the Consumer's triangles over keyed nodes (ADR-0311);
    // its colours' centre (ADR-0310) and its scales (ADR-0312). Nothing is interpolated: rows that form no grid
    // are refused, as SurfaceDefinition.fromPoints refuses them.
    #surface(s: JsonValue, path: string, notes: readonly string[]): SurfaceDefinition {
        if (!isJsonObject(s)) {
            throw refuse(path, `${path} is a JSON object.`);
        }
        known(s, path, surfaceKeys);
        const handle = requiredString(s, 'data', path, 'the Data Handle of the table of its points');
        const table = this.#table(handle, `${path}.data`);
        const x = requiredColumn(s, 'x', path, handle, table);
        const y = requiredColumn(s, 'y', path, handle, table);
        const z = requiredColumn(s, 'z', path, handle, table);
        const xTitle = optionalString(s, 'xTitle', path) ?? x;
        const yTitle = optionalString(s, 'yTitle', path) ?? y;
        const zTitle = optionalString(s, 'zTitle', path) ?? z;
        const scaleOf = (key: string): AxisScale | undefined =>
            (jsonMember(s, key) === undefined ? undefined : choice(s, key, path, surfaceScaleChoices, 'how its values are placed'));
        const xScale = scaleOf('xScale');
        const yScale = scaleOf('yScale');
        const zScale = jsonMember(s, 'zScale') === undefined
            ? 'proportional'
            : choice(s, 'zScale', path, zScaleChoices, 'how the values are placed up');
        let centre: number | undefined;
        const centreElement = jsonMember(s, 'centre');
        if (centreElement !== undefined) {
            if (!(centreElement instanceof JsonNumber) || !Number.isFinite(centreElement.double)) {
                throw refuse(`${path}.centre`, `${path}.centre is a finite number.`);
            }
            centre = centreElement.double;
        }
        const method = optionalString(s, 'method', path);
        const rows = table.rowCount;
        const zs: (ChartValue | null)[] = [];
        for (let r = 0; r < rows; r++) {
            zs.push(valueOf(cell(table, z, r, handle), `${path}.z`, handle, r));
        }
        const scaled = { zScale, ...(centre === undefined ? {} : { centre }) };

        let surface: SurfaceDefinition;
        const trianglesElement = jsonMember(s, 'triangles');
        if (trianglesElement !== undefined) {
            const at = `${path}.triangles`;
            for (const gridOnly of ['interpolated', 'method']) {
                if (jsonMember(s, gridOnly) !== undefined) {
                    throw refuse(`${path}.${gridOnly}`, `${path}.${gridOnly} belongs to a grid; a mesh's nodes are its values, and between them it is read linearly on its triangles, which it says.`);
                }
            }
            const key = optionalColumn(s, 'key', path, handle, table);
            if (key === undefined) {
                throw missing(path, 'key', 'a triangle names its nodes by key, so the nodes say which column holds theirs');
            }
            const nodes: SurfaceNode[] = [];
            const index = new Map<string, number>();
            for (let r = 0; r < rows; r++) {
                const name = keyOf(cell(table, key, r, handle), `${path}.key`, handle, r);
                const seen = index.get(name);
                if (seen !== undefined) {
                    throw refuse(`${path}.key`, `${path}.key: rows ${String(seen + 1)} and ${String(r + 1)} of "${handle}" are both keyed ${name}; a triangle could not say which it names.`);
                }
                index.set(name, r);
                nodes.push({
                    x: meshPosition(cell(table, x, r, handle), `${path}.x`, handle, r),
                    y: meshPosition(cell(table, y, r, handle), `${path}.y`, handle, r),
                    z: elementAt(zs, r),
                });
            }
            if (!isJsonObject(trianglesElement)) {
                throw refuse(at, `${at} is a JSON object.`);
            }
            known(trianglesElement, at, triangleKeys);
            const triangleHandle = requiredString(trianglesElement, 'data', at, 'the Data Handle of the triangles\' table');
            const triangleTable = this.#table(triangleHandle, `${at}.data`);
            const corners = (['a', 'b', 'c'] as const).map(c => ({ key: c, column: requiredColumn(trianglesElement, c, at, triangleHandle, triangleTable) }));
            const triangles: SurfaceTriangle[] = [];
            for (let r = 0; r < triangleTable.rowCount; r++) {
                const [a, b, c] = corners.map(corner => {
                    const name = keyOf(cell(triangleTable, corner.column, r, triangleHandle), `${at}.${corner.key}`, triangleHandle, r);
                    const i = index.get(name);
                    if (i === undefined) {
                        throw refuse(`${at}.${corner.key}`, `${at}.${corner.key}: row ${String(r + 1)} of "${triangleHandle}" names node ${name}, but no node is keyed ${name}.`);
                    }
                    return i;
                });
                if (a === undefined || b === undefined || c === undefined) {
                    throw new Error('Internal: a triangle has three corners.');
                }
                triangles.push({ a, b, c });
            }
            for (const [scaleKey, scale] of [['xScale', xScale], ['yScale', yScale]] as const) {
                if (scale === 'categories') {
                    throw refuse(`${path}.${scaleKey}`, `${path}.${scaleKey} "categories": a mesh's nodes are placed by their position, proportionally or logarithmically.`);
                }
            }
            surface = defined(path, () => SurfaceDefinition.fromTriangles({
                nodes, triangles, xTitle, yTitle, zTitle,
                xScale: xScale === undefined ? 'proportional' : xScale,
                yScale: yScale === undefined ? 'proportional' : yScale,
                notes, ...scaled,
            }));
        } else {
            if (jsonMember(s, 'key') !== undefined) {
                throw refuse(`${path}.key`, `${path}.key belongs to a mesh, whose triangles name its nodes; a grid's points stand at their x and y.`);
            }
            const xs = gridValues(table, x, `${path}.x`, `${path}.xScale`, handle, xScale);
            const ys = gridValues(table, y, `${path}.y`, `${path}.yScale`, handle, yScale);
            const xIndex = new Map(xs.values.map((v, i) => [v, i]));
            const yIndex = new Map(ys.values.map((v, i) => [v, i]));
            const grid = ys.values.map(() => xs.values.map((): ChartValue | null => null));
            const marked = ys.values.map(() => xs.values.map(() => false));
            const taken = ys.values.map(() => xs.values.map(() => false));
            const interpolated = optionalColumn(s, 'interpolated', path, handle, table);
            if (interpolated !== undefined && method === undefined) {
                throw missing(path, 'method', 'a surface that marks values interpolated names the method they were made with, which the chart says (ADR-0257)');
            }
            for (let r = 0; r < rows; r++) {
                const xOf = elementAt(xs.of, r);
                const yOf = elementAt(ys.of, r);
                const i = indexIn(xIndex, xOf);
                const j = indexIn(yIndex, yOf);
                if (elementAt(elementAt(taken, j), i)) {
                    throw refuse(`${path}.x`, `${path}: two rows of "${handle}" lie at ${xTitle} ${writePlaced(xOf, xs.kind)} and ${yTitle} ${writePlaced(yOf, ys.kind)}, the second row ${String(r + 1)}; a grid point holds one value.`);
                }
                elementAt(taken, j)[i] = true;
                elementAt(grid, j)[i] = elementAt(zs, r);
                if (interpolated !== undefined) {
                    const mark = cell(table, interpolated, r, handle);
                    if (mark !== null && typeof mark !== 'boolean') {
                        throw refuse(`${path}.interpolated`, `${path}.interpolated: row ${String(r + 1)} of "${handle}" holds "${cellText(mark)}", which is not true or false.`);
                    }
                    elementAt(marked, j)[i] = mark === true;
                }
            }
            // A row that alone holds its x, or its y, joins a surface only by interpolation, which is not the chart's.
            for (const [axis, index, title, other, key] of [[xs, xIndex, xTitle, yTitle, 'x'], [ys, yIndex, yTitle, xTitle, 'y']] as const) {
                const held = axis.values.map(() => 0);
                for (const o of axis.of) {
                    const at = indexIn(index, o);
                    held[at] = elementAt(held, at) + 1;
                }
                for (const v of axis.values) {
                    if (elementAt(held, indexIn(index, v)) < 2) {
                        throw refuse(`${path}.${key}`, `${path}.${key}: ${title} ${writePlaced(v, axis.kind)} has a value at only one ${other}. Rows that form no grid join a surface only by interpolation, which the chart does not choose: have the capability interpolate them onto a grid and name its method, or hand its own triangles.`);
                    }
                }
            }
            surface = defined(path, () => SurfaceDefinition.grid({
                x: { title: xTitle, values: xs.values, scale: xScale === undefined ? 'categories' : xScale, ...formatOf(xs.kind) },
                y: { title: yTitle, values: ys.values, scale: yScale === undefined ? 'categories' : yScale, ...formatOf(ys.kind) },
                z: grid, zTitle,
                ...(method === undefined ? {} : { method }),
                ...(interpolated === undefined ? {} : { interpolated: marked }),
                notes, ...scaled,
            }));
        }
        return surface;
    }

    #series(s: JsonValue, path: string, values: AxisValues): ChartSeriesOptions {
        if (!isJsonObject(s)) {
            throw refuse(path, `${path} is a JSON object.`);
        }
        known(s, path, seriesKeys);
        const id = requiredString(s, 'id', path, 'a series\' identity, unique in the chart');
        const name = requiredString(s, 'name', path, 'its name in the legend and the tooltip');
        const kindNames = [...kinds.keys()].join(', ');
        const kindText = requiredString(s, 'kind', path, `every series says what its numbers are: ${kindNames}; there is no default`);
        const kind = kinds.get(kindText);
        if (kind === undefined) {
            throw refuse(`${path}.kind`, `${path}.kind "${kindText}" is not a Series Kind: it is one of ${kindNames}.`);
        }
        const handle = requiredString(s, 'data', path, 'the Data Handle of the table its values are read from');
        const table = this.#table(handle, `${path}.data`);
        const x = requiredColumn(s, 'x', path, handle, table);
        const rows = table.rowCount;
        const positions: number[] = [];
        for (let r = 0; r < rows; r++) {
            positions.push(this.#position(cell(table, x, r, handle), values, `${path}.x`, handle, r));
        }
        const label = optionalColumn(s, 'label', path, handle, table);
        const key = optionalColumn(s, 'key', path, handle, table);
        let points: readonly ChartPoint[] | undefined;
        let bars: readonly PriceBar[] | undefined;
        if (kind === 'priceRange') {
            if (jsonMember(s, 'y') !== undefined) {
                throw refuse(`${path}.y`, `${path}.y belongs to a series of one value a row; a priceRange reads open, high, low and close.`);
            }
            const columns = new Map(priceColumns.map(c => [c, c === 'open' || c === 'volume'
                ? optionalColumn(s, c, path, handle, table)
                : requiredColumn(s, c, path, handle, table)]));
            const read: PriceBar[] = [];
            for (let r = 0; r < rows; r++) {
                const at = (c: (typeof priceColumns)[number]): ChartValue | null => {
                    const column = columns.get(c);
                    return column === undefined ? null : valueOf(cell(table, column, r, handle), `${path}.${c}`, handle, r);
                };
                const open = at('open');
                const high = at('high');
                const low = at('low');
                const close = at('close');
                read.push({ x: elementAt(positions, r), open, high, low, close, volume: at('volume') });
            }
            bars = read;
        } else {
            for (const c of priceColumns) {
                if (jsonMember(s, c) !== undefined) {
                    throw refuse(`${path}.${c}`, `${path}.${c} belongs to a priceRange; a ${kindText} reads one value a row, from y.`);
                }
            }
            const y = requiredColumn(s, 'y', path, handle, table);
            const read: ChartPoint[] = [];
            for (let r = 0; r < rows; r++) {
                const value = valueOf(cell(table, y, r, handle), `${path}.y`, handle, r);
                read.push({
                    x: elementAt(positions, r), y: value,
                    ...(label === undefined ? {} : { name: cellText(cell(table, label, r, handle)) }),
                    ...(key === undefined ? {} : { row: keyOf(cell(table, key, r, handle), `${path}.key`, handle, r) }),
                });
            }
            points = read;
        }
        let edges: readonly ChartEdge[] | undefined;
        const edgesElement = jsonMember(s, 'edges');
        if (edgesElement !== undefined) {
            const at = `${path}.edges`;
            if (kind !== 'observation') {
                throw refuse(at, `${at} belongs to an observation, whose points are the nodes.`);
            }
            if (key === undefined) {
                throw refuse(`${path}.key`, `${path}.key is missing: an edge names its nodes by key, so the nodes say which column holds theirs.`);
            }
            if (!isJsonObject(edgesElement)) {
                throw refuse(at, `${at} is a JSON object.`);
            }
            known(edgesElement, at, edgeKeys);
            const edgeHandle = requiredString(edgesElement, 'data', at, 'the Data Handle of the edges\' table');
            const edgeTable = this.#table(edgeHandle, `${at}.data`);
            const from = requiredColumn(edgesElement, 'from', at, edgeHandle, edgeTable);
            const to = requiredColumn(edgesElement, 'to', at, edgeHandle, edgeTable);
            const read: ChartEdge[] = [];
            for (let r = 0; r < edgeTable.rowCount; r++) {
                const start = keyOf(cell(edgeTable, from, r, edgeHandle), `${at}.from`, edgeHandle, r);
                read.push({ from: start, to: keyOf(cell(edgeTable, to, r, edgeHandle), `${at}.to`, edgeHandle, r) });
            }
            edges = read;
        }
        let interval: SeriesInterval | undefined;
        const intervalElement = jsonMember(s, 'interval');
        if (intervalElement !== undefined) {
            if (kind !== 'level' && kind !== 'observation') {
                throw refuse(`${path}.interval`, `${path}.interval belongs to a level or an observation, whose points the bounds stand at.`);
            }
            interval = readInterval(intervalElement, `${path}.interval`, handle, table, positions, (v, at, r) => valueOf(v, at, handle, r));
        }
        let xInterval: SeriesInterval | undefined;
        const xIntervalElement = jsonMember(s, 'xInterval');
        if (xIntervalElement !== undefined) {
            const at = `${path}.xInterval`;
            if (kind !== 'observation') {
                throw refuse(at, `${at} belongs to an observation, whose points each stand at a position of their own.`);
            }
            if (values === 'labels') {
                throw refuse(at, `${at}: a labels axis has no units for a bound to stand in.`);
            }
            xInterval = readInterval(xIntervalElement, at, handle, table, positions,
                (v, p, r) => (v === null ? null : this.#position(v, values, p, handle, r)));
        }
        const tooltipElement = jsonMember(s, 'tooltip');
        const details = tooltipElement === undefined ? undefined : detailsOf(tooltipElement, `${path}.tooltip`, kind, handle, table);
        const colourElement = jsonMember(s, 'colour');
        const colourBy = colourElement === undefined ? undefined : colourOf(colourElement, `${path}.colour`, handle, table);
        let sizeBy: { readonly title: string; readonly values: readonly (ChartValue | null)[] } | undefined;
        const sizeElement = jsonMember(s, 'size');
        if (sizeElement !== undefined) {
            const at = `${path}.size`;
            if (!isJsonObject(sizeElement)) {
                throw refuse(at, `${at} is a JSON object.`);
            }
            known(sizeElement, at, sizeKeys);
            const column = requiredColumn(sizeElement, 'column', at, handle, table);
            const sizeTitle = optionalString(sizeElement, 'title', at) ?? column;
            sizeBy = { title: sizeTitle, values: columnValues(table, column, `${at}.column`, handle) };
        }
        const panel = optionalString(s, 'panel', path);
        const axis = jsonMember(s, 'axis') === undefined ? 'left' : choice(s, 'axis', path, sideChoices, 'the vertical axis it reads on');
        const fromZero = optionalBool(s, 'fromZero', path);
        const step = jsonMember(s, 'step') === undefined ? 'preceding' : choice(s, 'step', path, stepChoices, 'which interval a step\'s point stands for');
        return {
            id, name, kind,
            ...(points === undefined ? {} : { points }),
            ...(bars === undefined ? {} : { bars }),
            ...(edges === undefined ? {} : { edges }),
            ...(interval === undefined ? {} : { interval }),
            ...(xInterval === undefined ? {} : { xInterval }),
            ...(details === undefined ? {} : { details }),
            ...(colourBy === undefined ? {} : { colourBy }),
            ...(sizeBy === undefined ? {} : { sizeBy }),
            panel: panel === undefined ? '' : panel,
            axis,
            ...(fromZero === undefined ? {} : { fromZero }),
            step,
        };
    }

    #table(handle: string, path: string): ChartData {
        const table = this.#tables(handle);
        if (table === undefined) {
            throw refuse(path, `${path} "${handle}" is not known: name a Data Handle a capability returned.`);
        }
        return table;
    }

    #position(value: unknown, values: AxisValues, path: string, handle: string, row: number): number {
        const date = values === 'dates' ? temporalDate(value) : undefined;
        if (date !== undefined) {
            if (date.ticks % ticksPerDay !== 0n) {
                throw refuse(path, `${path}: row ${String(row + 1)} of "${handle}" holds ${cellText(value)}, a time of day; a dates axis places days.`);
            }
            return Number(date.ticks / ticksPerDay);
        }
        const n = values === 'numbers' ? valueOf(value, path, handle, row) : null;
        if (n !== null) {
            return toNumber(n);
        }
        if (values === 'labels' && typeof value === 'string') {
            let at = this.#labelIndex.get(value);
            if (at === undefined) {
                at = this.#labels.length;
                this.#labels.push(value);
                this.#labelIndex.set(value, at);
            }
            return at;
        }
        if (value === null) {
            throw refuse(path, `${path}: row ${String(row + 1)} of "${handle}" has no position; a point needs one across.`);
        }
        const expected = values === 'dates' ? 'a date' : values === 'numbers' ? 'a number' : 'a label';
        throw refuse(path, `${path}: row ${String(row + 1)} of "${handle}" holds "${cellText(value)}", but the axis is ${values} and needs ${expected}.`);
    }
}

function notesOf(root: JsonObject): string[] {
    const notes: string[] = [];
    const notesElement = jsonMember(root, 'notes');
    if (notesElement !== undefined) {
        if (!isJsonArray(notesElement)) {
            throw refuse('notes', 'notes is a list of lines.');
        }
        notesElement.forEach((line, n) => {
            if (typeof line !== 'string') {
                throw refuse(`notes[${String(n)}]`, `notes[${String(n)}] is a line of text.`);
            }
            notes.push(line);
        });
    }
    return notes;
}

// A grid axis' values, each row's value among them: numbers and dates in order, labels as the data first
// shows them; one kind a column.
function gridValues(table: ChartData, column: string, path: string, scalePath: string, handle: string, scale: AxisScale | undefined): GridValues {
    const of: (number | string)[] = [];
    let kind: PlacedKind | undefined;
    for (let r = 0; r < table.rowCount; r++) {
        const value = cell(table, column, r, handle);
        let placed: number | string;
        let placedKind: PlacedKind;
        const date = temporalDate(value);
        if (value === null) {
            throw refuse(path, `${path}: row ${String(r + 1)} of "${handle}" has no position; a point needs one.`);
        } else if (date !== undefined) {
            if (date.ticks % ticksPerDay !== 0n) {
                throw refuse(path, `${path}: row ${String(r + 1)} of "${handle}" holds ${cellText(value)}, a time of day; a surface places days.`);
            }
            placed = Number(date.ticks / ticksPerDay);
            placedKind = 'dates';
        } else if (typeof value === 'string') {
            placed = value;
            placedKind = 'labels';
        } else {
            const n = valueOf(value, path, handle, r);
            if (n === null) {
                throw new Error('Internal: a value that is not null read as missing.');
            }
            placed = toNumber(n);
            placedKind = 'numbers';
        }
        kind ??= placedKind;
        if (placedKind !== kind) {
            throw refuse(path, `${path}: row ${String(r + 1)} of "${handle}" holds "${cellText(value)}", but the rows before it hold ${kind}; an axis places one kind of value.`);
        }
        of.push(placed);
    }
    if ((scale === 'proportional' || scale === 'logarithmic') && kind !== undefined && kind !== 'numbers') {
        throw refuse(scalePath, `${scalePath}: only numbers are placed by value; ${path}'s ${kind} are placed evenly, as categories.`);
    }
    const distinct = [...new Set(of)];
    return {
        values: kind === 'labels' ? distinct : distinct.toSorted((a, b) => (a < b ? -1 : a > b ? 1 : 0)),
        of,
        kind,
    };
}

// A date axis writes its days as dates; the other kinds as they are.
function formatOf(kind: PlacedKind | undefined): { readonly format?: (value: SurfaceValue) => string } {
    return kind === 'dates' ? { format: value => (typeof value === 'number' ? isoDate(dateOf(value)) : value) } : {};
}

function writePlaced(value: number | string, kind: PlacedKind | undefined): string {
    if (typeof value === 'string') {
        return value;
    }
    return kind === 'dates' ? isoDate(dateOf(value)) : roundTrip(value);
}

function indexIn(index: ReadonlyMap<number | string, number>, value: number | string): number {
    const at = index.get(value);
    if (at === undefined) {
        throw new Error(`Internal: ${String(value)} is not among the axis' values.`);
    }
    return at;
}

function meshPosition(value: unknown, path: string, handle: string, row: number): number {
    const v = valueOf(value, path, handle, row);
    if (v === null) {
        throw refuse(path, `${path}: row ${String(row + 1)} of "${handle}" has no position; a node needs one in the plane.`);
    }
    return toNumber(v);
}

function colourOf(c: JsonValue, path: string, handle: string, table: ChartData): PointColour {
    if (!isJsonObject(c)) {
        throw refuse(path, `${path} is a JSON object.`);
    }
    known(c, path, colourKeys);
    const column = requiredColumn(c, 'column', path, handle, table);
    const diverging = choice(c, 'scale', path, colourScaleChoices, 'how the colours run');
    let centre: number | undefined;
    const centreElement = jsonMember(c, 'centre');
    if (centreElement !== undefined) {
        if (!diverging) {
            throw refuse(`${path}.centre`, `${path}.centre belongs to a diverging scale; a sequential one runs from the lowest value to the highest.`);
        }
        if (!(centreElement instanceof JsonNumber) || !Number.isFinite(centreElement.double)) {
            throw refuse(`${path}.centre`, `${path}.centre is a finite number.`);
        }
        centre = centreElement.double;
    } else if (diverging) {
        throw refuse(`${path}.centre`, `${path}.centre is missing: a diverging scale says the value that means nothing, such as 0 for a P&L or 1 for a ratio.`);
    }
    const title = optionalString(c, 'title', path) ?? column;
    return { title, values: columnValues(table, column, `${path}.column`, handle), ...(centre === undefined ? {} : { centre }) };
}

// A series' interval: a lower and an upper bound a row, at the row's position (ADR-0303, ADR-0304).
function readInterval(iv: JsonValue, path: string, handle: string, table: ChartData, positions: readonly number[],
    bound: (value: unknown, path: string, row: number) => ChartValue | null): SeriesInterval {
    if (!isJsonObject(iv)) {
        throw refuse(path, `${path} is a JSON object.`);
    }
    known(iv, path, intervalKeys);
    const lower = requiredColumn(iv, 'lower', path, handle, table);
    const upper = requiredColumn(iv, 'upper', path, handle, table);
    const meaning = optionalString(iv, 'meaning', path);
    if (meaning === undefined || meaning.trim() === '') {
        throw missing(path, 'meaning', 'what the bounds are, such as 95% CI, ±1σ or Bid–Ask; an interval that does not say is not drawn');
    }
    const style = jsonMember(iv, 'style') === undefined ? 'whiskers' : choice(iv, 'style', path, intervalStyleChoices, 'how the bounds are drawn');
    const bounds: IntervalBound[] = [];
    for (let r = 0; r < table.rowCount; r++) {
        const low = bound(cell(table, lower, r, handle), `${path}.lower`, r);
        bounds.push({ x: elementAt(positions, r), lower: low, upper: bound(cell(table, upper, r, handle), `${path}.upper`, r) });
    }
    return { meaning, bounds, style };
}

// The columns an observation's tooltip says, each one value a row written as the table holds it (ADR-0321).
function detailsOf(t: JsonValue, path: string, kind: SeriesKind, handle: string, table: ChartData): PointDetail[] {
    if (kind !== 'observation') {
        throw refuse(path, `${path} belongs to an observation, whose tooltip names one point; a line's tooltip reads every series at a position.`);
    }
    if (!isJsonArray(t)) {
        throw refuse(path, `${path} is a list of the columns its tooltip says.`);
    }
    const details: PointDetail[] = [];
    t.forEach((c, n) => {
        const at = `${path}[${String(n)}]`;
        if (typeof c !== 'string') {
            throw refuse(at, `${at} names a column, as text.`);
        }
        if (!table.columns.includes(c)) {
            throw refuse(at, `${at}: "${handle}" has no column "${c}"; its columns are ${table.columns.join(', ')}.`);
        }
        if (details.some(d => d.title === c)) {
            throw refuse(at, `${at} names "${c}" twice.`);
        }
        const values: (string | null)[] = [];
        for (let r = 0; r < table.rowCount; r++) {
            const v = cell(table, c, r, handle);
            values.push(v === null ? null : cellText(v));
        }
        details.push({ title: c, values });
    });
    return details;
}

// A key is compared as its text, so a key read as a number in one table meets the same key in another.
function keyOf(value: unknown, path: string, handle: string, row: number): string {
    if (value === null) {
        throw refuse(path, `${path}: row ${String(row + 1)} of "${handle}" has no key.`);
    }
    return cellText(value);
}

function columnValues(table: ChartData, column: string, path: string, handle: string): (ChartValue | null)[] {
    const values: (ChartValue | null)[] = [];
    for (let r = 0; r < table.rowCount; r++) {
        values.push(valueOf(cell(table, column, r, handle), path, handle, r));
    }
    return values;
}

// A table's value, checked at the boundary: a table that answers `undefined` has broken its contract, where a
// missing value is `null`.
function cell(table: ChartData, column: string, row: number, handle: string): unknown {
    const value = table.value(column, row);
    if (value === undefined) {
        throw new TypeError(`"${handle}" answered undefined for row ${String(row + 1)} of its column "${column}"; a missing value is null.`);
    }
    return value;
}

// A value as a chart holds it (ADR-0251): a double as a number, a whole number exactly as a bigint, a decimal
// as the number that writes the same digits, or a bigint where it has none after the point.
function valueOf(value: unknown, path: string, handle: string, row: number): ChartValue | null {
    if (value === null) {
        return null;
    }
    if (typeof value === 'number') {
        if (!Number.isFinite(value)) {
            throw refuse(path, `${path}: row ${String(row + 1)} of "${handle}" is ${cellText(value)}, which is no number; a missing value is null, drawn as a gap.`);
        }
        return value;
    }
    if (typeof value === 'bigint') {
        return value;
    }
    const decimal = asDecimal(value);
    if (decimal !== undefined) {
        const held = decimalValue(decimal);
        if (held === undefined) {
            // A chart value has no decimal of its own; one a number cannot write digit for digit is refused,
            // never rounded into a figure the cell does not show (spine rule 1).
            throw refuse(path, `${path}: row ${String(row + 1)} of "${handle}" holds ${decimalText(decimal)}, which a chart value cannot hold digit for digit; a decimal is drawn with every digit it holds, or not at all.`);
        }
        return held;
    }
    throw refuse(path, `${path}: row ${String(row + 1)} of "${handle}" holds "${cellText(value)}", which is not a number.`);
}

// A Snapshot's decimal, `{ mantissa, scale }`, recognised by its shape as @ex-grid/data recognises one.
function asDecimal(value: unknown): SnapshotDecimal | undefined {
    if (typeof value !== 'object' || value === null) {
        return undefined;
    }
    const mantissa: unknown = Reflect.get(value, 'mantissa');
    const scale: unknown = Reflect.get(value, 'scale');
    return typeof mantissa === 'bigint' && typeof scale === 'number' && Number.isInteger(scale) && scale >= 0 && scale <= 28
        ? { mantissa, scale }
        : undefined;
}

// The decimal as a chart value: a whole one as a bigint; one with places as the number whose shortest digits
// are the decimal's own, or `undefined` where no number writes them.
function decimalValue(decimal: SnapshotDecimal): ChartValue | undefined {
    let mantissa = decimal.mantissa;
    let scale = decimal.scale;
    while (scale > 0 && mantissa % 10n === 0n) {
        mantissa /= 10n;
        scale -= 1;
    }
    if (scale === 0) {
        return mantissa;
    }
    const n = Number(decimalText({ mantissa, scale }));
    const written = Math.abs(n).toExponential();
    const match = /^(\d)(?:\.(\d+))?e([+-]\d+)$/.exec(written);
    if (match === null) {
        throw new Error(`Internal: ${written} is not a number in exponent form.`);
    }
    const [, first = '', rest = '', exponent = ''] = match;
    const digits = BigInt(first + rest);
    const places = rest.length - Number(exponent);
    const signed = mantissa < 0n ? -digits : digits;
    return places === scale && signed === mantissa ? n : undefined;
}

// A value written into a sentence as C#'s `Convert.ToString(value, CultureInfo.InvariantCulture)` writes it.
function cellText(value: unknown): string {
    if (value === null || value === undefined) {
        return '';
    }
    if (typeof value === 'string') {
        return value;
    }
    if (typeof value === 'number') {
        return roundTrip(value);
    }
    if (typeof value === 'bigint') {
        return value.toString();
    }
    // C# writes a bool as `True` or `False`.
    if (typeof value === 'boolean') {
        return value ? 'True' : 'False';
    }
    const decimal = asDecimal(value);
    if (decimal !== undefined) {
        return decimalText(decimal);
    }
    const date = temporalDate(value);
    if (date !== undefined) {
        const day = dateOf(Number(date.ticks / ticksPerDay));
        const written = `${two(day.month)}/${two(day.day)}/${String(day.year).padStart(4, '0')}`;
        if (!date.withTime) {
            return written;
        }
        const seconds = Number((date.ticks % ticksPerDay) / 10_000_000n);
        return `${written} ${two(Math.floor(seconds / 3600))}:${two(Math.floor(seconds / 60) % 60)}:${two(seconds % 60)}`;
    }
    // Any other value is named by its kind, as C# names an object it cannot write by its type.
    return Object.prototype.toString.call(value);
}

function two(n: number): string {
    return String(n).padStart(2, '0');
}

const ticksPerDay = 864_000_000_000n;

// A Temporal `PlainDate` (C#'s DateOnly) or `PlainDateTime` (DateTime), as its ticks since 0001-01-01.
function temporalDate(value: unknown): { readonly ticks: bigint; readonly withTime: boolean } | undefined {
    if (typeof value !== 'object' || value === null) {
        return undefined;
    }
    const ticks = ticksOfTemporal(value);
    return ticks === undefined ? undefined : { ticks, withTime: Object.prototype.toString.call(value) === '[object Temporal.PlainDateTime]' };
}

function requiredColumn(s: JsonObject, key: string, path: string, handle: string, table: ChartData): string {
    const name = optionalColumn(s, key, path, handle, table);
    if (name === undefined) {
        throw missing(path, key, 'the column it is read from');
    }
    return name;
}

function optionalColumn(s: JsonObject, key: string, path: string, handle: string, table: ChartData): string | undefined {
    const name = optionalString(s, key, path);
    if (name === undefined) {
        return undefined;
    }
    if (!table.columns.includes(name)) {
        throw refuse(`${path}.${key}`, `${path}.${key}: "${handle}" has no column "${name}"; its columns are ${table.columns.join(', ')}.`);
    }
    return name;
}

function defined<T>(path: string, define: () => T): T {
    try {
        return define();
    } catch (error) {
        if (error instanceof ChartRefusedError) {
            throw refuse(path, path === '' ? error.message : `${path}: ${error.message}`);
        }
        throw error;
    }
}

function known(o: JsonObject, path: string, keys: readonly string[]): void {
    for (const name of jsonMemberNames(o)) {
        if (!keys.includes(name)) {
            const at = path === '' ? name : `${path}.${name}`;
            const value = jsonMember(o, name);
            const numbers = value instanceof JsonNumber || isJsonArray(value) ? ' a spec holds no numbers, only the columns they are read from;' : '';
            throw refuse(at, `${at} is not part of a Chart Spec:${numbers} ${path === '' ? 'a spec' : path} takes ${keys.join(', ')}.`);
        }
    }
}

function objectMember(o: JsonObject, key: string, path: string): JsonObject {
    const at = path === '' ? key : `${path}.${key}`;
    const e = jsonMember(o, key);
    if (e === undefined) {
        throw refuse(at, `${at} is missing.`);
    }
    if (!isJsonObject(e)) {
        throw refuse(at, `${at} is a JSON object.`);
    }
    return e;
}

// A text member, `undefined` where it is absent or null.
function optionalString(o: JsonObject, key: string, path: string): string | undefined {
    const e = jsonMember(o, key);
    if (e === undefined || e === null) {
        return undefined;
    }
    if (typeof e === 'string') {
        return e;
    }
    const at = path === '' ? key : `${path}.${key}`;
    throw refuse(at, `${at} is text, not ${kindOf(e)}.`);
}

function requiredString(o: JsonObject, key: string, path: string, what: string): string {
    const text = optionalString(o, key, path);
    if (text === undefined) {
        throw missing(path, key, what);
    }
    return text;
}

// A JSON value's kind as System.Text.Json's `JsonValueKind` names it, in lower case.
function kindOf(e: JsonValue): string {
    if (e === null) {
        return 'null';
    }
    if (typeof e === 'boolean') {
        return e ? 'true' : 'false';
    }
    if (typeof e === 'string') {
        return 'string';
    }
    if (e instanceof JsonNumber) {
        return 'number';
    }
    return isJsonArray(e) ? 'array' : 'object';
}

function optionalBool(o: JsonObject, key: string, path: string): boolean | undefined {
    const e = jsonMember(o, key);
    if (e === undefined || e === null) {
        return undefined;
    }
    if (typeof e !== 'boolean') {
        throw refuse(`${path}.${key}`, `${path}.${key} is true or false.`);
    }
    return e;
}

function choice<T>(o: JsonObject, key: string, path: string, choices: ReadonlyMap<string, T>, what: string): T {
    const at = `${path}.${key}`;
    const names = [...choices.keys()].join(', ');
    const text = optionalString(o, key, path);
    if (text === undefined) {
        throw refuse(at, `${at} is missing: it says ${what}, one of ${names}.`);
    }
    const chosen = choices.get(text);
    if (chosen === undefined) {
        throw refuse(at, `${at} "${text}" is not known: it is one of ${names}.`);
    }
    return chosen;
}

function missing(path: string, key: string, what: string): ChartSpecRefusedError {
    return refuse(`${path}.${key}`, `${path}.${key} is missing: ${what}.`);
}

function refuse(path: string, message: string): ChartSpecRefusedError {
    return new ChartSpecRefusedError(path, message);
}
