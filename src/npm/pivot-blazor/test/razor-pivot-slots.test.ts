// @vitest-environment happy-dom
// ExPivot's Razor slots (ADR-0410): what the Razor's attributes wire in the browser. A press runs the context's
// callback, a control and an anchor are handed in by name and handed back as the contents go, and a menu's items take
// their command's role and the keyboard at the pivot's request. What the pivot does with them is its own tests'.
import { describe, expect, it, vi } from 'vitest';
import { drawnVersionAttribute } from '@ex-grid/blazor-shared';
import { pivotItemKeyId, textItemKey } from '@ex-grid/pivot-engine';
import type {
    PivotDetailsDialogSlotContext, PivotDetailsTabsSlotContext, PivotFieldListSlotContext, PivotFieldListView, PivotFieldSettingsSlotContext, PivotItemFilterSlotContext, PivotItemFilterView, PivotMenuSlotContext, PivotMenuView, PivotPartRef, PivotReportFiltersSlotContext,
    PivotToolbarSlotContext, PivotToolbarView, PivotValueFieldSettingsSlotContext, PivotValueFieldSettingsView,
} from '@ex-grid/pivot';
import type { DotNetHandle } from '../src/dotnet-pivot-source';
import { RazorPivotSlots, type PivotGrids, type PivotRazorSlotName } from '../src/razor-pivot-slots';

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

