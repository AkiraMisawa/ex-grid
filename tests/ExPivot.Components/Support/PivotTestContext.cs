using System.Globalization;
using AngleSharp.Dom;
using Bunit;
using ExGrid;
using ExGrid.Chrome;
using ExGrid.Components;
using ExGrid.Selection;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests.Support;

/// <summary>A sale, the Source Record every test pivots.</summary>
public sealed record Sale(string? Region, string Product, decimal Amount, int Quantity, bool Online);

/// <summary>
/// What every ExPivot test needs: ExGrid's JavaScript module stood in for, strictly — the day
/// ExPivot or the grid reaches for JavaScript somewhere new, a test says so (ADR-0021) — the clock
/// the grid's settle delay runs on, the data, and the ways a user reaches the pivot.
/// </summary>
public abstract class PivotTestContext : BunitContext
{
    private const string ModulePath = "./_content/ExGrid/ex-grid.min.js";

    /// <summary>ExGrid's settle delay: once a scroll has been still this long, the grid paints the
    /// rows it landed on and asks for them (ADR-0004).</summary>
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(150);

    private readonly BunitJSModuleInterop _module;
    // Where each handle says its scroller stands, as the browser would (ScrollReportAsync).
    private readonly List<JSRuntimeInvocationHandler<ScrollOffset>> _scrollOffsets = [];
    private bool _rendererInfoSet;

    protected PivotTestContext()
    {
        Services.AddSingleton<TimeProvider>(Clock);
        _module = JSInterop.SetupModule(ModulePath);
        StandIn(_module.SetupModule("attach", _ => true));
        // Blazor's own FocusAsync, which the built-in views use to hand the keyboard on.
        JSInterop.SetupVoid("Blazor._internal.domWrapper.focus", _ => true).SetVoidResult();
    }

    // Every call a grid makes to the handle its listener's attach gave it, answered as the
    // browser would answer it.
    private void StandIn(BunitJSModuleInterop handle)
    {
        handle.Setup<bool>("metaIsPrimary").SetResult(false);
        var offset = handle.Setup<ScrollOffset>("getScrollOffset");
        offset.SetResult(default);
        _scrollOffsets.Add(offset);
        handle.Setup<bool>("anchorScrollTop", _ => true).SetResult(true);
        foreach (var name in new[] { "setScrollOffset", "releaseTab", "setEditing", "setInnerPopup", "setClaims", "setCaret", "setPointerReporting", "forgetPointer", "writeCopy", "reclaimFocus", "focusEditor", "handKeyboardTo", "dispose" })
            handle.SetupVoid(name, _ => true).SetVoidResult();
    }

    /// <summary>The handle of every report grid attached from here on, kept apart from the details
    /// grids' handles: what the report's own grid asks of the browser — the keyboard back on its
    /// root among it (ADR-0070). Called before the pivot is rendered.</summary>
    internal BunitJSModuleInterop ReportGridHandle()
    {
        var handle = _module.SetupModule("attach",
            invocation => invocation.Arguments[2] is Microsoft.JSInterop.DotNetObjectReference<ExGrid<PivotDisplayRow>>);
        StandIn(handle);
        return handle;
    }

    /// <summary>How many times the report grid behind <paramref name="handle"/> has asked for the
    /// keyboard back on its root, from nothing or from inside it (ADR-0021/0070).</summary>
    internal static int KeyboardReturns(BunitJSModuleInterop handle)
        => handle.Invocations.Count(invocation => invocation.Identifier == "reclaimFocus");

    /// <summary>The elements the report grid behind <paramref name="handle"/> has handed the
    /// keyboard on to, by id, in the order asked: a control of ExPivot's own takes it from the
    /// report's grid only while it is still the report's (ExGrid's <c>HandKeyboardToAsync</c>,
    /// ADR-0070's note of 2026-10-02).</summary>
    internal static string[] KeyboardHandOffs(BunitJSModuleInterop handle)
        => handle.Invocations.Where(invocation => invocation.Identifier == "handKeyboardTo")
            .Select(invocation => ((ElementReference)invocation.Arguments[0]!).Id).ToArray();

