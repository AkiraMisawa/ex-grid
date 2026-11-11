import { useCallback, useLayoutEffect, useMemo, useRef, useState, useSyncExternalStore, type ReactElement } from 'react';
import {
    GridSource, type GridColumn, type GridColumnWidthChange, type GridEditIntent, type GridFilter, type GridHeaderDisplay, type GridSelection,
    type InMemoryGridSource,
} from '@ex-grid/core';
import { ExGrid } from '@ex-grid/react';
import { pick } from '../demo-data';
import { DemoNav } from '../demo-nav';

/** One row of this page: a swap position, most of whose values are far shorter than a header's chrome. */
interface HeaderRow {
    readonly id: number;
    readonly ccy: string;
    readonly tenor: string;
    readonly qty: number;
    readonly live: boolean;
    readonly rate: number;
    readonly pv: number;
    readonly dv01: number;
    readonly counterparty: string;
    readonly notional: number;
}

const currencies = ['USD', 'EUR', 'JPY', 'GBP', 'CHF'] as const;
const tenors = ['1Y', '2Y', '5Y', '10Y', '30Y'] as const;
const counterparties = ['Aldgate Securities', 'Bishopsgate Bank', 'Cornhill Capital', 'Dowgate Partners'] as const;

// Held once, never rebuilt per render (ADR-0003).
const rows: readonly HeaderRow[] = Object.freeze(Array.from({ length: 200 }, (_, i): HeaderRow => Object.freeze({
    id: i + 1,
    ccy: pick(currencies, i % currencies.length),
    tenor: pick(tenors, Math.floor(i / 2) % tenors.length),
    qty: (i % 9) + 1,
    live: i % 4 !== 0,
    rate: 1.25 + ((i * 37) % 300) / 100,
    pv: (((i * 7919) % 20_000) - 10_000) / 10,
    dv01: ((i * 31) % 900) / 10,
    counterparty: pick(counterparties, i % counterparties.length),
    notional: 1_000_000 * ((i % 25) + 1),
})));

const twoPlaces = (value: unknown): string => (value as number).toFixed(2);
const onePlace = (value: unknown): string => (value as number).toFixed(1);
const whole = (value: unknown): string => (value as number).toLocaleString('en-US');

/** The columns, each at the width `widthOf` gives it: no width is Auto. Short values under short labels, where the
 * header decides the width, and one label far longer than its values. */
function columnsAt(widthOf: (name: string) => number | undefined): readonly GridColumn<HeaderRow>[] {
    const at = (name: string): { readonly width?: number } => {
        const width = widthOf(name);
        return width === undefined ? {} : { width };
    };
    return Object.freeze([
        { name: 'Id', header: 'ID', type: 'number', value: (r: HeaderRow) => r.id, ...at('Id') },
        { name: 'Ccy', header: 'CCY', type: 'text', value: (r: HeaderRow) => r.ccy, filterUi: 'both', ...at('Ccy') },
        { name: 'Tenor', type: 'text', value: (r: HeaderRow) => r.tenor, filterUi: 'both', ...at('Tenor') },
        { name: 'Qty', type: 'number', value: (r: HeaderRow) => r.qty, editable: true, ...at('Qty') },
        { name: 'Live', type: 'boolean', value: (r: HeaderRow) => r.live, ...at('Live') },
        { name: 'Rate', type: 'number', value: (r: HeaderRow) => r.rate, format: twoPlaces, ...at('Rate') },
        { name: 'Pv', header: 'PV', type: 'number', value: (r: HeaderRow) => r.pv, format: onePlace, ...at('Pv') },
        { name: 'Dv01', header: 'DV01', type: 'number', value: (r: HeaderRow) => r.dv01, format: onePlace, ...at('Dv01') },
        { name: 'Counterparty', type: 'text', value: (r: HeaderRow) => r.counterparty, filterUi: 'both', ...at('Counterparty') },
        { name: 'Notional', header: 'Notional (USD equivalent)', type: 'number', value: (r: HeaderRow) => r.notional, format: whole, ...at('Notional') },
    ] satisfies readonly GridColumn<HeaderRow>[]);
}

/** The widths the first grid opens at: each what a standard header and the values need, rounded up to a whole
 * pixel, so the same columns under the same widths are read in both displays. */
const openingWidths: Readonly<Record<string, number>> = Object.freeze({
    Id: 92, Ccy: 102, Tenor: 121, Qty: 102, Live: 111, Rate: 111, Pv: 92, Dv01: 111, Counterparty: 189, Notional: 303,
});

const autoColumns = columnsAt(() => undefined);

/** What a grid's header cells measure, read from the page as a Consumer's own script may read its own page: each
 * column's painted width by its label, and their sum. */
