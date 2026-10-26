import { useCallback, useEffect, useMemo, useRef, useState, type ReactElement } from 'react';
import { readArrowSnapshot } from '@ex-grid/data';
import {
    FetchingPivotSource, PivotDetailPage, PivotJson, PivotRefusals, PivotSourceFeatures, SnapshotPivotSource, createPivotLayout, pivotField,
    type PivotAggregation, type PivotDetailRecord, type PivotDetailsQuery, type PivotField, type PivotFieldType, type PivotLayout,
} from '@ex-grid/pivot-engine';
import { ExPivot } from '@ex-grid/pivot-react';
import { DemoNav } from '../demo-nav';

const count = (value: number): string => value.toLocaleString('en-US');

/** The demo API server, at the page's own port plus 3000, as DemoApiClient finds it (ADR-0069). */
function apiUrl(path: string): string {
    const { protocol, hostname, port } = window.location;
    // A page served on its scheme's own port names none.
    const served = port === '' ? (protocol === 'https:' ? 443 : 80) : Number(port);
    return `${protocol}//${hostname}:${served + 3000}/${path}`;
}

/** The most records the server answers in one Details page. */
const maxDetailsPage = 10_000;

// P&L by region and desk across products, with the currency to filter by.
function firstLayout(): PivotLayout {
    return createPivotLayout({
        filters: ['Currency'],
        rows: ['Region', 'Desk'],
        columns: ['Product'],
        values: [{ field: 'Pnl', aggregation: 'sum', numberFormat: '#,##0' }],
    });
}

/** C#'s enum name, as the server writes it ("Text", "CountNumbers"), in the Script Core's spelling. */
function camel(name: unknown): string {
    if (typeof name !== 'string' || name === '') throw new TypeError(`The server named a field type or an Aggregation as ${JSON.stringify(name)}.`);
    return name.charAt(0).toLowerCase() + name.slice(1);
}

interface Declared {
    readonly fields: readonly PivotField[];
    readonly features: PivotSourceFeatures;
}

/** GET /api/pivot/fields: the server's fields, with their captions and formats, the Aggregations SQLite answers
 * exactly (ADR-0066), and whether it counts each field's Items and answers a question past its cap with the groups
 * shown (ADR-0370 section 3). */
async function declaredFields(signal: AbortSignal): Promise<Declared> {
    const response = await fetch(apiUrl('api/pivot/fields'), { signal });
    if (!response.ok) throw new Error(`GET /api/pivot/fields answered ${response.status}.`);
    const declared = await response.json() as {
        readonly fields: readonly { readonly name: string; readonly type: string; readonly caption: string; readonly format: string | null }[];
        readonly features: {
            readonly aggregations: readonly string[]; readonly canRefresh: boolean; readonly countsItems: boolean; readonly answersLevels: boolean;
        };
    };
    const fields = declared.fields.map(field => pivotField({
        name: field.name, type: camel(field.type) as PivotFieldType, caption: field.caption, ...(field.format === null ? {} : { format: field.format }),
    }));
    const { aggregations, canRefresh, countsItems, answersLevels } = declared.features;
    return { fields, features: new PivotSourceFeatures(aggregations.map(each => camel(each) as PivotAggregation), canRefresh, countsItems, answersLevels) };
}

/** GETs a PivotJson document. */
async function get(path: string, signal: AbortSignal | undefined): Promise<string> {
    const response = await fetch(apiUrl(path), signal === undefined ? {} : { signal });
    if (!response.ok) throw new Error(`GET /${path} answered ${response.status}: ${await response.text()}`);
    return response.text();
}

/** POSTs a PivotJson document and reads the answer's. */
async function post(path: string, body: string, signal: AbortSignal | undefined): Promise<string> {
    const response = await fetch(apiUrl(path), { method: 'POST', headers: { 'Content-Type': 'application/json' }, body, ...(signal === undefined ? {} : { signal }) });
    if (!response.ok) throw new Error(`POST /${path} answered ${response.status}: ${await response.text()}`);
    return response.text();
}

/** The server as a Pivot Source (ADR-0066, ADR-0370 section 3): it answers the Leaf Aggregates of each question
 * in SQL, and the pivot builds the report and its totals from them here. Details come a page at a time, each asked
 * under the question's Source Version. */
