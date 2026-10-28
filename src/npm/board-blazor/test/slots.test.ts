// @vitest-environment happy-dom
// A Board's Razor slots (ADR-0410): which slots each kind of view is dressed with, as .NET names them, and that a slot
// holds what the view it stands in holds. How a slot's contents move in and out, and what their controls do, is the
// Shared Script's host's and each product's kinds' (their own tests); what the views draw is the Script Core's.
import { describe, expect, it } from 'vitest';
import type { BoardHandle, BoardMounts, BoardOptions, ShapedRow } from '@ex-grid/board';
import type { GridHandle } from '@ex-grid/core';
import type { PivotHandle, PivotViewOptions } from '@ex-grid/pivot';
import { attach, type DotNetHandle, type DrawBoard, type WireAttach, type WireRazorSlots } from '../src/blazor-board';

const boardJson = `{"version": 1, "inputs": {"trades": {"columns": [{"name": "desk", "type": "text"}]}},
  "views": [{"id": "blotter", "grid": {"data": "trades", "columns": [{"column": "desk"}]}}],
  "layout": [{"views": ["blotter"]}]}`;

class FakeDotNet implements DotNetHandle {
    readonly calls: { readonly method: string; readonly args: readonly unknown[] }[] = [];

    invokeMethodAsync(method: string, ...args: readonly unknown[]): Promise<unknown> {
        this.calls.push({ method, args });
        return Promise.resolve(null);
    }

    of(method: string): readonly (readonly unknown[])[] {
        return this.calls.filter(call => call.method === method).map(call => call.args);
    }
}

function attachJson(razorSlots: WireRazorSlots | null): string {
    const init: WireAttach = {
        options: { width: 1200, locale: 'en-GB', hears: [] }, circuit: false, board: boardJson, data: {}, sent: [], sources: {}, diff: null, diffsSent: 0, razorSlots,
    };
    return JSON.stringify(init);
}

/** A slot the loading bar's kind fills, mounted in `within` by a view's Chrome as a view mounts its part, where the
 * Chrome fills it. */
function mountLoading(chrome: unknown, within: HTMLElement): HTMLElement | undefined {
    const mount: unknown = typeof chrome === 'object' && chrome !== null ? Reflect.get(chrome, 'loadingIndicator') : undefined;
    if (mount === undefined) return undefined;
    if (typeof mount !== 'function') throw new Error('The loading bar\'s mount is no function.');
    const slot = document.createElement('div');
    // Mounted before it is placed, as a grid mounts its loading bar.
    (mount as (slot: HTMLElement, context: unknown) => () => void)(slot, { loading: true, placeholderRows: 0 });
    within.append(slot);
    return slot;
}

/** What a stand-in view was asked to hold, and what it let go. */
class Holds {
    readonly asked: (Element | undefined)[] = [];
    released = 0;

    hold(element?: Element): () => void {
        this.asked.push(element);
        return () => { this.released++; };
    }
}

/** The Board's stand-in: it makes one grid view and one pivot view through the mounts it is handed, each handed its
 * kind's Chrome as the Board hands it, and each mounting the loading bar's slot as it is made, as a view mounts a part
 * as it stands. */
function drawing(grid: Holds, pivot: Holds, slots: HTMLElement[], drawnWith: BoardOptions[], viewsGiven: { readonly chrome?: unknown }[] = []): DrawBoard {
    return (root, options, withViews) => {
        drawnWith.push(options);
        const family: BoardMounts = {
            grid: (element, gridOptions) => {
                viewsGiven.push(gridOptions);
                const slot = mountLoading(gridOptions.chrome, element);
                if (slot !== undefined) slots.push(slot);
                return { holdSettled: () => grid.hold(), update: (next: object) => { viewsGiven.push(next); } } as unknown as GridHandle<ShapedRow>;
            },
            pivot: (element, pivotOptions) => {
                viewsGiven.push(pivotOptions);
                const report = document.createElement('div');
                element.append(report);
                const slot = mountLoading(pivotOptions.chrome?.grid, report);
                if (slot !== undefined) slots.push(slot);
                return {
                    holdSettled: (at: Element) => pivot.hold(at), gridAround: () => undefined, update: (next: object) => { viewsGiven.push(next); },
                } as unknown as PivotHandle;
            },
            chart: () => { throw new Error('The Board draws no chart here.'); },
        };
        const mounts = withViews(family);
        const gridElement = document.createElement('div');
        const pivotElement = document.createElement('div');
        root.append(gridElement, pivotElement);
        const chrome = options.chrome;
        const gridOptions = { columns: [], source: undefined as never, ...(chrome?.grid === undefined ? {} : { chrome: chrome.grid }) };
        const pivotChrome = chrome?.pivot ?? (chrome?.grid === undefined ? undefined : { grid: chrome.grid });
        const pivotOptions: object = pivotChrome === undefined ? {} : { chrome: pivotChrome };
        const gridView = mounts.grid(gridElement, gridOptions);
        const pivotView = mounts.pivot(pivotElement, pivotOptions as PivotViewOptions);
        // Handed their options again, as the Board does for an update: the same Chrome objects.
        gridView.update(gridOptions);
        pivotView.update(pivotOptions as PivotViewOptions);
        const handle: BoardHandle = { update: () => undefined, destroy: () => undefined, bound: undefined };
        return handle;
    };
}

