// ExSheet's find panel (ADR-0332 §5), C#'s SheetFindPanel: the grid's built-in panel, drawn by the Sheet through the
// grid's own find panel slot, with Within added — the Sheet shown, the default, or the Workbook. Its DOM and classes
// are the grid's built-in panel's, so ex-grid.css and layer 3 read it as they read the grid's, and the C# panel's for
// Within. The panel decides nothing: a step, a Replace and a Replace All over the Sheet are the grid's, and over the
// Workbook the Sheet's own; what Within is stands with the Sheet, across a close and a reopen, as the grid keeps the
// text.

import { findPanelLabelIds, type GridChromeSlot, type GridFindPanelSlotContext } from '@ex-grid/core';

/** Where the Sheet's find panel searches (ADR-0332 §5): the Sheet shown, or every Sheet of the Workbook. */
export type SheetFindWithin = 'sheet' | 'workbook';

/** The ids of the words the Sheet's find panel adds to the grid's, as C#'s SheetFindLabelIds spells them: a
 * Consumer's `commandLabel` answers them as it answers the grid's. */
export const sheetFindLabelIds = Object.freeze({
    /** The label of the Within choice. */
    within: 'exsheet.find-within',
    /** Within: the Sheet shown. */
    withinSheet: 'exsheet.find-within-sheet',
    /** Within: the Workbook. */
    withinWorkbook: 'exsheet.find-within-workbook',
} as const);

/** What the Sheet does for its find panel. */
export interface SheetFindPanelHost {
    /** Where the panel searches now. */
    readonly within: () => SheetFindWithin;
    /** A choice of Within, made in the panel. */
    readonly chooseWithin: (within: SheetFindWithin) => void;
    /** Replace All pressed: the grid's over the Sheet, the Sheet's own over the Workbook. */
    readonly replaceAll: (context: GridFindPanelSlotContext) => void;
    /** What the outcome line says now: the Sheet's own Replace All's outcome while it stands, else the grid's. */
    readonly outcome: (context: GridFindPanelSlotContext) => string;
    /** Hears each change to what the Sheet's own outcome is; answers the function that stops it. */
    readonly subscribe: (listener: () => void) => () => void;
    /** The panel is mounted for a request for its field: an outcome of the Sheet's own said before it is gone. */
    readonly mounted: () => void;
}

/** The panels drawn so far, so each one's two radios are a group of their own. */
let panels = 0;