function serverSource(declared: Declared, asked: (kind: string) => void): FetchingPivotSource {
    return new FetchingPivotSource(declared.fields, declared.features, {
        aggregate: async (query, signal) => {
            asked('aggregate');
            return PivotJson.readAnswer(await post('api/pivot/aggregate', PivotJson.writeQuery(query), signal));
        },
        items: async (query, signal) => {
            asked('items');
            return PivotJson.readItemPage(await post('api/pivot/items', PivotJson.writeItemsQuery(query), signal));
        },
        details: async (query, signal) => {
            const records: PivotDetailRecord[] = [];
            for (;;) {
                const next: PivotDetailsQuery = Object.freeze({
                    ...query, start: query.start + records.length, count: Math.min(maxDetailsPage, query.count - records.length),
                });
                asked('details');
                const page = PivotJson.readDetailPage(await post('api/pivot/details', PivotJson.writeDetailsQuery(next), signal));
                if (page.isRefused) return page;
                // Pages of two versions are not one page: the records moved between them.
                if (page.sourceVersion !== query.sourceVersion) return PivotDetailPage.refused(PivotRefusals.sourceVersionNotHeld(query.sourceVersion));
                records.push(...page.records);
                if (page.records.length === 0 || records.length >= query.count || query.start + records.length >= page.total) {
                    return PivotDetailPage.of(page.sourceVersion, page.fields, query.start, page.total, records);
                }
            }
        },
        ...(declared.features.countsItems ? {
            itemCounts: async (signal?: AbortSignal) => PivotJson.readItemCounts(await get('api/pivot/item-counts', signal)),
        } : {}),
    });
}

/** Database → Snapshot (ADR-0064/0065): the server reads its trades into a Snapshot and writes it as an Arrow
 * stream; the page reads the stream into a Snapshot of its own, and the bundled source answers every question from
 * it, in this page. */
async function readSnapshot(fields: readonly PivotField[], signal: AbortSignal): Promise<{ readonly source: SnapshotPivotSource; readonly version: string }> {
    const response = await fetch(apiUrl('api/trades.arrows'), { signal });
    if (!response.ok) throw new Error(`GET /api/trades.arrows answered ${response.status}.`);
    // The Source Version the server read the trades at: its run and its change counter.
    const version = response.headers.get('ExGrid-Source-Version');
    if (version === null) throw new Error('The server named no Source Version for the trades.');
    const snapshot = await readArrowSnapshot(await response.arrayBuffer(), { signal });
    return { source: new SnapshotPivotSource(snapshot, fields), version };
}

/** The page /pivot-db of the Blazor hosts under the Script Core: the demo API server's SQLite trades two ways, read
 * over Arrow into a Snapshot here, and asked of the server's Pivot Source question by question (ADR-0069). */
