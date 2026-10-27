// The zip under a workbook (APPNOTE 6.3), read where its bytes lie: the central directory names the entries, and an
// entry's bytes are inflated by the platform's DecompressionStream('deflate-raw') a piece at a time, so a worksheet
// is never held inflated whole. A File is read through Blob.slice, so not even the file is read whole.

/** The bytes of a file, read where they are asked for. */
export interface ByteSource {
    readonly size: number;
    /** The bytes from `offset`, at most `length` of them: fewer where the file ends first. */
    read(offset: number, length: number): Promise<Uint8Array>;
    /** The bytes from `offset`, `length` of them, a piece at a time. */
    stream(offset: number, length: number): ReadableStream<Uint8Array<ArrayBuffer>>;
}

const piece = 1 << 20;

/** The file as a source of its bytes: an array of them, or a Blob (a File) read where it is asked. */
export function byteSource(file: Uint8Array | ArrayBuffer | Blob): ByteSource {
    if (typeof Blob !== 'undefined' && file instanceof Blob) {
        return {
            size: file.size,
            read: async (offset, length) => new Uint8Array(await file.slice(offset, Math.min(file.size, offset + length)).arrayBuffer()),
            stream: (offset, length) => file.slice(offset, offset + length).stream(),
        };
    }
    if (!(file instanceof Uint8Array) && !(file instanceof ArrayBuffer)) {
        throw new TypeError('The file is a Uint8Array, an ArrayBuffer or a Blob.');
    }
    const bytes = file instanceof Uint8Array ? file : new Uint8Array(file);
    return {
        size: bytes.length,
        read: (offset, length) => Promise.resolve(bytes.subarray(offset, Math.min(bytes.length, offset + length))),
        stream: (offset, length) => {
            let at = offset;
            const end = Math.min(bytes.length, offset + length);
            return new ReadableStream<Uint8Array<ArrayBuffer>>({
                pull(controller) {
                    if (at >= end) {
                        controller.close();
                        return;
                    }
                    const next = Math.min(end, at + piece);
                    controller.enqueue(bytes.slice(at, next));
                    at = next;
                },
            });
        },
    };
}

/** A zip that cannot be read: what .NET's ZipArchive throws an InvalidDataException for. */
export class ZipFormatError extends Error {
    constructor(message: string) {
        super(message);
        this.name = 'ZipFormatError';
    }
}

/** An entry as the central directory lists it. */
export interface ZipEntry {
    /** Its name, as the directory writes it. */
    readonly name: string;
    /** How it is compressed: 0 stored, 8 deflated. */
    readonly method: number;
    readonly flags: number;
    readonly compressedSize: number;
    /** What it declares it inflates to. */
    readonly uncompressedSize: number;
    readonly localHeaderOffset: number;
}

/** A little-endian 16-bit integer of a record; a record cut short is not a zip. */
export function u16(bytes: Uint8Array, at: number): number {
    const low = bytes[at];
    const high = bytes[at + 1];
    if (low === undefined || high === undefined) throw new ZipFormatError('A record of the zip is cut short.');
    return low | (high << 8);
}

/** A little-endian 32-bit integer of a record; a record cut short is not a zip. */
export function u32(bytes: Uint8Array, at: number): number {
    return (u16(bytes, at) | (u16(bytes, at + 2) << 16)) >>> 0;
}

/** A 64-bit little-endian size, as a number; past 2^53 it is Infinity, which every limit refuses. */
function u64(bytes: Uint8Array, at: number): number {
    const value = u32(bytes, at) + u32(bytes, at + 4) * 0x1_0000_0000;
    return value > Number.MAX_SAFE_INTEGER ? Number.POSITIVE_INFINITY : value;
}

function need(bytes: Uint8Array, length: number, what: string): void {
    if (bytes.length < length) throw new ZipFormatError(`${what} is cut short.`);
}

const utf8 = new TextDecoder();

