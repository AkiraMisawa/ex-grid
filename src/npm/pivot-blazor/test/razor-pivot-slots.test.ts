// @vitest-environment happy-dom
// ExPivot's Razor slots (ADR-0410): what the Razor's attributes wire in the browser. A press runs the context's
// callback, a control and an anchor are handed in by name and handed back as the contents go, and a menu's items take
// their command's role and the keyboard at the pivot's request. What the pivot does with them is its own tests'.
import { describe, expect, it } from 'vitest';
import { drawnVersionAttribute } from '@ex-grid/blazor-shared';
import type { PivotMenuSlotContext, PivotMenuView, PivotPartRef, PivotToolbarSlotContext, PivotToolbarView } from '@ex-grid/pivot';
import type { DotNetHandle } from '../src/dotnet-pivot-source';
import { RazorPivotSlots, type PivotGrids } from '../src/razor-pivot-slots';

class FakeDotNet implements DotNetHandle {
    readonly calls: { readonly method: string; readonly args: readonly unknown[] }[] = [];

    invokeMethodAsync(method: string, ...args: readonly unknown[]): Promise<unknown> {
        this.calls.push({ method, args });
        return Promise.resolve(null);
    }
}

// No grid stands: nothing is held or ordered, as over an empty report.
const noGrids: PivotGrids = { gridAround: () => undefined, holdSettled: () => () => undefined };

function rendered(markup: string, version: number): HTMLElement {
    const holder = document.createElement('div');
    holder.hidden = true;
    const contents = document.createElement('div');
    contents.innerHTML = markup;
    contents.setAttribute(drawnVersionAttribute, String(version));
    holder.append(contents);
    document.body.append(holder);
    return contents;
}

function slotted(): HTMLElement {
    const slot = document.createElement('div');
    document.body.append(slot);
    return slot;
}

function mountOf(slots: RazorPivotSlots, name: 'menu' | 'toolbar' | 'staleReport'): (slot: HTMLElement, context: unknown) => () => void {
    const chrome = slots.chrome();
    const mount: unknown = chrome?.[name];
    if (typeof mount !== 'function') throw new Error(`No mount for the ${name}.`);
    return mount as (slot: HTMLElement, context: unknown) => () => void;
}

function toolbarOf(pressed: string[], handed: Map<string, HTMLElement | null>): PivotToolbarSlotContext {
    const view: PivotToolbarView = {
        layoutMenu: { label: 'Layout', open: false }, refresh: { label: 'Refresh' },
        fieldList: { label: 'Field List', shown: true }, reportFilters: true,
    };
    const ref = (name: string): PivotPartRef => element => { handed.set(name, element); };
    return {
        view: () => view, subscribe: () => () => undefined, label: id => id,
        toggleLayoutMenu: () => { pressed.push('layout'); }, refresh: () => { pressed.push('refresh'); },
        toggleFieldList: () => { pressed.push('field-list'); },
        control: name => ref(`control:${name}`), anchor: name => ref(`anchor:${name}`),
    };
}

function menuOf(invoked: string[], focused: HTMLElement[], focusRequest: number): PivotMenuSlotContext {
    const command = (id: string, enabled: boolean, checked?: boolean): PivotMenuView['commands'][number] => ({
        id, label: id, enabled, ...(checked === undefined ? {} : { checked }), invoke: () => { invoked.push(id); },
    });
    const view: PivotMenuView = {
        name: 'Layout',
        commands: [command('noSubtotals', false, true), command('subtotalsAtBottom', true, false), command('refresh', true)],
    };
    return {
        view: () => view, subscribe: () => () => undefined, close: () => undefined, label: id => id,
        focusRequest, takeFocus: control => { focused.push(control); },
    };
}

