// @vitest-environment happy-dom
// Layer 2 (ADR-0390 section 7): the Cell Editor's Chrome slot, as the core holds it. The box over the cell, the keys
// and the uncommitted text stay the core's (ADR-0010): the contents draw a field in the box, the core reads and
// writes it, and the keyboard owed at the opening is given once the contents hand the field over. The contents here
// are a plain Chrome written against the context alone.
import { afterEach, describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import type { GridEditIntent } from '../src/cells/edit-intent';
import type { CellEditorView, GridCellEditorSlotContext, GridChromeSlot } from '../src/dom/grid-chrome';
import type { GridColumn } from '../src/dom/grid-column';
import type { GridOptions } from '../src/dom/grid-view';
import { cell } from '../src/selection/cell';
import { selectionRange } from '../src/selection/range';
import { mount, painted, q, trades, unmountAll, type Trade } from './grid-harness';

afterEach(unmountAll);

const columns: readonly GridColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: trade => trade.book, width: 120, editable: true },
    {
        name: 'Notional', type: 'number', value: trade => trade.notional, width: 100, editable: true,
        validate: (_row, text) => (Number.isNaN(Number(text)) ? { kind: 'reject', message: `'${text}' is not a number` } : { kind: 'accept' }),
    },
];

interface Drawn {
    readonly contexts: GridCellEditorSlotContext[];
    readonly views: CellEditorView[];
    cleanups: number;
}

/** An editor drawn as one plain field holding the opening text, handed to the core at once, or once `later`
 * resolves, as a framework that renders after the task does. */
function plainEditor(drawn: Drawn, later?: Promise<void>): GridChromeSlot<GridCellEditorSlotContext> {
    return (slot, context) => {
        drawn.contexts.push(context);
        const field = document.createElement('input');
        field.className = 'plain-editor';
        field.defaultValue = context.initialText;
        const stop = context.subscribe(() => { drawn.views.push(context.view()); });
        const draw = (): void => {
            slot.append(field);
            context.takeFocus(field);
        };
        if (later === undefined) draw();
        else void later.then(draw);
        return () => {
            drawn.cleanups += 1;
            stop();
            field.remove();
        };
    };
}

function grid(drawn: Drawn, later?: Promise<void>, options: Partial<GridOptions<Trade>> = {}): {
    readonly root: HTMLElement; readonly edits: GridEditIntent<Trade>[];
} {
    const edits: GridEditIntent<Trade>[] = [];
    const { root, view } = mount<Trade>({
        columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, onEdit: intent => { edits.push(intent); },
        chrome: { cellEditor: plainEditor(drawn, later) }, ...options,
    });
    expect(view.placeSelection(selectionRange(0, 1, 1, 1), cell(0, 1), view.readView().rowSequenceVersion)).toBe(true);
    root.focus();
    return { root, edits };
}

function key(name: string, init: KeyboardEventInit = {}): KeyboardEvent {
    const target = document.activeElement;
    if (target === null) throw new Error('Nothing holds the keyboard.');
    const event = new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init });
    target.dispatchEvent(event);
    return event;
}

/** Text typed into the field: what the field does with a key the grid leaves to it. */
function typeInto(field: HTMLInputElement, text: string): void {
    field.value += text;
    field.dispatchEvent(new Event('input', { bubbles: true }));
}

function active(): Element {
    const holder = document.activeElement;
    if (holder === null) throw new Error('Nothing holds the keyboard.');
    return holder;
}

const field = (root: HTMLElement): HTMLInputElement => q(root, '.ex-viewport .ex-editor > input.plain-editor') as HTMLInputElement;
const fresh = (): Drawn => ({ contexts: [], views: [], cleanups: 0 });

