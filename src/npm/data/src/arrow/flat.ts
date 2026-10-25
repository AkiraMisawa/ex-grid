// Flatbuffers, as Arrow's IPC metadata is written in them (ADR-0065), read with every offset checked against
// the bytes: a message that points outside itself is a malformed stream, said as one, never a read past
// its end. Only what Arrow's Message, Schema and Footer tables use is read.

/** A flatbuffer that points outside its bytes. The reader says what that means where it meets it: bytes
 * that are not Arrow, or a stream that is malformed. */
export class FlatError extends Error {
    constructor(what: string, options?: ErrorOptions) {
        super(what, options);
        this.name = 'FlatError';
    }
}

const utf8 = new TextDecoder('utf-8', { fatal: true, ignoreBOM: true });

/** A table of a flatbuffer, its fields read by their ids. */
export class FlatTable {
    readonly #view: DataView;
    readonly #length: number;
    readonly #table: number;
    readonly #vtable: number;
    readonly #vtableSize: number;

    private constructor(view: DataView, table: number, vtable: number, vtableSize: number) {
        this.#view = view;
        this.#length = view.byteLength;
        this.#table = table;
        this.#vtable = vtable;
        this.#vtableSize = vtableSize;
    }

    /** The root table of the flatbuffer `bytes` hold. */
    static root(bytes: Uint8Array): FlatTable {
        const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
        if (bytes.byteLength < 4) throw new FlatError('its metadata is shorter than a flatbuffer');
        return FlatTable.#at(view, view.getUint32(0, true));
    }

    static #at(view: DataView, table: number): FlatTable {
        const length = view.byteLength;
        if (table < 0 || table > length - 4) throw new FlatError('a table lies outside its metadata');
        const vtable = table - view.getInt32(table, true);
        if (vtable < 0 || vtable > length - 4) throw new FlatError('a table\'s vtable lies outside its metadata');
        const size = view.getUint16(vtable, true);
        if (size < 4 || vtable + size > length) throw new FlatError('a table\'s vtable runs past its metadata');
        return new FlatTable(view, table, vtable, size);
    }

    /** Where field `id`'s value lies, or -1 when the table leaves it at its default. */
    #field(id: number, width: number): number {
        const slot = 4 + 2 * id;
        if (slot + 2 > this.#vtableSize) return -1;
        const offset = this.#view.getUint16(this.#vtable + slot, true);
        if (offset === 0) return -1;
        const at = this.#table + offset;
        if (at > this.#length - width) throw new FlatError('a field lies outside its metadata');
        return at;
    }

    /** Whether the table holds field `id`, rather than leaving it at its default. */
    has(id: number): boolean {
        return this.#field(id, 1) >= 0;
    }

    uint8(id: number, fallback: number): number {
        const at = this.#field(id, 1);
        return at < 0 ? fallback : this.#view.getUint8(at);
    }

    bool(id: number, fallback: boolean): boolean {
        const at = this.#field(id, 1);
        return at < 0 ? fallback : this.#view.getUint8(at) !== 0;
    }

    int16(id: number, fallback: number): number {
        const at = this.#field(id, 2);
        return at < 0 ? fallback : this.#view.getInt16(at, true);
    }

    int32(id: number, fallback: number): number {
        const at = this.#field(id, 4);
        return at < 0 ? fallback : this.#view.getInt32(at, true);
    }

    int64(id: number, fallback: bigint): bigint {
        const at = this.#field(id, 8);
        return at < 0 ? fallback : this.#view.getBigInt64(at, true);
    }

    /** Field `id`'s table, or `undefined` when the table holds none. */
    table(id: number): FlatTable | undefined {
        const at = this.#field(id, 4);
        return at < 0 ? undefined : FlatTable.#at(this.#view, at + this.#view.getUint32(at, true));
    }

    /** Field `id`'s string, or `undefined` when the table holds none. */
    string(id: number): string | undefined {
        const vector = this.#vector(id, 1);
        if (vector === undefined) return undefined;
        const bytes = new Uint8Array(this.#view.buffer, this.#view.byteOffset + vector.start, vector.count);
        return decodeName(bytes);
    }

    /** Field `id`'s vector of tables, empty when the table holds none: a flatbuffer may leave an empty
     * vector out, and Arrow's tables read an absent one as empty. */
    tables(id: number): FlatTable[] {
        const vector = this.#vector(id, 4);
        if (vector === undefined) return [];
        const tables: FlatTable[] = [];
        for (let i = 0; i < vector.count; i++) {
            const at = vector.start + 4 * i;
            tables.push(FlatTable.#at(this.#view, at + this.#view.getUint32(at, true)));
        }
        return tables;
    }

    /** Field `id`'s vector of structs `size` bytes wide, as a view of them and their count, or `undefined`
     * when the table holds none. */
    structs(id: number, size: number): FlatStructs | undefined {
        const vector = this.#vector(id, size);
        if (vector === undefined) return undefined;
        return { view: new DataView(this.#view.buffer, this.#view.byteOffset + vector.start, vector.count * size), count: vector.count };
    }

    #vector(id: number, size: number): { start: number; count: number } | undefined {
        const at = this.#field(id, 4);
        if (at < 0) return undefined;
        const vector = at + this.#view.getUint32(at, true);
        if (vector > this.#length - 4) throw new FlatError('a vector lies outside its metadata');
        const count = this.#view.getUint32(vector, true);
        if (count * size > this.#length - vector - 4) throw new FlatError('a vector runs past its metadata');
        return { start: vector + 4, count };
    }
}

/** A vector of structs: a view of their bytes, and how many there are. */
export interface FlatStructs {
    readonly view: DataView;
    readonly count: number;
}

function decodeName(bytes: Uint8Array): string {
    try {
        return utf8.decode(bytes);
    } catch (error) {
        throw new FlatError('a string in its metadata is not valid UTF-8', { cause: error });
    }
}
