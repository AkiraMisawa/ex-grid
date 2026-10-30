// @vitest-environment happy-dom
// No pixel is only a stylesheet value (ADR-0027/0028; UX-1). Every length literal the core stylesheet writes is one
// of three things, and this test says which:
// - the fallback of a Geometry Token the grid writes inline on its root, and then it is the number the grid writes
//   under the core default, Compact, so a fallback is never a second opinion;
// - the default of a Visual Token, named in visualTokenDefaults with its reason;
// - a bare literal, named by its rule, property and value in bareLengths with its reason: something painted that no
//   arithmetic reads.
// Comments are not CSS and are not read. A length is a number with a length unit; zero, a percentage and a
// resolution (dppx, in a media query's prelude) are not lengths. Ported from the C# core's StylesheetLengthTests as
// the C# core left (ADR-0370 section 6, step 3): the root the lengths are held to is the Script Core's.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { columns, mount, painted, trades } from './grid-harness';

const source = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../ExGrid/Assets/ex-grid.css');

const lengthPattern = /(?<![\w.#-])-?(?:\d+\.?\d*|\.\d+)(?:px|em|rem|ex|ch|lh|rlh|vh|vw|vmin|vmax|vi|vb|svh|lvh|dvh|svw|lvw|dvw|pt|pc|cm|mm|in|q)\b/giu;

const outline = 'an outline is painted outside layout and moves no box the arithmetic reads (ADR-0028: painted, never laid out)';
const rule = 'a rule\'s width is a Visual Token: a gradient or a shadow, painted, never laid out (ADR-0028/0029)';
const devicePixelSteps = 'the --ex-dp fallback table, one Device Pixel per resolution step until the browser tells it (ADR-0090)';
const popover = 'spacing inside a popover or menu, which stands outside the row flow and whose box no arithmetic reads (ADR-0018/0034)';
const strip = 'the pager\'s and status line\'s padding: a strip outside the scroller, whose height no arithmetic reads (ADR-0015)';
const em = 'a glyph\'s box in the em of the emitted font size (--ex-font-size), so it follows that Geometry Token';
const mark = 'a painted mark on a pseudo-element or a placeholder bar, positioned over its box and laid out nowhere (ADR-0006/0013)';
const visuallyHidden = 'the live region\'s visually-hidden clip, present to assistive technology only (ADR-0033)';
const scrollbar = 'the grid\'s own scrollbar, which the browser lays out and reports as the Scrollbar Gutter (ADR-0013/0021/0029)';
const referenceInset = 'a Reference outline\'s painted inset inside a box the grid places (ADR-0058)';

/** The Visual Tokens whose default is a length, as `token literal`, with the reason it is appearance and not geometry. */
const visualTokenDefaults = new Map<string, string>([
    ['--ex-rule-width 1px', rule],
    ['--ex-rule-dp 1px', rule],
    ['--ex-editor-outline 2px', outline],
    ['--ex-grid-focus-outline 1px', outline],
    ['--ex-grid-focus-outline 2px', outline],
    ['--ex-size-tip-outline 1px', outline],
    ['--ex-scrollbar-width 12px', scrollbar],
    ['--ex-pasting-bar-height 3px', 'the bar a paste under way paints over the root\'s top edge, absolutely placed: a painted mark, never a box the arithmetic reads (ADR-0240 section 12)'],
    ['--ex-dp 1px', 'the Device Pixel\'s own default before the browser\'s resolution is read: a painted line\'s width and inset, never a box the arithmetic reads (ADR-0090)'],
]);

/** The bare length literals, each as `selectors | property | literal`, with the reason it is no geometry.
 * `.ex-resize-grip`'s width is not here: it comes from Grid Metrics (UX-1). */
const bareLengths = new Map<string, string>([
    ['.ex-grid:focus-visible, .ex-grid[data-ex-focus-visible] | outline-offset | 1px', outline],
    ['.ex-formula-bar > .ex-editor:focus, .ex-formula-bar > .ex-editor:focus-within | outline-offset | -2px', outline],
    ['.ex-focus, .ex-range, .ex-point, .ex-action-chosen | outline-width | 2px', outline],
    ['.ex-focus, .ex-range, .ex-point, .ex-action-chosen | outline-offset | -2px', outline],
    ['.ex-fill-target | outline | 1px', outline],
    ['.ex-fill-target | outline-offset | -1px', outline],
    ['.ex-reference-outline | outline | 2px', outline],
    ['.ex-reference-outline | outline-offset | -2px', outline],
    ['.ex-point.ex-point-on-reference | outline-offset | -4px', outline],
    ['.ex-point-dashes | outline | 2px', outline],
    ['.ex-point-dashes | outline-offset | -4px', outline],
    ['.ex-message | outline | 1px', outline],
    ['[data-ex-notice] | outline | 1px', outline],
    ['[data-ex-notice] | outline-offset | -1px', outline],
    ['.ex-size-tip | outline-offset | -1px', outline],
    ['.ex-editor | outline-offset | -1px', outline],
    ['.ex-grid:not(:focus-within) .ex-viewport .ex-editor | outline-width | 1px', outline],
    ['.ex-key-field.ex-key-field-composing | outline-offset | -1px', outline],
    // The forced-colors block restates states and bands as outlines (ADR-0027).
    ['.ex-cell.ex-changed | outline | 2px', outline],
    ['.ex-cell.ex-changed | outline-offset | -2px', outline],
    ['.ex-cell.ex-match-mark::before | inset | 3px', mark],
    ['.ex-cell.ex-match-mark::before | border | 2px', mark],
    ['.ex-header-cell.ex-match-mark::before, .ex-header-group.ex-match-mark::before | inset | 3px', mark],
    ['.ex-header-cell.ex-match-mark::before, .ex-header-group.ex-match-mark::before | border | 2px', mark],
    ['.ex-cell.ex-state-missing | outline | 1px', outline],
    ['.ex-cell.ex-state-missing | outline-offset | -2px', outline],
    ['.ex-cell.ex-state-modified | outline | 1px', outline],
    ['.ex-cell.ex-state-modified | outline-offset | -2px', outline],
    ['.ex-focus-row | outline | 1px', outline],
    ['.ex-focus-row | outline-offset | -1px', outline],
    ['.ex-hover-row | outline | 1px', outline],
    ['.ex-hover-row | outline-offset | -1px', outline],

    ['.ex-row-total, .ex-row-total .ex-cell | --ex-tint | 1px', rule],

    ['.ex-grid | --ex-dp | 1px', devicePixelSteps],
    ['.ex-grid | --ex-dp | 0.8px', devicePixelSteps],
    ['.ex-grid | --ex-dp | 0.5px', devicePixelSteps],
    ['.ex-grid | --ex-dp | 0.4px', devicePixelSteps],

    ['.ex-popover | padding | 4px', popover],
    ['.ex-popover | gap | 4px', popover],
    ['.ex-message | padding | 4px', popover],
    ['.ex-popover-filter | gap | 4px', popover],
    ['.ex-popover-filter | padding-top | 4px', popover],
    ['.ex-popover button | padding | 2px', popover],
    ['.ex-popover-list | max-height | 240px', popover],
    ['.ex-popover-list | min-height | 48px', popover],
    ['.ex-popover-actions | gap | 4px', popover],
    ['.ex-popover-join | gap | 12px', popover],
    ['.ex-popover-find-body | gap | 4px', popover],
    ['.ex-find-outcome | min-height | 1.2em', popover],
    ['.ex-select-all::before | width | 1.2em', popover],
    ['.ex-summary-menu | min-width | 12em', popover],
    ['.ex-summary-menu-item | padding | 2px', popover],
    ['.ex-summary-menu-tick | width | 1.4em', popover],

    ['.ex-pager, .ex-status | padding | 4px', strip],
    ['.ex-summary | min-height | 1lh', strip],

    ['.ex-mark | width | 1em', em],
    ['.ex-mark | height | 1em', em],

    ['.ex-header-group-toggle::before | width | 1em', em],
    ['.ex-header-group-toggle::before | height | 1em', em],
    ['.ex-hidden-mark | width | 6px', mark],
    ['.ex-hidden-mark | height | 6px', mark],
    ['.ex-cell.ex-state-modified::after | width | 6px', mark],
    ['.ex-cell.ex-state-modified::after | height | 6px', mark],
    ['.ex-placeholder::after | border-radius | 2px', mark],

    ['.ex-announce | width | 1px', visuallyHidden],
    ['.ex-announce | height | 1px', visuallyHidden],
    ['.ex-announce | margin | -1px', visuallyHidden],

    ['.ex-scroller::-webkit-scrollbar-thumb | border | 2px', scrollbar],

    ['.ex-reference-outline | padding | 3px', referenceInset],
]);

interface Found {
    readonly selectors: string;
    readonly property: string;
    readonly literal: string;
    readonly fallbackOf: string | undefined;
}

const nameOf = (found: Found): string =>
    `${found.selectors} { ${found.property}: … ${found.literal} … }${found.fallbackOf === undefined ? '' : ` (fallback of ${found.fallbackOf})`}`;

/** The token of the innermost `var()` whose fallback holds the position, if any. */
function fallbackOf(value: string, position: number): string | undefined {
    let innermost: string | undefined;
    for (const read of value.matchAll(/var\(\s*(?<token>--[\w-]+)\s*,/gu)) {
        let depth = 1;
        let end = read.index + read[0].length;
        for (; end < value.length && depth > 0; end++) {
            if (value[end] === '(') depth++;
            else if (value[end] === ')') depth--;
        }
        if (position >= read.index + read[0].length && position < end) innermost = read.groups?.['token'];
    }
    return innermost;
}

/** Every non-zero length in a stylesheet's declarations, with the token whose fallback it stands in, if any. */
function lengths(css: string): Found[] {
    const found: Found[] = [];
    for (const ruleMatch of css.matchAll(/(?<selectors>[^{}]+)\{(?<body>[^{}]*)\}/gu)) {
        const selectors = (ruleMatch.groups?.['selectors'] ?? '').replace(/\s+/gu, ' ').trim(); // oxlint-disable-line ex-grid/no-stand-in-default -- the pattern's groups are always set when it matches
        for (const declaration of (ruleMatch.groups?.['body'] ?? '').split(';')) { // oxlint-disable-line ex-grid/no-stand-in-default -- the pattern's groups are always set when it matches
            const colon = declaration.indexOf(':');
            if (colon < 0) continue;
            const property = declaration.slice(0, colon).trim();
            const flat = declaration.slice(colon + 1).trim().replace(/\s+/gu, ' ');
            for (const length of flat.matchAll(lengthPattern)) {
                if (Number.parseFloat(length[0]) === 0) continue;
                found.push({ selectors, property, literal: length[0], fallbackOf: fallbackOf(flat, length.index) });
            }
        }
    }
    return found;
}

/** The core stylesheet's source, its comments taken out. */
const coreStylesheet = (): string => fs.readFileSync(source, 'utf8').replace(/\/\*[\s\S]*?\*\//gu, '');

/** The Geometry Tokens an untouched grid writes inline on its root, Compact's. */
async function emittedGeometryTokens(): Promise<Map<string, string>> {
    const { root } = mount({ columns, rows: trades(5), viewportHeight: 200 });
    await painted();
    return new Map([...(root.getAttribute('style') ?? '').matchAll(/(?<token>--ex-[\w-]+)\s*:\s*(?<value>[^;]+)/gu)] // oxlint-disable-line ex-grid/no-stand-in-default -- a root with no style emits no token, which the checks below then refuse by name
        .map(token => [token.groups?.['token'] ?? '', (token.groups?.['value'] ?? '').trim()])); // oxlint-disable-line ex-grid/no-stand-in-default -- the pattern's groups are always set when it matches
}

describe('the core stylesheet\'s lengths', () => {
    it('ADR-0027/0028, UX-1: every length in ex-grid.css is a Geometry Token\'s own fallback, a Visual Token\'s default, or a named painted value', async () => {
        const css = coreStylesheet();
        const found = lengths(css);
        const emitted = await emittedGeometryTokens();

        // Nothing escapes the reading: a length outside a declaration, in a selector or an at-rule's prelude, would
        // be counted here and not above.
        expect([...css.replace(/\s+/gu, ' ').matchAll(lengthPattern)].filter(m => !/^-?[0.]+[a-z]+$/iu.test(m[0]))).toHaveLength(found.length);

        const offenders: string[] = [];
        for (const length of found) {
            const token = length.fallbackOf;
            const value = token === undefined ? undefined : emitted.get(token);
            if (token !== undefined && value !== undefined) {
                // A Geometry Token's fallback stands only where the root's inline value does not reach; it may only
                // repeat that value, never hold a number of its own.
                if (value !== length.literal) offenders.push(`${nameOf(length)}: the root emits ${token}: ${value}`);
            } else if (token !== undefined) {
                if (!visualTokenDefaults.has(`${token} ${length.literal}`)) offenders.push(`${nameOf(length)}: a length default of a token neither emitted by the grid nor a listed Visual Token`);
            } else if (!bareLengths.has(`${length.selectors} | ${length.property} | ${length.literal}`)) {
                offenders.push(`${nameOf(length)}: a bare length — geometry comes from Grid Metrics (ADR-0027/0028)`);
            }
        }
        expect(offenders).toEqual([]);

        // An allowance nothing uses any more is removed, so the list stays what the sheet holds.
        expect([...bareLengths.keys()].filter(key => !found.some(l => l.fallbackOf === undefined && `${l.selectors} | ${l.property} | ${l.literal}` === key))).toEqual([]);
        expect([...visualTokenDefaults.keys()].filter(key => !found.some(l => `${l.fallbackOf} ${l.literal}` === key))).toEqual([]);
    });

    it('ADR-0027, UX-1: the check above refuses the grip\'s old bare width, and a geometry fallback that differs from what the grid emits', async () => {
        const emitted = await emittedGeometryTokens();
        expect(emitted.get('--ex-resize-grip-width')).toBe('5px');
        expect(emitted.get('--ex-column-guide-width')).toBe('2px');

        const bare = lengths('.ex-resize-grip { position: absolute; width: 5px; }');
        expect(bare).toHaveLength(1);
        expect(bare[0]?.fallbackOf).toBeUndefined();
        expect(bareLengths.has('.ex-resize-grip | width | 5px')).toBe(false);

        const fallback = lengths('.ex-resize-grip { width: var(--ex-resize-grip-width, 6px); }');
        expect(fallback.map(l => [l.fallbackOf, l.literal])).toEqual([['--ex-resize-grip-width', '6px']]);
        expect(emitted.get('--ex-resize-grip-width')).not.toBe('6px');

        // A length in a nested function inside the fallback is still that token's fallback.
        expect(lengths('.x { top: calc(var(--ex-outline-width, calc(2px + 0px)) * 2); }').map(l => l.fallbackOf)).toEqual(['--ex-outline-width']);
    });
});
