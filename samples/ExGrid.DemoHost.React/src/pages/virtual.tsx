import { useMemo, useSyncExternalStore, type ReactElement } from 'react';
import { GridSource, type FetchingGridSource, type GridPage, type GridQuery } from '@ex-grid/core';
import { ExGrid } from '@ex-grid/react';
import { DemoNav } from '../demo-nav';
import { tradeColumns, tradeWindow, type DemoTrade } from '../demo-data';
import { useFetchingSource } from '../use-fetching-source';

const total = 100_000;

/** Rows either side of the Viewport to answer with (ADR-0001: read-ahead belongs to the Consumer). */
const readAhead = 40;

const allRows = tradeWindow(total, 7);

/** Where a server call would go: it answers after 300 ms, so the Placeholders can be watched. */
function answerLate(query: GridQuery, signal: AbortSignal): Promise<GridPage<DemoTrade>> {
    return new Promise((resolve, reject) => {
        const timer = setTimeout(() => {
            const start = Math.min(query.range.start, total);
            const end = Math.min(total, query.range.start + query.range.count);
            resolve({ rows: allRows.slice(start, end), start, totalCount: total });
        }, 300);
        signal.addEventListener('abort', () => {
            clearTimeout(timer);
            reject(signal.reason instanceof Error ? signal.reason : new Error('The fetch was called off.'));
        }, { once: true });
    });
}

/** The page /virtual of the Blazor hosts: 100,000 rows the grid never holds (ADR-0001, ADR-0004). */
export function Virtual(): ReactElement | null {
    const source = useFetchingSource(() => GridSource.fetch(answerLate, { readAheadRows: readAhead }), []);
    return source === undefined ? null : <VirtualPage source={source} />;
}

function VirtualPage({ source }: { readonly source: FetchingGridSource<DemoTrade> }): ReactElement {
    // Bound once per source: a new subscribe function on every render would unsubscribe and subscribe again.
    const subscribe = useMemo(() => source.subscribe.bind(source), [source]);
    const snapshot = useMemo(() => source.snapshot.bind(source), [source]);
    const state = useSyncExternalStore(subscribe, snapshot);
    return (
        <>
            <DemoNav route="virtual" />
            <h1>Virtualised — 100,000 rows</h1>
            <p>
                The grid holds none of these rows. It paints the Window its source holds, and when the Viewport moves
                past what that Window covers it asks for the range and keeps painting Placeholders until an answer
                arrives (ADR-0001/0004). This page answers after 300&nbsp;ms, as a server would, and answers with more
                rows than were asked for: read-ahead is the Consumer&apos;s judgement, not the grid&apos;s.
            </p>
            <p className="demo-status">
                {state.window.length === 0
                    ? `Window: nothing pushed yet, of ${total} rows.`
                    : `Window: rows ${state.windowStart}–${state.windowStart + state.window.length - 1} of ${total}.`}
                {state.loading ? <strong>Loading…</strong> : null}
            </p>
            <ExGrid columns={tradeColumns} source={source} rowHeight={24} viewportHeight={480} />
        </>
    );
}
