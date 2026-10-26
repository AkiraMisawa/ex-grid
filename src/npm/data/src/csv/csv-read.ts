// A CSV read under a declared Schema into a Snapshot (ADR-0064, the second way in), as ExGrid.Data's CsvLoad
// reads one: the bytes cut into records under RFC 4180, the header matched to the declared columns, and each
// declared field read into its column, in batches of records that check in with the pacer between them. A
// load either yields a Snapshot or is refused whole, naming the record — its row among the data records,
// from one — the column, and the line of the file the record begins on, in ExGrid.Data's words, which the
// shared cases under tests/cases/data hold both languages to.
//
// The bytes are cut without decoding them: the separator, CR, LF and the quote are ASCII, and in UTF-8 and
// Shift-JIS no byte of a multi-byte character is any of them. A batch's bytes are then decoded at once, and a
// field's text taken from the batch's; a batch that is not valid in the encoding is decoded a field at a
// time, so the field that is not is refused by row and column.

import { decimalLimit, maxDecimalScale } from '../decimal';
import { elementAt, int64Max, int64Min } from '../must';
import { Pacer, type SnapshotReadOptions } from '../pacer';
import { SnapshotError, grouped } from '../snapshot-error';
import { builderOf, sealSnapshot, type AnyColumnBuilder, type Snapshot } from '../snapshot';
import { hasDigit, nonFinite, normalizeDouble, numberReading, parseNumber, parseShort, plainNumber } from './number-text';
import { csvPlan, encodingName, sameIgnoringCase, type CsvEncoding, type CsvPlan, type CsvSchema, type PlannedColumn } from './schema';

/** The longest record read. Excel holds at most 32,767 characters in a cell, so a record this long is a quote
 * left open, and the file is not read to its end to find that out. ExGrid.Data's CsvLoad.MaxRecordBytes. */
const maxRecordBytes = 64 << 20;

/** The records cut, then read, at a time, as ExGrid.Data reads them. */
const batchSize = 1024;

/** The NaN .NET reads, whatever its sign: `double.NaN`, whose bits are 0xFFF8000000000000, little-endian. A
 * JavaScript NaN's are 0x7FF8000000000000, and the Snapshot keeps a double's bits as they are. */
const dotnetNaN = Uint8Array.of(0, 0, 0, 0, 0, 0, 0xf8, 0xff);

/** The longest field a refusal shows whole; a longer one is cut, ending in an ellipsis. */
const shownLength = 60;

const quote = 0x22;
const cr = 0x0d;
const lf = 0x0a;

/** A field in quotes. */
const quoted = 1;
/** A quoted field holding a doubled quote, which stands for one. */
const doubled = 2;

/** What cutting a record from the bytes came to. */
type Cut = 'record' | 'needMore' | 'end' | 'malformed';

/** How a record broke RFC 4180. */
type Fault = 'unclosed' | 'quoteInside' | 'afterQuote';

/**
 * Reads a CSV under `schema` into a Snapshot (ADR-0064), in slices sized by time that yield between them and
 * report progress — the data records read, and the bytes read of the file's — as ExGrid.Data's
 * `CsvSchema.ReadAsync` reads one. The file is read whole first: a `Blob` (a `File` from an input) through
 * its `arrayBuffer()`.
 *
 * A Schema that contradicts itself is refused with a `TypeError` before a byte is read. The file is refused
 * whole with a {@link SnapshotError} naming the row, the column and the line where the record begins: a value
 * its kind cannot read, a record with more or fewer fields than the header, a quote left open or out of
 * place, text not valid in the encoding, a declared column missing from the header, and a Record Key that is
 * Blank or carried twice. A cancelled read rejects with the signal's reason.
 */
export async function readCsvSnapshot(schema: CsvSchema, file: Uint8Array | ArrayBuffer | Blob, options: SnapshotReadOptions = {}): Promise<Snapshot> {
    const plan = csvPlan(schema);
    const pacer = new Pacer(options);
    pacer.cancelled();
    const data = await bytesOf(file);
    pacer.cancelled();
    const start = preamble(data, plan.encoding);
    const read = new CsvRead(plan, data, start === 3 ? 'utf-8' : plan.encoding, start);
    const total = data.length;
    for (;;) {
        const done = read.parse();
        await pacer.checkpoint({ rows: read.rows, totalRows: undefined, bytes: read.consumed, totalBytes: total });
        if (done) break;
    }
    read.finish();
    pacer.report({ rows: read.rows, totalRows: read.rows, bytes: read.consumed, totalBytes: total });
    return read.seal();
}

