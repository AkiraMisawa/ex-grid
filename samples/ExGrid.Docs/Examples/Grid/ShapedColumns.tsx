import type { ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import { ExGrid } from '@ex-grid/react';
import { useShapedTrades } from './ShapedTrades';

const area = { width: '100%', fontFamily: 'monospace' } as const;

export default function ShapedColumns(): ReactElement {
    const { texts, edit, draw, drawn, result } = useShapedTrades();
    return (
        <>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: 8, fontSize: 13 }}>
                <label>Input<textarea rows={9} style={area} value={texts.input} onChange={event => { edit('input')(event.target.value); }} /></label>
                <label>Data<textarea rows={9} style={area} value={texts.data} onChange={event => { edit('data')(event.target.value); }} /></label>
                <label>Grid<textarea rows={9} style={area} value={texts.spec} onChange={event => { edit('spec')(event.target.value); }} /></label>
            </div>
            <div style={{ margin: '8px 0' }}>
                <button type="button" onClick={draw}>Draw</button>
                <span role="status" style={{ marginLeft: 8 }}>{'refusal' in result ? result.refusal : ''}</span>
            </div>
            {'grid' in result && (
                <ExGrid key={drawn} source={result.grid.source} columns={result.grid.columns} pinnedColumnCount={result.grid.pinnedColumnCount}
                    temporal={Temporal} viewportHeight={180} viewportWidth="stretch" />
            )}
        </>
    );
}
