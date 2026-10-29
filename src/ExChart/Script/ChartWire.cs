using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using global::ExChart.Engine;
using global::ExGrid.Script;
using ScriptFunction = global::ExGrid.Data.ScriptFunction;

namespace ExChart.Script;

/// <summary>
/// A Chart Definition and a Surface Definition as the browser's half reads them (ADR-0370 section 1): the C# records,
/// member for member, in the Script Core's spelling (<c>src/npm/chart-blazor/src/wire.ts</c>), which the Script Core
/// makes into its own definitions and checks again.
/// </summary>
/// <remarks>
/// Three things in a definition are not data, and each crosses as what it answers. A value keeps the type it was
/// handed in, so a decimal 3.30 is written "3.30" where a number is "3.3"; a series, an axis or a surface may carry
/// the Consumer's Format, a .NET function; and a point may carry a Row Key, any .NET object. Every value the
/// definition holds is written here once, through its writer, and the texts that differ from the Script Core's own
/// writing cross beside it. A writer the chart would ask of a value it does not hold, a tick between the data's
/// positions or a candle's change, cannot be answered that way, and is refused by name rather than sent to answer
/// some values its own way (spine rule 1). A Row Key crosses as a JSON value, and <see cref="RowKeys"/> maps the
/// value a click hands back to the key.
/// </remarks>
internal static class ChartWire
{
    /// <summary>The definition as JSON, and the Row Keys its points carry.</summary>
    /// <exception cref="NotSupportedException">The definition holds what the chart in the browser cannot be handed.</exception>
    public static (string Json, RowKeys Rows) Definition(ChartDefinition definition)
    {
        if (definition.Axis.Format is not null)
        {
            throw new NotSupportedException(
                "The chart's axis has a Format, a .NET function the chart in the browser would ask to write the position under the pointer and "
                + "a range's ends, which it cannot call (ADR-0420). Give the axis a FormatCode or a FormatScript in its place, or draw it with ExChart.Components.ExChart.");
        }
        var rows = new RowKeys();
        var json = Json(w =>
        {
            w.WriteStartObject();
            WriteAxis(w, definition.Axis);
            w.WriteStartArray("series");
            foreach (var s in definition.Series)
                WriteSeries(w, s, rows);
            w.WriteEndArray();
            WriteStrings(w, "notes", definition.Notes);
            w.WriteEndObject();
        });
        return (json, rows);
    }

