using System.Globalization;
using ExGrid.Data;

namespace ExPivot.Engine;

/// <summary>
/// The Snapshot behind <c>PivotSource.From(records, fields)</c>: the records read once through the
/// fields' untyped accessors, boxing allowed there for compatibility (ADR-0065). Each value keeps
/// its own kind (ADR-0059: a field "whose values are not all of the declared type is still pivoted
/// as its values are"), so a field is held as one column per kind its accessor returned — text,
/// an exact number (every integral type and <c>decimal</c>), a <c>double</c> (and <c>float</c>), a
/// date (<c>DateTime</c>, <c>DateOnly</c>, <c>DateTimeOffset</c>), a Boolean — with each row's
/// value in the column of its kind and a Blank in the others. A field of one kind, the usual
/// case, is one column. Any other type is text, by its invariant text; null and
/// <see cref="DBNull"/> are Blanks.
/// </summary>
internal sealed class RecordColumns<TRecord>
{
    private const int RowsPerCheckpoint = 1024;

    private readonly IReadOnlyList<TRecord> _records;
    private readonly PivotField<TRecord>[] _fields;
    private readonly SnapshotColumnsBuilder _columns;
    private readonly FieldColumns[] _byField;

    private RecordColumns(IReadOnlyList<TRecord> records, PivotField<TRecord>[] fields, SnapshotColumnsBuilder columns)
    {
        _records = records;
        _fields = fields;
        _columns = columns;
        _byField = [.. fields.Select((field, index) => new FieldColumns(columns, index, field.DatePart))];
    }

    /// <summary>Builds the Snapshot on the calling thread.</summary>
    public static (Snapshot Snapshot, Dictionary<string, FieldBinding> Bindings) Build(IReadOnlyList<TRecord> records, PivotField<TRecord>[] fields)
    {
        var build = new RecordColumns<TRecord>(records, fields, new SnapshotColumnsBuilder());
        for (var row = 0; row < records.Count; row++)
            build.Read(row);
        build.Seal();
        return build.Finish(build._columns.Build());
    }

    /// <summary>Builds the Snapshot in slices, yielding between them as the source shares the thread
    /// (ADR-0063/0065).</summary>
    public static async Task<(Snapshot Snapshot, Dictionary<string, FieldBinding> Bindings)> BuildAsync(
        IReadOnlyList<TRecord> records, PivotField<TRecord>[] fields, PivotSlicing slicing)
    {
        var options = new SnapshotLoadOptions
        {
            SliceBudget = slicing.Budget,
            Yield = () => slicing.YieldAsync(CancellationToken.None),
        };
        var build = new RecordColumns<TRecord>(records, fields, new SnapshotColumnsBuilder(options));
        for (var row = 0; row < records.Count; row++)
        {
            build.Read(row);
            if ((row + 1) % RowsPerCheckpoint == 0)
                await build._columns.CheckpointAsync().ConfigureAwait(false);
        }
        build.Seal();
        return build.Finish(await build._columns.BuildAsync().ConfigureAwait(false));
    }

    // Every field has a column before the Snapshot is built: one whose every value was Blank reads
    // a Text column of Blanks.
    private void Seal()
    {
        foreach (var field in _byField)
            field.EnsureOne(_records.Count);
    }

    private void Read(int row)
    {
        if (_records.Count <= row)
            throw new InvalidOperationException("The records changed while a Snapshot was being built from them.");
        var record = _records[row];
        for (var f = 0; f < _fields.Length; f++)
        {
            var field = _fields[f];
            object? value;
            try
            {
                value = field.Value(record);
            }
            catch (Exception e)
            {
                throw new SnapshotException(row + 1L, field.Name, $"reading the value threw {e.GetType().Name}: {e.Message}", e);
            }
            _byField[f].Append(row, field.Name, value);
        }
    }

    private (Snapshot, Dictionary<string, FieldBinding>) Finish(Snapshot snapshot)
    {
        var bindings = new Dictionary<string, FieldBinding>(_fields.Length, StringComparer.Ordinal);
        for (var f = 0; f < _fields.Length; f++)
            bindings.Add(_fields[f].Name, _byField[f].Bind(_fields[f], snapshot));
        return (snapshot, bindings);
    }

