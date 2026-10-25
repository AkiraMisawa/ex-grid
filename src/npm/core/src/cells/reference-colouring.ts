import { elementAt } from '../must';
import { sameRange, selectionRange, type SelectionRange } from '../selection/range';

/**
 * How many colours the Reference Outline palette has (ADR-0057). It is behaviour, not appearance: it
 * decides which References share a colour, so it lives here and not in the stylesheet. A Theme sets the
 * colours, never how many there are. Seven, as the eighth Windows run observed Excel's range finder cycle.
 */
export const referencePaletteLength = 7;

/**
 * A colour of the Reference Outline palette (ADR-0057), named by its place in the palette, 1 to 7, and never
 * by how it looks: the colour at place `n` is painted from the Visual Token `--ex-reference-n`, which a Theme
 * sets (ADR-0027, ADR-0029). A Consumer never picks one: the core hands them out, and tells the Consumer the
 * one each key was given.
 */
export type ReferenceColour = 1 | 2 | 3 | 4 | 5 | 6 | 7;

/** The colour at `place` in the palette, 1 to {@link referencePaletteLength}. */
export function referenceColour(place: number): ReferenceColour {
    if (!isPlace(place)) {
        throw new RangeError(`A Reference colour is a place in the palette, 1 to ${referencePaletteLength}. (place: ${place})`);
    }
    return place;
}

function isPlace(place: number): place is ReferenceColour {
    return Number.isSafeInteger(place) && place >= 1 && place <= referencePaletteLength;
}

/** The colour handed to the `appearance`th distinct thing a text refers to, counted from 0 in order of
 * first appearance: round the palette, so the eighth takes the first colour again. */
export function colourForAppearance(appearance: number): ReferenceColour {
    if (!Number.isSafeInteger(appearance) || appearance < 0) {
        throw new RangeError(`An appearance is counted from 0. (appearance: ${appearance})`);
    }
    return referenceColour((appearance % referencePaletteLength) + 1);
}

/**
 * One Reference in the text being edited, as the Consumer's References function answers it (ADR-0057): the
 * span of the text it occupies, and either the cells it names on this grid or a key naming something this
 * grid does not hold, such as a Linked Table's column. The grid does not know what a Reference is: it colours
 * what it is told, outlines the cells, and tells the Consumer the colour each key was given.
 */
export type EditorReference =
    | { readonly start: number; readonly length: number; readonly range: SelectionRange; readonly key: undefined }
    | { readonly start: number; readonly length: number; readonly range: undefined; readonly key: string };

/**
 * A Reference, checked: a span covers at least one character from a place in the text, and it names cells
 * (`range`: `A1` and `$A$1` name one range, and so share a colour and an outline) or a key, compared as
 * written, never both and never neither.
 */
export function editorReference(
    given: { readonly start: number; readonly length: number; readonly range?: SelectionRange | undefined; readonly key?: string | undefined },
): EditorReference {
    const { start, length, range, key } = given;
    if (!Number.isSafeInteger(start) || start < 0) {
        throw new RangeError(`A Reference starts at a place in the text, from 0. (start: ${start})`);
    }
    if (!Number.isSafeInteger(length) || length < 1) {
        throw new RangeError(`A Reference covers at least one character. (length: ${length})`);
    }
    if (range !== undefined && key === undefined) {
        // Checked as a range is anywhere: a Consumer's object is made one, or refused.
        return Object.freeze({ start, length, range: selectionRange(range.topRow, range.leftColumn, range.rowCount, range.columnCount), key: undefined });
    }
    if (range === undefined && typeof key === 'string' && key !== '') {
        return Object.freeze({ start, length, range: undefined, key });
    }
    throw new TypeError('A Reference names cells (range) or a non-empty key, one and not both (ADR-0057).');
}

/** The colour a key was given (ADR-0057), as the core tells it to the Consumer: the Consumer outlines what
 * the key names wherever it shows it, in that colour. */
export interface ReferenceKeyColour {
    /** The key, as the References function answered it. */
    readonly key: string;
    /** The colour its References wear. */
    readonly colour: ReferenceColour;
}

type RangeColour = readonly [range: SelectionRange, colour: ReferenceColour];

/**
 * The colours of the References in one text (ADR-0057). References that name the same cells, or carry the
 * same key, share a colour; the colours are handed out in order of first appearance in the text, round the
 * palette. An answer whose spans do not lie inside the text, or overlap one another, is refused by name
 * rather than coloured over the wrong characters.
 */
