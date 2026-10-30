// An Arrow schema as its flatbuffer says it (ADR-0065): each field's name, type and metadata, and a type's
// name as Arrow's own tools print it — `timestamp[ns, tz=Asia/Tokyo]`, `dictionary<values=utf8,
// indices=int32>` — for a refusal to name, spelt as ExGrid.Data.Arrow's ArrowTypeNames spells it.

import { FlatTable } from './flat';
import { unreadable } from './ipc';
import { unreachable } from '../must';

/** A time unit, as Arrow's TimeUnit numbers it. */
export type TimeUnit = 's' | 'ms' | 'us' | 'ns';

/** The integer types, by bit width and sign. */
export interface IntType {
    readonly id: 'int';
    readonly bitWidth: 8 | 16 | 32 | 64;
    readonly signed: boolean;
}

/** The types a Snapshot reads (ADR-0065's table), and every other by the name a refusal gives it. */
export type ArrowType =
    | IntType
    | { readonly id: 'float'; readonly precision: 'half' | 'single' | 'double' }
    | { readonly id: 'utf8' | 'large_utf8' | 'utf8_view' | 'bool' }
    | { readonly id: 'decimal'; readonly bitWidth: 32 | 64 | 128 | 256; readonly precision: number; readonly scale: number }
    | { readonly id: 'date'; readonly unit: 'day' | 'ms' }
    | { readonly id: 'time'; readonly bitWidth: 32 | 64; readonly unit: TimeUnit }
    | { readonly id: 'timestamp'; readonly unit: TimeUnit; readonly zone: string | undefined }
    | { readonly id: 'other'; readonly name: string };

/** A field of a schema. */
export interface ArrowField {
    readonly name: string;
    readonly nullable: boolean;
    readonly type: ArrowType;
    /** A dictionary-encoded field's dictionary: its id and its indices' type. */
    readonly dictionary: { readonly id: bigint; readonly indexType: IntType } | undefined;
    readonly metadata: ReadonlyMap<string, string>;
    /** A nested type's fields; a type the Snapshot reads has none it reads. */
    readonly children: readonly ArrowField[];
}

/** A schema: its fields and its metadata. */
export interface ArrowSchema {
    readonly fields: readonly ArrowField[];
    readonly metadata: ReadonlyMap<string, string>;
}

/** The Schema table a message or a footer holds. */
export function readSchema(schema: FlatTable): ArrowSchema {
    // Schema: endianness (0), fields (1), custom_metadata (2), features (3).
    if (schema.int16(0, 0) !== 0) throw unreadable('it is big-endian, and only little-endian Arrow is read');
    const fields = schema.tables(1).map(readField);
    return { fields, metadata: readMetadata(schema.tables(2)) };
}

const unitNames: readonly TimeUnit[] = ['s', 'ms', 'us', 'ns'];

function timeUnit(value: number): TimeUnit {
    const unit = unitNames[value];
    if (unit === undefined) throw unreadable(`the time unit ${value} is not one of Arrow's`);
    return unit;
}

function readField(field: FlatTable): ArrowField {
    // Field: name (0), nullable (1), type_type (2), type (3), dictionary (4), children (5), custom_metadata (6).
    const name = field.string(0);
    const children = field.tables(5).map(readField);
    const type = readType(field.uint8(2, 0), field.table(3), children);
    const encoding = field.table(4);
    let dictionary: ArrowField['dictionary'] = undefined;
    if (encoding !== undefined) {
        // DictionaryEncoding: id (0), indexType (1), isOrdered (2); the indices are int32 when unsaid.
        const index = encoding.table(1);
        const indexType = index === undefined ? { id: 'int', bitWidth: 32, signed: true } as const : intType(index);
        dictionary = { id: encoding.int64(0, 0n), indexType };
    }
    // A field's name may be absent, which a Snapshot refuses as it refuses an empty one.
    return { name: name === undefined ? '' : name, nullable: field.bool(1, false), type, dictionary, metadata: readMetadata(field.tables(6)), children };
}

function readMetadata(pairs: readonly FlatTable[]): ReadonlyMap<string, string> {
    const metadata = new Map<string, string>();
    for (const pair of pairs) {
        // KeyValue: key (0), value (1).
        const key = pair.string(0);
        const value = pair.string(1);
        if (key === undefined) throw unreadable('a metadata entry has no key');
        if (value === undefined) throw unreadable(`its metadata key '${key}' has no value`);
        if (metadata.has(key)) throw unreadable(`its metadata holds the key '${key}' twice`);
        metadata.set(key, value);
    }
    return metadata;
}

function intType(table: FlatTable): IntType {
    // Int: bitWidth (0), is_signed (1).
    const bitWidth = table.int32(0, 0);
    if (bitWidth !== 8 && bitWidth !== 16 && bitWidth !== 32 && bitWidth !== 64) throw unreadable(`an integer type is ${bitWidth} bits wide`);
    return { id: 'int', bitWidth, signed: table.bool(1, false) };
}

function needed(table: FlatTable | undefined, what: string): FlatTable {
    if (table === undefined) throw unreadable(`a field of type ${what} does not say its parameters`);
    return table;
}

