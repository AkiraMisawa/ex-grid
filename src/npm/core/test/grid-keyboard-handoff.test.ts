// @vitest-environment happy-dom
// The keyboard handed back to the grid, and handed on from it, by the Consumer (DC-61; ADR-0070, ADR-0021):
// `returnKeyboard` and `handKeyboardTo`, each granted only while the keyboard is still the grid's.
import { afterEach, describe, expect, it } from 'vitest';
import type { GridColumn } from '../src/dom/grid-column';
import type { GridSelection } from '../src/selection/selection';
import { mount, painted, q, trades, type Trade } from './grid-harness';

const columns: readonly GridColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: trade => trade.book, width: 120, editable: true },
    { name: 'Notional', type: 'number', value: trade => trade.notional, width: 100 },
];

const outside: HTMLElement[] = [];
afterEach(() => {
    for (const element of outside.splice(0)) element.remove();
});

/** An element of the page's own, beside the grid. */
function beside(html: string): HTMLElement {
    const box = document.createElement('div');
    box.innerHTML = html;
    document.body.append(box);
    outside.push(box);
    return box;
}

async function grid(edits = true): Promise<{ readonly root: HTMLElement; readonly view: ReturnType<typeof mount<Trade>>['view']; readonly heard: GridSelection[] }> {
    const heard: GridSelection[] = [];
    const { root, view } = mount<Trade>({
        columns: edits ? columns : columns.map(column => ({ ...column, editable: false })), rows: trades(20), rowHeight: 24,
        viewportHeight: 480, showFormulaBar: true, onSelectionChanged: selection => { heard.push(selection); },
        ...(edits ? { onEdit: () => { /* the edit is not the test's */ } } : {}),
    });
    await painted();
    return { root, view, heard };
}

const keyField = (root: HTMLElement): HTMLElement => q(root, 'input.ex-key-field');

describe('returnKeyboard (DC-61)', () => {
    it('DC-61: with the keyboard on nothing it goes to the Keyboard Field, or the root of a grid that does not edit, and moves no Selection', async () => {
        const editing = await grid();
        const display = await grid(false);
        (document.activeElement as HTMLElement | null)?.blur();

        editing.view.returnKeyboard();
        const first = document.activeElement;
        (document.activeElement as HTMLElement | null)?.blur();
        display.view.returnKeyboard();

        expect([first, document.activeElement]).toEqual([keyField(editing.root), display.root]);
        expect([editing.heard, display.heard]).toEqual([[], []]);
    });

    it('DC-61: a control of the page\'s, another grid, the Formula Bar and the Name Box each keep the keyboard', async () => {
        const { root, view } = await grid();
        const other = await grid();
        const button = q(beside('<button>Elsewhere</button>'), 'button');
        const kept: (Element | null)[] = [];
        for (const holder of [button, keyField(other.root), q(root, '.ex-formula-bar-text'), q(root, '.ex-name-box')]) {
            holder.focus();
            view.returnKeyboard();
            kept.push(document.activeElement === holder ? holder : document.activeElement);
        }

        expect(kept).toEqual([button, keyField(other.root), q(root, '.ex-formula-bar-text'), q(root, '.ex-name-box')]);
    });

    it('DC-61: handed the frame that holds the keyboard, it takes it back from there before the frame goes', async () => {
        const { root, view } = await grid();
        const frame = beside('<button>Close</button>');
        const close = q(frame, 'button');
        close.focus();
        view.returnKeyboard();
        const without = document.activeElement;

        view.returnKeyboard(frame);

        expect([without, document.activeElement]).toEqual([close, keyField(root)]);
    });

    it('DC-61: once the grid is destroyed it does nothing, and asks nothing of the grid it was', async () => {
        const { view } = await grid();
        view.destroy();
        (document.activeElement as HTMLElement | null)?.blur();

        expect(() => { view.returnKeyboard(); }).not.toThrow();
        expect(document.activeElement).toBe(document.body);
    });
});

describe('handKeyboardTo (DC-61)', () => {
    it('DC-61: a container hands the keyboard to its first text field, its text selected, while the keyboard is the grid\'s', async () => {
        const { root, view, heard } = await grid();
        keyField(root).focus();
        const frame = beside('<button>Close</button><input type="text" value="Rates">');
        const field = q(frame, 'input') as HTMLInputElement;

        view.handKeyboardTo(frame);

        expect([document.activeElement, field.selectionStart, field.selectionEnd, heard]).toEqual([field, 0, 5, []]);
    });

    it('DC-61: a container passes over a read-only combobox and an element out of the tab order for its text field, and the scroll is as asked', async () => {
        const { root, view } = await grid();
        keyField(root).focus();
        const frame = beside('<div tabindex="-1"><input readonly role="combobox" value="Pick"><input type="text" value="Rates"></div>');
        const field = q(frame, 'input[type="text"]') as HTMLInputElement;
        const asked: (FocusOptions | undefined)[] = [];
        const own = field.focus.bind(field);
        field.focus = (options?: FocusOptions) => { asked.push(options); own(options); };

        view.handKeyboardTo(frame, { preventScroll: true });

        expect([document.activeElement, asked]).toEqual([field, [{ preventScroll: true }]]);
    });

    it('DC-61: the Formula Bar and the Name Box keep the keyboard from a hand-on', async () => {
        const { root, view } = await grid();
        const close = q(beside('<button>Close</button>'), 'button');
        const kept: (Element | null)[] = [];
        for (const holder of [q(root, '.ex-formula-bar-text'), q(root, '.ex-name-box')]) {
            holder.focus();
            view.handKeyboardTo(close);
            kept.push(document.activeElement);
        }

        expect(kept).toEqual([q(root, '.ex-formula-bar-text'), q(root, '.ex-name-box')]);
    });

    it('DC-61: a control the user moved to meanwhile keeps the keyboard; a container with no control is refused', async () => {
        const { view } = await grid();
        const chosen = q(beside('<button>Chosen</button>'), 'button');
        const close = q(beside('<button>Close</button>'), 'button');
        chosen.focus();

        view.handKeyboardTo(close);

        expect(document.activeElement).toBe(chosen);
        expect(() => { view.handKeyboardTo(beside('<p>No control</p>')); }).toThrow(RangeError);
    });

    it('DC-61: once the grid is destroyed, the control takes the keyboard by its own focus', async () => {
        const { view } = await grid();
        const chosen = q(beside('<button>Chosen</button>'), 'button');
        const close = q(beside('<button>Close</button>'), 'button');
        view.destroy();
        chosen.focus();

        view.handKeyboardTo(close);

        expect(document.activeElement).toBe(close);
    });
});
