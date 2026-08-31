using ExGrid.Cells;

namespace ExGrid.Components;

/// <summary>
/// The class vocabulary a cell is painted with, resolved to one interned string per
/// combination. It lives in the component layer for the reason <see cref="ColumnStyles"/>
/// does: the core decides the meanings — is this a numeric presentation (ADR-0016), is
/// this cell Stale (ADR-0006) — and this answers how those are written down for a
/// browser. It is public because that vocabulary is the seam a replacement Chrome
/// (ADR-0010) paints against; a theme styles these classes rather than inventing its own.
///
/// <para>Every combination is composed once, up front. The alternative — concatenating
/// two or three class names per cell — allocates a string for each of the ~800 cells on
/// every render, on the exact path ADR-0004 caps. Twenty strings held forever is the
/// cheaper end of that trade by a wide margin.</para>
/// </summary>
public static class CellClasses
{
    // Indexed by (state, numeric, pinned) so the lookup is arithmetic, never a switch
    // over combinations that would have to be kept in step with the enum by hand.
    private const int Variants = 4;
    private static readonly string[] Composed = Compose();

    /// <summary>
    /// The full class attribute for one cell. <paramref name="numeric"/> is the core's
    /// classification (<see cref="Columns.OverflowRules.HashesWhenOverflowing"/>), not a
    /// re-derivation of it, and <see cref="CellState.Normal"/> adds nothing at all — an
    /// ordinary cell is painted exactly as it was before Cell State existed.
    /// </summary>
    public static string For(bool numeric, bool pinned, CellState state)
    {
        var index = Index(state) * Variants + (numeric ? 2 : 0) + (pinned ? 1 : 0);
        return Composed[index];
    }

    // An undefined CellState is refused rather than quietly painted as Normal: a state
    // cast in from an integer would otherwise hide a Consumer's mapping bug behind a
    // perfectly ordinary-looking cell, which is the one outcome ADR-0006 exists to
    // prevent.
    private static int Index(CellState state) => state switch
    {
        CellState.Normal => 0,
        CellState.Stale => 1,
        CellState.Missing => 2,
        CellState.Error => 3,
        CellState.Modified => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    private static string Suffix(CellState state) => state switch
    {
        CellState.Normal => "",
        CellState.Stale => " ex-state-stale",
        CellState.Missing => " ex-state-missing",
        CellState.Error => " ex-state-error",
        CellState.Modified => " ex-state-modified",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    private static string[] Compose()
    {
        CellState[] states = [CellState.Normal, CellState.Stale, CellState.Missing, CellState.Error, CellState.Modified];
        var composed = new string[states.Length * Variants];
        foreach (var state in states)
        {
            var suffix = Suffix(state);
            for (var variant = 0; variant < Variants; variant++)
            {
                var numeric = (variant & 2) != 0;
                var pinned = (variant & 1) != 0;
                composed[Index(state) * Variants + variant] = string.Concat(
                    "ex-cell",
                    numeric ? " ex-cell-numeric" : "",
                    pinned ? " ex-pinned" : "",
                    suffix);
            }
        }

        return composed;
    }
}
