using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Components;

namespace ExGrid.Components;

// The Row Key (ADR-0140): a Consumer's value that names a row across versions. With one in force,
// the key — not the instance — is each row component's @key, so a row whose instance changes under
// the same key keeps its component, and Blazor's diff writes only the text and attributes that
// differ. Row Identity stays the change signal (ADR-0003): the row compares its row by reference,
// and a key never says that a row is unchanged. The grid compares no values and holds nothing
// between Windows; the key pairs one render's rows with the next one's, and nothing else.
//
// Every new Window is taken in here: checked for a row that appears twice (ADR-0003), or a key
// that does (ADR-0140, LV-2) — unless its source vouches for it (ADR-0141, LV-10).
public partial class ExGrid<TRow>
{
    /// <summary>
    /// A Consumer declaration, the Row Key (ADR-0140): a function from a row to the value that
    /// tells it from every other row and stays the same when the row's data changes — a trade's id,
    /// never its position. With it, a row whose instance changes under the same key keeps its row
    /// component, and only the text and attributes that differ are written: what a live screen
    /// saves, on a circuit above all. Row Identity stays the change signal (ADR-0003): a changed
    /// row is still a new instance, and a row rewritten in place still does not repaint.
    ///
    /// <para>Null, the default, takes the bound <see cref="Source"/>'s own
    /// <see cref="IGridSource{TRow}.RowKey"/>; with neither, each row is keyed by its instance, as
    /// without the declaration (DC-1). A key is compared with <see cref="object.Equals(object)"/>,
    /// so a value type or a record is a key as it is. A key that repeats within a Window, or a null
    /// key, is refused by name, naming the key and its positions.</para>
    ///
    /// <para>A row component then lives as long as its key is in the Window, not as long as its
    /// instance: whatever a Template cell's own controls hold survives a change of the row's
    /// values, and is handed the new version.</para>
    /// </summary>
    [Parameter] public Func<TRow, object>? RowKey { get; set; }

    // One key object per row instance, so @key is reference identity even when TRow
    // overrides Equals (a record row model): value-equal rows are still different rows
    // (ADR-0003), and Blazor's duplicate-@key refusal must not fire on them. Entries
    // die with their rows; the table is per closed generic type. Used while no Row Key is in force.
    private static readonly ConditionalWeakTable<TRow, object> RowIdentityKeys = new();

    // The Row Key in force for the rows painted — the grid's own, else the Source's, else none — and
    // whether the Window in hand was taken on its source's word. Both are what the last Window taken
    // in was checked under, so a new key over the same Window checks it again.
    private Func<TRow, object>? _rowKey;
    private bool _windowVouched;

    // Under a Row Key a row component outlives its row's instance, so nothing in it may stand for the
    // instance it was built with. Read against that (ADR-0140, Consequences), as of 2026-10-06:
    // - ExGridRow keeps what it painted only for ShouldRender (Record), the row among it; a new
    //   instance under the key is a reason to render, as for any row (ADR-0003).
    // - Its other caches are keyed by what composes them, never by the row: cell ids by position and
    //   prefix, the Action and Template paints by the column list. Each reads the row when it paints
    //   or when it is pressed, so a kept row paints and reports the version it now holds.
    // - The Change Highlight's earliest end is the component's, gathered afresh on every render and
    //   told to the grid when it moves: a kept row whose new version is unmarked drops its end, and
    //   the grid's one timer with it (ADR-0068).
    // - Engagement (ADR-0037), the Selection, the Focus and the editor's text (ADR-0027 P7) are the
    //   core's, by position, and handed to whichever component paints that position.
    // - What a Template cell's own components hold survives a change of the row's values and is
    //   handed the new version: the consequence ADR-0140 names, and the Consumer's to handle.
    // - A press on a kept row reads the row when it is handled. A press taken against an older render
    //   is ADR-0142's to judge, by what that render painted.
    // RowKeyTests holds a test for each.

    /// <summary>A painted row's component key: its Row Key while one is in force, else an object
    /// that stands for its instance (ADR-0140/0003).</summary>
    private object RowComponentKey(TRow row) => _rowKey is { } key ? key(row) : RowIdentityKeys.GetValue(row, static _ => new object());

    /// <summary>
    /// A Placeholder's component key: its position, in a type of the grid's own. A bare boxed index
    /// would equal a Row Key of the same number — a trade id of 7 beside the Placeholder at row 7 —
    /// and two siblings under one key are Blazor's refusal, or a row's component carried onto a
    /// Placeholder (ADR-0140). No Consumer's key can be of this type.
    /// </summary>
    private static object PlaceholderKey(int row) => new PlaceholderPosition(row);

