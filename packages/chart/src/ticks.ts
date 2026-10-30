// Ticks and labels for both axes (ticket 04). Text is never measured: widths come from the estimate the
// caller hands in (ADR-0021).
import { addMonths, dateOf, dayNumber, isoDate, monthYear, wholeDay, yearMonth, yearOnly, type CivilDate } from './day-numbers';
import { upToPlaces } from './invariant-text';
import type { ChartAxis } from './model';
import { elementAt, unreachable } from './must';
import { roundHalfToEven, roundToDigits } from './numbers';
import { axisWrites, writePosition } from './values';

/** Where a horizontal axis puts its ticks. */
export type TickPlacement =
    /** The default for the axis: calendar boundaries for dates, round numbers for numbers, every category. */
    | 'auto'
    /** At calendar boundaries — years, quarters, months or days, the finest that fits. */
    | 'calendar'
    /** At every position some series has a point at. */
    | 'everyPoint'
    /** At the market's tenors counted from the first date: 1M, 3M, 6M, 1Y, 2Y, 3Y, 5Y, 7Y, 10Y … */
    | 'tenors'
    /** At the positions the axis names. */
    | 'named';

/** How a date tick is labelled: the finest format the ticks need, 2026, 2026-10, 2026-10-19, Oct 2026, or a tenor. */
export type DateLabel = 'auto' | 'year' | 'yearMonth' | 'date' | 'monthYear' | 'tenor';

/** How the horizontal axis is ticked and labelled: three independent choices. */
export interface AxisStyle {
    /** Where the ticks go; `auto` by default. */
    readonly placement?: TickPlacement;
    /** How a date tick is labelled; `auto` by default. */
    readonly label?: DateLabel;
    /** The labels' angle: 0 (the default), 45 or 90 degrees, read upwards. */
    readonly angle?: number;
}

/** A tick: where it stands on the axis, and its label, or `null` where it was left out so as not to touch its neighbour. */
export interface AxisTick {
    /** The position, in the axis' units. */
    readonly position: number;
    /** The label, or `null`. */
    readonly label: string | null;
}

/** The ticks of an axis, laid out: every tick is drawn; a label that would touch its neighbour is left out. */
export interface AxisTicks {
    /** The ticks, in order. */
    readonly ticks: readonly AxisTick[];
    /** How far the labels reach from the axis, in pixels. */
    readonly depth: number;
    /** How many ticks kept their label. */
    readonly labelled: number;
}

/** Round-number ticks: the range they cover and their step. */
export interface NiceTicks {
    /** The first tick. */
    readonly min: number;
    /** The last. */
    readonly max: number;
    /** The step between them. */
    readonly step: number;
}

const tenorMonths = [1, 3, 6, 12, 24, 36, 60, 84, 120, 180, 240, 300, 360, 480, 600] as const;

/** A tenor in years written as the market writes it: 6M, 18M, 1Y, 2.5Y. */
export function tenor(years: number): string {
    const months = years * 12;
    if (Math.abs(months - roundHalfToEven(months)) < 1e-6) {
        const m = roundHalfToEven(months);
        return m % 12 === 0 ? `${String(Math.trunc(m / 12))}Y` : `${String(m)}M`;
    }
    const weeks = years * 52;
    if (years < 1 / 12 && Math.abs(weeks - roundHalfToEven(weeks)) < 1e-6) {
        return `${String(roundHalfToEven(weeks))}W`;
    }
    return `${upToPlaces(years, 2)}Y`;
}

/** The whole decades that hold `min` to `max`, both above zero: the bounds of a logarithmic axis (ADR-0290). */
export function decades(min: number, max: number): readonly [min: number, max: number] {
    if (!(min > 0) || !(max > 0)) {
        return [1, 10];
    }
    const lo = 10 ** Math.floor(Math.log10(min) + 1e-9);
    const hi = 10 ** Math.ceil(Math.log10(max) - 1e-9);
    return hi > lo ? [lo, hi] : [lo, lo * 10];
}

