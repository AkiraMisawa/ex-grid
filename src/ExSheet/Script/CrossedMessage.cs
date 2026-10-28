using System.IO.Compression;
using System.Text.Json;
using Microsoft.JSInterop;

namespace ExSheet.Script;

/// <summary>
/// A message the browser held because it is larger than a message to .NET carries (<c>inlineBytes</c> in
/// <c>blazor-sheet.ts</c>): Blazor Server closes the circuit on a message larger than its hub takes, 32 KB unless the
/// application raises it. .NET takes it by its number, as a stream, gzip-compressed where the browser could compress,
/// as the grid's paste crosses (ADR-0005, as amended 2026-10-20). The two are told apart by gzip's first two bytes,
/// which no UTF-8 text begins with, and either way the text must be exactly the length the browser declared.
/// </summary>
internal static class CrossedMessage
{
    private const int GzipFirst = 0x1F, GzipSecond = 0x8B;

    /// <summary>Whether <paramref name="crossed"/> is a held message's number and length, in place of the message.</summary>
    public static bool IsHeld(JsonElement crossed) => crossed.ValueKind == JsonValueKind.Object && crossed.TryGetProperty("held", out _);

    /// <summary>The message <paramref name="crossed"/> stands for: itself, or the held message, taken from
    /// <paramref name="sheet"/>.</summary>
    public static async Task<JsonElement> OfAsync(IJSObjectReference sheet, JsonElement crossed)
    {
        if (!IsHeld(crossed))
            return crossed;
        var held = crossed.GetProperty("held").GetInt32();
        var length = crossed.GetProperty("bytes").GetInt64();
        // Taken whatever the circuit's timeout: a large message crosses at about one chunk per round trip.
        var reference = await sheet.InvokeAsync<IJSStreamReference>("take", CancellationToken.None, [held]);
        await using (reference)
        {
            var text = await ReadAsync(reference, length);
            using var document = JsonDocument.Parse(text);
            return document.RootElement.Clone();
        }
    }

    // Deflate packs at most 1,032 bytes into one, so a message declared longer than that allows for the stream that
    // crossed is not this message, and nothing that long is ever made ready for it.
    private const long MostUnpackedPerByte = 1_032;

    // The stream read whole, as the UTF-8 of the length declared.
    private static async Task<ReadOnlyMemory<byte>> ReadAsync(IJSStreamReference reference, long length)
    {
        // What crossed is never more than the text and gzip's own framing, which on text that does not compress is a
        // few bytes a block: a stream longer than that is not this message, and is not read.
        if (reference.Length > length + length / 64 + 1024)
            throw new InvalidDataException($"Internal: a held message crossed as {reference.Length} bytes, more than the {length} declared allow.");
        if (length < 0 || length >= Array.MaxLength || length > reference.Length * MostUnpackedPerByte + 1024)
            throw new InvalidDataException($"Internal: a held message declared {length} bytes, which {reference.Length} bytes that crossed cannot hold.");
        var crossed = new MemoryStream();
        await using (var stream = await reference.OpenReadStreamAsync(maxAllowedSize: reference.Length))
            await stream.CopyToAsync(crossed);
        crossed.Position = 0;
        var packed = crossed.Length >= 2 && crossed.GetBuffer()[0] == GzipFirst && crossed.GetBuffer()[1] == GzipSecond;
        if (!packed)
            return crossed.Length == length
                ? crossed.GetBuffer().AsMemory(0, (int)crossed.Length)
                : throw new InvalidDataException($"Internal: a held message is {crossed.Length} bytes, not the {length} declared.");

        // Unpacked into a buffer of the declared length and one byte more, so a stream that would unpack past it is
        // found at that byte, and nothing more is ever unpacked.
        var text = new byte[length + 1];
        var filled = 0;
        await using (var gzip = new GZipStream(crossed, CompressionMode.Decompress))
        {
            int read;
            while (filled < text.Length && (read = await gzip.ReadAsync(text.AsMemory(filled))) > 0)
                filled += read;
        }
        return filled == length
            ? text.AsMemory(0, filled)
            : throw new InvalidDataException($"Internal: a held message unpacks to {(filled > length ? "more than" : filled.ToString(System.Globalization.CultureInfo.InvariantCulture))} bytes, not the {length} declared.");
    }
}
