import { useEffect, useRef, useState, type ReactElement } from 'react';
import { GridSource, type CellChangedAt, type GridColumn, type InMemoryGridSource } from '@ex-grid/core';
import { ExGrid } from '@ex-grid/react';
import { pick } from '../demo-data';
import { DemoNav } from '../demo-nav';
import { DotNetRandom } from '../dotnet-random';
import { useQuery } from '../router';

/** A booked trade, as DemoPivotTrade, its amounts in whole cents so an amendment adds exactly. */
interface LiveTrade {
    readonly id: string;
    readonly region: string;
    readonly desk: string;
    readonly book: string;
    readonly product: string;
    readonly currency: string;
    readonly tradeDate: string;
    readonly notionalCents: number;
    readonly pnlCents: number;
    readonly quantity: number;
    readonly confirmed: boolean;
}

// How many trades the page holds unless ?rows= says, how many a batch amends unless ?batch= says, and how often.
const defaultRows = 1_000_000;
const defaultTradesPerBatch = 20;
const batchEveryMs = 250;
// The trades the busy half of every batch falls on: the rows the grid opens on.
const busyTrades = 500;
// A mark lasts a second (ADR-0068).
const highlightMs = 1_000;

const regions: readonly (readonly [string, readonly string[]])[] = [
    ['Americas', ['NY-RATES-01', 'NY-CREDIT-02', 'TOR-FX-01']],
    ['EMEA', ['LDN-RATES-01', 'LDN-FX-02', 'FRA-CREDIT-01', 'ZRH-EQ-01']],
    ['APAC', ['TKY-RATES-01', 'HKG-EQ-01', 'SGP-FX-01']],
];
const products = ['Swap', 'Bond', 'Option', 'Future', 'Spot'] as const;
const currencies = ['USD', 'EUR', 'JPY', 'GBP', 'CHF'] as const;
const firstDay = Date.UTC(2026, 0, 2);
const dayMs = 86_400_000;

/** DemoPivotData.Generate's trades, from its seed, a slice at a time, the browser given its turn between slices. */
async function makeTrades(count: number, progress: (made: number) => void, signal: AbortSignal): Promise<LiveTrade[]> {
    const random = new DotNetRandom(20260930);
    const trades: LiveTrade[] = [];
    while (trades.length < count) {
        const end = Math.min(count, trades.length + 50_000);
        for (let i = trades.length; i < end; i++) {
            const [region, books] = pick(regions, random.next(regions.length));
            const book = pick(books, random.next(books.length));
            const desk = book.includes('RATES') ? 'Rates' : book.includes('CREDIT') ? 'Credit' : book.includes('FX') ? 'FX' : 'Equities';
            const notional = random.next(1, 500) * 10_000;
            const pnlCents = Math.round((random.nextDouble() - 0.45) * notional * 100);
            trades.push(Object.freeze({
                id: `T${100000 + i}`,
                region,
                desk,
                book,
                product: pick(products, random.next(products.length)),
                currency: pick(currencies, random.next(currencies.length)),
                tradeDate: new Date(firstDay + (random.next(0, 270) * dayMs)).toISOString().slice(0, 10),
                notionalCents: notional * 100,
                pnlCents,
                quantity: random.next(1, 50),
                confirmed: random.next(10) > 0,
            }));
        }
        if (trades.length < count) {
            progress(trades.length);
            await new Promise(resolve => { setTimeout(resolve, 1); });
            signal.throwIfAborted();
        }
    }
    return trades;
}

const cents = (value: unknown): number => {
    if (typeof value !== 'number') throw new TypeError('A money column holds a number.');
    return value;
};

// #,##0.00, as the Blazor page formats its decimals.
const money = (value: unknown): string => {
    const amount = cents(value);
    const [whole = '', fraction = ''] = Math.abs(amount).toFixed(2).split('.');
    return `${amount < 0 ? '-' : ''}${whole.replace(/\B(?=(\d{3})+(?!\d))/g, ',')}.${fraction}`;
};

const count = (value: number): string => value.toLocaleString('en-US');

