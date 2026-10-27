import { createGrid } from '@ex-grid/core';
import type { PivotShowDetailsEvent } from './details';
import { PivotView, type PivotViewOptions } from './pivot-view';

/** An ExPivot drawn into an element, handed back by {@link createPivot}. */
export interface PivotHandle {
    /** Takes the options again, whole: what did not change asks nothing again. */
    update(options: PivotViewOptions): void;
    /** Opens the records of a Show Details event this pivot raised, as `detailsView` says: what a Consumer that
     * heard `onShowDetails` and does not take them hands back (ADR-0059). */
    openDetails(event: PivotShowDetailsEvent): void;
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
    const view = new PivotView(element, options, createGrid);
    return Object.freeze({
        update: (next: PivotViewOptions) => { view.update(next); },
        openDetails: (event: PivotShowDetailsEvent) => { view.openDetails(event); },
        destroy: () => { view.destroy(); },
    });
}
