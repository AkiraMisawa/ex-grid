using System.Globalization;
using Apache.Arrow.Types;

namespace ExGrid.Data.Arrow;

/// <summary>An Arrow type's name as Arrow's own tools print it — <c>timestamp[ns, tz=Asia/Tokyo]</c>,
/// <c>dictionary&lt;values=utf8, indices=int32&gt;</c> — for a refusal to name.</summary>
internal static class ArrowTypeNames
{
    public static string Of(IArrowType type) => type switch
    {
        DictionaryType dictionary => $"dictionary<values={Of(dictionary.ValueType)}, indices={Of(dictionary.IndexType)}>",
        TimestampType timestamp => string.IsNullOrWhiteSpace(timestamp.Timezone)
            ? $"timestamp[{Unit(timestamp.Unit)}]"
            : $"timestamp[{Unit(timestamp.Unit)}, tz={timestamp.Timezone}]",
        Decimal32Type d => Invariant($"decimal32({d.Precision}, {d.Scale})"),
        Decimal64Type d => Invariant($"decimal64({d.Precision}, {d.Scale})"),
        Decimal128Type d => Invariant($"decimal128({d.Precision}, {d.Scale})"),
        Decimal256Type d => Invariant($"decimal256({d.Precision}, {d.Scale})"),
        Time32Type time => $"time32[{Unit(time.Unit)}]",
        Time64Type time => $"time64[{Unit(time.Unit)}]",
        DurationType duration => $"duration[{Unit(duration.Unit)}]",
        IntervalType interval => $"interval[{interval.Unit}]",
        FixedSizeBinaryType binary => Invariant($"fixed_size_binary[{binary.ByteWidth}]"),
        FixedSizeListType list => Invariant($"fixed_size_list<{Of(list.ValueDataType)}>[{list.ListSize}]"),
        LargeListType list => $"large_list<{Of(list.ValueDataType)}>",
        ListType list => $"list<{Of(list.ValueDataType)}>",
        MapType map => $"map<{Of(map.KeyField.DataType)}, {Of(map.ValueField.DataType)}>",
        StructType record => $"struct<{string.Join(", ", record.Fields.Select(f => $"{f.Name}: {Of(f.DataType)}"))}>",
        _ => type.TypeId switch
        {
            ArrowTypeId.Null => "null",
            ArrowTypeId.Boolean => "bool",
            ArrowTypeId.Int8 => "int8",
            ArrowTypeId.Int16 => "int16",
            ArrowTypeId.Int32 => "int32",
            ArrowTypeId.Int64 => "int64",
            ArrowTypeId.UInt8 => "uint8",
            ArrowTypeId.UInt16 => "uint16",
            ArrowTypeId.UInt32 => "uint32",
            ArrowTypeId.UInt64 => "uint64",
            ArrowTypeId.HalfFloat => "float16",
            ArrowTypeId.Float => "float32",
            ArrowTypeId.Double => "float64",
            ArrowTypeId.String => "utf8",
            ArrowTypeId.LargeString => "large_utf8",
            ArrowTypeId.StringView => "utf8_view",
            ArrowTypeId.Binary => "binary",
            ArrowTypeId.LargeBinary => "large_binary",
            ArrowTypeId.BinaryView => "binary_view",
            ArrowTypeId.Date32 => "date32",
            ArrowTypeId.Date64 => "date64",
            _ => type.Name,
        },
    };

    public static string Unit(TimeUnit unit) => unit switch
    {
        TimeUnit.Second => "s",
        TimeUnit.Millisecond => "ms",
        TimeUnit.Microsecond => "us",
        TimeUnit.Nanosecond => "ns",
        _ => unit.ToString(),
    };

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
