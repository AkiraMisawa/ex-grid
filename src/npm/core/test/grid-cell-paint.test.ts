// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): a cell's Cell State (ADR-0006), and what a Consumer that holds every cell declares
// per cell (ADR-0050, items 1, 6, 7, 11 and 15; ADR-0051), as a Sheet declares it: a cell's own kind, alignment and painted text, its Font, Fill
// and Borders through generated classes, the text an edit opens on, and a header click that selects.
import { describe, expect, it } from 'vitest';
import type { CellAppearance } from '../src/cells/cell-appearance';
import type { GridEditIntent } from '../src/cells/edit-intent';
import type { GridColumn } from '../src/dom/grid-column';
import type { GridOptions } from '../src/dom/grid-view';
import type { GridSelection } from '../src/selection/selection';
import { GridSource } from '../src/source/grid-source';
import { mount, painted, q } from './grid-harness';

/** A row of a Sheet as the test holds it: each cell its value and how it looks. */
interface Line {
    readonly cells: readonly (string | number)[];
    readonly looks?: readonly CellAppearance[];
}

const sheetColumns: readonly GridColumn<Line>[] = ['A', 'B', 'C'].map((name, i) => ({
    name, type: 'text', width: 80, editable: true, queryable: false,
    value: (line: Line) => line.cells[i],
}));

const indexOf = (column: GridColumn<Line>): number => sheetColumns.findIndex(c => c.name === column.name);
const cellType: GridOptions<Line>['cellType'] = (line, column) => (typeof line.cells[indexOf(column)] === 'number' ? 'number' : 'text');
const lookOf = (line: Line, column: GridColumn<Line>): CellAppearance => line.looks?.[indexOf(column)] ?? {}; // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0050 item 15: a cell the test gives no look has none

const boldLook = (): CellAppearance => ({ bold: true });

/** A key typed where the keyboard is. */
function keyOn(name: string): void {
    const target = document.activeElement;
    if (target === null) throw new Error('Nothing holds the keyboard.');
    target.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true }));
}

function sheet(lines: readonly Line[], options: Partial<GridOptions<Line>> = {}): HTMLElement {
    return mount({ columns: sheetColumns, rows: lines, rowHeight: 24, viewportHeight: 480, ...options }).root;
}

const cellAt = (root: HTMLElement, row: number, column: number): HTMLElement =>
    q(root, `.ex-viewport > .ex-row[aria-rowindex="${row + 2}"] > [aria-colindex="${column + 1}"]`);

