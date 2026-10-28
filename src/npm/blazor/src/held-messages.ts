// A message too large for Blazor Server's hub, the browser's half (ADR-0005, as amended 2026-10-28): Blazor Server
// closes the circuit on a message to .NET larger than its hub takes, 32 KB unless the application raises it, and says
// nothing on the page. Each product's bridge holds such a message here, hands .NET its number and length instead, and
// .NET takes it as a stream, in its turn: .NET's half is `src/Shared/Script/HeldCalls.cs`, compiled into each
// adapter. In the Shared Script (ADR-0411), so every product crosses a large message alike.

/**
 * The most a message to .NET carries in itself, in UTF-8 bytes: half the hub's default limit, which leaves room for
 * the call's own framing. A message larger than this is held, and .NET takes it as a stream.
 */
export const inlineBytes = 16 * 1024;

/** The most a held message may carry, in UTF-8 bytes, as .NET takes one: a larger one is refused here, never held. */
export const mostHeldBytes = 256 * 1024 * 1024;

/** What .NET is handed in place of a message larger than {@link inlineBytes}: the number it takes the message by,
 * with `take`, and the message's JSON's length in UTF-8 bytes. */
export interface WireHeld {
    readonly held: number;
    readonly bytes: number;
}

/** What a bridge calls in .NET. */
export interface DotNetCalls {
    invokeMethodAsync(method: string, ...args: readonly unknown[]): Promise<unknown>;
}

/** The messages held for .NET until it takes them, by number: none, where no circuit limits a message. */
export class HeldMessages {
    #last = 0;
    readonly #messages = new Map<number, Uint8Array<ArrayBuffer>>();
    readonly #circuit: boolean;

    /** Holds messages only where `circuit`: across Blazor Server's hub. WebAssembly's .NET takes any message as it is. */
    constructor(circuit: boolean) {
        this.#circuit = circuit;
    }

    /** `value` as it crosses: held, or nothing when it fits in a message of its own. */
    crossing(value: unknown): WireHeld | undefined {
        if (!this.#circuit) return undefined;
        const json = JSON.stringify(value);
        // A UTF-16 unit is at most three bytes of UTF-8: most messages are found small without encoding them.
        if (json.length * 3 <= inlineBytes) return undefined;
        const bytes = new TextEncoder().encode(json);
        if (bytes.byteLength <= inlineBytes) return undefined;
        if (bytes.byteLength > mostHeldBytes) {
            throw new RangeError(`A call of ${String(bytes.byteLength)} bytes is more than the ${String(mostHeldBytes)} a held call may carry, and is not sent.`);
        }
        this.#last += 1;
        this.#messages.set(this.#last, bytes);
        return { held: this.#last, bytes: bytes.byteLength };
    }

    /** The message held as `held`, as the stream .NET reads it from: gzip-compressed where the browser can compress,
     * and as it is where it cannot. Taken once. */
    take(held: number): Promise<Uint8Array<ArrayBuffer>> {
        const bytes = this.#messages.get(held);
        if (bytes === undefined) throw new Error(`Internal: .NET took the message ${String(held)}, which the bridge does not hold.`);
        this.#messages.delete(held);
        return packed(bytes);
    }

    /** Lets go of the message held as `held`, once the call it was held for is answered, taken or not. */
    drop(held: number): void {
        this.#messages.delete(held);
    }

    /** Lets go of every message held, once .NET has let go of the bridge. */
    clear(): void {
        this.#messages.clear();
    }
}

/**
 * Calls into .NET by `dotnet` whose arguments, together, are too large for a message of their own, held in `held`:
 * such a call to one of `holdable`, the methods .NET's `Held` makes, crosses as `Held(method, { held, bytes })`, and
 * .NET takes the arguments and makes the call in its turn, ahead of every call that came after it. Every other call
 * crosses as it is, unmeasured: what it carries is bounded by what the bridge sends it.
 */
export function crossingCalls(dotnet: DotNetCalls, held: HeldMessages, holdable: ReadonlySet<string>): DotNetCalls {
    return Object.freeze({
        invokeMethodAsync: (method: string, ...args: readonly unknown[]) => {
            const crossing = holdable.has(method) ? held.crossing(args) : undefined;
            if (crossing === undefined) return dotnet.invokeMethodAsync(method, ...args);
            return dotnet.invokeMethodAsync('Held', method, crossing).finally(() => { held.drop(crossing.held); });
        },
    });
}

// Compressed where the browser can, since a stream crosses a circuit at about one chunk per round trip; .NET tells
// the two apart by gzip's first two bytes, which no UTF-8 text begins with.
async function packed(bytes: Uint8Array<ArrayBuffer>): Promise<Uint8Array<ArrayBuffer>> {
    if (typeof CompressionStream !== 'function') return bytes;
    const stream = new Blob([bytes]).stream().pipeThrough(new CompressionStream('gzip'));
    return new Uint8Array(await new Response(stream).arrayBuffer());
}