/**
 * A logarithmic axis' ticks between `min` and `max`, both above zero (ADR-0290): every 1, 2 and 5 times a
 * power of ten across up to three decades, every power of ten across more, every whole multiple within one.
 */
export function logSteps(min: number, max: number): readonly number[] {
    if (!(min > 0) || !(max > min)) {
        return min > 0 ? [min] : [];
    }
    const span = Math.log10(max / min);
    const multiples = span < 1 ? [1, 2, 3, 4, 5, 6, 7, 8, 9] : span <= 3 ? [1, 2, 5] : [1];
    const ticks: number[] = [];
    for (let e = Math.floor(Math.log10(min)) - 1; e <= Math.ceil(Math.log10(max)); e++) {
        for (const m of multiples) {
            const t = roundToDigits(m * 10 ** e, 12 - Math.min(12, Math.max(0, e)));
            if (t >= min * (1 - 1e-9) && t <= max * (1 + 1e-9)) {
                ticks.push(t);
            }
        }
    }
    return ticks;
}

/** Round-number ticks covering `min` to `max`, about `target` of them, at a step of 1, 2, 2.5 or 5 times a power of ten. */
export function nice(min: number, max: number, target = 5): NiceTicks {
    let lo = min;
    let hi = max;
    if (lo === hi) {
        const pad = lo === 0 ? 1 : Math.abs(lo) / 10;
        lo -= pad;
        hi += pad;
    }
    const raw = (hi - lo) / Math.max(target, 1);
    const mag = 10 ** Math.floor(Math.log10(raw));
    const step = [1, 2, 2.5, 5, 10].map(f => f * mag).find(s => s >= raw * (1 - 1e-9));
    if (step === undefined) {
        throw new Error(`Internal: no round step covers ${String(raw)}.`);
    }
    return { min: Math.floor(lo / step + 1e-9) * step, max: Math.ceil(hi / step - 1e-9) * step, step };
}

/** The positions of round-number ticks from `min` to `max` at `step`. */
export function steps(min: number, max: number, step: number): readonly number[] {
    const n = Math.trunc(roundHalfToEven((max - min) / step));
    if (n + 1 < 0) {
        throw new RangeError(`${String(n + 1)} ticks is not a count.`);
    }
    return Array.from({ length: n + 1 }, (_, i) => roundToDigits(min + i * step, 12));
}

/**
 * The horizontal axis' ticks over `from` to `to`, placed on the plot by `map`, in pixels, with labels left
 * out where they would touch. `points` are the positions series have points at; `textWidth` estimates a
 * label's width and `lineHeight` is a label's height, in pixels.
 */
