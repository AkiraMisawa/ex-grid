// The classes a per-cell appearance is painted with, and the stylesheet that gives them their meaning
// (ADR-0050, item 15; ADR-0071, "What the measurement chose"), as the C# core's AppearanceStyles writes them,
// so one class means one paint in every grid on a page (ADR-0018).
//
// Interned per part, never per combination of parts: each distinct Font, each Fill colour and each line on
// each side gets one class and one rule the first time it is asked for. A cell names the classes of its
// parts. Lines are drawn inside each cell, each cell painting its own share of Excel's centred line: the
// upper (left) cell the gridline's pixel and the pixels above it, the lower (right) cell the pixel past it,
// which only thick and double have. A solid share on the gridline is the cell's own border; every other
// share is a background layer read by one static rule in ex-grid.css (`.ex-lined`). Lengths are device
// pixels (`--ex-dp`), as Excel's are.

import type { BorderStyle, CellAppearance } from '../cells/cell-appearance';
import { unreachable } from '../must';

/** What a share of one edge paints in a cell: nothing, the cell's share of a line, or the neighbour's Fill
 * over the gridline this cell holds. */
export type ShareKind = 'none' | 'line' | 'cover';

/** One cell's share of one edge, resolved from both cells' records. */
export interface Share {
    readonly kind: ShareKind;
    readonly style: BorderStyle;
    readonly rgb: number;
}

export const noShare: Share = Object.freeze({ kind: 'none', style: 'none', rgb: 0 });

/** A cell's share of a line; none for no line. */
export function lineShare(style: BorderStyle, rgb: number): Share {
    return style === 'none' ? noShare : Object.freeze({ kind: 'line', style, rgb });
}

/** A neighbour's Fill over the gridline this cell holds. */
export function coverShare(rgb: number): Share {
    return Object.freeze({ kind: 'cover', style: 'none', rgb });
}

const coverKey = (rgb: number | undefined): string => (rgb === undefined ? '-' : String(rgb));

const shareKey = (share: Share): string => (share.kind === 'none' ? '-' : `${share.kind}:${share.style}:${share.rgb}`);

/** Excel's long dash: 9 device pixels at every scale (the fourteenth Windows run, case 18). */
const dash = 9;

interface DashRow {
    readonly height: number;
    readonly offset: number;
    readonly pattern: readonly number[];
}

/** The dash rows of a patterned style, nearest the gridline last, or undefined for a solid one. Each pattern
 * alternates on and off, starting on. */
function dashes(style: BorderStyle): readonly DashRow[] | undefined {
    switch (style) {
        case 'hair': return [{ height: 1, offset: 0, pattern: [1, 1] }];
        case 'dotted': return [{ height: 1, offset: 0, pattern: [2, 2] }];
        case 'dashed': return [{ height: 1, offset: 0, pattern: [3, 1] }];
        case 'dashDot': return [{ height: 1, offset: 0, pattern: [dash, 3, 3, 3] }];
        case 'dashDotDot': return [{ height: 1, offset: 0, pattern: [dash, 3, 3, 3, 3, 3] }];
        case 'mediumDashed': return [{ height: 2, offset: 0, pattern: [dash, 3] }];
        case 'mediumDashDot': return [{ height: 2, offset: 0, pattern: [dash, 3, 3, 3] }];
        case 'mediumDashDotDot': return [{ height: 2, offset: 0, pattern: [dash, 3, 3, 3, 3, 3] }];
        // Excel offsets the two rows of a slanted dash-dot (case 9, at both zooms).
        case 'slantedDashDot': return [{ height: 1, offset: 1, pattern: [11, 1, 5, 1] }, { height: 1, offset: 0, pattern: [9, 2, 4, 2, 1, 0] }];
        case 'none':
        case 'thin':
        case 'medium':
        case 'thick':
        case 'double':
            return undefined;
        default:
            return unreachable(style);
    }
}

/** How many device pixels a line takes on its gridline and above it (left of it): two for medium, thick,
 * double and the medium dashes, one for the rest (case 9). */
export function upPixels(style: BorderStyle): number {
    return style === 'medium' || style === 'thick' || style === 'double' || style === 'mediumDashed' || style === 'mediumDashDot'
        || style === 'mediumDashDotDot' || style === 'slantedDashDot' ? 2 : 1;
}

