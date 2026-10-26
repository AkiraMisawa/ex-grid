/**
 * A text the engine reads that is not in the form it reads: a cell address, a cell range or a number format
 * code. It is a `TypeError`, so code that catches one still catches it.
 */
export class FormatError extends TypeError {
    /** Refuses a text, saying why in `message`. */
    constructor(message: string) {
        super(message);
        this.name = 'FormatError';
    }
}
