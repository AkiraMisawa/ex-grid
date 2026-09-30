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
using PivotComponent = ExPivot.Components.ExPivot<ExPivot.Components.Tests.Support.Sale>;

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
    private const string ModulePath = "./_content/ExGrid/ex-grid.js";
    private bool _rendererInfoSet;

    protected PivotTestContext()
    {
        Services.AddSingleton<TimeProvider>(Clock);
        var module = JSInterop.SetupModule(ModulePath);
        var handle = module.SetupModule("attach", _ => true);
        handle.Setup<bool>("metaIsPrimary").SetResult(false);
        handle.Setup<ScrollOffset>("getScrollOffset").SetResult(default);
        handle.Setup<bool>("anchorScrollTop", _ => true).SetResult(true);
        foreach (var name in new[] { "setScrollOffset", "blur", "setEditing", "setInnerPopup", "setClaims", "setCaret", "setPointerReporting", "forgetPointer", "writeCopy", "reclaimFocus", "focusEditor", "dispose" })
            handle.SetupVoid(name, _ => true).SetVoidResult();
        // Blazor's own FocusAsync, which the built-in views use to hand the keyboard on.
        JSInterop.SetupVoid("Blazor._internal.domWrapper.focus", _ => true).SetVoidResult();
    }

    internal FakeTimeProvider Clock { get; } = new();

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

    public static PivotFieldPlacement P(string field) => new(field);

    public static PivotValueField Sum(string field) => new(field, PivotAggregation.Sum);

    /// <summary>Renders an ExPivot as a connected, interactive component, in en-US.</summary>
    internal IRenderedComponent<PivotComponent> RenderPivot(
        PivotLayout? layout = null,
        Action<ComponentParameterCollectionBuilder<PivotComponent>>? parameters = null,
        IReadOnlyList<Sale>? records = null)
    {
        Interactive();
        return Render<PivotComponent>(ps =>
        {
            ps.Add(p => p.Records, records ?? Sales)
              .Add(p => p.Fields, Fields)
              .Add(p => p.Culture, CultureInfo.GetCultureInfo("en-US"))
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

    internal static IRenderedComponent<ExGrid<PivotReportRow>> Grid(IRenderedComponent<PivotComponent> cut)
        => cut.FindComponent<ExGrid<PivotReportRow>>();

    /// <summary>Each painted row's cells as text, label cells first, joined with " | ".</summary>
    internal static string[] RowTexts(IRenderedComponent<PivotComponent> cut)
        => cut.FindAll(".ex-viewport .ex-row")
            .Select(row => string.Join(" | ", row.QuerySelectorAll("[role=gridcell]").Select(c => c.TextContent.Trim())).TrimEnd())
            .ToArray();

    /// <summary>The leaf headers as painted.</summary>
    internal static string[] HeaderTexts(IRenderedComponent<PivotComponent> cut)
        => cut.FindAll(".ex-header [role=columnheader]").Select(h => h.TextContent.Trim()).ToArray();

    /// <summary>The Field List's entry buttons of one Area, as captioned.</summary>
    internal static string[] AreaEntries(IRenderedComponent<PivotComponent> cut, string area)
        => AreaElement(cut, area).QuerySelectorAll(".ex-pivot-entry-caption").Select(e => e.TextContent.Trim()).ToArray();

    internal static IElement AreaElement(IRenderedComponent<PivotComponent> cut, string area)
        => cut.FindAll(".ex-pivot-area").Single(a => a.QuerySelector(".ex-pivot-area-title")!.TextContent.Trim() == area);

    /// <summary>The field's row in the list of fields.</summary>
    internal static IElement FieldItem(IRenderedComponent<PivotComponent> cut, string caption)
        => cut.FindAll(".ex-pivot-field").Single(f => f.QuerySelector(".ex-pivot-field-caption")!.TextContent.Trim() == caption);

    /// <summary>Opens the menu of the Area's entry captioned <paramref name="caption"/>.</summary>
    internal static Task OpenMenuAsync(IRenderedComponent<PivotComponent> cut, string area, string caption)
        => AreaElement(cut, area).QuerySelectorAll(".ex-pivot-entry-button")
            .Single(b => b.QuerySelector(".ex-pivot-entry-caption")!.TextContent.Trim() == caption)
            .ClickAsync(new MouseEventArgs());

    /// <summary>Runs the open menu's command called <paramref name="label"/>.</summary>
    internal static Task RunMenuAsync(IRenderedComponent<PivotComponent> cut, string label)
        => cut.FindAll(".ex-pivot-menu-item").Single(b => b.TextContent.Trim() == label).ClickAsync(new MouseEventArgs());

    /// <summary>The Context Menu's commands the grid would show on (row, column).</summary>
    internal static IReadOnlyList<GridCommand> ContextCommands(IRenderedComponent<PivotComponent> cut, int row, string column)
    {
        var grid = Grid(cut).Instance;
        var context = new ContextMenuContext<PivotReportRow>(
            grid.Window[row], column, ColumnType.Number, [new SelectionRange(row, 0, 1, 1)], grid.RowSequenceVersion, [], () => { });
        return grid.ContextCommands!(context).ToArray();
    }
}
