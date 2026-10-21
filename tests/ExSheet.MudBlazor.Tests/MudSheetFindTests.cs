using System.Globalization;
using Bunit;
using Bunit.Rendering;
using ExGrid;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using MudProviders = global::MudBlazor;
using SheetComponent = ExSheet.Components.ExSheet;

namespace ExSheet.MudBlazor.Tests;

/// <summary>
/// A Sheet's find panel under the MudBlazor Chrome (ADR-0332 §5; find-and-replace ticket 04): ExGrid.MudBlazor's
/// panel with Within as a MudRadioGroup, and every step and write meaning what it means under the built-in
/// Chrome — ExSheet decides, the Chrome draws (the spine's fourth rule).
/// </summary>
public class MudSheetFindTests : MudSheetTestContext
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    private WorkbookDocument? _saved;

    private IRenderedComponent<ContainerFragment> RenderWorkbook()
    {
        var workbook = new Workbook(Culture, ["One", "Two"]);
        workbook.Do("One", SheetEdit.Enter(CellAddress.Parse("A1"), "apple"));
        workbook.Do("Two", SheetEdit.Enter(CellAddress.Parse("C3"), "apple pie"));
        _saved = workbook.ToDocument();
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        return Render(builder =>
        {
            builder.OpenComponent<MudProviders.MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<SheetComponent>(1);
            builder.AddComponentParameter(2, nameof(SheetComponent.Chrome), MudSheetChrome.Default);
            builder.AddComponentParameter(3, nameof(SheetComponent.ViewportHeight), (ViewportSize)400);
            builder.AddComponentParameter(4, nameof(SheetComponent.ViewportWidth), (ViewportSize)700);
            builder.AddComponentParameter(5, nameof(SheetComponent.Culture), Culture);
            builder.AddComponentParameter(6, nameof(SheetComponent.Workbook), _saved);
            builder.AddComponentParameter(7, nameof(SheetComponent.WorkbookChanged),
                EventCallback.Factory.Create<WorkbookDocument>(this, changed => _saved = changed));
            builder.CloseComponent();
        });
    }

    private string EntryOn(string sheet, string address)
        => global::ExSheet.Engine.Workbook.Open(_saved!).Find(sheet)!.GetEntryText(CellAddress.Parse(address));

    private static string ShownSheet(IRenderedComponent<ContainerFragment> page) =>
        page.FindAll(".ex-sheet-tab").Single(tab => tab.GetAttribute("aria-selected") == "true").TextContent.Trim();

    private static string NameBox(IRenderedComponent<ContainerFragment> page) =>
        page.Find(".mud-ex-name-box").GetAttribute("value") ?? "";

    private static async Task WithinWorkbookAsync(IRenderedComponent<ContainerFragment> page)
        => await page.Find(".mud-ex-sheet-find-within-workbook input").ClickAsync(new MouseEventArgs());

    private static async Task TypeAsync(IRenderedComponent<ContainerFragment> page, string field, string text)
        => await page.Find($".{field} input").InputAsync(new ChangeEventArgs { Value = text });

    [Fact] // ADR-0332 §5: the Sheet's panel is drawn in MudBlazor's controls, Within a MudRadioGroup with the Sheet chosen
    public async Task ADR0332_the_panel_is_drawn_in_mudblazors_controls_with_within()
    {
        var page = RenderWorkbook();

        await PressAsync(page, "h", ctrl: true);

        var panel = page.Find(".ex-popover-find form.mud-ex-sheet-find");
        Assert.NotNull(panel.QuerySelector(".mud-ex-grid-find-field.mud-input-control"));
        Assert.NotNull(panel.QuerySelector(".mud-ex-grid-find-replace-field.mud-input-control"));
        Assert.NotNull(panel.QuerySelector(".mud-ex-sheet-find-within .mud-radio-group"));
        Assert.Contains("Within", panel.QuerySelector(".mud-ex-sheet-find-within")!.TextContent);
        Assert.Contains("Sheet", panel.QuerySelector(".mud-ex-sheet-find-within-sheet")!.TextContent);
        Assert.Contains("Workbook", panel.QuerySelector(".mud-ex-sheet-find-within-workbook")!.TextContent);
    }

    [Fact] // ADR-0332 §5: within the Workbook, a step shows the Sheet of the match, and Replace All writes every Sheet and says the count, as under the built-in Chrome
    public async Task ADR0332_within_the_workbook_steps_and_replaces_as_the_built_in_panel_does()
    {
        var page = RenderWorkbook();
        await GoToAsync(page, "A1");
        await PressAsync(page, "h", ctrl: true);
        await WithinWorkbookAsync(page);
        await TypeAsync(page, "mud-ex-grid-find-field", "apple");

        await page.Find("button.mud-ex-grid-find-next").ClickAsync(new MouseEventArgs());
        Assert.Equal(("Two", "C3"), (ShownSheet(page), NameBox(page)));

        await TypeAsync(page, "mud-ex-grid-find-replace-field", "pear");
        await page.Find("button.mud-ex-grid-find-replace-all").ClickAsync(new MouseEventArgs());

        Assert.Equal("pear", EntryOn("One", "A1"));
        Assert.Equal("pear pie", EntryOn("Two", "C3"));
        Assert.Contains("Replaced 2 cells.", page.Find(".mud-ex-grid-find-outcome").TextContent);
        Assert.True(await page.InvokeAsync(() => page.FindComponent<SheetComponent>().Instance.UndoAsync()));
        Assert.Equal("apple pie", EntryOn("Two", "C3"));
    }
}
