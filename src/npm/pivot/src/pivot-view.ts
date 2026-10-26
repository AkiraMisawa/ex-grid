// ExPivot's Script Core (ADR-0370 section 1): Excel's PivotTable, drawn by ExGrid as that grid's Consumer
// (ADR-0059). It holds the Pivot Layout and asks its report source; ExGrid paints, selects, navigates and
// copies. It owns the DOM inside its own root, writes the Razor component's DOM contract and reads ExPivot's
// stylesheet unchanged (ADR-0350 section 4), and depends on no UI framework.
//
// Every move is a question, and only the newest question's answer is shown: a move made while a question is
// out is taken against the layout that question asks for, and when that question is refused every move taken
// against it is refused with it, and the pane goes back to the layout the report shows (ADR-0370 section 3,
// spine rule 6). Nothing waits on a clock.

import type { GridColumnWidthChange, GridDensity, GridHandle, GridOptions, CellPosition, FetchingGridSource, ViewportSize } from '@ex-grid/core';
import {
    FetchingPivotSource, LocalPivotReportSource, defaultMaxLeaves, emptyPivotLayout, formatPivotCount, pivotValueCaptions, placementOf,
    pivotWordIdList, setCollapsed, validateLayout, type PivotDisplayRow, type PivotItemCounts, type PivotLabel, type PivotLayout,
    type PivotReportRefusal, type PivotReportSettings, type PivotReportState, type PivotSlicing, type PivotSource,
} from '@ex-grid/pivot-engine';
import { FieldListView, type FieldListEdit, type FieldListMove } from './field-list';
import { elementAt } from './must';
import { reportColumns, reportRows } from './report-grid';
import { PivotScreenWords } from './words';

/** A Pivot Layout the user made and the report now shows. */
export interface PivotLayoutChangedEvent {
    /** The layout the report shows. */
    readonly layout: PivotLayout;
}

/** The Field List shown or hidden by the user. */
export interface PivotFieldListShownEvent {
    /** Whether it now stands beside the report. */
    readonly shown: boolean;
}

/** A move the report could not take, said where the user sees it (ADR-0066, ADR-0370 section 3). */
export interface PivotRefusedEvent {
    /** The sentence the pivot shows. */
    readonly message: string;
    /** The fields the refusal names, in the order they were moved: every move taken against the refused
     * question, which went with it. Empty where it names none. */
    readonly fields: readonly string[];
}

/** An ExPivot's options (ADR-0370 section 1): what the Consumer hands, whole, on every update. */
export interface PivotViewOptions {
    /** The Pivot Source the report is computed from: its fields are what the Field List offers. A new
     * instance is a refresh. */
    readonly source: PivotSource;
    /** The Pivot Layout, View State the Consumer holds (ADR-0059). A layout handed in replaces the one on
     * screen; one this pivot raised through `onLayoutChanged` and handed back is the one on screen. Left out,
     * the pivot starts from nothing and keeps the user's changes itself. A layout naming a field the source
     * does not offer is refused by name. */
    readonly layout?: PivotLayout;
    /** Raised with the layout the report shows after each change the user made, once it is answered: never
     * for one a newer move superseded, nor for one refused. */
    readonly onLayoutChanged?: (event: PivotLayoutChangedEvent) => void;
    /** Raised when a move is refused, with the sentence the pivot shows. */
    readonly onRefused?: (event: PivotRefusedEvent) => void;
    /** The locale numbers, dates and counts are written in and text Items ordered by: a BCP 47 tag, or
     * `invariant`, which the pivot reads unless given (ADR-0370 section 10). A React adapter passes the
     * browser's language. It changes no word. */
    readonly locale?: string;
    /** The Consumer's word for an id, or `undefined` to keep the English (ADR-0370 section 10).
     * `japanesePivotWords` gives Excel's Japanese edition's words in one line. */
    readonly label?: PivotLabel;
    /** Whether the Field List stands beside the report; true unless given. */
    readonly showFieldList?: boolean;
    /** Raised when the user shows or hides the Field List. */
    readonly onShowFieldListChanged?: (event: PivotFieldListShownEvent) => void;
    /** The Field List's width in pixels; 300 unless given. */
    readonly fieldListWidth?: number;
    /** The report grid's height, header included: pixels, or `stretch` for the parent's; 400 unless given. */
    readonly viewportHeight?: ViewportSize;
    /** The report grid's width; `stretch` unless given, what the Field List leaves. */
    readonly viewportWidth?: ViewportSize;
    /** The most leaves a question may ask the source for (ADR-0066): 200,000 unless given. A layout past it is
     * refused by name, and the report stays on the layout before. */
    readonly maxLeaves?: number;
    /** The report's row height, or the Density's. */
    readonly rowHeight?: number;
    /** The report's Density. */
    readonly density?: GridDensity;
    /** How the report's work shares the thread: a slice of 30 ms between macrotasks unless given (PV-40). Read
     * once, when the pivot is made. */
    readonly slicing?: PivotSlicing;
}

