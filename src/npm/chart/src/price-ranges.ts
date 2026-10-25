// A Price Range's candles (ADR-0300, ADR-0302): its rows, and those rows merged into the market's own
// periods where the plot cannot hold one a row.
import { dateOf, dayNumber, dayOfWeek, wholeDay } from './day-numbers';
import type { CandlePeriod, ChartAxis, ChartSeries, PriceBar } from './model';
import { elementAt, unreachable } from './must';
import { greatest, least } from './numbers';
import { toNumber, writePosition, writeValue } from './values';

/** A candle as drawn, in the data's units: one row, or a merged candle standing for several (ADR-0302). */
export interface DrawnCandle {
    /** The first position it stands for. */
    readonly from: number;
    /** The last. */
    readonly to: number;
    /** Where it is centred. */
    readonly x: number;
    /** Its open, or `null` for a High-Low-Close bar. */
    readonly open: number | null;
    /** Its high. */
    readonly high: number;
    /** Its low. */
    readonly low: number;
    /** Its close. */
    readonly close: number;
    /** Its volume, or `null`. */
    readonly volume: number | null;
    /** How many rows it stands for. */
    readonly rows: number;
    /** Its close above its own open, drawn hollow (ADR-0300). */
    readonly rising: boolean;
    /** Its close equal to its own open, drawn as one horizontal line. */
    readonly unchanged: boolean;
}

/** The start of the period a position falls in: the row itself, or the period's first day. */
export function periodStart(x: number, period: CandlePeriod): number {
    if (period === 'row') {
        return x;
    }
    const day = wholeDay(Math.floor(x));
    const d = dateOf(day);
    switch (period) {
        case 'week':
            return day - ((dayOfWeek(day) + 6) % 7);
        case 'month':
            return dayNumber(d.year, d.month, 1);
        case 'quarter':
            return dayNumber(d.year, Math.floor((d.month - 1) / 3) * 3 + 1, 1);
        case 'year':
            return dayNumber(d.year, 1, 1);
        default:
            return unreachable(period);
    }
}

/** A period's name, as the title and the Notes say it. */
export function periodName(period: CandlePeriod): string {
    switch (period) {
        case 'week':
            return 'Weekly';
        case 'month':
            return 'Monthly';
        case 'quarter':
            return 'Quarterly';
        case 'year':
            return 'Yearly';
        case 'row':
            return 'Rows';
        default:
            return unreachable(period);
    }
}

/** The market's periods, finest first. */
export const candlePeriods: readonly CandlePeriod[] = Object.freeze(['row', 'week', 'month', 'quarter', 'year']);

/** Whether a row makes a candle: all four values present, or three where the series has no open at all. */
export function wholeBar(bar: PriceBar, withOpen: boolean): boolean {
    return bar.high !== null && bar.low !== null && bar.close !== null && (!withOpen || bar.open !== null);
}

/** Whether any row has an open: a series with none is drawn as High-Low-Close bars. */
export function hasOpen(bars: readonly PriceBar[]): boolean {
    return bars.some(b => b.open !== null);
}

/** The candles of the rows between `from` and `to`, one a row or merged into `period`. */
export function candlesOf(series: ChartSeries, from: number, to: number, period: CandlePeriod): DrawnCandle[] {
    const open = hasOpen(series.bars);
    const groups = new Map<number, PriceBar[]>();
    for (const b of series.bars) {
        if (b.x >= from && b.x <= to && wholeBar(b, open)) {
            const key = periodStart(b.x, period);
            const group = groups.get(key);
            if (group === undefined) {
                groups.set(key, [b]);
            } else {
                group.push(b);
            }
        }
    }
    const candles: DrawnCandle[] = [];
    for (const bars of groups.values()) {
        const first = elementAt(bars, 0);
        const last = elementAt(bars, bars.length - 1);
        const volumes = bars.flatMap(b => (b.volume === undefined || b.volume === null ? [] : [toNumber(b.volume)]));
        const openValue = open && first.open !== null ? toNumber(first.open) : null;
        const close = toNumber(must(last.close));
        candles.push(Object.freeze({
            from: first.x,
            to: last.x,
            x: period === 'row' ? first.x : (first.x + last.x) / 2,
            open: openValue,
            high: greatest(bars.map(b => toNumber(must(b.high)))),
            low: least(bars.map(b => toNumber(must(b.low)))),
            close,
            volume: volumes.length > 0 ? volumes.reduce((a, b) => a + b, 0) : null,
            rows: bars.length,
            rising: openValue !== null && close > openValue,
            unchanged: openValue !== null && close === openValue,
        }));
    }
    return candles;
}

function must<T>(value: T | null): T {
    if (value === null) {
        throw new Error('Internal: a whole row is missing a value.');
    }
    return value;
}

/** The refusal of a row whose values disagree (ADR-0300), named by its row and its values, or `undefined`. */
export function disagreement(series: ChartSeries, axis: ChartAxis, bar: PriceBar): string | undefined {
    if (bar.high === null || bar.low === null || bar.close === null) {
        return undefined;
    }
    const h = toNumber(bar.high);
    const l = toNumber(bar.low);
    const c = toNumber(bar.close);
    const row = `'${series.name}', the row of ${writePosition(axis, bar.x)}`;
    const w = (v: number | bigint): string => writeValue(series, v);
    if (bar.open !== null) {
        const o = toNumber(bar.open);
        if (h < o) {
            return `${row}: high ${w(bar.high)} is below open ${w(bar.open)}.`;
        }
        if (l > o) {
            return `${row}: low ${w(bar.low)} is above open ${w(bar.open)}.`;
        }
    }
    if (h < c) {
        return `${row}: high ${w(bar.high)} is below close ${w(bar.close)}.`;
    }
    if (l > c) {
        return `${row}: low ${w(bar.low)} is above close ${w(bar.close)}.`;
    }
    if (h < l) {
        return `${row}: high ${w(bar.high)} is below low ${w(bar.low)}.`;
    }
    return undefined;
}
