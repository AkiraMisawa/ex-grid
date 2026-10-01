using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Apache.Arrow;
using Apache.Arrow.Ipc;
using Apache.Arrow.Types;
using ExGrid.Data;
using ExGrid.Data.Arrow;
using Xunit;

namespace ExGrid.Data.Arrow.Tests;

/// <summary>A deal, the record the round trips build a Snapshot from: every kind, and a Blank possible in each.</summary>
internal sealed record Deal(long Id, string? Desk, decimal? Notional, double? Price, long? Quantity, DateOnly? Day, DateTime? When, bool? Live);

/// <summary>The Snapshots, streams and comparisons the tests share.</summary>
internal static class Fixtures
{
    public static readonly string?[] Desks = ["EMEA", "AMER", "amer", null, "", "APAC", "Zürich 東京 🚀", "AMER "];

    public static readonly DateTime Epoch = new(2026, 1, 1);

    /// <summary>The codecs a Consumer hands in to read a compressed stream: Apache.Arrow.Compression's.</summary>
    public static readonly ICompressionCodecFactory Codecs = new Apache.Arrow.Compression.CompressionCodecFactory();

    /// <summary>Deals keyed by id, with every kind of column, some captioned.</summary>
    public static SnapshotBuilder<Deal> Deals()
        => new SnapshotBuilder<Deal>()
            .Integer("Id", d => d.Id)
            .Text("Desk", d => d.Desk, caption: "Trading desk")
            .Decimal("Notional", d => d.Notional)
            .Double("Price", d => d.Price, caption: "Price (USD)")
            .Integer("Quantity", d => d.Quantity)
            .Date("Day", d => d.Day, caption: "Trade date")
            .Date("When", d => d.When)
            .Boolean("Live", d => d.Live)
            .Key("Id");

    /// <summary>Deal <paramref name="id"/>: a Blank in each column at its own interval, text in two
    /// cases and beyond ASCII, decimals at several places, non-finite doubles, the extreme integers,
    /// midnights, and times to the millisecond.</summary>
    public static Deal One(long id) => new(
        id,
        Desks[(int)(id % Desks.Length)],
        id % 11 == 0 ? null : (id % 3) switch { 0 => id * 25 / 100m, 1 => -id / 8m, _ => id * 1_000_000m },
        id % 13 == 0 ? null : (id % 50) switch { 7 => double.NaN, 8 => double.PositiveInfinity, 9 => double.NegativeInfinity, 10 => -0.0, _ => id * 1.5 },
        id % 17 == 0 ? null : (id % 40) switch { 5 => long.MinValue, 6 => long.MaxValue, _ => id - 500 },
        id % 19 == 0 ? null : DateOnly.FromDateTime(Epoch).AddDays((int)(id % 400) - 200),
        id % 23 == 0 ? null : Epoch.AddMilliseconds(id * 7_919),
        id % 29 == 0 ? null : id % 2 == 0);

    public static Deal[] Make(int count, long first = 1) => [.. Enumerable.Range(0, count).Select(i => One(first + i))];

    // ---- writing and reading ----------------------------------------------------------------

    /// <summary>The Snapshot written as an Arrow stream, in record batches of <paramref name="batchRows"/>.</summary>
    public static async Task<byte[]> WriteAsync(Snapshot snapshot, int batchRows = ArrowSnapshotWriter.DefaultBatchRows)
    {
        using var stream = new MemoryStream();
        await SnapshotArrow.WriteAsync(snapshot, stream, batchRows, TestContext.Current.CancellationToken);
        return stream.ToArray();
    }

    /// <summary>How the tests hand a payload to a read: in memory, as a stream that can seek, and as a
    /// network stream that cannot and gives a few bytes at a time.</summary>
    public enum Source
    {
        Memory,
        Seekable,
        Trickle,
    }

