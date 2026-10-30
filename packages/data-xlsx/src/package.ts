// The zip under a workbook as ExGrid.Data.Xlsx's Package reads it (ADR-0330 §9): its parts by name, each read a piece
// at a time through a reader that refuses a part that inflates past what the zip declares, or whose compressed bytes
// cannot be inflated; and the XML of a part read with DTDs prohibited. Beside it, what the first bytes of a file say
// it is before any zip is read, and an OLE compound file told apart as encrypted or legacy.

import { grouped } from './invariant';
import { XlsxError } from './xlsx-error';
import { XmlError, XmlReader, type TextSource } from './xml-reader';
import { foldCase } from './text-casing';
import { dataOffset, readCentralDirectory, u16, u32, ZipFormatError, type ByteSource, type ZipEntry } from './zip-read';

/** The bytes of a part kept for the refusal of a part that is not well-formed: the prolog lies in them (ADR-0330 §9). */
const headLength = 64 * 1024;

/** One part's bytes as they inflate, counted, and refused past what the zip declared for it. */
export class PartBytes {
    readonly #reader: ReadableStreamDefaultReader<Uint8Array>;
    readonly #declared: number;
    readonly #deflated: boolean;
    readonly #part: string;
    readonly #sheet: string | undefined;
    readonly #head = new Uint8Array(headLength);
    #headLength = 0;
    #read = 0;
    #done = false;

    constructor(stream: ReadableStream<Uint8Array<ArrayBuffer>>, entry: ZipEntry, part: string, sheet: string | undefined) {
        this.#deflated = entry.method === 8;
        this.#reader = (this.#deflated ? stream.pipeThrough(new DecompressionStream('deflate-raw')) : stream).getReader();
        this.#declared = entry.uncompressedSize;
        this.#part = part;
        this.#sheet = sheet;
    }

    /** The bytes inflated so far. */
    get bytesRead(): number {
        return this.#read;
    }

    /** The first bytes the part inflated to, as far as they have been read. */
    get head(): Uint8Array {
        return this.#head.subarray(0, this.#headLength);
    }

