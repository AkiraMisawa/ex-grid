// Code the lint must refuse. Each line marked `expect:` (or the line after `expect-next:`) must draw exactly the rules named, and
// no other line may draw any;
// `npm run lint` checks it first (lint/check.mjs), so a rule that stops firing fails the lint rather than
// passing everything quietly.

export function lookups(values: readonly number[], byName: ReadonlyMap<string, number>, name: string | undefined): number {
    const first = values[0] ?? 0; // expect: ex-grid(no-stand-in-default)
    const named = byName.get('x') ?? -1; // expect: ex-grid(no-stand-in-default)
    const label = name ?? ''; // expect: ex-grid(no-stand-in-default)
    const second = values[1]!; // expect: typescript(no-non-null-assertion)
    return first + named + second + label.length;
}

export function truthiness(count: number, name: string | undefined): boolean {
    if (count) { // expect: typescript(strict-boolean-expressions)
        return true;
    }
    return name !== undefined && name.length > 0;
}

export function swallowed(text: string): number {
    try {
        return Number(JSON.parse(text));
    } catch { // expect: ex-grid(no-catch)
        return 0;
    }
}

export function rethrown(text: string): unknown {
    try {
        return JSON.parse(text);
    } catch (error) {
        throw new Error('The text is not JSON.', { cause: error });
    }
}

export function rejected(work: Promise<number>): Promise<number> {
    return work.catch(() => 0); // expect: ex-grid(no-catch)
}

export function escaped(input: unknown): number {
    const loose = input as any; // expect: typescript(no-explicit-any), typescript(no-unsafe-assignment), typescript(no-unsafe-type-assertion)
    return loose.size; // expect: typescript(no-unsafe-member-access), typescript(no-unsafe-return)
}

export function silenced(name: string | undefined): string {
    // expect-next: ex-grid(disable-states-why)
    // oxlint-disable-next-line ex-grid/no-stand-in-default
    return name ?? '';
}

export function excused(name: string | undefined): string {
    // oxlint-disable-next-line ex-grid/no-stand-in-default -- the fixture's example of a reasoned exception
    return name ?? '';
}

export const noop = (): void => {}; // expect: eslint(no-empty-function)
