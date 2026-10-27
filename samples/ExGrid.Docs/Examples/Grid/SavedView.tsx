import type { ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import { ExGrid } from '@ex-grid/react';
import { indented, useSavedView } from './SavedViewParts';

export default function SavedView(): ReactElement {
    const view = useSavedView();
    return (
        <>
            <ExGrid source={view.source} columns={view.columns} pinnedColumnCount={view.pinned} temporal={Temporal}
                onColumnWidthChanged={view.onColumnWidthChanged} onColumnOrderChanged={view.onColumnOrderChanged}
                onPinnedCountChanged={view.onPinnedCountChanged} viewportHeight={260} viewportWidth="stretch" />
            <div className="bar" style={{ marginTop: 12 }}>
                <button type="button" onClick={view.save}>Save the view</button>
                <button type="button" onClick={view.reset}>Reset</button>
                <button type="button" disabled={view.saved === undefined} onClick={view.restore}>Restore the saved view</button>
                <span role="status">{view.said}</span>
            </div>
            {view.saved !== undefined && <pre style={{ margin: '8px 0 0', maxHeight: 220, overflow: 'auto', fontSize: 12 }}>{indented(view.saved)}</pre>}
        </>
    );
}
