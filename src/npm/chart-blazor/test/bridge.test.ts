// @vitest-environment happy-dom
// The bridge's own tests (ADR-0370 section 1): what crosses between .NET and ExChart's Script Core. What the chart
// draws and decides is the Script Core's, tested there; here, that a value .NET wrote another way is written by the
// text .NET sent and every other as the Script Core writes it, that a surface axis' writing reaches its ticks as C#
// writes them, that a click carries back the Row Key and the definition it was taken on, and that nothing crosses
// once the component has let go.
import { afterEach, describe, expect, it } from 'vitest';
import { attachChart, attachSurface, type DotNetHandle, type WireChart, type WireChartOptions } from '../src/blazor-chart';
import { readDefinition, readSurface, wireNumber, type WireDefinition, type WireSeries, type WireSurfaceGrid } from '../src/wire';

class FakeDotNet implements DotNetHandle {
    readonly heard: { readonly kind: unknown; readonly payload: unknown }[] = [];

    invokeMethodAsync(method: string, ...args: readonly unknown[]): Promise<unknown> {
        if (method !== 'Heard') throw new Error(`the bridge called ${method}`);
        this.heard.push({ kind: args[0], payload: args[1] });
        return Promise.resolve(null);
    }
}

function series(overrides: Partial<WireSeries> = {}): WireSeries {
    return {
        id: 'bonds', name: 'Bonds', kind: 'observation', step: 'following', axis: 'left', panel: '', formatCode: null, keys: null, fromZero: null, interpolated: false,
        segments: [], x: [0, 10, 5], y: [0, 10, 2], names: ['A', 'B', 'C'], rows: ['a', 7, true], colourBy: null, sizeBy: null, details: [],
        edges: [], bars: null, mergeTo: null, interval: null, xInterval: null, thinned: [], texts: null,
        ...overrides,
    };
}

function definition(s: WireSeries = series()): WireDefinition {
    return {
        axis: { values: 'numbers', title: '', scale: null, labels: [], named: [], daysPerYear: 365, packed: false, formatCode: null },
        series: [s],
        notes: [],
    };
}

const options: WireChartOptions = {
    width: 800, height: 400, density: 'standard', title: null, description: null, legend: null, axisStyle: null, independentScales: null,
    colourSlots: 10, hiddenSeries: null, range: null, scale: null, yScale: null, fileName: null, hears: ['pointClick'],
};

const roots: HTMLElement[] = [];

afterEach(() => {
    roots.splice(0);
    document.body.replaceChildren();
});

function root(): HTMLElement {
    const element = document.createElement('div');
    document.body.append(element);
    roots.push(element);
    return element;
}

// A press and a release at a share of the plot, on the hit layer, as the browser reports them.
function clickAt(element: HTMLElement, across: number, down: number): void {
    const layer = element.querySelector('.ex-chart-hit');
    if (!(layer instanceof HTMLElement)) throw new Error('No hit layer.');
    const at = { offsetX: parseFloat(layer.style.width) * across, offsetY: parseFloat(layer.style.height) * down, bubbles: true, pointerId: 1, button: 0 };
    for (const type of ['pointermove', 'pointerdown', 'pointerup']) layer.dispatchEvent(new PointerEvent(type, at));
}

describe('a definition .NET wrote', () => {
    it('ADR-0370: a value .NET wrote another way is written by its text, and every other as the Script Core writes it', () => {
        const read = readDefinition(definition(series({ texts: [[2, '2.00']] })));
        const [bonds] = read.series;
        expect(bonds?.format?.(2)).toBe('2.00');
        expect(bonds?.format?.(10)).toBe('10');
        expect(bonds?.format?.(0.1 + 0.2)).toBe('0.30000000000000004');
        expect(readDefinition(definition()).series[0]?.format).toBeUndefined();
    });

    it('ADR-0420: an axis\' and a series\' format codes cross as themselves, for the Script Core\'s reader to write by', () => {
        const wire = definition(series({ formatCode: '#,##0.00' }));
        const read = readDefinition({ ...wire, axis: { ...wire.axis, formatCode: '0.0"y"' } });
        expect(read.axis.formatCode).toBe('0.0"y"');
        expect(read.series[0]?.formatCode).toBe('#,##0.00');
        expect(readDefinition(definition()).axis.formatCode).toBeUndefined();
    });

    it('ADR-0370: a number JSON cannot hold crosses by its name', () => {
        expect(wireNumber('NaN')).toBeNaN();
        expect(wireNumber('-Infinity')).toBe(-Infinity);
        expect(() => wireNumber('one' as never)).toThrow(TypeError);
    });

    it('ADR-0370: columns of different lengths are a defect of the writer, said by name', () => {
        expect(() => readDefinition(definition(series({ y: [1] })))).toThrow(/'Bonds' with columns of different lengths/u);
    });
});

