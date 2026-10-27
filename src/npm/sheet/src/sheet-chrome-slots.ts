// The Sheet's own Chrome slots beside the Toolbar Item's (ADR-0390, ADR-0071, ADR-0331 §4): Format Cells' contents and
// the faces of the Sheet Tabs, the C# `ISheetChrome.FormatCells`, `SheetTab` and `SheetTabAdd`. What each shows and
// what a choice means stay the Sheet's: Format Cells' offer is `formatCellsOffer`, every choice is set through the
// draft, and OK is the Sheet's; a tab, its keyboard and its press are the Sheet's, and the face only draws.

import type { FormatCellsDraft } from './format-cells-draft';
import type { FormatCellsTab } from './format-cells-offer';
import type { SheetWords } from './sheet-words';

/**
 * What Format Cells' contents are handed (ADR-0071, ADR-0390), once per opening. The contents stand in the grid's
 * popover frame, as the built-in Format Cells does: where it stands, Escape, a press elsewhere, Tab wrapping inside
 * it and the keyboard back on the grid as it closes are the grid's (ADR-0050 item 16), so the frame is never the
 * Chrome's own dialog under TypeScript. The contents draw the tabs and their controls from `formatCellsOffer` and
 * `draft`, set every choice through `draft`, and call `ok` or `cancel`.
 */
export interface SheetFormatCellsSlotContext {
    /** What Format Cells opens on, the Focus cell's Cell Format with the parts that differ across the Selection shown
     * as Excel shows them, and what the user has touched. Every control is set through it, so what a choice means is
     * the Sheet's under every Chrome. */
    readonly draft: FormatCellsDraft;
    /** The tab to open on: the one shown last in this Sheet. */
    readonly tab: FormatCellsTab;
    /** Hears each tab the contents show, so the next opening shows it again, as Excel's does. */
    readonly tabShown: (tab: FormatCellsTab) => void;
    /** Sets the parts the user touched on the Selection Format Cells was opened over, as one undo step, and closes it:
     * `true`. `false` while the draft holds a refusal, which the contents show in the Sheet's words
     * (`words.formatCellsRefusal(draft.refusal)`), and Format Cells stays open. */
    readonly ok: () => boolean;
    /** Sets nothing, and closes Format Cells. */
    readonly cancel: () => void;
    /** The Sheet's words, its Japanese or its English and the Consumer's label over either: the tabs' names, the
     * fields' labels, OK and Cancel, a refusal. */
    readonly words: SheetWords;
    /** Gives the keyboard, which the frame holds while the contents are being drawn, to a control they drew: the tab
     * shown, as the opening asks. Granted only while the keyboard is still the grid's, so a press the user made
     * meanwhile keeps it where it put it. */
    readonly takeKeyboard: (control: HTMLElement) => void;
    /** The contents report a popup of their own opening (`true`) and closing (`false`): while one is open, Escape is
     * the popup's, not the frame's (ADR-0039). */
    readonly innerPopupChanged: (open: boolean) => void;
}

/**
 * Format Cells' slot (ADR-0390): mounts the contents into `slot`, which stands in the grid's popover frame, and
 * answers the function that takes them away, called however Format Cells closes. Asked once per opening.
 */
export type SheetFormatCellsSlot = (slot: HTMLElement, context: SheetFormatCellsSlotContext) => () => void;

/** What a Sheet Tab's face shows (ADR-0331 §4): the C# `SheetTabContext`. */
export interface SheetTabSlotContext {
    /** The Sheet's name, which the face shows as its only text. */
    readonly name: string;
    /** Whether the Sheet is the one shown. */
    readonly selected: boolean;
}

/**
 * A Sheet Tab's face (ADR-0331 §4, ADR-0390): mounts what stands inside the tab the Sheet keeps, which takes the
 * keyboard, is a tab to assistive technology and hears the press. The face takes no keyboard and hears nothing. It is
 * mounted again, on the same slot, whenever the name or `selected` changes, and answers the function that takes it
 * away.
 */
export type SheetTabSlot = (slot: HTMLElement, context: SheetTabSlotContext) => () => void;

/** The face of the button that adds a Sheet (ADR-0331 §4, ADR-0390), inside the button the Sheet keeps, which is
 * named and hears the press; the face takes no keyboard and hears nothing. Mounted once. */
export type SheetTabAddSlot = (slot: HTMLElement) => () => void;

/** Calls `mount` and answers the cleanup it returned, refused by name where it is not a function. */
export function mountedSlot(name: string, mount: () => unknown): () => void {
    const cleanup = mount();
    if (typeof cleanup !== 'function') {
        throw new TypeError(`chrome.${name} answered ${typeof cleanup}: a mount function answers the function that takes its contents away (ADR-0390).`);
    }
    return () => { Reflect.apply(cleanup, undefined, []); };
}
