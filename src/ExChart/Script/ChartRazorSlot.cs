using System.Text.Json;
using global::ExChart.Chrome;
using global::ExChart.Engine;
using Microsoft.AspNetCore.Components;

namespace ExChart.Script;

/// <summary>
/// ExChart's Chrome slots filled with Razor (ADR-0410): the script's name of each slot, the view each slot's data read
/// as, and the fragment the Chrome draws for it. The holders and the numbers the script sent are the shared
/// <c>RazorSlotHolders</c>'.
/// </summary>
internal static class ChartRazorSlot
{
    /// <summary>The script's name of each slot, as <see cref="ChartScriptSlots"/> names it.</summary>
    private static readonly IReadOnlyList<(ChartScriptSlots Slot, string Name)> Names =
    [
        (ChartScriptSlots.Toolbar, "toolbar"),
        (ChartScriptSlots.Menu, "menu"),
    ];

    /// <summary>The script's names of the slots <paramref name="slots"/> names, refusing one this adapter does not fill.</summary>
    internal static IEnumerable<string> NamesOf(ChartScriptSlots slots)
    {
        var known = Names.Aggregate(ChartScriptSlots.None, (all, each) => all | each.Slot);
        if ((slots & ~known) != 0)
            throw new InvalidOperationException($"The Chrome names Script slots ({slots & ~known}) that ExChart's Script adapter does not fill (ADR-0410).");
        return Names.Where(each => slots.HasFlag(each.Slot)).Select(each => each.Name);
    }

    /// <summary>The slot the script names.</summary>
    private static ChartScriptSlots NameOf(string name)
    {
        foreach (var (slot, each) in Names)
        {
            if (each == name)
                return slot;
        }
        throw new InvalidOperationException($"Internal: the script mounted a chart slot named '{name}', which the Chrome does not fill.");
    }

    /// <summary>The view the slot the script names <paramref name="slot"/> is drawn from: a <see cref="ChartToolbarView"/>
    /// or a <see cref="ChartMenuView"/>.</summary>
    internal static object ViewOf(string slot, string context)
    {
        var name = NameOf(slot);
        using var document = JsonDocument.Parse(context);
        var json = document.RootElement;
        return name switch
        {
            ChartScriptSlots.Toolbar => ToolbarOf(json),
            ChartScriptSlots.Menu => new ChartMenuView(Text(json, "name"), CommandsOf(json)),
            _ => throw new InvalidOperationException($"Internal: a Razor slot named {name}."),
        };
    }

    private static ChartToolbarView ToolbarOf(JsonElement json)
    {
        var words = json.GetProperty("words");
        string Word(string name) => Text(words, name);
        ChartFiltersView? filters = null;
        if (json.GetProperty("filters") is { ValueKind: JsonValueKind.Object } f)
        {
            filters = new ChartFiltersView(
                ChartWire.Member<AxisValues>(Text(f, "values")),
                f.GetProperty("labels").EnumerateArray().Select(label => label.GetString()
                    ?? throw new InvalidOperationException("Internal: the script sent null for a category.")).ToEquatableList(),
                Text(f, "hint"),
                OptionalText(f, "error"),
                OptionalText(f, "scale") is { } scale ? ChartWire.Member<AxisScale>(scale) : null);
        }
        return new ChartToolbarView(
            Text(json, "name"),
            CommandsOf(json),
            filters,
            new ChartToolbarWords(Word("from"), Word("to"), Word("apply"), Word("axis"), Word("proportional"), Word("categories"),
                Word("downloads"), Word("filters")));
    }

    private static EquatableList<ChartCommandView> CommandsOf(JsonElement json) =>
        json.GetProperty("commands").EnumerateArray().Select(command =>
            new ChartCommandView(Text(command, "id"), Text(command, "label"), command.GetProperty("enabled").GetBoolean())
            {
                Expanded = command.GetProperty("expanded") is { ValueKind: JsonValueKind.True or JsonValueKind.False } expanded ? expanded.GetBoolean() : null,
                Title = OptionalText(command, "title"),
            }).ToEquatableList();

    private static string Text(JsonElement json, string name) =>
        json.GetProperty(name).GetString() ?? throw new InvalidOperationException($"Internal: the script sent null for {name}.");

    private static string? OptionalText(JsonElement json, string name) =>
        json.GetProperty(name) is { ValueKind: JsonValueKind.String } text ? text.GetString() : null;

    /// <summary>The fragment <paramref name="chrome"/> draws for the view of the slot the script names
    /// <paramref name="slot"/>; nothing where no Chrome fills the slot any longer.</summary>
    internal static RenderFragment? Fragment(IChartChrome? chrome, string slot, object view)
    {
        var name = NameOf(slot);
        if (chrome is null || !chrome.ScriptSlots.HasFlag(name))
            return null;
        return (name, view) switch
        {
            (ChartScriptSlots.Toolbar, ChartToolbarView toolbar) => chrome.Toolbar(toolbar),
            (ChartScriptSlots.Menu, ChartMenuView menu) => chrome.Menu(menu),
            _ => throw new InvalidOperationException($"Internal: the slot {name} holds a view of another slot's."),
        };
    }
}
