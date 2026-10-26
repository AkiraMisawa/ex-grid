// How a delimited text file is read into a Snapshot (ADR-0064, the second way in), as ExGrid.Data's CsvSchema
// declares it: each column's header, kind and reading, and the file's encoding, separator and header row. It
// is declared, never guessed. A Schema that contradicts itself is the Consumer's mistake, not the file's, so
// `csvPlan` refuses it with a `TypeError` before a byte is read, in CsvPlan's words.

import type { SnapshotKind } from '../snapshot';
import { calendarRefusal, localeRefusal, numberCulture } from './culture';
import { DateFormat } from './date-text';
import { foldCase } from '../text-casing';

/** The character between a record's fields: those Excel writes, in every locale it writes them. */
export type CsvSeparator = 'comma' | 'tab' | 'semicolon';

/** The encoding of a file that does not begin with UTF-8's byte-order mark. Text is decoded strictly: bytes
 * not valid in it are refused by row and column, never read as replacement characters. */
export type CsvEncoding = 'utf-8' | 'shift-jis';

/** One column of a {@link CsvSchema}: which field of the file it is, the Snapshot column it becomes, and how
 * its text is read. An empty field is a Blank in every kind, quoted or not, as Excel reads it; so is each
 * declared blank text. Text is kept exactly as written; the other kinds set the ASCII spaces around a value
 * aside. */
export interface CsvColumn {
    /** The Snapshot column's name, unique within the Schema; the header it matches unless `header` says another. */
    readonly name: string;
    /** The kind every value of the column is read as. */
    readonly kind: SnapshotKind;
    /** The header the column matches, exactly, when the file has a header row and the column declares no
     * `position`. A declared column missing from the header fails the read by name; a column of the file the
     * Schema does not declare is skipped. */
    readonly header?: string;
    /** The field the column is, counted from zero: how a column is matched in a file without a header row,
     * where it defaults to the column's place in the Schema. In a file with one, it is matched instead of the
     * header. */
    readonly position?: number;
    /** The Snapshot column's caption; its name when left out. */
    readonly caption?: string;
    /** The locale (`de-DE`) a number's decimal point, thousands separator and signs are read in, and a date's
     * names, separators and calendar; .NET's invariant culture when left out. `decimalPoint` and
     * `thousandsSeparator` win over it. A hyphen-minus is read as the negative sign in every culture. */
    readonly culture?: string;
    /** A number's decimal point: the culture's when left out, or `.` without one. */
    readonly decimalPoint?: string;
    /** A number's thousands separator: the culture's when left out, or none without one; `''` for none at all.
     * It is read only between groups of digits of the culture's sizes, so `1,5` is refused rather than read
     * as fifteen. */
    readonly thousandsSeparator?: string;
    /** The exact formats a Date column reads, tried in order, in .NET's custom format strings (`yyyy-MM-dd`,
     * `dd.MM.yyyy HH:mm`); {@link csvIsoDateFormats} when left out. A value with an offset (`zzz`, `K`) or
     * marked UTC (`Z`, `GMT`) is held as the clock it shows. A format that would take part of a date from
     * the day it is read is refused. */
    readonly dateFormats?: readonly string[];
    /** The spellings a Boolean column reads as true, matched ignoring case; {@link csvExcelTrue} when left out. */
    readonly trueText?: readonly string[];
    /** The spellings a Boolean column reads as false, matched ignoring case; {@link csvExcelFalse} when left out. */
    readonly falseText?: readonly string[];
    /** The texts that are a Blank in this column besides the empty field, matched exactly, quoted or not: the
     * Schema's `blankText` when left out; a declared list, the empty one included, replaces it. */
    readonly blankText?: readonly string[];
}

/** How a CSV is read into a Snapshot (ADR-0064): declared, never guessed. Quoting follows RFC 4180, and a
 * record ends at CR LF, LF or CR outside quotes. */
