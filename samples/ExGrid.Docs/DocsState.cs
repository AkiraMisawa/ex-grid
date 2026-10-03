namespace ExGrid.Docs;

/// <summary>
/// What the reader chose for the whole site: the dark scheme, and whether the Examples run under
/// the built-in Chrome or MudBlazor's (ADR-0110). Every Example follows the one choice, so the code
/// beneath each matches what it shows.
/// </summary>
public sealed class DocsState
{
    /// <summary>Whether the site is shown in the dark scheme.</summary>
    public bool Dark { get; private set; }

    /// <summary>Whether the Examples run under MudBlazor's Chrome.</summary>
    public bool Mud { get; private set; }

    /// <summary>Raised after either choice changes.</summary>
    public event Action? Changed;

    /// <summary>Sets the scheme.</summary>
    public void SetDark(bool dark)
    {
        if (Dark == dark)
            return;
        Dark = dark;
        Changed?.Invoke();
    }

    /// <summary>Sets the Chrome the Examples run under.</summary>
    public void SetMud(bool mud)
    {
        if (Mud == mud)
            return;
        Mud = mud;
        Changed?.Invoke();
    }
}
