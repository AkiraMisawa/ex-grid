// @vitest-environment happy-dom
// Layer 2 (ADR-0380 section 3's note of 2026-11-05): a held input taken again edits a field through the browser's own
// editing where the field holds the keyboard, so the edit joins the field's undo history as the same input typed does,
// and is written from script only where it does not. happy-dom has no editing commands, so the document is given one
// that records what it was asked and edits as the browser would; what the browser's history then holds is layer 3's
// (differential.spec.mjs, seed 1165771808).
import { afterEach, describe, expect, it } from 'vitest';
import { HeldTextCommit } from '../src/dom/held-text-commit';
import { keyDefault, selectionOf, typeText } from '../src/dom/pending-commit';

type Call = readonly [command: string, text: string];

const fields: HTMLInputElement[] = [];

afterEach(() => {
    for (const field of fields.splice(0)) field.remove();
    Reflect.deleteProperty(document, 'execCommand');
});

/** A text field in the document holding `value`, with the caret at its end, and the keyboard when `focused`. */
function textField(value: string, focused = true): HTMLInputElement {
    const input = document.createElement('input');
    input.value = value;
    document.body.append(input);
    fields.push(input);
    input.setSelectionRange(value.length, value.length);
    if (focused) input.focus();
    return input;
}

/** The input types the field's listeners hear. */
function heard(input: HTMLInputElement): string[] {
    const types: string[] = [];
    input.addEventListener('input', (event) => types.push(event.inputType));
    return types;
}

/** Gives the document the browser's three editing commands the grid uses, answering `answer`, and records each call. */
function editing(answer = true): Call[] {
    const calls: Call[] = [];
    const run = (command: string, _ui: boolean, text = ''): boolean => {
        calls.push([command, text]);
        if (!answer) return false;
        const input = document.activeElement as HTMLInputElement;
        let start = selectionOf(input, 'start');
        let end = selectionOf(input, 'end');
        if (command === 'delete' && start === end) start = Math.max(0, start - 1);
        if (command === 'forwardDelete' && start === end) end = Math.min(input.value.length, end + 1);
        input.setRangeText(command === 'insertText' ? text : '', start, end, 'end');
        const inputType = command === 'insertText' ? 'insertText' : command === 'delete' ? 'deleteContentBackward' : 'deleteContentForward';
        input.dispatchEvent(new InputEvent('input', { bubbles: true, data: command === 'insertText' ? text : null, inputType }));
        return true;
    };
    Object.defineProperty(document, 'execCommand', { configurable: true, value: run });
    return calls;
}

const key = (name: string, init: KeyboardEventInit = {}): KeyboardEvent => new KeyboardEvent('keydown', { key: name, ...init });

describe('ADR-0380 §3 (note of 2026-11-05): a held input edits through the browser where the field holds the keyboard', () => {
    it('types a character through the browser\'s insertText, and its listeners hear one insertText', () => {
        const calls = editing();
        const input = textField('7');
        const types = heard(input);
        expect(keyDefault(input, key('5'), false)).toBe('done');
        expect(input.value).toBe('75');
        expect(calls).toEqual([['insertText', '5']]);
        expect(types).toEqual(['insertText']);
    });

    it('deletes a character through the browser\'s own deletion, which finds its extent, for Backspace and Delete', () => {
        const calls = editing();
        const input = textField('7ab');
        expect(keyDefault(input, key('Backspace'), false)).toBe('done');
        input.setSelectionRange(0, 0);
        expect(keyDefault(input, key('Delete'), false)).toBe('done');
        expect(input.value).toBe('a');
        expect(calls).toEqual([['delete', ''], ['forwardDelete', '']]);
    });

    it('a paste is a step of its own: the typing run is closed around it, and its listeners hear insertFromPaste', () => {
        const calls = editing();
        const input = textField('7ab');
        const types = heard(input);
        const selections: string[] = [];
        const select = input.setSelectionRange.bind(input);
        input.setSelectionRange = (start, end, direction) => {
            selections.push(`${String(start)}-${String(end)}`);
            select(start, end, direction);
        };
        typeText(input, 'XYZ', 'insertFromPaste');
        expect(input.value).toBe('7abXYZ');
        expect(calls).toEqual([['insertText', 'XYZ']]);
        // Elsewhere and back before, and after (the stand-in's own setRangeText selects between them).
        expect(selections.slice(0, 2)).toEqual(['0-0', '3-3']);
        expect(selections.slice(-2)).toEqual(['0-0', '6-6']);
        expect(input.selectionStart).toBe(6);
        expect(types).toEqual(['insertFromPaste']);
    });

    it('a deletion of nothing is no edit', () => {
        const calls = editing();
        const input = textField('7');
        const types = heard(input);
        typeText(input, '', 'deleteByCut');
        expect(calls).toEqual([]);
        expect(types).toEqual([]);
    });

    it('a field that does not hold the keyboard is written from script, as no typing could reach it', () => {
        const calls = editing();
        const input = textField('7', false);
        const types = heard(input);
        typeText(input, '5', 'insertText');
        expect(input.value).toBe('75');
        expect(calls).toEqual([]);
        expect(types).toEqual(['insertText']);
    });

    it('throws where the browser refuses the command, rather than leave the text out', () => {
        editing(false);
        const input = textField('7');
        expect(() => typeText(input, '5', 'insertText')).toThrow(/refused to insertText/);
    });
});

/** The values the field's `change` events carry. */
function changes(input: HTMLInputElement): string[] {
    const seen: string[] = [];
    input.addEventListener('change', () => seen.push(input.value));
    return seen;
}

describe('ADR-0210: the browser commits its own edit, and the grid supplies only the commit it does not', () => {
    it('leaving a field the browser edited supplies no change, which the browser sends itself', () => {
        const input = textField('7');
        const seen = changes(input);
        const commit = new HeldTextCommit();
        input.value = '75';
        commit.typed(input, '7', true);
        input.dispatchEvent(new Event('blur'));
        expect(seen).toEqual([]);
    });

    it('leaving a field written from script supplies its change', () => {
        const input = textField('7');
        const seen = changes(input);
        const commit = new HeldTextCommit();
        input.value = '75';
        commit.typed(input, '7', false);
        input.dispatchEvent(new Event('blur'));
        expect(seen).toEqual(['75']);
    });

    it('a held Enter, which has no default of its own, supplies the change even where the browser edited', () => {
        const input = textField('7');
        const seen = changes(input);
        const commit = new HeldTextCommit();
        input.value = '75';
        commit.typed(input, '7', true);
        commit.handOn(input, key('Enter'), () => input.dispatchEvent(key('Enter')));
        expect(seen).toEqual(['75']);
    });
});
