// The Sheet Tabs (ADR-0331 §1, §4): one tab per Sheet of the Workbook, at the Sheet's foot under the grid, as Excel
// draws them, and the + button after them. A tab list to assistive technology, the selected tab its one tab stop.
// A press on a tab or on + keeps the keyboard where it is, in the grid; the keys on the tab list, where Tab put the
// keyboard, select a tab or give the keyboard back. The elements wear the C# Sheet's classes, so one stylesheet lays
// out both. What a press or a key means is the Sheet's: this draws, and reports what was pressed.

import type { Sheet } from '@ex-grid/sheet-engine';
import { mountedSlot, type SheetTabAddSlot, type SheetTabSlot } from './sheet-chrome-slots';
import { preventDefault, setAttr } from './dom-attributes';

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
    /** A Chrome's face for each tab, or none for the built-in one, the name (ADR-0390). */
    readonly tabFace: SheetTabSlot | undefined;
    /** A Chrome's face for the + button, or none for the built-in one. */
    readonly addFace: SheetTabAddSlot | undefined;
}

// A face a Chrome drew inside a tab or the + button: its slot, the mount it was drawn by, what it was drawn for, and
// what takes it away.
interface DrawnFace {
    readonly slot: HTMLElement;
    readonly mount: unknown;
    readonly shown: string;
    readonly cleanup: () => void;
}

/** The tab area's elements, one button per Sheet, each made for its Sheet and never handed to another. */
export class SheetTabs {
    readonly area: HTMLElement;
    readonly #list: HTMLElement;
    readonly #add: HTMLButtonElement;
    readonly #tabs = new Map<Sheet, HTMLButtonElement>();
    readonly #hooks: SheetTabsHooks;
    readonly #doc: Document;
    readonly #faces = new Map<HTMLButtonElement, DrawnFace>();
    // The + button's built-in face, the glyph.
    readonly #plus: HTMLElement;

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
        this.#plus = doc.createElement('span');
        this.#plus.setAttribute('aria-hidden', 'true');
        this.#plus.textContent = '+';
        this.#add.append(this.#plus);
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
        this.#drawAddFace(paint.addFace);
        for (const [sheet, tab] of this.#tabs) {
            if (paint.sheets.includes(sheet)) continue;
            this.#tabs.delete(sheet);
            this.#takeFace(tab);
            tab.remove();
        }
        const tabs = paint.sheets.map(sheet => {
            const tab = this.#tabOf(sheet);
            const selected = sheet === paint.shown;
            this.#drawTabFace(tab, paint.tabFace, sheet.name, selected);
            setAttr(tab, 'aria-selected', selected ? 'true' : 'false');
            setAttr(tab, 'tabindex', selected ? '0' : '-1');
            return tab;
        });
        // Moved only when the order differs: a tab moved loses the keyboard it holds.
        const children = [...this.#list.children];
        if (children.length !== tabs.length || tabs.some((tab, i) => children[i] !== tab)) this.#list.replaceChildren(...tabs);
    }

    /** Takes away every face a Chrome drew, as the band or the Sheet goes. */
    dispose(): void {
        for (const button of this.#faces.keys()) this.#takeFace(button);
    }

    // A tab's face: the Chrome's, mounted again on the same slot when what it shows changes, or the name.
    #drawTabFace(tab: HTMLButtonElement, mount: SheetTabSlot | undefined, name: string, selected: boolean): void {
        if (mount === undefined) {
            this.#takeFace(tab);
            tab.removeAttribute('aria-label');
            if (tab.textContent !== name) tab.textContent = name;
            return;
        }
        // The face is the Chrome's; the tab's name for a screen reader stays the Sheet's, whatever the face shows.
        setAttr(tab, 'aria-label', name);
        const shown = JSON.stringify([name, selected]);
        this.#drawFace(tab, mount, shown, slot => mount(slot, Object.freeze({ name, selected })), 'sheetTab');
    }

    // The + button's face: the Chrome's, mounted once for each mount function, or the glyph.
    #drawAddFace(mount: SheetTabAddSlot | undefined): void {
        if (mount === undefined) {
            this.#takeFace(this.#add);
            if (this.#plus.parentElement !== this.#add) this.#add.replaceChildren(this.#plus);
            return;
        }
        this.#drawFace(this.#add, mount, '', slot => mount(slot), 'sheetTabAdd');
    }

    #drawFace(button: HTMLButtonElement, mount: unknown, shown: string, draw: (slot: HTMLElement) => unknown, name: string): void {
        const standing = this.#faces.get(button);
        if (standing !== undefined && standing.mount === mount && standing.shown === shown) return;
        let slot = standing?.slot;
        if (standing !== undefined) {
            this.#faces.delete(button);
            standing.cleanup();
        }
        if (slot === undefined) {
            slot = this.#doc.createElement('span');
            slot.className = 'ex-sheet-tab-face';
            button.replaceChildren(slot);
        }
        const at = slot;
        this.#faces.set(button, { slot: at, mount, shown, cleanup: mountedSlot(name, () => draw(at)) });
    }

    // Takes a Chrome's face away from a tab or the + button, where one stands.
    #takeFace(button: HTMLButtonElement): void {
        const standing = this.#faces.get(button);
        if (standing === undefined) return;
        this.#faces.delete(button);
        standing.cleanup();
        standing.slot.remove();
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
