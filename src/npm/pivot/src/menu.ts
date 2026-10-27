// ExPivot's built-in menu in its frame (ADR-0061): a placed field's commands, or the Layout menu's choices, in
// the markup and under the classes the Razor component's PivotPopupFrame and PivotMenuView draw, so one
// stylesheet paints both (ADR-0350 section 4). Where the frame stands and what closes it are the pivot's; the
// frame says only that Escape was pressed in it. A Chrome's menu slot draws the commands inside the same frame.

import { SlotMount, ViewListeners, type PivotChromeSlot, type PivotMenuCommand, type PivotMenuSlotContext, type PivotMenuView } from './chrome';
import type { PivotScreenWords } from './words';

export type { PivotMenuCommand } from './chrome';

/** Where a frame stands: in the Field List under its entry, or from the Pivot Toolbar over the report, at its
 * button's left edge (the report filter band) or its right edge (Layout ▾). */
export type PivotMenuPlace = 'field-list' | 'toolbar-start' | 'toolbar-end';

/**
 * ExPivot's frame around a menu or a panel (ADR-0061), as the Razor component's PivotPopupFrame draws it: where it
 * stands is its class, and Escape pressed anywhere in it calls `escape` and goes no further, so a dialog the
 * Consumer put the pivot in does not close on it too. It claims nothing of the pivot's input (ADR-0240 section 6).
 */
export function popupFrame(document: Document, place: PivotMenuPlace, role: 'menu' | 'dialog', escape: () => void,
    innerPopupOpen?: () => boolean): HTMLDivElement {
    const frame = document.createElement('div');
    frame.className = {
        'field-list': 'ex-pivot-popup',
        'toolbar-start': 'ex-pivot-popup ex-pivot-popup-overlay',
        'toolbar-end': 'ex-pivot-popup ex-pivot-popup-overlay ex-pivot-popup-end',
    }[place];
    frame.setAttribute('role', role);
    // A press on its padding, its text or a disabled command gives the frame the keyboard, so Escape stays inside it
    // without another Tab stop (ADR-0061).
    frame.tabIndex = -1;
    frame.setAttribute('data-ex-claims', '');
    frame.addEventListener('keydown', event => {
        if (event.key !== 'Escape' || event.ctrlKey || event.altKey || event.metaKey) return;
        // While a popup of a Chrome's own is open in the frame, Escape is that popup's (ADR-0039).
        if (innerPopupOpen?.() === true) return;
        event.preventDefault();
        event.stopPropagation();
        escape();
    });
    return frame;
}

interface DrawnCommand {
    readonly button: HTMLButtonElement;
    readonly mark: HTMLElement | undefined;
}

function sameShape(a: readonly PivotMenuCommand[], b: readonly PivotMenuCommand[]): boolean {
    return a.length === b.length
        && a.every((command, i) => {
            const other = b[i];
            return other !== undefined && other.id === command.id && other.heading === command.heading
                && (other.checked === undefined) === (command.checked === undefined);
        });
}

// Whether a Chrome's menu shows the same thing for `b` as for `a`.
function sameCommands(a: readonly PivotMenuCommand[], b: readonly PivotMenuCommand[]): boolean {
    return a.length === b.length
        && a.every((command, i) => {
            const other = b[i];
            return other !== undefined && other.id === command.id && other.label === command.label && other.enabled === command.enabled
                && other.checked === command.checked && other.heading === command.heading;
        });
}

/** A menu in ExPivot's frame, the built-in one's or a Chrome's: the pivot places its element, draws it again on every
 * change, and takes it away. */
export interface PivotMenu {
    /** The frame, which the pivot places. */
    readonly element: HTMLElement;
    /** Draws `commands` under the accessible name `label`. */
    draw(label: string, commands: readonly PivotMenuCommand[]): void;
    /** Gives the keyboard to the first enabled command, or to the frame when every command is disabled. */
    takeKeyboard(): void;
    /** The menu is taken away. */
    close(): void;
    /** Whether a popup of a Chrome's contents is open, which may stand outside the frame and take the keyboard there. */
    innerPopupOpen?(): boolean;
}

/** A menu in ExPivot's frame. The pivot draws it again on every change, and a command pressed is the one drawn
 * last: a press never reaches a command built against a layout the pane has since left. */
export class PivotMenuFrame implements PivotMenu {
    /** The frame, which the pivot places. */
    readonly element: HTMLDivElement;
    readonly #menu: HTMLDivElement;
    #commands: readonly PivotMenuCommand[] = [];
    #drawn = new Map<string, DrawnCommand>();

