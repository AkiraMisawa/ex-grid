// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): a Pending Commit (ADR-0380). A verdict or an onEdit may answer with a promise; while
// it is out the editor stands read-only, every input made meanwhile waits and is taken after the answer, and Escape
// cancels. Each answer here is held by the test and given when the test says, so "typed at once" and "typed after
// each answer" are the two orders spine rule 6 says must end the same.
import { afterEach, describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import { elementDefault, selectionOf } from '../src/dom/pending-commit';
import type { EditVerdict, GridEditIntent } from '../src/cells/edit-intent';
import type { GridPasteIntent } from '../src/clipboard/paste-intent';
import type { GridColumn } from '../src/dom/grid-column';
import type { GridOptions } from '../src/dom/grid-view';
import type { GridRefusal } from '../src/dom/grid-refusal';
import type { GridSelection } from '../src/selection/selection';
import { mount, painted, q, trades, unmountAll, type Told, type Trade } from './grid-harness';

afterEach(unmountAll);

// Rows of 24 px under a header of 24; Notional's left edge.
const rowY = (row: number): number => (row * 24) + 5;
const notional = 130;

/** An answer the test gives when it chooses, with what it was asked about. */
interface Asked<T> {
    readonly text: string;
    readonly signal: AbortSignal;
    readonly give: (answer: T) => void;
}

/** A Notional column whose verdict answers later, through `asked`. */
function laterColumns(asked: Asked<EditVerdict>[]): readonly GridColumn<Trade>[] {
    return [
        { name: 'Book', type: 'text', value: trade => trade.book, width: 120, editable: true },
        {
            name: 'Notional', type: 'number', value: trade => trade.notional, width: 100, editable: true,
            validate: (_row, text, signal) => new Promise<EditVerdict>(resolve => { asked.push({ text, signal, give: resolve }); }),
        },
    ];
}

// Under 1000 the domain says no such trade can exist; anything else is a trade.
const domain = (text: string): EditVerdict => (Number(text) < 1000 ? { kind: 'reject', message: `a notional of ${text} cannot be booked` } : { kind: 'accept' });

/** Lets an answer given, and what it lets through, run. */
const settled = async (): Promise<void> => {
    for (let i = 0; i < 4; i++) await painted();
};

/** Gives every answer asked, by `rule`, one at a time, until none is left: each held input taken after one may ask again. */
async function answerAll(asked: Asked<EditVerdict>[], rule: (text: string) => EditVerdict): Promise<void> {
    await settled();
    for (let next = asked.shift(); next !== undefined; next = asked.shift()) {
        next.give(rule(next.text));
        await settled();
    }
}

interface Heard {
    readonly edits: GridEditIntent<Trade>[];
    readonly refusals: GridRefusal[];
    readonly selections: GridSelection[];
}

function grid(options: Partial<GridOptions<Trade>>, asked: Asked<EditVerdict>[] = [], parent: HTMLElement = document.body): {
    readonly root: HTMLElement; readonly heard: Heard; readonly told: Told;
} {
    const heard: Heard = { edits: [], refusals: [], selections: [] };
    const { root, told } = mount({
        columns: laterColumns(asked), rows: trades(20), rowHeight: 24, viewportHeight: 480,
        onEdit: intent => { heard.edits.push(intent); },
        onRefused: refusal => { heard.refusals.push(refusal); },
        onSelectionChanged: selection => { heard.selections.push(selection); },
        ...options,
    }, {}, parent);
    return { root, heard, told };
}

/** A press on the rows, as the browser lands it: the scroller takes DOM focus by default, unless prevented. */
function press(root: HTMLElement, x: number, y: number): void {
    const viewport = q(root, '.ex-viewport');
    const mouse = (name: string): MouseEvent => {
        const event = new MouseEvent(name, { bubbles: true, cancelable: true, button: 0, buttons: name === 'mouseup' ? 0 : 1 });
        Object.defineProperty(event, 'offsetX', { value: x });
        Object.defineProperty(event, 'offsetY', { value: y });
        viewport.dispatchEvent(event);
        return event;
    };
    if (!mouse('mousedown').defaultPrevented) q(root, '.ex-scroller').focus();
    mouse('mouseup');
}

/** A key as the browser types it where the keyboard is: a character the page left alone goes into a field that takes
 * text, at its caret. */
function key(name: string, init: KeyboardEventInit = {}): KeyboardEvent {
    const target = holder();
    const event = new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init });
    target.dispatchEvent(event);
    if (!event.defaultPrevented && name.length === 1 && target instanceof HTMLInputElement && !target.readOnly) {
        target.setRangeText(name, selectionOf(target, 'start'), selectionOf(target, 'end'), 'end');
        target.dispatchEvent(new InputEvent('input', { bubbles: true, data: name, inputType: 'insertText' }));
    }
    return event;
}

