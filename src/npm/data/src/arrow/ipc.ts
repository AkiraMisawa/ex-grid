// The framing of Arrow's IPC stream and file (ADR-0065), as ExGrid.Data.Arrow's IpcFrames reads it, with
// the same refusals in the same order. Each message of a stream is an optional continuation marker
// (0xFFFFFFFF), its metadata's length, the metadata — a flatbuffer Message that says its kind and its
// body's length — and the body; a length of 0 is the end-of-stream marker.
//
// Arrow lets a stream end without the marker, by closing. A stream that does is refused as cut short,
// because a stream cut between two record batches is otherwise read as whole, and its totals would be
// quietly short — what ADR-0064 refuses. pyarrow, Polars and Apache.Arrow write the marker when closed.

import { FlatError, FlatTable } from './flat';
import { SnapshotError, grouped } from '../snapshot-error';

const continuation = -1;

/** A message's kind, as the Message table's header says it. */
export const schemaMessage = 1;
export const dictionaryBatchMessage = 2;
export const recordBatchMessage = 3;

/** The largest first message a stream is taken to start with: a schema's metadata. Bytes that begin
 * otherwise — an error page, another format — are refused before they are read as Arrow. */
const maxSchemaLength = 64 << 20;

/** The smallest metadata a message has: a flatbuffer Message padded to eight bytes. */
const minMetadataLength = 8;

/** The magic an Arrow file begins and ends with. */
const fileMagic = [0x41, 0x52, 0x52, 0x4f, 0x57, 0x31];

/** One message of a stream: its Message table, its body, where it ends, and a record batch's rows. */
export interface IpcMessage {
    readonly kind: number;
    readonly message: FlatTable;
    readonly body: Uint8Array;
    readonly end: number;
    readonly rows: bigint;
}

export function notArrow(): SnapshotError {
    return new SnapshotError('The bytes are not an Arrow IPC stream or file: they begin with neither an IPC message nor ARROW1.');
}

export function gzipped(): SnapshotError {
    return new SnapshotError('The bytes are compressed with gzip, not an Arrow IPC stream or file: undo the compression first. '
        + 'A fetch undoes it when the response says so in its Content-Encoding, and a DecompressionStream undoes it otherwise.');
}

export function empty(): SnapshotError {
    return new SnapshotError('The Arrow stream is empty: it holds no schema.');
}

export function endsInFirstMessage(): SnapshotError {
    return new SnapshotError('The bytes end inside their first message: they are not an Arrow IPC stream, or they are one cut short; a stream is read only whole.');
}

export function cutShort(): SnapshotError {
    return new SnapshotError('The Arrow stream ends before its end-of-stream marker, so it may have been cut short; a stream is read only whole.');
}

export function fileCutShort(): SnapshotError {
    return new SnapshotError('The Arrow file ends without its footer, so it may have been cut short; a file is read only whole.');
}

/** What Arrow's own reader would throw on: metadata that does not say what the format needs. */
export function unreadable(what: string): SnapshotError {
    return new SnapshotError(`The Arrow stream could not be read: ${what}.`);
}

function malformed(at: number, what: string): SnapshotError {
    return new SnapshotError(`The Arrow stream is malformed: the message at byte ${grouped(at)} ${what}.`);
}

/** Whether `bytes` begin with ARROW1, as an Arrow file does. */
export function isArrowFile(bytes: Uint8Array): boolean {
    return fileMagic.every((byte, i) => bytes[i] === byte);
}

/**
 * The length of a stream's first message — its framing and its metadata, the schema's — read from its
 * first eight bytes, or all it has. The metadata's length must be one a schema can have.
 */
export function firstMessageLength(stream: Uint8Array): number {
    const head = new DataView(stream.buffer, stream.byteOffset, Math.min(8, stream.byteLength));
    if (head.byteLength === 0) throw empty();
    if (head.byteLength >= 2 && head.getUint8(0) === 0x1f && head.getUint8(1) === 0x8b) throw gzipped();
    if (head.byteLength < 4) throw notArrow();
    let framing = 4;
    let length = head.getInt32(0, true);
    if (length === continuation) {
        if (head.byteLength < 8) throw notArrow();
        length = head.getInt32(4, true);
        framing = 8;
    }
    if (length === 0) throw empty();
    if (length < minMetadataLength || length > maxSchemaLength) throw notArrow();
    return framing + length;
}

/** Checks that `message`, a stream's first message whole, is a schema. */
export function checkSchema(message: Uint8Array): void {
    const view = new DataView(message.buffer, message.byteOffset, message.byteLength);
    const framing = view.getInt32(0, true) === continuation ? 8 : 4;
    if (messageFacts(message.subarray(framing), notArrow).kind !== schemaMessage) throw notArrow();
}

/** A stream's first message, its schema, checked, as a shape is read from it alone. */
export function firstMessage(stream: Uint8Array): FlatTable {
    const length = firstMessageLength(stream);
    if (length > stream.byteLength) throw endsInFirstMessage();
    checkSchema(stream.subarray(0, length));
    const framing = new DataView(stream.buffer, stream.byteOffset, 4).getInt32(0, true) === continuation ? 8 : 4;
    return FlatTable.root(stream.subarray(framing, length));
}

/**
 * A whole stream's messages, in order, up to its end-of-stream marker; refused when the bytes do not
 * begin with a schema message, a message is malformed, or the stream ends before its marker.
 */
