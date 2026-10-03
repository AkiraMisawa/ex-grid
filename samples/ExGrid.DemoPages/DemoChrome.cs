using global::ExGrid.Chrome;
using global::ExGrid.MudBlazor;
using global::ExSheet.MudBlazor;

namespace ExGrid.DemoPages;

/// <summary>
/// The <c>?chrome=</c> switch the pages that the browser suite runs under both Chromes share
/// (FN-17, WR-5, DC-17, ED-27): absent or <c>builtin</c> is the core's own Chrome, <c>mud</c> is
/// ExGrid.MudBlazor's. Anything else is refused by name rather than quietly run as the built-in
/// one — a mistyped value would otherwise make a test "under both Chromes" pass twice against the
/// same one. What a page under the Wrapper's Chrome needs besides is <see cref="DemoChromeAssets"/>.
/// </summary>
public static class DemoChrome
{
    /// <summary>Whether <paramref name="name"/> asks for ExGrid.MudBlazor's Chrome.</summary>
    public static bool IsMud(string? name) => name == "mud";

    /// <summary>The Chrome <paramref name="name"/> asks for: null for the core's own,
    /// <see cref="MudGridChrome.Default"/> for <c>mud</c>.</summary>
    /// <exception cref="ArgumentException">The name is neither.</exception>
    public static IGridChrome? From(string? name) => name switch
    {
        null or "builtin" => null,
        "mud" => MudGridChrome.Default,
        _ => throw new ArgumentException(
            $"Unknown chrome '{name}': this page runs with ?chrome=builtin (the default) or ?chrome=mud.",
            nameof(name)),
    };

    /// <summary>
    /// Whether <paramref name="scheme"/>, a page's <c>?scheme=</c>, asks for the dark scheme: absent or
    /// <c>light</c> is the light one, <c>dark</c> the dark one (<see cref="DemoChromeAssets"/>), and
    /// anything else is refused by name, as a mistyped Chrome is.
    /// </summary>
    /// <exception cref="ArgumentException">The scheme is neither.</exception>
    public static bool IsDark(string? scheme) => scheme switch
    {
        null or "light" => false,
        "dark" => true,
        _ => throw new ArgumentException(
            $"Unknown scheme '{scheme}': this page runs with ?scheme=light (the default) or ?scheme=dark.",
            nameof(scheme)),
    };

    /// <summary>The Chrome <paramref name="name"/> asks for on a page of Sheets: null for the core's
    /// own, which is ExSheet's built-in Format Cells too, and <see cref="MudSheetChrome.Default"/> for
    /// <c>mud</c> — ExGrid.MudBlazor's Chrome in the grid's seams and Format Cells as a MudDialog
    /// (ADR-0071).</summary>
    /// <exception cref="ArgumentException">The name is neither.</exception>
    public static IGridChrome? ForSheet(string? name) => From(name) is null ? null : MudSheetChrome.Default;
}
