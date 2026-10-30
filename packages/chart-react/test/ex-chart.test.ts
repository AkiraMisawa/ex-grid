// @vitest-environment happy-dom
// The adapter's own tests (ADR-0370 section 1): React Testing Library mounts it. What the chart draws is the
// Script Core's, tested there; here, that the adapter mounts the core once, hands it each render's props,
// raises its events to the Consumer, and destroys it with the component.
import { createElement, createRef, StrictMode, type ReactElement } from 'react';
import { act, cleanup, render } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { ChartDefinition, type ChartToolbarSlotContext, type SeriesVisibility } from '@ex-grid/chart';
import { ExChart, type ExChartApi, type ExChartChrome } from '../src/ex-chart';

afterEach(() => { cleanup(); });

const definition = (ys: readonly number[]): ChartDefinition => new ChartDefinition({
    axis: { values: 'numbers', title: 'Years' },
    series: [{ id: 'epe', name: 'EPE', kind: 'level', points: ys.map((y, x) => ({ x, y })) }],
});

describe('ExChart', () => {
    it('ADR-0370: the element React renders is the chart\'s instance root, drawn before the browser paints', () => {
        const { container } = render(createElement(ExChart, { definition: definition([1, 2, 3]), title: 'Exposure' }));
        const root = container.firstElementChild;
        expect(root?.className).toBe('ex-chart');
        expect(root?.querySelector('svg title')?.textContent).toMatch(/^Exposure\./);
    });

    it('ADR-0272: a render hands the chart its props, the same root keeps drawing, and a legend click reaches the Consumer', () => {
        const heard: SeriesVisibility[] = [];
        const onSeriesVisibilityChanged = (change: SeriesVisibility): void => { heard.push(change); };
        const { container, rerender } = render(createElement(ExChart, { definition: definition([1, 2]), onSeriesVisibilityChanged }));
        const root = container.firstElementChild;
        rerender(createElement(ExChart, { definition: definition([1, 2, 3, 4]), onSeriesVisibilityChanged, title: 'Four' }));
        expect(container.firstElementChild).toBe(root);
        expect(root?.querySelector('svg title')?.textContent).toMatch(/^Four\. Years, 0 to 3\./);
        act(() => { root?.querySelector('.ex-chart-legend-entry')?.dispatchEvent(new MouseEvent('click', { bubbles: true })); });
        expect(heard).toEqual([{ key: 'epe', hidden: true }]);
    });

    it('ADR-0282: a ref reaches the chart\'s saves', async () => {
        const ref = createRef<ExChartApi>();
        render(createElement(ExChart, { definition: new ChartDefinition({
            axis: { values: 'numbers' },
            series: [{ id: 's', name: 'S', kind: 'level', points: [{ x: 0, y: 1 }], thinned: [{ from: 0, to: 0, min: 1, max: 1, count: 2 }] }],
        }), ref }));
        // A chart its source thinned saves no CSV: the answer is the core's, reached through the ref.
        await expect(ref.current?.download('csv')).resolves.toBe(false);
    });

    it('ADR-0390: a render function handed again in another chrome draws nothing again, and one that is not a function is refused', async () => {
        const drawn: string[] = [];
        const toolbar = (context: ChartToolbarSlotContext): ReactElement => {
            drawn.push(context.name);
            return createElement('div', { role: 'toolbar', 'aria-label': context.name });
        };
        const { container, rerender } = render(createElement(ExChart, { definition: definition([1, 2, 3]), title: 'Exposure', chrome: { toolbar } }));
        await act(async () => { await Promise.resolve(); });
        expect(container.querySelector('.ex-chart-toolbar-slot [role=toolbar]')?.getAttribute('aria-label')).toBe('Exposure');
        rerender(createElement(ExChart, { definition: definition([1, 2, 3]), title: 'Exposure', chrome: { toolbar } }));
        await act(async () => { await Promise.resolve(); });
        expect(drawn).toEqual(['Exposure']);
        rerender(createElement(ExChart, { definition: definition([1, 2, 3]), title: 'Risk', chrome: { toolbar } }));
        await act(async () => { await Promise.resolve(); });
        expect(drawn).toEqual(['Exposure', 'Risk']);
        expect(() => render(createElement(ExChart, { definition: definition([1]), chrome: { toolbar: 'draw' } as unknown as ExChartChrome }))).toThrow(/render function/);
    });

    it('ADR-0018: unmounted, the chart is destroyed and its root emptied; under StrictMode one chart stands', () => {
        const { container, unmount } = render(createElement(StrictMode, null, createElement(ExChart, { definition: definition([1, 2]) })));
        const root = container.firstElementChild;
        expect(root?.querySelectorAll('svg.ex-chart-svg')).toHaveLength(1);
        unmount();
        expect(root?.childElementCount).toBe(0);
    });
});