    private readonly record struct PlaceholderPosition(int Row);

    /// <summary>
    /// Takes in the Window in hand, under the Row Key in force: checked whole when it is new or the
    /// key moved, and not at all when its source vouches for it (ADR-0141, LV-10). Refused up front
    /// rather than failing strangely mid-render, and before Blazor's own exception for clashing
    /// keys, which names neither the key nor the rows (LV-2).
    /// </summary>
    private void TakeInWindow()
    {
        var own = Source?.RowKey;
        var key = RowKey ?? own;
        // A vouch is taken only for the key the source refused repeats of: its own, whether the
        // grid was handed it or left to take it. A key of the Consumer's own is checked here, as is a
        // vouch with no Row Key behind it — "no row twice" is the source's word about its keys
        // (IGridSource.VouchesDistinctRows), and with no key it has none to give, so the grid's
        // check by instance stands. Delegates compare by method and target, so a source whose
        // RowKey property hands out a fresh delegate each time is still recognised.
        var vouched = key is not null && Source is { VouchesDistinctRows: true } && Equals(key, own);
        if (ReferenceEquals(_observedWindow, _window) && Equals(key, _rowKey) && (vouched || !_windowVouched))
            return;

        if (!vouched)
        {
            if (key is null)
                RequireDistinctRows(_window);
            else
                RequireDistinctKeys(_window, _windowStart, key);
        }
        _observedWindow = _window;
        _rowKey = key;
        _windowVouched = vouched;
    }

    // Refused up front rather than failing strangely mid-render: a duplicate instance
    // makes Row Identity ambiguous (ADR-0003 — which row did the Consumer mean?), and
    // a null is not a row (an absent row is a Placeholder, ADR-0004).
    //
    // A pass over the whole Window, which under GridSource.From is the whole result: 18.1 ms
    // at 451,115 rows (ADR-0141). A source that refuses a repeated Row Key vouches for its
    // Windows, and those skip it (TakeInWindow).
    private static void RequireDistinctRows(IReadOnlyList<TRow> window)
    {
        var seen = new HashSet<object>(window.Count, ReferenceEqualityComparer.Instance);
        for (var i = 0; i < window.Count; i++)
        {
            var row = window[i];
            if (row is null)
            {
                throw new InvalidOperationException(
                    $"Window[{i}] is null; a Window holds rows, not gaps.");
            }
            if (!seen.Add(row))
            {
                throw new InvalidOperationException(
                    $"Window[{i}] is the same row instance as an earlier position; Row Identity cannot tell them apart (ADR-0003).");
            }
        }
    }

    // The same pass by Row Key (ADR-0140, LV-2), which takes the place of the pass by instance: one
    // instance answers one key, so two positions holding the same row repeat a key too, and the
    // refusal is stricter, not weaker (ADR-0141's argument, made here for the grid's own check). A
    // key is compared as Blazor compares a @key, by Equals, so what is refused here is exactly what
    // Blazor would have refused — named, and before it.
    private static void RequireDistinctKeys(IReadOnlyList<TRow> window, int windowStart, Func<TRow, object> rowKey)
    {
        var seen = new Dictionary<object, int>(window.Count);
        for (var i = 0; i < window.Count; i++)
        {
            var row = window[i];
            if (row is null)
            {
                throw new InvalidOperationException(
                    $"Window[{i}] is null; a Window holds rows, not gaps.");
            }
            var key = rowKey(row);
            if (key is null)
            {
                throw new InvalidOperationException(
                    $"The Row Key of Window[{i}] (row {windowStart + i}) is null. A Row Key names its row across versions, " +
                    "and a null names none (ADR-0140).");
            }
            if (!seen.TryAdd(key, i))
            {
                var first = seen[key];
                throw new InvalidOperationException(
                    $"Window[{first}] and Window[{i}] (rows {windowStart + first} and {windowStart + i}) answer the same Row Key, " +
                    $"{Describe(key)}. A Row Key tells a row from every other, and two rows under one key would be painted as " +
                    "one (ADR-0140).");
            }
        }
    }

    /// <summary>A key as a refusal names it: its text, culture-invariant, and its type.</summary>
    private static string Describe(object key)
        => $"{(key is IFormattable formattable ? formattable.ToString(null, CultureInfo.InvariantCulture) : key.ToString())} " +
           $"({key.GetType().Name})";
}
