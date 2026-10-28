// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): completion and the argument hint (ADR-0051, ADR-0058), in the DOM. The editor's text
// and caret reach the Consumer as the user types or moves the caret, in the Cell Editor and the Formula Bar alike; the
// answer is painted beneath the surface typed in; ↑ and ↓ choose, Tab accepts and leaves the hint alone, Escape closes
// the list; a stale answer is never shown; and over Point, ← and the like point past the list.
import { afterEach, describe, expect, it } from 'vitest';
import { EditorCompletion, EditorHint } from '../src/cells/editor-text';
import { checkedCompletion, completionPlace } from '../src/dom/completion';
import { wireEditorCompletion } from '../src/dom/slot-wiring';
import type { GridEditorCompletionSlotContext } from '../src/dom/grid-chrome';
import type { GridColumn } from '../src/dom/grid-column';
import type { GridOptions } from '../src/dom/grid-view';
import { defined } from '../src/must';
import type { SelectionRange } from '../src/selection/range';
import { mount, painted, q, trades, unmountAll, type Trade } from './grid-harness';

const columns: readonly GridColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: trade => trade.book, width: 120, editable: true },
    { name: 'Notional', type: 'number', value: trade => trade.notional, width: 100, editable: true },
];

const names = ['SUM', 'SUMIF', 'AVERAGE'];

// A sheet's answer, cut down: the names that begin with the letters before the caret, each replacing them with its
// name and `(`; after `SUM(`, the hint with its first argument set off, and after `MATCH(x,` a fixed list of values.
function answer(text: string, caret: number): EditorCompletion | undefined {
    if (!text.startsWith('=')) return undefined;
    const before = text.slice(0, caret);
    if (before.endsWith('MATCH(x,')) {
        return new EditorCompletion({ candidates: ['0', '1'].map(value => ({ label: value, start: caret, length: 0, insert: value })) });
    }
    const letters = /[A-Z]+$/u.exec(before)?.[0];
    const hint = before.includes('SUM(') ? new EditorHint({ text: 'SUM(number1, [number2], ...)', emphasisStart: 4, emphasisLength: 7 }) : undefined;
    const candidates = letters === undefined ? [] : names.filter(name => name.startsWith(letters))
        .map(name => ({ label: name, start: caret - letters.length, length: letters.length, insert: `${name}(` }));
    return new EditorCompletion(hint === undefined ? { candidates } : { candidates, hint });
}

const cellName = (row: number, column: number): string => `${String.fromCharCode(65 + column)}${String(row + 1)}`;
const referenceText = (range: SelectionRange): string => cellName(range.topRow, range.leftColumn);
const pointAt = (text: string, caret: number): boolean => text.startsWith('=') && '=+-*/(,'.includes(text.charAt(caret - 1));

const rowY = (row: number): number => (row * 24) + 5;

function press(root: HTMLElement, row: number, column: number): void {
    for (const name of ['mousedown', 'mouseup']) {
        const event = new MouseEvent(name, { bubbles: true, cancelable: true, button: 0, buttons: name === 'mouseup' ? 0 : 1 });
        Object.defineProperty(event, 'offsetX', { value: (column * 120) + 10 });
        Object.defineProperty(event, 'offsetY', { value: rowY(row) });
        q(root, '.ex-viewport').dispatchEvent(event);
        if (name === 'mousedown' && !event.defaultPrevented) q(root, '.ex-scroller').focus();
    }
}

function key(name: string, init: KeyboardEventInit = {}): KeyboardEvent {
    const target = document.activeElement;
    if (target === null) throw new Error('Nothing holds the keyboard.');
    const event = new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init });
    target.dispatchEvent(event);
    target.dispatchEvent(new KeyboardEvent('keyup', { key: name, bubbles: true }));
    return event;
}