    /// <summary>The surface as JSON, and each axis' values as the browser holds them, in order.</summary>
    /// <exception cref="NotSupportedException">The surface holds what the surface in the browser cannot be handed.</exception>
    public static (string Json, SurfaceValues Values) Surface(SurfaceDefinition definition)
    {
        var x = Values(definition.X);
        var y = Values(definition.Y);
        var json = Json(w =>
        {
            w.WriteStartObject();
            if (definition.IsMesh)
            {
                w.WriteString("form", "triangles");
                w.WriteString("xTitle", definition.X.Title);
                w.WriteString("yTitle", definition.Y.Title);
                w.WriteString("xScale", Camel(definition.X.Scale));
                w.WriteString("yScale", Camel(definition.Y.Scale));
                WriteWriting(w, "xWriting", Writing(definition.X, x, placedByValue: true));
                WriteWriting(w, "yWriting", Writing(definition.Y, y, placedByValue: true));
                WriteOptional(w, "xFormatCode", NumberCode(definition.X));
                WriteOptional(w, "yFormatCode", NumberCode(definition.Y));
                WriteScript(w, "xFormatScript", definition.X.FormatScript);
                WriteScript(w, "yFormatScript", definition.Y.FormatScript);
                w.WriteStartArray("nodes");
                foreach (var n in definition.Nodes!)
                {
                    w.WriteStartArray();
                    WriteNumber(w, n.X);
                    WriteNumber(w, n.Y);
                    WriteValue(w, n.Z);
                    w.WriteEndArray();
                }
                w.WriteEndArray();
                w.WriteStartArray("triangles");
                foreach (var t in definition.Triangles!)
                {
                    w.WriteStartArray();
                    w.WriteNumberValue(t.A);
                    w.WriteNumberValue(t.B);
                    w.WriteNumberValue(t.C);
                    w.WriteEndArray();
                }
                w.WriteEndArray();
            }
            else
            {
                w.WriteString("form", "grid");
                WriteSurfaceAxis(w, "x", definition.X, x);
                WriteSurfaceAxis(w, "y", definition.Y, y);
                w.WriteStartArray("z");
                foreach (var row in definition.Z)
                {
                    w.WriteStartArray();
                    foreach (var v in row)
                        WriteValue(w, v);
                    w.WriteEndArray();
                }
                w.WriteEndArray();
                if (definition.Method is { } method)
                    w.WriteString("method", method);
                else
                    w.WriteNull("method");
                if (definition.Interpolated is { } interpolated)
                {
                    w.WriteStartArray("interpolated");
                    foreach (var row in interpolated)
                    {
                        w.WriteStartArray();
                        foreach (var b in row)
                            w.WriteBooleanValue(b);
                        w.WriteEndArray();
                    }
                    w.WriteEndArray();
                }
                else
                {
                    w.WriteNull("interpolated");
                }
            }
            w.WriteString("zTitle", definition.ZTitle);
            w.WriteString("zScale", Camel(definition.ZScale));
            if (definition.Centre is { } centre)
                w.WriteNumber("centre", centre);
            else
                w.WriteNull("centre");
            // A mesh's slice reads between its nodes, values the surface does not hold, through the same writer.
            if (definition.ZFormat is not null && definition.IsMesh)
            {
                throw new NotSupportedException(
                    $"The surface's Format for {Name(definition.ZTitle, "its values")} would be asked to write the values a slice through its "
                    + "triangles reads between its nodes, and the surface in the browser cannot call that .NET function (ADR-0420). "
                    + "Give the surface a ZFormatCode or a ZFormatScript in its place, or draw it with ExChart.Components.ExSurface.");
            }
            // A code is written by the Script Core's own reader, so it needs no texts (ADR-0420).
            var zTexts = definition.ZFormatCode is not null || definition.ZFormatScript is not null ? null
                : Texts(HeldZ(definition), definition.WriteZ, () => $"the surface's values ({Name(definition.ZTitle, "z")})");
            WriteTexts(w, "zTexts", zTexts);
            WriteOptional(w, "zFormatCode", definition.ZFormatCode);
            WriteScript(w, "zFormatScript", definition.ZFormatScript);
            WriteStrings(w, "notes", definition.Notes);
            w.WriteEndObject();
        });
        return (json, new SurfaceValues(x, y));
    }

    // ---- The chart ----

    private static void WriteAxis(Utf8JsonWriter w, ChartAxis axis)
    {
        w.WriteStartObject("axis");
        w.WriteString("values", Camel(axis.Values));
        w.WriteString("title", axis.Title);
        if (axis.Scale is { } scale)
            w.WriteString("scale", Camel(scale));
        else
            w.WriteNull("scale");
        WriteStrings(w, "labels", axis.Labels);
        w.WriteStartArray("named");
        foreach (var (position, label) in axis.Named)
        {
            w.WriteStartArray();
            WriteNumber(w, position);
            w.WriteStringValue(label);
            w.WriteEndArray();
        }
        w.WriteEndArray();
        w.WriteNumber("daysPerYear", axis.DaysPerYear);
        w.WriteBoolean("packed", axis.Packed);
        WriteOptional(w, "formatCode", axis.FormatCode);
        WriteScript(w, "formatScript", axis.FormatScript);
        w.WriteEndObject();
    }

