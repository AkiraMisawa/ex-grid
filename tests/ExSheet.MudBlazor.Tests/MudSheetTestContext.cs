using System.Globalization;
using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using ExGrid;
using ExGrid.Components;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using MudBlazor.Services;
using Xunit;
using MudProviders = global::MudBlazor;
using SheetComponent = ExSheet.Components.ExSheet;

namespace ExSheet.MudBlazor.Tests;

/// <summary>
/// A Consumer's MudBlazor page in small: MudBlazor's services, its popover and dialog providers,
/// and an ExSheet handed <see cref="MudSheetChrome"/>. ExGrid's module is stood in for as ExSheet's
/// own tests stand it in, so that the core's focus function is counted (ADR-0021's note of
/// 2026-09-30); every other call — MudBlazor's own script — answers loosely, as in ExGrid.MudBlazor's
/// tests: MudBlazor's script is MudBlazor's, and what these tests read is markup and calls.
/// </summary>
public abstract class MudSheetTestContext : BunitContext
{
    private const string ModulePath = "./_content/ExGrid/ex-grid.js";

    private readonly BunitJSModuleInterop _handle;
    private bool _rendererInfoSet;

    protected MudSheetTestContext()
    {
        Services.AddMudServices();
        Services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        JSInterop.Mode = JSRuntimeMode.Loose;
        var module = JSInterop.SetupModule(ModulePath);
        _handle = module.SetupModule("attach", _ => true);
        _handle.Setup<bool>("metaIsPrimary").SetResult(false);
        _handle.Setup<ScrollOffset>("getScrollOffset").SetResult(default);
        _handle.Setup<bool>("anchorScrollTop", _ => true).SetResult(true);
        foreach (var name in new[] { "setScrollOffset", "blur", "setEditing", "setInnerPopup", "setClaims", "setCaret", "setPointerReporting", "forgetPointer", "writeCopy", "reclaimFocus", "focusEditor", "handOff", "dispose" })
        {
            _handle.SetupVoid(name, _ => true).SetVoidResult();
        }
    }

    /// <summary>How many times the grid has been asked to take the keyboard back (ADR-0021's note of 2026-09-30).</summary>
    protected int ReclaimCount => _handle.Invocations["reclaimFocus"].Count;

    /// <summary>Where the grid told its key gate the keyboard is going, in order (ADR-0050 item 16, 2026-10-01).</summary>
    protected IReadOnlyList<string> HandOffs => [.. _handle.Invocations["handOff"].Select(i => (string)i.Arguments[0]!)];

    /// <summary>A Sheet of en-US whose cells are typed as given.</summary>
    protected static SheetDocument DocumentOf(params (string Address, string Typed)[] cells) => DocumentOf(null, cells);

