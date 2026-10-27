// @vitest-environment happy-dom
// Layer 2 (ADR-0390 section 7): the Formula Bar's two Chrome slots, its text field and its Name Box, as the core holds
// them. The boxes, what the fields show, what a key or a press in them means, and Enter's form stay the core's
// (ADR-0051): the contents draw a field in each box and hand it over, and the core reads and writes it. The contents
// here are a plain Chrome written against the contexts alone.
import { afterEach, describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import type { GridEditIntent } from '../src/cells/edit-intent';
import type { FormulaBarTextView, GridChromeSlot, GridFormulaBarTextSlotContext, GridNameBoxSlotContext } from '../src/dom/grid-chrome';
import type { GridColumn } from '../src/dom/grid-column';
import type { GridOptions } from '../src/dom/grid-view';
import { mount, painted, q, trades, unmountAll, type Trade } from './grid-harness';

afterEach(unmountAll);

const columns: readonly GridColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: trade => trade.book, width: 120, editable: true },
    { name: 'Notional', type: 'number', value: trade => trade.notional, width: 100 },
];

const rowY = (row: number): number => (row * 24) + 5;

function press(root: HTMLElement, x: number, y: number): void {
    for (const name of ['mousedown', 'mouseup']) {
        const event = new MouseEvent(name, { bubbles: true, cancelable: true, button: 0, buttons: name === 'mouseup' ? 0 : 1 });
        Object.defineProperty(event, 'offsetX', { value: x });
        Object.defineProperty(event, 'offsetY', { value: y });
        q(root, '.ex-viewport').dispatchEvent(event);
        if (name === 'mousedown' && !event.defaultPrevented) q(root, '.ex-scroller').focus();
    }
}

function key(name: string): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true });
    const target = document.activeElement;
    if (target === null) throw new Error('Nothing holds the keyboard.');
    target.dispatchEvent(event);
    return event;
}

function typeInto(field: HTMLInputElement, text: string): void {
    field.value += text;
    field.dispatchEvent(new Event('input', { bubbles: true }));
}

interface Drawn {
    mounts: number;
    cleanups: number;
    readonly views: FormulaBarTextView[];
}

const fresh = (): Drawn => ({ mounts: 0, cleanups: 0, views: [] });

/** A slot drawn as one plain input, wrapped in a span, handed over at once or once `later` resolves. */
function plainField<Context extends { readonly takeField: (control: HTMLElement) => void }>(drawn: Drawn, className: string, later?: Promise<void>,
    hear?: (context: Context) => () => void): GridChromeSlot<Context> {
    return (slot, context) => {
        drawn.mounts += 1;
        const wrapper = document.createElement('span');
        const field = document.createElement('input');
        field.className = className;
        wrapper.append(field);
        const stop = hear?.(context);
        const draw = (): void => {
            slot.append(wrapper);
            context.takeField(wrapper);
        };
        if (later === undefined) draw();
        else void later.then(draw);
        return () => {
            drawn.cleanups += 1;
            stop?.();
            wrapper.remove();
        };
    };
}

const textSlot = (drawn: Drawn, later?: Promise<void>): GridChromeSlot<GridFormulaBarTextSlotContext> => plainField<GridFormulaBarTextSlotContext>(
    drawn, 'plain-text', later, context => {
        drawn.views.push(context.view());
        return context.subscribe(() => { drawn.views.push(context.view()); });
    });
const nameSlot = (drawn: Drawn): GridChromeSlot<GridNameBoxSlotContext> => plainField<GridNameBoxSlotContext>(drawn, 'plain-name');

// A cell an array spills into: its Anchor's Formula is lent to the bar (ADR-0125).
const borrowed = (trade: Trade): string | undefined => (trade.book === 'Book-2' ? '=SEQUENCE(3)' : undefined);

// Contents that hand over their slot, which holds no field.
const handsTheBox: GridChromeSlot<GridFormulaBarTextSlotContext> = (slot, context) => { context.takeField(slot); return () => undefined; };

// A Name Box with a button of its own, which is a submit button by default.
const withButton: GridChromeSlot<GridNameBoxSlotContext> = (slot, context) => {
    const field = document.createElement('input');
    const clear = document.createElement('button');
    slot.append(field, clear);
    context.takeField(field);
    return () => { slot.replaceChildren(); };
};

// A Name Box drawn as a textarea, where Enter would submit nothing.
const drawsATextarea: GridChromeSlot<GridNameBoxSlotContext> = (slot, context) => {
    const area = document.createElement('textarea');
    slot.append(area);
    context.takeField(area);
    return () => { area.remove(); };
};

