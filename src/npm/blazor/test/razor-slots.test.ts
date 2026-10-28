// @vitest-environment happy-dom
// The Razor Chrome's own tests (ADR-0410 sections 1 and 5): what moves between a slot the core made and the holder
// .NET renders beside the root, what crosses to .NET as each slot comes and goes, and that the grid's Settled Mark
// is held while contents are owed. Wiring a menu's items is the core's (core/test/slot-wiring.test.ts).
import { describe, expect, it } from 'vitest';
import {
    filterPanelAttributes, findPanelAttributes, type FilterPanelView, type FindPanelView, type GridColumnMenuSlotContext, type GridFilterPanelSlotContext,
    type GridFindPanelSlotContext, type GridLoadingIndicatorSlotContext,
} from '@ex-grid/core';
import type { DotNetHandle } from '../src/dotnet-source';
import { drawnVersionAttribute, RazorSlots } from '../src/razor-slots';

class FakeDotNet implements DotNetHandle {
    readonly calls: { readonly method: string; readonly args: readonly unknown[] }[] = [];

    invokeMethodAsync(method: string, ...args: readonly unknown[]): Promise<unknown> {
        this.calls.push({ method, args });
        return Promise.resolve(null);
    }

    methods(): readonly string[] {
        return this.calls.map(call => call.method);
    }
}

/** The grid's Settled Mark, as holds on it come and go. */
class Mark {
    held = 0;

    readonly hold = (): (() => void) => {
        this.held += 1;
        let released = false;
        return () => {
            if (released) return;
            released = true;
            this.held -= 1;
        };
    };
}

const loading = (shown: boolean): GridLoadingIndicatorSlotContext => ({ loading: shown, placeholderRows: 0 });

function menu(focusRequest: number, focused: HTMLElement[]): GridColumnMenuSlotContext {
    return {
        column: 'Book',
        columnType: 'text',
        letter: () => undefined,
        commands: [{ id: 'sort-ascending', enabled: true, invoke: () => undefined }],
        label: id => `words for ${id}`,
        close: () => undefined,
        focusRequest,
        focusItem: 0,
        resolveKey: () => ({ kind: 'none', item: -1 }),
        itemFocused: () => undefined,
        innerPopupChanged: () => undefined,
        takeFocus: control => { focused.push(control); },
    };
}

/** .NET's render of a slot's contents: the holder beside the root, and the one element in it. */
function rendered(markup = ''): HTMLElement {
    const holder = document.createElement('div');
    holder.hidden = true;
    const contents = document.createElement('div');
    contents.innerHTML = markup;
    holder.append(contents);
    document.body.append(holder);
    return contents;
}

/** The inputs held behind contents owed, as the grid's Input Order holds them. */
class Order {
    owed = 0;

    readonly order = (until: Promise<void>): void => {
        this.owed += 1;
        void until.then(() => { this.owed -= 1; });
    };
}

/** The grid's root, which `keyboardBack` hands the keyboard to. */
interface Made {
    readonly dotnet: FakeDotNet;
    readonly mark: Mark;
    readonly order: Order;
    readonly slots: RazorSlots;
    readonly slot: HTMLElement;
    readonly root: HTMLElement;
}

function made(): Made {
    const dotnet = new FakeDotNet();
    const mark = new Mark();
    const order = new Order();
    const root = document.createElement('div');
    root.tabIndex = 0;
    const slot = document.createElement('div');
    root.append(slot);
    document.body.append(root);
    return { dotnet, mark, order, slots: new RazorSlots(dotnet, { hold: mark.hold, order: order.order, keyboardBack: () => { root.focus(); } }), slot, root };
}

const mountOf = (slots: RazorSlots, name: 'columnMenu' | 'filterPanel' | 'findPanel' | 'loadingIndicator') => {
    const mount = slots.chrome([name])[name];
    if (mount === undefined) throw new Error(`No mount for ${name}.`);
    return mount as (slot: HTMLElement, context: unknown) => () => void;
};

