// A Schema suggested from a CSV's first rows (ADR-0064, Q33), as ExGrid.Data's CsvSuggester suggests one: only
// the sample is read; each column is proposed a kind and a reading under which the whole sample reads, checked
// with the reader's own parsers; and every column whose reading is not clear — the account number `00123`, a
// comma that could be the decimal point, a date that could be the first of February or the second of January
// — is marked rather than settled quietly. The marks are ExGrid.Data's words, which the shared cases under
// tests/cases/data hold both languages to.

import { decimalLimit, maxDecimalScale } from '../decimal';
import { elementAt, int64Max, int64Min } from '../must';
import { SnapshotError, grouped } from '../snapshot-error';
import type { SnapshotKind } from '../snapshot';
import { foldCase } from '../text-casing';
import { dayFirst, listSeparatorOf, localeRefusal, numberCulture } from './culture';
import { DateFormat } from './date-text';
import { nonFinite, normalizeDouble, parseNumber, plainNumber, type NumberReading } from './number-text';
import { sameIgnoringCase, separatorCode, type CsvColumn, type CsvEncoding, type CsvSchema, type CsvSeparator } from './schema';
import { Decoding, Tokenizer, cr, doubled, lf, preamble, sourceOf, trimSpaces } from './tokenizer';

/** The bytes read first, doubling until the sample holds the rows asked for. */
const chunk = 1 << 16;
/** The most a sample takes, as the longest record read. */
const maxSampleBytes = 64 << 20;
/** The distinct values a column suggestion shows. */
const maxExamples = 5;
/** The values a mark names before it counts the rest. */
const maxNamed = 3;

/**
 * Why a suggestion is not clear, as ExGrid.Data's `CsvDoubt` names it:
 *
 * - `empty`: the column holds nothing in the sampled rows, or only texts that stand for nothing.
 * - `leadingZeros`: numbers written with a leading zero, which could be identifiers; suggested as Text.
 * - `bothSeparators`: numbers written with both `,` and `.`.
 * - `ambiguousSeparator`: a number that reads as two values, as `,` or `.` is taken as the decimal point.
 * - `mixedDateFormats`: dates written in more than one format.
 * - `dayOrMonth`: a date that could have its day or its month first.
 * - `mixedKinds`: numbers or dates but for a few values; suggested as Text.
 * - `blankText`: texts such as `NULL` or `N/A` among the values, suggested as Blanks.
 * - `couldBeBoolean`: only yes and no, or 0 and 1, which could be a Boolean with those spellings declared.
 * - `tooLong`: a number with more digits than a Decimal holds; suggested as Text.
 * - `headerName`: a header empty or repeated, so the column is matched by its position.
 * - `separator`: more than one separator splits every record alike, or none does.
 * - `header`: whether the first record is a header row cannot be told.
 * - `fieldCount`: records with other than the header's number of fields, which a read under the Schema refuses.
 */
export type CsvDoubt = 'empty' | 'leadingZeros' | 'bothSeparators' | 'ambiguousSeparator' | 'mixedDateFormats' | 'dayOrMonth' | 'mixedKinds'
    | 'blankText' | 'couldBeBoolean' | 'tooLong' | 'headerName' | 'separator' | 'header' | 'fieldCount';

/** Something a suggestion could not settle, and what it suggested instead, for the user to confirm. */
export interface CsvMark {
    /** What kind of doubt it is. */
    readonly doubt: CsvDoubt;
    /** The doubt in a sentence, naming the values and the rows it is about. */
    readonly note: string;
}

/** What is suggested for one column of the file. */
export interface CsvColumnSuggestion {
    /** The column suggested: its name, its kind and its reading, as the Schema holds it. */
    readonly column: CsvColumn;
    /** Why the column's kind or reading is not clear; none when it is. */
    readonly marks: readonly CsvMark[];
    /** Whether the column carries a mark. */
    readonly isUnclear: boolean;
    /** The first few distinct values of the column, as written. */
    readonly examples: readonly string[];
}

/** A Schema suggested from a file's first rows: a proposal, which reads the file only once it is handed to
 * `readCsvSnapshot`, changed or not. */
export interface CsvSuggestion {
    /** The Schema suggested, its separator, header row and encoding stated. */
    readonly schema: CsvSchema;
    /** What is suggested for each column, in the Schema's order. */
    readonly columns: readonly CsvColumnSuggestion[];
    /** What is not clear about the file as a whole: its separator, its header row, its records' fields. */
    readonly marks: readonly CsvMark[];
    /** The data records the suggestion read. */
    readonly sampledRows: number;
    /** Whether the file or any column carries a mark. */
    readonly isUnclear: boolean;
}

