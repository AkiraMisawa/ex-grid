// Text the browser sends .NET: inline while it fits well inside one message of a Blazor Server circuit, whose hub closes
// the circuit on a message over its limit (32 KB by default), and as a stream .NET reads in parts when it is longer. A
// link's narrowing, an output or a Board Diff can hold thousands of values; none may cost the page its circuit.

/** What crosses for a text: the text itself and no stream, or no text and the stream .NET reads it from. */
export type CrossedText = readonly [text: string | null, streamed: unknown];

/** How a long text is made a stream: Blazor's `DotNet.createJSStreamReference`, unless a test hands its own. */
export type StreamText = (bytes: Uint8Array) => unknown;

/** The most UTF-8 bytes a text crosses inline: half the hub's default limit, leaving room for the call around it. */
export const inlineBytes = 16 * 1024;

const encoder = new TextEncoder();

/** `text` as it crosses: inline while it is at most {@link inlineBytes}, and as a stream when it is longer. */
export function crossText(text: string, stream: StreamText = blazorStream): CrossedText {
    // A UTF-16 unit is at most three UTF-8 bytes, so a short text is known to fit without encoding it.
    if (text.length * 3 <= inlineBytes) return [text, null];
    const bytes = encoder.encode(text);
    return bytes.length <= inlineBytes ? [text, null] : [null, stream(bytes)];
}

function blazorStream(bytes: Uint8Array): unknown {
    const dotNet: unknown = Reflect.get(globalThis, 'DotNet');
    const create: unknown = typeof dotNet === 'object' && dotNet !== null ? Reflect.get(dotNet, 'createJSStreamReference') : undefined;
    if (typeof create !== 'function') throw new Error('Internal: Blazor\'s DotNet global is not loaded, so a long text cannot cross to .NET as a stream.');
    return Reflect.apply(create, dotNet, [bytes]);
}
