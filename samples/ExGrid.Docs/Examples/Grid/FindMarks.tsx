import { useState, type ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import { ExGrid } from '@ex-grid/react';
import { columns, match, source } from './FindMarksParts';

export default function FindMarks(): ReactElement {
    const [marked, setMarked] = useState(true);
    return (
        <>
            <label style={{ display: 'inline-flex', gap: 6, alignItems: 'center', marginBottom: 12, fontSize: 14 }}>
                <input type="checkbox" checked={marked} onChange={event => { setMarked(event.target.checked); }} />
                Pass the match function (press Ctrl+F in the grid and type "novak")
            </label>
            <ExGrid source={source} columns={columns} {...(marked ? { findMatch: match } : {})} temporal={Temporal} viewportHeight={320} viewportWidth="stretch" />
        </>
    );
}
