using System.Globalization;
using System.Text.Json;
using Bunit;
using ExGrid.Components.Tests.Support;
using ExGrid;
using ExGrid.Chrome;
using ExGrid.Selection;
using ExGrid.Summarizing;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// ADR-0370 section 1: Blazor's ExSheet over the Script Core. The Razor component's parameters cross with its defaults;
/// a document crosses as the JSON both engines read, and one the Sheet raised, bound back two ways, crosses no more;
/// the Sheet's events reach the Consumer's handlers as the Razor component raises them; a refusal in the browser is
/// thrown in .NET as the Razor component throws it, and what else the engine threw as the C# engine throws it; an edit,
/// a Cell Format change and a Linked Table's rows cross as <see cref="SheetWire"/> writes them; and <c>CommandLabel</c>,
/// a .NET function, crosses as the words it answers.
/// </summary>
public class ScriptSheetTests : BunitContext
{
    private static readonly string[] LabelIds = ["copy", "exsheet.summary-refused"];

    private readonly BunitJSModuleInterop _module;
    private readonly BunitJSModuleInterop _sheet;

    public ScriptSheetTests()
    {
        _module = JSInterop.SetupModule(invocation => invocation.Identifier == "import");
        _module.Setup<string?>("stamp").SetResult(global::ExSheet.Script.ScriptCoreAsset.Stamp);
        _module.Setup<string[]>("labelIds").SetResult(LabelIds);
        _sheet = _module.SetupModule("attach", _ => true);
        _sheet.SetupVoid("update", _ => true).SetVoidResult();
    }

    private IRenderedComponent<global::ExSheet.Script.ExSheet> Sheet(Action<ComponentParameterCollectionBuilder<global::ExSheet.Script.ExSheet>>? parameters = null)
        => Render<global::ExSheet.Script.ExSheet>(ps => parameters?.Invoke(ps));

    private DotNetObjectReference<global::ExSheet.Script.ScriptBridge> Bridge()
        => Assert.IsType<DotNetObjectReference<global::ExSheet.Script.ScriptBridge>>(Assert.Single(_module.Invocations["attach"]).Arguments[1]);

    private JsonElement Attach() => Parsed(Assert.Single(_module.Invocations["attach"]).Arguments[2]);

    private JsonElement[] Updates() => [.. _sheet.Invocations["update"].Select(update => Parsed(update.Arguments[0]))];

    private static JsonElement Parsed(object? json)
    {
        using var document = JsonDocument.Parse(Assert.IsType<string>(json));
        return document.RootElement.Clone();
    }

    private static Workbook WorkbookOf(string typed)
    {
        var workbook = new Workbook(CultureInfo.GetCultureInfo("en-US"));
        workbook.Do(workbook.Sheets[0].Name, SheetEdit.Enter(CellAddress.Parse("A1"), typed));
        return workbook;
    }

    // The Sheet's report of a change, as the browser writes it: its documents, and how many of the Consumer's it had
    // been sent.
    private static JsonElement Raised(Workbook workbook, int documentsSent) => Parsed(JsonSerializer.Serialize(new
    {
        workbook = workbook.ToDocument().ToJson(),
        sheet = workbook.ToDocument(workbook.Sheets[0].Name).ToJson(),
        documentsSent,
    }));

    [Fact] // ADR-0370 section 1: the Razor component's defaults cross, and only the handlers the Consumer bound are heard
    public void ADR0370_the_attach_carries_the_razor_components_defaults_and_the_handlers_bound()
    {
        Sheet(ps => ps.Add(p => p.Culture, CultureInfo.GetCultureInfo("ja-JP")).Add(p => p.DocumentChanged, (SheetDocument _) => { }));

        var attach = Attach();
        var options = attach.GetProperty("options");
        Assert.Equal("ja-JP", options.GetProperty("locale").GetString());
        Assert.Equal(Components.ExSheet.DefaultRowHeightPx, options.GetProperty("rowHeight").GetDouble());
        Assert.Equal(600, options.GetProperty("viewportHeight").GetDouble());
        Assert.Equal(800, options.GetProperty("viewportWidth").GetDouble());
        Assert.True(options.GetProperty("showSheetTabs").GetBoolean());
        Assert.False(options.GetProperty("showToolbar").GetBoolean());
        Assert.False(options.GetProperty("allowCopyWithHeaders").GetBoolean());
        Assert.Equal(["average", "count", "sum"], options.GetProperty("summaryFigures").EnumerateArray().Select(figure => figure.GetString()));
        // Unbound, Ctrl+F1 stays the browser's (ADR-0100): showToolbarChanged is not heard.
        Assert.Equal(["documentChanged"], options.GetProperty("hears").EnumerateArray().Select(kind => kind.GetString()));
        Assert.Equal(JsonValueKind.Null, attach.GetProperty("document").ValueKind);
        Assert.Equal(0, attach.GetProperty("documentsSent").GetInt32());
        // Not in a browser, so across a circuit: a message too large for one is held (ADR-0005 as amended 2026-10-20).
        Assert.True(attach.GetProperty("circuit").GetBoolean());
    }

    [Fact] // ADR-0030: a Wrapper's paper paints the cells in its font, so the Sheet is handed its widths and Density
    public void ADR0030_a_Wrappers_cascaded_widths_cross_and_none_crosses_without_one()
    {
        Sheet();
        Sheet(ps => ps.AddCascadingValue(new GridPresentationDefaults(10.4, 8.0, 4.95, 14, GridDensity.Standard)));

        var attached = _module.Invocations["attach"].Select(attach => Parsed(attach.Arguments[2]).GetProperty("options")).ToArray();
        Assert.False(attached[0].TryGetProperty("presentationDefaults", out _));
        var defaults = attached[1].GetProperty("presentationDefaults");
        Assert.Equal(8.0, defaults.GetProperty("digitWidthPx").GetDouble());
        Assert.Equal("standard", defaults.GetProperty("density").GetString());
    }

    [Fact] // ADR-0048, ADR-0331: a document raised and bound back crosses no more; the Consumer's own does, and null after one opens an empty Sheet
    public async Task ADR0048_a_document_bound_two_ways_crosses_only_when_it_is_the_consumers_own()
    {
        var first = WorkbookOf("first").ToDocument("Sheet1");
        SheetDocument? bound = first;
        var raisedWorkbooks = new List<WorkbookDocument>();
        var cut = Sheet(ps => ps
            .Add(p => p.Document, first)
            .Add(p => p.DocumentChanged, (SheetDocument document) => bound = document)
            .Add(p => p.WorkbookChanged, (WorkbookDocument document) => raisedWorkbooks.Add(document)));
        Assert.Equal(first.ToJson(), Attach().GetProperty("document").GetString());
        Assert.Equal(1, Attach().GetProperty("documentsSent").GetInt32());

        // The user typed: the browser raises both documents, the Sheet Document first.
        await cut.InvokeAsync(() => Bridge().Value.Heard("workbookChanged", Raised(WorkbookOf("typed"), documentsSent: 1)));
        Assert.NotSame(first, bound);
        Assert.Contains("typed", bound!.ToJson(), StringComparison.Ordinal);
        Assert.Single(raisedWorkbooks);

        cut.Render(ps => ps.Add(p => p.Document, bound));
        Assert.Empty(Updates());

        var own = WorkbookOf("own").ToDocument("Sheet1");
        cut.Render(ps => ps.Add(p => p.Document, own));
        var update = Assert.Single(Updates());
        Assert.Equal(own.ToJson(), update.GetProperty("document").GetString());
        Assert.Equal(2, update.GetProperty("documentsSent").GetInt32());

        // A document raised before the Consumer's reached the browser is not the one on screen: it crosses again.
        var late = bound;
        cut.Render(ps => ps.Add(p => p.Document, late));
        Assert.Equal(late!.ToJson(), Updates()[1].GetProperty("document").GetString());

        cut.Render(ps => ps.Add(p => p.Document, (SheetDocument?)null));
        Assert.Equal(JsonValueKind.Null, Updates()[2].GetProperty("document").ValueKind);
        Assert.Equal(4, Updates()[2].GetProperty("documentsSent").GetInt32());
    }

