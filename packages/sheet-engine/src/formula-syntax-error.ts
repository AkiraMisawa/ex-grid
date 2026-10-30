/**
 * A Formula that cannot be read. Excel refuses to accept such a Formula into a cell, and so does a Sheet:
 * nothing is recorded and no Value changes.
 */
export class FormulaSyntaxError extends SyntaxError {
    /** The Formula text as given. */
    readonly formula: string;
    /** The zero-based position in `formula` where reading stopped. */
    readonly position: number;
    /** Why, in words. */
    readonly reason: string;
    /**
     * Whether the Formula is one Excel reads, using syntax the engine does not implement (the intersection of
     * two References): typed without `=`, such text is refused by name rather than kept as text, since Excel
     * makes it a Formula (ADR-0047, third run). @internal
     */
    readonly isUnimplemented: boolean;
    /**
     * Whether the Formula is refused for its size alone: functions nested past Excel's 64 levels, or a
     * Formula nested past the engine's 256 levels (ADR-0047). Such text is still a Formula. @internal
     */
    readonly isBeyondLimit: boolean;

    /** The refusal of `formula` at `position`, for `reason`. */
    constructor(formula: string, position: number, reason: string, flags: { readonly isUnimplemented?: boolean; readonly isBeyondLimit?: boolean } = {}) {
        super(`The Formula '${formula}' cannot be read at position ${String(position)}: ${reason}`);
        this.name = 'FormulaSyntaxError';
        this.formula = formula;
        this.position = position;
        this.reason = reason;
        this.isUnimplemented = flags.isUnimplemented === true;
        this.isBeyondLimit = flags.isBeyondLimit === true;
    }
}
