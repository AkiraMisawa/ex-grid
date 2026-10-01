using Apache.Arrow.Ipc;

namespace ExGrid.Data.Arrow;

/// <summary>
/// What Arrow's reader is handed when the Consumer handed in no codecs (ADR-0064). Arrow asks a
/// factory for a codec only when a message's buffers are compressed, naming the codec; this one
/// answers with the refusal, so the refusal names the codec the stream needs. A stream that is not
/// compressed never asks.
/// </summary>
internal sealed class MissingCodecs : ICompressionCodecFactory
{
    public static readonly MissingCodecs Instance = new();

    public ICompressionCodec CreateCodec(CompressionCodecType compressionCodecType) => throw Refusal(compressionCodecType);

    public ICompressionCodec CreateCodec(CompressionCodecType compressionCodecType, int? compressionLevel) => throw Refusal(compressionCodecType);

    /// <summary>The refusal of a stream compressed with <paramref name="codec"/>.</summary>
    public static SnapshotException Refusal(CompressionCodecType codec)
        => new($"The Arrow stream's buffers are compressed with {Name(codec)}, and no codec was handed in to undo it; "
            + "pass one to the read, such as Apache.Arrow.Compression's CompressionCodecFactory.");

    /// <summary>A codec as Arrow's format names it.</summary>
    public static string Name(CompressionCodecType codec) => codec switch
    {
        CompressionCodecType.Lz4Frame => "LZ4 frame (LZ4_FRAME)",
        CompressionCodecType.Zstd => "ZSTD",
        _ => codec.ToString(),
    };
}
