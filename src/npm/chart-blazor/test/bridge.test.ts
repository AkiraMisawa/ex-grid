// @vitest-environment happy-dom
// The bridge's own tests (ADR-0370 section 1): what crosses between .NET and ExChart's Script Core. What the chart
// draws and decides is the Script Core's, tested there; here, that a value .NET wrote another way is written by the
// text .NET sent and every other as the Script Core writes it, that a surface axis' writing reaches its ticks as C#
// writes them, that a click carries back the Row Key and the definition it was taken on, and that nothing crosses
// once the component has let go.
import { afterEach, describe, expect, it } from 'vitest';
import { attachChart, attachSurface, type DotNetHandle, type WireChart, type WireChartOptions } from '../src/blazor-chart';
import { interpolateNaturalCubicSpline } from '@ex-grid/chart';
import { ScriptFunctionError, type ScriptCalls, type WireScript } from '../src/scripts';
import { readDefinition, readSurface, scriptsOf, wireNumber, type WireDefinition, type WireSeries, type WireSurfaceGrid } from '../src/wire';

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
        id: 'bonds', name: 'Bonds', kind: 'observation', step: 'following', axis: 'left', panel: '', formatCode: null, formatScript: null, keys: null, fromZero: null, interpolated: false,
        segments: [], sampler: null, x: [0, 10, 5], y: [0, 10, 2], names: ['A', 'B', 'C'], rows: ['a', 7, true], colourBy: null, sizeBy: null, details: [],
        edges: [], bars: null, mergeTo: null, interval: null, xInterval: null, thinned: [], texts: null,
        ...overrides,
    };
}

function definition(s: WireSeries = series()): WireDefinition {
    return {
        axis: { values: 'numbers', title: '', scale: null, labels: [], named: [], daysPerYear: 365, packed: false, formatCode: null, formatScript: null },
        series: [s],
        notes: [],
    };
}