export interface CsvSchema {
    /** The columns to read, in the order the Snapshot holds them. A column of the file none matches is skipped. */
    readonly columns: readonly CsvColumn[];
    /** The character between fields; a comma when left out. */
    readonly separator?: CsvSeparator;
    /** Whether the file's first record is a header row naming its columns; true when left out. */
    readonly hasHeader?: boolean;
    /** The encoding of a file that does not begin with UTF-8's byte-order mark; UTF-8 when left out. */
    readonly encoding?: CsvEncoding;
    /** The texts that are a Blank, besides the empty field, in every column that declares none of its own. */
    readonly blankText?: readonly string[];
    /** The name of the Text or Integer column whose value tells each record from every other. A Blank key, and
     * a key carried by two records, fail the read. */
    readonly recordKey?: string;
}

/** The formats a Date column reads when it declares none: ISO 8601's date, or its date and time with a `T`
 * or a space between them and up to seven decimal places of a second, with no offset. */
export const csvIsoDateFormats: readonly string[] = Object.freeze([
    'yyyy-MM-dd',
    'yyyy-MM-ddTHH:mm:ss',
    'yyyy-MM-dd HH:mm:ss',
    'yyyy-MM-ddTHH:mm:ss.FFFFFFF',
    'yyyy-MM-dd HH:mm:ss.FFFFFFF',
]);

/** The spellings of true a Boolean column reads when it declares none: `TRUE`, as Excel writes it. */
export const csvExcelTrue: readonly string[] = Object.freeze(['TRUE']);

/** The spellings of false a Boolean column reads when it declares none: `FALSE`, as Excel writes it. */
export const csvExcelFalse: readonly string[] = Object.freeze(['FALSE']);

/** An encoding's name, as a refusal names it. */
export function encodingName(encoding: CsvEncoding): string {
    return encoding === 'utf-8' ? 'UTF-8' : 'Shift-JIS';
}

/** A column checked, with what it reads copied. */
export interface PlannedColumn {
    readonly name: string;
    readonly caption: string;
    readonly kind: SnapshotKind;
    /** The header it matches when it is matched by header. */
    readonly header: string;
    /** The field it is, when it is matched by position. */
    readonly position: number | undefined;
    readonly culture: string | undefined;
    readonly decimalPoint: string | undefined;
    readonly thousandsSeparator: string | undefined;
    readonly dateFormats: readonly DateFormat[];
    readonly trueText: readonly string[];
    readonly falseText: readonly string[];
    readonly blankText: readonly string[];
}

/** A Schema checked before any byte is read. */
export interface CsvPlan {
    readonly columns: readonly PlannedColumn[];
    /** The separator's character code. */
    readonly separator: number;
    readonly hasHeader: boolean;
    readonly encoding: CsvEncoding;
    /** The Record Key's place among the columns, or -1. */
    readonly keyOrdinal: number;
    readonly recordKey: string | undefined;
}

const kinds: readonly string[] = ['text', 'decimal', 'double', 'integer', 'date', 'boolean'];

const encodings: readonly string[] = ['utf-8', 'shift-jis'];

function wrong(message: string): TypeError {
    return new TypeError(message);
}

/** A list of texts the Consumer declared, copied, so that one changed while the file is read changes nothing. */
function texts(list: readonly string[] | undefined, what: string): readonly string[] | undefined {
    if (list === undefined) return undefined;
    if (!isList(list)) throw wrong(`${what} are not a list.`);
    const items: readonly unknown[] = list;
    const copy: string[] = [];
    for (const each of items) {
        if (typeof each !== 'string') throw wrong(`${what} hold something other than text.`);
        copy.push(each);
    }
    return Object.freeze(copy);
}

/** Whether a value an untyped caller passed is a list, without narrowing what the types already say it is. */
function isList(value: unknown): boolean {
    return Array.isArray(value);
}

/** The byte a separator is, a comma when none is declared. */
export function separatorCode(separator: CsvSeparator | undefined): number {
    switch (separator) {
        case undefined:
        case 'comma': return 0x2C;
        case 'tab': return 0x09;
        case 'semicolon': return 0x3B;
        default: throw wrong(`The separator '${String(separator satisfies never)}' is not one a CSV is read with: a comma, a tab or a semicolon.`);
    }
}

