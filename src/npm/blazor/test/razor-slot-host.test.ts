// @vitest-environment happy-dom
// The Shared Script's Razor slot host (ADR-0410, ADR-0411) for a product with neither a Settled Mark nor an Input
// Order, as ExChart's is: a kind registered by name is mounted, crossed and wired, and a slot of no registered kind is
// refused. The grid's kinds over the host are razor-slots.test.ts'.
import { describe, expect, it } from 'vitest';
import type { DotNetCalls } from '../src/held-messages';
import { drawnVersionAttribute, RazorSlotHost, type RazorSlotKind } from '../src/razor-slot-host';

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
