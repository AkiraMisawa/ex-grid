// Layer 1: what an Auto column's header needs is held apart from what its values taught it (ADR-0500, ADR-0016). The
// Script Core's alone: the grow-only cases both runners read are tests/cases/columns/auto-width.json.
import { describe, expect, it } from 'vitest';
import { AutoWidth } from '../src/columns/auto-width';
import { ColumnWidth, ColumnWidthSpec } from '../src/columns/column-width';

const spec = (): ColumnWidthSpec => new ColumnWidthSpec({ width: ColumnWidth.auto, minWidthPx: 40, maxWidthPx: 400 });

describe('an Auto width under a header', () => {
    it('ADR-0500: another header replaces what the header needed, and comes down only as far as the values let it', () => {
        const headerDecided = AutoWidth.of(spec()).underHeader(101.25).observe(45);
        expect(headerDecided.currentPx).toBe(101.25);
        expect(headerDecided.underHeader(73.25).currentPx).toBe(73.25);
        expect(headerDecided.underHeader(30).currentPx).toBe(45);

        const valueDecided = AutoWidth.of(spec()).underHeader(101.25).observe(180);
        expect(valueDecided.underHeader(73.25).currentPx).toBe(180);
        // The way back is the width it had: nothing depends on the order the headers came in.
        expect(valueDecided.underHeader(73.25).underHeader(101.25).equals(valueDecided)).toBe(true);
        expect(headerDecided.underHeader(73.25).underHeader(101.25).equals(headerDecided)).toBe(true);
    });

    it('ADR-0016: a header\'s need is clamped to the bounds as an observation is, an observation still only grows, and nonsense is refused', () => {
        expect(AutoWidth.of(spec()).underHeader(900).currentPx).toBe(400);
        expect(AutoWidth.of(spec()).underHeader(10).currentPx).toBe(40);
        expect(AutoWidth.of(spec()).underHeader(73.25).observe(120).observe(60).currentPx).toBe(120);
        expect(() => AutoWidth.of(spec()).underHeader(Number.NaN)).toThrow(RangeError);
        expect(() => AutoWidth.of(spec()).underHeader(-1)).toThrow(RangeError);
    });
});
