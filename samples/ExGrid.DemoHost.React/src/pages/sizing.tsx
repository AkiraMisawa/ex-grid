import { useCallback, useMemo, useState, useSyncExternalStore, type ReactElement } from 'react';
import {
    GridSource, type GridColumn, type GridColumnWidthChange, type GridSelection, type GridSourceState, type RowKind, type SortSpec,
} from '@ex-grid/core';
import { ExGrid } from '@ex-grid/react';
import { pick } from '../demo-data';
import { DemoNav } from '../demo-nav';

/** One row of this page. The Row Kind is stored on the row: the last row is a total, painted bold, which is where
 * the estimate's digit width is tightest (ADR-0016). */
interface SizingRow {
    readonly book: string;
    readonly trader: string;
    readonly notional: number;
    readonly note: string;
    readonly kind: RowKind;
}

/** Where the one long note sits: far below the rows the 300px Viewport paints, so a fit that measured only the
 * painted rows would miss it (FN-12a). */
const longNoteRow = 60;

const books = ['Alpha', 'Beta', 'Gamma', 'Delta'] as const;
const traders = ['Ishikawa', 'Novak', 'Osei', 'Varga'] as const;

// Sizing.razor's rows, its decimals in cents so each value is the one C# holds.
const rows: readonly SizingRow[] = Object.freeze([
    ...Array.from({ length: 119 }, (_, i): SizingRow => ({
        book: pick(books, i % books.length),
        trader: pick(traders, Math.floor(i / 3) % traders.length),
        notional: (100_000_000 + (i * 1_234_567)) / 100,
        note: i === longNoteRow ? 'A note far longer than any other in this column' : 'ok',
        kind: 'detail',
    })),
    // A twelve-digit total, painted bold: its estimate must not fall short of what the bold digits paint (FN-12e).
    { book: 'Total', trader: '', notional: 123_456_789_012.5, note: '', kind: 'total' },
]);

const kindOf = (row: SizingRow): RowKind => row.kind;

const initialColumns: readonly GridColumn<SizingRow>[] = Object.freeze([
    { name: 'Book', type: 'text', value: (r: SizingRow) => r.book, width: 110 },
    { name: 'Trader', type: 'text', value: (r: SizingRow) => r.trader, width: 120 },
    // Auto, and sorted from the start, so its header carries the indicator whose room the estimate must count
    // (FN-12d).
    { name: 'Notional', type: 'number', value: (r: SizingRow) => r.notional },
    // A Japanese label over short values: the header is the widest thing in its column, and full-width characters
    // are an em each (FN-12d).
    { name: 'Amount', type: 'number', value: (r: SizingRow) => r.notional % 1000, header: '評価額（円）' },
    // Too narrow for its values on purpose: the #### run must fit the cell it stands in (FN-12e).
    { name: 'Narrow', type: 'number', value: (r: SizingRow) => r.notional, width: 60 },
    { name: 'Note', type: 'text', value: (r: SizingRow) => r.note, width: 120 },
]);

/** The Blazor page's way with a Sort: it holds the Sort it is told and leaves its rows in their order, so a header
 * click that sorts leaves the Selection standing, as no order changed (ADR-0011). The specs read the Sort, never
 * the order. Everything else is the rows' own source. */