function field(root: HTMLElement, selector: string): HTMLInputElement {
    const found = q(root, selector);
    if (!(found instanceof HTMLInputElement)) throw new TypeError(`${selector} is an input.`);
    return found;
}
const editor = (root: HTMLElement): HTMLInputElement => field(root, 'input.ex-editor');
const barText = (root: HTMLElement): HTMLInputElement => field(root, '.ex-formula-bar > input.ex-formula-bar-text');

// Typed at the caret, as the field types a character the grid leaves to it.
function type(into: HTMLInputElement, text: string): void {
    const at = into.selectionStart ?? into.value.length;
    into.setRangeText(text, at, into.selectionEnd ?? at, 'end');
    into.dispatchEvent(new Event('input', { bubbles: true }));
    into.dispatchEvent(new KeyboardEvent('keyup', { key: text.slice(-1), bubbles: true }));
}

const box = (root: HTMLElement): HTMLElement | null => root.querySelector('.ex-completion');
const labels = (root: HTMLElement): (string | null)[] => [...root.querySelectorAll('.ex-completion-item')].map(item => item.textContent);
const chosen = (root: HTMLElement): string | null | undefined => root.querySelector('.ex-completion-selected')?.textContent;
const hint = (root: HTMLElement): string | undefined => root.querySelector('.ex-completion-hint')?.innerHTML;

interface Mounted {
    readonly root: HTMLElement;
    readonly asked: (readonly [string, number])[];
    readonly edits: string[];
}

async function grid(given: Partial<GridOptions<Trade>> = {}, complete: GridOptions<Trade>['completeEditorText'] | null = answer): Promise<Mounted> {
    const asked: (readonly [string, number])[] = [];
    const edits: string[] = [];
    const { root } = mount<Trade>({
        columns, rows: trades(20), rowHeight: 24, viewportHeight: 480,
        ...(complete === null ? {} : {
            completeEditorText: (text: string, caret: number) => {
                asked.push([text, caret]);
                return complete(text, caret);
            },
        }),
        onEdit: intent => { edits.push(intent.value); },
        ...given,
    });
    await painted();
    press(root, 1, 0);
    await painted();
    return { root, asked, edits };
}

// Opens Overwrite on the pressed cell with `=`, then types the rest.
async function typeFormula(root: HTMLElement, rest: string): Promise<void> {
    key('=');
    await painted();
    type(editor(root), rest);
    await painted();
}

afterEach(unmountAll);

