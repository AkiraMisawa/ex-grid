import { useCallback, useState, type ReactElement } from 'react';
import type { CellState, GridColumn, GridHeaderGroup, HeaderGroupToggle } from '@ex-grid/core';
import { ExGrid } from '@ex-grid/react';
import { Temporal } from 'temporal-polyfill';
import { DemoNav } from '../demo-nav';
import { tradeColumns, tradeWindow, type DemoTrade } from '../demo-data';

const rows = tradeWindow(1_000, 7);

// Held once, never rebuilt per render: a new array would push the columns again (ADR-0003).
const closed: readonly GridColumn<DemoTrade>[] = Object.freeze(
    tradeColumns.filter(column => column.name !== 'TradeDate' && column.name !== 'Confirmed'));

const openGroups: readonly GridHeaderGroup[] = Object.freeze([
    { label: 'Desk', columns: ['Book', 'Trader'] },
    { label: 'Trade', columns: ['Notional', 'TradeDate', 'Confirmed'], id: 'trade', expanded: true },
]);

const closedGroups: readonly GridHeaderGroup[] = Object.freeze([
    { label: 'Desk', columns: ['Book', 'Trader'] },
    { label: 'Trade', columns: ['Notional'], id: 'trade', expanded: false },
]);

const marked: readonly GridColumn<DemoTrade>[] = Object.freeze(tradeColumns.slice(0, 4));

function marking(column: string, label: string, state: Exclude<CellState, 'normal'>, reason: string): GridHeaderGroup {
    return { label, columns: [column], id: state, expanded: false, hiddenMark: { state, reason } };
}

const markGroups: readonly GridHeaderGroup[] = Object.freeze([
    marking('Book', 'Stale', 'stale', '1 price older than the market\'s close'),
    marking('Trader', 'Missing', 'missing', '2 values the source could not give'),
    marking('Notional', 'Error', 'error', '3 values that broke a rule'),
    marking('TradeDate', 'Modified', 'modified', '4 values set by hand'),
]);

const ignored = (): void => { };

/** The page /group-toggle of the Blazor hosts: a Header Group that opens and closes, and the Hidden Mark (ADR-0400). */
export function GroupToggle(): ReactElement {
    const [open, setOpen] = useState(true);
    const [heard, setHeard] = useState<readonly string[]>([]);
    const toggled = useCallback((toggle: HeaderGroupToggle) => {
        const at = toggle.focusColumn === undefined ? '' : ` at ${toggle.focusColumn}`;
        setHeard(was => [...was, `${toggle.id} ${toggle.expanded ? 'open' : 'closed'}${at}`]);
        setOpen(toggle.expanded);
    }, []);
    return (
        <>
            <DemoNav route="group-toggle" />
            <h1>Header Groups that open and close</h1>
            <p>
                Trade is declared with an id and whether it is open, so the grid draws a toggle at its left (ADR-0400). A
                press on it, or Alt+Shift+↑ and Alt+Shift+↓ from a cell under it, asks this page to close or open the
                group; the page pushes the columns it then shows, and the grid adds and removes none itself. Closed, the
                group keeps Notional. Each notification is written out below, as the page heard it.
            </p>
            <div id="group-toggle">
                <ExGrid rows={rows} columns={open ? tradeColumns : closed} headerGroups={open ? openGroups : closedGroups}
                    onHeaderGroupToggled={toggled} temporal={Temporal} viewportHeight={240} viewportWidth={640} />
            </div>
            <p><span id="group-toggled" role="status">Toggled: {heard.length === 0 ? 'none' : heard.join('; ')}</span></p>
            <p>
                Below, four closed groups each carry a Hidden Mark in the colour of a Cell State, the reason its tooltip:
                what the hidden columns hold that the reader must not lose sight of. Their toggles are heard and change
                nothing.
            </p>
            <div id="hidden-marks">
                <ExGrid rows={rows} columns={marked} headerGroups={markGroups} onHeaderGroupToggled={ignored}
                    temporal={Temporal} viewportHeight={160} viewportWidth={640} />
            </div>
        </>
    );
}