export class ReferenceColouring {
    /** No Reference, and so no colour. */
    static readonly none: ReferenceColouring = new ReferenceColouring([], [], [], [], []);

    /** The References, in the order they stand in the text. */
    readonly references: readonly EditorReference[];
    /** The colour of each of {@link ReferenceColouring.references}, at the same index. */
    readonly colours: readonly ReferenceColour[];
    /** Each range the text names, once however often and however it is written, with its colour, in order of
     * first appearance. */
    readonly ranges: readonly RangeColour[];
    /** One Reference Outline per Reference that names cells, in the order they stand in the text: a range
     * named twice is outlined twice, in its one colour, as Excel draws it (the eighth Windows run), so a later
     * outline lies over an earlier one where they meet. */
    readonly outlines: readonly RangeColour[];
    /** Each key the text carries, once, with its colour, in order of first appearance: what the core tells
     * the Consumer. */
    readonly keys: readonly ReferenceKeyColour[];

    private constructor(
        references: readonly EditorReference[], colours: readonly ReferenceColour[], ranges: readonly RangeColour[],
        outlines: readonly RangeColour[], keys: readonly ReferenceKeyColour[],
    ) {
        this.references = Object.freeze(references);
        this.colours = Object.freeze(colours);
        this.ranges = Object.freeze(ranges);
        this.outlines = Object.freeze(outlines);
        this.keys = Object.freeze(keys);
    }

    /** Colours `references`, the Consumer's answer for `text`. The answer may list them in any order: first
     * appearance is where each stands in the text. `undefined` or empty colours nothing. */
    static of(text: string, references: readonly EditorReference[] | undefined): ReferenceColouring {
        if (references === undefined || references.length === 0) return ReferenceColouring.none;
        // The References function is a Consumer's: each answer is made a Reference again, which checks its
        // place and that it names cells or a key, one and not both.
        const checked = references.map(reference => {
            const given: unknown = reference;
            if (typeof given !== 'object' || given === null) {
                throw new TypeError('The References function answered something that is not a Reference (ADR-0057).');
            }
            return editorReference(reference);
        });
        for (const reference of checked) {
            if (reference.length > text.length - reference.start) {
                throw new RangeError(`The Reference at ${reference.start}+${reference.length} does not lie inside the editor's text of ${text.length} characters (ADR-0057).`);
            }
        }
        // Stable, so an answer already in order keeps it.
        const ordered = checked.toSorted((a, b) => a.start - b.start);
        for (let i = 1; i < ordered.length; i++) {
            const before = elementAt(ordered, i - 1);
            const here = elementAt(ordered, i);
            if (here.start < before.start + before.length) {
                throw new TypeError(`The References at ${before.start}+${before.length} and ${here.start}+${here.length} overlap: `
                    + 'a character belongs to one Reference at most (ADR-0057).');
            }
        }

        const colours: ReferenceColour[] = [];
        const ranges: RangeColour[] = [];
        const outlines: RangeColour[] = [];
        const keys: ReferenceKeyColour[] = [];
        for (const reference of ordered) {
            const known = reference.range === undefined ? colourOfKey(keys, reference.key) : colourOfRange(ranges, reference.range);
            // Something not named before takes the next colour: one on from every range and key named so far.
            const colour = known ?? colourForAppearance(ranges.length + keys.length);
            colours.push(colour);
            if (reference.range !== undefined) {
                const pair = Object.freeze([reference.range, colour] as const);
                if (known === undefined) ranges.push(pair);
                outlines.push(pair);
            } else if (known === undefined) {
                keys.push(Object.freeze({ key: reference.key, colour }));
            }
        }
        return new ReferenceColouring(ordered, colours, ranges, outlines, keys);
    }

    /** The colour of `range`, or `undefined` when the text does not name it. */
    colourOf(range: SelectionRange): ReferenceColour | undefined {
        return colourOfRange(this.ranges, range);
    }
}

function colourOfRange(ranges: readonly RangeColour[], range: SelectionRange): ReferenceColour | undefined {
    return ranges.find(([named]) => sameRange(named, range))?.[1];
}

function colourOfKey(keys: readonly ReferenceKeyColour[], key: string): ReferenceColour | undefined {
    return keys.find(each => each.key === key)?.colour;
}
