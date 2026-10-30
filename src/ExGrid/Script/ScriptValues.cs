using System.Globalization;
using System.Text.Json;

namespace ExGrid.Script;

/// <summary>
/// A cell's value as it crosses to the Script Core (ADR-0350 section 16): written as the JSON value it is, or
/// tagged where JSON cannot carry it, as <c>src/npm/blazor/src/wire-values.ts</c> reads it. The two are one
/// contract. A value the Script Core would read as another value is refused here, naming the column, rather
/// than sent as the nearest one (the spine's first rule).
/// </summary>
internal static class ScriptValues
{
    /// <summary>The most significant digits the Script Core reads a number with exactly, as a double holds
    /// them (ADR-0350 section 16, <c>exactDecimalDigits</c>).</summary>
    private const int ExactDigits = 15;

    /// <summary>The largest whole number a double holds exactly: past it a long is a <c>bigint</c>.</summary>
    private const long ExactWhole = 9_007_199_254_740_991;

    /// <summary>Writes <paramref name="value"/>, read from column <paramref name="column"/>.</summary>
    internal static void Write(Utf8JsonWriter json, object? value, string column)
    {
        switch (value)
        {
            case null:
                json.WriteNullValue();
                return;
            case string text:
                json.WriteStringValue(text);
                return;
            case bool flag:
                json.WriteBooleanValue(flag);
                return;
            case int or short or sbyte or byte or ushort or uint:
                json.WriteNumberValue(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                return;
            case long whole:
                WriteWhole(json, whole);
                return;
            case ulong whole:
                if (whole <= ExactWhole)
                    json.WriteNumberValue(whole);
                else
                    WriteTagged(json, "i", whole.ToString(CultureInfo.InvariantCulture));
                return;
            case double number:
                WriteDouble(json, number);
                return;
            case float number:
                WriteFloat(json, number, column);
                return;
            case decimal number:
                WriteDecimal(json, number, column);
                return;
            case DateOnly day:
                WriteTagged(json, "d", (day.DayNumber * TimeSpan.TicksPerDay).ToString(CultureInfo.InvariantCulture));
                return;
            case DateTime moment:
                WriteTagged(json, "t", moment.Ticks.ToString(CultureInfo.InvariantCulture));
                return;
            case DateTimeOffset instant:
                json.WriteStartObject();
                json.WriteString("$", "o");
                json.WriteString("v", instant.DateTime.Ticks.ToString(CultureInfo.InvariantCulture));
                json.WriteNumber("m", (int)instant.Offset.TotalMinutes);
                json.WriteEndObject();
                return;
            default:
                // Any other value shows its Display Text: a TimeOnly in its one invariant form, and a
                // value of the Consumer's own by its ToString (ADR-0006).
                json.WriteStringValue(DisplayText.Of(value));
                return;
        }
    }

    /// <summary>Writes a Row Key (ADR-0140): text, a whole number, or a Guid as its text. A key of another kind
    /// is refused: the Script Core tells two keys apart by their value, and only these cross with theirs.</summary>
    internal static void WriteKey(Utf8JsonWriter json, object key)
    {
        switch (key)
        {
            case string text:
                json.WriteStringValue(text);
                return;
            case int or short or sbyte or byte or ushort or uint:
                json.WriteNumberValue(Convert.ToInt64(key, CultureInfo.InvariantCulture));
                return;
            case long whole:
                WriteWhole(json, whole);
                return;
            case Guid id:
                json.WriteStringValue(id.ToString("D"));
                return;
            default:
                throw new NotSupportedException(
                    $"A Row Key of the type {key.GetType().Name} does not cross to the Script Core: a key is text, a whole number or a Guid (ADR-0140, ADR-0350 section 16).");
        }
    }

    private static void WriteWhole(Utf8JsonWriter json, long whole)
    {
        if (whole is >= -ExactWhole and <= ExactWhole)
            json.WriteNumberValue(whole);
        else
            WriteTagged(json, "i", whole.ToString(CultureInfo.InvariantCulture));
    }

    private static void WriteDouble(Utf8JsonWriter json, double number)
    {
        if (double.IsFinite(number))
            json.WriteNumberValue(number);
        else
            WriteTagged(json, "f", double.IsNaN(number) ? "NaN" : number > 0 ? "Infinity" : "-Infinity");
    }

    // A decimal is read in the browser as the double that holds it, as the Script Core reads a number (ADR-0350
    // section 16): exactly, up to fifteen significant digits. A whole decimal past that is a bigint; any other is
    // refused, since the browser would hold, show and sum another number.
    private static void WriteDecimal(Utf8JsonWriter json, decimal number, string column)
    {
        if (SignificantDigits(number) <= ExactDigits)
        {
            json.WriteNumberValue((double)number);
            return;
        }
        if (decimal.Truncate(number) == number)
        {
            WriteTagged(json, "i", decimal.Truncate(number).ToString(CultureInfo.InvariantCulture));
            return;
        }
        throw new NotSupportedException(FormattableString.Invariant(
            $"Column '{column}' holds {number}, of more significant digits than the Script Core reads a number exactly with ({ExactDigits}): it would show and sum another number (ADR-0350 section 16)."));
    }

    // A float is read straight to decimal, never widened to a double first, which would manufacture
    // digits the float never held, and refused past decimal's range either way, since a value that became 0 would
    // tie with the true zeros (ADR-0023).
    private static void WriteFloat(Utf8JsonWriter json, float number, string column)
    {
        if (!float.IsFinite(number))
        {
            WriteDouble(json, number);
            return;
        }
        decimal converted;
        try
        {
            converted = (decimal)number;
        }
        catch (OverflowException)
        {
            throw new NotSupportedException(FormattableString.Invariant(
                $"Column '{column}' holds {number}, outside the range a Number can represent (ADR-0023)."));
        }
        if (converted == 0m && number != 0)
        {
            throw new NotSupportedException(FormattableString.Invariant(
                $"Column '{column}' holds {number}, too small in magnitude for a Number to represent (ADR-0023)."));
        }
        WriteDecimal(json, converted, column);
    }

    private static int SignificantDigits(decimal number)
    {
        var digits = decimal.Abs(number).ToString(CultureInfo.InvariantCulture).Replace(".", "", StringComparison.Ordinal).TrimStart('0').TrimEnd('0');
        return digits.Length;
    }

    private static void WriteTagged(Utf8JsonWriter json, string tag, string text)
    {
        json.WriteStartObject();
        json.WriteString("$", tag);
        json.WriteString("v", text);
        json.WriteEndObject();
    }
}