function holdingSorts<Row>(rowsSource: GridSource<Row>, opening: readonly SortSpec[]): GridSource<Row> {
    let sorts = opening;
    let seen: GridSourceState<Row> | undefined;
    let shown: GridSourceState<Row> | undefined;
    // One entry per subscription, so the same listener subscribed twice is told until each has stopped.
    const listeners = new Set<{ readonly listener: () => void }>();
    const tell = (): void => { for (const { listener } of listeners) listener(); };
    const { find, summarize } = rowsSource;
    return {
        subscribe: (listener) => {
            const entry = { listener };
            listeners.add(entry);
            const stop = rowsSource.subscribe(listener);
            return () => { listeners.delete(entry); stop(); };
        },
        // The same object until the rows' snapshot or the Sort changes.
        snapshot: () => {
            const current = rowsSource.snapshot();
            if (current !== seen || shown?.sorts !== sorts) {
                seen = current;
                shown = { ...current, sorts };
            }
            return shown;
        },
        setColumns: columns => rowsSource.setColumns(columns),
        setSorts: (told) => {
            sorts = Object.freeze([...told]);
            tell();
        },
        setFilter: filter => rowsSource.setFilter(filter),
        needRange: range => rowsSource.needRange(range),
        rows: (range, signal) => rowsSource.rows(range, signal),
        distinctValues: (column, signal) => rowsSource.distinctValues(column, signal),
        ...(find === undefined ? {} : { find: find.bind(rowsSource) }),
        ...(summarize === undefined ? {} : { summarize: summarize.bind(rowsSource) }),
    };
}

const openingSorts: readonly SortSpec[] = Object.freeze([{ column: 'Notional', direction: 'ascending' }]);

/** The page /sizing of the Blazor hosts: column width gestures, and a box that narrows until pinning is suspended
 * (ADR-0016, ADR-0045). */
export function Sizing(): ReactElement {
    const [columns, setColumns] = useState(initialColumns);
    const [widths, setWidths] = useState('');
    const [selection, setSelection] = useState('');
    const [pinned, setPinned] = useState(2);
    const source = useMemo(() => holdingSorts(GridSource.from(rows), openingSorts), []);
    const subscribe = useMemo(() => source.subscribe.bind(source), [source]);
    const sorts = useSyncExternalStore(subscribe, () => source.snapshot().sorts);
    // Its opening sort is the Blazor page's, written capitalised and told from a sort the user made by its identity;
    // a sort it is told of, in lower case.
    const sortStatus = sorts === openingSorts ? 'Notional ascending'
        : sorts.length === 0 ? 'none' : sorts.map(s => `${s.column} ${s.direction}`.toLowerCase()).join(',');
    // Every change is kept: a gesture over several whole columns raises one per column.
    const onWidth = useCallback(({ column, widthPx }: GridColumnWidthChange) => {
        setWidths(before => {
            const entry = `${column}=${Number(widthPx.toFixed(3))}`;
            return before === '' ? entry : `${before};${entry}`;
        });
        setColumns(before => before.map(c => (c.name === column ? { ...c, width: widthPx } : c)));
    }, []);
    const onSelection = useCallback((changed: GridSelection) => {
        setSelection(changed.ranges.map(r => `${r.topRow},${r.leftColumn},${r.rowCount},${r.columnCount}`).join(';'));
    }, []);
    return (
        <>
            <DemoNav route="sizing" />
            <h1>Sizing — column widths and a box that narrows</h1>
            <p>
                What the column width gestures do and what a narrow box does to the grid (ADR-0016, ADR-0045). Double-click
                a column&apos;s right edge to size it to fit; Shift+click a header to select whole columns, then drag one of
                their edges to resize them together. The grid fills the width of the window: narrow the window until the
                two pinned columns would cover it, and pinning is suspended until it widens again.
            </p>
            <p className="demo-status">
                <span id="width-status">Widths: {widths}</span>{' '}
                <span id="sort-status">Sorts: {sortStatus}</span>{' '}
                <span id="selection-status">Selection: {selection}</span>{' '}
                <span id="pinned-status">Pinned: {pinned}</span>
            </p>
            <ExGrid
                columns={columns}
                source={source}
                rowKind={kindOf}
                rowHeight={24}
                viewportHeight={300}
                viewportWidth="stretch"
                pinnedColumnCount={pinned}
                onColumnWidthChanged={onWidth}
                onPinnedCountChanged={setPinned}
                onSelectionChanged={onSelection} />
        </>
    );
}
