// A Toolbar Item drawn by the Consumer's Chrome (ADR-0100, ADR-0390): the C# Sheet's `ISheetChrome.ToolbarItem`, as a
// mount function. The band keeps the item's place, its id, its active mark and its KeyTip; the contents draw the
// item's control inside it, and may draw its list where they like, beside the Sheet or in their own popover. What the
// item means stays the Sheet's: a press is `invoke`, a choice is handed back through `choicesClosed`, and the toolbar's
// keys ask for the list through `openRequest`. A mount that answers nothing leaves the item to the built-in Chrome.

/** A picture the built-in Chrome draws for a Toolbar Item. */
export type SheetToolbarIcon =
    | 'bold' | 'italic' | 'underline' | 'strikethrough' | 'alignLeft' | 'alignCenter' | 'alignRight' | 'percent' | 'comma'
    | 'formatCells' | 'fontColour' | 'fill' | 'borders';

/**
 * A Consumer's own content for a Toolbar Item (ADR-0100), such as an icon, drawn in place of its text: mounted into
 * `element`, it answers the function that takes it away. The item's text still names it, as its accessible name and its
 * tooltip. The C# `ToolbarButton`'s `ChildContent`.
 */
export type SheetToolbarContent = (element: HTMLElement) => () => void;

/** What a Toolbar Item is: a split control's face does what it shows and its arrow opens its list; a menu opens its
 * list. */
export type SheetToolbarItemKind = 'toggle' | 'button' | 'split' | 'menu';

/** One choice of a Toolbar Item's list, as the item's contents are handed it. */
export interface SheetToolbarItemChoice {
    /** Its words: a colour's name, a Number Format's category, a border preset's name. */
    readonly label: string;
    /** For a colour, the colour as CSS paints it: the label is its name. */
    readonly swatch: string | undefined;
    /** Whether it is what the Focus cell has. */
    readonly selected: boolean;
    /** Why it cannot be chosen, or `undefined` where it can. */
    readonly disabledReason: string | undefined;
}

/** What a Toolbar Item shows now: read through {@link SheetToolbarItemSlotContext.view}, the same object until
 * something in it changes. */
export interface SheetToolbarItemView {
    /** The item's name, the built-in Chrome's `aria-label`: `Fill Colour`. */
    readonly name: string;
    /** The name with its key, for the tooltip: `Bold (Ctrl+B)`. */
    readonly tooltip: string;
    /** The built-in Chrome's picture for it, or `undefined` for an item that shows its text. */
    readonly icon: SheetToolbarIcon | undefined;
    /** For a menu, the text its face shows: the Number Format's category; for a Consumer's button, its text, unless it
     * has `content`. */
    readonly text: string | undefined;
    /** For a Consumer's button, its own content, drawn in place of its text: the contents mount it into an element of
     * theirs, and call what it answers when they take that element away. */
    readonly content: SheetToolbarContent | undefined;
    /** For a colour control, the colour its face sets, as CSS paints it. */
    readonly swatch: string | undefined;
    /** For a split control, its arrow's name: `Borders, more`. */
    readonly moreName: string | undefined;
    /** For a toggle: whether the Focus cell has what it sets. */
    readonly pressed: boolean;
    /** Why it cannot be pressed now, or `undefined` where it can: while an edit is open, every item says so. */
    readonly disabledReason: string | undefined;
    /** Whether the toolbar's keyboard is on it, as the built-in Chrome outlines the item. */
    readonly active: boolean;
    /** For a menu or a split control, its list's choices, the Focus cell's marked; none otherwise. */
    readonly choices: readonly SheetToolbarItemChoice[];
    /** The count of the toolbar's requests for the list: ↓, Enter or Space on a menu, or a KeyTip. Whenever it
     * differs from the one the contents last answered, they open their list and give it the keyboard; one they
     * cannot open is closed at once through `choicesClosed`, choosing nothing. It starts at 0, a request not yet
     * made. */
    readonly openRequest: number;
    /** Set to `openRequest` when the toolbar asks the list to close: it gives the keyboard back while the list it
     * asked for may still be opening, or what the list was opened over has changed — the Selection moved, another
     * Sheet shown, an operation run, the toolbar hidden — as each ends the built-in list. Where no request stood, both
     * move on together, so a list opened by a press closes too. The contents close their list, telling nothing, and
     * answer no `openRequest` that `closeRequest` has reached. */
    readonly closeRequest: number;
}

/**
 * What a Toolbar Item's contents are handed (ADR-0100, ADR-0390), once per mount: what the item is, and how it shows
 * now through `view` and `subscribe`, as the Cell Editor's slot is. The contents draw a control in the slot that keeps
 * the keyboard where it is on a pointer press (its `mousedown` default prevented), as the built-in items do.
 */
export interface SheetToolbarItemSlotContext {
    /** Stable for the item's place in the toolbar. */
    readonly id: string;
    /** What the item is, which decides whether it has a list. */
    readonly kind: SheetToolbarItemKind;
    /** What the item shows now. */
    readonly view: () => SheetToolbarItemView;
    /** Hears each change to the view; answers the function that stops hearing it. */
    readonly subscribe: (listener: () => void) => () => void;
    /** Presses the item as the built-in Chrome's does: a toggle or a button, a split control's face, or a menu's,
     * which opens the built-in list. An item that cannot be pressed now does nothing. */
    readonly invoke: () => void;
    /**
     * The contents' own list has closed: with the index of the choice made, or `undefined` for none, and whether
     * Escape closed it. The keyboard goes back to the Sheet, or to the KeyTips where Escape closes a list a KeyTip
     * opened, and the choice is then made, as the built-in list makes it. A choice that is not the list's, or one that
     * cannot be chosen, is refused by name.
     */
    readonly choicesClosed: (choice: number | undefined, escaped: boolean) => void;
}

