using System.IO.Compression;
using Microsoft.JSInterop;

namespace ExGrid.Script;

/// <summary>
/// A text the browser handed .NET as a stream (ADR-0005, as amended 2026-10-20 and 2026-10-28): a paste's flavour, or a
/// held call's arguments. The browser sends it gzip-compressed where it can compress, and as it is where it cannot; the
/// two are told apart by gzip's first two bytes, which no UTF-8 text begins with (0x1F is a control character, and 0x8B
/// can only continue a character). Either way the text must be exactly the length its call declared: what was judged
/// was that length, so a stream that unpacks to more is refused rather than read on. Compiled into each adapter that
/// takes one, so every one reads it under the same bounds.
/// </summary>
internal static class CrossedText
{
    private const int GzipFirst = 0x1F, GzipSecond = 0x8B;

    // Deflate packs at most 1,032 bytes into one, so a text declared longer than that allows for the stream that crossed
    // is not this text, and nothing that long is ever made ready for it.
    private const long MostUnpackedPerByte = 1_032;

    /// <summary>Reads <paramref name="reference"/> whole and answers its UTF-8.</summary>
    /// <param name="reference">The text's stream, compressed or not.</param>
    /// <param name="length">The text's length in UTF-8 bytes, as its call declared it.</param>
    /// <param name="what">What the text is, as a refusal names it.</param>
    /// <param name="cancel">Cancels the read.</param>
    /// <exception cref="InvalidDataException">The stream is longer than the length declared allows, unpacks to more or
    /// fewer bytes than declared, or is not gzip it claims to be.</exception>
    public static async Task<ReadOnlyMemory<byte>> ReadAsync(IJSStreamReference reference, long length, string what, CancellationToken cancel)
    {
        // What crossed is never more than the text and gzip's own framing, which on text that does not compress is a few
        // bytes a block: a stream longer than that is not this text, and is not read.
        if (reference.Length > length + length / 64 + 1024)
            throw new InvalidDataException($"{what} crossed as {reference.Length} bytes, more than the {length} declared allow");
        if (length > reference.Length * MostUnpackedPerByte + 1024)
            throw new InvalidDataException($"{what} declared {length} bytes, which the {reference.Length} that crossed cannot hold");
        var crossed = new MemoryStream();
        await using (var stream = await reference.OpenReadStreamAsync(maxAllowedSize: reference.Length, cancel))
            await stream.CopyToAsync(crossed, cancel);
        crossed.Position = 0;
        var packed = crossed.Length >= 2 && crossed.GetBuffer()[0] == GzipFirst && crossed.GetBuffer()[1] == GzipSecond;
        if (!packed)
            return crossed.Length == length
                ? crossed.GetBuffer().AsMemory(0, (int)crossed.Length)
                : throw new InvalidDataException($"{what} is {crossed.Length} bytes, not the {length} declared");

        // Unpacked into a buffer of the declared length and one byte more, so a stream that would unpack past it is
        // found at that byte, and nothing more is ever unpacked.
        var text = new byte[length + 1];
        var filled = 0;
        await using (var gzip = new GZipStream(crossed, CompressionMode.Decompress))
        {
            int read;
            while (filled < text.Length && (read = await gzip.ReadAsync(text.AsMemory(filled), cancel)) > 0)
                filled += read;
        }
        return filled == length
            ? text.AsMemory(0, filled)
            : throw new InvalidDataException($"{what} unpacks to {(filled > length ? "more than" : filled.ToString(System.Globalization.CultureInfo.InvariantCulture))} bytes, not the {length} declared");
    }
}
