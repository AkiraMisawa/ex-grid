import { createGrid, type GridHandle, type GridOptions } from '@ex-grid/core';
import type { PivotShowDetailsEvent } from './details';
import { PivotView, type PivotViewOptions } from './pivot-view';

/** An ExPivot drawn into an element, handed back by {@link createPivot}. */
export interface PivotHandle {
    /** Takes the options again, whole: what did not change asks nothing again. */
    update(options: PivotViewOptions): void;
    /** Opens the records of a Show Details event this pivot raised, as `detailsView` says: what a Consumer that
     * heard `onShowDetails` and does not take them hands back (ADR-0059). */
    openDetails(event: PivotShowDetailsEvent): void;
    /**
     * The grid that takes the inputs made at `element` (ADR-0240 section 6): a Show Details grid's, where `element`
     * stands in that grid's Input Root, else the report's; undefined while the report draws no grid. For a Framework
     * Adapter whose Chrome's contents arrive later than their slot, to hold that grid's inputs and Settled Mark until
     * they stand (ADR-0410 section 5).
     */
    gridAround(element: Element): GridHandle<unknown> | undefined;
    /** Holds the Settled Mark (ADR-0220) of the grid `element` stands in, as `gridAround` finds it, until the
     * function it answers is called. Where no grid stands there yet, the hold waits for the grid that comes to: the
     * report's next one while the report draws none, and a grid being made for a part it mounted before placing it,
     * which meanwhile holds every grid. */
    holdSettled(element: Element): () => void;
    /** Stops the pivot: it asks and hears nothing more, and empties its element. The source stays the
     * Consumer's. */
    destroy(): void;
}

/**
 * Draws an ExPivot into `element`, which becomes its root (ADR-0370 section 1): the report, drawn by ExGrid,
 * beside the Field List. The pivot owns everything inside it; a Framework Adapter renders the element and
 * hands the pivot its props, and nothing here assumes React's lifecycle or Blazor's.
 */
export function createPivot(element: HTMLElement, options: PivotViewOptions): PivotHandle {
    const grids = new PivotGrids(element);
    const view = new PivotView(element, options, (root, gridOptions) => grids.mount(root, gridOptions));
    return Object.freeze({
        update: (next: PivotViewOptions) => { view.update(next); },
        openDetails: (event: PivotShowDetailsEvent) => { view.openDetails(event); },
        gridAround: (within: Element) => grids.around(within),
        holdSettled: (within: Element) => grids.holdSettled(within),
        destroy: () => { view.destroy(); },
    });
}

/** A hold of the Settled Mark taken before the grid it holds stood: let go, or handed to that grid's once it stands. */
interface WaitingHold {
    readonly element: Element;
    readonly releases: (() => void)[];
    gone: boolean;
}

/** Draws a grid into an element: ExGrid's `createGrid`, or a layer-2 test's own. */
export type MakeGrid = <Row>(root: HTMLElement, options: GridOptions<Row>) => GridHandle<Row>;

/** The grids a pivot draws, each by its root: the report's, and Show Details'. */
export class PivotGrids {
    readonly #pivot: HTMLElement;
    readonly #make: MakeGrid;
    readonly #grids = new Map<HTMLElement, GridHandle<unknown>>();
    readonly #waiting = new Set<WaitingHold>();

    constructor(pivot: HTMLElement, make: MakeGrid = createGrid) {
        this.#pivot = pivot;
        this.#make = make;
    }

    mount<Row>(root: HTMLElement, options: GridOptions<Row>): GridHandle<Row> {
        const grid = this.#make(root, options);
        // The grid's own members, its getters among them, with a destroy that forgets it first.
        // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- Object.create answers any; the members are the grid's
        const handle = Object.freeze(Object.create(grid, {
            destroy: { value: () => { this.#grids.delete(root); grid.destroy(); }, enumerable: true },
        })) as GridHandle<Row>;
        // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- kept to hold and order, which read no row
        this.#grids.set(root, handle as unknown as GridHandle<unknown>);
        // A hold taken before this grid stood holds it, once what it was taken for stands in the grid's Input Root:
        // the report's while the report drew no grid, and a part the grid mounted as it was made, before its first
        // paint placed it.
        const inputRoot = this.#inputRootOf(root);
        for (const waiting of this.#waiting) {
            if (this.#inputRootOf(waiting.element) !== inputRoot) continue;
            this.#waiting.delete(waiting);
            if (!waiting.gone) waiting.releases.push(handle.holdSettled());
        }
        return handle;
    }

    around(element: Element): GridHandle<unknown> | undefined {
        const inputRoot = this.#inputRootOf(element);
        const report = this.#gridIn(this.#pivot);
        if (inputRoot === this.#pivot || inputRoot === undefined) return report;
        return this.#gridIn(inputRoot) ?? report;
    }

    holdSettled(element: Element): () => void {
        const inputRoot = this.#inputRootOf(element);
        const grid = inputRoot === undefined ? undefined : this.#gridIn(inputRoot);
        if (grid !== undefined) return grid.holdSettled();
        // No grid stands where the element does yet. One not yet placed holds every grid now, as it may stand in any,
        // and each waits for the grid it comes to stand in.
        const waiting: WaitingHold = { element, releases: inputRoot === undefined ? [...this.#grids.values()].map(each => each.holdSettled()) : [], gone: false };
        this.#waiting.add(waiting);
        return () => {
            waiting.gone = true;
            this.#waiting.delete(waiting);
            for (const release of waiting.releases) release();
        };
    }

    // The grid whose Input Root is `inputRoot`.
    #gridIn(inputRoot: HTMLElement): GridHandle<unknown> | undefined {
        for (const [root, grid] of this.#grids) {
            if (this.#inputRootOf(root) === inputRoot) return grid;
        }
        return undefined;
    }

    // The Input Root an element stands in: the pivot's, or a Show Details grid's; undefined for one outside the pivot,
    // or not yet in the document.
    #inputRootOf(element: Element): HTMLElement | undefined {
        const around = element.parentElement?.closest<HTMLElement>('[data-ex-input-root]');
        return around === null || around === undefined || !this.#pivot.contains(around) ? undefined : around;
    }
}