describe('ExPivot\'s Razor slots', () => {
    it('ADR-0410 section 2: the toolbar\'s presses run its callbacks, and its controls and anchors are handed in by name and back as it goes', () => {
        const dotnet = new FakeDotNet();
        const slots = new RazorPivotSlots(dotnet, { pivot: ['toolbar'], grid: [] }, noGrids);
        const pressed: string[] = [];
        const handed = new Map<string, HTMLElement | null>();
        const unmount = mountOf(slots, 'toolbar')(slotted(), toolbarOf(pressed, handed));
        expect(dotnet.calls[0]?.method).toBe('MountSlot');

        const contents = rendered(
            '<div data-ex-pivot-anchor="report-filters"></div>'
            + '<button data-ex-pivot-press="layout" data-ex-pivot-control="layout">Layout</button><span data-ex-pivot-anchor="layout"></span>'
            + '<button data-ex-pivot-press="refresh" data-ex-pivot-control="refresh">Refresh</button>'
            + '<button data-ex-pivot-press="field-list" data-ex-pivot-control="field-list">Field List</button>', 1);
        slots.rendered(1, 1, contents);
        for (const button of contents.querySelectorAll('button')) button.click();
        expect(pressed).toEqual(['layout', 'refresh', 'field-list']);
        expect(contents.querySelector('[data-ex-pivot-press="layout"]')?.getAttribute('data-ex-claims')).toBe('click');
        expect([...handed.keys()].toSorted()).toEqual(['anchor:layout', 'anchor:report-filters', 'control:field-list', 'control:layout', 'control:refresh']);
        expect(handed.get('anchor:layout')).toBe(contents.querySelector('[data-ex-pivot-anchor="layout"]'));

        unmount();
        expect([...handed.values()].every(element => element === null)).toBe(true);
        slots.dispose();
    });

    it('ADR-0410 section 2: a toolbar control of a name the part does not hand in is refused by name', () => {
        const slots = new RazorPivotSlots(new FakeDotNet(), { pivot: ['toolbar'], grid: [] }, noGrids);
        mountOf(slots, 'toolbar')(slotted(), toolbarOf([], new Map()));
        expect(() => { slots.rendered(1, 1, rendered('<button data-ex-pivot-control="export">Export</button>', 1)); })
            .toThrow('The toolbar\'s Razor names \'export\', which is none of layout, refresh, field-list.');
        slots.dispose();
    });

    it('ADR-0410 section 2: a menu\'s items take their command\'s role, run the command drawn, and the first enabled one takes the keyboard at the request', () => {
        const slots = new RazorPivotSlots(new FakeDotNet(), { pivot: ['menu'], grid: [] }, noGrids);
        const invoked: string[] = [];
        const focused: HTMLElement[] = [];
        mountOf(slots, 'menu')(slotted(), menuOf(invoked, focused, 1));
        const contents = rendered(
            '<button data-ex-pivot-menu-item="0">No</button><button data-ex-pivot-menu-item="1">Bottom</button><button data-ex-pivot-menu-item="2">Refresh</button>', 1);
        slots.rendered(1, 1, contents);

        const items = [...contents.querySelectorAll('button')];
        expect(items.map(item => [item.getAttribute('role'), item.getAttribute('aria-checked'), item.getAttribute('aria-disabled')])).toEqual([
            ['menuitemradio', 'true', 'true'], ['menuitemradio', 'false', 'false'], ['menuitem', null, 'false'],
        ]);
        expect(focused).toEqual([items[1]]);
        for (const item of items) item.click();
        expect(invoked).toEqual(['subtotalsAtBottom', 'refresh']);
        slots.dispose();
    });

    it('ADR-0410 section 2: a menu whose Razor leaves a command unmarked is refused, never drawn short', () => {
        const slots = new RazorPivotSlots(new FakeDotNet(), { pivot: ['menu'], grid: [] }, noGrids);
        mountOf(slots, 'menu')(slotted(), menuOf([], [], 0));
        expect(() => { slots.rendered(1, 1, rendered('<button data-ex-pivot-menu-item="0">No</button>', 1)); })
            .toThrow('The menu draws 1 of its 3 commands');
        slots.dispose();
    });

    it('ADR-0410 section 1: a slot .NET names that the pivot has not is refused by name', () => {
        expect(() => new RazorPivotSlots(new FakeDotNet(), { pivot: ['fieldList'], grid: [] }, noGrids))
            .toThrow('.NET named \'fieldList\' as a pivot slot its Chrome fills.');
        expect(() => new RazorPivotSlots(new FakeDotNet(), { pivot: [], grid: ['toolbar'] }, noGrids))
            .toThrow('.NET named \'toolbar\' as a grid slot its Chrome fills.');
    });
});
