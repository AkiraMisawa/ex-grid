// The Engine Host Worker (ADR-0370 section 2): starts .NET in a Web Worker, finds EngineHost's
// [JSExport] methods, and answers each request the page posts by calling one of them. Never the page's
// thread: a recalculation runs here while the page keeps its frame.
//
// It takes one request at a time, in the order posted. An export that answers a promise (the pivot's)
// is awaited before the next request starts, so a write posted after a read is never done before it.

import type { EngineCalls, EngineMessage, EngineRequest, FetchedFile } from './protocol';

interface WorkerScope {
    postMessage(message: EngineMessage): void;
    addEventListener(type: 'message', listener: (event: MessageEvent<EngineRequest>) => void): void;
}

type Exported = { [K in keyof EngineCalls]: (...args: EngineCalls[K]) => string | Promise<string> };

interface DotnetModule {
    readonly dotnet: {
        create(): Promise<{
            getAssemblyExports(assembly: string): Promise<{ readonly EngineHost: { readonly Exports: Exported } }>;
            getConfig(): { readonly mainAssemblyName?: string };
        }>;
    };
}

const scope = globalThis as unknown as WorkerScope;
const scriptMs = performance.now();

// Requests that arrive while .NET starts wait for it, in order.
const queue: EngineRequest[] = [];
let exports: Exported | undefined;
let running = false;

const drain = async (): Promise<void> => {
    if (running || exports === undefined) return;
    running = true;
    try {
        for (let request = queue.shift(); request !== undefined; request = queue.shift()) {
            const started = performance.now();
            try {
                const method = exports[request.method] as (...args: readonly unknown[]) => string | Promise<string>;
                const json = await method(...request.args);
                scope.postMessage({ kind: 'answer', id: request.id, json, workerMs: performance.now() - started });
            } catch (error) {
                scope.postMessage({ kind: 'refused', id: request.id, error: String(error instanceof Error ? error.stack ?? error.message : error) });
            }
        }
    } finally {
        running = false;
    }
};

scope.addEventListener('message', (event) => {
    queue.push(event.data);
    void drain();
});

const files = (): FetchedFile[] => (performance.getEntriesByType('resource') as PerformanceResourceTiming[]).map((entry) => ({
    name: new URL(entry.name).pathname,
    transferSize: entry.transferSize,
    encodedBodySize: entry.encodedBodySize,
    decodedBodySize: entry.decodedBodySize,
}));

const start = async (): Promise<void> => {
    // Kept out of the bundle: the path is the published runtime's, served beside this script.
    const runtimeUrl = '/_framework/dotnet.js';
    const { dotnet } = await import(/* @vite-ignore */ runtimeUrl) as DotnetModule;
    const runtime = await dotnet.create();
    const createdMs = performance.now();
    const assembly = runtime.getConfig().mainAssemblyName;
    if (assembly === undefined) throw new Error('The runtime\'s configuration names no main assembly.');
    exports = (await runtime.getAssemblyExports(assembly)).EngineHost.Exports;
    scope.postMessage({ kind: 'ready', scriptMs, createdMs, exportsMs: performance.now(), files: files() });
    await drain();
};

start().catch((error: unknown) => {
    scope.postMessage({ kind: 'failed', error: String(error instanceof Error ? error.stack ?? error.message : error) });
});