    /// <summary>The id Blazor gives the element a component holds a reference to — what a focus
    /// request names.</summary>
    internal static string ElementIdOf(IComponent component)
    {
        for (var type = component.GetType(); type is not null; type = type.BaseType)
        {
            var field = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                .FirstOrDefault(f => f.FieldType == typeof(ElementReference));
            if (field is not null)
                return ((ElementReference)field.GetValue(component)!).Id;
        }
        throw new InvalidOperationException($"{component.GetType().Name} holds no element reference");
    }

    internal FakeTimeProvider Clock { get; } = new();

    /// <summary>
    /// Scrolls the report grid as a user does: its scroller stands <paramref name="topPx"/> down and
    /// says so, and the grid's settle delay passes on the test's clock, after which the grid paints
    /// the rows it landed on and asks for them (ADR-0004, ADR-0001). Every grid's handle answers the
    /// same offset, so the pivot is rendered without a details grid open.
    /// </summary>
    internal async Task ScrollReportAsync(IRenderedComponent<PivotComponent> cut, double topPx)
    {
        foreach (var offset in _scrollOffsets)
            offset.SetResult(new ScrollOffset(topPx, 0));
        await cut.InvokeAsync(() => cut.Find(".ex-pivot-sheet > .ex-grid > .ex-scroller").ScrollAsync(EventArgs.Empty));
        Clock.Advance(SettleDelay);
    }

    //  Region  Product  Amount  Quantity  Online
    //  East    Apples   100     10        TRUE
    //  East    Pears    50      5         FALSE
    //  East    Apples   30      3         TRUE
    //  West    Apples   70      7         FALSE
    //  West    Plums    20      2         TRUE
    //  North   Pears    10      1         FALSE
    //  (blank) Plums    5       1         TRUE
    public static readonly Sale[] Sales =
    [
        new("East", "Apples", 100m, 10, true),
        new("East", "Pears", 50m, 5, false),
        new("East", "Apples", 30m, 3, true),
        new("West", "Apples", 70m, 7, false),
        new("West", "Plums", 20m, 2, true),
        new("North", "Pears", 10m, 1, false),
        new(null, "Plums", 5m, 1, true),
    ];

    public static readonly PivotField<Sale>[] Fields =
    [
        new("Region", PivotFieldType.Text, s => s.Region),
        new("Product", PivotFieldType.Text, s => s.Product),
        new("Amount", PivotFieldType.Number, s => s.Amount),
        new("Quantity", PivotFieldType.Number, s => s.Quantity),
        new("Online", PivotFieldType.Boolean, s => s.Online),
    ];

    /// <summary>The bundled source over the sales — the reference, answering in the process.</summary>
    public static PivotSource Bundled(IReadOnlyList<Sale>? records = null) => PivotSource.From(records ?? Sales, Fields);

    public static PivotFieldPlacement P(string field) => new(field);

    public static PivotValueField Sum(string field) => new(field, PivotAggregation.Sum);

    /// <summary>Renders an ExPivot as a connected, interactive component, in en-US, over
    /// <paramref name="source"/> — the bundled source over <paramref name="records"/> unless told
    /// otherwise — or, when <paramref name="reportSource"/> is given, asking that report source
    /// instead.</summary>
    internal IRenderedComponent<PivotComponent> RenderPivot(
        PivotLayout? layout = null,
        Action<ComponentParameterCollectionBuilder<PivotComponent>>? parameters = null,
        IReadOnlyList<Sale>? records = null,
        PivotSource? source = null,
        PivotReportSource? reportSource = null)
    {
        Interactive();
        return Render<PivotComponent>(ps =>
        {
            if (reportSource is not null)
                ps.Add(p => p.ReportSource, reportSource);
            else
                ps.Add(p => p.Source, source ?? Bundled(records));
            ps.Add(p => p.Culture, CultureInfo.GetCultureInfo("en-US"))
              .Add(p => p.ViewportHeight, (ViewportSize)400)
              .Add(p => p.ViewportWidth, (ViewportSize)700);
            if (layout is not null)
                ps.Add(p => p.Layout, layout);
            parameters?.Invoke(ps);
        });
    }

    /// <summary>Renders a page of the tests' own, connected and interactive.</summary>
    internal IRenderedComponent<TPage> RenderPage<TPage>() where TPage : IComponent
    {
        Interactive();
        return Render<TPage>();
    }

