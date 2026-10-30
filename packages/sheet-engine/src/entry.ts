import { parseConstant } from './constant-parser';
import type { Culture } from './culture';
import { formatRoundTrip } from './dotnet';
import { FormulaSyntaxError } from './formula-syntax-error';
import { normalizeFormula, rewriteReferences } from './formulas/formula-text';
import type { Node } from './formulas/nodes';
import { parseFormulaWithTokens } from './formulas/parser';
import type { Reference } from './formulas/reference';
import { shiftReference } from './formulas/reference-shift';
import { distinctIgnoringCase, unboundNames } from './formulas/unbound-name-walk';
import { parseWrittenError, Value } from './value';

/**
 * What a user put into a cell: a constant or a Formula (CONTEXT.md). An Entry is what a Sheet Document records,
 * and what the user sees again when editing the cell. A constant is held already parsed, never as the text that
 * was typed (ADR-0048); a Formula is held in Excel's invariant syntax (ADR-0047). Immutable.
 */
export class Entry {
    /** The constant, or `undefined` when this Entry is a Formula. */
    readonly constant: Value | undefined;
    /**
     * The Formula in invariant syntax, beginning with `=`: the whitespace as it was typed where Excel keeps it
     * (not at the end, nor before a `,`), and each token as the engine writes it (function names and References
     * in upper case, `,` between arguments, `.` as the decimal separator) (ADR-0047); `undefined` when this Entry
     * is a constant.
     */
    readonly formula: string | undefined;
    /** The parsed Formula. @internal */
    readonly parsed: Node | undefined;

    private constructor(constant: Value | undefined, formula: string | undefined, parsed: Node | undefined) {
        this.constant = constant;
        this.formula = formula;
        this.parsed = parsed;
    }

    /** Whether this Entry is a Formula. */
    get isFormula(): boolean {
        return this.formula !== undefined;
    }

    /**
     * The functions the Formula calls that are outside ExSheet's declared set, in the text's order, each once
     * without regard to case: each gives `#NAME?` (ADR-0047). Empty for a constant, and for a Formula that calls
     * only declared functions. A reader of another program's workbook lists them before the user meets them
     * (ADR-0330 §4).
     */
    get undeclaredFunctions(): readonly string[] {
        if (this.parsed === undefined) return [];
        return distinctIgnoringCase([...this.parsed.calls()].filter(c => c.function === undefined).map(c => c.name));
    }

    /**
     * The names the Formula reads that nothing in it binds — not a function's, not a `LET`'s, not a `LAMBDA`'s
     * parameter, not `TRUE` or `FALSE` — in the text's order, each once without regard to case. ExSheet defines
     * no names, so each gives `#NAME?`; in Excel each is a defined name, or an Excel Table's, where the workbook
     * has one (ADR-0047, ADR-0330 §4). Empty for a constant.
     */
    get unboundNames(): readonly string[] {
        return this.parsed === undefined ? [] : unboundNames(this.parsed);
    }

    /**
     * The Entry as a copy writes it `rows` down and `columns` across from where it stands, as Excel writes a
     * copied Formula and a shared formula's cells (ADR-0048, ADR-0330 §4): each relative part of a Reference
     * moves by that distance, each absolute part (`$`) stays, a whole column or row spans every row or column
     * still, and a Reference moved off the Sheet is written `#REF!`. A constant, or a move of nothing, is this
     * Entry. Negative distances move up and left.
     */
    movedBy(rows: number, columns: number): Entry {
        return rows === 0 && columns === 0 ? this : Entry.rewrite(this, r => shiftReference(r, rows, columns));
    }

    /**
     * The Entry with its References mapped (`rewriteReferences`); the same instance when nothing changed.
     * @internal
     */
    static rewrite(entry: Entry, map: (reference: Reference) => Reference | undefined, requalify?: (sheet: string) => string | undefined): Entry {
        const formula = entry.formula;
        if (formula === undefined) return entry;
        const rewritten = rewriteReferences(formula, map, requalify);
        return rewritten === formula ? entry : Entry.fromFormula(rewritten);
    }

    /**
     * The constant Entry of an Error Value written exactly as Excel writes one in a file, case and all (`#N/A`,
     * `#DIV/0!`, `#SPILL!`), or `undefined` when the text is no Error Value a cell can hold: `#GETTING_DATA` and
     * `#CIRC!` are not one, and nothing else is read as a Formula or a number. A reader of another program's
     * workbook takes an error cell through it (ADR-0330 §4).
     */
    static fromErrorText(text: string): Entry | undefined {
        const error = parseWrittenError(text);
        return error === undefined ? undefined : new Entry(Value.fromError(error), undefined, undefined);
    }

