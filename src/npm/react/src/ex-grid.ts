// The React Framework Adapter (ADR-0350 section 3): it renders the element the Script Core draws into,
// hands the core its props, and unmounts it. It holds no state of the grid's, draws nothing inside the
// instance root, and decides nothing. The rows are never React's: a component per row would repeat,
// through `memo`, the fight with re-rendering that ADR-0003 measured.

import { createElement, useLayoutEffect, useRef, type ReactElement } from 'react';
import { createGrid, type GridHandle, type GridOptions } from '@ex-grid/core';

/** An ExGrid's props: the Script Core's options, whole. */
export type ExGridProps<Row> = GridOptions<Row>;

/**
 * An Excel-like grid. The element it renders is the grid's instance root, which the Script Core owns; every
 * render hands the core the props again, and what did not change costs nothing (ADR-0003).
 */
export function ExGrid<Row>(props: ExGridProps<Row>): ReactElement {
    const element = useRef<HTMLDivElement>(null);
    const grid = useRef<GridHandle<Row> | undefined>(undefined);
    // Drawn before the browser paints, so a mounted grid is never seen empty.
    useLayoutEffect(() => {
        const root = element.current;
        if (root === null) throw new Error('Internal: React mounted no element for the grid to draw into.');
        if (grid.current === undefined) {
            grid.current = createGrid(root, props);
        } else {
            grid.current.update(props);
        }
    });
    useLayoutEffect(() => () => {
        grid.current?.destroy();
        grid.current = undefined;
    }, []);
    return createElement('div', { ref: element });
}
