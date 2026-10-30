// Layer 1: the Change Highlight's rule (ADR-0068), in the Script Core's milliseconds. The C# core's cases
// (dotnet/tests/ExGrid.Tests/ChangeHighlightRulesTests.cs) are about DateTimeOffset, its offsets and its last
// instant, which an epoch number does not have, so they are transcribed here rather than shared.
import { describe, expect, it } from 'vitest';
import { changeHighlightEnd, changeHighlightShowingUntil, defaultChangeHighlightMs } from '../src/cells/change-highlight';

const changedAt = Date.UTC(2026, 8, 30, 9, 0, 0);
const second = 1000;

describe('the Change Highlight', () => {
    it('ADR-0068: a mark lasts one second unless the Consumer says otherwise', () => {
        expect(defaultChangeHighlightMs).toBe(1000);
    });

    it('ADR-0068: a cell is marked while the current time is before its change time plus the duration', () => {
        expect(changeHighlightShowingUntil(changedAt, second, changedAt)).toBe(changedAt + second);
        expect(changeHighlightShowingUntil(changedAt, second, changedAt + second - 1)).toBe(changedAt + second);
        // The end itself is already unmarked, and so is everything after it.
        expect(changeHighlightShowingUntil(changedAt, second, changedAt + second)).toBeUndefined();
        expect(changeHighlightShowingUntil(changedAt, second, changedAt + 86_400_000)).toBeUndefined();
    });

    it('ADR-0068: a cell nobody said changed is not marked', () => {
        expect(changeHighlightShowingUntil(undefined, second, changedAt)).toBeUndefined();
    });

    it('ADR-0068: a change time still to come is taken as given, and marked until it plus the duration', () => {
        const ahead = changedAt + 5 * second;
        expect(changeHighlightShowingUntil(ahead, second, changedAt)).toBe(ahead + second);
    });

    it('ADR-0068: a zero duration marks only a change still to come', () => {
        expect(changeHighlightShowingUntil(changedAt, 0, changedAt)).toBeUndefined();
        expect(changeHighlightShowingUntil(changedAt, 0, changedAt - second)).toBe(changedAt);
    });

    it('ADR-0068: a mark cannot end before the change it marks, and a time that is no instant is refused', () => {
        expect(() => changeHighlightEnd(changedAt, -second)).toThrow(RangeError);
        expect(() => changeHighlightEnd(changedAt, Number.NaN)).toThrow(RangeError);
        expect(() => changeHighlightEnd(Number.NaN, second)).toThrow(RangeError);
    });

    it('ADR-0068: a mark of no end lasts for good, never an overflow thrown out of a paint', () => {
        expect(changeHighlightEnd(changedAt, Number.POSITIVE_INFINITY)).toBe(Number.POSITIVE_INFINITY);
        expect(changeHighlightShowingUntil(changedAt, Number.POSITIVE_INFINITY, changedAt + 86_400_000)).toBe(Number.POSITIVE_INFINITY);
    });
});
