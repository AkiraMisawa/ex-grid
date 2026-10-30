using System.Text.Json;
using Bunit;
using ExGrid.Chrome;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// ADR-0410 sections 1 and 3: Blazor's ExGrid over the Script Core names the slots its Chrome fills, renders each
/// slot the script mounts beside its root in a hidden holder, drawn by the Chrome's view overload from the context
/// the script sent as data, hands the script the element once drawn, and removes the holder when the script says
/// the slot is gone. Moving the element and wiring its controls are the script's, tested there and in layer 3.
/// </summary>
public class ScriptGridChromeTests : BunitContext
{
    private readonly BunitJSModuleInterop _module;
    private readonly BunitJSModuleInterop _grid;

    public ScriptGridChromeTests()
    {
        _module = JSInterop.SetupModule(invocation => invocation.Identifier == "import");
        _module.Setup<string?>("stamp").SetResult(global::ExGrid.Components.ScriptCoreAsset.Stamp);
        _grid = _module.SetupModule("attach", _ => true);
        _grid.SetupVoid("update", _ => true).SetVoidResult();
        _grid.SetupVoid("state", _ => true).SetVoidResult();
        _grid.SetupVoid("slotRendered", _ => true).SetVoidResult();
    }

    private static readonly GridColumn<TestRow>[] Columns = [new("Book", ColumnType.Text, r => r.Book)];

    /// <summary>A Chrome of the Consumer's own that fills the column menu and the loading indicator over the Script Core.</summary>
    private sealed class RazorChrome : IGridChrome
    {
        public ScriptSlots ScriptSlots => ScriptSlots.ColumnMenu | ScriptSlots.LoadingIndicator;

        public RenderFragment? ColumnMenu(GridMenuView view) => builder =>
        {
            for (var i = 0; i < view.Items.Count; i++)
            {
                builder.OpenElement(0, "button");
                builder.AddAttribute(1, ScriptSlotAttributes.MenuItem, i);
                builder.AddContent(2, $"{view.Items[i].Label} ({view.Column}, {view.Type}, {(view.Items[i].Enabled ? "enabled" : "disabled")})");
                builder.CloseElement();
            }
        };

        public RenderFragment? LoadingIndicator(GridLoadingView view) => builder => builder.AddContent(0, view.IsLoading ? "Loading" : "");
    }

    private global::ExGrid.Components.ScriptBridge<TestRow> Bridge() => Assert.IsType<DotNetObjectReference<global::ExGrid.Components.ScriptBridge<TestRow>>>(
        Assert.Single(_module.Invocations["attach"]).Arguments[1]).Value;

    private IRenderedComponent<global::ExGrid.Components.ExGrid<TestRow>> Grid(IGridChrome? chrome) => Render<global::ExGrid.Components.ExGrid<TestRow>>(ps => ps
        .Add(g => g.Rows, TestRows.Window())
        .Add(g => g.Columns, Columns)
        .Add(g => g.Chrome, chrome));

    [Fact] // ADR-0410 section 1: the script is told which slots the Chrome fills, and no slot where it fills none
    public void ADR0410_the_slots_the_Chrome_fills_cross_with_the_options()
    {
        Grid(new RazorChrome());
        using var document = JsonDocument.Parse(Assert.IsType<string>(Assert.Single(_module.Invocations["attach"]).Arguments[2]));
        Assert.Equal("""["columnMenu","loadingIndicator"]""", document.RootElement.GetProperty("options").GetProperty("razorSlots").GetRawText());
    }

