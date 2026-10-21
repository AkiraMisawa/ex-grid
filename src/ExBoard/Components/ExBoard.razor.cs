using System.Globalization;
using ExChart.Chrome;
using ExGrid.Chrome;
using ExGrid.Data;
using ExGrid.Selection;
using ExPivot.Chrome;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;

namespace ExBoard.Components;

/// <summary>
/// Draws a Board (ADR-0340): its views laid out in its rows, each input bound to the Snapshot
/// <see cref="Data"/> holds under its name and checked against its declaration first (ADR-0341). A
/// Board that does not bind draws nothing but the refusal, naming the input or the path. Running a
/// Board never rewrites it.
/// </summary>
public partial class ExBoard
{
    /// <summary>The gap between views and between rows, in CSS pixels.</summary>
    public const double Gap = 12;

    /// <summary>The height a view's title takes, in CSS pixels, emitted on the title itself so the
    /// view under it is drawn at exactly what is left.</summary>
    public const double TitleHeight = 28;

    private BoardReading? _boundBoard;
    private IReadOnlyDictionary<string, Snapshot>? _boundData;
    private CultureInfo? _boundCulture;
    private BoundBoard? _bound;
    private string? _refusal;
    private readonly Dictionary<string, PivotLayout> _layouts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EventCallback<PivotLayout>> _layoutChanged = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EventCallback<GridSelection>> _selectionChanged = new(StringComparer.Ordinal);

    /// <summary>The Board to draw, as <see cref="BoardSpec.Read(string)"/> read it.</summary>
    [Parameter, EditorRequired]
    public BoardReading Board { get; set; } = null!;

    /// <summary>The Snapshot each input is bound to, by the input's name. A new dictionary binds
    /// again; the same one is not read twice.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyDictionary<string, Snapshot> Data { get; set; } = null!;

    /// <summary>The Board's width in CSS pixels, from which a chart's width is its share of its row
    /// (a chart is drawn at a width it is told). Grids and pivots stretch to their share.</summary>
    [Parameter]
    public double Width { get; set; } = 1200;

    /// <summary>The culture a grid's text is written in; the invariant culture by default.</summary>
    [Parameter]
    public CultureInfo? Culture { get; set; }

    /// <summary>The grid Chrome the grids and pivots are drawn with; the built-in one by default.</summary>
    [Parameter]
    public IGridChrome? Chrome { get; set; }

    /// <summary>The pivot Chrome; the built-in one by default.</summary>
    [Parameter]
    public IPivotChrome? PivotChrome { get; set; }

    /// <summary>Raised when the user confirms an output the Board declares (ADR-0340, Q6), and only
    /// then: a selection on the way raises nothing.</summary>
    [Parameter]
    public EventCallback<BoardOutput> OnOutput { get; set; }

    /// <summary>The chart Chrome; the built-in one by default.</summary>
    [Parameter]
    public IChartChrome? ChartChrome { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Board);
        ArgumentNullException.ThrowIfNull(Data);
        if (ReferenceEquals(Board, _boundBoard) && ReferenceEquals(Data, _boundData) && Equals(Culture, _boundCulture))
            return;
        _boundBoard = Board;
        _boundData = Data;
        _boundCulture = Culture;
        _layouts.Clear();
        _layoutChanged.Clear();
        _selectionChanged.Clear();
        try
        {
            _bound = BoundBoard.Bind(Board, Data, Culture);
            _refusal = null;
        }
        catch (BoardRefusedException refused)
        {
            _bound = null;
            _refusal = refused.Path.Length == 0 ? refused.Message : $"{refused.Path}: {refused.Message}";
            return;
        }
        foreach (var view in _bound.Views.OfType<BoundPivotView>())
        {
            var id = view.View.Id;
            _layouts[id] = view.Layout;
            // Cached, so a render does not hand the pivot a new delegate (AGENTS.md).
            _layoutChanged[id] = EventCallback.Factory.Create<PivotLayout>(this, layout => _layouts[id] = layout);
        }
        foreach (var from in _bound.Board.Links.Select(l => l.From).Concat(_bound.Board.Outputs.Select(o => o.From)).Distinct())
            _selectionChanged[from] = EventCallback.Factory.Create<GridSelection>(this, selection => Select(from, selection));
    }

    private EventCallback<GridSelection> SelectionChangedOf(string view) => _selectionChanged.GetValueOrDefault(view);

    // A grid's selection narrows the views its links reach; a dropped selection, which the grid raises
    // as an empty one, takes the narrowing away with it (ADR-0011).
    private void Select(string from, GridSelection selection) => _bound?.Select(from, selection);

    private Task ConfirmAsync(string output) =>
        _bound?.Confirm(output) is { } raised ? OnOutput.InvokeAsync(raised) : Task.CompletedTask;

    private static string RefusalStyle(BoardRow row) => FormattableString.Invariant($"height: {row.Height}px");

    private PivotLayout LayoutOf(BoundPivotView pivot) => _layouts[pivot.View.Id];

    private static string RootStyle => FormattableString.Invariant($"display: flex; flex-direction: column; gap: {Gap}px");

    // A row's height is each view's own: a grid's or a pivot's viewport, a chart's stage. A chart's
    // toolbar sits above its stage at the height its stylesheet gives it, so the row grows to hold
    // it rather than pair a pixel here with one there; nothing is clipped.
    private static string RowStyle(BoardRow row) =>
        FormattableString.Invariant($"display: flex; gap: {Gap}px; align-items: flex-start");

    private static string CellStyle(BoardCell cell) =>
        FormattableString.Invariant($"flex: {cell.Width} 1 0; min-width: 0; display: flex; flex-direction: column");

    private static string ConfirmStyle => FormattableString.Invariant($"margin-top: {Gap / 2}px; align-self: flex-start");

    private static string TitleStyle => FormattableString.Invariant(
        $"margin: 0; height: {TitleHeight}px; line-height: {TitleHeight}px; font-size: inherit; overflow: hidden; white-space: nowrap; text-overflow: ellipsis");

    private double ChartWidth(BoardRow row, BoardCell cell, int shares) =>
        Math.Max(120, Math.Floor((Width - (Gap * (row.Cells.Count - 1))) * cell.Width / shares));

}