    /**
     * The next bytes, or `undefined` at the part's end. A deflated part is inflated no further than it declares, as .NET
     * cuts it; a stored one, which .NET reads to its compressed size, is refused past it: a zip bomb's headers may lie.
     * Bytes that cannot be inflated are refused as malformed, naming the part.
     */
    async next(): Promise<Uint8Array | undefined> {
        if (this.#done) return undefined;
        let result: ReadableStreamReadResult<Uint8Array>;
        try {
            result = await this.#reader.read();
        } catch (error) {
            // The stream is errored now, and cancelling it would only throw its error again.
            this.#done = true;
            throw new XlsxError('malformed', `The part's compressed bytes cannot be inflated: ${inflateFailure(error)}`, { part: this.#part, sheet: this.#sheet }, { cause: error });
        }
        if (result.done) {
            this.#done = true;
            return undefined;
        }
        let bytes = result.value;
        if (this.#read + bytes.length > this.#declared) {
            if (!this.#deflated) {
                throw new XlsxError('tooLarge', `The part inflates to more than the ${grouped(this.#declared)} bytes the zip declares for it.`,
                    { part: this.#part, sheet: this.#sheet });
            }
            bytes = bytes.subarray(0, this.#declared - this.#read);
            this.#done = true;
            await this.#reader.cancel();
        }
        if (this.#headLength < headLength) {
            const kept = bytes.subarray(0, headLength - this.#headLength);
            this.#head.set(kept, this.#headLength);
            this.#headLength += kept.length;
        }
        this.#read += bytes.length;
        return bytes;
    }

    /** Lets go of what the part has not read. */
    async close(): Promise<void> {
        if (this.#done) return;
        this.#done = true;
        await this.#reader.cancel();
    }
}

/** What the platform says of bytes it could not inflate: Node's DecompressionStream throws an empty TypeError whose cause
 * is zlib's own account. */
function inflateFailure(error: unknown): string {
    if (!(error instanceof Error)) return String(error);
    if (error.message === '' && error.cause instanceof Error) return error.cause.message;
    return error.message;
}

/** The encoding a part's first bytes say it is in: a byte order mark, or the `<?` of a declaration in UTF-16. */
function encodingOf(head: Uint8Array): string {
    const [a, b, c, d] = head;
    if (a === 0xFF && b === 0xFE) return 'utf-16le';
    if (a === 0xFE && b === 0xFF) return 'utf-16be';
    if (a === 0x3C && b === 0x00 && c === 0x3F && d === 0x00) return 'utf-16le';
    if (a === 0x00 && b === 0x3C && c === 0x00 && d === 0x3F) return 'utf-16be';
    return 'utf-8';
}

/** A part's text, decoded as its first bytes say, piece by piece. Bytes the encoding does not hold are not well-formed. */
export function textOf(bytes: PartBytes): TextSource {
    let decoder: TextDecoder | undefined;
    let held = new Uint8Array(0);
    let finished = false;
    const decode = (chunk: Uint8Array, stream: boolean): string => {
        try {
            return (decoder ??= new TextDecoder(encodingOf(chunk), { fatal: true })).decode(chunk, { stream });
        } catch (error) {
            throw new XmlError('Invalid character in the given encoding.', { cause: error });
        }
    };
    return async () => {
        if (finished) return undefined;
        for (;;) {
            const chunk = await bytes.next();
            if (chunk === undefined) {
                finished = true;
                const rest = decode(held, false);
                held = new Uint8Array(0);
                return rest;
            }
            if (decoder === undefined && held.length + chunk.length < 4) {
                // The encoding is told by the first four bytes, which may arrive in more than one piece.
                const more = new Uint8Array(held.length + chunk.length);
                more.set(held);
                more.set(chunk, held.length);
                held = more;
                continue;
            }
            if (held.length > 0) {
                const joined = new Uint8Array(held.length + chunk.length);
                joined.set(held);
                joined.set(chunk, held.length);
                held = new Uint8Array(0);
                return decode(joined, true);
            }
            return decode(chunk, true);
        }
    };
}

/** A part's XML, being read, beside its bytes. */
export interface PartXml {
    readonly xml: XmlReader;
    readonly bytes: PartBytes;
}

/** The zip under a workbook: its parts by name, each opened a piece at a time. */
export class Package {
    readonly #source: ByteSource;
    readonly #entries: ReadonlyMap<string, ZipEntry>;
    /** The parts' names, in the zip's order, as the first entry of each name in any case writes it. */
    readonly partNames: readonly string[];

    private constructor(source: ByteSource, entries: ReadonlyMap<string, ZipEntry>, names: readonly string[]) {
        this.#source = source;
        this.#entries = entries;
        this.partNames = names;
    }

    /** The zip in `source`, refused when it cannot be read or holds more than `maxUncompressedBytes`. */
    static async open(source: ByteSource, maxUncompressedBytes: number): Promise<Package> {
        let listed: readonly ZipEntry[];
        try {
            listed = await readCentralDirectory(source);
        } catch (error) {
            if (!(error instanceof ZipFormatError)) throw error;
            throw new XlsxError('notAZip', `The file is not a zip package that can be read: ${error.message}`, {}, { cause: error });
        }
        const entries = new Map<string, ZipEntry>();
        const names: string[] = [];
        for (const entry of listed) {
            if (entry.name.endsWith('/')) continue;
            const name = entry.name.replace(/^\/+/, '');
            const key = foldCase(name);
            if (entries.has(key)) continue;
            entries.set(key, entry);
            names.push(name);
        }
        let total = 0;
        for (const entry of entries.values()) {
            total += entry.uncompressedSize;
            if (total > maxUncompressedBytes) {
                throw new XlsxError('tooLarge',
                    `The file holds more than the ${grouped(maxUncompressedBytes)} uncompressed bytes a read is allowed (XlsxReadOptions.MaxUncompressedBytes).`);
            }
        }
        return new Package(source, entries, Object.freeze(names));
    }

    has(part: string): boolean {
        return this.#entries.has(foldCase(part));
    }

    lengthOf(part: string): number {
        return this.#entries.get(foldCase(part))?.uncompressedSize ?? 0; // oxlint-disable-line ex-grid/no-stand-in-default -- ExGrid.Data.Xlsx: a part the package does not hold is 0 bytes of the worksheets' total
    }

    /** The part's bytes as they inflate, or `undefined` when the package has no such part.
     * @param sheet - The Sheet the part is, named by a refusal the read meets. */
    async openPart(part: string, sheet?: string): Promise<PartBytes | undefined> {
        const entry = this.#entries.get(foldCase(part));
        if (entry === undefined) return undefined;
        if (entry.method !== 0 && entry.method !== 8) {
            throw new XlsxError('notAZip', 'The part cannot be inflated: The archive entry was compressed using an unsupported compression method.', { part, sheet });
        }
        let start: number;
        try {
            start = await dataOffset(this.#source, entry);
        } catch (error) {
            if (!(error instanceof ZipFormatError)) throw error;
            throw new XlsxError('notAZip', `The part cannot be inflated: ${error.message}`, { part, sheet }, { cause: error });
        }
        return new PartBytes(this.#source.stream(start, entry.compressedSize), entry, part, sheet);
    }

    /** The part's XML, being read a piece at a time, or `undefined` when the package has no such part. */
    async openXml(part: string, sheet?: string): Promise<PartXml | undefined> {
        const bytes = await this.openPart(part, sheet);
        return bytes === undefined ? undefined : { xml: new XmlReader(textOf(bytes)), bytes };
    }

    /** The part's XML with all of its text read, for a part read whole, or `undefined` when the package has no such part. */
    async readXml(part: string): Promise<PartXml | undefined> {
        const opened = await this.openXml(part);
        if (opened === undefined) return undefined;
        await opened.xml.fill(Number.POSITIVE_INFINITY);
        return opened;
    }

    /**
     * The refusal a part that is not well-formed stands for: one that declares a document type, which the first 64 KiB
     * show in any part a workbook has, or one that is not well-formed.
     */
    static refuse(error: XmlError, opened: PartXml, part: string, sheet?: string): XlsxError {
        if (new TextDecoder().decode(opened.bytes.head).includes('<!DOCTYPE')) {
            return new XlsxError('dtd', 'The part declares a document type (<!DOCTYPE), which a workbook never needs; it is not read.', { part, sheet }, { cause: error });
        }
        return new XlsxError('malformed', `The part is not well-formed XML: ${error.message}`, { part, sheet }, { cause: error });
    }
}

// ---- what the first bytes say -----------------------------------------------------------------

const ole = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

export function isOle(head: Uint8Array): boolean {
    return head.length >= ole.length && ole.every((b, i) => head[i] === b);
}

export function isZip(head: Uint8Array): boolean {
    return head.length >= 4 && head[0] === 0x50 && head[1] === 0x4B
        && ((head[2] === 3 && head[3] === 4) || (head[2] === 5 && head[3] === 6) || (head[2] === 7 && head[3] === 8));
}

const lastSector = 0xFFFFFFFA;
const endOfChain = 0xFFFFFFFE;
const guard = 1 << 16;

/**
 * The refusal an OLE compound file stands for: an encrypted workbook, whose compound file holds an `EncryptedPackage`
 * stream, or a legacy binary workbook. The compound file's directory is read through its sector chains (MS-CFB), and
 * nothing else of the file: its header, the FAT sectors the directory's chain passes through, and the directory's own
 * sectors. A directory that cannot be followed is taken for a legacy workbook.
 */
export async function classifyOle(source: ByteSource): Promise<XlsxError> {
    if (await isEncrypted(source)) {
        return new XlsxError('encrypted',
            'The file is an encrypted workbook. Remove its password in Excel (File, Info, Protect Workbook, Encrypt with Password) and save it again.');
    }
    return new XlsxError('legacy',
        'The file is an Excel 97–2003 workbook (.xls), or another Office binary file, not an .xlsx. Save it from Excel as an Excel Workbook (.xlsx).');
}

/** The bytes at `offset`, all `length` of them, or `undefined` when the file ends first. */
async function readExactly(source: ByteSource, offset: number, length: number): Promise<Uint8Array | undefined> {
    if (offset + length > source.size) return undefined;
    const bytes = await source.read(offset, length);
    return bytes.length === length ? bytes : undefined;
}

/** Whether the compound file's directory names an `EncryptedPackage` or `EncryptionInfo` stream. */
async function isEncrypted(source: ByteSource): Promise<boolean> {
    const header = await readExactly(source, 0, 512);
    if (header === undefined) return false;
    const shift = u16(header, 30);
    if (shift !== 9 && shift !== 12) return false;
    const size = 1 << shift;
    const perSector = size / 4;
    const at = (sector: number): number => (sector + 1) * size;

    // The FAT's own sectors: the first 109 the header lists, the rest along the DIFAT chain, read only when the
    // directory's chain reaches that far.
    const fatSectors: number[] = [];
    for (let i = 0; i < 109; i++) {
        const s = u32(header, 76 + i * 4);
        if (s < lastSector) fatSectors.push(s);
    }
    let difat = u32(header, 68);
    let difatRead = 0;
    const fatCache = new Map<number, Uint8Array | null>();
    const next = async (sector: number): Promise<number> => {
        const fat = Math.floor(sector / perSector);
        while (fat >= fatSectors.length && difat < lastSector && difatRead++ < guard) {
            const block = await readExactly(source, at(difat), size);
            if (block === undefined) return endOfChain;
            for (let i = 0; i < size / 4 - 1; i++) {
                const s = u32(block, i * 4);
                if (s < lastSector) fatSectors.push(s);
            }
            difat = u32(block, size - 4);
        }
        const fatSector = fatSectors[fat];
        if (fatSector === undefined) return endOfChain;
        let entries = fatCache.get(fat);
        if (entries === undefined) {
            const read = await readExactly(source, at(fatSector), size);
            entries = read === undefined ? null : read;
            fatCache.set(fat, entries);
        }
        return entries === null ? endOfChain : u32(entries, (sector % perSector) * 4);
    };

    const names = new TextDecoder('utf-16le');
    let dir = u32(header, 48);
    for (let step = 0; dir < lastSector && step < guard; step++) {
        const directory = await readExactly(source, at(dir), size);
        if (directory === undefined) break;
        for (let e = 0; e < size / 128; e++) {
            const entry = directory.subarray(e * 128, e * 128 + 128);
            const length = u16(entry, 64);
            if (length >= 2 && length <= 64) {
                const name = names.decode(entry.subarray(0, length - 2));
                if (name === 'EncryptedPackage' || name === 'EncryptionInfo') return true;
            }
        }
        dir = await next(dir);
    }
    return false;
}
