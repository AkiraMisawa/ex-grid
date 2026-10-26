// The Sheets ExSheet holds, in order, under one culture, with one dependency graph across them (C#'s Workbook.cs and
// its partials, WorkbookChange and WorkbookStep; CONTEXT.md, ADR-0331).
import type { CellRange } from './cell-range';
import type { Culture } from './culture';
import { serialFromDate, daysInMonth } from './date-serial';
import { equalsIgnoreCase, ignoreCaseKey } from './dotnet';
import { Entry } from './entry';
import { elementAt } from './must';
import type { SheetDay, SheetMoment } from './formulas/cell-reader';
import type { Reference } from './formulas/reference';
import { Sheet, type LinkedTable, type RewrittenEntry, type TableState } from './sheet';
import { SheetCell, SheetCellSet } from './sheet-cell';
import { SheetChange, SheetRefusal, SheetRefusedError } from './sheet-change';
import { SheetDocument, sheetDocumentOf } from './sheet-document';
import { SheetEdit, WorkbookEdit, type WorkbookEditDone } from './sheet-edit';
import type { Value } from './value';
import { WorkbookDocument } from './workbook-document';

/** What a rename did, and the Formulas it rewrote on every Sheet, as they were, for undoing it. @internal */
export interface SheetRename {
    readonly change: SheetChange;
    readonly oldName: string;
    readonly rewritten: readonly RewrittenEntry[];
}

/** The largest seed: a seed is an unsigned 64-bit integer, as C#'s ulong holds it (ADR-0180). */
const maxSeed = (1n << 64n) - 1n;

/** The number at the end of a copy's name, which a copy of the copy replaces: ` (2)`. */
const copyNumber = / \([0-9]+\)$/u;

/**
 * The Sheets ExSheet holds, in order, under one culture, with one dependency graph across them (CONTEXT.md, ADR-0331).
 * A Formula on one Sheet reads another's cells by qualifying a Reference with its name (`=Sheet2!A1`), and a change on
 * any Sheet recalculates what depends on it on every Sheet, publishing the new Values only when that whole
 * recalculation has completed (ADR-0047). Linked Tables are declared on the Workbook, and read from any of its Sheets.
 * Changed and read by one caller at a time.
 */
export class Workbook {
    readonly #sheets: Sheet[] = [];

    /** What a Workbook operation under way has changed on the Sheets it did not start on; `undefined` when none is under way. */
    #collected: Map<Sheet, SheetChange[]> | undefined;

    /** Whether the Workbook operation under way has changed which Sheets the Workbook holds, their order or their names. */
    #reshaped = false;

    /**
     * How many Sheets the Workbook has made since it was created or opened, which numbers a new Sheet
     * (`nextSheetName`), as Excel's count does: an opened one has made none; a new one has made the Sheets it starts with.
     */
    #sheetsMade: number;

    #shown: Sheet;

    /** How deep the operations under way are nested: one operation counts as one change, however many steps it takes. */
    #operationDepth = 0;

    /** Whether the operation under way has counted its change already. */
    #operationCounted = false;

    #today: SheetDay | undefined;
    #seed: bigint | undefined;
    #changes = 0;

    /** The Workbook's declared culture, which every one of its Sheets reads and shows under (ADR-0048, ADR-0331). */
    readonly culture: Culture;

    /** The Linked Tables' state, which every Sheet of the Workbook reads, by name read without regard to case (ADR-0331). @internal */
    readonly tables = new Map<string, TableState>();

    /**
     * Where the engine reads the current moment for `NOW`: the date and time of day in the Workbook's zone, or
     * `undefined` while it is not known, when `NOW` is `#GETTING_DATA` (ADR-0124). The engine reads no clock itself; it
     * calls this once at the start of each recalculation, so every `NOW` on every Sheet in one recalculation answers the
     * same moment. Unset, the default, leaves `NOW` waiting.
     */
    nowSource: (() => SheetMoment | undefined) | undefined;

    /**
     * A Workbook of empty Sheets named `sheetNames`, in that order — one, `Sheet1`, when none are given — whose constants
     * are read and whose Values are shown under `culture` (ADR-0048). Throws a RangeError when no name is given, a name
     * is not one a Sheet can have (`Sheet.isValidName`), or two names are the same without regard to case.
     */
    constructor(culture: Culture, sheetNames: Iterable<string> = [Sheet.defaultName]) {
        this.culture = culture;
        for (const name of sheetNames) {
            Sheet.checkName(name);
            if (this.find(name) !== undefined) throw new RangeError(`Two Sheets cannot both be named '${name}'.`);
            this.#sheets.push(new Sheet(this, name));
        }
        const first = this.#sheets[0];
        if (first === undefined) throw new RangeError('A Workbook holds at least one Sheet.');
        this.#shown = first;
        this.#sheetsMade = this.#sheets.length;
    }

