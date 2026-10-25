// A Change Batch (ADR-0064, ADR-0067): the records added, the records changed and the Record Keys removed
// since a Snapshot, applied as one to make the next version, as ExGrid.Data's Snapshot.Apply makes it. Every
// check comes first, so a refused batch leaves nothing behind, and each refusal is worded as C# words it.
// The next version keeps the rows of the one before in their order, a changed record in the place of the
// record it replaces and an added one at the end, and its text under the codes the one before gave it: a
// dictionary only grows. Rows are copied in runs, and the Record Keys are found through an index the
// lineage carries forward, so a batch costs a copy of the columns and not a look at every key.

import {
    assembledColumn, lineageOf, nextSnapshot, type ColumnRun, type Lineage, type Snapshot, type SnapshotColumn, type SnapshotKind,
    type TextColumn,
} from './snapshot';
import { SnapshotError } from './snapshot-error';
import { elementAt, int64Max, int64Min } from './must';

/** The records a batch adds and changes, and the Record Keys it removes (ADR-0064). */
export interface ChangeBatch {
    /** The records the batch adds; they go at the end of the order. */
    readonly added: Snapshot | undefined;
    /** The records the batch changes, each found by its Record Key; each keeps the place of the record it
     * replaces. */
    readonly changed: Snapshot | undefined;
    /** The Record Keys of the records the batch removes: a string for a Text key, a bigint for an Integer
     * one. */
    readonly removedKeys: readonly (string | bigint)[];
}

/** What a Change Batch is made from. */
export interface ChangeBatchInput {
    /** The records the batch adds, a Snapshot with the Snapshot's columns. */
    readonly added?: Snapshot;
    /** The records the batch changes, a Snapshot with the Snapshot's columns. */
    readonly changed?: Snapshot;
    /** The Record Keys of the records the batch removes: strings for a Text key, bigints or safe integers for
     * an Integer one. */
    readonly removedKeys?: readonly (string | bigint | number)[];
}

/** What applying a batch made, and what a reader needs to fold it into what it computed rather than start
 * again (ADR-0067): take away what `removed` held in `before`, add what `added` holds in `after`. */
export interface SnapshotChange {
    /** The Snapshot the batch was applied to, exactly as it was. */
    readonly before: Snapshot;
    /** The next version. */
    readonly after: Snapshot;
    /** The rows of `before`, ascending, that `after` does not hold: the records removed, and the old
     * versions of the records changed. */
    readonly removed: readonly number[];
    /** The rows of `after`, ascending, that `before` did not hold: the new versions of the records changed,
     * and the records added. */
    readonly added: readonly number[];
}

/**
 * A batch of `added` records, `changed` records and `removedKeys`. Whether the keys are the Snapshot's is
 * decided when the batch is applied. A removed key that is neither a string nor an integer, or added and
 * changed records whose columns differ, are refused with a `TypeError`.
 */
export function createChangeBatch(input: ChangeBatchInput = {}): ChangeBatch {
    const { added, changed } = input;
    if (added !== undefined) lineageOf(added, "A batch's added records");
    if (changed !== undefined) lineageOf(changed, "A batch's changed records");
    if (added !== undefined && changed !== undefined) sameColumns(added, changed);
    const removedKeys = (input.removedKeys ?? []).map((key: unknown) => { // oxlint-disable-line ex-grid/no-stand-in-default -- a batch that names no removed key removes none
        if (key === null || key === undefined) throw new TypeError('A removed key is null; a Record Key is never Blank.');
        if (typeof key === 'string') return key;
        if (typeof key === 'bigint' && key >= int64Min && key <= int64Max) return key;
        if (typeof key === 'number' && Number.isSafeInteger(key)) return BigInt(key);
        throw new TypeError(`A removed key is a string or a 64-bit integer; ${describe(key)} (${typeof key}) is neither.`);
    });
    return Object.freeze({ added, changed, removedKeys: Object.freeze(removedKeys) });
}

/** A value as a refusal shows it. */
function describe(value: unknown): string {
    if (typeof value === 'object' && value !== null) return Object.prototype.toString.call(value);
    return String(value);
}

