// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): a flagged cell's message (ADR-0034, ED-17, ED-17b). The Consumer is asked why
// for one cell, once it is settled on, by the Focus standing still or the pointer coming to rest, and never while
// painting; and the bundled ruleset that answers both channels.
import { afterEach, describe, expect, it } from 'vitest';
import type { EditVerdict } from '../src/cells/edit-intent';
import { GridRuleset } from '../src/cells/grid-ruleset';
import type { GridColumn } from '../src/dom/grid-column';
import { columns, mount, painted, q, scrollTo, trades, unmountAll, type Trade } from './grid-harness';

afterEach(unmountAll);

// Rows of 24 px; a press or a move is given in offsets into the Viewport, whose top is the first row's.
function mouse(root: HTMLElement, type: string, x: number, y: number, target: Element = q(root, '.ex-viewport')): void {
    const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons: type === 'mousedown' ? 1 : 0 });
    Object.defineProperty(event, 'offsetX', { value: x });
    Object.defineProperty(event, 'offsetY', { value: y });
    target.dispatchEvent(event);
}

function click(root: HTMLElement, x: number, y: number): void {
    mouse(root, 'mousedown', x, y);
    mouse(root, 'mouseup', x, y);
}

function key(root: HTMLElement, name: string): void {
    root.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true }));
}

// The message shown, or undefined while none stands: an element present with no text would be a defect seen as ''.
const shown = (root: HTMLElement): string | undefined => {
    const message = root.querySelector('.ex-message');
    return message === null ? undefined : message.textContent;
};
const settled = (root: HTMLElement): boolean => root.hasAttribute('data-ex-settled');

function asking(): { readonly asked: string[]; readonly cellMessage: (row: Trade, column: GridColumn<Trade>) => string } {
    const asked: string[] = [];
    return { asked, cellMessage: (row, column) => { asked.push(`${row.book}/${column.name}`); return 'out of range'; } };
}

