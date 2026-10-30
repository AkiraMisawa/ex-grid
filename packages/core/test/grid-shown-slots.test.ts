// @vitest-environment happy-dom
// Layer 2 (ADR-0390 section 7): the Chrome slots for what the grid only shows, as the core holds them: a cell's
// message, the loading indicator and the Selection Summary. When each shows, where, and what it says stay the
// core's (ADR-0034, ADR-0004, ADR-0130); the contents draw it in the core's box, and are mounted again when what
// they show changes. The contents here are a plain Chrome written against the context alone.
import { afterEach, describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import {
    ShownSlot, type GridCellMessageSlotContext, type GridChromeSlot, type GridLoadingIndicatorSlotContext, type GridSelectionSummarySlotContext,
} from '../src/dom/grid-chrome';
import type { GridColumn } from '../src/dom/grid-column';
import { cell } from '../src/selection/cell';
import { selectionRange } from '../src/selection/range';
import type { GridPage } from '../src/source/fetching-source';
import { GridSource } from '../src/source/grid-source';
import { columns, mount, painted, q, trades, unmountAll, type Trade } from './grid-harness';

afterEach(unmountAll);

interface Drawn<Context> {
    readonly contexts: Context[];
    cleanups: number;
}

/** A slot drawn as `text` of its context, each mount and cleanup counted. */
function plain<Context>(text: (context: Context) => string): { readonly drawn: Drawn<Context>; readonly slot: GridChromeSlot<Context> } {
    const drawn: Drawn<Context> = { contexts: [], cleanups: 0 };
    return {
        drawn,
        slot: (slot, context) => {
            drawn.contexts.push(context);
            const shown = document.createElement('b');
            shown.className = 'plain';
            shown.textContent = text(context);
            slot.append(shown);
            return () => {
                drawn.cleanups += 1;
                shown.remove();
            };
        },
    };
}
const last = <Context>(drawn: Drawn<Context>): Context => defined(drawn.contexts.at(-1), 'a mount');

const settle = (): Promise<void> => new Promise(resolve => { setTimeout(resolve, 0); });

// A press on the rows at offsets into the Viewport, rows of 24 px: Notional is from 120 px.
function click(root: HTMLElement, x: number, y: number): void {
    for (const type of ['mousedown', 'mouseup']) {
        const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons: type === 'mousedown' ? 1 : 0 });
        Object.defineProperty(event, 'offsetX', { value: x });
        Object.defineProperty(event, 'offsetY', { value: y });
        q(root, '.ex-viewport').dispatchEvent(event);
    }
}

function key(name: string): void {
    const target = document.activeElement;
    if (target === null) throw new Error('Nothing holds the keyboard.');
    target.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true }));
}

describe('a cell\'s message slot', () => {
    it('ADR-0034: a flagged cell\'s message is drawn in the core\'s tooltip, its close takes it away, and the Focus moving takes the contents away', async () => {
        const { drawn, slot } = plain<GridCellMessageSlotContext>(context => `${context.column}: ${context.message}`);
        const { root, told } = mount({
            columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, cellMessage: () => 'out of range', chrome: { cellMessage: slot },
        });
        await painted();
        click(root, 130, 53);
        await painted();
        told.clock.advance(300);
        await painted();
        const message = q(root, '.ex-message');
        expect([message.getAttribute('role'), message.textContent, last(drawn).editorError]).toEqual(['tooltip', 'Notional: out of range', false]);
        last(drawn).close();
        await painted();
        expect([root.querySelector('.ex-message'), drawn.cleanups]).toEqual([null, 1]);
        click(root, 130, 77);
        await painted();
        told.clock.advance(300);
        await painted();
        expect([drawn.contexts.length, q(root, '.ex-message').textContent]).toEqual([2, 'Notional: out of range']);
        click(root, 10, 101);
        await painted();
        expect([root.querySelector('.ex-message'), drawn.cleanups]).toEqual([null, 2]);
    });

    it('ADR-0034: a Reject is drawn as the editor\'s error, which describes the editor; its close leaves it, and Escape takes it away', async () => {
        const { drawn, slot } = plain<GridCellMessageSlotContext>(context => context.message);
        const editable: readonly GridColumn<Trade>[] = [{
            name: 'Notional', type: 'number', value: trade => trade.notional, width: 100, editable: true,
            validate: (_row, text) => (Number.isNaN(Number(text)) ? { kind: 'reject', message: `'${text}' is not a number` } : { kind: 'accept' }),
        }];
        const { root, view } = mount({
            columns: editable, rows: trades(20), rowHeight: 24, viewportHeight: 480, onEdit: () => undefined, chrome: { cellMessage: slot },
        });
        await painted();
        expect(view.placeSelection(selectionRange(0, 0, 1, 1), cell(0, 0), view.readView().rowSequenceVersion)).toBe(true);
        root.focus();
        key('a');
        key('Enter');
        await painted();
        const message = q(root, '.ex-message');
        expect([message.textContent, last(drawn).editorError, q(root, 'input.ex-editor').getAttribute('aria-describedby')])
            .toEqual(['\'a\' is not a number', true, message.id]);
        last(drawn).close();
        await painted();
        expect(q(root, '.ex-message').textContent).toBe('\'a\' is not a number');
        key('Escape');
        await painted();
        expect([root.querySelector('.ex-message'), drawn.cleanups]).toEqual([null, 1]);
    });
});