describe('Completion and the argument hint (ADR-0051)', () => {
    it('DC-1: with nothing declared, nothing is asked and no box or attribute is painted', async () => {
        const { root } = await grid({}, null);
        await typeFormula(root, 'SU');
        expect(box(root)).toBeNull();
        expect(editor(root).hasAttribute('aria-autocomplete')).toBe(false);
    });

    it('DC-17: the text and the caret reach the Consumer as the user types, and the candidates are painted beneath the editor', async () => {
        const { root, asked } = await grid();
        await typeFormula(root, 'SU');
        expect(asked).toEqual([['=', 1], ['=SU', 3]]);
        expect(labels(root)).toEqual(['SUM', 'SUMIF']);
        expect(chosen(root)).toBe('SUM');
        // Beneath the cell: the header band and the pressed row, at a 24px row.
        expect(box(root)?.getAttribute('style')).toContain('top: 72px');
        const list = q(root, '.ex-completion-list');
        expect([editor(root).getAttribute('aria-autocomplete'), editor(root).getAttribute('aria-controls')]).toEqual(['list', list.id]);
        expect(editor(root).getAttribute('aria-activedescendant')).toBe(q(root, '.ex-completion-selected').id);
        expect(list.getAttribute('role')).toBe('listbox');
    });

    it('ADR-0051: ↑ and ↓ choose within the list, and Tab accepts the chosen candidate, closes the list and shows the hint alone', async () => {
        const { root, asked, edits } = await grid();
        await typeFormula(root, 'SU');
        expect(key('ArrowDown').defaultPrevented).toBe(true);
        key('ArrowDown');
        await painted();
        expect(chosen(root)).toBe('SUMIF');
        key('ArrowUp');
        key('ArrowUp');
        await painted();
        expect(chosen(root)).toBe('SUM');
        expect(key('Tab').defaultPrevented).toBe(true);
        await painted();
        expect([editor(root).value, editor(root).selectionStart]).toEqual(['=SUM(', 5]);
        // Asked again for what Tab wrote, and only its hint is shown: the list would come back on the name just written.
        expect(asked.at(-1)).toEqual(['=SUM(', 5]);
        expect(labels(root)).toEqual([]);
        expect(hint(root)).toBe('SUM(<strong>number1</strong>, [number2], ...)');
        expect(editor(root).hasAttribute('aria-controls')).toBe(false);
        expect(edits).toEqual([]);
        // The text typed after Tab is listed again.
        type(editor(root), 'AV');
        await painted();
        expect(labels(root)).toEqual(['AVERAGE']);
    });

    it('DC-17: Escape closes the list and leaves the edit open; with it closed, the next Escape cancels and Overwrite\'s arrows commit again', async () => {
        const { root, edits } = await grid();
        await typeFormula(root, 'SU');
        key('Escape');
        await painted();
        expect(box(root)).toBeNull();
        expect(editor(root).value).toBe('=SU');
        // The Escape's own keyup leaves the caret where it was, and asks nothing again.
        expect(box(root)).toBeNull();
        key('ArrowDown');
        await painted();
        expect(edits).toEqual(['=SU']);
        await typeFormula(root, 'SU');
        key('Escape');
        key('Escape');
        await painted();
        expect(root.querySelector('.ex-editor')).toBeNull();
        expect(edits).toEqual(['=SU']);
    });

    it('ADR-0051: a press on a candidate accepts it, and the editor keeps the keyboard', async () => {
        const { root } = await grid();
        await typeFormula(root, 'SU');
        const item = root.querySelectorAll('.ex-completion-item')[1];
        const down = new MouseEvent('mousedown', { bubbles: true, cancelable: true, button: 0 });
        item?.dispatchEvent(down);
        await painted();
        expect(down.defaultPrevented).toBe(true);
        expect(editor(root).value).toBe('=SUMIF(');
        expect(document.activeElement).toBe(editor(root));
        expect(labels(root)).toEqual([]);
    });

    it('DC-18: an answer for text that has since changed is dropped, the list goes the moment the text changes, and one after the edit closed is dropped', async () => {
        const pending = new Map<string, (answered: EditorCompletion | undefined) => void>();
        const { root } = await grid({}, text => new Promise(resolve => { pending.set(text, resolve); }));
        await typeFormula(root, 'S');
        pending.get('=S')?.(answer('=S', 2));
        await painted();
        expect(labels(root)).toEqual(['SUM', 'SUMIF']);
        type(editor(root), 'U');
        await painted();
        expect(box(root)).toBeNull();
        type(editor(root), 'X');
        // The answer for "=SU" lands after the user typed on: it answers text that is gone.
        pending.get('=SU')?.(answer('=SU', 3));
        await painted();
        await painted();
        expect(box(root)).toBeNull();
        key('Escape');
        pending.get('=SUX')?.(answer('=SU', 3));
        await painted();
        await painted();
        expect(box(root)).toBeNull();
    });

    it('DC-17: from the Formula Bar the list stands beneath the bar, and Tab writes both surfaces without committing', async () => {
        const { root, edits } = await grid({ showFormulaBar: true });
        const bar = barText(root);
        bar.focus();
        await painted();
        bar.setSelectionRange(0, bar.value.length);
        type(bar, '=SU');
        await painted();
        expect(labels(root)).toEqual(['SUM', 'SUMIF']);
        expect(box(root)?.getAttribute('style')).toContain(`top: ${q(root, '.ex-formula-bar').style.height}`);
        expect(bar.getAttribute('aria-controls')).toBe(q(root, '.ex-completion-list').id);
        // In Caret ← is the field's, the list open or not: it moves the caret, and the list is asked again there.
        expect(key('ArrowLeft').defaultPrevented).toBe(false);
        key('ArrowDown');
        key('Tab');
        await painted();
        expect([bar.value, editor(root).value]).toEqual(['=SUMIF(', '=SUMIF(']);
        expect(edits).toEqual([]);
    });

    it('ADR-0051: a caret moved in the text asks again at the caret where it stands', async () => {
        const { root, asked } = await grid();
        await typeFormula(root, 'SU+AV');
        expect(labels(root)).toEqual(['AVERAGE']);
        editor(root).setSelectionRange(3, 3);
        editor(root).dispatchEvent(new MouseEvent('mouseup', { bubbles: true }));
        await painted();
        expect(asked.at(-1)).toEqual(['=SU+AV', 3]);
        expect(labels(root)).toEqual(['SUM', 'SUMIF']);
    });

    it('ADR-0040: near the bottom of the box the list opens above the cell, bounded by the room there', async () => {
        const { root } = await grid({ viewportHeight: 120 });
        press(root, 3, 0);
        await painted();
        await typeFormula(root, 'SU');
        const style = box(root)?.getAttribute('style');
        expect(style).toContain('transform: translateY(-100%)');
        expect(style).toContain('top: 96px');
        expect(style).toContain('max-height: 96px');
    });
});

