// The contract every Wrapper keeps with the slots for what the grid only shows (ADR-0390 section 7): run at layer 2
// against each one alike. A cell's message says the core's sentence in the core's box, the Selection Summary shows
// the core's figures as the core formatted them, and the loading indicator draws something while rows are on their
// way and nothing once they are in: when each shows and what it says are the core's, and the Wrapper decides nothing.
import { createElement } from 'react';
import { act, cleanup, render } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { GridSource, type GridColumn, type GridPage } from '@ex-grid/core';
import { ExGrid } from '../src/ex-grid';
import type { ExGridChrome } from '../src/chrome';

interface Trade {
    readonly book: string;
    readonly notional: number;
}

const trades = (count: number): readonly Trade[] => Array.from({ length: count }, (_, at) => ({ book: `Book-${at}`, notional: (at + 1) * 1000 }));
const columns: readonly GridColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: trade => trade.book, width: 120 },
    {
        name: 'Notional', type: 'number', value: trade => trade.notional, width: 100, editable: true,
        validate: (_row, text) => (Number.isNaN(Number(text)) ? { kind: 'reject', message: `'${text}' is not a number` } : { kind: 'accept' }),
    },
];

async function settled(): Promise<void> {
    await act(async () => {
        for (let turn = 0; turn < 4; turn++) await new Promise(resolve => { setTimeout(resolve, 0); });
    });
}

function key(name: string, init: KeyboardEventInit = {}): void {
    const target = document.activeElement;
    if (target === null) throw new Error('Nothing holds the keyboard.');
    target.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init }));
}

function rootOf(container: HTMLElement): HTMLElement {
    const root = container.firstElementChild;
    if (!(root instanceof HTMLElement)) throw new Error('The grid drew no root.');
    return root;
}

function boxOf(root: Element, selector: string): HTMLElement {
    const box = root.querySelector<HTMLElement>(selector);
    if (box === null) throw new Error(`Nothing matches ${selector}.`);
    return box;
}

/** A press on a cell, as the browser lands it on the rows: Notional is from 120 px, rows are 24 px. */
function press(root: HTMLElement, x: number, y: number): void {
    for (const type of ['mousedown', 'mouseup']) {
        const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons: type === 'mousedown' ? 1 : 0 });
        Object.defineProperty(event, 'offsetX', { value: x });
        Object.defineProperty(event, 'offsetY', { value: y });
        boxOf(root, '.ex-viewport').dispatchEvent(event);
    }
}

/**
 * Registers the contract's tests against a Wrapper's Chrome: `chrome` names it, and must fill the cellMessage,
 * selectionSummary and loadingIndicator slots.
 */
export function shownSlotContract(name: string, chrome: ExGridChrome<unknown>): void {
    describe(`${name}: the contract of the slots for what the grid shows (ADR-0390 section 7)`, () => {
        afterEach(() => { cleanup(); });

        it('ADR-0034: a Reject\'s message says the core\'s sentence in the core\'s box, which describes the editor', async () => {
            const { container } = render(createElement(ExGrid<Trade>, {
                columns, rows: trades(20), rowHeight: 24, viewportHeight: 320, viewportWidth: 720, onEdit: () => undefined, chrome,
            }));
            const root = rootOf(container);
            await settled();
            press(root, 130, 5);
            root.focus();
            key('a');
            await settled();
            key('Enter');
            await settled();
            const message = boxOf(root, ':scope > .ex-message');
            expect([message.textContent.trim(), document.activeElement?.getAttribute('aria-describedby')]).toEqual(['\'a\' is not a number', message.id]);
        });

        it('ADR-0130: the Selection Summary shows the core\'s figures, each as the core formatted it, in Excel\'s order', async () => {
            const { container } = render(createElement(ExGrid<Trade>, {
                columns, source: GridSource.from(trades(10)), rowHeight: 24, viewportHeight: 320, viewportWidth: 720, chrome,
            }));
            const root = rootOf(container);
            await settled();
            press(root, 130, 29);
            root.focus();
            key('ArrowDown', { shiftKey: true });
            key('ArrowDown', { shiftKey: true });
            await settled();
            // Rows 1 to 3 hold 2000, 3000 and 4000: each figure's name and text, in that order.
            const text = boxOf(root, '.ex-status .ex-summary').textContent;
            expect(text).toMatch(/Average\D*3000\D.*Count\D*3\D.*Sum\D*9000(\D|$)/u);
        });

        it('ADR-0010 / ADR-0004: the loading indicator draws something while rows are on their way, at the grid\'s foot, and nothing once they are in', async () => {
            const answers: ((page: GridPage<Trade>) => void)[] = [];
            const source = GridSource.fetch<Trade>(() => new Promise(resolve => { answers.push(resolve); }), { readAheadRows: 0 });
            const { container } = render(createElement(ExGrid<Trade>, { columns, source, rowHeight: 24, viewportHeight: 240, viewportWidth: 720, chrome }));
            const root = rootOf(container);
            await settled();
            const box = boxOf(root, ':scope > .ex-loading-indicator');
            expect(box.childElementCount).toBeGreaterThan(0);
            answers.shift()?.({ rows: trades(10), start: 0, totalCount: 10 });
            await settled();
            expect([root.classList.contains('ex-loading'), box.childElementCount]).toEqual([false, 0]);
        });
    });
}