// Held once, never rebuilt per render: a new array would paint every row again (ADR-0003). The widths leave room
// for every digit the amendments can bring; a number that does not fit is #### rather than cut short (ADR-0016).
const columns: readonly GridColumn<LiveTrade>[] = Object.freeze([
    { name: 'Id', type: 'text', value: (t: LiveTrade) => t.id, header: 'Trade ID', width: 110 },
    { name: 'Region', type: 'text', value: (t: LiveTrade) => t.region, width: 100 },
    { name: 'Desk', type: 'text', value: (t: LiveTrade) => t.desk, width: 90 },
    { name: 'Book', type: 'text', value: (t: LiveTrade) => t.book, width: 140 },
    { name: 'Product', type: 'text', value: (t: LiveTrade) => t.product, width: 90 },
    { name: 'Currency', type: 'text', value: (t: LiveTrade) => t.currency, width: 90 },
    { name: 'TradeDate', type: 'text', value: (t: LiveTrade) => t.tradeDate, header: 'Trade date', width: 120 },
    { name: 'Notional', type: 'number', value: (t: LiveTrade) => t.notionalCents / 100, width: 170, format: money },
    { name: 'Pnl', type: 'number', value: (t: LiveTrade) => t.pnlCents / 100, header: 'P&L', width: 150, format: money },
    { name: 'Quantity', type: 'number', value: (t: LiveTrade) => t.quantity, width: 90 },
    { name: 'Confirmed', type: 'boolean', value: (t: LiveTrade) => t.confirmed, width: 100 },
]);

const rowKey = (t: LiveTrade): string => t.id;

/** What the page holds once its trades are made: the trades, newest version of each, and the source over them. */
interface Made {
    readonly trades: LiveTrade[];
    readonly source: InMemoryGridSource<LiveTrade>;
    readonly cellChangedAt: CellChangedAt<LiveTrade> | undefined;
}

/**
 * The page /grid-live-local of the Blazor hosts (ADR-0141, D8): a million trades in this process, bound through
 * GridSource.from with the trade's id as the Row Key, and a Change Batch of amended trades four times a second.
 */
