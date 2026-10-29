// The Sheet Toolbar's band (ADR-0100): above the Formula Bar, inside the Sheet's box and outside the grid's root, so
// the grid's key listener never hears a key pressed here. It is ARIA's toolbar: one tab stop, which names the item
// the keyboard is on through aria-activedescendant. ← and → move among the items that can be pressed, Home and End go
// to the ends, Enter and Space press one and give the keyboard back to the Sheet, and Escape gives it back. A menu, and a
// split control's arrow, open their list: Enter or Space on a menu, ↓ or Alt+↓ on either, or a press. A pointer
// press keeps the keyboard where it is (mousedown's default is prevented), as Excel's ribbon leaves it in the cell.
// While an edit is open every item is unavailable.
//
// The KeyTips take the keyboard to the band, as Excel's Alt takes it to the ribbon: the rows' letters first, a row's
// letter its items' letters, an item's letters press it, or open its list (Alt, H, H opens the Fill's palette). Letters
// typed that narrow a level are a level of their own, as Excel's Format menu is behind O. Escape goes back one level,
// from the rows' letters to the Sheet, as Excel's does (the Windows readings of 2026-10-12, R2); any other key ends
// them and does nothing, and every key typed while they stand is theirs, its default too. The elements wear the C# Sheet's classes, so one stylesheet lays
// out both. What an item means is the Sheet's: this draws, and reports what was pressed.
//
// A Chrome's toolbarItem slot may draw an item in the band's place for it (ADR-0390): the band keeps the keys, the
// active mark and the KeyTip, and asks for the item's list through the item's openRequest rather than opening the
// built-in one.

import type { SheetToolbarChoice } from './sheet-choices';
import { preventDefault, setAttr } from './dom-attributes';
import { SlottedToolbarItem, type SheetToolbarContent, type SheetToolbarIcon, type SheetToolbarItemKind, type SheetToolbarItemShown, type SheetToolbarItemSlot } from './sheet-toolbar-slot';

export type { SheetToolbarIcon } from './sheet-toolbar-slot';

/** One Toolbar Item as one pass shows it. */
export interface SheetToolbarItem {
    /** Stable within the toolbar: the element's id is made from it, so the item the keyboard is on stays named. */
    readonly key: string;
    /** A split control's face does what it shows and its arrow opens its list; a menu opens its list. */
    readonly kind: SheetToolbarItemKind;
    readonly name: string;
    /** The name with its key, for the tooltip: `Bold (Ctrl+B)`. */
    readonly tooltip: string;
    /** The picture, or `undefined` for an item that shows its text instead. */
    readonly icon: SheetToolbarIcon | undefined;
    /** For a menu, the text its face shows: the Number Format's category. */
    readonly text: string | undefined;
    /** For a Consumer's button, its own content, drawn in place of its text. */
    readonly content: SheetToolbarContent | undefined;
    /** For a colour control, the colour its face sets, as CSS paints it. */
    readonly swatch: string | undefined;
    /** For a split control, its arrow's name: `Borders, more`. */
    readonly moreName: string | undefined;
    /** For a toggle: whether the Focus cell has what it sets. */
    readonly pressed: boolean;
    /** Why it cannot be pressed now, or `undefined` where it can. */
    readonly disabledReason: string | undefined;
    /** What a press does; `undefined` for an item that is never pressed, whose `disabledReason` says why. */
    readonly press: (() => void) | undefined;
    /** For a menu or a split control, what opens its list; `keyTipRow` is the row whose KeyTips opened it, which Escape
     * there goes back to, or `undefined` where no KeyTip did. */
    readonly open: ((keyTipRow: number | undefined) => void) | undefined;
    /** For a menu or a split control, its list's choices as they stand now, which a Chrome-drawn item is handed. */
    readonly list: (() => readonly SheetToolbarChoice[]) | undefined;
    /** Its KeyTip letters, Excel's Home tab's, or `undefined` for none. */
    readonly keyTip: string | undefined;
}

/** A Toolbar Row as one pass shows it. */
export interface SheetToolbarRow {
    readonly label: string;
    /** Its KeyTip letter, a ribbon tab's: the formatting row's is H, Excel's Home. */
    readonly keyTip: string | undefined;
    readonly items: readonly (SheetToolbarItem | 'separator')[];
}

/** What one pass shows: the rows of items, a separator between groups. */
export interface SheetToolbarPaint {
    readonly rows: readonly SheetToolbarRow[];
    readonly rowHeightPx: number;
    readonly name: string;
    /** The Chrome's slot for each item, or `undefined` for the built-in items alone. */
    readonly itemSlot: SheetToolbarItemSlot | undefined;
}

