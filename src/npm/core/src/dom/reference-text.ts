// The coloured text (ADR-0057, "The coloured text is a layer that shows only while it is up to date"): an <input>
// cannot colour part of its text, so beneath each editor surface a layer draws the same text, and each Reference in
// it is coloured. The layer stands immediately before its field, in the field's box, and shows, with the field's own
// text made see-through, only in the surface the edit is in, the field holding DOM focus, only while its text is the
// field's value, and never during an IME composition: uncoloured for a moment rather than coloured on the wrong
// characters. The stylesheet does the rest, as it does for the C# core's layer, whose classes this one wears.
//
// The text is one run, as the field's is (ADR-0057's note of 2026-10-01): a run per Reference drifted 1/64 px a run
// from the field's characters. The References are coloured by the CSS Custom Highlight API instead, one Range per
// stretch over that one run, in highlights whose names are the grid's own: `CSS.highlights` is one registry per
// document, and a shared name would let one grid clear another's colours (ADR-0018).

import { referencePaletteLength, type ReferenceColour, type ReferenceColouring } from '../cells/reference-colouring';

/** A stretch of a layer's text in one highlight, in the text's UTF-16 offsets. */
export interface ColouredStretch {
    readonly start: number;
    readonly length: number;
    readonly name: string;
}

/** Where what Point wrote stands in the edit's text, shown selected on the grey ground (ADR-0051, ADR-0058). */
export interface PointedSpan {
    readonly start: number;
    readonly length: number;
}

/** The Custom Highlight API as a document's window offers it, or none where the browser has none. */
interface HighlightApi {
    readonly make: () => Highlight;
    readonly registry: HighlightRegistry;
}

function highlightApi(doc: Document): HighlightApi | undefined {
    const view = doc.defaultView;
    if (view === null || !('Highlight' in view) || !('highlights' in view.CSS)) return undefined;
    const made = view.Highlight;
    return { make: () => new made(), registry: view.CSS.highlights };
}

/**
 * One grid's highlights (ADR-0057, note of 2026-10-01): a name per colour of the palette, per pointed shade and for
 * the pointed ground, each carrying the grid's id prefix; the stylesheet that paints each name with the custom
 * property the shipped stylesheet declares on the layer, so the colours are still the Visual Tokens; and the
 * Highlight objects registered under them, which go with the grid.
 */
export class ReferenceHighlights {
    /** The grid's own stylesheet: one rule per highlight, each reading a custom property of the layer. */
    readonly css: string;
    /** The highlight of the grey ground the Reference Point is writing lies on. */
    readonly ground: string;
    readonly #colours: readonly string[];
    readonly #pointed: readonly string[];
    readonly #registered = new Map<string, Highlight>();
    #api: HighlightApi | undefined;

    constructor(idPrefix: string) {
        const colours: string[] = [];
        const pointed: string[] = [];
        let css = '';
        for (let place = 1; place <= referencePaletteLength; place++) {
            const colour = `${idPrefix}reference-${place}`;
            const shade = `${idPrefix}reference-${place}-pointed`;
            colours.push(colour);
            pointed.push(shade);
            css += `::highlight(${colour}){color:var(--ex-reference-text-${place})}\n`;
            css += `::highlight(${shade}){color:var(--ex-reference-text-${place}-pointed)}\n`;
        }
        this.ground = `${idPrefix}reference-pointed`;
        css += `::highlight(${this.ground}){background-color:var(--ex-reference-text-pointed)}\n`;
        this.#colours = colours;
        this.#pointed = pointed;
        this.css = css;
    }

