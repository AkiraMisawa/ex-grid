using System.Data.Common;
using System.Globalization;

namespace ExGrid.Data.Db;

/// <summary>
/// One column of a <see cref="DbDataReader"/> read into one Snapshot column: where it stands in the
/// reader, and how each of its values is read and appended. <see cref="DBNull"/> is a Blank.
/// </summary>
internal abstract class DbField(string name, string caption, int ordinal)
{
    /// <summary>The Snapshot column's name.</summary>
    public string Name { get; } = name;

    public string Caption { get; } = caption;

    /// <summary>The column's place in the reader.</summary>
    public int Ordinal { get; } = ordinal;

    public abstract SnapshotKind Kind { get; }

    /// <summary>Declares the Snapshot column this field is read into.</summary>
    public abstract void Declare(SnapshotColumnsBuilder builder);

    /// <summary>Reads the field of the row the reader stands on.</summary>
    public abstract void Read(DbDataReader reader);
}

/// <summary>A field read by a getter of the reader, or through a conversion the Consumer declared,
/// into a column built by <typeparamref name="TColumn"/>.</summary>
internal sealed class DbField<TColumn, TValue>(
    string name,
    string caption,
    int ordinal,
    SnapshotKind kind,
    Func<SnapshotColumnsBuilder, string, string, TColumn> declare,
    Func<DbDataReader, int, TValue> get,
    Action<TColumn, TValue> append)
    : DbField(name, caption, ordinal)
    where TColumn : ColumnBuilder
{
    private TColumn? column;

    public override SnapshotKind Kind => kind;

    public override void Declare(SnapshotColumnsBuilder builder) => column = declare(builder, Name, Caption);

    public override void Read(DbDataReader reader)
    {
        if (reader.IsDBNull(Ordinal))
            column!.AppendBlank();
        else
            append(column!, get(reader, Ordinal));
    }
}

/// <summary>
/// The types a reader's column is read by on its own (ADR-0064): <see cref="decimal"/> as Decimal;
/// <see cref="double"/> and <see cref="float"/> as Double; the integer types as Integer, an unsigned
/// one within a long's range; <see cref="DateTime"/>, <see cref="DateOnly"/>,
/// <see cref="DateTimeOffset"/> and <see cref="TimeOnly"/> as Date, each as the clock it shows;
/// <see cref="bool"/> as Boolean; <see cref="string"/> and <see cref="char"/> as Text.
/// </summary>
internal static class DbFields
{
    /// <summary>The field that reads a column of <paramref name="type"/> by its own type, or
    /// <see langword="null"/> for a type no kind reads by itself.</summary>
    public static DbField? ByType(Type? type, string name, string caption, int ordinal) => type switch
    {
        _ when type == typeof(string) => Text(name, caption, ordinal, static (r, o) => r.GetString(o)),
        _ when type == typeof(char) => new DbField<TextColumnBuilder, char>(name, caption, ordinal, SnapshotKind.Text, Texts,
            static (r, o) => r.GetChar(o), static (c, v) => c.Append(new ReadOnlySpan<char>(in v))),
        _ when type == typeof(decimal) => new DbField<DecimalColumnBuilder, decimal>(name, caption, ordinal, SnapshotKind.Decimal, Decimals,
            static (r, o) => r.GetDecimal(o), static (c, v) => c.Append(v)),
        _ when type == typeof(double) => Double(name, caption, ordinal, static (r, o) => r.GetDouble(o)),
        _ when type == typeof(float) => Double(name, caption, ordinal, static (r, o) => r.GetFloat(o)),
        _ when type == typeof(long) => Integer(name, caption, ordinal, static (r, o) => r.GetInt64(o)),
        _ when type == typeof(int) => Integer(name, caption, ordinal, static (r, o) => r.GetInt32(o)),
        _ when type == typeof(short) => Integer(name, caption, ordinal, static (r, o) => r.GetInt16(o)),
        _ when type == typeof(byte) => Integer(name, caption, ordinal, static (r, o) => r.GetByte(o)),
        _ when type == typeof(sbyte) => Integer(name, caption, ordinal, static (r, o) => r.GetFieldValue<sbyte>(o)),
        _ when type == typeof(ushort) => Integer(name, caption, ordinal, static (r, o) => r.GetFieldValue<ushort>(o)),
        _ when type == typeof(uint) => Integer(name, caption, ordinal, static (r, o) => r.GetFieldValue<uint>(o)),
        _ when type == typeof(ulong) => new DbField<IntegerColumnBuilder, ulong>(name, caption, ordinal, SnapshotKind.Integer, Integers,
            static (r, o) => r.GetFieldValue<ulong>(o), static (c, v) => c.Append(v <= long.MaxValue ? (long)v : throw new UnsignedOverflow(v))),
        _ when type == typeof(DateTime) => new DbField<DateColumnBuilder, DateTime>(name, caption, ordinal, SnapshotKind.Date, Dates,
            static (r, o) => r.GetDateTime(o), static (c, v) => c.Append(v)),
        _ when type == typeof(DateOnly) => new DbField<DateColumnBuilder, DateOnly>(name, caption, ordinal, SnapshotKind.Date, Dates,
            static (r, o) => r.GetFieldValue<DateOnly>(o), static (c, v) => c.Append(v)),
        _ when type == typeof(DateTimeOffset) => new DbField<DateColumnBuilder, DateTimeOffset>(name, caption, ordinal, SnapshotKind.Date, Dates,
            static (r, o) => r.GetFieldValue<DateTimeOffset>(o), static (c, v) => c.Append(v)),
        _ when type == typeof(TimeOnly) => new DbField<DateColumnBuilder, TimeOnly>(name, caption, ordinal, SnapshotKind.Date, Dates,
            static (r, o) => r.GetFieldValue<TimeOnly>(o), static (c, v) => c.AppendTicks(v.Ticks)),
        _ when type == typeof(bool) => new DbField<BooleanColumnBuilder, bool>(name, caption, ordinal, SnapshotKind.Boolean, Booleans,
            static (r, o) => r.GetBoolean(o), static (c, v) => c.Append(v)),
        _ => null,
    };

