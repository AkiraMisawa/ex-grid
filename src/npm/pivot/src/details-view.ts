// Show Details' sheets as the pivot shows them when the Consumer does not take the records (ADR-0059): a tab at the
// report's foot for each, where Excel's sheet tabs are, whose records cover the report while it is selected; or one
// modal dialog over the pivot. The markup and classes are the Razor component's PivotDetailsTabsView,
// PivotDialogFrame, PivotDetailsDialogView and PivotDetailsGrid (ADR-0350 section 4). Each sheet's records are an
// ExGrid of their own, in an Input Root of their own (ADR-0240 section 6).

import type { GridChrome, GridDensity, GridHandle, GridOptions } from '@ex-grid/core';
import type { PivotDetailRecord } from '@ex-grid/pivot-engine';
import {
    StandingSlot, type PivotChrome, type PivotDetailsDialogSlotContext, type PivotDetailsDialogView, type PivotDetailsTabsSlotContext, type PivotDetailsTabsView,
} from './chrome';
import { detailCellType, DetailsSheet, type DetailsProblem, type PivotDetailsView, type PivotShowDetailsEvent } from './details';
import { element } from './dom';
import { checkedName, keyboardIsThePivots, swap, type PartElements } from './parts';
import type { PivotScreenWords } from './words';

/** Draws a grid into an element: ExGrid's `createGrid`, or a layer-2 test's own. */
export type MountGrid = <Row>(element: HTMLElement, options: GridOptions<Row>) => GridHandle<Row>;

/** What the records' grids take from the pivot's options. */
export interface DetailsGridLook {
    readonly rowHeight: number | undefined;
    readonly density: GridDensity | undefined;
    readonly commandLabel: ((id: string) => string | undefined) | undefined;
    /** The grid's Chrome a pivot's Chrome dresses its grids in (ADR-0390). */
    readonly chrome: GridChrome<PivotDetailRecord> | undefined;
}

/** What the sheets ask of the pivot. */
export interface DetailsHost {
    readonly document: Document;
    readonly mountGrid: MountGrid;
    readonly words: () => PivotScreenWords;
    readonly look: () => DetailsGridLook;
    /** Why the records cannot be shown, in the pivot's words. */
    readonly problemText: (problem: DetailsProblem) => string;
    /** The sheets changed what the pivot draws around them: which covers the report, whether a dialog stands. */
    readonly changed: () => void;
    /** The report's grid takes the keyboard back (ADR-0070). */
    readonly returnKeyboard: () => void;
    /** The controls and anchors the pivot's parts hand in, the sheets' among them. */
    readonly parts: PartElements;
    /** The pivot's Chrome slots for the sheets, read each time the sheets are drawn. */
    readonly chrome: () => Pick<PivotChrome, 'detailsTabs' | 'detailsDialog'> | undefined;
}

// One sheet's records: its grid in an Input Root of its own, or the sentence that says why there are none.
class Records {
    readonly sheet: DetailsSheet;
    /** What stands in the panel or the dialog. */
    readonly box: HTMLElement;
    readonly #host: DetailsHost;
    readonly #leave: (() => void) | undefined;
    readonly #keyboardHome: () => void;
    /** Whether the box stands where the records are shown: a Chrome's dialog hands in the anchor it stands in once
     * its contents are drawn. */
    standing = true;
    #grid: GridHandle<PivotDetailRecord> | undefined;
    #status: HTMLElement | undefined;

    /** `leave` is the dialog's way out; `keyboardHome` gives the keyboard on when the grid that held it goes. */
    constructor(host: DetailsHost, sheet: DetailsSheet, leave: (() => void) | undefined, keyboardHome: () => void) {
        this.#host = host;
        this.sheet = sheet;
        this.#leave = leave;
        this.#keyboardHome = keyboardHome;
        // The box draws nothing (`display: contents`); the records' grid keeps an order of its own, apart from the
        // report's, which hears the rest of the pivot.
        this.box = element(host.document, 'div');
        this.box.setAttribute('data-ex-input-root', '');
        this.box.setAttribute('style', 'display: contents');
    }

