// The C# side of a differential run: dotnet/tools/pivot-oracle's `serve`, started once per file, answering one
// JSON request per line in the order they were sent.

import { spawn, type ChildProcessWithoutNullStreams } from 'node:child_process';
import { createInterface } from 'node:readline';

/** What the oracle answers: the fields of its op's answer, or the .NET exception it threw. */
export type OracleAnswer = Readonly<Record<string, unknown>>;

/** A running oracle. */
export interface Oracle {
    /** Sends every request at once and resolves with their answers, in order. */
    askAll(requests: readonly Readonly<Record<string, unknown>>[]): Promise<readonly OracleAnswer[]>;
    /** Stops the oracle. */
    close(): void;
}

/** Starts the oracle EXGRID_PIVOT_ORACLE names, a command line split on spaces. */
export function startOracle(): Oracle {
    const command = process.env['EXGRID_PIVOT_ORACLE'];
    if (command === undefined || command.trim() === '') {
        throw new Error('EXGRID_PIVOT_ORACLE names no command: set it to start dotnet/tools/pivot-oracle with `serve` (differential/README.md).');
    }
    const [program, ...args] = command.trim().split(/\s+/);
    if (program === undefined) throw new Error('EXGRID_PIVOT_ORACLE names no program.');
    const child: ChildProcessWithoutNullStreams = spawn(program, args, { stdio: ['pipe', 'pipe', 'pipe'] });
    // Each request owed an answer, in the order sent. Whatever goes wrong — the oracle failing to start,
    // exiting early, or answering what was not asked — fails every request owed and every one asked later,
    // so a run reports it at once instead of waiting out its timeout.
    const pending: { readonly resolve: (answer: OracleAnswer) => void; readonly reject: (error: Error) => void }[] = [];
    let failure: Error | undefined;
    let stderr = '';
    const fail = (error: Error): void => {
        failure ??= error;
        for (const owed of pending.splice(0)) owed.reject(failure);
    };
    child.stderr.on('data', (chunk: Buffer) => { stderr += chunk.toString('utf8'); });
    child.on('error', (error) => { fail(new Error(`The oracle '${command}' did not start.`, { cause: error })); });
    child.on('exit', (code) => { fail(new Error(`The oracle exited (${String(code)}). ${stderr}`)); });
    const lines = createInterface({ input: child.stdout });
    lines.on('line', (line) => {
        const owed = pending.shift();
        if (owed === undefined) {
            fail(new Error(`The oracle answered a request nobody asked: ${line}`));
            return;
        }
        let parsed: unknown;
        try {
            parsed = JSON.parse(line);
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- an answer that is not JSON fails the run, by the line it was
            fail(new Error(`The oracle answered ${line}, which is not JSON.`, { cause: error }));
            return;
        }
        if (typeof parsed !== 'object' || parsed === null) {
            fail(new Error(`The oracle answered ${line}.`));
            return;
        }
        owed.resolve(parsed as OracleAnswer); // oxlint-disable-line typescript/no-unsafe-type-assertion -- the oracle answers a JSON object, checked above
    });
    return {
        askAll(requests) {
            const answers = requests.map(() => new Promise<OracleAnswer>((resolve, reject) => {
                if (failure === undefined) pending.push({ resolve, reject });
                else reject(failure);
            }));
            if (failure === undefined) child.stdin.write(requests.map((request) => JSON.stringify(request)).join('\n') + '\n');
            return Promise.all(answers);
        },
        close() {
            child.stdin.end();
        },
    };
}

/** A seeded generator, so a failing run is repeated by its seed (EXGRID_PIVOT_SEED). */
export function random(seed: number): () => number {
    let state = seed >>> 0;
    return () => {
        state = (state + 0x6D2B79F5) >>> 0;
        let t = state;
        t = Math.imul(t ^ (t >>> 15), t | 1);
        t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
        return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
}

/** The seed of this run: EXGRID_PIVOT_SEED, a whole number, or one taken from the clock. */
export function seedOfRun(): number {
    const given = process.env['EXGRID_PIVOT_SEED'];
    if (given === undefined) return Date.now() % 1_000_000_007;
    if (!/^\d+$/u.test(given.trim())) throw new Error(`EXGRID_PIVOT_SEED is '${given}', which is not a whole number.`);
    return Number(given.trim());
}

// Where two answers first part, and what each holds there.
export function firstDifference(theirs: unknown, ours: unknown, at: string): string {
    if (JSON.stringify(theirs) === JSON.stringify(ours)) return '';
    if (typeof theirs === 'object' && theirs !== null && typeof ours === 'object' && ours !== null && Array.isArray(theirs) === Array.isArray(ours)) {
        const keys = new Set([...Object.keys(theirs), ...Object.keys(ours)]);
        for (const key of keys) {
            const found = firstDifference(Reflect.get(theirs, key), Reflect.get(ours, key), `${at}/${key}`);
            if (found !== '') return found;
        }
    }
    return `${at}: C# ${JSON.stringify(theirs)}, ours ${JSON.stringify(ours)}`;
}

// A JSON value with its objects' keys in order, so two answers compare by what they hold.
export function normal(value: unknown): unknown {
    if (Array.isArray(value)) return value.map(normal);
    if (typeof value !== 'object' || value === null) return value;
    return Object.fromEntries(Object.entries(value).toSorted(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0)).map(([k, v]) => [k, normal(v)]));
}