/** Checks `schema` and copies what it declares; a Schema that contradicts itself is refused with a `TypeError`. */
export function csvPlan(schema: CsvSchema): CsvPlan {
    if (!isList(schema.columns) || schema.columns.length === 0) throw wrong('A Schema declares at least one column.');
    const separator = separatorCode(schema.separator);
    const declaredHeader: unknown = schema.hasHeader;
    if (declaredHeader !== undefined && typeof declaredHeader !== 'boolean') throw wrong(`The Schema's hasHeader is ${JSON.stringify(declaredHeader)}, where it is true or false.`);
    const hasHeader = schema.hasHeader === undefined ? true : schema.hasHeader;
    const encoding = schema.encoding === undefined ? 'utf-8' : schema.encoding;
    if (!encodings.includes(encoding)) throw wrong(`The encoding '${encoding}' is not one a CSV is read in: 'utf-8' or 'shift-jis'.`);
    const schemaBlanks = texts(schema.blankText, "The Schema's blank texts");

    const names = new Set<string>();
    const headers = new Map<string, string>();
    const positions = new Map<number, string>();
    const columns = schema.columns.map((column, i): PlannedColumn => {
        if (typeof column.name !== 'string' || column.name === '') throw wrong(`The Schema's column ${i} has no name; every column has one.`);
        const { name } = column;
        if (names.has(name)) throw wrong(`A column named '${name}' is declared twice; a column's name is unique within a Snapshot.`);
        names.add(name);
        if (!kinds.includes(column.kind)) throw wrong(`Column '${name}' is declared with the kind '${column.kind}', which a Snapshot does not hold.`);
        if (column.header === '') throw wrong(`Column '${name}' matches an empty header; declare its Position instead.`);
        if (column.position !== undefined && (!Number.isInteger(column.position) || column.position < 0)) {
            throw wrong(`Column '${name}' is declared at position ${column.position}; a position is counted from zero.`);
        }
        const header = column.header === undefined ? name : column.header;
        const position = column.position === undefined ? (hasHeader ? undefined : i) : column.position;
        if (position === undefined) {
            const other = headers.get(header);
            if (other !== undefined) throw wrong(`Columns '${other}' and '${name}' both match the header '${header}'.`);
            headers.set(header, name);
        } else {
            const other = positions.get(position);
            if (other !== undefined) throw wrong(`Columns '${other}' and '${name}' are both declared at position ${position}.`);
            positions.set(position, name);
        }
        if (column.culture !== undefined) {
            const why = localeRefusal(column.culture);
            if (why !== undefined) throw wrong(`Column '${name}' reads in ${why}.`);
        }
        const own = texts(column.blankText, `Column '${name}''s blank texts`);
        const blankText = own === undefined ? (schemaBlanks === undefined ? Object.freeze([]) : schemaBlanks) : own;
        const planned = {
            name,
            caption: column.caption === undefined ? name : column.caption,
            kind: column.kind,
            header,
            position,
            culture: column.culture,
            decimalPoint: column.decimalPoint,
            thousandsSeparator: column.thousandsSeparator,
            dateFormats: Object.freeze([] as DateFormat[]),
            trueText: Object.freeze([] as string[]),
            falseText: Object.freeze([] as string[]),
            blankText,
        };
        switch (column.kind) {
            case 'decimal':
            case 'double':
            case 'integer':
                checkNumber(column);
                return Object.freeze(planned);
            case 'date':
                return Object.freeze({ ...planned, dateFormats: dateFormats(column) });
            case 'boolean':
                return Object.freeze({ ...planned, ...booleans(column, blankText) });
            case 'text':
                return Object.freeze(planned);
            default:
                throw wrong(`Column '${name}' is declared with the kind '${String(column.kind satisfies never)}', which a Snapshot does not hold.`);
        }
    });

    let keyOrdinal = -1;
    if (schema.recordKey !== undefined) {
        keyOrdinal = columns.findIndex(column => column.name === schema.recordKey);
        const key = columns[keyOrdinal];
        if (key === undefined) throw wrong(`The Record Key names '${schema.recordKey}', which is not a declared column.`);
        if (key.kind !== 'text' && key.kind !== 'integer') {
            throw wrong(`Only a Text or an Integer column can be the Record Key; '${key.name}' is ${kindName(key.kind)}.`);
        }
    }
    return Object.freeze({ columns: Object.freeze(columns), separator, hasHeader, encoding, keyOrdinal, recordKey: schema.recordKey });
}

