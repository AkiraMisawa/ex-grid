// @vitest-environment happy-dom
// Layer 2 (ADR-0370 section 1): ExChart's Script Core in a DOM emulation. The DOM contract the Razor
// component writes, which ex-chart.css and layer 3 read; that a move of the pointer rewrites the tooltip and
// the guide and nothing else (ADR-0252); and what each gesture raises, in the data's terms (ADR-0272). What
// the browser tells — a pointer's offsets on the layer it is over — is told here by hand.
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ChartDefinition, type ChartDefinitionOptions } from '../src/definition';
import { createChart, type ChartHandle } from '../src/dom/create-chart';
import type { ChartOptions, ChartPointClick, ChartRange, SeriesVisibility } from '../src/dom/chart-element';
import { fileName, themeStyle, tokenNames } from '../src/dom/chart-files';
import { japaneseChartWords } from '../src/dom/chart-words';
import { stylesheetStamp } from '../src/dom/shipped-stylesheet';
import { dayNumber } from '../src/day-numbers';
import type { ChartPoint } from '../src/model';

const mounted: ChartHandle[] = [];

afterEach(() => {
    mounted.splice(0).forEach(chart => { chart.destroy(); });
    document.body.replaceChildren();
});

function mount(options: ChartOptions): { readonly root: HTMLElement; readonly chart: ChartHandle } {
    const root = document.createElement('div');
    document.body.append(root);
    const chart = createChart(root, options);
    mounted.push(chart);
    return { root, chart };
}

function q(root: ParentNode, selector: string): Element;
function q<T extends Element>(root: ParentNode, selector: string, kind: abstract new () => T): T;
function q(root: ParentNode, selector: string, kind: abstract new () => Element = Element): Element {
    const found = root.querySelector(selector);
    if (found === null) {
        throw new Error(`Nothing matches ${selector}.`);
    }
    if (!(found instanceof kind)) {
        throw new Error(`${selector} is not a ${kind.name}.`);
    }
    return found;
}

const points = (ys: readonly number[]): ChartPoint[] => ys.map((y, x) => ({ x, y }));

function definition(options: Partial<ChartDefinitionOptions> = {}): ChartDefinition {
    return new ChartDefinition({
        axis: { values: 'numbers', title: 'Years' },
        series: [
            { id: 'epe', name: 'EPE', kind: 'level', points: points([1, 5, 9, 4, 2]), panel: 'EPE' },
            { id: 'pfe', name: 'PFE', kind: 'level', points: points([2, 6, 11, 5, 3]), panel: 'EPE' },
            { id: 'ene', name: 'ENE', kind: 'level', points: points([-1, -3, -2, -2, -1]), panel: 'ENE' },
        ],
        ...options,
    });
}

// How many nodes a series of `n` points is drawn with.
function seriesNodes(n: number): number {
    const { root } = mount({ definition: new ChartDefinition({
        axis: { values: 'numbers' },
        series: [{ id: 's', name: 'S', kind: 'level', points: points(Array.from({ length: n }, (_, i) => Math.sin(i / 7))) }],
    }) });
    return q(root, 'g.ex-chart-series').querySelectorAll('*').length;
}

function hit(root: HTMLElement, panel = 0): HTMLElement {
    return q(root, `.ex-chart-hit[data-ex-panel="${String(panel)}"]`, HTMLElement);
}

// What the browser reports with a pointer event: its offsets on the layer it is over, which happy-dom takes.
type Told = PointerEventInit & { readonly target: HTMLElement; readonly offsetX: number; readonly offsetY: number };

// A pointer at a share of the plot's width and height, as the browser reports it on the hit layer.
function at(root: HTMLElement, across: number, down = 0.5, panel = 0): Told {
    const layer = hit(root, panel);
    return { target: layer, offsetX: parseFloat(layer.style.width) * across, offsetY: parseFloat(layer.style.height) * down, bubbles: true, pointerId: 1 };
}

function pointer(type: string, told: Told): void {
    const init: PointerEventInit = { button: 0, ...told };
    told.target.dispatchEvent(new PointerEvent(type, init));
}

function drag(root: HTMLElement, from: number, to: number): void {
    pointer('pointermove', at(root, from));
    pointer('pointerdown', at(root, from));
    pointer('pointermove', at(root, to));
    pointer('pointerup', at(root, to));
}

function click(element: Element): void {
    element.dispatchEvent(new MouseEvent('click', { bubbles: true }));
}