function keys(text: string): void {
    for (const character of text) key(character);
}

/** What holds the keyboard. */
function holder(): Element {
    const active = document.activeElement;
    if (active === null) throw new Error('Nothing holds the keyboard.');
    return active;
}

function editor(root: HTMLElement): HTMLInputElement | undefined {
    const found = root.querySelector<HTMLInputElement>('input.ex-editor');
    return found === null ? undefined : found;
}

/** The paste event the browser's paste command fires, carrying text alone. */
function paste(target: Element, text: string): void {
    const event = new ClipboardEvent('paste', { bubbles: true, cancelable: true });
    Object.defineProperty(event, 'clipboardData', { value: { getData: (type: string) => (type === 'text/plain' ? text : '') } });
    target.dispatchEvent(event);
}
const focusOf = (heard: Heard): unknown => heard.selections.at(-1)?.focus;
const written = (heard: Heard): readonly (readonly [string, string])[] => heard.edits.map(intent => [intent.row.book, intent.value]);

/** What the grid shows once it has settled: the edits, the editor and its text, the Focus. */
function outcome(root: HTMLElement, heard: Heard): unknown {
    const open = editor(root);
    return { written: written(heard), editor: open?.value, held: open?.hasAttribute('aria-invalid'), focus: focusOf(heard) };
}