/** A kind as C#'s SnapshotKind names it. */
export function kindName(kind: SnapshotKind): string {
    return kind.charAt(0).toUpperCase() + kind.slice(1);
}

/** The decimal point a column reads: its own, the culture's, or a full stop. */
export function pointOf(column: { readonly culture?: string | undefined; readonly decimalPoint?: string | undefined }): string {
    if (column.decimalPoint !== undefined) return column.decimalPoint;
    return column.culture === undefined ? '.' : numberCulture(column.culture).decimalPoint;
}

/** The thousands separator a column reads: its own, the culture's, or none. */
export function thousandsOf(column: { readonly culture?: string | undefined; readonly thousandsSeparator?: string | undefined }): string {
    if (column.thousandsSeparator !== undefined) return column.thousandsSeparator;
    return column.culture === undefined ? '' : numberCulture(column.culture).thousandsSeparator;
}

function checkNumber(column: CsvColumn): void {
    const point = pointOf(column);
    const thousands = thousandsOf(column);
    if (point === '') throw wrong(`Column '${column.name}' reads an empty decimal point.`);
    if (/[0-9+\- ]/.test(point)) throw wrong(`Column '${column.name}' reads '${point}' as its decimal point, which holds a digit, a sign or a space.`);
    if (/[0-9+-]/.test(thousands)) throw wrong(`Column '${column.name}' reads '${thousands}' as its thousands separator, which holds a digit or a sign.`);
    if (thousands !== '' && (point.startsWith(thousands) || thousands.startsWith(point))) {
        throw wrong(`Column '${column.name}' reads its decimal point and its thousands separator alike, as '${point}' and '${thousands}'.`);
    }
}

function dateFormats(column: CsvColumn): readonly DateFormat[] {
    const declared = texts(column.dateFormats, `Column '${column.name}''s date formats`);
    if (declared !== undefined && declared.length === 0) throw wrong(`Column '${column.name}' declares no date format.`);
    if (column.culture !== undefined) {
        const why = calendarRefusal(column.culture);
        if (why !== undefined) throw wrong(`Column '${column.name}' reads dates in '${column.culture}', which ${why}.`);
    }
    return Object.freeze((declared === undefined ? csvIsoDateFormats : declared).map(format => {
        if (format === '') throw wrong(`Column '${column.name}' declares an empty date format.`);
        const read = DateFormat.of(format, column.culture);
        if (typeof read === 'string') throw wrong(`Column '${column.name}' declares the date format '${format}', which cannot be read: ${read}`);
        const today = read.takenFromToday();
        if (today !== undefined) throw wrong(`Column '${column.name}' declares the date format '${format}': ${today}.`);
        return read;
    }));
}

/** Whether two spellings are one, ignoring case as .NET's OrdinalIgnoreCase does. */
export function sameIgnoringCase(a: string, b: string): boolean {
    return a.length === b.length && foldCase(a) === foldCase(b);
}

function booleans(column: CsvColumn, blanks: readonly string[]): { readonly trueText: readonly string[]; readonly falseText: readonly string[] } {
    const trues = texts(column.trueText, `Column '${column.name}''s spellings of true`);
    const falses = texts(column.falseText, `Column '${column.name}''s spellings of false`);
    const trueText = trues === undefined ? csvExcelTrue : trues;
    const falseText = falses === undefined ? csvExcelFalse : falses;
    if (trueText.length === 0 || falseText.length === 0) {
        throw wrong(`Column '${column.name}' declares no spelling of ${trueText.length === 0 ? 'true' : 'false'}.`);
    }
    if ([...trueText, ...falseText].includes('')) {
        throw wrong(`Column '${column.name}' declares an empty spelling of true or false; an empty field is a Blank.`);
    }
    for (const spelling of trueText) {
        if (falseText.some(other => sameIgnoringCase(spelling, other))) throw wrong(`Column '${column.name}' reads '${spelling}' as both true and false.`);
    }
    for (const blank of blanks) {
        if ([...trueText, ...falseText].some(other => sameIgnoringCase(blank, other))) {
            throw wrong(`Column '${column.name}' reads '${blank}' as both a Blank and a Boolean.`);
        }
    }
    return { trueText, falseText };
}
