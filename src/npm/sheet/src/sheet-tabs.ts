// The Sheet Tabs (ADR-0331 §1, §4): one tab per Sheet of the Workbook, at the Sheet's foot under the grid, as Excel
// draws them, and the + button after them. A tab list to assistive technology, the selected tab its one tab stop.
// A press on a tab or on + keeps the keyboard where it is, in the grid; the keys on the tab list, where Tab put the
// keyboard, select a tab or give the keyboard back. The elements wear the C# Sheet's classes, so one stylesheet lays
// out both. What a press or a key means is the Sheet's: this draws, and reports what was pressed.

import type { Sheet } from '@ex-grid/sheet-engine';

/** What the tabs tell the Sheet they hear. */
export interface SheetTabsHooks {
    /** A tab was pressed. */
    readonly pressed: (sheet: Sheet) => void;
    /** The + button was pressed. */
    readonly added: () => void;
    /** A key on the tab list: true where it was the Sheet's, which the browser then does not act on. */
    readonly listKey: (event: KeyboardEvent) => boolean;
}

/** What one pass shows. */
export interface SheetTabsPaint {
    readonly sheets: readonly Sheet[];
    readonly shown: Sheet;
    readonly heightPx: number;
    readonly listName: string;
    readonly addName: string;
}

/** The tab area's elements, one button per Sheet, each made for its Sheet and never handed to another. */
export class SheetTabs {
    readonly area: HTMLElement;
    readonly #list: HTMLElement;
    readonly #add: HTMLButtonElement;
    readonly #tabs = new Map<Sheet, HTMLButtonElement>();
    readonly #hooks: SheetTabsHooks;
    readonly #doc: Document;

    constructor(doc: Document, hooks: SheetTabsHooks) {
        this.#doc = doc;
        this.#hooks = hooks;
        this.area = doc.createElement('div');
        this.area.className = 'ex-sheet-tab-area';
        this.#list = doc.createElement('div');
        this.#list.className = 'ex-sheet-tabs';
        this.#list.setAttribute('role', 'tablist');
        this.#list.addEventListener('keydown', event => {
            if (event.isComposing) return;
            if (hooks.listKey(event)) event.preventDefault();
        });
        this.#add = doc.createElement('button');
        this.#add.type = 'button';
        this.#add.className = 'ex-sheet-tab-add';
        this.#add.tabIndex = -1;
        const plus = doc.createElement('span');
        plus.setAttribute('aria-hidden', 'true');
        plus.textContent = '+';
        this.#add.append(plus);
        // A press keeps the keyboard where it is: in the grid, where the next key acts on the Sheet it showed.
        this.#add.addEventListener('mousedown', preventDefault);
        this.#add.addEventListener('click', () => { hooks.added(); });
        // Past the last tab: the strip's empty end.
        const end = doc.createElement('span');
        end.className = 'ex-sheet-tab-end';
        end.setAttribute('aria-hidden', 'true');
        this.area.append(this.#list, this.#add, end);
    }

    paint(paint: SheetTabsPaint): void {
        setAttr(this.area, 'style', `height: ${String(paint.heightPx)}px`);
        setAttr(this.#list, 'aria-label', paint.listName);
        setAttr(this.#add, 'aria-label', paint.addName);
        setAttr(this.#add, 'title', paint.addName);
        for (const [sheet, tab] of this.#tabs) {
            if (paint.sheets.includes(sheet)) continue;
            this.#tabs.delete(sheet);
            tab.remove();
        }
        const tabs = paint.sheets.map(sheet => {
            const tab = this.#tabOf(sheet);
            if (tab.textContent !== sheet.name) tab.textContent = sheet.name;
            const selected = sheet === paint.shown;
            setAttr(tab, 'aria-selected', selected ? 'true' : 'false');
            setAttr(tab, 'tabindex', selected ? '0' : '-1');
            return tab;
        });
        // Moved only when the order differs: a tab moved loses the keyboard it holds.
        const children = [...this.#list.children];
        if (children.length !== tabs.length || tabs.some((tab, i) => children[i] !== tab)) this.#list.replaceChildren(...tabs);
    }

    /** Gives the keyboard to the tab of `sheet`, where it stands: nothing scrolls. */
    focus(sheet: Sheet): void {
        this.#tabs.get(sheet)?.focus({ preventScroll: true });
    }

    /** Whether the keyboard is on the tab list. */
    get holdsKeyboard(): boolean {
        return this.#list.contains(this.#doc.activeElement);
    }

    #tabOf(sheet: Sheet): HTMLButtonElement {
        const known = this.#tabs.get(sheet);
        if (known !== undefined) return known;
        const tab = this.#doc.createElement('button');
        tab.type = 'button';
        tab.className = 'ex-sheet-tab';
        tab.setAttribute('role', 'tab');
        tab.addEventListener('mousedown', preventDefault);
        tab.addEventListener('click', () => { this.#hooks.pressed(sheet); });
        this.#tabs.set(sheet, tab);
        return tab;
    }
}

function preventDefault(event: Event): void {
    event.preventDefault();
}

function setAttr(element: HTMLElement, name: string, value: string): void {
    if (element.getAttribute(name) !== value) element.setAttribute(name, value);
}
