using Bunit;
using ExGrid.Cells;
using ExGrid.Chrome;
using ExGrid.Columns;
using ExGrid.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Xunit;

namespace ExGrid.MudBlazor.Tests;

/// <summary>
/// The seams this package fills (ADR-0010/0030): the Cell Editor — a bare input in
/// the core's box, reporting its text — and the loading bar. Both render and call
/// back; neither decides anything.
/// </summary>
public class MudGridChromeTests : MudTestContext
{
    private IRenderedComponent<ExGrid<Trade>> RenderGrid(MudGridChrome chrome, bool loading = false)
        => Render<ExGrid<Trade>>(ps => ps
            .Add(g => g.Window, Rows(5))
            .Add(g => g.TotalCount, 5)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 400)
            .Add(g => g.IsLoading, loading)
            .Add(g => g.Chrome, chrome));

    private static Task ClickCellAsync(IRenderedComponent<ExGrid<Trade>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    [Fact] // ADR-0010/0030: the editor is a bare input inside the core's box, carrying the typed character
    public async Task The_editor_is_a_bare_input_in_the_cores_box()
    {
        var cut = RenderGrid(MudGridChrome.Default);
        await ClickCellAsync(cut, 50, 30);

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("x", false, false, false, false, false));

        var box = cut.Find(".ex-editor");
        var input = box.QuerySelector("input.mud-ex-editor");
        Assert.NotNull(input);
        Assert.Equal("x", input!.GetAttribute("value"));
        Assert.Null(box.QuerySelector(".mud-input"));
        Assert.Null(cut.Find(".ex-grid").QuerySelector(".mud-textfield"));
    }

    [Fact] // ADR-0010: what is typed reaches the core, and the commit leaves as an intent
    public async Task Typing_reaches_the_core_and_commits_as_an_intent()
    {
        GridEditIntent<Trade>? committed = null;
        var cut = Render<ExGrid<Trade>>(ps => ps
            .Add(g => g.Window, Rows(5))
            .Add(g => g.TotalCount, 5)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 400)
            .Add(g => g.Chrome, MudGridChrome.Default)
            .Add(g => g.OnEdit, intent => committed = intent));
        await ClickCellAsync(cut, 50, 30);
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("x", false, false, false, false, false));

        await cut.Find("input.mud-ex-editor").InputAsync(new ChangeEventArgs { Value = "xyz" });
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("Enter", false, false, false, false, false));

        Assert.NotNull(committed);
        Assert.Equal("xyz", committed!.Value);
        Assert.Equal("Book", committed.Column);
    }

    [Fact] // ADR-0051/0030 / DC-22: under this Chrome too, the Formula Bar and the editor show one text, and one commit
    public async Task The_formula_bar_and_the_chrome_editor_agree()
    {
        var intents = new List<GridEditIntent<Trade>>();
        var cut = Render<ExGrid<Trade>>(ps => ps
            .Add(g => g.Window, Rows(5))
            .Add(g => g.TotalCount, 5)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 400)
            .Add(g => g.Chrome, MudGridChrome.Default)
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.OnEdit, intents.Add));
        await ClickCellAsync(cut, 50, 30);
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("x", false, false, false, false, false));

        await cut.Find("input.mud-ex-editor").InputAsync(new ChangeEventArgs { Value = "xyz" });
        Assert.Equal("xyz", cut.Find(".ex-formula-bar-text input.mud-ex-formula-bar-text").GetAttribute("value"));

        await cut.Find(".ex-formula-bar-text input.mud-ex-formula-bar-text").FocusAsync(new FocusEventArgs());
        await cut.Find(".ex-formula-bar-text input.mud-ex-formula-bar-text").InputAsync(new ChangeEventArgs { Value = "xyzw" });
        Assert.Equal("xyzw", cut.Find("input.mud-ex-editor").GetAttribute("value"));

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("Enter", false, false, false, false, false, fromDescendant: true));
        Assert.Equal("xyzw", Assert.Single(intents).Value);
    }

    private IRenderedComponent<ExGrid<Trade>> RenderBarGrid(MudGridChrome chrome, Action<string>? onNameBox = null)
        => Render<ExGrid<Trade>>(ps =>
        {
            ps.Add(g => g.Window, Rows(5))
              .Add(g => g.TotalCount, 5)
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 200)
              .Add(g => g.ViewportWidth, 400)
              .Add(g => g.Chrome, chrome)
              .Add(g => g.ShowFormulaBar, true)
              .Add(g => g.NameBoxLabel, cell => FormattableString.Invariant($"R{cell.Row + 1}C{cell.Column + 1}"));
            if (onNameBox is not null)
                ps.Add(g => g.OnNameBoxEntered, onNameBox);
        });

    [Fact] // ADR-0051/0030: the Formula Bar's two fields are this Chrome's bare inputs in the core's boxes, named in its words
    public async Task The_formula_bar_fields_are_bare_inputs_in_the_cores_boxes()
    {
        var chrome = new MudGridChrome { Label = id => id == MudExGridWords.NameBox ? "Cell reference" : null };
        var cut = RenderBarGrid(chrome);
        await ClickCellAsync(cut, 50, 30);                       // Book, row 1

        var nameBox = cut.Find(".ex-formula-bar .ex-name-box-form > div.ex-name-box > input.mud-ex-name-box");
        Assert.Equal("R2C1", nameBox.GetAttribute("value"));
        Assert.Equal("Cell reference", nameBox.GetAttribute("aria-label"));
        var bar = cut.Find(".ex-formula-bar > div.ex-editor.ex-formula-bar-text > input.mud-ex-formula-bar-text");
        Assert.Equal("Formula Bar", bar.GetAttribute("aria-label"));
        Assert.Empty(cut.FindAll("input.ex-name-box"));
        Assert.Null(cut.Find(".ex-formula-bar").QuerySelector(".mud-textfield"));
    }

    [Fact] // ADR-0051 / ADR-0050 item 4: typed into this Chrome's Name Box, Enter hands the text to the Consumer
    public async Task The_name_box_hands_what_was_typed_on_enter()
    {
        string? entered = null;
        var cut = RenderBarGrid(MudGridChrome.Default, text => entered = text);
        await ClickCellAsync(cut, 50, 30);

        await cut.Find("input.mud-ex-name-box").FocusAsync(new FocusEventArgs());
        await cut.Find("input.mud-ex-name-box").InputAsync(new ChangeEventArgs { Value = "R4C1" });
        await cut.Find(".ex-name-box-form").SubmitAsync();

        Assert.Equal("R4C1", entered);
    }

    [Fact] // ADR-0051/0035: this Chrome's bar is read-only where the Focus cell does not edit
    public async Task The_bar_is_read_only_where_the_focus_cell_does_not_edit()
    {
        var cut = RenderBarGrid(MudGridChrome.Default);
        await ClickCellAsync(cut, 50, 30);                       // Book edits
        Assert.False(cut.Find("input.mud-ex-formula-bar-text").HasAttribute("readonly"));

        await ClickCellAsync(cut, 150, 30);                      // Amount does not
        Assert.True(cut.Find("input.mud-ex-formula-bar-text").HasAttribute("readonly"));
    }

    [Fact] // ADR-0010/0030: the loading bar is a MudProgressLinear in the Chrome's colour, only while loading
    public void The_loading_bar_shows_only_while_loading()
    {
        var chrome = new MudGridChrome { LoadingProgressColor = Color.Secondary };

        var idle = RenderGrid(chrome, loading: false);
        Assert.Empty(idle.FindAll(".mud-ex-grid-loading"));

        var loading = RenderGrid(chrome, loading: true);
        var bar = loading.Find(".mud-ex-grid-loading");
        Assert.Contains("mud-progress-linear", bar.ClassName);
        Assert.Contains("mud-progress-indeterminate", bar.ClassName);
        Assert.Contains("secondary", bar.ClassName);
        Assert.Contains("ex-loading", loading.Find(".ex-grid").ClassName);
    }

    [Fact] // ADR-0034/0030: a Reject reaches the control — aria-invalid, described by the message, the error underline
    public void A_reject_reaches_the_control()
    {
        RenderFragment? fragment = MudGridChrome.Default.CellEditor(new CellEditorContext(
            "Book", ColumnType.Text, "x", CellEditMode.Overwrite, "needs approval",
            _ => { }, () => { }, () => { }, MessageId: "ex1-message", FocusRequest: 3));
        var cut = Render(fragment!);

        var input = cut.Find("input.mud-ex-editor");
        Assert.Equal("true", input.GetAttribute("aria-invalid"));
        Assert.Equal("ex1-message", input.GetAttribute("aria-describedby"));
        Assert.Contains("mud-ex-editor-error", input.ClassName);

        var clean = Render(MudGridChrome.Default.CellEditor(new CellEditorContext(
            "Book", ColumnType.Text, "x", CellEditMode.Overwrite, null, _ => { }, () => { }, () => { }))!);
        Assert.Null(clean.Find("input.mud-ex-editor").GetAttribute("aria-invalid"));
        Assert.DoesNotContain("mud-ex-editor-error", clean.Find("input.mud-ex-editor").ClassName);
    }

    [Fact] // ADR-0028/0030: Material's dense is Compact, never Excel
    public void Dense_is_compact_not_excel()
    {
        Assert.Equal(GridDensity.Compact, MudExGridPresentation.DensityFor(dense: true));
        Assert.Equal(GridDensity.Standard, MudExGridPresentation.DensityFor(dense: false));
        Assert.Same(MudExGridPresentation.For(true, true), MudExGridPresentation.For(true, true));
        Assert.NotSame(MudExGridPresentation.For(true, true), MudExGridPresentation.For(true, false));
    }

    private static ValueTask<EditorCompletion?> Complete(string text, int caret)
        => ValueTask.FromResult<EditorCompletion?>(text == "=SU" && caret == 3
            ? new EditorCompletion(
                [new CompletionCandidate("SUM", 1, 2, "SUM("), new CompletionCandidate("SUMIF", 1, 2, "SUMIF(")],
                new EditorHint("SUM(number1, [number2], …)", 4, 7))
            : null);

    [Fact] // ADR-0051/0030 / DC-17: under this Chrome the list is a Material list in the core's box, and a press accepts
    public async Task The_completion_list_is_a_material_list_in_the_cores_box()
    {
        var cut = Render<ExGrid<Trade>>(ps => ps
            .Add(g => g.Window, Rows(5))
            .Add(g => g.TotalCount, 5)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 400)
            .Add(g => g.Chrome, MudGridChrome.Default)
            .Add(g => g.CompleteEditorText, Complete));
        await ClickCellAsync(cut, 50, 30);
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("=", false, false, false, false, false));

        await cut.Find("input.mud-ex-editor").InputAsync(new ChangeEventArgs { Value = "=SU" });

        var items = cut.FindAll(".ex-grid > .ex-completion .mud-ex-completion-list .mud-ex-completion-item");
        Assert.Equal(["SUM", "SUMIF"], items.Select(item => item.TextContent.Trim()));
        Assert.Contains("mud-selected-item", items[0].ClassName);
        Assert.Equal("number1", cut.Find(".ex-completion .mud-ex-completion-hint strong").TextContent);

        // ↓ is the core's: the Chrome is repainted with the choice it makes.
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, false, false, false, false));
        Assert.Contains("mud-selected-item", cut.FindAll(".mud-ex-completion-item")[1].ClassName);

        await cut.FindAll(".mud-ex-completion-item")[1].MouseDownAsync(new MouseEventArgs { Button = 0 });
        cut.WaitForAssertion(() => Assert.Equal("=SUMIF(", cut.Find("input.mud-ex-editor").GetAttribute("value")));
    }
}
