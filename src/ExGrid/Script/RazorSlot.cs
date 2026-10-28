using System.Text.Json;
using ExGrid.Cells;
using ExGrid.Chrome;
using Microsoft.AspNetCore.Components;

namespace ExGrid.Script;

/// <summary>
/// ExGrid's Chrome slots filled with Razor (ADR-0410): the script's name of each slot, the view each slot's data read
/// as, and the fragment the Chrome draws for it. The holders and the numbers the script sent are the shared
/// <see cref="RazorSlotHolders"/>'.
/// </summary>
internal static class RazorSlot
{
    /// <summary>The script's name of each slot, as <see cref="ScriptSlots"/> names it.</summary>
    private static readonly IReadOnlyList<(ScriptSlots Slot, string Name)> Names =
    [
        (ScriptSlots.ColumnMenu, "columnMenu"),
        (ScriptSlots.ContextMenu, "contextMenu"),
        (ScriptSlots.LoadingIndicator, "loadingIndicator"),
        (ScriptSlots.FilterPanel, "filterPanel"),
        (ScriptSlots.FindPanel, "findPanel"),
        (ScriptSlots.CellEditor, "cellEditor"),
        (ScriptSlots.FormulaBarText, "formulaBarText"),
        (ScriptSlots.NameBox, "nameBox"),
        (ScriptSlots.EditorCompletion, "editorCompletion"),
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
    private static ScriptSlots NameOf(string name)
    {
        foreach (var (slot, each) in Names)
        {
            if (each == name)
                return slot;
        }
        throw new InvalidOperationException($"Internal: the script mounted a slot named '{name}', which the Chrome does not fill.");
    }

    /// <summary>The view the slot the script names <paramref name="slot"/> is drawn from: a <see cref="GridMenuView"/>, a
    /// <see cref="GridLoadingView"/>, a <see cref="GridFilterView"/>, a <see cref="GridFindView"/>, a
    /// <see cref="GridCellEditorView"/>, a <see cref="GridFormulaBarTextView"/>, a <see cref="GridNameBoxView"/> or a
    /// <see cref="GridEditorCompletionView"/>.</summary>
    internal static object ViewOf(string slot, string context)
    {
        var name = NameOf(slot);
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
            ScriptSlots.CellEditor => new GridCellEditorView(
                ScriptJson.Text(json, "column"),
                ScriptJson.Member<ColumnType>(json.GetProperty("columnType")),
                ScriptJson.Text(json, "initialText"),
                ScriptJson.Text(json, "mode") switch
                {
                    "overwrite" => CellEditMode.Overwrite,
                    "caret" => CellEditMode.Caret,
                    "point" => CellEditMode.Point,
                    var other => throw new InvalidOperationException($"Internal: the script sent '{other}' as the editor's mode."),
                },
                ScriptJson.Present(json, "error", out var error) ? error.GetString() : null,
                json.GetProperty("pending").GetBoolean()),
            ScriptSlots.FormulaBarText => new GridFormulaBarTextView(),
            ScriptSlots.NameBox => new GridNameBoxView(),
            ScriptSlots.EditorCompletion => new GridEditorCompletionView(
                [.. json.GetProperty("labels").EnumerateArray().Select(label => label.GetString()
                    ?? throw new InvalidOperationException("Internal: the script sent a candidate with no label."))],
                json.GetProperty("hint") is { ValueKind: JsonValueKind.Object } hint
                    ? new EditorHint(ScriptJson.Text(hint, "text"), hint.GetProperty("emphasisStart").GetInt32(), hint.GetProperty("emphasisLength").GetInt32())
                    : null),
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

    /// <summary>The fragment <paramref name="chrome"/> draws for the view of the slot the script names
    /// <paramref name="slot"/>; nothing where no Chrome fills the slot any longer.</summary>
    internal static RenderFragment? Fragment(IGridChrome? chrome, string slot, object view)
    {
        var name = NameOf(slot);
        if (chrome is null || !chrome.ScriptSlots.HasFlag(name))
            return null;
        return (name, view) switch
        {
            (ScriptSlots.ColumnMenu, GridMenuView menu) => chrome.ColumnMenu(menu),
            (ScriptSlots.ContextMenu, GridMenuView menu) => chrome.ContextMenu(menu),
            (ScriptSlots.LoadingIndicator, GridLoadingView loading) => chrome.LoadingIndicator(loading),
            (ScriptSlots.FilterPanel, GridFilterView filter) => chrome.FilterPanel(filter),
            (ScriptSlots.FindPanel, GridFindView find) => chrome.FindPanel(find),
            (ScriptSlots.CellEditor, GridCellEditorView editor) => chrome.CellEditor(editor),
            (ScriptSlots.FormulaBarText, GridFormulaBarTextView bar) => chrome.FormulaBarText(bar),
            (ScriptSlots.NameBox, GridNameBoxView box) => chrome.NameBox(box),
            (ScriptSlots.EditorCompletion, GridEditorCompletionView completion) => chrome.EditorCompletion(completion),
            _ => throw new InvalidOperationException($"Internal: the slot {name} holds a view of another slot's."),
        };
    }
}
