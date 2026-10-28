import { findMatches, findStep, GridSource, textOf, type GridColumn, type GridFindPattern } from '@ex-grid/core';
import { sampleTrades, type Trade } from './Trades';

const notional = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });

export const columns: readonly GridColumn<Trade>[] = [
    { name: 'Id', type: 'number', value: t => t.id },
    { name: 'Book', type: 'text', value: t => t.book },
    { name: 'Trader', type: 'text', value: t => t.trader },
    { name: 'Instrument', type: 'text', value: t => t.instrument },
    { name: 'Notional', type: 'number', value: t => t.notional, format: v => notional.format(v as number) },
    { name: 'TradeDate', type: 'date', value: t => t.tradeDate, header: 'Trade date', dateType: 'dateOnly' },
];

const held = GridSource.from(sampleTrades(2_000));
const byName = new Map(columns.map(column => [column.name, column]));

/** A source of the page's own over the trades it holds, with its own answer to a Find step: here the reference step
 * over the rows in their current order, by the columns' displayed text. A source that is not `GridSource.from`'s gets
 * no marks unless it passes the rule it finds by. */
export const source: GridSource<Trade> = {
    subscribe: listener => held.subscribe(listener),
    snapshot: () => held.snapshot(),
    setColumns: given => { held.setColumns(given); },
    setSorts: sorts => { held.setSorts(sorts); },
    setFilter: filter => { held.setFilter(filter); },
    needRange: range => held.needRange(range),
    rows: (range, signal) => held.rows(range, signal),
    distinctValues: (column, signal) => held.distinctValues(column, signal),
    find: request => Promise.resolve(findStep(held.snapshot().window, request, name => {
        const column = byName.get(name);
        return column === undefined ? undefined : (row: Trade) => textOf(column, row);
    })),
};

/** The rule the answer finds by, judged over the painted cells. Made once: its identity is the change signal. */
export const match = (row: Trade, column: GridColumn<Trade>, pattern: GridFindPattern): boolean => findMatches(textOf(column, row), pattern);
