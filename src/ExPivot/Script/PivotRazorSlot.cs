using System.Text.Json;
using ExGrid.Chrome;
using ExGrid.Script;
using ExPivot.Chrome;
using Microsoft.AspNetCore.Components;

namespace ExPivot.Script;

/// <summary>
/// ExPivot's Chrome slots filled with Razor (ADR-0410): the script's name of each of the pivot's slots, the view each
/// slot's data read as, and the fragment the Chrome draws for it; the report grid's slots are read and drawn as
/// ExGrid's adapter reads and draws them (<see cref="RazorSlot"/>). The holders and the numbers the script sent are the
/// shared <see cref="RazorSlotHolders"/>'.
/// </summary>
internal static class PivotRazorSlot
{
    /// <summary>The script's name of each of the pivot's slots, as <see cref="PivotScriptSlots"/> names it.</summary>
    private static readonly IReadOnlyList<(PivotScriptSlots Slot, string Name)> Names =
    [
        (PivotScriptSlots.Menu, "menu"),
        (PivotScriptSlots.Toolbar, "toolbar"),
        (PivotScriptSlots.StaleReport, "staleReport"),
    ];

    /// <summary>The script's names of the slots <paramref name="slots"/> names, refusing one this adapter does not fill.</summary>
    internal static IEnumerable<string> NamesOf(PivotScriptSlots slots)
    {
        var known = Names.Aggregate(PivotScriptSlots.None, (all, each) => all | each.Slot);
        if ((slots & ~known) != 0)
            throw new InvalidOperationException($"The Chrome names Script slots ({slots & ~known}) that ExPivot's Script adapter does not fill (ADR-0410).");
        return Names.Where(each => slots.HasFlag(each.Slot)).Select(each => each.Name);
    }

    /// <summary>The pivot's slot the script names, or none for one of the report grid's.</summary>
    private static PivotScriptSlots? NameOf(string name)
    {
        foreach (var (slot, each) in Names)
        {
            if (each == name)
                return slot;
        }
        return null;
    }

    /// <summary>The view the slot the script names <paramref name="slot"/> is drawn from: one of the pivot's, or one of
    /// the report grid's.</summary>
    internal static object ViewOf(string slot, string context)
    {
        if (NameOf(slot) is not { } name)
            return RazorSlot.ViewOf(slot, context);
        using var document = JsonDocument.Parse(context);
        var json = document.RootElement;
        return name switch
        {
            PivotScriptSlots.Menu => new PivotMenuView(
                ScriptJson.Text(json, "name"),
                [.. json.GetProperty("commands").EnumerateArray().Select(command => new PivotMenuItemView(
                    ScriptJson.Text(command, "id"),
                    ScriptJson.Text(command, "label"),
                    command.GetProperty("enabled").GetBoolean(),
                    command.GetProperty("checked") is { ValueKind: JsonValueKind.True or JsonValueKind.False } ticked ? ticked.GetBoolean() : null,
                    command.GetProperty("heading") is { ValueKind: JsonValueKind.String } heading ? heading.GetString() : null))]),
            PivotScriptSlots.Toolbar => ToolbarViewOf(json),
            PivotScriptSlots.StaleReport => new PivotStaleReportView(
                ScriptJson.Text(json, "message"),
                ScriptJson.Text(json, "asOfText"),
                ScriptJson.Text(json, "reason"),
                ScriptJson.Text(json.GetProperty("retry"), "label"),
                json.GetProperty("retry").GetProperty("enabled").GetBoolean()),
            _ => throw new InvalidOperationException($"Internal: a Razor slot named {name}."),
        };
    }

    private static PivotToolbarView ToolbarViewOf(JsonElement json)
    {
        var layout = json.GetProperty("layoutMenu");
        var fieldList = json.GetProperty("fieldList");
        return new PivotToolbarView(
            ScriptJson.Text(layout, "label"),
            layout.GetProperty("open").GetBoolean(),
            json.GetProperty("refresh") is { ValueKind: JsonValueKind.Object } refresh ? ScriptJson.Text(refresh, "label") : null,
            ScriptJson.Text(fieldList, "label"),
            fieldList.GetProperty("shown").GetBoolean(),
            json.GetProperty("reportFilters").GetBoolean(),
            json.GetProperty("refusal") is { ValueKind: JsonValueKind.String } refusal ? refusal.GetString() : null);
    }

    /// <summary>The fragment the Chrome draws for the view of the slot the script names <paramref name="slot"/>: the
    /// pivot Chrome's for one of the pivot's slots, the grid Chrome's for one of the report grid's; nothing where no
    /// Chrome fills the slot any longer.</summary>
    internal static RenderFragment? Fragment(IPivotChrome? chrome, IGridChrome? gridChrome, string slot, object view)
    {
        if (NameOf(slot) is not { } name)
            return RazorSlot.Fragment(gridChrome, slot, view);
        if (chrome is null || !chrome.ScriptSlots.HasFlag(name))
            return null;
        return (name, view) switch
        {
            (PivotScriptSlots.Menu, PivotMenuView menu) => chrome.Menu(menu),
            (PivotScriptSlots.Toolbar, PivotToolbarView toolbar) => chrome.Toolbar(toolbar),
            (PivotScriptSlots.StaleReport, PivotStaleReportView notice) => chrome.StaleReport(notice),
            _ => throw new InvalidOperationException($"Internal: the slot {name} holds a view of another slot's."),
        };
    }
}
