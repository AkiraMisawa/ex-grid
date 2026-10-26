// @vitest-environment happy-dom
// The surface adapter's own tests (ADR-0370 section 1). What the surface draws is the Script Core's, tested
// there; here, that the adapter hands the core each render's props, raises its events to the Consumer, reaches
// its drawing through a ref, and destroys it with the component.
import { createElement, createRef } from 'react';
import { act, cleanup, render } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { SurfaceDefinition, type SurfaceView } from '@ex-grid/chart';
import { ExSurface, type ExSurfaceApi } from '../src/ex-surface';

afterEach(() => { cleanup(); });

const surface = (z: number): SurfaceDefinition => SurfaceDefinition.grid({
    x: { title: 'Strike', values: [1, 2] },
    y: { title: 'Tenor', values: ['1M', '1Y'] },
    z: [[z, z + 1], [z + 1, z + 2]],
    zTitle: 'Vol',
});

describe('ExSurface', () => {
    it('ADR-0272: a render hands the surface its props, the same root keeps drawing, and a turn reaches the Consumer', () => {
        const views: SurfaceView[] = [];
        const onViewChanged = (view: SurfaceView): void => { views.push(view); };
        const { container, rerender } = render(createElement(ExSurface, { definition: surface(1), onViewChanged }));
        const root = container.firstElementChild;
        expect(root?.className).toBe('ex-chart ex-chart-surface');
        rerender(createElement(ExSurface, { definition: surface(2), onViewChanged, title: 'Vol' }));
        expect(container.firstElementChild).toBe(root);
        expect(root?.querySelector('.ex-chart-surface-box')?.getAttribute('aria-label')).toMatch(/^Vol, a 3D surface/);
        act(() => { root?.querySelector('.ex-chart-surface-box')?.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true })); });
        expect(views).toEqual([{ yaw: -30, pitch: 28, zoom: 1, panX: 0, panY: 0 }]);
    });

    it('ADR-0282: a ref reaches the drawing, and unmounted the surface is destroyed and its root emptied', () => {
        const ref = createRef<ExSurfaceApi>();
        const { container, unmount } = render(createElement(ExSurface, { definition: surface(1), ref }));
        expect(ref.current?.svgText()).toMatch(/^<svg /);
        const root = container.firstElementChild;
        unmount();
        expect(root?.childElementCount).toBe(0);
    });
});
