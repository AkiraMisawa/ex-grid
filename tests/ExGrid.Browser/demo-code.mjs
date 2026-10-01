import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { expect } from './fixtures.mjs';

// What a demo page shows under "The code" is read from its own source by DemoCode.cs, from the
// source files ExGrid.DemoPages.csproj embeds (ADR-0068: each page states its use case and the API
// it uses, in code an application developer can copy). These read the same regions from the
// repository, so a test can say that what a page shows is what it runs.

const PAGES = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../samples/ExGrid.DemoPages');

/** A region of a page's source, read as DemoCode.cs reads it to show it under "The code": the lines
 *  between `#region The code: name` and the next `#endregion` — in Razor markup, inside `@* … *@` —
 *  less the indentation they share. */
export function codeRegion(file, region) {
    const at = [path.join(PAGES, file), path.join(PAGES, 'Pages', file)].find((p) => fs.existsSync(p));
    if (!at) {
        throw new Error(`${file} is in neither ExGrid.DemoPages nor its Pages`);
    }
    const lines = fs.readFileSync(at, 'utf8').replace(/\r\n?/g, '\n').split('\n');
    const marker = (line) => line.trim().replace(/^@\*\s*(.*?)\s*\*@$/, '$1');
    const start = lines.findIndex((line) => marker(line) === `#region The code: ${region}`);
    const end = lines.findIndex((line, i) => i > start && marker(line).startsWith('#endregion'));
    if (start < 0 || end < 0) {
        throw new Error(`${file} has no region 'The code: ${region}'`);
    }
    const body = lines.slice(start + 1, end);
    const indent = Math.min(...body.filter((line) => line.trim()).map((line) => line.length - line.trimStart().length));
    return body.map((line) => (line.length >= indent ? line.slice(indent) : line.trimStart())).join('\n');
}

/** Every block the page shows under "The code" equals the region of the source it names. Answers
 *  the blocks' text by `file#region`. */
export async function expectCodeIsSource(page) {
    const blocks = await page.locator('.demo-code code').evaluateAll((codes) =>
        codes.map((code) => ({ file: code.dataset.file, region: code.dataset.region, text: code.textContent })));
    expect(blocks.length, 'blocks under "The code"').toBeGreaterThan(0);
    for (const { file, region, text } of blocks) {
        expect(text, `${file}, region ${region}`).toBe(codeRegion(file, region));
    }
    return Object.fromEntries(blocks.map(({ file, region, text }) => [`${file}#${region}`, text]));
}