describe('a slot filled with Razor', () => {
    it('ADR-0410 section 1: a mount asks .NET for the contents with the context as data, and they move into the slot once rendered', () => {
        const { dotnet, slots, slot } = made();
        mountOf(slots, 'loadingIndicator')(slot, loading(true));
        expect(dotnet.calls).toEqual([{ method: 'MountSlot', args: [1, 1, 'loadingIndicator', '{"loading":true,"placeholderRows":0}'] }]);
        const contents = rendered();
        slots.rendered(1, 1, contents);
        expect(contents.parentElement).toBe(slot);
    });

    it('ADR-0410 section 5: the grid is not settled while a mounted slot\'s contents are owed', () => {
        const { mark, slots, slot } = made();
        mountOf(slots, 'loadingIndicator')(slot, loading(true));
        expect(mark.held).toBe(1);
        slots.rendered(1, 1, rendered());
        expect(mark.held).toBe(0);
    });

    it('ADR-0410 section 1: the cleanup moves the contents back, and tells .NET, at the end of the task', async () => {
        const { dotnet, mark, slots, slot } = made();
        const cleanup = mountOf(slots, 'loadingIndicator')(slot, loading(true));
        const contents = rendered();
        const holder = contents.parentElement;
        slots.rendered(1, 1, contents);
        cleanup();
        expect(dotnet.methods()).toEqual(['MountSlot']);
        await Promise.resolve();
        expect([contents.parentElement, dotnet.methods()]).toEqual([holder, ['MountSlot', 'UnmountSlot']]);
        expect(mark.held).toBe(0);
    });

    it('ADR-0410, ADR-0240: contents that hold the keyboard as their slot goes hand it to the grid, never to the page\'s body', async () => {
        const { slots, slot, root } = made();
        const cleanup = mountOf(slots, 'loadingIndicator')(slot, loading(true));
        const contents = rendered('<button>Filter</button>');
        slots.rendered(1, 1, contents);
        contents.querySelector('button')?.focus();
        cleanup();
        slot.remove();
        await Promise.resolve();
        expect(document.activeElement).toBe(root);
    });

    it('ADR-0410: contents mounted again in the task, as for a focus request, never move, and keep the keyboard they hold', async () => {
        const { slots, slot } = made();
        const mount = mountOf(slots, 'loadingIndicator');
        const cleanup = mount(slot, loading(true));
        const contents = rendered('<button>Filter</button>');
        slots.rendered(1, 1, contents);
        const button = contents.querySelector('button');
        button?.focus();
        let blurred = false;
        button?.addEventListener('blur', () => { blurred = true; });
        cleanup();
        mount(slot, loading(true));
        await Promise.resolve();
        expect([contents.parentElement, document.activeElement, blurred]).toEqual([slot, button, false]);
    });

    it('ADR-0410 section 1: the same slot mounted again in the task keeps its contents, and .NET draws them for the new context', async () => {
        const { dotnet, slots, slot } = made();
        const mount = mountOf(slots, 'loadingIndicator');
        const cleanup = mount(slot, loading(true));
        const contents = rendered();
        slots.rendered(1, 1, contents);
        cleanup();
        mount(slot, loading(false));
        await Promise.resolve();
        expect(contents.parentElement).toBe(slot);
        expect(dotnet.calls.slice(1)).toEqual([{ method: 'UpdateSlot', args: [1, 2, '{"loading":false,"placeholderRows":0}'] }]);
    });

    it('ADR-0410 section 5: a render of data since replaced leaves the grid unsettled until the data last sent stand', () => {
        const { mark, slots, slot } = made();
        const mount = mountOf(slots, 'loadingIndicator');
        mount(slot, loading(true))();
        mount(slot, loading(false));
        const contents = rendered();
        slots.rendered(1, 1, contents);
        expect([contents.parentElement, mark.held]).toEqual([slot, 1]);
        slots.rendered(1, 2, contents);
        expect(mark.held).toBe(0);
    });

    it('ADR-0410 section 1: contents rendered for a slot already gone stay in their holder', async () => {
        const { slots, slot } = made();
        mountOf(slots, 'loadingIndicator')(slot, loading(true))();
        await Promise.resolve();
        const contents = rendered();
        const holder = contents.parentElement;
        slots.rendered(1, 1, contents);
        expect(contents.parentElement).toBe(holder);
    });

    it('ADR-0390: the Chrome is the same object while its slots are, so a standing part is not drawn again for an update', () => {
        const { slots } = made();
        expect(slots.chrome(['columnMenu', 'loadingIndicator'])).toBe(slots.chrome(['columnMenu', 'loadingIndicator']));
        expect(slots.chrome(['columnMenu'])).not.toBe(slots.chrome(['columnMenu', 'loadingIndicator']));
    });
});

