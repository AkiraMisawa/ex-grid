// What a chart is of, in the data's own terms (ADR-0251). A definition that cannot be drawn honestly is
// refused when it is made, with the sentence that says why (spine rule 1).
import { edgeRefusal } from './edges';
import { roundTrip } from './invariant-text';
import type {
    AppearanceKeys, ChartAxis, ChartAxisOptions, ChartPoint, ChartSeries, ChartSeriesOptions, ChartValue, SeriesInterval,
} from './model';
import { elementAt } from './must';
import { pointScaleRefusal } from './point-scales';
import { disagreement, hasOpen, wholeBar } from './price-ranges';
import { ChartRefusedError, toNumber, writePosition, writeValue } from './values';

const kinds = new Set(['level', 'rateOverInterval', 'amountPerPeriod', 'observation', 'priceRange']);

/** The horizontal axis with its defaults applied. */
export function chartAxis(options: ChartAxisOptions): ChartAxis {
    const { values, title = '', labels = [], named = [], daysPerYear = 365.25, packed = false } = options;
    const effectiveScale = options.scale === undefined ? (values === 'labels' ? 'categories' : 'proportional') : options.scale;
    return Object.freeze({
        values, title,
        ...(options.scale === undefined ? {} : { scale: options.scale }),
        labels: Object.freeze([...labels]),
        named: Object.freeze(named.map(n => Object.freeze([n[0], n[1]] as const))),
        daysPerYear, packed,
        ...(options.format === undefined ? {} : { format: options.format }),
        effectiveScale,
    });
}

// A value as the C# engine's `ChartValue.Of` takes it: finite, or a whole number held exactly. What it is
// is named only when it is refused, so a long series is not written out point by point to check it.
function checkedValue(value: unknown, what: () => string): ChartValue | null {
    if (value === null || typeof value === 'bigint') {
        return value;
    }
    if (typeof value !== 'number') {
        throw new TypeError(`${what()} is ${typeof value}; a chart value is a number or a bigint, and a missing value is null.`);
    }
    if (!Number.isFinite(value)) {
        throw new RangeError(`${what()} is ${String(value)}. A chart value is finite; a missing value is a gap, handed in as null.`);
    }
    return value;
}

/** One series with its defaults applied, its values checked. */
export function chartSeries(options: ChartSeriesOptions): ChartSeries {
    const {
        step = 'preceding', axis = 'left', panel = '', interpolated = false, segments = [], details = [], edges = [], thinned = [],
    } = options;
    const where = `'${options.name}'`;
    const bars = options.bars === undefined ? [] : options.bars.map(b => Object.freeze({
        x: b.x,
        open: checkedValue(b.open, () => `${where}'s open`),
        high: checkedValue(b.high, () => `${where}'s high`),
        low: checkedValue(b.low, () => `${where}'s low`),
        close: checkedValue(b.close, () => `${where}'s close`),
        volume: b.volume === undefined ? null : checkedValue(b.volume, () => `${where}'s volume`),
    }));
    const open = hasOpen(bars);
    const given = options.points === undefined ? [] : options.points;
    // A Price Range's points are its bars' closes, as C#'s Bars sets them. A series handed back as options, as
    // C#'s `with` copies one, carries `bars: []` beside its own points, which stand.
    const points: readonly ChartPoint[] = options.bars === undefined || options.bars.length === 0
        ? given.map(p => Object.freeze({ ...p, y: checkedValue(p.y, () => `${where}'s value at ${roundTrip(p.x)}`) }))
        : bars.map(b => Object.freeze({ x: b.x, y: wholeBar(b, open) ? b.close : null }));
    const keys = options.keys;
    const effectiveKeys: AppearanceKeys = Object.freeze(keys === undefined ? { colour: options.id } : { ...keys });
    const kind = options.kind;
    const reachesZero = kind === 'amountPerPeriod'
        || (kind === 'observation' || kind === 'priceRange' ? options.fromZero === true : options.fromZero !== false);
    const scaled = (values: readonly (ChartValue | null)[], title: string): readonly (ChartValue | null)[] =>
        Object.freeze(values.map(v => checkedValue(v, () => `${where}'s ${title}`)));
    // The Consumer's function answers at the boundary as a point does: a value it cannot be is refused where
    // it is asked, never drawn as NaN (CONVENTIONS.md, "Check at the boundary").
    const sampler = options.sampler;
    const checkedSampler = sampler === undefined ? undefined : Object.freeze({
        from: sampler.from,
        to: sampler.to,
        valueAt: (x: number) => checkedValue(sampler.valueAt(x), () => `${where}'s value at ${roundTrip(x)}`),
    });
    return Object.freeze({
        id: options.id,
        name: options.name,
        kind,
        step,
        axis,
        panel,
        ...(keys === undefined ? {} : { keys: effectiveKeys }),
        ...(options.fromZero === undefined ? {} : { fromZero: options.fromZero }),
        interpolated,
        // What the definition checked is copied and frozen, so a Consumer's later change to what it handed
        // in cannot reach past the checks (CONVENTIONS.md, "Immutable base plus a thin diff").
        segments: Object.freeze(segments.map(g => Object.freeze({ ...g }))),
        points: Object.freeze(points),
        ...(options.colourBy === undefined ? {} : { colourBy: Object.freeze({ ...options.colourBy, values: scaled(options.colourBy.values, options.colourBy.title) }) }),
        ...(options.sizeBy === undefined ? {} : { sizeBy: Object.freeze({ ...options.sizeBy, values: scaled(options.sizeBy.values, options.sizeBy.title) }) }),
        details: Object.freeze(details.map(d => Object.freeze({ ...d, values: Object.freeze([...d.values]) }))),
        edges: Object.freeze(edges.map(e => Object.freeze({ ...e }))),
        bars: Object.freeze(bars),
        ...(options.mergeTo === undefined ? {} : { mergeTo: options.mergeTo }),
        ...(options.interval === undefined ? {} : { interval: checkedInterval(options.interval, where) }),
        ...(options.xInterval === undefined ? {} : { xInterval: checkedInterval(options.xInterval, where) }),
        thinned: Object.freeze(thinned.map(t => Object.freeze({ ...t }))),
        ...(checkedSampler === undefined ? {} : { sampler: checkedSampler }),
        ...(options.format === undefined ? {} : { format: options.format }),
        effectiveKeys,
        reachesZero,
    });
}