    /// <summary>A Sheet of en-US whose cells are typed as given, then prepared.</summary>
    protected static SheetDocument DocumentOf(Action<Sheet>? prepare, params (string Address, string Typed)[] cells)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        foreach (var (address, typed) in cells) sheet.Enter(CellAddress.Parse(address), typed);
        prepare?.Invoke(sheet);
        return sheet.ToDocument();
    }

    /// <summary>
    /// The page: MudBlazor's popover and dialog providers, and an ExSheet under
    /// <paramref name="chrome"/> (<see cref="MudSheetChrome.Default"/> when none is given).
    /// </summary>
    protected IRenderedComponent<ContainerFragment> RenderPage(
        SheetDocument? document = null, ISheetChrome? chrome = null, Action<SheetDocument>? documentChanged = null, bool dialogs = true)
    {
        if (!_rendererInfoSet)
        {
            _rendererInfoSet = true;
            SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        }
        return Render(builder =>
        {
            builder.OpenComponent<MudProviders.MudPopoverProvider>(0);
            builder.CloseComponent();
            if (dialogs)
            {
                builder.OpenComponent<MudProviders.MudDialogProvider>(1);
                builder.CloseComponent();
            }
            builder.OpenComponent<SheetComponent>(2);
            builder.AddComponentParameter(3, nameof(SheetComponent.Chrome), chrome ?? MudSheetChrome.Default);
            builder.AddComponentParameter(4, nameof(SheetComponent.ViewportHeight), (ViewportSize)400);
            builder.AddComponentParameter(5, nameof(SheetComponent.ViewportWidth), (ViewportSize)700);
            builder.AddComponentParameter(6, nameof(SheetComponent.Culture), CultureInfo.GetCultureInfo("en-US"));
            if (document is not null) builder.AddComponentParameter(7, nameof(SheetComponent.Document), document);
            if (documentChanged is not null)
                builder.AddComponentParameter(8, nameof(SheetComponent.DocumentChanged), EventCallback.Factory.Create(this, documentChanged));
            builder.CloseComponent();
        });
    }

    /// <summary>The Sheet on the page.</summary>
    protected static SheetComponent Sheet(IRenderedComponent<ContainerFragment> page) => page.FindComponent<SheetComponent>().Instance;

    /// <summary>Types an address into the Name Box — ExGrid.MudBlazor's — and presses Enter (ADR-0051).</summary>
    protected static async Task GoToAsync(IRenderedComponent<ContainerFragment> page, string address)
    {
        await page.Find(".mud-ex-name-box").InputAsync(new ChangeEventArgs { Value = address });
        await page.Find(".ex-name-box-form").SubmitAsync();
    }

    /// <summary>A key pressed on the grid's root, as the capture listener forwards it.</summary>
    protected static Task PressAsync(IRenderedComponent<ContainerFragment> page, string key, bool ctrl = false)
    {
        var grid = page.FindComponent<ExGrid<SheetRow>>();
        return grid.InvokeAsync(() => grid.Instance.OnKeyAsync(key, ctrl, false, false, false, false));
    }

    /// <summary>The Cell Format of the cell at <paramref name="address"/>.</summary>
    protected static CellFormat FormatAt(IRenderedComponent<ContainerFragment> page, string address) =>
        Sheet(page).CellFormatAt(CellAddress.Parse(address));

    /// <summary>Format Cells' dialog, while it stands.</summary>
    protected static IElement Dialog(IRenderedComponent<ContainerFragment> page) => page.Find(".mud-dialog.mud-ex-sheet-format-cells");

    /// <summary>Whether Format Cells' dialog stands.</summary>
    protected static bool IsOpen(IRenderedComponent<ContainerFragment> page) => page.FindAll(".mud-dialog.mud-ex-sheet-format-cells").Count > 0;

    /// <summary>Opens Format Cells over <paramref name="selection"/> with <c>OpenFormatCellsAsync</c>, and waits for the dialog.</summary>
    protected static async Task OpenAsync(IRenderedComponent<ContainerFragment> page, string selection)
    {
        await GoToAsync(page, selection);
        Assert.True(await page.InvokeAsync(Sheet(page).OpenFormatCellsAsync));
        page.WaitForAssertion(() => Assert.True(IsOpen(page)));
        page.WaitForAssertion(() => Assert.NotEmpty(Tabs(page)));
    }

    /// <summary>The tabs' names, in order.</summary>
    protected static IReadOnlyList<string> Tabs(IRenderedComponent<ContainerFragment> page) =>
        [.. page.FindAll(".mud-ex-sheet-format-cells [role=tab]").Select(t => t.TextContent.Trim())];

    /// <summary>The tab shown.</summary>
    protected static IElement SelectedTab(IRenderedComponent<ContainerFragment> page) =>
        page.FindAll(".mud-ex-sheet-format-cells [role=tab]").Single(t => t.GetAttribute("aria-selected") == "true");

    /// <summary>Shows a tab by a press on it.</summary>
    protected static async Task ShowTabAsync(IRenderedComponent<ContainerFragment> page, string tab)
    {
        await page.FindAll(".mud-ex-sheet-format-cells [role=tab]").Single(t => t.TextContent.Trim() == tab).ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Equal(tab, SelectedTab(page).TextContent.Trim()));
    }

    /// <summary>A radio button in Format Cells by its label, as the user reads it.</summary>
    protected static IElement Radio(IRenderedComponent<ContainerFragment> page, string label) =>
        page.FindAll(".mud-ex-sheet-format-cells label").Single(l => l.QuerySelector("input[type=radio]") is not null && l.TextContent.Trim() == label)
            .QuerySelector("input[type=radio]")!;

    /// <summary>Chooses a MudBlazor radio button by its label.</summary>
    protected static Task ChooseAsync(IRenderedComponent<ContainerFragment> page, string label) =>
        Radio(page, label).ClickAsync(new MouseEventArgs());

    /// <summary>Chooses a swatch of the palette by the name Excel gives it.</summary>
    protected static Task SwatchAsync(IRenderedComponent<ContainerFragment> page, string name) =>
        page.FindAll(".mud-ex-sheet-swatch").First(s => s.GetAttribute("aria-label") == name).ChangeAsync(new ChangeEventArgs { Value = "on" });

    /// <summary>
    /// Presses OK, and waits until the keyboard has been handed back, which comes last: OK's change
    /// is set once the dialog has left the page, after it closes.
    /// </summary>
    protected async Task OkAsync(IRenderedComponent<ContainerFragment> page)
    {
        var reclaims = ReclaimCount;
        await page.Find(".mud-ex-sheet-format-cells-ok").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.True(ReclaimCount > reclaims));
    }

    /// <summary>Presses Cancel.</summary>
    protected static Task CancelAsync(IRenderedComponent<ContainerFragment> page) =>
        page.Find(".mud-ex-sheet-format-cells-cancel").ClickAsync(new MouseEventArgs());
}
