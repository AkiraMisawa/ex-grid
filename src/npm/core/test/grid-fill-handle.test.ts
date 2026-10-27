// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): the fill handle in the DOM (ADR-0050, item 5). Where it is painted, the drag
// that reaches along one axis, the one Fill Intent its release raises, the Editable gate it passes first, and
// the Selection after (DC-12, DC-13, DC-14, DC-27).
import { afterEach, describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import type { GridColumn } from '../src/dom/grid-column';
import type { GridRefusal } from '../src/dom/grid-refusal';
import type { GridFillIntent } from '../src/selection/fill-intent';
import { mount, painted, q, trades, unmountAll, type Trade } from './grid-harness';

const editable: readonly GridColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: trade => trade.book, width: 120, editable: true },
    { name: 'Notional', type: 'number', value: trade => trade.notional, width: 100, editable: true },
    // A third column, so a block of the first two is not whole rows, whose handle stands at their bottom-left.
    { name: 'Desk', type: 'text', value: trade => `Desk-${trade.book}`, width: 80, editable: true },
];

function pointer(root: HTMLElement, type: string, x: number, y: number, init: MouseEventInit = {}): MouseEvent {
    const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons: type === 'mouseup' ? 0 : 1, ...init });
    Object.defineProperty(event, 'offsetX', { value: x });
    Object.defineProperty(event, 'offsetY', { value: y });
    q(root, '.ex-viewport').dispatchEvent(event);
    return event;
}

// Rows of 24 px; Book is 120 px wide and Notional 100, so the handle (7 px) of a range ending in Notional
// stands at x 220.
const rowY = (row: number): number => (row * 24) + 5;
const columnX = (column: number): number => (column === 0 ? 10 : 130);

const pieces = (root: HTMLElement, name: string): (string | null)[] =>
    [...root.querySelectorAll(`.ex-viewport > .ex-selection > .${name}`)].map(piece => piece.getAttribute('style'));

function listen(): {
    readonly intents: GridFillIntent[]; readonly refusals: GridRefusal[];
    readonly onFill: (intent: GridFillIntent) => void; readonly onRefused: (refusal: GridRefusal) => void;
} {
    const intents: GridFillIntent[] = [];
    const refusals: GridRefusal[] = [];
    return { intents, refusals, onFill: intent => { intents.push(intent); }, onRefused: refusal => { refusals.push(refusal); } };
}

// A key, where the keyboard is.
function typed(name: string): void {
    const target = document.activeElement;
    if (target === null) throw new Error('Nothing holds the keyboard.');
    target.dispatchEvent(new KeyboardEvent('keydown', { key: name, code: name, bubbles: true, cancelable: true }));
}

// Selects rows 2 to 3 of both columns: the press, then Shift and the press.
async function selectBlock(root: HTMLElement): Promise<void> {
    pointer(root, 'mousedown', columnX(0), rowY(2));
    pointer(root, 'mouseup', columnX(0), rowY(2));
    pointer(root, 'mousedown', columnX(1), rowY(3), { shiftKey: true });
    pointer(root, 'mouseup', columnX(1), rowY(3), { shiftKey: true });
    // The browser's own focus on a press the grid does not prevent.
    q(root, '.ex-scroller').focus();
    await painted();
}

afterEach(unmountAll);