    [Fact] // ADR-0331, ADR-0048: Document and Workbook are not set together, and the culture is the Sheet's own once it is open
    public void ADR0331_document_and_workbook_together_and_a_culture_moved_are_refused_by_name()
    {
        var document = WorkbookOf("x").ToDocument();
        Assert.Throws<InvalidOperationException>(() => Sheet(ps => ps.Add(p => p.Workbook, document).Add(p => p.Document, document.Sheets[0])));

        var cut = Sheet(ps => ps.Add(p => p.Culture, CultureInfo.GetCultureInfo("en-US")));
        var moved = Assert.Throws<InvalidOperationException>(() => cut.Render(ps => ps.Add(p => p.Culture, CultureInfo.GetCultureInfo("de-DE"))));
        Assert.Contains("culture", moved.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0370 section 10: the locale is said once, the culture current when the Sheet was created, not the one current at each update
    public void ADR0370_the_locale_is_the_culture_current_when_the_sheet_was_created()
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var cut = Sheet();
            Assert.Equal("en-US", Attach().GetProperty("options").GetProperty("locale").GetString());

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            cut.Render(ps => ps.Add(p => p.ShowFormulaBar, false));
            Assert.Equal("en-US", Assert.Single(Updates()).GetProperty("options").GetProperty("locale").GetString());
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    [Fact] // ADR-0370 section 1: a command waiting on a Sheet whose attach failed is refused with the failure, never left waiting
    public async Task ADR0370_a_command_waiting_on_a_failed_attach_is_refused()
    {
        var labels = _module.Setup<string[]>("labelIds");
        var cut = Sheet();
        var undo = cut.InvokeAsync(() => cut.Instance.UndoAsync());
        Assert.False(undo.IsCompleted);

        labels.SetException(new JSException("The Script Core refused the document."));

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => undo);
        Assert.IsType<JSException>(refused.InnerException);
    }

    [Fact] // ADR-0123: a script that does not load, the circuit's fault included, is refused as another version's is, and a command waiting is told so
    public async Task ADR0123_a_script_that_did_not_load_is_refused_and_a_waiting_command_is_told()
    {
        var stamp = _module.Setup<string?>("stamp");
        var cut = Sheet(ps => ps.Add(p => p.CommandLabel, id => id == ReloadNoticeLabelIds.Script ? "Reload, please." : null));
        var undo = cut.InvokeAsync(() => cut.Instance.UndoAsync());

        stamp.SetException(new TaskCanceledException("The import took longer than the circuit waits."));

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => undo);
        Assert.Contains("another version's ExSheet script", refused.Message, StringComparison.Ordinal);
        Assert.Equal("Reload, please.", cut.Find("[data-ex-notice='script']").TextContent);
        Assert.Empty(_module.Invocations["attach"]);

        // The notice is worded through CommandLabel as it now stands (ADR-0203).
        cut.Render(ps => ps.Add(p => p.CommandLabel, id => id == ReloadNoticeLabelIds.Script ? "Bitte neu laden." : null));
        Assert.Equal("Bitte neu laden.", cut.Find("[data-ex-notice='script']").TextContent);
    }

