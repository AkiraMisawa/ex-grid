// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): a view read of the grid and handed back (ADR-0331). A Consumer that shows several
// sets of rows in one grid, one at a time, keeps each set's Selection and offsets and hands them back with the rows,
// under the version it brings them with: the grid takes the view in place of the empty Selection a new version
// leaves (ADR-0011), and one that does not fit the rows is dropped as any would be.
import { describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import type { GridOptions } from '../src/dom/grid-view';
import type { GridSelection } from '../src/selection/selection';
import { columns, mount, painted, q, scrollTo, trades, type Trade } from './grid-harness';

function press(root: HTMLElement, x: number, y: number): void {
    for (const name of ['mousedown', 'mouseup']) {
        const event = new MouseEvent(name, { bubbles: true, cancelable: true, button: 0, buttons: name === 'mouseup' ? 0 : 1 });
        Object.defineProperty(event, 'offsetX', { value: x });
        Object.defineProperty(event, 'offsetY', { value: y });
        q(root, '.ex-viewport').dispatchEvent(event);
    }
}

type Mounted = ReturnType<typeof mount<Trade>>;

async function grid(): Promise<{ readonly root: HTMLElement; readonly view: Mounted['view']; readonly show: (rows: readonly Trade[]) => void; readonly selections: GridSelection[] }> {
    const selections: GridSelection[] = [];
    const options: GridOptions<Trade> = { columns, rows: trades(200), rowHeight: 24, viewportHeight: 240, onSelectionChanged: selection => { selections.push(selection); } };
    const { root, view } = mount<Trade>(options);
    await painted();
    // Other rows, as a Consumer hands them: a source of their own, its order new to the grid.
    const show = (rows: readonly Trade[]): void => { view.update({ ...options, rows }); };
    return { root, view, show, selections };
}

describe('a view read and handed back (ADR-0331)', () => {
    it('ADR-0331: the view handed back with the rows it was read of comes back with them: its Selection and its offsets', async () => {
        const { root, view, show, selections } = await grid();
        press(root, 10, (3 * 24) + 5);
        await painted();
        await scrollTo(root, 480);
        const read = view.readView();
        expect([read.selection.focus, read.scrollTopPx]).toEqual([{ row: 3, column: 0 }, 480]);
        await scrollTo(root, 0);
        show(trades(200).toReversed());
        await painted();
        expect(defined(selections.at(-1), 'a Selection').isEmpty).toBe(true);

        show(trades(200));
        view.showViewWith(read, read.rowSequenceVersion);
        await painted();
        expect(defined(selections.at(-1), 'a Selection').focus).toEqual({ row: 3, column: 0 });
        expect(q(root, '.ex-scroller').scrollTop).toBe(480);
    });

    it('ADR-0331: a view whose Selection does not fit the rows then shown is dropped, its offsets kept inside them; one this grid did not read is refused', async () => {
        const { root, view, show, selections } = await grid();
        await scrollTo(root, 480);
        press(root, 10, (20 * 24) + 5);
        await painted();
        const read = view.readView();
        show(trades(12));
        view.showViewWith(read, read.rowSequenceVersion);
        await painted();
        expect(defined(selections.at(-1), 'a Selection').isEmpty).toBe(true);
        // Twelve rows of 24px under a 24px band in a 240px Viewport scroll 72px at most.
        expect([view.readView().scrollTopPx, q(root, '.ex-scroller').scrollTop]).toEqual([72, 72]);
        expect(() => { view.showViewWith({ ...read }, read.rowSequenceVersion); }).toThrow(TypeError);
    });
});
