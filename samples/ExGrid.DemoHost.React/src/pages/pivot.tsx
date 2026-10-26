import { useMemo, useRef, useState, type ReactElement } from 'react';
import { GridSource, type GridColumn } from '@ex-grid/core';
import { decimalText, type SnapshotDecimal } from '@ex-grid/data';
import {
    RecordPivotSource, japanesePivotWords, readPivotLayoutJson, writePivotLayoutJson, type PivotLayout, type PivotSource,
} from '@ex-grid/pivot-engine';
import type { PivotShowDetailsEvent } from '@ex-grid/pivot';
import { ExGrid } from '@ex-grid/react';
import { ExPivot } from '@ex-grid/pivot-react';
import { Temporal } from 'temporal-polyfill';
import { DemoNav } from '../demo-nav';
import { demoPivotFields, demoPivotFirstLayout, demoPivotTradeCount, demoPivotTrades, type DemoPivotTrade } from '../pivot-data';
import { useQuery } from '../router';

const count = (value: number): string => value.toLocaleString('en-US');

function amountText(value: unknown): string {
    if (typeof value !== 'object' || value === null || !('mantissa' in value)) throw new TypeError('An amount is a decimal.');
    return decimalText(value as SnapshotDecimal);
}

// The details grid of ?details=page, as the Blazor page's DetailColumns. Held once: a new array would paint every row
// again (ADR-0003). The amounts are exact decimals, which a Number column does not order or filter: they are shown,
// and not queried.
const detailColumns: readonly GridColumn<DemoPivotTrade>[] = Object.freeze([
    { name: 'Id', type: 'text', value: (t: DemoPivotTrade) => t.id },
    { name: 'Region', type: 'text', value: (t: DemoPivotTrade) => t.region },
    { name: 'Desk', type: 'text', value: (t: DemoPivotTrade) => t.desk },
    { name: 'Book', type: 'text', value: (t: DemoPivotTrade) => t.book },
    { name: 'Product', type: 'text', value: (t: DemoPivotTrade) => t.product },
    { name: 'Currency', type: 'text', value: (t: DemoPivotTrade) => t.currency },
    { name: 'TradeDate', type: 'date', dateType: 'dateOnly', value: (t: DemoPivotTrade) => t.tradeDate, header: 'Trade date' },
    { name: 'Notional', type: 'number', queryable: false, value: (t: DemoPivotTrade) => t.notional, format: amountText },
    { name: 'Pnl', type: 'number', queryable: false, value: (t: DemoPivotTrade) => t.pnl, header: 'P&L', format: amountText },
]);

/** ?first=held: the source's answers wait for the page's button, so the pane stands with no report drawn (ADR-0360). */
function heldFirst(data: PivotSource, held: Promise<void>): PivotSource {
    return Object.freeze({
        fields: data.fields,
        features: data.features,
        aggregate: async (query, signal) => { await held; return data.aggregate(query, signal); },
        items: (query, signal) => data.items(query, signal),
        details: (query, signal) => data.details(query, signal),
    } satisfies PivotSource);
}

interface Details {
    readonly caption: string;
    readonly trades: readonly DemoPivotTrade[];
}

/** The page /pivot of the Blazor hosts, as far as ExPivot's Script Core draws it so far: the trades pivoted
 * by the bundled source, the Field List with its drag and drop, collapsing by its button or a double-click, saving the
 * layout and Excel's Japanese words (ADR-0059, ADR-0061, ADR-0370 section 3). ?details=dialog opens Show Details in a
 * dialog, and ?details=page hands the records to the page (ADR-0063); ?second=1 adds a second pivot over the same
 * trades (ADR-0061); ?first=held holds the source's answers until "Answer the first report" is pressed (ADR-0360). */
