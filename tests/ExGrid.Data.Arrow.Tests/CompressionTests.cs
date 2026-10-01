using Apache.Arrow;
using Apache.Arrow.Ipc;
using Apache.Arrow.Types;
using ExGrid.Data;
using Xunit;
using static ExGrid.Data.Arrow.Tests.Fixtures;

namespace ExGrid.Data.Arrow.Tests;

/// <summary>
/// A compressed stream from another producer is read only with the codec the Consumer hands in, and
/// refused without it, naming the codec it needs (DA-14).
/// </summary>
public class CompressionTests
{
    public static TheoryData<string> Codecs => ["LZ4 frame (LZ4_FRAME)", "ZSTD"];

    [Theory] // ADR-0064: a compressed stream without a codec is refused, naming the codec it needs
    [MemberData(nameof(Codecs))]
    public async Task A_compressed_stream_without_a_codec_is_refused_naming_the_codec(string codec)
    {
        var refusal = await RefusalAsync(Stream(Compressed(codec), Parts()));

        Assert.Equal(
            $"The Arrow stream's buffers are compressed with {codec}, and no codec was handed in to undo it; pass one to the read, such as Apache.Arrow.Compression's CompressionCodecFactory.",
            refusal.Message);
        Assert.Null(refusal.Row);
        Assert.Null(refusal.Column);
    }

    [Theory] // ADR-0064: a compressed stream with the codec is read, every batch and its shared dictionary, as the same stream uncompressed is
    [MemberData(nameof(Codecs))]
    public async Task A_compressed_stream_with_the_codec_is_read(string codec)
    {
        var plain = await ReadEveryWayAsync(Stream(Parts()));

        var compressed = await ReadEveryWayAsync(Stream(Compressed(codec), Parts()), Fixtures.Codecs);

        AssertSame(plain, compressed);
        Assert.Equal(9 + 1_200 + 3, compressed.RowCount);
        // The dictionary's null entry and the null indices are Blanks; amer and AMER are two values;
        // the entries the first batch does not use are read from the dictionary by the batches after it.
        Assert.Equal(Shown("AMER", "amer", null, "amer", "AMER", "amer", "AMER", "amer", "AMER"), Values(compressed, "Region").Take(9));
        Assert.Equal(Shown("AMER", "amer", null, "emea"), Values(compressed, "Region").Skip(9).Take(4));
        Assert.Equal(["AMER", "amer", "emea"], Dictionary(compressed, "Region"));
    }

    [Theory] // ADR-0064: a compressed Arrow file is read with the codec, and refused without it, naming the codec
    [MemberData(nameof(Codecs))]
    public async Task A_compressed_file_needs_its_codec(string codec)
    {
        var file = File(Compressed(codec), Parts());

        var refusal = await RefusalAsync(file);
        var snapshot = await ReadEveryWayAsync(file, Fixtures.Codecs);

        Assert.Contains($"compressed with {codec}", refusal.Message, StringComparison.Ordinal);
        AssertSame(await ReadEveryWayAsync(Stream(Parts())), snapshot);
    }

    [Fact] // ADR-0064: an uncompressed stream never asks for a codec, so a read without one needs none
    public async Task An_uncompressed_stream_needs_no_codec()
    {
        var snapshot = await ReadEveryWayAsync(Stream(Parts()));

        Assert.Equal(9 + 1_200 + 3, snapshot.RowCount);
    }

    private static IpcOptions Compressed(string codec) => new()
    {
        CompressionCodec = codec == "ZSTD" ? CompressionCodecType.Zstd : CompressionCodecType.Lz4Frame,
        CompressionCodecFactory = Fixtures.Codecs,
    };

    /// <summary>Three record batches sharing one dictionary — out of order, amer beside AMER, a null
    /// entry — the first using only two of its entries, with text, money and numbers that compress.</summary>
    private static RecordBatch[] Parts() => [Part(9, [1, 3]), Part(1_200, [1, 3, 2, 0]), Part(3, [0])];

    private static readonly string?[] Entries = ["emea", "AMER", null, "amer"];

    /// <summary><paramref name="rows"/> rows, whose indices cycle through <paramref name="codes"/>,
    /// every seventh a null index.</summary>
    private static RecordBatch Part(int rows, int[] codes)
    {
        var indices = Enumerable.Range(0, rows).Select(i => i % 7 == 2 ? (int?)null : codes[i % codes.Length]).ToArray();
        var dictionary = Utf8(Entries);
        var region = new DictionaryArray(new DictionaryType(Int32Type.Default, StringType.Default, ordered: false), Raw(Int32Type.Default, indices), dictionary);
        var desk = Utf8([.. Enumerable.Range(0, rows).Select(i => i % 5 == 0 ? null : "desk " + (i % 3))]);
        var pnl = Decimals(new Decimal128Type(18, 2), [.. Enumerable.Range(0, rows).Select(i => i % 6 == 0 ? null : Words((i * 1_234) - 50_000))]);
        var quantity = Raw(Int64Type.Default, [.. Enumerable.Range(0, rows).Select(i => i % 4 == 0 ? (long?)null : i)]);
        return Batch(("Region", region), ("Desk", desk), ("Pnl", pnl), ("Quantity", quantity));
    }
}
