import { useMemo, useSyncExternalStore, type ReactElement } from 'react';
import { GridSource, type FilterClause, type GridColumn, type GridFilter } from '@ex-grid/core';
import { ExGrid } from '@ex-grid/react';
import { DemoNav } from '../demo-nav';
import { pick } from '../demo-data';

/** A trade of this page, as FilterPanel.razor makes them. */
interface FilterTrade {
    readonly id: number;
    readonly book: string;
    readonly trader: string | null;
    readonly notional: number;
}

// Past the reference source's cap of distinct values, so Id's value list is TooMany (ADR-0009).
const rowCount = 1200;
const books = ['Alpha', 'Beta', 'Gamma', 'Delta'] as const;
const traders = ['Ishikawa', 'Novak', 'Osei'] as const;

const rows: readonly FilterTrade[] = Object.freeze(Array.from({ length: rowCount }, (_, i) => Object.freeze({
    id: i,
    book: pick(books, i % books.length),
    // A Blank is no value at all, which the value list offers as its own entry (ADR-0023).
    trader: i % 7 === 6 ? null : pick(traders, i % traders.length),
    notional: 1_000_000 + (i * 1000),
})));

const columns: readonly GridColumn<FilterTrade>[] = Object.freeze([
    { name: 'Book', type: 'text', value: (t: FilterTrade) => t.book, width: 110, filterUi: 'both' },
    { name: 'Trader', type: 'text', value: (t: FilterTrade) => t.trader, width: 120, filterUi: 'both' },
    { name: 'Id', type: 'number', value: (t: FilterTrade) => t.id, width: 100, filterUi: 'both' },
    { name: 'Notional', type: 'number', value: (t: FilterTrade) => t.notional, width: 160 },
]);

// C#'s names for the operators and the combinator, so the line reads as the Blazor page writes it.
const pascal = (name: string): string => name.charAt(0).toUpperCase() + name.slice(1);
const valueText = (value: unknown): string => (value === null || value === undefined ? '(blank)' : String(value));

function clauseText(clause: FilterClause): string {
    if (clause.values !== undefined) return `${pascal(clause.operator)} (${clause.values.map(valueText).join(', ')})`;
    return clause.value === undefined || clause.value === null ? pascal(clause.operator) : `${pascal(clause.operator)} ${valueText(clause.value)}`;
}

/** The Filter as one line, columns by name and values in their order, so two pages' lines are equal exactly
 * when their Filters are. */
function describe(filter: GridFilter | undefined): string {
    const entries = filter === undefined ? [] : Object.entries(filter.columns).toSorted(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0));
    if (entries.length === 0) return 'No filter.';
    return entries.map(([name, spec]) => `${name}: ${pascal(spec.combinator ?? 'and')} [${spec.clauses.map(clauseText).join(', ')}]`).join('; ');
}

/** The page /filter-panel of the Blazor hosts, under the built-in Chrome: every case of the filter panel a
 * column can ask for, and the Filter the source holds written out below the grid (WR-1, ADR-0009/0023/0044). */
export function FilterPanel(): ReactElement {
    const source = useMemo(() => GridSource.from(rows), []);
    const subscribe = useMemo(() => source.subscribe.bind(source), [source]);
    const snapshot = useMemo(() => source.snapshot.bind(source), [source]);
    const state = useSyncExternalStore(subscribe, snapshot);
    return (
        <>
            <DemoNav route="filter-panel" />
            <h1>Filter panel — the same choices through either Chrome</h1>
            <p>
                Book and Trader offer their values and a condition; one trade in seven has no trader, so Trader&apos;s list
                ends with its Blank entry. Id has more distinct values than the source will list, so its panel answers with
                the condition form alone (TooMany). Notional asks for the condition form only. Below the grid is the Filter
                the source holds, as it was applied.
            </p>
            <ExGrid columns={columns} source={source} rowHeight={24} viewportHeight={300} viewportWidth={620} />
            <p className="demo-status">
                <span id="filter-spec">{describe(state.filter)}</span>
            </p>
        </>
    );
}
