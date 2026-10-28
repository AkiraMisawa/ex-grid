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

    /// <summary>The attribute the element the fragment is drawn in carries: the number of the data it was drawn from,
    /// which the script reads a press on the contents against (razor-slots.ts' <c>drawnVersionAttribute</c>).</summary>
    internal const string DrawnVersionAttribute = "data-ex-version";

    internal long Id { get; }

    internal ScriptSlots Name { get; }

    /// <summary>The number the script gave the data the view was read from, which it is told back once drawn.</summary>
    internal int Version { get; private set; }

    /// <summary>The view the slot's context crossed as: a <see cref="GridMenuView"/>, a <see cref="GridLoadingView"/>, a
    /// <see cref="GridFilterView"/> or a <see cref="GridFindView"/>.</summary>
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
        (ScriptSlots.FilterPanel, "filterPanel"),
        (ScriptSlots.FindPanel, "findPanel"),
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
            ScriptSlots.FilterPanel => FilterViewOf(json),
            ScriptSlots.FindPanel => FindViewOf(json),
            _ => throw new InvalidOperationException($"Internal: a Razor slot named {name}."),
        };
    }

    private static GridFilterView FilterViewOf(JsonElement json)
    {
        var words = json.GetProperty("words");
        string Word(string name) => ScriptJson.Text(words, name);
        GridFilterValueListView? valueList = null;
        GridFilterConditionsView? conditions = null;
        if (json.TryGetProperty("valueList", out var list))
        {
            valueList = new GridFilterValueListView(
                ScriptJson.Text(list, "search"),
                list.GetProperty("addOffered").GetBoolean(),
                list.GetProperty("addToCurrent").GetBoolean(),
                ScriptJson.Text(list, "selectAll") switch
                {
                    "checked" => SelectAllState.Checked,
                    "unchecked" => SelectAllState.Unchecked,
                    "mixed" => SelectAllState.Mixed,
                    var other => throw new InvalidOperationException($"Internal: the script sent '{other}' as the state of (Select All)."),
                },
                [.. list.GetProperty("entries").EnumerateArray().Select(entry => new GridFilterEntryView(
                    ScriptJson.Text(entry, "text"), entry.GetProperty("blank").GetBoolean(), entry.GetProperty("checked").GetBoolean()))]);
        }
        else
        {
            var form = json.GetProperty("conditions");
            conditions = new GridFilterConditionsView(
                [.. form.GetProperty("allowed").EnumerateArray().Select(op => new GridFilterOperatorView(ScriptJson.Text(op, "id"), ScriptJson.Text(op, "label")))],
                ConditionOf(form.GetProperty("first")),
                ScriptJson.Text(form, "combinator") switch
                {
                    "and" => FilterCombinator.And,
                    "or" => FilterCombinator.Or,
                    var other => throw new InvalidOperationException($"Internal: the script sent '{other}' as the join of two conditions."),
                },
                form.GetProperty("second") is { ValueKind: JsonValueKind.Object } second ? ConditionOf(second) : null);
        }
        return new GridFilterView(
            ScriptJson.Text(json, "column"),
            ScriptJson.Member<ColumnType>(json.GetProperty("columnType")),
            valueList,
            conditions,
            json.GetProperty("canApply").GetBoolean(),
            json.GetProperty("fetching").GetBoolean(),
            new GridFilterWords(Word("ok"), Word("cancel"), Word("search"), Word("addToCurrent"), Word("selectAll"),
                Word("selectAllSearchResults"), Word("and"), Word("or"), Word("none")));
    }

    private static GridFindView FindViewOf(JsonElement json)
    {
        var words = json.GetProperty("words");
        string Word(string name) => ScriptJson.Text(words, name);
        return new GridFindView(
            ScriptJson.Text(json, "text"),
            json.GetProperty("matchCase").GetBoolean(),
            json.GetProperty("wholeCell").GetBoolean(),
            json.GetProperty("replaceShown").GetBoolean(),
            ScriptJson.Text(json, "replaceText"),
            json.GetProperty("replaceWaiting").GetBoolean(),
            ScriptJson.Text(json, "outcome"),
            new GridFindWords(Word("field"), Word("matchCase"), Word("wholeCell"), Word("next"), Word("previous"),
                Word("replaceField"), Word("replace"), Word("replaceAll")));
    }

    private static GridFilterConditionView ConditionOf(JsonElement json) => new(
        ScriptJson.Text(json, "operator"),
        ScriptJson.Text(json, "operand"),
        json.GetProperty("takesOperand").GetBoolean(),
        json.GetProperty("refusal") is { ValueKind: JsonValueKind.String } refusal ? refusal.GetString() : null);

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
            (ScriptSlots.FilterPanel, GridFilterView filter) => chrome.FilterPanel(filter),
            (ScriptSlots.FindPanel, GridFindView find) => chrome.FindPanel(find),
            _ => throw new InvalidOperationException($"Internal: the slot {Name} holds a view of another slot's."),
        };
    }
}
