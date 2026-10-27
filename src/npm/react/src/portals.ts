// The portals of one ExGrid (ADR-0350 section 9): the core mounts content into slots it keeps, a Template cell's
// (ADR-0020) or a Chrome slot's (ADR-0390), and this hands React each slot to render into, as a portal of the
// ExGrid the slot stands in. A portal is keyed by its slot, so content mounted again on the same slot, for a
// focus request or for a new version of its row, is the same component given new props, and keeps its state.

import { createElement, useReducer, type ReactElement, type ReactNode } from 'react';
import { createPortal, flushSync } from 'react-dom';

/** What a slot shows: its render, rendered as a component of its own, so a render that throws reaches the
 * Consumer's error boundary. */
function SlotContent({ show }: { readonly show: () => ReactNode }): ReactNode {
    return show();
}

/**
 * The portals of one ExGrid. The core mounts and takes away content in its own pass, outside React; each change
 * asks for one render of the ExGrid, made at once in the next microtask, so a slot's content stands, and its
 * control has taken a keyboard it was asked to, before the browser hands over the next input (the spine's sixth
 * rule).
 */
export class GridPortals {
    readonly #portals = new Map<string, ReactElement>();
    readonly #render: () => void;
    #owed = false;
    #live = true;

    constructor(render: () => void) {
        this.#render = render;
    }

    /** A slot's content, made once per mount: the ExGrid rendering again hands React the same element, and every
     * slot whose content was not mounted again skips its render (ADR-0003). */
    set(id: string, slot: HTMLElement, show: () => ReactNode): void {
        this.#portals.set(id, createPortal(createElement(SlotContent, { show }), slot, id));
        this.#owe();
    }

    delete(id: string): void {
        this.#portals.delete(id);
        this.#owe();
    }

    /** Mounts `show` into `slot`, keyed by the slot, and answers the cleanup the core calls: the core's mount
     * function's shape, `(slot, context) => cleanup` (ADR-0350 section 9). */
    mount(slot: HTMLElement, show: () => ReactNode): () => void {
        const id = slotIdOf(slot);
        this.set(id, slot, show);
        return () => { this.delete(id); };
    }

    /** A grid is made, after one this component held went, as StrictMode makes it again: it renders again. */
    begin(): void {
        this.#live = true;
    }

    /** The grid is going, and its content with it: nothing renders for it again. */
    end(): void {
        this.#live = false;
        this.#portals.clear();
    }

    /** The portals, as the ExGrid renders them beside its root. */
    elements(): ReactElement[] {
        return [...this.#portals.values()];
    }

    #owe(): void {
        if (this.#owed || !this.#live) return;
        this.#owed = true;
        queueMicrotask(() => {
            this.#owed = false;
            if (this.#live) flushSync(this.#render);
        });
    }
}

/** The portals of each ExGrid, by the element it renders: the grid's instance root. */
const portalsOf = new WeakMap<Element, GridPortals>();
const slotIds = new WeakMap<HTMLElement, string>();
let slots = 0;

/** A slot's key, the same for as long as the slot stands. */
function slotIdOf(slot: HTMLElement): string {
    let id = slotIds.get(slot);
    if (id === undefined) {
        slots += 1;
        id = `ex-slot-${slots}`;
        slotIds.set(slot, id);
    }
    return id;
}

/** Holds an ExGrid's portals, and a render of the component that asks for one. */
export function useGridPortals(): GridPortals {
    const [, rendered] = useReducer((count: number) => count + 1, 0);
    const [portals] = useReducer((held: GridPortals) => held, undefined, () => new GridPortals(rendered));
    return portals;
}

/** Ties an ExGrid's portals to the element it renders, for its slots to find. */
export function holdPortals(root: Element, portals: GridPortals): void {
    portalsOf.set(root, portals);
}

/** The portals of the ExGrid `slot` stands in, or undefined outside one. */
export function portalsAround(slot: HTMLElement): GridPortals | undefined {
    for (let at: Element | null = slot.parentElement; at !== null; at = at.parentElement) {
        const portals = portalsOf.get(at);
        if (portals !== undefined) return portals;
    }
    return undefined;
}
