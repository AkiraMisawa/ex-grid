using Bunit;
using ExGrid.Cells;
using ExGrid.Chrome;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// FN-17 (ADR-0009/0010/0034/0036): the Chrome seams are substitutable, and swapping Chrome
/// changes rendering and nothing about behaviour. One script of keys and mouse gestures runs
/// against the built-in Chrome and against a stub that replaces every seam it touches — the
/// column menu, the filter panel, the Context Menu, the cell's message and the loading
/// indicator — and the two runs must leave the same trace: the selections told, the sorts,
/// the Edit Intents, the Consumer's command invoked, the message's text and the loading
/// state. Under the stub each seam must also have been the stub's, so neither run passes by
/// falling back to the other's markup.
/// </summary>
public class ChromeSubstitutionTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    /// <summary>A Chrome that replaces every seam the script reaches, records what the core
    /// handed it, and paints a marker of its own.</summary>
    private sealed class StubChrome : IGridChrome
    {
        public ColumnMenuContext? Menu { get; private set; }

        public FilterPanelContext? Panel { get; private set; }

        public object? ContextMenuContext { get; private set; }

        public IReadOnlyList<GridCommand> ContextCommands { get; private set; } = [];

        public CellMessageContext? Message { get; private set; }

        public LoadingContext? Loading { get; private set; }

        public RenderFragment? ColumnMenu(ColumnMenuContext context)
        {
            Menu = context;
            return b => b.AddMarkupContent(0, "<div class='stub-menu'></div>");
        }

        public RenderFragment? FilterPanel(FilterPanelContext context)
        {
            Panel = context;
            return b => b.AddMarkupContent(0, "<div class='stub-panel'></div>");
        }

        public RenderFragment? ContextMenu<TRow>(ContextMenuContext<TRow> context)
        {
            ContextMenuContext = context;
            ContextCommands = context.Commands;
            return b => b.AddMarkupContent(0, "<div class='stub-context'></div>");
        }

        public RenderFragment? CellMessage(CellMessageContext context)
        {
            Message = context;
            return b => b.AddMarkupContent(0, "<div class='stub-message'></div>");
        }

        public RenderFragment? CellEditor(CellEditorContext context) => null;

        public RenderFragment? LoadingIndicator(LoadingContext context)
        {
            Loading = context;
            return b => b.AddMarkupContent(0, "<div class='stub-loading'></div>");
        }
    }

    private sealed class Trace
    {
        public List<string> Lines { get; } = [];

        public void Add(string line) => Lines.Add(line);
    }

    private static readonly TestRow[] Rows = TestRows.Many(50);

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(IGridChrome? chrome, Trace trace, bool loading = false)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, Rows)
              .Add(g => g.TotalCount, Rows.Length)
              .Add(g => g.Columns, (GridColumn<TestRow>[])
              [
                  new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
                  new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
              ])
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 160)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.IsLoading, loading)
              .Add(g => g.SelectionChanged, (GridSelection s) =>
                  trace.Add($"selection {string.Join(" ", s.Ranges)} focus {s.Focus}"))
              .Add(g => g.OnSortChanged, (IReadOnlyList<SortSpec> sorts) =>
                  trace.Add($"sort {string.Join(" ", sorts.Select(x => $"{x.Column}:{x.Direction}"))}"))
              .Add(g => g.OnEdit, (GridEditIntent<TestRow> intent) =>
                  trace.Add($"edit {intent.Row.Book} {intent.Column} '{intent.Value}'"))
              .Add(g => g.CellMessageOf, (row, column) =>
                  row.Amount == 3 && column.Name == "Amount" ? "three is flagged" : null)
              .Add(g => g.CommandLabel, id => id == "note" ? "Note this row" : null)
              .Add(g => g.ContextCommands, context =>
                  [new GridCommand("note", true, () =>
                  {
                      trace.Add($"note {context.Row.Book} {context.Column}");
                      return Task.CompletedTask;
                  })]);
            if (chrome is not null)
                ps.Add(g => g.Chrome, chrome);
        });

    private static Task KeyAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false, bool shift = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, shift, false, false, false));

    private static async Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y, bool shift = false)
    {
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y, ShiftKey = shift });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0, OffsetX = x, OffsetY = y });
    }

    /// <summary>The script both Chromes run. Where a gesture lands on the Chrome's own markup —
    /// a menu item, the panel's Apply — the built-in markup is pressed, and the stub is driven
    /// through the context the core handed it, as a substitute's markup would.</summary>
    private async Task<List<string>> RunScriptAsync(StubChrome? stub)
    {
        var trace = new Trace();
        var cut = RenderGrid(stub, trace);

        // Mouse and keys: a click, an extension, a move, an edit committed with Enter.
        await ClickAsync(cut, 50, 30);                       // (1, 0)
        await ClickAsync(cut, 150, 70, shift: true);         // (1, 0)-(3, 1)
        await KeyAsync(cut, "ArrowDown");
        await KeyAsync(cut, "ArrowLeft");
        await KeyAsync(cut, "ArrowUp", shift: true);
        await KeyAsync(cut, "a", ctrl: true);
        await ClickAsync(cut, 50, 10);                       // (0, 0)
        await KeyAsync(cut, "x");
        await cut.Find(".ex-editor").InputAsync(new ChangeEventArgs { Value = "xyz" });
        await KeyAsync(cut, "Enter");

        // The column menu: Sort ascending on Book.
        await cut.FindAll(".ex-menu-button")[0].ClickAsync(new MouseEventArgs());
        if (stub is null)
            await cut.FindAll(".ex-popover button[role=menuitem]").Single(b => b.TextContent.StartsWith("Sort ascending", StringComparison.Ordinal)).ClickAsync(new MouseEventArgs());
        else
            await cut.InvokeAsync(() => stub.Menu!.Commands.Single(c => c.Id == GridCommandIds.SortAscending).Invoke());

        // The Context Menu on (2, 1): the Consumer's own command.
        await cut.Find(".ex-viewport").ContextMenuAsync(new MouseEventArgs { Button = 2, OffsetX = 150, OffsetY = 50 });
        if (stub is null)
            await cut.FindAll(".ex-popover[role=menu] button[role=menuitem]").Single(b => b.TextContent.StartsWith("Note this row", StringComparison.Ordinal)).ClickAsync(new MouseEventArgs());
        else
            await cut.InvokeAsync(() => stub.ContextCommands.Single(c => c.Id == "note").Invoke());
        await KeyAsync(cut, "Escape");

        // The cell's message: the pointer comes to rest on row 3's Amount.
        await cut.InvokeAsync(() => cut.Instance.OnPointerRestAsync(150, 70));
        trace.Add(stub is null
            ? $"message {cut.FindAll(".ex-message").SingleOrDefault()?.TextContent.Trim()}"
            : $"message {stub.Message?.Message}");

        // Loading: the Consumer says it is fetching, then that it is not.
        var loadingGrid = RenderGrid(stub, new Trace(), loading: true);
        trace.Add(stub is null
            ? $"loading {loadingGrid.Find(".ex-grid").ClassList.Contains("ex-loading")}"
            : $"loading {stub.Loading?.IsLoading}");

        if (stub is not null)
        {
            // Every seam the script crossed was the stub's.
            Assert.NotNull(stub.Menu);
            Assert.NotNull(stub.ContextMenuContext);
            Assert.NotNull(stub.Message);
            Assert.NotNull(stub.Loading);
            Assert.Empty(cut.FindAll(".ex-popover button[role=menuitem]"));
        }
        return trace.Lines;
    }

    [Fact] // FN-17, ADR-0009/0010/0034/0036: the same key and mouse script leaves the same trace under the built-in Chrome and a stub
    public async Task FN17_swapping_chrome_changes_rendering_and_nothing_about_behaviour()
    {
        var builtIn = await RunScriptAsync(null);
        var stubbed = await RunScriptAsync(new StubChrome());

        Assert.Equal(builtIn, stubbed);
        // And the trace is the behaviour, not an empty agreement.
        Assert.Contains(builtIn, l => l.StartsWith("edit Row 000000 Book 'xyz'", StringComparison.Ordinal));
        Assert.Contains("sort Book:Ascending", builtIn);
        Assert.Contains(builtIn, l => l.StartsWith("note ", StringComparison.Ordinal));
        Assert.Contains("message three is flagged", builtIn);
        Assert.Contains("loading True", builtIn);
    }
}
