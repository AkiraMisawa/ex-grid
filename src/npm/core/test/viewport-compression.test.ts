// The properties of the compressed mapping over many offsets (ADR-0053), which single cases cannot
// state. tests/ExGrid.Tests/ViewportCompressionTests.cs holds the C# core to the same ones; the single
// answers are shared cases (tests/cases/viewport/compression.json).
import { describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import { ViewportGeometry } from '../src/viewport/viewport-geometry';

const rowHeightPx = 28;
const viewportHeightPx = 572;
const rows = 1_000_000;
const ceilingAt150 = 22_369_618;

const compressed = () => new ViewportGeometry({ rowHeightPx, viewportHeightPx, totalRowCount: rows, scrollRoomPx: ceilingAt150 });

/** A small seeded generator (mulberry32), so a failure names offsets that can be run again. */
function seeded(seed: number): () => number {
    let state = seed;
    return () => {
        state = (state + 0x6D2B79F5) | 0;
        let t = state;
        t = Math.imul(t ^ (t >>> 15), t | 1);
        t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
        return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
}

describe('ViewportGeometry under compression', () => {
    it.each([
        [1000, 20, 100, Number.POSITIVE_INFINITY],
        [1000, 20, 100, 20_000],
        [1_000_000, 28, 572, 33_554_428],
        [1_000_000, 28.125, 571.5, 28_125_000.5],
    ])('ADR-0053 / VZ-15: under the ceiling every answer is ADR-0013\'s, bit for bit (%i rows of %fpx in %fpx, room %f)',
        (totalRowCount, rowHeight, viewport, room) => {
            const plain = new ViewportGeometry({ rowHeightPx: rowHeight, viewportHeightPx: viewport, totalRowCount });
            const told = new ViewportGeometry({ rowHeightPx: rowHeight, viewportHeightPx: viewport, totalRowCount, scrollRoomPx: room });

            expect(told.isCompressed).toBe(false);
            expect(told.compression).toBe(1);
            expect(told.scrollHeightPx).toBe(plain.scrollHeightPx);
            expect(told.scrollHeightPx).toBe(told.contentHeightPx);
            expect(told.maxScrollTopPx).toBe(Math.max(0, plain.scrollHeightPx - viewport));
            expect(told.scrollReachPx).toBe(told.maxScrollTopPx);
            for (const s of [-50, 0, 0.5, 99.9, 1234.5678, told.maxScrollTopPx, told.maxScrollTopPx + 10]) {
                expect(told.sliceAt(s)).toEqual(plain.sliceAt(s));
                expect(told.contentOffsetAt(s)).toBe(s);
                expect(told.scrollTopAt(s)).toBe(s);
                expect(told.scrollTopStepped(s, 3 * rowHeight)).toBe(Math.min(Math.max(s + (3 * rowHeight), 0), told.maxScrollTopPx));
                for (const row of [0, 1, 7, Math.trunc(totalRowCount / 2), totalRowCount - 1]) {
                    expect(told.paintedTopPxOf(row, s)).toBe(plain.offsetPxOf(row));
                    expect(told.viewportTopPxOf(row, s)).toBe(plain.offsetPxOf(row) - s);
                    expect(told.scrollTopToReveal(row, s)).toBe(plain.scrollTopToReveal(row, s));
                }
            }
        });

    it.each([0, 1, 123_456.6667, 11_000_000, 22_368_000.3333])(
        'ADR-0053: the slice is floor(c(s) / rowHeight), and the painted rows stand where the content says (s = %f)', offset => {
            const geometry = compressed();
            const s = Math.min(offset, geometry.maxScrollTopPx);
            const slice = defined(geometry.sliceAt(s), 'the slice');
            const c = geometry.contentOffsetAt(s);

            expect(slice.start).toBe(Math.min(Math.floor(c / rowHeightPx), rows - geometry.rowsPerViewport));
            const top = geometry.viewportTopPxOf(slice.start, s);
            expect(top).toBeGreaterThanOrEqual(-rowHeightPx);
            expect(top).toBeLessThanOrEqual(0);
            expect(geometry.paintedTopPxOf(slice.start, s) - s).toBeCloseTo(top, 6);
            expect(top + (slice.count * rowHeightPx)).toBeGreaterThanOrEqual(viewportHeightPx - 1e-6);
        });

    it('ADR-0053 / ADR-0012: a reveal asks for s = c* / k, rounded towards the whole Focus being visible', () => {
        const geometry = compressed();
        const random = seeded(53);
        for (let i = 0; i < 2000; i++) {
            const row = i === 0 ? 0 : i === 1 ? rows - 1 : Math.floor(random() * rows);
            const current = random() * geometry.maxScrollTopPx;
            const s = geometry.scrollTopToReveal(row, current);
            const at = `row ${row} from ${current}`;

            expect(s, at).toBeGreaterThanOrEqual(0);
            expect(s, at).toBeLessThanOrEqual(geometry.maxScrollTopPx);
            const top = geometry.viewportTopPxOf(row, s);
            expect(top, `${at}: its top is above the readable area`).toBeGreaterThanOrEqual(-1e-6);
            expect(top + rowHeightPx, `${at}: its bottom is below it`).toBeLessThanOrEqual(viewportHeightPx + 1e-6);
            expect(s === Math.floor(s) || s === geometry.maxScrollTopPx, `${at}: ${s} is not a whole pixel`).toBe(true);
            expect(geometry.scrollTopToReveal(row, s), `${at}: a second reveal moves`).toBe(s);
        }
    });
});
