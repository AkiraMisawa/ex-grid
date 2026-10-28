// One shared chart case (ADR-0370): what it builds, read from its JSON, and the checks it holds the result
// to. The C# runner (tests/ExChart.Engine.Tests/Cases/ChartCase.cs) reads the same files; a result is
// compared as the plain JSON it reads as, which is how C#'s serialised records read. A key the runner does
// not read fails the case, so a misspelt key never passes on a value nobody chose.
import { expect } from 'vitest';
import { appearanceOf } from '../src/appearance';
import { dayNumber } from '../src/day-numbers';
import { ChartDefinition, chartAxis, chartSeries } from '../src/definition';
import { grouped, roundTrip, threeSignificant, twoPlaces, upToPlaces } from '../src/invariant-text';
import type { ChartMetrics, ChartView } from '../src/metrics';
import type {
    AppearanceKeys, ChartAxisOptions, ChartEdge, ChartPoint, ChartSeriesOptions, ChartValue, IntervalBound, PixelSummary, PointDetail, PriceBar,
    AxisScale, Segment, SeriesInterval, SeriesKind, SeriesSampler,
} from '../src/model';
import { chartCsvLong, chartCsvWide, chartTable } from '../src/csv';
import { fitLeastSquares, fitPolynomial, fitTheilSen, type FitSpace, type FittedCurve } from '../src/fitting';
import {
    Interpolant1D, Interpolant2D, interpolateBilinear, interpolateLinear, interpolateNaturalCubicSpline, interpolateTriangles,
} from '../src/interpolation';
import { candlesOf } from '../src/price-ranges';
import { readingAt, readingOfEdge, readingOfMark } from '../src/reading';
import { chartSummary } from '../src/summary';
import { buildScene, legendMembers, positionAt, yOf, type ChartScene, type LegendPlacement } from '../src/scene';
import { drawSeries, marker, seriesDrawing, type DrawnPoint, type SeriesDrawing } from '../src/series-drawing';
import { thin, thinnedThrough } from '../src/thinning';
import { decades, fitLabels, horizontalTicks, logSteps, nice, steps, tenor, type AxisStyle } from '../src/ticks';
import { SurfaceDefinition, type SurfaceAxisOptions, type SurfaceScaleOptions, type SurfaceSlice, type SurfaceValue } from '../src/surface';
import { surfaceMesh, surfaceSlicePath, surfaceSummary } from '../src/surface-mesh';
import { invariantText } from '../src/values';
import { decimalFromText, ticksOfTemporal } from '@ex-grid/data';
import { Temporal } from 'temporal-polyfill';
import { chartDataOf, type ChartData } from '../src/chart-data';
import { ChartSpecRefusedError, readChartSpec, readChartSpecThrough } from '../src/spec';
import { thinChartData, type ChartSource, type ChartSourceAnswer, type ChartSourceQuery } from '../src/chart-source';
import type { AxisValues } from '../src/model';
import { answerChartSpec } from '../src/spec-answer';

/** A value as JSON holds it. */
export type Json = null | boolean | number | string | readonly Json[] | JsonObject;

/** An object as JSON holds it. */
export interface JsonObject {
    readonly [key: string]: Json;
}

/** A file of chart cases. */
export interface ChartFile {
    readonly about: string;
    readonly kind: string;
    readonly near?: number;
    readonly definitions?: JsonObject;
    /** The tables a Chart Spec case reads, by Data Handle. */
    readonly tables?: JsonObject;
    readonly cases: readonly JsonObject[];
}

const operations = [
    'scene', 'drawing', 'thin', 'through', 'tenor', 'nice', 'steps', 'decades', 'logSteps', 'fit', 'horizontal', 'candles', 'legendMembers',
    'format', 'appearance', 'mesh', 'slice', 'slicePath', 'reading', 'summary', 'csv', 'surfaceSummary', 'curve',
    'interpolate', 'sourceThin',
] as const;

// What a scene is laid out with, which the tooltip and the summary read their scene by.
const sceneKeys = ['metrics', 'view', 'style', 'independentScales', 'legend'];

type Operation = (typeof operations)[number];

const caseKeys = ['name', 'build', 'definition', 'surface', 'throws', 'saying', 'expect', ...operations];

const checkKeys = [
    'at', 'yOf', 'positionAt', 'asked', 'of', 'where', 'sortBy', 'map', 'differences', 'sum', 'count',
    'is', 'isNot', 'near', 'above', 'below', 'atLeast', 'atMost', 'sameAs', 'differsFrom', 'has', 'lacks', 'mentions', 'empty', 'evenlySpaced',
    'startsWith', 'each', 'then',
];

const assertions = [
    'is', 'isNot', 'above', 'below', 'atLeast', 'atMost', 'sameAs', 'differsFrom', 'has', 'lacks', 'mentions', 'empty', 'evenlySpaced', 'startsWith',
    'each', 'then',
];

// ---- Reading JSON without a quiet default ----