    [Fact] // ADR-0410 sections 1 and 3: a mounted slot is drawn in a hidden holder beside the root from the data sent, handed to the script, and goes when unmounted
    public async Task ADR0410_a_mounted_slot_is_drawn_beside_the_root_and_goes_when_unmounted()
    {
        var cut = Grid(new RazorChrome());
        await cut.InvokeAsync(() => Bridge().MountSlot(1, 1, "columnMenu",
            """{"column":"Book","columnType":"text","commands":[{"id":"sort-ascending","label":"Sort A to Z","enabled":true}]}"""));

        var holder = cut.Find("div[hidden] > .ex-razor-slot");
        Assert.Equal("Sort A to Z (Book, Text, enabled)", holder.QuerySelector("button[data-ex-menu-item='0']")?.TextContent);
        // Beside the root, never inside it: the component's markup is the root, then the holder.
        Assert.Equal(2, cut.Nodes.OfType<AngleSharp.Dom.IElement>().Count());
        Assert.Same(holder.ParentElement, cut.Nodes.OfType<AngleSharp.Dom.IElement>().Last());
        var handed = Assert.Single(_grid.Invocations["slotRendered"]);
        Assert.Equal([1L, 1], handed.Arguments.Take(2));

        await cut.InvokeAsync(() => Bridge().UpdateSlot(1, 2,
            """{"column":"Book","columnType":"text","commands":[{"id":"sort-ascending","label":"Sort A to Z","enabled":false}]}"""));
        Assert.Equal("Sort A to Z (Book, Text, disabled)", cut.Find(".ex-razor-slot button").TextContent);
        // Told back with the number of the data it was drawn for, so the script holds the grid unsettled until the last stands.
        Assert.Equal([1L, 2], _grid.Invocations["slotRendered"][^1].Arguments.Take(2));

        await cut.InvokeAsync(() => Bridge().UnmountSlot(1));
        Assert.Empty(cut.FindAll(".ex-razor-slot"));
    }

    /// <summary>A held call's arguments as the browser hands them over: gzip of their JSON.</summary>
    private sealed class Gzipped(byte[] text) : IJSStreamReference
    {
        private readonly byte[] _bytes = Packed(text);
        public long Length => _bytes.LongLength;
        public ValueTask<Stream> OpenReadStreamAsync(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            Length > maxAllowedSize ? throw new ArgumentOutOfRangeException(nameof(maxAllowedSize)) : ValueTask.FromResult<Stream>(new MemoryStream(_bytes, writable: false));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new NotSupportedException();

        private static byte[] Packed(byte[] text)
        {
            using var packed = new MemoryStream();
            using (var gzip = new System.IO.Compression.GZipStream(packed, System.IO.Compression.CompressionLevel.Fastest))
                gzip.Write(text);
            return packed.ToArray();
        }
    }