function useHeaderWidths(deps: readonly unknown[]): { readonly ref: React.RefObject<HTMLDivElement | null>; readonly widths: string; readonly total: string } {
    const ref = useRef<HTMLDivElement | null>(null);
    const [read, setRead] = useState({ widths: '', total: '' });
    useLayoutEffect(() => {
        let frame = 0;
        const measure = (): void => {
            const cells = [...(ref.current?.querySelectorAll<HTMLElement>('.ex-header-cell') ?? [])]; // oxlint-disable-line ex-grid/no-stand-in-default -- an unmounted page has no header to read
            const sizes = cells.map(cell => ({ label: cell.firstChild?.textContent ?? '', width: cell.getBoundingClientRect().width })); // oxlint-disable-line ex-grid/no-stand-in-default -- a header with no label reads as none
            const widths = sizes.map(({ label, width }) => `${label}=${Number(width.toFixed(2))}`).join(';');
            const total = String(Number(sizes.reduce((sum, { width }) => sum + width, 0).toFixed(2)));
            setRead(was => (was.widths === widths && was.total === total ? was : { widths, total }));
        };
        // After the grid's own paint, which follows this render.
        frame = requestAnimationFrame(() => { frame = requestAnimationFrame(measure); });
        return () => { cancelAnimationFrame(frame); };
    }, deps); // oxlint-disable-line react-hooks/exhaustive-deps -- the caller says what moves the widths
    return { ref, ...read };
}

/** A source's Sort and the columns its Filter holds, as a status line says them. */
function useQueryStatus(source: InMemoryGridSource<HeaderRow>): { readonly sorts: string; readonly filtered: string } {
    const subscribe = useMemo(() => source.subscribe.bind(source), [source]);
    const sorts = useSyncExternalStore(subscribe, () => source.snapshot().sorts);
    const filter = useSyncExternalStore(subscribe, () => source.snapshot().filter);
    return {
        sorts: sorts.length === 0 ? 'none' : sorts.map(s => `${s.column} ${s.direction}`).join(','),
        filtered: filter === undefined || Object.keys(filter.columns).length === 0 ? 'none' : Object.keys(filter.columns).join(','),
    };
}

const displays: readonly GridHeaderDisplay[] = Object.freeze(['standard', 'compact']);

/** The filter the page sets from a button of its own, where the grid shows no column menu to set one from. */
const usdOnly: GridFilter = { columns: { Ccy: { clauses: [{ operator: 'equals', value: 'USD' }] } } };

/**
 * The page /header-display: the header's two displays over the same rows (ADR-0500). The first grid keeps its
 * declared widths while its display changes; the two below declare every width Auto, one in each display, so what
 * each header needs is read from the columns themselves. The last hides the column menu and is filtered from the
 * page's own buttons, so its header has to take a box for the funnel (ADR-0502).
 */
