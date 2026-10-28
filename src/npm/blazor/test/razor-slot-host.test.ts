// @vitest-environment happy-dom
// The Shared Script's Razor slot host (ADR-0410, ADR-0411) for a product with neither a Settled Mark nor an Input
// Order, as ExChart's is: a kind registered by name is mounted, crossed and wired, and a slot of no registered kind is
// refused; and the options a product still being made is held through. The grid's kinds over the host are
// razor-slots.test.ts'.
import { describe, expect, it, vi } from 'vitest';
import type { DotNetCalls } from '../src/held-messages';
import { drawnVersionAttribute, RazorSlotHost, slotHostOptionsOf, type RazorSlotKind, type SlotHostProduct } from '../src/razor-slot-host';

class FakeDotNet implements DotNetCalls {
    readonly calls: { readonly method: string; readonly args: readonly unknown[] }[] = [];

    invokeMethodAsync(method: string, ...args: readonly unknown[]): Promise<unknown> {
        this.calls.push({ method, args });
        return Promise.resolve(null);
    }
}

interface Toolbar {
    readonly name: string;
    readonly invoked: string[];
}

/** A toolbar whose context never changes under its contents: each button invokes its command. */
const toolbarKind: RazorSlotKind<Toolbar> = {
    takesKeyboard: false,
    data: toolbar => ({ json: JSON.stringify({ name: toolbar.name }), drawn: undefined, memo: undefined }),
    wire: (contents, toolbar) => {
        const press = (event: Event): void => {
            const target = event.target;
            if (target instanceof HTMLElement && target.dataset['exCommand'] !== undefined) toolbar.invoked.push(target.dataset['exCommand']);
        };
        contents.addEventListener('click', press);
        return () => { contents.removeEventListener('click', press); };
    },
};

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

describe('a Razor slot host without a Settled Mark or an Input Order', () => {
    it('ADR-0410 section 1: a registered kind is mounted, crossed by name and wired once its contents stand', () => {
        const dotnet = new FakeDotNet();
        const host = new RazorSlotHost(dotnet, new Map([['toolbar', toolbarKind]]));
        const slot = document.createElement('div');
        document.body.append(slot);
        const toolbar: Toolbar = { name: 'Sales', invoked: [] };
        const mount = host.mounts(['toolbar'])['toolbar'];
        if (mount === undefined) throw new Error('No mount for the toolbar.');
        mount(slot, toolbar);
        expect(dotnet.calls).toEqual([{ method: 'MountSlot', args: [1, 1, 'toolbar', '{"name":"Sales"}'] }]);

        const contents = rendered('<button data-ex-command="export">Export</button>', 1);
        host.rendered(1, 1, contents);
        expect(contents.parentElement).toBe(slot);
        contents.querySelector('button')?.click();
        expect(toolbar.invoked).toEqual(['export']);
    });

    it('ADR-0410 section 1: contents drawn again in a slot outside the page, which Blazor cannot find there, are the contents standing', () => {
        const host = new RazorSlotHost(new FakeDotNet(), new Map([['toolbar', toolbarKind]]));
        // A slot the core made and has not put in the page: a band with nothing to show.
        const slot = document.createElement('div');
        const toolbar: Toolbar = { name: 'Sales', invoked: [] };
        const mount = host.mounts(['toolbar'])['toolbar'];
        if (mount === undefined) throw new Error('No mount for the toolbar.');
        mount(slot, toolbar);
        const contents = rendered('<button data-ex-command="export">Export</button>', 1);
        host.rendered(1, 1, contents);
        expect(contents.parentElement).toBe(slot);

        host.rendered(1, 2, null);
        expect(contents.parentElement).toBe(slot);
        contents.querySelector('button')?.click();
        expect(toolbar.invoked).toEqual(['export']);
    });

    it('ADR-0410 section 1: a slot .NET drew no element for, which has no contents standing, or whose contents stand in the page, is refused by name', () => {
        const host = new RazorSlotHost(new FakeDotNet(), new Map([['toolbar', toolbarKind]]));
        const mount = host.mounts(['toolbar'])['toolbar'];
        if (mount === undefined) throw new Error('No mount for the toolbar.');
        const slot = document.createElement('div');
        mount(slot, { name: 'Sales', invoked: [] });
        expect(() => { host.rendered(1, 1, null); }).toThrow('.NET rendered no element for the slot 1.');
        // Contents in the page are found wherever they stand: not found, they are not the ones .NET drew.
        document.body.append(slot);
        host.rendered(1, 1, rendered('<button data-ex-command="export">Export</button>', 1));
        expect(() => { host.rendered(1, 2, null); }).toThrow('.NET rendered no element for the slot 1.');
        slot.remove();
    });

    it('ADR-0410 section 1: a slot of no kind the product registered is refused by name', () => {
        const host = new RazorSlotHost(new FakeDotNet(), new Map([['toolbar', toolbarKind]]));
        expect(host.fills('menu')).toBe(false);
        expect(() => host.mounts(['menu'])).toThrow('.NET named \'menu\' as a slot its Chrome fills, of no kind the product registered.');
    });

    it('ADR-0410 section 1: a kind that takes the keyboard is refused by a host with no Input Order to hold the inputs', () => {
        const menuKind: RazorSlotKind<Toolbar> = { ...toolbarKind, takesKeyboard: true };
        expect(() => new RazorSlotHost(new FakeDotNet(), new Map([['menu', menuKind]]))).toThrow('the slot \'menu\' takes the keyboard');
    });
});

