// Layer 1: what the class vocabularies promise beyond the shared cases (tests/cases/paint): every
// combination is a class attribute of its own, so no cell or row can be painted as another.
import { describe, expect, it } from 'vitest';
import { cellClasses, type CellAlign, type CellState, type CellTone, type MatchMark } from '../src/paint/cell-classes';
import { rowClasses, type RowKind } from '../src/paint/row-classes';

const states: readonly CellState[] = ['normal', 'stale', 'missing', 'error', 'modified'];
const tones: readonly CellTone[] = ['none', 'positive', 'negative'];
const aligns: readonly CellAlign[] = ['auto', 'left', 'center', 'right'];
const marks: readonly MatchMark[] = ['none', 'match', 'current'];
const truths = [false, true];

describe('cell classes', () => {
    const all = marks.flatMap(mark => truths.flatMap(changed => tones.flatMap(tone => states.flatMap(state => aligns.flatMap(align =>
        truths.flatMap(numeric => truths.map(pinned => ({ changed, mark, classes: cellClasses({ numeric, pinned, state, align, tone, changed, mark }) }))))))));

    it('ADR-0006 / ADR-0068 / ADR-0332: every one of the 1440 combinations is a class attribute of its own', () => {
        expect(all).toHaveLength(1440);
        expect(new Set(all.map(each => each.classes)).size).toBe(1440);
    });

    it('ADR-0068 / ADR-0332: the Change Highlight and the Match Mark show exactly where they were asked for', () => {
        for (const { changed, mark, classes } of all) {
            const names = classes.split(' ');
            expect(names.includes('ex-changed'), classes).toBe(changed);
            expect(names.includes('ex-match-mark'), classes).toBe(mark !== 'none');
            expect(names.includes('ex-match-mark-current'), classes).toBe(mark === 'current');
        }
    });
});

describe('row classes', () => {
    it('ADR-0024 / ADR-0038: every one of the twelve combinations is a class attribute of its own', () => {
        const kinds: readonly RowKind[] = ['detail', 'group', 'total'];
        const all = kinds.flatMap(kind => truths.flatMap(placeholder => truths.map(stripe => rowClasses({ kind, placeholder, stripe }))));
        expect(new Set(all).size).toBe(12);
    });

    it('ADR-0024: a kind nobody declared is refused, never painted as a detail row', () => {
        expect(() => rowClasses({ kind: 'subtotal' as RowKind, placeholder: false })).toThrow(/kind is one of detail, group, total/);
    });
});
