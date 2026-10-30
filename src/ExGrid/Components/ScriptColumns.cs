using System.Text.Json;
using ExGrid.Columns;

namespace ExGrid.Components;

/// <summary>
/// A column as it crosses to the Script Core (ADR-0350 section 16): its declaration, which the core reads as a
/// plain column of its own, and nothing of its behaviour, which stays in .NET. A column's values and its format
/// are read in .NET and sent with each row, as are its tones, and its verdict is asked of .NET as a commit is made (ADR-0380).
/// </summary>
internal static class ScriptColumns
{
    /// <summary>Whether any column formats its values in .NET, so each row carries its texts.</summary>
    internal static bool AnyFormatted<TRow>(IReadOnlyList<GridColumn<TRow>> columns)
    {
        foreach (var column in columns)
        {
            if (column.Format is not null)
                return true;
        }
        return false;
    }

    /// <summary>Refuses a list that holds no column where one should stand.</summary>
    internal static void Check<TRow>(IReadOnlyList<GridColumn<TRow>> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        foreach (var column in columns)
            ArgumentNullException.ThrowIfNull(column);
    }

    /// <summary>Writes the columns as the bridge reads them (<c>WireColumn</c>). A formatted column's <c>format</c>
    /// names its format and <paramref name="culture"/>: another format, or the same in another culture, writes
    /// other texts, and the bridge takes them as a new column's (ADR-0006).</summary>
    internal static void Write<TRow>(Utf8JsonWriter json, IReadOnlyList<GridColumn<TRow>> columns, FormatNames formats, string culture)
    {
        json.WriteStartArray();
        foreach (var column in columns)
        {
            json.WriteStartObject();
            json.WriteString("name", column.Name);
            json.WriteString("type", ScriptJson.Camel(column.Type));
            json.WriteString("header", column.Header);
            json.WritePropertyName("width");
            WriteWidth(json, column.Width);
            json.WriteBoolean("editable", column.Editable);
            json.WriteString("align", ScriptJson.Camel(column.Align));
            json.WriteString("headerAlign", ScriptJson.Camel(column.HeaderAlign));
            json.WriteString("filterUi", ScriptJson.Camel(column.FilterUi));
            json.WriteString("dateType", ScriptJson.Camel(column.DateType));
            json.WriteBoolean("queryable", column.IsQueryable);
            if (column.Format is { } format)
                json.WriteString("format", FormattableString.Invariant($"{formats.NumberOf(format)} {culture}"));
            else
                json.WriteNull("format");
            // The browser asks .NET for the verdict, now or later alike: the commit is pending until it answers (ADR-0380).
            if (column.Validate is not null || column.ValidateAsync is not null)
                json.WriteBoolean("validates", true);
            // Each cell's content is the column's fragment, drawn in .NET from the row it holds and moved into the cell
            // by the script (ADR-0020, ADR-0410 section 6).
            if (column.Template is not null)
                json.WriteBoolean("template", true);
            // Each row carries the tone of each toned value, as it carries a formatted value's text (ADR-0006).
            if (column.Tone is not null)
                json.WriteBoolean("tone", true);
            if (column.IsMarkColumn)
                json.WriteBoolean("mark", true);
            if (column.Actions.Count > 0)
            {
                json.WriteStartArray("actions");
                foreach (var action in column.Actions)
                {
                    json.WriteStartObject();
                    json.WriteString("name", action.Name);
                    json.WriteString("label", action.Label);
                    if (action.CssClass is { } className)
                        json.WriteString("className", className);
                    json.WriteEndObject();
                }
                json.WriteEndArray();
            }
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    private static void WriteWidth(Utf8JsonWriter json, ColumnWidthSpec width)
    {
        json.WriteStartObject();
        if (width.Width.IsAuto)
            json.WriteBoolean("auto", true);
        else
            json.WriteNumber("px", width.Width.FixedPx);
        json.WriteNumber("minPx", width.MinWidthPx);
        json.WriteNumber("maxPx", width.MaxWidthPx);
        json.WriteEndObject();
    }
}

/// <summary>A number for each format a grid's columns have used, the same while the delegate is the same.</summary>
internal sealed class FormatNames
{
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, object> _numbers = [];
    private int _last;

    internal int NumberOf(Delegate format) => (int)_numbers.GetValue(format, _ => ++_last);
}
