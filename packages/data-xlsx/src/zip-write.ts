// The zip under a written workbook (APPNOTE 6.3): each part deflated as it is written, by the platform's
// CompressionStream('deflate-raw'), a piece at a time, and the package put together once every part is closed — a
// local header with the sizes known in front of each part's compressed bytes, then the central directory. Nothing past
// 4 GiB is written: a workbook is held to 1 GiB uncompressed (ADR-0330 §9).

import { crc32 } from './crc32';
import { elementAt } from './must';

/** The DOS date every entry carries, 1980-01-01 at midnight: what is written does not depend on when. */
const dosDate = (0 << 9) | (1 << 5) | 1;

interface Closed {
    readonly name: Uint8Array;
    readonly crc: number;
    readonly size: number;
    readonly compressed: readonly Uint8Array<ArrayBuffer>[];
    readonly compressedSize: number;
}

const encoder = new TextEncoder();

/** One part being written: its bytes deflated as they come, its CRC and sizes counted. */
export class ZipEntryWriter {
    readonly #name: string;
    readonly #writer: WritableStreamDefaultWriter<Uint8Array<ArrayBuffer>>;
    readonly #compressed: Uint8Array<ArrayBuffer>[] = [];
    readonly #pumped: Promise<void>;
    #written: Promise<void> = Promise.resolve();
    #compressedSize = 0;
    #crc = 0;
    #size = 0;
    #closed = false;
    #result: Closed | undefined;

    constructor(name: string) {
        this.#name = name;
        const stream = new CompressionStream('deflate-raw');
        this.#writer = stream.writable.getWriter();
        const reader = stream.readable.getReader();
        this.#pumped = (async () => {
            for (;;) {
                const result = await reader.read();
                if (result.done) return;
                this.#compressed.push(result.value);
                this.#compressedSize += result.value.length;
            }
        })();
    }

    /** Adds `bytes` to the part. They are deflated in turn; `flush` awaits them. */
    write(bytes: Uint8Array<ArrayBuffer>): void {
        if (this.#closed) throw new Error(`Internal: the part '${this.#name}' is written after it was closed.`);
        if (bytes.length === 0) return;
        this.#crc = crc32(this.#crc, bytes);
        this.#size += bytes.length;
        const writer = this.#writer;
        this.#written = this.#written.then(() => writer.write(bytes));
    }

    /** Awaits the deflating of every byte written so far, so that memory holds them compressed. */
    async flush(): Promise<void> {
        await this.#written;
    }

    /** Ends the part: what it was given, deflated. */
    async close(): Promise<void> {
        this.#closed = true;
        await this.#written;
        await this.#writer.close();
        await this.#pumped;
        this.#result = { name: encoder.encode(this.#name), crc: this.#crc, size: this.#size, compressed: this.#compressed, compressedSize: this.#compressedSize };
    }

    /** The part as closed. */
    get result(): Closed {
        if (this.#result === undefined) throw new Error(`Internal: the part '${this.#name}' is put in the package before it was closed.`);
        return this.#result;
    }
}

function header(size: number): { bytes: Uint8Array<ArrayBuffer>; view: DataView } {
    const bytes = new Uint8Array(size);
    return { bytes, view: new DataView(bytes.buffer) };
}

/** The package being written: its parts in the order they were begun. */
export class ZipWriter {
    readonly #entries: ZipEntryWriter[] = [];

    /** Begins the part `name`, after every part begun before it. */
    entry(name: string): ZipEntryWriter {
        const entry = new ZipEntryWriter(name);
        this.#entries.push(entry);
        return entry;
    }

    /** The package, every part of it closed. */
    finish(): Blob {
        const entries = this.#entries.map(e => e.result);
        const parts: Uint8Array<ArrayBuffer>[] = [];
        const offsets: number[] = [];
        let offset = 0;
        for (const e of entries) {
            offsets.push(offset);
            const { bytes, view } = header(30 + e.name.length);
            view.setUint32(0, 0x04034b50, true);
            view.setUint16(4, 20, true);
            view.setUint16(6, 0, true);
            view.setUint16(8, 8, true);
            view.setUint16(10, 0, true);
            view.setUint16(12, dosDate, true);
            view.setUint32(14, e.crc, true);
            view.setUint32(18, e.compressedSize, true);
            view.setUint32(22, e.size, true);
            view.setUint16(26, e.name.length, true);
            view.setUint16(28, 0, true);
            bytes.set(e.name, 30);
            parts.push(bytes);
            parts.push(...e.compressed);
            offset += bytes.length + e.compressedSize;
        }
        const directoryStart = offset;
        entries.forEach((e, i) => {
            const { bytes, view } = header(46 + e.name.length);
            view.setUint32(0, 0x02014b50, true);
            view.setUint16(4, 20, true);
            view.setUint16(6, 20, true);
            view.setUint16(8, 0, true);
            view.setUint16(10, 8, true);
            view.setUint16(12, 0, true);
            view.setUint16(14, dosDate, true);
            view.setUint32(16, e.crc, true);
            view.setUint32(20, e.compressedSize, true);
            view.setUint32(24, e.size, true);
            view.setUint16(28, e.name.length, true);
            view.setUint32(42, elementAt(offsets, i), true);
            bytes.set(e.name, 46);
            parts.push(bytes);
            offset += bytes.length;
        });
        const { bytes, view } = header(22);
        view.setUint32(0, 0x06054b50, true);
        view.setUint16(8, entries.length, true);
        view.setUint16(10, entries.length, true);
        view.setUint32(12, offset - directoryStart, true);
        view.setUint32(16, directoryStart, true);
        parts.push(bytes);
        return new Blob(parts, { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    }
}