async function bytesOf(file: Uint8Array | ArrayBuffer | Blob): Promise<Uint8Array> {
    if (file instanceof Uint8Array) return file;
    if (file instanceof ArrayBuffer) return new Uint8Array(file);
    if (file instanceof Blob) return new Uint8Array(await file.arrayBuffer());
    throw new TypeError('A CSV is read from a Uint8Array, an ArrayBuffer or a Blob.');
}

/** The length of the byte-order mark the file begins with: UTF-8's, which wins over the declared encoding, or
 * none. UTF-16's is refused, since a CSV is read only in UTF-8 or Shift-JIS. */
function preamble(data: Uint8Array, declared: CsvEncoding): number {
    if (data[0] === 0xef && data[1] === 0xbb && data[2] === 0xbf) return 3;
    if ((data[0] === 0xff && data[1] === 0xfe) || (data[0] === 0xfe && data[1] === 0xff)) {
        throw new SnapshotError(`The file begins with UTF-16's byte-order mark; a CSV is read in UTF-8 or Shift-JIS, and the Schema declares ${encodingName(declared)}.`);
    }
    return 0;
}

/** A field's text in single quotes, as a refusal shows it, a long one cut short. */
function shown(text: string): string {
    return text.length <= shownLength ? `'${text}'` : `'${text.slice(0, shownLength - 1)}…'`;
}

/** The text without the ASCII spaces around it, which the kinds other than Text set aside. */
function trimSpaces(text: string): string {
    let from = 0;
    let to = text.length;
    while (from < to && text.charCodeAt(from) === 0x20) from++;
    while (to > from && text.charCodeAt(to - 1) === 0x20) to--;
    return from === 0 && to === text.length ? text : text.slice(from, to);
}

/** The characters .NET's `char.IsWhiteSpace` takes: JavaScript's `\s` but U+FEFF, and U+0085. */
const dotnetSpace = /^[\t\n\v\f\r \u0085\u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000]+|[\t\n\v\f\r \u0085\u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000]+$/g;

/** The text without the white space around it, as .NET's `string.Trim()` sets it aside. */
function dotnetTrim(text: string): string {
    return text.replace(dotnetSpace, '');
}

function fields(count: number): string {
    return count === 1 ? '1 field' : `${grouped(count)} fields`;
}

function times(count: number): string {
    return count === 2 ? 'twice' : `${count} times`;
}

/** The file's text in its encoding: decoded strictly for a value, shown leniently for a refusal. */
class Decoding {
    readonly name: string;
    readonly #shiftJis: boolean;
    readonly #strict: TextDecoder;
    readonly #lenient: TextDecoder;

    constructor(encoding: CsvEncoding) {
        this.name = encodingName(encoding);
        this.#shiftJis = encoding === 'shift-jis';
        const label = this.#shiftJis ? 'shift_jis' : 'utf-8';
        // A byte-order mark inside the file is a character of it, as .NET keeps it.
        this.#strict = new TextDecoder(label, { fatal: true, ignoreBOM: true });
        this.#lenient = new TextDecoder(label, { ignoreBOM: true });
    }

    /** The bytes as text, or `undefined` when they are not valid in the encoding. */
    strict(bytes: Uint8Array): string | undefined {
        try {
            return this.#strict.decode(bytes);
        } catch { // oxlint-disable-line ex-grid/no-catch -- TextDecoder says bytes are not valid only by throwing; the caller refuses the field by row and column
            return undefined;
        }
    }

    /** The bytes as a refusal shows them: an invalid byte as UTF-8's replacement character, or as the `?` .NET's
     * code page 932 puts in its place. */
    lenient(bytes: Uint8Array): string {
        const text = this.#lenient.decode(bytes);
        return this.#shiftJis ? text.replaceAll('�', '?') : text;
    }

    /**
     * The characters the valid bytes from `from` to `to` decode to, both on a character's boundary. In UTF-8 a
     * byte that is not a continuation begins one, and one of four bytes begins two, a surrogate pair; in
     * Shift-JIS a lead byte and its trail are one character.
     */
    characters(data: Uint8Array, from: number, to: number): number {
        let count = 0;
        if (this.#shiftJis) {
            for (let i = from; i < to; i++) {
                const b = data[i];
                if (b === undefined) break;
                if ((b >= 0x81 && b <= 0x9f) || (b >= 0xe0 && b <= 0xfc)) i++;
                count++;
            }
            return count;
        }
        for (let i = from; i < to; i++) {
            const b = data[i];
            if (b === undefined) break;
            if ((b & 0xc0) !== 0x80) count += b >= 0xf0 ? 2 : 1;
        }
        return count;
    }
}

