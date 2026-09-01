namespace ExGrid.Clipboard;

/// <summary>
/// What the grid answers the <c>copy</c> event's synchronous question with
/// (ADR-0005). <see cref="Kind"/> is one of three strings the JS side switches on:
/// <c>"none"</c> — refused, the clipboard stays untouched; <c>"data"</c> — the
/// selection is in the Window and both formats are here to set synchronously;
/// <c>"async"</c> — the selection runs beyond the Window, ask again through the
/// asynchronous route.
/// </summary>
public sealed record ClipboardPayload(string Kind, string? Text = null, string? Html = null)
{
    public static ClipboardPayload None { get; } = new("none");

    public static ClipboardPayload Async { get; } = new("async");

    public static ClipboardPayload Data(string text, string html) => new("data", text, html);
}
