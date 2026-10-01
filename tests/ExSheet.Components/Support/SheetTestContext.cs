using AngleSharp.Dom;
using Bunit;
using ExGrid;
using ExGrid.Components;
using ExGrid.Selection;
using ExSheet;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using SheetComponent = ExSheet.Components.ExSheet;

namespace ExSheet.Components.Tests.Support;

/// <summary>
/// What every ExSheet test needs: ExGrid's JavaScript module stood in for, strictly — the day
/// ExSheet or the grid reaches for JavaScript somewhere new, a test says so (ADR-0021) — the
/// clock the grid's settle delay runs on, and the ways a user reaches the grid: keys, typing,
/// the Name Box, scrolling.
/// </summary>
public abstract class SheetTestContext : BunitContext
{
    private const string ModulePath = "./_content/ExGrid/ex-grid.js";

    private readonly BunitJSModuleInterop _handle;
    private bool _rendererInfoSet;

    protected SheetTestContext()
    {
        Services.AddSingleton<TimeProvider>(Clock);
        var module = JSInterop.SetupModule(ModulePath);
        _handle = module.SetupModule("attach", _ => true);
        _handle.Setup<bool>("metaIsPrimary").SetResult(false);
        _handle.Setup<ScrollOffset>("getScrollOffset").SetResult(default);
        _handle.Setup<bool>("anchorScrollTop", _ => true).SetResult(true);
        foreach (var name in new[] { "setScrollOffset", "blur", "setEditing", "setInnerPopup", "setClaims", "setCaret", "setPointerReporting", "forgetPointer", "writeCopy", "reclaimFocus", "focusEditor", "dispose" })
        {
            _handle.SetupVoid(name, _ => true).SetVoidResult();
        }
    }

    internal FakeTimeProvider Clock { get; } = new();

    /// <summary>Renders an ExSheet as a connected, interactive component.</summary>
    internal IRenderedComponent<SheetComponent> RenderSheet(Action<ComponentParameterCollectionBuilder<SheetComponent>>? parameters = null)
    {
        Interactive();
        return Render<SheetComponent>(ps =>
        {
            ps.Add(s => s.ViewportHeight, (ViewportSize)400)
              .Add(s => s.ViewportWidth, (ViewportSize)700)
              .Add(s => s.Culture, System.Globalization.CultureInfo.GetCultureInfo("en-US"));
            parameters?.Invoke(ps);
        });
    }

    /// <summary>Renders a Consumer's page holding an ExSheet, connected and interactive as <see cref="RenderSheet"/> renders one.</summary>
    internal IRenderedComponent<TPage> RenderPage<TPage>() where TPage : IComponent
    {
        Interactive();
        return Render<TPage>();
    }

