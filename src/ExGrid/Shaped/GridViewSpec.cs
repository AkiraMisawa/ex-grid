using System.Text.Json;
using System.Text.Json.Nodes;
using ExGrid.Columns;
using ExGrid.Data;

namespace ExGrid;

/// <summary>
/// A grid view's spec read (ADR-0342): the input it shows, the columns in display order, and the
/// View State it opens under.
/// </summary>
/// <param name="Data">The Board Input the view shows.</param>
/// <param name="Columns">The columns shown, in display order.</param>
/// <param name="ViewState">The widths, the pinning, the sort and the filter, in the View State's form (ADR-0207).</param>
public sealed record GridViewSpecReading(string Data, IReadOnlyList<ShapedColumn> Columns, GridViewState ViewState);

/// <summary>One column of a grid built from a source's shape (ADR-0342).</summary>
/// <param name="Column">The source's column it shows.</param>
/// <param name="Kind">The column's kind, as the input declares it.</param>
/// <param name="Header">The header; the column's name when the spec gives none.</param>
/// <param name="Format">The Excel format code the spec writes, in the invariant spelling (ADR-0120);
/// null where the kind's default applies: an integer <c>#,##0</c>, a date <c>yyyy-mm-dd</c>, text and
/// a boolean as they are. A decimal or a double column always carries one.</param>
public sealed record ShapedColumn(string Column, SnapshotKind Kind, string Header, string? Format);

/// <summary>
/// The grid's view in a Board (ADR-0342), as an AI writes it:
/// <c>{"data": "trades", "columns": [{"column": "notional", "header": "Notional", "format": "#,##0.00",
/// "width": 120, "pinned": true}, …], "sorts": […], "filter": {…}}</c>. A column's <c>width</c> and
/// <c>pinned</c>, the <c>sorts</c> and the <c>filter</c> are the View State's JSON
/// (<see cref="GridViewStateJson"/>), read by the same reader.
/// <para>What reading refuses, with a <see cref="BoardRefusedException"/> naming the path: a member
/// the spec does not have; an input the Board does not declare; a column the input does not declare,
/// or one shown twice; a decimal or double column with no format — the places are the composer's to
/// choose, knowing whether it is yen, basis points or an FX rate, never guessed from the type; a format
/// outside the subset a grid shows, or a date format on a number and the reverse; a format on text or
/// a boolean; and whatever the View State's reader refuses.</para>
/// </summary>
public static class GridViewSpec
{
    private static readonly string[] SpecKeys = ["data", "columns", "sorts", "filter"];
    private static readonly string[] ColumnKeys = ["column", "header", "format", "width", "pinned"];

