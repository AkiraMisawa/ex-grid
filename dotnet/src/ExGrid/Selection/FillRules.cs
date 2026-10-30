using ExGrid.Clipboard;

namespace ExGrid.Selection;

/// <summary>
/// What a fill-handle drag has reached (ADR-0050, item 5): the cells a release would fill,
/// which never include the source, and the direction they lie in from it.
/// </summary>
/// <param name="Target">The cells to fill, adjacent to the source along one axis and as
/// wide (or as tall) as it.</param>
/// <param name="Direction">Which way from the source the target lies.</param>
internal readonly record struct FillExtension(SelectionRange Target, GridDirection Direction);

/// <summary>
/// The Fill Intent (ADR-0050, item 5): a fill-handle drag, released. The grid writes
/// nothing — what a fill means (copies, a series, a refusal) is the Consumer's, which
/// resolves the intent into its own data and pushes new rows back, as with every edit
/// (ADR-0007). One drag is one intent.
///
/// <para>Positional, like a paste: the Consumer resolves the ranges under the order they
/// were written in, and a <see cref="RowSequenceVersion"/> that no longer matches its own
/// means the order moved under the intent, which must then be discarded (ADR-0011).</para>
///
/// <para>After a fill the Consumer accepts, the Selection is the source and the target
/// together, as in Excel; a Consumer that does not implement the pattern calls
/// <see cref="Refuse"/> before its handler completes, and the Selection stays on the source
/// (ADR-0050, item 5 refined).</para>
/// </summary>
public sealed class GridFillIntent
{
    internal GridFillIntent(SelectionRange source, FillExtension extension, int rowSequenceVersion)
    {
        Source = source;
        Target = extension.Target;
        Direction = extension.Direction;
        RowSequenceVersion = rowSequenceVersion;
    }

    /// <summary>The range the handle was dragged from: the Selection when the drag began.</summary>
    public SelectionRange Source { get; }

    /// <summary>The cells to fill, adjacent to the source along one axis; never the source
    /// itself. Every column it covers was Editable when the intent was raised (ADR-0035).</summary>
    public SelectionRange Target { get; }

    /// <summary>Which way from the source the target lies.</summary>
    public GridDirection Direction { get; }

    /// <summary>The order these positions are written in (ADR-0011).</summary>
    public int RowSequenceVersion { get; }

    /// <summary>Cancelled when the user cancels the fill with Escape while the Consumer is answering it (ADR-0380
    /// section 3): nothing is filled, and a handler that writes after the token is cancelled writes a fill the user
    /// took back.</summary>
    public CancellationToken CancellationToken { get; internal init; }

    /// <summary>Whether the Consumer refused the fill through <see cref="Refuse"/>.</summary>
    public bool IsRefused { get; private set; }

    /// <summary>
    /// The Consumer's answer that it does not fill this — a pattern it has not implemented is
    /// refused, never filled with copies (ADR-0050, item 5). Called before the <c>OnFill</c>
    /// handler completes, it leaves the Selection on the source; a handler that completes
    /// without calling it has accepted, and the Selection becomes the source and the target
    /// together. Telling the user why is the Consumer's: the grid knows only that it was
    /// refused, not what the pattern was.
    /// </summary>
    public void Refuse() => IsRefused = true;
}
