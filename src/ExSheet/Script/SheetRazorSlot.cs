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
        (SheetScriptSlots.FormatCells, "formatCells"),
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
                    Nullable(choice, "disabledReason")))],
                [.. json.GetProperty("rows").EnumerateArray().Select(row => (IReadOnlyList<int>)[.. row.EnumerateArray().Select(at => at.GetInt32())])]),
            SheetScriptSlots.SheetTab => new SheetTabContext(ScriptJson.Text(json, "name"), json.GetProperty("selected").GetBoolean()),
            SheetScriptSlots.SheetTabAdd => AddFace.Instance,
            SheetScriptSlots.FormatCells => FormatCellsView(json),
            _ => throw new InvalidOperationException($"Internal: a Razor slot named {name}."),
        };
    }

    /// <summary>Format Cells as the script describes it.</summary>
    private static SheetFormatCellsView FormatCellsView(JsonElement json)
    {
        var labels = json.GetProperty("labels");
        var number = json.GetProperty("number");
        var font = json.GetProperty("font");
        var border = json.GetProperty("border");
        IReadOnlyList<IReadOnlyList<(int Index, string Name, string Paint)>> swatches = [.. json.GetProperty("swatches").EnumerateArray()
            .Select(row => (IReadOnlyList<(int, string, string)>)[.. row.EnumerateArray().Select(swatch =>
                (swatch.GetProperty("index").GetInt32(), ScriptJson.Text(swatch, "name"), ScriptJson.Text(swatch, "paint")))])];
        return new SheetFormatCellsView(
            ScriptJson.Text(json, "title"),
            new SheetFormatCellsLabels(
                ScriptJson.Text(labels, "tabs"), ScriptJson.Text(labels, "category"), ScriptJson.Text(labels, "decimalPlaces"),
                ScriptJson.Text(labels, "thousandsSeparator"), ScriptJson.Text(labels, "negativeNumbers"), ScriptJson.Text(labels, "type"),
                ScriptJson.Text(labels, "horizontal"),
                ScriptJson.Text(labels, "fontStyle"), ScriptJson.Text(labels, "underline"), ScriptJson.Text(labels, "underlineNone"),
                ScriptJson.Text(labels, "underlineSingle"), ScriptJson.Text(labels, "strikethrough"), ScriptJson.Text(labels, "normalFont"),
                ScriptJson.Text(labels, "colour"), ScriptJson.Text(labels, "lineStyle"), ScriptJson.Text(labels, "presets"),
                ScriptJson.Text(labels, "border"), ScriptJson.Text(labels, "preview"), ScriptJson.Text(labels, "moreColours"),
                ScriptJson.Text(labels, "ok"), ScriptJson.Text(labels, "cancel")),
            [.. json.GetProperty("tabs").EnumerateArray().Select(tab => new SheetFormatCellsChoice(
                ScriptJson.Text(tab, "value"), ScriptJson.Text(tab, "label"), false, true, null))],
            Nullable(json, "refusal"),
            new SheetFormatCellsNumber(
                Choices(number.GetProperty("categories")),
                Nullable(number, "hint"),
                number.GetProperty("takesDecimalPlaces").GetBoolean(),
                ScriptJson.Present(number, "thousandsSeparator", out var separator) ? separator.GetBoolean() : null,
                Nullable(number, "currencySymbol"),
                Choices(number.GetProperty("negativeNumbers")),
                number.GetProperty("takesCode").GetBoolean(),
                Nullable(number, "codeRefusal"),
                Nullable(number, "typesLegend"),
                Choices(number.GetProperty("types"))),
            Choices(json.GetProperty("alignments")),
            new SheetFormatCellsFont(
                Choices(font.GetProperty("styles")),
                font.GetProperty("underline").GetBoolean(),
                font.GetProperty("strikethrough").GetBoolean(),
                font.GetProperty("normalFont").GetBoolean(),
                Palette(font.GetProperty("palette"), swatches)),
            new SheetFormatCellsBorder(
                [.. border.GetProperty("lineStyles").EnumerateArray().Select(column => (IReadOnlyList<SheetFormatCellsLineStyle>)[
                    .. column.EnumerateArray().Select(style => new SheetFormatCellsLineStyle(
                        ScriptJson.Text(style, "value"), ScriptJson.Text(style, "label"), style.GetProperty("selected").GetBoolean(),
                        ScriptJson.Text(style, "width"), Nullable(style, "dashes")))])],
                Choices(border.GetProperty("presets")),
                [.. border.GetProperty("edges").EnumerateArray().Select(edge => new SheetFormatCellsEdge(
                    ScriptJson.Text(edge, "value"), ScriptJson.Text(edge, "label"), ScriptJson.Text(edge, "pressed"),
                    edge.GetProperty("enabled").GetBoolean(), edge.GetProperty("inPreview").GetBoolean(),
                    ScriptJson.Present(edge, "line", out var line)
                        ? new SheetFormatCellsLine(line.GetProperty("none").GetBoolean(), ScriptJson.Text(line, "paint"), ScriptJson.Text(line, "width"), Nullable(line, "dashes"))
                        : null))],
                Palette(border.GetProperty("palette"), swatches)),
            Palette(json.GetProperty("fill"), swatches));
    }

    /// <summary>A list of Format Cells' choices.</summary>
    private static IReadOnlyList<SheetFormatCellsChoice> Choices(JsonElement json)
        => [.. json.EnumerateArray().Select(choice => new SheetFormatCellsChoice(
            ScriptJson.Text(choice, "value"), ScriptJson.Text(choice, "label"), choice.GetProperty("selected").GetBoolean(),
            choice.GetProperty("enabled").GetBoolean(), Nullable(choice, "note")))];

    /// <summary>A palette of Format Cells: the swatches the script sends once for the three, and the one this palette has
    /// chosen, by its number.</summary>
    private static SheetFormatCellsPalette Palette(JsonElement json, IReadOnlyList<IReadOnlyList<(int Index, string Name, string Paint)>> swatches)
    {
        int? selected = ScriptJson.Present(json, "selected", out var at) ? at.GetInt32() : null;
        return new(
            ScriptJson.Text(json, "target"), ScriptJson.Text(json, "legend"), ScriptJson.Text(json, "first"), json.GetProperty("firstSelected").GetBoolean(),
            [.. swatches.Select(row => (IReadOnlyList<SheetFormatCellsSwatch>)[
                .. row.Select(swatch => new SheetFormatCellsSwatch(swatch.Index, swatch.Name, swatch.Paint, swatch.Index == selected))])]);
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
        var fragment = (name, view) switch
        {
            (SheetScriptSlots.ToolbarItem, SheetToolbarItemView item) => sheet.ToolbarItem(item),
            (SheetScriptSlots.SheetTab, SheetTabContext tab) => sheet.SheetTab(tab),
            (SheetScriptSlots.SheetTabAdd, AddFace) => sheet.SheetTabAdd(),
            (SheetScriptSlots.FormatCells, SheetFormatCellsView formatCells) => sheet.FormatCells(formatCells),
            _ => throw new InvalidOperationException($"Internal: the slot {name} holds a view of another slot's."),
        };
        // A slot the Chrome names is the Chrome's alone: drawn as nothing, it would stand empty and say nothing.
        return fragment ?? throw new InvalidOperationException(
            $"{sheet.GetType().Name} names {name} in its SheetScriptSlots but draws nothing for it: answer a fragment for every view of a slot it names, or leave the slot out of SheetScriptSlots.");
    }

    /// <summary>The add button's face, which is drawn from nothing.</summary>
    private sealed class AddFace
    {
        internal static readonly AddFace Instance = new();
    }
}
