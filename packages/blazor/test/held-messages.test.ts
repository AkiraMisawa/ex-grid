// A call too large for Blazor Server's hub (ADR-0005, as amended 2026-10-28): held in the browser, and handed to
// .NET by number as a gzip stream, which .NET reads back to the very JSON the call would have carried.
import { gunzipSync } from 'node:zlib';
import { describe, expect, it } from 'vitest';
import { crossingCalls, HeldMessages, inlineBytes, type DotNetCalls } from '../src/held-messages';

class FakeDotNet implements DotNetCalls {
    readonly calls: { readonly method: string; readonly args: readonly unknown[] }[] = [];

    invokeMethodAsync(method: string, ...args: readonly unknown[]): Promise<unknown> {
        this.calls.push({ method, args });
        return Promise.resolve(null);
    }
}

const large = 'é'.repeat(inlineBytes);
const holdable: ReadonlySet<string> = new Set(['Heard']);

describe('held messages (ADR-0005)', () => {
    it('ADR-0005: a call within a message crosses as it is', () => {
        const dotnet = new FakeDotNet();
        void crossingCalls(dotnet, new HeldMessages(true), holdable).invokeMethodAsync('Heard', 'edit', { value: 'short' });
        expect(dotnet.calls).toEqual([{ method: 'Heard', args: ['edit', { value: 'short' }] }]);
    });

    it('ADR-0005: a call larger than a message is held, and .NET takes its arguments once, as gzip of their JSON', async () => {
        const dotnet = new FakeDotNet();
        const held = new HeldMessages(true);
        void crossingCalls(dotnet, held, holdable).invokeMethodAsync('Heard', 'paste', { values: [[large]] });
        const json = JSON.stringify(['paste', { values: [[large]] }]);
        const bytes = new TextEncoder().encode(json).byteLength;
        expect(dotnet.calls).toEqual([{ method: 'Held', args: ['Heard', { held: 1, bytes }] }]);
        const taken = await held.take(1);
        expect([taken[0], taken[1]]).toEqual([0x1f, 0x8b]);
        expect(gunzipSync(taken).toString('utf8')).toBe(json);
        expect(() => held.take(1)).toThrow('Internal: .NET took the message 1, which the bridge does not hold.');
    });

    it('ADR-0005: without a circuit nothing is held, as WebAssembly takes any message', () => {
        const dotnet = new FakeDotNet();
        void crossingCalls(dotnet, new HeldMessages(false), holdable).invokeMethodAsync('Heard', 'paste', { values: [[large]] });
        expect(dotnet.calls.map(call => call.method)).toEqual(['Heard']);
    });

    it('ADR-0005: a message whose UTF-8 fits is not held, however long its UTF-16 guess', () => {
        const dotnet = new FakeDotNet();
        void crossingCalls(dotnet, new HeldMessages(true), holdable).invokeMethodAsync('Heard', 'edit', 'a'.repeat(inlineBytes - 100));
        expect(dotnet.calls.map(call => call.method)).toEqual(['Heard']);
    });

    it('ADR-0005: only a call .NET makes held is measured and held; any other crosses as it is', () => {
        const dotnet = new FakeDotNet();
        void crossingCalls(dotnet, new HeldMessages(true), holdable).invokeMethodAsync('Rows', [[large]]);
        expect(dotnet.calls.map(call => call.method)).toEqual(['Rows']);
    });

    it('ADR-0005: a held message .NET answered without taking it is let go', async () => {
        const dotnet = new FakeDotNet();
        const held = new HeldMessages(true);
        await crossingCalls(dotnet, held, holdable).invokeMethodAsync('Heard', 'paste', { values: [[large]] });
        expect(() => held.take(1)).toThrow('Internal: .NET took the message 1, which the bridge does not hold.');
    });
});