describe('a verdict answered later', () => {
    it('ED-32 / ADR-0380: a Reject that comes later holds the editor with the text and raises no Edit Intent', async () => {
        const asked: Asked<EditVerdict>[] = [];
        const { root, heard } = grid({}, asked);
        await painted();
        press(root, notional, rowY(0));
        keys('150');
        key('Enter');
        await painted();
        expect(heard.edits).toEqual([]);
        expect(defined(editor(root), 'the editor').value).toBe('150');

        await answerAll(asked, domain);
        expect(heard.edits).toEqual([]);
        const field = defined(editor(root), 'the editor held');
        expect([field.value, field.readOnly, field.getAttribute('aria-invalid')]).toEqual(['150', false, 'true']);
        expect(q(root, '.ex-announce').textContent).toBe('a notional of 150 cannot be booked');
        expect(document.activeElement).toBe(field);
    });

    it('ED-32 / ADR-0380: an Accept that comes later raises exactly one Edit Intent, and Enter then moves down', async () => {
        const asked: Asked<EditVerdict>[] = [];
        const { root, heard } = grid({}, asked);
        await painted();
        press(root, notional, rowY(0));
        keys('1500');
        key('Enter');
        await answerAll(asked, domain);
        expect(written(heard)).toEqual([['Book-0', '1500']]);
        expect(editor(root)).toBe(undefined);
        expect(focusOf(heard)).toEqual({ row: 1, column: 1 });
    });

    it('ED-33 / ADR-0380: while it is out the editor is read-only and busy, the root carries the stamp, and nothing is applied', async () => {
        const asked: Asked<EditVerdict>[] = [];
        const { root, heard, told } = grid({}, asked);
        await painted();
        press(root, notional, rowY(0));
        keys('1500');
        key('Enter');
        await painted();
        told.frame();
        const field = defined(editor(root), 'the editor standing');
        expect([field.readOnly, field.getAttribute('aria-busy'), document.activeElement === field]).toEqual([true, 'true', true]);
        expect(root.getAttribute('data-ex-pending-commit')).toMatch(/^\d+$/);
        expect(root.hasAttribute('data-ex-settled')).toBe(false);
        expect(heard.edits).toEqual([]);

        await answerAll(asked, domain);
        // The move Enter made owes its frames and its settle, as it does after an answer given at once.
        told.frame();
        told.clock.advance(5000);
        told.frame();
        await settled();
        expect(root.hasAttribute('data-ex-pending-commit')).toBe(false);
        expect(root.hasAttribute('data-ex-settled')).toBe(true);
    });

    it('ED-33 / spine rule 6: 150 Enter 200 Enter typed at once ends as typed after each answer, under a Reject and an Accept', async () => {
        const slowly: Asked<EditVerdict>[] = [];
        const slow = grid({}, slowly);
        await painted();
        press(slow.root, notional, rowY(0));
        keys('150');
        key('Enter');
        await answerAll(slowly, domain);
        keys('200');
        key('Enter');
        await answerAll(slowly, domain);
        keys('3000');
        key('Enter');
        await answerAll(slowly, domain);
        unmountAll();

        const atOnce: Asked<EditVerdict>[] = [];
        const fast = grid({}, atOnce);
        await painted();
        press(fast.root, notional, rowY(0));
        keys('150');
        key('Enter');
        keys('200');
        key('Enter');
        keys('3000');
        key('Enter');
        await answerAll(atOnce, domain);

        // 150 is refused, the editor keeps it and takes 200 at its caret; 150200 is a trade, and Enter moves down;
        // 3000 is typed on the next row and is a trade too.
        expect(outcome(fast.root, fast.heard)).toEqual(outcome(slow.root, slow.heard));
        expect(written(fast.heard)).toEqual([['Book-0', '150200'], ['Book-1', '3000']]);
        expect(focusOf(fast.heard)).toEqual({ row: 2, column: 1 });
    });

    it('ED-33 / ED-15: a press made while pending is taken after the answer: it selects under an Accept, and nothing under a Reject', async () => {
        const asked: Asked<EditVerdict>[] = [];
        const accepted = grid({}, asked);
        await painted();
        press(accepted.root, notional, rowY(0));
        keys('1500');
        key('Enter');
        press(accepted.root, notional, rowY(5));
        await painted();
        expect(focusOf(accepted.heard)).toEqual({ row: 0, column: 1 });
        await answerAll(asked, domain);
        expect(written(accepted.heard)).toEqual([['Book-0', '1500']]);
        expect(focusOf(accepted.heard)).toEqual({ row: 5, column: 1 });
        unmountAll();

        const rejected = grid({}, asked);
        await painted();
        press(rejected.root, notional, rowY(0));
        keys('150');
        key('Enter');
        press(rejected.root, notional, rowY(5));
        await answerAll(asked, domain);
        expect(written(rejected.heard)).toEqual([]);
        expect(defined(editor(rejected.root), 'the editor held').value).toBe('150');
        expect(focusOf(rejected.heard)).toEqual({ row: 0, column: 1 });
    });

    it('ED-33 / ADR-0380: a paste key typed while pending keeps its default, and its paste waits and lands where the keys led', async () => {
        const asked: Asked<EditVerdict>[] = [];
        const pastes: GridPasteIntent[] = [];
        const { root } = grid({ onPaste: intent => { pastes.push(intent); } }, asked);
        await painted();
        press(root, notional, rowY(0));
        keys('1500');
        key('Enter');
        // The browser fires the paste event as the key's default; the key itself is held, not prevented.
        expect(key('v', { ctrlKey: true }).defaultPrevented).toBe(false);
        paste(holder(), '2500');
        await settled();
        expect(pastes).toEqual([]);
        await answerAll(asked, domain);
        expect(pastes.map(intent => [intent.plan.targets[0]?.topRow, intent.values])).toEqual([[1, [['2500']]]]);
    });

    it('ED-33 / ADR-0380: Shift+Home typed while pending selects the text, as it would after the answer', async () => {
        const asked: Asked<EditVerdict>[] = [];
        const { root } = grid({}, asked);
        await painted();
        press(root, notional, rowY(0));
        keys('150');
        key('Enter');
        key('F2');
        key('Home', { shiftKey: true });
        keys('2000');
        await answerAll(asked, domain);
        // Refused, the editor keeps 150; F2 puts it in Caret, Shift+Home selects it all, and 2000 replaces it.
        expect(defined(editor(root), 'the editor held').value).toBe('2000');
    });
});

