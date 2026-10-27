import { useEffect } from 'react';
import { GridSource, type CellChangedAt, type GridColumn, type InMemoryGridSource } from '@ex-grid/core';
import { mulberry32 } from './Seeded';

/** A live quote for an invented instrument. */
export interface Quote {
    readonly symbol: string;
    readonly name: string;
    readonly bid: number;
    readonly ask: number;
    readonly open: number;
    readonly volume: number;
    /** When the quote last moved, as `HH:mm:ss`. */
    readonly time: string;
}

const instruments: readonly (readonly [string, string, number])[] = [
    ['ALDR', 'Alder Energy', 48.2], ['BRKW', 'Brookwell Holdings', 112.75], ['CNTR', 'Centra Logistics', 23.1],
    ['DLMR', 'Dalmore Foods', 67.45], ['EVRN', 'Everline Telecom', 15.82], ['FNMR', 'Fenmoor Mining', 91.3],
    ['GLRD', 'Glenrod Pharma', 204.6], ['HRTN', 'Harton Steel', 36.95], ['IVRS', 'Iverson Bank', 58.1],
    ['JSPR', 'Jasper Software', 342.15], ['KNLY', 'Kenley Retail', 27.4], ['LMBK', 'Lambeck Insurance', 81.05],
];

const cents = (value: number): number => Math.round(value * 100) / 100;

/** The middle of the bid and the ask. */
const last = (quote: Quote): number => cents((quote.bid + quote.ask) / 2);

const price = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const change = new Intl.NumberFormat('en-US', { style: 'percent', minimumFractionDigits: 2, maximumFractionDigits: 2, signDisplay: 'exceptZero' });
const volume = new Intl.NumberFormat('en-US');

export const columns: readonly GridColumn<Quote>[] = [
    { name: 'Symbol', type: 'text', value: q => q.symbol },
    { name: 'Name', type: 'text', value: q => q.name },
    { name: 'Bid', type: 'number', value: q => q.bid, format: v => price.format(v as number) },
    { name: 'Ask', type: 'number', value: q => q.ask, format: v => price.format(v as number) },
    { name: 'Last', type: 'number', value: last, format: v => price.format(v as number) },
    {
        name: 'Change', type: 'number', header: 'Chg %', value: q => last(q) / q.open - 1, format: v => change.format(v as number),
        // The tone of the change as shown, read from the text the cell shows: a move that rounds to 0.00% is no gain
        // or loss, and the sign the format writes is the tone's.
        tone: v => { const shown = change.format(v as number); return shown.startsWith('+') ? 'positive' : shown.startsWith('-') ? 'negative' : 'none'; },
    },
    { name: 'Volume', type: 'number', value: q => q.volume, format: v => volume.format(v as number) },
    { name: 'Time', type: 'text', value: q => q.time },
];

/** A price board and the feed that moves it: each tick moves a few quotes. Keyed by symbol, so each tick hands over
 * a new version of the same quotes, and the source marks the cells whose text changed: its `cellChangedAt`, one
 * function for the source's life, asked again only for a replaced row. */
export function quoteBoard(): {
    readonly source: InMemoryGridSource<Quote>;
    readonly cellChangedAt: CellChangedAt<Quote>;
    readonly tick: () => void;
} {
    const next = mulberry32(20261003);
    const quotes = instruments.map(([symbol, name, open]): Quote => ({
        symbol, name, bid: cents(open - 0.02), ask: cents(open + 0.02), open, volume: 50_000 + Math.floor(next() * 350_000), time: '09:30:00',
    }));
    const source = GridSource.from(quotes, { rowKey: q => q.symbol });
    const { cellChangedAt } = source;
    if (cellChangedAt === undefined) throw new Error('A source with a Row Key offers a Change Highlight.');
    const tick = (): void => {
        const time = new Date().toTimeString().slice(0, 8);
        const moved = new Map<string, Quote>();
        for (let n = 0; n < 3; n++) {
            const at = Math.floor(next() * quotes.length);
            const before = quotes[at];
            if (before === undefined) throw new RangeError(`There is no quote ${String(at)}.`);
            const move = cents(last(before) * (next() - 0.5) * 0.006);
            const spread = cents(0.01 + next() * 0.04);
            const mid = last(before) + move;
            const after = { ...before, bid: cents(mid - spread), ask: cents(mid + spread), volume: before.volume + 100 + Math.floor(next() * 4_900), time };
            quotes[at] = after;
            moved.set(after.symbol, after);
        }
        // A Change Batch, as a feed sends it: the rows changed, by key.
        source.apply({ changed: [...moved.values()] });
    };
    return { source, cellChangedAt, tick };
}

/** Runs a board's feed while `live`: every 600 ms a few quotes move. */
export function useQuoteFeed(tick: () => void, live: boolean): void {
    useEffect(() => {
        if (!live) return undefined;
        const timer = setInterval(tick, 600);
        return () => { clearInterval(timer); };
    }, [tick, live]);
}