/** What the band tells the Sheet. */
export interface SheetToolbarHooks {
    /** The keyboard goes back to the Sheet: after a press from the keys, or Escape. */
    readonly giveBack: () => void;
    /** A Chrome-drawn item's list made a choice, the keyboard given back: the Sheet makes it, as the built-in list's. */
    readonly chosen: (choice: SheetToolbarChoice) => void;
    /** The band's keys ask for a Chrome-drawn item's list: the Sheet notes what it is opened over. */
    readonly opening: () => void;
}

const svgOpen = '<svg class="ex-sheet-toolbar-icon" viewBox="0 0 16 16" aria-hidden="true" focusable="false" fill="none" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round">';

/**
 * The built-in Chrome's pictures for the Toolbar Items (ADR-0100), the C# Chrome's: each the inside of an SVG of view
 * box `0 0 16 16`, drawn with `fill="none"` and `stroke="currentColor"`, round caps and joins, so the colour is the
 * text's. A Chrome without pictures of its own draws these.
 */
export const sheetToolbarPictures: Readonly<Record<SheetToolbarIcon, string>> = Object.freeze({
    bold: '<path stroke-width="2" d="M4.5 2.5h4a2.75 2.75 0 0 1 0 5.5h-4zM4.5 8h4.75a2.75 2.75 0 0 1 0 5.5H4.5z"/>',
    italic: '<path stroke-width="1.5" d="M7 2.5h6M3 13.5h6M10 2.5 6 13.5"/>',
    underline: '<path stroke-width="1.5" d="M4.5 2v5.5a3.5 3.5 0 0 0 7 0V2M3 14.5h10"/>',
    strikethrough: '<path stroke-width="1.5" d="M11 4.5C11 3.2 9.7 2.2 8 2.2S5 3.2 5 4.5c0 1.4 1.2 2.2 3 2.6M5 11.5c0 1.3 1.3 2.3 3 2.3s3-1 3-2.3M2 8.5h12"/>',
    alignLeft: '<path stroke-width="1.5" d="M2 3h12M2 6.5h8M2 10h12M2 13.5h8"/>',
    alignCenter: '<path stroke-width="1.5" d="M2 3h12M4 6.5h8M2 10h12M4 13.5h8"/>',
    alignRight: '<path stroke-width="1.5" d="M2 3h12M6 6.5h8M2 10h12M6 13.5h8"/>',
    percent: '<path stroke-width="1.5" d="M3 13 13 3"/><circle cx="4.5" cy="4.5" r="1.75" stroke-width="1.25"/><circle cx="11.5" cy="11.5" r="1.75" stroke-width="1.25"/>',
    formatCells: '<path stroke-width="1.25" d="M2 2.5h12v11H2zM2 5.5h12M5 5.5v8"/><path stroke-width="1.25" d="M8 9h4M8 11.5h3"/>',
    comma: '<circle cx="8" cy="9" r="1.5" fill="currentColor" stroke="none"/><path stroke-width="1.5" d="M9.4 9.2c0 2.2-.8 3.6-2.4 4.6"/>',
    fontColour: '<path stroke-width="1.5" d="M4 11.5 8 2l4 9.5M5.5 8h5"/>',
    fill: '<path stroke-width="1.25" d="M2.5 7.5 7 3l4.5 4.5L7 12zM5 1.5 7 3M13 9.5s1.5 1.8 1.5 2.8a1.5 1.5 0 0 1-3 0c0-1 1.5-2.8 1.5-2.8"/>',
    borders: '<path stroke-width="1.25" stroke-dasharray="1.5 1.5" d="M2 8h12M8 2v12"/><path stroke-width="1.5" d="M2 2h12v12H2z"/>',
});

// The arrow a menu or a split control's list opens from.
const caret = `${svgOpen}<path stroke-width="1.5" d="M4.5 6.5 8 10l3.5-3.5"/></svg>`;

// What an element of an item is drawn from: kept, so a pass that changes nothing writes nothing.
interface BuiltInDrawn {
    readonly element: HTMLElement;
    readonly buttons: readonly HTMLButtonElement[];
    readonly face: HTMLButtonElement;
    readonly kind: SheetToolbarItemKind;
    picture: string | undefined;
    // The Consumer's content drawn on the face, and what takes it away, while it is.
    content: SheetToolbarContent | undefined;
    unmount: (() => void) | undefined;
    // The KeyTip shown on it, while one is.
    badge: HTMLElement | undefined;
    // The Chrome's slot that left it to the built-in Chrome, which is not asked again.
    readonly declinedBy: SheetToolbarItemSlot | undefined;
}