/** How a Schema is suggested: how much is read, and what is already known about the file. */
export interface CsvSuggestionOptions {
    /** The data records read; 1,000 when left out. */
    readonly rows?: number;
    /** The file's encoding, when it does not begin with UTF-8's byte-order mark; UTF-8 when left out. */
    readonly encoding?: CsvEncoding;
    /** The separator, when it is known; found from the sample when left out. */
    readonly separator?: CsvSeparator;
    /** Whether the first record is a header row, when it is known; told from the sample when left out. */
    readonly hasHeader?: boolean;
    /** The locale whose conventions settle what the sample cannot: its decimal point, whether its dates have
     * the day first, and its list separator. .NET's invariant culture when left out. */
    readonly culture?: string;
    /** Cancels the suggestion, which rejects with the signal's reason. */
    readonly signal?: AbortSignal;
}

/** Texts that stand for a value that is not there, which a suggestion proposes as Blanks in a column of another kind. */
const blankWords = ['NULL', 'N/A', 'NA', '#N/A', '-', '--', 'nil', 'None', '(null)', '(blank)'];

/** The readings of a number a suggestion tries, simplest first: the decimal point, then the thousands separator. */
const readingTexts: readonly (readonly [point: string, thousands: string])[] = [
    ['.', ''],
    [',', ''],
    ['.', ','],
    [',', '.'],
    [',', ' '],
    [',', ' '],
];

const readings: readonly NumberReading[] = readingTexts.map(([point, thousands]) => Object.freeze({
    point, thousands, groupSizes: [3], minus: '', plus: '', plain: point === '.' && thousands === '',
}));

/** The date formats a suggestion tries: ISO 8601, then year first, day first and month first, as spreadsheets
 * write them. */
const dateTexts = [
    'yyyy-MM-dd',
    'yyyy-MM-ddTHH:mm:ss',
    'yyyy-MM-dd HH:mm:ss',
    'yyyy-MM-ddTHH:mm:ss.FFFFFFF',
    'yyyy-MM-dd HH:mm:ss.FFFFFFF',
    'yyyy-MM-ddTHH:mm',
    'yyyy-MM-dd HH:mm',
    'yyyy-MM-ddTHH:mm:ss.FFFFFFFK',
    'yyyy/M/d',
    'yyyy/M/d H:mm',
    'yyyy/M/d H:mm:ss',
    'd/M/yyyy',
    'd/M/yyyy H:mm',
    'd/M/yyyy H:mm:ss',
    'M/d/yyyy',
    'M/d/yyyy H:mm',
    'M/d/yyyy H:mm:ss',
    'M/d/yyyy h:mm tt',
    'M/d/yyyy h:mm:ss tt',
    'd.M.yyyy',
    'd.M.yyyy H:mm',
    'd.M.yyyy H:mm:ss',
];

const dateFormats: readonly DateFormat[] = dateTexts.map(text => {
    const format = DateFormat.of(text, undefined);
    if (typeof format === 'string') throw new Error(`Internal: the suggested format '${text}' is refused: ${format}.`);
    return format;
});

const separators: readonly CsvSeparator[] = ['comma', 'tab', 'semicolon'];
const encodings: readonly string[] = ['utf-8', 'shift-jis'];

/** A non-empty field of a column, with its row among the records it was taken from, from one. */
interface Value {
    readonly text: string;
    readonly row: number;
}

/** What is suggested for a column. */
interface Proposal {
    readonly kind: SnapshotKind;
    readonly point?: string;
    readonly thousands?: string;
    readonly dateFormats?: readonly string[];
    readonly blankText?: readonly string[];
}

/** A number as a Decimal holds it: ± its digits, `scale` of them after the point, the trailing zeros there not counted. */
interface DecimalValue {
    readonly negative: boolean;
    readonly digits: string;
    readonly scale: number;
}

interface Settings {
    readonly rows: number;
    readonly encoding: CsvEncoding;
    readonly separator: CsvSeparator | undefined;
    readonly hasHeader: boolean | undefined;
    readonly culture: string | undefined;
}

/**
 * Suggests a Schema for a CSV nobody has described, from its first rows (ADR-0064, Q33), as ExGrid.Data's
 * `CsvSchema.SuggestAsync` does: each column's kind and reading, with every column whose kind is not clear
 * marked, for the user to confirm. Nothing applies it: the file is read under it only when it, or a Schema
 * changed from it, is handed to `readCsvSnapshot`. A `Blob` is read only as far as the rows sampled.
 *
 * Options that contradict themselves are refused with a `TypeError` or a `RangeError`. The sample is refused
 * with a {@link SnapshotError} when it cannot be read at all: it is empty, not valid in the encoding, or a
 * quote in it is out of place.
 */