describe('the Cell Editor\'s slot', () => {
    it('ADR-0010 / ADR-0390: typing opens the editor in the core\'s box, the field takes the keyboard with the caret at its end, and Enter commits what the field holds', async () => {
        const drawn = fresh();
        const { root, edits } = grid(drawn);
        await painted();
        key('1');
        const context = defined(drawn.contexts[0], 'the editor drawn');
        const box = q(root, '.ex-viewport > .ex-editor');
        expect([context.column, context.columnType, context.initialText, context.focusRequest, box.tagName]).toEqual(['Notional', 'number', '1', 1, 'DIV']);
        expect([document.activeElement, field(root).selectionStart]).toEqual([field(root), 1]);
        typeInto(field(root), '25');
        key('Enter');
        await painted();
        expect([edits.map(intent => intent.value), root.querySelector('.ex-editor'), drawn.cleanups]).toEqual([['125'], null, 1]);
    });

    it('ADR-0010 / ADR-0220: contents drawn after the task that opened the editor are handed the keyboard then, and keys typed meanwhile are in their field', async () => {
        let draw: (() => void) | undefined;
        const drawn = fresh();
        const { root, edits } = grid(drawn, new Promise(resolve => { draw = resolve; }));
        await painted();
        key('7');
        expect(root.querySelector('input.plain-editor')).toBeNull();
        defined(draw, 'the draw')();
        await painted();
        expect([document.activeElement, field(root).value]).toEqual([field(root), '7']);
        key('Enter');
        expect(edits.map(intent => intent.value)).toEqual(['7']);
    });

    it('ADR-0034: a Reject marks the field and describes it by the message, and the contents hear the error; F2 is heard as the mode', async () => {
        const drawn = fresh();
        const { root } = grid(drawn);
        await painted();
        key('a');
        const context = defined(drawn.contexts[0], 'the editor drawn');
        expect(context.view()).toEqual({ mode: 'overwrite', error: undefined, pending: false });
        key('Enter');
        await painted();
        expect([field(root).getAttribute('aria-invalid'), field(root).getAttribute('aria-describedby'), document.activeElement])
            .toEqual(['true', q(root, '.ex-message').id, field(root)]);
        expect(drawn.views.at(-1)).toEqual({ mode: 'overwrite', error: '\'a\' is not a number', pending: false });
        key('F2');
        expect(context.view().mode).toBe('caret');
        const held = field(root);
        key('Escape');
        await painted();
        expect([root.querySelector('.ex-editor'), drawn.cleanups, document.activeElement === held]).toEqual([null, 1, false]);
    });

    it('ADR-0051: text typed in the Formula Bar is the editor\'s, and shows in the contents\' field', async () => {
        const drawn = fresh();
        const { root } = grid(drawn, undefined, { showFormulaBar: true });
        await painted();
        key('1');
        await painted();
        const bar = q(root, '.ex-formula-bar-text') as HTMLInputElement;
        expect(bar.value).toBe('1');
        typeInto(field(root), '2');
        expect(bar.value).toBe('12');
        bar.value = '345';
        bar.dispatchEvent(new Event('input', { bubbles: true }));
        expect(field(root).value).toBe('345');
    });

    it('ADR-0010 / ADR-0380: a field the contents draw anew is handed what was typed and the keyboard, and takes no input while a commit is pending', async () => {
        let redraw: (() => HTMLInputElement) | undefined;
        let answer: ((verdict: { readonly kind: 'accept' }) => void) | undefined;
        const pendingColumns: readonly GridColumn<Trade>[] = [columns[0] as GridColumn<Trade>, {
            name: 'Notional', type: 'number', value: trade => trade.notional, width: 100, editable: true,
            validate: () => new Promise(resolve => { answer = resolve; }),
        }];
        const { edits } = grid(fresh(), undefined, {
            columns: pendingColumns,
            chrome: {
                cellEditor: (slot, context) => {
                    const draw = (): HTMLInputElement => {
                        const drawn = document.createElement('input');
                        drawn.defaultValue = context.initialText;
                        slot.replaceChildren(drawn);
                        context.takeFocus(drawn);
                        return drawn;
                    };
                    draw();
                    redraw = draw;
                    return () => { slot.replaceChildren(); };
                },
            },
        });
        await painted();
        key('1');
        const first = active() as HTMLInputElement;
        typeInto(first, '23');
        const second = defined(redraw, 'the contents')();
        expect([second.value, active()]).toEqual(['123', second]);
        key('Enter');
        await painted();
        const third = defined(redraw, 'the contents')();
        expect([third.value, third.readOnly, active()]).toEqual(['123', true, third]);
        defined(answer, 'the verdict')({ kind: 'accept' });
        await new Promise(resolve => { setTimeout(resolve, 0); });
        expect(edits.map(intent => intent.value)).toEqual(['123']);
    });

    it('ADR-0390: contents that hand over no text field are named', async () => {
        const { root } = grid(fresh(), undefined, {
            chrome: { cellEditor: (slot, context) => { context.takeFocus(slot); return () => undefined; } },
        });
        await painted();
        expect(() => { key('1'); }).toThrow('takeFocus is handed the field the cell editor drew, or an element holding it, inside the slot.');
        expect(root.querySelector('.ex-editor')).toBeNull();
    });
});
