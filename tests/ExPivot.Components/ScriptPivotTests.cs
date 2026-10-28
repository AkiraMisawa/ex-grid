using System.Text.Json;
using Bunit;
using ExGrid.Selection;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Xunit;

namespace ExPivot.Components.Tests;

/// <summary>
/// ADR-0370 sections 1 and 10: Blazor's ExPivot over the Script Core. The Consumer's <c>Label</c>, a .NET function,
/// crosses as the words it answers for each id the module names, and only when they changed; the records of a value
/// cell double clicked in the browser reach the Consumer's <c>OnShowDetails</c> in .NET, asked of the source the
/// report was computed from, and records no handler takes are answered so, for the browser to open them itself.
/// </summary>
public class ScriptPivotTests : BunitContext
{
    private static readonly string[] LabelIds = ["row-labels", "grand-total", "copy"];

    private readonly BunitJSModuleInterop _module;
    private readonly BunitJSModuleInterop _pivot;

    public ScriptPivotTests()
    {
        _module = JSInterop.SetupModule(invocation => invocation.Identifier == "import");
        _module.Setup<string?>("stamp").SetResult(global::ExPivot.Script.ScriptCoreAsset.Stamp);
        _module.Setup<string[]>("labelIds").SetResult(LabelIds);
        _pivot = _module.SetupModule("attach", _ => true);
        _pivot.SetupVoid("update", _ => true).SetVoidResult();
    }

    private IRenderedComponent<global::ExPivot.Script.ExPivot> Pivot(PivotSource source, Action<ComponentParameterCollectionBuilder<global::ExPivot.Script.ExPivot>>? more = null)
        => Render<global::ExPivot.Script.ExPivot>(ps =>
        {
            ps.Add(p => p.Source, source);
            more?.Invoke(ps);
        });

    private DotNetObjectReference<global::ExPivot.Script.ScriptBridge> Bridge()
        => Assert.IsType<DotNetObjectReference<global::ExPivot.Script.ScriptBridge>>(Assert.Single(_module.Invocations["attach"]).Arguments[1]);

    private static JsonElement Parsed(object? json)
    {
        using var document = JsonDocument.Parse(Assert.IsType<string>(json));
        return document.RootElement.Clone();
    }

    private static async Task<JsonElement> GrandTotalDetails(PivotSource source, int generation)
    {
        var version = (await source.AggregateAsync(new PivotQuery(), Xunit.TestContext.Current.CancellationToken)).SourceVersion;
        var payload = JsonSerializer.Serialize(new
        {
            generation,
            title = "Details: Grand Total",
            rowItems = Array.Empty<object>(),
            columnItems = Array.Empty<object>(),
            valueField = "Sum of Amount",
            query = PivotJson.Write(new PivotDetailsQuery(version, start: 0, count: 100)),
        });
        return Parsed(payload);
    }

    [Fact] // ADR-0060, ADR-0370 section 10: the attach carries the Consumer's word for each id the module names, and none it does not answer
    public void ADR0370_the_attach_carries_the_words_Label_answers()
    {
        Pivot(PivotTestContext.Bundled(), ps => ps.Add(p => p.Label, id => id == "row-labels" ? "行ラベル" : null));

        var words = Parsed(Assert.Single(_module.Invocations["attach"]).Arguments[2]).GetProperty("words");
        Assert.Equal([("row-labels", "行ラベル")], words.EnumerateObject().Select(word => (word.Name, word.Value.GetString())));
    }

    [Fact] // ADR-0060, ADR-0370 section 10: a label whose answers change crosses again though its delegate is the same, and one whose answers stay the same does not
    public void ADR0370_words_cross_again_only_when_the_answers_changed()
    {
        var japanese = false;
        Func<string, string?> label = id => japanese ? PivotWords.JapaneseFor(id) : null;
        var cut = Pivot(PivotTestContext.Bundled(), ps => ps.Add(p => p.Label, label));

        cut.Render(ps => ps.Add(p => p.Label, label));
        Assert.Empty(_pivot.Invocations["update"]);

        japanese = true;
        cut.Render(ps => ps.Add(p => p.Label, label));
        var update = Parsed(Assert.Single(_pivot.Invocations["update"]).Arguments[0]);
        Assert.Equal("行ラベル", update.GetProperty("words").GetProperty("row-labels").GetString());
    }

    [Fact] // ADR-0059, ADR-0063: records the browser hands .NET reach the Consumer's handler, asked of the source the report was computed from
    public async Task ADR0059_records_reach_the_handler_asked_of_the_reports_source()
    {
        var source = PivotTestContext.Bundled();
        PivotDetails? taken = null;
        var cut = Pivot(source, ps => ps.Add(p => p.OnShowDetails, (PivotDetails details) => taken = details));

        var payload = await GrandTotalDetails(source, generation: 1);
        var heard = await cut.InvokeAsync(() => Bridge().Value.Heard("showDetails", payload));

        Assert.True(heard);
        Assert.NotNull(taken);
        Assert.Equal("Details: Grand Total", taken!.Title);
        var page = await taken.DetailsAsync(0, 100, Xunit.TestContext.Current.CancellationToken);
        Assert.Equal(PivotTestContext.Sales.Length, page.Total);
    }