function sameColumns(added: Snapshot, changed: Snapshot): void {
    for (const column of added.columns) {
        const other = changed.columns.find(candidate => candidate.name === column.name);
        if (other?.kind !== column.kind) {
            throw new TypeError(`The added and the changed records have different columns: '${column.name}' is ${kindName(column.kind)} in the added records and ${other === undefined ? 'missing' : kindName(other.kind)} in the changed.`);
        }
    }
    const extra = changed.columns.find(column => !added.columns.some(candidate => candidate.name === column.name));
    if (extra !== undefined) {
        throw new TypeError(`The added and the changed records have different columns: '${extra.name}' is missing from the added records.`);
    }
}

/** A kind as ExGrid.Data names it: Text, Decimal, Double, Integer, Date, Boolean. */
function kindName(kind: SnapshotKind): string {
    return kind.charAt(0).toUpperCase() + kind.slice(1);
}

/**
 * The next version of `snapshot`, with the batch applied, and what changed (ADR-0064, ADR-0067). The version
 * moves on by one. A batch that changes or removes a key the Snapshot does not hold, adds one it holds, names
 * a key twice or Blank, or whose records have other columns, is refused whole with a {@link SnapshotError}
 * naming the key or the column, and nothing of it is applied; so is a batch that changes or removes records
 * of a Snapshot without a Record Key.
 */
export function applyChangeBatch(snapshot: Snapshot, batch: ChangeBatch): SnapshotChange {
    const lineage = lineageOf(snapshot, 'The Snapshot a batch is applied to');
    const changed = batch.changed === undefined ? undefined : matched(snapshot, batch.changed, 'changed');
    const added = batch.added === undefined ? undefined : matched(snapshot, batch.added, 'added');
    if (snapshot.recordKey === undefined) {
        if ((batch.changed?.rowCount ?? 0) > 0 || batch.removedKeys.length > 0) { // oxlint-disable-line ex-grid/no-stand-in-default -- a batch without changed records changes none
            throw new SnapshotError('The Snapshot has no Record Key, so it takes only a batch that adds, and this batch changes or removes records.');
        }
        return executed(snapshot, lineage, new Map(), [], added, changed, undefined);
    }
    const key = snapshot.column(snapshot.recordKey);
    const index = keyIndexOf(snapshot, key, lineage);
    const checked = checkedKeys(snapshot, key, index, batch, changed, added);
    return executed(snapshot, lineage, checked.moves, checked.removedKeys, added, changed, { key, index });
}

// ---- matching the batch's columns ----------------------------------------------------------------

/** A batch's records as they meet the Snapshot's columns: each of the Snapshot's columns, the batch's of
 * the same name. */
interface Source {
    readonly snapshot: Snapshot;
    readonly columns: readonly SnapshotColumn[];
}

function matched(target: Snapshot, source: Snapshot, role: string): Source {
    const columns = target.columns.map(column => {
        const other = source.columns.find(candidate => candidate.name === column.name);
        if (other === undefined) throw new SnapshotError(`the batch's ${role} records have no such column.`, { column: column.name });
        if (other.kind !== column.kind) {
            throw new SnapshotError(`the column is ${kindName(column.kind)} in the Snapshot and ${kindName(other.kind)} in the batch's ${role} records.`, { column: column.name });
        }
        return other;
    });
    const extra = source.columns.find(column => !target.columns.some(candidate => candidate.name === column.name));
    if (extra !== undefined) throw new SnapshotError(`the batch's ${role} records carry a column the Snapshot does not have.`, { column: extra.name });
    return { snapshot: source, columns };
}

// ---- the Record Keys ------------------------------------------------------------------------------

type Key = string | bigint;

/**
 * The positions a version's Record Keys stand at: a base built once over a version, and the keys batches
 * have added or removed since, a removed key at -1. Each version made from the one before carries its own
 * copy of what changed, so two versions made from one keep their own keys.
 */
interface KeyIndex {
    readonly base: ReadonlyMap<Key, number>;
    readonly since: ReadonlyMap<Key, number>;
}

const keyIndexes = new WeakMap<Snapshot, KeyIndex>();

