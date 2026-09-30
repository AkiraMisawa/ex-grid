using Bunit;
using ExGrid.Cells;
using ExGrid.Chrome;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// An edit that opens takes the keyboard only while the keyboard is still this grid's (ADR-0021's
/// note of 2026-09-30, ADR-0018 section 6, ED-28). The core's request that an editor surface take
/// DOM focus — on opening, on F2, after a Reject — goes through the module's conditional
/// <c>focusEditor</c>, never Blazor's <c>FocusAsync</c>, which takes focus wherever it is; a
/// Chrome's control asks through its context's <c>TakeFocus</c>. Whether the request is granted
/// is the browser's (layer 3); that it is asked this way is pinned here. 20px rows; Book is
/// editable.
/// </summary>
public class EditorFocusTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns(Func<TestRow, string, EditVerdict>? validate = null) =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true, validate: validate),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        IGridChrome? chrome = null,
        Func<TestRow, string, EditVerdict>? validate = null,
        bool formulaBar = false)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(50))
              .Add(g => g.TotalCount, 50)
              .Add(g => g.Columns, Columns(validate))
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 200)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.ShowFormulaBar, formulaBar);
            if (chrome is not null)
                ps.Add(g => g.Chrome, chrome);
        });

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, bool fromBar = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, false, false, false, false, false, fromDescendant: fromBar));

    private static Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    /// <summary>The surfaces the core asked the module to focus, in order: false for the Cell
    /// Editor, true for the Formula Bar's text.</summary>
    private List<bool> SurfacesAsked()
        => [.. Js.EditorFocusAsked.Invocations.Select(i => (bool)i.Arguments[0]!)];

    private int BlazorFocusCalls()
        => JSInterop.Invocations.Count(i => i.Identifier == GridJSInterop.BlazorFocus);

    /// <summary>A Chrome that paints the Cell Editor and the bar's text, keeping the contracts it
    /// was last handed.</summary>
    private sealed class EditorChrome : IGridChrome
    {
        public CellEditorContext? EditorHanded { get; private set; }

        public FormulaBarTextContext? BarHanded { get; private set; }

        public RenderFragment? FilterPanel(FilterPanelContext context) => null;

        public RenderFragment? ColumnMenu(ColumnMenuContext context) => null;

        public RenderFragment? CellEditor(CellEditorContext context)
        {
            EditorHanded = context;
            return builder => builder.AddMarkupContent(0, "<input class='stub-editor' />");
        }

        public RenderFragment? LoadingIndicator(LoadingContext context) => null;

        public RenderFragment? FormulaBarText(FormulaBarTextContext context)
        {
            BarHanded = context;
            return builder => builder.AddMarkupContent(0, "<input class='stub-bar' />");
        }
    }

    [Fact] // ADR-0021 (note of 2026-09-30) / ED-28: an edit opened by typing asks the module for the Cell Editor's focus, never Blazor's FocusAsync
    public async Task An_edit_opened_by_typing_asks_the_module_for_the_cell_editors_focus()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 10);

        await PressAsync(cut, "5");

        Assert.Equal([false], SurfacesAsked());
        Assert.Equal(0, BlazorFocusCalls());
    }

    [Fact] // ADR-0021 (note of 2026-09-30) / ADR-0010 / ED-28: F2 asks again, through the module, as the opening did
    public async Task F2_asks_the_module_again()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 10);

        await PressAsync(cut, "F2");
        await PressAsync(cut, "F2");

        Assert.Equal([false, false], SurfacesAsked());
        Assert.Equal(0, BlazorFocusCalls());
    }

    [Fact] // ADR-0021 (note of 2026-09-30) / ADR-0034 / ED-28: a Reject asks for the editor again, through the module
    public async Task A_reject_asks_the_module_again()
    {
        var cut = RenderGrid(validate: (_, _) => EditVerdict.Reject("no"));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "5");

        await PressAsync(cut, "Enter");

        Assert.NotEmpty(cut.FindAll(".ex-editor"));
        Assert.Equal([false, false], SurfacesAsked());
        Assert.Equal(0, BlazorFocusCalls());
    }

    [Fact] // ADR-0021 (note of 2026-09-30) / ADR-0051 / ED-28: a request made while the user works in the Formula Bar asks for the bar, through the module
    public async Task A_request_in_the_formula_bar_asks_the_module_for_the_bar()
    {
        var cut = RenderGrid(formulaBar: true);
        await ClickCellAsync(cut, 50, 10);
        await cut.Find(".ex-formula-bar-text").FocusAsync(new FocusEventArgs());

        await PressAsync(cut, "F2", fromBar: true);

        Assert.Equal([true], SurfacesAsked());
        Assert.Equal(0, BlazorFocusCalls());
    }

    [Fact] // ADR-0010 / ADR-0021 (note of 2026-09-30) / ED-28: under a Chrome the core asks nothing itself; the control asks through its context, and the module finds it in the core's box
    public async Task A_chromes_controls_ask_through_their_contexts()
    {
        var chrome = new EditorChrome();
        var cut = RenderGrid(chrome, formulaBar: true);
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "5");

        // The core holds no reference to the Chrome's control, so it does not focus it itself.
        Assert.Empty(SurfacesAsked());
        Assert.NotNull(chrome.EditorHanded!.TakeFocus);
        Assert.NotNull(chrome.BarHanded!.TakeFocus);

        await cut.InvokeAsync(() => chrome.EditorHanded!.TakeFocus!());
        await cut.InvokeAsync(() => chrome.BarHanded!.TakeFocus!());

        Assert.Equal([false, true], SurfacesAsked());
        Assert.Equal(0, BlazorFocusCalls());
    }

    [Fact] // ADR-0010: the contexts carry the same focus function from render to render, so a Chrome's control is not re-rendered for it
    public async Task A_chromes_focus_function_is_the_same_delegate_each_render()
    {
        var chrome = new EditorChrome();
        var cut = RenderGrid(chrome, formulaBar: true);
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "5");
        var editor = chrome.EditorHanded!.TakeFocus;
        var bar = chrome.BarHanded!.TakeFocus;

        cut.Render();

        Assert.Same(editor, chrome.EditorHanded!.TakeFocus);
        Assert.Same(bar, chrome.BarHanded!.TakeFocus);
    }
}
