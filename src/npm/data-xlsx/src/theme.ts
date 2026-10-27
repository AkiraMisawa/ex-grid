// The colours of a workbook's theme, and the resolution of any colour a style writes into the RGB Excel paints
// (ADR-0330 §8), as ExGrid.Data.Xlsx's XlsxTheme resolves it.

import { elementAt } from './must';
import type { XlsxColor } from './styles';

/** The default indexed palette's 64 colours (ECMA-376 Part 1, §18.8.27), as ARGB values. */
export const defaultIndexedColors: readonly number[] = Object.freeze([
    0xFF000000, 0xFFFFFFFF, 0xFFFF0000, 0xFF00FF00, 0xFF0000FF, 0xFFFFFF00, 0xFFFF00FF, 0xFF00FFFF,
    0xFF000000, 0xFFFFFFFF, 0xFFFF0000, 0xFF00FF00, 0xFF0000FF, 0xFFFFFF00, 0xFFFF00FF, 0xFF00FFFF,
    0xFF800000, 0xFF008000, 0xFF000080, 0xFF808000, 0xFF800080, 0xFF008080, 0xFFC0C0C0, 0xFF808080,
    0xFF9999FF, 0xFF993366, 0xFFFFFFCC, 0xFFCCFFFF, 0xFF660066, 0xFFFF8080, 0xFF0066CC, 0xFFCCCCFF,
    0xFF000080, 0xFFFF00FF, 0xFFFFFF00, 0xFF00FFFF, 0xFF800080, 0xFF800000, 0xFF008080, 0xFF0000FF,
    0xFF00CCFF, 0xFFCCFFFF, 0xFFCCFFCC, 0xFFFFFF99, 0xFF99CCFF, 0xFFFF99CC, 0xFFCC99FF, 0xFFFFCC99,
    0xFF3366FF, 0xFF33CCCC, 0xFF99CC00, 0xFFFFCC00, 0xFFFF9900, 0xFFFF6600, 0xFF666699, 0xFF969696,
    0xFF003366, 0xFF339966, 0xFF003300, 0xFF333300, 0xFF993300, 0xFF993366, 0xFF333399, 0xFF333333,
]);

/**
 * The colours of a workbook's theme (`<a:clrScheme>`), and the resolution of any colour a style writes into the RGB
 * Excel paints (ADR-0330 §8): a theme slot through these colours, an indexed entry through the palette, and the tint
 * applied to the result as ECMA-376 Part 1, §18.8.19 says.
 */
export class XlsxTheme {
    /**
     * The theme of Office 2013–2022, which a workbook with no theme part takes: dark 1 `000000`, light 1 `FFFFFF`,
     * `44546A`, `E7E6E6`, the accents `4472C4`, `ED7D31`, `A5A5A5`, `FFC000`, `5B9BD5`, `70AD47`, and the hyperlinks
     * `0563C1` and `954F72`.
     */
    static readonly office: XlsxTheme = new XlsxTheme([
        0xFF000000, 0xFFFFFFFF, 0xFF44546A, 0xFFE7E6E6,
        0xFF4472C4, 0xFFED7D31, 0xFFA5A5A5, 0xFFFFC000, 0xFF5B9BD5, 0xFF70AD47,
        0xFF0563C1, 0xFF954F72,
    ]);

    /** The scheme's twelve colours as ARGB values, in the scheme's order: dark 1, light 1, dark 2, light 2, accents 1 to 6,
     * hyperlink, followed hyperlink. */
    readonly schemeColors: readonly number[];

    /** A theme of the twelve colours its scheme lists, as ARGB values in the scheme's own order. */
    constructor(schemeColors: readonly number[]) {
        if (schemeColors.length !== 12) {
            throw new TypeError("A theme's colour scheme has twelve colours: dark 1, light 1, dark 2, light 2, six accents and two hyperlinks.");
        }
        this.schemeColors = Object.freeze(schemeColors.map(c => c >>> 0));
    }

