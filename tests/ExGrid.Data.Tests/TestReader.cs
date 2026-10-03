using System.Collections;
using System.Data.Common;

namespace ExGrid.Data.Tests;

/// <summary>
/// A <see cref="DbDataReader"/> over another, giving its columns other names — two alike, or none,
/// as a query can and a <see cref="System.Data.DataTable"/> cannot — and making a getter throw where
/// <see cref="Fault"/> says, as a driver does for a value it cannot convert.
/// </summary>
internal sealed class TestReader(DbDataReader inner, string[]? names = null) : DbDataReader
{
    private int row;

    /// <summary>The exception a typed getter throws at a row (from one) and an ordinal, if any.</summary>
    public Func<int, int, Exception?>? Fault { get; init; }

    public override int Depth => inner.Depth;

    public override int FieldCount => inner.FieldCount;

    public override bool HasRows => inner.HasRows;

    public override bool IsClosed => inner.IsClosed;

    public override int RecordsAffected => inner.RecordsAffected;

    public override object this[int ordinal] => inner[ordinal];

    public override object this[string name] => inner[name];

    public override string GetName(int ordinal) => names?[ordinal] ?? inner.GetName(ordinal);

    public override int GetOrdinal(string name) => names is null ? inner.GetOrdinal(name) : Array.IndexOf(names, name);

    public override Type GetFieldType(int ordinal) => inner.GetFieldType(ordinal);

    public override string GetDataTypeName(int ordinal) => inner.GetDataTypeName(ordinal);

    public override bool Read()
    {
        row++;
        return inner.Read();
    }

    public override bool NextResult() => inner.NextResult();

    public override bool IsDBNull(int ordinal) => inner.IsDBNull(ordinal);

    public override object GetValue(int ordinal) => Checked(ordinal, inner.GetValue);

    public override int GetValues(object[] values) => inner.GetValues(values);

    public override bool GetBoolean(int ordinal) => Checked(ordinal, inner.GetBoolean);

    public override byte GetByte(int ordinal) => Checked(ordinal, inner.GetByte);

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => inner.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);

    public override char GetChar(int ordinal) => Checked(ordinal, inner.GetChar);

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => inner.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);

    public override DateTime GetDateTime(int ordinal) => Checked(ordinal, inner.GetDateTime);

    public override decimal GetDecimal(int ordinal) => Checked(ordinal, inner.GetDecimal);

    public override double GetDouble(int ordinal) => Checked(ordinal, inner.GetDouble);

    public override float GetFloat(int ordinal) => Checked(ordinal, inner.GetFloat);

    public override Guid GetGuid(int ordinal) => Checked(ordinal, inner.GetGuid);

    public override short GetInt16(int ordinal) => Checked(ordinal, inner.GetInt16);

    public override int GetInt32(int ordinal) => Checked(ordinal, inner.GetInt32);

    public override long GetInt64(int ordinal) => Checked(ordinal, inner.GetInt64);

    public override string GetString(int ordinal) => Checked(ordinal, inner.GetString);

    public override IEnumerator GetEnumerator() => inner.GetEnumerator();

    private TValue Checked<TValue>(int ordinal, Func<int, TValue> get)
        => Fault?.Invoke(row, ordinal) is { } fault ? throw fault : get(ordinal);
}