function checkedInterval(interval: SeriesInterval, where: string): SeriesInterval {
    return Object.freeze({
        ...interval,
        bounds: Object.freeze(interval.bounds.map(b => Object.freeze({
            x: b.x,
            lower: checkedValue(b.lower, () => `${where}'s lower bound`),
            upper: checkedValue(b.upper, () => `${where}'s upper bound`),
        }))),
    });
}

/** What a chart is of: its horizontal axis, its series, and the Consumer's Notes (ADR-0251, ADR-0257). */
export interface ChartDefinitionOptions {
    /** The horizontal axis. */
    readonly axis: ChartAxisOptions;
    /** The series, in the order the legend lists them. */
    readonly series: readonly ChartSeriesOptions[];
    /** The Consumer's Notes: plain lines shown under the chart (ADR-0257). */
    readonly notes?: readonly string[];
}

/**
 * What a chart is of, in the data's own terms (ADR-0251): its horizontal axis, its series and the
 * Consumer's Notes. A definition that cannot be drawn honestly is refused when it is made, with a
 * `ChartRefusedError` whose sentence says why.
 */
export class ChartDefinition {
    readonly #axis: ChartAxis;
    readonly #series: readonly ChartSeries[];
    readonly #notes: readonly string[];

    /** A chart of the given series on the given axis; refused when it cannot be drawn as asked. */
    constructor(options: ChartDefinitionOptions) {
        const { notes = [] } = options;
        this.#axis = chartAxis(options.axis);
        this.#series = Object.freeze(options.series.map(chartSeries));
        this.#notes = Object.freeze([...notes]);
        validate(this.#axis, this.#series);
        validatePacked(this.#axis, this.#series);
    }

    /** The horizontal axis. */
    get axis(): ChartAxis {
        return this.#axis;
    }

    /** The series, in the order the legend lists them. */
    get series(): readonly ChartSeries[] {
        return this.#series;
    }

    /** The Consumer's Notes. */
    get notes(): readonly string[] {
        return this.#notes;
    }
}

/** The packed axis's slots: each row of a Price Range with a value (ADR-0301). */
export function packedSessions(series: readonly ChartSeries[]): number[] {
    const sessions = new Set<number>();
    for (const s of series) {
        if (s.kind === 'priceRange') {
            const open = hasOpen(s.bars);
            for (const b of s.bars) {
                if (wholeBar(b, open)) {
                    sessions.add(b.x);
                }
            }
        }
    }
    return [...sessions];
}

function validatePacked(axis: ChartAxis, series: readonly ChartSeries[]): void {
    if (!axis.packed) {
        return;
    }
    if (axis.values !== 'dates') {
        throw new ChartRefusedError('Only a Dates axis packs its sessions.');
    }
    const candles = new Set(packedSessions(series));
    if (candles.size === 0) {
        throw new ChartRefusedError('A packed axis takes its sessions from a Price Range, and the chart has none.');
    }
    for (const s of series.filter(o => o.kind !== 'priceRange')) {
        if (s.sampler !== undefined) {
            throw new ChartRefusedError(`'${s.name}' is a function, which has no values between the packed sessions.`);
        }
        const off = s.points.find(p => !candles.has(p.x));
        if (off !== undefined) {
            throw new ChartRefusedError(`'${s.name}' has a point at ${writePosition(axis, off.x)}, where no candle stands: a packed axis has nowhere true to put it.`);
        }
    }
}

function edgeWithoutInterval(s: ChartSeries): ChartPoint | undefined {
    if (s.points.length === 0) {
        return undefined;
    }
    const edge = s.step === 'preceding' ? elementAt(s.points, 0) : elementAt(s.points, s.points.length - 1);
    return edge.y === null ? undefined : edge;
}

function validate(axis: ChartAxis, series: readonly ChartSeries[]): void {
    const ids = new Set<string>();
    const logarithmic = axis.effectiveScale === 'logarithmic';
    if (logarithmic && axis.values !== 'numbers') {
        throw new ChartRefusedError('Only a number axis can be logarithmic.');
    }
    for (const s of series) {
        if (ids.has(s.id)) {
            throw new ChartRefusedError(`Two series are named '${s.id}'; each series needs an identity of its own.`);
        }
        ids.add(s.id);
        if (!kinds.has(s.kind)) {
            throw new ChartRefusedError(`'${s.name}' declares no Series Kind: say whether it is a Level, a Rate over an interval or an Amount per period.`);
        }
        if (s.kind === 'amountPerPeriod' && s.fromZero === false) {
            throw new ChartRefusedError(`'${s.name}' is an Amount per period, which is always drawn from zero.`);
        }
        if (s.kind === 'amountPerPeriod' && axis.values === 'labels') {
            throw new ChartRefusedError(`'${s.name}' is an Amount per period, but categories have no length to draw it per year over.`);
        }
        if (s.sampler !== undefined && s.points.length > 0) {
            throw new ChartRefusedError(`'${s.name}' is handed in both as points and as a function; give one.`);
        }
        if (s.sampler !== undefined && axis.effectiveScale === 'categories') {
            throw new ChartRefusedError(`'${s.name}' is a function, which has no values between categories to sample.`);
        }
        if (s.sampler !== undefined && s.kind !== 'level') {
            throw new ChartRefusedError(`'${s.name}' is a function, which is drawn as a Level; a step is handed in as its points.`);
        }
        const edge = s.kind === 'amountPerPeriod' ? edgeWithoutInterval(s) : undefined;
        if (edge !== undefined) {
            throw new ChartRefusedError(`'${s.name}' has an amount at ${roundTrip(edge.x)} with no interval to draw it per year over: an Amount per period's ${s.step === 'preceding' ? 'first' : 'last'} point only bounds the interval next to it, and holds no amount.`);
        }
        if (s.sampler !== undefined && !(s.sampler.from < s.sampler.to)) {
            throw new ChartRefusedError(`'${s.name}' is sampled over an empty range.`);
        }
        if (s.kind === 'observation' && axis.effectiveScale === 'categories') {
            throw new ChartRefusedError(`'${s.name}' is an Observation, which needs a value axis across: numbers or dates, not categories.`);
        }
        if (s.kind === 'observation' && s.sampler !== undefined) {
            throw new ChartRefusedError(`'${s.name}' is an Observation, which is handed in as its points, not as a function.`);
        }
        const unplaced = logarithmic ? s.points.find(p => !(p.x > 0)) : undefined;
        if (unplaced !== undefined) {
            throw new ChartRefusedError(`'${s.name}' has a point at ${roundTrip(unplaced.x)}, which a logarithmic axis cannot place: every position must be above zero.`);
        }
        if (logarithmic && s.sampler !== undefined && !(s.sampler.from > 0)) {
            throw new ChartRefusedError(`'${s.name}' is sampled from ${roundTrip(s.sampler.from)}, which a logarithmic axis cannot place: every position must be above zero.`);
        }
        // An Observation's points are neither ordered nor unique in x (ADR-0290).
        for (let i = 1; i < s.points.length && s.kind !== 'observation'; i++) {
            if (!(elementAt(s.points, i - 1).x < elementAt(s.points, i).x)) {
                throw new ChartRefusedError(`'${s.name}' has its points out of order at position ${roundTrip(elementAt(s.points, i).x)}; a line joins only neighbours, so the order must be the data's.`);
            }
        }
        if (s.kind === 'priceRange') {
            if (axis.values !== 'dates') {
                throw new ChartRefusedError(`'${s.name}' is a Price Range, which stands on a Dates axis: its candles merge into weeks and months.`);
            }
            if (s.sampler !== undefined || s.points.length !== s.bars.length) {
                throw new ChartRefusedError(`'${s.name}' is a Price Range, handed in as its rows (open, high, low, close), not as points.`);
            }
            for (const b of s.bars) {
                const refusal = disagreement(s, axis, b);
                if (refusal !== undefined) {
                    throw new ChartRefusedError(refusal);
                }
            }
        } else if (s.bars.length > 0) {
            throw new ChartRefusedError(`'${s.name}' has rows of open, high, low and close, but is not a Price Range.`);
        }
        const scaleRefusal = pointScaleRefusal(s);
        if (scaleRefusal !== undefined) {
            throw new ChartRefusedError(scaleRefusal);
        }
        for (const detail of s.details) {
            if (s.kind !== 'observation') {
                throw new ChartRefusedError(`'${s.name}' says ${detail.title} in its tooltip, which belongs to marks drawn one a row: an observation.`);
            }
            if (detail.values.length !== s.points.length) {
                throw new ChartRefusedError(`'${s.name}' has ${String(s.points.length)} points and ${String(detail.values.length)} values of ${detail.title}: its tooltip says one a point.`);
            }
        }
        const edgeProblem = edgeRefusal(s);
        if (edgeProblem !== undefined) {
            throw new ChartRefusedError(edgeProblem);
        }
        if (s.mergeTo !== undefined && s.kind !== 'priceRange') {
            throw new ChartRefusedError(`'${s.name}' fixes a candle period, but is not a Price Range.`);
        }
        if (s.interval !== undefined) {
            if ((s.kind !== 'level' && s.kind !== 'observation') || s.sampler !== undefined) {
                throw new ChartRefusedError(`'${s.name}' has an Interval, which belongs to a Level's or an Observation's points.`);
            }
            validateInterval(axis, s, s.interval, '');
        }
        if (s.xInterval !== undefined) {
            if (s.kind !== 'observation') {
                throw new ChartRefusedError(`'${s.name}' has a horizontal Interval, which belongs to an Observation's points.`);
            }
            validateInterval(axis, s, s.xInterval, 'horizontal ');
        }
        for (const g of s.segments) {
            if (!(g.from < g.to)) {
                throw new ChartRefusedError(`'${s.name}' has an empty segment, '${g.method}'.`);
            }
        }
    }
}

// A Level's bounds are found by position; an Observation's are one a row, in the points' order
// (ADR-0303, ADR-0304).
function validateInterval(axis: ChartAxis, s: ChartSeries, iv: SeriesInterval, which: string): void {
    if (iv.meaning.trim() === '') {
        throw new ChartRefusedError(`'${s.name}' has a ${which}Interval that does not say what it means: name it ("95% CI", "±1σ", "Bid–Ask").`);
    }
    const write = (v: ChartValue): string => (which.length > 0 ? writePosition(axis, toNumber(v)) : writeValue(s, v));
    if (s.kind === 'observation') {
        if (iv.style === 'band') {
            throw new ChartRefusedError(`'${s.name}' draws its ${which}Interval as a band, which joins neighbours: an Observation's points have none, so it takes whiskers.`);
        }
        if (iv.bounds.length !== s.points.length) {
            throw new ChartRefusedError(`'${s.name}' has ${String(s.points.length)} points and ${String(iv.bounds.length)} ${which}bounds: an Observation's bounds are one a row, in its points' order.`);
        }
        for (let i = 0; i < s.points.length; i++) {
            const b = elementAt(iv.bounds, i);
            const p = elementAt(s.points, i);
            const at = p.name === undefined ? `row ${String(i + 1)}` : p.name;
            if (b.x !== p.x && !(Number.isNaN(b.x) && Number.isNaN(p.x))) {
                throw new ChartRefusedError(`'${s.name}' at ${at}: the ${which}bound stands at ${writePosition(axis, b.x)}, but its point at ${writePosition(axis, p.x)}.`);
            }
            if (b.lower !== null && b.upper !== null && toNumber(b.lower) > toNumber(b.upper)) {
                throw new ChartRefusedError(`'${s.name}' at ${at}: the lower bound ${write(b.lower)} of its ${iv.meaning} is above the upper ${write(b.upper)}.`);
            }
        }
        return;
    }
    const xs = new Set(s.points.map(p => p.x));
    const seen = new Set<number>();
    for (const b of iv.bounds) {
        if (!xs.has(b.x)) {
            throw new ChartRefusedError(`'${s.name}' has an interval at ${writePosition(axis, b.x)}, where it has no point.`);
        }
        if (seen.has(b.x)) {
            throw new ChartRefusedError(`'${s.name}' has two intervals at ${writePosition(axis, b.x)}.`);
        }
        seen.add(b.x);
        if (b.lower !== null && b.upper !== null && toNumber(b.lower) > toNumber(b.upper)) {
            throw new ChartRefusedError(`'${s.name}' at ${writePosition(axis, b.x)}: the lower bound ${writeValue(s, b.lower)} of its ${iv.meaning} is above the upper ${writeValue(s, b.upper)}.`);
        }
    }
}

