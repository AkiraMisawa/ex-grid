// The list of a Find All (ADR-0470 §3): drawn by the core under the find panel's contents in every Chrome, a
// virtualised listbox whose line height is the grid's row height from the Grid Metrics, so nothing is measured (rule 2)
// and only the lines in view stand in the DOM, however many cells matched. The list decides nothing: which lines it
// shows, which is active and what a choice does are the grid's, read through its model; it draws them and says what
// the user did.

/** One line of a Find All's list: where the match is, and the text it displays. */
export interface GridFindAllLine {
    /** Where the match is, in the grid's words: "Row 12, Trader", "Header Trader", "Sheet2!B12". */
    readonly where: string;
    /** The matched cell's displayed text. */
    readonly text: string;
}

/**
 * A Find All list a find panel slot's contents hold themselves (ADR-0470 §7), handed to the core to draw as it draws
 * its own: ExSheet's over every Sheet of its Workbook. The contents answer how many lines it holds, each line, and
 * what choosing one does; the core draws the count and the lines, takes the list's keys and clicks in their place in
 * the Input Order, and takes the list away when the text or an option changes or the panel closes.
 */
export interface GridFindAllList {
    /** How many lines the list holds: every match, never capped. */
    readonly count: number;
    /** Line `index`, from 0 to `count` − 1. */
    readonly line: (index: number) => GridFindAllLine;
    /** The user chose line `index`, in its turn: the contents move to it, as a step that found it would. */
    readonly choose: (index: number) => void;
}

/** What the list reads from the grid, and what it says back. */
export interface FindAllListModel {
    /** How many lines the list holds, or `undefined` while the first page is out. */
    readonly count: () => number | undefined;
    /** Line `index`, or `undefined` while its page is not yet read. */
    readonly line: (index: number) => GridFindAllLine | undefined;
    /** The line chosen last, the current match, or none. */
    readonly active: () => number | undefined;
    /** The lines from `first` to `last` are in view: the grid reads them, and ahead of them. */
    readonly inView: (first: number, last: number) => void;
    /** The user chose line `index`: a click, Enter, or ↑, ↓, Home or End moving onto it. */
    readonly choose: (index: number) => void;
    /** An input reached the list and was answered (ADR-0220). */
    readonly answered: () => void;
    /** The list's accessible name. */
    readonly label: string;
}

/** How many lines the list shows at once: Excel's list shows about this many under its dialog. */
export const findAllVisibleLines = 8;

/** The DOM id prefix of a list's lines, numbered per list so that two grids' lists never share one. */
let lists = 0;

/** The list, made once per Find All shown and refreshed as the grid's model moves: the count line, `.ex-find-all-count`,
 * a live region (ADR-0033), over the listbox, `.ex-find-all-list`. */
export class FindAllList {
    /** The part's box, `.ex-find-all`, which the panel places under its contents. */
    readonly element: HTMLElement;
    /** The listbox, which takes the keyboard for the list's keys. */
    readonly list: HTMLElement;
    readonly #countLine: HTMLElement;
    readonly #doc: Document;
    readonly #model: FindAllListModel;
    readonly #lineHeightPx: number;
    readonly #spacer: HTMLElement;
    readonly #id: string;
    #scrollTopPx = 0;
    // The lines drawn, by index, so a refresh rewrites only what changed.
    readonly #drawn = new Map<number, HTMLElement>();

