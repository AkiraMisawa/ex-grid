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
/// The Formula Bar's two fields as Chrome seams (ADR-0051 Consequences, ADR-0010/0030): the
/// core owns the boxes, the form, the keys and what the text means; a Chrome paints each
/// control from what the core hands it and calls back. Substituting Chrome changes rendering
/// and nothing about behaviour. 50 rows of 20px; Book is editable, Amount is not.
/// </summary>
public class FormulaBarChromeTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    private static string? Entry(TestRow row, GridColumn<TestRow> column)
        => column.Name == "Book" && row.Book == "Row 000002" ? "=A1*2" : null;

    /// <summary>A Chrome that paints only the bar's two fields, recording what it was handed.</summary>
    private sealed class BarChrome : IGridChrome
    {
        public NameBoxContext? NameBoxHanded { get; private set; }

        public FormulaBarTextContext? BarHanded { get; private set; }

        public RenderFragment? FilterPanel(FilterPanelContext context) => null;

        public RenderFragment? ColumnMenu(ColumnMenuContext context) => null;

        public RenderFragment? CellEditor(CellEditorContext context) => null;

        public RenderFragment? LoadingIndicator(LoadingContext context) => null;

        public RenderFragment? NameBox(NameBoxContext context)
        {
            NameBoxHanded = context;
            return builder => builder.AddMarkupContent(0, "<span class='stub-name-box'></span>");
        }

        public RenderFragment? FormulaBarText(FormulaBarTextContext context)
        {
            BarHanded = context;
            return builder => builder.AddMarkupContent(0, "<span class='stub-bar'></span>");
        }
    }

    /// <summary>A Chrome that paints none of the bar: the built-in inputs stay.</summary>
    private sealed class SilentChrome : IGridChrome
    {
        public RenderFragment? FilterPanel(FilterPanelContext context) => null;

        public RenderFragment? ColumnMenu(ColumnMenuContext context) => null;

        public RenderFragment? CellEditor(CellEditorContext context) => null;

        public RenderFragment? LoadingIndicator(LoadingContext context) => null;
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        IGridChrome chrome,
        List<GridEditIntent<TestRow>>? intents = null,
        Action<string>? onNameBox = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(50))
              .Add(g => g.TotalCount, 50)
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 200)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.ShowFormulaBar, true)
              .Add(g => g.EditorTextOf, Entry)
              .Add(g => g.NameBoxLabel, cell => FormattableString.Invariant($"R{cell.Row + 1}C{cell.Column + 1}"))
              .Add(g => g.Chrome, chrome)
              .Add(g => g.OnEdit, (GridEditIntent<TestRow> intent) => intents?.Add(intent));
            if (onNameBox is not null)
                ps.Add(g => g.OnNameBoxEntered, onNameBox);
        });

    private static Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    private static Task PressInBarAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, false, false, false, false, false, fromDescendant: true));

    private static string CellEditorText(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find(".ex-viewport .ex-editor").GetAttribute("value") ?? "";

    [Fact] // ADR-0051/0030: a Chrome's controls stand in the core's boxes — the Name Box's inside the form, the bar's inside an ex-editor box
    public void A_chromes_controls_stand_in_the_cores_boxes()
    {
        var cut = RenderGrid(new BarChrome());

        Assert.NotNull(cut.Find(".ex-formula-bar > .ex-name-box-form > div.ex-name-box > .stub-name-box"));
        Assert.NotNull(cut.Find(".ex-formula-bar > div.ex-editor.ex-formula-bar-text > .stub-bar"));
        Assert.Contains("width:", cut.Find("div.ex-name-box").GetAttribute("style"));
        Assert.Empty(cut.FindAll("input.ex-name-box"));
        Assert.Empty(cut.FindAll("input.ex-formula-bar-text"));
    }

    [Fact] // ADR-0010/0030: a Chrome that paints neither field leaves the built-in inputs
    public void A_chrome_that_paints_neither_field_leaves_the_built_in_inputs()
    {
        var cut = RenderGrid(new SilentChrome());

        Assert.NotNull(cut.Find(".ex-formula-bar > .ex-name-box-form > input.ex-name-box"));
        Assert.NotNull(cut.Find(".ex-formula-bar > input.ex-editor.ex-formula-bar-text"));
    }

    [Fact] // ADR-0051/0016 / DC-21: the core hands each field what the built-in shows
    public async Task Each_field_is_handed_what_the_built_in_shows()
    {
        var chrome = new BarChrome();
        var cut = RenderGrid(chrome);

        await ClickAsync(cut, 50, 50);                           // Book, row 2: an Entry
        Assert.Equal("R3C1", chrome.NameBoxHanded!.Text);
        Assert.Equal("=A1*2", chrome.BarHanded!.Text);
        Assert.False(chrome.BarHanded.ReadOnly);

        await ClickAsync(cut, 150, 10);                          // Amount, row 0: not editable
        Assert.Equal("R1C2", chrome.NameBoxHanded.Text);
        Assert.Equal("0", chrome.BarHanded.Text);
        Assert.True(chrome.BarHanded.ReadOnly);
    }

    [Fact] // ADR-0051 / DC-22: under a Chrome's bar too, one text in two places and one commit
    public async Task A_chromes_bar_is_the_editors_second_surface()
    {
        var chrome = new BarChrome();
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(chrome, intents);
        await ClickAsync(cut, 50, 50);

        await cut.InvokeAsync(() => chrome.BarHanded!.Focused());      // a press into the bar opens Caret
        Assert.Equal("=A1*2", CellEditorText(cut));
        await cut.InvokeAsync(() => chrome.BarHanded!.TextChanged("=A1*3"));
        Assert.Equal("=A1*3", CellEditorText(cut));
        Assert.Equal("=A1*3", chrome.BarHanded!.Text);

        await PressInBarAsync(cut, "Enter");

        Assert.Equal("=A1*3", Assert.Single(intents).Value);
    }

    [Fact] // ADR-0051/0010: a request for the keyboard while the user works in the bar goes to the Chrome's bar control
    public async Task A_focus_request_in_the_bar_reaches_the_chromes_control()
    {
        var chrome = new BarChrome();
        var cut = RenderGrid(chrome);
        await ClickAsync(cut, 50, 50);
        await cut.InvokeAsync(() => chrome.BarHanded!.Focused());
        var before = chrome.BarHanded!.FocusRequest;

        await PressInBarAsync(cut, "F2");                        // switches the mode, and asks for the keyboard

        Assert.NotEqual(before, chrome.BarHanded!.FocusRequest);
    }

    [Fact] // ADR-0051 / ADR-0050 item 4: under a Chrome's Name Box, Enter hands what was typed to the Consumer
    public async Task A_chromes_name_box_hands_what_was_typed_on_enter()
    {
        var chrome = new BarChrome();
        string? entered = null;
        var cut = RenderGrid(chrome, onNameBox: text => entered = text);
        await ClickAsync(cut, 50, 10);

        await cut.InvokeAsync(() => chrome.NameBoxHanded!.TextChanged("R41C2"));
        Assert.Equal("R41C2", chrome.NameBoxHanded!.Text);
        await cut.Find(".ex-name-box-form").SubmitAsync();

        Assert.Equal("R41C2", entered);
        Assert.Equal("R1C1", chrome.NameBoxHanded!.Text);        // back to the Focus's label
    }

    [Fact] // ADR-0051: leaving the Chrome's Name Box abandons what was typed there
    public async Task Leaving_a_chromes_name_box_abandons_the_typing()
    {
        var chrome = new BarChrome();
        var cut = RenderGrid(chrome);
        await ClickAsync(cut, 50, 10);

        await cut.InvokeAsync(() => chrome.NameBoxHanded!.TextChanged("R41"));
        await cut.InvokeAsync(() => chrome.NameBoxHanded!.Blurred());

        Assert.Equal("R1C1", chrome.NameBoxHanded!.Text);
    }

    [Fact] // ADR-0051/0010: a press into the Chrome's Name Box commits an open edit first
    public async Task A_press_into_a_chromes_name_box_commits_an_open_edit()
    {
        var chrome = new BarChrome();
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(chrome, intents);
        await ClickAsync(cut, 50, 10);
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("x", false, false, false, false, false));

        await cut.InvokeAsync(() => chrome.NameBoxHanded!.Focused());

        Assert.Equal("x", Assert.Single(intents).Value);
        Assert.Empty(cut.FindAll(".ex-viewport .ex-editor"));
    }
}
