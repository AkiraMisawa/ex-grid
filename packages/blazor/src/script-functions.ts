// The Consumer's own script, named by .NET (ADR-0420 section 3, ADR-0431): a function exported from a JavaScript
// module the Consumer serves, which a product drawn in the browser calls at once. Nothing is evaluated from a string:
// the module is imported by its address, read against the page's base as a stylesheet's is. A module that does not
// load, and an export that is not a function, are refused by name, never answered another way (spine rule 1).

/** A function of the Consumer's own script, as .NET names it. */
export interface WireScript {
    readonly module: string;
    readonly export: string;
}

/** Why the Consumer's script was refused, naming the function and what it did. */
export class ScriptFunctionError extends Error {
    /** A refusal naming the function and what it did. */
    constructor(message: string, options?: ErrorOptions) {
        super(message, options);
        this.name = 'ScriptFunctionError';
    }
}

/** How a function is named in a refusal. */
export function scriptFunctionName(script: WireScript): string {
    return `The script function "${script.export}" of "${script.module}"`;
}

/** The key a function is loaded and looked up by. */
export function keyOf(script: WireScript): string {
    return `${script.module}\u0000${script.export}`;
}

/** A value a function answered, as a refusal names it. */
export function describeAnswer(value: unknown): string {
    return typeof value === 'string' ? JSON.stringify(value) : typeof value === 'number' || typeof value === 'bigint' ? String(value) : value === null ? 'null' : typeof value;
}

/** A function of the Consumer's, loaded. */
export type LoadedScriptFunction = (...args: never[]) => unknown;

/**
 * The modules imported for the Consumer's functions, each once, by its address read against the page's base. An address
 * of another scheme than http or https, a `data:` one above all, would carry the script in the string, and is refused.
 */
export class ScriptModules {
    readonly #modules = new Map<string, Promise<Readonly<Record<string, unknown>>>>();

    /** Loads every function `scripts` names, refused by name when one cannot be had. */
    async load(scripts: readonly WireScript[]): Promise<(script: WireScript) => LoadedScriptFunction> {
        const modules = await Promise.all(scripts.map(script => this.#module(script)));
        const loaded = new Map<string, LoadedScriptFunction>();
        scripts.forEach((script, i) => {
            const module = modules[i];
            if (module === undefined) throw new Error(`Internal: ${scriptFunctionName(script)} has no module loaded for it.`);
            const f = module[script.export];
            if (typeof f !== 'function') {
                throw new ScriptFunctionError(f === undefined
                    ? `The module "${script.module}" exports no "${script.export}" to call.`
                    : `The module "${script.module}" exports "${script.export}" as ${describeAnswer(f)}, which is not a function.`);
            }
            loaded.set(keyOf(script), f as LoadedScriptFunction); // oxlint-disable-line typescript/no-unsafe-type-assertion -- checked to be a function above
        });
        return script => {
            const f = loaded.get(keyOf(script));
            if (f === undefined) throw new Error(`Internal: ${scriptFunctionName(script)} was read without being loaded.`);
            return f;
        };
    }

    #module(script: WireScript): Promise<Readonly<Record<string, unknown>>> {
        if (!URL.canParse(script.module, document.baseURI)) {
            return Promise.reject(new ScriptFunctionError(`The module "${script.module}" is no address a page can import a module from.`));
        }
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
                    // Asked again by the next load that names it: a module that failed once may load then.
                    this.#modules.delete(key);
                    throw new ScriptFunctionError(`The module "${script.module}" did not load: ${e instanceof Error ? e.message : describeAnswer(e)}.`, { cause: e });
                });
            this.#modules.set(key, module);
        }
        return module;
    }
}