/** Past this many keys changed since its base, beside a base this many times larger, an index is built
 * anew over the version, when a batch next asks for it. */
const mostSince = 4096;
const baseToSince = 8;

function keyIndexOf(snapshot: Snapshot, key: SnapshotColumn, lineage: Lineage): KeyIndex {
    const held = keyIndexes.get(snapshot);
    if (held !== undefined) return held;
    const base = new Map<Key, number>();
    for (let row = 0; row < snapshot.rowCount; row++) base.set(keyAt(key, row), positionOf(lineage, row));
    const index = { base, since: new Map<Key, number>() };
    keyIndexes.set(snapshot, index);
    return index;
}

/** The row a key stands at, or `undefined` where the version does not hold it. */
function rowOf(index: KeyIndex, lineage: Lineage, key: Key): number | undefined {
    const position = index.since.has(key) ? index.since.get(key) : index.base.get(key);
    if (position === undefined || position < 0) return undefined;
    const positions = lineage.positions;
    if (positions === undefined) return position;
    let low = 0;
    let high = positions.length - 1;
    while (low <= high) {
        const middle = (low + high) >> 1;
        const at = elementAt(positions, middle);
        if (at === position) return middle;
        if (at < position) low = middle + 1;
        else high = middle - 1;
    }
    throw new Error(`Internal: the key ${show(key)} stands at position ${position}, which no row holds.`);
}

function positionOf(lineage: Lineage, row: number): number {
    return lineage.positions === undefined ? row : elementAt(lineage.positions, row);
}

/** A row's key, which the Snapshot's checks keep from being Blank. */
function keyAt(key: SnapshotColumn, row: number): Key {
    const value = key.valueAt(row);
    if (typeof value === 'string' || typeof value === 'bigint') return value;
    throw new Error(`Internal: row ${row} of the Record Key '${key.name}' holds ${describe(value)}.`);
}

/** A key as ExGrid.Data shows it: text between single quotes, an integer as it is. */
function show(key: Key): string {
    return typeof key === 'string' ? `'${key}'` : key.toString();
}

function refused(column: SnapshotColumn, key: Key, reason: string): SnapshotError {
    return new SnapshotError(reason.replace('{0}', show(key)), { column: column.name, key });
}

/** A batch record's key, or `undefined` for a Blank. */
function batchKeyAt(column: SnapshotColumn, row: number): Key | undefined {
    const value = column.valueAt(row);
    return value === null ? undefined : keyOfValue(value);
}

function keyOfValue(value: unknown): Key {
    if (typeof value === 'string' || typeof value === 'bigint') return value;
    throw new Error(`Internal: a Record Key holds ${describe(value)}.`);
}

interface Checked {
    /** Each changed record's batch row, by the row of the record it replaces. */
    readonly moves: ReadonlyMap<number, number>;
    /** The keys removed, and the rows they stood at. */
    readonly removedKeys: readonly { readonly key: Key; readonly row: number }[];
}

/**
 * Every key rule, against the Snapshot before the batch, in ExGrid.Data's order: a removed or changed key is
 * held, an added key is not unless the batch removes it, no key comes twice in one role or in two roles that
 * contradict, and no key is Blank.
 */
