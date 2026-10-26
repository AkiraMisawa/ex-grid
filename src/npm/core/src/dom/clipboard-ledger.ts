// Whose write a change of the clipboard reports (ADR-0170). The browser says only that the clipboard changed,
// to every listener of the document at once, and says it a while after the write: the change can be heard after
// a second grid has copied. Counted per grid, the first grid's late change was taken by the second as its own,
// and the second grid's own change then dropped its outline (CP-27 on the React host). Every grid of a document
// therefore counts its writes in one ledger, in the order they were made, and each change reports the earliest
// write still unreported.
//
// That order is certain for a write the copy event sets, which lands as it is counted. A write by the
// asynchronous route is counted before the browser makes it, and another write counted before it lands may land
// on either side of it. Both are uncertain: neither is taken as its grid's own, so the race costs an outline and
// never leaves one standing where the clipboard holds something else.

/** One grid's write, counted before the browser reports it, until the change it causes is heard. */
export class LedgerWrite {
    /** The grid whose copy made it. */
    readonly owner: object;
    /** Counted before the browser makes it, until it lands or fails. */
    inFlight: boolean;
    /** Whether a write may have landed on either side of another, so its change is no proof of its grid's copy. */
    uncertain = false;

    constructor(owner: object, inFlight: boolean) {
        this.owner = owner;
        this.inFlight = inFlight;
    }
}

const ledgers = new WeakMap<Document, ClipboardLedger>();

/** The writes the grids of one document have made and the browser has yet to report, in the order made. */
export class ClipboardLedger {
    readonly #pending: LedgerWrite[] = [];
    readonly #heard = new WeakMap<object, LedgerWrite | null>();
    readonly #listeners = new Set<object>();

    /** The ledger every grid of `doc` shares. */
    static of(doc: Document): ClipboardLedger {
        let ledger = ledgers.get(doc);
        if (ledger === undefined) {
            ledger = new ClipboardLedger();
            ledgers.set(doc, ledger);
        }
        return ledger;
    }

    /** `listener` hears the document's changes from now on. */
    join(listener: object): void {
        this.#listeners.add(listener);
    }

    /**
     * `listener` hears no more changes. A write it made still causes its change, which the grids that remain
     * hear, so it stays counted; once no grid is left to hear, no change is reported, and the ledger empties.
     */
    leave(listener: object): void {
        this.#listeners.delete(listener);
        if (this.#listeners.size === 0) this.#pending.length = 0;
    }

    /**
     * A write `owner` makes, counted from now: landed as it is counted, or in flight until {@link landed}. A write
     * counted while another is in flight may land on either side of it, and both are uncertain.
     */
    count(owner: object, inFlight = false): LedgerWrite {
        const write = new LedgerWrite(owner, inFlight);
        for (const crossed of this.#pending) {
            if (!crossed.inFlight) continue;
            crossed.uncertain = true;
            write.uncertain = true;
        }
        this.#pending.push(write);
        return write;
    }

    /** A write counted in flight has landed: a write counted from now lands after it. */
    landed(write: LedgerWrite): void {
        write.inFlight = false;
    }

    /** A counted write that causes no change, because it failed. */
    withdraw(write: LedgerWrite): void {
        write.inFlight = false;
        const at = this.#pending.indexOf(write);
        if (at >= 0) this.#pending.splice(at, 1);
    }

    /** Whether `owner` has a write the browser has yet to report. */
    awaits(owner: object): boolean {
        return this.#pending.some(write => write.owner === owner);
    }

    /**
     * The write a change reports: the earliest still unreported, or `null` for a change no grid made. Every
     * listener of the document hears the same change, so it is answered once, by the change itself, and each
     * listener after the first reads the same answer.
     */
    hear(change: object): LedgerWrite | null {
        const heard = this.#heard.get(change);
        if (heard !== undefined) return heard;
        const write = this.#pending.shift() ?? null; // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0170: a change with no write still unreported is no grid's
        this.#heard.set(change, write);
        return write;
    }
}