type Drawn = BuiltInDrawn | SlottedToolbarItem;

// The KeyTips standing: the rows' letters, or one row's items' letters; and the letters typed at that level so far.
interface KeyTips {
    readonly row: number | undefined;
    readonly typed: string;
}

let instances = 0;

/** The band and its items' buttons, each made once for its key and kept while the key is shown. */
export class SheetToolbar {
    readonly band: HTMLElement;
    readonly #doc: Document;
    readonly #hooks: SheetToolbarHooks;
    readonly #prefix: string;
    readonly #rows: HTMLElement[] = [];
    // Each row's separators, the n-th kept for the n-th gap between groups.
    readonly #separators = new Map<HTMLElement, HTMLElement[]>();
    readonly #drawn = new Map<string, Drawn>();
    #items: readonly SheetToolbarItem[] = [];
    // The item the keyboard is on while the band holds it: its key, so a repaint keeps it.
    #active: string | undefined;
    #painted: SheetToolbarPaint | undefined;
    #disposed = false;
    // The KeyTips while they stand.
    #keyTips: KeyTips | undefined;
    // What a Chrome's slot threw in this pass, thrown once the pass is done: the item is the built-in Chrome's.
    #failed: Error | undefined;
    readonly #rowBadges = new Map<HTMLElement, HTMLElement>();
    // The list the band's keys asked a Chrome-drawn item for, until it closes: the item's key, and the row whose
    // KeyTips asked, which Escape in the list goes back to.
    #requested: { readonly key: string; readonly keyTipRow: number | undefined } | undefined;