    private static void WriteSeries(Utf8JsonWriter w, ChartSeries s, RowKeys rows)
    {
        if (s.Sampler is not null && s.Format is not null)
        {
            throw new NotSupportedException(
                $"'{s.Name}' has a Format, a .NET function the chart in the browser would ask to write the values it samples, which it does not hold, "
                + "and cannot call (ADR-0420). Give the series a FormatCode or a FormatScript in its place, or draw it with ExChart.Components.ExChart.");
        }
        if (s.Format is not null && s.Kind == SeriesKind.PriceRange)
        {
            throw new NotSupportedException(
                $"'{s.Name}' has a Format, a .NET function the chart in the browser would ask to write a candle's change and a merged candle's volume, "
                + "values it does not hold, and cannot call (ADR-0420). Give the series a FormatCode or a FormatScript in its place, or draw it with ExChart.Components.ExChart.");
        }
        w.WriteStartObject();
        w.WriteString("id", s.Id);
        w.WriteString("name", s.Name);
        w.WriteString("kind", Camel(s.Kind));
        w.WriteString("step", Camel(s.Step));
        w.WriteString("axis", Camel(s.Axis));
        w.WriteString("panel", s.Panel);
        WriteOptional(w, "formatCode", s.FormatCode);
        WriteScript(w, "formatScript", s.FormatScript);
        if (s.Keys is { } keys)
        {
            w.WriteStartObject("keys");
            w.WriteString("colour", keys.Colour);
            WriteOptional(w, "lineStyle", keys.LineStyle);
            WriteOptional(w, "marker", keys.Marker);
            w.WriteEndObject();
        }
        else
        {
            w.WriteNull("keys");
        }
        if (s.FromZero is { } fromZero)
            w.WriteBoolean("fromZero", fromZero);
        else
            w.WriteNull("fromZero");
        w.WriteBoolean("interpolated", s.Interpolated);
        w.WriteStartArray("segments");
        foreach (var g in s.Segments)
        {
            w.WriteStartObject();
            w.WritePropertyName("from");
            WriteNumber(w, g.From);
            w.WritePropertyName("to");
            WriteNumber(w, g.To);
            w.WriteString("method", g.Method);
            w.WriteBoolean("extrapolated", g.Extrapolated);
            w.WriteEndObject();
        }
        w.WriteEndArray();
        WriteSampler(w, s);

        // A Price Range's points are its closes, which the browser makes from its rows, as C# does.
        var bars = s.Kind == SeriesKind.PriceRange;
        w.WriteStartArray("x");
        if (!bars)
            foreach (var p in s.Points)
                WriteNumber(w, p.X);
        w.WriteEndArray();
        w.WriteStartArray("y");
        if (!bars)
            foreach (var p in s.Points)
                WriteValue(w, p.Y);
        w.WriteEndArray();
        if (!bars && s.Points.Any(p => p.Name is not null))
        {
            w.WriteStartArray("names");
            foreach (var p in s.Points)
                WriteOptionalValue(w, p.Name);
            w.WriteEndArray();
        }
        else
        {
            w.WriteNull("names");
        }
        if (!bars && s.Points.Any(p => p.Row is not null))
        {
            w.WriteStartArray("rows");
            foreach (var p in s.Points)
            {
                if (p.Row is { } row)
                    rows.Write(w, row, s.Name);
                else
                    w.WriteNullValue();
            }
            w.WriteEndArray();
        }
        else
        {
            w.WriteNull("rows");
        }

        if (s.ColourBy is { } colour)
        {
            w.WriteStartObject("colourBy");
            w.WriteString("title", colour.Title);
            WriteValues(w, "values", colour.Values);
            if (colour.Centre is { } centre)
                w.WriteNumber("centre", centre);
            else
                w.WriteNull("centre");
            w.WriteEndObject();
        }
        else
        {
            w.WriteNull("colourBy");
        }
        if (s.SizeBy is { } size)
        {
            w.WriteStartObject("sizeBy");
            w.WriteString("title", size.Title);
            WriteValues(w, "values", size.Values);
            w.WriteEndObject();
        }
        else
        {
            w.WriteNull("sizeBy");
        }
        w.WriteStartArray("details");
        foreach (var d in s.Details)
        {
            w.WriteStartObject();
            w.WriteString("title", d.Title);
            w.WriteStartArray("values");
            foreach (var v in d.Values)
                WriteOptionalValue(w, v);
            w.WriteEndArray();
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteStartArray("edges");
        foreach (var e in s.Edges)
        {
            w.WriteStartArray();
            rows.Write(w, e.From, s.Name);
            rows.Write(w, e.To, s.Name);
            w.WriteEndArray();
        }
        w.WriteEndArray();
        if (bars)
        {
            w.WriteStartArray("bars");
            foreach (var b in s.Bars)
            {
                w.WriteStartArray();
                WriteNumber(w, b.X);
                WriteValue(w, b.Open);
                WriteValue(w, b.High);
                WriteValue(w, b.Low);
                WriteValue(w, b.Close);
                WriteValue(w, b.Volume);
                w.WriteEndArray();
            }
            w.WriteEndArray();
        }
        else
        {
            w.WriteNull("bars");
        }
        if (s.MergeTo is { } mergeTo)
            w.WriteString("mergeTo", Camel(mergeTo));
        else
            w.WriteNull("mergeTo");
        WriteInterval(w, "interval", s.Interval);
        WriteInterval(w, "xInterval", s.XInterval);
        w.WriteStartArray("thinned");
        foreach (var t in s.Thinned)
        {
            w.WriteStartArray();
            WriteNumber(w, t.From);
            WriteNumber(w, t.To);
            WriteNumber(w, t.Min);
            WriteNumber(w, t.Max);
            w.WriteNumberValue(t.Count);
            w.WriteEndArray();
        }
        w.WriteEndArray();
        // A Price Range's change and a merged candle's volume are values the chart computes, which a text of a held value
        // equal to them would write: its values are written as the Script Core writes numbers, or by its code
        // (ADR-0370 section 1). A code is written by the Script Core's own reader, so it needs no texts either (ADR-0420).
        WriteTexts(w, "texts", bars || s.FormatCode is not null || s.FormatScript is not null ? null : Texts(Held(s), s.Write, () => $"'{s.Name}'"));
        w.WriteEndObject();
    }

    // A series sampled from one of the family's own curves crosses as what the curve was made from, and the Script Core
    // makes the same curve again, held to the shared cases (ADR-0420 section 4). The sampler is that curve's when it is
    // the curve's own ValueAt, which delegate equality tells without reading the function. Any other function is the
    // Consumer's, which the chart in the browser would ask at every pixel and cannot call.
    private static void WriteSampler(Utf8JsonWriter w, ChartSeries s)
    {
        if (s.Sampler is not { } sampler)
        {
            w.WriteNull("sampler");
            return;
        }
        w.WriteStartObject("sampler");
        w.WritePropertyName("from");
        WriteNumber(w, sampler.From);
        w.WritePropertyName("to");
        WriteNumber(w, sampler.To);
        w.WriteStartObject("madeFrom");
        if (sampler.Script is { } script)
        {
            // The Consumer's own script, which the browser imports and asks (ADR-0420 section 3).
            w.WriteString("kind", "script");
            w.WriteString("module", script.Module);
            w.WriteString("export", script.Export);
            w.WriteEndObject();
            w.WriteEndObject();
            return;
        }
        switch (sampler.ValueAt.Target)
        {
            case Engine.Interpolation.Interpolant1D curve when sampler.ValueAt.Equals(new Func<double, ChartValue?>(curve.ValueAt)) && curve.Xs.Count > 0:
                // Only Interpolate's methods give a curve its points, and a curve given another method loses them.
                w.WriteString("kind", curve.Method switch
                {
                    "linear" => "linear",
                    "natural cubic spline" => "naturalCubicSpline",
                    var method => throw new InvalidOperationException($"Internal: the interpolation \"{method}\" has no twin in the Script Core."),
                });
                w.WriteStartArray("xs");
                foreach (var x in curve.Xs)
                    WriteNumber(w, x);
                w.WriteEndArray();
                w.WriteStartArray("ys");
                foreach (var y in curve.Ys)
                    WriteNumber(w, y);
                w.WriteEndArray();
                break;
            case Engine.Fitting.FittedCurve curve when sampler.ValueAt.Equals(new Func<double, ChartValue?>(curve.ValueAt)):
                w.WriteString("kind", "fit");
                w.WriteString("method", curve.Method);
                w.WriteStartArray("coefficients");
                foreach (var c in curve.Coefficients)
                    WriteNumber(w, c);
                w.WriteEndArray();
                w.WritePropertyName("rSquared");
                WriteNumber(w, curve.RSquared);
                w.WriteNumber("count", curve.Count);
                w.WritePropertyName("from");
                WriteNumber(w, curve.From);
                w.WritePropertyName("to");
                WriteNumber(w, curve.To);
                w.WriteString("space", Camel(curve.Space));
                break;
            default:
                throw new NotSupportedException(
                    $"'{s.Name}' is sampled from a .NET function, which the chart in the browser would ask for a value at every pixel and cannot call "
                    + "(ADR-0420). Make it with ExChart.Engine.Interpolation.Interpolate or ExChart.Engine.Fitting.Fit, whose curves the chart makes "
                    + "again from what they were made from, sample a ScriptFunction of your own script, hand it in as its points, or draw it with "
                    + "ExChart.Components.ExChart.");
        }
        w.WriteEndObject();
        w.WriteEndObject();
    }

    // A function of the Consumer's own script, which the browser imports from its module and calls (ADR-0420 section 3).
    private static void WriteScript(Utf8JsonWriter w, string name, ScriptFunction? script)
    {
        if (script is null)
        {
            w.WriteNull(name);
            return;
        }
        w.WritePropertyName(name);
        ScriptFunctionJson.Write(w, script);
    }

    // Every value a series other than a Price Range holds that the chart writes through the series: its points' and its
    // bounds'.
    private static IEnumerable<ChartValue> Held(ChartSeries s)
    {
        foreach (var p in s.Points)
            if (p.Y is { } y) yield return y;
        if (s.Interval is { } interval)
            foreach (var b in interval.Bounds)
                foreach (var v in new[] { b.Lower, b.Upper })
                    if (v is { } held) yield return held;
    }

    private static void WriteInterval(Utf8JsonWriter w, string name, SeriesInterval? interval)
    {
        if (interval is null)
        {
            w.WriteNull(name);
            return;
        }
        w.WriteStartObject(name);
        w.WriteString("meaning", interval.Meaning);
        w.WriteString("style", Camel(interval.Style));
        w.WriteStartArray("bounds");
        foreach (var b in interval.Bounds)
        {
            w.WriteStartArray();
            WriteNumber(w, b.X);
            WriteValue(w, b.Lower);
            WriteValue(w, b.Upper);
            w.WriteEndArray();
        }
        w.WriteEndArray();
        w.WriteEndObject();
    }

    /// <summary>
    /// The texts a writer gives the values it is asked for, where they differ from the Script Core's own writing of the
    /// number, which is <c>R</c>'s; null where none differs. Two values that cross as one number but are written
    /// differently, a decimal 3.3 and 3.30, are refused: the browser could not tell which to write.
    /// </summary>
    private static List<(double Value, string Text)>? Texts(IEnumerable<ChartValue> values, Func<ChartValue, string> write, Func<string> what)
    {
        Dictionary<double, string>? texts = null;
        var seen = new Dictionary<double, string>();
        foreach (var value in values)
        {
            var number = value.ToDouble();
            var text = write(value);
            if (seen.TryGetValue(number, out var before))
            {
                if (before != text)
                {
                    throw new NotSupportedException(
                        $"{what()} writes the value {Own(number)} both as \"{before}\" and as \"{text}\": the chart in the browser holds both as one number "
                        + "and could not say which to write (ADR-0370 section 1).");
                }
                continue;
            }
            seen[number] = text;
            if (text != Own(number))
                (texts ??= [])[number] = text;
        }
        return texts?.Select(t => (t.Key, t.Value)).ToList();
    }

    private static void WriteTexts(Utf8JsonWriter w, string name, List<(double Value, string Text)>? texts)
    {
        if (texts is null)
        {
            w.WriteNull(name);
            return;
        }
        w.WriteStartArray(name);
        foreach (var (value, text) in texts)
        {
            w.WriteStartArray();
            WriteNumber(w, value);
            w.WriteStringValue(text);
            w.WriteEndArray();
        }
        w.WriteEndArray();
    }

    // ---- The surface ----

    // Every value a surface holds: its nodes' on a mesh, its grid's otherwise.
    private static IEnumerable<ChartValue> HeldZ(SurfaceDefinition definition) =>
        definition.Nodes is { } nodes ? nodes.Select(n => n.Z).OfType<ChartValue>() : definition.Z.SelectMany(r => r).OfType<ChartValue>();

    // Each axis value as the browser holds it. Two values the axis holds apart, such as 1 and 1.0m or two dates its Format
    // writes alike, would be one value in the browser, which could not tell which a slice is at: refused by name.
    private static object[] Values(SurfaceAxis axis)
    {
        object[] values = [.. axis.Values.Select(v => SurfaceValue(axis, v))];
        var seen = new Dictionary<object, int>();
        for (var i = 0; i < values.Length; i++)
        {
            if (seen.TryGetValue(values[i], out var before))
            {
                throw new NotSupportedException(
                    $"The surface's axis {Name(axis.Title, "")} holds {Describe(axis.Values[before])} and {Describe(axis.Values[i])}, which the surface in the "
                    + $"browser holds as one value, {Convert.ToString(values[i], CultureInfo.InvariantCulture)} (ADR-0370 section 1). Give the axis values "
                    + "of one type that its Format writes apart.");
            }
            seen[values[i]] = i;
        }
        return values;
    }

    private static string Describe(object value) => $"{Convert.ToString(value, CultureInfo.InvariantCulture)} ({value.GetType().Name})";

    /// <summary>A surface axis' value as the browser holds it: a number for a number, else the text the axis writes it as.</summary>
    internal static object SurfaceValue(SurfaceAxis axis, object value) =>
        IsNumber(value) ? Convert.ToDouble(value, CultureInfo.InvariantCulture) : axis.Write(value);

    private static void WriteSurfaceAxis(Utf8JsonWriter w, string name, SurfaceAxis axis, object[] values)
    {
        w.WriteStartObject(name);
        w.WriteString("title", axis.Title);
        w.WriteStartArray("values");
        foreach (var v in values)
        {
            if (v is double d)
                WriteNumber(w, d);
            else
                w.WriteStringValue((string)v);
        }
        w.WriteEndArray();
        w.WriteString("scale", Camel(axis.Scale));
        WriteWriting(w, "writing", Writing(axis, values, placedByValue: axis.Scale != AxisScale.Categories));
        WriteOptional(w, "formatCode", NumberCode(axis));
        WriteScript(w, "formatScript", axis.FormatScript);
        w.WriteEndObject();
    }

    // A surface axis' number code, which crosses as itself for the Script Core's reader (ADR-0420). A date code's axis
    // crosses as the texts it writes its dates with, which are the values the browser holds.
    private static string? NumberCode(SurfaceAxis axis) =>
        axis.FormatCode is { } code && axis.Values.Count > 0 && IsNumber(axis.Values[0]) ? code : null;

    /// <summary>
    /// How an axis writes its values: as the Script Core does (null); plainly (<c>plain</c>); or by the text of every
    /// value, where any differs from the Script Core's own, the browser writing a tick between them plainly, as C# writes
    /// a tick on an axis with no Format. An axis placed by value, or a mesh's, writes the ticks between its values through
    /// its Format, which the browser cannot call: it crosses only when that Format writes plainly at its values and at
    /// every tick it could be asked for, as a sampled surface's axis does, and is refused by name otherwise.
    /// </summary>
    private static object? Writing(SurfaceAxis axis, object[] values, bool placedByValue)
    {
        // A code or the Consumer's script writes every value in the browser.
        if (NumberCode(axis) is not null || axis.FormatScript is not null)
            return null;
        if (axis.Format is not null && placedByValue)
        {
            if (WritesPlainly(axis, values))
                return "plain";
            throw new NotSupportedException(
                $"The surface's axis {Name(axis.Title, "")} has a Format, which would be asked to write the ticks between its values, and the surface in "
                + "the browser cannot call that .NET function (ADR-0420). Give the axis a FormatCode or a FormatScript in its place, space it evenly as categories, "
                + "or draw it with ExChart.Components.ExSurface.");
        }
        var texts = new List<(object Value, string Text)>(values.Length);
        var differs = false;
        for (var i = 0; i < values.Length; i++)
        {
            var text = axis.Write(i);
            differs |= text != (values[i] is double d ? Own(d) : (string)values[i]);
            texts.Add((values[i], text));
        }
        return differs ? texts : null;
    }

    // Whether the axis writes its values, and every tick between them it could be asked for, as SurfaceMesh.Plain does.
    private static bool WritesPlainly(SurfaceAxis axis, object[] values)
    {
        if (values.Length == 0 || values.Any(v => v is not double))
            return false;
        for (var i = 0; i < values.Length; i++)
        {
            if (axis.Write(i) != Plain((double)values[i]))
                return false;
        }
        var min = values.Min(v => (double)v);
        var max = values.Max(v => (double)v);
        return Ticks(min, max).All(t => axis.WriteTick(t) == Plain(t));
    }

    // The positions a tick may stand at between min and max: multiples of 1, 2, 2.5 and 5 at the range's magnitude and
    // three finer, and on a logarithmic axis 1, 2 and 5 of each decade.
    private static IEnumerable<double> Ticks(double min, double max)
    {
        yield return min;
        yield return max;
        if (!(max > min) || !double.IsFinite(max - min))
            yield break;
        var top = Math.Floor(Math.Log10(max - min));
        for (var k = top - 3; k <= top; k++)
        {
            foreach (var m in (double[])[1, 2, 2.5, 5])
            {
                var step = m * Math.Pow(10, k);
                for (var j = Math.Ceiling(min / step); j * step <= max; j++)
                    yield return j * step;
            }
        }
        if (min > 0)
        {
            for (var d = Math.Floor(Math.Log10(min)); d <= Math.Ceiling(Math.Log10(max)); d++)
            {
                foreach (var m in (double[])[1, 2, 5])
                {
                    var t = m * Math.Pow(10, d);
                    if (t >= min && t <= max)
                        yield return t;
                }
            }
        }
    }

    private static void WriteWriting(Utf8JsonWriter w, string name, object? writing)
    {
        switch (writing)
        {
            case null:
                w.WriteNull(name);
                break;
            case string tag:
                w.WriteString(name, tag);
                break;
            case List<(object Value, string Text)> texts:
                w.WriteStartArray(name);
                foreach (var (value, text) in texts)
                {
                    w.WriteStartArray();
                    if (value is double d)
                        WriteNumber(w, d);
                    else
                        w.WriteStringValue((string)value);
                    w.WriteStringValue(text);
                    w.WriteEndArray();
                }
                w.WriteEndArray();
                break;
            default:
                throw new InvalidOperationException($"Internal: an axis' writing is a {writing.GetType().Name}.");
        }
    }

    // C#'s SurfaceMesh.Plain, the writing a sampled surface's axis takes, and the Script Core's tickText.
    private static string Plain(double value) =>
        value != 0 && Math.Abs(value) < 1e-4 ? value.ToString("G3", CultureInfo.InvariantCulture) : value.ToString("0.####", CultureInfo.InvariantCulture);

    private static bool IsNumber(object? value) =>
        value is double or float or decimal or int or long or short or byte or sbyte or uint or ulong or ushort;

    private static string Name(string title, string fallback) => string.IsNullOrWhiteSpace(title) ? fallback : title;

    // ---- JSON ----

    /// <summary>The Script Core's own writing of a number, as C#'s <c>R</c> writes it.</summary>
    private static string Own(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>A number, or the name of one JSON cannot hold.</summary>
    internal static void WriteNumber(Utf8JsonWriter w, double value)
    {
        if (double.IsFinite(value))
            w.WriteNumberValue(value);
        else
            w.WriteStringValue(double.IsNaN(value) ? "NaN" : value > 0 ? "Infinity" : "-Infinity");
    }

    private static void WriteValue(Utf8JsonWriter w, ChartValue? value)
    {
        if (value is { } v)
            WriteNumber(w, v.ToDouble());
        else
            w.WriteNullValue();
    }

    private static void WriteValues(Utf8JsonWriter w, string name, IReadOnlyList<ChartValue?> values)
    {
        w.WriteStartArray(name);
        foreach (var v in values)
            WriteValue(w, v);
        w.WriteEndArray();
    }

    private static void WriteStrings(Utf8JsonWriter w, string name, IEnumerable<string> values)
    {
        w.WriteStartArray(name);
        foreach (var v in values)
            w.WriteStringValue(v);
        w.WriteEndArray();
    }

    private static void WriteOptional(Utf8JsonWriter w, string name, string? value)
    {
        if (value is null)
            w.WriteNull(name);
        else
            w.WriteString(name, value);
    }

    private static void WriteOptionalValue(Utf8JsonWriter w, string? value)
    {
        if (value is null)
            w.WriteNullValue();
        else
            w.WriteStringValue(value);
    }

    /// <summary>The member the script names as <see cref="Camel{TEnum}"/> writes it; a name that is no member, or a
    /// number, is the script's defect, said by name.</summary>
    internal static TEnum Member<TEnum>(string text) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(text, ignoreCase: true, out var member) && Enum.IsDefined(member) && Camel(member) == text
            ? member
            : throw new InvalidOperationException($"Internal: the script sent '{text}', which is no {typeof(TEnum).Name}.");

    /// <summary>An enum member as the Script Core spells it: <c>AmountPerPeriod</c> is <c>amountPerPeriod</c>. A value
    /// that names no member is refused here, rather than sent as a number the browser would refuse later.</summary>
    internal static string Camel<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, $"{value} is not a {typeof(TEnum).Name}.");
        var name = value.ToString();
        return string.Concat(char.ToLowerInvariant(name[0]).ToString(), name.AsSpan(1));
    }