describe('Per-cell declarations (ADR-0050)', () => {
    it('ADR-0006: a Cell State is painted as its class and nothing more, and the function\'s identity is the change signal', async () => {
        const lines: readonly Line[] = [{ cells: ['a', 1, 'c'] }];
        let state: 'missing' | 'stale' = 'missing';
        const asked: GridOptions<Line>['cellState'] = (_line, column) => (indexOf(column) === 1 ? state : 'normal');
        const { root, view } = mount({ columns: sheetColumns, rows: lines, rowHeight: 24, viewportHeight: 480, cellState: asked });
        await painted();
        expect([0, 1, 2].map(c => cellAt(root, 0, c).className)).toEqual(['ex-cell', 'ex-cell ex-state-missing', 'ex-cell']);
        expect(cellAt(root, 0, 1).textContent).toBe('1');
        // The same function answering otherwise leaves the states on screen where they are (ADR-0003).
        state = 'stale';
        view.update({ columns: sheetColumns, rows: lines, rowHeight: 24, viewportHeight: 480, cellState: asked });
        await painted();
        expect(cellAt(root, 0, 1).className).toBe('ex-cell ex-state-missing');
        view.update({ columns: sheetColumns, rows: lines, rowHeight: 24, viewportHeight: 480, cellState: (line, column) => asked(line, column) });
        await painted();
        expect(cellAt(root, 0, 1).className).toBe('ex-cell ex-state-stale');
        expect(() => { view.update({ columns: sheetColumns, rows: lines, rowHeight: 24, viewportHeight: 480, cellState: 'missing' as never }); })
            .toThrow(/cellState is a function \(ADR-0006\)/);
    });

    it('ADR-0050 item 6: a cell declared a Number in a Text column is right-aligned and becomes #### when it does not fit', async () => {
        const root = sheet([{ cells: ['label', 123, 12_345_678_901_234] }], { cellType });
        await painted();
        expect(cellAt(root, 0, 0).className).toBe('ex-cell');
        expect(cellAt(root, 0, 1).className).toBe('ex-cell ex-cell-numeric');
        expect(cellAt(root, 0, 1).textContent).toBe('123');
        expect(cellAt(root, 0, 2).querySelector('.ex-hashes')?.textContent).toMatch(/^#+$/u);
        expect(cellAt(root, 0, 2).getAttribute('aria-label')).toBe('12345678901234');
    });

    it('ADR-0050 items 7 and 11: a cell\'s own alignment, and its painted text fitted to the content width, named by its value', async () => {
        const widths: number[] = [];
        const root = sheet([{ cells: ['left', 0.123_456_789] }], {
            cellType,
            cellAlign: (_line, column) => (indexOf(column) === 0 ? 'center' : 'auto'),
            paintedText: (line, column, contentWidthPx) => {
                widths.push(contentWidthPx);
                const value = line.cells[indexOf(column)];
                return typeof value === 'number' ? value.toFixed(3) : undefined;
            },
        });
        await painted();
        expect(cellAt(root, 0, 0).className).toBe('ex-cell ex-align-center');
        expect(cellAt(root, 0, 0).textContent).toBe('left');
        expect(cellAt(root, 0, 0).hasAttribute('aria-label')).toBe(false);
        expect(cellAt(root, 0, 1).textContent).toBe('0.123');
        expect(cellAt(root, 0, 1).getAttribute('aria-label')).toBe('0.123456789');
        // The column's width less the cell's padding on both sides.
        expect(widths[0]).toBeLessThan(80);
        expect(widths[0]).toBeGreaterThan(60);
    });

    it('ADR-0071: a Font and a Fill are interned classes whose rules the generated stylesheet, last in the root, holds', async () => {
        const red = 0xff_00_00;
        const yellow = 0xff_ff_00;
        const root = sheet([
            { cells: ['bold red', 'filled', 'plain'], looks: [{ fontColour: red, bold: true }, { fill: yellow }, {}] },
            { cells: ['bold red', '', ''], looks: [{ fontColour: red, bold: true }] },
        ], { cellAppearance: lookOf });
        await painted();
        // The Fill covers the gridline its left neighbour holds, beneath any line there (the fourteenth run, case 16).
        expect(cellAt(root, 0, 0).className).toBe('ex-cell ex-font-ff0000b ex-lined ex-lr-cover-ffff00');
        expect(cellAt(root, 1, 0).className).toBe('ex-cell ex-font-ff0000b');
        expect(cellAt(root, 0, 1).className).toBe('ex-cell ex-fill-ffff00');
        expect(cellAt(root, 0, 2).className).toBe('ex-cell');
        const style = root.lastElementChild;
        expect(style?.tagName).toBe('STYLE');
        expect(style?.textContent).toContain('.ex-cell:not(.ex-state-stale, .ex-state-error).ex-font-ff0000b{color:#ff0000;font-weight:700;}');
        expect(style?.textContent).toContain('.ex-cell.ex-fill-ffff00{background-color:#ffff00;');
    });

    it('ADR-0071: an edge is drawn once, from either cell\'s record, the upper cell holding the gridline and a thick line reaching past it', async () => {
        const thick: CellAppearance = { bottom: { style: 'thick', colour: 0x00_00_ff } };
        const root = sheet([{ cells: ['a', 'b', 'c'], looks: [thick] }, { cells: ['d', 'e', 'f'], looks: [{ top: { style: 'thin' } }] }], {
            cellAppearance: lookOf,
            // Where both record a line, the Consumer chooses; here the lower cell's.
            edgeBorder: (_upper, lower) => lower,
        });
        await painted();
        expect(cellAt(root, 0, 0).className).toBe('ex-cell ex-lined ex-lb-thin-000000');
        expect(cellAt(root, 1, 0).className).toBe('ex-cell');
        const root2 = sheet([{ cells: ['a'], looks: [thick] }, { cells: ['d'] }], { cellAppearance: lookOf });
        await painted();
        expect(cellAt(root2, 0, 0).className).toBe('ex-cell ex-lined ex-lb-thick-0000ff');
        expect(cellAt(root2, 1, 0).className).toBe('ex-cell ex-lined ex-lt-thick-0000ff');
        expect(root2.lastElementChild?.textContent).toContain('.ex-cell.ex-lb-thick-0000ff{border-bottom:calc(2 * var(--ex-dp)) solid #0000ff}');
    });

    it('ADR-0003: a row whose instance and declarations did not change is not painted again; a new lookup paints every row again', async () => {
        const lines: readonly Line[] = [{ cells: ['a'] }, { cells: ['b'] }];
        const { root, view } = mount<Line>({ columns: sheetColumns, rows: lines, rowHeight: 24, viewportHeight: 480, cellAppearance: boldLook });
        await painted();
        const first = cellAt(root, 0, 0);
        view.update({ columns: sheetColumns, rows: lines, rowHeight: 24, viewportHeight: 480, cellAppearance: boldLook });
        await painted();
        expect(cellAt(root, 0, 0)).toBe(first);
        view.update({ columns: sheetColumns, rows: lines, rowHeight: 24, viewportHeight: 480, cellAppearance: () => ({ italic: true }) });
        await painted();
        expect(cellAt(root, 0, 0)).not.toBe(first);
        expect(cellAt(root, 0, 0).className).toBe('ex-cell ex-font-xi');
    });

    it('ADR-0051: an edit opens on the Consumer\'s text, a Formula rather than its Value, and typing still replaces it', async () => {
        const edits: GridEditIntent<Line>[] = [];
        const root = sheet([{ cells: [2, 4] }], {
            cellType,
            editorText: (_line, column) => (indexOf(column) === 1 ? '=A1*2' : undefined),
            onEdit: intent => { edits.push(intent); },
        });
        await painted();
        const pointer = (type: string): MouseEvent => {
            const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons: type === 'mousedown' ? 1 : 0 });
            Object.defineProperty(event, 'offsetX', { value: 90 });
            Object.defineProperty(event, 'offsetY', { value: 5 });
            q(root, '.ex-viewport').dispatchEvent(event);
            return event;
        };
        if (!pointer('mousedown').defaultPrevented) q(root, '.ex-scroller').focus();
        pointer('mouseup');
        await painted();
        keyOn('F2');
        await painted();
        expect(q(root, 'input.ex-editor')).toHaveProperty('value', '=A1*2');
        keyOn('Escape');
        await painted();
        expect(edits).toEqual([]);
    });

    it('ADR-0050 item 1: where a header click selects, it selects the whole column and sorts nothing', async () => {
        const source = GridSource.from<Line>([{ cells: ['a', 'b'] }, { cells: ['c', 'd'] }]);
        const selections: GridSelection[] = [];
        const root = mount<Line>({
            columns: sheetColumns.map(column => ({ ...column, queryable: true })), source, rowHeight: 24, viewportHeight: 480,
            headerClickSelects: true, onSelectionChanged: selection => { selections.push(selection); },
        }).root;
        await painted();
        for (const type of ['mousedown', 'mouseup', 'click']) {
            const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons: type === 'mousedown' ? 1 : 0, clientX: 90, clientY: 10 });
            Object.defineProperty(event, 'offsetX', { value: 90 });
            Object.defineProperty(event, 'offsetY', { value: 10 });
            q(root, '.ex-header').dispatchEvent(event);
        }
        await painted();
        expect(source.snapshot().sorts).toEqual([]);
        expect(selections.at(-1)?.ranges).toEqual([{ topRow: 0, leftColumn: 1, rowCount: 2, columnCount: 1 }]);
    });

    it('ADR-0050 item 1: a header press claimed both to select and to reorder is refused by name', () => {
        const order: string[][] = [];
        expect(() => mount<Line>({
            columns: sheetColumns, rows: [{ cells: ['a'] }], headerClickSelects: true, onColumnOrderChanged: names => { order.push([...names]); },
        })).toThrow(/headerClickSelects and onColumnOrderChanged/u);
        expect(order).toEqual([]);
    });
});
