import { useRef, useState, type ReactElement } from 'react';
import { ExGrid, type ExGridApi } from '@ex-grid/react';
import { columns, currencies, selectWhere, source } from './SelectWhereParts';
import type { Trade } from './Trades';

export default function SelectWhere(): ReactElement {
    const grid = useRef<ExGridApi<Trade>>(null);
    const [status, setStatus] = useState('');
    return (
        <>
            <div className="bar">
                <span>Select where Currency is</span>
                {currencies.map(currency => (
                    <button key={currency} type="button" onClick={() => { setStatus(selectWhere(grid.current, currency)); }}>{currency}</button>
                ))}
                <span>{status}</span>
            </div>
            <ExGrid ref={grid} source={source} columns={columns} highlightHoverRow viewportHeight={300} viewportWidth="stretch" />
        </>
    );
}