/** Draws a grid into an element: ExGrid's `createGrid`, or a layer-2 test's own. */
export type MountGrid = (element: HTMLElement, options: GridOptions<PivotDisplayRow>) => GridHandle<PivotDisplayRow>;

/** Why a question was asked: a move of the user's, a layout the Consumer handed in, or new data. */
type Cause = 'user' | 'consumer' | 'data';

interface Question {
    readonly layout: PivotLayout;
    readonly generation: number;
    readonly controller: AbortController;
    readonly cause: Cause;
    /** The user's moves this question carries: its own, after those of the questions it superseded while they
     * were out, which were taken against them and go with it (ADR-0370 section 3). */
    readonly moves: readonly FieldListMove[];
    /** What it is asked of and under: the rows of its answer are asked of the same, with the same. */
    readonly reportSource: LocalPivotReportSource;
    readonly settings: PivotReportSettings;
    /** The locale its settings carry, which its counts and its report are written in. */
    readonly locale: string;
    readonly maxLeaves: number;
}

interface Shown {
    readonly layout: PivotLayout;
    /** The answer the report was drawn from; none for a layout with nothing to show. */
    readonly answer: PivotReportState | undefined;
}

// The rows a first answer brings: a screen's worth and more. The grid asks for the rest as it scrolls.
const firstWindowRows = 200;

// Whether a layout shows nothing: no field in Rows, Columns or Values, so there is nothing to compute, whatever
// stands in Filters (ADR-0059).
function showsNothing(layout: PivotLayout): boolean {
    return layout.rows.length === 0 && layout.columns.length === 0 && layout.values.length === 0;
}

function checkedOptions(options: PivotViewOptions): PivotViewOptions {
    // Checked as an untyped caller may hand them, where they arrive (CONVENTIONS.md).
    const given: Readonly<Record<string, unknown>> = { ...options };
    const source = given['source'];
    if (typeof source !== 'object' || source === null || !('fields' in source) || !('aggregate' in source)) {
        throw new TypeError('An ExPivot needs a Pivot Source (source) to compute its report from.');
    }
    const width = given['fieldListWidth'];
    if (width !== undefined && (typeof width !== 'number' || !Number.isFinite(width) || width <= 0 || width >= 10_000)) {
        throw new RangeError(`fieldListWidth is a width in pixels above zero, not ${JSON.stringify(width)}.`);
    }
    const maxLeaves = given['maxLeaves'];
    if (maxLeaves !== undefined && (typeof maxLeaves !== 'number' || !Number.isSafeInteger(maxLeaves) || maxLeaves < 1)) {
        throw new RangeError(`maxLeaves is a whole number of at least 1, not ${JSON.stringify(maxLeaves)} (ADR-0066).`);
    }
    const slicing = given['slicing'];
    if (slicing !== undefined && (typeof slicing !== 'object' || slicing === null)) throw new TypeError('slicing is an object.');
    for (const name of ['onLayoutChanged', 'onRefused', 'onShowFieldListChanged', 'label']) {
        const handler = given[name];
        if (handler !== undefined && typeof handler !== 'function') throw new TypeError(`${name} is a function.`);
    }
    const locale = given['locale'];
    if (locale !== undefined && (typeof locale !== 'string' || locale === '')) {
        throw new TypeError('locale is a BCP 47 tag or "invariant", never an empty tag (ADR-0370 section 10).');
    }
    return options;
}