describe('a flagged cell\'s message', () => {
    it('ED-17: the Consumer is asked once the Focus has stood still for 300 ms, never while painting, and a Focus move closes it', async () => {
        const { asked, cellMessage } = asking();
        const { root, told } = mount({ columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, cellMessage });
        await painted();
        click(root, 130, 5);
        await painted();
        // Painted by now, and nothing asked: the Focus has not stood still. Whether a message opens is known only
        // once it has, so the grid is not settled before (ADR-0220).
        expect([asked, shown(root), settled(root)]).toEqual([[], undefined, false]);

        told.clock.advance(299);
        expect(asked).toEqual([]);
        told.clock.advance(1);
        await painted();
        expect([asked, shown(root), settled(root)]).toEqual([['Book-0/Notional'], 'out of range', true]);
        expect(q(root, '.ex-message').getAttribute('role')).toBe('tooltip');

        key(root, 'ArrowDown');
        await painted();
        expect(shown(root)).toBeUndefined();
    });

    it('ADR-0380 s5: a message answered later opens once answered, and the grid is not settled until then', async () => {
        const answers: ((message: string | undefined) => void)[] = [];
        const { root, told } = mount({
            columns, rows: trades(20), rowHeight: 24, viewportHeight: 480,
            cellMessage: () => new Promise<string | undefined>(resolve => { answers.push(resolve); }),
        });
        await painted();
        click(root, 130, 5);
        await painted();
        told.clock.advance(300);
        await painted();
        expect([answers.length, shown(root), settled(root)]).toEqual([1, undefined, false]);

        answers[0]?.('out of range');
        await painted();
        expect([shown(root), settled(root)]).toEqual(['out of range', true]);
    });

    it('ADR-0380 s5: a message answered after the Focus moved on opens nothing, and the cell reached is asked in its turn', async () => {
        const answers: ((message: string | undefined) => void)[] = [];
        const { root, told } = mount({
            columns, rows: trades(20), rowHeight: 24, viewportHeight: 480,
            cellMessage: () => new Promise<string | undefined>(resolve => { answers.push(resolve); }),
        });
        await painted();
        click(root, 130, 5);
        await painted();
        told.clock.advance(300);
        key(root, 'ArrowDown');
        answers[0]?.('out of range');
        await painted();
        expect([answers.length, shown(root)]).toEqual([1, undefined]);

        told.clock.advance(300);
        await painted();
        answers[1]?.('below the floor');
        await painted();
        expect([answers.length, shown(root)]).toEqual([2, 'below the floor']);
    });

    it('ED-17: a run of arrows faster than the delay asks nothing', async () => {
        const { asked, cellMessage } = asking();
        const { root, told } = mount({ columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, cellMessage });
        await painted();
        click(root, 130, 5);
        for (let i = 0; i < 6; i++) {
            told.clock.advance(120);
            key(root, 'ArrowDown');
            await painted();
        }
        expect([asked, shown(root)]).toEqual([[], undefined]);
    });

    it('ED-17b: the pointer coming to rest asks once about the cell under it, a rest on the same cell asks nothing more, and leaving closes it', async () => {
        const { asked, cellMessage } = asking();
        const { root, told } = mount({ columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, cellMessage });
        await painted();
        mouse(root, 'mousemove', 150, 50);
        told.clock.advance(150);
        // Moved again before the delay ran: still passing over.
        mouse(root, 'mousemove', 152, 51);
        told.clock.advance(299);
        expect(asked).toEqual([]);
        told.clock.advance(1);
        await painted();
        expect([asked, shown(root)]).toEqual([['Book-2/Notional'], 'out of range']);

        mouse(root, 'mousemove', 155, 52);
        told.clock.advance(300);
        expect(asked).toEqual(['Book-2/Notional']);

        root.dispatchEvent(new MouseEvent('mouseleave'));
        await painted();
        expect(shown(root)).toBeUndefined();
    });

    it('ED-17b: a rest over the header asks nothing, and a cell with nothing to say shows nothing', async () => {
        const asked: string[] = [];
        const { root, told } = mount({
            columns, rows: trades(20), rowHeight: 24, viewportHeight: 480,
            cellMessage: (row, column) => { asked.push(row.book); return column.name === 'Book' ? undefined : ''; },
        });
        await painted();
        mouse(root, 'mousemove', 10, 5, q(root, '.ex-header-cell'));
        told.clock.advance(300);
        mouse(root, 'mousemove', 10, 5);
        told.clock.advance(300);
        mouse(root, 'mousemove', 130, 5);
        told.clock.advance(300);
        await painted();
        expect([asked, shown(root)]).toEqual([['Book-0', 'Book-0'], undefined]);
    });

    it('ADR-0034: while an editor holds with its own message, a rest asks nothing and the editor\'s message stands', async () => {
        const { asked, cellMessage } = asking();
        const editable = columns.map(column => ({ ...column, editable: true, validate: () => ({ kind: 'reject', message: 'not a number' }) as const }));
        const { root, told } = mount({ columns: editable, rows: trades(20), rowHeight: 24, viewportHeight: 480, cellMessage, onEdit: () => undefined });
        await painted();
        click(root, 130, 5);
        await painted();
        key(q(root, '.ex-key-field'), 'F2');
        await painted();
        key(q(root, '.ex-editor'), 'Enter');
        await painted();
        expect(shown(root)).toBe('not a number');

        mouse(root, 'mousemove', 150, 50);
        told.clock.advance(600);
        await painted();
        expect([asked, shown(root)]).toEqual([[], 'not a number']);
    });

    it('ED-17/ED-17b: a pointer at rest elsewhere does not take the message owed to the cell the Focus reached', async () => {
        const { asked, cellMessage } = asking();
        const { root, told } = mount({ columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, cellMessage });
        await painted();
        click(root, 130, 5);
        await painted();
        mouse(root, 'mousemove', 30, 50);
        told.clock.advance(300);
        await painted();
        expect(asked.toSorted()).toEqual(['Book-0/Notional', 'Book-2/Book']);
        expect(shown(root)).toBe('out of range');
    });

    it('ED-17b: a scroll under a still pointer asks nothing about the cell it was over', async () => {
        const { asked, cellMessage } = asking();
        const { root, told } = mount({ columns, rows: trades(200), rowHeight: 24, viewportHeight: 480, cellMessage });
        await painted();
        mouse(root, 'mousemove', 150, 50);
        told.clock.advance(100);
        await scrollTo(root, 240);
        told.clock.advance(600);
        expect(asked).toEqual([]);
    });

    it('ADR-0034: a new judgement asks the cell whose message stands again, and a cell put right says nothing', async () => {
        const options = { columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, cellMessage: (): string => 'negative' };
        const { root, view, told } = mount(options);
        await painted();
        mouse(root, 'mousemove', 150, 50);
        told.clock.advance(300);
        await painted();
        expect(shown(root)).toBe('negative');
        view.update({ ...options, cellMessage: () => undefined });
        await painted();
        expect(shown(root)).toBeUndefined();
    });

    it('ADR-0034: rows replaced under an open message take it away, rather than leave it under another row', async () => {
        const { cellMessage } = asking();
        const options = { columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, cellMessage };
        const { root, view, told } = mount(options);
        await painted();
        mouse(root, 'mousemove', 150, 50);
        told.clock.advance(300);
        await painted();
        expect(shown(root)).toBe('out of range');
        view.update({ ...options, rows: trades(20) });
        await painted();
        expect(shown(root)).toBeUndefined();
    });

    it('ED-17: a question given while the Focus stands on a cell asks about it once the delay has run', async () => {
        const options = { columns, rows: trades(20), rowHeight: 24, viewportHeight: 480 };
        const { root, view, told } = mount(options);
        await painted();
        click(root, 130, 5);
        await painted();
        const { asked, cellMessage } = asking();
        view.update({ ...options, cellMessage });
        told.clock.advance(300);
        await painted();
        expect([asked, shown(root)]).toEqual([['Book-0/Notional'], 'out of range']);
    });

    it('ADR-0021: the pointer is not heard for a rest while nobody asks why', async () => {
        const { root, told } = mount({ columns, rows: trades(20), rowHeight: 24, viewportHeight: 480 });
        await painted();
        mouse(root, 'mousemove', 150, 50);
        expect(told.clock.pending).toBe(0);
    });

    it('ADR-0034: an answer that is not text is refused by name', async () => {
        const { root, told } = mount({
            columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, cellMessage: () => 42 as unknown as string,
        });
        await painted();
        mouse(root, 'mousemove', 150, 50);
        expect(() => { told.clock.advance(300); }).toThrow("Column 'Notional': cellMessage answers a sentence or undefined, not number (ADR-0034).");
    });
});