describe('a fill whose verdict answers later', () => {
    it('ADR-0380 / ADR-0034: Ctrl+Enter waits for the verdict, a Reject holds the editor, and an Accept raises one fill', async () => {
        const asked: Asked<EditVerdict>[] = [];
        const fills: GridPasteIntent[] = [];
        const { root } = grid({ onPaste: intent => { fills.push(intent); } }, asked);
        await painted();
        press(root, notional, rowY(0));
        keys('150');
        key('Enter', { ctrlKey: true });
        await answerAll(asked, domain);
        expect([fills.length, defined(editor(root), 'the editor held').value]).toEqual([0, '150']);

        keys('0');
        key('Enter', { ctrlKey: true });
        await answerAll(asked, domain);
        expect(fills.map(fill => fill.values)).toEqual([[['1500']]]);
        expect(editor(root)).toBe(undefined);
    });
});

describe('an onEdit answered later', () => {
    it('ED-32 / ADR-0380: refuse called before its promise settles holds the editor, as an immediate refusal does', async () => {
        const answers: (() => void)[] = [];
        const { root, heard } = grid({
            onEdit: intent => new Promise<void>(resolve => {
                answers.push(() => {
                    intent.refuse('the desk is closed for the day');
                    resolve();
                });
            }),
        });
        await painted();
        // Book's column has no verdict: the Edit Intent is out at once, and only its answer comes later.
        press(root, 10, rowY(0));
        keys('Desk');
        key('Enter');
        key('x');
        await settled();
        expect(defined(editor(root), 'the editor standing').readOnly).toBe(true);
        defined(answers.shift(), 'the answer asked')();
        await settled();
        const field = defined(editor(root), 'the editor held');
        // The x typed while it was out is taken in the still-open editor, as it would have been after an immediate refusal.
        expect([field.value, field.readOnly, field.getAttribute('aria-invalid')]).toEqual(['Deskx', false, 'true']);
        expect(q(root, '.ex-announce').textContent).toBe('the desk is closed for the day');
        expect(heard.edits).toEqual([]);
    });

    it('ADR-0380: a refusal after its promise settled throws, as one after a handler returned does', async () => {
        let late: GridEditIntent<Trade> | undefined;
        let answer: (() => void) | undefined;
        const { root } = grid({
            onEdit: intent => new Promise<void>(resolve => {
                late = intent;
                answer = resolve;
            }),
        });
        await painted();
        press(root, 10, rowY(0));
        keys('Desk');
        key('Enter');
        await settled();
        defined(answer, 'the answer asked')();
        await settled();
        expect(() => { defined(late, 'the intent').refuse('too late'); }).toThrow(/settled/);
        expect(editor(root)).toBe(undefined);
    });
});

