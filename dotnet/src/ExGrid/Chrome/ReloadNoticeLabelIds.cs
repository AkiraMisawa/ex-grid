namespace ExGrid.Chrome;

/// <summary>
/// The words of the notices a page holding another version's files calls for (ADR-0123), by id.
/// The grid resolves them as the built-in menus resolve theirs — through its <c>CommandLabel</c>,
/// falling back to <see cref="BuiltInCommandLabels"/> (ADR-0036, ADR-0203) — and ExSheet and ExPivot
/// resolve their own stylesheet's notice through their label functions by the same id.
/// </summary>
public static class ReloadNoticeLabelIds
{
    /// <summary>The script the page holds is not the one the grid was built with, or did not load:
    /// shown in place of the rows and said once in the grid's live region (ERR-10).</summary>
    public const string Script = "reload-notice-script";

    /// <summary>The stylesheet the page holds is not the one a product was built with: shown at the
    /// top of that product's root, by the stylesheet's own rules (ERR-11).</summary>
    public const string Stylesheet = "reload-notice-stylesheet";
}
