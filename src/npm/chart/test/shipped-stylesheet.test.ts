// The stamp the chart renders its stylesheet notice with is the stamp of the stylesheet it draws against,
// and the text a saved SVG carries is the stylesheet as it ships (ADR-0123, ADR-0282): tools/assets/minify.mjs
// writes both, and a source edited without running it fails here by name.
import { createHash } from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { stylesheetName, stylesheetStamp, stylesheetText } from '../src/dom/shipped-stylesheet';

const here = path.dirname(fileURLToPath(import.meta.url));
const source = path.resolve(here, '../../../ExChart/Assets/ex-chart.css');
const shipped = path.resolve(here, '../../../ExChart/wwwroot/ex-chart.min.css');

describe('the shipped stylesheet', () => {
    it('ADR-0123: the stamp is the SHA-256 of the stylesheet\'s source, its line endings read as LF', () => {
        const text = fs.readFileSync(source, 'utf8').replace(/\r\n/g, '\n');
        expect(stylesheetName).toBe(path.basename(source));
        expect(stylesheetStamp, 'run tools/assets/minify.mjs and commit what it writes')
            .toBe(createHash('sha256').update(text, 'utf8').digest('hex'));
    });

    it('ADR-0282: the text a saved SVG carries is the minified stylesheet as it ships', () => {
        const minified = fs.readFileSync(shipped, 'utf8');
        expect(minified.startsWith(stylesheetText), 'run tools/assets/minify.mjs and commit what it writes').toBe(true);
        expect(stylesheetText).toContain(`[data-ex-stamp="${stylesheetStamp}"]`);
    });
});
