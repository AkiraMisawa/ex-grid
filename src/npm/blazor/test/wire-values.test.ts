// Runs the shared cases of kind `wireValue` (tests/cases/README.md): the JSON .NET's ScriptValues writes for a
// cell's value, read by the bridge as the Script Core reads a value. xUnit holds the C# half to the same text.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { valueOf } from '../src/wire-values';

interface WireCase {
    readonly name: string;
    readonly wire?: unknown;
    readonly reads?: { readonly type: string; readonly text: string };
    readonly refused?: string;
}

const dir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../tests/cases/blazor');
const files = fs.readdirSync(dir).filter(name => name.endsWith('.json')).toSorted()
    .map(name => JSON.parse(fs.readFileSync(path.join(dir, name), 'utf8')) as { readonly kind: string; readonly cases: readonly WireCase[] })
    .filter(file => file.kind === 'wireValue');

/** A value's type as a case names it: `typeof`, the Temporal type's name, or `null`. */
function typeOf(value: unknown): string {
    if (value === null) return 'null';
    if (typeof value !== 'object') return typeof value;
    const tag = /^\[object Temporal\.(\w+)\]$/.exec(Object.prototype.toString.call(value));
    if (tag?.[1] === undefined) throw new Error(`The bridge read an object that is no Temporal value: ${Object.prototype.toString.call(value)}.`);
    return tag[1];
}

describe('wireValue', () => {
    for (const file of files) {
        for (const entry of file.cases.filter(each => 'wire' in each)) {
            it(entry.name, () => {
                if (entry.refused !== undefined) {
                    expect(entry.refused).toBe('argument');
                    expect(() => valueOf(entry.wire)).toThrow(TypeError);
                    return;
                }
                if (entry.reads === undefined) throw new Error(`The case "${entry.name}" says neither what the bridge reads nor that it refuses.`);
                const value = valueOf(entry.wire);
                expect(typeOf(value)).toBe(entry.reads.type);
                expect(String(value)).toBe(entry.reads.text);
            });
        }
    }
});