export function streamMessages(stream: Uint8Array): IpcMessage[] {
    firstMessage(stream);
    const view = new DataView(stream.buffer, stream.byteOffset, stream.byteLength);
    const messages: IpcMessage[] = [];
    let position = 0;
    for (;;) {
        const start = position;
        if (stream.byteLength - position < 4) throw cutShort();
        let length = view.getInt32(position, true);
        position += 4;
        if (length === continuation) {
            if (stream.byteLength - position < 4) throw cutShort();
            length = view.getInt32(position, true);
            position += 4;
        }
        // The first message is a schema, so this ends a stream that holds one.
        if (length === 0) return messages;
        if (length < 0) throw malformed(start, 'claims a negative length');
        if (length > stream.byteLength - position) throw cutShort();
        const metadata = stream.subarray(position, position + length);
        const facts = messageFacts(metadata, () => malformed(start, 'is not a message'));
        position += length;
        if (facts.bodyLength < 0n) throw malformed(start, 'claims a negative body');
        if (facts.bodyLength > BigInt(stream.byteLength - position)) throw cutShort();
        const body = stream.subarray(position, position + Number(facts.bodyLength));
        position += Number(facts.bodyLength);
        messages.push({ kind: facts.kind, message: facts.message, body, end: position, rows: facts.rows });
    }
}

interface MessageFacts {
    readonly kind: number;
    readonly message: FlatTable;
    readonly bodyLength: bigint;
    readonly rows: bigint;
}

/** A message's kind, its body's length and, for a record batch, its rows; `refusal` when the metadata is
 * not a Message. */
function messageFacts(metadata: Uint8Array, refusal: () => SnapshotError): MessageFacts {
    try {
        // Message: version (0), header_type (1), header (2), bodyLength (3), custom_metadata (4).
        const message = FlatTable.root(metadata);
        const kind = message.uint8(1, 0);
        const bodyLength = message.int64(3, 0n);
        let rows = 0n;
        if (kind === recordBatchMessage) {
            // RecordBatch: length (0), nodes (1), buffers (2), compression (3).
            const batch = message.table(2);
            if (batch === undefined) throw new FlatError('a record batch message holds no record batch');
            rows = batch.int64(0, 0n);
        }
        return { kind, message, bodyLength, rows };
    } catch (error) {
        if (error instanceof FlatError) throw refusal();
        throw error;
    }
}

/** One message of an Arrow file, where its footer says it lies. */
export interface FileBlock {
    readonly message: FlatTable;
    readonly body: Uint8Array;
    readonly end: number;
}

/** An Arrow file's footer: its schema, and its dictionary and record batch messages, in order. */
export interface FileFooter {
    readonly schema: FlatTable;
    readonly dictionaries: readonly FileBlock[];
    readonly recordBatches: readonly FileBlock[];
}

/** An Arrow file's footer, refused when the file ends without it. */
export function fileFooter(file: Uint8Array): FileFooter {
    // A file ends with its footer and the magic again; one without them was cut short.
    if (file.byteLength < 2 * fileMagic.length || !fileMagic.every((byte, i) => file[file.byteLength - fileMagic.length + i] === byte)) {
        throw fileCutShort();
    }
    const view = new DataView(file.buffer, file.byteOffset, file.byteLength);
    const lengthAt = file.byteLength - fileMagic.length - 4;
    const footerLength = view.getInt32(lengthAt, true);
    if (footerLength <= 0 || footerLength > lengthAt - 8) throw fileCutShort();
    return flat(() => {
        // Footer: version (0), schema (1), dictionaries (2), recordBatches (3).
        const footer = FlatTable.root(file.subarray(lengthAt - footerLength, lengthAt));
        const schema = footer.table(1);
        if (schema === undefined) throw unreadable('the file\'s footer holds no schema');
        return { schema, dictionaries: blocks(file, view, footer, 2), recordBatches: blocks(file, view, footer, 3) };
    });
}

/** The messages a footer's vector of Blocks — offset, metadata length, body length — points to. */
function blocks(file: Uint8Array, view: DataView, footer: FlatTable, id: number): FileBlock[] {
    const vector = footer.structs(id, 24);
    if (vector === undefined) return [];
    const found: FileBlock[] = [];
    for (let i = 0; i < vector.count; i++) {
        const offset = vector.view.getBigInt64(24 * i, true);
        const metadataLength = vector.view.getInt32(24 * i + 8, true);
        const bodyLength = vector.view.getBigInt64(24 * i + 16, true);
        if (offset < 8n || metadataLength < 8 || bodyLength < 0n || offset + BigInt(metadataLength) + bodyLength > BigInt(file.byteLength)) {
            throw unreadable('a block of the file\'s footer lies outside the file');
        }
        const start = Number(offset);
        const framing = view.getInt32(start, true) === continuation ? 8 : 4;
        const length = view.getInt32(start + framing - 4, true);
        if (length <= 0 || framing + length > metadataLength) throw unreadable('a block of the file\'s footer does not hold its message');
        const message = FlatTable.root(file.subarray(start + framing, start + framing + length));
        const bodyStart = start + metadataLength;
        const end = bodyStart + Number(bodyLength);
        found.push({ message, body: file.subarray(bodyStart, end), end });
    }
    return found;
}

/** Runs `read`, saying a flatbuffer that points outside its bytes as metadata Arrow could not read. */
export function flat<T>(read: () => T): T {
    try {
        return read();
    } catch (error) {
        if (error instanceof FlatError) throw unreadable(error.message);
        throw error;
    }
}