/**
 * Cuts records into fields under RFC 4180, over bytes, as ExGrid.Data's CsvTokenizer does: a field in double
 * quotes may hold the separator, a line break and a doubled quote; a record ends at CR LF, LF or CR outside
 * quotes. It keeps only where each field lies. A record is cut from at most {@link maxRecordBytes} of the
 * file: one that needs more is not cut (`needMore`), and refused as too long.
 */
class Tokenizer {
    readonly #data: Uint8Array;
    readonly #separator: number;
    /** Where each field begins in the file: a double, since a file a browser holds may be longer than 2 GiB. */
    #starts = new Float64Array(64);
    #lengths = new Int32Array(64);
    #flags = new Uint8Array(64);
    /** Where the fields of the record last cut begin among those held. */
    #first = 0;
    /** The fields held: those of the records appended since `clear`. */
    held = 0;
    /** The fields of the record last cut. */
    fieldCount = 0;
    /** The line breaks inside the quoted fields of the record last cut. */
    breaks = 0;
    /** Where the next record begins, once a record is cut. */
    next = 0;
    fault: Fault = 'unclosed';
    /** The field where the record broke RFC 4180, from zero. */
    faultField = 0;
    /** Where in the file the record broke RFC 4180. */
    faultAt = 0;

    constructor(data: Uint8Array, separator: number) {
        this.#data = data;
        this.#separator = separator;
    }

