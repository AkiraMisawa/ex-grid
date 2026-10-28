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
        (PivotScriptSlots.ReportFilters, "reportFilters"),
        (PivotScriptSlots.ItemFilter, "itemFilter"),
        (PivotScriptSlots.FieldSettings, "fieldSettings"),
        (PivotScriptSlots.ValueFieldSettings, "valueFieldSettings"),
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
            PivotScriptSlots.ReportFilters => new PivotReportFiltersView(
                ScriptJson.Text(json, "name"),
                [.. json.GetProperty("filters").EnumerateArray().Select(filter => new PivotReportFiltersEntryView(
                    ScriptJson.Text(filter, "field"),
                    ScriptJson.Text(filter, "caption"),
                    ScriptJson.Text(filter, "summary"),
                    filter.GetProperty("filtered").GetBoolean(),
                    filter.GetProperty("open").GetBoolean(),
                    ScriptJson.Text(filter, "buttonLabel")))]),
            PivotScriptSlots.ItemFilter => ItemFilterViewOf(json),
            PivotScriptSlots.FieldSettings => FieldSettingsViewOf(json),
            PivotScriptSlots.ValueFieldSettings => ValueFieldSettingsViewOf(json),
            _ => throw new InvalidOperationException($"Internal: a Razor slot named {name}."),
        };
    }

    private static PivotItemFilterView ItemFilterViewOf(JsonElement json)
    {
        var words = json.GetProperty("words");
        return new PivotItemFilterView(
            ScriptJson.Text(json, "caption"),
            ScriptJson.Text(json, "search"),
            [.. json.GetProperty("items").EnumerateArray().Select(item => new PivotItemChoiceView(ScriptJson.Text(item, "key"), ScriptJson.Text(item, "label"), item.GetProperty("ticked").GetBoolean()))],
            json.GetProperty("allTicked") is { ValueKind: JsonValueKind.Null } ? null : json.GetProperty("allTicked").GetBoolean(),
            json.GetProperty("loading").GetBoolean(),
            json.GetProperty("updating").GetBoolean(),
            TextOrNull(json, "unavailable"),
            TextOrNull(json, "note"),
            json.GetProperty("canApply").GetBoolean(),
            TextOrNull(json, "refusal"),
            ScriptJson.Text(words, "search"),
            ScriptJson.Text(words, "selectAll"),
            ScriptJson.Text(words, "loading"),
            ScriptJson.Text(words, "ok"),
            ScriptJson.Text(words, "cancel"));
    }

    private static PivotFieldSettingsView FieldSettingsViewOf(JsonElement json)
    {
        var words = json.GetProperty("words");
        return new PivotFieldSettingsView(
            ScriptJson.Text(json, "caption"),
            json.GetProperty("subtotals").GetBoolean(),
            [.. json.GetProperty("sorts").EnumerateArray().Select(sort => sort.GetString() ?? throw new InvalidOperationException("Internal: an order crossed with no words."))],
            json.GetProperty("sort").GetInt32(),
            ScriptJson.Text(words, "subtotals"),
            ScriptJson.Text(words, "automatic"),
            ScriptJson.Text(words, "none"),
            ScriptJson.Text(words, "sortOrder"),
            ScriptJson.Text(words, "ok"),
            ScriptJson.Text(words, "cancel"));
    }

    private static PivotValueFieldSettingsView ValueFieldSettingsViewOf(JsonElement json)
    {
        var words = json.GetProperty("words");
        return new PivotValueFieldSettingsView(
            ScriptJson.Text(words, "title"),
            ScriptJson.Text(json, "sourceCaption"),
            ScriptJson.Text(json, "caption"),
            ScriptJson.Text(json, "aggregation"),
            ChoicesOf(json.GetProperty("aggregations")),
            ScriptJson.Text(json, "showValuesAs"),
            ChoicesOf(json.GetProperty("showValuesAsChoices")),
            ScriptJson.Text(json, "numberFormat"),
            ScriptJson.Text(json, "numberFormatPlaceholder"),
            ScriptJson.Text(json, "sample"),
            TextOrNull(json, "refusal"),
            ScriptJson.Text(words, "customName"),
            ScriptJson.Text(words, "summarizeBy"),
            ScriptJson.Text(words, "showValuesAs"),
            ScriptJson.Text(words, "numberFormat"),
            ScriptJson.Text(words, "ok"),
            ScriptJson.Text(words, "cancel"));
    }

    private static IReadOnlyList<PivotChoiceView> ChoicesOf(JsonElement choices) =>
        [.. choices.EnumerateArray().Select(choice => new PivotChoiceView(
            ScriptJson.Text(choice, "value"),
            ScriptJson.Text(choice, "label"),
            choice.GetProperty("enabled").GetBoolean(),
            TextOrNull(choice, "reason")))];

    // A text the script sends as null where there is none; anything but a text or null is refused.
    private static string? TextOrNull(JsonElement json, string name) => json.GetProperty(name).GetString();

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
            (PivotScriptSlots.ReportFilters, PivotReportFiltersView band) => chrome.ReportFilters(band),
            (PivotScriptSlots.ItemFilter, PivotItemFilterView filter) => chrome.ItemFilter(filter),
            (PivotScriptSlots.FieldSettings, PivotFieldSettingsView settings) => chrome.FieldSettings(settings),
            (PivotScriptSlots.ValueFieldSettings, PivotValueFieldSettingsView settings) => chrome.ValueFieldSettings(settings),
            _ => throw new InvalidOperationException($"Internal: the slot {name} holds a view of another slot's."),
        };
    }
}
