using ExGrid.Cells;
using ExGrid.Columns;

namespace ExGrid.Components;

/// <summary>
/// The class vocabulary a cell is painted with, resolved to one interned string per
/// combination. It lives in the component layer for the reason <see cref="ColumnStyles"/>
/// does: the core decides the meanings — is this a numeric presentation (ADR-0016), is
/// this cell Stale (ADR-0006) — and this answers how those are written down for a
/// browser. The class names it writes are the seam a replacement Chrome (ADR-0010) paints
/// against, and a theme styles them rather than inventing its own; the helper that composes
/// them is the grid's own (ADR-0201).
///
/// <para>Every combination is composed once, up front. The alternative — concatenating
/// two or three class names per cell — allocates a string for each of the ~800 cells on
/// every render, on the exact path ADR-0004 caps. A few hundred strings held forever is
/// the cheaper end of that trade by a wide margin.</para>
/// </summary>
internal static class CellClasses
{
    // Indexed by (mark, changed, tone, state, align, numeric, pinned) so the lookup is arithmetic,
    // never a switch over combinations that would have to be kept in step with the enums by
    // hand.
    private const int Variants = 4;
    private const int Aligns = 4;
    private const int States = 5;
    private const int Tones = 3;
    private const int Changes = 2;
    private static readonly string[] Composed = Compose();

    /// <summary>
    /// The full class attribute for one cell. <paramref name="numeric"/> is the core's
    /// classification (<see cref="Columns.OverflowRules.HashesWhenOverflowing"/>), not a
    /// re-derivation of it; <see cref="CellState.Normal"/>, <see cref="CellAlign.Auto"/>,
    /// <see cref="CellTone.None"/> and an unchanged cell add nothing at all — an ordinary
    /// cell is painted exactly as it was before any of the vocabularies existed
    /// (ADR-0006/0016/0068). <paramref name="changed"/> is the Change Highlight's
    /// <c>ex-changed</c>: the Consumer said the cell's shown value changed less than the
    /// highlight's duration ago (ADR-0068). <paramref name="mark"/> is the Match Mark: the cell
    /// matches what the find panel holds, <c>ex-match-mark</c>, and is the current match besides,
    /// <c>ex-match-mark-current</c> as well (ADR-0332 §4).
    /// </summary>
    public static string For(bool numeric, bool pinned, CellState state, CellAlign align = CellAlign.Auto, CellTone tone = CellTone.None, bool changed = false, MatchMark mark = MatchMark.None)
        => Composed[IndexOf(mark, changed, tone, state, align) * Variants + (numeric ? 2 : 0) + (pinned ? 1 : 0)];

    private static int IndexOf(MatchMark mark, bool changed, CellTone tone, CellState state, CellAlign align)
        => ((((MarkIndex(mark) * Changes) + (changed ? 1 : 0)) * Tones + ToneIndex(tone)) * States + Index(state)) * Aligns + AlignIndex(align);

    private static int MarkIndex(MatchMark mark) => mark switch
    {
        MatchMark.None => 0,
        MatchMark.Match => 1,
        MatchMark.Current => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(mark), mark, null),
    };

    private static string MarkSuffix(MatchMark mark) => mark switch
    {
        MatchMark.None => "",
        MatchMark.Match => " ex-match-mark",
        MatchMark.Current => " ex-match-mark ex-match-mark-current",
        _ => throw new ArgumentOutOfRangeException(nameof(mark), mark, null),
    };

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
        MatchMark[] marks = [MatchMark.None, MatchMark.Match, MatchMark.Current];
        bool[] changes = [false, true];
        var composed = new string[marks.Length * Changes * Tones * States * Aligns * Variants];
        foreach (var mark in marks)
        {
            var markSuffix = MarkSuffix(mark);
            foreach (var changed in changes)
            {
                var changedSuffix = changed ? " ex-changed" : "";
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
                                composed[IndexOf(mark, changed, tone, state, align) * Variants + variant] = string.Concat(
                                    "ex-cell",
                                    numeric ? " ex-cell-numeric" : "",
                                    pinned ? " ex-pinned" : "",
                                    alignSuffix,
                                    toneSuffix,
                                    suffix,
                                    changedSuffix,
                                    markSuffix);
                            }
                        }
                    }
                }
            }
        }

        return composed;
    }
}

/// <summary>Whether a painted cell wears the Match Mark (ADR-0332 §4): not at all, as a match of
/// what the find panel holds, or as the current match — the Focus a found step moved to.</summary>
internal enum MatchMark
{
    /// <summary>Not a match, or no search is held.</summary>
    None,

    /// <summary>A match among the painted cells.</summary>
    Match,

    /// <summary>The current match.</summary>
    Current,
}