export async function suggestCsvSchema(file: Uint8Array | ArrayBuffer | Blob, options: CsvSuggestionOptions = {}): Promise<CsvSuggestion> {
    const settings = check(options);
    const { signal } = options;
    signal?.throwIfAborted();
    const records = settings.rows + 1;
    const { bytes: sampled, eof } = await sample(file, records, signal);
    const start = preamble(sampled, settings.encoding);
    const encoding = start === 3 ? 'utf-8' : settings.encoding;
    const bytes = sampled.subarray(start);
    const { culture } = settings;
    const fileMarks: CsvMark[] = [];

    const separator = settings.separator ?? detect(bytes, eof, records, culture, fileMarks);
    const cut = recordsOf(bytes, separatorCode(separator), eof, records, encoding);
    if (cut.length === 0) throw new SnapshotError('The file is empty, so no Schema can be suggested.');
    // What the header row's test proposed for each column below the first record, which is the data's when it
    // finds a header row: proposed once, not again.
    const proposed = new Map<number, { readonly proposal: Proposal; readonly marks: readonly CsvMark[] }>();
    const hasHeader = settings.hasHeader ?? headerRow(cut, separator, culture, fileMarks, proposed);
    const data = (hasHeader ? cut.slice(1) : cut).slice(0, settings.rows);
    const headers = hasHeader ? elementAt(cut, 0) : [];
    const width = hasHeader ? headers.length : modal(data.map(record => record.length));

    const other = data.flatMap((record, i) => (record.length === width ? [] : [i + 1]));
    if (other.length > 0) {
        const one = other.length === 1;
        fileMarks.push(mark('fieldCount', `${rowsNamed(other)} ${one ? 'has' : 'have'} other than the ${grouped(width)} fields `
            + `${hasHeader ? 'the header has' : 'the first record has'}; reading the file under this Schema refuses ${one ? 'it' : 'them'}.`));
    }

    const names = new Set<string>();
    const seen = new Set<string>();
    const repeated = new Set<string>();
    for (const header of headers) {
        if (seen.has(header)) repeated.add(header);
        seen.add(header);
    }
    const columns: CsvColumn[] = [];
    const suggestions: CsvColumnSuggestion[] = [];
    for (let i = 0; i < width; i++) {
        // The sample is read at once, a column at a time; a cancel is heard between columns.
        signal?.throwIfAborted();
        const values = columnOf(data, i);
        const known = hasHeader ? proposed.get(i) : undefined;
        const marks: CsvMark[] = known === undefined ? [] : [...known.marks];
        const proposal = known === undefined ? propose(values, separator, culture, marks) : known.proposal;

        let name: string;
        let position: number | undefined;
        const text = i < headers.length ? elementAt(headers, i) : '';
        if (hasHeader && text.length > 0 && !repeated.has(text) && !names.has(text)) {
            names.add(text);
            name = text;
        } else {
            name = unique(names, `Column${i + 1}`);
            if (hasHeader) {
                position = i;
                marks.push(mark('headerName', text.length === 0
                    ? `The header is empty here, so the column is matched by its position, ${i}, and named '${name}'.`
                    : `The header '${text}' stands more than once, so the column is matched by its position, ${i}, and named '${name}'.`));
            }
        }

        const column: CsvColumn = Object.freeze({
            name,
            kind: proposal.kind,
            ...(position === undefined ? {} : { position }),
            ...(proposal.point === undefined ? {} : { decimalPoint: proposal.point }),
            ...(proposal.thousands === undefined ? {} : { thousandsSeparator: proposal.thousands }),
            ...(proposal.dateFormats === undefined ? {} : { dateFormats: proposal.dateFormats }),
            ...(proposal.blankText === undefined ? {} : { blankText: proposal.blankText }),
        });
        columns.push(column);
        const examples = [...new Set(values.map(value => value.text))].slice(0, maxExamples);
        suggestions.push(Object.freeze({ column, marks: Object.freeze(marks), isUnclear: marks.length > 0, examples: Object.freeze(examples) }));
    }

    const schema: CsvSchema = Object.freeze({ columns: Object.freeze(columns), separator, hasHeader, encoding });
    return Object.freeze({
        schema,
        columns: Object.freeze(suggestions),
        marks: Object.freeze(fileMarks),
        sampledRows: data.length,
        isUnclear: fileMarks.length > 0 || suggestions.some(suggestion => suggestion.isUnclear),
    });
}

/** The options, checked, with what is left out filled in. */
function check(options: CsvSuggestionOptions): Settings {
    const { rows = 1000, encoding = 'utf-8', separator, hasHeader, culture } = options;
    if (!Number.isInteger(rows) || rows < 1) throw new RangeError(`The suggestion reads at least one row, not ${JSON.stringify(rows)}.`);
    if (!encodings.includes(encoding)) {
        throw new TypeError(`The encoding ${JSON.stringify(encoding)} is not one a CSV is read in: 'utf-8' or 'shift-jis'.`);
    }
    if (separator !== undefined && !separators.includes(separator)) {
        throw new TypeError(`The separator ${JSON.stringify(separator)} is not one a CSV is read with: a comma, a tab or a semicolon.`);
    }
    if (hasHeader !== undefined && typeof hasHeader !== 'boolean') {
        throw new TypeError(`Whether the file has a header row is true or false, not ${JSON.stringify(hasHeader)}.`);
    }
    if (culture !== undefined) {
        const refusal = localeRefusal(culture);
        if (refusal !== undefined) throw new TypeError(`The suggestion reads in ${refusal}.`);
    }
    return { rows, encoding, separator, hasHeader, culture };
}

