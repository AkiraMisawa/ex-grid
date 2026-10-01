using System.Buffers;
using System.Text;
using System.Text.Unicode;

namespace ExGrid.Data.Csv;

/// <summary>
/// A field's bytes as text, in the encoding the file is read in: decoded strictly for a value, shown
/// leniently for a refusal, and a declared text encoded so that it can be matched against bytes.
/// </summary>
internal static class CsvText
{
    /// <summary>The longest field a refusal shows whole; a longer one is cut, ending in an ellipsis.</summary>
    private const int Shown = 60;

    /// <summary>Decodes <paramref name="bytes"/> into <paramref name="buffer"/>, growing it when needed;
    /// false when they are not valid in <paramref name="encoding"/>.</summary>
    public static bool TryDecode(CsvEncoding encoding, ReadOnlySpan<byte> bytes, ref char[] buffer, out int length)
    {
        // Neither encoding makes more characters than it has bytes.
        if (buffer.Length < bytes.Length)
            buffer = new char[Math.Max(bytes.Length, buffer.Length * 2)];
        if (encoding.Strict is null)
        {
            var status = Utf8.ToUtf16(bytes, buffer, out _, out length, replaceInvalidSequences: false);
            return status == OperationStatus.Done;
        }
        try
        {
            length = encoding.Strict.GetChars(bytes, buffer);
            return true;
        }
        catch (DecoderFallbackException)
        {
            length = 0;
            return false;
        }
    }

    /// <summary>Decodes <paramref name="bytes"/> into a string; <see langword="null"/> when they are
    /// not valid in <paramref name="encoding"/>.</summary>
    public static string? Decode(CsvEncoding encoding, ReadOnlySpan<byte> bytes)
    {
        var buffer = new char[bytes.Length];
        return TryDecode(encoding, bytes, ref buffer, out var length) ? new string(buffer, 0, length) : null;
    }

    /// <summary>The field as a refusal shows it, in single quotes: invalid bytes as replacement
    /// characters, and a long field cut short.</summary>
    public static string Show(CsvEncoding encoding, ReadOnlySpan<byte> bytes)
    {
        var text = encoding.Lenient.GetString(bytes);
        return text.Length <= Shown ? $"'{text}'" : $"'{text[..(Shown - 1)]}…'";
    }

    /// <summary>A declared text as the bytes it is written as in <paramref name="encoding"/>, or
    /// <see langword="null"/> when the encoding cannot write it, so that no field can hold it.</summary>
    public static byte[]? Encode(CsvEncoding encoding, string text)
    {
        if (encoding.Strict is null)
            return Encoding.UTF8.GetBytes(text);
        try
        {
            return encoding.Strict.GetBytes(text);
        }
        catch (EncoderFallbackException)
        {
            return null;
        }
    }

    /// <summary>The field without the ASCII spaces around it.</summary>
    public static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> field) => field.Trim((byte)' ');
}