/** Whether a line reaches one pixel past its gridline into the lower (right) cell: thick and double. */
export function reachesPast(style: BorderStyle): boolean {
    return style === 'thick' || style === 'double';
}

const styleNames: Readonly<Record<BorderStyle, string>> = {
    none: 'none', hair: 'hair', thin: 'thin', medium: 'medium', thick: 'thick', double: 'double', dotted: 'dotted', dashed: 'dashed',
    dashDot: 'dashdot', dashDotDot: 'dashdotdot', mediumDashed: 'mediumdashed', mediumDashDot: 'mediumdashdot',
    mediumDashDotDot: 'mediumdashdotdot', slantedDashDot: 'slanteddashdot',
};

const hex = (rgb: number): string => rgb.toString(16).padStart(6, '0');
const device = (pixels: number): string => (pixels === 1 ? 'var(--ex-dp)' : `calc(${pixels} * var(--ex-dp))`);

function repeating(along: string, colour: string, pattern: readonly number[]): string {
    let css = `repeating-linear-gradient(to ${along}`;
    let at = 0;
    pattern.forEach((length, i) => {
        const end = at + length;
        // A stop is one calc() of device pixels from the tile's start, so it never rounds on the way.
        css += `,${i % 2 === 0 ? colour : 'transparent'} ${at === 0 ? '0px' : device(at)} ${end === 0 ? '0px' : device(end)}`;
        at = end;
    });
    return `${css})`;
}

interface FontKey {
    readonly rgb: number;
    readonly bold: boolean;
    readonly italic: boolean;
    readonly underline: boolean;
    readonly strikethrough: boolean;
}

const fontKeyOf = (own: CellAppearance): FontKey => ({
    rgb: own.fontColour ?? -1, // oxlint-disable-line ex-grid/no-stand-in-default -- -1 is the key's "no colour", never painted
    bold: own.bold === true, italic: own.italic === true, underline: own.underline === true, strikethrough: own.strikethrough === true,
});

const fontId = (font: FontKey): string =>
    `${font.rgb}:${font.bold ? 'b' : ''}${font.italic ? 'i' : ''}${font.underline ? 'u' : ''}${font.strikethrough ? 's' : ''}`;

const paintsFont = (font: FontKey): boolean => font.rgb >= 0 || font.bold || font.italic || font.underline || font.strikethrough;

/** One grid's appearance classes and their generated stylesheet. */
export class AppearanceStyles {
    readonly #fonts = new Map<string, string>();
    readonly #fills = new Map<number, string>();
    readonly #parts = new Map<string, string>();
    readonly #cells = new Map<string, string | undefined>();
    readonly #editors = new Map<string, string | undefined>();
    readonly #editorRules = new Set<string>();
    #rules = '';
    #version = 0;

