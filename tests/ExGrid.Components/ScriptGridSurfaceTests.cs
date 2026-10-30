using System.Globalization;
using System.Text.Json;
using Bunit;
using ExGrid.Cells;
using ExGrid.Chrome;
using ExGrid.Clipboard;
using ExGrid.Components.Tests.Support;
using ExGrid.Data;
using ExGrid.Keys;
using ExGrid.Rows;
using ExGrid.Summarizing;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// ADR-0440: what Blazor's ExGrid over the Script Core asks of the Consumer as it paints crosses with each row, a
/// question the Consumer answers later is asked in its turn by the browser's number, and a gesture the Consumer handles
/// reaches it as a write. What the browser does with each is the core's, tested there and in the bridge's own tests.
/// </summary>
public class ScriptGridSurfaceTests : BunitContext
{
    private readonly BunitJSModuleInterop _module;
    private readonly BunitJSModuleInterop _grid;
    private readonly TestRow[] _rows = TestRows.Window();

    public ScriptGridSurfaceTests()
    {
        _module = JSInterop.SetupModule(invocation => invocation.Identifier == "import");
        _module.Setup<string?>("stamp").SetResult(global::ExGrid.Script.ScriptCoreAsset.Stamp);
        _grid = _module.SetupModule("attach", _ => true);
        _grid.SetupVoid("update", _ => true).SetVoidResult();
        _grid.SetupVoid("state", _ => true).SetVoidResult();
    }

