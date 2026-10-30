// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): the Range Request Margin (ADR-0183). At M, the grid asks its source for the rows on
// screen as soon as those rows plus M either side are not all in the Window, while the rows on screen may still be
// held; the request names the rows on screen, and is not made again until the Window moves. At 0 it asks only once
// the rows on screen are not all held (ADR-0001).
import { afterEach, describe, expect, it } from 'vitest';
import type { GridSource, GridSourceState } from '../src/source/grid-source';
import type { RowRange } from '../src/viewport/row-range';
import { columns, mount, painted, scrollTo, trades, unmountAll, type Trade } from './grid-harness';

afterEach(unmountAll);

const all = trades(1000);

const stateOf = (from: number, rows: number): GridSourceState<Trade> => Object.freeze({
    window: all.slice(from, from + rows), windowStart: from, totalCount: all.length, loading: false, rowSequenceVersion: 0,
    sorts: [], filter: undefined,
});

/** A Consumer's source holding the Window it is handed, and noting every range the grid asks for. */
function windowed(start: number, count: number): GridSource<Trade> & { readonly asked: [number, number][]; hold: (start: number, count: number) => void } {
    const listeners = new Set<() => void>();
    const asked: [number, number][] = [];
    let state = stateOf(start, count);
    return {
        asked,
        hold: (from, rows) => {
            state = stateOf(from, rows);
            for (const listener of listeners) listener();
        },
        subscribe: listener => { listeners.add(listener); return () => { listeners.delete(listener); }; },
        snapshot: () => state,
        setColumns: () => undefined,
        setSorts: () => undefined,
        setFilter: () => undefined,
        needRange: (range: RowRange) => { asked.push([range.start, range.count]); return Promise.resolve(); },
        rows: () => Promise.reject(new Error('Not asked here.')),
        distinctValues: () => Promise.resolve({ kind: 'tooMany' }),
    };
}

describe('the Range Request Margin', () => {
    it('ADR-0183: at 10, the rows on screen are asked for while still held, once the Window holds fewer than 10 rows past them', async () => {
        // 20 rows on screen at 24 px under a viewport of 480; the Window holds rows 0 to 49.
        const source = windowed(0, 50);
        const { root } = mount({ columns, source, rowHeight: 24, viewportHeight: 480, rangeRequestMargin: 10 });
        await painted();
        await scrollTo(root, 19 * 24);
        expect(source.asked).toEqual([]);
        // Rows 21 to 40 on screen: 9 rows past them are held, not 10.
        await scrollTo(root, 21 * 24);
        expect(source.asked).toHaveLength(1);
        const [[start, count] = [-1, -1]] = source.asked;
        expect(start + count).toBeLessThanOrEqual(50);
        expect(count).toBeGreaterThanOrEqual(20);
        // Not asked again until the Window moves; then asked for the rows on screen where the new one falls short.
        await painted();
        expect(source.asked).toHaveLength(1);
        source.hold(0, 200);
        await painted();
        expect(source.asked).toHaveLength(1);
    });

    it('ADR-0183: at 0, the default, nothing is asked while the rows on screen are held', async () => {
        const source = windowed(0, 50);
        const { root } = mount({ columns, source, rowHeight: 24, viewportHeight: 480 });
        await painted();
        await scrollTo(root, 19 * 24);
        await scrollTo(root, 25 * 24);
        await scrollTo(root, 30 * 24);
        expect(source.asked).toEqual([]);
        await scrollTo(root, 31 * 24);
        expect(source.asked).toHaveLength(1);
    });

    it('ADR-0183: a margin is a whole number of rows of at least 0, refused by name otherwise', () => {
        const source = windowed(0, 50);
        expect(() => mount({ columns, source, rowHeight: 24, viewportHeight: 480, rangeRequestMargin: -1 })).toThrow(/rangeRequestMargin is a whole number/u);
        expect(() => mount({ columns, source, rowHeight: 24, viewportHeight: 480, rangeRequestMargin: 2.5 })).toThrow(/rangeRequestMargin is a whole number/u);
    });

    it('ADR-0183: a total not known yet bounds nothing, so the margin reaches past the Window\'s end going down', async () => {
        const source = windowed(0, 50);
        const unknownTotal = Object.freeze({ ...source.snapshot(), totalCount: undefined });
        source.snapshot = () => unknownTotal;
        const { root } = mount({ columns, source, rowHeight: 24, viewportHeight: 480, rangeRequestMargin: 10 });
        await painted();
        await scrollTo(root, 19 * 24);
        expect(source.asked).toEqual([]);
        await scrollTo(root, 21 * 24);
        expect(source.asked).toHaveLength(1);
    });
});
