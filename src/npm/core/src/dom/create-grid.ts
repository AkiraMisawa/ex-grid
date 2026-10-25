import { browserReports } from './browser-reports';
import { GridView, type GridOptions } from './grid-view';

/** A grid drawn into an element, handed back by {@link createGrid}. */
export interface GridHandle<Row> {
    /** Takes the options again, whole: what did not change costs nothing, and a row that did not change is
     * not touched (ADR-0003). */
    update(options: GridOptions<Row>): void;
    /** Stops the grid: it hears nothing more and empties its element. A source it was handed stays the
     * Consumer's. */
    destroy(): void;
}

/**
 * Draws a grid into `element`, which becomes its instance root (ADR-0350 section 16): the grid owns
 * everything inside it, and the element's own attributes it writes. A Framework Adapter renders the element
 * and hands the grid its props; nothing here assumes React's lifecycle or Blazor's.
 */
export function createGrid<Row>(element: HTMLElement, options: GridOptions<Row>): GridHandle<Row> {
    const view = new GridView(element, options, browserReports);
    return Object.freeze({
        update: (next: GridOptions<Row>) => { view.update(next); },
        destroy: () => { view.destroy(); },
    });
}