    /// <summary>Reads <paramref name="json"/>, taking each input's shape from <paramref name="inputs"/>,
    /// which returns null for an input the Board does not declare.</summary>
    /// <exception cref="BoardRefusedException">The spec cannot be drawn; the message says where and why.</exception>
    public static GridViewSpecReading Read(string json, Func<string, DataShape?> inputs)
    {
        ArgumentNullException.ThrowIfNull(json);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowDuplicateProperties = false });
        }
        catch (JsonException e)
        {
            throw new BoardRefusedException("", $"The grid's spec is not JSON: {e.Message}");
        }
        using (document)
            return Read(document.RootElement, inputs, "");
    }

    /// <summary>Reads <paramref name="spec"/>, standing at <paramref name="path"/> in a Board, such as
    /// <c>views[0].grid</c>.</summary>
    /// <exception cref="BoardRefusedException">The spec cannot be drawn; the message says where and why.</exception>
    public static GridViewSpecReading Read(JsonElement spec, Func<string, DataShape?> inputs, string path)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(path);
        string At(string member) => path.Length == 0 ? member : $"{path}.{member}";
        if (spec.ValueKind != JsonValueKind.Object)
            throw new BoardRefusedException(path, "A grid's spec is an object.");
        foreach (var member in spec.EnumerateObject())
        {
            if (Array.IndexOf(SpecKeys, member.Name) < 0)
                throw new BoardRefusedException(At(member.Name), $"A grid's spec has no member '{member.Name}'.");
        }
        if (!spec.TryGetProperty("data", out var dataValue) || dataValue.ValueKind != JsonValueKind.String || dataValue.GetString() is not { Length: > 0 } data)
            throw new BoardRefusedException(At("data"), "A grid names the input it shows in 'data'.");
        var shape = inputs(data) ?? throw new BoardRefusedException(At("data"), $"The grid shows input '{data}', which the Board does not declare.");
        if (!spec.TryGetProperty("columns", out var list) || list.ValueKind != JsonValueKind.Array || list.GetArrayLength() == 0)
            throw new BoardRefusedException(At("columns"), "A grid lists the columns it shows, in order, in 'columns'.");

        var columns = new List<ShapedColumn>();
        var viewColumns = new JsonArray();
        var i = 0;
        foreach (var column in list.EnumerateArray())
        {
            var at = $"{At("columns")}[{i++}]";
            if (column.ValueKind != JsonValueKind.Object)
                throw new BoardRefusedException(at, "A grid's column is an object.");
            foreach (var member in column.EnumerateObject())
            {
                if (Array.IndexOf(ColumnKeys, member.Name) < 0)
                    throw new BoardRefusedException($"{at}.{member.Name}", $"A grid's column has no member '{member.Name}'.");
            }
            if (!column.TryGetProperty("column", out var nameValue) || nameValue.ValueKind != JsonValueKind.String || nameValue.GetString() is not { Length: > 0 } name)
                throw new BoardRefusedException($"{at}.column", "A grid's column names the input's column it shows in 'column'.");
            if (!shape.TryGetColumn(name, out var declared))
                throw new BoardRefusedException($"{at}.column", $"The grid shows column '{name}', which input '{data}' does not declare.");
            var header = name;
            if (column.TryGetProperty("header", out var headerValue))
            {
                if (headerValue.ValueKind != JsonValueKind.String || headerValue.GetString() is not { Length: > 0 } written)
                    throw new BoardRefusedException($"{at}.header", $"Column '{name}': a header is text.");
                header = written;
            }
            string? format = null;
            if (column.TryGetProperty("format", out var formatValue))
            {
                if (formatValue.ValueKind != JsonValueKind.String)
                    throw new BoardRefusedException($"{at}.format", $"Column '{name}': a format is an Excel format code, as text.");
                format = formatValue.GetString()!;
            }
            CheckFormat(name, declared.Kind, format, $"{at}.format");
            columns.Add(new ShapedColumn(name, declared.Kind, header, format));
            viewColumns.Add(new JsonObject
            {
                ["column"] = name,
                ["width"] = column.TryGetProperty("width", out var width) ? JsonNode.Parse(width.GetRawText()) : "Auto",
                ["pinned"] = column.TryGetProperty("pinned", out var pinned) ? JsonNode.Parse(pinned.GetRawText()) : false,
            });
        }

        // The rest is the View State's, read by its own reader, so that a Board's grid and a saved view
        // can never mean two things.
        var view = new JsonObject { ["version"] = GridViewStateJson.Version, ["columns"] = viewColumns };
        view["sorts"] = spec.TryGetProperty("sorts", out var sorts) ? JsonNode.Parse(sorts.GetRawText()) : new JsonArray();
        if (spec.TryGetProperty("filter", out var filter))
            view["filter"] = JsonNode.Parse(filter.GetRawText());
        GridViewState state;
        try
        {
            state = GridViewStateJson.Read(view.ToJsonString(), shape.Columns.Select(c => c.Name));
        }
        catch (JsonException e)
        {
            var where = e.Path is { Length: > 1 } jsonPath && jsonPath.StartsWith("$.", StringComparison.Ordinal) ? jsonPath[2..] : "";
            throw new BoardRefusedException(where.Length == 0 ? path : At(where), e.Message);
        }
        foreach (var shown in state.Columns)
        {
            if (!shown.Width.IsAuto && shown.Width.FixedPx < ColumnWidthSpec.DefaultMinWidthPx)
            {
                throw new BoardRefusedException(At("columns"), FormattableString.Invariant(
                    $"Column '{shown.Column}': a width of {shown.Width.FixedPx} is below the least a column is drawn at ({ColumnWidthSpec.DefaultMinWidthPx})."));
            }
        }
        return new GridViewSpecReading(data, columns, state);
    }

    private static void CheckFormat(string name, SnapshotKind kind, string? format, string path)
    {
        if (format is null)
        {
            if (kind is SnapshotKind.Decimal or SnapshotKind.Double)
            {
                throw new BoardRefusedException(path, $"Column '{name}' is a {BoardInput.NameOf(kind)} and needs a format, such as \"#,##0.00\", "
                    + "\"0.00%\" or \"0.00000\": its places are chosen for what it holds, never guessed from the type (ADR-0342).");
            }
            return;
        }
        if (kind is SnapshotKind.Text or SnapshotKind.Boolean)
            throw new BoardRefusedException(path, $"Column '{name}' is {BoardInput.NameOf(kind)} and shows as it is; it takes no format.");
        if (!FormatCode.TryParse(format, out var code, out var reason))
            throw new BoardRefusedException(path, $"Column '{name}': the format code \"{format}\" {reason}.");
        if (code.IsDate != (kind == SnapshotKind.Date))
        {
            throw new BoardRefusedException(path, code.IsDate
                ? $"Column '{name}' is a number, and \"{format}\" formats dates."
                : $"Column '{name}' is a date, and \"{format}\" formats numbers.");
        }
    }
}