    public static ValueTask<Snapshot> ReadAsync(byte[] payload, Source source = Source.Memory, ICompressionCodecFactory? codecs = null, SnapshotLoadOptions? options = null)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        return source switch
        {
            Source.Memory => SnapshotArrow.ReadAsync(payload, options, codecs, cancellationToken),
            Source.Seekable => SnapshotArrow.ReadAsync(new MemoryStream(payload, writable: false), options, codecs, cancellationToken),
            _ => SnapshotArrow.ReadAsync(new TrickleStream(payload), options, codecs, cancellationToken),
        };
    }

    /// <summary>Reads <paramref name="payload"/> every way a Consumer can hand it in, asserting each
    /// gives the same Snapshot, and returns it.</summary>
    public static async Task<Snapshot> ReadEveryWayAsync(byte[] payload, ICompressionCodecFactory? codecs = null)
    {
        var memory = await ReadAsync(payload, Source.Memory, codecs);
        AssertSame(memory, await ReadAsync(payload, Source.Seekable, codecs));
        AssertSame(memory, await ReadAsync(payload, Source.Trickle, codecs));
        return memory;
    }

    /// <summary>The refusal a read of <paramref name="payload"/> meets — the same whichever way it is
    /// handed in.</summary>
    public static async Task<SnapshotException> RefusalAsync(byte[] payload, ICompressionCodecFactory? codecs = null)
    {
        var memory = await Assert.ThrowsAsync<SnapshotException>(async () => await ReadAsync(payload, Source.Memory, codecs));
        var seekable = await Assert.ThrowsAsync<SnapshotException>(async () => await ReadAsync(payload, Source.Seekable, codecs));
        var trickle = await Assert.ThrowsAsync<SnapshotException>(async () => await ReadAsync(payload, Source.Trickle, codecs));
        Assert.Equal(memory.Message, seekable.Message);
        Assert.Equal(memory.Message, trickle.Message);
        return memory;
    }

    // ---- streams as other producers write them, with Apache.Arrow's own builders ---------------

    /// <summary>An IPC stream of <paramref name="batches"/>, written by Apache.Arrow's own writer.</summary>
    public static byte[] Stream(params RecordBatch[] batches) => Stream(new IpcOptions(), batches);

    public static byte[] Stream(IpcOptions options, params RecordBatch[] batches)
    {
        using var stream = new MemoryStream();
        using (var writer = new ArrowStreamWriter(stream, batches[0].Schema, leaveOpen: true, options))
        {
            foreach (var batch in batches)
                writer.WriteRecordBatch(batch);
            writer.WriteEnd();
        }
        return stream.ToArray();
    }

    /// <summary>An Arrow file (Feather 2) of <paramref name="batches"/>, written by Apache.Arrow's own writer.</summary>
    public static byte[] File(IpcOptions options, params RecordBatch[] batches)
    {
        using var stream = new MemoryStream();
        using (var writer = new ArrowFileWriter(stream, batches[0].Schema, leaveOpen: true, options))
        {
            foreach (var batch in batches)
                writer.WriteRecordBatch(batch);
            writer.WriteEnd();
        }
        return stream.ToArray();
    }

    /// <summary>A record batch of the named arrays, every field nullable.</summary>
    public static RecordBatch Batch(params (string Name, IArrowArray Array)[] columns)
        => Batch(null, columns);

    /// <summary>A record batch of the named arrays, with <paramref name="metadata"/> on its schema.</summary>
    public static RecordBatch Batch(IEnumerable<KeyValuePair<string, string>>? metadata, params (string Name, IArrowArray Array)[] columns)
    {
        var schema = new Schema([.. columns.Select(c => new Field(c.Name, c.Array.Data.DataType, nullable: true))], metadata);
        return new RecordBatch(schema, columns.Select(c => c.Array), columns[0].Array.Length);
    }

    public static StringArray Utf8(params string?[] values)
    {
        var builder = new StringArray.Builder();
        foreach (var value in values)
        {
            if (value is null)
                builder.AppendNull();
            else
                builder.Append(value);
        }
        return builder.Build();
    }

    public static LargeStringArray LargeUtf8(params string?[] values)
    {
        var builder = new LargeStringArray.Builder();
        foreach (var value in values)
        {
            if (value is null)
                builder.AppendNull();
            else
                builder.Append(value);
        }
        return builder.Build();
    }

    /// <summary>A dictionary-encoded text column as another producer may write one: its entries in any
    /// order, a null entry allowed, its indices with nulls.</summary>
    public static DictionaryArray Dictionary(string?[] entries, int?[] indices, bool large = false)
    {
        IArrowArray dictionary = large ? LargeUtf8(entries) : Utf8(entries);
        var codes = Raw(Int32Type.Default, indices);
        return new DictionaryArray(new DictionaryType(Int32Type.Default, dictionary.Data.DataType, ordered: false), codes, dictionary);
    }

    /// <summary>An array of <paramref name="type"/> made from raw values, a null for each null.</summary>
    public static IArrowArray Raw<T>(IArrowType type, params T?[] values)
        where T : unmanaged
    {
        var width = Unsafe.SizeOf<T>();
        var bytes = new byte[Math.Max(8, values.Length * width)];
        var span = MemoryMarshal.Cast<byte, T>(bytes.AsSpan(0, values.Length * width));
        var validity = new byte[Math.Max(8, (values.Length + 7) / 8)];
        var nulls = 0;
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] is { } value)
            {
                span[i] = value;
                validity[i >> 3] |= (byte)(1 << (i & 7));
            }
            else
            {
                nulls++;
            }
        }
        return ArrowArrayFactory.BuildArray(new ArrayData(type, values.Length, nulls, 0,
            [nulls == 0 ? ArrowBuffer.Empty : new ArrowBuffer(validity), new ArrowBuffer(bytes)]));
    }

    /// <summary>A <c>decimal128</c> or <c>decimal256</c> array of unscaled values given as their
    /// two's complement words, low word first, a null for each null.</summary>
    public static IArrowArray Decimals(IArrowType type, params long[]?[] values)
    {
        var lanes = type is Decimal256Type ? 4 : 2;
        var bytes = new byte[values.Length * lanes * 8];
        var words = MemoryMarshal.Cast<byte, long>(bytes.AsSpan());
        var validity = new byte[Math.Max(8, (values.Length + 7) / 8)];
        var nulls = 0;
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] is { } value)
            {
                value.CopyTo(words.Slice(i * lanes, lanes));
                validity[i >> 3] |= (byte)(1 << (i & 7));
            }
            else
            {
                nulls++;
            }
        }
        return ArrowArrayFactory.BuildArray(new ArrayData(type, values.Length, nulls, 0,
            [nulls == 0 ? ArrowBuffer.Empty : new ArrowBuffer(validity), new ArrowBuffer(bytes)]));
    }

    /// <summary>A value's two's complement words, low word first, <paramref name="lanes"/> of them.</summary>
    public static long[] Words(Int128 value, int lanes = 2)
    {
        var words = new long[lanes];
        words[0] = (long)(ulong)value;
        words[1] = (long)(value >> 64);
        for (var lane = 2; lane < lanes; lane++)
            words[lane] = words[1] >> 63;
        return words;
    }

    // ---- comparing ---------------------------------------------------------------------------

    /// <summary>
    /// Asserts that <paramref name="actual"/> holds what <paramref name="expected"/> holds: the version,
    /// the Record Key, each column's name, caption and kind, and every row's value — exactly, a double by
    /// its bits — and Blank, in order. With <paramref name="sameDictionaries"/>, each Text column's
    /// dictionary is the same too, in the same order.
    /// </summary>
    public static void AssertSame(Snapshot expected, Snapshot actual, bool sameDictionaries = true)
    {
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.RecordKey?.Name, actual.RecordKey?.Name);
        Assert.Equal(expected.RowCount, actual.RowCount);
        Assert.Equal(expected.Columns.Select(c => (c.Name, c.Caption, c.Kind)), actual.Columns.Select(c => (c.Name, c.Caption, c.Kind)));
        foreach (var column in expected.Columns)
        {
            Assert.Equal(Values(expected, column.Name), Values(actual, column.Name));
            Assert.Equal(Blanks(expected, column.Name), Blanks(actual, column.Name));
            if (sameDictionaries && column is TextColumn text)
                Assert.Equal(text.Dictionary, ((TextColumn)actual[column.Name]).Dictionary);
        }
    }

    /// <summary>A column's values in row order, each shown exactly; a Blank is ∅.</summary>
    public static string[] Values(Snapshot snapshot, string column)
    {
        var target = snapshot[column];
        return [.. snapshot.Rows.Select(r => Show(snapshot.ValueAt(r, target)))];
    }

    /// <summary>Which rows of a column are Blanks, in row order.</summary>
    public static bool[] Blanks(Snapshot snapshot, string column)
    {
        var target = snapshot[column];
        return [.. snapshot.Rows.Select(r => snapshot.IsBlank(r, target))];
    }

    /// <summary>A Text column's dictionary.</summary>
    public static string[] Dictionary(Snapshot snapshot, string column) => [.. ((TextColumn)snapshot[column]).Dictionary];

    /// <summary>A value shown exactly: a double by its bits, a date by its ticks, a decimal by its value.</summary>
    public static string Show(object? value) => value switch
    {
        null => "∅",
        string text => $"'{text}'",
        double number => BitConverter.DoubleToInt64Bits(number).ToString(CultureInfo.InvariantCulture) + "d",
        decimal number => number.ToString(CultureInfo.InvariantCulture) + "m",
        DateTime date => date.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture),
        long number => number.ToString(CultureInfo.InvariantCulture),
        bool flag => flag ? "true" : "false",
        _ => throw new InvalidOperationException($"No value of a Snapshot is a {value.GetType().Name}."),
    };

    /// <summary>The values a test expects, shown as <see cref="Show"/> shows them.</summary>
    public static string[] Shown(params object?[] values) => [.. values.Select(Show)];

    // ---- what other producers wrote ----------------------------------------------------------

    /// <summary>A stream <c>Producers/make_streams.py</c> wrote, by its file name.</summary>
    public static byte[] Produced(string name)
    {
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"No stream named {name} is embedded.");
        using var copy = new MemoryStream();
        resource.CopyTo(copy);
        return copy.ToArray();
    }
}

/// <summary>A stream as a network gives one: it cannot seek, and each read gives a few bytes.</summary>
internal sealed class TrickleStream(byte[] payload, int chunk = 7) : Stream
{
    private int position;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        var count = Math.Min(Math.Min(chunk, buffer.Length), payload.Length - position);
        payload.AsSpan(position, count).CopyTo(buffer);
        position += count;
        return count;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => new(Read(buffer.Span));

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => Task.FromResult(Read(buffer.AsSpan(offset, count)));

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
