import { useEffect, useMemo, useRef, useState, type ReactElement } from 'react';
import { createChangeBatch, createSnapshot, type Snapshot, type SnapshotDecimal } from '@ex-grid/data';
import { SnapshotPivotSource, pivotField, type FetchingPivotSource, type PivotLayout, type PivotServerNotice } from '@ex-grid/pivot-engine';
import { ExPivot } from '@ex-grid/pivot-react';
import { apiUrl, notAnswering } from '../demo-api';
import { connectDemoHub } from '../demo-hub';
import { DemoNav } from '../demo-nav';
import { serverPivotDeclared, serverPivotSource } from '../demo-server-pivot';
import { DotNetRandom } from '../dotnet-random';
import { decimalOfDouble, demoPivotFirstLayout, demoPivotTradeCount, demoPivotTrades, roundToCents, type DemoPivotTrade } from '../pivot-data';
import { useQuery } from '../router';

const count = (value: number): string => value.toLocaleString('en-US');

// How many trades a batch amends unless ?batch= says, and how often.
const defaultTradesPerBatch = 5;
const batchEveryMs = 250;

// ExPivot gathers the changes and redraws at most every redrawInterval, from the newest data, and marks each value
// whose painted text changed for changeHighlightDuration (ADR-0067/0068). Both are the defaults, written out.
const redrawInterval = 250;
const changeHighlightDuration = 1000;

// DemoPivotData.Fields over the Snapshot's columns: Month is the month of the trade date, and the trade's id is the
// Record Key, not a field (ADR-0064).
const tradeFields = Object.freeze([
    pivotField({ name: 'Region', type: 'text' }),
    pivotField({ name: 'Desk', type: 'text' }),
    pivotField({ name: 'Book', type: 'text' }),
    pivotField({ name: 'Product', type: 'text' }),
    pivotField({ name: 'Currency', type: 'text' }),
    pivotField({ name: 'TradeDate', type: 'date', caption: 'Trade date', format: 'yyyy-MM-dd' }),
    pivotField({ name: 'Month', type: 'date', datePart: 'month', column: 'TradeDate' }),
    pivotField({ name: 'Notional', type: 'number', format: '#,##0.00' }),
    pivotField({ name: 'Pnl', type: 'number', caption: 'P&L', format: '#,##0.00' }),
    pivotField({ name: 'Quantity', type: 'number' }),
    pivotField({ name: 'Confirmed', type: 'boolean' }),
]);

/** Trades as a Snapshot whose Record Key is the trade's id: a Change Batch changes a trade by its key. */
function tradesSnapshot(trades: readonly DemoPivotTrade[]): Snapshot {
    return createSnapshot({
        recordKey: 'Id',
        columns: [
            { name: 'Id', kind: 'text', values: trades.map(t => t.id) },
            { name: 'Region', kind: 'text', values: trades.map(t => t.region) },
            { name: 'Desk', kind: 'text', values: trades.map(t => t.desk) },
            { name: 'Book', kind: 'text', values: trades.map(t => t.book) },
            { name: 'Product', kind: 'text', values: trades.map(t => t.product) },
            { name: 'Currency', kind: 'text', values: trades.map(t => t.currency) },
            { name: 'TradeDate', kind: 'date', values: trades.map(t => t.tradeDate) },
            { name: 'Notional', kind: 'decimal', values: trades.map(t => t.notional) },
            { name: 'Pnl', kind: 'decimal', values: trades.map(t => t.pnl) },
            { name: 'Quantity', kind: 'integer', values: trades.map(t => t.quantity) },
            { name: 'Confirmed', kind: 'boolean', values: trades.map(t => t.confirmed) },
        ],
    });
}

/** Two exact decimals added. */
function sum(a: SnapshotDecimal, b: SnapshotDecimal): SnapshotDecimal {
    const scale = Math.max(a.scale, b.scale);
    return { mantissa: a.mantissa * 10n ** BigInt(scale - a.scale) + b.mantissa * 10n ** BigInt(scale - b.scale), scale };
}

/** The trades the page holds and changes, and the source over them. */
interface LocalTrades {
    readonly trades: DemoPivotTrade[];
    readonly random: DotNetRandom;
    readonly source: SnapshotPivotSource;
}

