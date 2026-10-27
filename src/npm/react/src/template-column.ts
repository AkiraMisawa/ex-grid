// A Template Column for React (ADR-0020, ADR-0350 section 16): the core mounts a cell's content into a slot it
// keeps, and this mount hands React the slot to render into, through the ExGrid's portals. A cell mounted again,
// for a focus request or for a new version of its row under a Row Key, is the same component given new props,
// and keeps its state.

import type { ReactNode } from 'react';
import type { GridColumn, GridTemplateCell } from '@ex-grid/core';
import { portalsAround } from './portals';

/** A Template Column's declaration: a column's, whose cells React renders in place of their value's text. */
export type TemplateColumnOptions<Row> = Omit<GridColumn<Row>, 'template' | 'actions'>;

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
            const portals = portalsAround(slot);
            if (portals === undefined) {
                throw new TypeError(`Column '${column.name}' is a React Template Column, which renders only inside an ExGrid of @ex-grid/react.`);
            }
            return portals.mount(slot, () => render(cell));
        },
    });
}