    /**
     * Opens a Workbook Document: a Workbook in the document's culture, with its Sheets in order, the Sheet it shows
     * shown, its Linked Tables declared and waiting for their first snapshot, and each Sheet's Entries, Cell Formats and
     * column widths, with every Value computed again (ADR-0048, ADR-0049, ADR-0331). A Sheet Document opens as a
     * Workbook of that one Sheet. The culture of the caller plays no part.
     */
    static open(document: WorkbookDocument | SheetDocument): Workbook {
        if (document instanceof SheetDocument) {
            const workbook = new Workbook(SheetDocument.resolveCulture(document.culture), [document.name]);
            const sheet = workbook.#first;
            // Declared before any Formula is computed: a reader shows #GETTING_DATA, never #NAME? (ADR-0049).
            for (const table of document.linkedTables) sheet.declare(table.name, table.columns, table.keyColumns);
            sheet.openFormats(document);
            sheet.openEntries(document);
            workbook.opened();
            return workbook;
        }
        const workbook = new Workbook(SheetDocument.resolveCulture(document.culture), document.sheets.map(s => s.name));
        for (const table of document.linkedTables) workbook.#first.declare(table.name, table.columns, table.keyColumns);
        document.sheets.forEach((sheet, i) => workbook.#sheetAt(i).openFormats(sheet));
        document.sheets.forEach((sheet, i) => workbook.#sheetAt(i).openEntries(sheet));
        const shown = workbook.find(document.shown);
        if (shown === undefined) throw new Error('Internal: a Workbook Document shows one of its Sheets.');
        workbook.#shown = shown;
        workbook.opened();
        return workbook;
    }

    get #first(): Sheet {
        return this.#sheetAt(0);
    }

    #sheetAt(index: number): Sheet {
        const sheet = this.#sheets[index];
        if (sheet === undefined) throw new Error(`Internal: the Workbook has no Sheet at ${String(index)}.`);
        return sheet;
    }