const rules = (): GridRuleset<Trade> => new GridRuleset<Trade>()
    .column('Notional', (_row, text) => (Number(text) < 0 ? { kind: 'flag', message: 'negative' } : { kind: 'accept' }))
    .row(row => (row.book === 'Book-1' ? [{ column: 'Book', message: 'book closed' }] : []));

describe('the bundled ruleset (ADR-0034)', () => {

    it('judges the column rules on the text the editor opens on and the row rules after, and answers both channels', () => {
        const ruleset = rules();
        const rows: Trade[] = [{ book: 'Book-0', notional: -5 }, { book: 'Book-1', notional: 10 }, { book: 'Book-2', notional: 3 }];
        const [book, notional] = columns;
        if (book === undefined || notional === undefined) throw new Error('two columns');
        expect(ruleset.validatorFor('Book')).toBeUndefined();
        ruleset.evaluate(rows, columns);
        const { cellState, cellMessage } = ruleset;
        expect(rows.map(row => [cellState(row, book), cellState(row, notional), cellMessage(row, book), cellMessage(row, notional)])).toEqual([
            ['normal', 'error', undefined, 'negative'],
            ['error', 'normal', 'book closed', undefined],
            ['normal', 'normal', undefined, undefined],
        ]);
    });

    it('a judgement replaces the last whole, with new functions, so a verdict that changed is painted', () => {
        const ruleset = rules();
        const row: Trade = { book: 'Book-0', notional: -5 };
        const [, notional] = columns;
        if (notional === undefined) throw new Error('two columns');
        ruleset.evaluate([row], columns);
        const before = ruleset.cellState;
        const fixed: Trade = { ...row, notional: 5 };
        ruleset.evaluate([fixed], columns);
        expect([ruleset.cellState === before, ruleset.cellState(row, notional), ruleset.cellState(fixed, notional)]).toEqual([false, 'normal', 'normal']);
    });

    it('a column rule that answers no verdict is refused by name', () => {
        const maybe: unknown = { kind: 'maybe' };
        const ruleset = new GridRuleset<Trade>().column('Notional', () => maybe as EditVerdict);
        expect(() => { ruleset.evaluate(trades(1), columns); }).toThrow(/Column 'Notional': validate answers/u);
    });
});
