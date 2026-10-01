using System.Globalization;

namespace ExGrid.Data.Storage;

/// <summary>
/// A column declared over the Consumer's records: how to read its value from a record, and into
/// which writer. A typed accessor returns the value unboxed (ADR-0063, DA-4); a chunk of records is
/// read column by column, so each loop makes one kind of call.
/// </summary>
internal abstract class ObjectColumn<T>(string name, string? caption, SnapshotKind kind)
{
    public string Name { get; } = name;

    public string Caption { get; } = caption ?? name;

    public SnapshotKind Kind { get; } = kind;

    /// <summary>Reads <paramref name="records"/>, whose first is row <paramref name="firstRow"/> (from
    /// zero), into <paramref name="writer"/>. A value that cannot be read fails the load, naming the
    /// row and the column.</summary>
    public abstract void Read(ReadOnlySpan<T> records, int firstRow, ColumnWriter writer);

    protected SnapshotException AccessorFailed(int row, Exception exception)
        => new(row + 1L, Name, $"reading the value threw {exception.GetType().Name}: {exception.Message}", exception);
}

internal sealed class TextObjectColumn<T>(string name, string? caption, Func<T, string?> value)
    : ObjectColumn<T>(name, caption, SnapshotKind.Text)
{
    public override void Read(ReadOnlySpan<T> records, int firstRow, ColumnWriter writer)
    {
        var text = (TextColumnWriter)writer;
        var i = 0;
        try
        {
            for (; i < records.Length; i++)
                text.Add(value(records[i]));
        }
        catch (Exception e) when (e is not SnapshotException)
        {
            throw AccessorFailed(firstRow + i, e);
        }
    }
}

internal sealed class DecimalObjectColumn<T>(string name, string? caption, Func<T, decimal?> value)
    : ObjectColumn<T>(name, caption, SnapshotKind.Decimal)
{
    public override void Read(ReadOnlySpan<T> records, int firstRow, ColumnWriter writer)
    {
        var numbers = (DecimalColumnWriter)writer;
        Span<int> bits = stackalloc int[4];
        var i = 0;
        try
        {
            for (; i < records.Length; i++)
            {
                var read = value(records[i]);
                if (read.HasValue)
                    numbers.Add(read.GetValueOrDefault(), bits);
                else
                    numbers.AddBlank();
            }
        }
        catch (Exception e) when (e is not SnapshotException)
        {
            throw AccessorFailed(firstRow + i, e);
        }
    }
}

internal sealed class DoubleObjectColumn<T>(string name, string? caption, Func<T, double?> value)
    : ObjectColumn<T>(name, caption, SnapshotKind.Double)
{
    public override void Read(ReadOnlySpan<T> records, int firstRow, ColumnWriter writer)
    {
        var numbers = (DoubleColumnWriter)writer;
        var i = 0;
        try
        {
            for (; i < records.Length; i++)
            {
                var read = value(records[i]);
                if (read.HasValue)
                    numbers.Add(read.GetValueOrDefault());
                else
                    numbers.AddBlank();
            }
        }
        catch (Exception e) when (e is not SnapshotException)
        {
            throw AccessorFailed(firstRow + i, e);
        }
    }
}

internal sealed class IntegerObjectColumn<T>(string name, string? caption, Func<T, long?> value)
    : ObjectColumn<T>(name, caption, SnapshotKind.Integer)
{
    public override void Read(ReadOnlySpan<T> records, int firstRow, ColumnWriter writer)
    {
        var numbers = (IntegerColumnWriter)writer;
        var i = 0;
        try
        {
            for (; i < records.Length; i++)
            {
                var read = value(records[i]);
                if (read.HasValue)
                    numbers.Add(read.GetValueOrDefault());
                else
                    numbers.AddBlank();
            }
        }
        catch (Exception e) when (e is not SnapshotException)
        {
            throw AccessorFailed(firstRow + i, e);
        }
    }
}

/// <summary>An Integer column read through an <see cref="int"/> accessor, widened without a box.</summary>
internal sealed class Int32ObjectColumn<T>(string name, string? caption, Func<T, int?> value)
    : ObjectColumn<T>(name, caption, SnapshotKind.Integer)
{
    public override void Read(ReadOnlySpan<T> records, int firstRow, ColumnWriter writer)
    {
        var numbers = (IntegerColumnWriter)writer;
        var i = 0;
        try
        {
            for (; i < records.Length; i++)
            {
                var read = value(records[i]);
                if (read.HasValue)
                    numbers.Add(read.GetValueOrDefault());
                else
                    numbers.AddBlank();
            }
        }
        catch (Exception e) when (e is not SnapshotException)
        {
            throw AccessorFailed(firstRow + i, e);
        }
    }
}

/// <summary>A Date column; <paramref name="ticks"/> turns the accessor's value into clock-value ticks.</summary>
internal sealed class DateObjectColumn<T, TDate>(string name, string? caption, Func<T, TDate?> value, Func<TDate, long> ticks)
    : ObjectColumn<T>(name, caption, SnapshotKind.Date)
    where TDate : struct
{
    public override void Read(ReadOnlySpan<T> records, int firstRow, ColumnWriter writer)
    {
        var dates = (DateColumnWriter)writer;
        var i = 0;
        try
        {
            for (; i < records.Length; i++)
            {
                var read = value(records[i]);
                if (read.HasValue)
                    dates.Add(ticks(read.GetValueOrDefault()));
                else
                    dates.AddBlank();
            }
        }
        catch (Exception e) when (e is not SnapshotException)
        {
            throw AccessorFailed(firstRow + i, e);
        }
    }
}