// The Consumer's words as the report source takes them, plain data that crosses to a Worker as it is.
function wordsRecord(label: PivotLabel | undefined): Readonly<Record<string, string>> | undefined {
    if (label === undefined) return undefined;
    const words: Record<string, string> = {};
    for (const id of pivotWordIdList()) {
        const word = label(id);
        if (word !== undefined) words[id] = word;
    }
    return Object.freeze(words);
}

/** One ExPivot, drawn into its root. */
export class PivotView {
    readonly #root: HTMLElement;
    readonly #mountGrid: MountGrid;
    readonly #report: HTMLElement;
    readonly #notice: HTMLElement;
    readonly #fieldListToggle: HTMLButtonElement;
    readonly #sheet: HTMLElement;
    readonly #empty: HTMLElement;
    readonly #gridRoot: HTMLElement;
    readonly #dropRemove: HTMLElement;
    readonly #fieldListBox: HTMLElement;
    readonly #fieldList: FieldListView;
    readonly #userWidths = new Map<string, number>();
    // Read once, when the pivot is made: every report source it makes shares the thread the same way.
    readonly #slicing: PivotSlicing | undefined;
    #options: PivotViewOptions;
    #words: PivotScreenWords;
    #settings: PivotReportSettings;
    #reportSource: LocalPivotReportSource;
    // The report source the rows on screen read their Windows from: the one before a new source's answer lands.
    #shownReportSource: LocalPivotReportSource | undefined;
    // The layout last raised through onLayoutChanged. Handed back while a newer move of the user's is out, it is
    // the Consumer catching up, not a layout asked for; at any other time the Consumer's layout is the one shown.
    #raised: PivotLayout | undefined;
    // The locale of the report on screen, which the grid writes it in until another report replaces it.
    #shownLocale: string | undefined;
    #layoutGiven: PivotLayout | undefined;
    #shown: Shown;
    #asked: Question | undefined;
    #generation = 0;
    #requests = 0;
    #refusal: string | undefined;
    #fieldListShown: boolean;
    #counts: PivotItemCounts | undefined;
    #countsAsked: AbortController | undefined;
    #grid: GridHandle<PivotDisplayRow> | undefined;
    #rows: FetchingGridSource<PivotDisplayRow> | undefined;
    #dropRemoveShown = false;
    #destroyed = false;