function checkedKeys(before: Snapshot, key: SnapshotColumn, index: KeyIndex, batch: ChangeBatch, changed: Source | undefined,
    added: Source | undefined): Checked {
    const lineage = lineageOf(before, 'The Snapshot a batch is applied to');
    const keyOrdinal = before.columns.indexOf(key);
    const removed = new Set<Key>();
    const removedKeys: { key: Key; row: number }[] = [];
    for (const raw of batch.removedKeys) {
        if ((typeof raw === 'string') !== (key.kind === 'text')) {
            throw refused(key, raw, `the batch removes the key {0}, which is not of the Record Key's kind, ${kindName(key.kind)}.`);
        }
        if (removed.has(raw)) throw refused(key, raw, 'the batch removes the key {0} twice.');
        removed.add(raw);
        const row = rowOf(index, lineage, raw);
        if (row === undefined) throw refused(key, raw, 'the batch removes the key {0}, which the Snapshot does not hold.');
        removedKeys.push({ key: raw, row });
    }

    const moves = new Map<number, number>();
    const changedKeys = new Set<Key>();
    if (changed !== undefined) {
        const column = elementAt(changed.columns, keyOrdinal);
        for (let i = 0; i < changed.snapshot.rowCount; i++) {
            const value = batchKeyAt(column, i);
            if (value === undefined) throw new SnapshotError('the Record Key of this changed record is Blank.', { row: i + 1, column: key.name });
            if (removed.has(value)) throw refused(key, value, 'the batch both changes and removes the key {0}.');
            if (changedKeys.has(value)) throw refused(key, value, 'the batch changes the key {0} twice.');
            changedKeys.add(value);
            const row = rowOf(index, lineage, value);
            if (row === undefined) throw refused(key, value, 'the batch changes the key {0}, which the Snapshot does not hold.');
            moves.set(row, i);
        }
    }

    if (added !== undefined) {
        const column = elementAt(added.columns, keyOrdinal);
        const addedKeys = new Set<Key>();
        for (let i = 0; i < added.snapshot.rowCount; i++) {
            const value = batchKeyAt(column, i);
            if (value === undefined) throw new SnapshotError('the Record Key of this added record is Blank.', { row: i + 1, column: key.name });
            if (addedKeys.has(value)) throw refused(key, value, 'the batch adds the key {0} twice.');
            addedKeys.add(value);
            if (changedKeys.has(value) || (!removed.has(value) && rowOf(index, lineage, value) !== undefined)) {
                throw refused(key, value, 'the batch adds the key {0}, which the Snapshot already holds.');
            }
        }
    }
    return { moves, removedKeys };
}

// ---- the next version -----------------------------------------------------------------------------

/** Rows [`from`, `to`) of the Snapshot before, or of the batch's changed or added records. */
interface Run {
    readonly source: 'before' | 'changed' | 'added';
    readonly from: number;
    readonly to: number;
    /** For a changed record, the row before of the record it replaces. */
    readonly replaces?: number;
}

function executed(before: Snapshot, lineage: Lineage, moves: ReadonlyMap<number, number>, removedKeys: readonly { readonly key: Key; readonly row: number }[],
    added: Source | undefined, changed: Source | undefined, keyed: { readonly key: SnapshotColumn; readonly index: KeyIndex } | undefined): SnapshotChange {
    const leaving = new Set<number>([...moves.keys(), ...removedKeys.map(removed => removed.row)]);
    const leavingRows = [...leaving].toSorted((a, b) => a - b);
    const addedCount = added?.snapshot.rowCount ?? 0; // oxlint-disable-line ex-grid/no-stand-in-default -- a batch without added records adds none

    // The rows of the next version: the rows before, in order, a changed record in the place of the one it
    // replaces and a removed one left out; then the records added.
    const runs: Run[] = [];
    let from = 0;
    for (const row of leavingRows) {
        if (row > from) runs.push({ source: 'before', from, to: row });
        const move = moves.get(row);
        if (move !== undefined) runs.push({ source: 'changed', from: move, to: move + 1, replaces: row });
        from = row + 1;
    }
    if (before.rowCount > from) runs.push({ source: 'before', from, to: before.rowCount });
    if (addedCount > 0) runs.push({ source: 'added', from: 0, to: addedCount });
    const rowCount = before.rowCount - removedKeys.length + addedCount;

    const sourceOf = (run: Run): Source => {
        if (run.source === 'changed' && changed !== undefined) return changed;
        if (run.source === 'added' && added !== undefined) return added;
        return { snapshot: before, columns: before.columns };
    };
    const columns = before.columns.map((column, ordinal) => {
        const columnRuns: ColumnRun[] = runs.map(run => ({ column: elementAt(sourceOf(run).columns, ordinal), from: run.from, to: run.to }));
        if (column.kind !== 'text') return assembledColumn(column, columnRuns, [], new Map());
        const { dictionary, remaps } = remappedText(column, columnRuns);
        return assembledColumn(column, columnRuns, dictionary, remaps);
    });

    const nextLineage = lineageAfter(lineage, before.rowCount, runs, removedKeys.length, addedCount);
    const after = nextSnapshot(columns, rowCount, before.recordKey, before.version + 1n, nextLineage);
    if (keyed !== undefined) {
        const next = indexAfter(keyed, removedKeys, added, before, lineage);
        if (next !== undefined) keyIndexes.set(after, next);
    }

    const addedRows: number[] = [];
    let at = 0;
    for (const run of runs) {
        if (run.source !== 'before') for (let row = run.from; row < run.to; row++) addedRows.push(at + row - run.from);
        at += run.to - run.from;
    }
    return Object.freeze({ before, after, removed: Object.freeze(leavingRows), added: Object.freeze(addedRows) });
}

