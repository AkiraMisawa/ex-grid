// The page's side of the Engine Host (ADR-0370 section 2): one Worker for the page, and a promise for
// each call. A call is posted at once and answered in the order posted; nothing here waits on a clock.
//
// Where the page sets window.engineOnPage to a module's URL (serve.mjs's `ts-page`), there is no Worker:
// that module serves one end of a MessageChannel on the page's own thread (ts-engine-page.ts), and the
// client holds the other end in the Worker's place. The calls and answers are the same.

import type { EngineCalls, EngineMessage, FetchedFile } from './protocol';

/** When the Worker started, on the page's clock (ms from navigation), and what it fetched to do it. */
export interface EngineBoot {
    /** The Worker was made. */
    readonly createdAt: number;
    /** It said .NET had started and its exports were found. */
    readonly readyAt: number;
    /** On the Worker's own clock: its script ran, .NET was created, the exports were found. */
    readonly worker: { readonly scriptMs: number; readonly createdMs: number; readonly exportsMs: number };
    readonly files: readonly FetchedFile[];
}

/** An answer, with the ms the Worker spent on it and the ms from the post until the page had it. */
export interface EngineAnswer<T> {
    readonly value: T;
    readonly workerMs: number;
    readonly roundTripMs: number;
}

export class EngineClient {
    readonly #worker: Worker | MessagePort;
    readonly #pending = new Map<number, { resolve: (answer: EngineAnswer<unknown>) => void; reject: (error: Error) => void; postedAt: number }>();
    #nextId = 1;
    readonly ready: Promise<EngineBoot>;

    constructor(workerUrl: string) {
        const createdAt = performance.now();
        const onPage = (globalThis as { engineOnPage?: string }).engineOnPage;
        if (onPage === undefined) {
            this.#worker = new Worker(workerUrl, { type: 'module' });
        } else {
            const channel = new MessageChannel();
            this.#worker = channel.port1;
            channel.port1.start();
            void (import(/* @vite-ignore */ onPage) as Promise<{ start: (port: MessagePort) => void }>).then((module) => { module.start(channel.port2); });
        }
        this.ready = new Promise<EngineBoot>((resolve, reject) => {
            (this.#worker as Worker).addEventListener('message', (event: MessageEvent<EngineMessage>) => {
                const message = event.data;
                switch (message.kind) {
                    case 'ready':
                        resolve({
                            createdAt,
                            readyAt: performance.now(),
                            worker: { scriptMs: message.scriptMs, createdMs: message.createdMs, exportsMs: message.exportsMs },
                            files: message.files,
                        });
                        return;
                    case 'failed':
                        reject(new Error(`The Engine Host did not start: ${message.error}`));
                        return;
                    case 'answer':
                    case 'refused': {
                        const call = this.#pending.get(message.id);
                        if (call === undefined) throw new Error(`The Engine Host answered call ${message.id}, which was never made.`);
                        this.#pending.delete(message.id);
                        if (message.kind === 'refused') {
                            call.reject(new Error(message.error));
                        } else {
                            call.resolve({ value: JSON.parse(message.json) as unknown, workerMs: message.workerMs, roundTripMs: performance.now() - call.postedAt });
                        }
                        return;
                    }
                }
            });
            if (this.#worker instanceof Worker) this.#worker.addEventListener('error', (event) => { reject(new Error(`The Engine Host failed: ${event.message}`)); });
        });
    }

    /** Calls an export; the answer is its JSON, read. */
    call<T, K extends keyof EngineCalls = keyof EngineCalls>(method: K, ...args: EngineCalls[K]): Promise<EngineAnswer<T>> {
        const id = this.#nextId++;
        return new Promise<EngineAnswer<T>>((resolve, reject) => {
            this.#pending.set(id, { resolve: resolve as (answer: EngineAnswer<unknown>) => void, reject, postedAt: performance.now() });
            this.#worker.postMessage({ id, method, args });
        });
    }
}
