using System.Text.Json;
using ExGrid.Chrome;
using Microsoft.AspNetCore.Components;

namespace ExGrid.Script;

/// <summary>
/// A Chrome slot the script mounted, filled with Razor (ADR-0410): the number the script gave it, which slot it
/// is, the view its context last crossed as, and the element its fragment is drawn in, which the script moves into
/// the slot once it has been rendered.
/// </summary>
internal sealed class RazorSlot
{
    internal RazorSlot(long id, ScriptSlots name, int version, string context)
    {
        Id = id;
        Name = name;
        Version = version;
        View = ViewOf(name, context);
        Owed = true;
    }

    internal long Id { get; }

    internal ScriptSlots Name { get; }

    /// <summary>The number the script gave the data the view was read from, which it is told back once drawn.</summary>
    internal int Version { get; private set; }

    /// <summary>The view the slot's context crossed as: a <see cref="GridMenuView"/> or a <see cref="GridLoadingView"/>.</summary>
    internal object View { get; private set; }

    /// <summary>The element the fragment is drawn in, once rendered.</summary>
    internal ElementReference Contents { get; set; }

    /// <summary>Drawn for a view the script has not yet been handed.</summary>
    internal bool Owed { get; set; }

    /// <summary>The script's name of each slot, as <see cref="ScriptSlots"/> names it.</summary>
    private static readonly IReadOnlyList<(ScriptSlots Slot, string Name)> Names =
    [
        (ScriptSlots.ColumnMenu, "columnMenu"),
        (ScriptSlots.ContextMenu, "contextMenu"),
        (ScriptSlots.LoadingIndicator, "loadingIndicator"),
    ];

    /// <summary>The script's names of the slots <paramref name="slots"/> names, refusing one this adapter does not fill.</summary>
    internal static IEnumerable<string> NamesOf(ScriptSlots slots)
    {
        var known = Names.Aggregate(ScriptSlots.None, (all, each) => all | each.Slot);
        if ((slots & ~known) != 0)
            throw new InvalidOperationException($"The Chrome names Script slots ({slots & ~known}) that ExGrid's Script adapter does not fill (ADR-0410).");
        return Names.Where(each => slots.HasFlag(each.Slot)).Select(each => each.Name);
    }

    /// <summary>The slot the script names.</summary>
    internal static ScriptSlots NameOf(string name)
    {
        foreach (var (slot, each) in Names)
        {
            if (each == name)
                return slot;
        }
        throw new InvalidOperationException($"Internal: the script mounted a slot named '{name}', which the Chrome does not fill.");
    }

    /// <summary>Takes the context the script sent under <paramref name="version"/>, as the slot's view, and owes the
    /// script its drawing.</summary>
    internal void Take(int version, string context)
    {
        Version = version;
        View = ViewOf(Name, context);
        Owed = true;
    }

    private static object ViewOf(ScriptSlots name, string context)
    {
        using var document = JsonDocument.Parse(context);
        var json = document.RootElement;
        return name switch
        {
            ScriptSlots.ColumnMenu or ScriptSlots.ContextMenu => new GridMenuView(
                ScriptJson.Text(json, "column"),
                ScriptJson.Member<ColumnType>(json.GetProperty("columnType")),
                [.. json.GetProperty("commands").EnumerateArray().Select(command => new GridMenuItemView(
                    ScriptJson.Text(command, "id"), ScriptJson.Text(command, "label"), command.GetProperty("enabled").GetBoolean()))]),
            ScriptSlots.LoadingIndicator => new GridLoadingView(json.GetProperty("loading").GetBoolean(), json.GetProperty("placeholderRows").GetInt32()),
            _ => throw new InvalidOperationException($"Internal: a Razor slot named {name}."),
        };
    }

    /// <summary>The fragment <paramref name="chrome"/> draws for the view; nothing where no Chrome fills the slot any longer.</summary>
    internal RenderFragment? Fragment(IGridChrome? chrome)
    {
        if (chrome is null || !chrome.ScriptSlots.HasFlag(Name))
            return null;
        return (Name, View) switch
        {
            (ScriptSlots.ColumnMenu, GridMenuView menu) => chrome.ColumnMenu(menu),
            (ScriptSlots.ContextMenu, GridMenuView menu) => chrome.ContextMenu(menu),
            (ScriptSlots.LoadingIndicator, GridLoadingView loading) => chrome.LoadingIndicator(loading),
            _ => throw new InvalidOperationException($"Internal: the slot {Name} holds a view of another slot's."),
        };
    }
}
