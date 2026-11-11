import { useState, type ReactElement } from 'react';
import { GridSource, type GridColumn, type GridFilter, type GridHeaderDisplay } from '@ex-grid/core';
import { ExGrid } from '@ex-grid/react';
import { samplePositions, type Position } from './Positions';

const source = GridSource.from(samplePositions());
const whole = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
const money = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

// No width is declared, so every column is Auto and is measured under the display its header has.
const columns: readonly GridColumn<Position>[] = [
    { name: 'Id', type: 'number', value: p => p.id, header: 'ID' },
    { name: 'Currency', type: 'text', value: p => p.currency, header: 'Ccy', filterUi: 'both' },
    { name: 'Quantity', type: 'number', value: p => p.quantity, header: 'Qty', format: v => whole.format(v as number) },
    { name: 'Hedged', type: 'boolean', value: p => p.hedged },
    { name: 'Price', type: 'number', value: p => p.price, format: v => money.format(v as number) },
    { name: 'Pnl', type: 'number', value: p => p.pnl, header: 'P&L', format: v => money.format(v as number) },
    { name: 'Desk', type: 'text', value: p => p.desk, filterUi: 'both' },
    { name: 'Instrument', type: 'text', value: p => p.instrument },
];

const displays: readonly GridHeaderDisplay[] = ['standard', 'compact'];

// A filter from the page's own controls: the only way to filter a grid that hides its column menu. The header of a
// filtered column shows the funnel either way; without the ▾ a standard header takes a box for it meanwhile.
const usdOnly: GridFilter = { columns: { Currency: { clauses: [{ operator: 'equals', value: 'USD' }] } } };
const onlyUsd = (): void => { source.setFilter(usdOnly); };
const clearFilter = (): void => { source.setFilter(undefined); };

export default function HeaderDisplayGrid(): ReactElement {
    const [display, setDisplay] = useState<GridHeaderDisplay>('compact');
    const [hideColumnMenu, setHideColumnMenu] = useState(false);
    return (
        <>
            <div className="bar">
                <label>
                    Header display{' '}
                    <select value={display} onChange={event => { setDisplay(event.target.value as GridHeaderDisplay); }}>
                        {displays.map(name => <option key={name} value={name}>{name}</option>)}
                    </select>
                </label>
                <label>
                    <input type="checkbox" checked={hideColumnMenu} onChange={event => { setHideColumnMenu(event.target.checked); }} />
                    {' '}Hide the column menu
                </label>
                <button type="button" onClick={onlyUsd}>Filter Ccy to USD</button>
                <button type="button" onClick={clearFilter}>Clear the filter</button>
            </div>
            <ExGrid source={source} columns={columns} headerDisplay={display} hideColumnMenu={hideColumnMenu} viewportHeight={300}
                viewportWidth="stretch" />
        </>
    );
}