function mountOf(slots: RazorPivotSlots, name: PivotRazorSlotName): (slot: HTMLElement, context: unknown) => () => void {
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

/** An entry of the Field List's view, named by its place. */
function entryOf(area: 'rows' | 'values', index: number, caption: string, menuOpen = false): PivotFieldListView['areas'][number]['entries'][number] {
    return {
        entry: { area, index }, key: `entry:${area}:${String(index)}`, caption, menuLabel: `Options for ${caption}`, filtered: false, values: false,
        menuOpen, dragging: false, dropBefore: false, dropIndex: index,
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
        expect(() => new RazorPivotSlots(new FakeDotNet(), { pivot: ['chart'], grid: [] }, noGrids))
            .toThrow('.NET named \'chart\' as a pivot slot its Chrome fills.');
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
    it('ADR-0410 section 2, ADR-0061: the Field List\'s search, ticks, presses and drags reach the pane in the browser, against the entries drawn', () => {
        const heard: string[] = [];
        const listeners = new Set<() => void>();
        let view: PivotFieldListView = {
            title: 'PivotTable Fields', hideLabel: 'Hide', chooseCaption: 'Choose', searchLabel: 'Search', search: '',
            fields: [{ name: 'Region', key: 'field:Region', caption: 'Region', placed: true, filtered: false, dragging: false }],
            dragCaption: 'Drag',
            areas: [
                { area: 'rows', title: 'Rows', entries: [entryOf('rows', 0, 'Region'), entryOf('rows', 1, 'Desk', true)], end: 2, dropTarget: false },
                { area: 'values', title: 'Values', entries: [entryOf('values', 0, 'Sum of P&L')], end: 1, dropTarget: false },
            ],
            dropRemoves: false, defer: { label: 'Defer', ticked: false }, update: { label: 'Update', enabled: false },
        };
        const handed = new Map<string, HTMLElement | null>();
        const list: PivotFieldListSlotContext = {
            view: () => view, subscribe: listener => { listeners.add(listener); return () => { listeners.delete(listener); }; }, label: id => id,
            setSearch: text => { heard.push(`search ${text}`); view = { ...view, search: text }; },
            toggle: field => { heard.push(`toggle ${field}`); }, openMenu: entry => { heard.push(`menu ${entry.area} ${String(entry.index)}`); },
            close: () => { heard.push('close'); }, update: () => { heard.push('update'); },
            startDrag: (_event, subject) => { heard.push(`start ${JSON.stringify(subject)}`); }, dragOver: () => { heard.push('over'); },
            dragEnter: (area, index) => { heard.push(`enter ${area} ${String(index)}`); }, drop: (_event, area, index) => { heard.push(`drop ${area} ${String(index)}`); },
            dropOnList: () => { heard.push('drop on list'); }, endDrag: () => { heard.push('end'); }, setDeferred: deferred => { heard.push(`defer ${String(deferred)}`); },
            control: key => element => { handed.set(`control ${key}`, element); }, anchor: key => element => { handed.set(`anchor ${key}`, element); },
            area: area => element => { handed.set(`area ${area}`, element); },
        };
        const slots = new RazorPivotSlots(new FakeDotNet(), { pivot: ['fieldList'], grid: [] }, noGrids);
        mountOf(slots, 'fieldList')(slotted(), list);
        const contents = rendered(
            '<button data-ex-pivot-press="close">Hide</button><div data-ex-pivot-text="search" data-ex-pivot-control="search"><input></div>'
            + '<div data-ex-pivot-fields=""><div data-ex-pivot-field="Region" draggable="true"><input type="checkbox" checked data-ex-pivot-control="field:Region"></div></div>'
            + '<div data-ex-pivot-area="rows">'
            + '<div data-ex-pivot-entry="entry:rows:0" draggable="true"><button data-ex-pivot-press="entry:rows:0" data-ex-pivot-control="entry:rows:0">Region</button><span data-ex-pivot-anchor="entry:rows:0"></span></div>'
            + '<div data-ex-pivot-entry="entry:rows:1" draggable="true"><button data-ex-pivot-press="entry:rows:1" data-ex-pivot-control="entry:rows:1">Desk</button><span data-ex-pivot-anchor="entry:rows:1"><input type="checkbox" class="placed"></span></div>'
            + '</div><div data-ex-pivot-area="values"><div data-ex-pivot-entry="entry:values:0" draggable="true"><button data-ex-pivot-press="entry:values:0">Sum</button><span data-ex-pivot-anchor="entry:values:0"></span></div></div>'
            + '<span data-ex-pivot-defer=""><input type="checkbox"></span><button data-ex-pivot-press="update">Update</button>', 1);
        slots.rendered(1, 1, contents);
        const at = (selector: string): HTMLElement => {
            const found = contents.querySelector<HTMLElement>(selector);
            if (found === null) throw new Error(`No ${selector}.`);
            return found;
        };
        expect([...handed.keys()].toSorted()).toEqual([
            'anchor entry:rows:0', 'anchor entry:rows:1', 'anchor entry:values:0', 'area rows', 'area values',
            'control entry:rows:0', 'control entry:rows:1', 'control field:Region', 'control search',
        ]);
        expect(handed.get('control search')).toBe(at('[data-ex-pivot-text] input'));
        // The entry whose menu stands open is not draggable, as the pane is now.
        expect(at('[data-ex-pivot-entry="entry:rows:1"]').draggable).toBe(false);
        expect(at('[data-ex-pivot-field] input').getAttribute('data-ex-claims')).toBe('click');

        const search = at('[data-ex-pivot-text] input');
        if (!(search instanceof HTMLInputElement)) throw new Error('No search.');
        search.value = 'Re';
        search.dispatchEvent(new Event('input', { bubbles: true }));
        // Unticked, the field leaves the layout; ticked again while the pane still shows it placed, nothing is asked.
        at('[data-ex-pivot-field] input').click();
        at('[data-ex-pivot-field] input').click();
        at('[data-ex-pivot-defer] input').click();
        at('[data-ex-pivot-press="entry:rows:0"]').click();
        at('[data-ex-pivot-press="close"]').click();
        // A tick in what the pivot placed in an entry's anchor is that part's, not the pane's.
        at('.placed').click();
        at('[data-ex-pivot-entry="entry:values:0"]').dispatchEvent(new DragEvent('dragstart', { bubbles: true }));
        at('[data-ex-pivot-entry="entry:rows:0"]').dispatchEvent(new DragEvent('dragenter', { bubbles: true }));
        at('[data-ex-pivot-area="rows"]').dispatchEvent(new DragEvent('dragover', { bubbles: true }));
        at('[data-ex-pivot-area="rows"]').dispatchEvent(new DragEvent('drop', { bubbles: true }));
        at('[data-ex-pivot-fields]').dispatchEvent(new DragEvent('drop', { bubbles: true }));
        expect(heard).toEqual([
            'search Re', 'toggle Region', 'defer true', 'menu rows 0', 'close',
            'start {"entry":{"area":"values","index":0}}', 'enter rows 0', 'over', 'drop rows 2', 'drop on list',
        ]);

        // The Areas changed under the contents, a round trip before .NET draws them again: an entry's place now names
        // another field, so a press or a drag on it does nothing, and a drop ends the drag.
        heard.length = 0;
        view = { ...view, areas: [{ ...view.areas[0] ?? (() => { throw new Error('No Rows.'); })(), entries: [entryOf('rows', 0, 'Book')] }, ...view.areas.slice(1)] };
        for (const listener of listeners) listener();
        at('[data-ex-pivot-press="entry:rows:0"]').click();
        at('[data-ex-pivot-entry="entry:rows:0"]').dispatchEvent(new DragEvent('dragstart', { bubbles: true }));
        at('[data-ex-pivot-area="values"]').dispatchEvent(new DragEvent('drop', { bubbles: true }));
        expect(heard).toEqual(['end']);
        slots.dispose();
    });

    it('ADR-0410 section 2: a button that is its own anchor is pressed, as a menu beside it would be placed in it', () => {
        const pressed: string[] = [];
        const handed = new Map<string, HTMLElement | null>();
        const slots = new RazorPivotSlots(new FakeDotNet(), { pivot: ['toolbar'], grid: [] }, noGrids);
        mountOf(slots, 'toolbar')(slotted(), toolbarOf(pressed, handed));
        const contents = rendered('<button data-ex-pivot-press="layout" data-ex-pivot-anchor="layout">Layout</button>', 1);
        slots.rendered(1, 1, contents);
        contents.querySelector('button')?.click();
        expect(pressed).toEqual(['layout']);
        expect(handed.get('anchor:layout')).toBe(contents.querySelector('button'));
        slots.dispose();
    });

    it('ADR-0410 section 2, ADR-0240: an entry .NET patched in, pressed before its wiring stands, opens its menu once its anchor is handed in', async () => {
        const heard: string[] = [];
        const listeners = new Set<() => void>();
        let view: PivotFieldListView = {
            title: 'PivotTable Fields', hideLabel: 'Hide', chooseCaption: 'Choose', searchLabel: 'Search', search: '',
            fields: [], dragCaption: 'Drag',
            areas: [{ area: 'values', title: 'Values', entries: [], end: 0, dropTarget: false }],
            dropRemoves: false, defer: { label: 'Defer', ticked: false }, update: { label: 'Update', enabled: false },
        };
        const list: PivotFieldListSlotContext = {
            view: () => view, subscribe: listener => { listeners.add(listener); return () => { listeners.delete(listener); }; }, label: id => id,
            setSearch: () => undefined, toggle: () => undefined, openMenu: entry => { heard.push(`menu ${entry.area} ${String(entry.index)}`); },
            close: () => undefined, update: () => undefined, startDrag: () => undefined, dragOver: () => undefined, dragEnter: () => undefined,
            drop: () => undefined, dropOnList: () => undefined, endDrag: () => undefined, setDeferred: () => undefined,
            control: () => () => undefined, anchor: key => element => { if (element !== null) heard.push(`anchor ${key}`); }, area: () => () => undefined,
        };
        const slots = new RazorPivotSlots(new FakeDotNet(), { pivot: ['fieldList'], grid: [] }, noGrids);
        mountOf(slots, 'fieldList')(slotted(), list);
        const contents = rendered('<div data-ex-pivot-area="values"></div>', 1);
        slots.rendered(1, 1, contents);

        // A field ticked into Values: .NET is handed the pane under the next number, and Blazor patches its entry in a
        // round trip before .NET says it rendered, which is when the wiring stands.
        view = { ...view, areas: [{ area: 'values', title: 'Values', entries: [entryOf('values', 0, 'Sum of Key')], end: 1, dropTarget: false }] };
        for (const listener of listeners) listener();
        contents.setAttribute(drawnVersionAttribute, '2');
        contents.querySelector('[data-ex-pivot-area]')?.insertAdjacentHTML('beforeend',
            '<div data-ex-pivot-entry="entry:values:0" draggable="true"><button data-ex-pivot-press="entry:values:0">Sum of Key</button><span data-ex-pivot-anchor="entry:values:0"></span></div>');
        contents.querySelector<HTMLButtonElement>('[data-ex-pivot-press="entry:values:0"]')?.click();
        await Promise.resolve();
        // The pivot has no anchor to place the menu in yet: the press waits for the wiring, and is taken once it stands.
        expect(heard).toEqual([]);
        slots.rendered(1, 2, contents);
        await vi.waitFor(() => { expect(heard).toEqual(['anchor entry:values:0', 'menu values 0']); });
        slots.dispose();
    });

    it('ADR-0410 section 2, spine rule 6: an entry pressed before its wiring stands, whose place the pane gave another field meanwhile, opens nothing', async () => {
        const heard: string[] = [];
        const listeners = new Set<() => void>();
        let view: PivotFieldListView = {
            title: 'PivotTable Fields', hideLabel: 'Hide', chooseCaption: 'Choose', searchLabel: 'Search', search: '',
            fields: [], dragCaption: 'Drag',
            areas: [{ area: 'values', title: 'Values', entries: [], end: 0, dropTarget: false }],
            dropRemoves: false, defer: { label: 'Defer', ticked: false }, update: { label: 'Update', enabled: false },
        };
        const list: PivotFieldListSlotContext = {
            view: () => view, subscribe: listener => { listeners.add(listener); return () => { listeners.delete(listener); }; }, label: id => id,
            setSearch: () => undefined, toggle: () => undefined, openMenu: entry => { heard.push(`menu ${entry.area} ${String(entry.index)}`); },
            close: () => undefined, update: () => undefined, startDrag: () => undefined, dragOver: () => undefined, dragEnter: () => undefined,
            drop: () => undefined, dropOnList: () => undefined, endDrag: () => undefined, setDeferred: () => undefined,
            control: () => () => undefined, anchor: key => element => { if (element !== null) heard.push(`anchor ${key}`); }, area: () => () => undefined,
        };
        const slots = new RazorPivotSlots(new FakeDotNet(), { pivot: ['fieldList'], grid: [] }, noGrids);
        mountOf(slots, 'fieldList')(slotted(), list);
        const contents = rendered('<div data-ex-pivot-area="values"></div>', 1);
        slots.rendered(1, 1, contents);

        // A field ticked into Values: .NET is handed the pane under the next number, and Blazor patches its entry in a
        // round trip before .NET says it rendered, which is when the wiring stands.
        view = { ...view, areas: [{ area: 'values', title: 'Values', entries: [entryOf('values', 0, 'Sum of Key')], end: 1, dropTarget: false }] };
        for (const listener of listeners) listener();
        contents.setAttribute(drawnVersionAttribute, '2');
        contents.querySelector('[data-ex-pivot-area]')?.insertAdjacentHTML('beforeend',
            '<div data-ex-pivot-entry="entry:values:0" draggable="true"><button data-ex-pivot-press="entry:values:0">Sum of Key</button><span data-ex-pivot-anchor="entry:values:0"></span></div>');
        contents.querySelector<HTMLButtonElement>('[data-ex-pivot-press="entry:values:0"]')?.click();
        await Promise.resolve();
        // Another field takes the place before the wiring comes.
        view = { ...view, areas: [{ area: 'values', title: 'Values', entries: [entryOf('values', 0, 'Sum of Qty')], end: 1, dropTarget: false }] };
        for (const listener of listeners) listener();
        contents.setAttribute(drawnVersionAttribute, '3');
        slots.rendered(1, 2, contents);
        await vi.waitFor(() => { expect(heard).toEqual(['anchor entry:values:0']); });
        await Promise.resolve();
        expect(heard).toEqual(['anchor entry:values:0']);
        slots.dispose();
    });

    it('ADR-0410 section 2, ADR-0059: a sheet tab and its close are pressed in the browser by the tab\'s key, and each tab is handed in by it', () => {
        const heard: string[] = [];
        const handed = new Map<string, HTMLElement | null>();
        const tabs: PivotDetailsTabsSlotContext = {
            view: () => ({ name: 'Sheets', tabs: [{ key: 'report', title: 'PivotTable', selected: false }, { key: 'd1', title: 'Rates', selected: true, panelId: 'p1', closeLabel: 'Rates' }] }),
            subscribe: () => () => undefined, label: id => id,
            select: key => { heard.push(`select ${key}`); }, close: key => { heard.push(`close ${key}`); },
            control: key => element => { handed.set(key, element); },
        };
        const slots = new RazorPivotSlots(new FakeDotNet(), { pivot: ['detailsTabs'], grid: [] }, noGrids);
        mountOf(slots, 'detailsTabs')(slotted(), tabs);
        const contents = rendered(
            '<button data-ex-pivot-tab="report" data-ex-pivot-control="report">PivotTable</button>'
            + '<span><button data-ex-pivot-tab="d1" data-ex-pivot-control="d1">Rates</button><button data-ex-pivot-close-tab="d1">x</button></span>', 1);
        slots.rendered(1, 1, contents);
        contents.querySelector<HTMLButtonElement>('[data-ex-pivot-tab="report"]')?.click();
        contents.querySelector<HTMLButtonElement>('[data-ex-pivot-close-tab="d1"]')?.click();
        expect(heard).toEqual(['select report', 'close d1']);
        expect([...handed.keys()].toSorted()).toEqual(['d1', 'report']);
        slots.dispose();
    });

    it('ADR-0410 section 2, ADR-0059: Show Details\' dialog presses Close in the browser, and hands in Close and the records\' anchor', () => {
        const heard: string[] = [];
        const handed = new Map<string, HTMLElement | null>();
        const dialog: PivotDetailsDialogSlotContext = {
            view: () => ({ title: 'Rates', closeLabel: 'Close' }), subscribe: () => () => undefined, label: id => id,
            close: () => { heard.push('close'); },
            control: name => element => { handed.set(`control ${name}`, element); }, anchor: name => element => { handed.set(`anchor ${name}`, element); },
        };
        const slots = new RazorPivotSlots(new FakeDotNet(), { pivot: ['detailsDialog'], grid: [] }, noGrids);
        mountOf(slots, 'detailsDialog')(slotted(), dialog);
        const contents = rendered('<div data-ex-pivot-anchor="records"></div><button data-ex-pivot-press="close" data-ex-pivot-control="close">Close</button>', 1);
        slots.rendered(1, 1, contents);
        contents.querySelector<HTMLButtonElement>('button')?.click();
        expect(heard).toEqual(['close']);
        expect([...handed.keys()].toSorted()).toEqual(['anchor records', 'control close']);
        slots.dispose();
        expect([...handed.values()]).toEqual([null, null]);
    });
});
