// Properties of the whole key table, which the shared cases (tests/cases/keys/) state key by key.
import { describe, expect, it } from 'vitest';
import { canonicalKey, declareKeys, keysTakenFor, resolveKey, takenOnEveryGrid } from '../src/keys/grid-keys';

// Split the canonical form back into fields, the way the listener reads a press. A key in the set that
// resolved to nothing would be taken from the browser and then do nothing at all.
function resolvesToSomething(canonical: string): boolean {
    const key = canonical.endsWith('+ ') ? ' ' : canonical.slice(canonical.lastIndexOf('+') + 1);
    const press = {
        key, ctrl: canonical.includes('Control+'), shift: canonical.includes('Shift+'), alt: canonical.includes('Alt+'),
        meta: false, metaIsPrimary: false,
    };
    return canonicalKey(press) === canonical && resolveKey(press).kind !== 'none';
}

describe('the key table as a whole', () => {
    it('ADR-0010: every key taken on every grid resolves to something', () => {
        expect(takenOnEveryGrid.length).toBeGreaterThan(0);
        expect(takenOnEveryGrid.filter(key => !resolvesToSomething(key))).toEqual([]);
    });

    it('ADR-0010: the widest set a grid can take resolves key by key, and holds every key taken everywhere', () => {
        const everything = keysTakenFor({ canEdit: true, canUndo: true, canRedo: true });
        expect(everything.filter(key => !resolvesToSomething(key))).toEqual([]);
        expect(takenOnEveryGrid.filter(key => !everything.includes(key))).toEqual([]);
    });

    it('ADR-0050 item 14: a declared key is refused when it is not text, which only an untyped caller can pass', () => {
        expect(() => declareKeys([7 as unknown as string])).toThrow(TypeError);
    });
});