describe('Escape on a Pending Commit', () => {
    it('ED-34 / ADR-0380: Escape cancels: the signal is aborted, the editor stays with the text, its own turn closes nothing, and it is said once', async () => {
        const asked: Asked<EditVerdict>[] = [];
        const { root, heard } = grid({}, asked);
        await painted();
        press(root, notional, rowY(0));
        keys('1500');
        key('Enter');
        await painted();
        const out = defined(asked[0], 'the verdict asked');
        expect(key('Escape').defaultPrevented).toBe(true);
        await settled();
        expect(out.signal.aborted).toBe(true);
        const field = defined(editor(root), 'the editor kept');
        expect([field.value, field.readOnly, field.hasAttribute('aria-busy')]).toEqual(['1500', false, false]);
        expect(root.hasAttribute('data-ex-pending-commit')).toBe(false);
        expect(q(root, '.ex-announce').textContent).toBe('Nothing was changed: the edit was cancelled while it was being checked.');
        expect(heard.refusals).toEqual([{ kind: 'commit', reason: 'cancelled', cell: { row: 0, column: 1 }, column: 'Notional' }]);

        // The answer that comes after the cancel is not taken.
        out.give({ kind: 'accept' });
        await settled();
        expect(heard.edits).toEqual([]);
        expect(defined(editor(root), 'the editor kept').value).toBe('1500');
        // The editor is the user's again: the next Escape leaves it.
        key('Escape');
        await painted();
        expect(editor(root)).toBe(undefined);
    });

    it('ED-34 / ADR-0012: the repeats of the Escape that cancelled close nothing', async () => {
        const asked: Asked<EditVerdict>[] = [];
        const { root } = grid({}, asked);
        await painted();
        press(root, notional, rowY(0));
        keys('1500');
        key('Enter');
        key('Escape');
        key('Escape', { repeat: true });
        key('Escape', { repeat: true });
        await settled();
        expect(defined(editor(root), 'the editor kept').value).toBe('1500');
    });

    it('ED-34 / ADR-0380: keys held before the Escape are taken in the editor it leaves open', async () => {
        const asked: Asked<EditVerdict>[] = [];
        const { root } = grid({}, asked);
        await painted();
        press(root, notional, rowY(0));
        keys('1500');
        key('Enter');
        keys('7');
        key('Escape');
        await settled();
        expect(defined(editor(root), 'the editor kept').value).toBe('15007');
    });

    it('ED-34 / ADR-0380: text put in with no key while held, a drop or an automation\'s fill, is put in again at its turn', async () => {
        const asked: Asked<EditVerdict>[] = [];
        const { root } = grid({}, asked);
        await painted();
        press(root, notional, rowY(0));
        keys('1500');
        key('Enter');
        const field = defined(editor(root), 'the editor pending');
        const put = new InputEvent('beforeinput', { bubbles: true, cancelable: true, data: '7', inputType: 'insertText' });
        field.dispatchEvent(put);
        expect(put.defaultPrevented).toBe(true);
        key('Escape');
        await settled();
        expect(defined(editor(root), 'the editor kept').value).toBe('15007');
    });

    it('ED-34 / ADR-0380: an Escape that sees no stamp is an ordinary key, and cancels the edit', async () => {
        const asked: Asked<EditVerdict>[] = [];
        const { root, heard } = grid({}, asked);
        await painted();
        press(root, notional, rowY(0));
        keys('1500');
        key('Escape');
        await settled();
        expect([editor(root), asked.length, heard.refusals]).toEqual([undefined, 0, []]);
    });
});

