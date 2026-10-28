// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): a declared key whose handler answers later holds the Input Order, and row commands
// clicked behind it wait their turn (ADR-0050 item 14, ADR-0240's note of 2026-10-19). A row Action or row Mark click
// is noted with its row at the click; at its turn it acts on that row, or is refused where the row left the Window
// while it waited, though it came back.
import { afterEach, describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import { actionColumn, markColumn, type GridColumn } from '../src/dom/grid-column';
import type { GridRefusal } from '../src/dom/grid-refusal';
import { GridSource } from '../src/source/grid-source';
import type { RowMarkIntent } from '../src/rows/row-marks';
import { columns, mount, painted, q, trades, unmountAll, type Trade } from './grid-harness';

afterEach(unmountAll);

const open = actionColumn<Trade>('Open', [{ name: 'open', label: 'Open' }], { width: 80 });
const commandColumns: readonly GridColumn<Trade>[] = [markColumn<Trade>({ width: 40 }), ...columns, open];

function mouse(target: Element, type: string): void {
    target.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons: type === 'mousedown' ? 1 : 0 }));
}

/** A click as the browser makes one: its press, its release and its click. */
function click(target: Element): void {
    mouse(target, 'mousedown');
    mouse(target, 'mouseup');
    mouse(target, 'click');
}

const prefix = (root: HTMLElement): string => root.id.replace(/root$/, '');
const openOn = (root: HTMLElement, row: number): HTMLElement => q(root, `#${prefix(root)}r${row}c3 button`);
const markOn = (root: HTMLElement, row: number): HTMLElement => q(root, `#${prefix(root)}r${row}c0 .ex-mark`);

function desk(): {
    readonly root: HTMLElement;
    readonly source: ReturnType<typeof GridSource.from<Trade>>;
    readonly trace: string[];
    readonly release: () => void;
    readonly ctrlB: () => void;
} {
    const trace: string[] = [];
    const source = GridSource.from(trades(30), { rowKey: trade => trade.book });
    const { marks } = source;
    const apply = marks.onMarkIntent.bind(marks);
    marks.onMarkIntent = (intent: RowMarkIntent<Trade>) => {
        if (intent.kind === 'oneRow') trace.push(`mark ${intent.row.book}`);
        return apply(intent);
    };
    let release: (() => void) | undefined;
    const { root } = mount<Trade>({
        columns: commandColumns, source, rowHeight: 24, viewportHeight: 480,
        declaredKeys: ['Control+b'],
        onDeclaredKey: () => new Promise<void>(resolve => {
            trace.push('started');
            release = () => { trace.push('finished'); resolve(); };
        }),
        onAction: ({ row }) => { trace.push(`open ${row.book}`); },
        onRefused: (refusal: GridRefusal<Trade>) => { trace.push(`${refusal.kind} ${'reason' in refusal ? refusal.reason : ''}`); },
    });
    return {
        root, source, trace,
        release: () => { defined(release, 'a key under way')(); },
        ctrlB: () => {
            root.focus();
            const target = document.activeElement ?? root;
            target.dispatchEvent(new KeyboardEvent('keydown', { key: 'b', ctrlKey: true, bubbles: true, cancelable: true }));
        },
    };
}

const settled = async (): Promise<void> => {
    for (let i = 0; i < 4; i++) await painted();
};

describe('row commands behind a declared key that answers later', () => {
    it('ADR-0240: an Action and a Mark clicked while the key\'s answer is out wait, and are taken in their order once it settles', async () => {
        const { root, trace, release, ctrlB } = desk();
        await painted();
        ctrlB();
        click(openOn(root, 0));
        click(markOn(root, 1));
        await settled();
        expect(trace).toEqual(['started']);
        release();
        await settled();
        expect(trace).toEqual(['started', 'finished', 'open Book-0', 'mark Book-1']);
    });

    it('ADR-0240: a waiting command whose row left the Window is refused at its turn though the row came back, and later input goes on', async () => {
        const { root, source, trace, release, ctrlB } = desk();
        await painted();
        ctrlB();
        click(openOn(root, 0));
        click(markOn(root, 1));
        const [first, second] = trades(2);
        source.apply({ removedKeys: ['Book-0', 'Book-1'] });
        await settled();
        source.apply({ added: [defined(first, 'Book-0'), defined(second, 'Book-1')] });
        await settled();
        release();
        await settled();
        expect(trace).toEqual(['started', 'finished', 'action rowLeftTheWindow', 'mark rowLeftTheWindow']);
        // Clicked again on the rows as they stand now, the commands act.
        click(openOn(root, 0));
        await settled();
        expect(trace.at(-1)).toBe('open Book-2');
    });

    it('ADR-0240: a row that only scrolled out of the painted rows, and stayed in the Window, is still acted on', async () => {
        const { root, trace, release, ctrlB } = desk();
        await painted();
        ctrlB();
        click(openOn(root, 3));
        const scroller = q(root, '.ex-viewport');
        scroller.scrollTop = 400;
        scroller.dispatchEvent(new Event('scroll'));
        await settled();
        release();
        await settled();
        expect(trace).toEqual(['started', 'finished', 'open Book-3']);
    });

    it('ADR-0240, ADR-0380: a waiting Action past an open editor whose verdict answers later acts once the commit lands', async () => {
        const trace: string[] = [];
        let land: (() => void) | undefined;
        const book: GridColumn<Trade> = { name: 'Book', type: 'text', value: trade => trade.book, width: 120, editable: true };
        const source = GridSource.from(trades(30), { rowKey: trade => trade.book });
        const { root, view } = mount<Trade>({
            columns: [markColumn<Trade>({ width: 40 }), book, defined(columns[1], 'Notional'), open], source, rowHeight: 24, viewportHeight: 480,
            onEdit: intent => new Promise<void>(resolve => {
                land = () => { trace.push(`edit ${intent.value}`); resolve(); };
            }),
            onAction: ({ row }) => { trace.push(`open ${row.book}`); },
        });
        await painted();
        view.placeSelection({ topRow: 2, leftColumn: 1, rowCount: 1, columnCount: 1 }, { row: 2, column: 1 }, view.readView().rowSequenceVersion);
        await painted();
        q(root, '.ex-key-field').dispatchEvent(new KeyboardEvent('keydown', { key: 'x', bubbles: true, cancelable: true }));
        await settled();
        let finish: (() => void) | undefined;
        void view.inOrder(() => new Promise<void>(resolve => { finish = resolve; }));
        click(openOn(root, 4));
        defined(finish, 'the work under way')();
        await settled();
        expect(trace).toEqual([]);
        defined(land, 'the edit out')();
        await settled();
        expect(trace).toEqual(['edit x', 'open Book-4']);
    });
});
