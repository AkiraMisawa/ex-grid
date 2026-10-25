// What the shared cases (tests/cases/columns/header-group*.json) cannot say: a reference's identity, and
// the whole numbers C# says with its types (CONVENTIONS.md, "Check at the boundary").
import { describe, expect, it } from 'vitest';
import { HeaderGroup, HeaderGroupLayout } from '../src/columns/header-groups';

const names = ['TradeId', 'CvaBefore', 'CvaAfter', 'CvaDiff', 'FvaBefore', 'FvaAfter'];

describe('a Header Group resolved', () => {
    it('ADR-0032 / HG-15: the declaration survives resolution unchanged, so membership is gesture-proof', () => {
        const declared = ['CvaDiff', 'CvaBefore', 'CvaAfter'];
        const declaration = new HeaderGroup({ label: 'CVA', columns: declared });

        const layout = HeaderGroupLayout.resolve({ groups: [declaration], columnNames: names, pinnedCount: 0 });

        expect(layout.groups[0]?.group).toBe(declaration);
        expect(declaration.columns).toEqual(['CvaDiff', 'CvaBefore', 'CvaAfter']);
        declared.reverse();
        expect(declaration.columns, 'the declaration is a copy of the list it was given').toEqual(['CvaDiff', 'CvaBefore', 'CvaAfter']);
    });

    it('ADR-0032: no declarations resolve to the one layout with no tiers', () => {
        expect(HeaderGroupLayout.resolve({ groups: [], columnNames: names, pinnedCount: 0 })).toBe(HeaderGroupLayout.empty);
    });
});

describe('a whole number where C# takes an int', () => {
    it.each([1.5, Number.NaN, Number.POSITIVE_INFINITY])('ADR-0032: a tier of %s is refused', tier => {
        expect(() => new HeaderGroup({ label: 'X', columns: ['A'], tier })).toThrow(RangeError);
        expect(() => new HeaderGroup({ label: 'X', columns: ['A'], tier: 2, tierSpan: tier })).toThrow(RangeError);
    });

    it('ADR-0032: a pinned count that is not whole is refused', () => {
        expect(() => HeaderGroupLayout.resolve({ groups: [], columnNames: names, pinnedCount: 0.5 })).toThrow(RangeError);
        expect(() => HeaderGroupLayout.empty.allowsPinnedCount(Number.NaN)).toThrow(RangeError);
    });

    it('ADR-0032: a leaf is asked about by a whole column, not a negative one', () => {
        const layout = HeaderGroupLayout.resolve({ groups: [new HeaderGroup({ label: 'CVA', columns: ['CvaBefore'] })], columnNames: names, pinnedCount: 0 });
        expect(() => layout.leafTierSpanOf(1.5)).toThrow(RangeError);
        expect(() => layout.leafTierSpanOf(-1)).toThrow(RangeError);
    });
});

describe('what only an untyped caller can pass', () => {
    it('ADR-0032: a member or a label that is not text is refused at construction, as C# refuses null', () => {
        expect(() => new HeaderGroup({ label: 'CVA', columns: ['A', null as unknown as string] })).toThrow(TypeError);
        expect(() => new HeaderGroup({ label: undefined as unknown as string, columns: ['A'] })).toThrow(TypeError);
        expect(() => new HeaderGroup({ label: 'CVA', columns: 'A' as unknown as string[] })).toThrow(TypeError);
    });
});