    constructor(doc: Document, hooks: SheetToolbarHooks) {
        this.#doc = doc;
        this.#hooks = hooks;
        this.#prefix = `ex-sheet-toolbar-${String(++instances)}-`;
        this.band = doc.createElement('div');
        this.band.className = 'ex-sheet-toolbar';
        this.band.setAttribute('role', 'toolbar');
        this.band.setAttribute('aria-orientation', 'horizontal');
        this.band.tabIndex = 0;
        this.band.addEventListener('keydown', this.#onKey);
        // A press anywhere on the band keeps the keyboard where it is: on a button, a disabled one, a separator or the
        // gap between them, none of which takes it from the Sheet.
        this.band.addEventListener('mousedown', preventDefault);
        // The keyboard arriving by Tab lands on the item it last stood on, or on the first that can be pressed.
        this.band.addEventListener('focus', () => {
            // The KeyTips take it with no item active, as the C# toolbar's do: the letters choose.
            if (this.#keyTips !== undefined) return;
            if (this.#activeItem() === undefined) this.#active = this.#enabled()[0]?.key;
            this.#showActive();
        });
        this.band.addEventListener('blur', () => {
            // The keyboard left the band, by a press elsewhere or Tab: the KeyTips go with it.
            if (this.#keyTips !== undefined) this.#showKeyTips(undefined);
            this.#showActive();
        });
    }

    /** Whether the KeyTips stand. */
    get showsKeyTips(): boolean {
        return this.#keyTips !== undefined;
    }

    /** Starts the KeyTips, the rows' letters, and takes the keyboard to the band; false where no row has a letter. */
    startKeyTips(): boolean {
        if (this.#painted?.rows.some(row => row.keyTip !== undefined) !== true) return false;
        this.#withdrawRequest();
        this.#showKeyTips({ row: undefined, typed: '' });
        this.band.focus({ preventScroll: true });
        return true;
    }

    /** Escape in a list a KeyTip opened: back to the letters of the row it was opened from, the keyboard on the band. */
    backToKeyTips(row: number): void {
        this.#showKeyTips({ row, typed: '' });
        this.band.focus({ preventScroll: true });
    }

    /** Ends the KeyTips where they stand without moving the keyboard, as the band is taken away. */
    dropKeyTips(): void {
        this.#keyTips = undefined;
    }

    /** The band's height for `rows` rows of `rowHeightPx`. */
    static heightPx(rows: number, rowHeightPx: number): number {
        return rows * rowHeightPx;
    }

    paint(paint: SheetToolbarPaint): void {
        // A band gone with its Sheet is drawn no more: a Chrome's slots, gone before it, would be mounted again.
        if (this.#disposed) return;
        this.#painted = paint;
        setAttr(this.band, 'aria-label', paint.name);
        setAttr(this.band, 'style', `height: ${String(SheetToolbar.heightPx(paint.rows.length, paint.rowHeightPx))}px; --ex-sheet-toolbar-row-height: ${String(paint.rowHeightPx)}px`);
        while (this.#rows.length > paint.rows.length) {
            const row = this.#rows.pop();
            if (row === undefined) break;
            this.#separators.delete(row);
            this.#rowBadges.delete(row);
            row.remove();
        }
        const items: SheetToolbarItem[] = [];
        const shown = new Set<string>();
        paint.rows.forEach((row, r) => {
            const element = this.#rowAt(r);
            setAttr(element, 'aria-label', row.label);
            setAttr(element, 'style', `height: ${String(paint.rowHeightPx)}px`);
            let gaps = 0;
            const children = row.items.map(item => {
                if (item === 'separator') return this.#separator(element, gaps++);
                items.push(item);
                shown.add(item.key);
                return this.#paintItem(item, this.#keyTipShown(r, item.keyTip));
            });
            const tips = this.#keyTips;
            if (tips !== undefined && tips.row === undefined && row.keyTip !== undefined && row.keyTip.startsWith(tips.typed)) {
                children.unshift(this.#rowBadge(element, row.keyTip));
            }
            const current = [...element.children];
            if (current.length !== children.length || children.some((child, i) => current[i] !== child)) element.replaceChildren(...children);
        });
        for (const [key, drawn] of this.#drawn) {
            if (shown.has(key)) continue;
            this.#takeAway(drawn);
            this.#drawn.delete(key);
        }
        this.#items = items;
        const failed = this.#failed;
        this.#failed = undefined;
        // The item the keyboard was on went, or can no longer be pressed: the nearest that can, from the start.
        if (this.#active !== undefined && this.#activeItem() === undefined) this.#active = this.#enabled()[0]?.key;
        this.#showActive();
        if (failed !== undefined) throw failed;
    }

    /** What the Chrome-drawn lists were opened over has changed, as it ends the built-in list: each is asked to close. */
    endLists(): void {
        this.#requested = undefined;
        for (const drawn of this.#drawn.values()) {
            if (drawn instanceof SlottedToolbarItem && (drawn.kind === 'menu' || drawn.kind === 'split')) drawn.requestClose();
        }
    }

    /** Takes every Chrome-drawn item's contents away, as the Sheet goes. */
    dispose(): void {
        this.#disposed = true;
        // The KeyTips go with the band: its blur, as the Sheet empties its element, has none to take away.
        this.#keyTips = undefined;
        for (const drawn of this.#drawn.values()) this.#takeAway(drawn);
        this.#drawn.clear();
        this.#requested = undefined;
    }

    /** Whether the keyboard is on the band. */
    get holdsKeyboard(): boolean {
        return this.#doc.activeElement === this.band;
    }

    #rowAt(index: number): HTMLElement {
        const known = this.#rows[index];
        if (known !== undefined) return known;
        const row = this.#doc.createElement('div');
        row.className = 'ex-sheet-toolbar-row';
        row.setAttribute('role', 'group');
        this.#rows.push(row);
        this.band.append(row);
        return row;
    }

    #separator(row: HTMLElement, ordinal: number): HTMLElement {
        const kept = this.#separators.get(row) ?? []; // oxlint-disable-line ex-grid/no-stand-in-default -- a row not yet holding a separator starts with none
        this.#separators.set(row, kept);
        const known = kept[ordinal];
        if (known !== undefined) return known;
        const separator = this.#doc.createElement('span');
        separator.className = 'ex-sheet-toolbar-separator';
        separator.setAttribute('role', 'separator');
        separator.setAttribute('aria-orientation', 'vertical');
        kept.push(separator);
        return separator;
    }

    // The letters an item shows while its row's KeyTips stand, those that begin with what was typed.
    #keyTipShown(row: number, letters: string | undefined): string | undefined {
        const tips = this.#keyTips;
        return tips?.row === row && letters?.startsWith(tips.typed) === true ? letters : undefined;
    }

    #rowBadge(row: HTMLElement, letters: string): HTMLElement {
        const badge = this.#badge(this.#rowBadges.get(row), letters, 'ex-sheet-keytip ex-sheet-row-keytip');
        this.#rowBadges.set(row, badge);
        return badge;
    }

    // A KeyTip's badge, the one kept or a new one, showing `letters`.
    #badge(kept: HTMLElement | undefined, letters: string, className: string): HTMLElement {
        const badge = kept ?? this.#doc.createElement('span');
        setAttr(badge, 'class', className);
        setAttr(badge, 'aria-hidden', 'true');
        if (badge.textContent !== letters) badge.textContent = letters;
        return badge;
    }

    #paintItem(item: SheetToolbarItem, keyTip: string | undefined): HTMLElement {
        const drawn = this.#drawnOf(item);
        if (drawn instanceof SlottedToolbarItem) {
            drawn.show(this.#shownOf(item, drawn.element.classList.contains('ex-sheet-toolbar-active')));
            drawn.badge = this.#placeBadge(drawn.element, drawn.badge, keyTip);
            return drawn.element;
        }
        const title = item.disabledReason === undefined ? item.tooltip : `${item.tooltip}: ${item.disabledReason}`;
        setAttr(drawn.face, 'title', title);
        setAttr(drawn.face, 'aria-label', item.name);
        if (item.kind === 'split') {
            setAttr(drawn.element, 'aria-label', item.name);
            const arrow = drawn.buttons[1];
            if (arrow === undefined || item.moreName === undefined) throw new Error(`Internal: the split control '${item.key}' has an arrow, and names it.`);
            setAttr(arrow, 'title', title);
            setAttr(arrow, 'aria-label', item.moreName);
        }
        if (item.kind === 'toggle') setAttr(drawn.face, 'aria-pressed', item.pressed ? 'true' : 'false');
        else drawn.face.removeAttribute('aria-pressed');
        const disabled = item.disabledReason !== undefined;
        for (const button of drawn.buttons) if (button.disabled !== disabled) button.disabled = disabled;
        drawn.element.classList.toggle('ex-sheet-toolbar-pressed', item.kind === 'toggle' && item.pressed);
        const picture = JSON.stringify([item.icon, item.text, item.swatch]);
        if (drawn.picture !== picture || drawn.content !== item.content) {
            drawn.picture = picture;
            this.#drawFace(drawn, item);
        }
        drawn.badge = this.#placeBadge(drawn.element, drawn.badge, keyTip);
        return drawn.element;
    }

    // The KeyTip stands last in the item, over its face, as the C# Chrome draws it.
    #placeBadge(element: HTMLElement, kept: HTMLElement | undefined, keyTip: string | undefined): HTMLElement | undefined {
        if (keyTip === undefined) {
            kept?.remove();
            return undefined;
        }
        const badge = this.#badge(kept, keyTip, 'ex-sheet-keytip');
        if (badge.parentElement !== element || element.lastChild !== badge) element.append(badge);
        return badge;
    }

    // What a Chrome-drawn item's contents are told it shows.
    #shownOf(item: SheetToolbarItem, active: boolean): SheetToolbarItemShown {
        return {
            name: item.name, tooltip: item.tooltip, icon: item.icon, text: item.text, content: item.content, swatch: item.swatch, moreName: item.moreName,
            pressed: item.pressed, disabledReason: item.disabledReason, active,
            choices: item.list?.().map(({ label, swatch, selected, disabledReason }) => ({ label, swatch, selected, disabledReason })) ?? [], // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0100: an item with no list, a toggle or a button, has no choices
        };
    }

    // The face's contents: the picture, a menu's text and its caret, a colour control's swatch; or a Consumer's own
    // content in their place.
    #drawFace(drawn: BuiltInDrawn, item: SheetToolbarItem): void {
        const { face } = drawn;
        this.#unmountContent(drawn);
        face.replaceChildren();
        if (item.content !== undefined) {
            const element = this.#doc.createElement('span');
            element.className = 'ex-sheet-toolbar-content';
            face.append(element);
            // Not mounted again on every pass where it fails: what it throws is thrown once this pass has drawn the rest.
            drawn.content = item.content;
            try {
                drawn.unmount = mountedContent(item.content, element, item.name);
            } catch (error: unknown) { // oxlint-disable-line ex-grid/no-catch -- ADR-0390: rethrown by paint once the pass has drawn the rest
                this.#failed ??= error instanceof Error ? error : new Error(`The content of the toolbar button '${item.name}' threw something that is not an Error.`, { cause: error });
            }
            return;
        }
        if (item.icon !== undefined) face.insertAdjacentHTML('beforeend', `${svgOpen}${sheetToolbarPictures[item.icon]}</svg>`);
        if (item.text !== undefined) {
            const text = this.#doc.createElement('span');
            text.className = 'ex-sheet-toolbar-text';
            text.textContent = item.text;
            face.append(text);
        }
        if (item.swatch !== undefined) {
            const swatch = this.#doc.createElement('span');
            swatch.className = 'ex-sheet-toolbar-swatch';
            // The colour is the user's choice, document data painted as recorded (ADR-0071).
            swatch.style.backgroundColor = item.swatch;
            face.append(swatch);
        }
        if (item.kind === 'menu') face.insertAdjacentHTML('beforeend', caret);
    }

    #drawnOf(item: SheetToolbarItem): Drawn {
        const known = this.#drawn.get(item.key);
        const slot = this.#painted?.itemSlot;
        if (known !== undefined && known.kind === item.kind
            && (known instanceof SlottedToolbarItem ? known.mount === slot : slot === undefined || known.declinedBy === slot)) return known;
        if (known !== undefined) this.#takeAway(known);
        this.#drawn.delete(item.key);
        const id = `${this.#prefix}${item.key}`;
        let declinedBy: SheetToolbarItemSlot | undefined;
        if (slot !== undefined) {
            // Not asked again on every pass where it fails, or answers that the built-in Chrome draws the item. What it
            // throws is thrown once this pass has drawn the rest.
            declinedBy = slot;
            let slotted: SlottedToolbarItem | undefined;
            try {
                slotted = SlottedToolbarItem.tryMount(this.#doc, item.key, item.kind, this.#shownOf(item, false), slot, {
                    invoke: () => { this.#invoke(item.key); },
                    choicesClosed: (choice, escaped) => { this.#choicesClosed(item.key, choice, escaped); },
                    closedChoosingNothing: escaped => { this.#choicesClosed(item.key, undefined, escaped); },
                });
            } catch (error: unknown) { // oxlint-disable-line ex-grid/no-catch -- ADR-0390: rethrown by paint once the pass has drawn the rest
                this.#failed ??= error instanceof Error ? error : new Error('A toolbarItem slot threw something that is not an Error.', { cause: error });
            }
            if (slotted !== undefined) {
                slotted.element.id = id;
                slotted.element.dataset['exKind'] = item.kind;
                this.#drawn.set(item.key, slotted);
                return slotted;
            }
        }
        const button = (className: string, act: () => void): HTMLButtonElement => {
            const made = this.#doc.createElement('button');
            made.type = 'button';
            made.className = className;
            made.tabIndex = -1;
            // A press keeps the keyboard where it is: on the Sheet, where the next key acts on what the command left.
            // Its press takes its place in the Sheet's order, behind a commit or an opening (ADR-0240).
            made.setAttribute('data-ex-claims', 'click');
            made.addEventListener('mousedown', preventDefault);
            made.addEventListener('click', act);
            return made;
        };
        const press = (): void => { this.#pressOf(item.key)?.(); };
        const open = (): void => { this.#openFromBand(item.key); };
        let drawn: Drawn;
        if (item.kind === 'split') {
            const group = this.#doc.createElement('span');
            group.className = 'ex-sheet-toolbar-item ex-sheet-toolbar-split';
            group.setAttribute('role', 'group');
            const face = button('ex-sheet-toolbar-face', press);
            const arrow = button('ex-sheet-toolbar-arrow', open);
            arrow.setAttribute('aria-haspopup', 'menu');
            arrow.innerHTML = caret;
            group.append(face, arrow);
            drawn = { element: group, buttons: [face, arrow], face, kind: item.kind, picture: undefined, content: undefined, unmount: undefined, badge: undefined, declinedBy };
        } else {
            const face = button(item.kind === 'menu' ? 'ex-sheet-toolbar-item ex-sheet-toolbar-menu' : 'ex-sheet-toolbar-item', item.kind === 'menu' ? open : press);
            if (item.kind === 'menu') face.setAttribute('aria-haspopup', 'menu');
            drawn = { element: face, buttons: [face], face, kind: item.kind, picture: undefined, content: undefined, unmount: undefined, badge: undefined, declinedBy };
        }
        drawn.element.id = id;
        drawn.element.dataset['exKind'] = item.kind;
        this.#drawn.set(item.key, drawn);
        return drawn;
    }

    // The item by its key, as the last pass described it: a press acts on what is shown now, never on a closure
    // a pass replaced.
    #pressOf(key: string): (() => void) | undefined {
        return this.#shownItem(key)?.press;
    }

    // Opens an item's list from a press or the band's keys, not a KeyTip; answers whether the item has one.
    #openFromBand(key: string): boolean {
        const item = this.#shownItem(key);
        if (item?.open === undefined) return false;
        this.#openList(item, undefined);
        return true;
    }

    // Opens an item's list: the built-in one, or, for a Chrome-drawn item, the Chrome's, which the band asks for.
    #openList(item: SheetToolbarItem, keyTipRow: number | undefined): void {
        const drawn = this.#drawn.get(item.key);
        if (!(drawn instanceof SlottedToolbarItem)) {
            item.open?.(keyTipRow);
            return;
        }
        this.#withdrawRequest();
        this.#hooks.opening();
        this.#requested = { key: item.key, keyTipRow };
        drawn.requestOpen();
    }

    // The keyboard goes back to the Sheet from the band's own keys: a list asked for and not yet closed is closed.
    #giveBack(): void {
        this.#withdrawRequest();
        this.#hooks.giveBack();
    }