    /**
     * The colour a style's theme slot names. A style numbers the first four slots in another order than the scheme lists
     * them — 0 is light 1 (Background 1), 1 dark 1 (Text 1), 2 light 2, 3 dark 2 — and the rest as listed.
     */
    slotColor(slot: number): number {
        switch (slot) {
            case 0: return elementAt(this.schemeColors, 1);
            case 1: return elementAt(this.schemeColors, 0);
            case 2: return elementAt(this.schemeColors, 3);
            case 3: return elementAt(this.schemeColors, 2);
            default:
                if (Number.isInteger(slot) && slot >= 4 && slot < 12) return elementAt(this.schemeColors, slot);
                throw new RangeError(`A theme has twelve slots, 0 to 11, not ${String(slot)}.`);
        }
    }

    /**
     * The RGB value, as `0xFFRRGGBB`, that `color` paints, or `undefined` for the automatic colour (and the indexed entries
     * 64 and 65, which name the window's text and background), whose meaning the place it colours decides.
     * @param indexedColors - The styles part's own indexed palette, or none for the default one.
     */
    resolve(color: XlsxColor, indexedColors?: readonly number[] | null): number | undefined {
        let argb: number | undefined;
        switch (color.kind) {
            case 'rgb': argb = color.value; break;
            case 'theme': argb = color.value < 12 ? this.slotColor(color.value) : undefined; break;
            case 'indexed': {
                const palette = indexedColors ?? defaultIndexedColors;
                argb = color.value < palette.length ? elementAt(palette, color.value) : undefined;
                break;
            }
            case 'automatic': argb = undefined; break;
            default: return unreachableKind(color.kind);
        }
        return argb !== undefined && color.tint !== 0 ? applyTint(argb, color.tint) : argb;
    }
}

function unreachableKind(kind: never): never {
    throw new Error(`Internal: ${String(kind)} is not a colour's kind.`);
}

/**
 * `argb` lightened or darkened by `tint`, as ECMA-376 Part 1, §18.8.19 says: the colour taken to hue, luminance and
 * saturation, its luminance scaled toward black (`L × (1 + tint)`) or toward white (`L × (1 − tint) + tint`), and taken
 * back, each channel rounded to the nearest of 0 to 255. Alpha is kept.
 * @param tint - From -1 (black) to 1 (white).
 */
export function applyTint(argb: number, tint: number): number {
    const t = Math.min(Math.max(tint, -1), 1);
    const r = ((argb >>> 16) & 0xFF) / 255;
    const g = ((argb >>> 8) & 0xFF) / 255;
    const b = (argb & 0xFF) / 255;
    const max = Math.max(r, Math.max(g, b));
    const min = Math.min(r, Math.min(g, b));
    let l = (max + min) / 2;
    let h = 0;
    let s = 0;
    if (max !== min) {
        const d = max - min;
        s = l <= 0.5 ? d / (max + min) : d / (2 - max - min);
        h = max === r ? (g - b) / d + (g < b ? 6 : 0)
            : max === g ? (b - r) / d + 2
                : (r - g) / d + 4;
        h /= 6;
    }
    l = t < 0 ? l * (1 + t) : l * (1 - t) + t;
    let r2: number;
    let g2: number;
    let b2: number;
    if (s === 0) {
        r2 = g2 = b2 = l;
    } else {
        const q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        const p = 2 * l - q;
        r2 = hue(p, q, h + 1 / 3);
        g2 = hue(p, q, h);
        b2 = hue(p, q, h - 1 / 3);
    }
    return ((argb & 0xFF000000) | (channel(r2) << 16) | (channel(g2) << 8) | channel(b2)) >>> 0;
}

function hue(p: number, q: number, given: number): number {
    let t = given;
    if (t < 0) t += 1;
    if (t > 1) t -= 1;
    if (t < 1 / 6) return p + (q - p) * 6 * t;
    if (t < 0.5) return q;
    if (t < 2 / 3) return p + (q - p) * (2 / 3 - t) * 6;
    return p;
}

/** A channel rounded as .NET's `Math.Round(v * 255, MidpointRounding.AwayFromZero)`, clamped to 0 to 255. */
function channel(v: number): number {
    const x = v * 255;
    const whole = Math.trunc(x);
    const rounded = Math.abs(x - whole) >= 0.5 ? whole + Math.sign(x) : whole;
    return Math.min(Math.max(rounded, 0), 255);
}
