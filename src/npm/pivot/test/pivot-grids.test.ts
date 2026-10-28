// @vitest-environment happy-dom
// The grids a pivot draws, as a Framework Adapter reaches them (ADR-0240 section 6, ADR-0410 section 5): which grid
// takes the inputs and holds the Settled Mark for a part, by the Input Root the part stands in, and a hold taken
// before that grid stands.
import { describe, expect, it } from 'vitest';
import type { GridHandle, GridOptions } from '@ex-grid/core';
import { PivotGrids } from '../src/create-pivot';

/** A grid that counts the holds of its Settled Mark still taken. */
interface CountedGrid {
    readonly name: string;
    held: number;
}

function inputRoot(): HTMLElement {
    const box = document.createElement('div');
    box.setAttribute('data-ex-input-root', '');
    return box;
}

/** Grids that count their holds, named by `name` from their options' locale; `asMade` runs as a grid is made, before
 * it is handed back, as a grid's first paint mounts its parts. */
function counting(asMade: (root: HTMLElement) => void = () => undefined): { readonly made: CountedGrid[]; make: <Row>(root: HTMLElement, options: GridOptions<Row>) => GridHandle<Row> } {
    const made: CountedGrid[] = [];
    return {
        made,
        make: <Row>(root: HTMLElement, options: GridOptions<Row>): GridHandle<Row> => {
            const name = options.locale;
            if (name === undefined) throw new Error('Each grid is named by its locale.');
            const counted: CountedGrid = { name, held: 0 };
            made.push(counted);
            asMade(root);
            const stand = {
                holdSettled: () => {
                    counted.held += 1;
                    return () => { counted.held -= 1; };
                },
                destroy: () => undefined,
            };
            return stand as unknown as GridHandle<Row>;
        },
    };
}

function gridOptions(name: string): GridOptions<unknown> {
    return { columns: [], locale: name };
}

describe('the grids a pivot draws', () => {
    it('ADR-0410 section 5: a part placed in a Show Details grid holds that grid, and one in the pivot\'s box the report\'s', () => {
        const pivot = inputRoot();
        document.body.append(pivot);
        const { made, make } = counting();
        const grids = new PivotGrids(pivot, make);
        const reportRoot = document.createElement('div');
        pivot.append(reportRoot);
        grids.mount(reportRoot, gridOptions('report'));
        const details = inputRoot();
        const detailsRoot = document.createElement('div');
        details.append(detailsRoot);
        pivot.append(details);
        grids.mount(detailsRoot, gridOptions('details'));

        const inDetails = document.createElement('div');
        detailsRoot.append(inDetails);
        const toolbar = document.createElement('div');
        pivot.append(toolbar);
        const releaseDetails = grids.holdSettled(inDetails);
        const releaseToolbar = grids.holdSettled(toolbar);
        expect(made.map(grid => [grid.name, grid.held])).toEqual([['report', 1], ['details', 1]]);
        releaseDetails();
        releaseToolbar();
        expect(made.map(grid => grid.held)).toEqual([0, 0]);
    });

    it('ADR-0410 section 5: a part a Show Details grid mounts as it is made, before placing it, holds that grid once it stands', () => {
        const pivot = inputRoot();
        document.body.append(pivot);
        let release: (() => void) | undefined;
        // A Show Details grid's first paint mounts its loading indicator's box before placing it in the root.
        const { made, make } = counting(root => {
            if (root.parentElement === pivot) return;
            const box = document.createElement('div');
            release = grids.holdSettled(box);
            root.append(box);
        });
        const grids: PivotGrids = new PivotGrids(pivot, make);
        const reportRoot = document.createElement('div');
        pivot.append(reportRoot);
        grids.mount(reportRoot, gridOptions('report'));
        const details = inputRoot();
        const detailsRoot = document.createElement('div');
        details.append(detailsRoot);
        pivot.append(details);
        grids.mount(detailsRoot, gridOptions('details'));

        // Held on every grid while it stood nowhere, then on the details grid it came to stand in.
        expect(made.map(grid => [grid.name, grid.held])).toEqual([['report', 1], ['details', 1]]);
        release?.();
        expect(made.map(grid => grid.held)).toEqual([0, 0]);
    });

    it('ADR-0410 section 5: a hold taken while the report draws no grid holds the report\'s next grid, and one let go first holds nothing', () => {
        const pivot = inputRoot();
        document.body.append(pivot);
        const { made, make } = counting();
        const grids = new PivotGrids(pivot, make);
        const toolbar = document.createElement('div');
        const menu = document.createElement('div');
        pivot.append(toolbar, menu);
        const releaseToolbar = grids.holdSettled(toolbar);
        grids.holdSettled(menu)();
        expect(grids.around(toolbar)).toBeUndefined();

        const reportRoot = document.createElement('div');
        pivot.append(reportRoot);
        grids.mount(reportRoot, gridOptions('report'));
        expect(made.map(grid => [grid.name, grid.held])).toEqual([['report', 1]]);
        releaseToolbar();
        expect(made.map(grid => grid.held)).toEqual([0]);
    });
});