export function isObject(value: Json | undefined): value is JsonObject {
    return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function isList(value: Json | undefined): value is readonly Json[] {
    return Array.isArray(value);
}

const text = (value: Json | undefined): string => (value === undefined ? 'nothing' : JSON.stringify(value));

/** The object `value` is, holding only the keys the runner reads. */
function allow(value: Json | undefined, what: string, keys: readonly string[]): JsonObject {
    if (!isObject(value)) {
        throw new Error(`${what} is ${text(value)}, not an object.`);
    }
    for (const key of Object.keys(value)) {
        if (!keys.includes(key)) {
            throw new Error(`${what} has a key "${key}", which the runner does not read.`);
        }
    }
    return value;
}

function list(value: Json | undefined, what: string): readonly Json[] {
    if (!isList(value)) {
        throw new Error(`${what} is ${text(value)}, not a list.`);
    }
    return value;
}

function string(value: Json | undefined, what: string): string {
    if (typeof value !== 'string') {
        throw new Error(`${what} is ${text(value)}, not a text.`);
    }
    return value;
}

function boolean(value: Json | undefined, what: string): boolean {
    if (typeof value !== 'boolean') {
        throw new Error(`${what} is ${text(value)}, not true or false.`);
    }
    return value;
}

function whole(value: Json | undefined, what: string): number {
    if (typeof value !== 'number' || !Number.isInteger(value)) {
        throw new Error(`${what} is ${text(value)}, not a whole number.`);
    }
    return value;
}

/** A number, or one JSON cannot hold, written as its name. */
function number(value: Json | undefined): number {
    if (typeof value === 'number') {
        return value;
    }
    if (value === 'Infinity') {
        return Infinity;
    }
    if (value === '-Infinity') {
        return -Infinity;
    }
    if (value === 'NaN') {
        return NaN;
    }
    throw new Error(`${text(value)} is not a number.`);
}

/** A position: a number, or a date as its day number. */
function position(value: Json | undefined): number {
    if (!isObject(value)) {
        return number(value);
    }
    const date = string(allow(value, 'a date', ['date'])['date'], 'a date');
    const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(date);
    if (match === null) {
        throw new Error(`"${date}" is not a date written yyyy-MM-dd.`);
    }
    return dayNumber(Number(match[1]), Number(match[2]), Number(match[3]));
}

/** A value: a number, or `null` for a missing one. A number JSON cannot hold is handed on, for the definition to refuse. */
function chartValue(v: Json | undefined): ChartValue | null {
    return v === null ? null : number(v);
}

function optional<T>(o: JsonObject, key: string, read: (v: Json) => T): T | undefined {
    const v = o[key];
    return v === undefined ? undefined : read(v);
}

// ---- What a case builds ----

/** Runs one case of the file: builds what it names, then holds the result to each check. */
export class ChartCase {
    readonly #file: ChartFile;
    readonly #entry: JsonObject;
    // The positions each sampled series was asked at, by its identity.
    readonly #asked = new Map<string, number[]>();
    // What each operation answered, by its name in `build` ("" for a case's own).
    readonly #results = new Map<string, unknown>();

    constructor(file: ChartFile, entry: JsonObject) {
        this.#file = file;
        this.#entry = entry;
    }

    get #name(): string {
        return string(this.#entry['name'], 'a case\'s name');
    }

    run(): void | Promise<void> {
        if (this.#entry['sources'] !== undefined) {
            return this.#runThrough();
        }
        if (this.#entry['spec'] !== undefined || this.#entry['specText'] !== undefined) {
            this.#runSpec();
            return;
        }
        const entry = allow(this.#entry, 'the case', caseKeys);
        const throws = entry['throws'];
        if (throws !== undefined) {
            if (entry['expect'] !== undefined) {
                throw new Error(`"${this.#name}" both throws and expects.`);
            }
            let thrown: unknown;
            try {
                this.#build();
            } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the refusal the case expects is held to it below
                thrown = error;
            }
            if (!(thrown instanceof Error)) {
                throw new Error(`"${this.#name}" expected a refusal (${text(throws)}), and ${thrown === undefined ? 'nothing was thrown' : 'what was thrown is no Error'}.`);
            }
            const error = thrown;
            holds(refusal(error) === string(throws, 'throws'), `"${this.#name}": expected ${text(throws)}, but ${error.name}: ${error.message}`);
            const saying = entry['saying'];
            for (const said of saying === undefined ? [] : isList(saying) ? saying : [saying]) {
                holds(error.message.includes(string(said, 'saying')), `"${this.#name}": "${error.message}" does not say ${text(said)}.`);
            }
            return;
        }
        const subject = this.#build();
        const checks = list(entry['expect'], `"${this.#name}"'s expect`);
        holds(checks.length > 0, `"${this.#name}" checks nothing.`);
        for (const check of checks) {
            this.#check(subject, subject, check);
        }
    }

    // A Chart Spec case (ADR-0320; ADR-0370, section 3): the reading held to the checks, or the refusal to its path.
    #runSpec(): void {
        const answer = specAnswer(this.#file, this.#entry, this.#name);
        if (answer === undefined) {
            return;
        }
        const checks = list(this.#entry['expect'], `"${this.#name}"'s expect`);
        holds(checks.length > 0, `"${this.#name}" checks nothing.`);
        for (const check of checks) {
            this.#check(answer, answer, check);
        }
    }

    // A Chart Spec read through Chart Sources (ADR-0344): the reading, the questions asked and the versions held to the
    // checks, or the refusal to its path and the questions asked before it.
    async #runThrough(): Promise<void> {
        const answer = await throughAnswer(this.#file, this.#entry, this.#name);
        if (answer === undefined) {
            return;
        }
        const checks = list(this.#entry['expect'], `"${this.#name}"'s expect`);
        holds(checks.length > 0, `"${this.#name}" checks nothing.`);
        for (const check of checks) {
            this.#check(answer, answer, check);
        }
    }

    #build(): Json {
        const build = this.#entry['build'];
        if (build === undefined) {
            const result = this.#operate(this.#entry);
            this.#results.set('', result);
            return plain(result);
        }
        const all: Record<string, Json> = {};
        if (!isObject(build)) {
            throw new Error(`"${this.#name}"'s build is ${text(build)}, not an object of named operations.`);
        }
        for (const [name, op] of Object.entries(build)) {
            const result = this.#operate(allow(op, `"${name}"`, ['definition', 'surface', ...operations]));
            this.#results.set(name, result);
            all[name] = plain(result);
        }
        return all;
    }

    #operate(op: JsonObject): unknown {
        const present = operations.filter(k => op[k] !== undefined);
        if (present.length > 1) {
            throw new Error(`"${this.#name}" names ${present.join(' and ')}; an operation is one.`);
        }
        const [key] = present;
        if (key === undefined) {
            return op['surface'] === undefined ? this.#definition(op['definition']) : this.#surface(op['surface']);
        }
        return this.#run(key, op[key], op);
    }

    #run(key: Operation, args: Json | undefined, op: JsonObject): unknown {
        switch (key) {
            case 'scene':
                return laidOut(this.#definition(op['definition']), allow(args, 'scene', sceneKeys));
            case 'sourceThin': {
                // The reference Chart Source (ADR-0344) over one of the file's tables: what it keeps, each row by its
                // position across and its height, and what each thinned pixel held.
                const a = allow(args, 'sourceThin', ['table', 'query', 'version']);
                const handle = string(a['table'], 'table');
                const table = specTables(this.#file, {}).get(handle);
                if (table === undefined) {
                    throw new Error(`"${this.#name}" thins the table "${handle}", which the file does not hold.`);
                }
                const answer = thinChartData(table, sourceQuery(a['query']), BigInt(whole(a['version'], 'version')));
                return answerRows(answer, sourceQuery(a['query']));
            }
            case 'reading': {
                // The tooltip of the scene the case lays out, at a position, over a mark or over an edge.
                const a = allow(args, 'reading', [...sceneKeys, 'at']);
                const d = this.#definition(op['definition']);
                const laid = laidOut(d, a);
                const at = a['at'];
                if (isObject(at) && at['mark'] !== undefined) {
                    return readingOfMark(d, laid, whole(allow(at, 'at', ['mark'])['mark'], 'mark'));
                }
                if (isObject(at) && at['edge'] !== undefined) {
                    return readingOfEdge(d, laid, whole(allow(at, 'at', ['edge'])['edge'], 'edge'));
                }
                const p = allow(at, 'at', ['position', 'panel', 'pointerY']);
                return readingAt(d, laid, metrics(a['metrics']), position(p['position']), whole(p['panel'], 'panel'), number(p['pointerY']));
            }
            case 'summary': {
                const a = allow(args, 'summary', [...sceneKeys, 'title', 'description']);
                const d = this.#definition(op['definition']);
                const title = optional(a, 'title', t => string(t, 'title'));
                const description = optional(a, 'description', t => string(t, 'description'));
                return chartSummary(d, laidOut(d, a), title, description);
            }
            case 'csv': {
                const a = allow(args, 'csv', ['form', 'metrics', 'view']);
                const d = this.#definition(op['definition']);
                const m = metrics(a['metrics']);
                const v = view(a['view']);
                const form = oneOf(a['form'], ['long', 'wide', 'table'], 'form of the files');
                switch (form) {
                    case 'long':
                        return chartCsvLong(d, m, v);
                    case 'wide':
                        return chartCsvWide(d, m, v);
                    case 'table':
                        return chartTable(d, m, v);
                    default:
                        throw new Error(`Internal: ${String(form)} is a form and not written.`);
                }
            }
            case 'interpolate': {
                // An interpolation a Consumer calls (ADR-0257): the interpolant, or its values where `at` names.
                const a = allow(args, 'interpolate', [...interpolantKeys(args), 'at']);
                const made = interpolant(a);
                if (a['at'] === undefined) {
                    return made;
                }
                const positions = list(a['at'], 'at');
                if (made instanceof Interpolant1D) {
                    return positions.map(x => made.valueAt(position(x)));
                }
                return positions.map(p => {
                    const [x, y] = list(p, 'a point');
                    return made.valueAt(number(x), number(y));
                });
            }
            case 'curve': {
                // A fit a Consumer calls (ADR-0291): the curve, or its values at the positions `at` names.
                const a = allow(args, 'curve', [...curveKeys(args), 'at']);
                const fit = fitted(a);
                return a['at'] === undefined ? fit : list(a['at'], 'at').map(x => fit.valueAt(position(x)));
            }
            case 'drawing': {
                const a = allow(args, 'drawing', ['axis', 'series', 'from', 'to', 'pixels', 'quotes']);
                return drawSeries(chartSeries(this.#series(a['series'])), chartAxis(axis(a['axis'])), {
                    ...optionalPart('from', optional(a, 'from', position)),
                    ...optionalPart('to', optional(a, 'to', position)),
                    ...optionalPart('pixels', optional(a, 'pixels', v => whole(v, 'pixels'))),
                    ...optionalPart('quotes', optional(a, 'quotes', v => list(v, 'quotes').map(position))),
                });
            }
            case 'thin': {
                const a = allow(args, 'thin', ['drawing', 'toPixel', 'pixels']);
                return thin(drawing(a['drawing']), linear(a['toPixel']), whole(a['pixels'], 'pixels'));
            }
            case 'through': {
                const a = allow(args, 'through', ['source', 'drawn', 'kept']);
                return thinnedThrough(summaries(a['source']), summaries(a['drawn']), list(a['kept'], 'kept').map(position));
            }
            case 'tenor':
                return tenor(number(args));
            case 'nice': {
                // C# answers a tuple; the Script Core names its parts.
                const a = list(args, 'nice');
                const n = a.length === 3 ? nice(number(a[0]), number(a[1]), whole(a[2], 'the target')) : nice(number(a[0]), number(a[1]));
                return [n.min, n.max, n.step];
            }
            case 'steps': {
                const a = list(args, 'steps');
                return steps(number(a[0]), number(a[1]), number(a[2]));
            }
            case 'decades': {
                const a = list(args, 'decades');
                return decades(number(a[0]), number(a[1]));
            }
            case 'logSteps': {
                const a = list(args, 'logSteps');
                return logSteps(number(a[0]), number(a[1]));
            }
            case 'fit': {
                const a = allow(args, 'fit', ['ticks', 'angle', 'map', 'textWidth', 'lineHeight', 'gap']);
                const ticks = list(a['ticks'], 'ticks').map(t => {
                    const pair = list(t, 'a tick');
                    return [position(pair[0]), string(pair[1], 'a label')] as const;
                });
                return fitLabels(ticks, whole(a['angle'], 'angle'), linear(a['map']), textWidth(a['textWidth']), number(a['lineHeight']), number(a['gap']));
            }
            case 'horizontal': {
                const a = allow(args, 'horizontal', ['axis', 'style', 'from', 'to', 'points', 'map', 'textWidth', 'lineHeight']);
                return horizontalTicks(chartAxis(axis(a['axis'])), style(a['style']), position(a['from']), position(a['to']),
                    list(a['points'], 'points').map(position), linear(a['map']), textWidth(a['textWidth']), number(a['lineHeight']));
            }
            case 'candles': {
                const a = allow(args, 'candles', ['series', 'from', 'to', 'period']);
                const period = string(a['period'], 'period');
                if (period !== 'row' && period !== 'week' && period !== 'month' && period !== 'quarter' && period !== 'year') {
                    throw new Error(`"${period}" is not a CandlePeriod.`);
                }
                // A range the case leaves open is the whole axis, as the C# runner reads it.
                const from = a['from'] === undefined ? -Infinity : position(a['from']);
                const to = a['to'] === undefined ? Infinity : position(a['to']);
                return candlesOf(chartSeries(this.#series(a['series'])), from, to, period);
            }
            case 'legendMembers':
                return legendMembers(this.#definition(op['definition']), string(args, 'legendMembers'));
            case 'format': {
                const a = allow(args, 'format', ['format', 'value']);
                return format(string(a['format'], 'format'), number(a['value']));
            }
            case 'appearance': {
                // C# answers a tuple; the Script Core names its parts.
                const a = allow(args, 'appearance', ['colourSlots']);
                const slots = optional(a, 'colourSlots', v => whole(v, 'colourSlots'));
                const { looks, coloursRepeat } = slots === undefined
                    ? appearanceOf(this.#definition(op['definition']))
                    : appearanceOf(this.#definition(op['definition']), slots);
                return [looks, coloursRepeat];
            }
            case 'mesh':
                allow(args, 'mesh', []);
                return surfaceMesh(this.#surface(op['surface']));
            case 'slice':
                return this.#surface(op['surface']).sliceChart(slice(args));
            case 'slicePath':
                return surfaceSlicePath(this.#surface(op['surface']), slice(args));
            case 'surfaceSummary': {
                const a = allow(args, 'surfaceSummary', ['title', 'description']);
                return surfaceSummary(this.#surface(op['surface']), optional(a, 'title', t => string(t, 'title')),
                    optional(a, 'description', t => string(t, 'description')));
            }
            default:
                throw new Error(`Internal: ${String(key)} is listed and not run.`);
        }
    }

    // What a case names: given in place, or by its name among the file's definitions.
    #named(given: Json | undefined): Json | undefined {
        if (typeof given !== 'string') {
            return given;
        }
        const named = this.#file.definitions?.[given];
        if (named === undefined) {
            throw new Error(`The file has no definition named "${given}".`);
        }
        return named;
    }

    #definition(given: Json | undefined): ChartDefinition {
        const d = allow(this.#named(given), 'a definition', ['axis', 'series', 'notes']);
        return new ChartDefinition({
            axis: axis(d['axis']),
            series: list(d['series'], 'series').map(s => this.#series(s)),
            ...optionalPart('notes', optional(d, 'notes', v => list(v, 'notes').map(n => string(n, 'a note')))),
        });
    }

    // ---- A surface (ADR-0274, ADR-0311) ----

    // A grid (`z`), points on a grid, a Consumer's triangles (`nodes`) or a sampled function, each with its
    // vertical scale and centre where the case names them.
    #surface(given: Json | undefined): SurfaceDefinition {
        const e = allow(this.#named(given), 'a surface', [
            'x', 'y', 'z', 'zTitle', 'zFormat', 'zFormatCode', 'method', 'interpolated', 'notes', 'points', 'xTitle', 'yTitle', 'nodes', 'triangles',
            'xScale', 'yScale', 'xFormatCode', 'yFormatCode', 'sampled', 'zScale', 'centre', 'interpolant', 'samples',
        ]);
        const forms = [...surfaceFormKeys.keys()].filter(k => e[k] !== undefined);
        const [form] = forms;
        if (form === undefined || forms.length !== 1) {
            throw new Error(`"${this.#name}": a surface is one of a grid, points, nodes, a sampled function and an interpolant, not ${String(forms.length)}.`);
        }
        // A key another form reads would be passed over here, and the case would test nothing by it.
        const formKeys = surfaceFormKeys.get(form);
        if (formKeys === undefined) {
            throw new Error(`Internal: ${form} is found among the forms and has no keys.`);
        }
        allow(e, `"${this.#name}"'s ${form} surface`, [...surfaceCommonKeys, ...formKeys]);
        const common = {
            ...optionalPart('zTitle', optional(e, 'zTitle', v => string(v, 'zTitle'))),
            ...optionalPart('zFormat', optional(e, 'zFormat', valueFormat)),
            ...optionalPart('zFormatCode', optional(e, 'zFormatCode', v => string(v, 'zFormatCode'))),
            ...optionalPart('zScale', optional(e, 'zScale', v => oneOf(v, scales, 'AxisScale'))),
            ...optionalPart('centre', optional(e, 'centre', number)),
        };
        const notes = optionalPart('notes', optional(e, 'notes', v => list(v, 'notes').map(n => string(n, 'a note'))));
        // A grid's axes carry their titles and codes; the other forms name them.
        const titles = (): { xTitle: string; yTitle: string; xFormatCode?: string; yFormatCode?: string } => ({
            xTitle: string(e['xTitle'], 'xTitle'), yTitle: string(e['yTitle'], 'yTitle'),
            ...optionalPart('xFormatCode', optional(e, 'xFormatCode', v => string(v, 'xFormatCode'))),
            ...optionalPart('yFormatCode', optional(e, 'yFormatCode', v => string(v, 'yFormatCode'))),
        });
        switch (form) {
            case 'z':
                return SurfaceDefinition.grid({
                    x: surfaceAxis(e['x']), y: surfaceAxis(e['y']),
                    z: list(e['z'], 'z').map(row => list(row, 'a row of z').map(chartValue)),
                    ...common, ...notes,
                    ...optionalPart('method', optional(e, 'method', v => string(v, 'method'))),
                    ...optionalPart('interpolated', optional(e, 'interpolated', v => list(v, 'interpolated').map(row => list(row, 'a row of marks').map(b => boolean(b, 'a mark'))))),
                });
            case 'points':
                return SurfaceDefinition.fromPoints({
                    points: list(e['points'], 'points').map(p => {
                        const [x, y, z] = list(p, 'a point');
                        return { x: surfaceValue(x), y: surfaceValue(y), z: number(z) };
                    }),
                    ...titles(), ...common,
                });
            case 'nodes':
                return SurfaceDefinition.fromTriangles({
                    nodes: list(e['nodes'], 'nodes').map(p => {
                        const [x, y, z] = list(p, 'a node');
                        return { x: number(x), y: number(y), z: chartValue(z) };
                    }),
                    triangles: list(e['triangles'], 'triangles').map(t => {
                        const [a, b, c] = list(t, 'a triangle');
                        return { a: whole(a, 'a corner'), b: whole(b, 'a corner'), c: whole(c, 'a corner') };
                    }),
                    ...titles(), ...common, ...notes,
                    ...optionalPart('xScale', optional(e, 'xScale', v => oneOf(v, scales, 'AxisScale'))),
                    ...optionalPart('yScale', optional(e, 'yScale', v => oneOf(v, scales, 'AxisScale'))),
                });
            case 'interpolant': {
                const made = interpolant(allow(e['interpolant'], 'an interpolant', interpolantKeys(e['interpolant'])));
                if (!(made instanceof Interpolant2D)) {
                    throw new Error(`"${this.#name}": a surface interpolates in two dimensions.`);
                }
                return made.surface({
                    ...titles(), ...common,
                    ...optionalPart('samples', optional(e, 'samples', v => whole(v, 'samples'))),
                    ...optionalPart('xScale', optional(e, 'xScale', v => oneOf(v, scales, 'AxisScale'))),
                    ...optionalPart('yScale', optional(e, 'yScale', v => oneOf(v, scales, 'AxisScale'))),
                });
            }
            default:
                return sampledSurface(e['sampled'], titles(), common);
        }
    }

    #series(given: Json | undefined): ChartSeriesOptions {
        // An interpolated curve's series, as a Consumer hands it in (ADR-0257).
        if (isObject(given) && given['interpolant'] !== undefined) {
            const f = allow(given, 'an interpolated series', ['id', 'name', 'interpolant']);
            const made = interpolant(allow(f['interpolant'], 'an interpolant', interpolantKeys(f['interpolant'])));
            if (!(made instanceof Interpolant1D)) {
                throw new Error(`"${this.#name}": a series interpolates in one dimension.`);
            }
            return made.series(string(f['id'], 'a series\' id'), string(f['name'], 'a series\' name'));
        }
        // A fit's series, as a Consumer hands it in (ADR-0291): its curve says the rest.
        if (isObject(given) && given['curve'] !== undefined) {
            const f = allow(given, 'a fitted series', ['id', 'name', 'curve']);
            return fitted(allow(f['curve'], 'a curve', curveKeys(f['curve']))).series(string(f['id'], 'a series\' id'), string(f['name'], 'a series\' name'));
        }
        const e = allow(given, 'a series', [
            'id', 'name', 'kind', 'step', 'axis', 'panel', 'keys', 'fromZero', 'interpolated', 'segments', 'points', 'colourBy', 'sizeBy',
            'details', 'edges', 'bars', 'mergeTo', 'interval', 'xInterval', 'thinned', 'sampler', 'format', 'formatCode',
        ]);
        const id = string(e['id'], 'a series\' id');
        const kind = optional(e, 'kind', v => oneOf(v, ['level', 'rateOverInterval', 'amountPerPeriod', 'observation', 'priceRange'], 'SeriesKind'));
        return {
            id,
            name: string(e['name'], 'a series\' name'),
            // A series that leaves out its kind declares none, as only an untyped caller can, and the definition refuses it (ADR-0253).
            kind: kind ?? undeclared,
            ...optionalPart('step', optional(e, 'step', v => oneOf(v, ['preceding', 'following'], 'StepSide'))),
            ...optionalPart('axis', optional(e, 'axis', v => oneOf(v, ['left', 'right'], 'AxisSide'))),
            ...optionalPart('panel', optional(e, 'panel', v => string(v, 'panel'))),
            ...optionalPart('keys', optional(e, 'keys', appearanceKeys)),
            ...optionalPart('fromZero', optional(e, 'fromZero', v => boolean(v, 'fromZero'))),
            ...optionalPart('interpolated', optional(e, 'interpolated', v => boolean(v, 'interpolated'))),
            ...optionalPart('segments', optional(e, 'segments', v => list(v, 'segments').map(segmentOf))),
            ...optionalPart('points', optional(e, 'points', points)),
            ...optionalPart('colourBy', optional(e, 'colourBy', v => {
                const c = allow(v, 'colourBy', ['title', 'values', 'centre']);
                return {
                    title: string(c['title'], 'title'), values: list(c['values'], 'values').map(chartValue),
                    ...optionalPart('centre', optional(c, 'centre', number)),
                };
            })),
            ...optionalPart('sizeBy', optional(e, 'sizeBy', v => {
                const c = allow(v, 'sizeBy', ['title', 'values']);
                return { title: string(c['title'], 'title'), values: list(c['values'], 'values').map(chartValue) };
            })),
            ...optionalPart('details', optional(e, 'details', v => list(v, 'details').map(detail))),
            ...optionalPart('edges', optional(e, 'edges', v => list(v, 'edges').map(edge))),
            ...optionalPart('bars', optional(e, 'bars', bars)),
            ...optionalPart('mergeTo', optional(e, 'mergeTo', v => oneOf(v, ['row', 'week', 'month', 'quarter', 'year'], 'CandlePeriod'))),
            ...optionalPart('interval', optional(e, 'interval', interval)),
            ...optionalPart('xInterval', optional(e, 'xInterval', interval)),
            ...optionalPart('thinned', optional(e, 'thinned', summaries)),
            ...optionalPart('sampler', optional(e, 'sampler', v => this.#sampler(id, v))),
            ...optionalPart('format', optional(e, 'format', seriesFormat)),
            ...optionalPart('formatCode', optional(e, 'formatCode', v => string(v, 'formatCode'))),
        };
    }

    #sampler(id: string, given: Json): SeriesSampler {
        const e = allow(given, 'a sampler', ['from', 'to', 'value']);
        const asked: number[] = [];
        this.#asked.set(id, asked);
        const forms = Object.entries(allow(e['value'], 'a sampled function', ['line', 'split']));
        const [form] = forms;
        if (form === undefined || forms.length > 1) {
            throw new Error(`A sampled function is one of line and split, not ${text(e['value'])}.`);
        }
        const [name, args] = form;
        const f = sampled(name, list(args, name).map(number));
        return {
            from: position(e['from']),
            to: position(e['to']),
            valueAt: x => {
                asked.push(x);
                return f(x);
            },
        };
    }

    // ---- The checks ----

    #check(root: Json, scope: Json, given: Json): void {
        const check = allow(given, 'a check', checkKeys);
        const where = `"${this.#name}", check ${JSON.stringify(check)}`;
        if (!assertions.some(a => check[a] !== undefined)) {
            throw new Error(`${where} asserts nothing.`);
        }
        // A check compares exactly unless it, or its file, names how near is the same (tests/cases/README.md).
        const fileNear = this.#file.near === undefined ? 0 : this.#file.near;
        const near = check['near'] !== undefined ? number(check['near']) : fileNear;
        let subject = this.#subject(root, scope, check, where);

        if (check['where'] !== undefined) {
            const wanted = expected(check['where']);
            subject = items(subject, where).filter(item => matches(wanted, item, near));
        }
        if (check['sortBy'] !== undefined) {
            const by = string(check['sortBy'], 'sortBy');
            subject = items(subject, where).toSorted((a, b) => asNumber(resolve(root, a, by, where), where) - asNumber(resolve(root, b, by, where), where));
        }
        if (check['map'] !== undefined) {
            const by = string(check['map'], 'map');
            subject = items(subject, where).map(item => resolve(root, item, by, where));
        }
        if (check['differences'] === true) {
            const xs = items(subject, where).map(x => asNumber(x, where));
            subject = xs.slice(1).map((x, i) => x - elementAtNumber(xs, i));
        }
        if (check['sum'] === true) {
            subject = items(subject, where).reduce((total: number, x) => total + asNumber(x, where), 0);
        }
        if (check['count'] === true) {
            subject = items(subject, where).length;
        }

        for (const [key, v] of Object.entries(check)) {
            const said = `${where}: is ${text(subject)}`;
            switch (key) {
                case 'is':
                    holds(same(expected(v), subject, near), said);
                    break;
                case 'isNot':
                    holds(!same(expected(v), subject, near), said);
                    break;
                case 'above':
                    holds(asNumber(subject, where) > this.#reference(root, scope, v, where), said);
                    break;
                case 'below':
                    holds(asNumber(subject, where) < this.#reference(root, scope, v, where), said);
                    break;
                case 'atLeast':
                    holds(asNumber(subject, where) >= this.#reference(root, scope, v, where) - near, said);
                    break;
                case 'atMost':
                    holds(asNumber(subject, where) <= this.#reference(root, scope, v, where) + near, said);
                    break;
                case 'sameAs': {
                    const other = otherOf(root, scope, v, where);
                    holds(same(other, subject, near), `${said}, and the other ${text(other)}`);
                    break;
                }
                case 'differsFrom':
                    holds(!same(otherOf(root, scope, v, where), subject, near), `${where}: both are ${text(subject)}`);
                    break;
                case 'has':
                    holds(has(subject, expected(v), near, where), said);
                    break;
                case 'lacks':
                    holds(!has(subject, expected(v), near, where), said);
                    break;
                case 'mentions': {
                    const wanted = string(v, 'mentions');
                    holds(items(subject, where).some(item => typeof item === 'string' && item.includes(wanted)), said);
                    break;
                }
                case 'empty':
                    holds((items(subject, where).length === 0) === boolean(v, 'empty'), said);
                    break;
                case 'evenlySpaced': {
                    const xs = items(subject, where).map(x => asNumber(x, where));
                    const gaps = xs.slice(1).map((x, i) => x - elementAtNumber(xs, i));
                    const [first] = gaps;
                    const even = first !== undefined && gaps.every(g => Math.abs(g - first) <= Math.max(near, 1e-6));
                    holds(even === boolean(v, 'evenlySpaced'), said);
                    break;
                }
                case 'startsWith':
                    holds(typeof subject === 'string' && subject.startsWith(string(v, 'startsWith')), said);
                    break;
                case 'each': {
                    const all = items(subject, where);
                    holds(all.length > 0, `${where}: has nothing to check each of.`);
                    for (const item of all) {
                        for (const inner of list(v, 'each')) {
                            this.#check(root, item, inner);
                        }
                    }
                    break;
                }
                case 'then':
                    for (const inner of list(v, 'then')) {
                        this.#check(root, subject, inner);
                    }
                    break;
                default:
                    break;
            }
        }
    }

    // What a check reads: a path, a vertical axis' height of a value, the position at a pixel across, or the
    // positions a sampled series was asked at.
    #subject(root: Json, scope: Json, check: JsonObject, where: string): Json {
        const forms = ['at', 'yOf', 'positionAt', 'asked'].filter(k => check[k] !== undefined);
        const [form] = forms;
        if (form === undefined || forms.length > 1) {
            throw new Error(`${where} reads ${String(forms.length)} things; a check reads one.`);
        }
        const of = check['of'] === undefined ? '' : string(check['of'], 'of');
        switch (form) {
            case 'at':
                return resolve(root, scope, string(check['at'], 'at'), where);
            case 'asked': {
                const id = string(check['asked'], 'asked');
                const asked = this.#asked.get(id);
                if (asked === undefined) {
                    throw new Error(`${where}: no sampled series is named "${id}".`);
                }
                return [...asked];
            }
            default:
                return this.#computed(root, scope, check, form, of, where);
        }
    }

    #computed(root: Json, scope: Json, e: JsonObject, form: string, of: string, where: string): number {
        const scene = this.#scene(of, where);
        if (form === 'positionAt') {
            return positionAt(scene, this.#reference(root, scope, e['positionAt'], where));
        }
        const args = list(e['yOf'], 'yOf');
        const panel = scene.panels[whole(args[0], 'a panel')];
        if (panel === undefined) {
            throw new Error(`${where}: the scene has no panel ${text(args[0])}.`);
        }
        const side = string(args[1], 'a side');
        const axisOf = side === 'right' ? panel.right : panel.left;
        if (axisOf === null) {
            throw new Error(`${where}: that panel has no ${side} axis.`);
        }
        return yOf(axisOf, this.#reference(root, scope, args[2], where), panel.plot);
    }

    #scene(of: string, where: string): ChartScene {
        const result = this.#results.get(of);
        if (!isScene(result)) {
            throw new Error(`${where}: "${of}" is not a scene.`);
        }
        return result;
    }

    // A number a check compares with: a literal, or something read, scaled by `times` and moved by `plus`.
    #reference(root: Json, scope: Json, given: Json | undefined, where: string): number {
        if (!isObject(given)) {
            return number(given);
        }
        const e = allow(given, 'a reference', ['at', 'yOf', 'positionAt', 'of', 'times', 'plus']);
        const forms = ['at', 'yOf', 'positionAt'].filter(k => e[k] !== undefined);
        const [form] = forms;
        if (form === undefined || forms.length > 1) {
            throw new Error(`${where}: a reference reads one thing.`);
        }
        const of = e['of'] === undefined ? '' : string(e['of'], 'of');
        let v = form === 'at' ? asNumber(resolve(root, scope, string(e['at'], 'at'), where), where) : this.#computed(root, scope, e, form, of, where);
        if (e['times'] !== undefined) {
            v *= number(e['times']);
        }
        if (e['plus'] !== undefined) {
            v += this.#reference(root, scope, e['plus'], where);
        }
        return v;
    }
}

/** Holds a case to what it asserts, as an assertion Vitest counts. */
function holds(ok: boolean, message: string): void {
    expect(ok, message).toBe(true);
}

/** What a refusal is named in the cases. */
function refusal(error: Error): string {
    if (error.name === 'ChartRefusedError') {
        return 'chartRefused';
    }
    if (error instanceof ChartSpecRefusedError) {
        return 'chartSpecRefused';
    }
    if (error instanceof RangeError) {
        return 'outOfRange';
    }
    if (error instanceof TypeError) {
        return 'argument';
    }
    return error.constructor === Error ? 'invalidOperation' : error.name;
}

// A sampled function: `line` [a, b] is a + b·x; `split` [at, below, from] is `below` left of `at` and `from` after.
function sampled(name: string, a: readonly number[]): (x: number) => number {
    if (name === 'line') {
        const [p, q] = a;
        if (p === undefined || q === undefined || a.length !== 2) {
            throw new Error(`line takes two numbers, not ${text(a)}.`);
        }
        return x => p + q * x;
    }
    const [at, below, from] = a;
    if (at === undefined || below === undefined || from === undefined || a.length !== 3) {
        throw new Error(`split takes three numbers, not ${text(a)}.`);
    }
    return x => (x < at ? below : from);
}

function isScene(value: unknown): value is ChartScene {
    return typeof value === 'object' && value !== null && 'panels' in value && 'positions' in value;
}

// The part of an options object a case names: the key and its value, or nothing where the case leaves it out.
function optionalPart<K extends string, T>(key: K, v: T | undefined): Partial<Record<K, T>> {
    const part: Partial<Record<K, T>> = {};
    if (v !== undefined) {
        part[key] = v;
    }
    return part;
}

function oneOf<const T extends string>(v: Json | undefined, names: readonly T[], what: string): T {
    const found = names.find(n => n === v);
    if (found === undefined) {
        throw new Error(`${text(v)} is not a ${what}.`);
    }
    return found;
}

// The scene a case lays out: its metrics, and its view, style, independent panels and legend where it names them.
function laidOut(definition: ChartDefinition, a: JsonObject): ChartScene {
    return buildScene(definition, metrics(a['metrics']), {
        ...optionalPart('view', optional(a, 'view', view)),
        ...optionalPart('style', optional(a, 'style', style)),
        ...optionalPart('independentScales', optional(a, 'independentScales', v => list(v, 'independentScales').map(s => string(s, 'a panel')))),
        ...optionalPart('legend', optional(a, 'legend', legend)),
    });
}

// ---- An interpolation (ADR-0257) ----

const interpolations = ['linear', 'naturalCubicSpline', 'bilinear', 'triangles'] as const;

// What an interpolant says: its method, and a curve's `x` and `y`, a grid's `x`, `y` and `z`, or a mesh's
// `points` and `triangles`.
function interpolantKeys(given: Json | undefined): string[] {
    const method = oneOf(isObject(given) ? given['method'] : undefined, interpolations, 'interpolation');
    switch (method) {
        case 'linear':
        case 'naturalCubicSpline':
            return ['method', 'x', 'y'];
        case 'bilinear':
            return ['method', 'x', 'y', 'z'];
        case 'triangles':
            return ['method', 'points', 'triangles'];
        default:
            throw new Error(`Internal: ${String(method)} is an interpolation and has no keys.`);
    }
}

function interpolant(e: JsonObject): Interpolant1D | Interpolant2D {
    const method = oneOf(e['method'], interpolations, 'interpolation');
    const numbers = (key: string): number[] => list(e[key], key).map(number);
    switch (method) {
        case 'linear':
            return interpolateLinear(numbers('x'), numbers('y'));
        case 'naturalCubicSpline':
            return interpolateNaturalCubicSpline(numbers('x'), numbers('y'));
        case 'bilinear':
            return interpolateBilinear(numbers('x'), numbers('y'), list(e['z'], 'z').map(r => list(r, 'a row of z').map(v => (v === null ? null : number(v)))));
        case 'triangles':
            return interpolateTriangles(
                list(e['points'], 'points').map(p => {
                    const [x, y, z] = list(p, 'a point');
                    return { x: number(x), y: number(y), z: number(z) };
                }),
                list(e['triangles'], 'triangles').map(t => {
                    const [a, b, c] = list(t, 'a triangle');
                    return { a: whole(a, 'a corner'), b: whole(b, 'a corner'), c: whole(c, 'a corner') };
                }));
        default:
            throw new Error(`Internal: ${String(method)} is an interpolation and not run.`);
    }
}

// ---- A fit (ADR-0291) ----

// What a curve says: its method, its points and its space, and a degree where it is a polynomial.
function curveKeys(given: Json | undefined): string[] {
    const keys = ['method', 'x', 'y', 'space'];
    return isObject(given) && given['method'] === 'polynomial' ? [...keys, 'degree'] : keys;
}

// A curve fitted by the method the case names, over its points, in the space it names or, where it names
// none, in the method's own default.
function fitted(e: JsonObject): FittedCurve {
    const method = oneOf(e['method'], ['leastSquares', 'theilSen', 'polynomial'], 'fit');
    const xs = run(e['x'], 'x');
    const ys = run(e['y'], 'y');
    const space = optional(e, 'space', v => oneOf<FitSpace>(v, ['linear', 'logY', 'logLog'], 'FitSpace'));
    switch (method) {
        case 'leastSquares':
            return space === undefined ? fitLeastSquares(xs, ys) : fitLeastSquares(xs, ys, space);
        case 'theilSen':
            return space === undefined ? fitTheilSen(xs, ys) : fitTheilSen(xs, ys, space);
        case 'polynomial': {
            const degree = whole(e['degree'], 'degree');
            return space === undefined ? fitPolynomial(xs, ys, degree) : fitPolynomial(xs, ys, degree, space);
        }
        default:
            throw new Error(`Internal: ${String(method)} is a fit and not run.`);
    }
}

// A fit's xs or ys: numbers, or a run of `count` from `from` by `step`.
function run(given: Json | undefined, what: string): number[] {
    if (isList(given)) {
        return given.map(number);
    }
    const r = allow(allow(given, what, ['run'])['run'], 'a run', ['count', 'from', 'step']);
    const from = number(r['from']);
    const step = number(r['step']);
    return Array.from({ length: whole(r['count'], 'count') }, (_, i) => from + i * step);
}

function axis(given: Json | undefined): ChartAxisOptions {
    const e = allow(given, 'an axis', ['values', 'title', 'scale', 'labels', 'named', 'daysPerYear', 'packed', 'formatCode']);
    return {
        values: oneOf(e['values'], ['dates', 'numbers', 'labels'], 'AxisValues'),
        ...optionalPart('title', optional(e, 'title', v => string(v, 'title'))),
        ...optionalPart('scale', optional(e, 'scale', v => oneOf(v, ['proportional', 'categories', 'logarithmic'], 'AxisScale'))),
        ...optionalPart('labels', optional(e, 'labels', v => list(v, 'labels').map(l => string(l, 'a label')))),
        ...optionalPart('named', optional(e, 'named', v => list(v, 'named').map(n => {
            const pair = list(n, 'a named position');
            return [position(pair[0]), string(pair[1], 'a label')] as const;
        }))),
        ...optionalPart('daysPerYear', optional(e, 'daysPerYear', number)),
        ...optionalPart('packed', optional(e, 'packed', v => boolean(v, 'packed'))),
        ...optionalPart('formatCode', optional(e, 'formatCode', v => string(v, 'formatCode'))),
    };
}

function appearanceKeys(given: Json): AppearanceKeys {
    const e = allow(given, 'keys', ['colour', 'lineStyle', 'marker']);
    return {
        colour: string(e['colour'], 'colour'),
        ...optionalPart('lineStyle', optional(e, 'lineStyle', v => string(v, 'lineStyle'))),
        ...optionalPart('marker', optional(e, 'marker', v => string(v, 'marker'))),
    };
}

function segmentOf(given: Json): Segment {
    const g = allow(given, 'a segment', ['from', 'to', 'method', 'extrapolated']);
    return {
        from: position(g['from']), to: position(g['to']), method: string(g['method'], 'method'),
        extrapolated: g['extrapolated'] === undefined ? false : boolean(g['extrapolated'], 'extrapolated'),
    };
}

function detail(given: Json): PointDetail {
    const d = allow(given, 'a detail', ['title', 'values']);
    return {
        title: string(d['title'], 'title'),
        values: list(d['values'], 'values').map(v => (v === null ? null : string(v, 'a detail'))),
    };
}

// A Row Key: a text or a number, as a Consumer's row hands it in.
function rowKey(given: Json | undefined): string | number {
    if (typeof given === 'string' || typeof given === 'number') {
        return given;
    }
    throw new Error(`${text(given)} is not a Row Key a case can write.`);
}

function edge(given: Json): ChartEdge {
    const pair = list(given, 'an edge');
    return { from: rowKey(pair[0]), to: rowKey(pair[1]) };
}

function points(given: Json): ChartPoint[] {
    const result: ChartPoint[] = [];
    for (const item of list(given, 'points')) {
        if (isList(item)) {
            const name = item[2];
            result.push({ x: position(item[0]), y: chartValue(item[1]), ...optionalPart('name', name === undefined ? undefined : string(name, 'a name')) });
        } else if (isObject(item) && item['generate'] !== undefined) {
            const g = allow(allow(item, 'a generated run', ['generate'])['generate'], 'generate', ['count', 'x', 'y', 'name', 'at']);
            const at = g['at'] === undefined ? {} : g['at'];
            if (!isObject(at)) {
                throw new Error(`at is ${text(at)}, not values by index.`);
            }
            const prefix = g['name'] === undefined ? undefined : string(g['name'], 'name');
            for (let i = 0; i < whole(g['count'], 'count'); i++) {
                const over = at[String(i)];
                result.push({
                    x: across(g['x'], i),
                    y: over === undefined ? byIndex(g['y'], i) : chartValue(over),
                    ...optionalPart('name', prefix === undefined ? undefined : `${prefix}${String(i)}`),
                });
            }
        } else {
            const p = allow(item, 'a point', ['x', 'y', 'name', 'row']);
            result.push({
                x: position(p['x']), y: chartValue(p['y']),
                ...optionalPart('name', optional(p, 'name', v => string(v, 'name'))),
                ...optionalPart('row', optional(p, 'row', rowKey)),
            });
        }
    }
    return result;
}

// A generated position: `from` and then a `step` a point.
function across(given: Json | undefined, i: number): number {
    const e = allow(given, 'x', ['from', 'step']);
    return position(e['from']) + i * number(e['step']);
}

// A generated value by its index: a constant, a line, a sine or a remainder.
function byIndex(given: Json | undefined, i: number): ChartValue | null {
    if (!isObject(given)) {
        return chartValue(given);
    }
    const forms = Object.entries(allow(given, 'a generated value', ['line', 'sin', 'mod']));
    const [form] = forms;
    if (form === undefined || forms.length > 1) {
        throw new Error(`A generated value is one of line, sin and mod, not ${text(given)}.`);
    }
    const [name, args] = form;
    switch (name) {
        case 'line': {
            const [a, b] = list(args, 'line').map(number);
            if (a === undefined || b === undefined) {
                throw new Error(`line takes two numbers, not ${text(args)}.`);
            }
            return a + b * i;
        }
        case 'sin':
            return Math.sin(i / number(args));
        case 'mod':
            return i % whole(args, 'mod');
        default:
            throw new Error(`${name} is not a generated value.`);
    }
}

function bars(given: Json): PriceBar[] {
    if (isList(given)) {
        return given.map(b => {
            const row = list(b, 'a bar');
            return {
                x: position(row[0]), open: chartValue(row[1]), high: chartValue(row[2]), low: chartValue(row[3]), close: chartValue(row[4]),
                volume: row.length > 5 ? chartValue(row[5]) : null,
            };
        });
    }
    const w = allow(allow(given, 'bars', ['weekdays'])['weekdays'], 'weekdays', ['count', 'from', 'volume', 'withoutOpen', 'set']);
    // Weekdays from a Monday, a fixed walk whose open, high, low and close always agree.
    let x = w['from'] === undefined ? dayNumber(2026, 1, 5) : position(w['from']);
    const volume = w['volume'] !== undefined && boolean(w['volume'], 'volume');
    let price = 100;
    let result: PriceBar[] = [];
    for (let i = 0; i < whole(w['count'], 'count'); i++) {
        // Day 0, 0001-01-01, is a Monday.
        while (x % 7 === 5 || x % 7 === 6) {
            x++;
        }
        const close = price + (i % 7 - 3);
        result.push({ x, open: price, high: Math.max(price, close) + 2, low: Math.min(price, close) - 2, close, volume: volume ? 1000 + i : null });
        price = close;
        x++;
    }
    if (w['withoutOpen'] !== undefined && boolean(w['withoutOpen'], 'withoutOpen')) {
        result = result.map(b => ({ ...b, open: null }));
    }
    if (w['set'] !== undefined) {
        const set = w['set'];
        if (!isObject(set)) {
            throw new Error(`set is ${text(set)}, not values by index.`);
        }
        for (const [index, values] of Object.entries(set)) {
            const row = allow(values, 'a bar\'s values', ['open', 'high', 'low', 'close', 'volume']);
            const i = Number(index);
            const b = result[i];
            if (b === undefined) {
                throw new Error(`There is no bar ${index} to set.`);
            }
            result[i] = {
                ...b,
                ...optionalPart('open', optional(row, 'open', chartValue)),
                ...optionalPart('high', optional(row, 'high', chartValue)),
                ...optionalPart('low', optional(row, 'low', chartValue)),
                ...optionalPart('close', optional(row, 'close', chartValue)),
                ...optionalPart('volume', optional(row, 'volume', chartValue)),
            };
        }
    }
    return result;
}

function interval(given: Json): SeriesInterval {
    const e = allow(given, 'an interval', ['meaning', 'bounds', 'style']);
    const b = e['bounds'];
    let bounds: IntervalBound[];
    if (isList(b)) {
        bounds = b.map(row => {
            const r = list(row, 'a bound');
            return { x: position(r[0]), lower: chartValue(r[1]), upper: chartValue(r[2]) };
        });
    } else {
        const g = allow(allow(b, 'bounds', ['generate'])['generate'], 'generated bounds', ['count', 'x', 'lower', 'upper', 'missing']);
        const missing = new Set(g['missing'] === undefined ? [] : list(g['missing'], 'missing').map(i => whole(i, 'a missing index')));
        bounds = Array.from({ length: whole(g['count'], 'count') }, (_, i) => ({
            x: across(g['x'], i), lower: missing.has(i) ? null : byIndex(g['lower'], i), upper: byIndex(g['upper'], i),
        }));
    }
    return {
        meaning: string(e['meaning'], 'meaning'), bounds,
        style: e['style'] === undefined ? 'whiskers' : oneOf(e['style'], ['whiskers', 'band'], 'IntervalStyle'),
    };
}

function summaries(given: Json | undefined): PixelSummary[] {
    return list(given, 'pixel summaries').map(p => {
        const s = allow(p, 'a pixel summary', ['from', 'to', 'min', 'max', 'count']);
        return { from: position(s['from']), to: position(s['to']), min: number(s['min']), max: number(s['max']), count: whole(s['count'], 'count') };
    });
}

function drawing(given: Json | undefined): SeriesDrawing {
    const e = allow(given, 'a drawing', ['lines', 'markers']);
    const lines: (readonly DrawnPoint[])[] = [];
    for (const line of list(e['lines'], 'lines')) {
        if (isObject(line) && line['levels'] !== undefined) {
            // A step's levels, each from i to i + 1.
            const levels = allow(line['levels'], 'levels', ['count', 'y']);
            for (let i = 0; i < whole(levels['count'], 'count'); i++) {
                const level = byIndex(levels['y'], i);
                if (level === null) {
                    throw new Error(`Level ${String(i)} has no height.`);
                }
                const y = Number(level);
                lines.push([{ x: i, y }, { x: i + 1, y }]);
            }
            continue;
        }
        lines.push(points(line).map(p => {
            if (p.y === null) {
                throw new Error(`A drawn line has no gap; ${roundTrip(p.x)} has no height.`);
            }
            return { x: p.x, y: Number(p.y) };
        }));
    }
    const markers = list(e['markers'], 'markers').map(m => {
        const row = list(m, 'a marker');
        return marker(number(row[0]), number(row[1]), boolean(row[2], 'filled'));
    });
    return seriesDrawing(lines, markers);
}

function metrics(given: Json | undefined): ChartMetrics {
    const e = allow(given, 'metrics', ['width', 'height', 'devicePixelRatio', 'lineHeight', 'padding', 'panelGap', 'textWidth', 'colourSlots']);
    return {
        width: number(e['width']), height: number(e['height']),
        ...optionalPart('devicePixelRatio', optional(e, 'devicePixelRatio', number)),
        ...optionalPart('lineHeight', optional(e, 'lineHeight', number)),
        ...optionalPart('padding', optional(e, 'padding', number)),
        ...optionalPart('panelGap', optional(e, 'panelGap', number)),
        ...optionalPart('textWidth', optional(e, 'textWidth', textWidth)),
        ...optionalPart('colourSlots', optional(e, 'colourSlots', v => whole(v, 'colourSlots'))),
    };
}

// What an untyped caller's series holds where it declares no kind.
const undeclared = undefined as unknown as SeriesKind;

const scales: readonly AxisScale[] = ['proportional', 'categories', 'logarithmic'];

function view(given: Json | undefined): ChartView {
    const e = allow(given, 'a view', ['hidden', 'from', 'to', 'scale', 'yFrom', 'yTo', 'yScale']);
    return {
        hidden: e['hidden'] === undefined ? [] : list(e['hidden'], 'hidden').map(h => string(h, 'a hidden key')),
        ...optionalPart('from', optional(e, 'from', position)),
        ...optionalPart('to', optional(e, 'to', position)),
        ...optionalPart('scale', optional(e, 'scale', v => oneOf(v, scales, 'AxisScale'))),
        ...optionalPart('yFrom', optional(e, 'yFrom', number)),
        ...optionalPart('yTo', optional(e, 'yTo', number)),
        ...optionalPart('yScale', optional(e, 'yScale', v => oneOf(v, scales, 'AxisScale'))),
    };
}

// What every form of surface may say, and what each form reads besides.
const surfaceCommonKeys = ['zTitle', 'zFormat', 'zFormatCode', 'zScale', 'centre'];
const surfaceFormKeys: ReadonlyMap<string, readonly string[]> = new Map([
    ['z', ['x', 'y', 'z', 'method', 'interpolated', 'notes']],
    ['points', ['points', 'xTitle', 'yTitle', 'xFormatCode', 'yFormatCode']],
    ['nodes', ['nodes', 'triangles', 'xTitle', 'yTitle', 'xScale', 'yScale', 'xFormatCode', 'yFormatCode', 'notes']],
    ['sampled', ['sampled', 'xTitle', 'yTitle', 'xFormatCode', 'yFormatCode']],
    ['interpolant', ['interpolant', 'xTitle', 'yTitle', 'samples', 'xScale', 'yScale', 'xFormatCode', 'yFormatCode']],
]);

function surfaceAxis(given: Json | undefined): SurfaceAxisOptions {
    const e = allow(given, 'a surface axis', ['title', 'values', 'scale', 'format', 'formatCode']);
    return {
        title: string(e['title'], 'title'),
        values: list(e['values'], 'values').map(surfaceValue),
        ...optionalPart('format', optional(e, 'format', valueFormat)),
        ...optionalPart('formatCode', optional(e, 'formatCode', v => string(v, 'formatCode'))),
        ...optionalPart('scale', optional(e, 'scale', v => oneOf(v, scales, 'AxisScale'))),
    };
}

// A surface axis' value: a text, or a number.
function surfaceValue(given: Json | undefined): SurfaceValue {
    return typeof given === 'string' ? given : number(given);
}

function slice(given: Json | undefined): SurfaceSlice {
    const e = allow(given, 'a slice', ['axis', 'value']);
    return { axis: oneOf(e['axis'], ['x', 'y'], 'SurfaceSliceAxis'), value: surfaceValue(e['value']) };
}

// A function sampled over a rectangle: a constant, or a plane [a, b, c], a + b·x + c·y.
function sampledSurface(given: Json | undefined, titles: { xTitle: string; yTitle: string; xFormatCode?: string; yFormatCode?: string }, common: SurfaceScaleOptions & {
    readonly zTitle?: string;
    readonly zFormat?: (value: ChartValue) => string;
}): SurfaceDefinition {
    const e = allow(given, 'sampled', ['method', 'x', 'y', 'z', 'samples', 'xScale', 'yScale']);
    const [xFrom, xTo] = list(e['x'], 'x').map(number);
    const [yFrom, yTo] = list(e['y'], 'y').map(number);
    if (xFrom === undefined || xTo === undefined || yFrom === undefined || yTo === undefined) {
        throw new Error('A sampled surface\'s x and y are each [from, to].');
    }
    const z = e['z'];
    let f: (x: number, y: number) => ChartValue;
    if (typeof z === 'number') {
        f = () => z;
    } else {
        const [a, b, c] = list(allow(z, 'a sampled z', ['plane'])['plane'], 'a plane').map(number);
        if (a === undefined || b === undefined || c === undefined) {
            throw new Error('A plane is [a, b, c].');
        }
        f = (x, y) => a + b * x + c * y;
    }
    return SurfaceDefinition.sampled({
        method: string(e['method'], 'method'), xFrom, xTo, yFrom, yTo, z: f, ...titles, ...common,
        ...optionalPart('samples', optional(e, 'samples', v => whole(v, 'samples'))),
        ...optionalPart('xScale', optional(e, 'xScale', v => oneOf(v, scales, 'AxisScale'))),
        ...optionalPart('yScale', optional(e, 'yScale', v => oneOf(v, scales, 'AxisScale'))),
    });
}

// A writer of a value: a prefix and a suffix around it, written with a fixed number of places or invariantly,
// as the C# runner writes with "F" and the invariant culture.
function valueFormat(given: Json): (value: SurfaceValue | ChartValue) => string {
    const e = allow(given, 'a format', ['prefix', 'suffix', 'places']);
    const prefix = e['prefix'] === undefined ? '' : string(e['prefix'], 'prefix');
    const suffix = e['suffix'] === undefined ? '' : string(e['suffix'], 'suffix');
    const places = optional(e, 'places', v => whole(v, 'places'));
    return v => {
        if (places === undefined) {
            return `${prefix}${typeof v === 'string' ? v : invariantText(v)}${suffix}`;
        }
        if (typeof v === 'string') {
            throw new TypeError(`"${v}" is not a number to write with ${String(places)} places.`);
        }
        return `${prefix}${Number(v).toFixed(places)}${suffix}`;
    };
}

function style(given: Json | undefined): AxisStyle {
    const e = allow(given, 'a style', ['placement', 'label', 'angle']);
    return {
        ...optionalPart('placement', optional(e, 'placement', v => oneOf(v, ['auto', 'calendar', 'everyPoint', 'tenors', 'named'], 'TickPlacement'))),
        ...optionalPart('label', optional(e, 'label', v => oneOf(v, ['auto', 'year', 'yearMonth', 'date', 'monthYear', 'tenor'], 'DateLabel'))),
        ...optionalPart('angle', optional(e, 'angle', v => whole(v, 'angle'))),
    };
}

function legend(given: Json): LegendPlacement {
    const e = allow(given, 'a legend', ['position', 'align']);
    return {
        ...optionalPart('position', optional(e, 'position', v => oneOf(v, ['top', 'bottom', 'left', 'right', 'none'], 'LegendPosition'))),
        ...optionalPart('align', optional(e, 'align', v => oneOf(v, ['start', 'centre', 'end'], 'LegendAlign'))),
    };
}

// A map from a position to a pixel: multiplied by `scale` or divided by `over`, then moved by `offset`.
function linear(given: Json | undefined): (x: number) => number {
    const e = allow(given, 'a map', ['scale', 'over', 'offset']);
    const offset = e['offset'] === undefined ? 0 : number(e['offset']);
    if ((e['scale'] === undefined) === (e['over'] === undefined)) {
        throw new Error(`A map ${text(e)} names one of scale and over.`);
    }
    if (e['over'] !== undefined) {
        const d = number(e['over']);
        return x => x / d + offset;
    }
    const k = number(e['scale']);
    return x => x * k + offset;
}

function textWidth(given: Json | undefined): (s: string) => number {
    const each = number(allow(given, 'a text width', ['perCharacter'])['perCharacter']);
    return s => s.length * each;
}

function seriesFormat(given: Json): (v: ChartValue) => string {
    const e = allow(given, 'a format', ['prefix', 'suffix']);
    const prefix = e['prefix'] === undefined ? '' : string(e['prefix'], 'prefix');
    const suffix = e['suffix'] === undefined ? '' : string(e['suffix'], 'suffix');
    return v => `${prefix}${invariantText(v)}${suffix}`;
}

// The .NET format strings the engine writes with, and the function of the Script Core that writes each.
function format(name: string, v: number): string {
    switch (name) {
        case '0.####':
            return upToPlaces(v, 4);
        case '0.##':
            return upToPlaces(v, 2);
        case 'R':
            return roundTrip(v);
        case 'G3':
            return threeSignificant(v);
        case 'N0':
            return grouped(v);
        case '0.###':
            return upToPlaces(v, 3);
        case '0.00':
            return twoPlaces(v);
        default:
            throw new Error(`"${name}" is not a format the engine writes with.`);
    }
}

// ---- How a result is read ----

/**
 * A result as the plain JSON a case compares it as, as C#'s runner serialises a record: a map as an
 * object, a class by its getters, a bigint as the number it is, a number JSON cannot hold by its name, a
 * function left out and an absent value as null.
 */
export function plain(v: unknown): Json {
    if (v === null) {
        return null;
    }
    switch (typeof v) {
        case 'undefined':
            return null;
        case 'string':
        case 'boolean':
            return v;
        case 'number':
            return Number.isFinite(v) ? v : Number.isNaN(v) ? 'NaN' : v > 0 ? 'Infinity' : '-Infinity';
        case 'bigint':
            return Number(v);
        case 'function':
        case 'symbol':
            return null;
        case 'object':
            break;
        default:
            throw new Error(`Internal: a ${typeof v} has no plain form.`);
    }
    if (v instanceof Map) {
        const o: Record<string, Json> = {};
        for (const [k, item] of v) {
            o[String(k)] = plain(item);
        }
        return o;
    }
    if (v instanceof Set || Array.isArray(v)) {
        return Array.from<unknown, Json>(v, plain);
    }
    const o: Record<string, Json> = {};
    for (const [k, item] of Object.entries(v)) {
        if (typeof item !== 'function') {
            o[k] = plain(item);
        }
    }
    // A class's data is its getters.
    for (let proto: unknown = Object.getPrototypeOf(v); proto !== null && proto !== Object.prototype; proto = Object.getPrototypeOf(proto)) {
        for (const [k, descriptor] of Object.entries(Object.getOwnPropertyDescriptors(proto))) {
            if (descriptor.get !== undefined && !(k in o)) {
                o[k] = plain(Reflect.get(v, k));
            }
        }
    }
    return o;
}

/**
 * The value at a path: dot-separated, read from the scope or, starting `$.`, from the root. A segment is a
 * property, an index (negative from the end), `length` of a list, or `[key=value]`, the one element of a
 * list whose key holds that value. A property a plain object leaves out reads as null, as C#'s null.
 */
export function resolve(root: Json, scope: Json, path: string, where: string): Json {
    let node = scope;
    let rest = path;
    if (rest.startsWith('$')) {
        node = root;
        rest = rest.length > 1 ? rest.slice(2) : '';
    }
    for (const segment of segments(rest)) {
        if (isList(node)) {
            if (segment === 'length') {
                node = node.length;
            } else if (segment.startsWith('[')) {
                const eq = segment.indexOf('=');
                const key = segment.slice(1, eq);
                const wanted = literal(segment.slice(eq + 1, -1));
                const found = node.filter(item => same(wanted, resolve(root, item, key, where), 0));
                const [one] = found;
                if (one === undefined || found.length > 1) {
                    throw new Error(`${where}: ${String(found.length)} elements match ${segment}.`);
                }
                node = one;
            } else {
                const n = Number(segment);
                if (!Number.isInteger(n)) {
                    throw new Error(`${where}: ${segment} reads into a list.`);
                }
                const i = n < 0 ? n + node.length : n;
                const item = node[i];
                if (item === undefined) {
                    throw new Error(`${where}: ${segment} is outside a list of ${String(node.length)}.`);
                }
                node = item;
            }
        } else if (isObject(node)) {
            const item = node[segment];
            node = item === undefined ? null : item;
        } else {
            throw new Error(`${where}: ${segment} reads into ${text(node)}.`);
        }
    }
    return node;
}

function segments(path: string): string[] {
    const result: string[] = [];
    let depth = 0;
    let start = 0;
    for (let i = 0; i < path.length; i++) {
        const c = path[i];
        if (c === '[') {
            depth++;
        } else if (c === ']') {
            depth--;
        } else if (c === '.' && depth === 0) {
            result.push(path.slice(start, i));
            start = i + 1;
        }
    }
    if (path.length > 0) {
        result.push(path.slice(start));
    }
    return result;
}

// A selector's value: JSON where it reads as JSON, a text otherwise.
function literal(t: string): Json {
    try {
        return parse(t);
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a selector that is not JSON is a text, as C#'s runner reads it
        if (error instanceof SyntaxError) {
            return t;
        }
        throw error;
    }
}

/** JSON as the cases read it. */
export function parse(t: string): Json {
    return JSON.parse(t) as Json;
}

// A value a check expects, a date written {"date": "yyyy-MM-dd"} read as its day number.
function expected(e: Json | undefined): Json {
    if (e === undefined) {
        throw new Error('Internal: an expectation is missing.');
    }
    if (isObject(e)) {
        if (e['date'] !== undefined) {
            return position(e);
        }
        return Object.fromEntries(Object.entries(e).map(([k, v]) => [k, expected(v)]));
    }
    return isList(e) ? e.map(expected) : e;
}

function otherOf(root: Json, scope: Json, e: Json | undefined, where: string): Json {
    if (typeof e !== 'string') {
        throw new Error(`${where}: sameAs and differsFrom name a path.`);
    }
    return resolve(root, scope, e, where);
}

function items(v: Json, where: string): readonly Json[] {
    if (!isList(v)) {
        throw new Error(`${where}: ${text(v)} is not a list.`);
    }
    return v;
}

function asNumber(v: Json, where: string): number {
    if (typeof v !== 'number') {
        throw new Error(`${where}: ${text(v)} is not a number.`);
    }
    return v;
}

function elementAtNumber(xs: readonly number[], i: number): number {
    const x = xs[i];
    if (x === undefined) {
        throw new Error(`Internal: index ${String(i)} is outside a list of ${String(xs.length)}.`);
    }
    return x;
}

/** Whether two values are the same: numbers within `near`, objects by every key, a missing key null. */
export function same(e: Json, a: Json, near: number): boolean {
    if (e === null || a === null) {
        return e === null && a === null;
    }
    if (isList(e)) {
        return isList(a) && e.length === a.length && e.every((x, i) => same(x, a[i] === undefined ? null : a[i], near));
    }
    if (isObject(e)) {
        if (!isObject(a)) {
            return false;
        }
        const all = new Set([...Object.keys(e), ...Object.keys(a)]);
        return [...all].every(k => same(e[k] === undefined ? null : e[k], a[k] === undefined ? null : a[k], near));
    }
    if (typeof e === 'number') {
        return typeof a === 'number' && Math.abs(e - a) <= near;
    }
    return e === a;
}

// An element matching: an object holds every key given, anything else is the same.
function matches(wanted: Json, item: Json, near: number): boolean {
    if (!isObject(wanted)) {
        return same(wanted, item, near);
    }
    return isObject(item) && Object.entries(wanted).every(([k, v]) => same(v, item[k] === undefined ? null : item[k], near));
}

function has(v: Json, wanted: Json, near: number, where: string): boolean {
    if (typeof v === 'string') {
        return v.includes(string(wanted, 'has'));
    }
    return items(v, where).some(item => matches(wanted, item, near));
}

// ---- The Chart Spec reader (ADR-0320; ADR-0370, section 3) ----

// The reading carries what the tool answers the AI, said at the case's "metrics" or the tool's own 800 by 400.
const specCaseKeys = ['name', 'spec', 'specText', 'tables', 'metrics', 'refused', 'expect'];

/**
 * Reads a spec case's Chart Spec against its tables. A refused case is held to its refusal here, and
 * answers `undefined`; any other answers the reading as `{ title, definition, surface }`, the one it is not
 * `null`, and a surface's definition the refusal reading it throws.
 */
function specAnswer(file: ChartFile, given: JsonObject, name: string): Json | undefined {
    const entry = allow(given, 'the spec case', specCaseKeys);
    const spec = entry['spec'];
    const specText = entry['specText'];
    if ((spec === undefined) === (specText === undefined)) {
        throw new Error(`"${name}" gives its spec as one of spec and specText.`);
    }
    // An object is written again as JSON, its numbers as JavaScript spells them; specText is the spec's text as
    // it is, for text that is not JSON, or a number whose spelling the reader reads.
    const source = spec === undefined ? string(specText, 'specText') : JSON.stringify(spec);
    const tables = specTables(file, entry);
    const lookup = (handle: string): ChartData | undefined => tables.get(handle);
    const refused = entry['refused'];
    if ((refused === undefined) === (entry['expect'] === undefined)) {
        throw new Error(`"${name}" is either refused or expects a reading.`);
    }
    if (refused !== undefined) {
        const r = allow(refused, 'refused', ['path', 'message', 'startsWith']);
        let thrown: unknown;
        try {
            readChartSpec(source, lookup);
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the refusal the case expects is held to it below
            thrown = error;
        }
        if (!(thrown instanceof ChartSpecRefusedError)) {
            throw new Error(`"${name}" expected a refusal, and ${thrown === undefined ? 'nothing was thrown' : `${thrown instanceof Error ? thrown.message : 'what is no Error'} was thrown`}.`, { cause: thrown });
        }
        const path = string(r['path'], 'path');
        holds(thrown.path === path, `"${name}": refused at "${thrown.path}", not "${path}": ${thrown.message}`);
        const message = r['message'];
        const startsWith = r['startsWith'];
        if ((message === undefined) === (startsWith === undefined)) {
            throw new Error(`"${name}"'s refusal says one of message and startsWith.`);
        }
        holds(message === undefined ? thrown.message.startsWith(string(startsWith, 'startsWith')) : thrown.message === string(message, 'message'),
            `"${name}": the refusal says "${thrown.message}".`);
        // The tool answers the AI with the same refusal, and no reading.
        const told = answerChartSpec(source, lookup, specMetrics(entry));
        holds(!told.accepted && told.text === thrown.message, `"${name}": the tool answers "${told.text}".`);
        return undefined;
    }
    const reading = readChartSpec(source, lookup);
    const told = answerChartSpec(source, lookup, specMetrics(entry));
    let definition: Json;
    try {
        definition = plain(reading.definition);
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a surface's definition is answered by the refusal it throws, which the case checks
        if (!(error instanceof Error)) {
            throw error;
        }
        definition = { refused: refusal(error) };
    }
    return { title: reading.title, definition, surface: plain(reading.surface), answer: { accepted: told.accepted, text: told.text } };
}

// The size the tool says the chart at: the case's "metrics", or undefined for the tool's own.
function specMetrics(entry: JsonObject): ChartMetrics | undefined {
    const given = entry['metrics'];
    return given === undefined ? undefined : metrics(given);
}

// The file's tables by handle, a case's own taking the place of the file's of the same handle.
function specTables(file: ChartFile, entry: JsonObject): Map<string, ChartData> {
    const tables = new Map<string, ChartData>();
    for (const named of [file.tables, entry['tables']]) {
        if (named === undefined) {
            continue;
        }
        if (!isObject(named)) {
            throw new Error(`tables is ${text(named)}, not an object of tables by handle.`);
        }
        for (const [handle, table] of Object.entries(named)) {
            if (!isObject(table)) {
                throw new Error(`The table "${handle}" is ${text(table)}, not an object of columns.`);
            }
            tables.set(handle, chartDataOf(Object.entries(table).map(([column, values]) => {
                // An object's own keys put a whole number first, which would reorder the columns.
                if (/^(?:0|[1-9]\d*)$/.test(column)) {
                    throw new Error(`The column "${column}" of "${handle}" is a whole number, whose place in the table the runner cannot keep.`);
                }
                return [column, list(values, `the column "${column}"`).map(specCell)] as const;
            })));
        }
    }
    return tables;
}

// A table's value, as the C# runner reads it: null, a text, a bool, a number, or one of {"decimal": "1.25"},
// {"integer": 3} (a bigint), {"date": "yyyy-MM-dd"} (a Temporal.PlainDate), {"dateTime":
// "yyyy-MM-ddTHH:mm:ss"} (a Temporal.PlainDateTime) and {"double": "NaN" | "Infinity" | "-Infinity"}.
function specCell(given: Json): unknown {
    if (given === null || typeof given === 'string' || typeof given === 'boolean' || typeof given === 'number') {
        return given;
    }
    const forms = Object.entries(allow(given, 'a table\'s value', ['decimal', 'integer', 'date', 'dateTime', 'double']));
    const [form] = forms;
    if (form === undefined || forms.length !== 1) {
        throw new Error(`${text(given)} is not one value a table holds.`);
    }
    const [kind, value] = form;
    switch (kind) {
        case 'decimal':
            return decimalFromText(string(value, 'a decimal'));
        case 'integer':
            return BigInt(whole(value, 'an integer'));
        case 'date':
            return Temporal.PlainDate.from(string(value, 'a date'));
        case 'dateTime':
            return Temporal.PlainDateTime.from(string(value, 'a date and time'));
        default:
            return number(value);
    }
}

// ---- The Chart Source (ADR-0322, ADR-0344) ----

const throughCaseKeys = ['name', 'spec', 'tables', 'sources', 'refused', 'expect'];

// A Chart Source query as a case writes it, every key given.
function sourceQuery(given: Json | undefined): ChartSourceQuery {
    const q = allow(given, 'query', ['data', 'x', 'y', 'values', 'from', 'to', 'pixels']);
    const values = string(q['values'], 'values');
    if (values !== 'dates' && values !== 'numbers' && values !== 'labels') {
        throw new Error(`values is "${values}", not dates, numbers or labels.`);
    }
    const end = (key: string): number | null => {
        const v = q[key];
        if (v === undefined) {
            throw new Error(`The query gives no ${key}; write null for the data's own end.`);
        }
        return v === null ? null : number(v);
    };
    return {
        data: string(q['data'], 'data'), x: string(q['x'], 'x'), y: string(q['y'], 'y'), values: values satisfies AxisValues,
        from: end('from'), to: end('to'), pixels: whole(q['pixels'], 'pixels'),
    };
}

// An answer as a case reads it: its version, each kept row as its position across and its height (null for a gap),
// and what each thinned pixel held.
function answerRows(answer: ChartSourceAnswer, query: ChartSourceQuery): Json {
    const rows: Json[] = [];
    for (let r = 0; r < answer.points.rowCount; r++) {
        const y = answer.points.value(query.y, r);
        rows.push({ x: placed(answer.points.value(query.x, r)), y: y === null ? null : placed(y) });
    }
    return { version: plain(answer.version), rows, thinned: plain(answer.thinned) };
}

// A value kept as a number: a date as its day number, a whole number, a decimal or a double as a double.
function placed(value: unknown): number {
    if (typeof value === 'number') {
        return value;
    }
    if (typeof value === 'bigint') {
        return Number(value);
    }
    const ticks = typeof value === 'object' && value !== null ? ticksOfTemporal(value) : undefined;
    if (ticks !== undefined) {
        return Number(ticks / 864_000_000_000n);
    }
    if (typeof value === 'object' && value !== null && typeof Reflect.get(value, 'mantissa') === 'bigint') {
        return Number(`${String(Reflect.get(value, 'mantissa'))}e-${String(Reflect.get(value, 'scale'))}`);
    }
    throw new Error(`A Chart Source kept ${text(plain(value))}, which is no position or height.`);
}

/**
 * Reads a sources case's Chart Spec through Chart Sources over its tables (ADR-0344), each answering the versions
 * `sources.versions` lists for its handle in turn. A refused case is held to its refusal and to how many questions
 * were asked before it, and answers `undefined`; any other answers `{ title, definition, asked, versions }`.
 */
async function throughAnswer(file: ChartFile, given: JsonObject, name: string): Promise<Json | undefined> {
    const entry = allow(given, 'the sources case', throughCaseKeys);
    const source = JSON.stringify(entry['spec']);
    const through = allow(entry['sources'], 'sources', ['pixels', 'from', 'to', 'versions']);
    const tables = specTables(file, entry);
    const versions = allow(through['versions'], 'versions', [...tables.keys()]);
    const asked: Json[] = [];
    const sources = new Map<string, ChartSource>();
    for (const [handle, listed] of Object.entries(versions)) {
        const left = [...list(listed, `the versions of "${handle}"`)].map(v => BigInt(whole(v, 'a version')));
        const table = tables.get(handle);
        if (table === undefined) {
            throw new Error(`Internal: "${handle}" is not among the tables.`);
        }
        sources.set(handle, {
            thin: (query: ChartSourceQuery) => {
                asked.push(plain(query));
                const version = left.shift();
                if (version === undefined) {
                    throw new Error(`"${name}": "${handle}" was asked more often than its versions say.`);
                }
                return Promise.resolve(thinChartData(table, query, version));
            },
        });
    }
    const options = {
        ...(through['from'] === undefined ? {} : { from: number(through['from']) }),
        ...(through['to'] === undefined ? {} : { to: number(through['to']) }),
    };
    const read = () => readChartSpecThrough(source, handle => sources.get(handle), whole(through['pixels'], 'pixels'), options);
    const refused = entry['refused'];
    if ((refused === undefined) === (entry['expect'] === undefined)) {
        throw new Error(`"${name}" is either refused or expects a reading.`);
    }
    if (refused !== undefined) {
        const r = allow(refused, 'refused', ['path', 'message', 'asked']);
        let thrown: unknown;
        try {
            await read();
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the refusal the case expects is held to it below
            thrown = error;
        }
        if (!(thrown instanceof ChartSpecRefusedError)) {
            throw new Error(`"${name}" expected a refusal, and ${thrown === undefined ? 'nothing was thrown' : `${thrown instanceof Error ? thrown.message : 'what is no Error'} was thrown`}.`, { cause: thrown });
        }
        const path = string(r['path'], 'path');
        holds(thrown.path === path, `"${name}": refused at "${thrown.path}", not "${path}": ${thrown.message}`);
        holds(thrown.message === string(r['message'], 'message'), `"${name}": the refusal says "${thrown.message}".`);
        holds(asked.length === whole(r['asked'], 'asked'), `"${name}": ${String(asked.length)} questions were asked before the refusal.`);
        return undefined;
    }
    const { reading, versions: answered } = await read();
    return { title: reading.title, definition: plain(reading.definition), asked, versions: plain(answered) };
}