async function grid(options: Partial<GridOptions<Trade>>): Promise<{
    readonly root: HTMLElement; readonly edits: GridEditIntent<Trade>[]; readonly entered: string[]; readonly view: ReturnType<typeof mount<Trade>>['view'];
}> {
    const edits: GridEditIntent<Trade>[] = [];
    const entered: string[] = [];
    const { root, view } = mount<Trade>({
        columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, showFormulaBar: true,
        nameBoxLabel: cell => `R${String(cell.row + 1)}C${String(cell.column + 1)}`,
        onEdit: intent => { edits.push(intent); },
        onNameBoxEntered: typed => { entered.push(typed); },
        ...options,
    });
    await painted();
    return { root, edits, entered, view };
}

const barText = (root: HTMLElement): HTMLInputElement => q(root, '.ex-formula-bar > div.ex-editor.ex-formula-bar-text input.plain-text') as HTMLInputElement;
const nameBox = (root: HTMLElement): HTMLInputElement => q(root, '.ex-formula-bar form.ex-name-box-form > div.ex-name-box input.plain-name') as HTMLInputElement;

describe('the Formula Bar\'s text slot', () => {
    it('ADR-0051 / ADR-0390: the field in the core\'s box reads the Focus cell, is named and made read only by the core, and typing in it is the editor\'s, committed once', async () => {
        const drawn = fresh();
        const { root, edits } = await grid({ chrome: { formulaBarText: textSlot(drawn) } });
        press(root, 130, rowY(3));
        await painted();
        expect([barText(root).value, barText(root).readOnly, barText(root).getAttribute('aria-label'), drawn.views.at(-1)])
            .toEqual(['3000', true, 'Formula Bar', { readOnly: true, borrowed: false }]);
        press(root, 10, rowY(1));
        await painted();
        expect([barText(root).value, barText(root).readOnly, drawn.views.at(-1)]).toEqual(['Book-1', false, { readOnly: false, borrowed: false }]);
        barText(root).focus();
        typeInto(barText(root), '-x');
        await painted();
        expect((q(root, '.ex-scroller input.ex-editor') as HTMLInputElement).value).toBe('Book-1-x');
        key('Enter');
        await painted();
        expect([edits.map(edit => edit.value), barText(root).value, root.contains(document.activeElement), document.activeElement === barText(root)])
            .toEqual([['Book-1-x'], 'Book-2', true, false]);
        // Drawn once: the passes write the field, they do not mount the contents again.
        expect([drawn.mounts, drawn.cleanups]).toEqual([1, 0]);
    });

    it('ADR-0051: what is typed in the cell is shown in the field, and Escape from it cancels the one edit', async () => {
        const { root, edits } = await grid({ chrome: { formulaBarText: textSlot(fresh()) } });
        press(root, 10, rowY(0));
        await painted();
        key('Q');
        await painted();
        expect(barText(root).value).toBe('Q');
        barText(root).focus();
        key('Escape');
        await painted();
        expect([edits.length, root.querySelector('.ex-scroller .ex-editor'), barText(root).value]).toEqual([0, null, 'Book-0']);
    });

    it('ADR-0125: borrowed text is shown dimmed and read only, and the contents hear it', async () => {
        const drawn = fresh();
        const { root } = await grid({ chrome: { formulaBarText: textSlot(drawn) }, formulaBarBorrowedText: borrowed });
        press(root, 10, rowY(2));
        await painted();
        const box = q(root, '.ex-formula-bar > .ex-formula-bar-text');
        expect([barText(root).value, barText(root).readOnly, box.classList.contains('ex-formula-bar-borrowed'), drawn.views.at(-1)])
            .toEqual(['=SEQUENCE(3)', true, true, { readOnly: true, borrowed: true }]);
    });

    it('ADR-0390: a field drawn after the pass is handed what the bar shows, and contents that hand over no field are named', async () => {
        let draw: (() => void) | undefined;
        const { root } = await grid({ chrome: { formulaBarText: textSlot(fresh(), new Promise(resolve => { draw = resolve; })) } });
        press(root, 10, rowY(4));
        await painted();
        expect(root.querySelector('input.plain-text')).toBeNull();
        defined(draw, 'the draw')();
        await painted();
        expect([barText(root).value, barText(root).getAttribute('aria-label')]).toEqual(['Book-4', 'Formula Bar']);
        expect(() => mount<Trade>({ columns, rows: trades(5), rowHeight: 24, showFormulaBar: true, chrome: { formulaBarText: handsTheBox } }))
            .toThrow('takeField is handed the field chrome.formulaBarText drew, or an element holding it, inside the slot.');
    });
});

