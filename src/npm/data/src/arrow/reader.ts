// One read of an Arrow IPC stream or file into a Snapshot (ADR-0065), as ExGrid.Data.Arrow's
// ArrowSnapshotReader reads one: the schema declares the columns — every type checked against ADR-0065's
// table before a row is read — and each record batch is appended in chunks, with a checkpoint after each,
// so a browser keeps painting and a cancellation is seen. A read yields a Snapshot or refuses whole.

import { SnapshotError, grouped } from '../snapshot-error';
import { sealSnapshot, type Snapshot, type SnapshotKind } from '../snapshot';
import { FlatTable } from './flat';
import {
    dictionaryBatchMessage, fileFooter, firstMessage, flat, isArrowFile, recordBatchMessage, schemaMessage,
    streamMessages, unreadable,
} from './ipc';
import { readSchema, type ArrowField, type ArrowSchema } from './schema';
import { ColumnReader, chunkRows, type ArrayData, type DictionaryData } from './columns';
import { elementAt, int64Max } from '../must';

/** The metadata keys under which a Snapshot's own facts travel in an Arrow schema (ADR-0065). Another
 * producer may write them too, and a read honours them whoever wrote them. */
export const snapshotArrowMetadata = Object.freeze({
    /** A field's caption, written only when it is not the column's name. */
    caption: 'exgrid.caption',
    /** The schema's Record Key: the name of the Text or Integer column whose value tells each record apart. */
    recordKey: 'exgrid.recordKey',
    /** The schema's Snapshot version, in decimal digits; a read without it gives version 0. */
    version: 'exgrid.version',
} as const);

/** The media type of an Arrow IPC stream, as IANA registers it: what a server answers a Snapshot with. */
export const arrowStreamMediaType = 'application/vnd.apache.arrow.stream';

/** How far a read has come. */
export interface SnapshotProgress {
    /** The rows read so far. */
    readonly rows: number;
    /** The rows there are to read, when that is known. */
    readonly totalRows: number | undefined;
    /** The bytes read so far, when the read can say. */
    readonly bytes: number | undefined;
    /** The bytes there are to read, when that is known. */
    readonly totalBytes: number | undefined;
}

/** How a read paces itself (ADR-0064): in slices sized by time, yielding between them so that a browser
 * keeps painting while a million rows load, and reporting its progress after each. */
export interface SnapshotReadOptions {
    /** Cancels the read: it rejects with the signal's reason and yields nothing. */
    readonly signal?: AbortSignal;
    /** Told how far the read has come after every slice, and once more at the end. */
    readonly onProgress?: (progress: SnapshotProgress) => void;
    /** How long one slice works, in milliseconds, before the read yields; 30 when left out. A slice always
     * does some work, so 0 yields after every chunk of rows. */
    readonly sliceBudgetMs?: number;
    /** What the read awaits between slices; a macrotask (`setTimeout(…, 0)`) when left out, which gives a
     * browser a turn of its event loop to paint in. */
    readonly yield?: () => Promise<void>;
}

/** The shape a schema says: its columns' names and kinds, as a read would make them (ADR-0341). */
export interface DataShape {
    /** The columns, in order. */
    readonly columns: readonly DataShapeColumn[];
}

/** One column of a {@link DataShape}. */
export interface DataShapeColumn {
    /** The column's name. */
    readonly name: string;
    /** The kind a read gives it. */
    readonly kind: SnapshotKind;
}

function macrotask(): Promise<void> {
    return new Promise(resolve => {
        setTimeout(resolve, 0);
    });
}

/** Paces a read in slices sized by time; cancellation is looked at between chunks. */
class Pacer {
    readonly #budget: number;
    readonly #yield: () => Promise<void>;
    readonly #onProgress: ((progress: SnapshotProgress) => void) | undefined;
    readonly #signal: AbortSignal | undefined;
    #sliceStart = performance.now();