    /** The highlight of a Reference in `colour`, or in its pointed shade. */
    nameOf(colour: ReferenceColour, pointed: boolean): string {
        const name = (pointed ? this.#pointed : this.#colours)[colour - 1];
        if (name === undefined) throw new RangeError(`A Reference colour is a place in the palette, 1 to ${referencePaletteLength}. (place: ${colour})`);
        return name;
    }

    /**
     * What a layer's highlights cover. Each Reference is one stretch in its colour. The span Point wrote lies on the
     * grey ground, written first, and the References inside it wear their pointed shades: a Reference standing
     * exactly there, or anything longer, a lookup a press outside the grid wrote, as long as no Reference crosses its
     * ends (a Reference standing exactly there crosses neither). A span that cuts through a Reference wears no look: half of a Reference on the grey would say that half
     * alone was pointed.
     */
    coloursOf(colouring: ReferenceColouring, pointed: PointedSpan | undefined): readonly ColouredStretch[] {
        const ground = pointed !== undefined && pointed.length > 0 && !cutsAReference(colouring, pointed) ? pointed : undefined;
        const stretches: ColouredStretch[] = [];
        if (ground !== undefined) stretches.push({ start: ground.start, length: ground.length, name: this.ground });
        colouring.references.forEach((reference, index) => {
            const colour = colouring.colours[index];
            if (colour === undefined) throw new RangeError('A Reference of a colouring has no colour.');
            const inGround = ground !== undefined && reference.start >= ground.start && reference.start + reference.length <= ground.start + ground.length;
            stretches.push({ start: reference.start, length: reference.length, name: this.nameOf(colour, inGround) });
        });
        return stretches;
    }

    /** The Highlight registered under `name`, made and registered the first time; none where the browser has no
     * Custom Highlight API. */
    highlight(doc: Document, name: string): Highlight | undefined {
        const registered = this.#registered.get(name);
        if (registered !== undefined) return registered;
        this.#api ??= highlightApi(doc);
        const api = this.#api;
        if (api === undefined) return undefined;
        const made = api.make();
        this.#registered.set(name, made);
        api.registry.set(name, made);
        return made;
    }

    /** Takes this grid's highlights out of the document's registry. */
    unregister(): void {
        const api = this.#api;
        if (api !== undefined) {
            for (const [name, registered] of this.#registered) {
                if (api.registry.get(name) === registered) api.registry.delete(name);
            }
        }
        this.#registered.clear();
    }
}

/** What a layer's highlights cover as the C# core writes it on its layer (`data-ex-colours`): `start,length,name`
 * per stretch, separated by spaces. */
export function coloursText(stretches: readonly ColouredStretch[]): string {
    return stretches.map(({ start, length, name }) => `${start},${length},${name}`).join(' ');
}

// Whether a Reference begins on one side of an end of the span and finishes on the other.
function cutsAReference(colouring: ReferenceColouring, span: PointedSpan): boolean {
    const end = span.start + span.length;
    return colouring.references.some(({ start, length }) => {
        const referenceEnd = start + length;
        return (start < span.start && referenceEnd > span.start) || (start < end && referenceEnd > end);
    });
}

/**
 * One surface's layer: the text it draws, as one run in its line, and the ranges it has added to the grid's
 * highlights while it shows. The text and what its highlights cover are written on it as the C# core's layer
 * carries them (`data-ex-text`, `data-ex-colours`).
 */
export class ReferenceTextLayer {
    readonly element: HTMLElement;
    readonly #line: HTMLElement;
    readonly #run: Text;
    #stretches: readonly ColouredStretch[] = [];
    #colours = '';
    #ranges: (readonly [Highlight, Range])[] = [];
    #colouredFor: { readonly text: string; readonly colours: string } | undefined;

    constructor(doc: Document, className: string) {
        this.element = doc.createElement('div');
        this.element.className = className;
        this.element.setAttribute('aria-hidden', 'true');
        this.element.setAttribute('data-ex-text', '');
        this.element.setAttribute('data-ex-colours', '');
        this.#line = doc.createElement('div');
        this.#line.className = 'ex-reference-text-line';
        this.#run = doc.createTextNode('');
        this.#line.append(this.#run);
        this.element.append(this.#line);
    }

    /** The text the layer draws. */
    get text(): string {
        return this.#run.data;
    }

    /** Writes the text the layer draws and what its highlights cover, where either changed. Whether the text did. */
    write(text: string, stretches: readonly ColouredStretch[]): boolean {
        const colours = coloursText(stretches);
        if (colours !== this.#colours) {
            this.#colours = colours;
            this.#stretches = stretches;
            this.element.setAttribute('data-ex-colours', colours);
        }
        if (this.#run.data === text) return false;
        this.#run.data = text;
        this.element.setAttribute('data-ex-text', text);
        return true;
    }

    /** Colours the layer's text: built again only when its text or its stretches changed since. */
    colour(highlights: ReferenceHighlights): void {
        const text = this.#run.data;
        const was = this.#colouredFor;
        if (was?.text === text && was.colours === this.#colours) return;
        this.uncolour();
        const doc = this.element.ownerDocument;
        for (const { start, length, name } of this.#stretches) {
            // The stretches are found in the text written with them: one outside it is the grid's own defect.
            if (start < 0 || start + length > text.length) throw new Error(`Internal: a coloured stretch ${start},${length} lies outside the layer's text.`);
            const highlight = highlights.highlight(doc, name);
            if (highlight === undefined) return;
            const range = doc.createRange();
            range.setStart(this.#run, start);
            range.setEnd(this.#run, start + length);
            highlight.add(range);
            this.#ranges.push([highlight, range]);
        }
        this.#colouredFor = { text, colours: this.#colours };
    }

    /** Takes the layer's ranges out of the grid's highlights. */
    uncolour(): void {
        for (const [highlight, range] of this.#ranges) highlight.delete(range);
        this.#ranges = [];
        this.#colouredFor = undefined;
    }

    /** Scrolls the layer's line sideways with its field. */
    scrollTo(left: number): void {
        this.#line.scrollLeft = left;
    }
}