describe('the Name Box\'s slot', () => {
    it('ADR-0051 / ADR-0390: the field names the Focus, a press selects its text, and Enter hands what was typed to the Consumer and the keyboard back', async () => {
        const drawn = fresh();
        const { root, entered } = await grid({ chrome: { nameBox: nameSlot(drawn) } });
        press(root, 10, rowY(5));
        await painted();
        const field = nameBox(root);
        expect([field.value, field.getAttribute('aria-label'), q(root, '.ex-name-box').style.width === '']).toEqual(['R6C1', 'Name Box', false]);
        field.focus();
        expect([field.selectionStart, field.selectionEnd]).toEqual([0, 4]);
        field.value = 'B9';
        field.dispatchEvent(new Event('input', { bubbles: true }));
        (q(root, 'form.ex-name-box-form') as HTMLFormElement).requestSubmit();
        await painted();
        expect([entered, document.activeElement === field, root.contains(document.activeElement)]).toEqual([['B9'], false, true]);
        expect([drawn.mounts, drawn.cleanups]).toEqual([1, 0]);
    });

    it('ADR-0051: Escape leaves the Name Box, and the field names the Focus again', async () => {
        const { root, entered } = await grid({ chrome: { nameBox: nameSlot(fresh()) } });
        press(root, 10, rowY(2));
        await painted();
        const field = nameBox(root);
        field.focus();
        typeInto(field, 'zz');
        key('Escape');
        await painted();
        expect([entered, field.value, document.activeElement === field]).toEqual([[], 'R3C1', false]);
    });
});

describe('the Name Box\'s form', () => {
    it('ADR-0051: Enter is the core\'s form\'s, whatever button the contents draw, and a textarea is refused', async () => {
        const { root, entered } = await grid({ chrome: { nameBox: withButton } });
        const form = q(root, 'form.ex-name-box-form') as HTMLFormElement;
        const first = form.querySelector('button');
        expect([first?.hidden, first?.type]).toEqual([true, 'submit']);
        const field = q(root, '.ex-name-box input') as HTMLInputElement;
        field.focus();
        typeInto(field, '7');
        form.requestSubmit(form.querySelector('.ex-name-box button'));
        expect(entered).toEqual([]);
        form.requestSubmit(defined(first, 'the default button'));
        expect(entered).toEqual(['7']);
        expect(() => mount<Trade>({ columns, rows: trades(5), rowHeight: 24, showFormulaBar: true, chrome: { nameBox: drawsATextarea } }))
            .toThrow('chrome.nameBox handed over no text field: draw one input in the slot.');
    });
});

describe('the bar\'s slots the Consumer changes', () => {
    it('ADR-0390: another mount function draws the field again, though not while the user types in it', async () => {
        const first = fresh();
        const second = fresh();
        const options = { columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, showFormulaBar: true, onEdit: () => undefined };
        const { root, view } = await grid({ ...options, chrome: { formulaBarText: textSlot(first) } });
        press(root, 10, rowY(1));
        await painted();
        barText(root).focus();
        view.update({ ...options, chrome: { formulaBarText: textSlot(second) } });
        await painted();
        expect([first.cleanups, second.mounts, document.activeElement === barText(root)]).toEqual([0, 0, true]);
        key('Escape');
        view.update({ ...options, chrome: { formulaBarText: textSlot(second) } });
        await painted();
        expect([first.cleanups, second.mounts, barText(root).value]).toEqual([1, 1, 'Book-1']);
        // Another Name Box leaves the text field standing.
        const names = fresh();
        const textMount = textSlot(fresh());
        view.update({ ...options, chrome: { formulaBarText: textMount } });
        await painted();
        const standing = barText(root);
        view.update({ ...options, chrome: { formulaBarText: textMount, nameBox: nameSlot(names) } });
        await painted();
        expect([names.mounts, barText(root) === standing]).toEqual([1, true]);
        // The band taken away takes the contents with it.
        view.update({ ...options, chrome: { formulaBarText: textMount, nameBox: nameSlot(names) }, showFormulaBar: false });
        await painted();
        expect([root.querySelector('.ex-formula-bar'), second.cleanups, names.cleanups]).toEqual([null, 1, 1]);
    });
});