/**
 * The start of the file, as ExGrid.Data samples a stream: read a chunk at a time, the room doubling, until it
 * holds more line breaks than twice `records` — a CR LF counts twice, and a quoted field may hold breaks — or
 * the file ends (`eof`), or it fills the most a sample takes.
 */
async function sample(file: Uint8Array | ArrayBuffer | Blob, records: number, signal: AbortSignal | undefined): Promise<{ readonly bytes: Uint8Array; readonly eof: boolean }> {
    const source = sourceOf(file);
    const size = source.kind === 'bytes' ? source.bytes.length : source.blob.size;
    const pieces: Uint8Array[] = [];
    let room = chunk;
    let length = 0;
    let breaks = 0;
    const taken = (eof: boolean): { readonly bytes: Uint8Array; readonly eof: boolean } => ({
        bytes: source.kind === 'bytes' ? source.bytes.subarray(0, length) : joined(pieces, length),
        eof,
    });
    for (;;) {
        if (length === room) {
            if (room >= maxSampleBytes) return taken(false);
            room = Math.min(maxSampleBytes, room * 2);
        }
        const read = Math.min(size - length, room - length);
        if (read === 0) return taken(true);
        let fresh: Uint8Array;
        if (source.kind === 'bytes') {
            fresh = source.bytes.subarray(length, length + read);
        } else {
            fresh = new Uint8Array(await source.blob.slice(length, length + read).arrayBuffer());
            signal?.throwIfAborted();
            pieces.push(fresh);
        }
        for (const byte of fresh) {
            if (byte === lf || byte === cr) breaks++;
        }
        length += read;
        if (breaks > 2 * records + 2) return taken(false);
    }
}

function joined(pieces: readonly Uint8Array[], length: number): Uint8Array {
    const bytes = new Uint8Array(length);
    let at = 0;
    for (const piece of pieces) {
        bytes.set(piece, at);
        at += piece.length;
    }
    return bytes;
}

const lenientUtf8 = new TextDecoder('utf-8', { ignoreBOM: true });

/**
 * The separator that splits every record of the sample into the same number of fields, two or more. When
 * several do, the one whose fields read best as numbers and dates is suggested, then the culture's list
 * separator, and the file is marked.
 */
function detect(bytes: Uint8Array, eof: boolean, records: number, culture: string | undefined, marks: CsvMark[]): CsvSeparator {
    const even: { readonly separator: CsvSeparator; readonly typed: number }[] = [];
    const uneven: { readonly separator: CsvSeparator; readonly alike: number }[] = [];
    let anySplit = false;
    for (const candidate of separators) {
        const widths = widthsOf(bytes, separatorCode(candidate), eof, records);
        if (widths === undefined || widths.counts.length === 0) continue;
        const { counts } = widths;
        anySplit ||= counts.some(count => count > 1);
        const most = modal(counts);
        if (most > 1 && counts.every(count => count === most)) even.push({ separator: candidate, typed: widths.typed });
        else if (most > 1) uneven.push({ separator: candidate, alike: counts.filter(count => count === most).length });
    }
    const [single] = even;
    if (single !== undefined && even.length === 1) return single.separator;
    if (single !== undefined) {
        const listSeparator = listSeparatorOf(culture);
        const isList = (each: { readonly separator: CsvSeparator }): number => Number(character(each.separator) === listSeparator);
        const ordered = even.toSorted((a, b) => (b.typed === a.typed ? isList(b) - isList(a) : b.typed - a.typed));
        const chosen = elementAt(ordered, 0);
        // The note says what chose it: the fields, else the culture, else only the order they are tried in.
        const reason = chosen.typed > elementAt(ordered, 1).typed
            ? 'since its fields read best as numbers and dates'
            : isList(chosen) === 1
                ? "since the fields read alike under each and it is the culture's list separator"
                : "though the fields read alike under each and the culture's list separator is none of them";
        marks.push(mark('separator', `Every record splits evenly at ${even.map(each => shown(each.separator)).join(' and at ')}; `
            + `${shown(chosen.separator)} is suggested, ${reason}.`));
        return chosen.separator;
    }
    const best = uneven.toSorted((a, b) => b.alike - a.alike)[0];
    if (!anySplit || best === undefined) return 'comma';
    marks.push(mark('separator', `No separator splits every record into the same number of fields; ${shown(best.separator)} is suggested, `
        + 'since it splits the most records alike.'));
    return best.separator;
}

/** The number of fields of each record of the sample cut at `separator`, and how many fields after the first
 * record read as a number, a date or a Boolean; `undefined` when a record breaks RFC 4180 under it. */
function widthsOf(bytes: Uint8Array, separator: number, eof: boolean, records: number): { readonly counts: number[]; readonly typed: number } | undefined {
    const tokenizer = new Tokenizer(bytes, separator, eof);
    const counts: number[] = [];
    let typed = 0;
    let pos = 0;
    while (counts.length < records) {
        const cut = tokenizer.cut(pos);
        if (cut === 'needMore' || cut === 'end') break;
        if (cut === 'malformed') return undefined;
        counts.push(tokenizer.fieldCount);
        if (counts.length > 1) {
            for (let f = 0; f < tokenizer.fieldCount; f++) {
                const length = tokenizer.length(f);
                if (length === 0) continue;
                const start = tokenizer.start(f);
                if (isTyped(lenientUtf8.decode(bytes.subarray(start, start + length)))) typed++;
            }
        }
        pos = tokenizer.next;
    }
    return { counts, typed };
}