    [Fact] // ADR-0005 (as amended 2026-10-20): a call too large for a circuit's message is taken as a stream in its turn, and the calls after it wait for it
    public async Task ADR0005_a_held_call_is_made_in_its_turn_ahead_of_the_calls_after_it()
    {
        var take = _grid.Setup<IJSStreamReference>("take", _ => true);
        var cut = Grid(new RazorChrome());
        var arguments = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new object[]
        {
            1, 1, "columnMenu", """{"column":"Book","columnType":"text","commands":[{"id":"sort-ascending","label":"Sort A to Z","enabled":true}]}""",
        }));
        using var crossed = JsonDocument.Parse($$"""{"held":7,"bytes":{{arguments.Length}}}""");
        Task mounted = Task.CompletedTask, updated = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            mounted = Bridge().Held("MountSlot", crossed.RootElement);
            updated = Bridge().UpdateSlot(1, 2,
                """{"column":"Book","columnType":"text","commands":[{"id":"sort-ascending","label":"Sort A to Z","enabled":false}]}""");
        });
        // The slot is not mounted until its call is taken, and the update that names it waits rather than finding nothing.
        Assert.Equal([7], Assert.Single(take.Invocations).Arguments);
        Assert.Empty(cut.FindAll(".ex-razor-slot"));
        Assert.False(updated.IsCompleted);

        take.SetResult(new Gzipped(arguments));
        await mounted.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        await updated.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        cut.WaitForAssertion(() => Assert.Equal("Sort A to Z (Book, Text, disabled)", cut.Find(".ex-razor-slot button").TextContent));
    }

    /// <summary>A Chrome that fills the filter panel, writing what its view says.</summary>
    private sealed class FilterChrome : IGridChrome
    {
        public ScriptSlots ScriptSlots => ScriptSlots.FilterPanel;

        public RenderFragment? FilterPanel(GridFilterView view) => builder => builder.AddContent(0, view.Conditions is { } form
            ? $"{view.Column} {view.Type}: {form.First.Operator} '{form.First.Operand}' ({form.First.Refusal}) {form.Combinator} {form.Second?.Operator ?? "none"}; "
                + $"{string.Join(",", form.Allowed.Select(op => $"{op.Id}={op.Label}"))}; OK {(view.CanApply ? "on" : "off")}; {view.Words.None}"
            : $"{view.Column}: {view.ValueList!.SelectAll} {string.Join(",", view.ValueList.Entries.Select(e => $"{e.Text}{(e.Blank ? "*" : "")}{(e.Checked ? "+" : "-")}"))}");
    }

    [Fact] // ADR-0410 section 3: a filter panel's view crosses as data, its value list or its condition form, and is drawn from it
    public async Task ADR0410_a_filter_panels_view_crosses_as_data()
    {
        var cut = Grid(new FilterChrome());
        const string words = "\"words\":{\"ok\":\"OK\",\"cancel\":\"Cancel\",\"search\":\"Search\",\"addToCurrent\":\"Add\",\"selectAll\":\"All\","
            + "\"selectAllSearchResults\":\"All found\",\"and\":\"And\",\"or\":\"Or\",\"none\":\"(none)\"}";
        await cut.InvokeAsync(() => Bridge().MountSlot(1, 1, "filterPanel",
            "{\"column\":\"Book\",\"columnType\":\"text\",\"canApply\":true,\"fetching\":false," + words + ",\"valueList\":{\"search\":\"\","
            + "\"addOffered\":false,\"addToCurrent\":false,\"selectAll\":\"mixed\",\"entries\":[{\"text\":\"FX\",\"blank\":false,\"checked\":true},"
            + "{\"text\":\"(Blanks)\",\"blank\":true,\"checked\":false}]}}"));
        Assert.Equal("Book: Mixed FX+,(Blanks)*-", cut.Find(".ex-razor-slot").TextContent);

        await cut.InvokeAsync(() => Bridge().UpdateSlot(1, 2,
            "{\"column\":\"Book\",\"columnType\":\"text\",\"canApply\":false,\"fetching\":false," + words + ",\"conditions\":{"
            + "\"allowed\":[{\"id\":\"equals\",\"label\":\"Equals\"},{\"id\":\"contains\",\"label\":\"Contains\"}],"
            + "\"first\":{\"operator\":\"contains\",\"operand\":\"x\",\"takesOperand\":true,\"refusal\":\"no\"},\"combinator\":\"or\",\"second\":null}}"));
        Assert.Equal("Book Text: contains 'x' (no) Or none; equals=Equals,contains=Contains; OK off; (none)", cut.Find(".ex-razor-slot").TextContent);
        Assert.Equal("2", cut.Find(".ex-razor-slot").GetAttribute("data-ex-version"));
    }

    private sealed class FindChrome : IGridChrome
    {
        public ScriptSlots ScriptSlots => ScriptSlots.FindPanel;

        public RenderFragment? FindPanel(GridFindView view) => builder => builder.AddContent(0,
            $"'{view.Text}' case {view.MatchCase} whole {view.WholeCell}; replace {(view.ReplaceShown ? $"'{view.ReplaceText}'" : "off")}"
            + $"{(view.ReplaceWaiting ? " waiting" : "")}; {view.Words.Next}/{view.Words.ReplaceAll}; said '{view.Outcome}'");
    }

    [Fact] // ADR-0410 section 3 / ADR-0332: the find panel's view crosses as data, its Replace and its outcome included, and is drawn from it
    public async Task ADR0410_a_find_panels_view_crosses_as_data()
    {
        var cut = Grid(new FindChrome());
        const string words = "\"words\":{\"field\":\"Find what\",\"matchCase\":\"Match case\",\"wholeCell\":\"Whole\",\"next\":\"Next\","
            + "\"previous\":\"Previous\",\"replaceField\":\"Replace with\",\"replace\":\"Replace\",\"replaceAll\":\"All\"}";
        await cut.InvokeAsync(() => Bridge().MountSlot(1, 1, "findPanel",
            "{\"text\":\"Osei\",\"matchCase\":true,\"wholeCell\":false,\"replaceShown\":false,\"replaceText\":\"\",\"replaceWaiting\":false,"
            + "\"outcome\":\"\"," + words + "}"));
        Assert.Equal("'Osei' case True whole False; replace off; Next/All; said ''", cut.Find(".ex-razor-slot").TextContent);

        await cut.InvokeAsync(() => Bridge().UpdateSlot(1, 2,
            "{\"text\":\"Osei\",\"matchCase\":true,\"wholeCell\":true,\"replaceShown\":true,\"replaceText\":\"Lee\",\"replaceWaiting\":true,"
            + "\"outcome\":\"No match\"," + words + "}"));
        Assert.Equal("'Osei' case True whole True; replace 'Lee' waiting; Next/All; said 'No match'", cut.Find(".ex-razor-slot").TextContent);
    }

    private sealed class EditorChrome : IGridChrome
    {
        public ScriptSlots ScriptSlots => ScriptSlots.CellEditor;

        public RenderFragment? CellEditor(GridCellEditorView view) => builder => builder.AddContent(0,
            $"{view.Column} {view.Type} '{view.InitialText}' {view.Mode} error '{view.Error}' pending {view.IsPending}");
    }

    [Fact] // ADR-0410 section 3: the Cell Editor's view crosses as data, drawn again as its mode, its error and its pending commit change
    public async Task ADR0410_a_cell_editors_view_crosses_as_data()
    {
        var cut = Grid(new EditorChrome());
        await cut.InvokeAsync(() => Bridge().MountSlot(1, 1, "cellEditor",
            "{\"column\":\"Book\",\"columnType\":\"text\",\"initialText\":\"a\",\"mode\":\"overwrite\",\"error\":null,\"pending\":false}"));
        Assert.Equal("Book Text 'a' Overwrite error '' pending False", cut.Find(".ex-razor-slot").TextContent);

        await cut.InvokeAsync(() => Bridge().UpdateSlot(1, 2,
            "{\"column\":\"Book\",\"columnType\":\"text\",\"initialText\":\"a\",\"mode\":\"point\",\"error\":\"Not a book\",\"pending\":true}"));
        Assert.Equal("Book Text 'a' Point error 'Not a book' pending True", cut.Find(".ex-razor-slot").TextContent);
    }

    private sealed class BarChrome : IGridChrome
    {
        public ScriptSlots ScriptSlots => ScriptSlots.FormulaBarText | ScriptSlots.NameBox;

        public RenderFragment? FormulaBarText(GridFormulaBarTextView view) => builder => builder.AddContent(0, "bar text");

        public RenderFragment? NameBox(GridNameBoxView view) => builder => builder.AddContent(0, "name box");
    }

    [Fact] // ADR-0410 section 3 / ADR-0051: the Formula Bar's fields are asked for by name and drawn from no data
    public async Task ADR0410_the_formula_bars_fields_are_drawn_from_no_data()
    {
        var cut = Grid(new BarChrome());
        using (var document = JsonDocument.Parse(Assert.IsType<string>(Assert.Single(_module.Invocations["attach"]).Arguments[2])))
            Assert.Equal("""["formulaBarText","nameBox"]""", document.RootElement.GetProperty("options").GetProperty("razorSlots").GetRawText());
        await cut.InvokeAsync(() => Bridge().MountSlot(1, 1, "formulaBarText", "{}"));
        await cut.InvokeAsync(() => Bridge().MountSlot(2, 1, "nameBox", "{}"));
        Assert.Equal(["bar text", "name box"], cut.FindAll(".ex-razor-slot").Select(slot => slot.TextContent));
    }

    private sealed class CompletionChrome : IGridChrome
    {
        public ScriptSlots ScriptSlots => ScriptSlots.EditorCompletion;

        public RenderFragment? EditorCompletion(GridEditorCompletionView view) => builder => builder.AddContent(0,
            $"{string.Join(",", view.Labels)} / {view.Hint?.Parts().Emphasis}");
    }

    [Fact] // ADR-0410 section 3 / ADR-0051: the completion's labels and hint cross as data, drawn for each answer
    public async Task ADR0410_the_completions_labels_and_hint_cross_as_data()
    {
        var cut = Grid(new CompletionChrome());
        using (var document = JsonDocument.Parse(Assert.IsType<string>(Assert.Single(_module.Invocations["attach"]).Arguments[2])))
            Assert.Equal("""["editorCompletion"]""", document.RootElement.GetProperty("options").GetProperty("razorSlots").GetRawText());
        await cut.InvokeAsync(() => Bridge().MountSlot(1, 1, "editorCompletion",
            """{"labels":["SUM","SUMIF"],"hint":{"text":"SUM(number1)","emphasisStart":4,"emphasisLength":7}}"""));
        Assert.Equal("SUM,SUMIF / number1", cut.Find(".ex-razor-slot").TextContent);

        await cut.InvokeAsync(() => Bridge().UpdateSlot(1, 2, """{"labels":[],"hint":null}"""));
        Assert.Equal(" / ", cut.Find(".ex-razor-slot").TextContent);
    }

    [Fact] // ADR-0051: the editor's text and caret are handed to CompleteEditorText, and its answer crosses as the core reads it
    public async Task ADR0051_completion_is_asked_of_CompleteEditorText()
    {
        var asked = new List<(string, int)>();
        var cut = Render<global::ExGrid.Components.ExGrid<TestRow>>(ps => ps
            .Add(g => g.Rows, TestRows.Window())
            .Add(g => g.Columns, Columns)
            .Add(g => g.CompleteEditorText, (text, caret) =>
            {
                asked.Add((text, caret));
                return ValueTask.FromResult<Cells.EditorCompletion?>(text == "=SU"
                    ? new Cells.EditorCompletion([new Cells.CompletionCandidate("SUM", 1, 2, "SUM(")], new Cells.EditorHint("SUM(number1)", 4, 7))
                    : null);
            }));
        using (var document = JsonDocument.Parse(Assert.IsType<string>(Assert.Single(_module.Invocations["attach"]).Arguments[2])))
            Assert.True(document.RootElement.GetProperty("options").GetProperty("completesText").GetBoolean());

        var answer = await cut.InvokeAsync(() => Bridge().Complete("=SU", 3));
        var none = await cut.InvokeAsync(() => Bridge().Complete("=", 1));

        Assert.Equal("""{"candidates":[{"label":"SUM","start":1,"length":2,"insert":"SUM("}],"hint":{"text":"SUM(number1)","emphasisStart":4,"emphasisLength":7}}""", answer);
        Assert.Null(none);
        Assert.Equal([("=SU", 3), ("=", 1)], asked);
    }

    [Fact] // ADR-0051 / ADR-0050 item 4: Enter in the Name Box is heard only where handled, and hands the Consumer the typed text
    public async Task ADR0051_enter_in_the_name_box_reaches_OnNameBoxEntered()
    {
        var typed = new List<string>();
        var cut = Render<global::ExGrid.Components.ExGrid<TestRow>>(ps => ps
            .Add(g => g.Rows, TestRows.Window())
            .Add(g => g.Columns, Columns)
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.OnNameBoxEntered, (string text) => typed.Add(text)));
        using (var document = JsonDocument.Parse(Assert.IsType<string>(Assert.Single(_module.Invocations["attach"]).Arguments[2])))
            Assert.Contains("nameBoxEntered", document.RootElement.GetProperty("options").GetProperty("hears").EnumerateArray().Select(kind => kind.GetString()));

        using var payload = JsonDocument.Parse("\"B7\"");
        await cut.InvokeAsync(() => Bridge().Heard("nameBoxEntered", payload.RootElement));

        Assert.Equal(["B7"], typed);
    }

    private static GridColumn<TestRow>[] Templated(string mark) =>
    [
        GridColumn<TestRow>.TemplateColumn("Book", ColumnType.Text, r => r.Book,
            cell => builder => builder.AddContent(0, $"{mark} {cell.Row.Book} #{cell.FocusRequest}")),
    ];

    private IRenderedComponent<global::ExGrid.Components.ExGrid<TestRow>> TemplatedGrid(GridColumn<TestRow>[] columns) => Render<global::ExGrid.Components.ExGrid<TestRow>>(ps => ps
        .Add(g => g.Rows, TestRows.Window())
        .Add(g => g.Columns, columns));

    private long RowIdAt(int index)
    {
        using var document = JsonDocument.Parse(Assert.IsType<string>(Assert.Single(_module.Invocations["attach"]).Arguments[2]));
        return document.RootElement.GetProperty("state").GetProperty("window")[index].GetProperty("i").GetInt64();
    }

    [Fact] // ADR-0410 section 6 / ADR-0020: a Template column crosses as one, and each cell the script mounts is the column's fragment over the row .NET holds
    public async Task ADR0410_a_Template_cell_is_drawn_from_the_row_dotnet_holds()
    {
        var cut = TemplatedGrid(Templated("bar"));
        using (var document = JsonDocument.Parse(Assert.IsType<string>(Assert.Single(_module.Invocations["attach"]).Arguments[2])))
            Assert.True(document.RootElement.GetProperty("options").GetProperty("columns")[0].GetProperty("template").GetBoolean());
        var beta = RowIdAt(1);

        await cut.InvokeAsync(() => Bridge().MountSlot(1, 1, "templateCell", $$"""{"row":{{beta}},"drawing":1,"column":"Book","focusRequest":0}"""));
        Assert.Equal("bar Beta #0", cut.Find(".ex-razor-slot").TextContent);

        // Space's request reaches the fragment on the one drawing it was made for (ADR-0037).
        await cut.InvokeAsync(() => Bridge().UpdateSlot(1, 2, $$"""{"row":{{beta}},"drawing":1,"column":"Book","focusRequest":7}"""));
        Assert.Equal("bar Beta #7", cut.Find(".ex-razor-slot").TextContent);

        // Drawn again as another cell mounts, it is handed zero: the request was for one drawing.
        await cut.InvokeAsync(() => Bridge().MountSlot(2, 1, "templateCell", $$"""{"row":{{RowIdAt(0)}},"drawing":2,"column":"Book","focusRequest":0}"""));
        Assert.Equal(["bar Beta #0", "bar Alpha #0"], cut.FindAll(".ex-razor-slot").Select(slot => slot.TextContent));
    }

    [Fact] // ADR-0020: a Template cell standing is drawn again by the fragment the column declares now, as the core mounts it again only for another row
    public async Task ADR0020_a_Template_cell_is_drawn_by_the_fragment_declared_now()
    {
        var cut = TemplatedGrid(Templated("bar"));
        await cut.InvokeAsync(() => Bridge().MountSlot(1, 1, "templateCell", $$"""{"row":{{RowIdAt(0)}},"drawing":1,"column":"Book","focusRequest":0}"""));

        cut.Render(ps => ps.Add(g => g.Columns, Templated("badge")));

        Assert.Equal("badge Alpha #0", cut.Find(".ex-razor-slot").TextContent);
    }

    [Fact] // ADR-0410: a Chrome that names a slot this adapter does not fill is refused by name, never left out quietly
    public void ADR0410_a_slot_the_adapter_does_not_fill_is_refused()
    {
        var refused = Assert.ThrowsAny<InvalidOperationException>(() => Grid(new UnknownSlotChrome()));
        Assert.Contains("does not fill", refused.Message, StringComparison.Ordinal);
    }

    private sealed class UnknownSlotChrome : IGridChrome
    {
        public ScriptSlots ScriptSlots => (ScriptSlots)1024;

    }
}