    constructor(document: Document, place: PivotMenuPlace, escape: () => void) {
        this.element = popupFrame(document, place, 'menu', escape);
        this.#menu = document.createElement('div');
        this.#menu.className = 'ex-pivot-menu';
        this.#menu.setAttribute('role', 'none');
        this.element.append(this.#menu);
    }

    /** Draws `commands` under the accessible name `label`. A command keeps its button while the menu holds the
     * same commands, so the one with the keyboard keeps it. */
    draw(label: string, commands: readonly PivotMenuCommand[]): void {
        this.element.setAttribute('aria-label', label);
        if (!sameShape(this.#commands, commands)) this.#build(commands);
        this.#commands = commands;
        for (const command of commands) {
            const drawn = this.#drawn.get(command.id);
            if (drawn === undefined) throw new Error(`Internal: the command '${command.id}' was not drawn.`);
            drawn.button.disabled = !command.enabled;
            if (command.checked !== undefined) drawn.button.setAttribute('aria-checked', command.checked ? 'true' : 'false');
            if (drawn.mark !== undefined) drawn.mark.textContent = command.checked === true ? '✓' : '';
            const text = drawn.button.lastChild;
            if (text?.textContent !== command.label) {
                if (text instanceof Text) text.textContent = command.label;
                else drawn.button.append(command.label);
            }
        }
    }

    /** Gives the keyboard to the first enabled command, or to the frame when every command is disabled. */
    takeKeyboard(): void {
        const first = this.#commands.find(command => command.enabled);
        if (first === undefined) {
            this.element.focus();
            return;
        }
        const drawn = this.#drawn.get(first.id);
        if (drawn === undefined) throw new Error(`Internal: the command '${first.id}' was not drawn.`);
        drawn.button.focus();
    }

    /** Nothing of the built-in menu outlives its frame. */
    close(): void {
        // The frame's listeners go with it.
    }

    #build(commands: readonly PivotMenuCommand[]): void {
        const document = this.element.ownerDocument;
        const drawn = new Map<string, DrawnCommand>();
        const parts: HTMLElement[] = [];
        let group: HTMLElement | undefined;
        for (const command of commands) {
            if (command.heading !== undefined) {
                // A group of choices stands under its heading, as the Razor component's menu draws it.
                group = document.createElement('div');
                group.className = 'ex-pivot-menu-group';
                group.setAttribute('role', 'group');
                group.setAttribute('aria-label', command.heading);
                const heading = document.createElement('div');
                heading.className = 'ex-pivot-menu-heading';
                heading.setAttribute('aria-hidden', 'true');
                heading.textContent = command.heading;
                group.append(heading);
                parts.push(group);
            }
            const button = document.createElement('button');
            button.type = 'button';
            const choice = command.checked !== undefined;
            button.className = choice ? 'ex-pivot-menu-item ex-pivot-menu-choice' : 'ex-pivot-menu-item';
            button.setAttribute('role', choice ? 'menuitemradio' : 'menuitem');
            button.setAttribute('data-ex-claims', 'click');
            let mark: HTMLElement | undefined;
            if (choice) {
                mark = document.createElement('span');
                mark.className = 'ex-pivot-menu-mark';
                mark.setAttribute('aria-hidden', 'true');
                button.append(mark);
            }
            const { id } = command;
            button.addEventListener('click', () => {
                // The command drawn last under this id: the one built against the pane's layout now.
                const current = this.#commands.find(c => c.id === id);
                if (current?.enabled === true) current.invoke();
            });
            drawn.set(id, { button, mark });
            if (group === undefined) parts.push(button);
            else group.append(button);
        }
        if (drawn.size !== commands.length) throw new Error('Internal: two commands of one menu share an id.');
        this.#drawn = drawn;
        this.#menu.replaceChildren(...parts);
    }
}

/**
 * A menu whose commands a Chrome's slot draws (ADR-0390), inside the same frame: the commands, their order and what
 * each does are the pivot's, and a command pressed is the one drawn last under its id, as the built-in menu's is, so a
 * press never reaches a command built against a layout the pane has since left.
 */
export class SlottedMenuFrame implements PivotMenu {
    readonly element: HTMLDivElement;
    readonly #slot: SlotMount<PivotMenuSlotContext>;
    readonly #listeners = new ViewListeners();
    #view: PivotMenuView = Object.freeze({ name: '', commands: Object.freeze([]) });
    #commands: readonly PivotMenuCommand[] = [];

    constructor(document: Document, place: PivotMenuPlace, mount: PivotChromeSlot<PivotMenuSlotContext>, words: PivotScreenWords, escape: () => void) {
        this.element = popupFrame(document, place, 'menu', escape, () => this.#slot.innerPopupOpen);
        const body = document.createElement('div');
        body.setAttribute('role', 'none');
        this.element.append(body);
        const close = (): void => { escape(); };
        this.#slot = new SlotMount('menu', mount, body, focusRequest => Object.freeze({
            view: () => this.#view,
            subscribe: this.#listeners.subscribe,
            close: this.#slot.input(close),
            focusRequest,
            label: (id: string) => words.word(id),
            takeFocus: this.#slot.takeFocus,
        }), close);
    }

    draw(label: string, commands: readonly PivotMenuCommand[]): void {
        this.element.setAttribute('aria-label', label);
        this.#commands = commands;
        // The view is the same object while nothing it shows changed: a press reaches the commands drawn now anyway.
        if (this.#view.name === label && sameCommands(this.#view.commands, commands)) {
            this.#slot.mount();
            return;
        }
        this.#view = Object.freeze({
            name: label,
            commands: Object.freeze(commands.map(command => Object.freeze({ ...command, invoke: this.#slot.input(() => { this.#press(command.id); }) }))),
        });
        // Mounted once its first commands are known, and told of each change after.
        this.#slot.mount();
        this.#listeners.tell();
    }

    takeKeyboard(): void {
        if (this.#commands.some(command => command.enabled)) this.#slot.request();
        else this.element.focus();
    }

    innerPopupOpen(): boolean {
        return this.#slot.innerPopupOpen;
    }

    close(): void {
        this.#listeners.clear();
        this.#slot.dispose();
    }

    // The command drawn last under this id: the one built against the pane's layout now.
    #press(id: string): void {
        const current = this.#commands.find(command => command.id === id);
        if (current?.enabled === true) current.invoke();
    }
}