    // Telling bUnit the renderer's info builds the renderer, so it is told once, at the first render.
    private void Interactive()
    {
        if (_rendererInfoSet) return;
        _rendererInfoSet = true;
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    internal static IRenderedComponent<ExGrid<SheetRow>> Grid(IRenderedComponent<SheetComponent> cut) =>
        cut.FindComponent<ExGrid<SheetRow>>();

    internal static Task PressAsync(IRenderedComponent<SheetComponent> cut, string key, bool ctrl = false, bool shift = false)
    {
        var grid = Grid(cut);
        return grid.InvokeAsync(() => grid.Instance.OnKeyAsync(key, ctrl, shift, false, false, false));
    }

    /// <summary>
    /// A key as the capture listener forwards it while an edit is open: with the editor's text
    /// and caret as the browser has them (ADR-0051), from the Formula Bar when
    /// <paramref name="fromBar"/>, and with a selection to <paramref name="selectionEnd"/> when
    /// one is given.
    /// </summary>
    internal static Task PressInEditorAsync(
        IRenderedComponent<SheetComponent> cut, string key, string text, int caret, bool shift = false, bool fromBar = false,
        int selectionEnd = -1)
    {
        var grid = Grid(cut);
        return grid.InvokeAsync(() => grid.Instance.OnKeyAsync(
            key, false, shift, false, false, false, fromDescendant: fromBar, editorText: text, editorCaret: caret,
            editorSelectionEnd: selectionEnd < 0 ? caret : selectionEnd));
    }

    /// <summary>What the Cell Editor holds, as painted.</summary>
    internal static string EditorText(IRenderedComponent<SheetComponent> cut) =>
        cut.Find(".ex-viewport .ex-editor").GetAttribute("value") ?? "";

    /// <summary>What the user has typed into the open Cell Editor so far.</summary>
    internal static async Task TypeAsync(IRenderedComponent<SheetComponent> cut, string text)
    {
        await cut.Find(".ex-viewport .ex-editor").InputAsync(new ChangeEventArgs { Value = text });
        await ReportCaretAsync(cut, text);
    }

    /// <summary>
    /// What the browser's listener does after each input: reports the caret position, which the
    /// core waits for before it asks for completion or points (ADR-0051, second round).
    /// </summary>
    internal static Task ReportCaretAsync(IRenderedComponent<SheetComponent> cut, string text, int? caret = null)
    {
        var grid = Grid(cut);
        return grid.InvokeAsync(() => grid.Instance.OnEditorCaretAsync(text, caret ?? text.Length));
    }

    /// <summary>Types into the Formula Bar's surface, then reports the caret as the listener would.</summary>
    internal static async Task TypeInBarAsync(IRenderedComponent<SheetComponent> cut, string text)
    {
        await cut.Find(".ex-formula-bar-text").InputAsync(new ChangeEventArgs { Value = text });
        await ReportCaretAsync(cut, text);
    }

    /// <summary>Types an address into the Name Box and presses Enter (ADR-0051).</summary>
    internal static async Task GoToAsync(IRenderedComponent<SheetComponent> cut, string address)
    {
        await cut.Find(".ex-name-box").InputAsync(new ChangeEventArgs { Value = address });
        await cut.Find(".ex-name-box-form").SubmitAsync();
    }

    /// <summary>
    /// Types <paramref name="typed"/> into the cell at <paramref name="address"/> as a user does:
    /// the Name Box to get there, the first character to open Overwrite, the rest into the
    /// editor, Enter to commit.
    /// </summary>
    internal static async Task EnterAsync(IRenderedComponent<SheetComponent> cut, string address, string typed)
    {
        await GoToAsync(cut, address);
        await PressAsync(cut, typed[..1]);
        await TypeAsync(cut, typed);
        await PressAsync(cut, "Enter");
    }

    /// <summary>The text painted in the cell at <paramref name="address"/>, which must be on screen.</summary>
    internal static string CellText(IRenderedComponent<SheetComponent> cut, string address) => Cell(cut, address).TextContent;

    /// <summary>The painted cell at <paramref name="address"/>, found by its row and its column index.</summary>
    internal static IElement Cell(IRenderedComponent<SheetComponent> cut, string address)
    {
        var at = CellAddress.Parse(address);
        var row = cut.FindAll(".ex-row").Single(r => r.GetAttribute("aria-rowindex") == (at.Row + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        return row.QuerySelectorAll("[role=gridcell]").Single(c => c.GetAttribute("aria-colindex") == (at.Column + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>Moves the browser's scroll position and raises the scroll event the grid listens for:
    /// the Sheet's grid, or a grid of the page's own.</summary>
    internal async Task ScrollToAsync<TComponent>(IRenderedComponent<TComponent> cut, double top, double left)
        where TComponent : IComponent
    {
        _handle.Setup<ScrollOffset>("getScrollOffset").SetResult(new ScrollOffset(top, left));
        await cut.Find(".ex-scroller").ScrollAsync(EventArgs.Empty);
        await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(500)));
    }

    /// <summary>A click on a column header, at <paramref name="x"/> px from the header's left edge.</summary>
    internal static Task ClickHeaderAsync(IRenderedComponent<SheetComponent> cut, double x, bool shift = false) =>
        cut.Find(".ex-header").ClickAsync(new MouseEventArgs { Button = 0, OffsetX = x, OffsetY = 5, ShiftKey = shift });
}