describe('an input made on contents .NET patched ahead of their wiring', () => {
    interface Changing extends Toolbar {
        again: (() => void) | undefined;
    }

    /** A toolbar drawn again as it changes, whose wiring takes a press on contents newer than itself `later`. */
    const changingKind: RazorSlotKind<Changing> = {
        takesKeyboard: false,
        data: toolbar => ({ json: JSON.stringify({ name: toolbar.name }), drawn: undefined, memo: undefined }),
        subscribe: (toolbar, again) => { toolbar.again = again; return () => { toolbar.again = undefined; }; },
        wire: (contents, toolbar, _answered, _drawn, later) => {
            const wiredFor = contents.getAttribute(drawnVersionAttribute);
            const press = (event: Event): void => {
                const target = event.target;
                if (!(target instanceof HTMLElement) || target.dataset['exCommand'] === undefined) return;
                const command = target.dataset['exCommand'];
                if (contents.getAttribute(drawnVersionAttribute) === wiredFor) toolbar.invoked.push(command);
                else later(() => { toolbar.invoked.push(`${command}, wired for ${String(contents.getAttribute(drawnVersionAttribute))}`); });
            };
            contents.addEventListener('click', press);
            return () => { contents.removeEventListener('click', press); };
        },
    };

    function standing(): { readonly host: RazorSlotHost; readonly toolbar: Changing; readonly contents: HTMLElement; readonly order: (handling: () => void | PromiseLike<void>) => void; readonly unmount: () => void } {
        // An Input Order that takes each handling once the one before it has settled, as a grid's does.
        let last: Promise<void> = Promise.resolve();
        const order = (handling: () => void | PromiseLike<void>): void => { last = last.then(() => handling()); };
        const host = new RazorSlotHost(new FakeDotNet(), new Map([['toolbar', changingKind]]), { order });
        const slot = document.createElement('div');
        document.body.append(slot);
        const toolbar: Changing = { name: 'Sales', invoked: [], again: undefined };
        const mount = host.mounts(['toolbar'])['toolbar'];
        if (mount === undefined) throw new Error('No mount for the toolbar.');
        const unmount = mount(slot, toolbar);
        const contents = rendered('<button data-ex-command="export">Export</button>', 1);
        host.rendered(1, 1, contents);
        // Changed: .NET is handed the data numbered 2, and Blazor patches them in a round trip before .NET says so.
        toolbar.again?.();
        contents.setAttribute(drawnVersionAttribute, '2');
        contents.insertAdjacentHTML('beforeend', '<button data-ex-command="print">Print</button>');
        return { host, toolbar, contents, order, unmount };
    }

    it('ADR-0410 section 2, ADR-0240: it is taken once the contents are wired for what they show, before any input made after it', async () => {
        const { host, toolbar, contents, order } = standing();
        contents.querySelector<HTMLElement>('[data-ex-command="print"]')?.click();
        order(() => { toolbar.invoked.push('made after'); });
        await Promise.resolve();
        expect(toolbar.invoked).toEqual([]);
        host.rendered(1, 2, contents);
        await vi.waitFor(() => { expect(toolbar.invoked).toEqual(['print, wired for 2', 'made after']); });
    });

    it('ADR-0410 section 2: one whose contents were drawn again before its wiring came is taken by the wiring of what they show, which judges it', async () => {
        const { host, toolbar, contents, order } = standing();
        contents.querySelector<HTMLElement>('[data-ex-command="print"]')?.click();
        order(() => { toolbar.invoked.push('made after'); });
        // Changed again before the wiring came: the wiring stands for what the contents show.
        toolbar.again?.();
        contents.setAttribute(drawnVersionAttribute, '3');
        host.rendered(1, 2, contents);
        await vi.waitFor(() => { expect(toolbar.invoked).toEqual(['print, wired for 3', 'made after']); });
    });

    it('ADR-0410 section 2: one whose slot goes before the wiring comes is taken nowhere, and holds nothing after it', async () => {
        const { toolbar, contents, order, unmount } = standing();
        contents.querySelector<HTMLElement>('[data-ex-command="print"]')?.click();
        order(() => { toolbar.invoked.push('made after'); });
        unmount();
        await vi.waitFor(() => { expect(toolbar.invoked).toEqual(['made after']); });
    });
});