    constructor(root: HTMLElement, options: PivotViewOptions, mountGrid: MountGrid) {
        this.#options = checkedOptions(options);
        this.#root = root;
        this.#mountGrid = mountGrid;
        this.#slicing = options.slicing;
        this.#words = new PivotScreenWords(options.label);
        this.#settings = this.#settingsOf(options);
        this.#fieldListShown = options.showFieldList !== false;
        this.#layoutGiven = options.layout;
        if (options.layout !== undefined) validateLayout(options.layout, this.#declared(options.source));
        this.#shown = { layout: emptyPivotLayout, answer: undefined };
        this.#reportSource = this.#reportSourceOf(options.source);

        const document = root.ownerDocument;
        const div = (className: string): HTMLDivElement => {
            const made = document.createElement('div');
            made.className = className;
            return made;
        };
        root.replaceChildren();
        root.classList.add('ex-pivot');
        // The pivot's box is its Input Root (ADR-0240 section 6): the report grid hears the whole pivot from here.
        root.setAttribute('data-ex-input-root', '');
        this.#report = div('ex-pivot-report');
        const toolbar = div('ex-pivot-toolbar');
        const start = div('ex-pivot-toolbar-start');
        const end = div('ex-pivot-toolbar-end');
        this.#fieldListToggle = document.createElement('button');
        this.#fieldListToggle.type = 'button';
        this.#fieldListToggle.className = 'ex-pivot-toolbar-button ex-pivot-field-list-toggle';
        this.#fieldListToggle.setAttribute('data-ex-claims', 'click');
        this.#fieldListToggle.addEventListener('click', () => { this.#setFieldListShown(!this.#fieldListShown); });
        end.append(this.#fieldListToggle);
        toolbar.append(start, end);
        this.#notice = div('ex-pivot-refusal-notice');
        this.#notice.setAttribute('role', 'alert');
        this.#sheet = div('ex-pivot-sheet');
        this.#sheet.addEventListener('dragover', event => {
            if (!this.#fieldList.removable) return;
            event.preventDefault();
        });
        this.#sheet.addEventListener('drop', event => {
            event.preventDefault();
            this.#fieldList.dropOnReport();
        });
        this.#empty = div('ex-pivot-empty');
        this.#empty.setAttribute('role', 'status');
        this.#gridRoot = document.createElement('div');
        this.#dropRemove = div('ex-pivot-report-drop-remove');
        this.#dropRemove.setAttribute('aria-hidden', 'true');
        this.#dropRemove.append(document.createElement('span'));
        this.#report.append(toolbar, this.#notice, this.#sheet);
        this.#fieldListBox = div('ex-pivot-field-list');
        this.#fieldListBox.setAttribute('role', 'region');
        const scroll = div('ex-pivot-field-list-scroll');
        this.#fieldListBox.append(scroll);
        this.#fieldList = new FieldListView(scroll, {
            edit: edit => { this.#edited(edit); },
            close: () => { this.#setFieldListShown(false); },
            dragging: removable => { this.#paintDropRemove(removable); },
        });
        root.append(this.#report, this.#fieldListBox);
        // A drag that moves on from an Area, anywhere in the pivot, leaves it unmarked; and one that leaves the
        // pivot for an element the browser names. WebKit names none on leaving one element for another, so a
        // leave that names nothing is not taken as one.
        root.addEventListener('dragenter', event => { this.#fieldList.dragEntered(event.target); });
        root.addEventListener('dragleave', event => {
            const to = event.relatedTarget;
            if (to instanceof Node && !root.contains(to)) this.#fieldList.leaveTargets();
        });

        this.#askCounts();
        if (options.layout !== undefined) this.#ask(options.layout, 'consumer', undefined);
        this.#draw();
    }

    #reportSourceOf(source: PivotSource): LocalPivotReportSource {
        const slicing = this.#slicing;
        return new LocalPivotReportSource(source, slicing === undefined ? undefined : { slicing });
    }

    // A report source is disposed once nothing asks it and no row on screen reads it.
    #release(reportSource: LocalPivotReportSource | undefined): void {
        if (reportSource !== undefined && reportSource !== this.#reportSource && reportSource !== this.#shownReportSource) reportSource.dispose();
    }


    /** Takes the options again, whole. Options refused leave the pivot as it was. */
    update(options: PivotViewOptions): void {
        if (this.#destroyed) throw new Error('This ExPivot was destroyed: make a new one.');
        checkedOptions(options);
        const before = this.#options;
        const sourceMoved = options.source !== before.source;
        // A layout handed in is a question, unless the pane holds it already, or it is the one this pivot last
        // raised, handed back while the user's newer move is out.
        const layoutMoved = options.layout !== this.#layoutGiven;
        const echo = options.layout === this.#raised && this.#asked?.cause === 'user';
        const consumerLayout = layoutMoved && options.layout !== undefined && options.layout !== this.#paneLayout && !echo
            ? options.layout
            : undefined;
        const asked = consumerLayout ?? (sourceMoved ? this.#paneLayout : undefined);
        // Checked against the source it will be asked of before anything changes.
        if (asked !== undefined) validateLayout(asked, this.#declared(options.source));
        this.#options = options;
        if (layoutMoved) this.#layoutGiven = options.layout;
        const settingsMoved = options.locale !== before.locale || options.label !== before.label;
        if (options.label !== before.label) this.#words = new PivotScreenWords(options.label);
        if (settingsMoved) this.#settings = this.#settingsOf(options);
        if (options.showFieldList !== before.showFieldList) this.#fieldListShown = options.showFieldList !== false;
        if (sourceMoved) {
            // The rows on screen keep reading the report source that answered them until a new answer replaces them.
            const replaced = this.#reportSource;
            this.#reportSource = this.#reportSourceOf(options.source);
            this.#release(replaced);
            this.#counts = undefined;
            this.#askCounts();
        }
        if (asked !== undefined) {
            this.#ask(asked, consumerLayout !== undefined ? 'consumer' : 'data', undefined);
        } else if (settingsMoved || options.maxLeaves !== before.maxLeaves) {
            this.#ask(this.#paneLayout, 'consumer', undefined);
        }
        if (options.rowHeight !== before.rowHeight || options.density !== before.density || options.viewportHeight !== before.viewportHeight
            || options.viewportWidth !== before.viewportWidth) {
            this.#drawGrid();
        }
        this.#draw();
    }

    /** Stops the pivot: it asks and hears nothing more, and empties its element. The source stays the
     * Consumer's. */
    destroy(): void {
        if (this.#destroyed) return;
        this.#destroyed = true;
        this.#generation++;
        this.#asked?.controller.abort();
        this.#asked = undefined;
        this.#countsAsked?.abort();
        this.#grid?.destroy();
        this.#grid = undefined;
        this.#rows?.dispose();
        this.#rows = undefined;
        const asked = this.#reportSource;
        const read = this.#shownReportSource;
        this.#shownReportSource = undefined;
        asked.dispose();
        if (read !== asked) read?.dispose();
        this.#root.replaceChildren();
        this.#root.classList.remove('ex-pivot');
        this.#root.removeAttribute('data-ex-input-root');
        this.#root.removeAttribute('style');
        this.#root.removeAttribute('aria-busy');
    }

    /** The layout the report on screen was laid out under. */
    get shownLayout(): PivotLayout {
        return this.#shown.layout;
    }

    // The pane's layout: the one the newest question asks for, or the one the report shows.
    get #paneLayout(): PivotLayout {
        return this.#asked?.layout ?? this.#shown.layout;
    }

    get #maxLeaves(): number {
        return this.#options.maxLeaves ?? defaultMaxLeaves;
    }

    get #locale(): string {
        return this.#localeOf(this.#options);
    }

    #settingsOf(options: PivotViewOptions): PivotReportSettings {
        const words = wordsRecord(options.label);
        return Object.freeze({ locale: this.#localeOf(options), ...(words === undefined ? {} : { words }) });
    }

    #localeOf(options: PivotViewOptions): string {
        return options.locale ?? 'invariant'; // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0370 section 10: a Script Core given no locale reads as .NET's InvariantCulture does
    }

    #declared(source: PivotSource): ReadonlyMap<string, unknown> {
        return new Map(source.fields.map(field => [field.name, field]));
    }

    // ---- Asking ----------------------------------------------------------------------------------------

    #edited(edit: FieldListEdit): void {
        // A gesture taken against a layout that is no longer the pane's means nothing now: its indexes named the
        // entries of a layout that was replaced (ADR-0061, spine rule 6).
        if (edit.against !== this.#paneLayout) {
            this.#draw();
            return;
        }
        this.#move(edit.layout, edit.moved);
    }

    #move(layout: PivotLayout, moved: FieldListMove | undefined): void {
        this.#refusal = undefined;
        this.#ask(layout, 'user', moved);
        this.#draw();
    }