/** A few trades, each picked once, their P&L moved by up to 0.1% of their notional, as the Blazor page's Amend:
 * `Math.Round(Notional * (decimal)(NextDouble() - 0.5) / 500m, 2)`. */
function amend(local: LocalTrades, howMany: number): DemoPivotTrade[] {
    const { trades, random } = local;
    const picked = new Set<number>();
    while (picked.size < howMany) picked.add(random.next(trades.length));
    return [...picked].map(at => {
        const trade = trades[at];
        if (trade === undefined) throw new Error(`Internal: trade ${at} of ${trades.length} picked.`);
        const fraction = decimalOfDouble(random.nextDouble() - 0.5);
        // × notional, then / 500 as × 2 / 1000: exact.
        const move = roundToCents({ mantissa: fraction.mantissa * trade.notional.mantissa * 2n, scale: fraction.scale + trade.notional.scale + 3 });
        const amended = Object.freeze({ ...trade, pnl: sum(trade.pnl, move) });
        trades[at] = amended;
        return amended;
    });
}

/** POST /api/live: twenty trades every 250 ms while on. `keepalive` lets the request outlive the page. */
async function setLive(on: boolean, keepalive = false): Promise<void> {
    const response = await fetch(apiUrl('api/live'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ on, intervalMs: 250, tradesPerTick: 20 }),
        keepalive,
    });
    if (!response.ok) throw new Error(`POST /api/live answered ${response.status}.`);
}

/** The page /pivot-live of the Blazor hosts under the Script Core: live data both ways (ADR-0067, ADR-0068).
 * In the page, its own timer folds Change Batches into the bundled source; on the demo API server, live updates
 * keep changing the trades, and the hub's notices reach the server's source through its transport. ?trades= and
 * ?batch= change how many trades the page holds and amends; ?scheme=dark shows the page in the dark scheme. */