describe('a menu filled with Razor', () => {
    it('ADR-0410 section 3: .NET is handed the menu as data, the core\'s words included', () => {
        const { dotnet, slots, slot } = made();
        mountOf(slots, 'columnMenu')(slot, menu(1, []));
        expect(dotnet.calls[0]?.args).toEqual([1, 1, 'columnMenu',
            '{"column":"Book","columnType":"text","commands":[{"id":"sort-ascending","label":"words for sort-ascending","enabled":true}]}']);
    });

    it('ADR-0410 section 5: a focus request asks nothing of .NET, and the contents standing take the keyboard for it at once', () => {
        const { dotnet, mark, slots, slot } = made();
        const focused: HTMLElement[] = [];
        const mount = mountOf(slots, 'columnMenu');
        const cleanup = mount(slot, menu(1, focused));
        slots.rendered(1, 1, rendered('<button data-ex-menu-item="0">Sort</button>'));
        cleanup();
        mount(slot, menu(2, focused));
        expect([dotnet.methods(), focused.length, mark.held]).toEqual([['MountSlot'], 2, 0]);
    });

    it('ADR-0039: the contents take the keyboard for a request once, not again as .NET draws them again for it a round trip later', () => {
        const { slots, slot } = made();
        const focused: HTMLElement[] = [];
        mountOf(slots, 'columnMenu')(slot, menu(1, focused));
        const contents = rendered('<button data-ex-menu-item="0">Sort</button>');
        slots.rendered(1, 1, contents);
        slots.rendered(1, 1, contents);
        expect(focused.length).toBe(1);
    });
});

/** A filter panel's context whose view the test moves, and the values it was asked to tick. */
function filter(): { readonly context: GridFilterPanelSlotContext; move(view: FilterPanelView): void; readonly ticked: unknown[] } {
    const listeners = new Set<() => void>();
    let view: FilterPanelView = valueList(['Novak', 'Osei']);
    const ticked: unknown[] = [];
    const context: GridFilterPanelSlotContext = {
        column: 'Trader', columnType: 'text', dateType: 'dateTime', mode: 'valueList', current: undefined,
        label: id => `words for ${id}`,
        view: () => view,
        subscribe: listener => { listeners.add(listener); return () => { listeners.delete(listener); }; },
        setSearch: () => undefined, setAddToCurrent: () => undefined,
        setChecked: (value, checked) => { ticked.push([value, checked]); },
        toggleAll: () => undefined, setOperator: () => undefined, setOperand: () => undefined, setCombinator: () => undefined,
        setSecond: () => undefined, setSecondOperand: () => undefined, apply: () => undefined, cancel: () => undefined,
        valueListKey: () => false, focusRequest: 0, focusTarget: undefined, innerPopupChanged: () => undefined, takeFocus: () => undefined,
    };
    return {
        context, ticked,
        move: next => { view = next; for (const listener of listeners) listener(); },
    };
}

function valueList(values: readonly string[]): FilterPanelView {
    return {
        kind: 'valueList', search: '', addOffered: false, addToCurrent: false, selectAll: 'checked', canApply: true, fetching: false,
        entries: values.map(value => ({ value, text: value, blank: false, checked: true })),
    };
}

/** The entries of the value list .NET was handed. */
const entries = (text: unknown): unknown => (JSON.parse(String(text)) as { readonly valueList: { readonly entries: unknown } }).valueList.entries;