    /**
     * A constant Entry. `#GETTING_DATA` and `#CIRC!` are states of a computation and cannot be one, and neither
     * can the `#VALUE!` of a cell whose Formula ExSheet refuses, read from it: held as a constant it would be an
     * Error Value a Formula can catch (ADR-0047; 2026-10-12, decided with the user: a refusal is never caught,
     * anywhere). Throws a `RangeError` for one of those.
     */
    static fromValue(constant: Value): Entry {
        if (constant.isRefused) {
            throw new RangeError(`This ${constant.error} is a Formula ExSheet refused, because it cannot answer it as Excel; held as a constant it would be an Error Value a Formula can catch.`);
        }
        if (constant.isError && (constant.error === '#GETTING_DATA' || constant.error === '#CIRC!')) {
            throw new RangeError(`${constant.error} is not a constant a cell can hold.`);
        }
        return new Entry(constant, undefined, undefined);
    }

    /**
     * A Formula Entry from its invariant text, which begins with `=`; `Entry.parse` reads the same text as the
     * same Formula. Throws a `FormulaSyntaxError` when the Formula cannot be read. @internal
     */
    static fromFormula(formula: string): Entry {
        const { node, tokens } = parseFormulaWithTokens(formula);
        return new Entry(undefined, normalizeFormula(formula, tokens), node);
    }

    /**
     * Reads text as a user typed it into a cell: text beginning with `=` is a Formula in invariant syntax;
     * anything else is a constant read under `culture` and recorded parsed (ADR-0048). Empty text is no Entry,
     * and gives `undefined`. Throws a `FormulaSyntaxError` when the text is a Formula that cannot be read, or
     * text with a leading `+` or `-` that Excel reads as a Formula using syntax the engine does not implement
     * (`-B2 C2`) or nested past the limits the engine reads.
     */
    static parse(typed: string, culture: Culture): Entry | undefined {
        if (typed.length === 0) return undefined;
        if (typed.startsWith('=')) return Entry.fromFormula(typed);
        const constant = parseConstant(typed, culture);
        if (constant.kind === 'text') {
            const formula = Entry.signedFormula(typed);
            if (formula !== undefined) return formula;
        }
        return new Entry(constant, undefined, undefined);
    }

    /**
     * Text typed with a leading `+` or `-` that is not a constant is the Formula it spells after an `=`, as
     * Excel's cell editor reads it: `+A1` is `=+A1` and `-A1` is `=-A1` (observed with real keys). Text that does
     * not read as a Formula that way stays text: `undefined`. Throws where Excel reads the text as a Formula whose
     * syntax the engine does not implement (`-B2 C2`, TYPED-054; ADR-0047, third run), or one refused for its
     * size alone: kept as text it would quietly be another Entry than the one typed. @internal
     */
    static signedFormula(typed: string): Entry | undefined {
        if (typed.length < 2 || (typed[0] !== '+' && typed[0] !== '-')) return undefined;
        try {
            return Entry.fromFormula(`=${typed}`);
        } catch (e) { // oxlint-disable-line ex-grid/no-catch -- text that is no Formula stays text, as Excel keeps it
            if (e instanceof FormulaSyntaxError && !e.isUnimplemented && !e.isBeyondLimit) return undefined;
            throw e;
        }
    }

    /** Whether typed text would not be kept as text because of its leading sign: `signedFormula` reads it as a Formula, or refuses it. @internal */
    static isSignedFormula(typed: string): boolean {
        try {
            return Entry.signedFormula(typed) !== undefined;
        } catch (e) { // oxlint-disable-line ex-grid/no-catch -- a refusal is an answer here: the text is a Formula
            if (e instanceof FormulaSyntaxError) return true;
            throw e;
        }
    }

    /** Two Entries are equal when they hold the same constant (exactly) or the same Formula text. */
    equals(other: Entry | undefined): boolean {
        return other !== undefined && Value.same(this.constant, other.constant) && this.formula === other.formula;
    }

    /** Whether two Entries that may be absent are equal: both absent, or equal. */
    static same(a: Entry | undefined, b: Entry | undefined): boolean {
        if (a === undefined || b === undefined) return a === b;
        return a.equals(b);
    }

    /** The Formula text, or the constant's invariant diagnostic form. */
    toString(): string {
        if (this.formula !== undefined) return this.formula;
        if (this.constant === undefined) throw new Error('Internal: an Entry is a constant or a Formula.');
        return this.constant.kind === 'number' ? formatRoundTrip(this.constant.number) : this.constant.toString();
    }
}