/** The sample's records, each field decoded strictly; a record cut short by the end of the sample is left out. */
function recordsOf(bytes: Uint8Array, separator: number, eof: boolean, records: number, encoding: CsvEncoding): string[][] {
    const tokenizer = new Tokenizer(bytes, separator, eof);
    const decoding = new Decoding(encoding);
    const result: string[][] = [];
    let pos = 0;
    let line = 1;
    while (result.length < records) {
        const cut = tokenizer.cut(pos);
        if (cut === 'needMore' || cut === 'end') break;
        if (cut === 'malformed') {
            let reason: string;
            switch (tokenizer.fault) {
                case 'unclosed': reason = 'a quoted field is not closed before the sample ends'; break;
                case 'quoteInside': reason = 'a quote stands inside a field that does not begin with one'; break;
                case 'afterQuote': reason = "a quoted field's closing quote is followed by something other than a separator or a line end"; break;
                default: return tokenizer.fault satisfies never;
            }
            throw new SnapshotError(`Line ${grouped(line)}: ${reason}, so no Schema can be suggested.`);
        }
        const fields: string[] = [];
        for (let f = 0; f < tokenizer.fieldCount; f++) {
            const start = tokenizer.start(f);
            const text = decoding.strict(bytes.subarray(start, start + tokenizer.length(f)));
            if (text === undefined) {
                throw new SnapshotError(`Line ${grouped(line)}: the text is not valid ${decoding.name}; if the file is in another encoding, `
                    + "say which in the suggestion's options.");
            }
            fields.push((tokenizer.flags(f) & doubled) === 0 ? text : text.replaceAll('""', '"'));
        }
        result.push(fields);
        line += 1 + tokenizer.breaks;
        pos = tokenizer.next;
    }
    return result;
}

/** Whether the first record is a header row: it is, unless every column whose other values are numbers, dates
 * or Booleans reads its first value as one of them too. */
function headerRow(records: readonly (readonly string[])[], separator: CsvSeparator, culture: string | undefined, marks: CsvMark[],
    proposed: Map<number, { readonly proposal: Proposal; readonly marks: readonly CsvMark[] }>): boolean {
    const [first] = records;
    if (first === undefined || records.length < 2) {
        marks.push(mark('header', 'The sample holds one record at most, which is taken as the header row.'));
        return true;
    }
    const rest = records.slice(1);
    let typedColumns = 0;
    let fitting = 0;
    for (let i = 0; i < first.length; i++) {
        const values = columnOf(rest, i);
        const columnMarks: CsvMark[] = [];
        const proposal = propose(values, separator, culture, columnMarks);
        proposed.set(i, { proposal, marks: columnMarks });
        if (proposal.kind === 'text') continue;
        typedColumns++;
        const head = elementAt(first, i);
        if (head.length === 0 || propose([{ text: head, row: 0 }, ...values], separator, culture, []).kind !== 'text') fitting++;
    }
    if (typedColumns === 0) {
        marks.push(mark('header', 'Every column holds text, so whether the first record is a header row cannot be told; it is taken as one.'));
        return true;
    }
    return fitting < typedColumns;
}

/** The kind and reading suggested for a column's non-empty values, and its marks. */
function propose(values: readonly Value[], separator: CsvSeparator, culture: string | undefined, marks: CsvMark[]): Proposal {
    if (values.length === 0) {
        marks.push(mark('empty', 'The column is empty in every sampled row; it is suggested as Text.'));
        return { kind: 'text' };
    }
    const blanks = [...new Set(values.filter(value => isBlankWord(value.text)).map(value => value.text))];
    const rest = blanks.length === 0 ? values : values.filter(value => !isBlankWord(value.text));
    if (rest.length === 0) {
        marks.push(mark('empty', `The column holds nothing but ${listed(blanks)} in the sampled rows; it is suggested as Text.`));
        return { kind: 'text' };
    }

    const proposal = booleans(rest) ?? numbers(rest, separator, culture, marks) ?? dates(rest, culture, marks);
    if (proposal === undefined) {
        const spellings = distinctIgnoringCase(rest.map(value => trimSpaces(value.text)));
        if (spellings.every(spelling => ['YES', 'NO', 'Y', 'N'].some(word => sameIgnoringCase(spelling, word)))) {
            marks.push(mark('couldBeBoolean', `The column holds only ${listed(spellings)}; it could be a Boolean, with those spellings declared.`));
        } else {
            mixed(rest, separator, culture, marks);
        }
        return { kind: 'text' };
    }
    if (proposal.kind !== 'text' && blanks.length > 0) {
        const one = blanks.length === 1;
        marks.push(mark('blankText', `${listed(blanks)} ${one ? 'stands' : 'stand'} among the ${described(proposal.kind)} and `
            + `${one ? 'is' : 'are'} read as ${one ? 'a Blank' : 'Blanks'}.`));
        return { ...proposal, blankText: Object.freeze(blanks) };
    }
    return proposal;
}

