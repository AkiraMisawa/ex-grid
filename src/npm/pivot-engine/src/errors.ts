// The refusals the engine throws that are neither a RangeError, a TypeError nor a plain Error
// (tests/cases/README.md): each is a C# exception of its own, and a shared case names it by kind.

/** Text that is not in the form it must be: a .NET format string .NET does not read, a word's template
 * with a brace out of place, a Pivot Layout's JSON that is not one. The C# engine's FormatException. */
export class PivotFormatError extends Error {
    constructor(message: string, options?: ErrorOptions) {
        super(message, options);
        this.name = 'PivotFormatError';
    }
}

/** A Pivot Layout written by a version this reader does not read: the C# engine's NotSupportedException. */
export class PivotVersionError extends Error {
    constructor(message: string) {
        super(message);
        this.name = 'PivotVersionError';
    }
}

/** A field's Order Key failed on an Item, or gave its Items keys of two types (ADR-0060): the order is
 * refused, naming the field and the value, rather than fall back to the labels. */
export class PivotOrderKeyError extends Error {
    /** The name of the field whose Order Key failed. */
    readonly field: string;

    constructor(field: string, message: string, options?: ErrorOptions) {
        super(message, options);
        this.name = 'PivotOrderKeyError';
        this.field = field;
    }
}
