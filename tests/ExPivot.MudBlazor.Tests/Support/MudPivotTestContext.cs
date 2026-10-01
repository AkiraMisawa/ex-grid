using System.Globalization;
using System.Reflection;
using AngleSharp.Dom;
using Bunit;
using ExGrid;
using ExGrid.Chrome;
using ExGrid.Components;
using ExGrid.MudBlazor;
using ExGrid.Selection;
using ExPivot.Chrome;
using ExPivot.Components;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using MudBlazor.Services;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.MudBlazor.Tests.Support;

/// <summary>A sale, the Source Record every test pivots.</summary>
public sealed record Sale(string? Region, string Product, decimal Amount, int Quantity, bool Online);

/// <summary>
/// MudBlazor's services, the popover provider every MudBlazor page has, and a loose JavaScript
/// seam: the grid's module import answers null in loose mode, which the core treats as "no browser
/// yet" and paints without — enough for what these tests read, which is markup, layouts and the
/// focus calls Blazor itself makes. The pivot stands in the grid Wrapper's paper, as ADR-0061 has
/// a Consumer write it.
/// </summary>
public abstract class MudPivotTestContext : BunitContext
{
    internal const string Focus = "Blazor._internal.domWrapper.focus";

    private bool _rendererInfoSet;
    private IRenderedComponent<MudPopoverProvider>? _provider;

    protected MudPivotTestContext()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
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

    public static PivotFieldPlacement P(string field) => new(field);

    public static PivotValueField Sum(string field) => new(field, PivotAggregation.Sum);

