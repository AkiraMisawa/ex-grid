// Show Details' sheets as the pivot shows them when the Consumer does not take the records (ADR-0059): a tab at the
// report's foot for each, where Excel's sheet tabs are, whose records cover the report while it is selected; or one
// modal dialog over the pivot. The markup and classes are the Razor component's PivotDetailsTabsView,
// PivotDialogFrame, PivotDetailsDialogView and PivotDetailsGrid (ADR-0350 section 4). Each sheet's records are an
// ExGrid of their own, in an Input Root of their own (ADR-0240 section 6).

import type { GridChrome, GridDensity, GridHandle, GridOptions } from '@ex-grid/core';
import type { PivotDetailRecord } from '@ex-grid/pivot-engine';
import { detailCellType, DetailsSheet, type DetailsProblem, type PivotDetailsView, type PivotShowDetailsEvent } from './details';
import { element } from './dom';
import { defined } from './must';
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
}

// One sheet's records: its grid in an Input Root of its own, or the sentence that says why there are none.
class Records {
    readonly sheet: DetailsSheet;
    /** What stands in the panel or the dialog. */
    readonly box: HTMLElement;
    readonly #host: DetailsHost;
    readonly #leave: (() => void) | undefined;
    readonly #keyboardHome: () => HTMLElement;
    #grid: GridHandle<PivotDetailRecord> | undefined;
    #status: HTMLElement | undefined;

    /** `leave` is the dialog's way out; `keyboardHome` takes the keyboard when the grid that held it goes. */
    constructor(host: DetailsHost, sheet: DetailsSheet, leave: (() => void) | undefined, keyboardHome: () => HTMLElement) {
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
            if (held) this.#keyboardHome().focus();
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
    readonly records: Records;
    readonly tab: HTMLElement;
    readonly button: HTMLButtonElement;
    readonly close: HTMLButtonElement;
    readonly panel: HTMLElement;
}

// The dialog's frame over its backdrop, and the button that takes the keyboard when it opens.
interface Dialog {
    readonly records: Records;
    readonly backdrop: HTMLElement;
    readonly frame: HTMLElement;
    readonly close: HTMLButtonElement;
}

// Each pivot's ids are its own on the page (ADR-0018).
let pivots = 0;

/** The pivot's details sheets: the tabs and the dialog. */
export class DetailsSheets {
    /** The tab list at the report's foot, which the pivot places after the report while a tab stands. */
    readonly tabList: HTMLElement;
    readonly #host: DetailsHost;
    readonly #idPrefix: string;
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
        this.tabList = element(document, 'div', 'ex-pivot-tabs');
        this.tabList.setAttribute('role', 'tablist');
        // The report's own tab, first: selecting it uncovers the report.
        const { tab, button } = this.#tab(`${this.#idPrefix}-report`, () => { this.#select(undefined); });
        this.#reportTab = tab;
        this.#reportButton = button;
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
        if (view === 'dialog') {
            this.#closeDialogNow();
            // Drawn only while it stands, so its Close is there to take the keyboard.
            const records = new Records(this.#host, sheet, () => { this.closeDialog(); }, () => defined(this.#dialog, 'the dialog the records stand in').close);
            shown = records;
            const dialog = this.#dialogOf(records);
            this.#dialog = dialog;
            this.#host.changed();
            records.draw();
            dialog.close.focus();
            return;
        }
        const { tab, button } = this.#tab(id, () => {
            const pressed = standing();
            if (pressed !== undefined) this.#select(pressed);
        });
        const records = new Records(this.#host, sheet, undefined, () => button);
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
        const made: Tab = { records, tab, button, close, panel };
        this.#tabs.push(made);
        this.#selected?.records.hide();
        this.#selected = made;
        this.#drawTabs();
        this.#host.changed();
        records.draw();
        // The records now cover the report, so the keyboard leaves it for the new tab: keys left in a report nobody
        // sees would move and copy what is not on screen.
        button.focus();
    }

    /** Draws the words again and hands the records' grids the pivot's options again. */
    draw(): void {
        this.#drawTabs();
        this.#selected?.records.draw();
        const dialog = this.#dialog;
        if (dialog !== undefined) {
            dialog.close.textContent = this.#host.words().word('close');
            dialog.records.draw();
        }
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

    #drawTabs(): void {
        const words = this.#host.words();
        this.tabList.setAttribute('aria-label', words.word('sheets'));
        this.#reportButton.textContent = words.word('report-tab');
        const all: readonly { readonly tab: HTMLElement; readonly button: HTMLButtonElement; readonly selected: boolean }[] = [
            { tab: this.#reportTab, button: this.#reportButton, selected: this.#selected === undefined },
            ...this.#tabs.map(tab => ({ tab: tab.tab, button: tab.button, selected: tab === this.#selected })),
        ];
        for (const { tab, button, selected } of all) {
            tab.className = selected ? 'ex-pivot-tab ex-pivot-tab-selected' : 'ex-pivot-tab';
            button.setAttribute('aria-selected', selected ? 'true' : 'false');
        }
        for (const tab of this.#tabs) {
            const label = words.fill('close-tab', tab.records.sheet.details.title);
            tab.close.setAttribute('aria-label', label);
            tab.close.title = label;
        }
        const children = all.map(tab => tab.tab);
        if (children.length !== this.tabList.children.length || children.some((child, i) => this.tabList.children[i] !== child)) {
            this.tabList.replaceChildren(...children);
        }
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
        selected.button.focus();
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
        frame.append(content);
        return { records, backdrop, frame, close };
    }

    #closeDialogNow(): void {
        const dialog = this.#dialog;
        if (dialog === undefined) return;
        this.#dialog = undefined;
        dialog.records.dispose();
        dialog.backdrop.remove();
        dialog.frame.remove();
    }
}