describe('the keys that toggle a control in a Razor slot', () => {
    it('ADR-0410 section 3: Space on a checkbox stays the browser\'s, never reaching a component library\'s handler above the contents', () => {
        const host = new RazorSlotHost(new FakeDotNet(), new Map([['toolbar', toolbarKind]]));
        const slot = document.createElement('div');
        document.body.append(slot);
        const mount = host.mounts(['toolbar'])['toolbar'];
        if (mount === undefined) throw new Error('No mount for the toolbar.');
        mount(slot, { name: 'Sales', invoked: [] });
        const contents = rendered('<input type="checkbox"><input type="text">', 1);
        host.rendered(1, 1, contents);
        const heard: string[] = [];
        const hear = (event: KeyboardEvent): void => {
            if (!(event.target instanceof HTMLInputElement)) throw new Error('A key on no input.');
            heard.push(`${event.target.type} ${event.key}`);
        };
        document.addEventListener('keydown', hear);
        try {
            for (const input of contents.querySelectorAll('input')) {
                for (const key of [' ', 'Delete', 'Enter']) input.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true }));
            }
        } finally {
            document.removeEventListener('keydown', hear);
        }
        expect(heard).toEqual(['checkbox Enter', 'text  ', 'text Delete', 'text Enter']);
    });
});

describe('the options over a product being made', () => {
    it('ADR-0410 section 5: a hold or an order taken while the product is made holds it once it stands, before the task ends; handing the keyboard back then is a defect', async () => {
        const made: { product: SlotHostProduct | undefined } = { product: undefined };
        const options = slotHostOptionsOf(() => made.product, 'grid');
        const slot = document.createElement('div');
        let held = 0;
        const released: string[] = [];
        const ordered: Promise<void>[] = [];
        const release = options.hold?.(slot);
        options.order?.(() => Promise.resolve(), slot);
        expect(() => { options.keyboardBack?.(slot); }).toThrow('Internal: a Razor slot held the keyboard while the grid was being made.');
        made.product = {
            holdSettled: () => { held += 1; return () => { released.push('held'); }; },
            inOrder: handling => {
                const handled = Promise.resolve(handling());
                ordered.push(handled);
                return handled;
            },
            focus: () => undefined,
        };
        await Promise.resolve();
        expect([held, ordered.length]).toEqual([1, 1]);
        release?.();
        expect(released).toEqual(['held']);

        // Let go before the product stood: nothing is held for it.
        made.product = undefined;
        const early = options.hold?.(slot);
        early?.();
        made.product = { holdSettled: () => { held += 1; return () => undefined; }, inOrder: () => Promise.resolve(), focus: () => undefined };
        await Promise.resolve();
        expect(held).toBe(1);
    });
});
