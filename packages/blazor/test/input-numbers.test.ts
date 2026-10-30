// Input made while the Server circuit is down (ADR-0240 section 11): each call that carries what .NET carries out is
// numbered as it crosses, and the calls .NET says never arrived end as lost, so nothing waits on them for good.
import { describe, expect, it } from 'vitest';
import type { DotNetCalls } from '../src/held-messages';
import { InputNumbers, LostCall } from '../src/input-numbers';

// .NET as a connection that is down: a call is sent, and its promise never ends, as Blazor's does not.
class DownDotNet implements DotNetCalls {
    readonly calls: { readonly method: string; readonly args: readonly unknown[] }[] = [];

    invokeMethodAsync(method: string, ...args: readonly unknown[]): Promise<unknown> {
        this.calls.push({ method, args });
        return new Promise(() => undefined);
    }
}

const words = 'Input made while the connection to the server was lost was not carried out. Make it again.';

describe('input numbers (ADR-0240 section 11)', () => {
    it('ADR-0240: a numbered call crosses as Call with its number, method and arguments, in the order made', () => {
        const dotnet = new DownDotNet();
        const numbers = new InputNumbers(dotnet);
        void numbers.call('Heard', ['action', { row: 1 }]);
        void numbers.calls(new Set(['SetSorts']), dotnet).invokeMethodAsync('SetSorts', 3, '[]');
        void numbers.calls(new Set(['SetSorts']), dotnet).invokeMethodAsync('Release', [1], 2);
        expect(dotnet.calls).toEqual([
            { method: 'Call', args: [1, 'Heard', ['action', { row: 1 }]] },
            { method: 'Call', args: [2, 'SetSorts', [3, '[]']] },
            { method: 'Release', args: [[1], 2] },
        ]);
    });

    it('ADR-0240: a call .NET says never arrived ends as lost, with the words to tell the user', async () => {
        const numbers = new InputNumbers(new DownDotNet());
        const lost = numbers.call('Heard', ['edit', {}]);
        expect(numbers.lose([1], words)).toBe(true);
        const answer = await lost;
        expect(answer).toBeInstanceOf(LostCall);
        expect((answer as LostCall).words).toBe(words);
    });

    it('ADR-0240: a lost call that tells the user itself, as a refused edit does, is not said again', () => {
        const numbers = new InputNumbers(new DownDotNet());
        void numbers.call('Heard', ['edit', {}], true);
        void numbers.call('Heard', ['action', {}]);
        expect(numbers.lose([1], words)).toBe(false);
        expect(numbers.lose([2], words)).toBe(true);
    });

    it('ADR-0240: how far the numbers went is told with each input while a call is unanswered, until .NET has heard it', async () => {
        const answered = new InputNumbers({ invokeMethodAsync: () => Promise.resolve(null) });
        await answered.call('Heard', ['undo', null]);
        answered.tell();

        const down = new DownDotNet();
        const numbers = new InputNumbers(down);
        void numbers.call('Heard', ['undo', null]);
        // A tell made while the connection is down is lost as the call was: the next input tells again.
        numbers.tell();
        numbers.tell();
        expect(down.calls.filter(call => call.method === 'InputsSent').map(call => call.args)).toEqual([[1], [1]]);

        let hear: (() => void) | undefined;
        const up = {
            calls: [] as unknown[][],
            invokeMethodAsync(method: string, ...args: readonly unknown[]): Promise<unknown> {
                if (method === 'InputsSent') {
                    this.calls.push([...args]);
                    return new Promise<unknown>(resolve => { hear = () => { resolve(null); }; });
                }
                return new Promise(() => undefined);
            },
        };
        const heard = new InputNumbers(up);
        void heard.call('Heard', ['undo', null]);
        heard.tell();
        hear?.();
        await Promise.resolve();
        await Promise.resolve();
        heard.tell();
        expect(up.calls).toEqual([[1]]);
        void heard.call('Heard', ['redo', null]);
        heard.tell();
        expect(up.calls).toEqual([[1], [2]]);
    });

    it('ADR-0240: a call refused as it is made never crossed, and takes no number', () => {
        const dotnet = new DownDotNet();
        let refuse = true;
        const numbers = new InputNumbers({
            invokeMethodAsync: (method, ...args) => {
                if (refuse) throw new RangeError('Too large to hold.');
                return dotnet.invokeMethodAsync(method, ...args);
            },
        });
        expect(() => numbers.call('Heard', ['paste', {}])).toThrow('Too large to hold.');
        refuse = false;
        void numbers.call('Heard', ['undo', null]);
        numbers.tell();
        expect(dotnet.calls).toEqual([{ method: 'Call', args: [1, 'Heard', ['undo', null]] }, { method: 'InputsSent', args: [1] }]);
    });

    it('ADR-0240: a number the bridge never sent is a defect of .NET\'s, named', () => {
        const numbers = new InputNumbers(new DownDotNet());
        void numbers.call('Heard', ['undo', null]);
        expect(() => numbers.lose([2], words)).toThrow('Internal: .NET named input 2 lost, which the bridge never sent.');
    });
});
