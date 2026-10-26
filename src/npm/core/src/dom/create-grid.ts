import type { CellPosition } from '../selection/cell';
import type { SelectionRange } from '../selection/range';
import { browserReports } from './browser-reports';
import { GridView, type GridOptions, type GridViewRead } from './grid-view';

/** A grid drawn into an element, handed back by {@link createGrid}. */
export interface GridHandle<Row> {
    /** Takes the options again, whole: what did not change costs nothing, and a row that did not change is
     * not touched (ADR-0003). */
    update(options: GridOptions<Row>): void;
    /** Places `range` as the one range with the Focus on `focus`, as a click leaves it, if the grid's source
     * still says `rowSequenceVersion` (ADR-0050 item 4): true once placed, and otherwise refused through
     * `onRefused` (kind `placement`) or held by an open edit's Reject. A range outside the grid throws a
     * RangeError. */
    placeSelection(range: SelectionRange, focus: CellPosition, rowSequenceVersion: number): boolean;
    /** The view as the grid holds it now (ADR-0331): the Selection, with the Row Sequence Version it is written in,
     * and the scroll offsets the grid last knew. A Consumer that shows several sets of rows in one grid keeps each
     * set's view to hand back with `showViewWith`. */
    readView(): GridViewRead;
    /**
     * Shows `view`, read of this grid, once the grid takes rows under `rowSequenceVersion`, the source's (ADR-0331):
     * its Selection in place of the empty one a new version leaves (ADR-0011), and its scroll offsets. Handed as the
     * Consumer brings the rows the view was read of, under the version it brings them with, the two arrive together
     * in the grid's next pass. A view
     * handed later replaces one not yet taken; a Selection that does not fit the rows then shown is dropped.
     */
    showViewWith(view: GridViewRead, rowSequenceVersion: number): void;
    /** Hands the keyboard to the grid: to the field an open edit is typed in, else to where the grid takes keys
     * (ADR-0080). A Consumer's control beside the grid, such as a Sheet's tabs, gives it back with this. */
    focus(): void;
    /** A key press in the grid's canonical form (ADR-0012), `Control+PageUp`, as this grid reads its own keys: Command
     * stands for Control on an Apple platform. A Consumer that hears keys beside the grid's names them so. */
    keyName(event: KeyboardEvent): string;
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
        readView: () => view.readView(),
        showViewWith: (state: GridViewRead, rowSequenceVersion: number) => { view.showViewWith(state, rowSequenceVersion); },
        focus: () => { view.focus(); },
        keyName: (event: KeyboardEvent) => view.keyName(event),
        destroy: () => { view.destroy(); },
    });
}
