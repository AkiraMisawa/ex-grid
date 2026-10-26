// The Sheet Toolbar's band (ADR-0100): above the Formula Bar, inside the Sheet's box and outside the grid's root, so
// the grid's key listener never hears a key pressed here. It is ARIA's toolbar: one tab stop, which names the item
// the keyboard is on through aria-activedescendant. ← and → move among the items that can be pressed, Home and End go
// to the ends, Enter and Space press one and give the keyboard back to the Sheet, and Escape gives it back. A pointer
// press keeps the keyboard where it is (mousedown's default is prevented), as Excel's ribbon leaves it in the cell.
// While an edit is open every item is unavailable. The elements wear the C# Sheet's classes, so one stylesheet lays
// out both. What an item means is the Sheet's: this draws, and reports what was pressed.

import { preventDefault, setAttr } from './dom-attributes';

/** A picture the built-in Chrome draws for a Toolbar Item. */
export type SheetToolbarIcon =
    | 'bold' | 'italic' | 'underline' | 'strikethrough' | 'alignLeft' | 'alignCenter' | 'alignRight' | 'percent' | 'comma'
    | 'formatCells';

/** One Toolbar Item as one pass shows it. */
export interface SheetToolbarItem {
    /** Stable within the toolbar: the element's id is made from it, so the item the keyboard is on stays named. */
    readonly key: string;
    readonly kind: 'toggle' | 'button';
    readonly name: string;
    /** The name with its key, for the tooltip: `Bold (Ctrl+B)`. */
    readonly tooltip: string;
    readonly icon: SheetToolbarIcon;
    /** For a toggle: whether the Focus cell has what it sets. */
    readonly pressed: boolean;
    /** Why it cannot be pressed now, or `undefined` where it can. */
    readonly disabledReason: string | undefined;
    /** What a press does; `undefined` for an item that is never pressed, whose `disabledReason` says why. */
    readonly press: (() => void) | undefined;
}

/** What one pass shows: the rows of items, a separator between groups. */
export interface SheetToolbarPaint {
    readonly rows: readonly { readonly label: string; readonly items: readonly (SheetToolbarItem | 'separator')[] }[];
    readonly rowHeightPx: number;
    readonly name: string;
}

/** What the band tells the Sheet. */
export interface SheetToolbarHooks {
    /** The keyboard goes back to the Sheet: after a press from the keys, or Escape. */
    readonly giveBack: () => void;
}

const svgOpen = '<svg class="ex-sheet-toolbar-icon" viewBox="0 0 16 16" aria-hidden="true" focusable="false" fill="none" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round">';

// The C# Chrome's pictures, inline SVG in currentColor, so the stylesheet's Visual Tokens colour them.
const pictures: Readonly<Record<SheetToolbarIcon, string>> = {
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
};

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
    readonly #buttons = new Map<string, HTMLButtonElement>();
    #items: readonly SheetToolbarItem[] = [];
    // The item the keyboard is on while the band holds it: its key, so a repaint keeps it.
    #active: string | undefined;

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
            if (this.#activeItem() === undefined) this.#active = this.#enabled()[0]?.key;
            this.#showActive();
        });
        this.band.addEventListener('blur', () => { this.#showActive(); });
    }

    /** The band's height for `rows` rows of `rowHeightPx`. */
    static heightPx(rows: number, rowHeightPx: number): number {
        return rows * rowHeightPx;
    }

    paint(paint: SheetToolbarPaint): void {
        setAttr(this.band, 'aria-label', paint.name);
        setAttr(this.band, 'style', `height: ${String(SheetToolbar.heightPx(paint.rows.length, paint.rowHeightPx))}px; --ex-sheet-toolbar-row-height: ${String(paint.rowHeightPx)}px`);
        while (this.#rows.length > paint.rows.length) {
            const row = this.#rows.pop();
            if (row === undefined) break;
            this.#separators.delete(row);
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
                return this.#paintItem(item);
            });
            const current = [...element.children];
            if (current.length !== children.length || children.some((child, i) => current[i] !== child)) element.replaceChildren(...children);
        });
        for (const [key, button] of this.#buttons) {
            if (shown.has(key)) continue;
            this.#buttons.delete(key);
            button.remove();
        }
        this.#items = items;
        // The item the keyboard was on went, or can no longer be pressed: the nearest that can, from the start.
        if (this.#active !== undefined && this.#activeItem() === undefined) this.#active = this.#enabled()[0]?.key;
        this.#showActive();
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

    #paintItem(item: SheetToolbarItem): HTMLButtonElement {
        const button = this.#buttonOf(item);
        setAttr(button, 'title', item.disabledReason === undefined ? item.tooltip : `${item.tooltip}: ${item.disabledReason}`);
        setAttr(button, 'aria-label', item.name);
        if (item.kind === 'toggle') setAttr(button, 'aria-pressed', item.pressed ? 'true' : 'false');
        else button.removeAttribute('aria-pressed');
        const disabled = item.disabledReason !== undefined;
        if (button.disabled !== disabled) button.disabled = disabled;
        button.classList.toggle('ex-sheet-toolbar-pressed', item.kind === 'toggle' && item.pressed);
        if (button.dataset['exIcon'] !== item.icon) {
            button.dataset['exIcon'] = item.icon;
            button.innerHTML = `${svgOpen}${pictures[item.icon]}</svg>`;
        }
        return button;
    }

    #buttonOf(item: SheetToolbarItem): HTMLButtonElement {
        const known = this.#buttons.get(item.key);
        if (known !== undefined) return known;
        const button = this.#doc.createElement('button');
        button.type = 'button';
        button.className = 'ex-sheet-toolbar-item';
        button.id = `${this.#prefix}${item.key}`;
        button.tabIndex = -1;
        // A press keeps the keyboard where it is: on the Sheet, where the next key acts on what the command left.
        button.addEventListener('mousedown', preventDefault);
        button.addEventListener('click', () => { this.#pressOf(item.key)?.(); });
        this.#buttons.set(item.key, button);
        return button;
    }

    // The item by its key, as the last pass described it: a press acts on what is shown now, never on a closure
    // a pass replaced.
    #pressOf(key: string): (() => void) | undefined {
        return this.#items.find(shown => shown.key === key && shown.disabledReason === undefined)?.press;
    }

    #enabled(): SheetToolbarItem[] {
        return this.#items.filter(item => item.disabledReason === undefined);
    }

    #activeItem(): SheetToolbarItem | undefined {
        return this.#enabled().find(item => item.key === this.#active);
    }

    #showActive(): void {
        const active = this.holdsKeyboard ? this.#activeItem() : undefined;
        for (const [key, button] of this.#buttons) button.classList.toggle('ex-sheet-toolbar-active', key === active?.key);
        if (active === undefined) this.band.removeAttribute('aria-activedescendant');
        else setAttr(this.band, 'aria-activedescendant', `${this.#prefix}${active.key}`);
    }

    readonly #onKey = (event: KeyboardEvent): void => {
        if (event.isComposing || event.target !== this.band) return;
        if (event.ctrlKey || event.metaKey || event.altKey) return;
        const enabled = this.#enabled();
        const at = enabled.findIndex(item => item.key === this.#active);
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
                const press = this.#active === undefined ? undefined : this.#pressOf(this.#active);
                if (press === undefined) return;
                // The keyboard goes back before the press, so an item that opens what takes it (Format Cells) keeps it.
                this.#hooks.giveBack();
                press();
                break;
            }
            case 'Escape': this.#hooks.giveBack(); break;
            default: return;
        }
        event.preventDefault();
    };
}
