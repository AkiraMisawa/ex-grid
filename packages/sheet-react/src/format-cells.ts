// What a Chrome's Format Cells needs to stay thin (ADR-0390 section 3, ADR-0071): the tab shown, each choice set
// through the Sheet's draft and drawn again, the refusal an OK met, and the keyboard taken by the tab shown at the
// opening. What a choice means and what OK sets are the Sheet's; this holds only what the contents show.

import { useCallback, useLayoutEffect, useReducer, useRef, useState, type FormEvent } from 'react';
import { flushSync } from 'react-dom';
import { formatCellsOffer, type FormatCellsDraft, type FormatCellsTab, type SheetFormatCellsSlotContext } from '@ex-grid/sheet';

/** What {@link useFormatCells} hands Format Cells' contents. */
export interface FormatCellsHandles {
    /** The tab shown: the one Format Cells opened on, until another is shown. */
    readonly tab: FormatCellsTab;
    /** Shows another tab, and tells the Sheet, so the next opening shows it again. */
    readonly showTab: (tab: FormatCellsTab) => void;
    /** The ref of the control that stands for the tab shown: it takes the keyboard as Format Cells opens. */
    readonly tabControl: (control: HTMLElement | null) => void;
    /** Makes a choice through the draft, and draws the contents again from it. A refusal OK showed goes once its
     * cause has. */
    readonly set: (choose: (draft: FormatCellsDraft) => void) => void;
    /** Why OK set nothing, in the Sheet's words, while that still stands; `undefined` otherwise. */
    readonly refusal: string | undefined;
    /** OK: sets what was touched, and closes Format Cells; one refused leaves it open and `refusal` saying why. */
    readonly ok: () => void;
    /** Cancel: sets nothing, and closes Format Cells. */
    readonly cancel: () => void;
    /** OK as the submit of a `noValidate` form holding every tab, each tab's fields inside an element whose
     * `data-ex-tab` names it: a field the browser holds invalid, such as a number of places out of range, is shown on
     * its tab and reported there, and nothing is set, whichever tab OK was pressed from; otherwise `ok`. */
    readonly submit: (event: FormEvent<HTMLFormElement>) => void;
}

/**
 * Draws Format Cells from its slot's context (ADR-0071, ADR-0390): the tab shown, what each choice changed, and the
 * refusal an OK met. Draw the tabs from `formatCellsOffer.tabs`, put `tabControl` on the control of the tab shown,
 * read every control's state from `context.draft`, and make every choice through `set`.
 */
export function useFormatCells(context: SheetFormatCellsSlotContext): FormatCellsHandles {
    const [tab, setTab] = useState<FormatCellsTab>(context.tab);
    const [, drawn] = useReducer((count: number) => count + 1, 0);
    const [refused, setRefused] = useState(false);
    const control = useRef<HTMLElement | null>(null);
    const opened = useRef<SheetFormatCellsSlotContext | undefined>(undefined);
    // The opening's keyboard goes to the tab shown, once per opening, as soon as it is drawn.
    useLayoutEffect(() => {
        if (opened.current === context) return;
        opened.current = context;
        if (control.current === null) throw new Error('Format Cells drew no control for the tab shown: put useFormatCells\' tabControl on it.');
        context.takeKeyboard(control.current);
    }, [context]);
    const showTab = useCallback((shown: FormatCellsTab) => {
        setTab(shown);
        context.tabShown(shown);
    }, [context]);
    const set = useCallback((choose: (draft: FormatCellsDraft) => void) => {
        choose(context.draft);
        if (context.draft.refusal === undefined) setRefused(false);
        drawn();
    }, [context]);
    const ok = useCallback(() => {
        if (!context.ok()) setRefused(true);
    }, [context]);
    const submit = useCallback((event: FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        const invalid = [...event.currentTarget.querySelectorAll('input, select, textarea')]
            .find((field): field is HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement =>
                (field instanceof HTMLInputElement || field instanceof HTMLSelectElement || field instanceof HTMLTextAreaElement) && !field.checkValidity());
        if (invalid === undefined) {
            ok();
            return;
        }
        const holding = formatCellsOffer.tabs.find(each => each === invalid.closest<HTMLElement>('[data-ex-tab]')?.dataset['exTab']);
        if (holding === undefined) throw new Error('A field of Format Cells the browser holds invalid stands in no element marked data-ex-tab with its tab.');
        flushSync(() => { showTab(holding); });
        invalid.reportValidity();
    }, [ok, showTab]);
    const tabControl = useCallback((element: HTMLElement | null) => { control.current = element; }, []);
    const standing = context.draft.refusal;
    return {
        tab, showTab, tabControl, set, ok, submit, cancel: context.cancel,
        refusal: refused && standing !== undefined ? context.words.formatCellsRefusal(standing) : undefined,
    };
}
