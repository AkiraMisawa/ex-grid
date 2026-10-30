// Input made while the Server circuit is down is refused, never replayed (ADR-0240 section 11), over the Script
// Core. A call to .NET made while the connection is down never arrives, and Blazor never ends its promise: the send's
// rejection escapes as an uncaught error. So each call that carries what the user made and .NET must carry out (a
// write, an Action, a sort, a context command) is numbered as it crosses, `Call(number, method, arguments)`, over the
// one connection that delivers in order; .NET finds a number missing when a later one arrives, or when the bridge says
// how far its numbers have gone (`InputsSent`), and tells the bridge which never arrived (`inputsLost`). Their calls
// end as lost, so what waits on them lets go, and the user is told aloud. What the browser has already carried out and
// .NET only hears, a Selection or a column's width, is not numbered: it was not lost to the user.

import type { DotNetCalls } from './held-messages';

/** What a numbered call ends with when it never reached .NET: the sentence the user is told, in the grid's words. */
export class LostCall {
    readonly words: string;

    constructor(words: string) {
        this.words = words;
    }
}

/** Numbers the calls into .NET that carry the user's input, and ends those .NET says never arrived. */
export class InputNumbers {
    readonly #dotnet: DotNetCalls;
    #sent = 0;
    // How far .NET has heard the numbers went: a tell it answered.
    #heard = 0;
    // The numbered calls not yet answered, by number, each with what ends it as lost.
    readonly #waiting = new Map<number, (lost: LostCall) => void>();
    // The numbered calls whose own ending tells the user, as a refused commit does.
    readonly #speaking = new Set<number>();

    /** `dotnet` crosses each numbered call as `Call`, held where it is too large for a message of its own. */
    constructor(dotnet: DotNetCalls) {
        this.#dotnet = dotnet;
    }

    /**
     * Calls `method` with `args` under the next number, and answers what .NET answers, or a {@link LostCall} where the
     * call never reached it. A call that tells the user itself when it ends lost, as an edit refused in its editor
     * does, says `speaks`, and the loss is not said again for it.
     */
    call(method: string, args: readonly unknown[], speaks = false): Promise<unknown> {
        // Numbered once it is made: a call refused as it is made, too large to hold, never crossed, and is no gap.
        const number = this.#sent + 1;
        const answer = this.#dotnet.invokeMethodAsync('Call', number, method, args);
        this.#sent = number;
        if (speaks) this.#speaking.add(number);
        const lost = new Promise<LostCall>(resolve => { this.#waiting.set(number, resolve); });
        // Its wait ends before what .NET answered is handed on, so an input made after it tells .NET nothing for it.
        const ended = (): void => {
            this.#waiting.delete(number);
            this.#speaking.delete(number);
        };
        return Promise.race([answer.finally(ended), lost]);
    }

    /** Calls through `dotnet`, `methods` among them numbered. */
    calls(methods: ReadonlySet<string>, dotnet: DotNetCalls): DotNetCalls {
        return Object.freeze({
            invokeMethodAsync: (method: string, ...args: readonly unknown[]) =>
                methods.has(method) ? this.call(method, args) : dotnet.invokeMethodAsync(method, ...args),
        });
    }

    /** Tells .NET how far the numbers have gone, as an input is made while a numbered call is unanswered: such a call
     * may have been lost, and nothing numbered may follow it while the input waits. Told again with each such input
     * until .NET has answered a tell of that far, since a tell made while the connection is down is lost too. */
    tell(): void {
        if (this.#waiting.size === 0 || this.#heard >= this.#sent) return;
        const sent = this.#sent;
        void this.#dotnet.invokeMethodAsync('InputsSent', sent).then(() => { this.#heard = Math.max(this.#heard, sent); });
    }

    /** Ends the calls `numbers` as lost, with `words`, and answers whether the user is still to be told: a call lost
     * that does not tell the user itself. */
    lose(numbers: readonly number[], words: string): boolean {
        let untold = false;
        for (const number of numbers) {
            if (!Number.isInteger(number) || number < 1 || number > this.#sent) {
                throw new RangeError(`Internal: .NET named input ${String(number)} lost, which the bridge never sent.`);
            }
            if (!this.#speaking.has(number)) untold = true;
            const end = this.#waiting.get(number);
            this.#waiting.delete(number);
            this.#speaking.delete(number);
            end?.(new LostCall(words));
        }
        return untold;
    }
}
