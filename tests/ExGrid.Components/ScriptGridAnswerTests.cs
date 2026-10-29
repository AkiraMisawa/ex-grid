using System.Text.Json;
using Bunit;
using ExGrid.Cells;
using ExGrid.Clipboard;
using ExGrid.Components.Tests.Support;
using ExGrid.Finding;
using ExGrid.Selection;
using ExGrid.Validation;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// ED-35 / ADR-0380 section 5: Blazor's ExGrid over the Script Core carries a verdict, now or later, and a refusal
/// of an edit or a paste back to the browser as the answer the commit waits on, by the number the browser gave it,
/// and calls off what the user cancelled. Cell States cross with their rows, and a cell's message is asked as the
/// popover opens. What the browser does with each answer is the core's, tested there and in layer 3.
/// </summary>
public class ScriptGridAnswerTests : BunitContext
{
    private readonly BunitJSModuleInterop _module;
    private readonly BunitJSModuleInterop _grid;
    private readonly TestRow[] _rows = TestRows.Window();

    public ScriptGridAnswerTests()
    {
        _module = JSInterop.SetupModule(invocation => invocation.Identifier == "import");
        _module.Setup<string?>("stamp").SetResult(global::ExGrid.Script.ScriptCoreAsset.Stamp);
        _grid = _module.SetupModule("attach", _ => true);
        _grid.SetupVoid("update", _ => true).SetVoidResult();
        _grid.SetupVoid("state", _ => true).SetVoidResult();
    }

    private static GridColumn<TestRow>[] Columns(GridColumn<TestRow> book) =>
    [
        book,
        new("Amount", ColumnType.Number, r => r.Amount, editable: true),
    ];

