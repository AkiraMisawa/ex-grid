// The contract every Wrapper's Chart Dock keeps (ADR-0258, ADR-0390 section 7): run at layer 2 against each design
// system's dock alike. The dock draws useChartDock's state and decides nothing: a chart made opens a tab named
// "Chart N" and shows it, every open tab is a target of "Add to chart", a chart added to replaces its tab, a refusal
// is said above the tabs until dismissed, a closed tab shows the one beside it, and a chart its rows no longer draw
// says why in its tab, never what it last drew.
import { createElement, useState, type ComponentType, type ReactElement, type ReactNode } from 'react';
import { act, cleanup, render } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { GridSource, type GridColumn } from '@ex-grid/core';
import { GridChartBridge, type GridChart } from '@ex-grid/chart';
import { useChartDock, type ChartDockPlacement, type ChartDockState } from '../src/chart-dock';
import type { ChartContractHost } from './chart-slot-contract';

/** What a Wrapper's dock takes, as the contract renders it. */
export interface ContractDockProps {
    readonly dock: ChartDockState;
    readonly bridge: GridChartBridge<Rate>;
    readonly children?: ReactNode;
    readonly placement?: ChartDockPlacement;
}

/** A row of the grid the dock's charts are made from. */
export interface Rate {
    readonly day: number;
    readonly y1: number;
}

const rows: readonly Rate[] = Array.from({ length: 5 }, (_, day) => ({ day, y1: 3 + day / 10 }));
const columns: readonly GridColumn<Rate>[] = [
    { name: 'day', type: 'number', value: r => r.day },
    { name: 'y1', type: 'number', value: r => r.y1, header: '1Y' },
];

/** A chart of the 1Y column along the days, as the bridge makes one from a selection. */
function chartAlong(axisColumn: string): GridChart {
    return {
        orientation: 'seriesInColumns', axisColumn,
        series: [{ type: 'column', id: 'y1', panel: '', column: 'y1', rows: rows.map(r => ({ key: String(r.day), x: r.day })) }],
    };
}

async function settled(): Promise<void> {
    await act(async () => {
        for (let turn = 0; turn < 4; turn++) await Promise.resolve();
    });
}

function dockOf(held: { dock?: ChartDockState }): ChartDockState {
    if (held.dock === undefined) throw new Error('The dock did not render.');
    return held.dock;
}

const tabs = (container: HTMLElement): string[] => [...container.querySelectorAll('[role=tab]')].map(tab => tab.textContent.trim());
const selected = (container: HTMLElement): string | undefined =>
    container.querySelector('[role=tab][aria-selected=true]')?.textContent.trim();
const panel = (container: HTMLElement): Element | null => container.querySelector('[role=region][aria-label="Charts"]');

