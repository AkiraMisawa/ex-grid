import { useMemo, type ReactElement } from 'react';
import { GridSource, type GridColumn, type GridHeaderGroup } from '@ex-grid/core';
import { ExGrid } from '@ex-grid/react';
import type { Temporal } from 'temporal-polyfill';
import { DemoNav } from '../demo-nav';
import { firstDay, pick, type DemoTrade } from '../demo-data';
import { useQuery } from '../router';

const books = ['Alpha', 'Beta', 'Gamma', 'Delta'] as const;
// The last name is Arabic on purpose: RTL content renders correctly inside the LTR island (ADR-0031).
const traders = ['Ishikawa', 'Novak', 'Osei', 'عمر'] as const;

/** The Features page's 400 trades, as Features.razor makes them; the Notional is the decimal's nearest number. */
const rows: readonly DemoTrade[] = Object.freeze(Array.from({ length: 400 }, (_, i) => Object.freeze({
    id: i,
    book: pick(books, i % books.length),
    trader: pick(traders, Math.floor(i / 3) % traders.length),
    notional: (100_000_000 + (i * 1_234_567)) / 100,
    tradeDate: firstDay.add({ days: i % 200 }),
    confirmed: i % 3 !== 0,
})));

const isoDate = (value: unknown): string => (value as Temporal.PlainDateTime).toPlainDate().toString();
// The Blazor page's Notional is a decimal of two places, which C# writes with both; a number keeps no
// places, so they are written here.
const twoPlaces = (value: unknown): string => (value as number).toFixed(2);

const columns: readonly GridColumn<DemoTrade>[] = Object.freeze([
    { name: 'Book', type: 'text', value: (t: DemoTrade) => t.book, width: 110 },
    { name: 'Trader', type: 'text', value: (t: DemoTrade) => t.trader, width: 120 },
    { name: 'Notional', type: 'number', value: (t: DemoTrade) => t.notional, width: 140, format: twoPlaces },
    // Deliberately too narrow for its values: what #### looks like, and what the clipboard must never
    // receive (ADR-0016, CP-5). The label is the full word, so the header's own ellipsis is the real thing.
    { name: 'Narrow', type: 'number', value: (t: DemoTrade) => t.notional, width: 48, format: twoPlaces },
    // ISO, and the widths sum under the Viewport minus its scrollbar gutter: a horizontal bar here would sit
    // where the browser suite drags into the bottom edge band (SL-14/15).
    { name: 'TradeDate', type: 'date', value: (t: DemoTrade) => t.tradeDate, width: 120, format: isoDate },
    { name: 'Confirmed', type: 'boolean', value: (t: DemoTrade) => t.confirmed, width: 120 },
]);

const groups: readonly GridHeaderGroup[] = Object.freeze([
    { label: 'Who', columns: ['Book', 'Trader'] },
    { label: 'What', columns: ['Notional', 'Narrow', 'TradeDate', 'Confirmed'] },
]);

const secondColumns: readonly GridColumn<DemoTrade>[] = Object.freeze([
    { name: 'Book', type: 'text', value: (t: DemoTrade) => t.book },
    { name: 'Trader', type: 'text', value: (t: DemoTrade) => t.trader },
]);

/**
 * The page /features of the Blazor hosts: an interaction surface on one page. The Blazor page writes every
 * notification to status lines below the heading; the Script Core raises none of those yet (it does not
 * edit, copy, paste, filter, reorder or open a menu), so a line comes here with the notification it shows,
 * and a test that reads one fails by name until then rather than reading a value nothing could change.
 */
export function Features(): ReactElement {
    const showGroups = useQuery('groups') === '1';
    const source = useMemo(() => GridSource.from(rows), []);
    const second = useMemo(() => GridSource.from(rows.slice(0, 20)), []);
    return (
        <>
            <DemoNav route="features" />
            <h1>Features — editing, clipboard, sorting, filtering, Header Groups</h1>
            <p>
                The interaction surface on one page, for the browser layer to drive: a source-bound grid with tiered
                headers. Sorting and the Selection work here now; editing, the clipboard and filtering, and the status
                lines that report them, join as the Script Core comes to them.
            </p>
            <ExGrid
                columns={columns}
                source={source}
                rowHeight={24}
                headerHeight={28}
                viewportHeight={320}
                viewportWidth={720}
                headerGroups={groups} />
            {showGroups
                ? (
                    <p className="demo-status">
                        <span id="groups-status">
                            Groups: {groups.map(g => `${g.label} [${g.columns.join(',')}] tier ${g.tier ?? 1} span ${g.tierSpan ?? 1}`).join('; ')}
                        </span>
                    </p>
                )
                : null}
            <h2>A second, independent instance</h2>
            <p>Two grids on one page hold independent DOM, tokens and handles (ADR-0018).</p>
            <ExGrid columns={secondColumns} source={second} rowHeight={20} viewportHeight={140} viewportWidth={480} />
        </>
    );
}
