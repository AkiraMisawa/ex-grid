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
    readonly #discard: (state: State) => void;
    readonly #held: Sent<Update, State>[] = [];
    readonly #idle: (() => void)[] = [];
    #handling = 0;
    #stopped = false;

    /** `take` takes a message in; `discard` gives up a state another state after it replaces. */
    constructor(take: (message: Sent<Update, State>) => void, discard: (state: State) => void) {
        this.#take = take;
        this.#discard = discard;
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

    // In the order .NET sent them; of states one after another only the last, which holds all the ones before it
    // did, since a state is the source's whole Window. A state before an update is taken in before it.
    #release(): void {
        if (this.#stopped) return;
        const held = this.#held.splice(0);
        held.forEach((message, i) => {
            const after = held[i + 1];
            if ('state' in message && after !== undefined && 'state' in after) this.#discard(message.state);
            else this.#take(message);
        });
    }
}