/** Runs the Chart Dock's contract against `Dock`, named `name` in the report. */
export function chartDockContract(name: string, Dock: ComponentType<ContractDockProps>, host: ChartContractHost = {}): void {
    function mounted(placement?: ChartDockPlacement): { readonly container: HTMLElement; readonly held: { dock?: ChartDockState } } {
        const held: { dock?: ChartDockState } = {};
        function Page(): ReactElement {
            const dock = useChartDock();
            held.dock = dock;
            const source = useState(() => GridSource.from(rows, { rowKey: r => String(r.day) }))[0];
            const [bridge] = useState(() => new GridChartBridge<Rate>({
                ...dock.bridgeOptions,
                declarations: { rowKey: r => String(r.day), rowOf: key => rows.find(r => String(r.day) === key), kindOf: () => 'level' },
                grid: () => ({ columns, source }),
            }));
            return createElement(Dock, { dock, bridge, ...(placement === undefined ? {} : { placement }) },
                createElement('div', { className: 'the-grid' }, 'The grid'));
        }
        const { container } = render(createElement(Page), host);
        return { container, held };
    }

    describe(`${name} keeps the Chart Dock's contract (ADR-0258, ADR-0390 section 7)`, () => {
        afterEach(() => { cleanup(); });

        it('ADR-0258: the grid stands alone until a chart is made; each chart made opens a tab, shown, and is a target of "Add to chart"', async () => {
            const { container, held } = mounted();
            await settled();
            expect(container.querySelector('.the-grid')).not.toBeNull();
            expect(panel(container)).toBeNull();
            await act(async () => { dockOf(held).bridgeOptions.onChart(chartAlong('day')); await Promise.resolve(); });
            await act(async () => { dockOf(held).bridgeOptions.onChart(chartAlong('day')); await Promise.resolve(); });
            await settled();
            expect(tabs(container)).toEqual(['Chart 1', 'Chart 2']);
            expect(selected(container)).toBe('Chart 2');
            expect(dockOf(held).bridgeOptions.targets().map(t => t.name)).toEqual(['Chart 1', 'Chart 2']);
            // Every tab holds its chart, drawn under the dock's Chrome.
            expect(container.querySelectorAll('svg.ex-chart-svg')).toHaveLength(2);
            expect(container.querySelector('.ex-chart-toolbar')).toBeNull();
            expect(container.querySelector('[role=toolbar]')?.getAttribute('aria-label')).toBe('Chart 1');
        });

        it('ADR-0258: a chart added to replaces its tab and is shown; an add taken against a tab since changed or closed is refused by name, above the tabs until dismissed', async () => {
            const { container, held } = mounted();
            await act(async () => { dockOf(held).bridgeOptions.onChart(chartAlong('day')); await Promise.resolve(); });
            await act(async () => { dockOf(held).bridgeOptions.onChart(chartAlong('day')); await Promise.resolve(); });
            const [first, second] = dockOf(held).bridgeOptions.targets();
            if (first === undefined || second === undefined) throw new Error('The dock offers no targets.');
            const added = chartAlong('day');
            await act(async () => { dockOf(held).bridgeOptions.onAdded(first, added); await Promise.resolve(); });
            await settled();
            expect(selected(container)).toBe('Chart 1');
            expect(dockOf(held).charts.find(c => c.name === 'Chart 1')?.chart).toBe(added);
            // A second add the menu offered against Chart 1 as it was: refused, never put over the series just added
            // (spine rule 6).
            await act(async () => { dockOf(held).bridgeOptions.onAdded(first, chartAlong('day')); await Promise.resolve(); });
            await settled();
            expect(dockOf(held).charts.find(c => c.name === 'Chart 1')?.chart).toBe(added);
            expect(panel(container)?.textContent).toContain('Chart 1 changed before the series was added to it; add it again.');
            const dismiss = (): HTMLButtonElement => {
                const button = [...container.querySelectorAll('button')].find(b => b.getAttribute('aria-label') === 'Dismiss');
                if (button === undefined) throw new Error('The refusal has no Dismiss.');
                return button;
            };
            await act(async () => { dismiss().click(); await Promise.resolve(); });
            await settled();
            expect(panel(container)?.textContent).not.toContain('add it again');
            await act(async () => { dockOf(held).close('Chart 2'); await Promise.resolve(); });
            await act(async () => { dockOf(held).bridgeOptions.onAdded(second, chartAlong('day')); await Promise.resolve(); });
            await settled();
            expect(tabs(container)).toEqual(['Chart 1']);
            expect(panel(container)?.textContent).toContain('Chart 2 was closed before the series was added to it.');
            await act(async () => { dockOf(held).bridgeOptions.onRefused('The selection has no axis column.'); await Promise.resolve(); });
            await settled();
            expect(panel(container)?.textContent).toContain('The selection has no axis column.');
            await act(async () => { dismiss().click(); await Promise.resolve(); });
            await settled();
            expect(panel(container)?.textContent).not.toContain('The selection has no axis column.');
        });

        it('ADR-0258: a closed tab shows the one beside it, and the panel goes with the last; a chart its rows no longer draw says why', async () => {
            const { container, held } = mounted('bottom');
            await act(async () => { dockOf(held).bridgeOptions.onChart(chartAlong('day')); await Promise.resolve(); });
            await act(async () => { dockOf(held).bridgeOptions.onChart(chartAlong('gone')); await Promise.resolve(); });
            await settled();
            expect(panel(container)?.textContent).toContain('The chart is drawn along gone, which is no longer in the grid.');
            const close = (tab: string): HTMLElement => {
                const mark = container.querySelector<HTMLElement>(`[aria-label="Close ${tab}"]`);
                if (mark === null) throw new Error(`${tab} has no close mark.`);
                return mark;
            };
            await act(async () => { close('Chart 2').click(); await Promise.resolve(); });
            await settled();
            expect([tabs(container), selected(container)]).toEqual([['Chart 1'], 'Chart 1']);
            expect(dockOf(held).bridgeOptions.targets().map(t => t.name)).toEqual(['Chart 1']);
            await act(async () => { close('Chart 1').click(); await Promise.resolve(); });
            await settled();
            expect(panel(container)).toBeNull();
        });
    });
}
