// The Script Core's glyph tables are the C# core's (ADR-0016; ticket 83; ADR-0350): both are written by
// tests/GlyphWidths/tables.mjs from the same records, and its --check fails when either differs from what
// it would write, so a hand edit to one table cannot leave the two cores charging a glyph differently.
// The checksum cases in tests/cases/columns/grid-metrics.json say each core reads its table whole.
import { execFileSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { expect, it } from 'vitest';
import { CellTextMetrics } from '../src/columns/cell-text-metrics';

const generator = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../tests/GlyphWidths/tables.mjs');

it('ADR-0016 / ADR-0350: both cores\' glyph tables are what the generator writes from the records', () => {
    expect(execFileSync(process.execPath, [generator, '--check'], { encoding: 'utf8' })).toContain('every table as written');
});

it('ADR-0016: the generator charges the em to the code points the core charges it to, block for block', () => {
    // classes.mjs's isFullWidth leaves the em's glyphs out of @ex-grid/mantine's other class; a block it missed or
    // added would charge a glyph under the width it paints.
    const classes = path.resolve(path.dirname(generator), 'classes.mjs');
    const script = `import(${JSON.stringify(pathToFileURL(classes).href)}).then(m => { const runs = []; `
        + 'for (let c = 0; c <= 0x3FFFF; c++) if (m.isFullWidth(c) !== (runs.length % 2 === 1)) runs.push(c); '
        + 'process.stdout.write(JSON.stringify(runs)); })';
    const theirs: unknown = JSON.parse(execFileSync(process.execPath, ['--input-type=module', '-e', script], { encoding: 'utf8' }));
    const metrics = new CellTextMetrics({ digitWidthPx: 2, cellHorizontalPaddingPx: 0, wideWidthPx: 3, narrowWidthPx: 1, fullWidthPx: 100 });
    const ours: number[] = [];
    for (let c = 0; c <= 0x3FFFF; c++) {
        if (c >= 0xD800 && c <= 0xDFFF) {
            if (ours.length % 2 === 1) ours.push(c);
            continue;
        }
        if ((metrics.textWidthPx(String.fromCodePoint(c)) === 100) !== (ours.length % 2 === 1)) ours.push(c);
    }
    expect(theirs).toEqual(ours);
});