    [Fact] // ADR-0059, spine rule 6: records whose handler was let go before they arrived are answered as not taken, and the browser opens them
    public async Task ADR0059_records_no_handler_takes_are_answered_as_not_taken()
    {
        var source = PivotTestContext.Bundled();
        var calls = 0;
        var cut = Pivot(source, ps => ps.Add(p => p.OnShowDetails, (PivotDetails _) => calls++));
        cut.Render(ps => ps.Add(p => p.OnShowDetails, default(EventCallback<PivotDetails>)));

        var payload = await GrandTotalDetails(source, generation: 1);
        var heard = await cut.InvokeAsync(() => Bridge().Value.Heard("showDetails", payload));

        Assert.False(heard);
        Assert.Equal(0, calls);
    }

    [Fact] // ADR-0201's note of 2026-10-27, ADR-0370 section 1: the report's Selection the browser reports reaches the handler as a GridSelection, and none as Empty
    public async Task ADR0370_the_reports_selection_reaches_the_handler_as_a_GridSelection()
    {
        var heard = new List<GridSelection>();
        var cut = Pivot(PivotTestContext.Bundled(), ps => ps.Add(p => p.SelectionChanged, (GridSelection selection) => heard.Add(selection)));
        Assert.Contains("selectionChanged", Parsed(Assert.Single(_module.Invocations["attach"]).Arguments[2])
            .GetProperty("options").GetProperty("hears").EnumerateArray().Select(kind => kind.GetString()));

        var two = Parsed("""{"ranges":[{"topRow":1,"leftColumn":1,"rowCount":2,"columnCount":1},{"topRow":4,"leftColumn":2,"rowCount":1,"columnCount":1}],"focus":{"row":4,"column":2},"focusRangeIndex":1}""");
        var none = Parsed("""{"ranges":[],"focus":null,"focusRangeIndex":0}""");
        await cut.InvokeAsync(() => Bridge().Value.Heard("selectionChanged", two));
        await cut.InvokeAsync(() => Bridge().Value.Heard("selectionChanged", none));

        Assert.Equal(2, heard.Count);
        Assert.Equal([new SelectionRange(1, 1, 2, 1), new SelectionRange(4, 2, 1, 1)], heard[0].Ranges);
        Assert.Equal(new CellPosition(4, 2), heard[0].Focus);
        Assert.Equal(new SelectionRange(4, 2, 1, 1), heard[0].FocusRange);
        Assert.Same(GridSelection.Empty, heard[1]);
    }

    /// <summary>A held call's arguments as a browser without CompressionStream hands them over: their JSON as it is.</summary>
    private sealed class Plain(byte[] text) : IJSStreamReference
    {
        public long Length => text.LongLength;
        public ValueTask<Stream> OpenReadStreamAsync(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            Length > maxAllowedSize ? throw new ArgumentOutOfRangeException(nameof(maxAllowedSize)) : ValueTask.FromResult<Stream>(new MemoryStream(text, writable: false));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new NotSupportedException();
    }

    [Fact] // ADR-0005 (as amended 2026-10-20), spine rule 6: a Selection too large for a circuit's message is taken as a stream, and reaches the handler before the one raised after it
    public async Task ADR0005_a_held_selection_reaches_the_handler_before_the_one_after_it()
    {
        var take = _pivot.Setup<IJSStreamReference>("take", _ => true);
        var heard = new List<GridSelection>();
        var cut = Pivot(PivotTestContext.Bundled(), ps => ps.Add(p => p.SelectionChanged, (GridSelection selection) => heard.Add(selection)));
        var ranges = string.Join(",", Enumerable.Range(0, 2_000).Select(row => $$"""{"topRow":{{row * 2}},"leftColumn":1,"rowCount":1,"columnCount":1}"""));
        var arguments = System.Text.Encoding.UTF8.GetBytes($$"""["selectionChanged",{"ranges":[{{ranges}}],"focus":{"row":0,"column":1},"focusRangeIndex":0}]""");
        var crossed = Parsed($$"""{"held":3,"bytes":{{arguments.Length}}}""");
        var none = Parsed("""{"ranges":[],"focus":null,"focusRangeIndex":0}""");

        Task large = Task.CompletedTask, after = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            large = Bridge().Value.Held("Heard", crossed);
            after = Bridge().Value.Heard("selectionChanged", none);
        });
        Assert.Empty(heard);
        take.SetResult(new Plain(arguments));
        await large.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        await after.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);

        Assert.Equal([2_000, 0], heard.Select(selection => selection.Ranges.Count));
    }
}
