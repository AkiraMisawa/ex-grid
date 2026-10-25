import { useMemo, useRef, useState, useSyncExternalStore, type ReactElement } from 'react';
import { GridSource, type FetchingGridSource, type GridColumn, type GridPage, type GridQuery } from '@ex-grid/core';
import { ExGrid } from '@ex-grid/react';
import { Temporal } from 'temporal-polyfill';
import { DemoNav } from '../demo-nav';
import { pick } from '../demo-data';
import { useFetchingSource } from '../use-fetching-source';

const total = 100_000;
const delayMs = 150;
const books = ['Rates', 'Credit', 'FX', 'Equity', 'Commodity'] as const;
const firstMinute = Temporal.PlainDateTime.from('2026-01-01T00:00:00');

/** A row of the server's result, as DemoData.cs's DemoServerRow. */
interface ServerRow {
    readonly index: number;
    readonly book: string;
    readonly amount: number;
    readonly asOf: Temporal.PlainDateTime;
}

// The Blazor page's Amount is a decimal of two places, which C# writes with both; a number keeps none.
const twoPlaces = (value: unknown): string => (value as number).toFixed(2);

/** DemoData.ServerColumns. */
const columns: readonly GridColumn<ServerRow>[] = Object.freeze([
    { name: 'Index', type: 'number', value: (r: ServerRow) => r.index, width: 90 },
    { name: 'Book', type: 'text', value: (r: ServerRow) => r.book, width: 180 },
    { name: 'Amount', type: 'number', value: (r: ServerRow) => r.amount, format: twoPlaces },
    { name: 'AsOf', type: 'date', value: (r: ServerRow) => r.asOf, header: 'As of' },
]);

/** DemoData.FetchAsync: a server that answers the range asked for, and nothing else, after a delay. */
function answer(query: GridQuery, signal: AbortSignal): Promise<GridPage<ServerRow>> {
    return new Promise((resolve, reject) => {
        const timer = setTimeout(() => {
            const start = Math.min(query.range.start, total);
            const count = Math.min(query.range.count, total - start);
            const rows = Array.from({ length: count }, (_, i): ServerRow => {
                const index = start + i;
                return Object.freeze({
                    index,
                    book: `${pick(books, index % books.length)}-${String(index).padStart(6, '0')}`,
                    amount: ((index * 7919) % 1_999_999) / 100,
                    asOf: firstMinute.add({ minutes: index }),
                });
            });
            resolve({ rows, start, totalCount: total });
        }, delayMs);
        signal.addEventListener('abort', () => {
            clearTimeout(timer);
            reject(signal.reason instanceof Error ? signal.reason : new Error('The fetch was called off.'));
        }, { once: true });
    });
}

/** The page /fetch of the Blazor hosts: GridSource.fetch over a slow server (ADR-0025). */
export function Fetch(): ReactElement | null {
    const [fetches, setFetches] = useState(0);
    const [error, setError] = useState<string | undefined>(undefined);
    // Read by the fetch when it is called, so the source is made once and never for a click.
    const failNext = useRef(false);
    const [failArmed, setFailArmed] = useState(false);
    const source = useFetchingSource(() => GridSource.fetch<ServerRow>(async (query, signal) => {
        setFetches(n => n + 1);
        if (failNext.current) {
            failNext.current = false;
            setFailArmed(false);
            await new Promise(resolve => { setTimeout(resolve, delayMs); });
            throw new Error('the server refused this range');
        }
        return answer(query, signal);
    }, {
        readAheadRows: 60,
        // Heard, so the failure is shown rather than thrown at a page that wired up nothing (ADR-0025).
        onFetchFailed: ({ error: failure }) => { setError(failure instanceof Error ? failure.message : String(failure)); },
    }), []);
    return source === undefined ? null : (
        <FetchPage
            source={source}
            fetches={fetches}
            error={error}
            failArmed={failArmed}
            onFailNext={() => { failNext.current = true; setFailArmed(true); }}
            onClear={() => { failNext.current = false; setFailArmed(false); setError(undefined); }} />
    );
}

interface FetchPageProps {
    readonly source: FetchingGridSource<ServerRow>;
    readonly fetches: number;
    readonly error: string | undefined;
    readonly failArmed: boolean;
    readonly onFailNext: () => void;
    readonly onClear: () => void;
}

function FetchPage({ source, fetches, error, failArmed, onFailNext, onClear }: FetchPageProps): ReactElement {
    const subscribe = useMemo(() => source.subscribe.bind(source), [source]);
    const snapshot = useMemo(() => source.snapshot.bind(source), [source]);
    const state = useSyncExternalStore(subscribe, snapshot);
    return (
        <>
            <DemoNav route="fetch" />
            <h1>Fetching source — 100,000 rows, one Window at a time</h1>
            <p>
                The pull entry point (ADR-0001): <code>GridSource.fetch</code> holds the Window and answers Range
                Requests, so this page holds no rows of its own. The &quot;server&quot; waits {delayMs} ms before
                answering, which is what makes the Placeholders and the loading state visible. Scroll fast and let go:
                only the range you landed on is painted; the answers to the ranges you passed through are discarded,
                never applied (ADR-0025).
            </p>
            <div className="demo-toolbar">
                <button id="fail-next" type="button" onClick={onFailNext} disabled={failArmed}>Make the next fetch fail</button>
                <button id="clear-error" type="button" onClick={onClear}>Clear</button>
            </div>
            <p className="demo-status">
                <span id="fetch-status">Fetches: {fetches}{state.loading ? ' (loading)' : ''}</span>
                <span id="fetch-error">{error === undefined ? '' : `Last failure: ${error}`}</span>
            </p>
            {/* The polyfill this page's dates are, which a date typed into a filter is made with. */}
            <ExGrid columns={columns} source={source} rowHeight={28} viewportHeight={600} viewportWidth={700} pinnedColumnCount={1} temporal={Temporal} />
        </>
    );
}