    /// <summary>A Text field read by <paramref name="get"/>; a <see langword="null"/> it returns is a Blank.</summary>
    public static DbField Text(string name, string caption, int ordinal, Func<DbDataReader, int, string?> get)
        => new DbField<TextColumnBuilder, string?>(name, caption, ordinal, SnapshotKind.Text, Texts, get, static (c, v) => c.Append(v));

    public static DbField Decimal(string name, string caption, int ordinal, Func<DbDataReader, int, decimal?> get)
        => new DbField<DecimalColumnBuilder, decimal?>(name, caption, ordinal, SnapshotKind.Decimal, Decimals, get, static (c, v) => c.Append(v));

    public static DbField Double(string name, string caption, int ordinal, Func<DbDataReader, int, double?> get)
        => new DbField<DoubleColumnBuilder, double?>(name, caption, ordinal, SnapshotKind.Double, Doubles, get, static (c, v) => c.Append(v));

    public static DbField Integer(string name, string caption, int ordinal, Func<DbDataReader, int, long?> get)
        => new DbField<IntegerColumnBuilder, long?>(name, caption, ordinal, SnapshotKind.Integer, Integers, get, static (c, v) => c.Append(v));

    public static DbField Date(string name, string caption, int ordinal, Func<DbDataReader, int, DateTime?> get)
        => new DbField<DateColumnBuilder, DateTime?>(name, caption, ordinal, SnapshotKind.Date, Dates, get, static (c, v) =>
        {
            if (v.HasValue)
                c.Append(v.GetValueOrDefault());
            else
                c.AppendBlank();
        });

    public static DbField Boolean(string name, string caption, int ordinal, Func<DbDataReader, int, bool?> get)
        => new DbField<BooleanColumnBuilder, bool?>(name, caption, ordinal, SnapshotKind.Boolean, Booleans, get, static (c, v) => c.Append(v));

    /// <summary>The name a refusal gives a type: C#'s keyword where it has one, and the type's own
    /// name otherwise — <c>Guid</c>, <c>byte[]</c>, <c>TimeSpan</c>.</summary>
    public static string Shown(Type? type) => type switch
    {
        null => "not known",
        _ when type == typeof(object) => "object",
        _ when type == typeof(byte[]) => "byte[]",
        _ => type.Name,
    };

    private static TextColumnBuilder Texts(SnapshotColumnsBuilder b, string name, string caption) => b.Text(name, caption);

    private static DecimalColumnBuilder Decimals(SnapshotColumnsBuilder b, string name, string caption) => b.Decimal(name, caption);

    private static DoubleColumnBuilder Doubles(SnapshotColumnsBuilder b, string name, string caption) => b.Double(name, caption);

    private static IntegerColumnBuilder Integers(SnapshotColumnsBuilder b, string name, string caption) => b.Integer(name, caption);

    private static DateColumnBuilder Dates(SnapshotColumnsBuilder b, string name, string caption) => b.Date(name, caption);

    private static BooleanColumnBuilder Booleans(SnapshotColumnsBuilder b, string name, string caption) => b.Boolean(name, caption);
}

/// <summary>An unsigned value beyond a 64-bit Integer, which the load refuses by row and column.</summary>
internal sealed class UnsignedOverflow(ulong value) : Exception(string.Create(CultureInfo.InvariantCulture, $"the value {value} is outside the range of a 64-bit Integer."));
