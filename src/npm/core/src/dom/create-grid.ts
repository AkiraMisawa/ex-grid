import type { CellPosition } from '../selection/cell';
import type { SelectionRange } from '../selection/range';
import { browserReports } from './browser-reports';
import { GridView, type GridOptions } from './grid-view';

/** A grid drawn into an element, handed back by {@link createGrid}. */
export interface GridHandle<Row> {
    /** Takes the options again, whole: what did not change costs nothing, and a row that did not change is
     * not touched (ADR-0003). */
    update(options: GridOptions<Row>): void;
    /**
     * Places the Selection (ADR-0050, item 4): `range` becomes the one range, with the Focus on `focus`, scrolled
     * into view, as the Name Box's address or an undo's range asks. Taken against `rowSequenceVersion`, the
     * source's when the Consumer chose the place: false, and nothing placed, once the rows' order has moved since,
     * or while an edit stands. A range or a Focus outside the grid is refused with a RangeError.
     */
    placeSelection(range: SelectionRange, focus: CellPosition, rowSequenceVersion: number): boolean;
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
        placeSelection: (range: SelectionRange, focus: CellPosition, rowSequenceVersion: number) => view.placeSelection(range, focus, rowSequenceVersion),
        destroy: () => { view.destroy(); },
    });
}
