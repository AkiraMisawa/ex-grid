// @vitest-environment happy-dom
// The Mantine chart Chrome and Chart Dock keep the contracts every Wrapper keeps with ExChart for React (ADR-0390
// section 7): the same tests the React adapter runs against a Chrome of plain controls, run here against Mantine's
// components inside a MantineProvider, as a Consumer's page renders them.
import { createElement, useState, type ReactElement, type ReactNode } from 'react';
import { act, cleanup, fireEvent, render } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { MantineProvider } from '@mantine/core';
import { GridSource } from '@ex-grid/core';
import { GridChartBridge, type GridChart } from '@ex-grid/chart';
import { useChartDock } from '@ex-grid/chart-react';
import { chartSlotContract, type ChartContractHost } from '../../chart-react/test/chart-slot-contract';
import { chartDockContract } from '../../chart-react/test/chart-dock-contract';
import { mantineChartChrome, MantineChartDock } from '../src/index';

const host: ChartContractHost = { wrapper: ({ children }: { readonly children: ReactNode }) => createElement(MantineProvider, null, children) };

chartSlotContract('the Mantine chart Chrome', mantineChartChrome, host);
chartDockContract('the Mantine Chart Dock', MantineChartDock, host);

describe('MantineChartDock', () => {
    afterEach(() => { cleanup(); });

    it('ADR-0258: Delete on a tab closes it and hands the keyboard to the tab shown in its place, and on the last tab back to the grid', async () => {
        const rows = [{ day: 0, y1: 3 }, { day: 1, y1: 3.1 }];
        const chart: GridChart = {
            orientation: 'seriesInColumns', axisColumn: 'day',
            series: [{ type: 'column', id: 'y1', panel: '', column: 'y1', rows: rows.map(r => ({ key: String(r.day), x: r.day })) }],
        };
        function Page(): ReactElement {
            const dock = useChartDock();
            const [bridge] = useState(() => new GridChartBridge<(typeof rows)[number]>({
                ...dock.bridgeOptions,
                declarations: { rowKey: r => String(r.day), rowOf: key => rows.find(r => String(r.day) === key), kindOf: () => 'level' },
                grid: () => ({
                    columns: [{ name: 'day', type: 'number', value: r => r.day }, { name: 'y1', type: 'number', value: r => r.y1 }],
                    source: GridSource.from(rows, { rowKey: r => String(r.day) }),
                }),
            }));
            useState(() => { dock.open(chart); dock.open(chart); return true; });
            return createElement(MantineChartDock<(typeof rows)[number]>, { dock, bridge }, createElement('button', { type: 'button', className: 'the-grid' }, 'The grid'));
        }
        const { container } = render(createElement(Page), host);
        await act(async () => { await Promise.resolve(); });
        const tab = (name: string): HTMLElement => {
            const found = [...container.querySelectorAll<HTMLElement>('[role=tab]')].find(t => t.textContent.trim() === name);
            if (found === undefined) throw new Error(`No tab ${name}.`);
            return found;
        };
        tab('Chart 2').focus();
        await act(async () => { fireEvent.keyDown(tab('Chart 2'), { key: 'Delete' }); await Promise.resolve(); });
        expect([...container.querySelectorAll('[role=tab]')].map(t => t.textContent.trim())).toEqual(['Chart 1']);
        expect(document.activeElement).toBe(tab('Chart 1'));
        await act(async () => { fireEvent.keyDown(tab('Chart 1'), { key: 'Delete' }); await Promise.resolve(); });
        expect(container.querySelector('[role=tab]')).toBeNull();
        expect(document.activeElement).toBe(container.querySelector('.the-grid'));
    });
});
