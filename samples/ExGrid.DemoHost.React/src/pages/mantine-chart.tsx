import { useCallback, useRef, useState, type ReactElement } from 'react';
import { MantineProvider } from '@mantine/core';
import { Temporal } from 'temporal-polyfill';
import { GridSource, type GridColumn, type GridHeaderGroup } from '@ex-grid/core';
import { GridChartBridge, gridChartCommandLabel } from '@ex-grid/chart';
import { useChartDock } from '@ex-grid/chart-react';
import { MantineChartDock } from '@ex-grid/chart-mantine';
import type { ExGridApi } from '@ex-grid/react';
import { MantineExGrid } from '@ex-grid/mantine';
// Mantine's styles and the Wrappers', as text this page writes into a <style> of its own and takes away when it
// goes: the other pages stay bare, and a spec file's document is the one it booted with once the page is left
// (ADR-0056).
import mantineStyles from '@mantine/core/styles.css?inline';
import gridStyles from '../../../../src/npm/mantine/styles.css?inline';
import chartStyles from '../../../../src/npm/chart-mantine/styles.css?inline';
import { DemoNav } from '../demo-nav';

/** One date's rates. */
interface CurveRow {
    readonly date: Temporal.PlainDate;
    readonly y1: number;
    readonly y2: number;
    readonly y5: number;
    readonly y10: number;
}

const start = Temporal.PlainDate.from('2026-10-01');

function threePlaces(value: unknown): string {
    if (typeof value !== 'number') throw new TypeError(`A rate is a number, not ${typeof value}.`);
    return value.toFixed(3);
}

const rows: readonly CurveRow[] = Object.freeze(Array.from({ length: 8 }, (_, i) => Object.freeze({
    date: start.add({ days: i }),
    y1: 3.0 + i * 0.01,
    y2: 3.1 + i * 0.01,
    y5: 3.3 - i * 0.02,
    y10: 3.6 - i * 0.01,
})));

// Held once, never rebuilt per render (ADR-0003).
const columns: readonly GridColumn<CurveRow>[] = Object.freeze([
    { name: 'date', type: 'date', dateType: 'dateOnly', value: (r: CurveRow) => r.date, header: 'Date', width: 140 },
    { name: 'y1', type: 'number', value: (r: CurveRow) => r.y1, header: '1Y', format: threePlaces, width: 80 },
    { name: 'y2', type: 'number', value: (r: CurveRow) => r.y2, header: '2Y', format: threePlaces, width: 80 },
    { name: 'y5', type: 'number', value: (r: CurveRow) => r.y5, header: '5Y', format: threePlaces, width: 80 },
    { name: 'y10', type: 'number', value: (r: CurveRow) => r.y10, header: '10Y', format: threePlaces, width: 80 },
]);

const groups: readonly GridHeaderGroup[] = Object.freeze([{ label: 'Rates', columns: ['y1', 'y2', 'y5', 'y10'] }]);
const tenors: ReadonlyMap<string, number> = new Map([['y1', 1], ['y2', 2], ['y5', 5], ['y10', 10]]);
const keyOf = (r: CurveRow): string => r.date.toString();

/**
 * The page /mantine-chart, React's alone (ADR-0258, ADR-0281, ADR-0390): the curve history of /chart-bridge in
 * @ex-grid/mantine's paper, inside @ex-grid/chart-mantine's Chart Dock. The grid's context menu opens each chart made
 * from the selection as a Mantine tab beside the grid, its toolbar and context menu in Mantine's components, and the
 * theme's primary leads its series in both schemes. The provider keeps its scheme and variables on this page's own
 * element, not on the document.
 */
export function MantineChartDemo(): ReactElement {
    const [scope, setScope] = useState<HTMLDivElement | null>(null);
    const [dark, setDark] = useState(false);
    const dock = useChartDock();
    const grid = useRef<ExGridApi<CurveRow>>(null);
    const [source] = useState(() => GridSource.from(rows, { rowKey: keyOf }));
    const [bridge] = useState(() => new GridChartBridge<CurveRow>({
        ...dock.bridgeOptions,
        declarations: {
            rowKey: keyOf,
            rowOf: key => rows.find(r => keyOf(r) === key),
            kindOf: () => 'level',
            positionOf: column => tenors.get(column),
            positionTitle: 'Tenor (years)',
            rowName: r => r.date.toString(),
        },
        grid: () => ({ columns, headerGroups: groups, source }),
        placeSelection: ({ range, focus, rowSequenceVersion }) => {
            if (grid.current === null) throw new Error('Internal: the grid is not mounted.');
            return grid.current.placeSelection(range, focus, rowSequenceVersion);
        },
    }));
    const rootOf = useCallback((): HTMLElement => {
        if (scope === null) throw new Error('Internal: the provider is drawn only inside its element.');
        return scope;
    }, [scope]);
    return (
        <>
            <style>{mantineStyles}{gridStyles}{chartStyles}</style>
            <DemoNav route="mantine-chart" />
            <h1>@ex-grid/chart-mantine — the Chart Dock, under Mantine&apos;s theme</h1>
            <div className="demo-toolbar">
                <button id="toggle-dark" type="button" onClick={() => { setDark(on => !on); }}>Toggle dark mode</button>
            </div>
            <p className="demo-status"><span id="dark-status">Dark: {dark ? 'True' : 'False'}</span></p>
            <div ref={setScope} className="demo-mantine">
                {scope === null ? null : (
                    <MantineProvider
                        forceColorScheme={dark ? 'dark' : 'light'}
                        getRootElement={rootOf}
                        cssVariablesSelector=".demo-mantine"
                        deduplicateCssVariables={false}>
                        <MantineChartDock dock={dock} bridge={bridge} chartWidth={520} chartHeight={260}>
                            <MantineExGrid
                                ref={grid}
                                withBorder
                                columns={columns}
                                source={source}
                                headerGroups={groups}
                                contextCommands={bridge.commands}
                                commandLabel={gridChartCommandLabel}
                                pinnedColumnCount={1}
                                viewportHeight={260}
                                viewportWidth={480}
                                temporal={Temporal} />
                        </MantineChartDock>
                    </MantineProvider>
                )}
            </div>
        </>
    );
}
