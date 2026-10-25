// The Script Core's glyph tables are the C# core's (ADR-0016; ticket 83; ADR-0350): both are written by
// tests/GlyphWidths/tables.mjs from the same records, and its --check fails when either differs from what
// it would write, so a hand edit to one table cannot leave the two cores charging a glyph differently.
// The checksum cases in tests/cases/columns/grid-metrics.json say each core reads its table whole.
import { execFileSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { expect, it } from 'vitest';

const generator = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../tests/GlyphWidths/tables.mjs');

it('ADR-0016 / ADR-0350: both cores\' glyph tables are what the generator writes from the records', () => {
    expect(execFileSync(process.execPath, [generator, '--check'], { encoding: 'utf8' })).toContain('every table as written');
});