    constructor(options: SnapshotReadOptions) {
        const budget = options.sliceBudgetMs === undefined ? 30 : options.sliceBudgetMs;
        if (typeof budget !== 'number' || Number.isNaN(budget)) throw new TypeError(`The slice budget ${String(budget)} is not a number of milliseconds.`);
        this.#budget = Math.max(0, budget);
        this.#yield = options.yield === undefined ? macrotask : options.yield;
        this.#onProgress = options.onProgress;
        this.#signal = options.signal;
    }

    cancelled(): void {
        this.#signal?.throwIfAborted();
    }

    /** A point between chunks: when the slice has spent its budget, reports `progress` and yields. */
    async checkpoint(progress: SnapshotProgress): Promise<void> {
        this.cancelled();
        if (performance.now() < this.#sliceStart + this.#budget) return;
        this.#onProgress?.(progress);
        await this.#yield();
        this.cancelled();
        this.#sliceStart = performance.now();
    }

    report(progress: SnapshotProgress): void {
        this.#onProgress?.(progress);
    }
}

/** The bytes of an `ArrayBuffer` or a `Uint8Array`, refused with a `TypeError` otherwise. */
function bytesOf(arrow: Uint8Array | ArrayBuffer): Uint8Array {
    if (arrow instanceof Uint8Array) return arrow;
    if (arrow instanceof ArrayBuffer) return new Uint8Array(arrow);
    throw new TypeError('An Arrow stream is read from a Uint8Array or an ArrayBuffer.');
}

/**
 * Reads a Snapshot from an Arrow IPC stream, or from an Arrow file (`.arrow`, Feather 2), told apart by the
 * file's leading `ARROW1`, held whole in memory — a payload already fetched (ADR-0065). Record batch by
 * record batch, in slices that yield between them and report progress. A read either yields a Snapshot or
 * rejects whole with a {@link SnapshotError}, naming the column, and the row when a value is the problem: a
 * type outside ADR-0065's table, a value a kind cannot hold, a compressed stream, a stream cut short, or
 * bytes that are not Arrow at all. It never yields a Snapshot with a row left out.
 */
export async function readArrowSnapshot(arrow: Uint8Array | ArrayBuffer, options: SnapshotReadOptions = {}): Promise<Snapshot> {
    const pacer = new Pacer(options);
    pacer.cancelled();
    const bytes = bytesOf(arrow);
    if (isArrowFile(bytes)) {
        const footer = fileFooter(bytes);
        const schema = flat(() => readSchema(footer.schema));
        // A block's message says its own kind, which must be the one its footer list says.
        const blocks = [
            ...footer.dictionaries.map(block => ({ ...block, kind: blockKind(block.message, dictionaryBatchMessage) })),
            ...footer.recordBatches.map(block => ({ ...block, kind: blockKind(block.message, recordBatchMessage) })),
        ];
        return readBatches(schema, blocks, pacer, undefined, undefined);
    }
    const messages = streamMessages(bytes);
    const [first, ...rest] = messages;
    if (first === undefined) throw new Error('Internal: a stream walked whole holds no schema.');
    const schema = flat(() => readSchema(schemaOf(first.message)));
    const totalRows = rest.filter(message => message.kind === recordBatchMessage).reduce((sum, message) => sum + Number(message.rows), 0);
    return readBatches(schema, rest, pacer, totalRows, bytes.byteLength);
}

/**
 * Reads the shape an Arrow IPC stream's schema says, before any row (ADR-0341): its columns' names and
 * kinds, as {@link readArrowSnapshot} would make them. Only the stream's first message, its schema, is
 * read. Refused with a {@link SnapshotError} when the bytes are not an Arrow stream's schema, are an Arrow
 * file (whose schema is in its footer), or hold a type outside ADR-0065's table, a column with no name or a
 * name twice.
 */
export function readArrowShape(arrow: Uint8Array | ArrayBuffer): DataShape {
    const bytes = bytesOf(arrow);
    if (isArrowFile(bytes)) {
        throw new SnapshotError('An Arrow file says its schema in its footer, after its rows; a shape is read from an Arrow stream, whose schema comes first.');
    }
    const message = firstMessage(bytes);
    const schema = flat(() => readSchema(schemaOf(message)));
    const declared = declare(schema);
    return Object.freeze({
        columns: Object.freeze(declared.readers.map(reader => Object.freeze({ name: reader.name, kind: reader.builder.kind }))),
    });
}

/** The kind of a file's block, refused when its message says another. */
function blockKind(message: FlatTable, expected: number): number {
    const kind = flat(() => message.uint8(1, 0));
    if (kind !== expected) throw unreadable(`a block of the file's footer points at a message of kind ${kind}, where its list holds kind ${expected}`);
    return kind;
}

/** A Schema message's schema. */
function schemaOf(message: FlatTable): FlatTable {
    return flat(() => {
        const schema = message.table(2);
        if (schema === undefined) throw unreadable('its schema message holds no schema');
        return schema;
    });
}

interface Declared {
    readonly readers: readonly ColumnReader[];
    readonly fields: readonly ArrowField[];
    readonly version: bigint;
    readonly recordKey: string | undefined;
}

const kindNames: Readonly<Record<SnapshotKind, string>> = {
    text: 'Text', decimal: 'Decimal', double: 'Double', integer: 'Integer', date: 'Date', boolean: 'Boolean',
};

/** Declares a column for each field, by ADR-0065's table, with the captions, the version and the Record Key
 * the metadata carries. */
function declare(schema: ArrowSchema): Declared {
    const names = new Set<string>();
    const readers = schema.fields.map((field, i) => {
        if (field.name === '') throw new SnapshotError(`The Arrow stream's column ${i + 1} has no name; every column of a Snapshot is named.`);
        if (names.has(field.name)) {
            throw new SnapshotError('the Arrow stream has two columns of this name, and a column\'s name is unique within a Snapshot.', { column: field.name });
        }
        names.add(field.name);
        const caption = field.metadata.get(snapshotArrowMetadata.caption);
        return ColumnReader.for(field, caption === undefined ? field.name : caption);
    });
    let version = 0n;
    const versionText = schema.metadata.get(snapshotArrowMetadata.version);
    if (versionText !== undefined) {
        if (!/^\d+$/.test(versionText) || BigInt(versionText) > int64Max) {
            throw new SnapshotError(`The Arrow stream's ${snapshotArrowMetadata.version} metadata '${versionText}' is not a version: a version is a whole number, 0 or more.`);
        }
        version = BigInt(versionText);
    }
    const recordKey = schema.metadata.get(snapshotArrowMetadata.recordKey);
    if (recordKey !== undefined) {
        const column = readers.find(reader => reader.name === recordKey);
        if (column === undefined) {
            throw new SnapshotError(`The Arrow stream names '${recordKey}' its Record Key (${snapshotArrowMetadata.recordKey}), and holds no column of that name.`);
        }
        const kind = column.builder.kind;
        if (kind !== 'text' && kind !== 'integer') {
            throw new SnapshotError(`the Arrow stream names it the Record Key, and a Record Key is a Text or an Integer column; this one is ${kindNames[kind]}.`,
                { column: recordKey });
        }
    }
    return { readers, fields: schema.fields, version, recordKey };
}

/** A message after the schema: a dictionary batch or a record batch, and where it ends in the bytes. */
interface BatchMessage {
    readonly kind: number;
    readonly message: FlatTable;
    readonly body: Uint8Array;
    readonly end: number;
}

/** The read itself, whichever way the bytes came. */
async function readBatches(
    schema: ArrowSchema,
    messages: readonly BatchMessage[],
    pacer: Pacer,
    totalRows: number | undefined,
    totalBytes: number | undefined,
): Promise<Snapshot> {
    const declared = declare(schema);
    const { readers, fields } = declared;
    const dictionaries = new Map<bigint, DictionaryData>();
    let generations = 0;
    let rows = 0;
    for (const { kind, message, body, end } of messages) {
        if (kind === dictionaryBatchMessage) {
            const { id, data, isDelta } = flat(() => dictionaryBatch(message, body, fields));
            const known = dictionaries.get(id);
            if (isDelta && known === undefined) throw unreadable(`a delta dictionary batch extends the dictionary ${id}, which the stream has not sent`);
            const parts = isDelta && known !== undefined ? [...known.parts, data] : [data];
            dictionaries.set(id, { generation: ++generations, parts, length: parts.reduce((sum, part) => sum + part.length, 0) });
            continue;
        }
        if (kind === schemaMessage) throw unreadable('it holds a second schema');
        if (kind !== recordBatchMessage) throw unreadable(`it holds a message of kind ${kind}, which is neither a dictionary batch nor a record batch`);
        const { length, arrays } = flat(() => recordBatch(message, body, readers));
        const bytes = totalBytes === undefined ? undefined : end;
        for (let start = 0; start < length; start += chunkRows) {
            const count = Math.min(chunkRows, length - start);
            const rowBase = rows;
            for (let c = 0; c < readers.length; c++) elementAt(readers, c).read(elementAt(arrays, c), start, count, rowBase, dictionaries);
            rows += count;
            await pacer.checkpoint({ rows, totalRows, bytes, totalBytes });
        }
        pacer.cancelled();
    }
    pacer.cancelled();
    const snapshot = sealSnapshot(readers.map(reader => reader.builder), rows, declared.recordKey, declared.version);
    pacer.report({ rows, totalRows: rows, bytes: totalBytes, totalBytes });
    return snapshot;
}

/** The codec a compressed body names, as Arrow's format names it. */
function refuseCompression(batch: FlatTable): void {
    // BodyCompression: codec (0), LZ4_FRAME 0, ZSTD 1; method (1).
    const compression = batch.table(3);
    if (compression === undefined) return;
    const codec = compression.uint8(0, 0);
    const name = codec === 0 ? 'LZ4 frame (LZ4_FRAME)' : codec === 1 ? 'ZSTD' : `codec ${codec}`;
    throw new SnapshotError(`The Arrow stream's buffers are compressed with ${name}, and this reader holds no codec to undo it; `
        + 'write the stream uncompressed, which HTTP\'s own compression makes as small.');
}

/** A record batch's nodes and buffers, read in the order its fields lay them out. */
class BatchLayout {
    readonly #nodes: DataView;
    readonly #nodeCount: number;
    readonly #buffers: DataView;
    readonly #bufferCount: number;
    readonly #variadic: DataView | undefined;
    readonly #variadicCount: number;
    readonly #body: Uint8Array;
    #node = 0;
    #buffer = 0;
    #variadicAt = 0;

