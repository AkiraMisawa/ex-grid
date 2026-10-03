namespace ExGrid.MudBlazor;

/// <summary>
/// The text one of the Wrapper's text fields shows (the Cell Editor's, the Formula Bar's, the
/// Name Box's), kept so that a render never writes the user's own typing back into the field
/// (SRV-5, ED-22). The field is bound with <c>@bind:get</c>/<c>@bind:set</c>, so the renderer
/// takes what the browser reports as what the field shows; this keeps what the field renders
/// equal to it. The core's text arrives in the seam's context a round trip after each report on
/// a Server circuit: an echo of something this field reported is not news, since the browser
/// already shows that text or a later one, and only a text the core wrote itself — an edit
/// opening, a candidate accepted, a Reference pointed, the Name Box's label — is shown.
/// </summary>
internal sealed class FieldText
{
    private readonly Queue<string> _reported = new();
    private string? _told;

    /// <summary>What the field renders.</summary>
    public string Shown { get; private set; } = "";

    /// <summary>The text the seam's context carries now.</summary>
    public void Told(string text)
    {
        if (string.Equals(text, _told, StringComparison.Ordinal))
            return;
        _told = text;
        // Reports older than an echo are behind the browser too.
        while (_reported.Count > 0)
        {
            if (string.Equals(_reported.Dequeue(), text, StringComparison.Ordinal))
                return;
        }
        Shown = text;
    }

    /// <summary>The browser reported what the field shows after an input.</summary>
    public void Typed(string text)
    {
        Shown = text;
        _reported.Enqueue(text);
        // Only reports still on the wire can come back; a bounded number is plenty.
        if (_reported.Count > 256)
            _reported.Dequeue();
    }
}