export function horizontalTicks(axis: ChartAxis, style: AxisStyle, from: number, to: number, points: Iterable<number>,
    map: (position: number) => number, textWidth: (text: string) => number, lineHeight: number, gap = 4): AxisTicks {
    const { placement: declared = 'auto', angle = 0 } = style;
    const placement: TickPlacement = declared === 'auto'
        ? axis.values === 'dates' ? 'calendar' : axis.values === 'labels' ? 'everyPoint' : axis.named.length > 0 ? 'named' : 'calendar'
        : declared;
    const label = style.label === undefined ? 'auto' : style.label;
    const ticks: (readonly [number, string])[] = [];
    if (axis.effectiveScale === 'logarithmic' && from > 0) {
        for (const x of logSteps(from, to)) {
            ticks.push([x, axisWrites(axis) ? writePosition(axis, x) : upToPlaces(x, 4)]);
        }
    } else if (axis.values === 'labels') {
        axis.labels.forEach((text, i) => ticks.push([i, text]));
    } else if (placement === 'named') {
        for (const [position, text] of axis.named) {
            if (position >= from && position <= to) {
                ticks.push([position, text]);
            }
        }
    } else if (placement === 'everyPoint') {
        const xs = [...new Set([...points].filter(x => x >= from && x <= to))].toSorted((a, b) => a - b);
        for (const x of xs) {
            ticks.push([x, labelOf(axis, label, x, from, 'day')]);
        }
    } else if (placement === 'tenors') {
        for (const m of tenorMonths) {
            const x = axis.values === 'dates' ? dayOfDate(addMonths(dateOf(wholeDay(from)), m)) : from + m / 12;
            if (x <= to) {
                ticks.push([x, axis.values === 'dates' && label !== 'tenor' && label !== 'auto' ? labelOf(axis, label, x, from, 'day') : tenor(m / 12)]);
            }
        }
    } else if (axis.values === 'dates') {
        const unit = calendarUnit(from, to, map, textWidth, gap, axis.formatCode !== undefined && label === 'auto' ? axis : undefined);
        for (const x of calendarTicks(from, to, unit)) {
            ticks.push([x, labelOf(axis, label, x, from, unit.kind)]);
        }
    } else {
        const n = nice(from, to, 6);
        for (const x of steps(n.min, n.max, n.step)) {
            if (x >= from - 1e-9 && x <= to + 1e-9) {
                ticks.push([x, axis.formatCode === undefined ? upToPlaces(x, 4) : writePosition(axis, x)]);
            }
        }
    }
    return fitLabels(ticks, angle, map, textWidth, lineHeight, gap);
}

/** Keeps each label whose footprint along the axis clears the last kept one's; every tick stays. */
export function fitLabels(ticks: readonly (readonly [position: number, label: string])[], angle: number, map: (position: number) => number,
    textWidth: (text: string) => number, lineHeight: number, gap: number): AxisTicks {
    const rad = angle * Math.PI / 180;
    const result: AxisTick[] = [];
    let lastEnd = -Infinity;
    let depth = 0;
    let labelled = 0;
    for (const [x, text] of ticks) {
        const w = textWidth(text);
        // Along the axis a label at an angle takes its height's share, not its width's; read upwards, it
        // is anchored at its tick, so its footprint starts there.
        const along = w * Math.cos(rad) + lineHeight * Math.sin(rad);
        const px = map(x);
        const start = angle === 0 ? px - along / 2 : px - lineHeight * Math.sin(rad) / 2;
        if (start >= lastEnd + gap) {
            result.push(Object.freeze({ position: x, label: text }));
            labelled++;
            lastEnd = start + along;
            depth = Math.max(depth, w * Math.sin(rad) + lineHeight * Math.cos(rad));
        } else {
            result.push(Object.freeze({ position: x, label: null }));
        }
    }
    return Object.freeze({ ticks: Object.freeze(result), depth, labelled });
}

export type Unit = 'day' | 'month' | 'quarter' | 'year';

interface CalendarStep {
    readonly kind: Unit;
    readonly every: number;
}

const calendarSteps: readonly CalendarStep[] = [
    { kind: 'day', every: 1 }, { kind: 'day', every: 7 }, { kind: 'month', every: 1 }, { kind: 'quarter', every: 1 }, { kind: 'month', every: 6 },
    { kind: 'year', every: 1 }, { kind: 'year', every: 2 }, { kind: 'year', every: 5 }, { kind: 'year', every: 10 }, { kind: 'year', every: 25 },
    { kind: 'year', every: 50 },
];

const unitOrder: readonly Unit[] = ['day', 'month', 'quarter', 'year'];

// A day whose names are the longest a code writes: a Wednesday in September.
const widestDay = dayNumber(2026, 9, 30);

/**
 * The finest calendar step a date code tells apart, found by writing days a day, a month and a year apart with
 * it; `undefined` for a code that writes none of them apart, such as one of the time alone (ADR-0420).
 */
export function codeResolution(write: (position: number) => string): Unit | undefined {
    const text = write(dayNumber(2001, 2, 3));
    if (write(dayNumber(2001, 2, 4)) !== text) {
        return 'day';
    }
    if (write(dayNumber(2001, 3, 3)) !== text) {
        return 'month';
    }
    return write(dayNumber(2002, 2, 3)) === text ? undefined : 'year';
}