describe('an onPaste answered later', () => {
    it('ADR-0380 section 2: a paste refused later leaves the Selection, and a key typed meanwhile waits for it', async () => {
        const pastes: { readonly intent: GridPasteIntent; readonly give: () => void }[] = [];
        const { root, heard } = grid({
            onPaste: intent => new Promise<void>(resolve => { pastes.push({ intent, give: resolve }); }),
        });
        await painted();
        press(root, notional, rowY(0));
        paste(holder(), '7');
        key('ArrowDown');
        await settled();
        expect(root.hasAttribute('data-ex-pending-commit')).toBe(true);
        expect(focusOf(heard)).toEqual({ row: 0, column: 1 });
        const out = defined(pastes[0], 'the paste heard');
        out.intent.refuse();
        out.give();
        await settled();
        expect(focusOf(heard)).toEqual({ row: 1, column: 1 });
    });

    it('ED-34 / ADR-0380: Escape cancels a paste whose answer is out, aloud, and aborts its signal', async () => {
        const pastes: GridPasteIntent[] = [];
        const { root, heard } = grid({
            onPaste: intent => new Promise<void>(() => { pastes.push(intent); }),
        });
        await painted();
        press(root, notional, rowY(0));
        paste(holder(), '7');
        key('Escape');
        await settled();
        const intent = defined(pastes[0], 'the paste heard');
        expect(intent.signal.aborted).toBe(true);
        expect(() => { intent.refuse(); }).toThrow(/cancelled/);
        expect(heard.refusals).toEqual([{ kind: 'paste', reason: 'cancelled' }]);
        expect(q(root, '.ex-announce').textContent).toBe('Nothing was pasted: the paste was cancelled.');
    });
});

describe('a held key done again on a control', () => {
    it('ADR-0055 / ED-22: a Shift+Enter held for a field submits its form, which reads the Shift, as the browser\'s Enter does', () => {
        const form = document.createElement('form');
        const field = document.createElement('input');
        form.append(field);
        document.body.append(form);
        const heard: boolean[] = [];
        let shift = false;
        field.addEventListener('keydown', event => { shift = event.shiftKey; });
        form.addEventListener('submit', event => {
            event.preventDefault();
            heard.push(shift);
        });
        const enter = new KeyboardEvent('keydown', { key: 'Enter', shiftKey: true, bubbles: true });
        field.dispatchEvent(enter);
        expect(elementDefault(field, enter)).toBe('done');
        expect(heard).toEqual([true]);
        const box = document.createElement('input');
        box.type = 'checkbox';
        form.append(box);
        expect(elementDefault(box, enter)).toBe('none');
        form.remove();
    });
});

/** A surface of the product's around the grid, holding a text field whose owner commits on `change`, as Blazor's
 * `@bind` and a MudBlazor text field do; the grid stands in it with a commit pending. */
async function pendingBeside(withForm = false, multiline = false): Promise<{
    readonly root: HTMLElement; readonly field: HTMLInputElement | HTMLTextAreaElement; readonly changes: string[];
    readonly asked: Asked<EditVerdict>[]; readonly surface: HTMLElement;
}> {
    const surface = document.createElement('div');
    surface.setAttribute('data-ex-input-root', '');
    const field = document.createElement(multiline ? 'textarea' : 'input');
    if (withForm) {
        const form = document.createElement('form');
        form.addEventListener('submit', event => { event.preventDefault(); });
        form.append(field);
        surface.append(form);
    } else {
        surface.append(field);
    }
    document.body.append(surface);
    const changes: string[] = [];
    field.addEventListener('change', () => { changes.push(field.value); });
    const asked: Asked<EditVerdict>[] = [];
    const { root } = grid({}, asked, surface);
    await painted();
    press(root, notional, rowY(0));
    keys('1500');
    key('Enter');
    // The keyboard moves to the field while the verdict is out, and what is typed there waits for it.
    field.focus();
    return { root, field, changes, asked, surface };
}

