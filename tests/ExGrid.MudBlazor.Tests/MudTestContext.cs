using Bunit;
using Microsoft.AspNetCore.Components;
using ExGrid.Columns;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace ExGrid.MudBlazor.Tests;

/// <summary>
/// MudBlazor's services, and a loose JavaScript seam: the grid's module import
/// answers null in loose mode, which the core treats as "no browser yet" and paints
/// without — enough for what these tests read, which is markup and render counts.
/// </summary>
public abstract class MudTestContext : BunitContext
{
    protected MudTestContext()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
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

    // A twelve-digit amount in a 112px column: fits at Roboto's 8.0px digit, hashes
    // at the system default's 9.058px (ADR-0016) — the observable that says whose
    // widths the grid is using.
    internal static GridColumn<Trade>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: new ColumnWidthSpec(ColumnWidth.Fixed(100)), editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: new ColumnWidthSpec(ColumnWidth.Fixed(112))),
    ];
}