    constructor(batch: FlatTable, body: Uint8Array) {
        // RecordBatch: length (0), nodes (1), buffers (2), compression (3), variadicBufferCounts (4).
        const nodes = batch.structs(1, 16);
        const buffers = batch.structs(2, 16);
        const variadic = batch.structs(4, 8);
        this.#nodes = nodes === undefined ? new DataView(new ArrayBuffer(0)) : nodes.view;
        this.#nodeCount = nodes === undefined ? 0 : nodes.count;
        this.#buffers = buffers === undefined ? new DataView(new ArrayBuffer(0)) : buffers.view;
        this.#bufferCount = buffers === undefined ? 0 : buffers.count;
        this.#variadic = variadic?.view;
        this.#variadicCount = variadic === undefined ? 0 : variadic.count;
        this.#body = body;
    }

    /** The next field's array: its node, its validity bitmap and `buffers` more, and as many data buffers
     * again as the batch says when the field is `variadic`. */
    next(buffers: number, variadic: boolean): ArrayData {
        if (this.#node >= this.#nodeCount) throw unreadable('a record batch holds fewer field nodes than its schema has columns');
        const length = this.#nodes.getBigInt64(16 * this.#node, true);
        const nullCount = this.#nodes.getBigInt64(16 * this.#node + 8, true);
        this.#node++;
        if (length < 0n || length > 0x7fffffffn) throw unreadable(`a column holds ${length} rows`);
        let count = 1 + buffers;
        if (variadic) {
            if (this.#variadic === undefined || this.#variadicAt >= this.#variadicCount) throw unreadable('a view column does not say how many data buffers it has');
            const more = this.#variadic.getBigInt64(8 * this.#variadicAt++, true);
            if (more < 0n || more > BigInt(this.#bufferCount)) throw unreadable(`a view column says it has ${more} data buffers`);
            count += Number(more);
        }
        const arrays: Uint8Array[] = [];
        for (let b = 0; b < count; b++) arrays.push(this.#nextBuffer());
        return { length: Number(length), nullCount, buffers: arrays };
    }

    #nextBuffer(): Uint8Array {
        if (this.#buffer >= this.#bufferCount) throw unreadable('a record batch holds fewer buffers than its columns need');
        const offset = this.#buffers.getBigInt64(16 * this.#buffer, true);
        const length = this.#buffers.getBigInt64(16 * this.#buffer + 8, true);
        this.#buffer++;
        if (offset < 0n || length < 0n || offset + length > BigInt(this.#body.byteLength)) throw unreadable('a buffer of a record batch lies outside its body');
        return this.#body.subarray(Number(offset), Number(offset + length));
    }
}

/** A record batch's rows and each column's array, checked to hold the batch's rows. */
function recordBatch(message: FlatTable, body: Uint8Array, readers: readonly ColumnReader[]): { length: number; arrays: ArrayData[] } {
    const batch = message.table(2);
    if (batch === undefined) throw unreadable('a record batch message holds no record batch');
    refuseCompression(batch);
    const rows = batch.int64(0, 0n);
    if (rows < 0n || rows > 0x7fffffffn) throw unreadable(`a record batch holds ${rows} rows`);
    const length = Number(rows);
    // A Snapshot's rows are its columns' values: rows with no column would be read as none.
    if (readers.length === 0 && length > 0) {
        throw new SnapshotError(`The Arrow stream holds ${grouped(length)} rows and no column, and a Snapshot holds rows only in its columns.`);
    }
    const layout = new BatchLayout(batch, body);
    const arrays = readers.map(reader => layout.next(reader.buffers, reader.variadic));
    arrays.forEach((array, c) => {
        if (array.length !== length) {
            throw new SnapshotError(`the Arrow stream is malformed: a record batch of ${length} rows holds ${array.length} of this column.`,
                { column: elementAt(readers, c).name });
        }
    });
    return { length, arrays };
}

/** A dictionary batch's id, its entries, and whether they extend the dictionary sent before. */
function dictionaryBatch(message: FlatTable, body: Uint8Array, fields: readonly ArrowField[]): { id: bigint; data: ArrayData; isDelta: boolean } {
    // DictionaryBatch: id (0), data (1), isDelta (2).
    const batch = message.table(2);
    if (batch === undefined) throw unreadable('a dictionary batch message holds no dictionary batch');
    const id = batch.int64(0, 0n);
    const field = fields.find(each => each.dictionary?.id === id);
    if (field === undefined) throw unreadable(`a dictionary batch has the id ${id}, which no column of its schema names`);
    const data = batch.table(1);
    if (data === undefined) throw unreadable('a dictionary batch holds no entries');
    refuseCompression(data);
    const layout = new BatchLayout(data, body);
    const entries = field.type.id === 'utf8_view' ? layout.next(1, true) : layout.next(2, false);
    return { id, data: entries, isDelta: batch.bool(2, false) };
}
