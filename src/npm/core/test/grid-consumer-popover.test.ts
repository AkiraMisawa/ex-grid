// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): a Consumer's popover (ADR-0050, item 16). The Consumer's own contents stand in the
// grid's popover frame, take the keyboard, wrap Tab inside it, and close by Escape, by a press elsewhere, or by the
// contents' own close, the keyboard back on the grid. ExSheet's Format Cells stands in it.
import { describe, expect, it } from 'vitest';
import type { GridPopoverContext } from '../src/dom/consumer-popover';
import { selectionRange } from '../src/selection/range';
import { columns, mount, painted, q, trades, type Trade } from './grid-harness';

function contents(context: { current: GridPopoverContext | undefined }): (popover: GridPopoverContext) => HTMLElement {
    return popover => {
        context.current = popover;
        const element = document.createElement('div');
        element.className = 'mine';
        for (const name of ['first', 'last']) {
            const button = document.createElement('button');
            button.textContent = name;
            element.append(button);
        }
        return element;
    };
}

function key(name: string, init: KeyboardEventInit = {}): KeyboardEvent {
    const target = document.activeElement;
    if (target === null) throw new Error('Nothing holds the keyboard.');
    const event = new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init });
    target.dispatchEvent(event);
    return event;
}

async function grid(): Promise<{ readonly root: HTMLElement; readonly view: ReturnType<typeof mount<Trade>>['view'] }> {
    const { root, view } = mount({ columns: columns.map(column => ({ ...column, editable: true })), rows: trades(20), rowHeight: 24, viewportHeight: 480,
        onEdit: intent => { intent.refuse('Not written: the test only opens edits.'); } });
    await painted();
    q(root, '.ex-scroller').focus();
    return { root, view };
}

describe('a Consumer\'s popover (ADR-0050, item 16)', () => {
    it('ADR-0050 item 16: the contents stand in the grid\'s frame, named in the Consumer\'s words, and take the keyboard; Escape closes it and gives the keyboard back', async () => {
        const { root, view } = await grid();
        const answered = (): number => Number(q(root, '[data-ex-answered]').getAttribute('data-ex-answered'));
        const before = answered();
        const held: { current: GridPopoverContext | undefined } = { current: undefined };
        expect(view.openPopover(contents(held), 'Format Cells')).toBe(true);
        await painted();
        // The opening is an input answered (ADR-0220): a script reads the frame once the count has moved.
        expect(answered()).toBe(before + 1);
        const frame = q(root, '.ex-popover.ex-popover-consumer');
        expect([frame.getAttribute('role'), frame.getAttribute('aria-label')]).toEqual(['dialog', 'Format Cells']);
        expect(document.activeElement?.textContent).toBe('first');
        expect(frame.style.transform).toBe('translateX(-50%)');
        // Ctrl+F inside it is taken, so the browser's find stays shut, and the find panel does not take its place.
        expect(key('f', { ctrlKey: true }).defaultPrevented).toBe(true);
        expect([root.querySelector('.ex-popover-find'), document.activeElement?.textContent]).toEqual([null, 'first']);
        expect(key('Escape').defaultPrevented).toBe(true);
        expect(root.querySelector('.ex-popover-consumer')).toBeNull();
        expect(root.contains(document.activeElement)).toBe(true);
    });

    it('ADR-0039: Tab wraps inside the frame, and the contents\' own close closes it; a close of an earlier opening, or an opening whose contents fail, closes nothing', async () => {
        const { root, view } = await grid();
        const first: { current: GridPopoverContext | undefined } = { current: undefined };
        view.openPopover(contents(first), 'One');
        const [leading, trailing] = [...root.querySelectorAll<HTMLElement>('.ex-popover-consumer > .ex-focus-wrap')];
        trailing?.focus();
        expect(document.activeElement?.textContent).toBe('first');
        leading?.focus();
        expect(document.activeElement?.textContent).toBe('last');
        // Reached from outside the contents, a sentinel lets the keyboard in at its own end.
        q(root, '.ex-scroller').focus();
        leading?.focus();
        expect(document.activeElement?.textContent).toBe('first');
        q(root, '.ex-scroller').focus();
        trailing?.focus();
        expect(document.activeElement?.textContent).toBe('last');
        const second: { current: GridPopoverContext | undefined } = { current: undefined };
        view.openPopover(contents(second), 'Two');
        first.current?.close();
        expect(q(root, '.ex-popover-consumer').getAttribute('aria-label')).toBe('Two');
        expect(() => view.openPopover(() => { throw new Error('Made nothing.'); }, 'Three')).toThrow('Made nothing.');
        expect(q(root, '.ex-popover-consumer').getAttribute('aria-label')).toBe('Two');
        second.current?.close();
        expect(root.querySelector('.ex-popover-consumer')).toBeNull();
        expect(root.contains(document.activeElement)).toBe(true);
    });

    it('ADR-0390: closed hears each close, by Escape, by the handle, by another opening or with the grid, while the contents still stand', async () => {
        const { root, view } = await grid();
        const heard: string[] = [];
        const hears = (name: string) => () => { heard.push(`${name}:${String(root.querySelector('.ex-popover-consumer .mine') !== null)}`); };
        view.openPopover(contents({ current: undefined }), 'One', hears('one'));
        key('Escape');
        view.openPopover(contents({ current: undefined }), 'Two', hears('two'));
        view.openPopover(contents({ current: undefined }), 'Three', hears('three'));
        view.closePopover();
        view.openPopover(contents({ current: undefined }), 'Four', hears('four'));
        view.destroy();
        expect(heard).toEqual(['one:true', 'two:true', 'three:true', 'four:true']);
    });

    it('ADR-0390: a closed that is not a function is refused by name', async () => {
        const { view } = await grid();
        expect(() => view.openPopover(contents({ current: undefined }), 'One', 'no' as never)).toThrow(/hears it close/u);
    });

    it('ADR-0039: while a popup of the contents\' own is open, Escape is the popup\'s', async () => {
        const { root, view } = await grid();
        const held: { current: GridPopoverContext | undefined } = { current: undefined };
        view.openPopover(contents(held), 'Format Cells');
        held.current?.innerPopupChanged(true);
        key('Escape');
        expect(root.querySelector('.ex-popover-consumer')).not.toBeNull();
        held.current?.innerPopupChanged(false);
        key('Escape');
        expect(root.querySelector('.ex-popover-consumer')).toBeNull();
    });

    it('ADR-0050 item 16: refused while an edit is open, and a label that names nothing is refused by name', async () => {
        const { root, view } = await grid();
        expect(() => view.openPopover(contents({ current: undefined }), ' ')).toThrow(/label is not empty/u);
        expect(view.placeSelection(selectionRange(0, 0, 1, 1), { row: 0, column: 0 }, Number(q(root, '.ex-viewport').getAttribute('data-ex-sequence')))).toBe(true);
        await painted();
        key('Z');
        await painted();
        expect(root.querySelector('.ex-editor')).not.toBeNull();
        expect(view.openPopover(contents({ current: undefined }), 'Format Cells')).toBe(false);
        expect(root.querySelector('.ex-popover-consumer')).toBeNull();
    });
});