export function Pivot(): ReactElement {
    const asked = useQuery('trades');
    const tradeCount = asked === undefined ? demoPivotTradeCount : Number(asked);
    const japaneseAsked = useQuery('words') === 'ja';
    const detailsAsked = useQuery('details');
    const secondOn = useQuery('second') === '1';
    const firstHeld = useQuery('first') === 'held';
    // Made once, with the page: the button releases it, and the report is asked again by nothing else.
    const [gate] = useState(() => {
        if (!firstHeld) return undefined;
        let release: (() => void) | undefined;
        const held = new Promise<void>(resolve => { release = resolve; });
        if (release === undefined) throw new Error('Internal: a promise runs its executor at once.');
        return { held, release };
    });
    const [answered, setAnswered] = useState(false);
    const { source, read } = useMemo(() => {
        const begun = performance.now();
        const trades = demoPivotTrades(tradeCount);
        const made: PivotSource = new RecordPivotSource(trades, demoPivotFields);
        return {
            source: gate === undefined ? made : heldFirst(made, gate.held),
            read: `${count(trades.length)} trades made in ${count(Math.round(performance.now() - begun))} ms.`,
        };
    }, [tradeCount, gate]);
    const [layout, setLayout] = useState<PivotLayout>(demoPivotFirstLayout);
    const [changes, setChanges] = useState(0);
    const [saved, setSaved] = useState<string | undefined>(undefined);
    const [japanese, setJapanese] = useState(japaneseAsked);
    const [showFieldList, setShowFieldList] = useState(true);
    const [secondLayout, setSecondLayout] = useState<PivotLayout>(demoPivotFirstLayout);
    const [secondChanges, setSecondChanges] = useState(0);
    const [details, setDetails] = useState<Details>({ caption: 'Double-click a value in the report.', trades: [] });
    const detailsSource = useMemo(() => GridSource.from(details.trades), [details]);
    const detailsAsks = useRef(0);
    // Listening to Show Details is what takes the records from the tab and the dialog. The page takes every record
    // behind the cell, under the report's Source Version, and its own trade objects with them; an answer to a cell
    // asked before the newest is not shown (spine rule 6). Made once: a new function would reach the pivot as a change.
    const onShowDetails = useMemo(() => {
        if (detailsAsked !== 'page') return undefined;
        const show = async (event: PivotShowDetailsEvent): Promise<void> => {
            const asked = ++detailsAsks.current;
            const page = await event.records(0, Number.MAX_SAFE_INTEGER);
            if (asked !== detailsAsks.current) return;
            if (page.refusal !== undefined) {
                setDetails({ caption: page.refusal.message, trades: [] });
                return;
            }
            const trades = page.records.map(record => record.record as DemoPivotTrade);
            const where = [...event.rowItems, ...event.columnItems].map(item => `${item.field} = ${item.item}`).join(', ');
            setDetails({
                caption: `${count(page.total)} trades behind ${event.valueField ?? 'the cell'}${where.length > 0 ? ` where ${where}.` : '.'}`,
                trades,
            });
        };
        return (event: PivotShowDetailsEvent): void => { void show(event); };
    }, [detailsAsked]);
    const words = japanese ? { label: japanesePivotWords } : {};
    return (
        <>
            <DemoNav route="pivot" />
            <h1>Pivot — ExPivot drawn by ExGrid</h1>
            <p>
                <strong>The use case:</strong> records the application already holds in memory, pivoted as Excel's
                PivotTable pivots them (ADR-0059). Here, the Blazor hosts' 2,000 invented trades, from the same seed.
                Month is not a property of the trade: it is declared as the month of the trade date, labelled Jan to
                Dec and ordered by the calendar (ADR-0060). <code>RecordPivotSource</code> reads the trades into a
                Snapshot on the first question and answers every question from it (ADR-0066).
            </p>
            <p><code>?trades=1000000</code> pivots a million of the trades instead, or any other count.</p>
            <p>
                Tick fields in the PivotTable Fields pane, or drag them between Filters, Columns, Rows and Values;
                drop one back on the list to remove it, or press a placed field to open its menu. Layout ▾ chooses the
                subtotals, the grand totals and the report's form. The report is one ExGrid: select and copy, and press
                the button beside an outer Item, or double-click its label, to collapse or expand it. Double-click a
                value, or use the Context Menu's Show Details, to see the trades behind it (ADR-0063).
            </p>
            <div className="demo-toolbar">
                <button id="pivot-reset" type="button" onClick={() => { setLayout(demoPivotFirstLayout()); }}>Reset the layout</button>
                <button id="pivot-save" type="button" onClick={() => { setSaved(writePivotLayoutJson(layout)); }}>Save the layout</button>
                <button id="pivot-restore" type="button" disabled={saved === undefined}
                        onClick={() => { if (saved !== undefined) setLayout(readPivotLayoutJson(saved)); }}>
                    Restore the saved layout
                </button>
                {gate === undefined || answered ? null : (
                    <button id="pivot-answer-first" type="button" onClick={() => { gate.release(); setAnswered(true); }}>
                        Answer the first report
                    </button>
                )}
                <button id="pivot-words" type="button" onClick={() => { setJapanese(!japanese); }}>
                    {japanese ? 'English words' : '日本語 (Excel\'s Japanese words)'}
                </button>
            </div>
            <p className="demo-status">
                <span id="pivot-read">{read}</span>{' '}
                <span id="pivot-status">
                    {changes} changes made in the pane; {saved === undefined ? 'no layout saved.' : `the saved layout is ${count(saved.length)} characters of JSON.`}
                </span>{' '}
                <span id="pivot-field-list-status">Field List shown: {showFieldList ? 'True' : 'False'}</span>
            </p>
            <ExPivot
                source={source}
                layout={layout}
                onLayoutChanged={({ layout: next }) => { setLayout(next); setChanges(made => made + 1); }}
                showFieldList={showFieldList}
                onShowFieldListChanged={({ shown }) => { setShowFieldList(shown); }}
                {...(onShowDetails === undefined ? {} : { onShowDetails })}
                detailsView={detailsAsked === 'dialog' ? 'dialog' : 'tab'}
                {...words}
                locale="en-US"
                viewportHeight={460}
            />
            {secondOn ? (
                <>
                    <h2>A second pivot</h2>
                    <p>
                        The same trades, pivoted again with a layout of its own. The two are independent: a field
                        dragged from one pivot and dropped on the other changes neither (ADR-0061).
                    </p>
                    <p className="demo-status"><span id="pivot-second-status">{secondChanges} changes made in the second pane</span></p>
                    <div id="pivot-second">
                        <ExPivot
                            source={source}
                            layout={secondLayout}
                            onLayoutChanged={({ layout: next }) => { setSecondLayout(next); setSecondChanges(made => made + 1); }}
                            {...words}
                            locale="en-US"
                            viewportHeight={460}
                        />
                    </div>
                </>
            ) : null}
            {detailsAsked === 'page' ? (
                <>
                    <h2>Details</h2>
                    <p id="pivot-details-caption">{details.caption}</p>
                    <div id="pivot-details">
                        <ExGrid columns={detailColumns} source={detailsSource} rowHeight={28} viewportHeight={240} viewportWidth={900}
                                temporal={Temporal} />
                    </div>
                </>
            ) : null}
        </>
    );
}
