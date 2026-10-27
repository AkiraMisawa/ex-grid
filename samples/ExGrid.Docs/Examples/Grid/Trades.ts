import { Temporal } from 'temporal-polyfill';
import { mulberry32 } from './Seeded';

/** A trade, as the ExGrid Examples show it. */
export interface Trade {
    readonly id: number;
    readonly book: string;
    readonly trader: string;
    readonly instrument: string;
    readonly currency: string;
    readonly notional: number;
    readonly price: number;
    readonly tradeDate: Temporal.PlainDate;
    readonly confirmed: boolean;
}

const books = ['Rates', 'Credit', 'FX', 'Equities'];
const traders = ['Ishikawa', 'Novak', 'Osei', 'Laurent', 'Haddad', 'Kowalski'];
const instruments = ['UST 10Y', 'Bund 5Y', 'EUR/USD', 'USD/JPY', 'iTraxx Main', 'CDX IG', 'SPX Dec', 'NKY Mar'];
const currencies = ['USD', 'EUR', 'JPY', 'GBP'];

/** `count` invented trades, the same on every call: the rows come from a fixed seed. They are not the Blazor
 * Examples' rows, since .NET's generator draws another sequence from the same seed. */
export function sampleTrades(count = 200): Trade[] {
    const next = mulberry32(20261003);
    const pick = <T>(list: readonly T[]): T => list[Math.floor(next() * list.length)] as T;
    const firstDay = Temporal.PlainDate.from('2026-09-01');
    return Array.from({ length: count }, (_, at) => ({
        id: 10_001 + at,
        book: pick(books),
        trader: pick(traders),
        instrument: pick(instruments),
        currency: pick(currencies),
        notional: Math.round((next() * 49 + 1) * 10) * 100_000,
        price: Math.round((next() * 40 + 80) * 1000) / 1000,
        tradeDate: firstDay.add({ days: Math.floor(next() * 30) }),
        confirmed: next() >= 0.25,
    }));
}
