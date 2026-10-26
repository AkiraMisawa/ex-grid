// @vitest-environment happy-dom
// Layer 2 (ADR-0370 section 1): ExChart's 3D surface in a DOM emulation. The DOM contract ExSurface.razor
// writes, which ex-chart.css and layer 3 read; that a hole is drawn as a hole (ADR-0274); and what each
// gesture raises, in the data's terms, and when it is refused (ADR-0272, spine rule 6). What the browser
// tells — a pointer's offsets on the box — is told here by hand.
import { afterEach, describe, expect, it, vi } from 'vitest';
import { japaneseChartWords } from '../src/dom/chart-words';
import { createSurface, type SurfaceHandle } from '../src/dom/create-surface';
import type { SurfaceOptions } from '../src/dom/surface-element';
import { elementAt } from '../src/must';
import { SurfaceDefinition, type SurfaceSlice, type SurfaceView } from '../src/surface';

const mounted: SurfaceHandle[] = [];

afterEach(() => {
    mounted.splice(0).forEach(surface => { surface.destroy(); });
    document.body.replaceChildren();
});

function mount(options: SurfaceOptions): { readonly root: HTMLElement; readonly surface: SurfaceHandle } {
    const root = document.createElement('div');
    document.body.append(root);
    const surface = createSurface(root, options);
    mounted.push(surface);
    return { root, surface };
}

function q(root: ParentNode, selector: string): Element {
    const found = root.querySelector(selector);
    if (found === null) {
        throw new Error(`Nothing matches ${selector}.`);
    }
    return found;
}

const deltas = ['10P', '25P', 'ATM', '25C', '10C'];
const tenors = ['1W', '1M', '3M', '6M', '1Y', '2Y', '5Y', '10Y'];

// The demo's volatility surface: five deltas by eight tenors, with a hole at 25C 5Y.
function vol(run = 0): SurfaceDefinition {
    const smile = [1.6, 0.5, 0, 0.3, 1.1] as const;
    return SurfaceDefinition.grid({
        x: { title: 'Delta', values: deltas },
        y: { title: 'Tenor', values: tenors },
        z: tenors.map((tenor, t) => deltas.map((delta, i) => (delta === '25C' && tenor === '5Y' ? null
            : Math.round((9.5 + 1.8 * Math.exp(-t / 2.5) + 0.12 * t + elementAt(smile, i) * (1 + 0.1 * t) + run * 0.2) * 100) / 100))),
        zTitle: 'Volatility',
        zFormat: v => `${Number(v).toFixed(2)}%`,
    });
}

function mesh(): SurfaceDefinition {
    return SurfaceDefinition.fromTriangles({
        nodes: [{ x: 0, y: 0, z: 0 }, { x: 1, y: 0, z: 1 }, { x: 0, y: 1, z: 1 }, { x: 1, y: 1, z: 2 }],
        triangles: [{ a: 0, b: 1, c: 3 }, { a: 0, b: 3, c: 2 }],
        xTitle: 'x', yTitle: 'y', zTitle: 'u',
    });
}

const box = (root: HTMLElement): HTMLElement => {
    const found = q(root, '.ex-chart-surface-box');
    if (!(found instanceof HTMLElement)) {
        throw new Error('The box is not an HTML element.');
    }
    return found;
};

// Where the n-th dot was drawn on the box.
function dot(root: HTMLElement, index: number): readonly [number, number] {
    const circle = root.querySelectorAll('circle.ex-chart-surface-point')[index];
    if (circle === undefined) {
        throw new Error(`There is no dot ${String(index)}.`);
    }
    return [Number(circle.getAttribute('cx')), Number(circle.getAttribute('cy'))];
}

function pointer(root: HTMLElement, type: string, x: number, y: number, init: PointerEventInit = {}): void {
    // happy-dom takes the offsets the browser would report on the box.
    const told: PointerEventInit & { readonly offsetX: number; readonly offsetY: number } = { bubbles: true, pointerId: 1, button: 0, offsetX: x, offsetY: y, ...init };
    box(root).dispatchEvent(new PointerEvent(type, told));
}

function key(root: HTMLElement, k: string, init: KeyboardEventInit = {}): void {
    box(root).dispatchEvent(new KeyboardEvent('keydown', { key: k, bubbles: true, cancelable: true, ...init }));
}

function click(element: Element): void {
    element.dispatchEvent(new MouseEvent('click', { bubbles: true }));
}

