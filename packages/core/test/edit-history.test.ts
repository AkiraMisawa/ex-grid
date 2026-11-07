// Layer 1 (ADR-0380 section 3's note of 2026-11-07, ADR-0007's last bullet): an open edit's undo history is the grid's,
// so where Ctrl+Z goes back to is decided by the edit's own inputs, never by what else the page repainted meanwhile.
import { describe, expect, it } from 'vitest';
import { EditHistory, type EditState } from '../src/dom/edit-history';

const at = (text: string, start = text.length, end = start): EditState => ({ text, start, end });

/** Types `text` one character at a time at the caret of `history`'s state `from`, and answers the state it ends in. */
function type(history: EditHistory, from: EditState, text: string): EditState {
    let now = from;
    for (const character of text) {
        const value = now.text.slice(0, now.start) + character + now.text.slice(now.end);
        now = at(value, now.start + character.length);
        history.typed('insertText', now);
    }
    return now;
}

function backspace(history: EditHistory, from: EditState, times: number): EditState {
    let now = from;
    for (let i = 0; i < times; i++) {
        now = at(now.text.slice(0, now.start - 1) + now.text.slice(now.start), now.start - 1);
        history.typed('deleteContentBackward', now);
    }
    return now;
}

describe('ADR-0380 §3 (note of 2026-11-07): an open edit keeps its own undo history', () => {
    it('ADR-0380: characters typed one after another are undone together, however they were typed', () => {
        // The Hunt of 2026-11-07, seed 1165771808: an edit opened on "7", then ".5" and "Bravo 2" typed.
        const history = new EditHistory(at('7'));
        const typed = type(history, at('7'), '.5');
        type(history, typed, 'Bravo 2');
        expect(history.undo()).toEqual(at('7'));
        expect(history.canUndo).toBe(false);
    });

    it('ADR-0380: the text the edit opened on is not undone', () => {
        const history = new EditHistory(at('7'));
        expect(history.undo()).toBeUndefined();
    });

    it('ADR-0380: a run of deletions is undone together, apart from the typing before it', () => {
        const history = new EditHistory(at(''));
        const typed = type(history, at(''), 'abcd');
        backspace(history, typed, 2);
        expect(history.undo()).toEqual(at('abcd'));
        expect(history.undo()).toEqual(at(''));
    });

    it('ADR-0380: a caret moved between two inputs starts a step of its own', () => {
        const history = new EditHistory(at(''));
        const typed = type(history, at(''), 'abc');
        history.moved(at('abc', 1));
        type(history, at('abc', 1), 'X');
        expect(history.undo()).toEqual(at('abc', 1));
        expect(history.undo()).toEqual(at('', 0));
        expect(typed.text).toBe('abc');
    });

    it('ADR-0380: a caret moved with no input said is read from where the next character went', () => {
        const history = new EditHistory(at(''));
        type(history, at(''), 'abc');
        // A press put the caret after "a", and nothing told the history so: "X" went in there.
        history.typed('insertText', at('aXbc', 2));
        expect(history.undo()).toEqual(at('abc', 1));
    });

    it('ADR-0380: a character typed beside its twin is read where it was typed', () => {
        const history = new EditHistory(at(''));
        type(history, at(''), 'aa');
        history.moved(at('aa', 1));
        // An "a" typed between the two: the text alone could say it went at the end.
        history.typed('insertText', at('aaa', 2));
        expect(history.undo()).toEqual(at('aa', 1));
    });

    it('ADR-0380: a paste is a step of its own between two runs of typing', () => {
        const history = new EditHistory(at(''));
        const typed = type(history, at(''), 'ab');
        const pasted = at(`${typed.text}PASTE`);
        history.typed('insertFromPaste', pasted);
        type(history, pasted, 'cd');
        expect(history.undo()).toEqual(at('abPASTE'));
        expect(history.undo()).toEqual(at('ab'));
        expect(history.undo()).toEqual(at(''));
    });

    it('ADR-0380: text the grid writes itself, a Reference pointed at, is a step of its own', () => {
        const history = new EditHistory(at(''));
        const typed = type(history, at(''), '=SUM(');
        history.written(at(`${typed.text}A1`));
        expect(history.undo()).toEqual(at('=SUM('));
        expect(history.undo()).toEqual(at(''));
    });

    it('ADR-0380: redo does an undone step again, and typing after an undo leaves nothing to redo', () => {
        const history = new EditHistory(at(''));
        type(history, at(''), 'ab');
        expect(history.undo()).toEqual(at(''));
        expect(history.redo()).toEqual(at('ab'));
        expect(history.undo()).toEqual(at(''));
        type(history, at(''), 'c');
        expect(history.canRedo).toBe(false);
        expect(history.redo()).toBeUndefined();
    });

    it('ADR-0380: typing after an undo starts a run of its own', () => {
        const history = new EditHistory(at(''));
        const typed = type(history, at(''), 'ab');
        backspace(history, typed, 1);
        expect(history.undo()).toEqual(at('ab'));
        type(history, at('ab'), 'c');
        expect(history.undo()).toEqual(at('ab'));
    });

    it('ADR-0380: typed over a selection, the text it replaced comes back selected', () => {
        const history = new EditHistory(at('hello', 0, 5));
        history.typed('insertText', at('X'));
        expect(history.undo()).toEqual(at('hello', 0, 5));
    });

    it('ADR-0380: a composition is one step, apart from the typing before it', () => {
        const history = new EditHistory(at(''));
        const typed = type(history, at(''), 'a');
        history.typed('insertCompositionText', at('aか'));
        history.typed('insertCompositionText', at('aかな'));
        history.typed('insertCompositionText', at('a仮名'));
        expect(typed.text).toBe('a');
        expect(history.undo()).toEqual(at('a'));
        expect(history.undo()).toEqual(at(''));
    });

    it('ADR-0380: the history refuses to hear its own undo as typing', () => {
        const history = new EditHistory(at(''));
        expect(() => history.typed('historyUndo', at('x'))).toThrow(/its own historyUndo/);
    });
});