function grid(writing: WireSurfaceGrid['x']['writing']): WireSurfaceGrid {
    return {
        form: 'grid',
        x: { title: 'Strike', values: [0.5, 1.25, 2], scale: 'proportional', writing },
        y: { title: 'Tenor', values: ['1M', '1Y'], scale: 'categories', writing: null },
        z: [[1, 2, 3], [4, 5, 6]],
        method: null, interpolated: null, zTitle: 'Vol', zScale: 'proportional', centre: null, zTexts: null, notes: [],
    };
}

describe('a surface .NET wrote', () => {
    it('ADR-0370: a plain axis writes a value as it writes a tick, as a sampled surface\'s axis does in C#', () => {
        const { x } = readSurface(grid('plain'));
        expect(x.writeValue(1.25)).toBe(x.writeTick(1.25));
        expect(x.writeTick(1.123456)).toBe('1.1235');
    });

    it('ADR-0370: an axis written by texts writes its values by them, and a tick between them plainly', () => {
        const { x } = readSurface(grid([[0.5, '0.50'], [1.25, '1.25'], [2, '2.00']]));
        expect(x.writeValue(0.5)).toBe('0.50');
        expect(x.writeTick(1.123456)).toBe('1.1235');
        expect(readSurface(grid(null)).x.writeValue(0.5)).toBe('0.5');
    });

    it('ADR-0370: a mesh\'s plain writing reaches its axes', () => {
        const mesh = readSurface({
            form: 'triangles', xTitle: 'x', yTitle: 'y', xScale: 'proportional', yScale: 'proportional', xWriting: 'plain', yWriting: null,
            nodes: [[0, 0, 1], [1, 0, 2], [0, 1, 3]], triangles: [[0, 1, 2]], zTitle: 'z', zScale: 'proportional', centre: null, zTexts: null, notes: [],
        });
        expect(mesh.x.writeValue(1)).toBe(mesh.x.writeTick(1));
        expect(mesh.y.format).toBeUndefined();
    });
});

describe('the chart the component attached', () => {
    it('ADR-0258, ADR-0370: a click carries back the Row Key .NET sent and the definition it was taken on', () => {
        const dotnet = new FakeDotNet();
        const element = root();
        const wire: WireChart = { options, definition: definition(), generation: 1 };
        const chart = attachChart(element, dotnet, JSON.stringify(wire));
        // B stands at the plot's top right corner.
        clickAt(element, 1, 0);
        expect(dotnet.heard).toEqual([{ kind: 'pointClick', payload: { generation: 1, seriesId: 'bonds', position: 10, row: 7 } }]);
        // Another definition: a click on it carries its generation.
        chart.update(JSON.stringify({ options, definition: definition(series({ rows: ['a', 'b', 'c'] })), generation: 2 } satisfies WireChart));
        clickAt(element, 1, 0);
        expect(dotnet.heard.at(-1)).toEqual({ kind: 'pointClick', payload: { generation: 2, seriesId: 'bonds', position: 10, row: 'b' } });
        // The parameters again, and the definition the chart has: still the second.
        chart.update(JSON.stringify({ options: { ...options, title: 'Bonds' } } satisfies WireChart));
        clickAt(element, 1, 0);
        expect(dotnet.heard.at(-1)).toMatchObject({ payload: { generation: 2 } });
        chart.dispose();
        expect(element.childElementCount).toBe(0);
    });

    it('ADR-0370: an event .NET has no handler for does not cross, and nothing crosses once the component let go', () => {
        const dotnet = new FakeDotNet();
        const element = root();
        const chart = attachChart(element, dotnet, JSON.stringify({ options: { ...options, hears: [] }, definition: definition(), generation: 1 } satisfies WireChart));
        clickAt(element, 1, 0);
        expect(dotnet.heard).toEqual([]);
        chart.update(JSON.stringify({ options } satisfies WireChart));
        chart.dispose();
        expect(dotnet.heard).toEqual([]);
        expect(() => attachChart(root(), dotnet, JSON.stringify({ options }))).toThrow(/without its definition/u);
    });

    it('ADR-0282: a file of a format .NET does not name is a defect, said by name', () => {
        const chart = attachChart(root(), new FakeDotNet(), JSON.stringify({ options, definition: definition(), generation: 1 } satisfies WireChart));
        expect(() => chart.download('pdf' as never)).toThrow(/the format "pdf"/u);
        chart.dispose();
    });
});

describe('the surface the component attached', () => {
    it('ADR-0370: the surface draws, and its SVG text is there until it stops', () => {
        const element = root();
        const surface = attachSurface(element, new FakeDotNet(), JSON.stringify({
            options: { width: 640, height: 420, sliceWidth: 420, density: 'standard', title: null, description: null, view: null, slice: null, fileName: null, hears: [] },
            definition: {
                form: 'grid',
                x: { title: 'x', values: [1, 2], scale: 'categories', writing: null },
                y: { title: 'y', values: [1, 2], scale: 'categories', writing: null },
                z: [[1, 2], [3, 4]],
                method: null, interpolated: null, zTitle: 'z', zScale: 'proportional', centre: null, zTexts: null, notes: [],
            },
            generation: 1,
        }));
        expect(surface.svgText()).toContain('<svg');
        surface.dispose();
        expect(surface.svgText()).toBeNull();
    });
});
