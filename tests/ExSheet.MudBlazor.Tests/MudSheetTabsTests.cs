using System.Globalization;
using AngleSharp.Dom;
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
/// The Sheet Tabs' operations under the MudBlazor Chrome (ADR-0331 §4; exsheet-workbook ticket 06): the
/// tabs' names, the <c>+</c> button and the tab's menu are drawn in MudBlazor's controls, and every press
/// and key means what it means under the built-in Chrome — ExSheet decides, the Chrome draws (the spine's
/// fourth rule).
/// </summary>
public class MudSheetTabsTests : MudSheetTestContext
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    private IRenderedComponent<ContainerFragment> RenderWorkbook(params string[] names)
    {
        var workbook = new Workbook(Culture, names);
        workbook.Do(names[0], SheetEdit.Enter(CellAddress.Parse("A1"), "21"));
        if (names.Length > 1) workbook.Do(names[1], SheetEdit.Enter(CellAddress.Parse("A1"), $"={names[0]}!A1*2"));
        var document = workbook.ToDocument();
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
            builder.AddComponentParameter(6, nameof(SheetComponent.Workbook), document);
            builder.CloseComponent();
        });
    }

    private static IReadOnlyList<string> TabNames(IRenderedComponent<ContainerFragment> page) =>
        [.. page.FindAll(".ex-sheet-tabs [role=tab]").Select(tab => tab.TextContent.Trim())];

    private static IElement Tab(IRenderedComponent<ContainerFragment> page, string name) =>
        page.FindAll("button.ex-sheet-tab").Single(tab => tab.TextContent.Trim() == name);

    private static string Notice(IRenderedComponent<ContainerFragment> page) => page.Find(".ex-sheet-notice").TextContent;

    private static IReadOnlyList<IElement> Commands(IRenderedComponent<ContainerFragment> page) =>
        page.FindAll(".ex-sheet-tab-menu [role=menuitem]");

    private static IElement Command(IRenderedComponent<ContainerFragment> page, string label) =>
        Commands(page).Single(c => c.TextContent.Trim() == label);

    private static Task MenuKeyAsync(IRenderedComponent<ContainerFragment> page, string key) =>
        page.Find(".ex-sheet-tab-menu").KeyDownAsync(new KeyboardEventArgs { Key = key });

    [Fact] // ADR-0331 §4: the tabs' names, + and the tab's menu are drawn in MudBlazor's controls
    public async Task The_tabs_the_plus_button_and_the_menu_are_drawn_in_mudblazors_controls()
    {
        var page = RenderWorkbook("One", "Two");

        Assert.Equal(2, page.FindAll("button.ex-sheet-tab .mud-typography.mud-ex-sheet-tab-face").Count);
        Assert.NotNull(page.Find(".ex-sheet-tab-add").QuerySelector("svg.mud-icon-root"));

        await Tab(page, "One").TriggerEventAsync("oncontextmenu", new MouseEventArgs { Button = 2 });

        var paper = page.Find(".ex-sheet-tab-menu .mud-paper.mud-ex-sheet-tab-menu");
        Assert.Equal(5, paper.QuerySelectorAll(".mud-ex-sheet-tab-command svg.mud-icon-root").Length);
        Assert.Equal(["Rename", "Delete", "Move Left", "Move Right", "Copy"], [.. Commands(page).Select(c => c.TextContent.Trim())]);
        // Move Left cannot be done on the first Sheet, and says why.
        Assert.Equal("true", Command(page, "Move Left").GetAttribute("aria-disabled"));
        Assert.Contains("mud-ex-sheet-tab-command-disabled", Command(page, "Move Left").ClassName);
        // The keyboard is on Rename, the first command, marked for the reader and named for aria-activedescendant.
        Assert.Contains("mud-ex-sheet-tab-command-active", Command(page, "Rename").ClassName);
        Assert.Equal(Command(page, "Rename").Id, page.Find(".ex-sheet-tab-menu").GetAttribute("aria-activedescendant"));
    }

    [Fact] // ADR-0331 §4: + adds a Sheet after the one shown under MudBlazor too, said in the notice, and one undo takes it out
    public async Task Plus_adds_a_sheet()
    {
        var page = RenderWorkbook("One", "Two");

        await page.Find(".ex-sheet-tab-add").ClickAsync(new MouseEventArgs());

        Assert.Equal(["One", "Sheet1", "Two"], TabNames(page));
        Assert.Equal("Sheet 'Sheet1' was added.", Notice(page));
        await page.InvokeAsync(() => Sheet(page).UndoAsync());
        Assert.Equal(["One", "Two"], TabNames(page));
    }

    [Fact] // ADR-0331 §4: a click on a MudBlazor-drawn command does the command: Move Right moves the tab
    public async Task A_click_on_a_command_does_it()
    {
        var page = RenderWorkbook("One", "Two");
        await Tab(page, "One").TriggerEventAsync("oncontextmenu", new MouseEventArgs { Button = 2 });

        await Command(page, "Move Right").ClickAsync(new MouseEventArgs());

        Assert.Equal(["Two", "One"], TabNames(page));
        Assert.Empty(page.FindAll(".ex-sheet-tab-menu"));
        Assert.Equal("Sheet 'One' was moved to position 2 of 2.", Notice(page));
    }

    [Fact] // ADR-0331 §4: the menu's keys are ExSheet's under MudBlazor too: ↓ to Delete and Enter deletes, and one undo gives it back
    public async Task The_menu_keys_delete_and_undo_gives_it_back()
    {
        var page = RenderWorkbook("One", "Two");
        await Tab(page, "One").TriggerEventAsync("oncontextmenu", new MouseEventArgs { Button = 2 });

        await MenuKeyAsync(page, "ArrowDown");
        Assert.Contains("mud-ex-sheet-tab-command-active", Command(page, "Delete").ClassName);
        await MenuKeyAsync(page, "Enter");

        Assert.Equal(["Two"], TabNames(page));
        Assert.Equal("Sheet 'One' was deleted. Ctrl+Z gives it back.", Notice(page));
        await page.InvokeAsync(() => Sheet(page).UndoAsync());
        Assert.Equal(["One", "Two"], TabNames(page));
        Assert.Equal("=One!A1*2", Sheet(page).ToWorkbookDocument().Sheets.Single(s => s.Name == "Two").Cells.Single(c => c.Address == CellAddress.Parse("A1")).Entry!.ToString());
    }

    [Fact] // ADR-0331 §4: Copy on the menu places Name (2) after the Sheet and shows it, under MudBlazor too
    public async Task Copy_places_the_copy_after_the_sheet()
    {
        var page = RenderWorkbook("One", "Two");
        await Tab(page, "One").TriggerEventAsync("oncontextmenu", new MouseEventArgs { Button = 2 });

        await Command(page, "Copy").ClickAsync(new MouseEventArgs());

        Assert.Equal(["One", "One (2)", "Two"], TabNames(page));
        Assert.Equal("One (2)", page.FindAll("button.ex-sheet-tab").Single(t => t.GetAttribute("aria-selected") == "true").TextContent.Trim());
    }

    [Fact] // ADR-0331 §4: a double click renames on the tab under MudBlazor too; Enter commits, and a name Excel refuses is refused by name
    public async Task A_double_click_renames_and_a_refused_name_is_refused_by_name()
    {
        var page = RenderWorkbook("One", "Two");

        await Tab(page, "One").DoubleClickAsync(new MouseEventArgs());
        await page.Find(".ex-sheet-tab-name").InputAsync(new ChangeEventArgs { Value = "Q1/Q2" });
        await page.Find(".ex-sheet-tab-name").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("true", page.Find(".ex-sheet-tab-name").GetAttribute("aria-invalid"));
        Assert.StartsWith("The Sheet was not renamed:", Notice(page));

        await page.Find(".ex-sheet-tab-name").InputAsync(new ChangeEventArgs { Value = "Costs" });
        await page.Find(".ex-sheet-tab-name").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(["Costs", "Two"], TabNames(page));
        Assert.Equal("Sheet 'One' was renamed 'Costs'.", Notice(page));
        Assert.Equal("=Costs!A1*2", Sheet(page).ToWorkbookDocument().Sheets.Single(s => s.Name == "Two").Cells.Single(c => c.Address == CellAddress.Parse("A1")).Entry!.ToString());
    }

    [Fact] // ADR-0331 §4: a drag moves a tab under MudBlazor too: the press on the tab's MudText face is the tab's
    public async Task A_drag_moves_a_tab()
    {
        var page = RenderWorkbook("One", "Two");

        await Tab(page, "One").MouseDownAsync(new MouseEventArgs { Button = 0 });
        await Tab(page, "Two").MouseUpAsync(new MouseEventArgs { Button = 0 });

        Assert.Equal(["Two", "One"], TabNames(page));
    }
}
