import { useState, type ReactElement } from 'react';
import { GridSource, type FetchingGridSource, type FetchingRowMarks, type GridFilter } from '@ex-grid/core';
import { ExGrid } from '@ex-grid/react';
import { DemoNav } from '../demo-nav';
import { DemoMarkServer, markColumns, type DemoMarkRow } from '../demo-mark-server';
import { useFetchingSource } from '../use-fetching-source';

const total = 1_000_000;
const delayMs = 30;

const alphaOnly: GridFilter = { columns: { Desk: { clauses: [{ operator: 'equals', value: 'Alpha' }] } } };

// Written as C#'s N0 writes a count on the Blazor page.
const counted = (value: number): string => value.toLocaleString('en-US');

const messageOf = (failure: unknown): string => (failure instanceof Error ? failure.message : String(failure));

/** The page /marks of the Blazor hosts: Row Marks through GridSource.fetch with a mark adapter (ADR-0043). */
export function Marks(): ReactElement | null {
    const [server] = useState(() => new DemoMarkServer(total));
    const [error, setError] = useState<string | undefined>(undefined);
    const source = useFetchingSource(() => GridSource.fetch<DemoMarkRow>(async (query, signal) => {
        await new Promise<void>((resolve, reject) => {
            const timer = setTimeout(resolve, delayMs);
            signal.addEventListener('abort', () => {
                clearTimeout(timer);
                reject(signal.reason instanceof Error ? signal.reason : new Error('The fetch was called off.'));
            }, { once: true });
        });
        return server.fetch(query);
    }, {
        readAheadRows: 60,
        marks: server.adapter(),
        // Heard, so a failure is shown rather than thrown at a page that wired up nothing (ADR-0025).
        onFetchFailed: ({ error: failure }) => { setError(messageOf(failure)); },
    }), [server]);
    return source === undefined ? null : <MarksPage server={server} source={source} error={error} onFailed={failure => { setError(messageOf(failure)); }} />;
}

interface MarksPageProps {
    readonly server: DemoMarkServer;
    readonly source: FetchingGridSource<DemoMarkRow>;
    readonly error: string | undefined;
    readonly onFailed: (failure: unknown) => void;
}

function MarksPage({ server, source, error, onFailed }: MarksPageProps): ReactElement {
    const [result, setResult] = useState('');
    const marks = (): FetchingRowMarks<DemoMarkRow> => {
        const held = source.marks;
        if (held === undefined) throw new Error('This page\'s source is given a mark adapter, and keeps marks.');
        return held;
    };
    // News only the server has: the page tells the marks to count again, and the header turns to "some" if
    // everything had been marked (ADR-0043).
    const recount = (): void => { marks().recount().catch(onFailed); };
    // Run by identity, verified where the data lives, and reported as it went: a partial result is never passed
    // off as success (ADR-0043).
    const approve = (): void => {
        const { applied, gone } = server.approve(marks().state);
        setResult(gone === 0
            ? `Approved ${counted(applied)} rows.`
            : `Approved ${counted(applied)} of ${counted(applied + gone)} marked rows; ${counted(gone)} no longer exist.`);
    };
    return (
        <>
            <DemoNav route="marks" />
            <h1>Row Marks — 1,000,000 rows through a fetching source</h1>
            <p>
                The Mark Column (ADR-0043). The header marks every row of the current result, as it stands at the
                press, whether or not it is on screen; Space with the Focus in the Mark Column brings the selected
                rows into line. The marks belong to each row&apos;s identity and are kept by the source, as keys and
                snapshots the server can count and act on — never as positions. Filtering keeps them, and the count
                says how many lie outside the filter.
            </p>
            <div className="demo-toolbar">
                <button id="filter-alpha" type="button" onClick={() => { source.setFilter(alphaOnly); }}>Show desk Alpha only</button>
                <button id="filter-none" type="button" onClick={() => { source.setFilter(undefined); }}>Show every desk</button>
                <button id="add-row" type="button" onClick={() => { server.add(); recount(); }}>Add a row elsewhere</button>
                <button id="delete-row" type="button" onClick={() => { server.delete(3); recount(); }}>Delete row 3 elsewhere</button>
                <button id="approve" type="button" onClick={approve}>Approve the marked rows</button>
            </div>
            <p className="demo-status">
                <span id="approve-result">{result}</span>
                <span id="fetch-error">{error === undefined ? '' : `Last failure: ${error}`}</span>
            </p>
            <ExGrid columns={markColumns} source={source} rowHeight={28} viewportHeight={600} viewportWidth={500} />
        </>
    );
}
