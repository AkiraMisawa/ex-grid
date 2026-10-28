using System.Text;
using ExGrid.Script;
using Microsoft.JSInterop;

namespace ExGrid.Clipboard;

/// <summary>
/// One flavour of a paste, read from the stream it crossed as (ADR-0005, as amended 2026-10-20), under the bounds
/// every text the browser streams to .NET is read by (<see cref="CrossedText"/>): the cap was judged on the length
/// the paste's call declared, so a stream that unpacks to more is refused rather than read on.
/// </summary>
internal static class PasteFlavour
{
    /// <summary>Reads <paramref name="reference"/> whole and answers its text.</summary>
    /// <param name="reference">The flavour's stream, compressed or not.</param>
    /// <param name="length">The text's length in UTF-8 bytes, as the paste's call declared it.</param>
    /// <param name="cancel">Cancels the read: an Escape while the paste is under way (ADR-0240 section 12).</param>
    /// <exception cref="InvalidDataException">The stream unpacks to more or fewer bytes than declared, or is
    /// not gzip it claims to be.</exception>
    public static async Task<string> ReadAsync(IJSStreamReference reference, long length, CancellationToken cancel)
        => Encoding.UTF8.GetString((await CrossedText.ReadAsync(reference, length, "the paste's flavour", cancel)).Span);
}