export function PivotDb(): ReactElement {
    const [declared, setDeclared] = useState<Declared | undefined>(undefined);
    const [snapshot, setSnapshot] = useState<SnapshotPivotSource | undefined>(undefined);
    const [snapshotStatus, setSnapshotStatus] = useState('Reading the trades from the demo API server…');
    const [fieldsFailure, setFieldsFailure] = useState<string | undefined>(undefined);
    const [reading, setReading] = useState(false);
    const [asked, setAsked] = useState<Readonly<Record<string, number>>>({});
    const [snapshotLayout, setSnapshotLayout] = useState<PivotLayout>(firstLayout);
    const [serverLayout, setServerLayout] = useState<PivotLayout>(firstLayout);
    const leaving = useRef(new AbortController());

    const read = useCallback(async (fields: readonly PivotField[]): Promise<void> => {
        setReading(true);
        const { signal } = leaving.current;
        try {
            const started = performance.now();
            const { source, version } = await readSnapshot(fields, signal);
            // A new source is new data: ExPivot asks it again, and marks what changed (ADR-0067).
            setSnapshot(source);
            setSnapshotStatus(`Read ${count(source.snapshot.rowCount)} trades at version ${version} in ${count(Math.round(performance.now() - started))} ms.`);
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the page leaving stops its read; a failed read is said on the status line and rethrown
            if (signal.aborted) return;
            setSnapshotStatus(`The trades could not be read: ${String(error)}`);
            throw error;
        } finally {
            setReading(false);
        }
    }, []);

    useEffect(() => {
        const controller = new AbortController();
        leaving.current = controller;
        void (async () => {
            let fields: Declared;
            try {
                fields = await declaredFields(controller.signal);
            } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the page leaving stops its reads; any other failure is said on both status lines and rethrown
                if (controller.signal.aborted) return;
                const said = `The demo API server is not answering: ${String(error)}`;
                setSnapshotStatus(said);
                setFieldsFailure(said);
                throw error;
            }
            setDeclared(fields);
            await read(fields.fields);
        })();
        return () => { controller.abort(); };
    }, [read]);

    // Told of each question as the server's source sends it.
    const server = useMemo(() => (declared === undefined
        ? undefined
        : serverSource(declared, kind => { setAsked(was => ({ ...was, [kind]: (was[kind] ?? 0) + 1 })); })), [declared]); // oxlint-disable-line ex-grid/no-stand-in-default -- a kind not asked yet has been asked none
    const times = (kind: string): string => count(asked[kind] ?? 0); // oxlint-disable-line ex-grid/no-stand-in-default -- a kind not asked yet has been asked none

    return (
        <>
            <DemoNav route="pivot-db" />
            <h1>Pivot over a database — the same trades two ways</h1>
            <p>
                <strong>The use case:</strong> the trades live in a database on a server, and the page pivots them. The
                demo API server owns a SQLite file of trades (ADR-0069), and the two pivots below read the same file two
                ways, side by side:
            </p>
            <ul>
                <li>
                    <strong>Database → Snapshot.</strong> The server sends its trades as an Arrow stream
                    (<code>GET /api/trades.arrows</code>); the page reads it into a Snapshot of its own with{' '}
                    <code>readArrowSnapshot</code>, and <code>SnapshotPivotSource</code> answers every question here.
                </li>
                <li>
                    <strong>Database → server Pivot Source.</strong> <code>FetchingPivotSource</code> asks the server
                    for the Leaf Aggregates of each question, answered in SQL, and the pivot builds the report and its
                    totals from them here (ADR-0370 section 3). Items and Details are asked of the server too.
                </li>
            </ul>
            <div className="demo-toolbar">
                <button id="pivot-db-reread" type="button" disabled={declared === undefined || reading}
                        onClick={() => {
                            if (declared === undefined) return;
                            setSnapshotStatus('Reading the trades from the demo API server…');
                            void read(declared.fields);
                        }}>
                    Read the trades into a new Snapshot
                </button>
                <button id="pivot-db-same-layout" type="button" onClick={() => { setServerLayout(snapshotLayout); }}>
                    Show the Snapshot pivot's layout on the server's too
                </button>
                <button id="pivot-db-reset" type="button" onClick={() => { setSnapshotLayout(firstLayout()); setServerLayout(firstLayout()); }}>
                    Reset both layouts
                </button>
            </div>
            <div className="demo-pivot-pair">
                <section id="pivot-db-snapshot" aria-labelledby="pivot-db-snapshot-title">
                    <h2 id="pivot-db-snapshot-title">Database → Snapshot</h2>
                    <p className="demo-status" id="pivot-db-snapshot-status">{snapshotStatus}</p>
                    {snapshot === undefined ? null : (
                        <ExPivot source={snapshot} layout={snapshotLayout} onLayoutChanged={({ layout }) => { setSnapshotLayout(layout); }}
                                 locale="en-US" viewportHeight={360} />
                    )}
                </section>
                <section id="pivot-db-server" aria-labelledby="pivot-db-server-title">
                    <h2 id="pivot-db-server-title">Database → server Pivot Source</h2>
                    <p className="demo-status" id="pivot-db-server-status">
                        {server !== undefined
                            ? `Asked of the server so far: ${times('aggregate')} aggregate, ${times('items')} items and ${times('details')} details questions, each one POST.`
                            : fieldsFailure !== undefined ? fieldsFailure : 'Asking the demo API server for its fields…'}
                    </p>
                    {server === undefined ? null : (
                        <ExPivot source={server} layout={serverLayout} onLayoutChanged={({ layout }) => { setServerLayout(layout); }}
                                 locale="en-US" viewportHeight={360} />
                    )}
                </section>
            </div>
        </>
    );
}
