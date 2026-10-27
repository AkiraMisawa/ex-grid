import { Temporal } from 'temporal-polyfill';
import { mulberry32 } from './Seeded';

/** A position on a trading desk, as several ExGrid Examples show it. */
export interface Position {
    readonly id: number;
    readonly desk: string;
    readonly trader: string;
    readonly instrument: string;
    readonly currency: string;
    readonly quantity: number;
    readonly cost: number;
    readonly price: number;
    /** The unrealised profit or loss: what the position is worth now against what it cost. */
    readonly pnl: number;
    readonly tradeDate: Temporal.PlainDate;
    readonly hedged: boolean;
    /** How much of the desk's limit the position takes, 1 being all of it. */
    readonly limitUsed: number;
}

const desks = ['Rates', 'Credit', 'FX', 'Equities'];
const traders = ['Ishikawa', 'Novak', 'Osei', 'Laurent', 'Haddad', 'Kowalski', 'Brennan', 'Sato'];
const instruments: readonly (readonly [string, string, number])[] = [
    ['UST 10Y', 'USD', 98.4], ['Bund 5Y', 'EUR', 101.15], ['Gilt 30Y', 'GBP', 87.65],
    ['JGB 10Y', 'JPY', 99.82], ['iTraxx Main', 'EUR', 61.3], ['CDX IG', 'USD', 54.75],
    ['EUR/USD 3M', 'USD', 108.42], ['USD/JPY 1M', 'JPY', 149.1], ['SPX Dec', 'USD', 57.25],
    ['NKY Mar', 'JPY', 38.9], ['DAX Jun', 'EUR', 182.4], ['FTSE Sep', 'GBP', 81.15],
];

/** `count` invented positions, the same on every call: the rows come from a fixed seed. They are not the Blazor
 * Examples' rows, since .NET's generator draws another sequence from the same seed. */
export function samplePositions(count = 300): Position[] {
    const next = mulberry32(20261003);
    // The limit used is drawn from a sequence of its own, so the other values stay what they were before it.
    const nextLimit = mulberry32(20261004);
    const below = (n: number): number => Math.floor(next() * n);
    const cents = (value: number): number => Math.round(value * 100) / 100;
    const firstDay = Temporal.PlainDate.from('2026-09-01');
    return Array.from({ length: count }, (_, at) => {
        const [instrument, currency, mark] = instruments[below(instruments.length)] as readonly [string, string, number];
        const cost = cents(mark * (0.94 + next() * 0.12));
        const price = cents(mark * (0.97 + next() * 0.06));
        const quantity = (below(2) === 0 ? -1 : 1) * (1 + below(399)) * 250;
        return {
            id: 5_001 + at,
            desk: desks[below(desks.length)] as string,
            trader: traders[below(traders.length)] as string,
            instrument,
            currency,
            quantity,
            cost,
            price,
            pnl: cents((price - cost) * quantity),
            tradeDate: firstDay.add({ days: below(30) }),
            hedged: below(3) !== 0,
            limitUsed: Math.round(nextLimit() * 1.1 * 1000) / 1000,
        };
    });
}
