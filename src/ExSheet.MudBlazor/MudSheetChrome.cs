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
            _stylesheets = null;
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

    /// <summary>The slots the Grid Chrome fills over the Script Core (ADR-0410), forwarded, except the find panel:
    /// a Sheet's is its own, with Within (ADR-0332 section 5), and a grid's panel drawn in its place would search
    /// the Sheet shown alone; and except the Cell Editor and the Formula Bar's text, whose own fields colour a
    /// formula's References (ADR-0057), which a field drawn in the slot would show plain.</summary>
    public ScriptSlots ScriptSlots => GridSeams.ScriptSlots & ~(ScriptSlots.FindPanel | ScriptSlots.CellEditor | ScriptSlots.FormulaBarText);

    /// <summary>The Grid Chrome's column menu over the Script Core (ADR-0410), in ExSheet's words.</summary>
    public RenderFragment? ColumnMenu(GridMenuView view) => GridSeams.ColumnMenu(view);

    /// <summary>The Grid Chrome's Context Menu over the Script Core (ADR-0410), in ExSheet's words.</summary>
    public RenderFragment? ContextMenu(GridMenuView view) => GridSeams.ContextMenu(view);

    /// <summary>The Grid Chrome's loading bar over the Script Core (ADR-0410).</summary>
    public RenderFragment? LoadingIndicator(GridLoadingView view) => GridSeams.LoadingIndicator(view);

    /// <summary>The Grid Chrome's filter panel over the Script Core (ADR-0410).</summary>
    public RenderFragment? FilterPanel(GridFilterView view) => GridSeams.FilterPanel(view);

    /// <summary>No find panel over the Script Core: a Sheet's is its own, with Within (ADR-0332 section 5), and
    /// <see cref="ScriptSlots"/> leaves the slot to it (ADR-0410 section 4's note of 2026-10-28).</summary>
    public RenderFragment? FindPanel(GridFindView view) => null;

    /// <summary>No Cell Editor over the Script Core: a Sheet keeps the core's own, which colours a formula's References
    /// (ADR-0057), and <see cref="ScriptSlots"/> leaves the slot to it (ADR-0410).</summary>
    public RenderFragment? CellEditor(GridCellEditorView view) => null;

    /// <summary>No Formula Bar text over the Script Core: a Sheet keeps the core's own, which colours a formula's
    /// References (ADR-0057), and <see cref="ScriptSlots"/> leaves the slot to it (ADR-0410).</summary>
    public RenderFragment? FormulaBarText(GridFormulaBarTextView view) => null;

    /// <summary>The Grid Chrome's Name Box over the Script Core (ADR-0410).</summary>
    public RenderFragment? NameBox(GridNameBoxView view) => GridSeams.NameBox(view);

    /// <summary>The Sheet's own slots this Chrome fills over the Script Core (ADR-0410): the Toolbar Items, Format Cells,
    /// and the Sheet Tabs' faces and the + button's, drawn as they are over the C# core.</summary>
    public SheetScriptSlots SheetScriptSlots
        => SheetScriptSlots.ToolbarItem | SheetScriptSlots.FormatCells | SheetScriptSlots.SheetTab | SheetScriptSlots.SheetTabAdd;

    /// <summary>
    /// Format Cells over the Script Core (ADR-0071, ADR-0410): the tabs <see cref="FormatCells(FormatCellsContext)"/>'s
    /// dialog holds, in MudBlazor's type and buttons, standing in the grid's popover frame. Every choice is a native
    /// control the script reads, so a choice made at once before OK is the one OK sets.
    /// </summary>
    public RenderFragment? FormatCells(SheetFormatCellsView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return builder =>
        {
            builder.OpenComponent<MudScriptFormatCells>(0);
            builder.AddComponentParameter(1, nameof(MudScriptFormatCells.View), view);
            builder.CloseComponent();
        };
    }

    /// <summary>
    /// A Toolbar Item over the Script Core (ADR-0100, ADR-0410), in the MudBlazor controls <see cref="ToolbarItem(ToolbarItemContext)"/>
    /// draws: a toggle as a <c>MudIconButton</c> in the palette's primary while it is pressed, a menu as a
    /// <c>MudButton</c>, a split control as a <c>MudIconButton</c> beside its arrow, and a list as a <c>MudPaper</c> of
    /// choices, colours as Excel's palette ten across. Each control is marked for the script, which wires it.
    /// </summary>
    public RenderFragment? ToolbarItem(SheetToolbarItemView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return builder =>
        {
            builder.OpenComponent<MudScriptToolbarItem>(0);
            builder.AddComponentParameter(1, nameof(MudScriptToolbarItem.View), view);
            builder.CloseComponent();
        };
    }

    /// <summary>The Formula Bar's Name Box: <see cref="Grid"/>'s.</summary>
    public RenderFragment? NameBox(NameBoxContext context) => GridSeams.NameBox(context);

    /// <summary>The Formula Bar's text field: <see cref="Grid"/>'s.</summary>
    public RenderFragment? FormulaBarText(FormulaBarTextContext context) => GridSeams.FormulaBarText(context);

    /// <summary>The completion list and argument hint: <see cref="Grid"/>'s.</summary>
    public RenderFragment? EditorCompletion(EditorCompletionContext context) => GridSeams.EditorCompletion(context);

    /// <summary>The Selection Summary (ADR-0130), as the Grid Chrome draws it.</summary>
    public RenderFragment? SelectionSummary(SelectionSummaryContext context) => GridSeams.SelectionSummary(context);

    /// <summary>The stylesheets the Sheet's grid is dressed with (ADR-0123): ExSheet.MudBlazor's own,
    /// <c>mud-ex-sheet.css</c>, and <see cref="Grid"/>'s, <c>mud-ex-grid.css</c>, each with the stamp
    /// its package ships. The Sheet's grid renders a notice for each beside its own, which only
    /// another version's stylesheet shows.</summary>
    public IReadOnlyList<ChromeStylesheet> Stylesheets => _stylesheets ??=
        [new ChromeStylesheet(ShippedAssets.StylesheetName, ShippedAssets.StylesheetStamp), .. GridSeams.Stylesheets];

    private IReadOnlyList<ChromeStylesheet>? _stylesheets;

    /// <summary>The words a refusal is said in (ADR-0203): <see cref="Grid"/>'s, with ExSheet's own
    /// words beneath its <see cref="MudGridChrome.Label"/>, so a refusal on a Sheet is said once, in
    /// the Sheet's words.</summary>
    /// <param name="id">The id of the words asked for.</param>
    public string? Words(string id) => GridSeams.Words(id);

    /// <summary>
    /// A Toolbar Item of the Sheet Toolbar in MudBlazor's controls (ADR-0100): a toggle as a
    /// <c>MudToggleIconButton</c>, a list as a <c>MudMenu</c>, a split control as a
    /// <c>MudIconButton</c> beside one. What it means is ExSheet's, in <paramref name="context"/>.
    /// </summary>
    public RenderFragment? ToolbarItem(ToolbarItemContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return builder =>
        {
            builder.OpenComponent<MudToolbarItem>(0);
            builder.AddComponentParameter(1, nameof(MudToolbarItem.Context), context);
            builder.CloseComponent();
        };
    }

    /// <summary>
    /// A Sheet Tab's face in MudBlazor's type (ADR-0331 §4): the Sheet's name in a <c>MudText</c>, in the
    /// palette's primary on the Sheet shown. The tab around it, and what pressing it means, are ExSheet's.
    /// </summary>
    public RenderFragment? SheetTab(SheetTabContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return builder =>
        {
            builder.OpenComponent<global::MudBlazor.MudText>(0);
            builder.AddComponentParameter(1, nameof(global::MudBlazor.MudText.Typo), global::MudBlazor.Typo.body2);
            builder.AddComponentParameter(2, nameof(global::MudBlazor.MudText.HtmlTag), "span");
            builder.AddComponentParameter(3, nameof(global::MudBlazor.MudText.Color), context.Selected ? global::MudBlazor.Color.Primary : global::MudBlazor.Color.Inherit);
            builder.AddComponentParameter(4, nameof(global::MudBlazor.MudText.Class), "mud-ex-sheet-tab-face");
            builder.AddComponentParameter(5, nameof(global::MudBlazor.MudText.ChildContent), (RenderFragment)(name => name.AddContent(0, context.Name)));
            builder.CloseComponent();
        };
    }

    /// <summary>The face of the button that adds a Sheet: MudBlazor's Add icon (ADR-0331 §4).</summary>
    public RenderFragment? SheetTabAdd() => static builder =>
    {
        builder.OpenComponent<global::MudBlazor.MudIcon>(0);
        builder.AddComponentParameter(1, nameof(global::MudBlazor.MudIcon.Icon), global::MudBlazor.Icons.Material.Filled.Add);
        builder.AddComponentParameter(2, nameof(global::MudBlazor.MudIcon.Size), global::MudBlazor.Size.Small);
        builder.CloseComponent();
    };

    /// <summary>
    /// A Sheet Tab's menu as a <c>MudPaper</c> of commands, each with its <c>MudIcon</c> (ADR-0331 §4). ExSheet
    /// keeps the element around it that holds the keyboard; every command calls back into the context.
    /// </summary>
    public RenderFragment? SheetTabMenu(SheetTabMenuContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return builder =>
        {
            builder.OpenComponent<MudSheetTabMenu>(0);
            builder.AddComponentParameter(1, nameof(MudSheetTabMenu.Context), context);
            builder.CloseComponent();
        };
    }

    /// <summary>
    /// A Sheet's find panel in MudBlazor's controls (ADR-0332 §5): <see cref="Grid"/>'s find panel, control for
    /// control, with Within as two <c>MudButton</c>s, the chosen one filled. What a step finds and what Replace writes are ExSheet's,
    /// in <paramref name="context"/>.
    /// </summary>
    public RenderFragment? SheetFindPanel(SheetFindContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return builder =>
        {
            builder.OpenComponent<MudSheetFindPanel>(0);
            builder.AddComponentParameter(1, nameof(MudSheetFindPanel.Context), context);
            builder.CloseComponent();
        };
    }

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
