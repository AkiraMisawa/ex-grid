// The Snapshots a Blazor component sends its Script Core (ADR-0065, ADR-0370 section 1): read from their Arrow
// streams under .NET's numbers, held only as their message applies, and let go once no message names them. How a
// product's bridge orders its messages around them is tested with that bridge (board-blazor's).

import { describe, expect, it } from 'vitest';
import { createSnapshot, writeArrowSnapshot, type Snapshot } from '@ex-grid/data';
import { HeldSnapshots, type DotNetStream } from '../src/snapshots';

function stream(snapshot: Snapshot): DotNetStream {
    return {
        arrayBuffer: () => {
            const bytes = writeArrowSnapshot(snapshot);
            const copy = new ArrayBuffer(bytes.byteLength);
            new Uint8Array(copy).set(bytes);
            return Promise.resolve(copy);
        },
    };
}

const desks = (version: number, ...names: string[]): Snapshot => createSnapshot({ version, columns: [{ name: 'desk', kind: 'text', values: names }] });

describe('ADR-0065: a Snapshot crosses to the browser once, as its Arrow stream, and is named by its number after', () => {
    it('holds what a message carried only once it applies, and lets go of what the data no longer names', async () => {
        const held = new HeldSnapshots();
        const read = await HeldSnapshots.read([1, 2], [stream(desks(3, 'Rates')), stream(desks(4, 'FX'))]);

        expect(() => held.data({ trades: 1 })).toThrow("Internal: input 'trades' names Snapshot 1, which .NET never sent or the browser let go.");
        held.hold(read);
        const data = held.data({ trades: 1, other: 2 });
        expect([data['trades']?.version, data['other']?.version]).toEqual([3n, 4n]);
        expect(data['trades']?.columns[0]?.valueAt(0)).toBe('Rates');

        expect(held.release({ trades: 2 })).toEqual([1]);
        expect(held.release({ trades: 2 })).toEqual([]);
        expect(() => held.data({ trades: 1 })).toThrow('which .NET never sent or the browser let go');
    });

    it('refuses a message whose streams are not the Snapshots it numbers', async () => {
        await expect(HeldSnapshots.read([1, 2], [stream(desks(1, 'Rates'))])).rejects.toThrow('Internal: .NET numbered 2 Snapshots and sent 1.');
    });
});
