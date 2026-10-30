// What .NET sends while it handles a write of the user's own (ADR-0142, D1). The core takes an intent's handler
// to have answered once it returns, and the first Window taken in after it as holding the write; Blazor's
// handler runs in .NET, after the browser's has returned. So what .NET sends meanwhile is held, and taken in
// once .NET says it handled the write, by when the write's own Window has crossed: a Window sent before it
// would otherwise read as the write's answer, and the write, arriving after, as someone else's over the cell.

/** A message .NET sends the grid: parameters with whatever comes with them, or a state of its source alone. */
export type Sent<Update, State> = { readonly update: Update } | { readonly state: State };

/** Holds what .NET sends while any write of the user's own is being handled there. */
export class WriteGate<Update, State> {
    readonly #take: (message: Sent<Update, State>) => void;
    readonly #held: Sent<Update, State>[] = [];
    readonly #idle: (() => void)[] = [];
    #handling = 0;
    #stopped = false;

    /** `take` takes a message in. */
    constructor(take: (message: Sent<Update, State>) => void) {
        this.#take = take;
    }

    /** Takes in `message`, or holds it while a write is being handled. */
    receive(message: Sent<Update, State>): void {
        if (this.#stopped) return;
        if (this.#handling > 0) this.#held.push(message);
        else this.#take(message);
    }

    /** Holds what .NET sends until `handled` settles: .NET's answer to the event that carried a write. */
    handling(handled: Promise<unknown>): Promise<unknown> {
        this.#handling += 1;
        return handled.finally(() => {
            this.#handling -= 1;
            if (this.#handling === 0) {
                try {
                    this.#release();
                } finally {
                    for (const resolve of this.#idle.splice(0)) resolve();
                }
            }
        });
    }

    /** Settles once nothing is held: at once while no write is being handled, and otherwise once every write is,
     * and what .NET sent meanwhile is taken in. */
    released(): Promise<void> {
        if (this.#handling === 0) return Promise.resolve();
        return new Promise(resolve => { this.#idle.push(resolve); });
    }

    /** Takes in what is held now, and holds on: .NET asked something of the grid, in a write's handler, that it
     * judges against what .NET sent it before asking. */
    flush(): void {
        this.#release();
    }

    /** Takes nothing in from now: the grid is gone. */
    stop(): void {
        this.#stopped = true;
        this.#held.length = 0;
        // Nothing will be taken in: whoever waits on it is let go, to a grid that reads no answer any more.
        for (const resolve of this.#idle.splice(0)) resolve();
    }

    // Every one, in the order .NET sent them. A state is the source's whole Window, but one replaced by the next is
    // still a Window the source held: a row command held behind the write whose row left it has lost its row for good,
    // even when the next Window brings the row back (ADR-0240's note of 2026-10-19), as on React, where each Window is
    // taken in as the source says it. Keeping only the last would make that depend on how quickly the two came.
    #release(): void {
        if (this.#stopped) return;
        for (const message of this.#held.splice(0)) this.#take(message);
    }
}
