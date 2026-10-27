// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): a paste into the grid (ADR-0014, ADR-0035, ADR-0050 item 3). The browser's
// paste command fires the paste event on the root; an approved block raises one intent and the grid writes
// nothing, and a refused one raises none and is said by name.
import { describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import type { GridPasteIntent } from '../src/clipboard/paste-intent';
import type { GridColumn } from '../src/dom/grid-column';
import { exceedsUtf8, utf8Length } from '../src/dom/grid-paste';
import type { GridRefusal } from '../src/dom/grid-refusal';
import type { GridSelection } from '../src/selection/selection';
import { pasteRefusalLabelIds, columnPlaceholder as refusalColumnPlaceholder } from '../src/chrome/refusal-words';
import { GridSource } from '../src/source/grid-source';
import { FakeClock } from './fake-clock';
import { mount, painted, q, trades, type Trade } from './grid-harness';

// Book and Notional take a paste; the Desk does not, and is named by its header when a paste covers it.
const columns: readonly GridColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: trade => trade.book, width: 120, editable: true },
    { name: 'Notional', type: 'number', value: trade => trade.notional, width: 100, editable: true },
    { name: 'Desk', type: 'text', value: trade => trade.book, width: 100, header: 'Trading desk ($&)' },
];

// Words for a paste over the Desk that name the column twice.
const twiceNamed = (id: string): string | undefined => (id === pasteRefusalLabelIds.targetNotEditable
    ? `${refusalColumnPlaceholder} is read-only: leave ${refusalColumnPlaceholder} out.` : undefined);

// Rows of 24 px under a header of 24; the columns' left edges.
const rowY = (row: number): number => (row * 24) + 5;
const book = 10;
const notional = 130;

function press(root: HTMLElement, x: number, y: number, init: MouseEventInit = {}): void {
    for (const type of ['mousedown', 'mouseup']) {
        const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons: type === 'mousedown' ? 1 : 0, ...init });
        Object.defineProperty(event, 'offsetX', { value: x });
        Object.defineProperty(event, 'offsetY', { value: y });
        q(root, '.ex-viewport').dispatchEvent(event);
    }
}

function key(root: HTMLElement, name: string, init: KeyboardEventInit = {}): void {
    root.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init }));
}

/** The paste event the browser's paste command fires, carrying both flavours; an empty one is absent. */
function paste(target: HTMLElement, text: string, html = ''): Event {
    const flavours = new Map([['text/plain', text], ['text/html', html]]);
    const event = new Event('paste', { bubbles: true, cancelable: true });
    Object.defineProperty(event, 'clipboardData', { value: { getData: (type: string) => defined(flavours.get(type), `the ${type} flavour`) } });
    target.dispatchEvent(event);
    return event;
}

const table = (rows: readonly (readonly string[])[]): string =>
    `<table data-ex-grid="invariant">${rows.map(row => `<tr>${row.map(value => `<td>${value}</td>`).join('')}</tr>`).join('')}</table>`;

/** What the Consumer hears; `refuse` makes it refuse every paste. */
function heard(refuse = false): {
    readonly intents: GridPasteIntent[]; readonly refusals: GridRefusal[]; readonly selections: GridSelection[];
    readonly onPaste: (intent: GridPasteIntent) => void; readonly onRefused: (refusal: GridRefusal) => void;
    readonly onSelectionChanged: (selection: GridSelection) => void;
} {
    const intents: GridPasteIntent[] = [];
    const refusals: GridRefusal[] = [];
    const selections: GridSelection[] = [];
    return {
        intents, refusals, selections,
        onPaste: intent => {
            intents.push(intent);
            if (refuse) intent.refuse();
        },
        onRefused: refusal => { refusals.push(refusal); },
        onSelectionChanged: selection => { selections.push(selection); },
    };
}

const lastSelection = (selections: readonly GridSelection[]): GridSelection => defined(selections.at(-1), 'a Selection heard');