    private IRenderedComponent<global::ExGrid.Script.ExGrid<TestRow>> Grid(
        Action<ComponentParameterCollectionBuilder<global::ExGrid.Script.ExGrid<TestRow>>>? extra = null, GridColumn<TestRow>[]? columns = null)
        => Render<global::ExGrid.Script.ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Rows, _rows).Add(g => g.Columns, columns ?? TestRows.Columns());
            extra?.Invoke(ps);
        });

    private global::ExGrid.Script.ScriptBridge<TestRow> Bridge() => Assert.IsType<DotNetObjectReference<global::ExGrid.Script.ScriptBridge<TestRow>>>(
        Assert.Single(_module.Invocations["attach"]).Arguments[1]).Value;

    private static JsonElement Parsed(object? json)
    {
        using var document = JsonDocument.Parse(Assert.IsType<string>(json));
        return document.RootElement.Clone();
    }

    private JsonElement Attached() => Parsed(Assert.Single(_module.Invocations["attach"]).Arguments[2]);

    private JsonElement Window() => Attached().GetProperty("state").GetProperty("window");

    private static JsonElement Payload(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    [Fact] // ADR-0024 / ADR-0050 item 15 / ADR-0068 / ADR-0440: each row crosses with its kind, its cells' paint and when they changed
    public void ADR0440_a_rows_kind_appearance_and_change_times_cross_with_it_on_dotnets_clock()
    {
        var changed = DateTimeOffset.FromUnixTimeMilliseconds(1_000_000);
        var columns = TestRows.Columns();
        Grid(ps => ps
            .Add(g => g.RowKind, (TestRow row) => row.Book == "Gamma" ? RowKind.Total : RowKind.Detail)
            .Add(g => g.CellAppearance, (TestRow row, GridColumn<TestRow> column) => row.Book == "Beta" && column.Name == "Amount"
                ? new CellAppearance { Bold = true } : CellAppearance.None)
            .Add(g => g.CellChangedAt, (TestRow row, GridColumn<TestRow> column) => column.Name == "Book" && row.Book == "Alpha" ? changed : null), columns);

        var attached = Attached();
        Assert.True(attached.GetProperty("options").GetProperty("rowKinds").GetBoolean());
        Assert.True(attached.GetProperty("state").GetProperty("now").GetInt64() > 0);
        var window = Window();
        Assert.False(window[0].TryGetProperty("r", out _));
        Assert.Equal("total", window[2].GetProperty("r").GetString());
        // A row the Consumer paints nothing on crosses with no appearances at all.
        Assert.False(window[0].TryGetProperty("a", out _));
        var amount = columns.ToList().FindIndex(column => column.Name == "Amount");
        Assert.True(window[1].GetProperty("a")[amount].GetProperty("bold").GetBoolean());
        Assert.Equal(JsonValueKind.Null, window[1].GetProperty("a")[0].ValueKind);
        Assert.Equal(1_000_000, window[0].GetProperty("h")[0].GetInt64());
        Assert.Equal(JsonValueKind.Null, window[0].GetProperty("h")[1].ValueKind);
        Assert.False(window[1].TryGetProperty("h", out _));
    }

    [Fact] // ADR-0043 / ADR-0440: a source's marks cross only beside a Mark Column, and a press hands the source the row the browser named
    public async Task ADR0440_a_mark_crosses_with_its_row_and_a_gesture_marks_the_row_named()
    {
        var source = GridSource.From(_rows);
        var cut = Render<global::ExGrid.Script.ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source).Add(g => g.Columns, [GridColumn<TestRow>.MarkColumn(), .. TestRows.Columns()]));
        Assert.True(Attached().GetProperty("options").GetProperty("marks").GetBoolean());
        Assert.False(Window()[1].GetProperty("m").GetBoolean());
        Assert.Equal(0, Attached().GetProperty("state").GetProperty("markCounts").GetProperty("markedInResult").GetInt32());
        var beta = Window()[1].GetProperty("i").GetInt64();

        await cut.InvokeAsync(() => Bridge().Heard("mark", Payload($$"""{"kind":"oneRow","row":{{beta}},"marked":true}""")));

        Assert.True(source.Marks.IsMarked(_rows[1]));
        var state = Parsed(_grid.Invocations["state"][^1].Arguments[0]);
        Assert.True(state.GetProperty("window")[1].GetProperty("m").GetBoolean());
        Assert.Equal(1, state.GetProperty("markCounts").GetProperty("markedInResult").GetInt32());
    }

    [Fact] // ADR-0036 / ADR-0440 section 4: the grid's own words cross as a table, asked once per function, placeholders never asked
    public void ADR0440_command_label_crosses_as_a_table_of_the_ids_it_answers()
    {
        var asked = new List<string>();
        Func<string, string?> words = id =>
        {
            asked.Add(id);
            return id switch
            {
                RefusalLabelIds.CopyTooLarge => "Too many cells.",
                GridCommandIds.Copy => "Duplicate",
                _ => null,
            };
        };
        var cut = Grid(ps => ps.Add(g => g.CommandLabel, words));

        var table = Attached().GetProperty("options").GetProperty("commandLabels");
        Assert.Equal("Too many cells.", table.GetProperty(RefusalLabelIds.CopyTooLarge).GetString());
        Assert.Equal("Duplicate", table.GetProperty(GridCommandIds.Copy).GetString());
        Assert.Equal(2, table.EnumerateObject().Count());
        Assert.DoesNotContain(asked, id => id.StartsWith('{'));
        var once = asked.Count;

        cut.Render(ps => ps.Add(g => g.ViewportHeight, 300));
        Assert.Equal(once, asked.Count);

        cut.Render(ps => ps.Add(g => g.CommandLabel, id => id == GridCommandIds.Copy ? "Copy cells" : null));
        var updated = Parsed(_grid.Invocations["update"][^1].Arguments[0]).GetProperty("options").GetProperty("commandLabels");
        Assert.Equal("Copy cells", Assert.Single(updated.EnumerateObject()).Value.GetString());
    }

    [Fact] // ADR-0043 / ADR-0440: a source that keeps marks sends none for a grid with no Mark Column
    public void ADR0440_rows_cross_without_marks_where_no_mark_column_shows_them()
    {
        Grid();
        Assert.False(Window()[0].TryGetProperty("m", out _));
        Assert.False(Attached().GetProperty("options").TryGetProperty("marks", out _));
    }

    [Fact] // ADR-0043 / ADR-0130 / ADR-0050 item 9: what one of two holders would have to win quietly is refused by name
    public void ADR0440_two_holders_of_one_answer_are_refused_by_name()
    {
        var marks = GridSource.From(TestRows.Window()).Marks;
        IRenderedComponent<global::ExGrid.Script.ExGrid<TestRow>> Over(Action<ComponentParameterCollectionBuilder<global::ExGrid.Script.ExGrid<TestRow>>> extra)
            => Render<global::ExGrid.Script.ExGrid<TestRow>>(ps =>
            {
                ps.Add(g => g.Source, GridSource.From(_rows)).Add(g => g.Columns, [GridColumn<TestRow>.MarkColumn(), .. TestRows.Columns()]);
                extra(ps);
            });
        Assert.Contains("keeps Row Marks of its own", Assert.Throws<InvalidOperationException>(() => Over(ps => ps.Add(g => g.Marks, marks))).Message);
        Assert.Contains("CanSummarize", Assert.Throws<InvalidOperationException>(() => Over(ps => ps
            .Add(g => g.OnSummarize, (GridSummaryRequest _, CancellationToken _) => Task.FromResult(GridSummaryResult.Declined("No."))))).Message);
        Assert.Contains("not both", Assert.Throws<InvalidOperationException>(() => Grid(ps => ps
            .Add(g => g.CopyAnswer, (GridCopyRequest _) => GridCopyAnswer.Refuse("No."))
            .Add(g => g.CopyAnswerAsync, (GridCopyRequest _, CancellationToken _) => Task.FromResult(GridCopyAnswer.Refuse("No."))))).Message);
        Assert.Contains("needs OnDeclaredKey", Assert.Throws<InvalidOperationException>(() => Grid(ps => ps.Add(g => g.DeclaredKeys, ["F9"]))).Message);
    }

    [Fact] // ADR-0043 / ADR-0130 / ADR-0440: over Rows, the source the grid makes keeps nothing the Consumer gives it for
    public void ADR0440_over_rows_marks_are_the_consumers_and_on_summarize_answers_in_the_sources_place()
    {
        Assert.Contains("over Rows", Assert.Throws<InvalidOperationException>(() => Grid(columns: [GridColumn<TestRow>.MarkColumn(), .. TestRows.Columns()])).Message);
        var marks = GridSource.From(_rows).Marks;
        Grid(ps => ps
            .Add(g => g.Marks, marks)
            .Add(g => g.OnSummarize, (GridSummaryRequest _, CancellationToken _) => Task.FromResult(GridSummaryResult.Declined("Ask the desk."))),
            [GridColumn<TestRow>.MarkColumn(), .. TestRows.Columns()]);

        var attached = Attached();
        Assert.True(attached.GetProperty("options").GetProperty("marks").GetBoolean());
        Assert.True(attached.GetProperty("options").GetProperty("summarizes").GetBoolean());
        Assert.False(attached.GetProperty("source").GetProperty("summarizes").GetBoolean());
    }

    [Fact] // ADR-0440 section 5 / LV-20: the row an open edit follows crosses past the rows sent, at its place in the Window
    public async Task ADR0440_the_row_an_edit_holds_crosses_beyond_the_rows_sent_wherever_the_window_holds_it()
    {
        var source = GridSource.From(TestRows.Many(1000), r => r.Book);
        var cut = Render<global::ExGrid.Script.ExGrid<TestRow>>(ps => ps.Add(g => g.Source, source).Add(g => g.Columns, TestRows.Columns()));
        var generation = Attached().GetProperty("source").GetProperty("generation").GetInt32();
        var first = Window()[0].GetProperty("i").GetInt64();

        await cut.InvokeAsync(() => Bridge().HoldRow(generation, first));
        await cut.InvokeAsync(() => source.OnSortChanged([new SortSpec("Amount", SortDirection.Descending)]));
        await cut.InvokeAsync(() => Bridge().NeedRange(generation, 0, 20));

        var sent = Parsed(_grid.Invocations["state"][^1].Arguments[0]);
        var beyond = Assert.Single(sent.GetProperty("beyond").EnumerateArray());
        Assert.Equal(999, beyond.GetProperty("at").GetInt32());
        Assert.Equal("Row 000000", beyond.GetProperty("row").GetProperty("k").GetString());
        Assert.True(sent.GetProperty("window").GetArrayLength() < 1000);

        await cut.InvokeAsync(() => Bridge().HoldRow(generation, null));
        await cut.InvokeAsync(() => source.OnSortChanged([new SortSpec("Amount", SortDirection.Ascending)]));
        await cut.InvokeAsync(() => source.OnSortChanged([new SortSpec("Amount", SortDirection.Descending)]));
        await cut.InvokeAsync(() => Bridge().NeedRange(generation, 0, 20));

        Assert.False(Parsed(_grid.Invocations["state"][^1].Arguments[0]).TryGetProperty("beyond", out _));
    }

    [Fact] // ADR-0130 / ADR-0440: a figure takes the Focus column's .NET format before it crosses, which the browser cannot run
    public async Task ADR0440_a_figure_crosses_in_the_focus_columns_format()
    {
        GridColumn<TestRow>[] columns =
        [
            new("Book", ColumnType.Text, r => r.Book),
            new("Amount", ColumnType.Number, r => r.Amount, format: v => ((decimal)v).ToString("0.00", CultureInfo.InvariantCulture)),
            new("AsOf", ColumnType.Date, r => r.AsOf),
            new("Active", ColumnType.Boolean, r => r.Active),
        ];
        var cut = Grid(columns: columns);
        var request = Payload("""{"ranges":[{"topRow":0,"leftColumn":1,"rowCount":2,"columnCount":1}],"columns":["Book","Amount","AsOf","Active"],"rowSequenceVersion":0,"figures":["count","sum"],"focus":{"row":0,"column":1}}""");
        var generation = Attached().GetProperty("source").GetProperty("generation").GetInt32();

        var answered = Parsed(await cut.InvokeAsync(() => Bridge().Summarize(1, generation, request)));

        var texts = answered.GetProperty("texts");
        Assert.Equal("93.50", texts.GetProperty("sum").GetString());
        Assert.False(texts.TryGetProperty("count", out _));
    }

    [Fact] // ADR-0130 / ADR-0440: a figure past a double's range crosses by its name, never as another number
    public async Task ADR0440_an_infinite_figure_crosses_by_its_name()
    {
        var cut = Render<global::ExGrid.Script.ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, GridSource.Fetch<TestRow>((_, _) => ValueTask.FromResult(new GridPage<TestRow>([], 0, 0))))
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.OnSummarize, (GridSummaryRequest _, CancellationToken _) => Task.FromResult(GridSummaryResult.Answered(
                new Dictionary<SummaryFigures, AggregateResult> { [SummaryFigures.Sum] = AggregateResult.Of(double.PositiveInfinity) }))));
        var request = Payload("""{"ranges":[{"topRow":0,"leftColumn":1,"rowCount":2,"columnCount":1}],"columns":["Book","Amount","AsOf","Active"],"rowSequenceVersion":0,"figures":["sum"]}""");

        var answered = Parsed(await cut.InvokeAsync(() => Bridge().Summarize(1, null, request)));

        Assert.Equal("Infinity", answered.GetProperty("figures").GetProperty("sum").GetProperty("number").GetString());
    }

    [Fact] // ADR-0130 / SM-11: a question the user replaced, which the Consumer keeps answering, holds the next one nowhere
    public async Task ADR0130_a_summary_the_consumer_holds_does_not_hold_the_next_one()
    {
        var held = new TaskCompletionSource<GridSummaryResult>();
        var asked = 0;
        var cut = Render<global::ExGrid.Script.ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, GridSource.Fetch<TestRow>((_, _) => ValueTask.FromResult(new GridPage<TestRow>([], 0, 0))))
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.OnSummarize, (GridSummaryRequest _, CancellationToken _) => ++asked == 1 ? held.Task : Task.FromResult(GridSummaryResult.Answered(
                new Dictionary<SummaryFigures, AggregateResult> { [SummaryFigures.Sum] = AggregateResult.Of(3d) }))));
        var request = Payload("""{"ranges":[{"topRow":0,"leftColumn":1,"rowCount":2,"columnCount":1}],"columns":["Book","Amount","AsOf","Active"],"rowSequenceVersion":0,"figures":["sum"]}""");

        Task<string?> first = null!;
        await cut.InvokeAsync(() => { first = Bridge().Summarize(1, null, request); });
        await cut.InvokeAsync(() => Bridge().CancelAnswer(1));
        var second = Parsed(await cut.InvokeAsync(() => Bridge().Summarize(2, null, request)));

        Assert.Equal(2, asked);
        Assert.True(second.GetProperty("figures").TryGetProperty("sum", out _));
        Assert.False(first.IsCompleted);
        held.SetResult(GridSummaryResult.Answered(new Dictionary<SummaryFigures, AggregateResult> { [SummaryFigures.Sum] = AggregateResult.Of(2d) }));
        // Answered once the Consumer answers it: the browser, which cancelled it, reads none of it.
        Assert.NotNull(await cut.InvokeAsync(() => first));
    }

    [Fact] // ADR-0050 item 9 / ADR-0440: a copy is answered by the browser's number, in the Consumer's flavours or its sentence
    public async Task ADR0440_a_copy_is_answered_by_the_consumer_by_the_browsers_number()
    {
        GridCopyRequest? asked = null;
        var cut = Grid(ps => ps.Add(g => g.CopyAnswer, (GridCopyRequest request) =>
        {
            asked = request;
            return request.WithHeaders ? GridCopyAnswer.Refuse("Headers stay inside.") : GridCopyAnswer.Write("Alpha", "<table><tr><td>Alpha</td></tr></table>");
        }));
        static JsonElement Request(bool withHeaders) => Payload(
            $$"""{"plan":{"segments":[{"topRow":0,"leftColumn":0,"rowCount":1,"columnCount":1}],"orientation":"vertical","totalRows":1,"totalColumns":1},"withHeaders":{{(withHeaders ? "true" : "false")}},"rowSequenceVersion":0}""");

        var written = await cut.InvokeAsync(() => Bridge().Copy(1, Request(false)));
        var refused = await cut.InvokeAsync(() => Bridge().Copy(2, Request(true)));

        Assert.True(Attached().GetProperty("options").GetProperty("copies").GetBoolean());
        Assert.Equal(1, asked?.Plan.TotalRows);
        var write = Parsed(written);
        Assert.Equal("write", write.GetProperty("kind").GetString());
        Assert.Equal("Alpha", write.GetProperty("text").GetString());
        Assert.Equal("<table><tr><td>Alpha</td></tr></table>", write.GetProperty("html").GetString());
        Assert.Equal("""{"kind":"refuse","sentence":"Headers stay inside."}""", refused);
    }

    [Fact] // ADR-0130 / ADR-0440: a summary asked of the source crosses its exact figure as digits, scale and all
    public async Task ADR0440_a_summary_is_asked_of_the_numbered_source_and_its_exact_figure_crosses_as_digits()
    {
        var cut = Grid(columns: TestRows.Columns());
        var request = Payload("""{"ranges":[{"topRow":0,"leftColumn":1,"rowCount":2,"columnCount":1}],"columns":["Book","Amount","AsOf","Active"],"rowSequenceVersion":0,"figures":["sum"],"focus":{"row":0,"column":1}}""");
        var generation = Attached().GetProperty("source").GetProperty("generation").GetInt32();

        var answered = Parsed(await cut.InvokeAsync(() => Bridge().Summarize(1, generation, request)));
        var gone = await cut.InvokeAsync(() => Bridge().Summarize(2, generation + 1, request));

        Assert.Equal("answered", answered.GetProperty("kind").GetString());
        var sum = answered.GetProperty("figures").GetProperty("sum");
        // 100.5 + -7 = 93.5, held as the decimal it was summed in.
        Assert.Equal("935", sum.GetProperty("exact").GetProperty("mantissa").GetString());
        Assert.Equal(1, sum.GetProperty("exact").GetProperty("scale").GetInt32());
        Assert.Null(gone);
    }

    [Fact] // ADR-0050 item 14 / ADR-0440: a declared key reaches the Consumer with the selection it was pressed on
    public async Task ADR0440_a_declared_key_reaches_the_consumer_with_its_selection()
    {
        GridDeclaredKeyPress? heard = null;
        var cut = Grid(ps => ps.Add(g => g.DeclaredKeys, ["F9"]).Add(g => g.OnDeclaredKey, (GridDeclaredKeyPress press) => { heard = press; }));
        Assert.Equal("F9", Attached().GetProperty("options").GetProperty("declaredKeys")[0].GetString());

        await cut.InvokeAsync(() => Bridge().Heard("declaredKey", Payload(
            """{"key":"F9","editOpen":false,"selection":{"ranges":[{"topRow":1,"leftColumn":0,"rowCount":1,"columnCount":1}],"focus":{"row":1,"column":0},"focusRangeIndex":0},"rowSequenceVersion":0}""")));

        Assert.Equal("F9", heard?.Key);
        Assert.Equal(1, heard?.Selection.Focus.Row);
    }

    [Fact] // ADR-0130 / ADR-0440: an exact figure the browser reports crosses back as the decimal it was
    public async Task ADR0440_the_summary_the_browser_reports_reads_back_as_decimals()
    {
        SelectionSummary? heard = null;
        var cut = Grid(ps => ps.Add(g => g.OnSelectionSummaryChanged, (SelectionSummary summary) => { heard = summary; }));

        await cut.InvokeAsync(() => Bridge().Heard("selectionSummaryChanged", Payload(
            """{"status":"answered","result":{"kind":"answered","figures":{"sum":{"kind":"number","number":93.5,"exact":{"mantissa":"9350","scale":2}}},"texts":{"sum":"93.50"}}}""")));

        var sum = heard?.Result?[SummaryFigures.Sum];
        Assert.Equal(93.50m, sum?.Exact);
        Assert.Equal((byte?)2, sum?.Exact?.Scale);
        Assert.Equal("93.50", heard?.Result?.TextOf(SummaryFigures.Sum));
    }

    [Fact] // ADR-0332 section 4 / ADR-0440 section 3: GridSource.From finds by the reference rule, so the browser marks by it
    public void ADR0332_the_reference_rule_marks_matches_only_over_GridSource_From()
    {
        Grid();
        Assert.True(Attached().GetProperty("options").GetProperty("referenceMarks").GetBoolean());
    }

    [Fact] // ADR-0332 section 4 / ADR-0440 section 3: a source that finds by a rule of its own is marked only through FindMatch
    public void ADR0332_another_source_marks_nothing_without_FindMatch()
    {
        Render<global::ExGrid.Script.ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, GridSource.Fetch<TestRow>((_, _) => ValueTask.FromResult(new GridPage<TestRow>([], 0, 0))))
            .Add(g => g.Columns, TestRows.Columns()));
        Assert.False(Attached().GetProperty("options").TryGetProperty("referenceMarks", out _));
    }

    [Fact] // ADR-0123 / ERR-11: the stylesheets the Chrome brings cross with the options, for the browser's root to render their notices
    public void ERR11_the_stylesheets_the_chrome_brings_cross_with_the_options()
    {
        Grid(ps => ps.Add(g => g.Chrome, new BringsOne()));

        var stylesheets = Attached().GetProperty("options").GetProperty("stylesheets");
        Assert.Equal("acme-grid.css", Assert.Single(stylesheets.EnumerateArray()).GetProperty("name").GetString());
        Assert.Equal(BringsOne.Stamp, stylesheets[0].GetProperty("stamp").GetString());
    }

    /// <summary>A Wrapper's Chrome that draws nothing of its own and brings a stylesheet.</summary>
    private sealed class BringsOne : IGridChrome
    {
        internal const string Stamp = "5e1f0c7a2b9d4e6f8a0b1c2d3e4f5a6b7c8d9e0f1a2b3c4d5e6f7a8b9c0d1e2f";

        public IReadOnlyList<ChromeStylesheet> Stylesheets { get; } = [new("acme-grid.css", Stamp)];

    }
}
