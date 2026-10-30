// Cutting a CSV's bytes into records under RFC 4180, and decoding a field's, as ExGrid.Data's CsvTokenizer and
// CsvText do: shared by the read under a Schema (csv-read.ts) and the suggestion of one (suggest.ts).

import { elementAt } from '../must';
import { SnapshotError } from '../snapshot-error';
import { encodingName, type CsvEncoding } from './schema';

/** The longest record read. Excel holds at most 32,767 characters in a cell, so a record this long is a quote
 * left open, and the file is not read to its end to find that out. ExGrid.Data's CsvLoad.MaxRecordBytes. */
export const maxRecordBytes = 64 << 20;

export const quote = 0x22;
export const cr = 0x0d;
export const lf = 0x0a;

/** A field in quotes. */
export const quoted = 1;
/** A quoted field holding a doubled quote, which stands for one. */
export const doubled = 2;

/** What cutting a record from the bytes came to. */
export type Cut = 'record' | 'needMore' | 'end' | 'malformed';

/** How a record broke RFC 4180. */
export type Fault = 'unclosed' | 'quoteInside' | 'afterQuote';

/** The length of the byte-order mark the file begins with: UTF-8's, which wins over the declared encoding, or
 * none. UTF-16's is refused, since a CSV is read only in UTF-8 or Shift-JIS. */
export function preamble(data: Uint8Array, declared: CsvEncoding): number {
    if (data[0] === 0xef && data[1] === 0xbb && data[2] === 0xbf) return 3;
    if ((data[0] === 0xff && data[1] === 0xfe) || (data[0] === 0xfe && data[1] === 0xff)) {
        throw new SnapshotError(`The file begins with UTF-16's byte-order mark; a CSV is read in UTF-8 or Shift-JIS, and the Schema declares ${encodingName(declared)}.`);
    }
    return 0;
}

/** A file a CSV is read from: bytes held, or a Blob read as far as needed. */
export type CsvSource = { readonly kind: 'bytes'; readonly bytes: Uint8Array } | { readonly kind: 'blob'; readonly blob: Blob };

/** The file as bytes held or a Blob, or a `TypeError` for anything else. */
export function sourceOf(file: Uint8Array | ArrayBuffer | Blob): CsvSource {
    if (file instanceof Uint8Array) return { kind: 'bytes', bytes: file };
    if (file instanceof ArrayBuffer) return { kind: 'bytes', bytes: new Uint8Array(file) };
    if (file instanceof Blob) return { kind: 'blob', blob: file };
    throw new TypeError('A CSV is read from a Uint8Array, an ArrayBuffer or a Blob.');
}

/** The text without the ASCII spaces around it, which the kinds other than Text set aside. */
export function trimSpaces(text: string): string {
    let from = 0;
    let to = text.length;
    while (from < to && text.charCodeAt(from) === 0x20) from++;
    while (to > from && text.charCodeAt(to - 1) === 0x20) to--;
    return from === 0 && to === text.length ? text : text.slice(from, to);
}

/** The file's text in its encoding: decoded strictly for a value, shown leniently for a refusal. */
export class Decoding {
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
export class Tokenizer {
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

    /** Whether the bytes run to the end of the file: a sample's do not, so a record at its end is not cut. */
    readonly #eof: boolean;

    constructor(data: Uint8Array, separator: number, eof = true) {
        this.#data = data;
        this.#separator = separator;
        this.#eof = eof;
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
        const final = this.#eof && data.length - pos < maxRecordBytes;
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
