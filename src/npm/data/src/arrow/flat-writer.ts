// Flatbuffers, as Arrow's IPC metadata is written in them (ADR-0065). A table is laid out with its vtable just
// before it and everything it points to after it, so every offset points forward as the format asks; every
// scalar lies at a multiple of its width, and every field is written, a default included, so a reader never
// has to know a default to read what was meant.

import { unreachable } from '../must';

/** A field of a table, by the type its schema gives it. */
export type FlatField =
    | { readonly type: 'uint8' | 'int16' | 'int32'; readonly value: number }
    | { readonly type: 'bool'; readonly value: boolean }
    | { readonly type: 'int64'; readonly value: bigint }
    | { readonly type: 'table'; readonly value: FlatTableSpec }
    | { readonly type: 'string'; readonly value: string }
    | { readonly type: 'tables'; readonly value: readonly FlatTableSpec[] }
    /** A vector of structs eight bytes aligned, as Arrow's FieldNode, Buffer and Block are: their bytes and their count. */
    | { readonly type: 'structs'; readonly value: Uint8Array; readonly count: number };

/** A table: its fields by id, a field left out where the table has none. */
export type FlatTableSpec = readonly (FlatField | undefined)[];

const encoder = new TextEncoder();

function inlineWidth(field: FlatField): number {
    switch (field.type) {
        case 'uint8':
        case 'bool':
            return 1;
        case 'int16': return 2;
        case 'int64': return 8;
        case 'int32':
        case 'table':
        case 'string':
        case 'tables':
        case 'structs':
            return 4;
        default: return unreachable(field);
    }
}

function align(position: number, to: number): number {
    return Math.ceil(position / to) * to;
}

/** Writes one flatbuffer, its root table first. */
export class FlatWriter {
    #bytes = new Uint8Array(512);
    #view = new DataView(this.#bytes.buffer);
    #length = 0;

    /** The flatbuffer whose root is `root`, padded to eight bytes. */
    static write(root: FlatTableSpec): Uint8Array {
        const writer = new FlatWriter();
        writer.#reserve(4);
        writer.#length = 4;
        // The table is written first: writing it may grow the bytes, and the view with them.
        const table = writer.#table(root);
        writer.#view.setUint32(0, table, true);
        writer.#length = align(writer.#length, 8);
        writer.#reserve(0);
        return writer.#bytes.slice(0, writer.#length);
    }

    #reserve(more: number): void {
        const needed = this.#length + more + 8;
        if (needed <= this.#bytes.byteLength) return;
        const grown = new Uint8Array(Math.max(needed, this.#bytes.byteLength * 2));
        grown.set(this.#bytes);
        this.#bytes = grown;
        this.#view = new DataView(grown.buffer);
    }

    /** Writes a table at the end, and what it points to after it; answers where the table lies. */
    #table(fields: FlatTableSpec): number {
        const vtableSize = 4 + 2 * fields.length;
        const vtable = align(this.#length, 2);
        let table = align(vtable + vtableSize, 4);
        const present = fields.flatMap((field, id) => (field === undefined ? [] : [{ field, id, width: inlineWidth(field) }]))
            .toSorted((a, b) => (a.width === b.width ? a.id - b.id : b.width - a.width));
        // The soffset takes four bytes; an eight-byte field after it lies at a multiple of eight.
        if (present.some(each => each.width === 8) && (table + 4) % 8 !== 0) table += 4;
        let at = table + 4;
        const placed = present.map(each => {
            const position = at;
            at += each.width;
            return { ...each, position };
        });
        const objectSize = at - table;
        this.#reserve(at - this.#length);
        this.#bytes.fill(0, this.#length, at);
        this.#view.setUint16(vtable, vtableSize, true);
        this.#view.setUint16(vtable + 2, objectSize, true);
        for (const { id, position } of placed) this.#view.setUint16(vtable + 4 + 2 * id, position - table, true);
        this.#view.setInt32(table, table - vtable, true);
        this.#length = at;
        for (const { field, position } of placed) this.#inline(field, position);
        for (const { field, position } of placed) {
            const child = this.#child(field);
            if (child !== undefined) this.#view.setUint32(position, child - position, true);
        }
        return table;
    }

    #inline(field: FlatField, position: number): void {
        switch (field.type) {
            case 'uint8': this.#view.setUint8(position, field.value); break;
            case 'bool': this.#view.setUint8(position, field.value ? 1 : 0); break;
            case 'int16': this.#view.setInt16(position, field.value, true); break;
            case 'int32': this.#view.setInt32(position, field.value, true); break;
            case 'int64': this.#view.setBigInt64(position, field.value, true); break;
            case 'table':
            case 'string':
            case 'tables':
            case 'structs':
                break;
            default: unreachable(field);
        }
    }

    /** Writes what a field points to, and answers where it lies; `undefined` for a scalar. */
    #child(field: FlatField): number | undefined {
        switch (field.type) {
            case 'uint8':
            case 'bool':
            case 'int16':
            case 'int32':
            case 'int64':
                return undefined;
            case 'table': return this.#table(field.value);
            case 'string': {
                const bytes = encoder.encode(field.value);
                const position = this.#vector(bytes.byteLength, 4);
                this.#reserve(bytes.byteLength + 1);
                this.#bytes.set(bytes, this.#length);
                this.#bytes[this.#length + bytes.byteLength] = 0;
                this.#length += bytes.byteLength + 1;
                return position;
            }
            case 'tables': {
                const position = this.#vector(field.value.length, 4);
                const slots = this.#length;
                this.#reserve(4 * field.value.length);
                this.#length += 4 * field.value.length;
                field.value.forEach((table, i) => {
                    const slot = slots + 4 * i;
                    const child = this.#table(table);
                    this.#view.setUint32(slot, child - slot, true);
                });
                return position;
            }
            case 'structs': {
                const position = this.#vector(field.count, 8);
                this.#reserve(field.value.byteLength);
                this.#bytes.set(field.value, this.#length);
                this.#length += field.value.byteLength;
                return position;
            }
            default: return unreachable(field);
        }
    }

    /** Writes a vector's length where its elements, which follow it, lie at a multiple of `elementAlign`. */
    #vector(count: number, elementAlign: number): number {
        let position = align(this.#length, 4);
        if ((position + 4) % elementAlign !== 0) position += 4;
        this.#reserve(position + 4 - this.#length);
        this.#bytes.fill(0, this.#length, position);
        this.#view.setUint32(position, count, true);
        this.#length = position + 4;
        return position;
    }
}