/** .NET's render of a filter panel: a checkbox per entry of the view numbered `version`, which the contents carry. */
function drawnPanel(version: number, view: FilterPanelView, contents = rendered()): HTMLElement {
    contents.setAttribute(drawnVersionAttribute, String(version));
    contents.innerHTML = view.kind === 'valueList'
        ? view.entries.map((_, index) => `<input type="checkbox" checked ${filterPanelAttributes.value}="${String(index)}">`).join('')
        : '';
    return contents;
}

describe('a filter panel filled with Razor', () => {
    it('ADR-0410 section 3: .NET is handed the view as data, and drawn again at each change the core makes to it', () => {
        const { dotnet, slots, slot } = made();
        const panel = filter();
        mountOf(slots, 'filterPanel')(slot, panel.context);
        panel.move(valueList(['Osei']));
        const sent = dotnet.calls.map(call => [call.method, call.args[1], entries(call.args.at(-1))]);
        expect(sent).toEqual([
            ['MountSlot', 1, [{ text: 'Novak', blank: false, checked: true }, { text: 'Osei', blank: false, checked: true }]],
            ['UpdateSlot', 2, [{ text: 'Osei', blank: false, checked: true }]],
        ]);
    });

    it('ADR-0410 section 2: a checkbox ticks the value of the entry it was drawn for, in the view whose number the contents carry', () => {
        const { slots, slot } = made();
        const panel = filter();
        mountOf(slots, 'filterPanel')(slot, panel.context);
        const contents = drawnPanel(1, panel.context.view());
        slots.rendered(1, 1, contents);
        panel.move(valueList(['Osei']));
        const second = contents.querySelector<HTMLInputElement>(`[${filterPanelAttributes.value}="1"]`);
        if (second === null) throw new Error('No second checkbox.');
        second.checked = false;
        second.dispatchEvent(new Event('change', { bubbles: true }));
        expect(panel.ticked).toEqual([['Osei', false]]);
    });

    it('ADR-0240 / ADR-0410 section 5: inputs wait in their order while a part the keyboard reaches owes its contents, and not for a loading bar', async () => {
        const { slots, slot, order } = made();
        mountOf(slots, 'loadingIndicator')(document.createElement('div'), loading(true));
        expect(order.owed).toBe(0);
        const panel = filter();
        mountOf(slots, 'filterPanel')(slot, panel.context);
        expect(order.owed).toBe(1);
        slots.rendered(2, 1, drawnPanel(1, panel.context.view()));
        await Promise.resolve();
        expect(order.owed).toBe(0);
    });
});

describe('a focus request while a newer drawing is owed', () => {
    it('ADR-0410 section 5: it waits for the contents drawn from the data last sent, and so does every input after it', async () => {
        const { slots, slot, order } = made();
        const panel = filter();
        const focused: HTMLElement[] = [];
        const asking = (target: 'first' | undefined, request: number): GridFilterPanelSlotContext =>
            ({ ...panel.context, focusRequest: request, focusTarget: target, takeFocus: control => { focused.push(control); } });
        const mount = mountOf(slots, 'filterPanel');
        const first = drawnPanel(1, panel.context.view());
        first.querySelector('input')?.setAttribute(filterPanelAttributes.focus, 'first');
        const leave = mount(slot, asking(undefined, 0));
        slots.rendered(1, 1, first);
        await Promise.resolve();
        expect(order.owed).toBe(0);
        // The value list moves; before .NET draws it, the core asks the keyboard onto the panel's first control,
        // taking the slot away and mounting it again in one task.
        panel.move(valueList(['Osei']));
        leave();
        mount(slot, asking('first', 1));
        expect([focused.length, order.owed]).toEqual([0, 1]);
        const second = drawnPanel(2, panel.context.view(), first);
        second.querySelector('input')?.setAttribute(filterPanelAttributes.focus, 'first');
        slots.rendered(1, 2, second);
        await Promise.resolve();
        expect([focused.length, order.owed]).toEqual([1, 0]);
    });
});