    [Fact] // ADR-0330 s10: .NET's read runs in the opening's turn in the browser; its failure is thrown as it was, and an Escape cancels its token
    public async Task ADR0330_an_opening_reads_in_dotnet_when_the_browser_asks_and_its_failure_is_thrown_whole()
    {
        var opened = WorkbookOf("opened").ToDocument();
        var answers = _sheet.Setup<JsonElement>("open", _ => true);
        var cut = Sheet();
        var bridge = Bridge().Value;

        var opening = cut.InvokeAsync(() => cut.Instance.OpenAsync(_ => Task.FromResult(opened)));
        // Each opening reaches the browser once the Sheet is ready, after the InvokeAsync returns: wait for its call.
        cut.WaitForAssertion(() => Assert.Single(_sheet.Invocations["open"]));
        var id = Assert.IsType<int>(_sheet.Invocations["open"][^1].Arguments[0]);
        Assert.Equal(opened.ToJson(), Parsed(await bridge.Read(id)).GetProperty("document").GetString());
        answers.SetResult(Parsed("""{"done":true}"""));
        Assert.True(await opening);

        // A read that fails: the browser is told its message, and .NET throws the failure itself.
        answers = _sheet.Setup<JsonElement>("open", _ => true);
        var failure = new IOException("The file is not an .xlsx workbook.");
        opening = cut.InvokeAsync(() => cut.Instance.OpenAsync(_ => Task.FromException<WorkbookDocument>(failure)));
        cut.WaitForAssertion(() => Assert.Equal(2, _sheet.Invocations["open"].Count));
        id = Assert.IsType<int>(_sheet.Invocations["open"][^1].Arguments[0]);
        Assert.Equal("""{"failed":"The file is not an .xlsx workbook."}""", await bridge.Read(id));
        answers.SetResult(Parsed("""{"thrown":{"kind":"invalidOperation","message":"The file is not an .xlsx workbook."}}"""));
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => opening));

        // An Escape the browser heard cancels the read's token, and nothing opens.
        answers = _sheet.Setup<JsonElement>("open", _ => true);
        opening = cut.InvokeAsync(() => cut.Instance.OpenAsync(async token =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return opened;
        }));
        cut.WaitForAssertion(() => Assert.Equal(3, _sheet.Invocations["open"].Count));
        id = Assert.IsType<int>(_sheet.Invocations["open"][^1].Arguments[0]);
        var read = bridge.Read(id);
        Assert.False(read.IsCompleted);
        await bridge.CancelRead(id);
        Assert.Equal("""{"cancelled":true}""", await read);
        answers.SetResult(Parsed("""{"done":false}"""));
        Assert.False(await opening);
    }

    [Fact] // ADR-0330 s10, ADR-0180: a save hands the write a Workbook of .NET's that computes the Values the browser shows
    public async Task ADR0330_a_save_writes_dotnets_copy_of_the_browsers_workbook_with_its_values()
    {
        var browser = new Workbook(CultureInfo.GetCultureInfo("en-US"));
        browser.DeclareLinkedTable("Rates", ["Code", "Rate"], "Code");
        browser.Do("Sheet1", SheetEdit.Enter(CellAddress.Parse("A1"), "=RAND()"));
        browser.Do("Sheet1", SheetEdit.Enter(CellAddress.Parse("A2"), "=SUM(Rates[Rate])"));
        browser.Do("Sheet1", SheetEdit.Enter(CellAddress.Parse("A3"), "=TODAY()"));
        browser.Do("Sheet1", SheetEdit.Enter(CellAddress.Parse("A4"), "=NOW()"));
        browser.PushLinkedTable("Rates", [[Value.FromText("USD"), Value.FromNumber(1.5)], [Value.FromText("EUR"), Value.FromNumber(2)]]);
        var moment = new DateTime(2026, 10, 28, 9, 30, 15, 250);
        browser.NowSource = () => moment;
        browser.RecalculateVolatile();
        browser.SetToday(new DateOnly(2026, 10, 28));
        browser.SetSeed(42, browser.Changes + 3);
        var saved = JsonSerializer.Serialize(new
        {
            document = browser.ToDocument().ToJson(),
            tables = new[] { new { name = "Rates", rows = SheetWire.WriteRows([[Value.FromText("USD"), Value.FromNumber(1.5)], [Value.FromText("EUR"), Value.FromNumber(2)]]) } },
            seed = "42",
            changes = browser.Changes,
            today = new { year = 2026, month = 10, day = 28 },
            now = new { year = 2026, month = 10, day = 28, hour = 9, minute = 30, second = 15, millisecond = 250 },
        });
        var answers = _sheet.Setup<JsonElement>("save", _ => true);
        var cut = Sheet();
        Workbook? written = null;

        var saving = cut.InvokeAsync(() => cut.Instance.SaveAsync(workbook =>
        {
            written = workbook;
            return Task.CompletedTask;
        }));
        cut.WaitForAssertion(() => Assert.Single(_sheet.Invocations["save"]));
        var id = Assert.IsType<int>(_sheet.Invocations["save"][^1].Arguments[0]);
        Assert.Equal("""{"done":true}""", await Bridge().Value.Write(id, Parsed(saved)));
        answers.SetResult(Parsed("""{"done":null}"""));
        await saving;

        var copy = Assert.IsType<Workbook>(written);
        foreach (var cell in new[] { "A1", "A2", "A3", "A4" })
            Assert.Equal(browser.Sheets[0].GetValue(CellAddress.Parse(cell)), copy.Sheets[0].GetValue(CellAddress.Parse(cell)));
        Assert.Equal(3.5, copy.Sheets[0].GetValue(CellAddress.Parse("A2"))?.Number);
        Assert.Equal((42UL, browser.Changes), (copy.Seed, copy.Changes));

        // A write that fails: the browser is told its message, and .NET throws the failure itself.
        answers = _sheet.Setup<JsonElement>("save", _ => true);
        var failure = new IOException("The disk is full.");
        saving = cut.InvokeAsync(() => cut.Instance.SaveAsync(_ => Task.FromException(failure)));
        cut.WaitForAssertion(() => Assert.Equal(2, _sheet.Invocations["save"].Count));
        id = Assert.IsType<int>(_sheet.Invocations["save"][^1].Arguments[0]);
        Assert.Equal("""{"failed":"The disk is full."}""", await Bridge().Value.Write(id, Parsed(saved)));
        answers.SetResult(Parsed("""{"thrown":{"kind":"invalidOperation","message":"The disk is full."}}"""));
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => saving));
    }

    [Fact] // ADR-0005 as amended 2026-10-28: an event, an answer and a save too large for a circuit's message are taken as streams, in their turn
    public async Task ADR0005_messages_held_in_the_browser_are_taken_as_streams_in_their_turn()
    {
        WorkbookDocument? raised = null;
        var heard = new List<string>();
        var cut = Sheet(ps => ps
            .Add(p => p.WorkbookChanged, (WorkbookDocument document) => { heard.Add("workbook"); raised = document; })
            .Add(p => p.EditingChanged, (bool _) => heard.Add("editing")));
        var large = WorkbookOf(new string('x', 20_000));
        var raisedJson = JsonSerializer.Serialize(new object[] { "workbookChanged", new { workbook = large.ToDocument().ToJson(), sheet = (string?)null, documentsSent = 0 } });
        var taking = new TaskCompletionSource();
        _sheet.Setup<IJSStreamReference>("take", invocation => Equals(invocation.Arguments[0], 1))
            .SetResult(FakeJSStream.Gated(raisedJson, taking.Task));

        // The held event, its kind and payload taken as the call's arguments, and one after it: the second waits for
        // the first to be taken.
        var first = cut.InvokeAsync(() => Bridge().Value.Held("Heard", Held(1, raisedJson)));
        var second = cut.InvokeAsync(() => Bridge().Value.Heard("editingChanged", Parsed("true")));
        Assert.Empty(heard);
        Assert.False(second.IsCompleted);
        taking.SetResult();
        await first;
        await second;
        cut.WaitForAssertion(() => Assert.Equal(["workbook", "editing"], heard));
        Assert.Equal(large.ToDocument().ToJson(), raised?.ToJson());

        // An answer, held as the event was, and gzip-compressed.
        var documentsJson = JsonSerializer.Serialize(new { workbook = large.ToDocument().ToJson(), sheet = (string?)null });
        _sheet.Setup<JsonElement>("documents").SetResult(Held(2, documentsJson));
        _sheet.Setup<IJSStreamReference>("take", invocation => Equals(invocation.Arguments[0], 2)).SetResult(FakeJSStream.Gzipped(documentsJson));
        Assert.Equal(large.ToDocument().ToJson(), (await cut.InvokeAsync(() => cut.Instance.ToWorkbookDocumentAsync())).ToJson());

        // A save's write, its id and Workbook taken as the call's arguments, answered as the write it makes.
        var answers = _sheet.Setup<JsonElement>("save", _ => true);
        Workbook? written = null;
        var saving = cut.InvokeAsync(() => cut.Instance.SaveAsync(workbook =>
        {
            written = workbook;
            return Task.CompletedTask;
        }));
        cut.WaitForAssertion(() => Assert.Single(_sheet.Invocations["save"]));
        var id = Assert.IsType<int>(_sheet.Invocations["save"][^1].Arguments[0]);
        var writeJson = JsonSerializer.Serialize(new object[]
        {
            id,
            new { document = large.ToDocument().ToJson(), tables = Array.Empty<object>(), seed = (string?)null, changes = 0, today = (object?)null, now = (object?)null },
        });
        _sheet.Setup<IJSStreamReference>("take", invocation => Equals(invocation.Arguments[0], 4)).SetResult(FakeJSStream.Gzipped(writeJson));
        Assert.Equal("""{"done":true}""", await cut.InvokeAsync(() => Bridge().Value.Held("Write", Held(4, writeJson))));
        answers.SetResult(Parsed("""{"done":null}"""));
        await saving;
        Assert.Equal(large.ToDocument().ToJson(), written?.ToDocument().ToJson());

        // A stream that is not the length declared is refused, never read as an answer.
        _sheet.Setup<JsonElement>("linkedTables").SetResult(Held(3, "[ ]"));
        _sheet.Setup<IJSStreamReference>("take", invocation => Equals(invocation.Arguments[0], 3)).SetResult(FakeJSStream.Of("[]"));
        await Assert.ThrowsAsync<InvalidDataException>(() => cut.InvokeAsync(() => cut.Instance.LinkedTablesAsync()));
    }

    // What the browser hands .NET in place of a message too large for one: its number and length.
    private static JsonElement Held(int held, string json)
        => Parsed(JsonSerializer.Serialize(new { held, bytes = System.Text.Encoding.UTF8.GetByteCount(json) }));

    [Fact] // ADR-0048, ADR-0071: a command answers as the browser's Sheet did, and its refusal is thrown as the Razor component throws it
    public async Task ADR0048_a_refusal_in_the_browser_is_thrown_as_a_SheetRefusedException()
    {
        var cut = Sheet();
        _sheet.Setup<JsonElement>("undo").SetResult(Parsed("""{"done":true}"""));
        _sheet.Setup<JsonElement>("openFormatCells").SetResult(Parsed("""{"refused":{"reason":"editIsOpen","message":"Finish the edit first."}}"""));

        Assert.True(await cut.InvokeAsync(() => cut.Instance.UndoAsync()));
        var refused = await Assert.ThrowsAsync<SheetRefusedException>(() => cut.InvokeAsync(() => cut.Instance.OpenFormatCellsAsync()));
        Assert.Equal(new SheetRefusal(SheetRefusalReason.EditIsOpen, "Finish the edit first."), refused.Refusal);
    }

    [Fact] // ADR-0370 section 1, ADR-0048, ADR-0331 §6: an edit crosses as SheetWire writes it, to the one Sheet or to the Sheet it names
    public async Task ADR0370_an_edit_crosses_as_SheetWire_writes_it()
    {
        var cut = Sheet();
        _sheet.Setup<JsonElement>("do", _ => true).SetResult(Parsed("""{"done":null}"""));
        var edit = SheetEdit.Enter(CellAddress.Parse("A1"), "=1+2");

        await cut.InvokeAsync(() => cut.Instance.DoAsync(edit));
        await cut.InvokeAsync(() => cut.Instance.DoAsync("Prices", edit));

        var calls = _sheet.Invocations["do"];
        Assert.Equal([null, "Prices"], calls.Select(call => call.Arguments[0]));
        Assert.All(calls, call => Assert.Equal(SheetWire.Write(edit), call.Arguments[1]));
    }

    [Fact] // ADR-0370 section 1, ADR-0049: what the browser's engine threw at the caller crosses as data, and is thrown as the C# engine throws it
    public async Task ADR0370_what_the_engine_threw_in_the_browser_is_thrown_as_the_csharp_engine_throws_it()
    {
        var cut = Sheet();
        _sheet.Setup<JsonElement>("do", _ => true)
            .SetResult(Parsed("""{"thrown":{"kind":"formulaSyntax","message":"m","formula":"=1+","position":3,"reason":"An operand is missing."}}"""));
        // A refused snapshot as the bridge writes it (writeRepeatedKey; src/npm/sheet-blazor/test/bridge.test.ts reads the same).
        _sheet.Setup<JsonElement>("pushLinkedTable", call => (string?)call.Arguments[0] == "Rates").SetResult(Parsed("""
            {"thrown":{"kind":"repeatedKey","message":"m","repeatedKey":"{\"table\":\"Rates\",\"keyColumns\":[\"Code\"],\"keys\":[\"USD\"],\"firstRow\":0,\"repeats\":[\"usd\"],\"secondRow\":1,\"change\":{\"valueChanges\":[\"B2\"],\"recalculated\":[],\"rows\":[1],\"columns\":[],\"reformatsUnnamedRows\":false}}"}}
            """));
        _sheet.Setup<JsonElement>("pushLinkedTable", call => (string?)call.Arguments[0] == "Nothing")
            .SetResult(Parsed("""{"thrown":{"kind":"invalidOperation","message":"No Linked Table named 'Nothing' is declared."}}"""));
        _sheet.Setup<JsonElement>("cellFormatAt", _ => true)
            .SetResult(Parsed("""{"thrown":{"kind":"outOfRange","message":"No Sheet of the Workbook is named 'Three'."}}"""));
        _sheet.Setup<JsonElement>("declareLinkedTable", _ => true).SetResult(Parsed("""{"thrown":{"kind":"argument","message":"'1st' is no name."}}"""));

        var syntax = await Assert.ThrowsAsync<FormulaSyntaxException>(() => cut.InvokeAsync(() => cut.Instance.DoAsync(SheetEdit.Enter(CellAddress.Parse("A1"), "=1+"))));
        Assert.Equal(("=1+", 3, "An operand is missing."), (syntax.Formula, syntax.Position, syntax.Reason));

        Value?[] usd = [Value.FromText("USD"), Value.FromNumber(1)];
        Value?[] again = [Value.FromText("usd"), null];
        var rows = new[] { usd, again };
        var repeated = await Assert.ThrowsAsync<RepeatedKeyException>(() => cut.InvokeAsync(() => cut.Instance.PushLinkedTableAsync("Rates", rows)));
        Assert.Equal(("Rates", "Code", Value.FromText("USD"), 0, 1), (repeated.Table, repeated.KeyColumn, repeated.Key, repeated.FirstRow, repeated.SecondRow));
        Assert.Equal([CellAddress.Parse("B2")], repeated.Change.ValueChanges);
        Assert.Contains("usd", repeated.Message, StringComparison.Ordinal);
        Assert.Equal(SheetWire.WriteRows(rows), _sheet.Invocations["pushLinkedTable"][0].Arguments[1]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => cut.InvokeAsync(() => cut.Instance.PushLinkedTableAsync("Nothing", [])));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => cut.InvokeAsync(() => cut.Instance.CellFormatAtAsync("Three", CellAddress.Parse("A1"))));
        await Assert.ThrowsAsync<ArgumentException>(() => cut.InvokeAsync(() => cut.Instance.DeclareLinkedTableAsync("1st", ["A"])));
    }

    [Fact] // ADR-0071, ADR-0049: a Cell Format and the Linked Tables are read back as the browser's Sheet holds them, and a change that sets nothing never crosses
    public async Task ADR0071_cell_formats_and_linked_tables_are_read_back_from_the_browser()
    {
        var cut = Sheet();
        // A Cell Format as the bridge writes it (writeCellFormat; src/npm/sheet-blazor/test/bridge.test.ts expects the same text).
        _sheet.Setup<JsonElement>("cellFormatAt", _ => true).SetResult(Parsed("""
            {"done":"{\"numberFormat\":\"0.00\",\"alignment\":\"general\",\"font\":{\"colour\":\"Automatic\",\"bold\":true,\"italic\":false,\"underline\":false,\"strikethrough\":false},\"fill\":\"none\",\"borders\":{\"top\":\"none\",\"bottom\":\"none\",\"left\":\"none\",\"right\":\"none\"}}"}
            """));
        _sheet.Setup<JsonElement>("linkedTables").SetResult(Parsed("""[{"name":"Rates","columns":["Code","Region","Rate"],"keyColumns":["Code","Region"],"isWaiting":false,"rowCount":2}]"""));
        _sheet.Setup<JsonElement>("setCellFormat", _ => true).SetResult(Parsed("""{"done":true}"""));

        var format = await cut.InvokeAsync(() => cut.Instance.CellFormatAtAsync(CellAddress.Parse("B2")));
        Assert.Equal(new CellFormat(NumberFormat.Parse("0.00"), HorizontalAlignment.General, new CellFont(Bold: true), CellFill.None, CellBorders.None), format);
        Assert.Equal([null, "B2"], Assert.Single(_sheet.Invocations["cellFormatAt"]).Arguments);

        var table = Assert.Single(await cut.InvokeAsync(() => cut.Instance.LinkedTablesAsync()));
        Assert.Equal(("Rates", false, 2, (string?)null), (table.Name, table.IsWaiting, table.RowCount, table.Key));
        Assert.Equal(["Code", "Region", "Rate"], table.Columns);
        Assert.Equal(["Code", "Region"], table.KeyColumns);

        await Assert.ThrowsAsync<ArgumentException>(() => cut.InvokeAsync(() => cut.Instance.SetCellFormatAsync(new CellFormatChange())));
        Assert.True(await cut.InvokeAsync(() => cut.Instance.SetNumberFormatAsync(null)));
        Assert.Equal(SheetWire.Write(new CellFormatChange { NumberFormat = NumberFormat.General }), Assert.Single(_sheet.Invocations["setCellFormat"]).Arguments[0]);
    }

    [Fact] // ADR-0100 (decided 2026-10-28): SetCellFormatFromFocusAsync makes its change from the Focus cell the browser read, sets it against that reading, and lets the reading go when the change throws
    public async Task ADR0100_a_change_from_the_focus_cell_is_set_against_the_reading_it_was_made_from()
    {
        var cut = Sheet();
        _sheet.Setup<JsonElement>("readFocusFormat").SetResult(Parsed("""
            {"done":{"reading":7,"format":"{\"numberFormat\":\"General\",\"alignment\":\"general\",\"font\":{\"colour\":\"Automatic\",\"bold\":true,\"italic\":false,\"underline\":false,\"strikethrough\":false},\"fill\":\"none\",\"borders\":{\"top\":\"none\",\"bottom\":\"none\",\"left\":\"none\",\"right\":\"none\"}}"}}
            """));
        _sheet.Setup<JsonElement>("setCellFormatFrom", _ => true).SetResult(Parsed("""{"done":true}"""));
        _sheet.SetupVoid("forgetFocusFormat", _ => true).SetVoidResult();

        Assert.True(await cut.InvokeAsync(() => cut.Instance.SetCellFormatFromFocusAsync(focus => new() { Bold = !focus.Font.Bold })));
        var set = Assert.Single(_sheet.Invocations["setCellFormatFrom"]);
        Assert.Equal([7, SheetWire.Write(new CellFormatChange { Bold = false })], set.Arguments);

        // A change that sets nothing is the caller's error, and the reading is let go.
        await Assert.ThrowsAsync<ArgumentException>(() => cut.InvokeAsync(() => cut.Instance.SetCellFormatFromFocusAsync(_ => new CellFormatChange())));
        Assert.Equal([7], Assert.Single(_sheet.Invocations["forgetFocusFormat"]).Arguments);
        Assert.Single(_sheet.Invocations["setCellFormatFrom"]);

        // The Selection moved between the read and the set: refused as the browser refused it.
        _sheet.Setup<JsonElement>("setCellFormatFrom", _ => true).SetResult(Parsed("""{"refused":{"reason":"selectionMoved","message":"The selection moved."}}"""));
        var refused = await Assert.ThrowsAsync<SheetRefusedException>(() => cut.InvokeAsync(() => cut.Instance.SetCellFormatFromFocusAsync(_ => new() { Italic = true })));
        Assert.Equal(SheetRefusalReason.SelectionMoved, refused.Refusal.Reason);
    }

    [Fact] // ADR-0100, ADR-0048: over no Selection SetCellFormatFromFocusAsync answers false, and under an open edit it is refused, and neither asks for the change
    public async Task ADR0100_a_change_from_the_focus_cell_is_not_asked_for_over_no_selection_or_an_open_edit()
    {
        var cut = Sheet();
        _sheet.Setup<JsonElement>("readFocusFormat").SetResult(Parsed("""{"done":null}"""));
        var asked = false;
        Assert.False(await cut.InvokeAsync(() => cut.Instance.SetCellFormatFromFocusAsync(_ =>
        {
            asked = true;
            return new() { Bold = true };
        })));
        Assert.False(asked);

        // While an edit is open the read is refused, before the change is asked for, as on the Razor component.
        _sheet.Setup<JsonElement>("readFocusFormat").SetResult(Parsed("""{"refused":{"reason":"editIsOpen","message":"Finish the edit first."}}"""));
        var refused = await Assert.ThrowsAsync<SheetRefusedException>(() => cut.InvokeAsync(() => cut.Instance.SetCellFormatFromFocusAsync(_ =>
        {
            asked = true;
            return new() { Bold = true };
        })));
        Assert.Equal(SheetRefusalReason.EditIsOpen, refused.Refusal.Reason);
        Assert.False(asked);
        Assert.Empty(_sheet.Invocations["setCellFormatFrom"]);
    }

    [Fact] // ADR-0130, ADR-0048, ADR-0071, ADR-0331: the Sheet's events reach the handlers as the Razor component raises them
    public async Task ADR0370_the_sheets_events_reach_the_handlers()
    {
        SummaryFigures? figures = null;
        bool? editing = null;
        SheetRefusal? refusal = null;
        SheetsChange? sheets = null;
        var cut = Sheet(ps => ps
            .Add(p => p.SummaryFiguresChanged, (SummaryFigures chosen) => figures = chosen)
            .Add(p => p.EditingChanged, (bool open) => editing = open)
            .Add(p => p.OnFormatKeyRefused, (SheetRefusal refused) => refusal = refused)
            .Add(p => p.SheetsChanged, (SheetsChange change) => sheets = change));

        await cut.InvokeAsync(() => Bridge().Value.Heard("summaryFiguresChanged", Parsed("""["numericalCount","max"]""")));
        await cut.InvokeAsync(() => Bridge().Value.Heard("editingChanged", Parsed("true")));
        await cut.InvokeAsync(() => Bridge().Value.Heard("formatKeyRefused", Parsed("""{"reason":"editIsOpen","message":"m"}""")));
        await cut.InvokeAsync(() => Bridge().Value.Heard("sheetsChanged", Parsed("""{"before":["Sheet1"],"after":["Sheet1","Sheet2"],"shown":"Sheet2"}""")));

        Assert.Equal(SummaryFigures.NumericalCount | SummaryFigures.Max, figures);
        Assert.True(editing);
        Assert.True(cut.Instance.IsEditing);
        Assert.Equal(SheetRefusalReason.EditIsOpen, refusal?.Reason);
        Assert.Equal(["Sheet1", "Sheet2"], sheets?.After);
        Assert.Equal("Sheet2", sheets?.Shown);
    }

    [Fact] // ADR-0130: the Selection Summary the Sheet answered in the browser reaches the handler whole, its columns the Sheet's letters
    public async Task ADR0130_the_selection_summary_reaches_the_handler_with_the_sheets_columns()
    {
        var summaries = new List<SelectionSummary>();
        var cut = Sheet(ps => ps.Add(p => p.OnSelectionSummaryChanged, (SelectionSummary summary) => summaries.Add(summary)));
        Assert.Contains("selectionSummaryChanged", Attach().GetProperty("options").GetProperty("hears").EnumerateArray().Select(kind => kind.GetString()));

        // As the bridge writes it (bridge.test.ts): no column names cross.
        const string request = """{"ranges":[{"topRow":0,"leftColumn":0,"rowCount":3,"columnCount":1}],"rowSequenceVersion":0,"figures":["average","count","sum"],"focus":{"row":0,"column":0}}""";
        await cut.InvokeAsync(() => Bridge().Value.Heard("selectionSummaryChanged", Parsed($$$"""{"status":"pending","request":{{{request}}},"result":null}""")));
        await cut.InvokeAsync(() => Bridge().Value.Heard("selectionSummaryChanged", Parsed($$$"""
            {"status":"answered","request":{{{request}}},"result":{
              "figures":{"average":{"kind":"error","error":"divideByZero"},"count":{"kind":"number","exact":"3"},"sum":{"kind":"number","number":0.30000000000000004}},
              "texts":{"sum":"0.3"} } }
            """)));
        await cut.InvokeAsync(() => Bridge().Value.Heard("selectionSummaryChanged", Parsed($$$"""{"status":"declined","request":{{{request}}},"result":{"reason":"too many"}}""")));

        Assert.Equal([SelectionSummaryStatus.Pending, SelectionSummaryStatus.Answered, SelectionSummaryStatus.Declined], summaries.Select(summary => summary.Status));
        Assert.Null(summaries[0].Result);
        var asked = summaries[1].Request ?? throw new InvalidOperationException("An answered summary carries its question.");
        Assert.Equal(new SelectionRange(0, 0, 3, 1), Assert.Single(asked.Ranges));
        Assert.Equal(SummaryFigures.Average | SummaryFigures.Count | SummaryFigures.Sum, asked.Figures);
        Assert.Equal(new CellPosition(0, 0), asked.Focus);
        Assert.Equal(16_384, asked.Columns.Count);
        Assert.Equal(["A", "B", "Z", "AA", "XFD"], new[] { 0, 1, 25, 26, 16_383 }.Select(at => asked.Columns[at]));
        var answer = summaries[1].Result ?? throw new InvalidOperationException("An answered summary carries its answer.");
        Assert.Equal(ExGrid.Data.AggregateResult.Of(3m), answer[SummaryFigures.Count]);
        Assert.Equal(ExGrid.Data.AggregateResult.Of(0.30000000000000004), answer[SummaryFigures.Sum]);
        Assert.Equal(ExGrid.Data.AggregateResult.Of(ExGrid.Data.AggregateError.DivideByZero), answer[SummaryFigures.Average]);
        Assert.Equal("0.3", answer.TextOf(SummaryFigures.Sum));
        Assert.Equal("too many", summaries[2].Result?.DeclineReason);
    }

    [Fact] // ADR-0036, ADR-0370 section 10: CommandLabel is asked for each id the module names, and crosses again only when its answers changed
    public void ADR0370_command_label_crosses_as_the_words_it_answers()
    {
        var japanese = true;
        Func<string, string?> label = id => japanese && id == "copy" ? "コピー" : null;
        var cut = Sheet(ps => ps.Add(p => p.CommandLabel, label));
        Assert.Equal([("copy", "コピー")], Attach().GetProperty("words").EnumerateObject().Select(word => (word.Name, word.Value.GetString())));

        cut.Render(ps => ps.Add(p => p.CommandLabel, label));
        Assert.Empty(Updates());

        japanese = false;
        cut.Render(ps => ps.Add(p => p.CommandLabel, label));
        Assert.Empty(Assert.Single(Updates()).GetProperty("words").EnumerateObject());
    }

    private sealed class Keyed(string id)
    {
        public string Id { get; } = id;
    }

    [Fact] // ADR-0430 section 1: the Scope crosses by its name in the page, and a refusal decided there reaches the handler
    public async Task ADR0430_the_Scope_crosses_by_name_and_a_refusal_reaches_the_handler()
    {
        var scope = new PointingScope();
        PointingRefusal? refusal = null;
        var cut = Sheet(ps => ps
            .Add(p => p.PointingScope, scope)
            .Add(p => p.OnPointingRefused, (PointingRefusal refused) => refusal = refused));

        var options = Attach().GetProperty("options");
        Assert.Equal(scope.Id, options.GetProperty("pointingScope").GetString());
        Assert.Contains("pointingRefused", options.GetProperty("hears").EnumerateArray().Select(kind => kind.GetString()));

        await cut.InvokeAsync(() => Bridge().Value.Heard("pointingRefused", Parsed("""{"reason":"blankKey","message":"m"}""")));
        Assert.Equal(new PointingRefusal(PointingRefusalReason.BlankKey, "m"), refusal);
    }

    [Fact] // ADR-0431 section 2: each Linked Table column the Formula reads reaches the handler with the colour the core handed out
    public async Task ADR0431_linked_column_colours_reach_the_handler_with_their_places_in_the_palette()
    {
        LinkedColumnColours? told = null;
        var cut = Sheet(ps => ps.Add(p => p.OnLinkedColumnColoursChanged, (LinkedColumnColours columns) => told = columns));

        Assert.Contains("linkedColumnColoursChanged", Attach().GetProperty("options").GetProperty("hears").EnumerateArray().Select(kind => kind.GetString()));
        await cut.InvokeAsync(() => Bridge().Value.Heard("linkedColumnColoursChanged",
            Parsed("""[{"table":"Positions","column":"PV","colour":1},{"table":"Positions","column":"Book","colour":2}]""")));

        Assert.Equal(
            [new LinkedColumnColour(new LinkedTableColumn("Positions", "PV"), global::ExGrid.Cells.ReferenceColour.Reported(1)),
             new LinkedColumnColour(new LinkedTableColumn("Positions", "Book"), global::ExGrid.Cells.ReferenceColour.Reported(2))],
            told);
        await cut.InvokeAsync(() => Bridge().Value.Heard("linkedColumnColoursChanged", Parsed("[]")));
        Assert.Empty(Assert.IsType<LinkedColumnColours>(told));
    }

    [Fact] // ADR-0430 section 2: a Scope's declaration reads a row as the table's row, written as a Linked Table's rows are
    public void ADR0430_a_declaration_reads_a_row_as_SheetWire_writes_a_Linked_Tables_row()
    {
        var scope = new PointingScope();
        IReadOnlyList<Value?> Read(Keyed row) => [Value.FromText(row.Id), null];

        var inBrowser = scope.RegisterGrid<Keyed>("Positions", Read).InBrowser
            ?? throw new InvalidOperationException("A Scope's declaration says where it stands in the page.");

        Assert.Equal(scope.Id, inBrowser.Scope);
        Assert.Equal("Positions", inBrowser.Table);
        Assert.Equal(SheetWire.WriteRows([Read(new Keyed("R-1"))]), $"[{inBrowser.TableRow(new Keyed("R-1"))}]");
    }

    [Fact] // ADR-0430 section 3: a Scope holding a Razor grid refuses a Script Sheet, and one holding a Script Sheet a Razor grid
    public void ADR0430_a_Scope_of_both_kinds_is_refused_by_name()
    {
        var razor = new PointingScope();
        var razorGrid = razor.RegisterGrid<Keyed>("Positions", row => [Value.FromText(row.Id)]).InBrowser
            ?? throw new InvalidOperationException("A Scope's declaration says where it stands in the page.");
        razorGrid.Take(inBrowser: false);
        var thrown = Assert.Throws<InvalidOperationException>(() => Sheet(ps => ps.Add(p => p.PointingScope, razor)));
        Assert.Contains("a Sheet drawn by the Script Core cannot join it", thrown.Message, StringComparison.Ordinal);

        var script = new PointingScope();
        var scriptGrid = script.RegisterGrid<Keyed>("Positions", row => [Value.FromText(row.Id)]).InBrowser
            ?? throw new InvalidOperationException("A Scope's declaration says where it stands in the page.");
        Sheet(ps => ps.Add(p => p.PointingScope, script));
        thrown = Assert.Throws<InvalidOperationException>(() => scriptGrid.Take(inBrowser: false));
        Assert.Contains("a Razor ExGrid cannot join it", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>A Chrome of the Consumer's own that fills the Context Menu and the find panel over the Script Core.</summary>
    private sealed class MenuChrome : IGridChrome
    {
        public ScriptSlots ScriptSlots => ScriptSlots.ContextMenu | ScriptSlots.FindPanel;

        public RenderFragment? FilterPanel(FilterPanelContext context) => null;

        public RenderFragment? ColumnMenu(ColumnMenuContext context) => null;

        public RenderFragment? CellEditor(CellEditorContext context) => null;

        public RenderFragment? LoadingIndicator(LoadingContext context) => null;

        public RenderFragment? ContextMenu(GridMenuView view) => builder =>
        {
            for (var i = 0; i < view.Items.Count; i++)
            {
                builder.OpenElement(0, "button");
                builder.AddAttribute(1, ScriptSlotAttributes.MenuItem, i);
                builder.AddContent(2, $"{view.Items[i].Label} ({(view.Items[i].Enabled ? "enabled" : "disabled")})");
                builder.CloseElement();
            }
        };
    }

    [Fact] // ADR-0410 sections 1 and 3, ADR-0332 section 5: the Chrome fills the grid's slots but the find panel, a slot is drawn beside the root, handed to the script, and goes when unmounted
    public async Task ADR0410_the_Chrome_fills_the_sheets_grid_slots_but_the_find_panel()
    {
        _sheet.SetupVoid("slotRendered", _ => true).SetVoidResult();
        var cut = Sheet(ps => ps.Add(p => p.Chrome, new MenuChrome()));
        Assert.Equal("""["contextMenu"]""", Attach().GetProperty("options").GetProperty("razorSlots").GetRawText());

        await cut.InvokeAsync(() => Bridge().Value.MountSlot(1, 1, "contextMenu",
            """{"column":"A","columnType":"text","commands":[{"id":"exsheet.insert-rows","label":"Insert rows above","enabled":true}]}"""));
        var holder = cut.Find("div[hidden] > .ex-razor-slot");
        Assert.Equal("Insert rows above (enabled)", holder.QuerySelector("button[data-ex-menu-item='0']")?.TextContent);
        // Beside the root, never inside it.
        Assert.Same(holder.ParentElement, cut.Nodes.OfType<AngleSharp.Dom.IElement>().Last());
        Assert.Equal([1L, 1], Assert.Single(_sheet.Invocations["slotRendered"]).Arguments.Take(2));

        await cut.InvokeAsync(() => Bridge().Value.UnmountSlot(1));
        Assert.Empty(cut.FindAll(".ex-razor-slot"));
    }

    /// <summary>A Sheet Chrome that draws the Toolbar Items and the Sheet Tabs' faces from the views the script sends.</summary>
    private sealed class ToolbarChrome : ISheetChrome
    {
        public SheetScriptSlots SheetScriptSlots => SheetScriptSlots.ToolbarItem | SheetScriptSlots.SheetTab | SheetScriptSlots.SheetTabAdd;

        public RenderFragment? FilterPanel(FilterPanelContext context) => null;

        public RenderFragment? ColumnMenu(ColumnMenuContext context) => null;

        public RenderFragment? CellEditor(CellEditorContext context) => null;

        public RenderFragment? LoadingIndicator(LoadingContext context) => null;

        public RenderFragment? ToolbarItem(SheetToolbarItemView view) => builder =>
        {
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, SheetScriptSlotAttributes.Press, "list");
            builder.AddContent(2, $"{view.Name} ({view.Kind}, {view.Icon}, {view.Swatch ?? "no swatch"}, {view.Rows.Count} rows)");
            builder.CloseElement();
            builder.OpenElement(3, "div");
            builder.AddAttribute(4, SheetScriptSlotAttributes.List, "");
            for (var i = 0; i < view.Choices.Count; i++)
            {
                builder.OpenElement(5, "button");
                builder.AddAttribute(6, SheetScriptSlotAttributes.Choice, i);
                builder.AddContent(7, $"{view.Choices[i].Label}{(view.Choices[i].Selected ? " (selected)" : "")}");
                builder.CloseElement();
            }
            builder.CloseElement();
        };

        public RenderFragment? SheetTab(SheetTabContext context) => builder => builder.AddContent(0, $"[{context.Name}{(context.Selected ? "*" : "")}]");

        public RenderFragment? SheetTabAdd() => static builder => builder.AddContent(0, "add");
    }

    [Fact] // ADR-0410 sections 1 and 3: an ISheetChrome fills the Sheet's own slots too, each drawn from the view the script sends
    public async Task ADR0410_a_sheet_chrome_fills_the_toolbar_items_and_the_tabs_faces()
    {
        _sheet.SetupVoid("slotRendered", _ => true).SetVoidResult();
        var cut = Sheet(ps => ps.Add(p => p.Chrome, new ToolbarChrome()));
        Assert.Equal("""["toolbarItem","sheetTab","sheetTabAdd"]""", Attach().GetProperty("options").GetProperty("razorSlots").GetRawText());

        await cut.InvokeAsync(() => Bridge().Value.MountSlot(1, 1, "toolbarItem",
            """{"id":"fill","kind":"split","name":"Fill Colour","tooltip":"Fill Colour","icon":"fill","text":null,"swatch":"#FFFF00","moreName":"Fill Colour, more","pressed":false,"disabledReason":null,"choices":[{"label":"No Fill","swatch":null,"selected":true,"disabledReason":null},{"label":"Red","swatch":"#FF0000","selected":false,"disabledReason":null}],"rows":[[0],[1]],"content":false}"""));
        await cut.InvokeAsync(() => Bridge().Value.MountSlot(2, 1, "sheetTab", """{"name":"Sheet1","selected":true}"""));
        await cut.InvokeAsync(() => Bridge().Value.MountSlot(3, 1, "sheetTabAdd", "{}"));
        var slots = cut.FindAll("div[hidden] > .ex-razor-slot");
        Assert.Equal(["Fill Colour (Split, Fill, #FFFF00, 2 rows)", "No Fill (selected)", "Red"],
            slots[0].QuerySelectorAll("button").Select(button => button.TextContent));
        Assert.Equal(["[Sheet1*]", "add"], slots.Skip(1).Select(slot => slot.TextContent));
    }

    /// <summary>A Sheet Chrome that names the Toolbar Items and draws nothing for them.</summary>
    private sealed class BlankToolbarChrome : ISheetChrome
    {
        public SheetScriptSlots SheetScriptSlots => SheetScriptSlots.ToolbarItem;

        public RenderFragment? FilterPanel(FilterPanelContext context) => null;

        public RenderFragment? ColumnMenu(ColumnMenuContext context) => null;

        public RenderFragment? CellEditor(CellEditorContext context) => null;

        public RenderFragment? LoadingIndicator(LoadingContext context) => null;
    }

    [Fact] // ADR-0410, the spine's first rule: a slot the Chrome names and draws nothing for is refused, never left empty
    public async Task ADR0410_a_sheet_chrome_that_names_a_slot_and_draws_nothing_is_refused()
    {
        _sheet.SetupVoid("slotRendered", _ => true).SetVoidResult();
        var cut = Sheet(ps => ps.Add(p => p.Chrome, new BlankToolbarChrome()));
        await cut.InvokeAsync(() => Bridge().Value.MountSlot(1, 1, "toolbarItem",
            """{"id":"bold","kind":"toggle","name":"Bold","tooltip":"Bold (Ctrl+B)","icon":"bold","text":null,"swatch":null,"moreName":null,"pressed":false,"disabledReason":null,"choices":[],"rows":[],"content":false}"""));
        // The slot is drawn as it is mounted, so the refusal has been made by the time the mount returns.
        Assert.True(Renderer.UnhandledException.IsCompleted);
        var refused = Assert.IsType<InvalidOperationException>(await Renderer.UnhandledException);
        Assert.Contains("BlankToolbarChrome names ToolbarItem", refused.Message);
    }

    /// <summary>A Sheet Chrome that draws Format Cells as a line of what its view says.</summary>
    private sealed class FormatCellsChrome : ISheetChrome
    {
        public SheetScriptSlots SheetScriptSlots => SheetScriptSlots.FormatCells;

        public RenderFragment? FilterPanel(FilterPanelContext context) => null;

        public RenderFragment? ColumnMenu(ColumnMenuContext context) => null;

        public RenderFragment? CellEditor(CellEditorContext context) => null;

        public RenderFragment? LoadingIndicator(LoadingContext context) => null;

        public RenderFragment? FormatCells(SheetFormatCellsView view) => builder => builder.AddContent(0, string.Join(" | ",
            view.Title, view.Labels.Ok, string.Join(",", view.Tabs.Select(tab => tab.Value)),
            string.Join(",", view.Number.Categories.Select(each => $"{each.Value}{(each.Selected ? "*" : "")}{(each.Enabled ? "" : "!")}")),
            view.Number.Categories[^1].Note ?? "no note",
            string.Join(",", view.Border.Edges.Select(edge => $"{edge.Value}:{edge.Pressed}:{(edge.Line is null ? "differs" : "line")}")),
            string.Join(",", view.Fill.Rows.SelectMany(row => row).Select(swatch => $"{view.Fill.ValueOf(swatch)}={swatch.Name}{(swatch.Selected ? "*" : "")}")),
            view.Font.Palette.FirstValue));
    }

    [Fact] // ADR-0410, ADR-0071: an ISheetChrome draws Format Cells from the view the script sends, its choices' values as the script reads them
    public async Task ADR0410_a_sheet_chrome_draws_format_cells_from_the_view()
    {
        _sheet.SetupVoid("slotRendered", _ => true).SetVoidResult();
        var cut = Sheet(ps => ps.Add(p => p.Chrome, new FormatCellsChrome()));
        Assert.Equal("""["formatCells"]""", Attach().GetProperty("options").GetProperty("razorSlots").GetRawText());

        await cut.InvokeAsync(() => Bridge().Value.MountSlot(1, 1, "formatCells", """{"title":"Format Cells","labels":{"tabs":"Format Cells","category":"Category","generalHint":"General cells have no specific number format.","textHint":"Text cells show what is typed, as typed.","decimalPlaces":"Decimal places","thousandsSeparator":"Use 1000 Separator (,)","negativeNumbers":"Negative numbers","type":"Type","codes":"Codes","horizontal":"Horizontal","fontStyle":"Font style","underline":"Underline","underlineNone":"None","underlineSingle":"Single","strikethrough":"Strikethrough","normalFont":"Normal font","colour":"Colour","lineStyle":"Style","presets":"Presets","border":"Border","preview":"Preview","backgroundColour":"Background Colour","automatic":"Automatic","noColour":"No Colour","moreColours":"More Colours","ok":"OK","cancel":"Cancel"},"tabs":[{"value":"number","label":"Number"},{"value":"alignment","label":"Alignment"},{"value":"font","label":"Font"},{"value":"border","label":"Border"},{"value":"fill","label":"Fill"}],"refusal":null,"swatches":[[{"index":0,"name":"White, Background 1","paint":"#FFFFFF"}]],"number":{"categories":[{"value":"general","label":"General","selected":true,"enabled":true,"note":null},{"value":"number","label":"Number","selected":false,"enabled":true,"note":null},{"value":"accounting","label":"Accounting","selected":false,"enabled":false,"note":"Accounting pads each value with * to fill its column, which ExSheet does not read. Use Currency, or type a code under Custom."}],"hint":"General cells have no specific number format.","takesDecimalPlaces":false,"thousandsSeparator":null,"currencySymbol":null,"negativeNumbers":[],"takesCode":false,"codeRefusal":null,"typesLegend":null,"types":[]},"alignments":[{"value":"general","label":"General","selected":true,"enabled":true,"note":null},{"value":"left","label":"Left","selected":false,"enabled":true,"note":null}],"font":{"styles":[{"value":"regular","label":"Regular","selected":true,"enabled":true,"note":null},{"value":"italic","label":"Italic","selected":false,"enabled":true,"note":null}],"underline":false,"strikethrough":false,"normalFont":true,"palette":{"target":"font","legend":"Colour","first":"Automatic","firstSelected":true,"selected":null}},"border":{"lineStyles":[[{"value":"none","label":"None","selected":false,"width":"1","dashes":null},{"value":"hair","label":"Hair","selected":false,"width":"1","dashes":"1 1"}]],"presets":[{"value":"none","label":"None","selected":false,"enabled":true,"note":null}],"edges":[{"value":"top","label":"Top","pressed":"mixed","enabled":true,"inPreview":true,"line":null},{"value":"insideHorizontal","label":"Horizontal","pressed":"false","enabled":false,"inPreview":false,"line":{"none":true,"paint":"currentColor","width":"1","dashes":null}}],"palette":{"target":"border","legend":"Colour","first":"Automatic","firstSelected":true,"selected":null}},"fill":{"target":"fill","legend":"Background Colour","first":"No Colour","firstSelected":false,"selected":0}}"""));
        Assert.Equal(
            "Format Cells | OK | number,alignment,font,border,fill | general*,number,accounting! | "
            + "Accounting pads each value with * to fill its column, which ExSheet does not read. Use Currency, or type a code under Custom. | "
            + "top:mixed:differs,insideHorizontal:false:line | fill:0=White, Background 1* | font:none",
            cut.Find("div[hidden] > .ex-razor-slot").TextContent);
    }

    [Fact] // ADR-0100, ADR-0370 step 2: ToolbarContent crosses as the rows it declares, in markup order, and again when they change
    public void ADR0100_toolbar_content_crosses_as_the_rows_it_declares()
    {
        var cut = Render<ScriptSheetSupport.ScriptSheetWithToolbar>();
        Assert.Equal(
            """["default",{"label":"Review","keyTip":"R","items":[{"button":1,"text":"Mark reviewed","keyTip":"M","content":false},"separator","bold",{"button":2,"text":"Flag","content":true}]}]""",
            Attach().GetProperty("options").GetProperty("toolbar").GetRawText());
        // The rows and items draw nothing of their own: the Script Core draws the toolbar.
        Assert.Empty(cut.FindAll(".ex-sheet-toolbar-row"));

        cut.Render(ps => ps.Add(p => p.Reason, "Nothing to review."));
        var update = Assert.Single(Updates());
        Assert.Equal("Nothing to review.", update.GetProperty("options").GetProperty("toolbar")[1].GetProperty("items")[0].GetProperty("disabledReason").GetString());
    }

    [Fact] // ADR-0100: a button inside a component of the Consumer's that renders on its own reaches the browser, as the Razor component's item redraws itself
    public async Task ADR0100_a_toolbar_button_rendered_by_a_consumers_component_alone_crosses_again()
    {
        var cut = Render<ScriptSheetSupport.ScriptSheetWithOwnButtons>();
        Assert.Equal("""[{"label":"Review","items":[{"button":1,"text":"Approve","content":false}]}]""",
            Attach().GetProperty("options").GetProperty("toolbar").GetRawText());

        await cut.InvokeAsync(() => cut.Instance.Button!.Disable("Nothing to approve."));
        cut.WaitForAssertion(() => Assert.Equal("Nothing to approve.",
            Updates()[^1].GetProperty("options").GetProperty("toolbar")[0].GetProperty("items")[0].GetProperty("disabledReason").GetString()));
    }

    [Fact] // ADR-0100: a button's OnClick runs when the browser says it was pressed, its content is drawn from its markup, and a refusal is said in the Sheet's notice
    public async Task ADR0100_a_toolbar_buttons_press_runs_its_on_click_and_its_content_is_drawn_from_its_markup()
    {
        _sheet.SetupVoid("slotRendered", _ => true).SetVoidResult();
        _sheet.SetupVoid("tell", _ => true).SetVoidResult();
        var cut = Render<ScriptSheetSupport.ScriptSheetWithToolbar>(ps => ps.Add(p => p.OnFlag, () => throw new SheetRefusedException(new SheetRefusal(SheetRefusalReason.EditIsOpen, "An edit is open."))));

        await cut.InvokeAsync(() => Bridge().Value.ToolbarPressed(1, 1));
        Assert.Equal(1, cut.Instance.Reviewed);

        await cut.InvokeAsync(() => Bridge().Value.MountSlot(1, 1, "toolbarContent", """{"button":2}"""));
        Assert.Equal("F", cut.Find("div[hidden] > .ex-razor-slot > b.flag-icon").TextContent);

        await cut.InvokeAsync(() => Bridge().Value.ToolbarPressed(2, 2));
        Assert.Equal(["An edit is open."], _sheet.Invocations["tell"].Select(call => call.Arguments[0]));
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    [Fact] // ADR-0240 section 6: a save awaited inside a press is asked for inside that press's turn, by its number, and one called outside any press in its own place
    public async Task ADR0240_a_save_called_from_a_toolbar_buttons_on_click_is_asked_for_inside_the_press()
    {
        var answers = _sheet.Setup<JsonElement>("save", _ => true);
        IRenderedComponent<ScriptSheetSupport.ScriptSheetWithToolbar>? cut = null;
        cut = Render<ScriptSheetSupport.ScriptSheetWithToolbar>(ps => ps.Add(p => p.OnFlag, () => cut!.Instance.Sheet!.SaveAsync(_ => Task.CompletedTask)));

        var press = cut.InvokeAsync(() => Bridge().Value.ToolbarPressed(2, 7));
        cut.WaitForAssertion(() => Assert.Single(_sheet.Invocations["save"]));
        Assert.Equal(7, Assert.IsType<int>(_sheet.Invocations["save"][^1].Arguments[1]));
        // The press holds its turn until the save it awaits has ended.
        Assert.False(press.IsCompleted);
        answers.SetResult(Parsed("""{"done":null}"""));
        await press;
        Assert.False(Renderer.UnhandledException.IsCompleted);

        // Outside any press, the save takes its own place.
        answers = _sheet.Setup<JsonElement>("save", _ => true);
        var saving = cut.InvokeAsync(() => cut.Instance.Sheet!.SaveAsync(_ => Task.CompletedTask));
        cut.WaitForAssertion(() => Assert.Equal(2, _sheet.Invocations["save"].Count));
        Assert.Null(_sheet.Invocations["save"][^1].Arguments[1]);
        answers.SetResult(Parsed("""{"done":null}"""));
        await saving;
    }

    /// <summary>A Toolbar Item of the Consumer's own, which the Script Core has no counterpart for.</summary>
    private sealed class OwnItem : ToolbarItemBase
    {
        protected override ToolbarItemFace Face() => new(ToolbarItemKind.Button, "Own", ToolbarIcon.None);

        protected override Task PressAsync() => Task.CompletedTask;
    }

    [Fact] // ADR-0100, the spine's first rule: an item of the Consumer's own is refused by name, never left out
    public void ADR0100_a_toolbar_item_of_the_consumers_own_is_refused_on_the_script_sheet()
    {
        var refused = Assert.Throws<InvalidOperationException>(() => Sheet(ps => ps.Add(p => p.ToolbarContent, toolbar =>
        {
            toolbar.OpenComponent<OwnItem>(0);
            toolbar.CloseComponent();
        })));
        Assert.Contains("OwnItem is a Toolbar Item of its own", refused.Message);
    }
}
