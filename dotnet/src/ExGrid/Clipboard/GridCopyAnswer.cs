namespace ExGrid.Clipboard;

/// <summary>
/// What a copy asks a Consumer that declared a copy answer (ADR-0050, item 9): the copy the
/// grid's own rules approved — the ranges, in the order and orientation they combine — and
/// whether the declared headers were asked for as a row above the block (the menu's "copy
/// with headers"). Positional, like the Selection it came from: meaningful only under
/// <see cref="RowSequenceVersion"/> (ADR-0011).
/// </summary>
public sealed class GridCopyRequest
{
    internal GridCopyRequest(CopyPlan plan, bool withHeaders, int rowSequenceVersion)
    {
        Plan = plan;
        WithHeaders = withHeaders;
        RowSequenceVersion = rowSequenceVersion;
    }

    /// <summary>The ranges being copied, as the grid's own rules approved them: empty,
    /// misaligned and past-the-cap selections are refused before a Consumer is asked
    /// (ADR-0005).</summary>
    public CopyPlan Plan { get; }

    /// <summary>Whether the copy was asked for with the declared headers as one row above
    /// the block (ADR-0005/0036).</summary>
    public bool WithHeaders { get; }

    /// <summary>The order these positions are written in (ADR-0011).</summary>
    public int RowSequenceVersion { get; }
}

/// <summary>
/// A Consumer's answer to one copy (ADR-0050, item 9): the two flavours to write, or a
/// refusal carrying the Consumer's own sentence — never both, never neither. The grid writes
/// what it is given and assembles nothing of its own; on a refusal the clipboard is left
/// untouched, the sentence is announced through the root's live region, and
/// <c>OnCopyRefused</c> is raised as <see cref="CopyRefusalReason.RefusedByConsumer"/>.
/// </summary>
public sealed class GridCopyAnswer
{
    private GridCopyAnswer(string? text, string? html, string? sentence)
    {
        Text = text;
        Html = html;
        Sentence = sentence;
    }

    /// <summary>Write these two flavours: <paramref name="text"/> for <c>text/plain</c> and
    /// <paramref name="html"/> for <c>text/html</c>, exactly as given (ADR-0005's two
    /// formats; which values they carry is the Consumer's).</summary>
    public static GridCopyAnswer Write(string text, string html)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(html);
        return new(text, html, null);
    }

    /// <summary>Refuse the copy, and say why. A refusal with nothing to say would be a key
    /// that did nothing and said nothing, so the sentence is required.</summary>
    public static GridCopyAnswer Refuse(string sentence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sentence);
        return new(null, null, sentence);
    }

    /// <summary>Whether the copy is refused: read <see cref="Sentence"/> if so, and
    /// <see cref="Text"/> and <see cref="Html"/> if not.</summary>
    public bool IsRefused => Sentence is not null;

    /// <summary>The <c>text/plain</c> flavour, or null on a refusal.</summary>
    public string? Text { get; }

    /// <summary>The <c>text/html</c> flavour, or null on a refusal.</summary>
    public string? Html { get; }

    /// <summary>The Consumer's own sentence for a refusal, or null when the copy is
    /// answered. The grid relays it and writes none of its own (ADR-0036).</summary>
    public string? Sentence { get; }
}
