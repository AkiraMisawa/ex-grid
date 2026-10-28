// @vitest-environment happy-dom
// ExPivot's Razor slots (ADR-0410): what the Razor's attributes wire in the browser. A press runs the context's
// callback, a control and an anchor are handed in by name and handed back as the contents go, and a menu's items take
// their command's role and the keyboard at the pivot's request. What the pivot does with them is its own tests'.
import { describe, expect, it } from 'vitest';
import { drawnVersionAttribute } from '@ex-grid/blazor-shared';
import { pivotItemKeyId, textItemKey } from '@ex-grid/pivot-engine';
import type {
    PivotFieldSettingsSlotContext, PivotItemFilterSlotContext, PivotItemFilterView, PivotMenuSlotContext, PivotMenuView, PivotPartRef, PivotReportFiltersSlotContext,
    PivotToolbarSlotContext, PivotToolbarView, PivotValueFieldSettingsSlotContext, PivotValueFieldSettingsView,
} from '@ex-grid/pivot';
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

function mountOf(slots: RazorPivotSlots, name: 'menu' | 'toolbar' | 'staleReport' | 'reportFilters' | 'fieldSettings' | 'valueFieldSettings' | 'itemFilter'): (slot: HTMLElement, context: unknown) => () => void {
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

    it('ADR-0410 section 2: the report filter band\'s buttons toggle their field\'s Filter…, and each button and its anchor are handed in by field', () => {
        const slots = new RazorPivotSlots(new FakeDotNet(), { pivot: ['reportFilters'], grid: [] }, noGrids);
        const toggled: string[] = [];
        const handed = new Map<string, HTMLElement | null>();
        const ref = (name: string): PivotPartRef => element => { handed.set(name, element); };
        const band: PivotReportFiltersSlotContext = {
            view: () => ({ name: 'Report filters', filters: [{ field: 'Desk', caption: 'Desk', summary: '(All)', filtered: false, open: false, buttonLabel: 'Filter Desk' }] }),
            subscribe: () => () => undefined, label: id => id,
            toggle: field => { toggled.push(field); }, control: field => ref(`control:${field}`), anchor: field => ref(`anchor:${field}`),
        };
        mountOf(slots, 'reportFilters')(slotted(), band);
        const contents = rendered('<span><button data-ex-pivot-press="Desk" data-ex-pivot-control="Desk">(All)</button><span data-ex-pivot-anchor="Desk"></span></span>', 1);
        slots.rendered(1, 1, contents);
        contents.querySelector('button')?.click();
        expect(toggled).toEqual(['Desk']);
        expect([...handed.keys()].toSorted()).toEqual(['anchor:Desk', 'control:Desk']);
        slots.dispose();
    });

    it('ADR-0410 section 2: Filter…\'s search, Items and (Select All) reach the panel in the browser, against the Items drawn, and OK applies', () => {
        const heard: string[] = [];
        const focused: HTMLElement[] = [];
        let view: PivotItemFilterView = {
            caption: 'Desk', search: '', items: [{ key: textItemKey('Rates'), label: 'Rates', ticked: true }, { key: textItemKey('FX'), label: 'FX', ticked: false }],
            allTicked: undefined, loading: false, updating: false, canApply: true,
        };
        const panel: PivotItemFilterSlotContext = {
            view: () => view, subscribe: () => () => undefined, label: id => id, focusRequest: 1, takeFocus: control => { focused.push(control); },
            apply: () => { heard.push('apply'); }, cancel: () => { heard.push('cancel'); }, innerPopupChanged: () => undefined,
            setSearch: text => { heard.push(`search ${text}`); view = { ...view, search: text }; },
            setTicked: (key, ticked) => { heard.push(`tick ${JSON.stringify(key)} ${String(ticked)}`); },
            setAll: ticked => { heard.push(`all ${String(ticked)}`); view = { ...view, allTicked: ticked }; },
        };
        const dotnet = new FakeDotNet();
        const withDotnet = new RazorPivotSlots(dotnet, { pivot: ['itemFilter'], grid: [] }, noGrids);
        mountOf(withDotnet, 'itemFilter')(slotted(), panel);
        expect(JSON.parse(String(dotnet.calls[0]?.args[3]))).toMatchObject({
            search: '', items: [{ key: pivotItemKeyId(textItemKey('Rates')), label: 'Rates', ticked: true }, { key: pivotItemKeyId(textItemKey('FX')), label: 'FX', ticked: false }],
            allTicked: null,
        });
        const contents = rendered(
            '<div data-ex-pivot-text="search" data-ex-pivot-first=""><input type="search"></div>'
            + '<input type="checkbox" data-ex-pivot-all=""><input type="checkbox" data-ex-pivot-item="0" checked><input type="checkbox" data-ex-pivot-item="1">'
            + '<button data-ex-pivot-press="ok">OK</button><button data-ex-pivot-press="cancel">Cancel</button>', 1);
        withDotnet.rendered(1, 1, contents);
        const search = contents.querySelector('input[type="search"]');
        if (!(search instanceof HTMLInputElement)) throw new Error('No search.');
        expect(focused).toEqual([search]);
        expect(contents.querySelector('[data-ex-pivot-text]')?.getAttribute('data-ex-claims')).toBe('');

        search.value = 'r';
        search.dispatchEvent(new Event('input', { bubbles: true }));
        // The pivot's list moves under the search; the checkbox still ticks the Item it was drawn for.
        view = { ...view, items: [{ key: textItemKey('Rates'), label: 'Rates', ticked: true }] };
        const fx = contents.querySelector('[data-ex-pivot-item="1"]');
        if (!(fx instanceof HTMLInputElement)) throw new Error('No FX.');
        fx.click();
        // Pressed twice before .NET draws again, (Select All) ticks every Item and then unticks them, judged on the
        // draft as it stands and not on the box as drawn.
        contents.querySelector<HTMLInputElement>('[data-ex-pivot-all]')?.click();
        contents.querySelector<HTMLInputElement>('[data-ex-pivot-all]')?.click();
        contents.querySelector<HTMLButtonElement>('[data-ex-pivot-press="ok"]')?.click();
        expect(heard).toEqual(['search r', 'tick {"kind":"text","value":"FX"} true', 'all true', 'all false', 'apply']);
        withDotnet.dispose();
    });

    it('ADR-0410 section 2: Value Field Settings…\' caption follows the Aggregation chosen in the browser, written into the field only where it differs', () => {
        const dotnet = new FakeDotNet();
        const slots = new RazorPivotSlots(dotnet, { pivot: ['valueFieldSettings'], grid: [] }, noGrids);
        const listeners = new Set<() => void>();
        let view: PivotValueFieldSettingsView = {
            sourceCaption: 'Amount', caption: 'Sum of Amount', aggregation: 'sum',
            aggregations: [{ value: 'sum', label: 'Sum', enabled: true }, { value: 'count', label: 'Count', enabled: true }],
            showValuesAs: 'noCalculation', showValuesAsChoices: [{ value: 'noCalculation', label: 'No Calculation', enabled: true }],
            numberFormat: '', numberFormatPlaceholder: 'General', sample: '1234.5',
        };
        const change = (next: Partial<PivotValueFieldSettingsView>): void => {
            view = { ...view, ...next };
            for (const listener of listeners) listener();
        };
        const panel: PivotValueFieldSettingsSlotContext = {
            view: () => view, subscribe: listener => { listeners.add(listener); return () => { listeners.delete(listener); }; },
            label: id => id, focusRequest: 0, takeFocus: () => undefined, apply: () => undefined, cancel: () => undefined, innerPopupChanged: () => undefined,
            setCaption: text => { change({ caption: text }); },
            setAggregation: aggregation => { change({ aggregation, caption: aggregation === 'count' ? 'Count of Amount' : 'Sum of Amount' }); },
            setShowValuesAs: () => undefined, setNumberFormat: text => { change({ numberFormat: text }); },
        };
        mountOf(slots, 'valueFieldSettings')(slotted(), panel);
        const contents = rendered(
            '<input data-ex-pivot-text="caption" value="Sum of Amount"><select data-ex-pivot-choice="aggregation"><option value="sum">Sum</option><option value="count">Count</option></select>'
            + '<input data-ex-pivot-text="number-format" value="">', 1);
        slots.rendered(1, 1, contents);
        const caption = contents.querySelector<HTMLInputElement>('[data-ex-pivot-text="caption"]');
        const aggregation = contents.querySelector('select');
        if (caption === null || aggregation === null) throw new Error('No controls.');
        aggregation.value = 'count';
        aggregation.dispatchEvent(new Event('change', { bubbles: true }));
        expect(caption.value).toBe('Count of Amount');

        // A caption typed stays as typed, and every render .NET draws after opens with the text the field opened with.
        caption.value = 'Trades';
        caption.dispatchEvent(new Event('input', { bubbles: true }));
        expect(view.caption).toBe('Trades');
        const sent = dotnet.calls.filter(call => call.method === 'UpdateSlot').map(call => JSON.parse(String(call.args[2])) as { caption: string });
        expect(sent.length).toBeGreaterThan(0);
        expect(sent.every(each => each.caption === 'Sum of Amount')).toBe(true);
        slots.dispose();
    });

    it('ADR-0410 section 2: Field Settings…\' subtotals are chosen by the radio button\'s value, and a choice the panel lacks is refused by name', () => {
        const slots = new RazorPivotSlots(new FakeDotNet(), { pivot: ['fieldSettings'], grid: [] }, noGrids);
        const heard: string[] = [];
        const panel: PivotFieldSettingsSlotContext = {
            view: () => ({ caption: 'Desk', subtotals: true, sorts: ['A to Z', 'Z to A'], sort: 0 }), subscribe: () => () => undefined,
            label: id => id, focusRequest: 0, takeFocus: () => undefined, apply: () => undefined, cancel: () => undefined, innerPopupChanged: () => undefined,
            setSubtotals: automatic => { heard.push(`subtotals ${String(automatic)}`); }, setSort: index => { heard.push(`sort ${String(index)}`); },
        };
        mountOf(slots, 'fieldSettings')(slotted(), panel);
        const contents = rendered(
            '<label data-ex-pivot-choice="subtotals" data-ex-pivot-value="none"><input type="radio" name="s"></label>'
            + '<select data-ex-pivot-choice="sort"><option value="0">A to Z</option><option value="1">Z to A</option></select>'
            + '<select data-ex-pivot-choice="aggregation"><option value="sum">Sum</option></select>', 1);
        slots.rendered(1, 1, contents);
        contents.querySelector<HTMLInputElement>('input[type="radio"]')?.click();
        const [sort, wrong] = contents.querySelectorAll('select');
        if (sort === undefined || wrong === undefined) throw new Error('No selects.');
        sort.value = '1';
        sort.dispatchEvent(new Event('change', { bubbles: true }));
        expect(heard).toEqual(['subtotals false', 'sort 1']);
        expect(() => { wrong.dispatchEvent(new Event('change', { bubbles: true })); }).toThrow('Field Settings… has no choice \'aggregation\'.');
        slots.dispose();
    });

    it('ADR-0410 section 2: what the pivot placed in a part\'s anchor, the report filter band in the toolbar\'s, keeps its marks to itself', () => {
        const slots = new RazorPivotSlots(new FakeDotNet(), { pivot: ['toolbar'], grid: [] }, noGrids);
        const pressed: string[] = [];
        mountOf(slots, 'toolbar')(slotted(), toolbarOf(pressed, new Map()));
        // The band's contents, wired by their own slot, already stand in the anchor as the toolbar is drawn again.
        const contents = rendered(
            '<div data-ex-pivot-anchor="report-filters"><span><button data-ex-pivot-press="Desk" data-ex-pivot-control="Desk">(All)</button><span data-ex-pivot-anchor="Desk"></span></span></div>'
            + '<button data-ex-pivot-press="layout" data-ex-pivot-control="layout">Layout</button><span data-ex-pivot-anchor="layout"></span>', 1);
        slots.rendered(1, 1, contents);
        for (const button of contents.querySelectorAll('button')) button.click();
        expect(pressed).toEqual(['layout']);
        expect(contents.querySelector('[data-ex-pivot-press="Desk"]')?.hasAttribute('data-ex-claims')).toBe(false);
        slots.dispose();
    });
});
