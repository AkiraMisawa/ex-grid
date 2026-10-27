// The workbook's shared strings, held whole: the one part read whole as text (ADR-0330 §9).

/** The workbook's shared strings, held whole, in the part's order, and which of them came in runs of their own
 * formatting. */
export class XlsxSharedStrings {
    /** A workbook with no shared strings. */
    static readonly empty: XlsxSharedStrings = new XlsxSharedStrings([], new Set());

    readonly #texts: readonly string[];
    readonly #rich: ReadonlySet<number>;

    /** The strings `texts`, those at the indexes in `rich` marked as having come in runs. */
    constructor(texts: readonly string[], rich: ReadonlySet<number>) {
        this.#texts = Object.freeze(texts);
        this.#rich = rich;
    }

    /** The strings, in the part's order: a cell's `<v>` is an index into them. */
    get texts(): readonly string[] {
        return this.#texts;
    }

    /** How many there are. */
    get count(): number {
        return this.#texts.length;
    }

    /** Whether the string at `index` came in runs of their own formatting, which are not carried; its text is. */
    isRich(index: number): boolean {
        return this.#rich.has(index);
    }
}