describe('a paste into the grid', () => {
    it('ADR-0014 CP-14/PST-1: a block onto a range of its shape raises one intent with every cell, and the grid writes nothing', async () => {
        const listener = heard();
        const { root } = mount({ columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, ...listener });
        await painted();
        press(root, book, rowY(1));
        key(root, 'ArrowDown', { shiftKey: true });
        key(root, 'ArrowRight', { shiftKey: true });
        await painted();

        const event = paste(root, 'x\t1\r\ny\t2\r\n', table([['x', '1.5'], ['y', '2.5']]));
        expect(event.defaultPrevented).toBe(true);
        const intent = defined(listener.intents[0], 'the intent');
        expect(listener.intents).toHaveLength(1);
        expect([intent.cellCount, intent.plan.source, intent.values, intent.rowSequenceVersion]).toEqual([4, { rows: 2, columns: 2 }, [['x', '1.5'], ['y', '2.5']], 0]);
        // The family's own HTML is read under the invariant culture (ADR-0050, item 10).
        expect([intent.valueFor({ row: 2, column: 1 }), intent.originFor({ row: 2, column: 1 })]).toEqual(['2.5', 'invariant']);
        await painted();
        expect(q(root, '[id$="r1c0"]').textContent).toBe('Book-1');
        expect(listener.refusals).toEqual([]);
    });

    it('ADR-0014: one value from a table fills the range; one of plain text goes into one cell, and the Selection collapses to it once accepted', async () => {
        const listener = heard();
        const { root } = mount({ columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, ...listener });
        await painted();
        press(root, book, rowY(0));
        key(root, 'ArrowDown', { shiftKey: true });
        key(root, 'ArrowDown', { shiftKey: true });
        await painted();

        paste(root, 'fill\r\n', table([['fill']]));
        expect(defined(listener.intents[0], 'the table\'s intent').cellCount).toBe(3);
        await painted();
        expect(lastSelection(listener.selections).ranges).toEqual([{ topRow: 0, leftColumn: 0, rowCount: 3, columnCount: 1 }]);

        paste(root, 'one');
        const typed = defined(listener.intents[1], 'the text\'s intent');
        expect([typed.cellCount, typed.plan.targets, typed.originFor({ row: 0, column: 0 })])
            .toEqual([1, [{ topRow: 0, leftColumn: 0, rowCount: 1, columnCount: 1 }], 'shownText']);
        await painted();
        const collapsed = lastSelection(listener.selections);
        expect([collapsed.ranges, collapsed.focus]).toEqual([[{ topRow: 0, leftColumn: 0, rowCount: 1, columnCount: 1 }], { row: 0, column: 0 }]);
    });

    it('ADR-0050 item 3: a paste the Consumer refuses leaves the Selection where it was', async () => {
        const listener = heard(true);
        const { root } = mount({ columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, pasteMaySpill: true, ...listener });
        await painted();
        press(root, book, rowY(0));
        key(root, 'ArrowDown', { shiftKey: true });
        await painted();
        paste(root, 'one');
        await painted();
        expect(lastSelection(listener.selections).ranges).toEqual([{ topRow: 0, leftColumn: 0, rowCount: 2, columnCount: 1 }]);

        // Nor does a spill the Consumer refuses become the Selection.
        press(root, book, rowY(5));
        await painted();
        paste(root, 'a\tb\r\nc\td\r\n');
        await painted();
        expect([listener.intents.length, lastSelection(listener.selections).ranges])
            .toEqual([2, [{ topRow: 5, leftColumn: 0, rowCount: 1, columnCount: 1 }]]);
    });

    it('ADR-0050 item 3: a refusal after onPaste returned is refused by name, and a grid nobody hears a paste from takes none', async () => {
        let kept: GridPasteIntent | undefined;
        const refusals: GridRefusal[] = [];
        const options = { columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, onRefused: (refusal: GridRefusal) => { refusals.push(refusal); } } as const;
        const { root, view } = mount({ ...options, onPaste: intent => { kept = intent; } });
        await painted();
        press(root, book, rowY(0));
        paste(root, 'x');
        expect(() => { defined(kept, 'the intent').refuse(); }).toThrow(/answered when onPaste returned/);

        view.update(options);
        await painted();
        paste(root, 'x');
        expect(refusals).toEqual([{ kind: 'paste', reason: 'targetNotEditable' }]);
        expect(q(root, '.ex-announce').textContent).toBe('Nothing was changed: the selection covers Book, which cannot be edited. Select cells only in columns that can be edited.');
    });

    it('ADR-0203: a refusal is said in the words commandLabel answers for its id, the column written in, and silenced by an empty answer', async () => {
        const refusals: GridRefusal[] = [];
        const said = new Map<string, string | undefined>([
            [pasteRefusalLabelIds.targetNotEditable, `Only Book and Notional take a paste, and the selection covers ${refusalColumnPlaceholder}.`],
        ]);
        const asked: string[] = [];
        const commandLabel = (id: string): string | undefined => { asked.push(id); return said.get(id); };
        const options = { columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, commandLabel, onPaste: () => undefined,
            onRefused: (refusal: GridRefusal) => { refusals.push(refusal); } } as const;
        const { root, view } = mount(options);
        await painted();
        press(root, 230, rowY(0));
        paste(root, 'x');
        await painted();
        expect(refusals).toEqual([{ kind: 'paste', reason: 'targetNotEditable' }]);
        expect(q(root, '.ex-announce').textContent).toBe('Only Book and Notional take a paste, and the selection covers Trading desk ($&).');
        expect(asked).toContain('refused-paste-target-not-editable');

        // An empty answer silences the reason; the refusal is still raised by name.
        said.set(pasteRefusalLabelIds.targetNotEditable, '');
        view.update({ ...options, commandLabel: (id: string) => said.get(id) });
        await painted();
        paste(root, 'y');
        await painted();
        // Said nothing: the last words stand, neither emptied nor said again.
        expect([q(root, '.ex-announce').textContent, refusals.length])
            .toEqual(['Only Book and Notional take a paste, and the selection covers Trading desk ($&).', 2]);

        // No answer keeps the built-in English, and an answer that is not text is refused by name.
        said.delete(pasteRefusalLabelIds.targetNotEditable);
        paste(root, 'z');
        await painted();
        expect(q(root, '.ex-announce').textContent).toBe('Nothing was changed: the selection covers Trading desk ($&), which cannot be edited. Select cells only in columns that can be edited.');
        view.update({ ...options, commandLabel: () => 42 as unknown as string });
        await painted();
        expect(() => paste(root, 'w')).toThrow(/commandLabel answered number for 'refused-paste-target-not-editable'/);
    });

    it('ADR-0203: a column with a blank header is written in by its name, wherever the words hold the placeholder', async () => {
        const unheaded = columns.map(column => (column.name === 'Desk' ? { ...column, header: ' ' } : column));
        const { root } = mount({ columns: unheaded, rows: trades(20), rowHeight: 24, viewportHeight: 480, commandLabel: twiceNamed, onPaste: () => undefined });
        await painted();
        press(root, 230, rowY(0));
        paste(root, 'x');
        await painted();
        expect(q(root, '.ex-announce').textContent).toBe('Desk is read-only: leave Desk out.');
    });

    it('ADR-0035 CP-16: a paste over a column that is not Editable is refused whole, and the words name it by its header', async () => {
        const listener = heard();
        const { root } = mount({ columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, ...listener });
        await painted();
        press(root, notional, rowY(0));
        key(root, 'ArrowRight', { shiftKey: true });
        await painted();

        paste(root, 'v\r\n', table([['v']]));
        expect(listener.intents).toEqual([]);
        expect(listener.refusals).toEqual([{ kind: 'paste', reason: 'targetNotEditable' }]);
        expect(q(root, '.ex-announce').textContent)
            .toBe('Nothing was changed: the selection covers Trading desk ($&), which cannot be edited. Select cells only in columns that can be edited.');
    });

    it('ADR-0014 / ADR-0050 item 3: a block onto one cell is refused unless the paste may spill, and then it becomes the Selection', async () => {
        const listener = heard();
        const options = { columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, ...listener } as const;
        const { root, view } = mount(options);
        await painted();
        press(root, book, rowY(2));
        await painted();
        const block = 'a\t1\r\nb\t2\r\n';
        paste(root, block);
        expect([listener.intents, listener.refusals]).toEqual([[], [{ kind: 'paste', reason: 'singleCellTarget' }]]);

        view.update({ ...options, pasteMaySpill: true });
        await painted();
        paste(root, block);
        expect(defined(listener.intents[0], 'the spill\'s intent').plan.targets).toEqual([{ topRow: 2, leftColumn: 0, rowCount: 2, columnCount: 2 }]);
        await painted();
        const spilled = lastSelection(listener.selections);
        expect([spilled.ranges, spilled.focus]).toEqual([[{ topRow: 2, leftColumn: 0, rowCount: 2, columnCount: 2 }], { row: 2, column: 0 }]);

        // A block that would run past the last row is refused, never clipped.
        press(root, book, rowY(19));
        paste(root, block);
        expect(listener.refusals.at(-1)).toEqual({ kind: 'paste', reason: 'spillPastExtent' });
        // Where a paste may spill, a shape mismatch offers a single cell too (ADR-0203).
        press(root, book, rowY(0));
        key(root, 'ArrowDown', { shiftKey: true });
        key(root, 'ArrowDown', { shiftKey: true });
        await painted();
        paste(root, 'a\r\nb\r\n');
        expect(q(root, '.ex-announce').textContent).toBe('Nothing was pasted: the selection is not a whole multiple of the copied block. '
            + 'Select a range of the copied block\'s shape, one it repeats over exactly, or a single cell.');
    });

    it('ADR-0005: a paste past the byte cap is refused whole, counted in UTF-8 across both flavours', async () => {
        const listener = heard();
        const { root } = mount({ columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, pasteByteCap: 10, ...listener });
        await painted();
        press(root, book, rowY(0));
        paste(root, 'ééééé');
        expect([listener.intents.length, listener.refusals]).toEqual([1, []]);
        paste(root, 'éééé', table([['é']]));
        expect(listener.refusals).toEqual([{ kind: 'paste', reason: 'tooLarge' }]);
        expect(listener.intents).toHaveLength(1);
        // Counted as the encoder writes it: a pair is four bytes, a lone surrogate the three of U+FFFD.
        const text = 'aé€\u{1F600}\ud800z';
        const bytes = new TextEncoder().encode(text).byteLength;
        expect([utf8Length(text), exceedsUtf8([text, 'é'], bytes + 2), exceedsUtf8([text, 'é'], bytes + 1)]).toEqual([bytes, false, true]);
    });

    it('ADR-0142 / ADR-0011: a gathered live change that moves the order under the paste refuses it aloud', async () => {
        const clock = new FakeClock();
        const source = GridSource.from(trades(10), { rowKey: trade => trade.book, clock });
        const listener = heard();
        const { root } = mount({ columns, source, rowHeight: 24, viewportHeight: 480, ...listener });
        await painted();
        source.setSorts([{ column: 'Notional', direction: 'ascending' }]);
        // The first change after a quiet interval reaches the grid at once; the next is gathered.
        source.apply({ changed: [{ book: 'Book-5', notional: 5001 }] });
        await painted();
        press(root, book, rowY(0));
        await painted();
        source.apply({ changed: [{ book: 'Book-0', notional: 99_999 }] });

        paste(root, 'x');
        expect([listener.intents, listener.refusals]).toEqual([[], [{ kind: 'paste', reason: 'orderMoved' }]]);
        expect(q(root, '.ex-announce').textContent).toBe('Nothing was changed: the rows moved after the cells were selected. Select the cells again.');

        // So is a paste made in the task a reorder was heard in, before the grid painted it.
        await painted();
        press(root, book, rowY(0));
        await painted();
        source.setSorts([{ column: 'Notional', direction: 'descending' }]);
        paste(root, 'x');
        expect([listener.intents, listener.refusals.at(-1)]).toEqual([[], { kind: 'paste', reason: 'orderMoved' }]);
    });

    it('ADR-0035 / ADR-0005: a paste\'s options a grid could not honour are refused by name', () => {
        const options = { columns, rows: trades(10), rowHeight: 24, viewportHeight: 480 } as const;
        const { view } = mount(options);
        const untyped = (extra: object): typeof options => ({ ...options, ...extra });
        expect(() => { view.update(untyped({ columns: [{ ...columns[0], editable: 'yes' }] })); }).toThrow(/editable is true or false/);
        expect(() => { view.update(untyped({ pasteMaySpill: 1 })); }).toThrow(/pasteMaySpill is true or false/);
        expect(() => { view.update({ ...options, pasteByteCap: 0 }); }).toThrow(/pasteByteCap is a whole number of at least one byte/);
    });

    it('ADR-0014/0020: a paste aimed at something inside the root is not the grid\'s', async () => {
        const listener = heard();
        const { root } = mount({ columns, rows: trades(10), rowHeight: 24, viewportHeight: 480, ...listener });
        await painted();
        press(root, book, rowY(0));
        const event = paste(q(root, '.ex-header'), 'x');
        expect([event.defaultPrevented, listener.intents, listener.refusals]).toEqual([false, [], []]);
    });
});