function readType(typeId: number, table: FlatTable | undefined, children: readonly ArrowField[]): ArrowType {
    const child = (index: number): string => {
        const found = children[index];
        if (found === undefined) throw unreadable('a nested type has fewer children than it needs');
        return fieldTypeName(found);
    };
    switch (typeId) {
        case 1: return { id: 'other', name: 'null' };
        case 2: return intType(needed(table, 'Int'));
        case 3: {
            // FloatingPoint: precision (0), HALF 0, SINGLE 1, DOUBLE 2.
            const precision = needed(table, 'FloatingPoint').int16(0, 0);
            if (precision === 0) return { id: 'float', precision: 'half' };
            if (precision === 1) return { id: 'float', precision: 'single' };
            if (precision === 2) return { id: 'float', precision: 'double' };
            throw unreadable(`the floating-point precision ${precision} is not one of Arrow's`);
        }
        case 4: return { id: 'other', name: 'binary' };
        case 5: return { id: 'utf8' };
        case 6: return { id: 'bool' };
        case 7: {
            // Decimal: precision (0), scale (1), bitWidth (2), 128 when unsaid.
            const decimal = needed(table, 'Decimal');
            const bitWidth = decimal.int32(2, 128);
            if (bitWidth !== 32 && bitWidth !== 64 && bitWidth !== 128 && bitWidth !== 256) throw unreadable(`a decimal type is ${bitWidth} bits wide`);
            return { id: 'decimal', bitWidth, precision: decimal.int32(0, 0), scale: decimal.int32(1, 0) };
        }
        case 8: {
            // Date: unit (0), DAY 0, MILLISECOND 1, which is the default.
            const unit = table === undefined ? 1 : table.int16(0, 1);
            if (unit === 0) return { id: 'date', unit: 'day' };
            if (unit === 1) return { id: 'date', unit: 'ms' };
            throw unreadable(`the date unit ${unit} is not one of Arrow's`);
        }
        case 9: {
            // Time: unit (0), MILLISECOND when unsaid; bitWidth (1), 32 when unsaid.
            const unit = timeUnit(table === undefined ? 1 : table.int16(0, 1));
            const bitWidth = table === undefined ? 32 : table.int32(1, 32);
            if (bitWidth !== 32 && bitWidth !== 64) throw unreadable(`a time type is ${bitWidth} bits wide`);
            // Arrow defines time32 in seconds or milliseconds, and time64 in microseconds or nanoseconds.
            if ((bitWidth === 32) !== (unit === 's' || unit === 'ms')) throw unreadable(`a time${bitWidth} type is in ${unit}, which Arrow does not define`);
            return { id: 'time', bitWidth, unit };
        }
        case 10: {
            // Timestamp: unit (0), SECOND when unsaid; timezone (1).
            const unit = timeUnit(table === undefined ? 0 : table.int16(0, 0));
            return { id: 'timestamp', unit, zone: table?.string(1) };
        }
        case 11: {
            // Interval: unit (0), YEAR_MONTH 0, DAY_TIME 1, MONTH_DAY_NANO 2.
            const unit = table === undefined ? 0 : table.int16(0, 0);
            const names = ['month_interval', 'day_time_interval', 'month_day_nano_interval'];
            const name = names[unit];
            if (name === undefined) throw unreadable(`the interval unit ${unit} is not one of Arrow's`);
            return { id: 'other', name };
        }
        case 12: return { id: 'other', name: `list<${child(0)}>` };
        case 13: return { id: 'other', name: `struct<${children.map(field => `${field.name}: ${fieldTypeName(field)}`).join(', ')}>` };
        case 14: return { id: 'other', name: 'union' };
        case 15: return { id: 'other', name: `fixed_size_binary[${needed(table, 'FixedSizeBinary').int32(0, 0)}]` };
        case 16: return { id: 'other', name: `fixed_size_list<${child(0)}>[${needed(table, 'FixedSizeList').int32(0, 0)}]` };
        case 17: {
            // A map's child is its entries, a struct of the key and the value.
            const key = children[0]?.children[0];
            const value = children[0]?.children[1];
            if (key === undefined || value === undefined) throw unreadable('a map type does not hold its key and value');
            return { id: 'other', name: `map<${fieldTypeName(key)}, ${fieldTypeName(value)}>` };
        }
        case 18: return { id: 'other', name: `duration[${timeUnit(table === undefined ? 1 : table.int16(0, 1))}]` };
        case 19: return { id: 'other', name: 'large_binary' };
        case 20: return { id: 'large_utf8' };
        case 21: return { id: 'other', name: `large_list<${child(0)}>` };
        case 22: return { id: 'other', name: `run_end_encoded<${child(0)}, ${child(1)}>` };
        case 23: return { id: 'other', name: 'binary_view' };
        case 24: return { id: 'utf8_view' };
        case 25: return { id: 'other', name: `list_view<${child(0)}>` };
        case 26: return { id: 'other', name: `large_list_view<${child(0)}>` };
        default: throw unreadable(`the type ${typeId} is not one of Arrow's`);
    }
}

/** A type's name, as a refusal gives it. */
export function typeName(type: ArrowType): string {
    switch (type.id) {
        case 'int': return `${type.signed ? 'int' : 'uint'}${type.bitWidth}`;
        case 'float': return type.precision === 'half' ? 'float16' : type.precision === 'single' ? 'float32' : 'float64';
        case 'utf8':
        case 'large_utf8':
        case 'utf8_view':
        case 'bool':
            return type.id;
        case 'decimal': return `decimal${type.bitWidth}(${type.precision}, ${type.scale})`;
        case 'date': return type.unit === 'day' ? 'date32' : 'date64';
        case 'time': return `time${type.bitWidth}[${type.unit}]`;
        case 'timestamp': return type.zone === undefined || type.zone.trim() === '' ? `timestamp[${type.unit}]` : `timestamp[${type.unit}, tz=${type.zone}]`;
        case 'other': return type.name;
        default: return unreachable(type);
    }
}

/** A field's type name, a dictionary-encoded one's naming its values and its indices. */
export function fieldTypeName(field: ArrowField): string {
    return field.dictionary === undefined
        ? typeName(field.type)
        : `dictionary<values=${typeName(field.type)}, indices=${typeName(field.dictionary.indexType)}>`;
}