/** The find panel slot that draws ExSheet's panel for `host`. */
export function sheetFindPanelSlot(host: SheetFindPanelHost): GridChromeSlot<GridFindPanelSlotContext> {
    return (slot, context) => {
        host.mounted();
        const doc = slot.ownerDocument;
        const label = context.label;
        const body = doc.createElement('div');
        body.className = 'ex-popover-find-body';
        const textField = (type: string, className: string, id: string, value: string, changed: (text: string) => void): HTMLInputElement => {
            const field = doc.createElement('input');
            field.type = type;
            field.className = className;
            field.setAttribute('aria-label', label(id));
            field.value = value;
            field.addEventListener('input', () => { changed(field.value); });
            return field;
        };
        const button = (className: string, id: string, press: () => void): HTMLButtonElement => {
            const element = doc.createElement('button');
            element.type = 'button';
            element.className = className;
            element.setAttribute('data-ex-claims', 'click');
            element.textContent = label(id);
            element.addEventListener('click', press);
            return element;
        };
        // An option's tick is a click claimed on the checkbox alone, taken in its place in the Input Order (ADR-0240):
        // a click on its words reaches the checkbox as a second click, which is the one taken.
        const option = (className: string, id: string, checked: boolean, set: (on: boolean) => void): { readonly label: HTMLLabelElement; readonly box: HTMLInputElement } => {
            const box = doc.createElement('input');
            box.type = 'checkbox';
            box.className = className;
            box.checked = checked;
            box.setAttribute('data-ex-claims', 'click');
            box.addEventListener('change', () => { set(box.checked); });
            const element = doc.createElement('label');
            element.append(box, label(id));
            return { label: element, box };
        };
        // A radio's choice is its click: a label's click, an arrow key in the group and a press all reach the radio as
        // one, claimed on the radio alone and taken in its place in the Input Order (ADR-0240), so a step numbered
        // before it searches where the panel searched then.
        panels += 1;
        const group = `ex-sheet-find-within-${String(panels)}`;
        const radio = (className: string, id: string, within: SheetFindWithin): HTMLLabelElement => {
            const input = doc.createElement('input');
            input.type = 'radio';
            input.name = group;
            input.className = className;
            input.checked = host.within() === within;
            input.setAttribute('data-ex-claims', 'click');
            input.addEventListener('change', () => { if (input.checked) host.chooseWithin(within); });
            const element = doc.createElement('label');
            element.append(input, label(id));
            return element;
        };

        let view = context.view();
        const field = textField('search', 'ex-find-field', findPanelLabelIds.field, view.text, context.setText);
        const matchCase = option('ex-find-match-case', findPanelLabelIds.matchCase, view.matchCase, context.setMatchCase);
        const wholeCell = option('ex-find-whole-cell', findPanelLabelIds.wholeCell, view.wholeCell, context.setWholeCell);
        const within = doc.createElement('fieldset');
        within.className = 'ex-sheet-find-within';
        const legend = doc.createElement('legend');
        legend.textContent = label(sheetFindLabelIds.within);
        within.append(legend,
            radio('ex-sheet-find-within-sheet', sheetFindLabelIds.withinSheet, 'sheet'),
            radio('ex-sheet-find-within-workbook', sheetFindLabelIds.withinWorkbook, 'workbook'));
        const steps = doc.createElement('div');
        steps.className = 'ex-popover-actions';
        steps.append(button('ex-find-previous', findPanelLabelIds.previous, context.previous), button('ex-find-next', findPanelLabelIds.next, context.next));
        const outcome = doc.createElement('div');
        outcome.className = 'ex-find-outcome';
        outcome.setAttribute('role', 'status');
        body.append(field, matchCase.label, wholeCell.label, within, steps, outcome);

        // Replace (ADR-0332 §2): the replacement and the two writes, shown while the panel offers Replace. Enter in its
        // field is still "find next", as in Excel's dialog.
        let replace: { readonly field: HTMLInputElement; readonly actions: HTMLElement; readonly buttons: readonly HTMLButtonElement[] } | undefined;
        const paint = (): void => {
            view = context.view();
            if (matchCase.box.checked !== view.matchCase) matchCase.box.checked = view.matchCase;
            if (wholeCell.box.checked !== view.wholeCell) wholeCell.box.checked = view.wholeCell;
            if (view.replaceShown && replace === undefined) {
                const replaceField = textField('text', 'ex-find-replace-field', findPanelLabelIds.replaceField, view.replaceText, context.setReplaceText);
                const actions = doc.createElement('div');
                actions.className = 'ex-popover-actions';
                const buttons = [
                    button('ex-find-replace', findPanelLabelIds.replace, context.replace),
                    button('ex-find-replace-all', findPanelLabelIds.replaceAll, () => { host.replaceAll(context); }),
                ];
                actions.append(...buttons);
                outcome.before(replaceField, actions);
                replace = { field: replaceField, actions, buttons };
            } else if (!view.replaceShown && replace !== undefined) {
                replace.field.remove();
                replace.actions.remove();
                replace = undefined;
            }
            for (const element of replace?.buttons ?? []) element.disabled = view.replaceWaiting; // oxlint-disable-line ex-grid/no-stand-in-default -- a panel without Replace has no button to wait
            const said = host.outcome(context);
            if (outcome.textContent !== said) outcome.textContent = said;
        };
        paint();
        slot.append(body);
        const stopView = context.subscribe(paint);
        const stopOwn = host.subscribe(paint);
        // Each mount answers a request for the field: it takes the keyboard, and the core selects its text where the
        // request asks for that (FD-1).
        context.takeFocus(field, { preventScroll: true });
        return () => {
            stopView();
            stopOwn();
            body.remove();
        };
    };
}