export function GridLiveLocal(): ReactElement {
    const rowsAsked = Number(useQuery('rows') ?? defaultRows);
    const batchAsked = Number(useQuery('batch') ?? defaultTradesPerBatch);
    const intervalAsked = useQuery('interval');
    const rowCount = Number.isInteger(rowsAsked) && rowsAsked > 0 ? rowsAsked : defaultRows;
    const [made, setMade] = useState<Made | undefined>(undefined);
    const [madeLine, setMadeLine] = useState('Making trades…');
    const [batchLine, setBatchLine] = useState('No batch applied yet.');
    const [on, setOn] = useState(true);
    // Written by the click itself and read by the timer's tick, which checks the switch before it applies; the click
    // shows the last batch's line with "Resume", in the same render, so the count shown beside it is the last
    // whenever React renders a tick's line (principle 6).
    const onRef = useRef(true);
    const lineRef = useRef('No batch applied yet.');

    useEffect(() => {
        const leaving = new AbortController();
        const clock = performance.now();
        void makeTrades(rowCount, made => { setMadeLine(`Making trades: ${count(made)} of ${count(rowCount)}…`); }, leaving.signal)
            .then(trades => {
                // A run the page has left, or asked again over, shows nothing.
                if (leaving.signal.aborted) return;
                const making = performance.now() - clock;
                const from = performance.now();
                const source = GridSource.from(trades, { rowKey });
                if (intervalAsked !== undefined) source.gatherInterval = Number(intervalAsked);
                setMadeLine(`${count(trades.length)} trades made in ${count(Math.round(making))} ms; `
                    + `GridSource.from took ${count(Math.round(performance.now() - from))} ms.`);
                setMade({ trades, source, cellChangedAt: source.cellChangedAt });
            }, (error: unknown) => {
                // The page went first; anything else is a defect of the page's, reported as the page's.
                if (!leaving.signal.aborted) throw error;
            });
        return () => { leaving.abort(); };
    }, [rowCount, intervalAsked]);

    useEffect(() => {
        if (made === undefined) return undefined;
        const { trades, source } = made;
        const perBatch = Math.min(Number.isInteger(batchAsked) && batchAsked > 0 ? batchAsked : defaultTradesPerBatch, trades.length);
        const random = new DotNetRandom(20261006);
        let batches = 0;
        // Trades each picked once, half of them — up to half the busy ones — among the busy ones: the P&L moved by up to
        // 0.1% of the notional, and one in four the notional amended by a million as well. Every amendment is a new
        // object (ADR-0003); the page keeps it as the trade's newest version.
        const amend = (): LiveTrade[] => {
            const picked = new Set<number>();
            const busy = Math.min(Math.floor(perBatch / 2), busyTrades / 2, trades.length);
            while (picked.size < busy) picked.add(random.next(Math.min(busyTrades, trades.length)));
            while (picked.size < perBatch) picked.add(random.next(trades.length));
            return [...picked].map(at => {
                const trade = trades[at];
                if (trade === undefined) throw new Error(`Internal: no trade at ${at}.`);
                const move = Math.round(trade.notionalCents * (random.nextDouble() - 0.5) / 500);
                // Up or down by a million, down only from above a million, as the demo API server amends.
                const notionalCents = random.next(4) !== 0 ? trade.notionalCents
                    : trade.notionalCents > 100_000_000 && random.next(2) === 0 ? trade.notionalCents - 100_000_000
                        : trade.notionalCents + 100_000_000;
                const amended = Object.freeze({ ...trade, pnlCents: trade.pnlCents + move, notionalCents });
                trades[at] = amended;
                return amended;
            });
        };
        const timer = setInterval(() => {
            if (!onRef.current) return;
            const amended = amend();
            const from = performance.now();
            source.apply({ changed: amended });
            batches += 1;
            lineRef.current = `Batch ${count(batches)}: ${count(amended.length)} trades amended, applied in ${count(Math.round(performance.now() - from))} ms.`;
            setBatchLine(lineRef.current);
        }, batchEveryMs);
        // The page's changes stop when it goes.
        return () => { clearInterval(timer); };
    }, [made, batchAsked]);

    return (
        <>
            <DemoNav route="grid-live-local" />
            <h1>Live grid in this process — Change Batches over a million trades</h1>
            <p>
                <strong>The use case:</strong> a blotter whose trades are held in this process and amended several times a
                second. The page makes a million trades and hands them to <code>GridSource.from</code> with the
                trade&apos;s id as the Row Key (ADR-0140/0141). Its own timer amends a few trades four times a second and
                hands each amendment to the source as one Change Batch (<code>source.apply({'{ changed }'})</code>). Half
                of every batch, up to 250 trades, falls on the first 500 trades, the busy ones the grid opens on, so the rows
                in view change while you read them; the rest fall anywhere in the million.
            </p>
            <p>
                The source pairs each amended trade with the one it holds by key, brings its result up to date from the rows
                that changed, and hands the grid the newest version at most every 250 ms. It answers the grid&apos;s
                {' '}<code>cellChangedAt</code> with the cells whose painted text changed, and the grid marks them for a
                second (ADR-0068). A click on a header sorts the million by that column, and the order then follows the
                changes at once. The page&apos;s changes stop when you leave it.
            </p>
            <p><code>?rows=2000&amp;batch=1000</code> holds 2,000 trades and amends a thousand a batch, instead of a million and twenty.</p>
            <div className="demo-toolbar">
                <button id="grid-live-local-toggle" type="button" disabled={made === undefined} onClick={() => {
                    onRef.current = !onRef.current;
                    setOn(onRef.current);
                    setBatchLine(lineRef.current);
                }}>
                    {on ? "Pause the page's changes" : "Resume the page's changes"}
                </button>
            </div>
            <p className="demo-status">
                <span id="grid-live-local-made">{madeLine}</span>
                {' '}
                <span id="grid-live-local-status">{batchLine}</span>
            </p>
            {made === undefined
                ? <p className="demo-status">The grid appears once the trades are made.</p>
                : (
                    <ExGrid
                        columns={columns}
                        source={made.source}
                        pinnedColumnCount={1}
                        {...(made.cellChangedAt === undefined ? {} : { cellChangedAt: made.cellChangedAt })}
                        changeHighlightDuration={highlightMs}
                        viewportHeight={480}
                        viewportWidth={1270} />
                )}
        </>
    );
}
