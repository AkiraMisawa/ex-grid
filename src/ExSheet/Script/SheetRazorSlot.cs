using System.Text.Json;
using ExGrid.Chrome;
using ExGrid.Script;
using Microsoft.AspNetCore.Components;

namespace ExSheet.Script;

/// <summary>
/// ExSheet's own Chrome slots filled with Razor (ADR-0410): the script's name of each of the Sheet's slots, the view
/// each slot's data read as, and the fragment the Chrome draws for it; the grid's slots are read and drawn as ExGrid's
/// adapter reads and draws them (<see cref="RazorSlot"/>). The holders and the numbers the script sent are the shared
/// <see cref="RazorSlotHolders"/>'.
/// </summary>
internal static class SheetRazorSlot
{
    /// <summary>The script's name of each of the Sheet's slots, as <see cref="SheetScriptSlots"/> names it.</summary>
    private static readonly IReadOnlyList<(SheetScriptSlots Slot, string Name)> Names =
    [
        (SheetScriptSlots.ToolbarItem, "toolbarItem"),
        (SheetScriptSlots.SheetTab, "sheetTab"),
        (SheetScriptSlots.SheetTabAdd, "sheetTabAdd"),
    ];

    /// <summary>The script's names of the slots <paramref name="slots"/> names, refusing one this adapter does not fill.</summary>
    internal static IEnumerable<string> NamesOf(SheetScriptSlots slots)
    {
        var known = Names.Aggregate(SheetScriptSlots.None, (all, each) => all | each.Slot);
        if ((slots & ~known) != 0)
            throw new InvalidOperationException($"The Chrome names Script slots ({slots & ~known}) that ExSheet's Script adapter does not fill (ADR-0410).");
        return Names.Where(each => slots.HasFlag(each.Slot)).Select(each => each.Name);
    }

    /// <summary>The Sheet's slot the script names, or none for one of the grid's.</summary>
    private static SheetScriptSlots? NameOf(string name)
    {
        foreach (var (slot, each) in Names)
        {
            if (each == name)
                return slot;
        }
        return null;
    }

    /// <summary>The view the slot the script names <paramref name="slot"/> is drawn from: one of the Sheet's, or one of
    /// the grid's.</summary>
    internal static object ViewOf(string slot, string context)
    {
        if (NameOf(slot) is not { } name)
            return RazorSlot.ViewOf(slot, context);
        using var document = JsonDocument.Parse(context);
        var json = document.RootElement;
        return name switch
        {
            SheetScriptSlots.ToolbarItem => new SheetToolbarItemView(
                ScriptJson.Text(json, "id"),
                ScriptJson.Member<ToolbarItemKind>(json.GetProperty("kind")),
                ScriptJson.Text(json, "name"),
                ScriptJson.Text(json, "tooltip"),
                ScriptJson.Present(json, "icon", out var icon) ? ScriptJson.Member<ToolbarIcon>(icon) : ToolbarIcon.None,
                Nullable(json, "text"),
                Nullable(json, "swatch"),
                Nullable(json, "moreName"),
                json.GetProperty("pressed").GetBoolean(),
                Nullable(json, "disabledReason"),
                [.. json.GetProperty("choices").EnumerateArray().Select(choice => new SheetToolbarChoiceView(
                    ScriptJson.Text(choice, "label"), Nullable(choice, "swatch"), choice.GetProperty("selected").GetBoolean(),
                    Nullable(choice, "disabledReason")))]),
            SheetScriptSlots.SheetTab => new SheetTabContext(ScriptJson.Text(json, "name"), json.GetProperty("selected").GetBoolean()),
            SheetScriptSlots.SheetTabAdd => AddFace.Instance,
            _ => throw new InvalidOperationException($"Internal: a Razor slot named {name}."),
        };
    }

    /// <summary>A text the script sends as null where the view has none.</summary>
    private static string? Nullable(JsonElement json, string name)
        => ScriptJson.Present(json, name, out var value) ? value.GetString() : null;

    /// <summary>The fragment the Chrome draws for the view of the slot the script names <paramref name="slot"/>: the
    /// Sheet Chrome's for one of the Sheet's slots, the grid Chrome's for one of the grid's; nothing where no Chrome
    /// fills the slot any longer.</summary>
    internal static RenderFragment? Fragment(IGridChrome? chrome, string slot, object view)
    {
        if (NameOf(slot) is not { } name)
            return RazorSlot.Fragment(chrome, slot, view);
        if (chrome is not ISheetChrome sheet || !sheet.SheetScriptSlots.HasFlag(name))
            return null;
        return (name, view) switch
        {
            (SheetScriptSlots.ToolbarItem, SheetToolbarItemView item) => sheet.ToolbarItem(item),
            (SheetScriptSlots.SheetTab, SheetTabContext tab) => sheet.SheetTab(tab),
            (SheetScriptSlots.SheetTabAdd, AddFace) => sheet.SheetTabAdd(),
            _ => throw new InvalidOperationException($"Internal: the slot {name} holds a view of another slot's."),
        };
    }

    /// <summary>The add button's face, which is drawn from nothing.</summary>
    private sealed class AddFace
    {
        internal static readonly AddFace Instance = new();
    }
}
