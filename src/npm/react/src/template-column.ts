// A Template Column for React (ADR-0020, ADR-0350 section 16): the core mounts a cell's content into a slot it
// keeps, and this mount hands React the slot to render into, as a portal of the ExGrid the slot stands in. The
// portal is keyed by the slot, so a cell mounted again, for a focus request or for a new version of its row
// under a Row Key, is the same component given new props, and keeps its state.

import { createElement, useReducer, type ReactElement, type ReactNode } from 'react';
import { createPortal, flushSync } from 'react-dom';
import type { GridColumn, GridTemplateCell } from '@ex-grid/core';

/** A Template Column's declaration: a column's, whose cells React renders in place of their value's text. */
export type TemplateColumnOptions<Row> = Omit<GridColumn<Row>, 'template' | 'actions'>;

/** What a Template cell shows: the column's render of the cell, rendered as a component of its own, so a
 * render that throws reaches the Consumer's error boundary. */
function TemplateCell({ show }: { readonly show: () => ReactNode }): ReactNode {
    return show();
}

/**
 * The portals of one ExGrid's Template cells. The core mounts and takes away content in its own pass, outside
 * React; each change asks for one render of the ExGrid, made at once in the next microtask, so a cell's content
 * stands, and its control has taken a keyboard it was asked to, before the browser hands over the next input
 * (the spine's sixth rule).
 */
export class TemplatePortals {
    readonly #portals = new Map<string, ReactElement>();
    readonly #render: () => void;
    #owed = false;
    #live = true;

    constructor(render: () => void) {
        this.#render = render;
    }

    /** A cell's content, made once per mount: the ExGrid rendering again hands React the same element, and
     * every cell whose content was not mounted again skips its render (ADR-0003). */
    set(id: string, slot: HTMLElement, show: () => ReactNode): void {
        this.#portals.set(id, createPortal(createElement(TemplateCell, { show }), slot, id));
        this.#owe();
    }

    delete(id: string): void {
        this.#portals.delete(id);
        this.#owe();
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
const portalsOf = new WeakMap<Element, TemplatePortals>();
const slotIds = new WeakMap<HTMLElement, string>();
let slots = 0;

/** Holds an ExGrid's portals, and a render of the component that asks for one. */
export function useTemplatePortals(): TemplatePortals {
    const [, rendered] = useReducer((count: number) => count + 1, 0);
    const [portals] = useReducer((held: TemplatePortals) => held, undefined, () => new TemplatePortals(rendered));
    return portals;
}

/** Ties an ExGrid's portals to the element it renders, for its Template cells to find. */
export function holdPortals(root: Element, portals: TemplatePortals): void {
    portalsOf.set(root, portals);
}

function portalsFor(slot: HTMLElement, column: string): TemplatePortals {
    for (let at: Element | null = slot.parentElement; at !== null; at = at.parentElement) {
        const portals = portalsOf.get(at);
        if (portals !== undefined) return portals;
    }
    throw new TypeError(`Column '${column}' is a React Template Column, which renders only inside an ExGrid of @ex-grid/react.`);
}

/**
 * A Template Column (ADR-0020): its cells render `render`'s answer in place of their value's text, which is
 * still read for sorting, filtering, Find and copy. `render` is handed the cell: its row, its column, and a
 * focus request, non-zero on exactly one render after Space engaged the cell, which a control answers by
 * handing itself to `takeKeyboard` from a layout effect (ADR-0037). A component it renders keeps its state
 * while its cell is painted, through a new version of its row where the source has a Row Key; without one a
 * new instance is another row (ADR-0140), rendered afresh. `render` only makes elements: a hook belongs in
 * the component it renders. Make the column once, outside the render
 * of the component that holds the grid, as every column: a new column is a new declaration, and every row
 * paints again (ADR-0003).
 */
export function templateColumn<Row>(column: TemplateColumnOptions<Row>, render: (cell: GridTemplateCell<Row>) => ReactNode): GridColumn<Row> {
    if (typeof render !== 'function') {
        throw new TypeError(`Column '${column.name}': render is a function of the cell, answering what it shows.`);
    }
    return Object.freeze({
        ...column,
        template: (slot: HTMLElement, cell: GridTemplateCell<Row>) => {
            const portals = portalsFor(slot, column.name);
            let id = slotIds.get(slot);
            if (id === undefined) {
                slots += 1;
                id = `ex-template-${slots}`;
                slotIds.set(slot, id);
            }
            const held = id;
            portals.set(held, slot, () => render(cell));
            return () => { portals.delete(held); };
        },
    });
}
