// Layer 1: what an untyped caller can hand the editor's text and the Change Highlight and C#'s types cannot,
// refused where it arrives rather than painted (CONVENTIONS.md, "Check at the boundary").
import { describe, expect, it } from 'vitest';
import { changeHighlightShowingUntil } from '../src/cells/change-highlight';
import { EditorHint } from '../src/cells/editor-text';
import { ReferenceColouring, type EditorReference } from '../src/cells/reference-colouring';
import { rowMarkHeaderPress, type RowMarkHeaderState } from '../src/rows/row-marks';

const range = { topRow: 0, leftColumn: 0, rowCount: 1, columnCount: 1 };
const answered = (reference: object): readonly EditorReference[] => [reference as EditorReference];

describe('a Consumer answer the types would have refused', () => {
    it('ADR-0051: a hint\'s emphasis is a whole-number span, never one that splits the hint where nobody said', () => {
        expect(() => new EditorHint({ text: 'SUM(number1)', emphasisStart: 4, emphasisLength: Number.NaN })).toThrow(RangeError);
        expect(() => new EditorHint({ text: 'SUM(number1)', emphasisStart: 1.5, emphasisLength: 2 })).toThrow(RangeError);
    });

    it('ADR-0057: each Reference the References function answers is checked as a Reference is made', () => {
        expect(() => ReferenceColouring.of('=A1', answered({ start: -1, length: 2, range, key: undefined }))).toThrow(RangeError);
        expect(() => ReferenceColouring.of('=A1', answered({ start: Number.NaN, length: 2, range, key: undefined }))).toThrow(RangeError);
        expect(() => ReferenceColouring.of('=A1', answered({ start: 1, length: 1 }))).toThrow(TypeError);
        expect(() => ReferenceColouring.of('=A1', answered({ start: 1, length: 1, range, key: 'k' }))).toThrow(TypeError);
    });

    it('ADR-0068: a clock reading that is no instant is refused, not read as every mark over', () => {
        expect(() => changeHighlightShowingUntil(0, 1000, Number.NaN)).toThrow(RangeError);
        expect(() => changeHighlightShowingUntil(undefined, 1000, Number.POSITIVE_INFINITY)).toThrow(RangeError);
    });

    it('ADR-0043: a header state nobody declared is refused, not read as some', () => {
        expect(() => rowMarkHeaderPress('All' as RowMarkHeaderState)).toThrow(RangeError);
    });
});
