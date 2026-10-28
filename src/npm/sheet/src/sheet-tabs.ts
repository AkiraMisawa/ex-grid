// The Sheet Tabs (ADR-0331 §1, §4): one tab per Sheet of the Workbook, at the Sheet's foot under the grid, as Excel
// draws them, and the + button after them. A tab list to assistive technology, the selected tab its one tab stop.
// A press on a tab or on + keeps the keyboard where it is, in the grid; the keys on the tab list, where Tab put the
// keyboard, select a tab or give the keyboard back. A double click edits a tab's name in place, a secondary press
// opens the tab's menu, and a drag from one tab onto another moves it (exsheet-workbook ticket 06). The elements wear
// the C# Sheet's classes, so one stylesheet lays out both. What a press or a key means is the Sheet's: this draws,
// and reports what was pressed.

import type { Sheet } from '@ex-grid/sheet-engine';
import { mountedSlot, type SheetTabAddSlot, type SheetTabSlot } from './sheet-chrome-slots';
import { preventDefault, setAttr } from './dom-attributes';
import type { SheetTabCommand } from './sheet-words';

/** What the tabs tell the Sheet they hear. */
export interface SheetTabsHooks {
    /** A tab was pressed. */
    readonly pressed: (sheet: Sheet) => void;
    /** The + button was pressed. */
    readonly added: () => void;
    /** A key on the tab list: true where it was the Sheet's, which the browser then does not act on. */
    readonly listKey: (event: KeyboardEvent) => boolean;
    /** A double click on a tab: its name is to be edited. */
    readonly renameAsked: (sheet: Sheet) => void;
    /** The text in the name's field changed. */
    readonly renameInput: (text: string) => void;
    /** Enter or Escape in the name's field, outside a composition. */
    readonly renameKey: (key: 'Enter' | 'Escape') => void;
    /** The keyboard left the name's field opened as `session`. */
    readonly renameLeft: (session: number) => void;
    /** A secondary press on a tab, or the browser's context menu for it: its menu is to open. */
    readonly menuAsked: (sheet: Sheet) => void;
    /** A key on the tab's menu: true where it was the menu's. */
    readonly menuKey: (event: KeyboardEvent) => boolean;
    /** A command of the tab's menu pressed. */
    readonly menuCommand: (command: SheetTabCommand) => void;
    /** The keyboard left the tab's menu opened as `session`. */
    readonly menuLeft: (session: number) => void;
    /** A tab dragged onto another tab, or past the last (`undefined`). */
    readonly dropped: (sheet: Sheet, target: Sheet | undefined) => void;
}

/** One command of a tab's menu, as it is drawn. */
export interface SheetTabMenuItem {
    readonly command: SheetTabCommand;
    readonly label: string;
    /** Why it cannot be done on the Sheet now, or `undefined` where it can. */
    readonly disabledReason: string | undefined;
}

/** The tab's menu, as it is drawn: its name, its commands, and the one the keyboard is on. */
export interface SheetTabMenuPaint {
    readonly name: string;
    readonly items: readonly SheetTabMenuItem[];
    readonly active: SheetTabCommand;
}

// The name being edited on its tab: the element standing in the tab's place, its field, and the edit's session.
interface Renaming {
    readonly sheet: Sheet;
    readonly element: HTMLElement;
    readonly field: HTMLInputElement;
    readonly session: number;
}