export function HeaderDisplay(): ReactElement {
    const [display, setDisplay] = useState<GridHeaderDisplay>('standard');
    const [widthsHeld, setWidthsHeld] = useState(openingWidths);
    const [order, setOrder] = useState<readonly string[]>(() => Object.keys(openingWidths));
    const [pinned, setPinned] = useState(1);
    const [changes, setChanges] = useState('');
    const [selection, setSelection] = useState('');
    const [editStatus, setEditStatus] = useState('none');
    const source = useMemo(() => GridSource.from(rows), []);
    const standardSource = useMemo(() => GridSource.from(rows), []);
    const compactSource = useMemo(() => GridSource.from(rows), []);
    const menulessSource = useMemo(() => GridSource.from(rows), []);
    const columns = useMemo(() => {
        const declared = columnsAt(name => widthsHeld[name]);
        return Object.freeze(order.map(name => {
            const column = declared.find(candidate => candidate.name === name);
            if (column === undefined) throw new Error(`The page holds no column named ${name}.`);
            return column;
        }));
    }, [widthsHeld, order]);
    const status = useQueryStatus(source);
    const menulessStatus = useQueryStatus(menulessSource);
    const onWidth = useCallback(({ column, widthPx }: GridColumnWidthChange) => {
        setChanges(before => {
            const entry = `${column}=${Number(widthPx.toFixed(3))}`;
            return before === '' ? entry : `${before};${entry}`;
        });
        setWidthsHeld(before => ({ ...before, [column]: widthPx }));
    }, []);
    const onSelection = useCallback((changed: GridSelection) => {
        setSelection(changed.ranges.map(r => `${r.topRow},${r.leftColumn},${r.rowCount},${r.columnCount}`).join(';'));
    }, []);
    const onEdit = useCallback((intent: GridEditIntent<HeaderRow>) => {
        const qty = Number(intent.value);
        if (intent.column !== 'Qty' || !Number.isFinite(qty)) {
            setEditStatus(`${intent.column}=${intent.value} (not applied)`);
            return;
        }
        source.replaceRow(intent.row, Object.freeze({ ...intent.row, qty }));
        setEditStatus(`Qty=${qty} on ${intent.row.id}`);
    }, [source]);
    const held = useHeaderWidths([display, widthsHeld, order, pinned]);
    const standard = useHeaderWidths([]);
    const compact = useHeaderWidths([]);
    const menuless = useHeaderWidths([menulessStatus.filtered]);
    return (
        <>
            <DemoNav route="header-display" />
            <h1>Header display — standard and compact</h1>
            <p>
                The same rows under the header&apos;s two displays (ADR-0500). A standard header gives the ▾ a band of its own
                and the sort mark a box of its own; a compact header gives both one box inside the cell&apos;s right padding,
                which shows the ▾ while the column holds nothing and the mark of its Sort or filter while it holds one.
                Under either, the ▾ of a filtered column is a funnel (ADR-0501).
                Click a header to sort, press its ▾ or Alt+↓ for the menu and the filter, and drag or double-click a
                column&apos;s right edge.
            </p>
            <h2>The same widths, either display</h2>
            <p>
                Every width here is declared, as a dragged or a saved width is. Changing the display changes no width: the
                room a compact header gives back goes to its label. Size a column to fit to take the room back.
            </p>
            <p className="demo-status">
                {displays.map(name => (
                    <button key={name} id={`header-display-${name}`} type="button" aria-pressed={display === name}
                        onClick={() => { setDisplay(name); }}>{name === 'standard' ? 'Standard' : 'Compact'}</button>
                ))}{' '}
                <button id="header-display-reset" type="button" onClick={() => { setWidthsHeld(openingWidths); }}>Reset widths</button>{' '}
                <span id="header-display-status">Display: {display}</span>{' '}
                <span id="header-total-status">Total: {held.total}</span>
            </p>
            <p className="demo-status">
                <span id="header-widths-status">Widths: {held.widths}</span>
            </p>
            <p className="demo-status">
                <span id="width-status">Changes: {changes}</span>{' '}
                <span id="sort-status">Sorts: {status.sorts}</span>{' '}
                <span id="filter-status">Filtered: {status.filtered}</span>{' '}
                <span id="selection-status">Selection: {selection}</span>{' '}
                <span id="order-status">Order: {order.join(',')}</span>{' '}
                <span id="pinned-status">Pinned: {pinned}</span>{' '}
                <span id="edit-status">Edit: {editStatus}</span>
            </p>
            <div id="held-grid" ref={held.ref}>
                <ExGrid columns={columns} source={source} headerDisplay={display} viewportHeight={260} viewportWidth="stretch"
                    pinnedColumnCount={pinned} onColumnWidthChanged={onWidth} onColumnOrderChanged={setOrder}
                    onPinnedCountChanged={setPinned} onSelectionChanged={onSelection} onEdit={onEdit} />
            </div>
            <h2>Auto widths, one grid in each display</h2>
            <p>
                No width is declared below, so each column is as wide as its header and the values painted need. Where the
                header is what decides, the compact grid&apos;s column is narrower by what its header no longer keeps aside.
            </p>
            <p className="demo-status">
                <span id="standard-total-status">Standard total: {standard.total}</span>{' '}
                <span id="standard-widths-status">Widths: {standard.widths}</span>
            </p>
            <div id="standard-grid" ref={standard.ref}>
                <ExGrid columns={autoColumns} source={standardSource} headerDisplay="standard" viewportHeight={200} viewportWidth="stretch" />
            </div>
            <p className="demo-status">
                <span id="compact-total-status">Compact total: {compact.total}</span>{' '}
                <span id="compact-widths-status">Widths: {compact.widths}</span>
            </p>
            <div id="compact-grid" ref={compact.ref}>
                <ExGrid columns={autoColumns} source={compactSource} headerDisplay="compact" viewportHeight={200} viewportWidth="stretch" />
            </div>
            <h2>No column menu, filtered from the page</h2>
            <p>
                This grid hides the column menu, so a standard header has no ▾ and keeps no room for one. The buttons
                below set a filter from the page. While a column is filtered its header takes the ▾&apos;s band for a
                funnel, a mark with nothing to press, and an Auto column its header decides is that band wider until
                the filter is cleared (ADR-0502).
            </p>
            <p className="demo-status">
                <button id="menuless-filter" type="button" onClick={() => { menulessSource.setFilter(usdOnly); }}>Filter CCY to USD</button>{' '}
                <button id="menuless-clear" type="button" onClick={() => { menulessSource.setFilter(undefined); }}>Clear the filter</button>{' '}
                <span id="menuless-sort-status">Sorts: {menulessStatus.sorts}</span>{' '}
                <span id="menuless-filter-status">Filtered: {menulessStatus.filtered}</span>{' '}
                <span id="menuless-widths-status">Widths: {menuless.widths}</span>
            </p>
            <div id="menuless-grid" ref={menuless.ref}>
                <ExGrid columns={autoColumns} source={menulessSource} hideColumnMenu viewportHeight={200} viewportWidth="stretch" />
            </div>
        </>
    );
}