    /**
     * The Sheet named `sheet` as a Sheet Document: the Workbook's culture, the Sheet's name, the Linked Tables'
     * declarations, its Entries, Cell Formats and column widths — never its Values, nor a table's rows. A Reference to
     * another Sheet is kept as it is written, so opened on its own it reads `#REF!`. Throws a RangeError when no Sheet
     * is named so.
     */
    toSheetDocument(sheet: string): SheetDocument {
        return sheetDocumentOf(this.#named(sheet));
    }

    /**
     * The Workbook as a Workbook Document: its culture, the Sheet shown, its Linked Tables' declarations, and each Sheet
     * as a Sheet Document records one, in order — never a Value, nor a table's rows (ADR-0331).
     */
    toDocument(): WorkbookDocument {
        return new WorkbookDocument(this.culture.name, this.#shown.name, this.#first.tableDeclarations, this.#sheets.map(s => sheetDocumentOf(s, [])));
    }

    /**
     * The Sheet shown: the one whose tab is selected (ADR-0331). Showing another Sheet changes nothing a step records.
     * Setting a Sheet that is not one of this Workbook's throws a RangeError.
     */
    get shown(): Sheet {
        return this.#shown;
    }

    set shown(sheet: Sheet) {
        if (!this.#sheets.includes(sheet)) throw new RangeError("The Sheet is not one of this Workbook's.");
        this.#shown = sheet;
    }

    /** The Sheets, in the order their tabs stand. Change one through `do`. */
    get sheets(): readonly Sheet[] {
        return this.#sheets;
    }

    /** The Sheet named `name`, without regard to case, as a Reference's qualifier names one; `undefined` when none is. */
    find(name: string): Sheet | undefined {
        return this.#sheets.find(sheet => equalsIgnoreCase(sheet.name, name));
    }

    /** The Linked Tables declared, in the order they were first declared (ADR-0049). */
    get linkedTables(): LinkedTable[] {
        return this.#first.linkedTables;
    }

    /**
     * The current snapshot of the Linked Table named `name`, without regard to case, column by column in the order the
     * columns are declared, each holding one Value per row (`undefined` for a blank); `undefined` while it waits. A later
     * snapshot replaces it whole. Throws when no table of that name is declared.
     */
    linkedTableColumns(name: string): readonly (readonly (Value | undefined)[])[] | undefined {
        const table = this.tables.get(ignoreCaseKey(name));
        if (table === undefined) throw new Error(`No Linked Table named '${name}' is declared.`);
        return table.data;
    }

    /**
     * Does `edit` on the Sheet named `sheet` as one operation, and returns the step that undoes it (ADR-0048, ADR-0331).
     * A refused or unreadable operation changes nothing and returns no step. Throws a RangeError when no Sheet is named
     * so, a SheetRefusedError when the Sheet refuses the operation (`check` says why), and a FormulaSyntaxError when a
     * typed Formula cannot be read.
     */
    do(sheet: string, edit: SheetEdit): WorkbookStep {
        const target = this.#named(sheet);
        if (edit instanceof WorkbookEdit) {
            const refusal = edit.checkIn(this, target);
            if (refusal !== undefined) throw new SheetRefusedError(refusal);
            let done: WorkbookEditDone | undefined;
            const change = this.collecting(() => {
                const result = edit.applyIn(this, target);
                done = result;
                return { sheet: result.sheet, change: result.change };
            });
            if (done === undefined) throw new Error('Internal: a Sheet operation done says what it did.');
            const { sheet: made, undo, redo } = done;
            return new WorkbookStep(made, change, () => this.collectOn(made, undo), () => this.collectOn(made, redo), []);
        }
        let step: ReturnType<Sheet['do']> | undefined;
        const made = this.collectOn(target, () => {
            step = target.do(edit);
            return step.change;
        });
        const done = step;
        if (done === undefined) throw new Error('Internal: an edit done returns its step.');
        return new WorkbookStep(target, made, () => this.collectOn(target, () => done.undo()), () => this.collectOn(target, () => done.redo()), edit.ranges);
    }

    /**
     * Does several edits, each on the Sheet it names, as one operation, and returns the one step that undoes them all
     * (ADR-0048, ADR-0331 §3): a Replace All over the Workbook (ADR-0332 §5) is one step and one change (ADR-0180). The
     * edits are done in the order given and undone in the reverse. Every edit is checked before any is done, and one
     * that cannot be done leaves the Workbook as it was: the edits done before it are put back, and the count of changes
     * with them. A Sheet operation is done alone, with `do`.
     *
     * Each edit is recalculated across the Workbook as it is done, before the next: the operation is one recalculation
     * per edit, its change reporting them together once the last has completed.
     */
    doAll(edits: readonly (readonly [string, SheetEdit])[]): WorkbookStep {
        if (edits.length === 0) throw new RangeError('An operation is at least one edit.');
        const targets = edits.map(([sheet, edit]) => {
            if (edit instanceof WorkbookEdit) throw new RangeError('A Sheet operation is done alone, with do(sheet, edit) (ADR-0331).');
            return this.#named(sheet);
        });
        edits.forEach(([, edit], i) => {
            const refusal = elementAt(targets, i).check(edit);
            if (refusal !== undefined) throw new SheetRefusedError(refusal);
        });
        const first = elementAt(targets, 0);
        const steps: ReturnType<Sheet['do']>[] = [];
        const countBefore = this.#changes;
        const undoAll = (): WorkbookChange => this.asOneChange(() => this.collecting(() => {
            for (let i = steps.length - 1; i >= 0; i--) {
                const step = elementAt(steps, i);
                this.collect(step.sheet, step.undo());
            }
            return { sheet: first, change: SheetChange.none };
        }));
        let change: WorkbookChange;
        try {
            change = this.asOneChange(() => this.collecting(() => {
                edits.forEach(([, edit], i) => {
                    const target = elementAt(targets, i);
                    const step = target.do(edit);
                    steps.push(step);
                    this.collect(target, step.change);
                });
                return { sheet: first, change: SheetChange.none };
            }));
        } catch (e) {
            // Put back what was done before the edit that could not be, with the count of changes: the same Entries,
            // seed and count draw the same, so the Workbook is as it was (ADR-0180).
            undoAll();
            if (this.#changes !== countBefore) this.setSeed(this.#seed, countBefore);
            throw e;
        }
        return new WorkbookStep(first, change, undoAll,
            () => this.asOneChange(() => this.collecting(() => {
                for (const step of steps) this.collect(step.sheet, step.redo());
                return { sheet: first, change: SheetChange.none };
            })),
            // What an undo or a redo selects is on the Sheet acted on first: the smallest range holding what the edits
            // on it changed (ADR-0331 §3, one range selected).
            SheetEdit.bounding(edits.filter((_, i) => targets[i] === first).flatMap(([, edit]) => edit.ranges).flatMap(r => [r.first, r.last])));
    }

    /** Whether `do` would refuse `edit` on the Sheet named `sheet` as it stands, and why; `undefined` when it would not. */
    check(sheet: string, edit: SheetEdit): SheetRefusal | undefined {
        return this.#named(sheet).check(edit);
    }

    /**
     * Declares a Linked Table by name and columns (ADR-0049), before any of its rows arrive. Formulas that read it stop
     * being `#NAME?` and show `#GETTING_DATA` until the first snapshot is pushed, on every Sheet. The declaration is
     * recorded in the document; a table is never undeclared. Declaring it again with the same columns, in the same
     * order, with the same key, changes nothing; other columns or another key replace the declaration and drop the rows
     * held so far.
     *
     * `key` is the key column, or the key's columns for a table whose rows are told apart by several columns together
     * (ADR-0058), each one of `columns` named without regard to case; none declares no key. Throws a RangeError when the
     * name or the columns are not ones a structured reference can name, or a key column is not one of the columns, or is
     * named twice.
     */
    declareLinkedTable(name: string, columns: readonly string[], key: string | readonly string[] = []): WorkbookChange {
        const keyColumns = typeof key === 'string' ? [key] : key;
        const first = this.#first;
        return this.collectOn(first, () => first.declareLinkedTable(name, columns, keyColumns));
    }

    /**
     * Replaces a Linked Table's data with a whole snapshot, in one step (ADR-0049): no recalculation sees part of one
     * snapshot and part of another, and only the Formulas that read the table — and what depends on them — recalculate.
     * A snapshot of a keyed table in which a key repeats is refused with a RepeatedKeyError, and the table waits again.
     * Throws when no table of that name is declared, and a RangeError when a row does not have one Value per column or
     * holds `#GETTING_DATA` or `#CIRC!`.
     */
    pushLinkedTable(name: string, rows: Iterable<readonly (Value | undefined)[]>): WorkbookChange {
        const first = this.#first;
        return this.collectOn(first, () => first.pushLinkedTable(name, rows));
    }

    #named(sheet: string): Sheet {
        const found = this.find(sheet);
        if (found === undefined) throw new RangeError(`No Sheet of the Workbook is named '${sheet}'.`);
        return found;
    }

    /** Runs an operation on `sheet`, and reports what it changed there with what its recalculation changed on every other Sheet. @internal */
    collectOn(sheet: Sheet, operation: () => SheetChange): WorkbookChange {
        return this.collecting(() => ({ sheet, change: operation() }));
    }

    /**
     * Runs an operation, and reports what it changed on the Sheet it names together with what its recalculation changed
     * on every other Sheet, and whether it changed which Sheets the Workbook holds. @internal
     */
    collecting(operation: () => { sheet: Sheet; change: SheetChange }): WorkbookChange {
        const outer = this.#collected;
        const outerReshaped = this.#reshaped;
        const collected = new Map<Sheet, SheetChange[]>();
        this.#collected = collected;
        this.#reshaped = false;
        let sheet: Sheet;
        let reshaped: boolean;
        try {
            const result = operation();
            sheet = result.sheet;
            const own = collected.get(sheet);
            if (own === undefined) collected.set(sheet, [result.change]);
            else own.push(result.change);
        } finally {
            reshaped = this.#reshaped;
            this.#collected = outer;
            this.#reshaped = outerReshaped || reshaped;
        }
        if (outer !== undefined) {
            for (const [other, changes] of collected) {
                const list = outer.get(other);
                if (list === undefined) outer.set(other, [...changes]);
                else list.push(...changes);
            }
        }
        const changes: { sheet: Sheet; change: SheetChange }[] = [];
        for (const s of this.#sheets) {
            const list = collected.get(s);
            if (list === undefined) continue;
            const change = SheetChange.merge(list);
            if (s === sheet || change !== SheetChange.none) changes.push({ sheet: s, change });
        }
        return new WorkbookChange(changes, reshaped);
    }

    /** Records what a recalculation changed on `sheet`, which the operation under way did not start on. @internal */
    collect(sheet: Sheet, change: SheetChange): void {
        if (this.#collected === undefined || change === SheetChange.none) return;
        const list = this.#collected.get(sheet);
        if (list === undefined) this.#collected.set(sheet, [change]);
        else list.push(change);
    }

    // ---- The Sheet Day and NOW (Workbook.Clock.cs) ----

    /**
     * The Sheet Day: the calendar day `TODAY()` answers on every Sheet, or `undefined` while it is not known, when every
     * Formula that calls `TODAY` is `#GETTING_DATA` (ADR-0121). The engine never reads a clock; whoever runs it says
     * which day it is. It is not part of a document.
     */
    get today(): SheetDay | undefined {
        return this.#today;
    }

    /** Whether any Formula on any Sheet calls a volatile function, so that moving the clock on can change a Value. */
    get hasVolatileFormulas(): boolean {
        return this.#sheets.some(sheet => sheet.hasVolatileFormulas);
    }

    /**
     * Sets the Sheet Day, and recalculates only the Formulas that call `TODAY`, on every Sheet, and what reads them, in
     * one recalculation. Setting the day the Workbook already has changes nothing. It is not an operation a user undoes.
     * Throws a RangeError for a day that is not a calendar day from 1 January 1900, where Excel's dates begin (ADR-0047).
     */
    setToday(today: SheetDay | undefined): WorkbookChange {
        if (today !== undefined) checkDay(today);
        const shown = this.#shown;
        if (sameDay(today, this.#today)) return this.collectOn(shown, () => SheetChange.none);
        this.#today = today === undefined ? undefined : { year: today.year, month: today.month, day: today.day };
        const readers = new SheetCellSet(this.#sheets.flatMap(sheet => sheet.todayReaders));
        return this.collectOn(shown, () => (readers.size === 0 ? SheetChange.none : shown.recalculateCells(readers)));
    }

    /**
     * Recalculates the Formulas that call a volatile function, on every Sheet, and what reads them, reading the moment
     * again: what ExSheet does as the minute turns (ADR-0124). It is not an operation a user undoes.
     */
    recalculateVolatile(): WorkbookChange {
        const shown = this.#shown;
        return this.collectOn(shown, () => (this.hasVolatileFormulas ? shown.recalculateCells(new SheetCellSet()) : SheetChange.none));
    }

    // ---- The Workbook Seed and the count of changes (Workbook.Random.cs) ----

    /**
     * The seed every draw of `RAND` and `RANDBETWEEN` on every Sheet is made from, with the Sheet's name, the cell, the
     * call's place in its Formula and `changes` (ADR-0180); `undefined` while it is not set, when every Formula that
     * calls either is `#GETTING_DATA`. The engine never chooses a random number of its own. It is not part of a document.
     */
    get seed(): bigint | undefined {
        return this.#seed;
    }

    /**
     * How many changes the Workbook has had since it was made or opened (ADR-0180): each draws again, on every Sheet. An
     * edit of an Entry, an insertion or a deletion, a Sheet added, deleted, renamed, moved or copied, the undo or redo of
     * one of those, a Linked Table's snapshot, and a declaration that declares or replaces one, each count once.
     */
    get changes(): number {
        return this.#changes;
    }

    /**
     * Sets the seed — an unsigned 64-bit integer, or `undefined` to make the draws wait again — and with `changes` the
     * count of changes too, and recalculates only the Formulas that draw, on every Sheet, and what reads them. The same
     * Entries, seed and count give the same draws (SH-17). Setting what the Workbook already has changes nothing. It is
     * not a change, and not an operation a user undoes. Throws a RangeError for a seed outside 0 to 2^64 - 1 or a count
     * below 0.
     */
    setSeed(seed: bigint | undefined, changes: number = this.#changes): WorkbookChange {
        if (seed !== undefined && (seed < 0n || seed > maxSeed)) throw new RangeError(`${String(seed)} is not a seed: a seed is from 0 to 2^64 - 1.`);
        if (!Number.isSafeInteger(changes) || changes < 0) throw new RangeError(`${String(changes)} is not a count of changes.`);
        const shown = this.#shown;
        if (seed === this.#seed && changes === this.#changes) return this.collectOn(shown, () => SheetChange.none);
        this.#seed = seed;
        this.#changes = changes;
        const readers = new SheetCellSet(this.#sheets.flatMap(sheet => sheet.randomReaders));
        return this.collectOn(shown, () => (readers.size === 0 ? SheetChange.none : shown.recalculateCells(readers)));
    }

    /** Does `operation` as one operation: whatever steps it takes, on whichever Sheets, it counts as one change at most. @internal */
    asOneChange<T>(operation: () => T): T {
        if (this.#operationDepth++ === 0) this.#operationCounted = false;
        try {
            return operation();
        } finally {
            this.#operationDepth--;
        }
    }

    /** Counts a change (ADR-0180), before the recalculation it brings draws: once for the operation under way, or once on its own. @internal */
    countChange(): void {
        if (this.#operationDepth > 0) {
            if (this.#operationCounted) return;
            this.#operationCounted = true;
        }
        this.#changes++;
    }

    /** Opening is no change: the count starts from 0 (ADR-0180). @internal */
    forgetChanges(): void {
        this.#changes = 0;
    }

    // ---- Adding, renaming, moving, copying and deleting a Sheet (Workbook.Sheets.cs) ----

    /** Where `sheet` stands in the order; -1 for a Sheet the Workbook does not hold. @internal */
    indexOf(sheet: Sheet): number {
        return this.#sheets.indexOf(sheet);
    }

    /**
     * The Sheets a 3D Reference from `first` to `last` spans (ADR-0331 §7): those from one to the other in tab order,
     * both included, whichever stands first; the names are read without regard to case. `undefined` when no Sheet has
     * one of the names. @internal
     */
    span(first: string, last: string): Sheet[] | undefined {
        const from = this.find(first);
        const to = this.find(last);
        if (from === undefined || to === undefined) return undefined;
        const a = this.#sheets.indexOf(from);
        const b = this.#sheets.indexOf(to);
        return this.#sheets.slice(Math.min(a, b), Math.max(a, b) + 1);
    }

    /**
     * Renames `sheet`: every Reference on every Sheet qualified with its old name is rewritten to the new one, as Excel
     * rewrites them (ADR-0046, ADR-0331). A rename to the name the Sheet already has changes nothing. Throws a
     * SheetRefusedError when another Sheet of the Workbook has the name, without regard to case. @internal
     */
    renameSheet(sheet: Sheet, name: string): SheetRename {
        const oldName = sheet.name;
        if (oldName === name) return { change: SheetChange.none, oldName, rewritten: [] };
        const refusal = this.nameTaken(name, sheet);
        if (refusal !== undefined) throw new SheetRefusedError(refusal);
        const rewritten: RewrittenEntry[] = [];
        const change = this.#reshape(sheet, dirty => {
            const requalify = (qualifier: string): string => (Sheet.names(qualifier, oldName) ? name : qualifier);
            // Either end of a 3D Reference's span is renamed as a qualifier is (ADR-0331 §7).
            this.#rewrite(r => r.with({
                sheetName: r.sheetName !== undefined && Sheet.names(r.sheetName, oldName) ? name : r.sheetName,
                lastSheetName: r.lastSheetName !== undefined && Sheet.names(r.lastSheetName, oldName) ? name : r.lastSheetName,
            }), requalify, rewritten, dirty);
            sheet.setName(name);
        });
        return { change, oldName, rewritten };
    }

    /** Undoes a rename: the old name back, and the Formulas it rewrote exactly as they were. @internal */
    undoRename(sheet: Sheet, rename: SheetRename): SheetChange {
        if (sheet.name === rename.oldName) return SheetChange.none;
        return this.#reshape(sheet, () => {
            for (const { sheet: owner, address, entry } of rename.rewritten) owner.replaceEntry(address, entry);
            sheet.setName(rename.oldName);
        });
    }

    /** Why `name` cannot be given to `sheet`: another Sheet has it, without regard to case; `undefined` when none does. @internal */
    nameTaken(name: string, sheet: Sheet | undefined): SheetRefusal | undefined {
        const other = this.find(name);
        return other !== undefined && other !== sheet
            ? new SheetRefusal('sheetNameTaken', `A Sheet of the Workbook is already named '${other.name}'.`)
            : undefined;
    }

    /**
     * The name a new Sheet takes, as Excel names one (exsheet-workbook ticket 12, answer 13): `Sheet` and the next number
     * of the Workbook's count of the Sheets it has made, passing over a name a Sheet has. @internal
     */
    nextSheetName(): string {
        return this.sheetName(this.nextSheetNumber());
    }

    /** The number `nextSheetName` gives the next new Sheet. @internal */
    nextSheetNumber(): number {
        for (let n = this.#sheetsMade + 1; ; n++) {
            if (this.find(this.sheetName(n)) === undefined) return n;
        }
    }

    /** `Sheet` and `number`: the name a new Sheet numbered so takes. @internal */
    sheetName(number: number): string {
        return `Sheet${String(number)}`;
    }

    /** A Workbook just opened from a document: it has made no Sheet yet, as Excel's opened workbook has not. @internal */
    opened(): void {
        this.#sheetsMade = 0;
    }

    /**
     * Counts a new Sheet as made, as Excel counts it: one numbered by the count takes the count to its number, past the
     * names it passed over; any other counts one. The count before is returned, for `uncountSheetMade`. @internal
     */
    countSheetMade(numbered: number | undefined): number {
        const before = this.#sheetsMade;
        this.#sheetsMade = numbered ?? this.#sheetsMade + 1;
        return before;
    }

    /** Puts back the count of Sheets made as it stood before an insertion or a copy, when the step is undone. @internal */
    uncountSheetMade(before: number): void {
        this.#sheetsMade = before;
    }

    /** The name a copy of the Sheet named `name` takes, as Excel names one: `Name (2)`, or the next number free, the name cut to fit 31 characters. @internal */
    copyName(name: string): string {
        const root = name.replace(copyNumber, '');
        for (let n = 2; ; n++) {
            const suffix = ` (${String(n)})`;
            const candidate = root.slice(0, Math.min(root.length, Sheet.maximumNameLength - suffix.length)) + suffix;
            if (this.find(candidate) === undefined) return candidate;
        }
    }

    /** Inserts a new, empty Sheet at `index`. @internal */
    insertSheet(index: number, name: string): { sheet: Sheet; change: SheetChange } {
        const sheet = new Sheet(this, name);
        const change = this.#reshape(sheet, () => {
            this.#sheets.splice(index, 0, sheet);
        });
        return { sheet, change };
    }

    /** Puts back a Sheet taken out, at `index`, with what it held. @internal */
    putBack(sheet: Sheet, index: number, rewritten: readonly RewrittenEntry[], shown: boolean): SheetChange {
        return this.#reshape(sheet, () => {
            this.#sheets.splice(index, 0, sheet);
            for (const { sheet: owner, address, entry } of rewritten) owner.replaceEntry(address, entry);
            if (shown) this.#shown = sheet;
        });
    }

