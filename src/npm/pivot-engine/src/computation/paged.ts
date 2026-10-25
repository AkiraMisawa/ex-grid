// A dense array by id that the versions of a computation share a page at a time (ADR-0153): a version is a
// directory of pages, and an update copies the directory and each page it writes, never one a published
// version reads. It reaches no earlier version, so letting one go keeps no history alive.

const pageBits = 8;

/** How many entries a page holds. */
export const pageSize = 1 << pageBits;

const pageMask = pageSize - 1;

type Page<T> = readonly (T | undefined)[];

/** A published version: read only. */
export class PagedArray<T> {
    readonly #pages: readonly (Page<T> | undefined)[];

    private constructor(pages: readonly (Page<T> | undefined)[]) {
        this.#pages = pages;
    }

    /** No entries. */
    static empty<T>(): PagedArray<T> {
        return new PagedArray<T>([]);
    }

    /** The entry at an id, or `undefined` for none. */
    get(id: number): T | undefined {
        return this.#pages[id >>> pageBits]?.[id & pageMask];
    }

    /** A writer of the next version, which starts as this one. */
    writer(): PagedWriter<T> {
        return new PagedWriter(this.#pages, pages => new PagedArray(pages));
    }
}

/** The next version, written: a page is copied the first time it is written. */
export class PagedWriter<T> {
    readonly #pages: (Page<T> | undefined)[];
    // The pages this writer copied: its own until it publishes.
    readonly #written = new Map<number, (T | undefined)[]>();
    readonly #publish: (pages: readonly (Page<T> | undefined)[]) => PagedArray<T>;

    constructor(pages: readonly (Page<T> | undefined)[], publish: (pages: readonly (Page<T> | undefined)[]) => PagedArray<T>) {
        this.#pages = [...pages];
        this.#publish = publish;
    }

    /** The entry at an id, as written so far. */
    get(id: number): T | undefined {
        return this.#pages[id >>> pageBits]?.[id & pageMask];
    }

    /** Writes the entry at an id; `undefined` takes it away. */
    set(id: number, value: T | undefined): void {
        const number = id >>> pageBits;
        let page = this.#written.get(number);
        if (page === undefined) {
            const held = this.#pages[number];
            page = held === undefined ? Array.from<T | undefined>({ length: pageSize }) : [...held];
            this.#written.set(number, page);
            this.#pages[number] = page;
        }
        page[id & pageMask] = value;
    }

    /** The version written, published: its pages frozen, and this writer no longer used. */
    freeze(): PagedArray<T> {
        for (const page of this.#written.values()) Object.freeze(page);
        return this.#publish(Object.freeze([...this.#pages]));
    }
}
