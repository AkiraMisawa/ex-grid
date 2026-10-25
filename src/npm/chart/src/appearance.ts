// Each series' look from its Appearance Keys (ADR-0256).
import type { ChartDefinition } from './definition';
import type { MarkerShape, SeriesLook } from './model';
import { elementAt } from './must';

/** The line-style slots the stylesheet defines: solid, dashed, dotted, dash-dot. */
export const lineStyles = 4;

/** The marker shapes there are. */
export const markers = 4;

/** The colour slots the stylesheet defines unless a theme declares more. */
export const defaultColourSlots = 10;

const shapes: readonly MarkerShape[] = ['circle', 'square', 'diamond', 'triangle'];

/** Each series' look, and whether colours repeat. */
export interface Looks {
    /** Each series' look by its identity. */
    readonly looks: ReadonlyMap<string, SeriesLook>;
    /** Whether more colour keys than slots made colours repeat. */
    readonly coloursRepeat: boolean;
}

/** A look of these slots. */
export function seriesLook(colour: number, lineStyle: number, marker: number): SeriesLook {
    return Object.freeze({ colour, lineStyle, marker, shape: elementAt(shapes, marker % markers) });
}

// A key's slot: the one it took when first named, or the next.
function slot(table: Map<string, number>, key: string): number {
    const found = table.get(key);
    if (found !== undefined) {
        return found;
    }
    table.set(key, table.size);
    return table.size - 1;
}

/**
 * Each series' look by its identity, and whether more colour keys than `colourSlots` made colours repeat.
 * A key takes the next slot the first time any series of the definition names it, hidden series included,
 * so the same key keeps the same look when another series is hidden, filtered or added after it. A series
 * that sets no line-style key is solid; one that sets no marker key takes a circle.
 */
export function appearanceOf(definition: ChartDefinition, colourSlots = defaultColourSlots): Looks {
    if (!Number.isInteger(colourSlots) || colourSlots < 1) {
        throw new RangeError(`${String(colourSlots)} colour slots is not a count of one at least.`);
    }
    const colours = new Map<string, number>();
    const lines = new Map<string, number>();
    const marks = new Map<string, number>();
    const looks = new Map<string, SeriesLook>();
    for (const s of definition.series) {
        const k = s.effectiveKeys;
        looks.set(s.id, seriesLook(
            slot(colours, k.colour) % colourSlots,
            k.lineStyle === undefined ? 0 : slot(lines, k.lineStyle) % lineStyles,
            k.marker === undefined ? 0 : slot(marks, k.marker) % markers));
    }
    return Object.freeze({ looks, coloursRepeat: colours.size > colourSlots });
}