/**
 * The next version's dictionary and the codes a batch's text takes in it: the dictionary before, with the
 * text it does not hold yet appended in the order the new rows bring it, so a code keeps its meaning in
 * every version.
 */
function remappedText(column: TextColumn, runs: readonly ColumnRun[]): { dictionary: readonly string[]; remaps: Map<SnapshotColumn, Int32Array> } {
    const dictionary = [...column.dictionary];
    const codes = new Map(dictionary.map((text, code) => [text, code]));
    const remaps = new Map<SnapshotColumn, Int32Array>();
    for (const run of runs) {
        if (run.column === column) continue;
        const source = run.column;
        if (source.kind !== 'text') throw new Error(`Internal: the batch's column '${source.name}' is ${source.kind}.`);
        let remap = remaps.get(source);
        if (remap === undefined) {
            remap = new Int32Array(source.dictionary.length).fill(-2);
            remaps.set(source, remap);
        }
        for (let row = run.from; row < run.to; row++) {
            const code = source.codeAt(row);
            if (code < 0 || elementAt(remap, code) !== -2) continue;
            const text = elementAt(source.dictionary, code);
            let target = codes.get(text);
            if (target === undefined) {
                target = dictionary.length;
                dictionary.push(text);
                codes.set(text, target);
            }
            remap[code] = target;
        }
    }
    return { dictionary, remaps };
}

/** Where the next version's rows stand: the rows kept and changed where they stood, the records added past
 * every position so far. */
function lineageAfter(lineage: Lineage, rowCount: number, runs: readonly Run[], removedCount: number, addedCount: number): Lineage {
    const nextPosition = lineage.nextPosition + addedCount;
    if (lineage.positions === undefined && removedCount === 0 && lineage.nextPosition === rowCount) {
        return { positions: undefined, nextPosition };
    }
    const positions = new Float64Array(rowCount - removedCount + addedCount);
    let at = 0;
    for (const run of runs) {
        const length = run.to - run.from;
        if (run.source === 'added') {
            for (let i = 0; i < length; i++) positions[at + i] = lineage.nextPosition + run.from + i;
        } else if (run.source === 'changed') {
            // A changed record stands where the record it replaces stood.
            if (run.replaces === undefined) throw new Error('Internal: a changed run names no row it replaces.');
            positions[at] = positionOf(lineage, run.replaces);
        } else {
            for (let i = 0; i < length; i++) positions[at + i] = positionOf(lineage, run.from + i);
        }
        at += length;
    }
    return { positions, nextPosition };
}

/** The key index of the next version: the one before's, with the keys removed at -1 and the keys added at
 * the positions they took; dropped, to be built anew, once the keys changed since its base pass its bound. */
function indexAfter(keyed: { readonly key: SnapshotColumn; readonly index: KeyIndex }, removedKeys: readonly { readonly key: Key }[],
    added: Source | undefined, before: Snapshot, lineage: Lineage): KeyIndex | undefined {
    const since = new Map(keyed.index.since);
    for (const removed of removedKeys) since.set(removed.key, -1);
    if (added !== undefined) {
        const column = elementAt(added.columns, before.columns.indexOf(keyed.key));
        for (let i = 0; i < added.snapshot.rowCount; i++) since.set(keyOfValue(column.valueAt(i)), lineage.nextPosition + i);
    }
    if (since.size > Math.max(keyed.index.base.size / baseToSince, mostSince)) return undefined;
    return { base: keyed.index.base, since };
}
