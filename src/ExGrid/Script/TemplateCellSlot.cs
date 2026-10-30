using System.Text.Json;

namespace ExGrid.Script;

/// <summary>
/// A Template cell the script mounted (ADR-0020, ADR-0410 section 6): the row by the id the bridge knows it by, the
/// column, and the focus request Space made, which the column's fragment is drawn from in .NET and moved into the cell.
/// </summary>
/// <param name="Row">The row's id, which <c>ScriptRows</c> holds while the browser holds the row.</param>
/// <param name="Column">The column's name.</param>
/// <param name="FocusRequest">Non-zero on the one drawing Space asked the content to take the keyboard in (ADR-0037).</param>
internal sealed record TemplateCellSlot(long Row, string Column, int FocusRequest)
{
    /// <summary>The slot's name, as the script names it.</summary>
    public const string Name = "templateCell";

    /// <summary>Whether this cell's fragment was drawn once: the request is handed on that drawing, and zero on every
    /// later one (ADR-0037).</summary>
    public bool Drawn { get; set; }

    /// <summary>The cell the script's data name.</summary>
    public static TemplateCellSlot Of(string context)
    {
        using var document = JsonDocument.Parse(context);
        var json = document.RootElement;
        return new TemplateCellSlot(json.GetProperty("row").GetInt64(), ScriptJson.Text(json, "column"), json.GetProperty("focusRequest").GetInt32());
    }
}
