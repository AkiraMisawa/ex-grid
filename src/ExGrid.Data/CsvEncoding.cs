using System.Runtime.CompilerServices;
using System.Text;

namespace ExGrid.Data;

/// <summary>
/// The encoding a CSV is read in, as its Schema declares it (ADR-0064). It is the encoding of a file
/// that does not begin with a byte-order mark: a file that begins with UTF-8's mark is read as UTF-8
/// whatever is declared, since the mark is the file's own declaration and no file in another encoding
/// begins with it.
/// <para>
/// Text is decoded strictly: bytes that are not valid in the encoding fail the load, naming the row
/// and the column, rather than turn into replacement characters.
/// </para>
/// <para>
/// UTF-8 is built in. Shift-JIS, which Excel on Japanese Windows saves, is offered by
/// <see cref="ShiftJis"/>, the only member of this package that refers to the code pages. An
/// application that never asks for it never loads them, and a browser application that reads only
/// UTF-8 does not download them.
/// </para>
/// </summary>
public sealed class CsvEncoding
{
    private static CsvEncoding? shiftJis;

    private CsvEncoding(string name, Encoding? strict)
    {
        Name = name;
        Strict = strict;
        if (strict is not null)
        {
            var lenient = (Encoding)strict.Clone();
            lenient.DecoderFallback = DecoderFallback.ReplacementFallback;
            Lenient = lenient;
        }
        else
        {
            Lenient = Encoding.UTF8;
        }
    }

    /// <summary>UTF-8, with or without its byte-order mark. The default.</summary>
    public static CsvEncoding Utf8 { get; } = new("UTF-8", null);

    /// <summary>
    /// Shift-JIS as Windows writes it (code page 932), which Excel on Japanese Windows saves a CSV in.
    /// Asking for it loads the code pages, from the framework's <c>System.Text.Encoding.CodePages</c>;
    /// nothing else in this package refers to them, and nothing is registered with
    /// <see cref="Encoding.RegisterProvider"/>.
    /// </summary>
    public static CsvEncoding ShiftJis
    {
        // Kept out of every caller's body: a caller the JIT inlined this into would load the code
        // pages when it was compiled, whether or not it ever asked for them (DA-8).
        [MethodImpl(MethodImplOptions.NoInlining)]
        get
        {
            var made = Volatile.Read(ref shiftJis);
            if (made is not null)
                return made;
            made = new CsvEncoding(
                "Shift-JIS",
                CodePagesEncodingProvider.Instance.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
                    ?? throw new InvalidOperationException("The framework offers no code page 932."));
            return Interlocked.CompareExchange(ref shiftJis, made, null) ?? made;
        }
    }

    /// <summary>The encoding's name, as a refusal names it: <c>UTF-8</c> or <c>Shift-JIS</c>.</summary>
    public string Name { get; }

    /// <summary>The decoder that refuses invalid bytes; <see langword="null"/> for UTF-8, which the
    /// reader decodes itself.</summary>
    internal Encoding? Strict { get; }

    /// <summary>The decoder that shows invalid bytes as replacement characters, for a refusal's
    /// message only.</summary>
    internal Encoding Lenient { get; }

    /// <summary>The encoding's name.</summary>
    public override string ToString() => Name;
}