// The tab's menu standing: its element and its session.
interface TabMenu {
    readonly element: HTMLElement;
    readonly session: number;
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

/** The tab areas made so far, so each menu's ids are its own. */
let tabAreas = 0;

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
    #renaming: Renaming | undefined;
    #menu: TabMenu | undefined;
    // Each name edit and each menu is numbered, so the keyboard leaving one already ended is not taken for the next.
    #session = 0;
    // The tab a drag started on, from the press until its release or the pointer leaving the tabs.
    #dragFrom: Sheet | undefined;
    // The ids the menu's commands are named by, unique to these tabs.
    readonly #menuIds = `ex-sheet-tab-menu-${String(++tabAreas)}`;

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
        // Its press takes its place in the Sheet's order, behind a commit or an opening (ADR-0240).
        this.#add.setAttribute('data-ex-claims', 'click');
        this.#add.addEventListener('mousedown', preventDefault);
        this.#add.addEventListener('click', () => { hooks.added(); });
        // Past the last tab: the strip's empty end.
        const end = doc.createElement('span');
        end.className = 'ex-sheet-tab-end';
        end.setAttribute('aria-hidden', 'true');
        // A release on + or past the last tab drops a tab dragged there, last; a release anywhere else in the area,
        // or the pointer leaving it, ends the drag with nothing moved.
        for (const past of [this.#add, end]) past.addEventListener('mouseup', event => { this.#drop(event, undefined); });
        this.area.addEventListener('mouseup', () => { this.#endDrag(); });
        this.area.addEventListener('mouseleave', () => { this.#endDrag(); });
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
            const renaming = this.#renaming?.sheet === sheet ? this.#renaming.element : undefined;
            // The tab keeps its place in the Tab order while its name is edited, as the field's frame.
            for (const element of renaming === undefined ? [tab] : [tab, renaming]) {
                setAttr(element, 'aria-selected', selected ? 'true' : 'false');
                setAttr(element, 'tabindex', selected ? '0' : '-1');
            }
            return renaming ?? tab;
        });
        // Moved only when the order differs: a tab moved loses the keyboard it holds.
        const children = [...this.#list.children];
        if (children.length !== tabs.length || tabs.some((tab, i) => children[i] !== tab)) this.#list.replaceChildren(...tabs);
    }

    /** Takes away every face a Chrome drew, as the band or the Sheet goes. */
    dispose(): void {
        for (const button of this.#faces.keys()) this.#takeFace(button);
    }

    // ---- The name edited on its tab --------------------------------------------------------------------------

    /** The Sheet whose name is being edited on its tab, if one is. */
    get renaming(): Sheet | undefined {
        return this.#renaming?.sheet;
    }

    /** The name's field takes the tab's place, holding `name` whole selected, and the keyboard: what is typed next
     * replaces it. Answers the edit's session. */
    beginRename(sheet: Sheet, name: string, fieldName: string): number {
        this.endRename();
        const tab = this.#tabOf(sheet);
        const element = this.#doc.createElement('span');
        element.className = 'ex-sheet-tab ex-sheet-tab-renaming';
        element.setAttribute('role', 'tab');
        // Selected as its tab is: the next paint keeps the two alike.
        const selected = tab.getAttribute('aria-selected') === 'true';
        element.setAttribute('aria-selected', selected ? 'true' : 'false');
        element.tabIndex = selected ? 0 : -1;
        const box = this.#doc.createElement('span');
        box.className = 'ex-sheet-tab-name-box';
        const field = this.#doc.createElement('input');
        field.type = 'text';
        field.className = 'ex-sheet-tab-name';
        field.setAttribute('aria-label', fieldName);
        field.spellcheck = false;
        field.autocomplete = 'off';
        field.setAttribute('data-ex-claims', 'keydown');
        field.value = name;
        const session = ++this.#session;
        field.addEventListener('input', () => { this.#hooks.renameInput(field.value); });
        field.addEventListener('keydown', event => {
            // An IME's keys are its own while it composes: its Enter confirms a candidate and its Escape cancels the
            // composition, and neither ends the name's edit (ADR-0010, ADR-0080).
            if (event.isComposing || event.key === 'Process' || (event.key !== 'Enter' && event.key !== 'Escape')) return;
            event.preventDefault();
            event.stopPropagation();
            this.#hooks.renameKey(event.key);
        });
        field.addEventListener('focusout', () => { this.#hooks.renameLeft(session); });
        box.append(field);
        element.append(box);
        this.#renaming = { sheet, element, field, session };
        if (tab.parentElement === this.#list) tab.replaceWith(element);
        field.focus({ preventScroll: true });
        field.select();
        return session;
    }

    /** Marks the name's field refused, or takes the mark away. */
    markRenameRefused(refused: boolean): void {
        const field = this.#renaming?.field;
        if (field === undefined) return;
        if (refused) field.setAttribute('aria-invalid', 'true');
        else field.removeAttribute('aria-invalid');
    }

    /** The tab is drawn again in the field's place. The keyboard, where the field held it, is the caller's to have
     * handed on first: a field taken away holding it leaves it on the page's body. */
    endRename(): void {
        const renaming = this.#renaming;
        if (renaming === undefined) return;
        this.#renaming = undefined;
        this.#session++;
        const tab = this.#tabs.get(renaming.sheet);
        if (tab !== undefined && renaming.element.parentElement === this.#list) renaming.element.replaceWith(tab);
        else renaming.element.remove();
    }

    // ---- The tab's menu --------------------------------------------------------------------------------------

    /** Whether the tab's menu stands. */
    get menuOpen(): boolean {
        return this.#menu !== undefined;
    }

    /** Opens the tab's menu, or draws it again as it stands, the keyboard on it. Answers its session. */
    showMenu(paint: SheetTabMenuPaint): number {
        let menu = this.#menu;
        if (menu === undefined) {
            const element = this.#doc.createElement('div');
            element.className = 'ex-sheet-tab-menu';
            element.setAttribute('role', 'menu');
            element.tabIndex = -1;
            element.setAttribute('data-ex-claims', 'keydown click');
            const session = ++this.#session;
            element.addEventListener('keydown', event => {
                if (event.isComposing) return;
                if (this.#hooks.menuKey(event)) event.preventDefault();
            });
            element.addEventListener('mousedown', preventDefault);
            element.addEventListener('contextmenu', preventDefault);
            element.addEventListener('focusout', () => { this.#hooks.menuLeft(session); });
            menu = { element, session };
            this.#menu = menu;
            this.area.append(element);
        }
        const { element } = menu;
        setAttr(element, 'aria-label', paint.name);
        const items = paint.items.map(item => {
            const id = `${this.#menuIds}-${item.command.toLowerCase()}`;
            const command = this.#doc.createElement('div');
            command.id = id;
            command.setAttribute('role', 'menuitem');
            command.className = item.command === paint.active ? 'ex-sheet-tab-command ex-sheet-tab-command-active' : 'ex-sheet-tab-command';
            if (item.disabledReason !== undefined) {
                command.setAttribute('aria-disabled', 'true');
                command.title = item.disabledReason;
            }
            command.textContent = item.label;
            command.addEventListener('click', () => { this.#hooks.menuCommand(item.command); });
            return command;
        });
        element.replaceChildren(...items);
        setAttr(element, 'aria-activedescendant', `${this.#menuIds}-${paint.active.toLowerCase()}`);
        if (this.#doc.activeElement !== element) element.focus({ preventScroll: true });
        return menu.session;
    }

    /** The tab's menu goes. The keyboard, where the menu held it, is the caller's to have handed on first. */
    closeMenu(): void {
        const menu = this.#menu;
        if (menu === undefined) return;
        this.#menu = undefined;
        this.#session++;
        menu.element.remove();
    }

    // ---- Dragging --------------------------------------------------------------------------------------------

    // A press of the main button on a tab notes where a drag starts; nothing is done yet.
    #startDrag(event: MouseEvent, sheet: Sheet): void {
        if (event.button !== 0 || event.ctrlKey || event.shiftKey || event.altKey || event.metaKey) {
            this.#endDrag();
            return;
        }
        this.#dragFrom = sheet;
        this.area.setAttribute('data-ex-dragging', 'true');
    }

    #endDrag(): void {
        this.#dragFrom = undefined;
        this.area.removeAttribute('data-ex-dragging');
    }

    // The release on a tab, or past the last: the tab dragged moves there.
    #drop(event: MouseEvent, target: Sheet | undefined): void {
        event.stopPropagation();
        const from = this.#dragFrom;
        this.#endDrag();
        if (from === undefined || from === target || event.button !== 0) return;
        this.#hooks.dropped(from, target);
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
        tab.setAttribute('aria-haspopup', 'menu');
        tab.setAttribute('data-ex-claims', 'click dblclick contextmenu keydown');
        tab.addEventListener('mousedown', event => {
            event.preventDefault();
            this.#startDrag(event, sheet);
        });
        tab.addEventListener('mouseup', event => { this.#drop(event, sheet); });
        tab.addEventListener('click', () => { this.#hooks.pressed(sheet); });
        tab.addEventListener('dblclick', () => { this.#hooks.renameAsked(sheet); });
        tab.addEventListener('contextmenu', event => {
            event.preventDefault();
            this.#hooks.menuAsked(sheet);
        });
        this.#tabs.set(sheet, tab);
        return tab;
    }
}