describe('held text in a field that commits on change', () => {
    afterEach(() => {
        for (const surface of document.querySelectorAll('[data-ex-input-root]')) surface.remove();
    });

    it('ADR-0210: leaving the field commits the text held typing put there, once', async () => {
        const { field, changes, asked } = await pendingBeside();
        keys('abc');
        expect(field.value).toBe('');
        await answerAll(asked, domain);
        expect([field.value, changes]).toEqual(['abc', []]);
        field.blur();
        field.focus();
        field.blur();
        expect(changes).toEqual(['abc']);
    });

    it('ADR-0210: a change of the field\'s own first is not sent again, and held keys that change nothing commit nothing', async () => {
        const { field, changes, asked } = await pendingBeside();
        keys('ab');
        await answerAll(asked, domain);
        field.dispatchEvent(new Event('change', { bubbles: true }));
        field.blur();
        expect(changes).toEqual(['ab']);

        const again = await pendingBeside();
        key('ArrowLeft');
        await answerAll(again.asked, domain);
        again.field.blur();
        expect(again.changes).toEqual([]);
    });

    it('ADR-0210: an Enter typed once the hold is over commits the held text before the field\'s own handlers hear it', async () => {
        const { field, changes, asked } = await pendingBeside();
        const heard: string[] = [];
        field.addEventListener('keydown', event => {
            if (event instanceof KeyboardEvent) heard.push(`${event.key} after ${String(changes.length)} change`);
        });
        keys('abc');
        await answerAll(asked, domain);
        key('Enter');
        field.blur();
        expect([changes, heard.at(-1)]).toEqual([['abc'], 'Enter after 1 change']);
    });

    it('ADR-0210: where typing since the hold changed the value, the browser commits it and the grid does not', async () => {
        const { field, changes, asked } = await pendingBeside();
        keys('abc');
        await answerAll(asked, domain);
        keys('d');
        key('Enter');
        expect(changes).toEqual([]);
        // The browser's own commit, which a native Enter on text the user typed makes.
        field.dispatchEvent(new Event('change', { bubbles: true }));
        field.blur();
        expect(changes).toEqual(['abcd']);
    });

    it('ADR-0210: a held Enter commits the held text before its form is submitted', async () => {
        const { field, changes, asked } = await pendingBeside(true);
        const submitted: number[] = [];
        const { form } = field;
        if (form === null) throw new Error('The field stands in no form.');
        form.addEventListener('submit', () => { submitted.push(changes.length); });
        keys('abc');
        key('Enter');
        await answerAll(asked, domain);
        expect([changes, submitted.at(-1)]).toEqual([['abc'], 1]);
    });

    it('ADR-0210: an Enter in a textarea is a line, and commits nothing until the field is left', async () => {
        const { field, changes, asked } = await pendingBeside(false, true);
        keys('abc');
        key('Enter');
        await answerAll(asked, domain);
        key('Enter');
        expect(changes).toEqual([]);
        field.blur();
        expect(changes).toEqual([field.value]);
    });

    it('ADR-0210: text put in with no key commits on the field it was put in, wherever the keyboard was', async () => {
        const { field, asked, surface } = await pendingBeside();
        const other = document.createElement('input');
        surface.prepend(other);
        const changes: string[] = [];
        other.addEventListener('change', () => { changes.push(other.value); });
        other.dispatchEvent(new InputEvent('beforeinput', { bubbles: true, cancelable: true, data: 'z', inputType: 'insertText' }));
        await answerAll(asked, domain);
        expect([other.value, field.value, changes]).toEqual(['z', '', []]);
        other.focus();
        other.blur();
        expect(changes).toEqual(['z']);
    });

    it('ADR-0210: a value the field\'s owner wrote since is the owner\'s, and is not sent back as a change', async () => {
        const { field, changes, asked } = await pendingBeside();
        keys('abc');
        await answerAll(asked, domain);
        field.value = 'reset';
        field.blur();
        expect(changes).toEqual([]);
    });

    it('ADR-0210: a grid that goes forgets the field, and leaving it then commits nothing', async () => {
        const { field, changes, asked } = await pendingBeside();
        keys('abc');
        await answerAll(asked, domain);
        unmountAll();
        field.blur();
        expect(changes).toEqual([]);
    });
});