    private IRenderedComponent<global::ExGrid.Script.ExGrid<TestRow>> Grid(
        GridColumn<TestRow>[] columns, Action<ComponentParameterCollectionBuilder<global::ExGrid.Script.ExGrid<TestRow>>>? extra = null)
        => Render<global::ExGrid.Script.ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Rows, _rows).Add(g => g.Columns, columns);
            extra?.Invoke(ps);
        });

    private global::ExGrid.Script.ScriptBridge<TestRow> Bridge() => Assert.IsType<DotNetObjectReference<global::ExGrid.Script.ScriptBridge<TestRow>>>(
        Assert.Single(_module.Invocations["attach"]).Arguments[1]).Value;

    private static JsonElement Parsed(object? json)
    {
        using var document = JsonDocument.Parse(Assert.IsType<string>(json));
        return document.RootElement.Clone();
    }

    /// <summary>The id the row at <paramref name="index"/> of the attach's Window crossed with.</summary>
    private long IdOf(int index)
        => Parsed(Assert.Single(_module.Invocations["attach"]).Arguments[2]).GetProperty("state").GetProperty("window")[index].GetProperty("i").GetInt64();

    private static JsonElement Edit(long answer, long row, string value) => Payload(
        $$"""{"answer":{{answer}},"row":{{row}},"column":"Book","value":"{{value}}","seenText":"Alpha","replacedText":"Alpha"}""");

    private static JsonElement Paste(long answer) => Payload(
        $$"""{"answer":{{answer}},"targets":[{"topRow":0,"leftColumn":0,"rowCount":1,"columnCount":1}],"source":{"rows":1,"columns":1},"collapsesSelection":false,"values":[["x"]],"origins":[["shownText"]],"rowSequenceVersion":0}""");

    private static JsonElement Payload(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    [Fact] // ED-35: a Reject crosses back by the browser's number, judged against the row the browser named
    public async Task ED35_ADR0380_a_verdict_is_answered_by_dotnet_against_the_row_the_browser_named()
    {
        TestRow? judged = null;
        var cut = Grid(Columns(new("Book", ColumnType.Text, r => r.Book, editable: true, validate: (row, text) =>
        {
            judged = row;
            return text == "" ? EditVerdict.Reject("A book is named.") : EditVerdict.Flag("Not a book we trade.");
        })));

        var rejected = await cut.InvokeAsync(() => Bridge().Validate(1, 1, IdOf(1), "Book", ""));
        var flagged = await cut.InvokeAsync(() => Bridge().Validate(2, 1, IdOf(1), "Book", "Delta"));

        Assert.Same(_rows[1], judged);
        Assert.Equal("""{"kind":"reject","message":"A book is named."}""", rejected);
        Assert.Equal("""{"kind":"flag","message":"Not a book we trade."}""", flagged);
    }

    [Fact] // ED-35 / ED-34: a verdict answered later sees the user's cancel on its token, and its answer is not read
    public async Task ED35_ADR0380_a_later_verdict_is_called_off_by_the_users_cancel()
    {
        var asked = new TaskCompletionSource<CancellationToken>();
        var cut = Grid(Columns(new GridColumn<TestRow>("Book", ColumnType.Text, r => r.Book, editable: true)
        {
            ValidateAsync = async (_, _, token) =>
            {
                asked.SetResult(token);
                await Task.Delay(Timeout.Infinite, token);
                return EditVerdict.Accept;
            },
        }));

        var answer = cut.InvokeAsync(() => Bridge().Validate(1, 1, IdOf(0), "Book", "Delta"));
        var token = await asked.Task;
        await cut.InvokeAsync(() => Bridge().CancelAnswer(1));

        Assert.True(token.IsCancellationRequested);
        Assert.Null(await answer);
    }

    [Fact] // ED-35: OnEdit's Refuse crosses back as its sentence, and OnPaste's as a refusal with none
    public async Task ED35_ADR0380_a_refused_edit_and_a_refused_paste_are_answered_as_refused()
    {
        var cut = Grid(Columns(new("Book", ColumnType.Text, r => r.Book, editable: true)), ps => ps
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> intent) => intent.Refuse("Trade is locked."))
            .Add(g => g.OnPaste, (GridPasteIntent intent) => intent.Refuse()));

        var edit = await cut.InvokeAsync(() => Bridge().Heard("edit", Edit(1, IdOf(0), "Delta")));
        var paste = await cut.InvokeAsync(() => Bridge().Heard("paste", Paste(2)));

        Assert.Equal("Trade is locked.", edit);
        Assert.Equal("", paste);
    }

    [Fact] // ADR-0380 s3: a verdict is the column's as the browser held it, whatever the Consumer declared since
    public async Task ADR0380_a_verdict_is_asked_of_the_columns_the_browser_judged_by()
    {
        var cut = Grid(Columns(new("Book", ColumnType.Text, r => r.Book, editable: true, validate: (_, _) => EditVerdict.Reject("Books are closed."))));
        cut.Render(ps => ps.Add(g => g.Columns, Columns(new("Book", ColumnType.Text, r => r.Book, editable: true))));

        var asked = await cut.InvokeAsync(() => Bridge().Validate(1, 1, IdOf(0), "Book", "Delta"));
        var now = await cut.InvokeAsync(() => Bridge().Validate(2, 2, IdOf(0), "Book", "Delta"));

        Assert.Equal("""{"kind":"reject","message":"Books are closed."}""", asked);
        Assert.Equal("""{"kind":"accept"}""", now);
    }

    [Fact] // ADR-0380 s3: a handler that gives up on the user's cancel, as the token asks, does not fail the grid
    public async Task ADR0380_a_handler_that_honours_the_cancel_is_answered_as_cancelled()
    {
        var started = new TaskCompletionSource();
        var cut = Grid(Columns(new("Book", ColumnType.Text, r => r.Book, editable: true)), ps => ps
            .Add(g => g.OnEdit, async (GridEditIntent<TestRow> intent) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, intent.CancellationToken);
            }));

        var answer = cut.InvokeAsync(() => Bridge().Heard("edit", Edit(1, IdOf(0), "Delta")));
        await started.Task;
        await cut.InvokeAsync(() => Bridge().CancelAnswer(1));

        // A failed handler is answered by its failure (HeardAsync): null is the cancel, caught where it is the token's.
        Assert.Null(await answer);
    }

    [Fact] // ADR-0380 s3: an edit the user cancelled before .NET reached it is never heard, and one taken is answered
    public async Task ADR0380_an_edit_cancelled_before_its_turn_is_not_handed_to_the_consumer()
    {
        var heard = new List<string>();
        var first = new TaskCompletionSource();
        var cut = Grid(Columns(new("Book", ColumnType.Text, r => r.Book, editable: true)), ps => ps
            .Add(g => g.OnEdit, async (GridEditIntent<TestRow> intent) =>
            {
                heard.Add(intent.Value);
                if (intent.Value == "Delta")
                    await first.Task;
            }));

        var delta = cut.InvokeAsync(() => Bridge().Heard("edit", Edit(1, IdOf(0), "Delta")));
        var epsilon = cut.InvokeAsync(() => Bridge().Heard("edit", Edit(2, IdOf(1), "Epsilon")));
        await cut.InvokeAsync(() => Bridge().CancelAnswer(2));
        first.SetResult();

        Assert.Null(await delta);
        Assert.Null(await epsilon);
        Assert.Equal(["Delta"], heard);
    }

    [Fact] // ADR-0240 s12 / ADR-0380 s3: a cancel heard before its call was taken in, a paste still crossing, is never handed on
    public async Task ADR0240_a_paste_cancelled_while_it_crosses_is_never_handed_to_the_consumer()
    {
        var heard = 0;
        var cut = Grid(Columns(new("Book", ColumnType.Text, r => r.Book, editable: true)), ps => ps
            .Add(g => g.OnPaste, (GridPasteIntent _) => { heard += 1; }));

        // The cancel reaches .NET while the paste's arguments are still being read: before its call is made.
        await cut.InvokeAsync(() => Bridge().CancelAnswer(1));
        var cancelled = await cut.InvokeAsync(() => Bridge().Heard("paste", Paste(1)));
        var next = await cut.InvokeAsync(() => Bridge().Heard("paste", Paste(2)));

        Assert.Null(cancelled);
        Assert.Null(next);
        Assert.Equal(1, heard);
    }

    [Fact] // ADR-0240 s11: a number missing when a later one arrives, or when the browser says how far it sent, is told lost once
    public async Task ADR0240_inputs_that_never_arrived_are_told_lost_once_in_the_grids_words()
    {
        _grid.SetupVoid("inputsLost", _ => true).SetVoidResult();
        var heard = new List<string>();
        var cut = Grid(Columns(new("Book", ColumnType.Text, r => r.Book, editable: true)), ps => ps
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> intent) => { heard.Add(Assert.IsType<string>(intent.Value)); }));

        await cut.InvokeAsync(() => Bridge().Call(1, "Heard", Payload($$"""["edit",{{Edit(1, IdOf(0), "Delta").GetRawText()}}]""")));
        await cut.InvokeAsync(() => Bridge().Call(3, "Heard", Payload($$"""["edit",{{Edit(2, IdOf(1), "Epsilon").GetRawText()}}]""")));
        await cut.InvokeAsync(() => Bridge().InputsSent(3));
        await cut.InvokeAsync(() => Bridge().InputsSent(5));

        var words = global::ExGrid.Chrome.BuiltInCommandLabels.For(global::ExGrid.Chrome.KeyRefusalLabelIds.LostOnTheWay);
        Assert.Equal([(2L, 2L, words), (4L, 5L, words)],
            _grid.Invocations["inputsLost"].Select(call => ((long)call.Arguments[0]!, (long)call.Arguments[1]!, (string)call.Arguments[2]!)));
        Assert.Equal(["Delta", "Epsilon"], heard);
        // A number that came again, or out of its order, is a defect of the bridge's, never taken.
        await Assert.ThrowsAsync<InvalidOperationException>(() => cut.InvokeAsync(() => Bridge().Call(5, "Heard", Payload("""["undo",null]"""))));
    }

    [Fact] // ED-35: a Flag's Error crosses with the row, a new judgement sends the rows again, and the message is asked once
    public async Task ED35_ADR0006_cell_states_cross_with_their_rows_and_a_message_is_asked_as_it_opens()
    {
        CellStateOf<TestRow> none = (_, _) => CellState.Normal;
        CellStateOf<TestRow> flagged = (row, column) => row.Book == "Beta" && column.Name == "Book" ? CellState.Error : CellState.Normal;
        var messages = 0;
        var cut = Grid(Columns(new("Book", ColumnType.Text, r => r.Book, editable: true)), ps => ps
            .Add(g => g.CellState, none)
            .Add(g => g.CellMessageOf, (TestRow row, GridColumn<TestRow> column) =>
            {
                messages++;
                return row.Book == "Beta" ? $"{column.Name} is not a book we trade." : null;
            }));

        var options = Parsed(Assert.Single(_module.Invocations["attach"]).Arguments[2]).GetProperty("options");
        Assert.True(options.GetProperty("cellStates").GetBoolean());
        var judgement = options.GetProperty("cellMessages").GetInt32();
        Assert.Equal(0, messages);

        cut.Render(ps => ps.Add(g => g.CellState, flagged));
        var window = Parsed(_grid.Invocations["state"][^1].Arguments[0]).GetProperty("window");
        // Another judgement: a message the browser shows is asked again.
        var updated = Parsed(_grid.Invocations["update"][^1].Arguments[0]).GetProperty("options");
        Assert.NotEqual(judgement, updated.GetProperty("cellMessages").GetInt32());
        Assert.False(window[0].TryGetProperty("s", out _));
        Assert.Equal("""["error","normal"]""", window[1].GetProperty("s").GetRawText());

        var message = await cut.InvokeAsync(() => Bridge().Message(IdOf(1), "Book"));
        Assert.Equal("Book is not a book we trade.", message);
        Assert.Equal(1, messages);
    }

    private IRenderedComponent<global::ExGrid.Script.ExGrid<TestRow>> Stepping(
        InMemoryGridSource<TestRow> source, Func<GridErrorStepRequest, CancellationToken, Task<GridFindResult>> find)
        => Render<global::ExGrid.Script.ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns(new("Book", ColumnType.Text, r => r.Book, editable: true)))
            .Add(g => g.OnFindError, find));

    private static JsonElement Step(int rowSequenceVersion) => Payload(
        $$"""{"backward":true,"from":{"row":1,"column":0},"columns":["Book","Amount"],"rowSequenceVersion":{{rowSequenceVersion}}}""");

    [Fact] // ED-38: F8 is answered by .NET, from the request the browser made, in the order it was asked under
    public async Task ED38_ADR0381_an_error_step_is_asked_of_the_consumer_and_answered_as_a_find_result()
    {
        GridErrorStepRequest? asked = null;
        var source = GridSource.From(_rows);
        var cut = Stepping(source, (request, _) =>
        {
            asked = request;
            return Task.FromResult(GridFindResult.Found(2, "Amount"));
        });
        Assert.True(Parsed(Assert.Single(_module.Invocations["attach"]).Arguments[2]).GetProperty("options").GetProperty("findsErrors").GetBoolean());

        var answer = await cut.InvokeAsync(() => Bridge().FindError(1, 1, Step(source.RowSequenceVersion)));

        Assert.Equal("""{"found":true,"row":2,"column":"Amount"}""", answer);
        Assert.NotNull(asked);
        Assert.Equal((true, new CellPosition(1, 0), source.RowSequenceVersion), (asked.Backward, asked.From, asked.RowSequenceVersion));
        Assert.Equal(["Book", "Amount"], asked.Columns);
    }

    [Fact] // ADR-0381 / ADR-0011: a step asked under an order the source has left is not handed to the Consumer
    public async Task ADR0381_an_error_step_asked_under_another_order_is_answered_as_moved()
    {
        var asked = 0;
        var source = GridSource.From(_rows);
        var cut = Stepping(source, (_, _) =>
        {
            asked++;
            return Task.FromResult(GridFindResult.NotFound);
        });
        var version = source.RowSequenceVersion;
        await cut.InvokeAsync(() => source.OnSortChanged([new SortSpec("Book", SortDirection.Descending)]));

        var answer = await cut.InvokeAsync(() => Bridge().FindError(1, 1, Step(version)));

        Assert.Equal("""{"moved":true}""", answer);
        Assert.Equal(0, asked);
        // What the source owes crosses ahead of the answer, so the browser holds the order .NET answered in.
        var sent = Parsed(_grid.Invocations["state"][^1].Arguments[0]);
        Assert.Equal(source.RowSequenceVersion, sent.GetProperty("rowSequenceVersion").GetInt32());
    }

    [Fact] // ADR-0381 / spine rule 6: a step the browser asked as the Consumer took OnFindError away is withdrawn, not a failure
    public async Task ADR0381_an_error_step_asked_as_the_consumer_took_it_away_is_answered_as_withdrawn()
    {
        var source = GridSource.From(_rows);
        var cut = Stepping(source, (_, _) => Task.FromResult(GridFindResult.NotFound));
        cut.Render(ps => ps.Add(g => g.OnFindError, null));

        var answer = await cut.InvokeAsync(() => Bridge().FindError(1, 1, Step(source.RowSequenceVersion)));

        Assert.Equal("""{"withdrawn":true}""", answer);
    }

    [Fact] // ADR-0381: Rows are ordered out of the Consumer's sight, so the step a Consumer answers needs a Source
    public void ADR0381_an_error_step_over_rows_the_grid_orders_itself_is_refused()
    {
        var refused = Assert.Throws<InvalidOperationException>(() => Grid(Columns(new("Book", ColumnType.Text, r => r.Book)), ps => ps
            .Add(g => g.OnFindError, (_, _) => Task.FromResult(GridFindResult.NotFound))));
        Assert.Contains("OnFindError", refused.Message);
    }

    private static JsonElement FindStep(string text, int rowSequenceVersion) => Payload(
        $$"""{"text":"{{text}}","matchCase":false,"wholeCell":false,"backward":false,"from":{"row":0,"column":0},"columns":["Book","Amount"],"rowSequenceVersion":{{rowSequenceVersion}}}""");

    [Fact] // ADR-0055: Ctrl+F is answered by the source in .NET, by the browser's number, where it searches, and said of the source as it attaches
    public async Task ADR0055_a_find_step_is_answered_by_the_source_in_dotnet()
    {
        var source = GridSource.From(_rows);
        var cut = Render<global::ExGrid.Script.ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns(new("Book", ColumnType.Text, r => r.Book))));
        Assert.True(Parsed(Assert.Single(_module.Invocations["attach"]).Arguments[2]).GetProperty("source").GetProperty("finds").GetBoolean());

        var answer = await cut.InvokeAsync(() => Bridge().Find(1, 1, FindStep("gam", source.RowSequenceVersion)));
        Assert.Equal("""{"found":true,"row":2,"column":"Book"}""", answer);

        // Under an order the source has left, the step is not run, and the browser is answered that the rows moved.
        var version = source.RowSequenceVersion;
        await cut.InvokeAsync(() => source.OnSortChanged([new SortSpec("Book", SortDirection.Descending)]));
        Assert.Equal("""{"moved":true}""", await cut.InvokeAsync(() => Bridge().Find(2, 1, FindStep("gam", version))));
    }
}