/**
 * A Toolbar Item's slot (ADR-0390): mounts the item's contents in `slot` and answers the function that takes them
 * away, or answers `undefined` and draws nothing, for the built-in Chrome to draw the item, as the C# Chrome's
 * `ToolbarItem` answers null. Asked once per item, again only when the item's kind or the slot changes.
 */
export type SheetToolbarItemSlot = (slot: HTMLElement, context: SheetToolbarItemSlotContext) => (() => void) | undefined;

/** What the contents' calls reach. */
export interface SlottedToolbarItemHooks {
    readonly invoke: () => void;
    readonly choicesClosed: (choice: number, escaped: boolean) => void;
    readonly closedChoosingNothing: (escaped: boolean) => void;
}

/** The view less what the slotted item counts itself. */
export type SheetToolbarItemShown = Omit<SheetToolbarItemView, 'openRequest' | 'closeRequest'>;

/** An item the Chrome draws: its place in the band, its contents, and the view they read. */
export class SlottedToolbarItem {
    /** The item's place in the band, which wears its id, its active mark and its KeyTip. */
    readonly element: HTMLElement;
    readonly mount: SheetToolbarItemSlot;
    readonly kind: SheetToolbarItemKind;
    /** The KeyTip shown on it, while one is. */
    badge: HTMLElement | undefined;
    readonly #listeners = new Set<() => void>();
    #cleanup: (() => void) | undefined;
    #shown: SheetToolbarItemShown;
    #shownKey: string;
    #view: SheetToolbarItemView;
    #openRequest = 0;
    #closeRequest = 0;
    #live = true;

    private constructor(element: HTMLElement, mount: SheetToolbarItemSlot, kind: SheetToolbarItemKind, shown: SheetToolbarItemShown) {
        this.element = element;
        this.mount = mount;
        this.kind = kind;
        this.#shown = shown;
        this.#shownKey = JSON.stringify(shown);
        this.#view = this.#made();
    }

    /** Mounts the contents for the item, or answers `undefined` where the slot leaves it to the built-in Chrome. */
    static tryMount(doc: Document, id: string, kind: SheetToolbarItemKind, shown: SheetToolbarItemShown, mount: SheetToolbarItemSlot,
        hooks: SlottedToolbarItemHooks): SlottedToolbarItem | undefined {
        const element = doc.createElement('span');
        element.className = 'ex-sheet-toolbar-slot';
        const slot = doc.createElement('span');
        slot.className = 'ex-sheet-toolbar-slot-contents';
        element.append(slot);
        const made = new SlottedToolbarItem(element, mount, kind, shown);
        const context: SheetToolbarItemSlotContext = Object.freeze({
            id, kind,
            view: () => made.#view,
            subscribe: (listener: () => void) => {
                if (typeof listener !== 'function') throw new TypeError('subscribe is handed the function that hears each change to the view.');
                made.#listeners.add(listener);
                return () => { made.#listeners.delete(listener); };
            },
            invoke: () => { if (made.#live) hooks.invoke(); },
            choicesClosed: (choice: number | undefined, escaped: boolean) => {
                // Checked as a script may hand it, whatever its type says.
                const index: unknown = choice;
                if (index !== undefined && (typeof index !== 'number' || !Number.isInteger(index))) {
                    throw new TypeError('choicesClosed is handed the index of the choice made, or undefined for none (ADR-0100).');
                }
                if (typeof escaped !== 'boolean') throw new TypeError('choicesClosed is handed whether Escape closed the list (ADR-0100).');
                if (!made.#live) return;
                if (choice === undefined) hooks.closedChoosingNothing(escaped);
                else hooks.choicesClosed(choice, escaped);
            },
        });
        const cleanup: unknown = mount(slot, context);
        if (cleanup === undefined) {
            // Left to the built-in Chrome: the context handed reaches nothing.
            made.#live = false;
            return undefined;
        }
        if (typeof cleanup !== 'function') {
            throw new TypeError('A toolbarItem slot answers the function that takes its contents away, or undefined for the built-in item (ADR-0390).');
        }
        // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- checked a function just above, the cleanup the slot's type answers
        made.#cleanup = cleanup as () => void;
        return made;
    }

    /** What the item shows now: the contents hear it where it changed. */
    show(shown: SheetToolbarItemShown): void {
        // A function is not written by JSON: the content is compared as itself.
        const key = JSON.stringify(shown);
        if (key === this.#shownKey && shown.content === this.#shown.content) return;
        this.#shown = shown;
        this.#shownKey = key;
        this.#changed();
    }

    /** Whether the toolbar's keyboard is on the item. */
    setActive(active: boolean): void {
        if (this.#shown.active !== active) this.show({ ...this.#shown, active });
    }

    /** The toolbar's keys ask for the list. */
    requestOpen(): void {
        this.#openRequest += 1;
        this.#changed();
    }

    /** The toolbar asks the list to close: the one it asked for, or, where no request stands, any the contents opened. */
    requestClose(): void {
        if (this.#closeRequest === this.#openRequest) this.#openRequest += 1;
        this.#closeRequest = this.#openRequest;
        this.#changed();
    }

    /** Takes the contents away; their calls reach nothing from now on. */
    dispose(): void {
        if (!this.#live) return;
        this.#live = false;
        this.#listeners.clear();
        const cleanup = this.#cleanup;
        this.#cleanup = undefined;
        this.element.remove();
        cleanup?.();
    }

    #changed(): void {
        this.#view = this.#made();
        for (const listener of this.#listeners) listener();
    }

    #made(): SheetToolbarItemView {
        return Object.freeze({ ...this.#shown, openRequest: this.#openRequest, closeRequest: this.#closeRequest });
    }
}
