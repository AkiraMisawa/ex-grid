// The files a chart saves (ADR-0252, ADR-0282), written on the click and never during a drawing: CSV of
// the visible series in the range shown, the SVG of the drawing on screen, or that SVG painted to PNG at
// the screen's Device Pixel. The SVG is written by the same part writers as the screen, so a file cannot
// drift from it, and carries the stylesheet inside it, since a file has no page to take it from.
import type { ChartScene } from '../scene';
import { drawingParts } from './chart-drawing';
import { stylesheetText } from './shipped-stylesheet';
import { element, escapeText, n } from './svg-markup';

/** The formats a chart saves as. */
export type ChartFileFormat = 'csv' | 'svg' | 'png';

/** The media type a format is saved as. */
export function mediaType(format: ChartFileFormat): string {
    return format === 'csv' ? 'text/csv' : format === 'svg' ? 'image/svg+xml' : 'image/png';
}

// The characters no file system takes in a name: Windows' set, which holds every other's.
// oxlint-disable-next-line no-control-regex -- the control characters are what it matches: a name cannot hold them
const unsafe = /[\u0000-\u001f\u007f-\u009f/\\:*?"<>|]/g;

/** The file name: the chart's own, or its title's, made safe for a file system, with the format's extension. */
export function fileName(name: string | undefined, title: string | undefined, format: ChartFileFormat): string {
    const stem = name !== undefined && name.trim() !== '' ? name : title !== undefined && title.trim() !== '' ? title : 'chart';
    // A file system drops trailing dots, so "." or ".." would name no file at all.
    const safe = stem.trim().replace(unsafe, '_').replace(/[. ]+$/, '');
    return `${safe === '' ? 'chart' : safe}.${format}`;
}

/** The names of the Visual Tokens the stylesheet reads, without their prefix (ADR-0282). */
export const tokenNames: readonly string[] = Object.freeze([...new Set([...stylesheetText.matchAll(/--_ex-chart-([a-z0-9-]+):/g)].map(m => m[1]).filter(
    (name): name is string => name !== undefined))]);

// A value that would end the declaration is left out, and the stylesheet's default stands for it.
const declarable = (value: string): boolean => value !== '' && !/[;{}<>]/.test(value);

/**
 * The style a saved file's root carries: each token's value as the screen has it, then the font, so the
 * file has the theme's colours (ADR-0282). `values` are the tokens' values in {@link tokenNames}' order,
 * then the root's font family.
 */
export function themeStyle(values: readonly string[]): string {
    let style = '';
    tokenNames.forEach((name, i) => {
        const value = values[i];
        if (value !== undefined && declarable(value)) {
            style += `--ex-chart-${name}:${value};`;
        }
    });
    const font = values[tokenNames.length];
    if (font !== undefined && declarable(font)) {
        style += `font-family:${font};`;
    }
    return style;
}

/** The values of the chart's Visual Tokens on screen, and its font family, read from its root on the click. */
export function screenTheme(root: Element): string {
    const style = getComputedStyle(root);
    return themeStyle([...tokenNames.map(name => style.getPropertyValue(`--_ex-chart-${name}`).trim()), style.fontFamily]);
}

/** What a saved SVG is drawn from. */
export interface FileDrawing {
    readonly scene: ChartScene;
    readonly width: number;
    readonly height: number;
    readonly fontSize: number;
    readonly lineHeight: number;
    readonly angle: number;
    readonly summary: string;
    readonly theme: string;
}

// The stylesheet as an SVG's style element holds it: inside CDATA, so a selector's > stays CSS.
const styleMarkup = (): string => `<![CDATA[${stylesheetText.replaceAll(']]>', ']]]]><![CDATA[>')}]]>`;

/** The SVG text of the drawing as it stands, without the pointer's guide, tooltip or band. */
export function svgFile(d: FileDrawing): string {
    const content = `<style>${styleMarkup()}</style>`
        + element('title', [], escapeText(d.summary))
        // A file has no page behind it: the background is drawn, so a PNG is not transparent.
        + element('rect', [['class', 'ex-chart-file-background'], ['width', n(d.width)], ['height', n(d.height)]])
        + drawingParts(d.scene, d.lineHeight, d.angle).map(p => p.markup).join('');
    return '<?xml version="1.0" encoding="UTF-8"?>\n' + element('svg', [
        ['xmlns', 'http://www.w3.org/2000/svg'], ['class', 'ex-chart ex-chart-svg ex-chart-file'], ['width', n(d.width)], ['height', n(d.height)],
        ['viewBox', `0 0 ${n(d.width)} ${n(d.height)}`], ['style', `font-size:${n(d.fontSize)}px;--ex-chart-line-height:${n(d.lineHeight)}px;${d.theme}`],
    ], content);
}

/** Saves bytes under a file name, through a Blob and an object URL revoked once the save has started: one click saves. */
export function save(name: string, type: string, data: BlobPart): void {
    const url = URL.createObjectURL(new Blob([data], { type }));
    const link = document.createElement('a');
    link.href = url;
    link.download = name;
    link.style.display = 'none';
    document.body.appendChild(link);
    try {
        link.click();
    } finally {
        link.remove();
        setTimeout(() => { URL.revokeObjectURL(url); }, 0);
    }
}

/**
 * Paints an SVG to PNG. The canvas is the drawing's size at the screen's Device Pixel, so a PNG taken at
 * 150% is drawn at 150% and not scaled up from 100% (ADR-0090); the browser lays out the text in the fonts
 * the screen shows.
 */
export async function paintPng(svgText: string, width: number, height: number): Promise<Blob> {
    const scale = window.devicePixelRatio;
    if (!(scale > 0)) {
        throw new RangeError(`The screen's Device Pixel is ${String(scale)} CSS pixels, so a PNG cannot be painted at it.`);
    }
    const url = URL.createObjectURL(new Blob([svgText], { type: 'image/svg+xml' }));
    try {
        const image = new Image();
        image.decoding = 'sync';
        await new Promise<void>((resolve, reject) => {
            image.addEventListener('load', () => { resolve(); }, { once: true });
            image.addEventListener('error', () => { reject(new Error('The chart\'s SVG could not be painted.')); }, { once: true });
            image.src = url;
        });
        const canvas = document.createElement('canvas');
        canvas.width = Math.round(width * scale);
        canvas.height = Math.round(height * scale);
        const context = canvas.getContext('2d');
        if (context === null) {
            throw new Error('The browser gave no canvas to paint the chart\'s PNG on.');
        }
        context.scale(canvas.width / width, canvas.height / height);
        context.drawImage(image, 0, 0, width, height);
        return await new Promise<Blob>((resolve, reject) => {
            canvas.toBlob(blob => {
                if (blob === null) {
                    reject(new Error('The chart\'s PNG could not be encoded.'));
                } else {
                    resolve(blob);
                }
            }, 'image/png');
        });
    } finally {
        URL.revokeObjectURL(url);
    }
}
