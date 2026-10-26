// The pieces SVG markup is written from (ADR-0252): numbers as the C# component writes them, text escaped,
// a marker's outline, a look's classes. One writer serves the screen and a saved file, so the two cannot
// drift (ADR-0282).
import type { MarkerShape, SeriesLook } from '../model';
import { roundToDigits } from '../numbers';
import { upToPlaces } from '../invariant-text';
import { unreachable } from '../must';

/** A number written into an SVG attribute: invariant, to a hundredth of a pixel, as C#'s `Svg.N`. */
export function n(value: number): string {
    return upToPlaces(roundToDigits(value, 2), 2);
}

const escapes: Readonly<Record<string, string>> = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', '\'': '&#39;' };

/** Text as markup holds it, in content or in a quoted attribute: a Consumer's name is never markup. */
export function escapeText(text: string): string {
    return text.replace(/[&<>"']/g, c => escapes[c] ?? c);
}

/** A marker's outline at (x, y): a circle, square, diamond or triangle of radius r, as one subpath. */
export function markerPath(shape: MarkerShape, x: number, y: number, r: number): string {
    switch (shape) {
        case 'square':
            return `M${n(x - r)},${n(y - r)}h${n(2 * r)}v${n(2 * r)}h${n(-2 * r)}Z`;
        case 'diamond':
            return `M${n(x)},${n(y - r * 1.25)}L${n(x + r * 1.25)},${n(y)}L${n(x)},${n(y + r * 1.25)}L${n(x - r * 1.25)},${n(y)}Z`;
        case 'triangle':
            return `M${n(x)},${n(y - r * 1.3)}L${n(x + r * 1.2)},${n(y + r * 0.8)}L${n(x - r * 1.2)},${n(y + r * 0.8)}Z`;
        case 'circle':
            return `M${n(x - r)},${n(y)}a${n(r)},${n(r)} 0 1,0 ${n(2 * r)},0a${n(r)},${n(r)} 0 1,0 ${n(-2 * r)},0Z`;
        default:
            return unreachable(shape);
    }
}

/** The classes a look is painted by: the stylesheet's Visual Tokens say what each slot looks like (ADR-0256). */
export function lookClasses(look: SeriesLook): string {
    return `ex-chart-colour-${String(look.colour)} ex-chart-line-${String(look.lineStyle)}`;
}

/** An element's markup: its attributes written in order, each value escaped. */
export function element(name: string, attributes: readonly (readonly [name: string, value: string])[], content = ''): string {
    const written = attributes.map(([key, value]) => ` ${key}="${escapeText(value)}"`).join('');
    return `<${name}${written}>${content}</${name}>`;
}
