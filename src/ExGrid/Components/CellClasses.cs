using ExGrid.Cells;
using ExGrid.Columns;

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
/// every render, on the exact path ADR-0004 caps. A few hundred strings held forever is
/// the cheaper end of that trade by a wide margin.</para>
/// </summary>
public static class CellClasses
{
    // Indexed by (tone, state, align, numeric, pinned) so the lookup is arithmetic, never
    // a switch over combinations that would have to be kept in step with the enums by hand.
    private const int Variants = 4;
    private const int Aligns = 4;
    private const int States = 5;
    private static readonly string[] Composed = Compose();

    /// <summary>
    /// The full class attribute for one cell. <paramref name="numeric"/> is the core's
    /// classification (<see cref="Columns.OverflowRules.HashesWhenOverflowing"/>), not a
    /// re-derivation of it; <see cref="CellState.Normal"/>, <see cref="CellAlign.Auto"/>
    /// and <see cref="CellTone.None"/> add nothing at all — an ordinary cell is painted
    /// exactly as it was before any of the vocabularies existed (ADR-0006/0016).
    /// </summary>
    public static string For(bool numeric, bool pinned, CellState state, CellAlign align = CellAlign.Auto, CellTone tone = CellTone.None)
    {
        var index = (((ToneIndex(tone) * States + Index(state)) * Aligns) + AlignIndex(align)) * Variants + (numeric ? 2 : 0) + (pinned ? 1 : 0);
        return Composed[index];
    }

    private static int ToneIndex(CellTone tone) => tone switch
    {
        CellTone.None => 0,
        CellTone.Positive => 1,
        CellTone.Negative => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(tone), tone, null),
    };

    private static string ToneSuffix(CellTone tone) => tone switch
    {
        CellTone.None => "",
        CellTone.Positive => " ex-tone-positive",
        CellTone.Negative => " ex-tone-negative",
        _ => throw new ArgumentOutOfRangeException(nameof(tone), tone, null),
    };

    private static int AlignIndex(CellAlign align) => align switch
    {
        CellAlign.Auto => 0,
        CellAlign.Left => 1,
        CellAlign.Center => 2,
        CellAlign.Right => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(align), align, null),
    };

    private static string AlignSuffix(CellAlign align) => align switch
    {
        CellAlign.Auto => "",
        CellAlign.Left => " ex-align-left",
        CellAlign.Center => " ex-align-center",
        CellAlign.Right => " ex-align-right",
        _ => throw new ArgumentOutOfRangeException(nameof(align), align, null),
    };

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
        CellTone[] tones = [CellTone.None, CellTone.Positive, CellTone.Negative];
        CellState[] states = [CellState.Normal, CellState.Stale, CellState.Missing, CellState.Error, CellState.Modified];
        CellAlign[] aligns = [CellAlign.Auto, CellAlign.Left, CellAlign.Center, CellAlign.Right];
        var composed = new string[tones.Length * States * Aligns * Variants];
        foreach (var tone in tones)
        {
            var toneSuffix = ToneSuffix(tone);
            foreach (var state in states)
            {
                var suffix = Suffix(state);
                foreach (var align in aligns)
                {
                    var alignSuffix = AlignSuffix(align);
                    for (var variant = 0; variant < Variants; variant++)
                    {
                        var numeric = (variant & 2) != 0;
                        var pinned = (variant & 1) != 0;
                        composed[(((ToneIndex(tone) * States + Index(state)) * Aligns) + AlignIndex(align)) * Variants + variant] = string.Concat(
                            "ex-cell",
                            numeric ? " ex-cell-numeric" : "",
                            pinned ? " ex-pinned" : "",
                            alignSuffix,
                            toneSuffix,
                            suffix);
                    }
                }
            }
        }

        return composed;
    }
}