describe('createChart', () => {
    it('ADR-0370: the root, the toolbar, the stage and the drawing are the Razor component\'s DOM contract', () => {
        const { root } = mount({ definition: definition(), title: 'Exposure profile' });
        expect(root.className).toBe('ex-chart');
        expect(root.getAttribute('style')).toBe('width: 800px; font-size: 14px; --ex-chart-line-height: 20px;');
        expect([...root.children].map(c => c.className)).toEqual(['', 'ex-chart-toolbar', 'ex-chart-stage']);
        expect(q(root, '[data-ex-notice]').getAttribute('data-ex-stamp')).toBe(stylesheetStamp);
        expect([...q(root, '.ex-chart-toolbar').querySelectorAll('button')].map(b => [b.className, b.textContent])).toEqual([
            ['ex-chart-button ex-chart-filters-toggle', 'Chart Filters'],
            ['ex-chart-button ex-chart-reset', 'Reset range'],
            ['ex-chart-button ex-chart-table-toggle', 'View as table'],
            ['ex-chart-button ex-chart-download-csv', 'CSV'],
            ['ex-chart-button ex-chart-download-svg', 'SVG'],
            ['ex-chart-button ex-chart-download-png', 'PNG'],
        ]);
        expect(q(root, '.ex-chart-toolbar').getAttribute('aria-label')).toBe('Exposure profile');
        expect(q(root, '.ex-chart-reset', HTMLButtonElement).disabled).toBe(true);
        const svg = q(root, 'svg.ex-chart-svg');
        expect([svg.getAttribute('width'), svg.getAttribute('height'), svg.getAttribute('role')]).toEqual(['800', '400', 'img']);
        const title = q(svg, 'title');
        expect(svg.getAttribute('aria-labelledby')).toBe(title.id);
        expect(title.textContent).toMatch(/^Exposure profile\. Years, 0 to 4\./);
        expect(root.querySelectorAll('g.ex-chart-axes')).toHaveLength(2);
        expect([...root.querySelectorAll('g.ex-chart-series')].map(g => g.getAttribute('data-series'))).toEqual(['epe', 'pfe', 'ene']);
        expect([...root.querySelectorAll('.ex-chart-legend-entry')].map(g => [g.getAttribute('data-series'), g.getAttribute('aria-pressed')]))
            .toEqual([['epe', 'true'], ['pfe', 'true'], ['ene', 'true']]);
        expect(root.querySelectorAll('.ex-chart-hit')).toHaveLength(2);
        expect(svg.namespaceURI).toBe('http://www.w3.org/2000/svg');
        expect(q(svg, 'g.ex-chart-series path.ex-chart-path').namespaceURI).toBe('http://www.w3.org/2000/svg');
    });

    it('ADR-0252, CH-3: a series\' node count does not grow with its points', () => {
        expect(seriesNodes(10)).toBe(seriesNodes(10_000));
    });

    it('ADR-0270, ADR-0252: a pointer move reads its position and rewrites only the tooltip and the guide', () => {
        const { root } = mount({ definition: definition() });
        const drawn = [...q(root, 'svg').children].filter(c => !c.classList.contains('ex-chart-guide'));
        pointer('pointermove', at(root, 0.5));
        expect(q(root, '.ex-chart-tooltip-position').textContent).toBe('2');
        expect([...root.querySelectorAll('.ex-chart-tooltip-line')].map(l => [l.getAttribute('data-series'), q(l, '.ex-chart-tooltip-value').textContent]))
            .toEqual([['pfe', '11'], ['epe', '9']]);
        // The guide crosses every panel, and the dots stand on the pointer's panel's series.
        const guide = q(root, '.ex-chart-guide-line');
        const first = hit(root, 0);
        const second = hit(root, 1);
        expect(Number(guide.getAttribute('y1'))).toBe(Number(first.getAttribute('data-ex-y')));
        expect(Number(guide.getAttribute('y2'))).toBe(Number(second.getAttribute('data-ex-y')) + parseFloat(second.style.height));
        expect(root.querySelectorAll('.ex-chart-dot')).toHaveLength(2);
        expect([...q(root, 'svg').children].filter(c => !c.classList.contains('ex-chart-guide'))).toEqual(drawn);
        drawn.forEach((node, i) => { expect(node).toBe(drawn[i]); });

        // A move within the same position reads nothing again; a move off the plot clears it.
        const tooltip = q(root, '.ex-chart-tooltip');
        pointer('pointermove', at(root, 0.52));
        expect(q(root, '.ex-chart-tooltip')).toBe(tooltip);
        root.dispatchEvent(new PointerEvent('pointerleave'));
        expect(root.querySelector('.ex-chart-tooltip')).toBeNull();
        expect(root.querySelector('.ex-chart-guide')).toBeNull();
    });

    it('ADR-0270: past what fits the plot\'s height the tooltip lists the nearest and says how many more', () => {
        const many = new ChartDefinition({
            axis: { values: 'numbers' },
            series: Array.from({ length: 12 }, (_, i) => ({ id: `s${String(i)}`, name: `S${String(i)}`, kind: 'level' as const, points: points([i, i + 1]) })),
        });
        const { root } = mount({ definition: many, height: 160 });
        pointer('pointermove', at(root, 0));
        const listed = root.querySelectorAll('.ex-chart-tooltip-line').length;
        expect(listed).toBeLessThan(12);
        expect(q(root, '.ex-chart-tooltip-more').textContent).toBe(`and ${String(12 - listed)} more series`);
    });

    it('ADR-0271, ADR-0272: a legend click hides a series and raises its key; Enter shows it again', () => {
        const heard: SeriesVisibility[] = [];
        const { root } = mount({ definition: definition(), onSeriesVisibilityChanged: change => { heard.push(change); } });
        const ene = q(root, 'g.ex-chart-series[data-series="ene"]');
        click(q(root, '.ex-chart-legend-entry[data-series="pfe"] text'));
        expect(heard).toEqual([{ key: 'pfe', hidden: true }]);
        expect(root.querySelector('g.ex-chart-series[data-series="pfe"]')).toBeNull();
        expect(q(root, '.ex-chart-legend-entry[data-series="pfe"]').getAttribute('class')).toContain('ex-chart-hidden');
        // The other panel's series was not touched.
        expect(q(root, 'g.ex-chart-series[data-series="ene"]')).toBe(ene);
        q(root, '.ex-chart-legend-entry[data-series="pfe"]').dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
        expect(heard).toEqual([{ key: 'pfe', hidden: true }, { key: 'pfe', hidden: false }]);
        expect(root.querySelector('g.ex-chart-series[data-series="pfe"]')).not.toBeNull();
    });

    it('ADR-0272: hidden keys are taken back, kept across new data, and replaced only when the option changes', () => {
        const { root, chart } = mount({ definition: definition(), hiddenSeries: ['epe'] });
        expect(root.querySelector('g.ex-chart-series[data-series="epe"]')).toBeNull();
        click(q(root, '.ex-chart-legend-entry[data-series="ene"]'));
        // The same option again, made anew as a render makes it, leaves what the user did.
        chart.update({ definition: definition(), hiddenSeries: ['epe'] });
        expect([...root.querySelectorAll('g.ex-chart-series')].map(g => g.getAttribute('data-series'))).toEqual(['pfe']);
        chart.update({ definition: definition(), hiddenSeries: [] });
        expect([...root.querySelectorAll('g.ex-chart-series')].map(g => g.getAttribute('data-series'))).toEqual(['epe', 'pfe', 'ene']);
    });

    it('ADR-0273, ADR-0272: a drag narrows the range in the axis\' values, and Reset range and a double-click return it', () => {
        const heard: (ChartRange | null)[] = [];
        const { root } = mount({ definition: definition(), onRangeChanged: range => { heard.push(range); } });
        pointer('pointermove', at(root, 0.25));
        pointer('pointerdown', at(root, 0.25));
        pointer('pointermove', at(root, 0.75));
        const band = q(root, '.ex-chart-band');
        expect(Number(band.getAttribute('width'))).toBeGreaterThan(0);
        pointer('pointerup', at(root, 0.75));
        expect(heard).toEqual([{ from: 1, to: 3 }]);
        expect(root.querySelector('.ex-chart-band')).toBeNull();
        expect(q(root, 'title').textContent).toContain('Years, 1 to 3.');
        expect(q(root, '.ex-chart-reset', HTMLButtonElement).disabled).toBe(false);
        hit(root).dispatchEvent(new MouseEvent('dblclick', { bubbles: true }));
        expect(heard).toEqual([{ from: 1, to: 3 }, null]);
        expect(q(root, '.ex-chart-reset', HTMLButtonElement).disabled).toBe(true);
    });

    it('ADR-0272, CH-5: a drag begun before the data is replaced is refused when it ends', () => {
        const heard: (ChartRange | null)[] = [];
        const { root, chart } = mount({ definition: definition(), onRangeChanged: range => { heard.push(range); } });
        pointer('pointermove', at(root, 0.25));
        pointer('pointerdown', at(root, 0.25));
        // Tomorrow's run: one value moved.
        const next = definition({ series: [{ id: 'epe', name: 'EPE', kind: 'level', points: points([1, 5, 9, 4, 3]), panel: 'EPE' }] });
        chart.update({ definition: next, onRangeChanged: range => { heard.push(range); } });
        pointer('pointermove', at(root, 0.75));
        expect(root.querySelector('.ex-chart-band')).toBeNull();
        pointer('pointerup', at(root, 0.75));
        expect(heard).toEqual([]);
    });

    it('ADR-0258: a click names the nearest series that has a value there, at the position clicked', () => {
        const clicks: ChartPointClick[] = [];
        const { root } = mount({ definition: definition(), onPointClick: c => { clicks.push(c); } });
        // Near the top of the first panel at position 2, where PFE's 11 stands above EPE's 9.
        pointer('pointermove', at(root, 0.5, 0.05));
        pointer('pointerdown', at(root, 0.5, 0.05));
        pointer('pointerup', at(root, 0.5, 0.05));
        expect(clicks).toEqual([{ seriesId: 'pfe', position: 2 }]);
    });

    it('ADR-0273: a typed range narrows the chart, and a value the axis does not hold is said, never guessed', () => {
        const heard: (ChartRange | null)[] = [];
        const { root } = mount({ definition: definition(), onRangeChanged: range => { heard.push(range); } });
        click(q(root, '.ex-chart-filters-toggle'));
        expect(q(root, '.ex-chart-filters-toggle').getAttribute('aria-expanded')).toBe('true');
        const from = q(root, '.ex-chart-from', HTMLInputElement);
        const to = q(root, '.ex-chart-to', HTMLInputElement);
        expect(from.placeholder).toBe('number');
        from.value = 'one';
        from.dispatchEvent(new Event('input', { bubbles: true }));
        click(q(root, '.ex-chart-apply'));
        expect(q(root, '.ex-chart-filter-error').textContent).toBe('“one” is not a number on this axis.');
        expect(heard).toEqual([]);
        from.value = '3';
        from.dispatchEvent(new Event('input', { bubbles: true }));
        to.value = '1';
        to.dispatchEvent(new Event('input', { bubbles: true }));
        click(q(root, '.ex-chart-apply'));
        expect(q(root, '.ex-chart-filter-error').textContent).toBe('From is after To.');
        to.value = ' 4 ';
        to.dispatchEvent(new Event('input', { bubbles: true }));
        click(q(root, '.ex-chart-apply'));
        expect(heard).toEqual([{ from: 3, to: 4 }]);
        expect(root.querySelector('.ex-chart-filter-error')).toBeNull();
        // A drag writes the range it set into the fields.
        drag(root, 0.0, 1.0);
        expect([from.value, to.value]).toEqual(['3', '4']);
    });

    it('ADR-0273: a date axis reads yyyy-MM-dd and refuses a day its month does not have', () => {
        const heard: (ChartRange | null)[] = [];
        const dates = new ChartDefinition({
            axis: { values: 'dates' },
            series: [{ id: 's', name: 'S', kind: 'level', points: [{ x: 739_617, y: 1 }, { x: 739_700, y: 2 }] }],
        });
        const { root } = mount({ definition: dates, onRangeChanged: range => { heard.push(range); } });
        click(q(root, '.ex-chart-filters-toggle'));
        const from = q(root, '.ex-chart-from', HTMLInputElement);
        from.value = '2026-02-30';
        from.dispatchEvent(new Event('input', { bubbles: true }));
        click(q(root, '.ex-chart-apply'));
        expect(q(root, '.ex-chart-filter-error').textContent).toBe('“2026-02-30” is not a date (yyyy-MM-dd) on this axis.');
        from.value = '2026-03-01';
        from.dispatchEvent(new Event('input', { bubbles: true }));
        click(q(root, '.ex-chart-apply'));
        expect(heard).toEqual([{ from: 739_675 }]);
    });

    it('ADR-0251: the axis switch is raised, and offered only where the axis can be switched', () => {
        const heard: string[] = [];
        const { root } = mount({ definition: definition(), onScaleChanged: scale => { heard.push(scale); } });
        click(q(root, '.ex-chart-filters-toggle'));
        const select = q(root, '.ex-chart-scale', HTMLSelectElement);
        expect(select.value).toBe('Proportional');
        select.value = 'Categories';
        select.dispatchEvent(new Event('change', { bubbles: true }));
        expect(heard).toEqual(['categories']);
        const labels = new ChartDefinition({ axis: { values: 'labels', labels: ['a', 'b'] }, series: [{ id: 's', name: 'S', kind: 'level', points: points([1, 2]) }] });
        const other = mount({ definition: labels }).root;
        click(q(other, '.ex-chart-filters-toggle'));
        expect(other.querySelector('.ex-chart-scale')).toBeNull();
    });

    it('ADR-0275: View as table holds the chart\'s data under its title', () => {
        const { root } = mount({ definition: definition(), title: 'Exposure profile' });
        click(q(root, '.ex-chart-table-toggle'));
        expect(q(root, '.ex-chart-table caption').textContent).toBe('Exposure profile');
        expect([...root.querySelectorAll('.ex-chart-table thead th')].map(th => th.textContent)).toEqual(['Years', 'EPE', 'PFE', 'ENE']);
        expect([...q(root, '.ex-chart-table tbody tr').children].map(c => [c.tagName, c.textContent])).toEqual([['TH', '0'], ['TD', '1'], ['TD', '2'], ['TD', '-1']]);
        click(q(root, '.ex-chart-table-toggle'));
        expect(root.querySelector('.ex-chart-table')).toBeNull();
    });

    it('ADR-0344, spine rule 1: a chart its Chart Source thinned saves no CSV and shows no table of the points it holds', async () => {
        const thinned = new ChartDefinition({
            axis: { values: 'numbers' },
            series: [{ id: 's', name: 'S', kind: 'level', points: points([1, 5]), thinned: [{ from: 0, to: 1, min: 1, max: 5, count: 400 }] }],
        });
        const { root, chart } = mount({ definition: thinned });
        const csv = q(root, '.ex-chart-download-csv', HTMLButtonElement);
        expect(csv.disabled).toBe(true);
        expect(csv.title).toContain('where the data lives');
        await expect(chart.download('csv')).resolves.toBe(false);
        click(q(root, '.ex-chart-table-toggle'));
        expect(root.querySelector('.ex-chart-table')).toBeNull();
        expect(q(root, '.ex-chart-table-notice').textContent).toContain('where the data lives');
    });

    it('ADR-0281: the context menu opens where it was asked for, runs a command and closes; Escape closes it', () => {
        const { root } = mount({ definition: definition() });
        hit(root).dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, clientX: 120, clientY: 80, cancelable: true }));
        const menu = q(root, '.ex-chart-menu', HTMLElement);
        expect(menu.getAttribute('style')).toBe('left: 120px; top: 80px;');
        expect(document.activeElement).toBe(menu);
        expect([...menu.querySelectorAll('button')].map(b => [b.getAttribute('data-ex-command'), b.textContent, b.disabled])).toEqual([
            ['exchart.reset-range', 'Reset range', true],
            ['exchart.table', 'View as table', false],
            ['exchart.download.csv', 'Download CSV', false],
            ['exchart.download.svg', 'Download SVG', false],
            ['exchart.download.png', 'Download PNG', false],
        ]);
        click(q(menu, '[data-ex-command="exchart.table"]'));
        expect(root.querySelector('.ex-chart-menu')).toBeNull();
        expect(root.querySelector('.ex-chart-table')).not.toBeNull();
        hit(root).dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true }));
        expect(q(root, '[data-ex-command="exchart.table"]').textContent).toBe('Hide table');
        q(root, '.ex-chart-menu').dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
        expect(root.querySelector('.ex-chart-menu')).toBeNull();
    });

    it('ADR-0282: the CSV and SVG are saved on the click, the SVG being the drawing with its stylesheet and no pointer', async () => {
        const saved: { name: string; blob: Blob }[] = [];
        const created = vi.spyOn(URL, 'createObjectURL').mockImplementation((blob: Blob | MediaSource) => {
            if (blob instanceof Blob) {
                saved.push({ name: '', blob });
            }
            return 'blob:chart';
        });
        vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
        const names: string[] = [];
        const anchor = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) { names.push(this.download); });
        const { root, chart } = mount({ definition: definition({
            series: [{ id: 'epe', name: 'EPE <before> & after', kind: 'level', points: points([1, 5, 9]) }],
        }), title: 'Exposure profile', fileName: 'epe/ene' });
        pointer('pointermove', at(root, 0.5));
        await expect(chart.download('csv')).resolves.toBe(true);
        await expect(chart.download('svg')).resolves.toBe(true);
        expect(names).toEqual(['epe_ene.csv', 'epe_ene.svg']);
        const [csv, svg] = await Promise.all(saved.map(s => s.blob.text()));
        expect(csv?.startsWith('﻿Years,EPE <before> & after\r\n')).toBe(true);
        expect(saved[1]?.blob.type).toBe('image/svg+xml');
        // happy-dom's XML parser refuses CDATA, so the stylesheet is checked as text and parsed without it.
        expect(svg).toMatch(/<style><!\[CDATA\[[^]*\.ex-chart-path[^]*\]\]><\/style>/);
        if (svg === undefined) {
            throw new Error('No SVG was saved.');
        }
        const doc = new DOMParser().parseFromString(svg.replace(/<style>[^]*?<\/style>/, '<style/>'), 'image/svg+xml');
        const file = doc.documentElement;
        expect([file.tagName, file.getAttribute('width'), file.getAttribute('class')]).toEqual(['svg', '800', 'ex-chart ex-chart-svg ex-chart-file']);
        expect(file.querySelector('title')?.textContent).toMatch(/^Exposure profile\./);
        expect([...file.querySelectorAll('text')].some(t => t.textContent === 'EPE <before> & after')).toBe(true);
        expect(file.querySelectorAll('.ex-chart-series')).toHaveLength(1);
        expect(file.querySelector('.ex-chart-guide')).toBeNull();
        created.mockRestore();
        anchor.mockRestore();
    });

    it('ADR-0282: a file name names a file, and the theme on screen is written on a file\'s root', () => {
        expect(fileName('a/b:c', undefined, 'csv')).toBe('a_b_c.csv');
        expect(fileName('..', undefined, 'csv')).toBe('chart.csv');
        expect(fileName(' . ', undefined, 'csv')).toBe('chart.csv');
        expect(fileName('Report.', undefined, 'csv')).toBe('Report.csv');
        expect(fileName(undefined, 'Exposure', 'png')).toBe('Exposure.png');
        expect(tokenNames).toContain('colour-0');
        const style = themeStyle([...tokenNames.map(name => (name === 'colour-0' ? 'rgb(89, 74, 226)' : name === 'colour-1' ? 'red;}' : '')), 'Roboto, sans-serif']);
        expect(style).toContain('--ex-chart-colour-0:rgb(89, 74, 226);');
        expect(style).toContain('font-family:Roboto, sans-serif;');
        expect(style).not.toContain('colour-1');
    });

    it('ADR-0370 section 10: the built-in words are English by default and Japanese in one line, a Consumer\'s winning', () => {
        const { root, chart } = mount({ definition: definition(), label: japaneseChartWords });
        expect(q(root, '.ex-chart-filters-toggle').textContent).toBe('グラフ フィルター');
        expect(q(root, '.ex-chart-toolbar').getAttribute('aria-label')).toBe('グラフ');
        chart.update({ definition: definition(), label: id => (id === 'exchart.filters' ? 'Filters' : japaneseChartWords(id)) });
        expect(q(root, '.ex-chart-filters-toggle').textContent).toBe('Filters');
        expect(q(root, '.ex-chart-reset').textContent).toBe('範囲をリセット');
    });

    it('ADR-0290: on a scatter the pointer reads the nearest mark, a click raises its row and a rectangle narrows both axes', () => {
        const clicks: ChartPointClick[] = [];
        const ranges: (ChartRange | null)[] = [];
        const scatter = new ChartDefinition({
            axis: { values: 'numbers', title: 'Duration' },
            series: [{ id: 'bonds', name: 'Bonds', kind: 'observation', points: [
                { x: 0, y: 0, name: 'A', row: 'a' }, { x: 10, y: 10, name: 'B', row: 'b' }, { x: 5, y: 2, name: 'C', row: 'c' },
            ] }],
        });
        const { root, chart } = mount({ definition: scatter, onPointClick: c => { clicks.push(c); }, onRangeChanged: r => { ranges.push(r); } });
        const marks = root.querySelectorAll('g.ex-chart-series');
        expect(marks).toHaveLength(1);
        // B stands at the plot's top right corner.
        pointer('pointermove', at(root, 1, 0));
        expect(q(root, '.ex-chart-tooltip-position').textContent).toBe('B');
        pointer('pointerdown', at(root, 1, 0));
        pointer('pointerup', at(root, 1, 0));
        expect(clicks).toEqual([{ seriesId: 'bonds', position: 10, row: 'b' }]);
        // A rectangle from the middle to the bottom left corner.
        pointer('pointerdown', at(root, 0.5, 0.5));
        pointer('pointermove', at(root, 0, 1));
        expect(q(root, '.ex-chart-band')).not.toBeNull();
        pointer('pointerup', at(root, 0, 1));
        const [range] = ranges;
        expect(ranges).toHaveLength(1);
        expect(range?.from).toBeLessThan(0.5);
        expect(range?.to).toBeGreaterThan(4.5);
        expect(range?.yFrom).toBeLessThan(0.5);
        expect(range?.yTo).toBeGreaterThan(4.5);
        // A release against another drawing is refused.
        pointer('pointerdown', at(root, 0.5, 0.5));
        chart.update({ definition: new ChartDefinition({ axis: { values: 'numbers' }, series: [{ id: 'bonds', name: 'Bonds', kind: 'observation', points: [{ x: 1, y: 1 }, { x: 2, y: 3 }] }] }) });
        pointer('pointerup', at(root, 0.1, 0.1));
        expect(ranges).toHaveLength(1);
    });

    it('ADR-0321: over an edge the tooltip says its two keys', () => {
        const network = new ChartDefinition({
            axis: { values: 'numbers' },
            series: [{ id: 'n', name: 'Network', kind: 'observation', points: [{ x: 0, y: 0, row: 'a' }, { x: 10, y: 10, row: 'b' }], edges: [{ from: 'a', to: 'b' }] }],
        });
        const { root } = mount({ definition: network });
        pointer('pointermove', at(root, 0.5, 0.5));
        expect(q(root, '.ex-chart-tooltip-position').textContent).toBe('a → b');
    });

    it('ADR-0300: candles and volume are drawn rising, falling and unchanged', () => {
        const monday = dayNumber(2026, 1, 5);
        const prices = new ChartDefinition({
            axis: { values: 'dates' },
            series: [{ id: 'p', name: 'Price', kind: 'priceRange', bars: [
                { x: monday, open: 1, high: 3, low: 0.5, close: 2, volume: 10 },
                { x: monday + 1, open: 2, high: 2.5, low: 1, close: 1.5, volume: 12 },
                { x: monday + 2, open: 1.5, high: 2, low: 1, close: 1.5, volume: 8 },
            ] }],
        });
        const { root } = mount({ definition: prices });
        expect(root.querySelectorAll('.ex-chart-rising').length).toBeGreaterThan(0);
        expect(root.querySelectorAll('.ex-chart-falling').length).toBeGreaterThan(0);
        expect(root.querySelectorAll('.ex-chart-unchanged').length).toBeGreaterThan(0);
        expect(root.querySelectorAll('g.ex-chart-axes')).toHaveLength(2);
    });

    it('ADR-0253: the scene\'s Notes are said under the chart', () => {
        const noted = definition({ notes: ['Exposure at 95%.'] });
        const { root, chart } = mount({ definition: noted });
        expect([...q(root, '.ex-chart-notes').children].map(li => li.textContent)).toContain('Exposure at 95%.');
        chart.update({ definition: definition() });
        expect(root.querySelector('.ex-chart-notes')).toBeNull();
    });

    it('ADR-0370: an untyped caller\'s options are refused at the boundary, and a destroyed chart empties its element', () => {
        const root = document.createElement('div');
        // @ts-expect-error -- an untyped caller with no definition
        expect(() => createChart(root, {})).toThrow(TypeError);
        expect(() => createChart(root, { definition: definition(), width: -1 })).toThrow(RangeError);
        // @ts-expect-error -- an untyped caller's handler that is not a function
        expect(() => createChart(root, { definition: definition(), onPointClick: 'no' })).toThrow(TypeError);
        const { root: drawn, chart } = mount({ definition: definition() });
        chart.destroy();
        expect(drawn.childElementCount).toBe(0);
        expect(drawn.className).toBe('');
        expect(() => { chart.update({ definition: definition() }); }).toThrow(Error);
    });
});
