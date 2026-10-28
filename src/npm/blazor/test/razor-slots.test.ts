// @vitest-environment happy-dom
// The Razor Chrome's own tests (ADR-0410 sections 1 and 5): what moves between a slot the core made and the holder
// .NET renders beside the root, what crosses to .NET as each slot comes and goes, and that the grid's Settled Mark
// is held while contents are owed. Wiring a menu's items is the core's (core/test/slot-wiring.test.ts).
import { describe, expect, it } from 'vitest';
import type { GridColumnMenuSlotContext, GridLoadingIndicatorSlotContext } from '@ex-grid/core';
import type { DotNetHandle } from '../src/dotnet-source';
import { RazorSlots } from '../src/razor-slots';

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

function made(): { readonly dotnet: FakeDotNet; readonly mark: Mark; readonly slots: RazorSlots; readonly slot: HTMLElement } {
    const dotnet = new FakeDotNet();
    const mark = new Mark();
    const slot = document.createElement('div');
    document.body.append(slot);
    return { dotnet, mark, slots: new RazorSlots(dotnet, mark.hold), slot };
}

const mountOf = (slots: RazorSlots, name: 'columnMenu' | 'loadingIndicator') => {
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

    it('ADR-0410 section 1: the cleanup moves the contents back at once, and .NET is told at the end of the task', async () => {
        const { dotnet, mark, slots, slot } = made();
        const cleanup = mountOf(slots, 'loadingIndicator')(slot, loading(true));
        const contents = rendered();
        const holder = contents.parentElement;
        slots.rendered(1, 1, contents);
        cleanup();
        expect([contents.parentElement, dotnet.methods()]).toEqual([holder, ['MountSlot']]);
        await Promise.resolve();
        expect(dotnet.methods()).toEqual(['MountSlot', 'UnmountSlot']);
        expect(mark.held).toBe(0);
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
