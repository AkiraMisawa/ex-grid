using System.IO.Compression;
using System.Text;
using Microsoft.JSInterop;

namespace ExGrid.Clipboard;

/// <summary>
/// One flavour of a paste, read from the stream it crossed as (ADR-0005, as amended 2026-10-20). The
/// script sends it gzip-compressed where the browser can compress, and as it is where it cannot; the two
/// are told apart by gzip's first two bytes, which no UTF-8 text begins with (0x1F is a control
/// character, and 0x8B can only continue a character). Either way the text must be exactly the length
/// the paste's call declared: the cap was judged on that length, so a stream that unpacks to more is
/// refused rather than read on.
/// </summary>
internal static class PasteFlavour
{
    private const int GzipFirst = 0x1F, GzipSecond = 0x8B;

    /// <summary>Reads <paramref name="reference"/> whole and answers its text.</summary>
    /// <param name="reference">The flavour's stream, compressed or not.</param>
    /// <param name="length">The text's length in UTF-8 bytes, as the paste's call declared it.</param>
    /// <param name="cancel">Cancels the read: an Escape while the paste is under way (ADR-0240 section 12).</param>
    /// <exception cref="InvalidDataException">The stream unpacks to more or fewer bytes than declared, or is
    /// not gzip it claims to be.</exception>
    public static async Task<string> ReadAsync(IJSStreamReference reference, long length, CancellationToken cancel)
    {
        // What crossed is never more than the text and gzip's own framing, which on text that does not
        // compress is a few bytes a block: a stream longer than that is not this paste, and is not read.
        if (reference.Length > length + length / 64 + 1024)
            throw new InvalidDataException($"the paste's flavour crossed as {reference.Length} bytes, more than {length} declared allow");
        var crossed = new MemoryStream();
        await using (var stream = await reference.OpenReadStreamAsync(maxAllowedSize: reference.Length, cancel))
            await stream.CopyToAsync(crossed, cancel);
        crossed.Position = 0;
        var packed = crossed.Length >= 2 && crossed.GetBuffer()[0] == GzipFirst && crossed.GetBuffer()[1] == GzipSecond;
        if (!packed)
            return crossed.Length == length
                ? Encoding.UTF8.GetString(crossed.GetBuffer(), 0, (int)crossed.Length)
                : throw new InvalidDataException($"the paste's flavour is {crossed.Length} bytes, not the {length} declared");

        // Unpacked into a buffer of the declared length and one byte more, so a stream that would unpack
        // past it is found at that byte, and nothing more is ever unpacked.
        var text = new byte[length + 1];
        var filled = 0;
        await using (var gzip = new GZipStream(crossed, CompressionMode.Decompress))
        {
            int read;
            while (filled < text.Length && (read = await gzip.ReadAsync(text.AsMemory(filled), cancel)) > 0)
                filled += read;
        }
        return filled == length
            ? Encoding.UTF8.GetString(text, 0, filled)
            : throw new InvalidDataException($"the paste's flavour unpacks to {(filled > length ? "more than" : filled.ToString(System.Globalization.CultureInfo.InvariantCulture))} bytes, not the {length} declared");
    }
}