function booleans(values: readonly Value[]): Proposal | undefined {
    return values.every(value => isTrueOrFalse(trimSpaces(value.text))) ? { kind: 'boolean' } : undefined;
}

function isTrueOrFalse(text: string): boolean {
    return sameIgnoringCase(text, 'TRUE') || sameIgnoringCase(text, 'FALSE');
}

/** The simplest reading of a number under which every value reads, or none. */
function numbers(values: readonly Value[], separator: CsvSeparator, culture: string | undefined, marks: CsvMark[]): Proposal | undefined {
    const texts = values.map(value => trimSpaces(value.text));
    const passing: { readonly reading: number; readonly values: readonly DecimalValue[]; readonly anyPoint: boolean }[] = [];
    let tooLong: number[] | undefined;
    for (let r = 0; r < readings.length; r++) {
        const reading = elementAt(readings, r);
        const read: DecimalValue[] = [];
        let anyPoint = false;
        let isNumber = true;
        const longOnes: number[] = [];
        for (let i = 0; i < texts.length && isNumber; i++) {
            const parsed = parseNumber(elementAt(texts, i), reading);
            if (parsed.status === 'notANumber') {
                isNumber = false;
            } else if (parsed.status === 'tooLong' || parsed.scale > maxDecimalScale || (parsed.digits !== '' && BigInt(parsed.digits) >= decimalLimit)) {
                longOnes.push(i);
            } else {
                read.push({ negative: parsed.negative, digits: parsed.digits, scale: parsed.scale });
                anyPoint ||= parsed.hasPoint;
            }
        }
        if (!isNumber) continue;
        if (longOnes.length > 0) {
            tooLong ??= longOnes;
            continue;
        }
        passing.push({ reading: r, values: read, anyPoint });
    }

    const [first] = passing;
    if (first === undefined) {
        if (tooLong === undefined) return floating(texts);
        const longOnes = tooLong;
        marks.push(mark('tooLong', `${named(longOnes.map(i => elementAt(values, i)))} ${longOnes.length === 1 ? 'has' : 'have'} more digits than a `
            + 'Decimal holds; the column is suggested as Text.'));
        return { kind: 'text' };
    }

    // Readings that read every value alike are one reading. When two read some value apart, the culture
    // decides which is suggested, and the column is marked.
    let chosen = first;
    if (passing.some(each => !sameNumbers(each.values, first.values))) {
        const comma = separator === 'semicolon' || numberCulture(culture).decimalPoint === ',';
        chosen = passing.find(each => pointOf(each.reading) === (comma ? ',' : '.')) ?? first;
        const picked = chosen;
        const apart = values.findIndex((_, i) => passing.some(each => !sameNumber(elementAt(each.values, i), elementAt(picked.values, i))));
        const otherwise = passing.find(each => !sameNumber(elementAt(each.values, apart), elementAt(picked.values, apart)));
        if (otherwise === undefined) throw new Error('Internal: no reading reads the value apart.');
        const value = elementAt(values, apart);
        const point = pointOf(chosen.reading);
        marks.push(mark('ambiguousSeparator', `'${value.text}' (row ${value.row}) reads as ${decimalText(elementAt(chosen.values, apart))} with '${point}' as the `
            + `decimal point, and as ${decimalText(elementAt(otherwise.values, apart))} with '${pointOf(otherwise.reading)}'; '${point}' is suggested.`));
    }
    const [point, thousands] = elementAt(readingTexts, chosen.reading);
    const both = values.find(value => value.text.includes(',') && value.text.includes('.'));
    if (both !== undefined) {
        marks.push(mark('bothSeparators', `'${both.text}' (row ${both.row}) is written with both ',' and '.'; '${point}' is read as the decimal point `
            + `and '${thousands}' as the thousands separator.`));
    }

    const zeros = values.filter(value => leadingZero(value.text, point, thousands));
    if (zeros.length > 0) {
        marks.push(mark('leadingZeros', `${named(zeros)} ${zeros.length === 1 ? 'has a leading zero' : 'have leading zeros'}, so the column could be an `
            + 'identifier rather than a number; it is suggested as Text, kept exactly.'));
        return { kind: 'text' };
    }

    const integer = !chosen.anyPoint && chosen.values.every(fitsInt64);
    if (integer && chosen.values.every(value => isZero(value) || isOne(value)) && chosen.values.some(isZero) && chosen.values.some(isOne)) {
        marks.push(mark('couldBeBoolean', 'The column holds only 0 and 1; it could be a Boolean, with those spellings declared.'));
    }
    return {
        kind: integer ? 'integer' : 'decimal',
        ...(point === '.' ? {} : { point }),
        ...(thousands === '' ? {} : { thousands }),
    };
}