export function PivotLive(): ReactElement {
    const tradesAsked = Number(useQuery('trades') ?? demoPivotTradeCount); // oxlint-disable-line ex-grid/no-stand-in-default -- a page not asked for a count holds /pivot's
    const batchAsked = Number(useQuery('batch') ?? defaultTradesPerBatch); // oxlint-disable-line ex-grid/no-stand-in-default -- a page not asked for a batch amends the Blazor page's five
    const dark = useQuery('scheme') === 'dark';
    const [local] = useState<LocalTrades>(() => {
        const trades = [...demoPivotTrades(tradesAsked > 0 ? tradesAsked : demoPivotTradeCount)];
        return { trades, random: new DotNetRandom(20261001), source: new SnapshotPivotSource(tradesSnapshot(trades), tradeFields) };
    });
    const tradesPerBatch = Math.min(batchAsked > 0 ? batchAsked : defaultTradesPerBatch, local.trades.length);
    const [localLayout, setLocalLayout] = useState<PivotLayout>(demoPivotFirstLayout);
    const [serverLayout, setServerLayout] = useState<PivotLayout>(demoPivotFirstLayout);
    const [localOn, setLocalOn] = useState(true);
    const [localStatus, setLocalStatus] = useState('No batch applied yet.');
    const [server, setServer] = useState<FetchingPivotSource | undefined>(undefined);
    const [serverSaid, setServerSaid] = useState<string | undefined>(undefined);
    const [connected, setConnected] = useState(false);
    const [serverOn, setServerOn] = useState(false);
    const [lastVersion, setLastVersion] = useState<string | undefined>(undefined);
    const [cutOff, setCutOff] = useState(false);
    // Read by the timer and by the server's questions, which outlive a render.
    const localOnNow = useRef(localOn);
    localOnNow.current = localOn;
    const cutOffNow = useRef(cutOff);
    cutOffNow.current = cutOff;

    // Four times a second, a few trades amended and handed to the source as one Change Batch: the source makes the
    // next Snapshot, brings the answer it holds up to date from what the batch removed and added, and tells its
    // listeners, ExPivot among them (ADR-0067).
    useEffect(() => {
        let batches = 0;
        const ticks = window.setInterval(() => {
            if (!localOnNow.current) return;
            const amended = amend(local, tradesPerBatch);
            const begun = performance.now();
            const change = local.source.apply(createChangeBatch({ changed: tradesSnapshot(amended) }));
            const applied = performance.now() - begun;
            batches++;
            const which = amended.length <= defaultTradesPerBatch ? amended.map(t => t.id).join(', ') : `${count(amended.length)} trades`;
            setLocalStatus(`Batch ${count(batches)}: ${which} amended, applied in ${count(Math.round(applied))} ms; `
                + `the Snapshot is at version ${change.after.version.toString()}.`);
        }, batchEveryMs);
        return () => { window.clearInterval(ticks); };
    }, [local, tradesPerBatch]);

    // The server's report through its Pivot Source, as on /pivot-db. Then the server is asked to keep changing its
    // trades, and the page passes on each Source Version its hub says (ADR-0067): neither ExPivot nor ExGrid learns
    // how the notice arrived, and polling GET /api/status would do as well.
    useEffect(() => {
        const leaving = new AbortController();
        const heard = new Set<(notice: PivotServerNotice) => void>();
        const hub = connectDemoHub({
            versionChanged: version => {
                for (const notify of Array.from(heard)) notify({ kind: 'changed', sourceVersion: version });
                setLastVersion(version);
            },
            // What the hub said while the connection was down was not heard: the report is asked again.
            reconnected: () => {
                for (const notify of Array.from(heard)) notify({ kind: 'changed' });
            },
            // Given up reconnecting: the server's report hears no more of its data, and the status line says so.
            lost: error => { setServerSaid(`The hub's connection is lost, and the server's changes are no longer heard: ${String(error)}`); },
        });
        // Whether the page asked for live updates, and whether the server has said it turned them on.
        let liveAsked = false;
        let liveOn = false;
        // The page's going turns the server's live updates off, so the data holds still for whoever reads it next
        // (ADR-0069). A document unloaded asks at once, with keepalive, whatever it asked last; a route left inside the
        // app asks once the server has turned them on, and otherwise the request turning them on asks after it.
        const turnOff = (): void => {
            setLive(false, true).catch(() => { // oxlint-disable-line ex-grid/no-catch -- a server that has gone is changing nothing
            });
        };
        const unloaded = (): void => {
            if (liveAsked) turnOff();
        };
        window.addEventListener('pagehide', unloaded);
        void (async () => {
            try {
                // Asked together, so that whichever fails is heard here.
                const [declared] = await Promise.all([serverPivotDeclared(leaving.signal), hub.started]);
                setServer(serverPivotSource(declared, {
                    // "Cut the server off" stands for a server out of reach: each question fails before it is sent,
                    // and ExPivot keeps the last report it could compute as a Stale Report.
                    asked: () => {
                        if (cutOffNow.current) throw new Error('The page cut the server off.');
                    },
                    subscribe: notify => {
                        heard.add(notify);
                        return () => { heard.delete(notify); };
                    },
                }));
                if (leaving.signal.aborted) return;
                // Live updates are off until a page turns them on (ADR-0069), and this one turns them off as it goes.
                liveAsked = true;
                await setLive(true);
                liveOn = true;
                if (leaving.signal.aborted) {
                    turnOff();
                    return;
                }
                setConnected(true);
                setServerOn(true);
            } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the page leaving stops its reads; a server not answering is said on the status line
                if (leaving.signal.aborted) return;
                setServerSaid(notAnswering(error));
            }
        })();
        return () => {
            leaving.abort();
            window.removeEventListener('pagehide', unloaded);
            if (liveOn) turnOff();
            void hub.stop();
        };
    }, []);

    const toggleServer = async (): Promise<void> => {
        try {
            await setLive(!serverOn);
            setServerOn(!serverOn);
            setServerSaid(undefined);
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a server not answering is said on the status line
            setServerSaid(notAnswering(error));
        }
    };
    const live = serverOn ? 'on, twenty trades every 250 ms' : 'off';
    const serverStatus = serverSaid !== undefined
        ? serverSaid
        : !connected
            ? 'Asking the demo API server for its fields…'
            : lastVersion === undefined
                ? `Live updates ${live}; no change heard yet.`
                : `Live updates ${live}; the hub last said version ${lastVersion}.`;

    // Made again only when what they show changes: the status lines move several times a second, and a pivot updated
    // with each of them would redraw its pane each time.
    const localPivot = useMemo(() => (
        <ExPivot source={local.source} layout={localLayout} onLayoutChanged={({ layout }) => { setLocalLayout(layout); }}
                 redrawInterval={redrawInterval} changeHighlightDuration={changeHighlightDuration}
                 locale="en-US" viewportHeight={300} />
    ), [local, localLayout]);
    const serverPivot = useMemo(() => (server === undefined ? null : (
        <ExPivot source={server} layout={serverLayout} onLayoutChanged={({ layout }) => { setServerLayout(layout); }}
                 redrawInterval={redrawInterval} changeHighlightDuration={changeHighlightDuration}
                 locale="en-US" viewportHeight={300} />
    )), [server, serverLayout]);

    return (
        <>
            {dark ? <style>{':root { color-scheme: dark; }'}</style> : null}
            <DemoNav route="pivot-live" />
            <h1>Live pivots — a Change Batch in the page, and a server whose data moves on</h1>
            <p>
                <strong>The use case:</strong> a risk screen whose trades change while it is read — amended, booked and
                cancelled several times a second. The data changes in one of two places, and what reaches the screen is
                the same either way (ADR-0067):
            </p>
            <ul>
                <li>
                    <strong>In this page.</strong> The page holds /pivot&apos;s 2,000 trades in a Snapshot whose Record Key
                    is the trade&apos;s id, read by <code>SnapshotPivotSource</code>. Its own timer amends a few trades four
                    times a second and hands each amendment to the source as one Change Batch
                    (<code>source.apply(createChangeBatch(&#123; changed &#125;))</code>). The source makes the next
                    Snapshot and brings the answer it holds up to date from what the batch removed and added, rather than
                    from 2,000 records again.
                </li>
                <li>
                    <strong>On a server.</strong> <code>POST /api/live</code> turns the demo API server&apos;s live updates
                    on: twenty trades of its database changed four times a second, and one in eight ticks a trade cancelled
                    and another booked. Its SignalR hub says each new Source Version, and the page passes it on through the
                    transport it hands <code>FetchingPivotSource</code> (<code>notify(&#123; kind: &apos;changed&apos;,
                    sourceVersion &#125;)</code>); ExPivot asks the server again, as on /pivot-db.
                </li>
            </ul>
            <p>
                ExPivot gathers the changes and redraws at most every 250 ms, from the newest data
                (<code>redrawInterval</code>), and marks each value whose painted text changed for a second
                (<code>changeHighlightDuration</code>, ADR-0068). A new layout, a sort or a collapse marks nothing, and
                the Selection stays while only values change. The page&apos;s changes stop, and the server&apos;s live
                updates go off, when you leave the page.
            </p>
            <p>
                <strong>Cut the server off</strong> stands for a server out of reach: the server report&apos;s next
                question fails, and ExPivot keeps the last report it could compute as a Stale Report. The notice under
                its Pivot Toolbar says as of when, with Retry, its numbers are muted, and Copy is refused, until the
                server answers again (ADR-0067).
            </p>
            <p>
                <code>?trades=1000000&amp;batch=1000</code> holds a million trades in this page and amends a thousand a
                batch, instead of 2,000 and five. <code>?scheme=dark</code> shows the page in the dark scheme.
            </p>
            <div className="demo-toolbar">
                <button id="pivot-live-local-toggle" type="button" onClick={() => { setLocalOn(!localOn); }}>
                    {localOn ? "Pause the page's changes" : "Resume the page's changes"}
                </button>
                <button id="pivot-live-server-toggle" type="button" disabled={!connected} onClick={() => { void toggleServer(); }}>
                    {serverOn ? "Turn the server's live updates off" : "Turn the server's live updates on"}
                </button>
                <button id="pivot-live-server-cut" type="button" disabled={!connected} onClick={() => { setCutOff(!cutOff); }}>
                    {cutOff ? 'Reconnect the server' : 'Cut the server off'}
                </button>
            </div>
            <div className="demo-pivot-pair">
                <section id="pivot-live-local" aria-labelledby="pivot-live-local-title">
                    <h2 id="pivot-live-local-title">In this page: Change Batches</h2>
                    <p className="demo-status" id="pivot-live-local-status">{localStatus}</p>
                    {localPivot}
                </section>
                <section id="pivot-live-server" aria-labelledby="pivot-live-server-title">
                    <h2 id="pivot-live-server-title">On a server: its data moves on</h2>
                    <p className="demo-status" id="pivot-live-server-status">{serverStatus}</p>
                    {serverPivot}
                </section>
            </div>
        </>
    );
}