    /** Draws the records, once their box stands in the document. */
    draw(): void {
        if (!this.standing) return;
        const { problem } = this.sheet;
        if (problem !== undefined) {
            // The sentence replaces the grid, and the keyboard the grid held goes to the tab or to Close, so it stays
            // in the sheet.
            const held = this.box.contains(this.#host.document.activeElement);
            this.#grid?.destroy();
            this.#grid = undefined;
            const text = this.#host.problemText(problem);
            if (this.#status === undefined) {
                this.#status = element(this.#host.document, 'div', 'ex-pivot-details-status');
                this.#status.setAttribute('role', 'alert');
                this.box.replaceWith(this.#status);
            }
            if (this.#status.textContent !== text) this.#status.textContent = text;
            if (held) this.#keyboardHome();
            return;
        }
        const { rowHeight, density, commandLabel, chrome } = this.#host.look();
        const options: GridOptions<PivotDetailRecord> = {
            columns: this.sheet.columns,
            source: this.sheet.source,
            // The locale its records' text was written in, so one grid never shows two cultures.
            locale: this.sheet.locale,
            viewportHeight: 'stretch',
            viewportWidth: 'stretch',
            // The records are in the data's order and none is left out: the header click selects, and there is no
            // column menu to sort or filter them by (ADR-0059).
            headerClickSelects: true,
            hideColumnMenu: true,
            cellType: detailCellType,
            ...(commandLabel === undefined ? {} : { commandLabel }),
            ...(chrome === undefined ? {} : { chrome }),
            ...(rowHeight === undefined ? {} : { rowHeight }),
            ...(density === undefined ? {} : { density }),
            // The dialog's way out: the Escape its grid has nothing left to dismiss closes it (ADR-0070). A tab's grid
            // has none; Escape does not close a sheet.
            ...(this.#leave === undefined ? {} : { onLeave: this.#leave }),
        };
        if (this.#grid === undefined) {
            const root = element(this.#host.document, 'div');
            this.box.replaceChildren(root);
            this.#grid = this.#host.mountGrid(root, options);
        } else {
            this.#grid.update(options);
        }
    }

    /** Takes the grid away while the records are not on screen: a tab not selected. Its source stays. */
    hide(): void {
        this.#grid?.destroy();
        this.#grid = undefined;
    }

    dispose(): void {
        this.hide();
        this.sheet.dispose();
    }
}

// A details tab: its tab at the report's foot, and its panel over the report.
interface Tab {
    /** The name its control is handed in by, the built-in button's id. */
    readonly key: string;
    readonly records: Records;
    readonly tab: HTMLElement;
    readonly button: HTMLButtonElement;
    readonly close: HTMLButtonElement;
    readonly panel: HTMLElement;
}

// The dialog's frame over its backdrop; what stands inside it, the built-in markup's or a Chrome's contents; and
// the box its records stand in.
interface Dialog {
    readonly records: Records;
    readonly backdrop: HTMLElement;
    readonly frame: HTMLElement;
    /** The built-in markup's: the title, the records and Close. */
    readonly content: HTMLElement;
    readonly heading: HTMLElement;
    readonly recordsBox: HTMLElement;
    readonly close: HTMLButtonElement;
    /** Whether a Chrome's contents stand in the frame. */
    slotted: boolean;
}

/** The name the dialog's Close is handed in by. */
const dialogClose = 'details-close';
/** The name of the anchor the dialog's records stand in. */
const dialogRecords = 'details-records';

// The name a tab's control is handed in by.
function tabName(key: string): string {
    return `tab:${key}`;
}

// Each pivot's ids are its own on the page (ADR-0018).
let pivots = 0;

/** The pivot's details sheets: the tabs and the dialog. */
export class DetailsSheets {
    readonly #host: DetailsHost;
    readonly #idPrefix: string;
    readonly #builtInTabList: HTMLElement;
    readonly #tabsSlot: StandingSlot<PivotDetailsTabsView, PivotDetailsTabsSlotContext>;
    #tabsSlotted = false;
    // One for every dialog, so contents refused once are not tried again at the next.
    readonly #dialogSlot: StandingSlot<PivotDetailsDialogView, PivotDetailsDialogSlotContext>;
    // The slots' contents that could not be drawn, for the pivot to throw once the sheets have drawn.
    #refused: Error[] = [];
    readonly #reportTab: HTMLElement;
    readonly #reportButton: HTMLButtonElement;
    readonly #tabs: Tab[] = [];
    #selected: Tab | undefined;
    #dialog: Dialog | undefined;
    #sequence = 0;

    constructor(host: DetailsHost) {
        this.#host = host;
        this.#idPrefix = `ex-pivot-${++pivots}`;
        const { document } = host;
        this.#builtInTabList = element(document, 'div', 'ex-pivot-tabs');
        this.#builtInTabList.setAttribute('role', 'tablist');
        this.#tabsSlot = new StandingSlot('detailsTabs', document, Object.freeze({ name: '', tabs: Object.freeze([]) }));
        this.#dialogSlot = new StandingSlot('detailsDialog', document, Object.freeze({ title: '', closeLabel: '' }));
        // The slot draws nothing (`display: contents`): the contents lay themselves out in the dialog's frame.
        this.#dialogSlot.element.setAttribute('style', 'display: contents');
        // The report's own tab, first: selecting it uncovers the report.
        const { tab, button } = this.#tab(`${this.#idPrefix}-report`, () => { this.#select(undefined); });
        this.#reportTab = tab;
        this.#reportButton = button;
    }

    /** The tab list at the report's foot, the built-in markup's or a Chrome's, which the pivot places after the report
     * while a tab stands. */
    get tabList(): HTMLElement {
        return this.#tabsSlotted ? this.#tabsSlot.element : this.#builtInTabList;
    }

    /** The slots' contents that could not be drawn since this was last asked, the built-in markup standing in their
     * place. */
    takeRefused(): readonly Error[] {
        const refused = this.#refused;
        this.#refused = [];
        return refused;
    }

    /** Whether a details tab is selected, its records covering the report. */
    get covering(): boolean {
        return this.#selected !== undefined;
    }

    /** The selected tab's panel, which the pivot places in the report's sheet over the report. */
    get panel(): HTMLElement | undefined {
        return this.#selected?.panel;
    }

    /** Whether a tab stands at the report's foot. */
    get tabsShown(): boolean {
        return this.#tabs.length > 0;
    }

    /** Whether the dialog stands: what it covers takes neither the keyboard nor the pointer (ADR-0059). */
    get dialogShown(): boolean {
        return this.#dialog !== undefined;
    }

    /** The dialog's backdrop and frame, which the pivot places last in its root; none while no dialog stands. */
    get dialogElements(): readonly HTMLElement[] {
        const dialog = this.#dialog;
        return dialog === undefined ? [] : [dialog.backdrop, dialog.frame];
    }

    /** Opens the records behind a cell as a new tab, selected and holding the keyboard, or in the dialog, whose
     * Close holds it. */
    open(details: PivotShowDetailsEvent, view: PivotDetailsView, locale: string): void {
        const id = `${this.#idPrefix}-details-${++this.#sequence}`;
        // `changed` reaches whichever of the tab and the dialog shows the sheet once it is made.
        let shown: Records | undefined;
        const sheet = new DetailsSheet(id, details, locale, () => {
            if (shown !== undefined && this.#showing(shown)) {
                shown.draw();
                this.#host.changed();
            }
        });
        const { parts } = this.#host;
        if (view === 'dialog') {
            this.#closeDialogNow();
            const records = new Records(this.#host, sheet, () => { this.closeDialog(); }, () => { parts.focus(dialogClose); });
            shown = records;
            const dialog = this.#dialogOf(records);
            this.#dialog = dialog;
            // Its contents are drawn once its frame stands, so Close is there to take the keyboard. Until a Chrome's
            // Close is drawn, a round trip later on a circuit, the frame holds it: the report it covers is inert, and a
            // key typed meanwhile is then the pivot's, never the page's (ADR-0240 section 6, ADR-0410 section 5).
            this.#host.changed();
            this.#drawDialog(dialog);
            const root = dialog.frame.parentElement;
            if (root !== null && keyboardIsThePivots(root)) dialog.frame.focus();
            parts.focus(dialogClose);
            return;
        }
        const { tab, button } = this.#tab(id, () => {
            const pressed = standing();
            if (pressed !== undefined) this.#select(pressed);
        });
        const records = new Records(this.#host, sheet, undefined, () => { parts.focus(tabName(id)); });
        shown = records;
        const tabs = this.#tabs;
        // Pressed only while the tab stands: a closed tab leaves the page.
        const standing = (): Tab | undefined => tabs.find(t => t.records === records);
        const close = this.#closeButton(tab, () => {
            const closed = standing();
            if (closed !== undefined) this.#closeTab(closed);
        });
        button.textContent = details.title;
        button.setAttribute('aria-controls', `${id}-panel`);
        const panel = element(this.#host.document, 'div', 'ex-pivot-details-panel');
        panel.setAttribute('role', 'tabpanel');
        panel.id = `${id}-panel`;
        panel.setAttribute('aria-labelledby', id);
        panel.append(records.box);
        const made: Tab = { key: id, records, tab, button, close, panel };
        this.#tabs.push(made);
        this.#selected?.records.hide();
        this.#selected = made;
        this.#drawTabs();
        this.#host.changed();
        records.draw();
        // The records now cover the report, so the keyboard leaves it for the new tab: keys left in a report nobody
        // sees would move and copy what is not on screen.
        parts.focus(tabName(id));
    }

    /** Draws the words again and hands the records' grids the pivot's options again. */
    draw(): void {
        this.#drawTabs();
        this.#selected?.records.draw();
        const dialog = this.#dialog;
        if (dialog !== undefined) this.#drawDialog(dialog);
    }

    /** Closes the dialog: the report's grid takes the keyboard back (ADR-0070), however it closes. */
    closeDialog(): void {
        if (this.#dialog === undefined) return;
        this.#closeDialogNow();
        this.#host.changed();
        this.#host.returnKeyboard();
    }

    /** Closes every sheet: the pivot is gone. */
    destroy(): void {
        for (const tab of this.#tabs) tab.records.dispose();
        this.#tabs.length = 0;
        this.#selected = undefined;
        this.#tabsSlot.hide();
        this.#closeDialogNow();
    }

    // Whether the records are on screen: the dialog's, or the selected tab's.
    #showing(records: Records): boolean {
        return this.#dialog?.records === records || this.#selected?.records === records;
    }

    #tab(id: string, select: () => void): { readonly tab: HTMLElement; readonly button: HTMLButtonElement } {
        const { document } = this.#host;
        const tab = element(document, 'div', 'ex-pivot-tab');
        const button = element(document, 'button', 'ex-pivot-tab-button');
        button.type = 'button';
        button.id = id;
        button.setAttribute('role', 'tab');
        button.setAttribute('data-ex-claims', 'click');
        button.addEventListener('click', select);
        tab.append(button);
        return { tab, button };
    }

    // A details tab's ×, after its button.
    #closeButton(tab: HTMLElement, close: () => void): HTMLButtonElement {
        const closing = element(this.#host.document, 'button', 'ex-pivot-tab-close');
        closing.type = 'button';
        closing.textContent = '×';
        closing.setAttribute('data-ex-claims', 'click');
        closing.addEventListener('click', close);
        tab.append(closing);
        return closing;
    }

    // The tab list: the built-in markup's, or a Chrome's in its place while a details tab stands, drawn from the same
    // view. The built-in markup's buttons are handed in again each time it stands, as a Chrome's are when it draws
    // them.
    #drawTabs(): void {
        const words = this.#host.words();
        const view = this.#tabsSlot.viewOf(Object.freeze({
            name: words.word('sheets'),
            tabs: Object.freeze([
                Object.freeze({ key: this.#reportButton.id, title: words.word('report-tab'), selected: this.#selected === undefined }),
                ...this.#tabs.map(tab => Object.freeze({
                    key: tab.key,
                    title: tab.records.sheet.details.title,
                    selected: tab === this.#selected,
                    panelId: tab.panel.id,
                    closeLabel: words.fill('close-tab', tab.records.sheet.details.title),
                })),
            ]),
        }));
        const mount = this.#host.chrome()?.detailsTabs;
        const leaving = this.tabList;
        const held = leaving.contains(this.#host.document.activeElement);
        const slotted = mount !== undefined && this.#tabs.length > 0 && this.#answered(this.#tabsSlot.show(mount, (shared, input) => {
            const refs = this.#host.parts.refs();
            return {
                ...shared,
                select: input((key: string) => {
                    if (key === this.#reportButton.id) {
                        this.#select(undefined);
                        return;
                    }
                    const pressed = this.#tabs.find(tab => tab.key === key);
                    if (pressed !== undefined) this.#select(pressed);
                }),
                close: input((key: string) => {
                    const closed = this.#tabs.find(tab => tab.key === key);
                    if (closed !== undefined) this.#closeTab(closed);
                }),
                control: (key: string) => refs.control(tabName(checkedKey(key))),
            };
        }, id => words.word(id)));
        if (!slotted) this.#tabsSlot.hide();
        this.#tabsSlotted = slotted;
        if (leaving !== this.tabList) {
            swap(leaving, this.tabList);
            // The tab that held the keyboard went with the markup it stood in: the selected sheet's tab takes it.
            if (held) this.#host.parts.focus(tabName(this.#selected?.key ?? this.#reportButton.id));
        }
        // A Chrome's tab need not take the id the built-in one does, so the panel is named by its words.
        for (const tab of this.#tabs) {
            if (slotted) {
                tab.panel.removeAttribute('aria-labelledby');
                tab.panel.setAttribute('aria-label', tab.records.sheet.details.title);
            } else {
                tab.panel.removeAttribute('aria-label');
                tab.panel.setAttribute('aria-labelledby', tab.key);
            }
        }
        if (slotted) return;
        this.#builtInTabList.setAttribute('aria-label', view.name);
        this.#reportButton.textContent = words.word('report-tab');
        const all: readonly { readonly tab: HTMLElement; readonly button: HTMLButtonElement; readonly selected: boolean }[] = [
            { tab: this.#reportTab, button: this.#reportButton, selected: this.#selected === undefined },
            ...this.#tabs.map(tab => ({ tab: tab.tab, button: tab.button, selected: tab === this.#selected })),
        ];
        for (const { tab, button, selected } of all) {
            tab.className = selected ? 'ex-pivot-tab ex-pivot-tab-selected' : 'ex-pivot-tab';
            button.setAttribute('aria-selected', selected ? 'true' : 'false');
            this.#host.parts.handInControl(tabName(button.id), button);
        }
        for (const tab of this.#tabs) {
            const label = words.fill('close-tab', tab.records.sheet.details.title);
            tab.close.setAttribute('aria-label', label);
            tab.close.title = label;
        }
        const children = all.map(tab => tab.tab);
        const list = this.#builtInTabList;
        if (children.length !== list.children.length || children.some((child, i) => list.children[i] !== child)) {
            list.replaceChildren(...children);
        }
    }

    // Whether a slot's contents stand, as `answer` says; an error it answers is the pivot's to throw.
    #answered(answer: { readonly shown: boolean; readonly error?: Error }): boolean {
        if (answer.error !== undefined) this.#refused.push(answer.error);
        return answer.shown;
    }

    // A tab pressed: the report's (`undefined`), or a details tab that still stands.
    #select(tab: Tab | undefined): void {
        if (tab === this.#selected) return;
        this.#selected?.records.hide();
        this.#selected = tab;
        this.#drawTabs();
        this.#host.changed();
        tab?.records.draw();
    }

    #closeTab(tab: Tab): void {
        const at = this.#tabs.indexOf(tab);
        if (at < 0) return;
        this.#tabs.splice(at, 1);
        tab.records.dispose();
        this.#host.parts.forget(tabName(tab.key), tab.button);
        // Excel's next sheet: the one after the closed tab, or before it, or the report.
        const moved = this.#selected === tab;
        if (moved) this.#selected = this.#tabs[Math.min(at, this.#tabs.length - 1)];
        this.#drawTabs();
        this.#host.changed();
        const selected = this.#selected;
        // The close button that held the keyboard is gone: the selected tab takes it. The report's tab is its grid,
        // which takes the keyboard back (ADR-0070); there may be no tab left to hold it, the last one having closed.
        if (selected === undefined) {
            this.#host.returnKeyboard();
            return;
        }
        if (moved) selected.records.draw();
        this.#host.parts.focus(tabName(selected.key));
    }

    #dialogOf(records: Records): Dialog {
        const { document } = this.#host;
        const words = this.#host.words();
        const { title } = records.sheet.details;
        // Under the dialog, a press anywhere else closes it.
        const backdrop = element(document, 'div', 'ex-pivot-dialog-backdrop');
        backdrop.setAttribute('data-ex-claims', 'click');
        backdrop.setAttribute('aria-hidden', 'true');
        backdrop.addEventListener('click', () => { this.closeDialog(); });
        // A modal dialog named by the cell. It claims nothing, which makes it, holding the keyboard, the pivot's own
        // (ADR-0240 section 6). Escape closes it from anywhere inside it that hears the key: an Escape in the records'
        // grid never reaches it, the grid keeping every one, and the grid's way out closes it instead.
        const frame = element(document, 'div', 'ex-pivot-dialog');
        frame.setAttribute('role', 'dialog');
        frame.setAttribute('aria-modal', 'true');
        frame.setAttribute('aria-label', title);
        frame.tabIndex = -1;
        frame.setAttribute('data-ex-claims', '');
        frame.addEventListener('keydown', event => {
            if (event.key !== 'Escape' || event.defaultPrevented) return;
            event.preventDefault();
            this.closeDialog();
        });
        const content = element(document, 'div', 'ex-pivot-dialog-content');
        const heading = element(document, 'div', 'ex-pivot-dialog-title');
        heading.textContent = title;
        const recordsBox = element(document, 'div', 'ex-pivot-dialog-records');
        recordsBox.append(records.box);
        const actions = element(document, 'div', 'ex-pivot-panel-actions');
        const close = element(document, 'button', 'ex-pivot-close');
        close.type = 'button';
        close.textContent = words.word('close');
        close.setAttribute('data-ex-claims', 'click');
        close.addEventListener('click', () => { this.closeDialog(); });
        actions.append(close);
        content.append(heading, recordsBox, actions);
        return { records, backdrop, frame, content, heading, recordsBox, close, slotted: false };
    }

    // What stands inside the dialog's frame: a Chrome's contents while they are given and can be drawn, else the
    // built-in markup's. The records stand in the anchor the contents hand in, and are drawn once they land there;
    // Close takes the keyboard again where the markup that held it went.
    #drawDialog(dialog: Dialog): void {
        const words = this.#host.words();
        const { parts } = this.#host;
        const slot = this.#dialogSlot;
        const view = slot.viewOf(Object.freeze({ title: dialog.records.sheet.details.title, closeLabel: words.word('close') }));
        const mount = this.#host.chrome()?.detailsDialog;
        const { frame, content, recordsBox, records } = dialog;
        const held = frame.contains(this.#host.document.activeElement);
        // The slot stands in the frame as the contents mount, so plain script hands in Close already in the page.
        const trying = mount !== undefined && !slot.refuses(mount);
        if (trying && slot.element.parentNode !== frame) frame.replaceChildren(slot.element);
        const slotted = trying && this.#answered(slot.show(mount, (shared, input) => {
            const refs = parts.refs();
            return {
                ...shared,
                close: input(() => { this.closeDialog(); }),
                control: name => {
                    checkedName(name, ['close'], 'detailsDialog');
                    return refs.control(dialogClose);
                },
                anchor: name => {
                    checkedName(name, ['records'], 'detailsDialog');
                    return refs.anchor(dialogRecords);
                },
            };
        }, id => words.word(id)));
        const moved = slotted !== dialog.slotted;
        dialog.slotted = slotted;
        if (slotted) {
            if (moved) {
                parts.forget(dialogClose, dialog.close);
                records.standing = false;
                parts.place(dialogRecords, recordsBox, () => {
                    records.standing = true;
                    records.draw();
                });
            } else {
                records.draw();
            }
        } else {
            slot.hide();
            parts.place(dialogRecords, undefined);
            if (content.parentNode !== frame) frame.replaceChildren(content);
            if (recordsBox.parentNode !== content) dialog.heading.after(recordsBox);
            parts.handInControl(dialogClose, dialog.close);
            dialog.close.textContent = view.closeLabel;
            records.standing = true;
            records.draw();
        }
        if (moved && held) parts.focus(dialogClose);
    }

    #closeDialogNow(): void {
        const dialog = this.#dialog;
        if (dialog === undefined) return;
        this.#dialog = undefined;
        this.#dialogSlot.hide();
        this.#host.parts.unplace(dialog.recordsBox);
        // The anchor the records stood in goes with the contents, though their framework lets go of it only later.
        this.#host.parts.dropAnchor(dialogRecords);
        this.#host.parts.forget(dialogClose, dialog.close);
        dialog.records.dispose();
        dialog.backdrop.remove();
        dialog.frame.remove();
    }
}

// A tab's key, as a script may hand it to `control`.
function checkedKey(key: string): string {
    const given: unknown = key;
    if (typeof given !== 'string' || given === '') throw new TypeError('An ExPivot\'s sheet tabs hand in a tab\'s control by the key its view gives it (ADR-0390).');
    return given;
}
