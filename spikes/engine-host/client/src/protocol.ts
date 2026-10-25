// The messages between a page and the Engine Host Worker (ADR-0370 section 2). One request names one
// export of EngineHost (EngineHost/Program.cs) and its arguments; one reply answers it by id. The Worker
// takes requests in the order they were posted and answers each in that order, so the writes a page posts
// are done in the order it made them.

/** The exports a page may call, by name, with their arguments. */
export interface EngineCalls {
    SheetOpen: [documentJson: string];
    SheetEnter: [address: string, typed: string, start: number, count: number, columns: number];
    SheetWindow: [start: number, count: number, columns: number];
    PivotOpen: [tradesJson: string];
    PivotWindow: [across: string, start: number, count: number, markChanges: boolean];
    PivotAmend: [index: number, delta: number, across: string, start: number, count: number];
    ProbeRun: [runtime: string, repeats: number];
}

/** A call, as the page posts it. */
export interface EngineRequest {
    readonly id: number;
    readonly method: keyof EngineCalls;
    readonly args: readonly unknown[];
}

/** One file the Worker fetched to start .NET, from its Resource Timing. */
export interface FetchedFile {
    readonly name: string;
    readonly transferSize: number;
    readonly encodedBodySize: number;
    readonly decodedBodySize: number;
}

/** What the Worker posts: that .NET has started, with what it cost; or an answer to a call. */
export type EngineMessage =
    | {
        readonly kind: 'ready';
        /** ms on the Worker's clock from its start until its script ran, .NET was created, and the exports were found. */
        readonly scriptMs: number;
        readonly createdMs: number;
        readonly exportsMs: number;
        readonly files: readonly FetchedFile[];
    }
    | { readonly kind: 'failed'; readonly error: string }
    | { readonly kind: 'answer'; readonly id: number; readonly json: string; readonly workerMs: number }
    | { readonly kind: 'refused'; readonly id: number; readonly error: string };
