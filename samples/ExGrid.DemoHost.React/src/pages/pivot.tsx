import { useMemo, useState, type ReactElement } from 'react';
import {
    RecordPivotSource, japanesePivotWords, readPivotLayoutJson, writePivotLayoutJson, type PivotLayout,
} from '@ex-grid/pivot-engine';
import { ExPivot } from '@ex-grid/pivot-react';
import { DemoNav } from '../demo-nav';
import { demoPivotFields, demoPivotFirstLayout, demoPivotTradeCount, demoPivotTrades } from '../pivot-data';
import { useQuery } from '../router';

const count = (value: number): string => value.toLocaleString('en-US');

/** The page /pivot of the Blazor hosts, as far as ExPivot's Script Core draws it so far: the trades pivoted
 * by the bundled source, the Field List with its drag and drop, collapsing by double-click, saving the
 * layout and Excel's Japanese words (ADR-0059, ADR-0061, ADR-0370 section 3). */
export function Pivot(): ReactElement {
    const asked = useQuery('trades');
    const tradeCount = asked === undefined ? demoPivotTradeCount : Number(asked);
    const japaneseAsked = useQuery('words') === 'ja';
    const { source, read } = useMemo(() => {
        const begun = performance.now();
        const trades = demoPivotTrades(tradeCount);
        const made = new RecordPivotSource(trades, demoPivotFields);
        return { source: made, read: `${count(trades.length)} trades made in ${count(Math.round(performance.now() - begun))} ms.` };
    }, [tradeCount]);
    const [layout, setLayout] = useState<PivotLayout>(demoPivotFirstLayout);
    const [changes, setChanges] = useState(0);
    const [saved, setSaved] = useState<string | undefined>(undefined);
    const [japanese, setJapanese] = useState(japaneseAsked);
    const [showFieldList, setShowFieldList] = useState(true);
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
                drop one back on the list to remove it. The report is one ExGrid: select and copy, and double-click
                an outer Item to collapse or expand it.
            </p>
            <div className="demo-toolbar">
                <button id="pivot-reset" type="button" onClick={() => { setLayout(demoPivotFirstLayout()); }}>Reset the layout</button>
                <button id="pivot-save" type="button" onClick={() => { setSaved(writePivotLayoutJson(layout)); }}>Save the layout</button>
                <button id="pivot-restore" type="button" disabled={saved === undefined}
                        onClick={() => { if (saved !== undefined) setLayout(readPivotLayoutJson(saved)); }}>
                    Restore the saved layout
                </button>
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
                {...(japanese ? { label: japanesePivotWords } : {})}
                locale="en-US"
                viewportHeight={460}
            />
        </>
    );
}