    #withdrawRequest(): void {
        const requested = this.#requested;
        if (requested === undefined) return;
        this.#requested = undefined;
        const drawn = this.#drawn.get(requested.key);
        if (drawn instanceof SlottedToolbarItem) drawn.requestClose();
    }

    // A Chrome-drawn item's press: as the built-in item's, a menu's opening the built-in list.
    #invoke(key: string): void {
        const item = this.#shownItem(key);
        if (item === undefined) return;
        if (item.kind === 'menu') item.open?.(undefined);
        else item.press?.();
    }

    // A Chrome-drawn item's list closed: the keyboard goes back, to the KeyTips after Escape in a list one opened, and
    // the choice is then made.
    #choicesClosed(key: string, choice: number | undefined, escaped: boolean): void {
        const requested = this.#requested?.key === key ? this.#requested : undefined;
        if (requested !== undefined) this.#requested = undefined;
        if (choice === undefined) {
            if (escaped && requested?.keyTipRow !== undefined) this.backToKeyTips(requested.keyTipRow);
            // A list the band asked to close, closing late, leaves the keyboard where the band has it: on its KeyTips.
            else if (!this.holdsKeyboard) this.#hooks.giveBack();
            return;
        }
        const choices = this.#items.find(item => item.key === key)?.list?.() ?? []; // oxlint-disable-line ex-grid/no-stand-in-default -- an item with no list has no choice to make, and the index is refused below
        const made = choices[choice];
        if (made === undefined) throw new RangeError(`choicesClosed is handed the index of one of the item's ${String(choices.length)} choices, not ${String(choice)} (ADR-0100).`);
        if (made.disabledReason !== undefined) throw new Error(`The choice '${made.label}' cannot be made: ${made.disabledReason}`);
        this.#hooks.giveBack();
        this.#hooks.chosen(made);
    }

    #takeAway(drawn: Drawn): void {
        if (drawn instanceof SlottedToolbarItem) {
            if (this.#requested !== undefined && this.#drawn.get(this.#requested.key) === drawn) this.#requested = undefined;
            drawn.dispose();
        } else {
            this.#unmountContent(drawn);
            drawn.element.remove();
        }
    }

    // The Consumer's content goes with the face it was drawn on.
    #unmountContent(drawn: BuiltInDrawn): void {
        const unmount = drawn.unmount;
        drawn.content = undefined;
        drawn.unmount = undefined;
        unmount?.();
    }

    #shownItem(key: string): SheetToolbarItem | undefined {
        return this.#items.find(shown => shown.key === key && shown.disabledReason === undefined);
    }

    #enabled(): SheetToolbarItem[] {
        return this.#items.filter(item => item.disabledReason === undefined);
    }

    #activeItem(): SheetToolbarItem | undefined {
        return this.#enabled().find(item => item.key === this.#active);
    }

    #showActive(): void {
        const active = this.holdsKeyboard && this.#keyTips === undefined ? this.#activeItem() : undefined;
        for (const [key, drawn] of this.#drawn) {
            drawn.element.classList.toggle('ex-sheet-toolbar-active', key === active?.key);
            if (drawn instanceof SlottedToolbarItem) drawn.setActive(key === active?.key);
        }
        if (active === undefined) this.band.removeAttribute('aria-activedescendant');
        else setAttr(this.band, 'aria-activedescendant', `${this.#prefix}${active.key}`);
    }

    readonly #onKey = (event: KeyboardEvent): void => {
        if (event.isComposing || event.target !== this.band) return;
        if (this.#keyTips !== undefined) {
            event.preventDefault();
            event.stopPropagation();
            this.#keyTipKey(event, this.#keyTips);
            return;
        }
        if (event.ctrlKey || event.metaKey) return;
        const enabled = this.#enabled();
        const at = enabled.findIndex(item => item.key === this.#active);
        const active = enabled[at];
        // ↓ or Alt+↓ opens a menu's or a split control's list, which takes the keyboard; Alt and nothing else.
        if (event.key === 'ArrowDown') {
            if (active === undefined || !this.#openFromBand(active.key)) return;
            event.preventDefault();
            return;
        }
        if (event.altKey) return;
        const move = (to: SheetToolbarItem | undefined): void => {
            if (to !== undefined) this.#active = to.key;
            this.#showActive();
        };
        switch (event.key) {
            case 'ArrowRight': move(enabled[at + 1]); break;
            case 'ArrowLeft': move(at > 0 ? enabled[at - 1] : undefined); break;
            case 'Home': move(enabled[0]); break;
            case 'End': move(enabled.at(-1)); break;
            case 'Enter':
            case ' ': {
                if (active?.kind === 'menu') {
                    if (!this.#openFromBand(active.key)) return;
                    break;
                }
                const press = active === undefined ? undefined : this.#pressOf(active.key);
                if (press === undefined) return;
                // The keyboard goes back before the press, so an item that opens what takes it (Format Cells) keeps it.
                this.#giveBack();
                press();
                break;
            }
            case 'Escape': this.#giveBack(); break;
            default: return;
        }
        event.preventDefault();
    };

    // A key while the KeyTips stand (ADR-0100).
    #keyTipKey(event: KeyboardEvent, tips: KeyTips): void {
        if (event.key === 'Escape') {
            if (tips.typed !== '') this.#showKeyTips({ row: tips.row, typed: '' });
            else if (tips.row !== undefined) this.#showKeyTips({ row: undefined, typed: '' });
            else this.#endKeyTips();
            return;
        }
        // Shift and Caps Lock change no letter the KeyTips read, which are matched in capitals: the letter they come
        // before is still to come.
        if (event.key === 'Shift' || event.key === 'CapsLock') return;
        if (event.key.length !== 1 || event.ctrlKey || event.metaKey || event.altKey) {
            this.#endKeyTips();
            return;
        }
        const typed = tips.typed + event.key.toUpperCase();
        const painted = this.#painted;
        if (painted === undefined) throw new Error('Internal: the KeyTips stand only on a painted band.');
        const rows = painted.rows;
        if (tips.row === undefined) {
            const exact = rows.findIndex(row => row.keyTip === typed);
            if (exact >= 0) this.#showKeyTips({ row: exact, typed: '' });
            else if (rows.some(row => row.keyTip?.startsWith(typed) === true)) this.#showKeyTips({ row: undefined, typed });
            else this.#endKeyTips();
            return;
        }
        const row = rows[tips.row];
        if (row === undefined) {
            // A repaint took the row away: its letters went with it.
            this.#endKeyTips();
            return;
        }
        const items = row.items.filter(item => item !== 'separator');
        const chosen = items.find(item => item.keyTip === typed);
        if (chosen === undefined) {
            if (items.some(item => item.keyTip?.startsWith(typed) === true)) this.#showKeyTips({ row: tips.row, typed });
            else this.#endKeyTips();
            return;
        }
        if (chosen.disabledReason !== undefined) {
            this.#endKeyTips();
            return;
        }
        // The item becomes the active one before the letters go, so the band shows the item its keys act on should
        // the list not open.
        this.#active = chosen.key;
        this.#showKeyTips(undefined);
        // Excel's Alt, H, H opens the Fill's palette rather than setting its colour: a list opens.
        if (chosen.open !== undefined) {
            this.#openList(chosen, tips.row);
            return;
        }
        this.#giveBack();
        chosen.press?.();
    }

    #endKeyTips(): void {
        this.#showKeyTips(undefined);
        this.#giveBack();
    }

    #showKeyTips(tips: KeyTips | undefined): void {
        this.#keyTips = tips;
        if (this.#painted !== undefined) this.paint(this.#painted);
    }
}

// A Consumer's content mounted on a face, and what takes it away: checked as a script may answer it.
function mountedContent(content: SheetToolbarContent, element: HTMLElement, name: string): () => void {
    const unmount: unknown = content(element);
    if (typeof unmount !== 'function') throw new TypeError(`The content of the toolbar button '${name}' answers the function that takes it away (ADR-0100).`);
    // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- checked a function just above, the cleanup the content's type answers
    return unmount as () => void;
}
