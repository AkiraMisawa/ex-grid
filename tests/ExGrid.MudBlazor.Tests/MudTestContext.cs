using Bunit;
using Microsoft.AspNetCore.Components;
using ExGrid.Columns;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace ExGrid.MudBlazor.Tests;

/// <summary>
/// MudBlazor's services, and a loose JavaScript seam: the Script Core's module answers the stamp this
/// build was made with (ADR-0123), and the grid attaches to a handle that answers nothing — enough for
/// what these tests read, which is the markup .NET draws and what it hands the browser at attach
/// (<see cref="Attached"/>). A module answering no stamp would be refused, and no grid made.
/// </summary>
public abstract class MudTestContext : BunitContext
{
    private readonly BunitJSModuleInterop _module;

    protected MudTestContext()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        _module = JSInterop.SetupModule(invocation => invocation.Identifier == "import");
        _module.Setup<string?>("stamp").SetResult(global::ExGrid.Script.ScriptCoreAsset.Stamp);
        var grid = _module.SetupModule("attach", _ => true);
        grid.SetupVoid("update", _ => true).SetVoidResult();
        grid.SetupVoid("state", _ => true).SetVoidResult();
        grid.SetupVoid("slotRendered", _ => true).SetVoidResult();
    }

    /// <summary>The options the last grid attached was made with, as the browser was handed them.</summary>
    protected System.Text.Json.JsonElement Attached()
    {
        using var document = System.Text.Json.JsonDocument.Parse(
            Xunit.Assert.IsType<string>(_module.Invocations["attach"].Last().Arguments[2]));
        return document.RootElement.GetProperty("options").Clone();
    }

    // An interactive renderer, as a Consumer's page has once connected; the grid reads it
    // to tell a Prerendered paint (ADR-0033). Set at the first render rather than in the
    // constructor: telling bUnit builds the renderer, which closes the service
    // collection a test may still add to.
    private bool _rendererInfoSet;

    private void EnsureRendererInfo()
    {
        if (_rendererInfoSet)
            return;
        _rendererInfoSet = true;
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    public new IRenderedComponent<TComponent> Render<TComponent>(
        Action<ComponentParameterCollectionBuilder<TComponent>>? parameterBuilder = null)
        where TComponent : IComponent
    {
        EnsureRendererInfo();
        return base.Render(parameterBuilder);
    }

    public new IRenderedComponent<Bunit.Rendering.ContainerFragment> Render(RenderFragment renderFragment)
    {
        EnsureRendererInfo();
        return base.Render(renderFragment);
    }

    internal sealed class Trade
    {
        public string Book = "";
        public decimal Amount;
    }

    internal static Trade[] Rows(int count)
    {
        var rows = new Trade[count];
        for (var i = 0; i < count; i++)
            rows[i] = new Trade { Book = $"Book {i:D3}", Amount = 123456789012m };
        return rows;
    }

    // A twelve-digit amount in a 116px column: fits at Roboto's 8.3px digit class, hashes
    // at the system default's 9.75px (ADR-0016) — the observable that says whose
    // widths the grid is using.
    internal static GridColumn<Trade>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: new ColumnWidthSpec(ColumnWidth.Fixed(100)), editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: new ColumnWidthSpec(ColumnWidth.Fixed(116))),
    ];
}