    #ask(layout: PivotLayout, cause: Cause, moved: FieldListMove | undefined): void {
        const superseded = this.#asked;
        superseded?.controller.abort();
        // A user's move made while a user's question was out was taken against it, and carries its moves on.
        const moves = [...(cause === 'user' && superseded?.cause === 'user' ? superseded.moves : []), ...(moved === undefined ? [] : [moved])];
        const generation = ++this.#generation;
        if (showsNothing(layout)) {
            // Nothing to compute: the report is the empty box that says how to start.
            this.#asked = undefined;
            this.#showLayout(layout, undefined, cause);
            return;
        }
        const question: Question = {
            layout, generation, controller: new AbortController(), cause, moves: Object.freeze(moves),
            reportSource: this.#reportSource, settings: this.#settings, locale: this.#locale, maxLeaves: this.#maxLeaves,
        };
        this.#asked = question;
        void this.#run(question);
    }

    async #run(question: Question): Promise<void> {
        const current = (): boolean => question.generation === this.#generation && !this.#destroyed;
        let update;
        try {
            update = await question.reportSource.window(Object.freeze({
                requestId: this.#nextRequestId(),
                layout: question.layout,
                settings: question.settings,
                window: Object.freeze({ start: 0, count: firstWindowRows }),
                maxLeaves: question.maxLeaves,
                markChanges: question.cause === 'data',
            }), question.controller.signal);
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a superseded question's failure is nobody's; the newest one's is said where the user sees it and rethrown
            if (!current()) return;
            this.#asked = undefined;
            this.#refusal = this.#words.fill('source-failed', error instanceof Error ? error.message : String(error));
            this.#draw();
            throw error;
        }
        // Late and superseded: a newer question was asked, and this answers a layout no longer wanted.
        if (!current()) return;
        this.#asked = undefined;
        if (update.refusal !== undefined) {
            this.#refuse(update.refusal, question);
            return;
        }
        this.#showLayout(question.layout, { answer: update, question }, question.cause);
    }

    #nextRequestId(): string {
        this.#requests++;
        return `ex-pivot-${this.#requests}`;
    }

    #showLayout(layout: PivotLayout, answered: { readonly answer: PivotReportState; readonly question: Question } | undefined, cause: Cause): void {
        const answer = answered?.answer;
        this.#shown = { layout, answer };
        this.#showReport(answered);
        if (answer !== undefined && this.#counts !== undefined && this.#counts.sourceVersion !== answer.metadata.sourceVersion) {
            // Counted under another Source Version: asked again, so the marks follow the data (ADR-0370 section 3).
            this.#askCounts();
        }
        this.#draw();
        if (cause === 'user') {
            this.#raised = layout;
            this.#options.onLayoutChanged?.(Object.freeze({ layout }));
        }
    }

    // A refused question: the pane goes back to the layout the report shows, and the sentence says why. Every
    // move taken against the refused layout went with it, and is named (ADR-0370 section 3).
    #refuse(refusal: PivotReportRefusal, question: Question): void {
        const { message, fields } = this.#sentenceFor(refusal, question);
        this.#refusal = message;
        this.#draw();
        this.#options.onRefused?.(Object.freeze({ message, fields: Object.freeze(fields) }));
    }

    #sentenceFor(refusal: PivotReportRefusal, question: Question): { readonly message: string; readonly fields: readonly string[] } {
        // The moves the refused layout still holds: a field moved and then taken out again is not in it.
        // A field may stand in Values as well as in an axis, so a move is known by its field and its Area.
        const held = question.moves.filter((move, i, all) => all.findLastIndex(later => later.field === move.field && later.area === move.area) === i
            && (move.area === 'values' ? question.layout.values.some(value => value.field === move.field) : placementOf(question.layout, move.field)?.area === move.area));
        const named = [...new Set(held.map(move => move.field))];
        const source = refusal.sourceRefusal;
        if (source?.kind !== 'tooManyLeaves') return { message: refusal.message, fields: refusal.field === undefined ? named : [refusal.field] };
        // A source that names no limit stopped at the cap it was asked with (ADR-0066).
        const { locale } = question;
        const cap = formatPivotCount(source.limit ?? question.maxLeaves, locale);
        // Server data is refused naming each field moved into Rows or Columns, which make the combinations
        // (ADR-0370 section 3); data the pivot computes itself is refused by the cells the layout needs, as the
        // Razor component says it.
        const combining = held.filter(move => move.area === 'rows' || move.area === 'columns');
        if (!(this.#options.source instanceof FetchingPivotSource) || combining.length === 0) {
            return { message: this.#words.fill('refused-too-many-cells', cap), fields: named };
        }
        const sentences = combining.map(move => {
            const caption = this.#captionOf(move.field);
            const area = this.#words.area(move.area);
            const count = this.#counts?.countOf(move.field);
            const sentence = count === undefined
                ? this.#words.fill('refused-too-many-combinations', caption, area, cap)
                : this.#words.fill('refused-too-many-combinations-counted', caption, formatPivotCount(count, locale), area, cap);
            return `${sentence} ${this.#words.fill('refused-too-many-combinations-remedy', caption)}`;
        });
        return { message: sentences.join(' '), fields: named };
    }

    #captionOf(field: string): string {
        const found = this.#options.source.fields.find(f => f.name === field);
        if (found === undefined) throw new Error(`Internal: the field '${field}' is not the source's.`);
        return found.caption;
    }

    // Each field's count of Items, asked once when the source is opened and again under a new Source Version,
    // of a source that declares it counts them (ADR-0370 section 3). A source that does not gets no count.
    #askCounts(): void {
        this.#countsAsked?.abort();
        const source = this.#options.source;
        if (!source.features.countsItems || source.itemCounts === undefined) {
            this.#countsAsked = undefined;
            return;
        }
        const controller = new AbortController();
        this.#countsAsked = controller;
        void this.#countItems(source, controller);
    }

    async #countItems(source: PivotSource, controller: AbortController): Promise<void> {
        let counts;
        try {
            if (source.itemCounts === undefined) throw new Error('Internal: a source counted without counting Items.');
            counts = await source.itemCounts(controller.signal);
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a count superseded is nobody's; one that failed leaves the fields uncounted, said by rethrowing
            if (controller.signal.aborted || this.#destroyed) return;
            this.#countsAsked = undefined;
            throw error;
        }
        if (controller.signal.aborted || this.#destroyed || source !== this.#options.source) return;
        this.#countsAsked = undefined;
        this.#counts = counts;
        this.#draw();
    }

    // ---- The report ------------------------------------------------------------------------------------

    #showReport(answered: { readonly answer: PivotReportState; readonly question: Question } | undefined): void {
        this.#rows?.dispose();
        this.#rows = undefined;
        // The rows of the report before read a source that may since have been replaced.
        const before = this.#shownReportSource;
        this.#shownReportSource = answered?.question.reportSource;
        this.#release(before);
        if (answered === undefined) {
            this.#grid?.destroy();
            this.#grid = undefined;
            return;
        }
        const { answer, question } = answered;
        this.#shownLocale = question.locale;
        this.#rows = reportRows({
            source: question.reportSource,
            layout: question.layout,
            settings: question.settings,
            maxLeaves: question.maxLeaves,
            answered: answer,
            nextRequestId: () => this.#nextRequestId(),
            moved: () => { this.#reportMoved(answer); },
        });
        this.#drawGrid();
    }

    // The source answered a Window from another report than the one shown: the data moved on under it, and the
    // report on screen is asked again under its layout, unless something newer is asked already.
    #reportMoved(answer: PivotReportState): void {
        if (this.#destroyed || this.#shown.answer !== answer || this.#asked !== undefined) return;
        this.#ask(this.#shown.layout, 'data', undefined);
        this.#draw();
    }

    #drawGrid(): void {
        const answer = this.#shown.answer;
        const rows = this.#rows;
        const locale = this.#shownLocale;
        if (answer === undefined || rows === undefined || locale === undefined) return;
        const { columns, headerGroups, pinnedColumnCount } = reportColumns(answer.metadata, this.#userWidths);
        const { rowHeight, density, label } = this.#options;
        const options: GridOptions<PivotDisplayRow> = {
            columns,
            headerGroups,
            pinnedColumnCount,
            source: rows,
            locale,
            viewportHeight: this.#options.viewportHeight ?? 400, // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0028: the report's declared default height, as the Razor component's
            viewportWidth: this.#options.viewportWidth ?? 'stretch', // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0028: the report takes what the Field List leaves unless told
            // Item order and Hidden Items are the Pivot Layout's, and a report is computed: no column menu, no
            // sort, no filter, and every write refused by the grid's own gate, since nobody hears one (ADR-0059).
            hideColumnMenu: true,
            onColumnWidthChanged: change => { this.#widthChanged(change); },
            onCellDoubleClick: cell => { this.#doubleClicked(cell); },
            ...(label === undefined ? {} : { commandLabel: label }),
            ...(rowHeight === undefined ? {} : { rowHeight }),
            ...(density === undefined ? {} : { density }),
        };
        if (this.#grid === undefined) this.#grid = this.#mountGrid(this.#gridRoot, options);
        else this.#grid.update(options);
    }

    // A width the user dragged a column to is the Consumer's to hand back (ADR-0016): the pivot keeps it, by the
    // column's name, for every report that has the column.
    #widthChanged(change: GridColumnWidthChange): void {
        this.#userWidths.set(change.column, change.widthPx);
        this.#drawGrid();
    }

    // A double click on an outer Item's label expands or collapses it, as Excel's does (ADR-0063).
    #doubleClicked(cell: CellPosition): void {
        const answer = this.#shown.answer;
        const rows = this.#rows;
        if (answer === undefined || rows === undefined || cell.column >= answer.metadata.labelColumns.length) return;
        const state = rows.snapshot();
        const row = state.window[cell.row - state.windowStart];
        if (row === undefined) return;
        const toggle = elementAt(row.labels, cell.column).toggle;
        if (toggle === undefined) return;
        this.#move(setCollapsed(this.#paneLayout, toggle.field, toggle.item, !toggle.isCollapsed), undefined);
    }

    // ---- Drawing ---------------------------------------------------------------------------------------

    #setFieldListShown(shown: boolean): void {
        if (shown === this.#fieldListShown) return;
        this.#fieldListShown = shown;
        this.#draw();
        this.#options.onShowFieldListChanged?.(Object.freeze({ shown }));
    }

    #paintDropRemove(removable: boolean): void {
        this.#dropRemoveShown = removable;
        this.#draw();
    }

    #draw(): void {
        if (this.#destroyed) return;
        const words = this.#words;
        const stretch = this.#options.viewportHeight === 'stretch';
        const width = this.#options.fieldListWidth ?? 300; // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0061: the Field List's declared default width, as the Razor component's
        // The Geometry Tokens, inline on the root (ADR-0027): the Field List's width and a level's indent.
        this.#root.setAttribute('style', `--ex-pivot-field-list-width: ${width}px; --ex-pivot-indent: 16px${stretch ? '; height: 100%; flex: 1 1 auto; min-height: 0' : ''}`);
        if (this.#asked === undefined) this.#root.removeAttribute('aria-busy');
        else this.#root.setAttribute('aria-busy', 'true');
        this.#fieldListToggle.textContent = words.word('field-list-toggle');
        this.#fieldListToggle.setAttribute('aria-pressed', this.#fieldListShown ? 'true' : 'false');
        if (this.#notice.textContent !== (this.#refusal ?? '')) this.#notice.textContent = this.#refusal ?? ''; // oxlint-disable-line ex-grid/no-stand-in-default -- no refusal is an empty notice, which takes no room
        this.#notice.hidden = this.#refusal === undefined;
        this.#sheet.setAttribute('style', stretch ? 'display: flex; flex-direction: column; flex: 1 1 auto; min-height: 0' : '');
        const answered = this.#shown.answer !== undefined;
        // The report or the box that says why there is none, and over the report, while a placed field is
        // dragged, the mark that a drop there removes it (ADR-0061). Elements come and go rather than hide, since
        // the stylesheet gives the box a display of its own.
        const dropRemove = answered && this.#dropRemoveShown;
        elementAt([...this.#dropRemove.children], 0).textContent = words.word('remove-field');
        const shownInSheet = [answered ? this.#gridRoot : this.#empty, ...(dropRemove ? [this.#dropRemove] : [])];
        if (shownInSheet.length !== this.#sheet.children.length || shownInSheet.some((child, i) => this.#sheet.children[i] !== child)) {
            this.#sheet.replaceChildren(...shownInSheet);
        }
        if (!answered) {
            const pane = this.#paneLayout;
            this.#empty.textContent = words.word(this.#asked !== undefined && !showsNothing(pane) ? 'loading' : 'empty-report');
            const height = this.#options.viewportHeight ?? 400; // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0028: the report's declared default height, as the Razor component's
            this.#empty.setAttribute('style', height === 'stretch' ? 'flex: 1 1 auto; min-height: 0' : `height: ${height}px`);
        }
        this.#fieldListBox.hidden = !this.#fieldListShown;
        this.#fieldListBox.setAttribute('aria-label', words.word('field-list'));
        if (this.#fieldListShown) {
            const layout = this.#paneLayout;
            const fields = this.#options.source.fields;
            this.#fieldList.draw({
                layout,
                fields,
                valueCaptions: pivotValueCaptions(layout, fields, { locale: this.#locale, ...(this.#words.label === undefined ? {} : { label: this.#words.label }) }),
                counts: this.#counts,
                maxLeaves: this.#maxLeaves,
                locale: this.#locale,
                words,
            });
        }
    }
}