    /// <summary>The columns of one field, made as its kinds are met.</summary>
    private sealed class FieldColumns(SnapshotColumnsBuilder columns, int index, PivotDatePart? part)
    {
        private readonly List<(SnapshotKind Kind, ColumnBuilder Builder)> _made = [];

        public void Append(int row, string field, object? value)
        {
            var kind = KindOf(value);
            if (part is not null && kind is not (null or SnapshotKind.Date))
            {
                throw new SnapshotException(row + 1L, field,
                    $"the value {Convert.ToString(value, CultureInfo.InvariantCulture)} ({value!.GetType().Name}) is not a date, and the field is the {part} of a date.");
            }
            if (kind is { } wanted && !_made.Exists(m => m.Kind == wanted))
                Make(wanted, row);
            foreach (var (made, builder) in _made)
            {
                if (made != kind)
                    builder.AppendBlank();
                else
                    Write(builder, value!);
            }
        }

        /// <summary>The field's binding to the columns it made.</summary>
        public FieldBinding Bind(PivotField field, Snapshot snapshot)
        {
            if (_made.Count == 0)
                throw new InvalidOperationException("A field's columns are made before it is bound.");
            var bound = _made
                .Select(m => snapshot[m.Builder.Name])
                .Select(column => new BoundColumn(column, FieldBinding.RoleOf(column.Kind)))
                .ToArray();
            return new FieldBinding(field, bound, field.DatePart);
        }

        /// <summary>Makes a Text column of Blanks for a field no value was read for — every value
        /// Blank, or no records at all — before the Snapshot is built.</summary>
        public void EnsureOne(int rows)
        {
            if (_made.Count == 0)
                Make(SnapshotKind.Text, rows);
        }

        private void Make(SnapshotKind kind, int rows)
        {
            var name = index.ToString(CultureInfo.InvariantCulture) + ":" + kind;
            ColumnBuilder builder = kind switch
            {
                SnapshotKind.Text => columns.Text(name),
                SnapshotKind.Decimal => columns.Decimal(name),
                SnapshotKind.Double => columns.Double(name),
                SnapshotKind.Date => columns.Date(name),
                _ => columns.Boolean(name),
            };
            builder.AppendBlanks(rows);
            _made.Add((kind, builder));
        }

        private static SnapshotKind? KindOf(object? value) => value switch
        {
            null or DBNull => null,
            string => SnapshotKind.Text,
            decimal or int or long or short or byte or sbyte or uint or ulong or ushort => SnapshotKind.Decimal,
            double or float => SnapshotKind.Double,
            DateTime or DateOnly or DateTimeOffset => SnapshotKind.Date,
            bool => SnapshotKind.Boolean,
            _ => SnapshotKind.Text,
        };

        private static void Write(ColumnBuilder builder, object value)
        {
            switch (value)
            {
                case string text:
                    ((TextColumnBuilder)builder).Append(text);
                    return;
                case decimal number:
                    ((DecimalColumnBuilder)builder).Append(number);
                    return;
                case int number:
                    ((DecimalColumnBuilder)builder).Append(number);
                    return;
                case long number:
                    ((DecimalColumnBuilder)builder).Append(number);
                    return;
                case short number:
                    ((DecimalColumnBuilder)builder).Append(number);
                    return;
                case byte number:
                    ((DecimalColumnBuilder)builder).Append(number);
                    return;
                case sbyte number:
                    ((DecimalColumnBuilder)builder).Append(number);
                    return;
                case uint number:
                    ((DecimalColumnBuilder)builder).Append(number);
                    return;
                case ulong number:
                    ((DecimalColumnBuilder)builder).Append(number);
                    return;
                case ushort number:
                    ((DecimalColumnBuilder)builder).Append(number);
                    return;
                case double number:
                    ((DoubleColumnBuilder)builder).Append(number);
                    return;
                case float number:
                    ((DoubleColumnBuilder)builder).Append(number);
                    return;
                case DateTime date:
                    ((DateColumnBuilder)builder).Append(date);
                    return;
                case DateOnly date:
                    ((DateColumnBuilder)builder).Append(date);
                    return;
                case DateTimeOffset date:
                    ((DateColumnBuilder)builder).Append(date);
                    return;
                case bool flag:
                    ((BooleanColumnBuilder)builder).Append(flag);
                    return;
                default:
                    // Any other type is text, by its invariant text (ADR-0059): an enum by its name.
                    ((TextColumnBuilder)builder).Append(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
                    return;
            }
        }
    }
}