    constructor(doc: Document, model: FindAllListModel, lineHeightPx: number) {
        if (!(lineHeightPx > 0)) throw new RangeError(`A Find All list's lines are taller than 0px, not ${lineHeightPx}.`);
        this.#doc = doc;
        this.#model = model;
        this.#lineHeightPx = lineHeightPx;
        lists += 1;
        this.#id = `ex-find-all-${lists}`;
        this.element = doc.createElement('div');
        this.element.className = 'ex-find-all';
        this.#countLine = doc.createElement('div');
        this.#countLine.className = 'ex-find-all-count';
        this.#countLine.setAttribute('role', 'status');
        const element = doc.createElement('div');
        element.className = 'ex-find-all-list';
        element.setAttribute('role', 'listbox');
        element.setAttribute('aria-label', model.label);
        element.tabIndex = 0;
        element.setAttribute('data-ex-claims', 'keydown click');
        element.style.height = `${findAllVisibleLines * lineHeightPx}px`;
        this.#spacer = doc.createElement('div');
        this.#spacer.className = 'ex-find-all-spacer';
        element.append(this.#spacer);
        // Told where the list stands as it scrolls, as the grid's Viewport is (rule 2): the event says it moved.
        element.addEventListener('scroll', () => {
            if (element.scrollTop === this.#scrollTopPx) return;
            this.#scrollTopPx = element.scrollTop;
            this.refresh();
        });
        element.addEventListener('click', event => {
            const line = event.target instanceof Element ? event.target.closest('.ex-find-all-line') : null;
            if (!(line instanceof HTMLElement) || !element.contains(line)) return;
            const index = Number(line.dataset['exIndex']);
            if (!Number.isInteger(index)) throw new Error('Internal: a Find All line without its index.');
            model.answered();
            if (model.line(index) !== undefined) model.choose(index);
        });
        element.addEventListener('keydown', event => {
            if (event.ctrlKey || event.altKey || event.metaKey || event.isComposing) return;
            const target = this.#keyTarget(event.key);
            if (target === undefined) return;
            event.preventDefault();
            model.answered();
            if (target >= 0) model.choose(target);
        });
        this.list = element;
        this.element.append(this.#countLine, element);
        this.refresh();
    }

    /** What the count line says: how many cells were found, or why the list went (ADR-0470 §5). */
    say(text: string): void {
        if (this.#countLine.textContent !== text) this.#countLine.textContent = text;
    }

    // The line a key moves to (ADR-0470 §4), -1 for a key the list takes that moves nowhere, or `undefined` for a key
    // that is not the list's.
    #keyTarget(key: string): number | undefined {
        const count = this.#model.count();
        if (!['ArrowDown', 'ArrowUp', 'Home', 'End', 'Enter', 'PageDown', 'PageUp'].includes(key)) return undefined;
        if (count === undefined || count === 0) return -1;
        const active = this.#model.active();
        const last = count - 1;
        const at = (index: number): number => this.#model.line(index) === undefined ? -1 : index;
        switch (key) {
            case 'ArrowDown': return at(active === undefined ? 0 : Math.min(last, active + 1));
            case 'ArrowUp': return at(active === undefined ? 0 : Math.max(0, active - 1));
            case 'PageDown': return at(active === undefined ? 0 : Math.min(last, active + findAllVisibleLines));
            case 'PageUp': return at(active === undefined ? 0 : Math.max(0, active - findAllVisibleLines));
            case 'Home': return at(0);
            case 'End': return at(last);
            default: return active === undefined ? -1 : at(active);
        }
    }

    /** Draws the lines in view as the model now holds them, and brings the active line into view. */
    refresh(): void {
        const count = this.#model.count();
        const lines = count === undefined ? 0 : count;
        this.#spacer.style.height = `${lines * this.#lineHeightPx}px`;
        const active = this.#model.active();
        if (active !== undefined && active < lines) this.#reveal(active);
        const first = Math.min(Math.max(0, lines - 1), Math.floor(this.#scrollTopPx / this.#lineHeightPx));
        const last = Math.min(lines - 1, first + findAllVisibleLines);
        for (const [index, line] of this.#drawn) {
            if (index < first || index > last) {
                line.remove();
                this.#drawn.delete(index);
            }
        }
        for (let index = first; index <= last; index++) this.#draw(index, index === active);
        this.list.hidden = lines === 0;
        if (active !== undefined && active < lines) this.list.setAttribute('aria-activedescendant', `${this.#id}-${active}`);
        else this.list.removeAttribute('aria-activedescendant');
        if (lines > 0) this.#model.inView(first, last);
    }

    // The active line scrolled into view, as far as it must, as the grid reveals its Focus.
    #reveal(index: number): void {
        const top = index * this.#lineHeightPx;
        const room = findAllVisibleLines * this.#lineHeightPx;
        let scrollTop = this.#scrollTopPx;
        if (top < scrollTop) scrollTop = top;
        else if (top + this.#lineHeightPx > scrollTop + room) scrollTop = top + this.#lineHeightPx - room;
        if (scrollTop === this.#scrollTopPx) return;
        this.#scrollTopPx = scrollTop;
        this.list.scrollTop = scrollTop;
    }

    #draw(index: number, active: boolean): void {
        let line = this.#drawn.get(index);
        if (line === undefined) {
            line = this.#doc.createElement('div');
            line.className = 'ex-find-all-line';
            line.id = `${this.#id}-${index}`;
            line.setAttribute('role', 'option');
            line.dataset['exIndex'] = String(index);
            line.style.top = `${index * this.#lineHeightPx}px`;
            line.style.height = `${this.#lineHeightPx}px`;
            const where = this.#doc.createElement('span');
            where.className = 'ex-find-all-where';
            const text = this.#doc.createElement('span');
            text.className = 'ex-find-all-text';
            line.append(where, text);
            this.#spacer.append(line);
            this.#drawn.set(index, line);
        }
        const read = this.#model.line(index);
        const [where, text] = [line.children[0], line.children[1]];
        if (where === undefined || text === undefined) throw new Error('Internal: a Find All line without its two parts.');
        const whereText = read === undefined ? '' : read.where;
        const shownText = read === undefined ? '' : read.text;
        if (where.textContent !== whereText) where.textContent = whereText;
        if (text.textContent !== shownText) text.textContent = shownText;
        if (read === undefined) line.setAttribute('aria-busy', 'true');
        else line.removeAttribute('aria-busy');
        line.setAttribute('aria-selected', active ? 'true' : 'false');
        line.classList.toggle('ex-find-all-active', active);
    }
}
