// The bands and fills the grid draws in its selection layer lie above the cells' text (ADR-0008, ADR-0029), so every
// colour this stylesheet gives them lets the text through. An opaque one hides the values it lies over and still
// looks like a hover: Mantine's own Table hover shade, gray-1, written as the hover band hid every value in the row
// under the pointer. The value is read as the stylesheet writes it, in each scheme, through the Wrapper's own
// --mantine-ex-* names.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

const source = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../styles.css');
const css = fs.readFileSync(source, 'utf8').replace(/\/\*[\s\S]*?\*\//gu, '');

/** The text `match` captured in group `index`, which its pattern always captures. */
function captured(match: RegExpMatchArray, index: number): string {
    const text = match[index];
    if (text === undefined) throw new Error(`The pattern captured no group ${String(index)} in '${match[0]}'.`);
    return text;
}

/** The declarations of the rule whose selector is exactly `selector`. */
function declarations(selector: string): ReadonlyMap<string, string> {
    for (const match of css.matchAll(/([^{}]+)\{([^{}]*)\}/gu)) {
        if (captured(match, 1).trim() !== selector) continue;
        const body = captured(match, 2);
        return new Map([...body.matchAll(/(--[\w-]+)\s*:\s*([^;]+);/gu)].map(d => [captured(d, 1), captured(d, 2).trim()]));
    }
    throw new Error(`styles.css has no rule '${selector}'.`);
}

const ground = declarations('.mantine-ex-grid');
const schemes = ['light', 'dark'].map(scheme => ({
    scheme,
    names: declarations(`:where([data-mantine-color-scheme='${scheme}']) .mantine-ex-grid`),
}));

/** The tokens painted by a rectangle of the selection layer, above the text. */
const overlayTokens = ['--ex-selection-fill', '--ex-focus-row-fill', '--ex-focus-column-fill', '--ex-row-hover-background'];

/** `value` with the Wrapper's own --mantine-ex-* names replaced by what `names` gives them. */
function resolved(value: string, names: ReadonlyMap<string, string>): string {
    return value.replace(/var\((--mantine-ex-[\w-]+)\)/gu, (_, name: string) => {
        const given = names.get(name);
        if (given === undefined) throw new Error(`${name} is used but not given in this scheme.`);
        return resolved(given, names);
    });
}

/** Whether `value` is written as a colour that lets what is beneath through. */
function translucent(value: string): boolean {
    const mix = /^color-mix\(in srgb, .+ (\d+(?:\.\d+)?)%, transparent\)$/u.exec(value);
    return mix !== null && Number(mix[1]) < 100;
}

describe('the Mantine stylesheet\'s overlay colours (ADR-0029)', () => {
    for (const { scheme, names } of schemes) {
        for (const token of overlayTokens) {
            const value = ground.get(token);
            if (value === undefined) continue;
            it(`${token} lets the text through in the ${scheme} scheme`, () => {
                expect(translucent(resolved(value, names)), `${token}: ${resolved(value, names)}`).toBe(true);
            });
        }
    }

    it('reads the hover band, so a rename does not leave the check with nothing to judge', () => {
        expect(ground.has('--ex-row-hover-background')).toBe(true);
    });
});
