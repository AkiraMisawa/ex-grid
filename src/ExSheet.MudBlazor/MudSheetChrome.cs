using ExGrid.Chrome;
using ExGrid.MudBlazor;
using Microsoft.AspNetCore.Components;

namespace ExSheet.MudBlazor;

/// <summary>
/// MudBlazor for a Sheet (ADR-0071, ADR-0019's note of 2026-09-30): ExSheet's Format Cells drawn
/// as a <c>MudDialog</c>, a page-level modal MudBlazor already draws, and every seam of the grid
/// filled by <see cref="Grid"/>, ExGrid.MudBlazor's Chrome. Hand it to <c>ExSheet.Chrome</c>.
///
/// <para>Format Cells is the one seam whose frame is the Chrome's (ADR-0010's note of
/// 2026-09-30). What it offers and what OK sets stay ExSheet's: the tabs, the categories, the
/// palette and the line styles come from <see cref="FormatCellsOffer"/>, and every choice is set
/// through the context's <see cref="FormatCellsDraft"/>, so the same choices set the same Cell
/// Format under either Chrome. MudBlazor's controls draw it; More Colours is MudBlazor's colour
/// picker. While the dialog is open the keys are its own: it lies outside the Sheet's root, which
/// does not hear them (ADR-0018). When it closes, however it closes, the keyboard is handed back
/// through the core's focus function (ADR-0021's note of 2026-09-30). No script is added
/// (ADR-0021).</para>
///
/// <para>The page needs MudBlazor's <c>MudDialogProvider</c>, beside the theme and popover
/// providers ExGrid.MudBlazor asks for. Without one, Format Cells is refused by name at its first
/// opening rather than standing open where nothing draws it.</para>
/// </summary>
public sealed class MudSheetChrome : ISheetChrome
{
    /// <summary>The one every Sheet can share: Format Cells as a dialog, and <see cref="MudGridChrome.Default"/> for the grid.</summary>
    public static MudSheetChrome Default { get; } = new();

    private readonly MudGridChrome _grid = MudGridChrome.Default;
    private MudGridChrome? _worded;

    /// <summary>
    /// The Chrome that fills the grid's seams — the menus, the filter and find panels, the Cell
    /// Editor, the Formula Bar's fields, the completion list and the loading bar. A Consumer who
    /// words or decorates those passes a <see cref="MudGridChrome"/> of their own here. A menu
    /// words ExSheet's own commands, which ExGrid.MudBlazor does not know, by
    /// <see cref="SheetCommandIds.EnglishFor"/> wherever this Chrome's
    /// <see cref="MudGridChrome.Label"/> says nothing (ADR-0036: a substituted Chrome words a
    /// command from its id).
    /// </summary>
    public MudGridChrome Grid
    {
        get => _grid;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _grid = value;
            _worded = null;
        }
    }

    // Every seam of the grid's is Grid's, asked through the interface so that a seam Grid leaves
    // to the interface's default answers that default (ADR-0010) — with ExSheet's commands worded.
    private IGridChrome GridSeams => _worded ??= Worded(_grid);

    /// <summary>
    /// <paramref name="grid"/> as it is, with ExSheet's English beneath its <see cref="MudGridChrome.Label"/>.
    /// Every other property is carried over as given; a test holds this to every property
    /// <see cref="MudGridChrome"/> has.
    /// </summary>
    internal static MudGridChrome Worded(MudGridChrome grid) => new()
    {
        LoadingProgressColor = grid.LoadingProgressColor,
        Icon = grid.Icon,
        Label = grid.Label is { } label ? id => label(id) ?? SheetCommandIds.EnglishFor(id) : SheetCommandIds.EnglishFor,
    };

    /// <summary>The filter panel: <see cref="Grid"/>'s.</summary>
    public RenderFragment? FilterPanel(FilterPanelContext context) => GridSeams.FilterPanel(context);

    /// <summary>The column menu: <see cref="Grid"/>'s.</summary>
    public RenderFragment? ColumnMenu(ColumnMenuContext context) => GridSeams.ColumnMenu(context);

    /// <summary>The Context Menu, with ExSheet's commands in it: <see cref="Grid"/>'s.</summary>
    public RenderFragment? ContextMenu<TRow>(ContextMenuContext<TRow> context) => GridSeams.ContextMenu(context);

    /// <summary>A cell's message: <see cref="Grid"/>'s.</summary>
    public RenderFragment? CellMessage(CellMessageContext context) => GridSeams.CellMessage(context);

    /// <summary>The Cell Editor: <see cref="Grid"/>'s.</summary>
    public RenderFragment? CellEditor(CellEditorContext context) => GridSeams.CellEditor(context);

    /// <summary>The find panel: <see cref="Grid"/>'s.</summary>
    public RenderFragment? FindPanel(FindContext context) => GridSeams.FindPanel(context);

    /// <summary>The loading bar: <see cref="Grid"/>'s.</summary>
    public RenderFragment? LoadingIndicator(LoadingContext context) => GridSeams.LoadingIndicator(context);

    /// <summary>The Formula Bar's Name Box: <see cref="Grid"/>'s.</summary>
    public RenderFragment? NameBox(NameBoxContext context) => GridSeams.NameBox(context);

    /// <summary>The Formula Bar's text field: <see cref="Grid"/>'s.</summary>
    public RenderFragment? FormulaBarText(FormulaBarTextContext context) => GridSeams.FormulaBarText(context);

    /// <summary>The completion list and argument hint: <see cref="Grid"/>'s.</summary>
    public RenderFragment? EditorCompletion(EditorCompletionContext context) => GridSeams.EditorCompletion(context);

    /// <summary>
    /// Format Cells as a <c>MudDialog</c> (ADR-0071). ExSheet renders the fragment beside the grid
    /// from the opening until OK or Cancel; each opening is a dialog of its own.
    /// </summary>
    public RenderFragment? FormatCells(FormatCellsContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return builder =>
        {
            builder.OpenComponent<MudFormatCellsFrame>(0);
            // A new opening is a new frame, and a new dialog: the one it replaces closes.
            builder.SetKey(context);
            builder.AddComponentParameter(1, nameof(MudFormatCellsFrame.Context), context);
            builder.CloseComponent();
        };
    }
}