describe('the fill handle (ADR-0050, item 5)', () => {
    it('DC-12: declared, it is painted at the bottom-right of the Selection\'s one range, and not without the declaration or on a disjoint Selection', async () => {
        const { root, view } = mount({ columns: editable, rows: trades(100), rowHeight: 24, viewportHeight: 480, showFillHandle: true });
        await painted();
        await selectBlock(root);
        expect(pieces(root, 'ex-fill-handle')).toEqual(['left: 216.5px; top: 92.5px; width: 7px; height: 7px']);
        // Ctrl adds a range: a disjoint Selection has no handle, as Excel's has none.
        pointer(root, 'mousedown', columnX(0), rowY(8), { ctrlKey: true });
        pointer(root, 'mouseup', columnX(0), rowY(8), { ctrlKey: true });
        await painted();
        expect(pieces(root, 'ex-fill-handle')).toEqual([]);
        await selectBlock(root);
        view.update({ columns: editable, rows: trades(100), rowHeight: 24, viewportHeight: 480 });
        await painted();
        expect(pieces(root, 'ex-fill-handle')).toEqual([]);
    });

    it('DC-12: whole columns put it at the first row\'s top-right, beside the Heading', async () => {
        const { root } = mount({ columns: editable, rows: trades(100), rowHeight: 24, viewportHeight: 480, showFillHandle: true });
        await painted();
        pointer(root, 'mousedown', columnX(1), rowY(4));
        pointer(root, 'mouseup', columnX(1), rowY(4));
        root.dispatchEvent(new KeyboardEvent('keydown', { key: ' ', code: 'Space', ctrlKey: true, bubbles: true, cancelable: true }));
        await painted();
        expect(pieces(root, 'ex-fill-handle')).toEqual(['left: 216.5px; top: 0px; width: 7px; height: 7px']);
    });

    it('DC-13, DC-27: a drag reaches along one axis and paints its outline; the release raises one intent, and the Selection becomes the source and the target', async () => {
        const heard = listen();
        const { root, view } = mount({
            columns: editable, rows: trades(100), rowHeight: 24, viewportHeight: 480, showFillHandle: true, onFill: heard.onFill, onRefused: heard.onRefused,
        });
        await painted();
        await selectBlock(root);
        pointer(root, 'mousedown', 220, 96);
        // A diagonal reach goes the way it went further: three rows down beats one column across.
        pointer(root, 'mousemove', columnX(1) + 90, rowY(6));
        await painted();
        expect(pieces(root, 'ex-fill-target')).toEqual(['left: 0px; top: 48px; width: 220px; height: 120px']);
        // The Selection stays the source while the drag runs.
        expect(view.readView().selection.ranges).toEqual([{ topRow: 2, leftColumn: 0, rowCount: 2, columnCount: 2 }]);
        pointer(root, 'mouseup', columnX(1), rowY(6));
        await painted();
        expect(heard.intents.map(({ source, target, direction }) => ({ source, target, direction }))).toEqual([{
            source: { topRow: 2, leftColumn: 0, rowCount: 2, columnCount: 2 },
            target: { topRow: 4, leftColumn: 0, rowCount: 3, columnCount: 2 },
            direction: 'down',
        }]);
        expect(heard.refusals).toEqual([]);
        expect(pieces(root, 'ex-fill-target')).toEqual([]);
        const after = view.readView().selection;
        expect(after.ranges).toEqual([{ topRow: 2, leftColumn: 0, rowCount: 5, columnCount: 2 }]);
        expect(after.focus).toEqual({ row: 2, column: 0 });
        // Released back inside the source, a drag raises nothing.
        pointer(root, 'mousedown', 220, 168);
        pointer(root, 'mousemove', columnX(1), rowY(9));
        pointer(root, 'mouseup', columnX(0), rowY(3));
        await painted();
        expect(heard.intents).toHaveLength(1);
    });

    it('DC-27: a fill the Consumer refuses leaves the Selection on the source', async () => {
        const { root, view } = mount({
            columns: editable, rows: trades(100), rowHeight: 24, viewportHeight: 480, showFillHandle: true, onFill: intent => { intent.refuse(); },
        });
        await painted();
        await selectBlock(root);
        pointer(root, 'mousedown', 220, 96);
        pointer(root, 'mousemove', columnX(1), rowY(7));
        pointer(root, 'mouseup', columnX(1), rowY(7));
        await painted();
        expect(view.readView().selection.ranges).toEqual([{ topRow: 2, leftColumn: 0, rowCount: 2, columnCount: 2 }]);
    });

    it('DC-27 / ADR-0380 section 2: a fill answered later is pending; refused, the Selection stays on the source, and accepted, it takes the target', async () => {
        const answers: { readonly intent: GridFillIntent; readonly give: () => void }[] = [];
        const { root, view } = mount({
            columns: editable, rows: trades(100), rowHeight: 24, viewportHeight: 480, showFillHandle: true,
            onFill: intent => new Promise<void>(resolve => { answers.push({ intent, give: resolve }); }),
        });
        await painted();
        await selectBlock(root);
        const source = [{ topRow: 2, leftColumn: 0, rowCount: 2, columnCount: 2 }];
        pointer(root, 'mousedown', 220, 96);
        pointer(root, 'mousemove', columnX(1), rowY(6));
        pointer(root, 'mouseup', columnX(1), rowY(6));
        await painted();
        expect(root.hasAttribute('data-ex-pending-commit')).toBe(true);
        expect(view.readView().selection.ranges).toEqual(source);
        const refused = defined(answers[0], 'the first fill heard');
        refused.intent.refuse();
        refused.give();
        await painted();
        await painted();
        expect(root.hasAttribute('data-ex-pending-commit')).toBe(false);
        expect(view.readView().selection.ranges).toEqual(source);

        pointer(root, 'mousedown', 220, 96);
        pointer(root, 'mousemove', columnX(1), rowY(6));
        pointer(root, 'mouseup', columnX(1), rowY(6));
        await painted();
        defined(answers[1], 'the second fill heard').give();
        await painted();
        await painted();
        expect(view.readView().selection.ranges).toEqual([{ topRow: 2, leftColumn: 0, rowCount: 5, columnCount: 2 }]);
    });

    it('ED-34 / ADR-0380: Escape cancels a fill whose answer is out, aloud, aborts its signal and leaves the Selection on the source', async () => {
        const heard = listen();
        const answers: GridFillIntent[] = [];
        const { root, view } = mount({
            columns: editable, rows: trades(100), rowHeight: 24, viewportHeight: 480, showFillHandle: true, onRefused: heard.onRefused,
            onFill: intent => new Promise<void>(() => { answers.push(intent); }),
        });
        await painted();
        await selectBlock(root);
        pointer(root, 'mousedown', 220, 96);
        pointer(root, 'mousemove', columnX(1), rowY(6));
        pointer(root, 'mouseup', columnX(1), rowY(6));
        await painted();
        typed('Escape');
        await painted();
        expect(defined(answers[0], 'the fill heard').signal.aborted).toBe(true);
        expect(heard.refusals).toEqual([{ kind: 'paste', reason: 'cancelled' }]);
        expect(root.hasAttribute('data-ex-pending-commit')).toBe(false);
        expect(view.readView().selection.ranges).toEqual([{ topRow: 2, leftColumn: 0, rowCount: 2, columnCount: 2 }]);
    });

    it('ADR-0050: an edit opened, or the handle taken away, while the button is held ends the drag: nothing is raised and the Selection does not follow the pointer', async () => {
        const heard = listen();
        const edits: unknown[] = [];
        const options = {
            columns: editable, rows: trades(100), rowHeight: 24, viewportHeight: 480, onFill: heard.onFill, onRefused: heard.onRefused,
            onEdit: (intent: unknown) => { edits.push(intent); },
        };
        const { root, view } = mount({ ...options, showFillHandle: true });
        await painted();
        await selectBlock(root);
        pointer(root, 'mousedown', 220, 96);
        pointer(root, 'mousemove', columnX(1), rowY(6));
        typed('F2');
        await painted();
        expect(root.querySelector('.ex-editor')).not.toBeNull();
        pointer(root, 'mouseup', columnX(1), rowY(6));
        await painted();
        expect(heard.intents).toEqual([]);
        expect(view.readView().selection.ranges).toEqual([{ topRow: 2, leftColumn: 0, rowCount: 2, columnCount: 2 }]);
        typed('Escape');
        expect(edits).toEqual([]);
        await painted();
        pointer(root, 'mousedown', 220, 96);
        pointer(root, 'mousemove', columnX(1), rowY(6));
        view.update(options);
        pointer(root, 'mousemove', columnX(1), rowY(9));
        pointer(root, 'mouseup', columnX(1), rowY(9));
        await painted();
        expect(heard.intents).toEqual([]);
        expect(view.readView().selection.ranges).toEqual([{ topRow: 2, leftColumn: 0, rowCount: 2, columnCount: 2 }]);
    });

    it('DC-14: a target over a column that is not Editable is refused whole before anything is raised', async () => {
        const heard = listen();
        const { root } = mount({
            columns: [defined(editable[0], 'Book'), { name: 'Notional', type: 'number', value: trade => trade.notional, width: 100 }, defined(editable[2], 'Desk')],
            rows: trades(100), rowHeight: 24, viewportHeight: 480, showFillHandle: true, onFill: heard.onFill, onRefused: heard.onRefused,
        });
        await painted();
        pointer(root, 'mousedown', columnX(0), rowY(2));
        pointer(root, 'mouseup', columnX(0), rowY(2));
        await painted();
        // Book's handle stands at x 120 on row 2's bottom edge; dragged across into Notional.
        pointer(root, 'mousedown', 120, 72);
        pointer(root, 'mousemove', columnX(1), rowY(2));
        pointer(root, 'mouseup', columnX(1), rowY(2));
        await painted();
        expect(heard.intents).toEqual([]);
        expect(heard.refusals).toEqual([{ kind: 'paste', reason: 'targetNotEditable' }]);
        expect(q(root, '.ex-announce').textContent).toContain('Notional');
    });
});
