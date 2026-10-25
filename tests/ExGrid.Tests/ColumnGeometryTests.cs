using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The horizontal arithmetic every visual layer reads (ADR-0004/0013). Pinned here, in
/// the pure layer, because the selection overlay takes the same offsets and must not
/// re-derive them (ADR-0008 — the two are one implementation).
///
/// <para>Its single answers are shared cases (<c>tests/cases/columns/geometry.json</c>); what stays here
/// are the Device Pixel properties over many widths and ratios, which
/// <c>src/npm/core/test/column-geometry.test.ts</c> holds the Script Core to as well.</para>
/// </summary>
public class ColumnGeometryTests
{
    // Whether a CSS length lies on a whole Device Pixel at a ratio, within a float's noise.
    private static bool OnADevicePixel(double px, double ratio)
        => Math.Abs((px * ratio) - Math.Round(px * ratio)) < 1e-9;

    [Theory] // ADR-0090 / VZ-17: every column edge lies on a Device Pixel, at whole and fractional ratios alike
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2.25)]
    public void Every_column_edge_lies_on_a_device_pixel(double ratio)
    {
        var geometry = new ColumnGeometry([99, 99, 99, 64.4, 8.55], 0, 350, leadWidthPx: 37.3, devicePixelRatio: ratio);

        for (var c = 0; c <= geometry.Count; c++)
            Assert.True(OnADevicePixel(geometry.OffsetPxOf(c), ratio), $"edge {c} at {geometry.OffsetPxOf(c)}px");
        Assert.True(OnADevicePixel(geometry.LeadWidthPx, ratio));
        Assert.Equal(ratio, geometry.DevicePixelRatio);
    }

    [Theory] // ADR-0090 / VZ-17: the edges are the declared positions rounded once, so the total never drifts
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2.25)]
    public void Edges_are_the_declared_positions_rounded_once(double ratio)
    {
        var widths = Enumerable.Repeat(99.0, 200).ToArray();
        var geometry = new ColumnGeometry(widths, 0, 350, leadWidthPx: 0, devicePixelRatio: ratio);

        for (var c = 0; c <= geometry.Count; c++)
        {
            // Never more than half a Device Pixel from where the columns declare the edge.
            Assert.InRange(geometry.OffsetPxOf(c) - (99.0 * c), (-0.5 / ratio) - 1e-9, (0.5 / ratio) + 1e-9);
        }
        // 99 CSS px is 148.5 Device Pixels at 150%: the columns alternate 149 and 148, never all one.
        if (ratio == 1.5)
        {
            Assert.Equal(149.0 / 1.5, geometry.WidthPxOf(0), 9);
            Assert.Equal(148.0 / 1.5, geometry.WidthPxOf(1), 9);
        }
    }

    [Theory] // ADR-0090 (2026-10-11) / VZ-19: a column is painted under one Device Pixel narrower than declared, so the one a number may use of its right padding leaves it at least its declared content, below 100% too
    [InlineData(0.5)]
    [InlineData(0.67)]
    [InlineData(0.8)]
    [InlineData(0.9)]
    [InlineData(1.0)]
    [InlineData(1.1)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(1.75)]
    [InlineData(2.25)]
    [InlineData(3.0)]
    public void A_column_is_painted_under_one_device_pixel_narrower_than_declared(double ratio)
    {
        // SH-41's 49.87 and widths a little past it, after a lead band, so the edges fall at every
        // offset from the Device Pixels.
        double[] widths = [.. Enumerable.Range(0, 120).Select(i => 49.87 + (i % 7 * 0.137) + (i % 5 * 0.011))];
        var geometry = new ColumnGeometry(widths, 0, 400, leadWidthPx: 41.3, devicePixelRatio: ratio);

        for (var c = 0; c < geometry.Count; c++)
        {
            // Each edge moves by at most half a Device Pixel, so a width by under one.
            var shortBy = geometry.DeclaredWidthPxOf(c) - geometry.WidthPxOf(c);
            Assert.True(shortBy < (1 / ratio) + 1e-9, $"column {c}: declared {geometry.DeclaredWidthPxOf(c)}px, painted {geometry.WidthPxOf(c)}px");
        }
        // And some are painted narrower, which is why a number is given the pixel at all.
        Assert.Contains(Enumerable.Range(0, geometry.Count), c => geometry.WidthPxOf(c) < geometry.DeclaredWidthPxOf(c));
    }
}