    /// <summary>Renders an ExPivot in a <c>MudExGridPaper</c>, connected and interactive, in
    /// en-US, dressed by <paramref name="chrome"/> — the Mud Chrome unless told otherwise; pass
    /// <see cref="BuiltIn"/> for ExPivot's own markup.</summary>
    internal IRenderedComponent<PivotComponent> RenderPivot(
        PivotLayout? layout = null,
        IPivotChrome? chrome = null,
        Func<string, string?>? label = null,
        EventCallback<PivotDetails>? showDetails = null,
        PivotSource? source = null,
        PivotDetailsView? detailsView = null)
    {
        if (!_rendererInfoSet)
        {
            _rendererInfoSet = true;
            SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        }
        _provider ??= Render<MudPopoverProvider>();
        var pivotChrome = ReferenceEquals(chrome, BuiltIn) ? null : chrome ?? MudPivotChrome.Default;
        var host = Render(builder =>
        {
            builder.OpenComponent<MudExGridPaper>(0);
            builder.AddComponentParameter(1, nameof(MudExGridPaper.ChildContent), (RenderFragment)(inner =>
            {
                inner.OpenComponent<PivotComponent>(0);
                inner.AddComponentParameter(1, nameof(PivotComponent.Source), source ?? PivotSource.From(Sales, Fields));
                inner.AddComponentParameter(3, nameof(PivotComponent.Culture), CultureInfo.GetCultureInfo("en-US"));
                inner.AddComponentParameter(4, nameof(PivotComponent.ViewportHeight), (ViewportSize)400);
                inner.AddComponentParameter(5, nameof(PivotComponent.ViewportWidth), (ViewportSize)700);
                if (pivotChrome is not null)
                    inner.AddComponentParameter(6, nameof(PivotComponent.PivotChrome), pivotChrome);
                if (layout is not null)
                    inner.AddComponentParameter(7, nameof(PivotComponent.Layout), layout);
                if (label is not null)
                    inner.AddComponentParameter(8, nameof(PivotComponent.Label), label);
                if (showDetails is { } details)
                    inner.AddComponentParameter(9, nameof(PivotComponent.OnShowDetails), details);
                if (detailsView is { } view)
                    inner.AddComponentParameter(10, nameof(PivotComponent.DetailsView), view);
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        });
        return host.FindComponent<PivotComponent>();
    }

    /// <summary>Asks <see cref="RenderPivot"/> for ExPivot's built-in markup.</summary>
    internal static readonly IPivotChrome BuiltIn = new BuiltInMarker();

    private sealed class BuiltInMarker : IPivotChrome;

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

    /// <summary>One Area of the Mud pane, by its title.</summary>
    internal static IElement Area(IRenderedComponent<PivotComponent> cut, string title)
        => cut.FindAll(".mud-ex-pivot-area").Single(a => a.QuerySelector(".mud-ex-pivot-area-name")!.TextContent.Trim() == title);

    /// <summary>The entries of one Area of the Mud pane, as captioned.</summary>
    internal static string[] Entries(IRenderedComponent<PivotComponent> cut, string title)
        => Area(cut, title).QuerySelectorAll(".mud-ex-pivot-entry-caption").Select(e => e.TextContent.Trim()).ToArray();

    /// <summary>A field's row in the Mud pane's list of fields.</summary>
    internal static IElement Field(IRenderedComponent<PivotComponent> cut, string caption)
        => cut.FindAll(".mud-ex-pivot-field").Single(f => f.TextContent.Trim() == caption);

    /// <summary>Ticks or unticks a field in the Mud pane.</summary>
    internal static Task TickFieldAsync(IRenderedComponent<PivotComponent> cut, string caption, bool tick)
        => Field(cut, caption).QuerySelector("input[type=checkbox]")!.ChangeAsync(new ChangeEventArgs { Value = tick });

    /// <summary>An entry's MudButton in the Mud pane.</summary>
    internal static IElement EntryButton(IRenderedComponent<PivotComponent> cut, string area, string caption)
        => Area(cut, area).QuerySelectorAll(".mud-ex-pivot-entry-button")
            .Single(b => b.QuerySelector(".mud-ex-pivot-entry-caption")!.TextContent.Trim() == caption);

    /// <summary>Opens the menu of an entry in the Mud pane.</summary>
    internal static Task OpenMenuAsync(IRenderedComponent<PivotComponent> cut, string area, string caption)
        => EntryButton(cut, area, caption).ClickAsync(new MouseEventArgs());

    /// <summary>The open menu's commands, as the Mud Chrome draws them.</summary>
    internal static IReadOnlyList<IElement> MenuItems(IRenderedComponent<PivotComponent> cut)
        => cut.FindAll(".mud-ex-pivot-menu .mud-ex-pivot-menu-item");

    /// <summary>Runs the open menu's command called <paramref name="label"/>.</summary>
    internal static Task RunMenuAsync(IRenderedComponent<PivotComponent> cut, string label)
        => MenuItems(cut).Single(b => b.TextContent.Trim() == label).ClickAsync(new MouseEventArgs());

    /// <summary>Ticks or unticks an Item in the Mud Filter….</summary>
    internal static Task TickItemAsync(IRenderedComponent<PivotComponent> cut, string label, bool tick)
        => cut.FindAll(".mud-ex-pivot-item-filter .mud-checkbox")
            .Single(c => c.TextContent.Trim() == label)
            .QuerySelector("input[type=checkbox]")!
            .ChangeAsync(new ChangeEventArgs { Value = tick });

    /// <summary>The Context Menu's commands the grid would show on (row, column).</summary>
    internal static IReadOnlyList<GridCommand> ContextCommands(IRenderedComponent<PivotComponent> cut, int row, string column)
        => Grid(cut).Instance.ContextCommands!(ContextAt(cut, row, column)).ToArray();

    internal static ContextMenuContext<PivotReportRow> ContextAt(IRenderedComponent<PivotComponent> cut, int row, string column)
    {
        var grid = Grid(cut).Instance;
        return new ContextMenuContext<PivotReportRow>(
            grid.Window[row], column, ColumnType.Number, [new SelectionRange(row, 0, 1, 1)], grid.RowSequenceVersion,
            grid.ContextCommands!(new ContextMenuContext<PivotReportRow>(
                grid.Window[row], column, ColumnType.Number, [new SelectionRange(row, 0, 1, 1)], grid.RowSequenceVersion, [], () => { }))
                .ToArray(),
            () => { });
    }

    /// <summary>The id Blazor gives the element a MudButton renders — what a focus call names.</summary>
    internal static string ElementIdOf(global::MudBlazor.MudButton button)
    {
        for (var type = button.GetType(); type is not null; type = type.BaseType)
        {
            var field = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .FirstOrDefault(f => f.FieldType == typeof(ElementReference));
            if (field is not null)
                return ((ElementReference)field.GetValue(button)!).Id;
        }
        throw new InvalidOperationException("MudButton holds no element reference");
    }

    /// <summary>The last focus call Blazor made: the element's id and whether it kept the scroll.</summary>
    internal (string Id, bool PreventScroll)? LastFocus()
    {
        var call = JSInterop.Invocations.LastOrDefault(i => i.Identifier == Focus);
        return call.Identifier is null ? null : (((ElementReference)call.Arguments[0]!).Id, call.Arguments[1] is true);
    }
}