    internal static string Json(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var json = new Utf8JsonWriter(buffer))
            write(json);
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}

/// <summary>
/// The Row Keys a definition's points carry, by the JSON value each crosses as: text for text, a number for a number,
/// a Boolean for a Boolean, and any other key as the text it is written as in a sentence, invariantly, as the tooltip
/// names a node by it. A click hands the value back, and the key is found by it. Two keys that would cross as one value
/// are refused: a click on either could not say which it was.
/// </summary>
internal sealed class RowKeys
{
    private readonly Dictionary<(char Kind, object Value), object> _keys = [];

    /// <summary>Writes <paramref name="key"/> as the value it crosses as.</summary>
    public void Write(Utf8JsonWriter w, object key, string series)
    {
        var wire = WireOf(key);
        if (_keys.TryGetValue(wire, out var before) && !Equals(before, key))
        {
            throw new NotSupportedException(
                $"'{series}' has the Row Keys {Describe(before)} and {Describe(key)}, which the chart in the browser holds as one value; a click on either "
                + "could not say which it was (ADR-0370 section 1). Give the rows keys that are text or numbers, or a key whose ToString writes each apart.");
        }
        _keys[wire] = key;
        switch (wire.Kind)
        {
            case 's': w.WriteStringValue((string)wire.Value); break;
            case 'n': ChartWire.WriteNumber(w, (double)wire.Value); break;
            case 'b': w.WriteBooleanValue((bool)wire.Value); break;
            default: throw new InvalidOperationException($"Internal: a Row Key of the kind '{wire.Kind}'.");
        }
    }