describe('the loading indicator\'s slot', () => {
    it('ADR-0010 / ADR-0004: it is handed whether rows are on their way and how many painted rows are Placeholders, which stay the core\'s', async () => {
        const { drawn, slot } = plain<GridLoadingIndicatorSlotContext>(context => `${context.loading} ${context.placeholderRows}`);
        const answers: ((page: GridPage<Trade>) => void)[] = [];
        const source = GridSource.fetch<Trade>(() => new Promise(resolve => { answers.push(resolve); }), { readAheadRows: 0 });
        const { root } = mount({ columns, source, rowHeight: 24, viewportHeight: 480, chrome: { loadingIndicator: slot } });
        await painted();
        const box = q(root, ':scope > .ex-loading-indicator');
        expect([last(drawn).loading, root.classList.contains('ex-loading')]).toEqual([true, true]);
        defined(answers[0], 'the first fetch')({ rows: trades(10), start: 0, totalCount: 5000 });
        await settle();
        expect([last(drawn), root.querySelectorAll('.ex-viewport > .ex-placeholder').length]).toEqual([{ loading: true, placeholderRows: 10 }, 10]);
        defined(answers[1], 'the rows on screen')({ rows: trades(20), start: 0, totalCount: 5000 });
        await settle();
        expect([last(drawn), box.textContent, root.classList.contains('ex-loading')]).toEqual([{ loading: false, placeholderRows: 0 }, 'false 0', false]);
        expect(drawn.contexts.length - drawn.cleanups).toBe(1);
    });
});

describe('the Selection Summary\'s slot', () => {
    it('ADR-0130: the figures are the core\'s, as it formats them, drawn in the core\'s box, which keeps the pending class', async () => {
        const { drawn, slot } = plain<GridSelectionSummarySlotContext>(context => (context.status === 'pending'
            ? context.label('summary-pending')
            : context.figures.map(figure => `${context.label(figure.labelId)}=${figure.text}`).join(' ')));
        const { root, view } = mount({ columns, source: GridSource.from(trades(10)), rowHeight: 24, chrome: { selectionSummary: slot } });
        await painted();
        expect(view.placeSelection(selectionRange(1, 1, 3, 1), cell(1, 1), view.readView().rowSequenceVersion)).toBe(true);
        await painted();
        const box = q(root, '.ex-status .ex-summary');
        expect([box.className, last(drawn).status]).toEqual(['ex-summary ex-summary-pending', 'pending']);
        await settle();
        expect([box.className, box.textContent, last(drawn).figures.map(figure => figure.figure)])
            .toEqual(['ex-summary', 'Average=2000 Count=3 Sum=6000', ['average', 'count', 'sum']]);
        expect(drawn.contexts.length - drawn.cleanups).toBe(1);
        // The strip leaving the status line takes the contents with it.
        view.update({ columns, source: GridSource.from(trades(10)), rowHeight: 24, chrome: { selectionSummary: slot }, showSelectionSummary: false });
        await painted();
        expect([root.querySelector('.ex-summary'), drawn.contexts.length - drawn.cleanups]).toEqual([null, 0]);
    });
});

describe('a slot the Consumer takes back', () => {
    it('ADR-0390: contents that cannot be drawn are thrown once, not again on every pass, and are tried for what shows next', () => {
        let tries = 0;
        const shown = new ShownSlot<string>('selectionSummary', document.createElement('span'));
        const failing: GridChromeSlot<string> = () => { tries += 1; throw new Error('drawn nowhere'); };
        expect(() => { shown.show(failing, 'a', () => 'a'); }).toThrow('drawn nowhere');
        shown.show(failing, 'a', () => 'a');
        expect(() => { shown.show(failing, 'b', () => 'b'); }).toThrow('drawn nowhere');
        expect([tries, shown.shows('b')]).toEqual([2, false]);
    });

    it('ADR-0390: the built-in message takes the place of the contents\' own, though they said the same', async () => {
        const { drawn, slot } = plain<GridCellMessageSlotContext>(context => context.message);
        const options = { columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, cellMessage: () => 'out of range' };
        const { root, view, told } = mount({ ...options, chrome: { cellMessage: slot } });
        await painted();
        click(root, 130, 5);
        await painted();
        told.clock.advance(300);
        await painted();
        view.update(options);
        await painted();
        expect([q(root, '.ex-message').innerHTML, drawn.cleanups]).toEqual(['out of range', 1]);
    });
});
