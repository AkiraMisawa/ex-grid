// What confirming an output hands on (ADR-0340, Q6): C#'s BoardOutput.

import { BoardRefusedError, decimalText, ticksText, type Snapshot, type SnapshotKind } from '@ex-grid/data';
import { narrowedPositions, narrowedSnapshot, type BoardValue, type LinkCondition } from './link-condition';

/**
 * What confirming an output hands on (ADR-0340, Q6): the values its grid's selection held in its columns, on its
 * input, with the version of the input's data they were taken against. A workflow takes the rows they name from
 * the data it holds ({@link BoardOutput.take}), or writes them as JSON ({@link BoardOutput.toJson}).
 */
export class BoardOutput {
    readonly #takenFrom: Snapshot;
    readonly #kinds: readonly SnapshotKind[];

    /** The output's name, as the Board declares it. */
    readonly name: string;
    /** The input its values were taken from. */
    readonly input: string;
    /** The version of the input's data the values were taken against. */
    readonly sourceVersion: bigint;
    /** The values chosen, as a link's condition on the output's columns. */
    readonly condition: LinkCondition;

    /** @internal Made by a bound Board when the user confirms. */
    constructor(name: string, input: string, takenFrom: Snapshot, condition: LinkCondition, kinds: readonly SnapshotKind[]) {
        this.name = name;
        this.input = input;
        this.#takenFrom = takenFrom;
        this.#kinds = Object.freeze([...kinds]);
        this.sourceVersion = takenFrom.version;
        this.condition = condition;
        Object.freeze(this);
    }

    /**
     * The rows of `data` the values chosen name, in its order. Refused, with a {@link BoardRefusedError} at
     * `outputs.<name>`: data of another version than the one the values were taken against, or, at version 0,
     * which names none, any data but that same Snapshot; and data missing a column handed on, or holding it as
     * another kind.
     */
    take(data: Snapshot): Snapshot {
        const path = `outputs.${this.name}`;
        if (data !== this.#takenFrom) {
            if (data.version !== this.sourceVersion) {
                throw new BoardRefusedError(path, `Output '${this.name}' was taken against version ${this.sourceVersion} of input '${this.input}', and the data is `
                    + `version ${data.version}; the values the user chose may no longer be these.`);
            }
            if (this.sourceVersion === 0n) {
                throw new BoardRefusedError(path, `Output '${this.name}' was taken against input '${this.input}' at version 0, which names no version, so only that `
                    + 'same data can be taken; a source that moves on gives its data a version.');
            }
        }
        this.condition.on.forEach((name, i) => {
            const held = data.columns.find(column => column.name === name);
            if (held === undefined) {
                throw new BoardRefusedError(path, `Output '${this.name}' hands on column '${name}', which the data of input '${this.input}' does not have.`);
            }
            const kind = this.#kinds[i];
            if (held.kind !== kind) {
                throw new BoardRefusedError(path, `Output '${this.name}', column '${name}': chosen as a ${String(kind)}, and the data holds a ${held.kind}.`);
            }
        });
        return narrowedSnapshot(data, narrowedPositions(data, [this.condition]));
    }

    /**
     * The output as JSON: `{"output": "chosen", "input": "trades", "version": 3, "on": ["desk"], "values": [["Rates"]]}`.
     * A Decimal is written as its number, a double that is no number as .NET writes it (`"NaN"`, `"Infinity"`), and
     * a Date as `yyyy-MM-dd`, or with its time where it has one.
     */
    toJson(): string {
        const values = this.condition.values.map(tuple => `[${tuple.map((value, i) => written(value, this.#kinds[i])).join(',')}]`);
        return `{"output":${JSON.stringify(this.name)},"input":${JSON.stringify(this.input)},"version":${this.sourceVersion},`
            + `"on":[${this.condition.on.map(name => JSON.stringify(name)).join(',')}],"values":[${values.join(',')}]}`;
    }
}

function written(value: BoardValue, kind: SnapshotKind | undefined): string {
    if (value === null) return 'null';
    switch (typeof value) {
        case 'string': return JSON.stringify(value);
        case 'boolean': return value ? 'true' : 'false';
        case 'number': {
            if (Number.isFinite(value)) return JSON.stringify(value);
            // JSON has no NaN or infinity: written as .NET writes them, never as a number.
            return Number.isNaN(value) ? '"NaN"' : value > 0 ? '"Infinity"' : '"-Infinity"';
        }
        case 'bigint': {
            if (kind !== 'date') return value.toString();
            const text = ticksText(value);
            return JSON.stringify(text.endsWith('T00:00:00') ? text.slice(0, -'T00:00:00'.length) : text);
        }
        default: return decimalText(value);
    }
}
