// Layer 1: where the clipboard code meets what an untyped caller or the browser hands it, which C#'s types
// rule out and the shared cases therefore cannot say (CONVENTIONS.md, "Check at the boundary").
import { describe, expect, it } from 'vitest';
import { assembleCopy } from '../src/clipboard/clipboard-data';
import { parseClipboard, parseClipboardBlock } from '../src/clipboard/clipboard-parse';
import { planCopy } from '../src/clipboard/clipboard-rules';
import { Selection } from '../src/selection/selection';

const grid = { rowCount: 10, columnCount: 10 };
const decision = planCopy(Selection.empty.click({ row: 0, column: 0 }, grid));
if (decision.refused) throw new Error('A one-cell copy is approved.');
const { plan } = decision;
const shown = () => 'shown';

describe('assembleCopy', () => {
    it('ADR-0005: a provider that answers anything but text is refused, never copied as what String makes of it', () => {
        expect(() => assembleCopy(plan, { displayAt: () => 12 as never, rawAt: shown })).toThrow(TypeError);
        expect(() => assembleCopy(plan, { displayAt: shown, rawAt: () => undefined as never })).toThrow(TypeError);
        expect(() => assembleCopy(plan, { displayAt: shown, rawAt: shown, headerAt: () => null as never })).toThrow(TypeError);
        expect(() => assembleCopy(plan, { displayAt: shown, rawAt: shown, headerAt: shown, headerRawAt: () => 1 as never })).toThrow(TypeError);
    });
});

describe('parseClipboardBlock', () => {
    it('ADR-0014: a flavour is text or absent', () => {
        expect(() => parseClipboardBlock(42 as never, undefined)).toThrow(TypeError);
        expect(() => parseClipboard(undefined, null as never)).toThrow(TypeError);
    });

    it('ADR-0014: a paste of more lines than a call takes arguments is read whole', () => {
        const lines = 300_000;
        const block = parseClipboardBlock(undefined, 'a\tb\r\n'.repeat(lines));
        expect(block?.values.length).toBe(lines);
        expect(block?.values[lines - 1]).toEqual(['a', 'b']);
    });
});