// The finest step whose labels, at their usual width, fit side by side across the plot. Where the axis' code
// writes the ticks, no step finer than the code tells apart, so no two ticks read alike, and its labels are as
// wide as the code writes a long date (ADR-0420).
function calendarUnit(from: number, to: number, map: (position: number) => number, textWidth: (text: string) => number, gap: number,
    coded: ChartAxis | undefined): CalendarStep {
    const plot = Math.abs(map(to) - map(from));
    const finest = coded === undefined ? 'day' : codeResolution(x => writePosition(coded, x));
    if (finest === undefined) {
        throw new Error('Internal: a code that tells no dates apart was not refused.');
    }
    const codedWidth = coded === undefined ? 0 : textWidth(writePosition(coded, widestDay));
    for (const s of calendarSteps) {
        if (unitOrder.indexOf(s.kind) < unitOrder.indexOf(finest)) {
            continue;
        }
        const count = Math.max(1, calendarTicks(from, to, s).length);
        const label = coded === undefined ? textWidth(s.kind === 'year' ? '2026' : s.kind === 'day' ? '2026-10-19' : '2026-10') : codedWidth;
        if (plot / count >= label + gap) {
            return s;
        }
    }
    return elementAt(calendarSteps, calendarSteps.length - 1);
}

const dayOfDate = (d: CivilDate): number => dayNumber(d.year, d.month, d.day);

function calendarTicks(from: number, to: number, step: CalendarStep): number[] {
    const start = wholeDay(Math.ceil(from));
    const end = wholeDay(Math.floor(to));
    const first = dateOf(start);
    let d: CivilDate = step.kind === 'day' ? first : { year: first.year, month: 1, day: 1 };
    const result: number[] = [];
    for (let day = dayOfDate(d); day <= end; day = dayOfDate(d)) {
        let on: boolean;
        switch (step.kind) {
            case 'day':
                on = true;
                break;
            case 'month':
                on = d.day === 1 && (d.month - 1) % step.every === 0;
                break;
            case 'quarter':
                on = d.day === 1 && (d.month - 1) % 3 === 0;
                break;
            case 'year':
                on = d.day === 1 && d.month === 1 && d.year % step.every === 0;
                break;
            default:
                return unreachable(step.kind);
        }
        if (on && day >= start) {
            result.push(day);
        }
        d = step.kind === 'day' ? dateOf(day + step.every) : step.kind === 'year' ? addMonths(d, 12) : addMonths(d, 1);
    }
    return result;
}

function labelOf(axis: ChartAxis, style: DateLabel, x: number, first: number, unit: Unit): string {
    if (axis.values === 'numbers') {
        const named = axis.named.find(n => n[0] === x);
        if (named !== undefined) {
            return named[1];
        }
        return axis.formatCode === undefined ? upToPlaces(x, 4) : writePosition(axis, x);
    }
    // A code writes a date tick, unless the axis' style names how date ticks are labelled (ADR-0420); a Format,
    // written for the values a column holds, leaves the ticks to the axis' own labels.
    if (axis.formatCode !== undefined && style === 'auto') {
        return writePosition(axis, x);
    }
    const d = dateOf(wholeDay(x));
    const label = style === 'auto' ? unit === 'year' ? 'year' : unit === 'day' ? 'date' : 'yearMonth' : style;
    switch (label) {
        case 'year':
            return yearOnly(d);
        case 'yearMonth':
            return yearMonth(d);
        case 'monthYear':
            return monthYear(d);
        case 'tenor': {
            const a = dateOf(wholeDay(first));
            return tenor(((d.year - a.year) * 12 + d.month - a.month) / 12);
        }
        case 'date':
            return isoDate(d);
        default:
            return unreachable(label);
    }
}