/** The entries the zip's central directory lists, in its order. */
export async function readCentralDirectory(source: ByteSource): Promise<readonly ZipEntry[]> {
    const tailLength = Math.min(source.size, 22 + 0xFFFF);
    const tailStart = source.size - tailLength;
    const tail = await source.read(tailStart, tailLength);
    let end = -1;
    for (let at = tail.length - 22; at >= 0; at--) {
        if (u32(tail, at) === 0x06054b50) {
            end = at;
            break;
        }
    }
    if (end < 0) throw new ZipFormatError('End of Central Directory record could not be found.');
    let count = u16(tail, end + 10);
    let size = u32(tail, end + 12);
    let offset = u32(tail, end + 16);
    if (count === 0xFFFF || size === 0xFFFFFFFF || offset === 0xFFFFFFFF) {
        const locator = end - 20;
        if (locator < 0 || u32(tail, locator) !== 0x07064b50) throw new ZipFormatError('The Zip64 end of central directory locator could not be found.');
        const at = u64(tail, locator + 8);
        const record = await source.read(at, 56);
        need(record, 56, 'The Zip64 end of central directory record');
        if (u32(record, 0) !== 0x06064b50) throw new ZipFormatError('The Zip64 end of central directory record could not be found.');
        count = u64(record, 32);
        size = u64(record, 40);
        offset = u64(record, 48);
    }
    if (!Number.isFinite(size) || !Number.isFinite(offset) || offset + size > source.size) {
        throw new ZipFormatError('The central directory lies outside the file.');
    }
    const directory = await source.read(offset, size);
    const entries: ZipEntry[] = [];
    let at = 0;
    while (at + 46 <= directory.length && u32(directory, at) === 0x02014b50) {
        const flags = u16(directory, at + 8);
        const method = u16(directory, at + 10);
        let compressedSize = u32(directory, at + 20);
        let uncompressedSize = u32(directory, at + 24);
        const nameLength = u16(directory, at + 28);
        const extraLength = u16(directory, at + 30);
        const commentLength = u16(directory, at + 32);
        let localHeaderOffset = u32(directory, at + 42);
        const nameAt = at + 46;
        need(directory, nameAt + nameLength + extraLength + commentLength, 'The central directory');
        const name = utf8.decode(directory.subarray(nameAt, nameAt + nameLength));
        // The Zip64 extra field carries, in this order, each size or offset the record writes as 0xFFFFFFFF.
        let extra = nameAt + nameLength;
        const extraEnd = extra + extraLength;
        while (extra + 4 <= extraEnd) {
            const id = u16(directory, extra);
            const length = u16(directory, extra + 2);
            if (id === 0x0001) {
                let field = extra + 4;
                if (uncompressedSize === 0xFFFFFFFF) { uncompressedSize = u64(directory, field); field += 8; }
                if (compressedSize === 0xFFFFFFFF) { compressedSize = u64(directory, field); field += 8; }
                if (localHeaderOffset === 0xFFFFFFFF) { localHeaderOffset = u64(directory, field); }
            }
            extra += 4 + length;
        }
        entries.push({ name, method, flags, compressedSize, uncompressedSize, localHeaderOffset });
        at = extraEnd + commentLength;
    }
    if (entries.length !== count) {
        throw new ZipFormatError('Number of entries expected in End Of Central Directory does not correspond to number of entries in Central Directory.');
    }
    return entries;
}

/** Where an entry's compressed bytes begin, past its local header. */
export async function dataOffset(source: ByteSource, entry: ZipEntry): Promise<number> {
    const header = await source.read(entry.localHeaderOffset, 30);
    if (header.length < 30 || u32(header, 0) !== 0x04034b50) throw new ZipFormatError('A local file header is corrupt.');
    const start = entry.localHeaderOffset + 30 + u16(header, 26) + u16(header, 28);
    if (start + entry.compressedSize > source.size) throw new ZipFormatError('A local file header is corrupt.');
    return start;
}