    /// <summary>The key a click handed back, or null for none.</summary>
    /// <exception cref="InvalidOperationException">The browser handed back a value no point of the definition carries.</exception>
    public object? Read(JsonElement json)
    {
        (char, object) wire = json.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => default,
            JsonValueKind.String => ('s', json.GetString()!),
            JsonValueKind.Number => ('n', json.GetDouble()),
            JsonValueKind.True => ('b', true),
            JsonValueKind.False => ('b', false),
            _ => throw new InvalidOperationException($"Internal: the chart handed back a Row Key of the kind {json.ValueKind}."),
        };
        if (wire == default)
            return null;
        return _keys.TryGetValue(wire, out var key)
            ? key
            : throw new InvalidOperationException($"Internal: the chart handed back the Row Key {json.GetRawText()}, which no point of its definition carries.");
    }

    private static (char Kind, object Value) WireOf(object key) => key switch
    {
        string text => ('s', text),
        bool flag => ('b', flag),
        double or float or decimal or int or long or short or byte or sbyte or uint or ulong or ushort => ('n', Convert.ToDouble(key, CultureInfo.InvariantCulture)),
        _ => ('s', Convert.ToString(key, CultureInfo.InvariantCulture)
            ?? throw new NotSupportedException($"A Row Key of the type {key.GetType().Name} writes itself as nothing, which the chart in the browser cannot hold.")),
    };

    private static string Describe(object key) => $"{Convert.ToString(key, CultureInfo.InvariantCulture)} ({key.GetType().Name})";
}

/// <summary>A surface's axis values as the browser holds them, in order, by which a slice it hands back is read.</summary>
internal sealed record SurfaceValues(object[] X, object[] Y);