function pointOf(reading: number): string {
    return elementAt(elementAt(readingTexts, reading), 0);
}

function isZero(value: DecimalValue): boolean {
    return value.digits === '';
}

function isOne(value: DecimalValue): boolean {
    return !value.negative && value.digits === '1' && value.scale === 0;
}

function fitsInt64(value: DecimalValue): boolean {
    if (value.digits === '') return true;
    const magnitude = BigInt(value.digits);
    const whole = value.negative ? -magnitude : magnitude;
    return value.scale === 0 && whole >= int64Min && whole <= int64Max;
}

/** Whether two Decimals are equal, as C# compares them: by value, whatever their sign when zero. */
function sameNumber(a: DecimalValue, b: DecimalValue): boolean {
    if (a.digits === '' || b.digits === '') return a.digits === b.digits;
    return a.negative === b.negative && a.digits === b.digits && a.scale === b.scale;
}

function sameNumbers(a: readonly DecimalValue[], b: readonly DecimalValue[]): boolean {
    return a.every((value, i) => sameNumber(value, elementAt(b, i)));
}

/** A Decimal as .NET's invariant culture writes it, at its scale. */
function decimalText(value: DecimalValue): string {
    if (value.digits === '') return '0';
    const digits = value.digits.padStart(value.scale + 1, '0');
    const whole = value.scale === 0 ? digits : `${digits.slice(0, -value.scale)}.${digits.slice(-value.scale)}`;
    return value.negative ? `-${whole}` : whole;
}

/** Numbers that read only as floating point — with an exponent, or NaN and Infinity — as a Double. */
function floating(texts: readonly string[]): Proposal | undefined {
    for (let r = 0; r < 2; r++) {
        const reading = elementAt(readings, r);
        const all = texts.every(text => {
            const normal = normalizeDouble(text, reading);
            if (normal !== undefined) return Number.isFinite(Number(normal));
            // .NET reads the text as it stands, which past the column's separators is a number only when it
            // is NaN, an infinity, or written as the invariant culture writes one and too large for a double.
            const symbol = nonFinite(text) ?? plainNumber(text, elementAt(readings, 0));
            return symbol !== undefined && !Number.isFinite(symbol);
        });
        if (all) return r === 0 ? { kind: 'double' } : { kind: 'double', point: ',' };
    }
    return undefined;
}

/** The fewest formats under which every value reads as a date, or none. */
function dates(values: readonly Value[], culture: string | undefined, marks: CsvMark[]): Proposal | undefined {
    const trimmed = values.map(value => trimSpaces(value.text));
    const ticks = dateFormats.map(format => trimmed.map(text => format.read(text)));
    const whole = ticks.flatMap((read, f) => (read.every(t => t !== undefined) ? [f] : []));
    const [firstWhole] = whole;
    if (firstWhole !== undefined) {
        let chosen = firstWhole;
        const ticksOf = (f: number): readonly (bigint | undefined)[] => elementAt(ticks, f);
        if (whole.some(f => ticksOf(f).some((t, i) => t !== ticksOf(firstWhole)[i]))) {
            const first = dayFirst(culture) ? 'd' : 'M';
            chosen = whole.find(f => elementAt(dateTexts, f).startsWith(first)) ?? firstWhole;
            const picked = chosen;
            const apart = values.findIndex((_, i) => whole.some(f => ticksOf(f)[i] !== ticksOf(picked)[i]));
            const value = elementAt(values, apart);
            marks.push(mark('dayOrMonth', `'${value.text}' (row ${value.row}) could have its day or its month first; '${elementAt(dateTexts, chosen)}' is suggested.`));
        }
        return { kind: 'date', dateFormats: Object.freeze([elementAt(dateTexts, chosen)]) };
    }

    const uncovered = new Set(values.keys());
    const cover: number[] = [];
    while (uncovered.size > 0) {
        let best = 0;
        let count = -1;
        for (let f = 0; f < dateFormats.length; f++) {
            const read = elementAt(ticks, f);
            let covered = 0;
            for (const i of uncovered) {
                if (read[i] !== undefined) covered++;
            }
            if (covered > count) {
                best = f;
                count = covered;
            }
        }
        if (count === 0) return undefined;
        cover.push(best);
        const read = elementAt(ticks, best);
        for (const i of uncovered) {
            if (read[i] !== undefined) uncovered.delete(i);
        }
    }
    const formats = cover.map(f => elementAt(dateTexts, f));
    const examples = cover.map(f => elementAt(values, elementAt(ticks, f).findIndex(t => t !== undefined)));
    marks.push(mark('mixedDateFormats', `The dates are written in ${formats.length} formats: `
        + `${formats.map((format, i) => `'${format}', as '${elementAt(examples, i).text}' (row ${elementAt(examples, i).row})`).join(', ')}; all of them are suggested.`));
    if (formats.some(format => format.startsWith('d')) && formats.some(format => format.startsWith('M'))) {
        marks.push(mark('dayOrMonth', 'Some dates have the day first and others the month first; which is which cannot be told.'));
    }
    return { kind: 'date', dateFormats: Object.freeze(formats) };
}

