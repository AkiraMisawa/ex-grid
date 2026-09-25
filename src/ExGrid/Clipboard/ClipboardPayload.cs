namespace ExGrid.Clipboard;

/// <summary>
/// What the grid answers the <c>copy</c> event's synchronous question with
/// (ADR-0005). <see cref="Kind"/> is one of three strings the JS side switches on:
/// <c>"none"</c> — refused, the clipboard stays untouched; <c>"data"</c> — the
/// selection is in the Window and both formats are here to set synchronously;
/// <c>"async"</c> — the selection runs beyond the Window, ask again through the
/// asynchronous route.
/// </summary>
/// <param name="Kind"><c>"none"</c>, <c>"data"</c> or <c>"async"</c>.</param>
/// <param name="Text">The TSV for <c>text/plain</c>; present only with <c>"data"</c>.</param>
/// <param name="Html">The HTML table for <c>text/html</c>; present only with <c>"data"</c>.</param>
public sealed record ClipboardPayload(string Kind, string? Text = null, string? Html = null)
{
    /// <summary><c>"none"</c>: the copy is refused, and the clipboard stays untouched.</summary>
    public static ClipboardPayload None { get; } = new("none");

    /// <summary><c>"async"</c>: the selection runs beyond the Window; ask again through
    /// the asynchronous route.</summary>
    public static ClipboardPayload Async { get; } = new("async");

    /// <summary><c>"data"</c>: both formats, ready to set on the clipboard.</summary>
    public static ClipboardPayload Data(string text, string html) => new("data", text, html);
}
