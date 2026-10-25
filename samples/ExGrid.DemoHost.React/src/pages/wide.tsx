import { useMemo, useState, type ReactElement } from 'react';
import { ExGrid } from '@ex-grid/react';
import { DemoNav } from '../demo-nav';
import { wideColumns, type DemoWideRow } from '../demo-data';
import { useQuery } from '../router';

/** The Definition of Done's large-data scenario (§12): a million rows. */
const defaultTotal = 1_000_000;

/** The page /wide of the Blazor hosts: both axes virtualised, two columns pinned (ADR-0004). The Blazor page
 * hands its grid the count and the Window up front; here the rows are held as the grid's from the first
 * paint, so a scroll made the moment the grid appears has the whole height to move in, as there. A row is
 * its index, and every value is computed from it as it is painted. */
export function Wide(): ReactElement {
    const asked = useQuery('rows');
    const total = asked === undefined ? defaultTotal : Number(asked);
    const [virtualiseColumns, setVirtualise] = useState(true);
    const [pinnedColumnCount, setPinned] = useState(2);
    const rows = useMemo(() => Array.from({ length: total }, (_, index): DemoWideRow => ({ index })), [total]);
    return (
        <>
            <DemoNav route="wide" />
            <h1>Wide — {total.toLocaleString('en-US')} rows × 100 columns</h1>
            <p>
                Both axes are virtualised (ADR-0004): only the rows and the columns the Viewport reaches are in the DOM,
                and the first two columns are pinned, so they stay against the left edge while the rest pans under
                them. The button turns horizontal virtualisation off so the two can be compared on the same data.
            </p>
            <div className="demo-toolbar">
                <button id="toggle-virtualise" type="button" onClick={() => { setVirtualise(!virtualiseColumns); }}>
                    Horizontal virtualisation: {virtualiseColumns ? 'on' : 'off'} — turn it {virtualiseColumns ? 'off' : 'on'}
                </button>
                <button id="toggle-pinned" type="button" onClick={() => { setPinned(pinnedColumnCount === 0 ? 2 : 0); }}>
                    Pinned columns: {pinnedColumnCount} — {pinnedColumnCount === 0 ? 'pin two' : 'unpin'}
                </button>
            </div>
            <p className="demo-status">
                {/* The grid's source holds every row, so its Window is all of them. */}
                Window: rows 0–{total - 1} of {total}, {wideColumns.length} columns.
            </p>
            <ExGrid
                columns={wideColumns}
                rows={rows}
                rowHeight={28}
                viewportHeight={600}
                viewportWidth={900}
                pinnedColumnCount={pinnedColumnCount}
                virtualiseColumns={virtualiseColumns} />
        </>
    );
}
