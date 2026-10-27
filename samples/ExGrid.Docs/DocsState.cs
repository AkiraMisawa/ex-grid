namespace ExGrid.Docs;

/// <summary>The framework the reader reads the site for (ADR-0350 §12).</summary>
public enum DocsFramework
{
    /// <summary>The Blazor packages, on nuget.org.</summary>
    Blazor,

    /// <summary>The React packages, <c>@ex-grid/react</c>, with <c>@ex-grid/mantine</c> and <c>@ex-grid/mui</c>.</summary>
    React,
}

/// <summary>The Chrome a React Example runs under (ADR-0390 section 8): the built-in one, or a design system's Wrapper.</summary>
public enum ReactChrome
{
    /// <summary>The grid's own Chrome, <c>@ex-grid/react</c> alone.</summary>
    Builtin,

    /// <summary><c>@ex-grid/mantine</c>.</summary>
    Mantine,

    /// <summary><c>@ex-grid/mui</c>.</summary>
    Mui,
}

/// <summary>The names a <see cref="ReactChrome"/> goes by, in one place.</summary>
public static class ReactChromeNames
{
    /// <summary>What a Wrapper's React Example adds to the built-in one's file name: <c>FirstGrid</c>,
    /// <c>FirstGridMantine</c>, <c>FirstGridMui</c>.</summary>
    public static string FileSuffix(this ReactChrome chrome) => chrome switch
    {
        ReactChrome.Builtin => "",
        ReactChrome.Mantine => "Mantine",
        ReactChrome.Mui => "Mui",
        _ => throw new ArgumentOutOfRangeException(nameof(chrome), chrome, "No such Chrome."),
    };

    /// <summary>The name the reader knows it by, as the site's switch shows it.</summary>
    public static string DisplayName(this ReactChrome chrome) => chrome switch
    {
        ReactChrome.Builtin => "Built-in",
        ReactChrome.Mantine => "Mantine",
        ReactChrome.Mui => "MUI",
        _ => throw new ArgumentOutOfRangeException(nameof(chrome), chrome, "No such Chrome."),
    };

    /// <summary>The name the Example's frame takes it by in its address (<c>?chrome=mui</c>).</summary>
    public static string AddressName(this ReactChrome chrome) => chrome switch
    {
        ReactChrome.Builtin => "builtin",
        ReactChrome.Mantine => "mantine",
        ReactChrome.Mui => "mui",
        _ => throw new ArgumentOutOfRangeException(nameof(chrome), chrome, "No such Chrome."),
    };
}

/// <summary>
/// What the reader chose for the whole site: the framework, the dark scheme, and whether the Examples
/// run under the built-in Chrome or a design system's Wrapper — MudBlazor's under Blazor, Mantine's or
/// MUI's under React (ADR-0110, ADR-0350 §12, ADR-0390 section 8). Every Example follows the one choice,
/// so the code beneath each matches what it shows. Only the framework is remembered between visits.
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

    /// <summary>Whether the Blazor Examples run under MudBlazor's Chrome.</summary>
    public bool Mud { get; private set; }

    /// <summary>The Chrome the React Examples run under.</summary>
    public ReactChrome ReactChrome { get; private set; }

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

    /// <summary>Sets the Chrome the Blazor Examples run under.</summary>
    public void SetMud(bool mud)
    {
        if (Mud == mud)
            return;
        Mud = mud;
        Changed?.Invoke();
    }

    /// <summary>Sets the Chrome the React Examples run under.</summary>
    public void SetReactChrome(ReactChrome chrome)
    {
        if (ReactChrome == chrome)
            return;
        ReactChrome = chrome;
        Changed?.Invoke();
    }
}