const options: WireChartOptions = {
    width: 800, height: 400, density: 'standard', title: null, description: null, legend: null, axisStyle: null, independentScales: null,
    colourSlots: 10, hiddenSeries: null, range: null, scale: null, yScale: null, fileName: null, hears: ['pointClick'], razorSlots: null,
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

    it('ADR-0420: a series sampled from the family\'s own curve crosses as what it was made from, and is made again', () => {
        const spline = readDefinition(definition(series({
            kind: 'level', x: [], y: [], names: null, rows: null, interpolated: true,
            sampler: { from: 0, to: 2, madeFrom: { kind: 'naturalCubicSpline', xs: [0, 1, 2], ys: [0, 1, 0] } },
        }))).series[0]?.sampler;
        expect(spline?.valueAt(0.5)).toBe(interpolateNaturalCubicSpline([0, 1, 2], [0, 1, 0]).valueAt(0.5));
        expect(spline?.valueAt(3)).toBeNull();
        const fitted = readDefinition(definition(series({
            kind: 'level', x: [], y: [], names: null, rows: null,
            sampler: { from: 1, to: 4, madeFrom: { kind: 'fit', method: 'OLS', coefficients: [1, 2], rSquared: 'NaN', count: 4, from: 1, to: 4, space: 'logY' } },
        }))).series[0]?.sampler;
        expect(fitted?.valueAt(2)).toBe(Math.exp(5));
        const linear = readDefinition(definition(series({
            kind: 'level', x: [], y: [], names: null, rows: null,
            sampler: { from: 0, to: 2, madeFrom: { kind: 'linear', xs: [0, 1, 2], ys: [0, 1, 0] } },
        }))).series[0]?.sampler;
        expect(linear?.valueAt(0.5)).toBe(0.5);
        expect(() => readDefinition(definition(series({
            kind: 'level', x: [], y: [], names: null, rows: null,
            sampler: { from: 0, to: 2, madeFrom: { kind: 'pchip' as never, xs: [0, 1, 2], ys: [0, 1, 0] } },
        })))).toThrow(/"pchip", which has no twin/u);
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
        x: { title: 'Strike', values: [0.5, 1.25, 2], scale: 'proportional', writing, formatCode: null, formatScript: null },
        y: { title: 'Tenor', values: ['1M', '1Y'], scale: 'categories', writing: null, formatCode: null, formatScript: null },
        z: [[1, 2, 3], [4, 5, 6]],
        method: null, interpolated: null, zTitle: 'Vol', zScale: 'proportional', centre: null, zTexts: null, zFormatCode: null, zFormatScript: null, notes: [],
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

    it('ADR-0420: a surface\'s codes cross as themselves, and write its axis\' ticks and its values', () => {
        const wire = grid(null);
        const read = readSurface({ ...wire, x: { ...wire.x, formatCode: '0.00' }, zFormatCode: '0.0%' });
        expect(read.x.writeTick(1.123456)).toBe('1.12');
        expect(read.writeZ(0.25)).toBe('25.0%');
    });

    it('ADR-0370: a mesh\'s plain writing reaches its axes', () => {
        const mesh = readSurface({
            form: 'triangles', xTitle: 'x', yTitle: 'y', xScale: 'proportional', yScale: 'proportional', xWriting: 'plain', yWriting: null,
            xFormatCode: null, yFormatCode: null, xFormatScript: null, yFormatScript: null,
            nodes: [[0, 0, 1], [1, 0, 2], [0, 1, 3]], triangles: [[0, 1, 2]], zTitle: 'z', zScale: 'proportional', centre: null, zTexts: null,
            zFormatCode: null, zFormatScript: null, notes: [],
        });
        expect(mesh.x.writeValue(1)).toBe(mesh.x.writeTick(1));
        expect(mesh.y.format).toBeUndefined();
    });
});

describe('the chart the component attached', () => {
    it('ADR-0258, ADR-0370: a click carries back the Row Key .NET sent and the definition it was taken on', async () => {
        const dotnet = new FakeDotNet();
        const element = root();
        const wire: WireChart = { circuit: false, options, definition: definition(), generation: 1 };
        const chart = await attachChart(element, dotnet, JSON.stringify(wire));
        // B stands at the plot's top right corner.
        clickAt(element, 1, 0);
        expect(dotnet.heard).toEqual([{ kind: 'pointClick', payload: { generation: 1, seriesId: 'bonds', position: 10, row: 7 } }]);
        // Another definition: a click on it carries its generation.
        await chart.update(JSON.stringify({ circuit: false, options, definition: definition(series({ rows: ['a', 'b', 'c'] })), generation: 2 } satisfies WireChart));
        clickAt(element, 1, 0);
        expect(dotnet.heard.at(-1)).toEqual({ kind: 'pointClick', payload: { generation: 2, seriesId: 'bonds', position: 10, row: 'b' } });
        // The parameters again, and the definition the chart has: still the second.
        await chart.update(JSON.stringify({ circuit: false, options: { ...options, title: 'Bonds' } } satisfies WireChart));
        clickAt(element, 1, 0);
        expect(dotnet.heard.at(-1)).toMatchObject({ payload: { generation: 2 } });
        chart.dispose();
        expect(element.childElementCount).toBe(0);
    });

    it('ADR-0370: an event .NET has no handler for does not cross, and nothing crosses once the component let go', async () => {
        const dotnet = new FakeDotNet();
        const element = root();
        const chart = await attachChart(element, dotnet, JSON.stringify({ circuit: false, options: { ...options, hears: [] }, definition: definition(), generation: 1 } satisfies WireChart));
        clickAt(element, 1, 0);
        expect(dotnet.heard).toEqual([]);
        await chart.update(JSON.stringify({ circuit: false, options } satisfies WireChart));
        chart.dispose();
        expect(dotnet.heard).toEqual([]);
        await expect(attachChart(root(), dotnet, JSON.stringify({ circuit: false, options }))).rejects.toThrow(/without its definition/u);
    });

    it('ADR-0282: a file of a format .NET does not name is a defect, said by name', async () => {
        const chart = await attachChart(root(), new FakeDotNet(), JSON.stringify({ circuit: false, options, definition: definition(), generation: 1 } satisfies WireChart));
        expect(() => chart.download('pdf' as never)).toThrow(/the format "pdf"/u);
        chart.dispose();
    });
});

describe('the surface the component attached', () => {
    it('ADR-0370: the surface draws, and its SVG text is there until it stops', async () => {
        const element = root();
        const surface = await attachSurface(element, new FakeDotNet(), JSON.stringify({
            options: { width: 640, height: 420, sliceWidth: 420, density: 'standard', title: null, description: null, view: null, slice: null, fileName: null, hears: [] },
            definition: {
                form: 'grid',
                x: { title: 'x', values: [1, 2], scale: 'categories', writing: null, formatCode: null, formatScript: null },
                y: { title: 'y', values: [1, 2], scale: 'categories', writing: null, formatCode: null, formatScript: null },
                z: [[1, 2], [3, 4]],
                method: null, interpolated: null, zTitle: 'z', zScale: 'proportional', centre: null, zTexts: null, zFormatCode: null, zFormatScript: null, notes: [],
            },
            generation: 1,
        }));
        expect(surface.svgText()).toContain('<svg');
        surface.dispose();
        expect(surface.svgText()).toBeNull();
    });
});

function calls(functions: Readonly<Record<string, (...args: never[]) => unknown>>, failures: ScriptFunctionError[] = [], thrown = true): ScriptCalls {
    return {
        functionOf: script => {
            const f = functions[script.export];
            if (f === undefined) throw new Error(`no ${script.export}`);
            return f;
        },
        failed: error => {
            failures.push(error);
            return thrown;
        },
    };
}

describe('the Consumer\'s own script', () => {
    const tenor: WireScript = { module: './js/chart-formats.js', export: 'tenor' };

    it('ADR-0420: a script function writes where a code would, and samples where a sampler would', () => {
        const wire = definition(series({ formatScript: { module: tenor.module, export: 'percent' } }));
        const read = readDefinition({ ...wire, axis: { ...wire.axis, formatScript: tenor }, series: [...wire.series, series({
            id: 'curve', name: 'Curve', kind: 'level', x: [], y: [], names: null, rows: null,
            sampler: { from: 0, to: 10, madeFrom: { kind: 'script', module: tenor.module, export: 'square' } },
        })] }, calls({
            tenor: (x: number) => `${String(x)}y`,
            percent: (v: number) => `${String(v * 100)}%`,
            square: (x: number) => x * x,
        }));
        expect(read.axis.format?.(5)).toBe('5y');
        expect(read.series[0]?.format?.(2)).toBe('200%');
        expect(read.series[1]?.sampler?.valueAt(3)).toBe(9);
        expect(scriptsOf({ ...wire, axis: { ...wire.axis, formatScript: tenor } })).toEqual([tenor, { module: tenor.module, export: 'percent' }]);
    });

    it('ADR-0420: a script function that throws or answers what its member does not take is refused by name, and told', () => {
        const failures: ScriptFunctionError[] = [];
        const wire = definition(series({ formatScript: { module: tenor.module, export: 'wrong' } }));
        const read = readDefinition(wire, calls({ wrong: () => 7 }, failures));
        expect(() => read.series[0]?.format?.(2)).toThrow(/"wrong" of ".\/js\/chart-formats.js" wrote a value of 'Bonds' 2 as 7, which is not text/u);
        const sampled = readDefinition(definition(series({
            kind: 'level', x: [], y: [], names: null, rows: null,
            sampler: { from: 0, to: 10, madeFrom: { kind: 'script', module: tenor.module, export: 'broken' } },
        })), calls({ broken: () => { throw new Error('no curve'); } }, failures));
        expect(() => sampled.series[0]?.sampler?.valueAt(1)).toThrow(/"broken" of ".\/js\/chart-formats.js" threw: no curve/u);
        expect(failures.map(f => f.name)).toEqual(['ScriptFunctionError', 'ScriptFunctionError']);

        // Asked as the pointer moves, the refusal is on its way to the chart's place: nothing is thrown into the Script
        // Core's handler, and what stands in is never painted.
        const moving: ScriptFunctionError[] = [];
        const hovered = readDefinition(wire, calls({ wrong: () => 7 }, moving, false));
        expect(hovered.series[0]?.format?.(2)).toBe('');
        expect(moving.map(f => f.message)).toEqual([expect.stringMatching(/wrote a value of 'Bonds' 2 as 7/u)]);
    });

    it('ADR-0420: a module the chart cannot import, or one carried in its address, is refused in the chart\'s place', async () => {
        const wire = definition();
        const element = root();
        const chart = await attachChart(element, new FakeDotNet(), JSON.stringify({
            circuit: false, options, definition: { ...wire, axis: { ...wire.axis, formatScript: { module: 'data:text/javascript,export const tenor = () => "x"', export: 'tenor' } } }, generation: 1,
        } satisfies WireChart));
        expect(element.querySelector('[role=alert]')?.textContent).toMatch(/is addressed by "data:"; a script function's module is a file the application serves/u);
        expect(element.querySelector('svg')).toBeNull();
        expect(await chart.download('svg')).toBe(false);
        // Another definition draws in its place.
        await chart.update(JSON.stringify({ circuit: false, options, definition: definition(), generation: 2 } satisfies WireChart));
        expect(element.querySelector('[role=alert]')).toBeNull();
        expect(element.querySelector('svg')).not.toBeNull();
        chart.dispose();
    });
});

/** .NET as the Razor Chrome's slots reach it: each call recorded, a held one with what it stands for. */
class SlotDotNet implements DotNetHandle {
    readonly calls: { readonly method: string; readonly args: readonly unknown[] }[] = [];

    invokeMethodAsync(method: string, ...args: readonly unknown[]): Promise<unknown> {
        this.calls.push({ method, args });
        return Promise.resolve(null);
    }

    /** The data last sent for the slot named `name`, its number and its slot's id. */
    last(name: string): { readonly id: number; readonly version: number; readonly data: Record<string, unknown> } {
        const mounted = this.calls.find(call => call.method === 'MountSlot' && call.args[2] === name);
        if (mounted === undefined) throw new Error(`No slot ${name} was mounted.`);
        const id = mounted.args[0] as number;
        const sent = this.calls.filter(call => (call.method === 'MountSlot' || call.method === 'UpdateSlot') && call.args[0] === id).at(-1);
        if (sent === undefined) throw new Error('Nothing was sent.');
        return { id, version: sent.args[1] as number, data: JSON.parse(sent.args.at(-1) as string) as Record<string, unknown> };
    }
}

// What .NET renders for a slot: the contents in their hidden holder, carrying the number of the data they were drawn from.
function drawn(markup: string, version: number): HTMLElement {
    const holder = document.createElement('div');
    holder.hidden = true;
    const contents = document.createElement('div');
    contents.className = 'ex-razor-slot';
    contents.setAttribute('data-ex-version', String(version));
    contents.innerHTML = markup;
    holder.append(contents);
    document.body.append(holder);
    return contents;
}

function press(contents: HTMLElement, selector: string): void {
    const control = contents.querySelector(selector);
    if (!(control instanceof HTMLElement)) throw new Error(`No ${selector}.`);
    control.click();
}

function field(element: HTMLElement, selector: string): HTMLInputElement {
    const found = element.querySelector(selector);
    if (!(found instanceof HTMLInputElement)) throw new Error(`No ${selector}.`);
    return found;
}

describe('a Razor Chrome in the chart\'s slots', () => {
    const toolbarMarkup = '<button data-ex-command="exchart.filters">Filters</button><button data-ex-command="exchart.reset-range">Reset</button>'
        + '<button data-ex-command="exchart.download.csv">CSV</button>';
    const filtersMarkup = `${toolbarMarkup}<input data-ex-chart-from><span data-ex-chart-to><input></span><button data-ex-chart-apply>Apply</button>`;

    it('ADR-0410: a command, a typed range and the range the chart holds are the browser\'s, never a round trip behind', async () => {
        const dotnet = new SlotDotNet();
        const element = root();
        const chart = await attachChart(element, dotnet, JSON.stringify({
            circuit: true, options: { ...options, hears: ['rangeChanged'], razorSlots: ['toolbar', 'menu'] }, definition: definition(), generation: 1,
        } satisfies WireChart));
        // The toolbar is asked of .NET with its commands and words, and stands only once .NET drew it.
        const toolbar = dotnet.last('toolbar');
        expect(toolbar.data).toMatchObject({ filters: null, words: { apply: 'Apply', downloads: 'Download' } });
        expect(toolbar.data['commands']).toContainEqual({ id: 'exchart.reset-range', label: 'Reset range', enabled: false, expanded: null, title: null });
        expect(element.querySelector('.ex-chart-toolbar')).toBeNull();
        const contents = drawn(toolbarMarkup, toolbar.version);
        chart.slotRendered(toolbar.id, toolbar.version, contents);
        expect(contents.parentElement?.classList.contains('ex-chart-toolbar-slot')).toBe(true);
        // Chart Filters opens on the press, in the browser: the same slot is drawn again for it.
        press(contents, '[data-ex-command="exchart.filters"]');
        const opened = dotnet.last('toolbar');
        expect(opened.id).toBe(toolbar.id);
        expect(opened.data['filters']).toEqual({ values: 'numbers', labels: [], hint: 'number', error: null, scale: 'proportional' });
        const filters = drawn(filtersMarkup, opened.version);
        chart.slotRendered(opened.id, opened.version, filters);
        // What is typed is read as Apply is pressed, from the fields as they stand, a field inside a wrapper among them.
        field(filters, '[data-ex-chart-from]').value = '2';
        field(filters, '[data-ex-chart-to] input').value = '8';
        press(filters, '[data-ex-chart-apply]');
        expect(dotnet.calls.filter(call => call.method === 'Heard')).toEqual([{ method: 'Heard', args: ['rangeChanged', { from: 2, to: 8 }] }]);
        // Reset range returns the range, and the fields with it, before .NET has drawn anything again.
        press(filters, '[data-ex-command="exchart.reset-range"]');
        expect(dotnet.calls.at(-1)).toMatchObject({ method: 'Heard', args: ['rangeChanged', null] });
        expect(field(filters, '[data-ex-chart-from]').value).toBe('');
        expect(field(filters, '[data-ex-chart-to] input').value).toBe('');
        chart.dispose();
        // The contents are back in their holder, which .NET removes, and nothing is asked of .NET as the chart goes.
        expect(filters.parentElement?.hidden).toBe(true);
        expect(dotnet.calls.filter(call => call.method === 'UnmountSlot')).toEqual([]);
    });

    it('ADR-0410, spine rule 6: ends typed into fields .NET drew, before the browser hears they were drawn, are the ones applied', async () => {
        const dotnet = new SlotDotNet();
        const chart = await attachChart(root(), dotnet, JSON.stringify({
            circuit: true, options: { ...options, hears: ['rangeChanged'], razorSlots: ['toolbar'] }, definition: definition(), generation: 1,
        } satisfies WireChart));
        const first = dotnet.last('toolbar');
        const contents = drawn(toolbarMarkup, first.version);
        chart.slotRendered(first.id, first.version, contents);
        press(contents, '[data-ex-command="exchart.filters"]');
        // .NET's render reaches the page, the fields in it, and the user types at once: that it was rendered is told a
        // message later, over the circuit.
        const opened = dotnet.last('toolbar');
        contents.innerHTML = filtersMarkup;
        contents.setAttribute('data-ex-version', String(opened.version));
        field(contents, '[data-ex-chart-from]').value = '2';
        field(contents, '[data-ex-chart-to] input').value = '8';
        chart.slotRendered(opened.id, opened.version, contents);
        expect([field(contents, '[data-ex-chart-from]').value, field(contents, '[data-ex-chart-to] input').value]).toEqual(['2', '8']);
        press(contents, '[data-ex-chart-apply]');
        expect(dotnet.calls.filter(call => call.method === 'Heard')).toEqual([{ method: 'Heard', args: ['rangeChanged', { from: 2, to: 8 }] }]);
        chart.dispose();
    });

    it('ADR-0410, ADR-0273: an end typed and not applied is kept while the range stands, and both ends are replaced when it moves', async () => {
        const dotnet = new SlotDotNet();
        const chart = await attachChart(root(), dotnet, JSON.stringify({
            circuit: false, options: { ...options, razorSlots: ['toolbar'] }, definition: definition(), generation: 1,
        } satisfies WireChart));
        const first = dotnet.last('toolbar');
        chart.slotRendered(first.id, first.version, drawn(toolbarMarkup, first.version));
        press(document.body, '[data-ex-command="exchart.filters"]');
        const opened = dotnet.last('toolbar');
        const filters = drawn(`${filtersMarkup}<button data-ex-command="exchart.table">Table</button>`, opened.version);
        chart.slotRendered(opened.id, opened.version, filters);
        const from = field(filters, '[data-ex-chart-from]');
        const to = field(filters, '[data-ex-chart-to] input');
        to.value = '7';
        press(filters, '[data-ex-chart-apply]');
        expect([from.value, to.value]).toEqual(['', '7']);
        // Typed and not applied: the table shown changes what the toolbar shows, and not the range, so the text stays.
        from.value = '3';
        press(filters, '[data-ex-command="exchart.table"]');
        expect(from.value).toBe('3');
        // Reset range moves the range, and both ends are replaced by it, the start the chart held unchanged among them.
        press(filters, '[data-ex-command="exchart.reset-range"]');
        expect([from.value, to.value]).toEqual(['', '']);
        chart.dispose();
    });

    it('ADR-0410, ADR-0281: a menu item runs its command and closes the menu, whose frame stays the chart\'s', async () => {
        const dotnet = new SlotDotNet();
        const element = root();
        const chart = await attachChart(element, dotnet, JSON.stringify({
            circuit: false, options: { ...options, razorSlots: ['menu'] }, definition: definition(), generation: 1,
        } satisfies WireChart));
        // Only the menu is the Consumer's: the toolbar is the chart's own.
        expect(element.querySelector('.ex-chart-toolbar')).not.toBeNull();
        element.querySelector('.ex-chart-stage')?.dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: 10, clientY: 10 }));
        const menu = dotnet.last('menu');
        expect(menu.data).toMatchObject({ name: 'Chart' });
        const contents = drawn('<button data-ex-command="exchart.table">View as table</button>', menu.version);
        chart.slotRendered(menu.id, menu.version, contents);
        expect(contents.parentElement?.getAttribute('role')).toBe('menu');
        press(contents, '[data-ex-command="exchart.table"]');
        expect(element.querySelector('.ex-chart-menu')).toBeNull();
        expect(element.querySelector('table')).not.toBeNull();
        await Promise.resolve();
        expect(dotnet.calls.at(-1)).toEqual({ method: 'UnmountSlot', args: [menu.id] });
        chart.dispose();
    });

    it('ADR-0005: a toolbar carrying more categories than a circuit\'s message holds crosses as a held call', async () => {
        const dotnet = new SlotDotNet();
        const labels = Array.from({ length: 4000 }, (_, i) => `Category ${String(i)}`);
        const wire = definition(series({ kind: 'level', x: [0, 1], y: [1, 2], names: null, rows: null }));
        const chart = await attachChart(root(), dotnet, JSON.stringify({
            circuit: true, options: { ...options, razorSlots: ['toolbar'] }, definition: { ...wire, axis: { ...wire.axis, values: 'labels', labels } }, generation: 1,
        } satisfies WireChart));
        const toolbar = dotnet.last('toolbar');
        chart.slotRendered(toolbar.id, toolbar.version, drawn(toolbarMarkup, toolbar.version));
        press(document.body, '[data-ex-command="exchart.filters"]');
        const held = dotnet.calls.at(-1);
        if (held === undefined) throw new Error('Nothing crossed.');
        expect(held.method).toBe('Held');
        expect(held.args[0]).toBe('UpdateSlot');
        const { held: number } = held.args[1] as { held: number };
        const taken = await chart.take(number);
        expect(taken.byteLength).toBeGreaterThan(0);
        chart.dispose();
    });
});