describe('a filter panel whose value list is on its way', () => {
    it('spine rule 6, ADR-0410 section 5: a search asked for waits with every input after it until the list is drawn, never landing in the form it replaces', async () => {
        const { slots, slot, order } = made();
        const panel = filter();
        const conditions: FilterPanelView = {
            kind: 'conditions', allowed: ['equals', 'contains'], first: { operator: 'equals', operand: '', takesOperand: true, refusal: undefined },
            combinator: 'and', second: undefined, canApply: true, fetching: true,
        };
        panel.move(conditions);
        const focused: HTMLElement[] = [];
        const context: GridFilterPanelSlotContext = { ...panel.context, focusRequest: 1, focusTarget: 'search', takeFocus: control => { focused.push(control); } };
        mountOf(slots, 'filterPanel')(slot, context);
        const form = rendered(`<input ${filterPanelAttributes.operand}="first" ${filterPanelAttributes.focus}="search">`);
        form.setAttribute(drawnVersionAttribute, '1');
        slots.rendered(1, 1, form);
        await Promise.resolve();
        expect([focused.length, order.owed]).toEqual([0, 1]);

        panel.move(valueList(['Novak', 'Osei']));
        form.innerHTML = `<input ${filterPanelAttributes.search}="" ${filterPanelAttributes.focus}="first search">`;
        form.setAttribute(drawnVersionAttribute, '2');
        slots.rendered(1, 2, form);
        await Promise.resolve();
        expect([focused.map(control => control.hasAttribute(filterPanelAttributes.search)), order.owed]).toEqual([[true], 0]);
    });
});

describe('a find panel filled with Razor', () => {
    it('ADR-0410 sections 2 and 3: .NET is handed the panel as data and draws it again as a step\'s outcome is said; the field takes the keyboard, and what is typed is the core\'s', () => {
        const { dotnet, slots, slot } = made();
        const listeners = new Set<() => void>();
        let view: FindPanelView = { text: 'Osei', matchCase: false, wholeCell: true, replaceShown: false, replaceText: '', replaceWaiting: false, outcome: '' };
        const typed: string[] = [];
        const focused: HTMLElement[] = [];
        const context: GridFindPanelSlotContext = {
            view: () => view,
            subscribe: listener => { listeners.add(listener); return () => { listeners.delete(listener); }; },
            label: id => `words for ${id}`,
            setText: text => { typed.push(text); }, setMatchCase: () => undefined, setWholeCell: () => undefined, setReplaceText: () => undefined,
            next: () => undefined, previous: () => undefined, replace: () => undefined, replaceAll: () => undefined, close: () => undefined,
            focusRequest: 1, innerPopupChanged: () => undefined, takeFocus: control => { focused.push(control); },
        };
        mountOf(slots, 'findPanel')(slot, context);
        // What is typed is the field's own, and asks nothing of .NET; what the step came to is drawn.
        view = { ...view, text: 'Osei Lee' };
        for (const listener of listeners) listener();
        view = { ...view, outcome: 'No match' };
        for (const listener of listeners) listener();
        const sent = dotnet.calls.map(call => ({ method: call.method, version: call.args[1], json: JSON.parse(String(call.args.at(-1))) as Record<string, unknown> }));
        expect(sent.map(each => [each.method, each.version, each.json['outcome']])).toEqual([['MountSlot', 1, ''], ['UpdateSlot', 2, 'No match']]);
        expect(sent[0]?.json).toMatchObject({ text: 'Osei', wholeCell: true, replaceShown: false, words: { next: 'words for find-next', replaceAll: 'words for find-replace-all' } });
        expect(sent[1]?.json['text']).toBe('Osei');
        const contents = rendered(`<input ${findPanelAttributes.field}="find">`);
        slots.rendered(1, 2, contents);
        const field = contents.querySelector('input');
        if (field === null) throw new Error('No field.');
        field.value = 'Novak';
        field.dispatchEvent(new Event('input', { bubbles: true }));
        expect([focused, typed]).toEqual([[field], ['Novak']]);
    });
});
