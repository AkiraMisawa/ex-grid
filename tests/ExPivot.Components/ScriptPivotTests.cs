using System.Text.Json;
using Bunit;
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
}
