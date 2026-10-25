// The Device Pixel properties of the column edges over many widths and ratios (ADR-0090), which single
// cases cannot state. tests/ExGrid.Tests/ColumnGeometryTests.cs holds the C# core to the same ones; the
// single answers are shared cases (tests/cases/columns/geometry.json).
import { describe, expect, it } from 'vitest';
import { ColumnGeometry } from '../src/columns/column-geometry';

/** Whether a CSS length lies on a whole Device Pixel at a ratio, within a float's noise. */
const onADevicePixel = (px: number, ratio: number) => Math.abs((px * ratio) - Math.round(px * ratio)) < 1e-9;

describe('ColumnGeometry on Device Pixels', () => {
    it.each([1.0, 1.25, 1.5, 2.25])('ADR-0090 / VZ-17: every column edge lies on a Device Pixel (ratio %f)', ratio => {
        const geometry = new ColumnGeometry({
            widthsPx: [99, 99, 99, 64.4, 8.55], pinnedCount: 0, viewportWidthPx: 350, leadWidthPx: 37.3, devicePixelRatio: ratio,
        });

        for (let c = 0; c <= geometry.count; c++) {
            expect(onADevicePixel(geometry.offsetPxOf(c), ratio), `edge ${c} at ${geometry.offsetPxOf(c)}px`).toBe(true);
        }
        expect(onADevicePixel(geometry.leadWidthPx, ratio)).toBe(true);
        expect(geometry.devicePixelRatio).toBe(ratio);
    });

    it.each([1.25, 1.5, 2.25])('ADR-0090 / VZ-17: the edges are the declared positions rounded once, so the total never drifts (ratio %f)', ratio => {
        const geometry = new ColumnGeometry({
            widthsPx: Array.from({ length: 200 }, () => 99), pinnedCount: 0, viewportWidthPx: 350, leadWidthPx: 0, devicePixelRatio: ratio,
        });

        for (let c = 0; c <= geometry.count; c++) {
            const drift = geometry.offsetPxOf(c) - (99 * c);
            expect(drift, `edge ${c}`).toBeGreaterThanOrEqual((-0.5 / ratio) - 1e-9);
            expect(drift, `edge ${c}`).toBeLessThanOrEqual((0.5 / ratio) + 1e-9);
        }
    });

    it.each([0.5, 0.67, 0.8, 0.9, 1.0, 1.1, 1.25, 1.5, 1.75, 2.25, 3.0])(
        'ADR-0090 / VZ-19: a column is painted under one Device Pixel narrower than declared (ratio %f)', ratio => {
            // SH-41's 49.87 and widths a little past it, after a lead band, so the edges fall at every
            // offset from the Device Pixels.
            const widthsPx = Array.from({ length: 120 }, (_, i) => 49.87 + ((i % 7) * 0.137) + ((i % 5) * 0.011));
            const geometry = new ColumnGeometry({ widthsPx, pinnedCount: 0, viewportWidthPx: 400, leadWidthPx: 41.3, devicePixelRatio: ratio });

            let narrower = 0;
            for (let c = 0; c < geometry.count; c++) {
                const shortBy = geometry.declaredWidthPxOf(c) - geometry.widthPxOf(c);
                expect(shortBy < (1 / ratio) + 1e-9, `column ${c}: declared ${geometry.declaredWidthPxOf(c)}px, painted ${geometry.widthPxOf(c)}px`).toBe(true);
                if (shortBy > 0) narrower++;
            }
            // And some are painted narrower, which is why a number is given the pixel at all.
            expect(narrower).toBeGreaterThan(0);
        });
});