describe('createSurface', () => {
    it('ADR-0274, CH-7: writes the DOM contract, and a hole has no point and none of the four faces round it', () => {
        const { root } = mount({ definition: vol(), title: 'EURUSD volatility' });
        expect(root.classList.contains('ex-chart')).toBe(true);
        expect(root.classList.contains('ex-chart-surface')).toBe(true);
        expect([...root.querySelectorAll('.ex-chart-toolbar button')].map(b => b.textContent)).toEqual(['Reset view', 'View as table', 'SVG', 'PNG']);
        expect(q(root, '.ex-chart-surface-reset').hasAttribute('disabled')).toBe(true);
        const b = box(root);
        expect(b.getAttribute('role')).toBe('img');
        expect(b.getAttribute('aria-roledescription')).toBe('3D surface');
        expect(b.getAttribute('aria-label')).toMatch(/^EURUSD volatility, a 3D surface of Volatility over Delta/);
        expect(b.tabIndex).toBe(0);
        expect(b.style.width).toBe('640px');
        expect(root.querySelectorAll('circle.ex-chart-surface-point')).toHaveLength(39);
        expect(root.querySelectorAll('polygon.ex-chart-surface-face')).toHaveLength(4 * 7 - 4);
        expect(root.querySelectorAll('.ex-chart-surface-bar-band')).toHaveLength(9);
        expect(root.hasAttribute('aria-busy')).toBe(false);
    });

    it('ADR-0274, ADR-0255: the tooltip names the point nearest the pointer, and is rewritten only when that point changes', () => {
        const { root } = mount({ definition: vol() });
        const [x, y] = dot(root, 0);
        pointer(root, 'pointermove', x + 1, y);
        const tooltip = q(root, '.ex-chart-surface-tooltip');
        expect([...tooltip.querySelectorAll('.ex-chart-tooltip-line')].map(l => l.textContent)).toEqual(['Delta10P', 'Tenor1W', 'Volatility12.90%']);
        pointer(root, 'pointermove', x + 2, y);
        expect(q(root, '.ex-chart-surface-tooltip')).toBe(tooltip);
        box(root).dispatchEvent(new PointerEvent('pointerleave'));
        expect(root.querySelector('.ex-chart-surface-tooltip')).toBeNull();
    });

    it('ADR-0274, ADR-0272: a drag turns the surface and raises the view once, when it ends; the echo leaves it, and Reset view returns it', () => {
        const views: SurfaceView[] = [];
        const onViewChanged = (view: SurfaceView): void => { views.push(view); };
        const { root, surface } = mount({ definition: vol(), height: 360, onViewChanged });
        const before = q(root, '.ex-chart-surface-faces').innerHTML;
        pointer(root, 'pointerdown', 100, 100);
        pointer(root, 'pointermove', 110, 100);
        pointer(root, 'pointermove', 130, 100);
        expect(q(root, '.ex-chart-surface-faces').innerHTML).not.toBe(before);
        expect(views).toEqual([]);
        pointer(root, 'pointerup', 130, 100);
        // 360 degrees a box's height: 30 pixels of 360 turn it 30 degrees.
        expect(views).toEqual([{ yaw: -5, pitch: 28, zoom: 1, panX: 0, panY: 0 }]);
        const turned = q(root, '.ex-chart-surface-faces').innerHTML;
        surface.update({ definition: vol(), height: 360, onViewChanged, view: elementAt(views, 0) });
        expect(q(root, '.ex-chart-surface-faces').innerHTML).toBe(turned);
        expect(q(root, '.ex-chart-surface-reset').hasAttribute('disabled')).toBe(false);
        click(q(root, '.ex-chart-surface-reset'));
        expect(views.at(-1)).toEqual({ yaw: -35, pitch: 28, zoom: 1, panX: 0, panY: 0 });
        expect(q(root, '.ex-chart-surface-faces').innerHTML).toBe(before);
    });

    it('spine rule 6: a drag begun before the data is replaced ends unreported', () => {
        const onViewChanged = vi.fn<(view: SurfaceView) => void>();
        const { root, surface } = mount({ definition: vol(), onViewChanged });
        pointer(root, 'pointerdown', 100, 100);
        pointer(root, 'pointermove', 140, 100);
        surface.update({ definition: vol(1), onViewChanged });
        pointer(root, 'pointermove', 160, 100);
        pointer(root, 'pointerup', 160, 100);
        expect(onViewChanged).not.toHaveBeenCalled();
    });

    it('ADR-0274: the arrows turn, + zooms and Shift with an arrow pans, each raised as the view it leaves', () => {
        const views: SurfaceView[] = [];
        const { root } = mount({ definition: vol(), onViewChanged: v => { views.push(v); } });
        key(root, 'ArrowRight');
        key(root, '+');
        key(root, 'ArrowDown', { shiftKey: true });
        key(root, 'ArrowLeft', { ctrlKey: true });
        expect(views).toEqual([
            { yaw: -30, pitch: 28, zoom: 1, panX: 0, panY: 0 },
            { yaw: -30, pitch: 28, zoom: 1.25, panX: 0, panY: 0 },
            { yaw: -30, pitch: 28, zoom: 1.25, panX: 0, panY: 0.05 },
        ]);
    });

    it('ADR-0274: a drag with the right button pans, opens no menu and draws no slice', () => {
        const onSliceChanged = vi.fn<(slice: SurfaceSlice) => void>();
        const views: SurfaceView[] = [];
        const { root } = mount({ definition: vol(), onSliceChanged, onViewChanged: v => { views.push(v); } });
        const menu = new MouseEvent('contextmenu', { bubbles: true, cancelable: true });
        box(root).dispatchEvent(menu);
        expect(menu.defaultPrevented).toBe(true);
        pointer(root, 'pointerdown', 100, 100, { button: 2 });
        pointer(root, 'pointermove', 164, 100, { button: 2 });
        pointer(root, 'pointerup', 164, 100, { button: 2 });
        expect(views).toEqual([{ yaw: -35, pitch: 28, zoom: 1, panX: 0.1, panY: 0 }]);
        expect(onSliceChanged).not.toHaveBeenCalled();
    });

    it('ADR-0274: a click on a point raises its slice and draws it beside the surface and on it; a slice it does not hold is said', () => {
        const slices: SurfaceSlice[] = [];
        const { root, surface } = mount({ definition: vol(), onSliceChanged: s => { slices.push(s); } });
        const [x, y] = dot(root, 0);
        pointer(root, 'pointerdown', x, y);
        pointer(root, 'pointerup', x, y);
        expect(slices).toHaveLength(1);
        const slice = slices[0];
        if (slice === undefined) {
            throw new Error('No slice was raised.');
        }
        expect(slice.axis === 'x' ? slice.value === '10P' : slice.value === '1W').toBe(true);
        expect(root.querySelector('.ex-chart-surface-slice polyline')).not.toBeNull();
        const chart = q(root, '.ex-chart-surface-slice-chart .ex-chart');
        expect(q(chart, '.ex-chart-svg').getAttribute('width')).toBe('420');
        surface.update({ definition: vol(), slice: { axis: 'y', value: '3Y' } });
        expect(root.querySelector('.ex-chart-surface-slice-chart')).toBeNull();
        expect(q(root, '.ex-chart-notes').textContent).toBe('No slice at Tenor 3Y: this surface has no such value.');
    });

    it('ADR-0275, ADR-0311: View as table tabulates a grid, its hole blank, and lists a mesh\'s nodes', () => {
        const { root, surface } = mount({ definition: vol(), title: 'EURUSD' });
        click(q(root, '.ex-chart-table-toggle'));
        expect(q(root, '.ex-chart-table-toggle').getAttribute('aria-expanded')).toBe('true');
        expect(q(root, '.ex-chart-table caption').textContent).toBe('EURUSD');
        expect([...root.querySelectorAll('.ex-chart-table thead th')].map(th => th.textContent)).toEqual(['Tenor \\ Delta', ...deltas]);
        expect(root.querySelectorAll('.ex-chart-table tbody tr')).toHaveLength(8);
        expect(root.querySelectorAll('.ex-chart-table tbody tr')[6]?.querySelectorAll('td')[3]?.textContent).toBe('');
        surface.update({ definition: mesh() });
        expect(q(root, '.ex-chart-table caption').textContent).toBe('Surface data');
        expect([...root.querySelectorAll('.ex-chart-table thead th')].map(th => th.textContent)).toEqual(['x', 'y', 'u']);
        expect(root.querySelectorAll('.ex-chart-table tbody tr')).toHaveLength(4);
    });

    it('ADR-0370 section 10: Japanese is chosen in one line, and reaches the slice\'s chart', () => {
        const { root } = mount({ definition: vol(), label: japaneseChartWords, slice: { axis: 'y', value: '1Y' } });
        expect([...root.querySelectorAll('.ex-chart-surface > .ex-chart-toolbar button')].map(b => b.textContent))
            .toEqual(['表示をリセット', '表として表示', 'SVG', 'PNG']);
        expect(box(root).getAttribute('aria-roledescription')).toBe('3D サーフェス');
        expect(q(root, '.ex-chart-surface-slice-chart .ex-chart-table-toggle').textContent).toBe('表として表示');
    });

    it('ADR-0282: the drawing is SVG text with its colours resolved and no classes, and a surface saves no CSV', async () => {
        const { surface } = mount({ definition: vol() });
        const text = surface.svgText();
        expect(text).toMatch(/^<svg /);
        expect(text).not.toContain('class=');
        expect(text).toContain('viewBox="0 0 640 420"');
        await expect(surface.download('csv')).resolves.toBe(false);
    });

    it('destroy empties the element, leaves the Consumer\'s style, and refuses another update', () => {
        const { root, surface } = mount({ definition: vol() });
        root.style.setProperty('margin', '4px');
        surface.destroy();
        expect(root.childElementCount).toBe(0);
        expect(root.classList.contains('ex-chart')).toBe(false);
        expect(root.style.getPropertyValue('margin')).toBe('4px');
        expect(root.style.getPropertyValue('font-size')).toBe('');
        expect(() => { surface.update({ definition: vol() }); }).toThrow(/destroyed/);
        expect(surface.svgText()).toBeUndefined();
    });
});