    private void Interactive()
    {
        if (_rendererInfoSet)
            return;
        _rendererInfoSet = true;
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    internal static IRenderedComponent<ExGrid<PivotDisplayRow>> Grid(IRenderedComponent<PivotComponent> cut)
        => cut.FindComponent<ExGrid<PivotDisplayRow>>();

    /// <summary>Each painted row's cells as text, label cells first, joined with " | ". Read on the
    /// renderer so parsing the DOM cannot race an asynchronous report's markup update.</summary>
    internal static string[] RowTexts(IRenderedComponent<PivotComponent> cut)
        => cut.InvokeAsync(() => RowTextsOf(cut.FindAll(".ex-pivot-sheet > .ex-grid .ex-viewport .ex-row"))).GetAwaiter().GetResult();

    /// <summary>Each painted row's cells as text, joined with " | ".</summary>
    internal static string[] RowTextsOf(IEnumerable<IElement> rows)
        => rows.Select(row => string.Join(" | ", row.QuerySelectorAll("[role=gridcell]").Select(c => c.TextContent.Trim())).TrimEnd()).ToArray();

    /// <summary>The leaf headers as painted.</summary>
    internal static string[] HeaderTexts(IRenderedComponent<PivotComponent> cut)
        => Grid(cut).FindAll(".ex-header [role=columnheader]").Select(h => h.TextContent.Trim()).ToArray();

    /// <summary>The Field List's entry buttons of one Area, as captioned.</summary>
    internal static string[] AreaEntries(IRenderedComponent<PivotComponent> cut, string area)
        => AreaElement(cut, area).QuerySelectorAll(".ex-pivot-entry-caption").Select(e => e.TextContent.Trim()).ToArray();

    internal static IElement AreaElement(IRenderedComponent<PivotComponent> cut, string area)
        => cut.FindAll(".ex-pivot-area").Single(a => a.QuerySelector(".ex-pivot-area-title")!.TextContent.Trim() == area);

    /// <summary>The field's row in the list of fields.</summary>
    internal static IElement FieldItem(IRenderedComponent<PivotComponent> cut, string caption)
        => cut.FindAll(".ex-pivot-field").Single(f => f.QuerySelector(".ex-pivot-field-caption")!.TextContent.Trim() == caption);

    /// <summary>Ticks or unticks a field in the list of fields.</summary>
    internal static Task TickFieldAsync(IRenderedComponent<PivotComponent> cut, string caption, bool tick)
        => cut.InvokeAsync(() => FieldItem(cut, caption).QuerySelector("input")!.ChangeAsync(new ChangeEventArgs { Value = tick }));

    /// <summary>Opens the menu of the Area's entry captioned <paramref name="caption"/>. Lookup and
    /// dispatch share the renderer's turn so a concurrent render cannot retire the event handler.</summary>
    internal static Task OpenMenuAsync(IRenderedComponent<PivotComponent> cut, string area, string caption)
        => cut.InvokeAsync(() => AreaElement(cut, area).QuerySelectorAll(".ex-pivot-entry-button")
            .Single(b => b.QuerySelector(".ex-pivot-entry-caption")!.TextContent.Trim() == caption)
            .ClickAsync(new MouseEventArgs()));

    /// <summary>Runs the open menu's command called <paramref name="label"/>.</summary>
    internal static Task RunMenuAsync(IRenderedComponent<PivotComponent> cut, string label)
        => cut.InvokeAsync(() => MenuItem(cut, label).ClickAsync(new MouseEventArgs()));

    /// <summary>The open menu's command called <paramref name="label"/>, a choice's mark aside.</summary>
    internal static IElement MenuItem(IRenderedComponent<PivotComponent> cut, string label)
        => cut.FindAll(".ex-pivot-menu-item").Single(b => MenuLabel(b) == label);

    /// <summary>A menu item's label, a choice's mark aside.</summary>
    internal static string MenuLabel(IElement item) => item.TextContent.Replace("✓", "", StringComparison.Ordinal).Trim();

    /// <summary>The Context Menu's commands the grid would show on (row, column).</summary>
    internal static IReadOnlyList<GridCommand> ContextCommands(IRenderedComponent<PivotComponent> cut, int row, string column)
    {
        var grid = Grid(cut).Instance;
        var context = new ContextMenuContext<PivotDisplayRow>(
            grid.Window[row], column, ColumnType.Number, [new SelectionRange(row, 0, 1, 1)], grid.RowSequenceVersion, [], () => { });
        return grid.ContextCommands!(context).ToArray();
    }
}
