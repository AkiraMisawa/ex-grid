// The Snapshots a Blazor component sends its Script Core (ADR-0065, ADR-0370 section 1), the browser's half: each
// crosses once, as the Arrow stream ADR-0065 has a Snapshot travel, under the number .NET gave it; a later message
// names it by that number alone, and the browser says which numbers it has let go, so .NET lets go of them too.
// .NET's half is src/Shared/Script/SnapshotCrossing.cs, compiled into each adapter that sends Snapshots. Part of the
// Shared Script (ADR-0411), so that every product's module crosses them alike.

import { readArrowSnapshot, type Snapshot } from '@ex-grid/data';

/** A Snapshot as .NET sends it: Blazor's `DotNetStreamReference` over its Arrow stream (ADR-0065). */
export interface DotNetStream {
    arrayBuffer(): Promise<ArrayBuffer>;
}

/** The Snapshots one message carried, read and not yet held: {@link HeldSnapshots.read}. */
export type SnapshotsRead = readonly (readonly [number, Snapshot])[];

/**
 * The Snapshots the browser holds for a component, by the number .NET gave each. A message's Snapshots are read as
 * it arrives ({@link HeldSnapshots.read}) and held only as the message applies ({@link HeldSnapshots.hold}), so that a
 * message before it, applied meanwhile, lets go of nothing it carries.
 */
export class HeldSnapshots {
    readonly #held = new Map<number, Snapshot>();

    /** Reads each Snapshot a message carries, `streams` in the order of `sent`, the numbers .NET gave them. */
    static async read(sent: readonly number[], streams: readonly DotNetStream[]): Promise<SnapshotsRead> {
        if (sent.length !== streams.length) throw new Error(`Internal: .NET numbered ${String(sent.length)} Snapshots and sent ${String(streams.length)}.`);
        const snapshots = await Promise.all(streams.map(async stream => readArrowSnapshot(await stream.arrayBuffer())));
        return snapshots.map((snapshot, i) => {
            const number = sent[i];
            if (number === undefined) throw new Error(`Internal: Snapshot ${String(i)} has no number.`);
            return [number, snapshot] as const;
        });
    }

    /** Holds what {@link HeldSnapshots.read} read, as the message that carried it applies. */
    hold(read: SnapshotsRead): void {
        for (const [number, snapshot] of read) this.#held.set(number, snapshot);
    }

    /** The data a message names, by input: each Snapshot by its number. A number not held is the bridge's own defect,
     * refused, never data drawn as nothing. */
    data(numbers: Readonly<Record<string, number>>): Readonly<Record<string, Snapshot>> {
        return Object.freeze(Object.fromEntries(Object.entries(numbers).map(([name, number]) => {
            const snapshot = this.#held.get(number);
            if (snapshot === undefined) throw new Error(`Internal: input '${name}' names Snapshot ${String(number)}, which .NET never sent or the browser let go.`);
            return [name, snapshot];
        })));
    }

    /** Lets go of every Snapshot `numbers` does not name, and answers their numbers, for .NET to let go of them too;
     * called once anything raised from them has been told. */
    release(numbers: Readonly<Record<string, number>>): readonly number[] {
        const named = new Set(Object.values(numbers));
        const gone = [...this.#held.keys()].filter(number => !named.has(number));
        for (const number of gone) this.#held.delete(number);
        return gone;
    }

    /** Lets go of every Snapshot, as the component stops. */
    clear(): void {
        this.#held.clear();
    }
}