    /** Moves whenever a rule is added; the grid writes its stylesheet again when it does, and at no other time. */
    get version(): number { return this.#version; }

    /** Every rule asked for so far, as the grid's generated `<style>` holds them. */
    get css(): string { return this.#rules; }

    /**
     * The appearance half of one cell's class attribute, interned: its Font, its Fill, its four shares, and
     * the Fill over each gridline it holds that is not its own, beneath any line there; or undefined when it
     * paints none of them.
     */
    classFor(own: CellAppearance, top: Share, right: Share, bottom: Share, left: Share, rightCover: number | undefined,
        bottomCover: number | undefined): string | undefined {
        const font = fontKeyOf(own);
        const fill = own.fill ?? -1; // oxlint-disable-line ex-grid/no-stand-in-default -- -1 is the key's "no Fill", never painted
        const key = `${fontId(font)}|${fill}|${shareKey(top)}|${shareKey(right)}|${shareKey(bottom)}|${shareKey(left)}|${coverKey(rightCover)}|${coverKey(bottomCover)}`;
        if (this.#cells.has(key)) return this.#cells.get(key);
        const parts: string[] = [];
        if (paintsFont(font)) parts.push(this.#fontClass(font));
        if (fill >= 0) parts.push(this.#fillClass(fill));
        if (top.kind !== 'none' || right.kind !== 'none' || bottom.kind !== 'none' || left.kind !== 'none' || rightCover !== undefined
            || bottomCover !== undefined) {
            parts.push('ex-lined');
            this.#addPart(parts, 't', top);
            this.#addPart(parts, 'r', right);
            this.#addPart(parts, 'b', bottom);
            this.#addPart(parts, 'l', left);
            if (rightCover !== undefined) this.#addPart(parts, 'r', coverShare(rightCover));
            if (bottomCover !== undefined) this.#addPart(parts, 'b', coverShare(bottomCover));
        }
        const composed = parts.length === 0 ? undefined : parts.join(' ');
        this.#cells.set(key, composed);
        return composed;
    }

    /**
     * The Cell Editor's appearance half over a cell that looks like `own`, interned: the cell's own Font and
     * Fill classes, or undefined for neither. The first time an editor names one, the class gains a rule for
     * the editor's surfaces through the editor's own tokens (`--ex-editor-background`, `--ex-editor-color`).
     */
    editorClassFor(own: CellAppearance): string | undefined {
        const font = fontKeyOf(own);
        const fill = own.fill ?? -1; // oxlint-disable-line ex-grid/no-stand-in-default -- -1 is the key's "no Fill", never painted
        const key = `${fontId(font)}|${fill}`;
        if (this.#editors.has(key)) return this.#editors.get(key);
        let fontClass: string | undefined;
        if (paintsFont(font)) {
            fontClass = this.#fontClass(font);
            if (!this.#editorRules.has(fontClass)) {
                this.#editorRules.add(fontClass);
                this.#rules += `.ex-viewport .ex-editor.${fontClass},.ex-viewport .ex-editor.${fontClass} :is(input,textarea,.ex-reference-text)`
                    + `,.ex-viewport .ex-reference-text-cell.${fontClass}{${fontDeclarations(font, '--ex-editor-color')}}\n`;
                this.#version += 1;
            }
        }
        let fillClass: string | undefined;
        if (fill >= 0) {
            fillClass = this.#fillClass(fill);
            if (!this.#editorRules.has(fillClass)) {
                this.#editorRules.add(fillClass);
                this.#rules += `.ex-viewport .ex-editor.${fillClass},.ex-viewport .ex-reference-text-cell.${fillClass}`
                    + `{--ex-editor-background:#${hex(fill)}}\n`;
                this.#version += 1;
            }
        }
        const composed = fontClass === undefined ? fillClass : fillClass === undefined ? fontClass : `${fontClass} ${fillClass}`;
        this.#editors.set(key, composed);
        return composed;
    }

    #addPart(parts: string[], side: 't' | 'r' | 'b' | 'l', share: Share): void {
        if (share.kind === 'none') return;
        const key = `${side}${shareKey(share)}`;
        let name = this.#parts.get(key);
        if (name === undefined) {
            name = share.kind === 'cover' ? `ex-l${side}-cover-${hex(share.rgb)}` : `ex-l${side}-${styleNames[share.style]}-${hex(share.rgb)}`;
            this.#parts.set(key, name);
            this.#appendPartRule(name, side, share);
        }
        parts.push(name);
    }

    #fontClass(font: FontKey): string {
        const id = fontId(font);
        const known = this.#fonts.get(id);
        if (known !== undefined) return known;
        const flags = `${font.bold ? 'b' : ''}${font.italic ? 'i' : ''}${font.underline ? 'u' : ''}${font.strikethrough ? 's' : ''}`;
        const name = `ex-font-${font.rgb >= 0 ? hex(font.rgb) : 'x'}${flags}`;
        this.#fonts.set(id, name);
        // A Stale or Error Cell State keeps its own look (ADR-0006). Over a tone the Font wins, being the cell's own.
        this.#rules += `.ex-cell:not(.ex-state-stale, .ex-state-error).${name}{${fontDeclarations(font, 'color')}}\n`;
        this.#version += 1;
        return name;
    }

    #fillClass(rgb: number): string {
        const known = this.#fills.get(rgb);
        if (known !== undefined) return known;
        const name = `ex-fill-${hex(rgb)}`;
        this.#fills.set(rgb, name);
        // The Fill covers the gridlines at its edges (ADR-0071; the eleventh run, cases 4-6). --ex-fill-color is
        // what a double line's middle pixel shows on a gridline no neighbour's Fill covers.
        this.#rules += `.ex-cell.${name}{background-color:#${hex(rgb)};--ex-fill-color:#${hex(rgb)}`
            + ';--ex-column-rule-color:transparent;--ex-row-rule:none}\n';
        this.#version += 1;
        return name;
    }

    #appendPartRule(name: string, side: 't' | 'r' | 'b' | 'l', share: Share): void {
        // t and l are the shares a line reaches into the lower (right) cell with; b and r the gridline's pixel
        // and what lies above (left of) it.
        const horizontal = side === 't' || side === 'b';
        const fromFar = side === 'b' || side === 'r';
        const toward = horizontal ? (fromFar ? 'top' : 'bottom') : (fromFar ? 'left' : 'right');
        const along = horizontal ? 'right' : 'bottom';
        const colour = `#${hex(share.rgb)}`;
        const property = `--ex-line-${side}`;
        this.#version += 1;

        if (share.kind === 'cover') {
            // A neighbour's Fill over the gridline this cell holds: a layer of its own, beneath every line, exactly
            // the band of the rule it covers (--ex-rule-dp; ADR-0090).
            this.#rules += `.${name}{--ex-cover-${side}:linear-gradient(to ${toward},${colour} 0 var(--ex-rule-dp, 1px),transparent 0)`
                + `;--ex-cover-${side}-color:${colour}}\n`;
            return;
        }

        const rows = dashes(share.style);
        if (fromFar && rows === undefined && share.style !== 'double') {
            // A solid line on and above (left of) the gridline is the cell's own border. A right border gives its
            // width back out of the right padding, so the content box the #### decision assumes stays (ADR-0016).
            const width = device(upPixels(share.style));
            this.#rules += horizontal
                ? `.ex-cell.${name}{border-bottom:${width} solid ${colour}}\n`
                : `.ex-cell.${name}{border-right:${width} solid ${colour};padding-right:calc(var(--ex-cell-padding-x, 8px) - ${width}`
                    + ' - var(--ex-number-dp, 0px))}\n';
            return;
        }

        let image: string;
        let size: string;
        let at: string;
        if (!fromFar) {
            // The pixel past the gridline: thick's and double's only.
            image = `linear-gradient(to ${toward},${colour} 0 var(--ex-dp),transparent 0)`;
            size = '100% 100%';
            at = '0 0';
        } else if (rows !== undefined) {
            // Each row of the line its own tile, as high as the row and as long as the cell.
            image = rows.map(row => repeating(along, colour, row.pattern)).join(',');
            size = rows.map(row => {
                const thick = row.height === 1 ? 'var(--ex-dp)' : `calc(${row.height} * var(--ex-dp))`;
                return horizontal ? `100% ${thick}` : `${thick} 100%`;
            }).join(',');
            at = rows.map(row => {
                const offset = row.offset === 0 ? '0px' : 'var(--ex-dp)';
                return horizontal ? `left 0 bottom ${offset}` : `right ${offset} top 0`;
            }).join(',');
        } else {
            // Double: a line above (left of) the gridline, whose own pixel shows what the gridline beneath it
            // would: the lower (right) cell's Fill, else this cell's, else the ground (case 17; case 9).
            const under = `var(--ex-cover-${side}-color,var(--ex-fill-color,var(--ex-background, Canvas)))`;
            image = `linear-gradient(to ${toward},${under} 0 var(--ex-dp),${colour} 0 calc(2 * var(--ex-dp)),transparent 0)`;
            size = '100% 100%';
            at = '0 0';
        }
        this.#rules += `.${name}{${property}:${image};${property}-size:${size};${property}-at:${at}}\n`;
    }
}

function fontDeclarations(font: FontKey, colour: string): string {
    let css = font.rgb >= 0 ? `${colour}:#${hex(font.rgb)};` : '';
    if (font.bold) css += 'font-weight:700;';
    if (font.italic) css += 'font-style:italic;';
    if (font.underline || font.strikethrough) {
        css += `text-decoration-line:${font.underline && font.strikethrough ? 'underline line-through' : font.underline ? 'underline' : 'line-through'};`;
    }
    return css;
}
