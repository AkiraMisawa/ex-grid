// The Consumer's own script, named by .NET (ADR-0420 section 3): a function exported from a JavaScript module the
// Consumer serves, which stands where a format code or a sampler stands. Nothing is evaluated from a string: the module
// is imported by its address, read against the page's base as a stylesheet's is, once per chart. What the function
// answers is checked as it is called, and a module that does not load, an export that is not a function, and a function
// that throws or answers what its member does not take refuse the chart in its place, by name, never written another
// way (spine rule 1).

/** A function of the Consumer's own script, as .NET names it. */
export interface WireScript {
    readonly module: string;
    readonly export: string;
}

/** Why the Consumer's script cannot draw the chart, said in the chart's place. */
export class ScriptFunctionError extends Error {
    /** A refusal naming the function and what it did. */
    constructor(message: string, options?: ErrorOptions) {
        super(message, options);
        this.name = 'ScriptFunctionError';
    }
}

/** How a function is named in a refusal. */
function named(script: WireScript): string {
    return `The script function "${script.export}" of "${script.module}"`;
}

/** The key a function is loaded and looked up by. */
export function keyOf(script: WireScript): string {
    return `${script.module}\u0000${script.export}`;
}

/** The Consumer's functions a definition names, loaded, and what to tell when one fails as the chart draws. */
export interface ScriptCalls {
    /** The function `script` names; every one the definition names was loaded before it is read. */
    readonly functionOf: (script: WireScript) => (...args: never[]) => unknown;
    /**
     * Told when a function throws or answers what its member does not take. True: the error is thrown to the reading
     * or the drawing that asked, which refuses the chart. False: the Script Core asked as the pointer moved, the
     * refusal takes the drawing's place before the browser paints, and what is answered meanwhile is never seen.
     */
    readonly failed: (error: ScriptFunctionError) => boolean;
}

/** For a definition that names no script function: asking for one is a defect of the reader. */
export const noScripts: ScriptCalls = Object.freeze({
    functionOf: (script: WireScript) => {
        throw new Error(`Internal: ${named(script)} was read without being loaded.`);
    },
    failed: () => true,
});

function describe(value: unknown): string {
    return typeof value === 'string' ? JSON.stringify(value) : typeof value === 'number' || typeof value === 'bigint' ? String(value) : value === null ? 'null' : typeof value;
}

/** What a failed function answers while its refusal is on its way: never drawn, since the refusal replaces the drawing first. */
const unseen: unique symbol = Symbol('unseen');

// Calls the function, a throw named as the function's own.
function call(calls: ScriptCalls, script: WireScript, args: readonly unknown[]): unknown {
    const f = calls.functionOf(script) as (...args: readonly unknown[]) => unknown; // oxlint-disable-line typescript/no-unsafe-type-assertion -- the Consumer's function, whose answer is checked here
    try {
        return f(...args);
    } catch (e: unknown) { // oxlint-disable-line ex-grid/no-catch -- named as the Consumer's function's own, and thrown on or refused in the chart's place
        return failure(calls, new ScriptFunctionError(`${named(script)} threw: ${e instanceof Error ? e.message : describe(e)}.`, { cause: e }));
    }
}

function failure(calls: ScriptCalls, error: ScriptFunctionError): typeof unseen {
    if (calls.failed(error)) throw error;
    return unseen;
}

/** A writer by the Consumer's function, which answers text for every value it is handed. */
export function writerBy(calls: ScriptCalls, script: WireScript, what: string): (value: unknown) => string {
    return value => {
        const text = call(calls, script, [value]);
        if (typeof text === 'string') return text;
        if (text !== unseen) failure(calls, new ScriptFunctionError(`${named(script)} wrote ${what} ${describe(value)} as ${describe(text)}, which is not text.`));
        return ''; // never painted: the refusal takes the drawing's place first
    };
}

/** A sampler by the Consumer's function, which answers a finite number, or null where the curve has no value. */
export function samplerBy(calls: ScriptCalls, script: WireScript, name: string): (position: number) => number | bigint | null {
    return position => {
        const value = call(calls, script, [position]);
        if (value === null || typeof value === 'bigint' || (typeof value === 'number' && Number.isFinite(value))) return value;
        if (value !== unseen) {
            failure(calls, new ScriptFunctionError(
                `${named(script)} answered ${describe(value)} for '${name}' at ${String(position)}; a value is a finite number, and a missing one is null.`));
        }
        return null; // never painted: the refusal takes the drawing's place first
    };
}

/**
 * The modules a chart imported, each once, by its address read against the page's base. An address of another scheme
 * than http or https, a `data:` one above all, would carry the script in the string, and is refused.
 */
export class ScriptModules {
    readonly #modules = new Map<string, Promise<Readonly<Record<string, unknown>>>>();

    /** Loads every function `scripts` names, refused by name when one cannot be had. */
    async load(scripts: readonly WireScript[]): Promise<ScriptCalls['functionOf']> {
        const modules = await Promise.all(scripts.map(script => this.#module(script)));
        const loaded = new Map<string, (...args: never[]) => unknown>();
        scripts.forEach((script, i) => {
            const module = modules[i];
            if (module === undefined) throw new Error(`Internal: ${named(script)} has no module loaded for it.`);
            const f = module[script.export];
            if (typeof f !== 'function') {
                throw new ScriptFunctionError(f === undefined
                    ? `The module "${script.module}" exports no "${script.export}" for the chart to call.`
                    : `The module "${script.module}" exports "${script.export}" as ${describe(f)}, which is not a function.`);
            }
            loaded.set(keyOf(script), f as (...args: never[]) => unknown); // oxlint-disable-line typescript/no-unsafe-type-assertion -- checked to be a function above
        });
        return script => {
            const f = loaded.get(keyOf(script));
            if (f === undefined) throw new Error(`Internal: ${named(script)} was read without being loaded.`);
            return f;
        };
    }

    #module(script: WireScript): Promise<Readonly<Record<string, unknown>>> {
        const address = new URL(script.module, document.baseURI);
        if (address.protocol !== 'http:' && address.protocol !== 'https:') {
            return Promise.reject(new ScriptFunctionError(
                `The module "${script.module}" is addressed by "${address.protocol}"; a script function's module is a file the application serves.`));
        }
        const key = address.href;
        let module = this.#modules.get(key);
        if (module === undefined) {
            module = (import(/* @vite-ignore */ key) as Promise<Readonly<Record<string, unknown>>>) // oxlint-disable-line typescript/no-unsafe-type-assertion -- a module namespace, read by name below
                .catch((e: unknown) => { // oxlint-disable-line ex-grid/no-catch -- rethrown as the refusal that names the module
                    // Asked again by the next definition that names it: a module that failed once may load then.
                    this.#modules.delete(key);
                    throw new ScriptFunctionError(`The module "${script.module}" did not load: ${e instanceof Error ? e.message : describe(e)}.`, { cause: e });
                });
            this.#modules.set(key, module);
        }
        return module;
    }
}
