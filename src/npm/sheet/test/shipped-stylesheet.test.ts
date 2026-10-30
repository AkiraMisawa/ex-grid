// The stamp ExSheet's Script Core renders its stylesheet notice with is the stamp of the stylesheet it draws
// against (ADR-0123, ADR-0370 section 1): tools/assets/minify.mjs writes it, and a source edited without
// running it fails here by name, as ShippedAssetTests holds the package to ShippedAssets.g.cs.
import { createHash } from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { stylesheetName, stylesheetStamp } from '../src/shipped-stylesheet';

const source = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../ExSheet/Assets/ex-sheet.css');

describe('the shipped stylesheet', () => {
    it('ADR-0123: the stamp is the SHA-256 of the stylesheet\'s source, its line endings read as LF', () => {
        const text = fs.readFileSync(source, 'utf8').replace(/\r\n/g, '\n');
        expect(stylesheetName).toBe(path.basename(source));
        expect(stylesheetStamp, 'run tools/assets/minify.mjs and commit what it writes')
            .toBe(createHash('sha256').update(text, 'utf8').digest('hex'));
    });
});