/** Marks a column suggested as Text whose values are, all but a few, numbers or dates. */
function mixed(values: readonly Value[], separator: CsvSeparator, culture: string | undefined, marks: CsvMark[]): void {
    if (values.length < 10) return;
    const typed: Value[] = [];
    const misfits: Value[] = [];
    for (const value of values) (isTyped(value.text) ? typed : misfits).push(value);
    if (misfits.length === 0 || misfits.length * 10 > values.length) return;
    const { kind } = propose(typed, separator, culture, []);
    if (kind === 'text') return;
    marks.push(mark('mixedKinds', `Most values are ${described(kind)}, but ${named(misfits)} ${misfits.length === 1 ? 'is' : 'are'} not; `
        + 'the column is suggested as Text.'));
}

/** Whether a field reads as a number, a date or a Boolean under some reading. */
function isTyped(text: string): boolean {
    const trimmed = trimSpaces(text);
    if (isTrueOrFalse(trimmed)) return true;
    if (readings.some(reading => parseNumber(trimmed, reading).status === 'ok')) return true;
    return dateFormats.some(format => format.read(trimmed) !== undefined);
}

function columnOf(records: readonly (readonly string[])[], index: number): Value[] {
    const values: Value[] = [];
    records.forEach((record, r) => {
        const text = record[index];
        if (text !== undefined && text.length > 0) values.push({ text, row: r + 1 });
    });
    return values;
}

/** Whether the digits before the decimal point begin with a zero that is not the only digit. */
function leadingZero(text: string, point: string, thousands: string): boolean {
    const digits = trimSpaces(text).replace(/^[-+]+/, '');
    const end = digits.indexOf(point);
    let whole = end < 0 ? digits : digits.slice(0, end);
    if (thousands.length > 0) whole = whole.replaceAll(thousands, '');
    return whole.length >= 2 && whole.startsWith('0');
}

function isBlankWord(text: string): boolean {
    const trimmed = trimSpaces(text);
    return blankWords.some(word => sameIgnoringCase(word, trimmed));
}

/** The texts without repeats, ignoring case, each as first written. */
function distinctIgnoringCase(texts: readonly string[]): string[] {
    const seen = new Set<string>();
    return texts.filter(text => {
        const folded = foldCase(text);
        if (seen.has(folded)) return false;
        seen.add(folded);
        return true;
    });
}

/** The most frequent count, the larger on a tie; 0 for none. */
function modal(counts: readonly number[]): number {
    const tally = new Map<number, number>();
    for (const count of counts) {
        const times = tally.get(count);
        tally.set(count, times === undefined ? 1 : times + 1);
    }
    let best = 0;
    let most = 0;
    for (const [count, times] of tally) {
        if (times > most || (times === most && count > best)) {
            best = count;
            most = times;
        }
    }
    return best;
}

/** The separator as the character it is. */
function character(separator: CsvSeparator): string {
    return String.fromCharCode(separatorCode(separator));
}

function shown(separator: CsvSeparator): string {
    switch (separator) {
        case 'comma': return 'a comma';
        case 'tab': return 'a tab';
        case 'semicolon': return 'a semicolon';
        default: return separator satisfies never;
    }
}

function described(kind: SnapshotKind): string {
    switch (kind) {
        case 'decimal':
        case 'double':
        case 'integer': return 'numbers';
        case 'date': return 'dates';
        case 'boolean': return 'true or false';
        case 'text': return 'text';
        default: return kind satisfies never;
    }
}

function unique(names: Set<string>, name: string): string {
    let candidate = name;
    for (let n = 2; names.has(candidate); n++) candidate = `${name}_${n}`;
    names.add(candidate);
    return candidate;
}

function mark(doubt: CsvDoubt, note: string): CsvMark {
    return Object.freeze({ doubt, note });
}

/** Items joined with commas and a final "and". */
function joinedAnd(items: readonly string[]): string {
    return items.length <= 1 ? items.join('') : `${items.slice(0, -1).join(', ')} and ${elementAt(items, items.length - 1)}`;
}

function listed(texts: readonly string[]): string {
    return joinedAnd(texts.map(text => `'${text}'`));
}

/** A few values with their rows, for a mark: `'00123' (row 3) and '007' (row 9)`. */
function named(values: readonly Value[]): string {
    const shownValues = values.slice(0, maxNamed).map(value => `'${value.text}' (row ${value.row})`);
    if (values.length > maxNamed) shownValues.push(`${grouped(values.length - maxNamed)} more`);
    return joinedAnd(shownValues);
}

function rowsNamed(rows: readonly number[]): string {
    const shownRows = rows.slice(0, maxNamed).map(String);
    if (rows.length > maxNamed) shownRows.push(`${grouped(rows.length - maxNamed)} more`);
    return (rows.length === 1 ? 'Row ' : 'Rows ') + joinedAnd(shownRows);
}
