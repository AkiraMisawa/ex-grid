using System.Globalization;
using System.Text;

namespace ExposureChart.Charting;

/// <summary>
/// The chart's data as CSV: one row per date, one column per series, in the order drawn — or,
/// long, one row per point. The wide file has a cell for every date and series, so an empty one
/// means either "missing" or "not charted at this date"; the long file has no such cell, and its
/// empty Value is only ever a missing one. Values
/// are the data's own — invariant, unformatted, every digit — because a file read back into a
/// risk system must not carry "12.5M" or a thousands separator of the reader's culture. A
/// missing value is an empty field, never a zero.
/// </summary>
public static class ChartCsv
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string Write(ChartModel model)
    {
        var series = model.Panels.SelectMany(p => p.Series).ToList();
        var csv = new StringBuilder();
        csv.Append(Field(model.XHeader));
        foreach (var s in series) csv.Append(',').Append(Field(s.Name));
        csv.Append("\r\n");
        foreach (var x in model.Xs)
        {
            csv.Append(model.Scale.Raw(x));
            foreach (var s in series)
                csv.Append(',').Append(s.At(x)?.Y is { } v ? v.ToString(Invariant) : "");
            csv.Append("\r\n");
        }
        return csv.ToString();
    }

    public static string WriteLong(ChartModel model)
    {
        var csv = new StringBuilder();
        csv.Append(Field("Series")).Append(',').Append(Field(model.XHeader)).Append(",Value\r\n");
        foreach (var s in model.Panels.SelectMany(p => p.Series))
            foreach (var p in s.Points)
                csv.Append(Field(s.Name)).Append(',').Append(model.Scale.Raw(p.X)).Append(',')
                    .Append(p.Y is { } v ? v.ToString(Invariant) : "").Append("\r\n");
        return csv.ToString();
    }

    /// <summary>
    /// A link the browser saves as a file, with no script: a data URL behind an anchor's
    /// <c>download</c>. The byte-order mark is what makes Excel read the file as UTF-8 rather
    /// than the machine's ANSI code page (Shift_JIS on a Japanese Windows).
    /// </summary>
    public static string DataUrl(string text, string mediaType) =>
        $"data:{mediaType};charset=utf-8,{Uri.EscapeDataString((mediaType == "text/csv" ? "﻿" : "") + text)}";

    // RFC 4180: a field with a comma, a quote or a line break is quoted, its quotes doubled.
    private static string Field(string text) =>
        text.AsSpan().IndexOfAny(",\"\r\n") >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
}
