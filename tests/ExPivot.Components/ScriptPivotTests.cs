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

    [Fact] // A11Y-20's note of 2026-10-30: the element the script mounts on is Busy and says it is mounting until the script attaches, and a refused script's says neither
    public void A11Y20_the_mount_is_busy_until_the_script_attaches_and_a_refused_one_is_not()
    {
        var stamp = _module.Setup<string?>("stamp");
        var cut = Pivot(SalesData.Bundled());

        var mount = cut.Find("[data-ex-mounting='pivot']");
        Assert.Equal("true", mount.GetAttribute("aria-busy"));

        stamp.SetResult(new string('0', 64));

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-ex-notice='script']")));
        Assert.Empty(cut.FindAll("[data-ex-mounting]"));
        Assert.Empty(cut.FindAll("[aria-busy]"));
    }

    [Fact] // ADR-0060, ADR-0370 section 10: the attach carries the Consumer's word for each id the module names, and none it does not answer
    public void ADR0370_the_attach_carries_the_words_Label_answers()
    {
        Pivot(SalesData.Bundled(), ps => ps.Add(p => p.Label, id => id == "row-labels" ? "行ラベル" : null));

        var words = Parsed(Assert.Single(_module.Invocations["attach"]).Arguments[2]).GetProperty("words");
        Assert.Equal([("row-labels", "行ラベル")], words.EnumerateObject().Select(word => (word.Name, word.Value.GetString())));
    }

    [Fact] // ADR-0016, ADR-0030: the report's Cell Metrics and a Wrapper's cascaded widths cross to the browser, where its grids and label columns are sized by them
    public void ADR0016_cell_metrics_and_a_wrappers_widths_cross_to_the_pivot()
    {
        var metrics = new global::ExGrid.Columns.CellTextMetrics(9, 7, 3, 8);
        var defaults = new global::ExGrid.GridPresentationDefaults(10.4, 8.0, 4.95, 14, global::ExGrid.GridDensity.Standard);
        Render<CascadingValue<global::ExGrid.GridPresentationDefaults>>(ps => ps
            .Add(c => c.Value, defaults)
            .AddChildContent<global::ExPivot.Script.ExPivot>(pivot => pivot
                .Add(p => p.Source, SalesData.Bundled())
                .Add(p => p.CellMetrics, metrics)));

        var options = Parsed(Assert.Single(_module.Invocations["attach"]).Arguments[2]).GetProperty("options");
        Assert.Equal(7, options.GetProperty("cellMetrics").GetProperty("digitWidthPx").GetDouble());
        Assert.Equal(8, options.GetProperty("cellMetrics").GetProperty("cellHorizontalPaddingPx").GetDouble());
        Assert.Equal("standard", options.GetProperty("presentationDefaults").GetProperty("density").GetString());
        Assert.Equal(14, options.GetProperty("presentationDefaults").GetProperty("fontSizePx").GetDouble());
    }

    [Fact] // ADR-0123 / ERR-11: the stylesheets the Chrome brings cross with the options, whose notices the browser's pivot renders beside its own
    public void ERR11_the_stylesheets_the_Chrome_and_its_grid_Chrome_bring_cross_to_the_pivot()
    {
        Pivot(SalesData.Bundled(), ps => ps.Add(p => p.Chrome, new BringsOne()));

        var stylesheets = Parsed(Assert.Single(_module.Invocations["attach"]).Arguments[2]).GetProperty("options").GetProperty("stylesheets");
        Assert.Equal([("acme-pivot.css", BringsOne.Stamp), ("acme-grid.css", BringsOne.Stamp)],
            stylesheets.EnumerateArray().Select(sheet => (sheet.GetProperty("name").GetString(), sheet.GetProperty("stamp").GetString())));
    }

    /// <summary>A Wrapper's Pivot Chrome, bringing a stylesheet of its own and dressing the report grid with a Chrome
    /// that brings another, as MudPivotChrome brings mud-ex-pivot.css and its MudGridChrome mud-ex-grid.css; it fills no
    /// slot with Razor.</summary>
    private sealed class BringsOne : global::ExPivot.Chrome.IPivotChrome
    {
        internal const string Stamp = "0f1e2d3c4b5a69788796a5b4c3d2e1f00f1e2d3c4b5a69788796a5b4c3d2e1f0";

        public IReadOnlyList<global::ExGrid.Chrome.ChromeStylesheet> Stylesheets { get; } = [new("acme-pivot.css", Stamp)];

        public global::ExGrid.Chrome.IGridChrome GridChrome(Func<string, string?> commandLabel) => new GridBringsOne();
    }

    private sealed class GridBringsOne : global::ExGrid.Chrome.IGridChrome
    {
        public IReadOnlyList<global::ExGrid.Chrome.ChromeStylesheet> Stylesheets { get; } = [new("acme-grid.css", BringsOne.Stamp)];

    }

    [Fact] // ADR-0060, ADR-0370 section 10: a label whose answers change crosses again though its delegate is the same, and one whose answers stay the same does not
    public void ADR0370_words_cross_again_only_when_the_answers_changed()
    {
        var japanese = false;
        Func<string, string?> label = id => japanese ? PivotWords.JapaneseFor(id) : null;
        var cut = Pivot(SalesData.Bundled(), ps => ps.Add(p => p.Label, label));

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
        var source = SalesData.Bundled();
        PivotDetails? taken = null;
        var cut = Pivot(source, ps => ps.Add(p => p.OnShowDetails, (PivotDetails details) => taken = details));

        var payload = await GrandTotalDetails(source, generation: 1);
        var heard = await cut.InvokeAsync(() => Bridge().Value.Heard("showDetails", payload));

        Assert.True(heard);
        Assert.NotNull(taken);
        Assert.Equal("Details: Grand Total", taken!.Title);
        var page = await taken.DetailsAsync(0, 100, Xunit.TestContext.Current.CancellationToken);
        Assert.Equal(SalesData.Records.Length, page.Total);
    }

    [Fact] // ADR-0059, spine rule 6: records whose handler was let go before they arrived are answered as not taken, and the browser opens them
    public async Task ADR0059_records_no_handler_takes_are_answered_as_not_taken()
    {
        var source = SalesData.Bundled();
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
        var cut = Pivot(SalesData.Bundled(), ps => ps.Add(p => p.SelectionChanged, (GridSelection selection) => heard.Add(selection)));
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
        var cut = Pivot(SalesData.Bundled(), ps => ps.Add(p => p.SelectionChanged, (GridSelection selection) => heard.Add(selection)));
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

    // An answer that names Items of a field with an Order Key: the line of their keys, and the answer after it.
    private static (JsonElement Keys, string Answer) KeysAndAnswer(string text)
    {
        var at = text.IndexOf('\n');
        Assert.True(at > 0, "The answer came with no line of keys.");
        return (Parsed(text[..at]), text[(at + 1)..]);
    }

    [Fact] // ADR-0370 section 1, ADR-0067: a question the source fails crosses in the source's own words, which the browser shows as the reason
    public async Task ADR0370_a_question_the_source_fails_crosses_in_its_own_words()
    {
        var source = new LiveSource { Fails = new InvalidOperationException("The warehouse is out of reach.") };
        Pivot(source);

        var answer = await Bridge().Value.Aggregate(1, 1, PivotJson.Write(new PivotQuery(rows: [new PivotQueryField("Region")])));

        Assert.Equal("The warehouse is out of reach.", Parsed(answer).GetProperty("failed").GetString());
    }

    [Fact] // ADR-0067, ADR-0370 section 1: the change a Refresh the browser asked says is the browser's own, which asks again once it is done; one said after it crosses
    public async Task ADR0067_the_change_a_refresh_says_is_not_passed_on()
    {
        _pivot.SetupVoid("changed", _ => true).SetVoidResult();
        var source = new LiveSource(canRefresh: true);
        var cut = Pivot(source);

        await Bridge().Value.Refresh(1);

        Assert.Equal(1, source.Refreshes);
        Assert.Empty(_pivot.Invocations["changed"]);
        source.Notify("v2");
        cut.WaitForAssertion(() => Assert.Single(_pivot.Invocations["changed"]));
    }

    [Fact] // ADR-0412: an answer that names no Item of a field with an Order Key crosses as it is
    public async Task ADR0412_an_answer_naming_no_keyed_field_crosses_as_it_is()
    {
        var source = PivotSource.From(SalesData.Records, [
            new PivotField<Sale>("Region", PivotFieldType.Text, s => s.Region) { OrderKey = item => (string)item },
            .. SalesData.Fields.Skip(1),
        ]);
        Pivot(source);

        var answer = await Bridge().Value.Aggregate(1, 1, PivotJson.Write(new PivotQuery(rows: [new PivotQueryField("Product")])));

        Assert.DoesNotContain('\n', answer);
        Assert.False(PivotJson.ReadAnswer(answer).IsRefused);
    }

    [Fact] // ADR-0412: a date Item hands the Order Key its DateTime, as .NET's engine hands it
    public async Task ADR0412_a_date_item_hands_the_order_key_its_datetime()
    {
        var day = new DateTime(2026, 10, 28, 9, 30, 0).AddTicks(7);
        var source = PivotSource.From(SalesData.Records, [
            new PivotField<Sale>("Day", PivotFieldType.Date, _ => day) { OrderKey = item => ((DateTime)item).Ticks },
            .. SalesData.Fields,
        ]);
        Pivot(source);

        var (answered, _) = KeysAndAnswer(await Bridge().Value.Aggregate(1, 1, PivotJson.Write(new PivotQuery(rows: [new PivotQueryField("Day")]))));

        var item = Assert.Single(Assert.Single(answered.GetProperty("orderKeys").EnumerateArray()).GetProperty("items").EnumerateArray());
        Assert.Equal(day.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture), item[1].GetProperty("bigint").GetString());
    }

    [Fact] // ADR-0412: a field's Order Key, a .NET function, crosses as the key of each Item an answer names, as the browser compares it
    public async Task ADR0412_an_order_key_crosses_as_each_answered_items_key()
    {
        var regionRank = new Dictionary<string, int> { ["West"] = 1, ["North"] = 2 };
        var source = PivotSource.From(SalesData.Records, [
            new PivotField<Sale>("Region", PivotFieldType.Text, s => s.Region)
            {
                // East has no key; West and North are ranked.
                OrderKey = item => regionRank.TryGetValue((string)item, out var rank) ? rank : null,
            },
            new PivotField<Sale>("Product", PivotFieldType.Text, s => s.Product)
            {
                OrderKey = item => (string)item == "Plums" ? throw new InvalidOperationException("No rank for Plums.") : new Version(1, ((string)item).Length),
            },
            .. SalesData.Fields.Skip(2),
        ]);
        Pivot(source);
        var fields = Parsed(Assert.Single(_module.Invocations["attach"]).Arguments[2]).GetProperty("source").GetProperty("fields");
        Assert.Equal([true, true, false], fields.EnumerateArray().Take(3).Select(field => field.GetProperty("orderKey").GetBoolean()));

        var query = PivotJson.Write(new PivotQuery(rows: [new PivotQueryField("Region")], columns: [new PivotQueryField("Product")]));
        var (answered, answer) = KeysAndAnswer(await Bridge().Value.Aggregate(1, 1, query));

        Assert.False(PivotJson.ReadAnswer(answer).IsRefused);
        var keys = answered.GetProperty("orderKeys").EnumerateArray().ToDictionary(
            field => field.GetProperty("field").GetString()!,
            field => field.GetProperty("items").EnumerateArray().ToDictionary(item => item[0].GetProperty("value").GetString()!, item => item[1]));
        // The Blank Region is ordered by no key, and is not listed.
        Assert.Equal(["East", "North", "West"], keys["Region"].Keys.Order());
        Assert.Equal(JsonValueKind.Null, keys["Region"]["East"].ValueKind);
        Assert.Equal("1", keys["Region"]["West"].GetProperty("bigint").GetString());
        Assert.Equal("System.Int32", keys["Region"]["West"].GetProperty("type").GetString());
        // A key that throws is raised only if the browser orders by it; one the browser cannot order as .NET does is refused by name.
        Assert.Equal("No rank for Plums.", keys["Product"]["Plums"].GetProperty("threw").GetString());
        Assert.Contains("a key of type Version", keys["Product"]["Apples"].GetProperty("refused").GetString());
    }

    [Fact] // ADR-0412: a key crosses as a whole number that orders as .NET's CompareTo orders its type, exactly
    public async Task ADR0412_keys_of_each_ordered_type_cross_in_their_order()
    {
        object[] keys = [-1.5m, 0.1m, 2m, DateTime.MinValue.AddTicks(7), new DateOnly(2026, 10, 28), TimeSpan.FromTicks(-3), ulong.MaxValue, 'b',
            new Guid("00000000-0000-0000-0000-0000000000ff"), DayOfWeek.Friday, 0.1, true, "Wide"];
        var named = keys.Select((key, at) => (Name: $"K{at}", Key: key)).ToArray();
        var source = PivotSource.From(SalesData.Records, [
            .. named.Select(entry => new PivotField<Sale>(entry.Name, PivotFieldType.Text, s => s.Region) { OrderKey = _ => (IComparable)entry.Key }),
            .. SalesData.Fields.Skip(2),
        ]);
        Pivot(source);

        var query = PivotJson.Write(new PivotQuery(rows: [.. named.Select(entry => new PivotQueryField(entry.Name))]));
        var (answered, _) = KeysAndAnswer(await Bridge().Value.Aggregate(1, 1, query));

        var crossed = answered.GetProperty("orderKeys").EnumerateArray().Select(field => field.GetProperty("items")[0][1]).ToArray();
        Assert.Equal(["-15000000000000000000000000000", "1000000000000000000000000000", "20000000000000000000000000000", "7", "739916", "-3",
            "18446744073709551615", "98", "255", "5"], crossed.Take(10).Select(key => key.GetProperty("bigint").GetString()));
        Assert.Equal("0.1", crossed[10].GetProperty("number").GetString());
        Assert.True(crossed[11].GetProperty("boolean").GetBoolean());
        Assert.Equal("Wide", crossed[12].GetProperty("text").GetString());
    }
}