    start(field: number): number {
        return elementAt(this.#starts, this.#first + field);
    }

    length(field: number): number {
        return elementAt(this.#lengths, this.#first + field);
    }

    flags(field: number): number {
        return elementAt(this.#flags, this.#first + field);
    }

    /** Where held field `index` begins, among the fields of every record appended. */
    heldStart(index: number): number {
        return elementAt(this.#starts, index);
    }

    heldLength(index: number): number {
        return elementAt(this.#lengths, index);
    }

    heldFlags(index: number): number {
        return elementAt(this.#flags, index);
    }

    clear(): void {
        this.held = 0;
    }

    /** Cuts the record that begins at `pos`, letting go of the fields held before. */
    cut(pos: number): Cut {
        this.held = 0;
        return this.append(pos);
    }

    /** Cuts the record that begins at `pos`, and holds its fields after those of the records appended before it. */
    append(pos: number): Cut {
        const data = this.#data;
        const separator = this.#separator;
        // The bytes a record may take: the file's, up to the longest record read.
        const end = Math.min(data.length, pos + maxRecordBytes);
        const final = data.length - pos < maxRecordBytes;
        this.next = pos;
        if (pos >= end) return final ? 'end' : 'needMore';
        this.fieldCount = 0;
        this.breaks = 0;
        this.#first = this.held;
        let fieldsCut = 0;
        let breaks = 0;
        let p = pos;
        for (;;) {
            let start: number;
            let length: number;
            let flag: number;
            if (p < end && data[p] === quote) {
                // A quoted field runs to a quote that is not doubled. Its line breaks are counted, CR LF as one.
                flag = quoted;
                start = p + 1;
                let q = start;
                for (;;) {
                    let at = -1;
                    for (; q < end; q++) {
                        const c = data[q];
                        if (c === quote || c === cr || c === lf) {
                            at = q;
                            break;
                        }
                    }
                    if (at < 0) return final ? this.#malformed('unclosed', p, fieldsCut) : 'needMore';
                    const found = data[at];
                    if (found !== quote) {
                        if (found === cr || at === start || data[at - 1] !== cr) breaks++;
                        q = at + 1;
                        continue;
                    }
                    if (at + 1 < end) {
                        if (data[at + 1] !== quote) {
                            q = at;
                            break;
                        }
                        flag = quoted | doubled;
                        q = at + 2;
                        continue;
                    }
                    // The quote is the last byte at hand: the next byte would say whether it is doubled.
                    if (!final) return 'needMore';
                    q = at;
                    break;
                }
                length = q - start;
                p = q + 1;
                if (p < end) {
                    const after = data[p];
                    if (after !== separator && after !== cr && after !== lf) return this.#malformed('afterQuote', p, fieldsCut);
                }
            } else {
                // An unquoted field runs to the separator or a line end; a quote may not stand in it.
                flag = 0;
                start = p;
                let q = p;
                for (; q < end; q++) {
                    const c = data[q];
                    if (c === separator || c === cr || c === lf || c === quote) break;
                }
                if (q < end && data[q] === quote) return this.#malformed('quoteInside', q, fieldsCut);
                if (q === end && !final) return 'needMore';
                length = q - p;
                p = q;
            }

            const index = this.#first + fieldsCut;
            if (index === this.#starts.length) this.#grow();
            this.#starts[index] = start;
            this.#lengths[index] = length;
            this.#flags[index] = flag;
            fieldsCut++;
            if (p >= end) {
                this.next = p;
                return this.#done(fieldsCut, breaks);
            }
            const b = data[p];
            if (b === separator) {
                p++;
                continue;
            }
            if (b === lf) {
                this.next = p + 1;
                return this.#done(fieldsCut, breaks);
            }
            // CR, alone or before LF.
            if (p + 1 < end) {
                this.next = data[p + 1] === lf ? p + 2 : p + 1;
                return this.#done(fieldsCut, breaks);
            }
            if (!final) return 'needMore';
            this.next = p + 1;
            return this.#done(fieldsCut, breaks);
        }
    }

    #done(fieldsCut: number, breaks: number): Cut {
        this.fieldCount = fieldsCut;
        this.breaks = breaks;
        this.held = this.#first + fieldsCut;
        return 'record';
    }

    #malformed(fault: Fault, at: number, field: number): Cut {
        this.fieldCount = field;
        this.fault = fault;
        this.faultField = field;
        this.faultAt = at;
        return 'malformed';
    }

    #grow(): void {
        const length = this.#starts.length * 2;
        const starts = new Float64Array(length);
        starts.set(this.#starts);
        this.#starts = starts;
        const lengths = new Int32Array(length);
        lengths.set(this.#lengths);
        this.#lengths = lengths;
        const flags = new Uint8Array(length);
        flags.set(this.#flags);
        this.#flags = flags;
    }
}

/** Reads one declared column's field of a record into its builder, or throws the refusal of it: `text` is the
 * field's, or `undefined` when its bytes are not valid in the encoding, and `index` is where the tokenizer
 * holds the field, by which a refusal shows it. */
type FieldReader = (text: string | undefined, index: number) => void;

/**
 * One read of a CSV: the header matched to the declared columns, then the records read into them a batch at
 * a time, as ExGrid.Data's CsvRead reads them. A batch's records are cut first, then read record after
 * record, so the refusal thrown is the first a record-by-record read meets: a value of a record before a
 * malformed one, a malformed one before the records after it.
 */
class CsvRead {
    readonly #plan: CsvPlan;
    readonly #data: Uint8Array;
    readonly #decoding: Decoding;
    readonly #tokenizer: Tokenizer;
    readonly #builders: readonly AnyColumnBuilder[];
    readonly #readers: readonly FieldReader[];
    readonly #fieldOf: number[];
    readonly #lines: number[] = [];
    /** The batch at hand: where its bytes begin, their text, or `undefined` when they are not valid in the
     * encoding, and where each held field's text begins in it, when a character is not always one byte. */
    #batchFrom = 0;
    #batchText: string | undefined;
    #charStarts: Int32Array | undefined;
    #charEnds: Int32Array | undefined;
    /** The record being read: its row among the data records, and the line it begins on. */
    #recordRow = 0;
    #recordLine = 0;
    #columnAt: Map<number, string> | undefined;
    #header: readonly string[] | undefined;
    #expectedFields = -1;
    #headerPending: boolean;
    /** Where the next record begins. */
    #pos: number;

    /** The data records read. */
    rows = 0;
    /** The line of the file the record at hand begins on, from one. */
    line = 1;

    constructor(plan: CsvPlan, data: Uint8Array, encoding: CsvEncoding, start: number) {
        this.#plan = plan;
        this.#data = data;
        this.#decoding = new Decoding(encoding);
        this.#tokenizer = new Tokenizer(data, plan.separator);
        this.#builders = plan.columns.map(column => builderOf(column.kind, column.name, column.caption));
        this.#readers = plan.columns.map((column, c) => this.#reader(column, elementAt(this.#builders, c), c === plan.keyOrdinal));
        this.#fieldOf = plan.columns.map(() => -1);
        this.#headerPending = plan.hasHeader;
        this.#pos = start;
    }

    /** The bytes of the file read so far, its byte-order mark among them. */
    get consumed(): number {
        return this.#pos;
    }

    /** Reads a batch of records; true once the file has ended. */
    parse(): boolean {
        const tokenizer = this.#tokenizer;
        if (this.#headerPending) {
            const cut = tokenizer.cut(this.#pos);
            if (cut === 'end') return true;
            if (cut === 'needMore') throw this.#tooLong();
            if (cut === 'malformed') throw this.#malformed();
            this.#matchHeader();
            this.line += 1 + tokenizer.breaks;
            this.#pos = tokenizer.next;
        }

        // The records first, each beside the line it begins on; a record that ends the batch short — one that
        // breaks RFC 4180, has as many fields as no other, or is too long — is refused once the records before
        // it are read, as it would be read after them.
        tokenizer.clear();
        const batchStart = this.#pos;
        let records = 0;
        let cut: Cut = 'record';
        while (records < batchSize) {
            cut = tokenizer.append(this.#pos);
            if (cut !== 'record') break;
            const count = tokenizer.fieldCount;
            if (this.#expectedFields < 0) this.#place(count);
            if (count !== this.#expectedFields) break;
            this.#lines[records++] = this.line;
            this.line += 1 + tokenizer.breaks;
            this.#pos = tokenizer.next;
        }
        if (records > 0) this.#readBatch(batchStart, records);
        switch (cut) {
            case 'record':
                if (records < batchSize) throw this.#fieldCount(tokenizer.fieldCount);
                return false;
            case 'end': return true;
            case 'needMore': throw this.#tooLong();
            case 'malformed': throw this.#malformed();
            default: return unreachableCut(cut);
        }
    }

    /** Ends the read once the file has. */
    finish(): void {
        if (this.#headerPending) throw new SnapshotError('The file is empty.');
    }

    seal(): Snapshot {
        return sealSnapshot(this.#builders, this.rows, this.#plan.recordKey, 0n);
    }

    /** Reads the batch's records into the columns, record after record. */
    #readBatch(from: number, records: number): void {
        const tokenizer = this.#tokenizer;
        const decoding = this.#decoding;
        const data = this.#data;
        const width = this.#expectedFields;
        const last = records * width - 1;
        const to = tokenizer.heldStart(last) + tokenizer.heldLength(last);
        const text = decoding.strict(data.subarray(from, to));
        this.#batchFrom = from;
        this.#batchText = text;
        this.#charStarts = undefined;
        this.#charEnds = undefined;
        if (text !== undefined && text.length !== to - from) {
            // Where each field's text begins and ends in the batch's, counted on from the field before, so each
            // byte is counted once.
            const charStarts = new Int32Array(records * width);
            const charEnds = new Int32Array(records * width);
            let at = from;
            let chars = 0;
            for (let i = 0; i <= last; i++) {
                const start = tokenizer.heldStart(i);
                const end = start + tokenizer.heldLength(i);
                chars += decoding.characters(data, at, start);
                charStarts[i] = chars;
                chars += decoding.characters(data, start, end);
                charEnds[i] = chars;
                at = end;
            }
            this.#charStarts = charStarts;
            this.#charEnds = charEnds;
        }
        const readers = this.#readers;
        const fieldOf = this.#fieldOf;
        for (let r = 0; r < records; r++) {
            this.#recordRow = this.rows + 1;
            this.#recordLine = elementAt(this.#lines, r);
            for (let c = 0; c < readers.length; c++) {
                const index = r * width + elementAt(fieldOf, c);
                elementAt(readers, c)(this.#fieldText(index), index);
            }
            this.rows++;
        }
    }

    /** Held field `index`'s content, with a doubled quote made single, or `undefined` when it is not valid in
     * the encoding. */
    #fieldText(index: number): string | undefined {
        const tokenizer = this.#tokenizer;
        const start = tokenizer.heldStart(index);
        const length = tokenizer.heldLength(index);
        const text = this.#batchText;
        let value: string | undefined;
        if (text === undefined) {
            value = this.#decoding.strict(this.#data.subarray(start, start + length));
        } else if (this.#charStarts === undefined || this.#charEnds === undefined) {
            value = text.slice(start - this.#batchFrom, start - this.#batchFrom + length);
        } else {
            value = text.slice(elementAt(this.#charStarts, index), elementAt(this.#charEnds, index));
        }
        return value !== undefined && (tokenizer.heldFlags(index) & doubled) !== 0 ? value.replaceAll('""', '"') : value;
    }

    /** Held field `index` as a refusal shows it: its text, or its bytes decoded leniently when they are not valid. */
    #show(index: number, text: string | undefined): string {
        if (text !== undefined) return shown(text);
        const tokenizer = this.#tokenizer;
        const start = tokenizer.heldStart(index);
        const lenient = this.#decoding.lenient(this.#data.subarray(start, start + tokenizer.heldLength(index)));
        return shown((tokenizer.heldFlags(index) & doubled) !== 0 ? lenient.replaceAll('""', '"') : lenient);
    }

    /** A refusal of the record being read, in `column`. */
    #refuseField(column: string, reason: string): SnapshotError {
        return new SnapshotError(`${reason} (line ${grouped(this.#recordLine)}).`, { row: this.#recordRow, column });
    }

    /** A refusal of the record at hand — the one after the last read — in `column`, or in none. */
    #refuse(column: string | undefined, reason: string): SnapshotError {
        const text = `${reason} (line ${grouped(this.line)}).`;
        return new SnapshotError(text, column === undefined ? { row: this.rows + 1 } : { row: this.rows + 1, column });
    }

    #tooLong(): SnapshotError {
        const reason = `is longer than ${maxRecordBytes >> 20} MiB, which no spreadsheet writes; a quote may be left open`;
        return this.#headerPending
            ? new SnapshotError(`The header ${reason} (line ${grouped(this.line)}).`)
            : this.#refuse(undefined, `the record ${reason}`);
    }

    /** A header field's content: what lies between its quotes, with a doubled quote made single, or
     * `undefined` when it is not valid in the encoding. */
    #headerField(field: number): string | undefined {
        const tokenizer = this.#tokenizer;
        const start = tokenizer.start(field);
        const text = this.#decoding.strict(this.#data.subarray(start, start + tokenizer.length(field)));
        return text !== undefined && (tokenizer.flags(field) & doubled) !== 0 ? text.replaceAll('""', '"') : text;
    }

    /** Matches the declared columns to the header row: by position where one is declared, and by the exact
     * header otherwise. */
    #matchHeader(): void {
        const count = this.#tokenizer.fieldCount;
        const header: string[] = [];
        const first = new Map<string, number>();
        const repeats = new Map<string, number>();
        for (let f = 0; f < count; f++) {
            const text = this.#headerField(f);
            if (text === undefined) throw new SnapshotError(`The header is not valid ${this.#decoding.name} (line ${grouped(this.line)}).`);
            header.push(text);
            if (first.has(text)) {
                const before = repeats.get(text);
                repeats.set(text, (before === undefined ? 1 : before) + 1);
            } else {
                first.set(text, f);
            }
        }
        this.#header = header;

        const missing: PlannedColumn[] = [];
        this.#plan.columns.forEach((column, c) => {
            if (column.position !== undefined) {
                if (column.position >= count) {
                    throw new SnapshotError(`it is declared at position ${column.position}, but the header has ${fields(count)}.`, { column: column.name });
                }
                this.#fieldOf[c] = column.position;
                return;
            }
            const field = first.get(column.header);
            if (field === undefined) {
                missing.push(column);
                return;
            }
            const repeated = repeats.get(column.header);
            if (repeated !== undefined) {
                throw new SnapshotError(`the header holds '${column.header}' ${times(repeated)}, so which field it is cannot be told; declare its Position.`, { column: column.name });
            }
            this.#fieldOf[c] = field;
        });
        if (missing.length > 0) throw this.#missing(missing, header);
        this.#expectedFields = count;
        this.#indexed();
        this.#headerPending = false;
    }

    /** Places the declared columns in a file without a header row, by their positions, once its first record
     * says how many fields a record has. */
    #place(count: number): void {
        this.#plan.columns.forEach((column, c) => {
            const at = column.position === undefined ? c : column.position;
            if (at >= count) throw new SnapshotError(`it is declared at position ${at}, but the first record has ${fields(count)}.`, { column: column.name });
            this.#fieldOf[c] = at;
        });
        this.#expectedFields = count;
        this.#indexed();
    }

    #indexed(): void {
        const columnAt = new Map<number, string>();
        this.#plan.columns.forEach((column, c) => {
            const field = elementAt(this.#fieldOf, c);
            if (!columnAt.has(field)) columnAt.set(field, column.name);
        });
        this.#columnAt = columnAt;
    }

    #missing(missing: readonly PlannedColumn[], header: readonly string[]): SnapshotError {
        const column = elementAt(missing, 0);
        const wanted = column.header;
        let reason = wanted === column.name ? 'it is missing from the header' : `its header, '${wanted}', is missing from the header row`;
        const others = missing.slice(1).map(other => `'${other.name}'`);
        if (others.length === 1) reason += `, and so is ${elementAt(others, 0)}`;
        else if (others.length > 1) reason += `, and so are ${others.slice(0, -1).join(', ')} and ${elementAt(others, others.length - 1)}`;
        const near = header.find(each => sameIgnoringCase(dotnetTrim(each), dotnetTrim(wanted)));
        if (near !== undefined) reason += `; the header has '${near}'`;
        return new SnapshotError(`${reason}.`, { column: column.name });
    }

    #fieldCount(count: number): SnapshotError {
        const tokenizer = this.#tokenizer;
        const source = this.#header === undefined ? 'the first record' : 'the header';
        if (count === 1 && tokenizer.length(0) === 0 && tokenizer.flags(0) === 0) {
            return this.#refuse(undefined, `the line is empty, where ${source} has ${fields(this.#expectedFields)}`);
        }
        return this.#refuse(undefined, `the record has ${fields(count)} where ${source} has ${grouped(this.#expectedFields)}`);
    }

    #malformed(): SnapshotError {
        const tokenizer = this.#tokenizer;
        const field = tokenizer.faultField;
        let reason: string;
        switch (tokenizer.fault) {
            case 'unclosed': reason = 'a quoted field is not closed before the file ends'; break;
            case 'quoteInside': reason = 'a quote stands inside a field that does not begin with one'; break;
            case 'afterQuote': {
                const after = shown(this.#decoding.lenient(this.#data.subarray(tokenizer.faultAt, tokenizer.faultAt + 1)));
                reason = `a quoted field's closing quote is followed by ${after} rather than a separator or a line end`;
                break;
            }
            default: return unreachableFault(tokenizer.fault);
        }
        if (this.#headerPending) return new SnapshotError(`The header's field ${field + 1}: ${reason} (line ${grouped(this.line)}).`);
        let column: string | undefined;
        if (this.#columnAt !== undefined) {
            column = this.#columnAt.get(field);
            if (column === undefined && this.#header !== undefined && field < this.#header.length) column = elementAt(this.#header, field);
        }
        return column === undefined ? this.#refuse(undefined, `in field ${field + 1} of the record, ${reason}`) : this.#refuse(column, reason);
    }

    /** The reader of a declared column, by its kind, into `builder`, which is of that kind. */
    #reader(column: PlannedColumn, builder: AnyColumnBuilder, isKey: boolean): FieldReader {
        const blanks = column.blankText.filter(text => text !== '');
        const isBlank = (text: string | undefined): boolean => text === '' || (text !== undefined && blanks.includes(text));
        const refuse = (reason: string): SnapshotError => this.#refuseField(column.name, reason);
        const show = (index: number, text: string | undefined): string => this.#show(index, text);
        switch (builder.kind) {
            case 'text': {
                const name = this.#decoding.name;
                return text => {
                    if (isBlank(text)) {
                        if (isKey) throw refuse('the Record Key is Blank');
                        builder.appendBlank();
                        return;
                    }
                    if (text === undefined) throw refuse(`the text is not valid ${name}`);
                    builder.append(text);
                };
            }
            case 'decimal': {
                const reading = numberReading(column);
                return (text, index) => {
                    if (isBlank(text)) {
                        builder.appendBlank();
                        return;
                    }
                    const value = text === undefined ? undefined : trimSpaces(text);
                    const short = value === undefined ? undefined : parseShort(value, reading);
                    if (short !== undefined) {
                        builder.append(BigInt(short.negative ? -short.mantissa : short.mantissa), short.scale);
                        return;
                    }
                    const parsed = value === undefined ? undefined : parseNumber(value, reading);
                    if (parsed === undefined || parsed.status === 'notANumber') throw refuse(`${show(index, text)} is not a number`);
                    if (parsed.status === 'ok' && parsed.scale <= maxDecimalScale) {
                        const magnitude = parsed.digits === '' ? 0n : BigInt(parsed.digits);
                        if (magnitude < decimalLimit) {
                            builder.append(parsed.negative ? -magnitude : magnitude, parsed.scale);
                            return;
                        }
                    }
                    throw refuse(`${show(index, text)} has more digits than a Decimal holds exactly`);
                };
            }
            case 'integer': {
                const reading = numberReading(column);
                return (text, index) => {
                    if (isBlank(text)) {
                        if (isKey) throw refuse('the Record Key is Blank');
                        builder.appendBlank();
                        return;
                    }
                    const value = text === undefined ? undefined : trimSpaces(text);
                    const short = value === undefined ? undefined : parseShort(value, reading);
                    if (short !== undefined && !short.hasPoint) {
                        builder.appendSafe(short.negative ? -short.mantissa : short.mantissa);
                        return;
                    }
                    const parsed = value === undefined ? undefined : parseNumber(value, reading);
                    if (parsed !== undefined && parsed.status !== 'notANumber' && !parsed.hasPoint) {
                        if (parsed.status === 'ok') {
                            const magnitude = parsed.digits === '' ? 0n : BigInt(parsed.digits);
                            const whole = parsed.negative ? -magnitude : magnitude;
                            if (whole >= int64Min && whole <= int64Max) {
                                builder.append(whole);
                                return;
                            }
                        }
                        throw refuse(`${show(index, text)} is outside the range of a 64-bit Integer`);
                    }
                    throw refuse(`${show(index, text)} is not an integer`);
                };
            }
            case 'double': {
                const reading = numberReading(column);
                return (text, index) => {
                    if (isBlank(text)) {
                        builder.appendBlank();
                        return;
                    }
                    if (text === undefined) throw refuse(`${show(index, text)} is not a number`);
                    const value = trimSpaces(text);
                    const plain = plainNumber(value, reading);
                    const normal = plain === undefined ? normalizeDouble(value, reading) : undefined;
                    // Only NaN and Infinity are read past the column's separators.
                    const number = plain !== undefined ? plain : normal === undefined ? nonFinite(value) : Number(normal);
                    if (number === undefined) throw refuse(`${show(index, text)} is not a number`);
                    if ((number === Number.POSITIVE_INFINITY || number === Number.NEGATIVE_INFINITY) && hasDigit(value)) {
                        throw refuse(`${show(index, text)} is outside the range of a Double`);
                    }
                    if (Number.isNaN(number)) builder.appendBytes(dotnetNaN, 0);
                    else builder.append(number);
                };
            }
            case 'date': {
                const formats = column.dateFormats;
                const reason = formats.length === 1
                    ? `is not a date in the format '${elementAt(formats, 0).format}'`
                    : `is not a date in any of the formats ${formats.map(format => `'${format.format}'`).join(', ')}`;
                return (text, index) => {
                    if (isBlank(text)) {
                        builder.appendBlank();
                        return;
                    }
                    if (text !== undefined) {
                        const value = trimSpaces(text);
                        for (const format of formats) {
                            const ticks = format.read(value);
                            if (ticks !== undefined) {
                                builder.append(ticks);
                                return;
                            }
                        }
                    }
                    throw refuse(`${show(index, text)} ${reason}`);
                };
            }
            case 'boolean': {
                const trues = column.trueText;
                const falses = column.falseText;
                const spellings = [...trues, ...falses].map(spelling => `'${spelling}'`).join(', ');
                return (text, index) => {
                    if (isBlank(text)) {
                        builder.appendBlank();
                        return;
                    }
                    if (text !== undefined) {
                        const value = trimSpaces(text);
                        if (isOne(value, trues)) {
                            builder.append(true);
                            return;
                        }
                        if (isOne(value, falses)) {
                            builder.append(false);
                            return;
                        }
                    }
                    throw refuse(`${show(index, text)} is not a spelling of true or false (${spellings})`);
                };
            }
            default: return unreachableKind(builder);
        }
    }
}

/** Whether `value` is one of `spellings`, ignoring case. */
function isOne(value: string, spellings: readonly string[]): boolean {
    for (const spelling of spellings) {
        if (value === spelling || sameIgnoringCase(value, spelling)) return true;
    }
    return false;
}

function unreachableCut(cut: never): never {
    throw new Error(`Internal: ${String(cut)} is not what a cut comes to.`);
}

function unreachableFault(fault: never): never {
    throw new Error(`Internal: ${String(fault)} is not how a record breaks RFC 4180.`);
}

function unreachableKind(builder: never): never {
    throw new Error(`Internal: ${String(builder)} is not a column builder of a kind.`);
}
