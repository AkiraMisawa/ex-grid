namespace ExGrid.Docs;

/// <summary>The framework the reader reads the site for (ADR-0350 §12).</summary>
public enum DocsFramework
{
    /// <summary>The Blazor packages, on nuget.org.</summary>
    Blazor,

    /// <summary>The React packages, <c>@ex-grid/react</c> and <c>@ex-grid/mui</c>.</summary>
    React,
}

/// <summary>
/// What the reader chose for the whole site: the framework, the dark scheme, and whether the Examples
/// run under the built-in Chrome or the design system's Wrapper — MudBlazor's under Blazor, MUI's under
/// React (ADR-0110, ADR-0350 §12). Every Example follows the one choice, so the code beneath each
/// matches what it shows. Only the framework is remembered between visits.
/// </summary>
public sealed class DocsState
{
    private readonly FrameworkMemory _memory;

    /// <summary>Starts from the framework the reader chose last time, or Blazor on a first visit.</summary>
    public DocsState(FrameworkMemory memory)
    {
        _memory = memory;
        Framework = memory.Recalled ?? DocsFramework.Blazor;
    }

    /// <summary>The framework the site is read for.</summary>
    public DocsFramework Framework { get; private set; }

    /// <summary>Whether the framework chosen is kept for the next visit: the browser may refuse to keep it.</summary>
    public bool FrameworkKept => _memory.Kept;

    /// <summary>Whether the site is shown in the dark scheme.</summary>
    public bool Dark { get; private set; }

    /// <summary>Whether the Examples run under the design system's Chrome: MudBlazor's, or MUI's under React.</summary>
    public bool Mud { get; private set; }

    /// <summary>The design system's name, as the Chrome switch says it under the framework chosen.</summary>
    public string WrapperName => Framework == DocsFramework.React ? "MUI" : "MudBlazor";

    /// <summary>Raised after any choice changes.</summary>
    public event Action? Changed;

    /// <summary>Sets the framework, and keeps it for the next visit.</summary>
    public void SetFramework(DocsFramework framework)
    {
        if (Framework == framework)
            return;
        Framework = framework;
        _memory.Remember(framework);
        Changed?.Invoke();
    }

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