describe('A list over Point (ADR-0058)', () => {
    const pointing = { pointAt, referenceText } as const;

    it('Q51: where a Reference can go, ← points past the list and closes it, while ↑, ↓ and Tab stay the list\'s', async () => {
        const { root } = await grid(pointing);
        await typeFormula(root, 'MATCH(x,');
        expect(labels(root)).toEqual(['0', '1']);
        key('ArrowDown');
        await painted();
        expect(chosen(root)).toBe('1');
        expect(key('ArrowLeft').defaultPrevented).toBe(true);
        await painted();
        // Pointing starts from the edited cell, A2: ← finds no column to its left, and points at A2 itself.
        expect(editor(root).value).toBe('=MATCH(x,A2');
        expect(box(root)).toBeNull();
    });

    it('Q51: in a list of names, where no Reference can go, ← and End are the editor\'s', async () => {
        const { root } = await grid(pointing);
        await typeFormula(root, 'SU');
        expect(key('ArrowLeft').defaultPrevented).toBe(false);
        expect(key('End').defaultPrevented).toBe(false);
    });
});

describe('The answer and the box (ADR-0051, ADR-0040)', () => {
    it('ADR-0051: an answer that is not an EditorCompletion is refused by name', () => {
        expect(checkedCompletion(undefined)).toBeUndefined();
        expect(() => checkedCompletion({ candidates: [] })).toThrow(/EditorCompletion/u);
        expect(() => mount<Trade>({ columns, rows: trades(1), completeEditorText: 'yes' as never })).toThrow(/completeEditorText is a function/u);
    });

    it('ADR-0040: a box with no row\'s height of room on either side is not shown', () => {
        const geometry = { inBar: false, barPx: 0, nameBoxWidthPx: 0, bandHeightPx: 24, rowHeightPx: 30, visibleHeightPx: 40, cellLeftPx: 0, cellTopPx: 0 };
        expect(completionPlace(geometry)).toBeUndefined();
        expect(completionPlace({ ...geometry, visibleHeightPx: 84 })).toEqual({ leftPx: 0, topPx: 54, roomPx: 30, above: false });
    });
});