internal sealed class BooleanObjectColumn<T>(string name, string? caption, Func<T, bool?> value)
    : ObjectColumn<T>(name, caption, SnapshotKind.Boolean)
{
    public override void Read(ReadOnlySpan<T> records, int firstRow, ColumnWriter writer)
    {
        var flags = (BooleanColumnWriter)writer;
        var i = 0;
        try
        {
            for (; i < records.Length; i++)
            {
                var read = value(records[i]);
                if (read.HasValue)
                    flags.Add(read.GetValueOrDefault());
                else
                    flags.AddBlank();
            }
        }
        catch (Exception e) when (e is not SnapshotException)
        {
            throw AccessorFailed(firstRow + i, e);
        }
    }
}

/// <summary>
/// A column known only at run time, read as <see cref="object"/> under a declared kind. A Text column
/// takes any value, a non-string by its invariant text (ADR-0059). The other kinds take a value only
/// when it is of that kind, or converts to it exactly: any integer for Decimal and Integer, a
/// <see cref="float"/> for Double, and all three date types for Date. <see langword="null"/> and
/// <see cref="DBNull"/> are Blanks. Anything else fails the load, naming the row and the column.
/// </summary>
internal sealed class UntypedObjectColumn<T>(string name, string? caption, SnapshotKind kind, Func<T, object?> value)
    : ObjectColumn<T>(name, caption, kind)
{
    public override void Read(ReadOnlySpan<T> records, int firstRow, ColumnWriter writer)
    {
        Span<int> bits = stackalloc int[4];
        var i = 0;
        object? read = null;
        try
        {
            for (; i < records.Length; i++)
            {
                read = value(records[i]);
                if (read is null || read is DBNull)
                {
                    writer.AddBlank();
                    continue;
                }
                if (!TryWrite(read, writer, bits))
                    throw new SnapshotException(firstRow + i + 1L, Name, Mismatch(read));
            }
        }
        catch (Exception e) when (e is not SnapshotException)
        {
            throw AccessorFailed(firstRow + i, e);
        }
    }

    private bool TryWrite(object read, ColumnWriter writer, Span<int> bits)
    {
        switch (Kind)
        {
            case SnapshotKind.Text:
                // Any other value is Text by its text (ADR-0059): the invariant text, since a Snapshot
                // is read apart from any report's culture — an enum by its name, a Guid in its D form.
                ((TextColumnWriter)writer).Add(read as string ?? Convert.ToString(read, CultureInfo.InvariantCulture));
                return true;
            case SnapshotKind.Decimal:
                if (read is decimal number)
                {
                    ((DecimalColumnWriter)writer).Add(number, bits);
                    return true;
                }
                if (read is ulong large)
                {
                    ((DecimalColumnWriter)writer).Add(large, bits);
                    return true;
                }
                if (Integers.TryWiden(read, out var whole))
                {
                    ((DecimalColumnWriter)writer).AddScaled(whole, 0);
                    return true;
                }
                return false;
            case SnapshotKind.Double:
                if (read is double real)
                    ((DoubleColumnWriter)writer).Add(real);
                else if (read is float single)
                    ((DoubleColumnWriter)writer).Add(single);
                else
                    return false;
                return true;
            case SnapshotKind.Integer:
                if (Integers.TryWiden(read, out var integer))
                {
                    ((IntegerColumnWriter)writer).Add(integer);
                    return true;
                }
                return false;
            case SnapshotKind.Date:
                if (read is DateTime dateTime)
                    ((DateColumnWriter)writer).Add(dateTime.Ticks);
                else if (read is DateOnly date)
                    ((DateColumnWriter)writer).Add(Clock.Ticks(date));
                else if (read is DateTimeOffset clock)
                    ((DateColumnWriter)writer).Add(clock.Ticks);
                else
                    return false;
                return true;
            case SnapshotKind.Boolean:
                if (read is not bool flag)
                    return false;
                ((BooleanColumnWriter)writer).Add(flag);
                return true;
            default:
                return false;
        }
    }

    private string Mismatch(object read)
    {
        if (Kind == SnapshotKind.Integer && read is ulong)
            return string.Create(CultureInfo.InvariantCulture, $"the value {read} is outside the range of a 64-bit Integer.");
        var shown = read is string text ? $"'{text}'" : Convert.ToString(read, CultureInfo.InvariantCulture);
        return $"the value {shown} ({read.GetType().Name}) is not of the column's kind, {Kind}.";
    }
}

/// <summary>The integer types, widened to a long exactly.</summary>
internal static class Integers
{
    /// <summary>True when <paramref name="value"/> is of an integer type and within a long's range.</summary>
    public static bool TryWiden(object value, out long widened)
    {
        switch (value)
        {
            case long l: widened = l; return true;
            case int i: widened = i; return true;
            case short s: widened = s; return true;
            case sbyte b: widened = b; return true;
            case byte b: widened = b; return true;
            case ushort s: widened = s; return true;
            case uint u: widened = u; return true;
            case ulong u when u <= long.MaxValue: widened = (long)u; return true;
            default:
                widened = 0;
                return false;
        }
    }
}

/// <summary>A date as the clock value it shows, in ticks (ADR-0063).</summary>
internal static class Clock
{
    public static long Ticks(DateTime value) => value.Ticks;

    public static long Ticks(DateOnly value) => value.DayNumber * TimeSpan.TicksPerDay;

    public static long Ticks(DateTimeOffset value) => value.Ticks;
}