describe('A Board\'s Razor slots', () => {
    it('ADR-0410: each kind of view is dressed with the slots .NET names, the report grids with those .NET names for them, the same Chrome for the Board\'s life', async () => {
        const drawnWith: BoardOptions[] = [];
        const viewsGiven: { readonly chrome?: unknown }[] = [];
        const board = await attach(document.createElement('div'), new FakeDotNet(),
            attachJson({ grid: ['loadingIndicator'], pivot: ['menu'], pivotGrid: ['contextMenu', 'loadingIndicator'] }), [],
            drawing(new Holds(), new Holds(), [], drawnWith, viewsGiven));
        const chrome = drawnWith[0]?.chrome;
        if (chrome?.grid === undefined || chrome.pivot?.grid === undefined) throw new Error('The Board was handed no grid Chrome, or its pivots none.');
        expect(Object.keys(chrome.grid)).toEqual(['loadingIndicator']);
        expect(Object.keys(chrome.pivot).toSorted()).toEqual(['grid', 'menu']);
        expect(Object.keys(chrome.pivot.grid).toSorted()).toEqual(['contextMenu', 'loadingIndicator']);
        // One mount for one slot name, whichever views it dresses: .NET reads the name, never the view.
        expect(chrome.pivot.grid.loadingIndicator).toBe(chrome.grid.loadingIndicator);
        expect(chrome.chart).toBeUndefined();
        // Each view is handed the same Chrome for an update as it was made with: a part it keeps standing keeps its
        // mount function, and is not drawn again.
        const [gridMade, pivotMade, gridAgain, pivotAgain] = viewsGiven.map(given => given.chrome);
        expect(gridAgain).toBe(gridMade);
        expect(pivotAgain).toBe(pivotMade);
        await board.update(JSON.stringify({ options: { width: 900, locale: 'en-GB', hears: [] }, sent: [] }), []);
        expect(drawnWith).toHaveLength(1);
        board.dispose();
    });

    it('ADR-0410: a Board whose Chromes fill no slot is handed no Chrome, and a name of no kind is refused by name', async () => {
        const drawnWith: BoardOptions[] = [];
        await attach(document.createElement('div'), new FakeDotNet(), attachJson(null), [], drawing(new Holds(), new Holds(), [], drawnWith));
        expect(drawnWith[0]?.chrome).toBeUndefined();
        await expect(attach(document.createElement('div'), new FakeDotNet(), attachJson({ grid: ['toolbar'], pivot: [], pivotGrid: [] }), [],
            drawing(new Holds(), new Holds(), [], []))).rejects.toThrow('\'toolbar\' as a grid slot');
    });

    it('ADR-0410 section 5: a slot mounted as its view is made holds that view\'s Settled Mark once it is, until .NET has drawn it', async () => {
        const dotnet = new FakeDotNet();
        const grid = new Holds();
        const pivot = new Holds();
        const slots: HTMLElement[] = [];
        const board = await attach(document.createElement('div'), dotnet, attachJson({ grid: ['loadingIndicator'], pivot: [], pivotGrid: ['loadingIndicator'] }), [],
            drawing(grid, pivot, slots, []));
        // The grid view holds its own; the pivot holds the grid the slot stands in, which it finds from the slot.
        expect(grid.asked).toEqual([undefined]);
        expect(pivot.asked).toEqual([slots[1]]);
        const mounted = dotnet.of('MountSlot');
        expect(mounted.map(([, , name]) => name)).toEqual(['loadingIndicator', 'loadingIndicator']);
        for (const [id, version] of mounted) {
            const holder = document.createElement('div');
            const contents = document.createElement('div');
            holder.append(contents);
            document.body.append(holder);
            board.slotRendered(id as number, version as number, contents);
        }
        expect([grid.released, pivot.released]).toEqual([1, 1]);
        expect(slots[0]?.firstElementChild).not.toBeNull();
        board.dispose();
    });
});
