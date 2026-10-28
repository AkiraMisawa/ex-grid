using System.Globalization;
using System.Text.Json;
using Bunit;
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
/// thrown in .NET as the Razor component throws it; and <c>CommandLabel</c>, a .NET function, crosses as the words it
/// answers.
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
}