    /**
     * Takes `sheet` out of the Workbook. Deleted, every Reference on another Sheet qualified with its name becomes
     * `#REF!` (ADR-0331), and a 3D Reference it ends hands that end to the Sheet next to it inward; taken out to undo its
     * insertion or copy, nothing is rewritten. The Sheet shown, if it was this one, becomes the one that takes its place.
     * @internal
     */
    takeOut(sheet: Sheet, deleting: boolean, rewritten: RewrittenEntry[]): SheetChange {
        const index = this.#sheets.indexOf(sheet);
        const before = this.#sheets.map(s => s.name);
        return this.#reshape(sheet, dirty => {
            this.#sheets.splice(index, 1);
            if (deleting) {
                const name = sheet.name;
                this.#rewrite(
                    r => (r.isThreeD ? deleted(r, before, name) : r.sheetName !== undefined && Sheet.names(r.sheetName, name) ? undefined : r),
                    q => (Sheet.names(q, name) ? undefined : q), rewritten, dirty);
            }
            if (this.#shown === sheet) this.#shown = this.#sheetAt(Math.min(index, this.#sheets.length - 1));
        });
    }

    /**
     * Moves `sheet` to `index` in the order. A 3D Reference whose end it is, moved past the other end, hands that end to
     * the Sheet next to it inward; each Formula so rewritten is added to `rewritten` as it was, for undoing the move.
     * @internal
     */
    move(sheet: Sheet, index: number, rewritten: RewrittenEntry[]): SheetChange {
        const before = this.#sheets.map(s => s.name);
        return this.#reshape(sheet, dirty => {
            this.#sheets.splice(this.#sheets.indexOf(sheet), 1);
            this.#sheets.splice(index, 0, sheet);
            const after = this.#sheets.map(s => s.name);
            this.#rewrite(r => (r.isThreeD ? moved(r, before, after, sheet.name) : r), q => q, rewritten, dirty);
        });
    }

    /** Undoes a move: `sheet` back at `index`, and the Formulas it rewrote exactly as they were. @internal */
    moveBack(sheet: Sheet, index: number, rewritten: readonly RewrittenEntry[]): SheetChange {
        return this.#reshape(sheet, () => {
            this.#sheets.splice(this.#sheets.indexOf(sheet), 1);
            this.#sheets.splice(index, 0, sheet);
            for (const { sheet: owner, address, entry } of rewritten) owner.replaceEntry(address, entry);
        });
    }

    /**
     * A copy of `source` named `name` at `index`: its Entries, Cell Formats and column widths. A Reference in it
     * qualified with the source's name names the copy, as Excel's copy does (ADR-0331); a 3D Reference the source ends is
     * `#REF!` in the copy, and any other is copied as it is (ADR-0331 §7). @internal
     */
    copy(source: Sheet, index: number, name: string): { sheet: Sheet; change: SheetChange } {
        const copy = new Sheet(this, name);
        const document = sheetDocumentOf(source, []);
        const sourceName = source.name;
        const change = this.#reshape(copy, dirty => {
            this.#sheets.splice(index, 0, copy);
            copy.openFormats(document);
            for (const cell of document.cells) {
                const entry = cell.entry;
                if (entry === undefined) continue;
                const requalified = Entry.rewrite(entry,
                    r => (r.isThreeD
                        ? Sheet.names(r.sheetName, sourceName) || Sheet.names(r.lastSheetName, sourceName) ? undefined : r
                        : r.sheetName !== undefined && Sheet.names(r.sheetName, sourceName) ? r.with({ sheetName: name }) : r),
                    q => (Sheet.names(q, sourceName) ? name : q));
                copy.replaceEntry(cell.address, requalified);
                dirty.add(new SheetCell(copy, cell.address));
            }
        });
        return { sheet: copy, change };
    }

    /** Puts back a copy, or an inserted Sheet, taken out by an undo. @internal */
    reinsert(sheet: Sheet, index: number): SheetChange {
        return this.#reshape(sheet, () => {
            this.#sheets.splice(index, 0, sheet);
        });
    }

    /**
     * Changes the Workbook's Sheets — which it holds, their order or their names — as one change: the References to
     * other Sheets are forgotten before and learnt again after, and every Formula whose qualifier may now name another
     * Sheet, or none, is recalculated with every volatile Formula, and whatever `mutate` adds. What it changed on
     * `initiator` is returned; what it changed on every other Sheet is reported to the Workbook operation under way.
     */
    #reshape(initiator: Sheet, mutate: (dirty: SheetCellSet) => void): SheetChange {
        // A change, and draws again, as a rename did in Excel (ADR-0180).
        initiator.countChange();
        const before = new Map(this.#sheets.map(s => [s, s.snapshot()] as const));
        for (const sheet of this.#sheets) sheet.unregisterAllRemote();
        const dirty = new SheetCellSet();
        mutate(dirty);
        this.#reshaped = true;
        for (const sheet of this.#sheets) sheet.rebuildDependencies();
        // INDIRECT's text may name a Sheet by a name it has had or now has (ADR-0124): it is volatile.
        for (const sheet of this.#sheets) dirty.addAll(sheet.qualifiedFormulas().map(a => new SheetCell(sheet, a)));
        const from = this.#sheets.includes(initiator) ? initiator : this.#first;
        const recalculated = dirty.size === 0 && !this.#sheets.some(s => s.hasVolatileFormulas)
            ? new Map<Sheet, SheetChange>()
            : Sheet.recalculateWorkbook(this, dirty, from, new Set());
        let own = SheetChange.none;
        for (const sheet of this.#sheets) {
            const change = sheet.diff(before.get(sheet) ?? new Map(), recalculated.get(sheet)?.recalculated ?? []); // oxlint-disable-line ex-grid/no-stand-in-default -- a Sheet put back has no snapshot from before, and a Sheet not recalculated recalculated nothing, as C#'s GetValueOrDefault
            if (sheet === initiator) own = change;
            else this.collect(sheet, change);
        }
        return own;
    }

    /**
     * Rewrites every Formula on every Sheet that names a Sheet through `map` and `requalify`, recording each as it was,
     * and marks it to be recalculated: a Formula left naming no Sheet (`=#REF!*2`) is no longer among the qualified ones
     * the change recalculates.
     */
    #rewrite(map: (reference: Reference) => Reference | undefined, requalify: (qualifier: string) => string | undefined, rewritten: RewrittenEntry[], dirty: SheetCellSet): void {
        for (const sheet of this.#sheets) {
            for (const { address, entry } of sheet.formulaEntries()) {
                // A qualified #REF! (Sheet1!#REF!) is no Reference, but its qualifier is rewritten too.
                if (entry.formula?.includes('!') !== true) continue;
                const mapped = Entry.rewrite(entry, map, requalify);
                if (mapped === entry) continue;
                rewritten.push({ sheet, address, entry });
                sheet.replaceEntry(address, mapped);
                dirty.add(new SheetCell(sheet, address));
            }
        }
    }

    // ---- 3D References (Workbook.ThreeD.cs) ----

    /**
     * A 3D Reference as a Formula is entered with it (ADR-0331 §7): its ends in tab order, each written in its Sheet's
     * own name, and a span of one Sheet written as a Reference to that Sheet. One naming a Sheet the Workbook lacks is
     * left as typed: it is `#NAME?`. @internal
     */
    inTabOrder(reference: Reference): Reference {
        const names = this.#sheets.map(s => s.name);
        const found = ends(reference, names);
        return found === undefined ? reference : spanning(reference, elementAt(names, found[0]), elementAt(names, found[1]));
    }

    /** An Entry with every 3D Reference in it written as `inTabOrder` writes one; the same instance when it holds none, or nothing changes. @internal */
    entryInTabOrder(entry: Entry): Entry {
        const parsed = entry.parsed;
        return parsed !== undefined && [...parsed.references()].some(r => r.isThreeD)
            ? Entry.rewrite(entry, r => (r.isThreeD ? this.inTabOrder(r) : r), q => q)
            : entry;
    }

    /** Whether `reference` is a 3D Reference whose span holds `sheet`, as the Workbook stands. @internal */
    spans(reference: Reference, sheet: Sheet): boolean {
        if (!reference.isThreeD || reference.sheetName === undefined || reference.lastSheetName === undefined) return false;
        return this.span(reference.sheetName, reference.lastSheetName)?.includes(sheet) === true;
    }
}

function checkDay(today: SheetDay): void {
    const { year, month, day } = today;
    const valid = Number.isInteger(year) && Number.isInteger(month) && Number.isInteger(day) && year >= 1 && year <= 9999
        && month >= 1 && month <= 12 && day >= 1 && day <= daysInMonth(year, month);
    if (!valid) throw new RangeError(`${String(year)}-${String(month)}-${String(day)} is not a calendar day.`);
    if (serialFromDate(year, month, day) === undefined) {
        throw new RangeError('The Sheet Day is a day from 1 January 1900, where Excel\'s dates begin.');
    }
}

function sameDay(a: SheetDay | undefined, b: SheetDay | undefined): boolean {
    if (a === undefined || b === undefined) return a === b;
    return a.year === b.year && a.month === b.month && a.day === b.day;
}

/**
 * A 3D Reference as the move of the Sheet named `movedName` leaves it: `before` names the Sheets in their order before
 * the move, and `after` after it. An end that stays on its side of the other is kept; one moved past the other hands
 * its end to the Sheet that stood next to it inward.
 */
function moved(reference: Reference, before: readonly string[], after: readonly string[], movedName: string): Reference {
    const found = ends(reference, before);
    if (found === undefined) return reference;
    const [a, b] = found;
    let first = elementAt(before, a);
    let last = elementAt(before, b);
    if (indexOfName(after, first) < indexOfName(after, last)) return reference;
    if (Sheet.names(first, movedName)) first = elementAt(before, a + 1);
    else last = elementAt(before, b - 1);
    return spanning(reference, first, last);
}

/**
 * A 3D Reference as the deletion of the Sheet named `deletedName` leaves it: an end it names is handed to the Sheet
 * next to it inward, in `before`'s order. `undefined` (`#REF!`) for one whose two ends were that Sheet.
 */
function deleted(reference: Reference, before: readonly string[], deletedName: string): Reference | undefined {
    const found = ends(reference, before);
    if (found === undefined) return reference;
    const [a, b] = found;
    const atA = elementAt(before, a);
    const atB = elementAt(before, b);
    if (a === b) return Sheet.names(atA, deletedName) ? undefined : reference;
    if (!Sheet.names(atA, deletedName) && !Sheet.names(atB, deletedName)) return reference;
    const first = Sheet.names(atA, deletedName) ? elementAt(before, a + 1) : atA;
    const last = Sheet.names(atB, deletedName) ? elementAt(before, b - 1) : atB;
    return spanning(reference, first, last);
}

/** A 3D Reference's two ends by where they stand in `names`, the first first; `undefined` for any other Reference, or one an end of which no Sheet has. */
function ends(reference: Reference, names: readonly string[]): readonly [number, number] | undefined {
    if (!reference.isThreeD || reference.sheetName === undefined || reference.lastSheetName === undefined) return undefined;
    const a = indexOfName(names, reference.sheetName);
    const b = indexOfName(names, reference.lastSheetName);
    return a < 0 || b < 0 ? undefined : [Math.min(a, b), Math.max(a, b)];
}

/** The Reference over the Sheets from `first` to `last`; one to that Sheet alone when they are one. */
function spanning(reference: Reference, first: string, last: string): Reference {
    return Sheet.names(first, last)
        ? reference.with({ sheetName: first, lastSheetName: undefined })
        : reference.with({ sheetName: first, lastSheetName: last });
}

function indexOfName(names: readonly string[], name: string): number {
    return names.findIndex(n => equalsIgnoreCase(n, name));
}

/**
 * What one change to a Workbook did, Sheet by Sheet, reported after its recalculation has completed: every Value it
 * names is already published, and all of them come from that one recalculation (ADR-0047, ADR-0331) — or, for an
 * operation of several edits, from the last of the recalculations its edits were done with.
 */
export class WorkbookChange {
    readonly #changes: readonly { readonly sheet: Sheet; readonly change: SheetChange }[];

    /** @internal */
    constructor(changes: readonly { readonly sheet: Sheet; readonly change: SheetChange }[], changesSheets: boolean) {
        this.#changes = changes;
        this.changesSheets = changesSheets;
    }

    /** Whether the change added, deleted, renamed, moved or copied a Sheet, so that the Sheets and their tabs may differ (ADR-0331). */
    readonly changesSheets: boolean;

    /** The Sheets the change reached, in the Workbook's order: the one it was made on, and every other whose Values or formats it changed. */
    get sheets(): Sheet[] {
        return this.#changes.map(c => c.sheet);
    }

    /** What the change did on `sheet`; a change that changed nothing for a Sheet it did not reach. */
    for(sheet: Sheet): SheetChange {
        return this.#changes.find(c => c.sheet === sheet)?.change ?? SheetChange.none;
    }
}

/**
 * One operation a Workbook has done on one of its Sheets, or on several (`doAll`), as one step of ExSheet's one undo
 * stack (ADR-0048, ADR-0331): undoing it restores what it changed exactly; redoing it does it again.
 */
export class WorkbookStep {
    readonly #undo: () => WorkbookChange;
    readonly #redo: () => WorkbookChange;
    #isUndone = false;

    /** The ranges the operation changed on `sheet`: what an undo or a redo of it selects, as Excel's do (ADR-0331 §3). None for an operation on the Sheets. */
    readonly ranges: readonly CellRange[];

    /** The Sheet the operation was done on: for a Sheet added or copied, the new Sheet. */
    readonly sheet: Sheet;

    /** What doing the operation changed. */
    readonly change: WorkbookChange;

    /** @internal */
    constructor(
        sheet: Sheet,
        change: WorkbookChange,
        undo: () => WorkbookChange,
        redo: () => WorkbookChange,
        ranges: readonly CellRange[],
    ) {
        this.sheet = sheet;
        this.change = change;
        this.#undo = undo;
        this.#redo = redo;
        this.ranges = [...ranges];
    }

    /** Whether the step is currently undone. */
    get isUndone(): boolean {
        return this.#isUndone;
    }

    /** Undoes the operation, and says what that changed. Throws when the step is already undone. */
    undo(): WorkbookChange {
        if (this.#isUndone) throw new Error('The step is already undone.');
        const change = this.#undo();
        this.#isUndone = true;
        return change;
    }

    /** Does the operation again after `undo`, and says what that changed. Throws when the step is not undone. */
    redo(): WorkbookChange {
        if (!this.#isUndone) throw new Error('Only an undone step can be redone.');
        const change = this.#redo();
        this.#isUndone = false;
        return change;
    }
}

