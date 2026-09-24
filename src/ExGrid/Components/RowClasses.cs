using ExGrid.Rows;

namespace ExGrid.Components;

/// <summary>
/// The class vocabulary a row is painted with, resolved to one interned string per
/// combination — the row's counterpart to <see cref="CellClasses"/>, and public for the
/// same reason: it is what a replacement Chrome and a theme paint against (ADR-0010).
/// <see cref="RowKind.Detail"/> adds nothing, so an ordinary row is painted exactly as it
/// was before Row Kind existed.
/// </summary>
public static class RowClasses
{
    private static readonly string[] Composed = Compose();

    /// <summary>The full class attribute for one row. A Placeholder keeps its Kind: a
    /// group row that blinked into a detail row mid-fling would be the landmark lying
    /// about itself (ADR-0004).</summary>
    public static string For(RowKind kind, bool placeholder) => For(kind, placeholder, stripe: false);

    /// <summary>The same, with the Row Stripe the grid decided from the row's absolute
    /// position (ADR-0038) — one more interned string per combination, never composed on
    /// the render path (ADR-0027 P5).</summary>
    public static string For(RowKind kind, bool placeholder, bool stripe)
        => Composed[Index(kind) * 4 + (placeholder ? 2 : 0) + (stripe ? 1 : 0)];

    // An undefined RowKind is refused rather than quietly painted as a detail row, for
    // the reason an undefined CellState is (ADR-0006 / ADR-0024).
    private static int Index(RowKind kind) => kind switch
    {
        RowKind.Detail => 0,
        RowKind.Group => 1,
        RowKind.Total => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static string Suffix(RowKind kind) => kind switch
    {
        RowKind.Detail => "",
        RowKind.Group => " ex-row-group",
        RowKind.Total => " ex-row-total",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static string[] Compose()
    {
        RowKind[] kinds = [RowKind.Detail, RowKind.Group, RowKind.Total];
        var composed = new string[kinds.Length * 4];
        foreach (var kind in kinds)
        {
            composed[Index(kind) * 4] = "ex-row" + Suffix(kind);
            composed[Index(kind) * 4 + 1] = "ex-row" + Suffix(kind) + " ex-row-stripe";
            composed[Index(kind) * 4 + 2] = "ex-row ex-placeholder" + Suffix(kind);
            composed[Index(kind) * 4 + 3] = "ex-row ex-placeholder" + Suffix(kind) + " ex-row-stripe";
        }

        return composed;
    }
}