// A Chrome's list: each candidate a <li> carrying the ids it is handed, its click accepting it; mounts and cleanups
// counted.
function slotted(): {
    readonly contexts: GridEditorCompletionSlotContext[];
    readonly cleanups: () => number;
    readonly chrome: NonNullable<GridOptions<Trade>['chrome']>;
} {
    const contexts: GridEditorCompletionSlotContext[] = [];
    let cleanups = 0;
    return {
        contexts,
        cleanups: () => cleanups,
        chrome: {
            editorCompletion: (slot, context) => {
                contexts.push(context);
                const list = document.createElement('ul');
                list.id = context.listId;
                list.className = 'drawn';
                context.candidates.forEach((candidate, index) => {
                    const item = document.createElement('li');
                    item.id = context.optionId(index);
                    item.textContent = candidate.label;
                    item.addEventListener('click', () => { context.accept(index); });
                    list.append(item);
                });
                slot.append(list);
                return () => {
                    cleanups += 1;
                    list.remove();
                };
            },
        },
    };
}

describe('The editorCompletion slot (ADR-0390, ADR-0051)', () => {
    it('ADR-0390: the contents draw the answer in the core\'s box, named by the editor, and are mounted again for another chosen candidate', async () => {
        const drawn = slotted();
        const { root } = await grid({ chrome: drawn.chrome });
        await typeFormula(root, 'SU');
        const first = defined(drawn.contexts.at(-1), 'a mount');
        expect([first.candidates.map(candidate => candidate.label), first.selected, first.hint]).toEqual([['SUM', 'SUMIF'], 0, undefined]);
        expect(root.querySelector('.ex-completion-item')).toBeNull();
        expect(q(root, '.ex-completion .drawn').id).toBe(first.listId);
        expect([editor(root).getAttribute('aria-controls'), editor(root).getAttribute('aria-activedescendant')])
            .toEqual([first.listId, first.optionId(0)]);
        key('ArrowDown');
        await painted();
        expect(drawn.contexts.at(-1)?.selected).toBe(1);
        expect(drawn.cleanups()).toBe(drawn.contexts.length - 1);
        expect(editor(root).getAttribute('aria-activedescendant')).toBe(first.optionId(1));
        // Escape closes the list: the contents go.
        key('Escape');
        await painted();
        expect(box(root)).toBeNull();
        expect(drawn.cleanups()).toBe(drawn.contexts.length);
    });

    it('ADR-0051: the box takes the press on a drawn candidate, keeping the keyboard in the editor, and its click accepts it', async () => {
        const drawn = slotted();
        const { root } = await grid({ chrome: drawn.chrome });
        await typeFormula(root, 'SU');
        const standing = defined(drawn.contexts.at(-1), 'a mount');
        const heard: string[] = [];
        const hear = (event: Event): void => { heard.push(event.type); };
        document.addEventListener('mousedown', hear);
        try {
            const item = defined(root.querySelectorAll('.drawn li')[1], 'SUMIF');
            const down = new MouseEvent('mousedown', { bubbles: true, cancelable: true, button: 0 });
            item.dispatchEvent(down);
            expect([down.defaultPrevented, heard]).toEqual([true, []]);
            item.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, button: 0 }));
            await painted();
        } finally {
            document.removeEventListener('mousedown', hear);
        }
        expect(editor(root).value).toBe('=SUMIF(');
        expect(document.activeElement).toBe(editor(root));
        // The list closed with the accept: an accept from contents taken away reaches nothing.
        expect(drawn.cleanups()).toBe(drawn.contexts.length);
        standing.accept(0);
        await painted();
        expect(editor(root).value).toBe('=SUMIF(');
    });

    it('Spine rule 6: an accept made on an answer another has replaced is refused, before the box is painted again', async () => {
        const drawn = slotted();
        const { root } = await grid({ chrome: drawn.chrome });
        await typeFormula(root, 'S');
        const drawnOn = defined(drawn.contexts.at(-1), 'a mount');
        expect(drawnOn.candidates.map(candidate => candidate.label)).toEqual(['SUM', 'SUMIF']);
        // The next letter answers at once; the contents drawn for the last answer still stand until the pass.
        type(editor(root), 'U');
        drawnOn.accept(1);
        await painted();
        expect(editor(root).value).toBe('=SU');
        expect(drawn.contexts.at(-1)?.candidates.map(candidate => candidate.label)).toEqual(['SUM', 'SUMIF']);
    });

    it('ADR-0390: an answer with only a hint hands no candidate chosen, and the contents go with the grid', async () => {
        const drawn = slotted();
        const { root } = await grid({ chrome: drawn.chrome });
        await typeFormula(root, 'SUM(');
        expect([drawn.contexts.at(-1)?.selected, drawn.contexts.at(-1)?.hint?.text]).toEqual([-1, 'SUM(number1, [number2], ...)']);
        unmountAll();
        expect(drawn.cleanups()).toBe(drawn.contexts.length);
    });
});

