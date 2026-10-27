// @vitest-environment happy-dom
// The MUI chart Chrome and Chart Dock keep the contracts every Wrapper keeps with ExChart for React (ADR-0390 section
// 7): the same tests the React adapter runs against plain controls, run here against MUI's components inside a
// ThemeProvider whose theme has CSS variables, as a Consumer's page renders them.
import { createElement, useState, type ReactElement, type ReactNode } from 'react';
import { act, cleanup, fireEvent, render } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import { GridSource } from '@ex-grid/core';
import { GridChartBridge, type GridChart } from '@ex-grid/chart';
import { useChartDock } from '@ex-grid/chart-react';
import { chartSlotContract, type ChartContractHost } from '../../chart-react/test/chart-slot-contract';
import { chartDockContract } from '../../chart-react/test/chart-dock-contract';
import { muiChartChrome, MuiChartDock } from '../src/index';

const theme = createTheme({ cssVariables: true });
const host: ChartContractHost = { wrapper: ({ children }: { readonly children: ReactNode }) => createElement(ThemeProvider, { theme }, children) };

chartSlotContract('the MUI chart Chrome', muiChartChrome, host);
chartDockContract('the MUI Chart Dock', MuiChartDock, host);

// A dock with no chart open: the refusal comes before anything is drawn.
function UnthemedPage(): ReactElement {
    const dock = useChartDock();
    const [bridge] = useState(() => new GridChartBridge<{ readonly day: number }>({
        ...dock.bridgeOptions,
        declarations: { rowKey: r => String(r.day), rowOf: () => undefined, kindOf: () => 'level' },
        grid: () => ({ columns: [{ name: 'day', type: 'number', value: r => r.day }], source: GridSource.from<{ readonly day: number }>([]) }),
    }));
    return createElement(MuiChartDock<{ readonly day: number }>, { dock, bridge });
}

describe('MuiChartDock', () => {
    afterEach(() => { cleanup(); });

    it('ADR-0390 section 5: a theme made without CSS variables is refused by name, never drawn in colours not its own', () => {
        const plain = createTheme();
        // React reports the throw on the console as well; the throw is what is judged.
        const quiet = vi.spyOn(console, 'error').mockImplementation(() => undefined);
        try {
            expect(() => render(createElement(ThemeProvider, { theme: plain }, createElement(UnthemedPage)))).toThrow(/MuiChartDock needs a MUI theme with CSS variables/u);
        } finally {
            quiet.mockRestore();
        }
    });

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
            return createElement(MuiChartDock<(typeof rows)[number]>, { dock, bridge }, createElement('button', { type: 'button', className: 'the-grid' }, 'The grid'));
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
