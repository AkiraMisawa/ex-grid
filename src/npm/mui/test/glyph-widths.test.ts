// No text a built-in Number Format paints is charged below its width under the widths MuiExGrid hands its grid
// (ADR-0016's note of 2026-10-01, ADR-0390; principle 1), in any face MUI's default stack resolves to: Roboto,
// Liberation Sans for Helvetica and Arial (the faces macOS and Windows draw those two in have the widths Liberation
// Sans was made to match), DejaVu Sans for sans-serif on Linux, and each platform's system face, which paints what
// Helvetica and Arial lack. The painted widths are tests/GlyphWidths'
// records; GlyphWidthCorpusTests holds the core's defaults and RobotoGlyphWidthCorpusTests the MudBlazor Wrapper's to
// the same records the same way.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import type { GridDensity } from '@ex-grid/core';
import { GridMetrics } from '../../core/src/grid-metrics';
import { muiDefaultFont, muiPresentationDefaults } from '../src/presentation';

interface GlyphWidthRecord {
    readonly face: string;
    readonly glyphs: readonly string[];
    readonly sizes: Readonly<Record<string, { readonly glyphs: Readonly<Record<string, readonly number[]>>; readonly strings: Readonly<Record<string, readonly number[]>> }>>;
}

const folder = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../tests/GlyphWidths');
// The faces the stack resolves to, and the system faces that paint what Helvetica and Arial lack.
const files = fs.readdirSync(folder)
    .filter(name => /^(?:system-ui|roboto|dejavu-sans|liberation-sans)\..*\.json$/u.test(name))
    .toSorted();
// The JSON as its file holds it: measure.mjs writes it, and the C# reader takes the same shape.
const corpus = JSON.parse(fs.readFileSync(path.join(folder, 'corpus.json'), 'utf8')) as readonly string[];
const recordOf = (file: string): GlyphWidthRecord =>
    JSON.parse(fs.readFileSync(path.join(folder, file), 'utf8')) as GlyphWidthRecord;
const weights = ['400', '500', '600', '700'] as const;
const densities: readonly GridDensity[] = ['compact', 'excel', 'standard', 'comfortable'];
const defaults = muiPresentationDefaults(muiDefaultFont);

/** Each text with its painted width in pixels, the record's 64ths read at the same position. */
function paired(texts: readonly string[], widths: readonly number[]): { readonly text: string; readonly painted: number }[] {
    if (texts.length !== widths.length) throw new Error(`${widths.length} widths for ${texts.length} texts.`);
    return texts.map((text, at) => {
        const width = widths.at(at);
        if (width === undefined) throw new Error(`No width at ${at}.`);
        return { text, painted: width / 64 };
    });
}

function widthsOf(record: GlyphWidthRecord, size: number, weight: string, kind: 'glyphs' | 'strings'): readonly number[] {
    const widths = record.sizes[String(size)]?.[kind][weight];
    if (widths === undefined) throw new Error(`${record.face} holds no ${kind} at ${size}px, weight ${weight}.`);
    return widths;
}

describe('the widths MuiExGrid hands its grid (ADR-0016, ADR-0390)', () => {
    it('ADR-0016: every face of the stack is measured, on each platform that resolves to it', () => {
        expect(files).toEqual(expect.arrayContaining([
            'dejavu-sans.macos.json', 'liberation-sans.linux.json', 'roboto.macos.json', 'roboto.windows.chrome.json',
            'roboto.windows.msedge.json', 'system-ui.macos.json', 'system-ui.windows.chrome.json', 'system-ui.windows-ja.chrome.json',
        ]));
    });

    for (const file of files) {
        const record = recordOf(file);
        for (const density of densities) {
            const metrics = GridMetrics.resolve({ density, defaults });
            for (const weight of weights) {
                const charged = weight === '700' ? metrics.cellMetrics.bold : metrics.cellMetrics;
                const size = metrics.fontSizePx;
                it(`principle 1: in ${file}, ${density}, weight ${weight}, no corpus string or glyph paints wider than it is charged`, () => {
                    const strings = widthsOf(record, size, weight, 'strings');
                    const glyphs = widthsOf(record, size, weight, 'glyphs');
                    const cut = [...paired(corpus, strings), ...paired(record.glyphs, glyphs)]
                        .filter(each => !(charged.textWidthPx(each.text) >= each.painted));
                    expect(cut.slice(0, 12).map(each => `"${each.text}" paints ${each.painted} and is charged ${charged.textWidthPx(each.text)}`)).toEqual([]);
                });
            }
        }
    }
});