// Contents as a Framework Adapter's markup draws them, with no handler and no choice of their own: the candidates and
// the hint marked for wireEditorCompletion, which the adapter calls, here with whether they show the answer.
function marked(current: () => boolean): NonNullable<GridOptions<Trade>['chrome']> {
    return {
        editorCompletion: (slot, context) => {
            const contents = document.createElement('div');
            const list = document.createElement('ul');
            list.setAttribute('data-ex-completion-list', '');
            context.candidates.forEach((candidate, index) => {
                const item = document.createElement('li');
                item.setAttribute('data-ex-candidate', String(index));
                item.textContent = candidate.label;
                list.append(item);
            });
            contents.append(list);
            slot.append(contents);
            const unwire = wireEditorCompletion(contents, context, current);
            return () => {
                unwire();
                contents.remove();
            };
        },
    };
}

describe('wireEditorCompletion: the completion slot\'s marked contents (ADR-0410 section 2, ADR-0051)', () => {
    it('ADR-0410: the list and each candidate take the ids the editor names and the chosen one is marked, and a click accepts a candidate', async () => {
        const { root } = await grid({ chrome: marked(() => true) });
        await typeFormula(root, 'SU');
        const list = q(root, '.ex-completion ul');
        const items = [...list.querySelectorAll('li')];
        expect([list.getAttribute('role'), editor(root).getAttribute('aria-controls')]).toEqual(['listbox', list.id]);
        expect(items.map(item => [item.getAttribute('role'), item.getAttribute('aria-selected')])).toEqual([['option', 'true'], ['option', 'false']]);
        expect(editor(root).getAttribute('aria-activedescendant')).toBe(items[0]?.id);
        key('ArrowDown');
        await painted();
        const moved = [...root.querySelectorAll('.ex-completion li')];
        expect(moved.map(item => item.getAttribute('aria-selected'))).toEqual(['false', 'true']);
        expect(editor(root).getAttribute('aria-activedescendant')).toBe(moved[1]?.id);
        defined(moved[1], 'SUMIF').dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, button: 0 }));
        await painted();
        expect([editor(root).value, document.activeElement]).toEqual(['=SUMIF(', editor(root)]);
    });

    it('Spine rule 6: a click on contents still showing an earlier answer accepts nothing', async () => {
        const { root } = await grid({ chrome: marked(() => false) });
        await typeFormula(root, 'SU');
        defined(root.querySelectorAll('.ex-completion li')[1], 'SUMIF').dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, button: 0 }));
        await painted();
        expect(editor(root).value).toBe('=SU');
    });

    it('ADR-0410: a candidate marked with no index of the answer\'s is refused by name', () => {
        const contents = document.createElement('div');
        contents.innerHTML = '<ul data-ex-completion-list><li data-ex-candidate="2">SUM</li></ul>';
        const context: GridEditorCompletionSlotContext = {
            candidates: [{ label: 'SUM', start: 1, length: 2, insert: 'SUM(' }], selected: 0, hint: undefined, listId: 'l',
            optionId: index => `o${String(index)}`, accept: () => undefined,
        };
        expect(() => wireEditorCompletion(contents, context, () => true)).toThrow('data-ex-candidate="2"');
    });
});
