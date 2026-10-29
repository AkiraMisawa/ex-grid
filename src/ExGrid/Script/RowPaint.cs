using System.Text.Json;
using ExGrid.Cells;
using ExGrid.Rows;

namespace ExGrid.Script;

/// <summary>
/// What a Blazor ExGrid's Consumer answers of a row it paints, read in .NET and sent with the row (ADR-0440): the
/// Script Core asks each of these as it paints, in the browser, where a .NET function cannot answer without a round trip
/// per cell. A row whose answers change is another object in the browser, so the core paints it again (ADR-0003).
/// </summary>
internal sealed record RowPaint<TRow>(
    CellStateOf<TRow>? CellState, Func<TRow, RowKind>? RowKind, CellAppearanceOf<TRow>? Appearance, CellChangeOf<TRow>? ChangedAt, IRowMarks<TRow>? Marks)
{
    /// <summary>Whether every answer is the same function as <paramref name="other"/>'s, by identity: another delegate
    /// is another judgement, whatever its target and method (ADR-0003), as a <c>CellState</c> is.</summary>
    internal bool Same(RowPaint<TRow> other) => ReferenceEquals(CellState, other.CellState) && ReferenceEquals(RowKind, other.RowKind)
        && ReferenceEquals(Appearance, other.Appearance) && ReferenceEquals(ChangedAt, other.ChangedAt) && ReferenceEquals(Marks, other.Marks);

    /// <summary>Writes the row's kind where it is not Detail, whether it is marked, each cell's appearance where any is
    /// other than none, and when each cell changed where any did.</summary>
    internal void WriteRow(Utf8JsonWriter json, TRow row, IReadOnlyList<GridColumn<TRow>> columns)
    {
        var kind = RowKind?.Invoke(row) ?? Rows.RowKind.Detail;
        if (kind is not (Rows.RowKind.Detail or Rows.RowKind.Group or Rows.RowKind.Total))
            throw new ArgumentOutOfRangeException(nameof(row), kind, "RowKind answered a kind the grid does not know (ADR-0024).");
        if (kind != Rows.RowKind.Detail)
            json.WriteString("r", ScriptJson.Camel(kind));
        if (Marks is { } marks)
            json.WriteBoolean("m", kind == Rows.RowKind.Detail && marks.IsMarked(row));
        if (Appearance is { } appearance)
            WriteAppearances(json, row, columns, appearance);
        if (ChangedAt is { } changedAt)
            WriteChangeTimes(json, row, columns, changedAt);
    }

    // Left out where every cell is none, as the grid paints a row the Consumer answers nothing for.
    private static void WriteAppearances(Utf8JsonWriter json, TRow row, IReadOnlyList<GridColumn<TRow>> columns, CellAppearanceOf<TRow> appearanceOf)
    {
        CellAppearance[]? appearances = null;
        for (var c = 0; c < columns.Count; c++)
        {
            var appearance = appearanceOf(row, columns[c]);
            if (appearance != CellAppearance.None)
                appearances ??= new CellAppearance[columns.Count];
            if (appearances is not null)
                appearances[c] = appearance;
        }
        if (appearances is null)
            return;
        json.WriteStartArray("a");
        foreach (var appearance in appearances)
        {
            if (appearance == CellAppearance.None)
                json.WriteNullValue();
            else
                WriteAppearance(json, appearance);
        }
        json.WriteEndArray();
    }

    /// <summary>An appearance as the Script Core's <c>CellAppearance</c> holds it: only what is set.</summary>
    internal static void WriteAppearance(Utf8JsonWriter json, CellAppearance appearance)
    {
        json.WriteStartObject();
        if (appearance.FontColour is { } font)
            json.WriteNumber("fontColour", font.Rgb);
        if (appearance.Bold)
            json.WriteBoolean("bold", true);
        if (appearance.Italic)
            json.WriteBoolean("italic", true);
        if (appearance.Underline)
            json.WriteBoolean("underline", true);
        if (appearance.Strikethrough)
            json.WriteBoolean("strikethrough", true);
        if (appearance.Fill is { } fill)
            json.WriteNumber("fill", fill.Rgb);
        WriteBorder(json, "top", appearance.Top);
        WriteBorder(json, "right", appearance.Right);
        WriteBorder(json, "bottom", appearance.Bottom);
        WriteBorder(json, "left", appearance.Left);
        json.WriteEndObject();
    }

    private static void WriteBorder(Utf8JsonWriter json, string side, Border border)
    {
        if (border.IsNone)
            return;
        json.WriteStartObject(side);
        json.WriteString("style", ScriptJson.Camel(border.Style));
        json.WriteNumber("colour", border.Colour.Rgb);
        json.WriteEndObject();
    }

    // Left out where no cell changed lately. A time is milliseconds since the epoch on .NET's clock, which the state's
    // own reading of that clock lets the browser read on its own (ADR-0068).
    private static void WriteChangeTimes(Utf8JsonWriter json, TRow row, IReadOnlyList<GridColumn<TRow>> columns, CellChangeOf<TRow> changedAt)
    {
        long?[]? times = null;
        for (var c = 0; c < columns.Count; c++)
        {
            if (changedAt(row, columns[c]) is not { } at)
                continue;
            times ??= new long?[columns.Count];
            times[c] = at.ToUnixTimeMilliseconds();
        }
        if (times is null)
            return;
        json.WriteStartArray("h");
        foreach (var time in times)
        {
            if (time is { } at)
                json.WriteNumberValue(at);
            else
                json.WriteNullValue();
        }
        json.WriteEndArray();
    }
}
