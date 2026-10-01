using System.Text;
using System.Text.Json;
using ExPivot.Engine;
using Microsoft.Data.Sqlite;

namespace ExGrid.DemoApi;

/// <summary>A statement's parameters, named <c>$p0</c>, <c>$p1</c>, … in the order added.</summary>
internal sealed class SqlParameters
{
    private readonly List<(string Name, object Value)> _values = [];

    /// <summary>Adds a value and returns the name the SQL refers to it by.</summary>
    public string Add(object value)
    {
        var name = "$p" + _values.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _values.Add((name, value));
        return name;
    }

    /// <summary>Gives the values to a command.</summary>
    public void AddTo(SqliteCommand command)
    {
        foreach (var (name, value) in _values)
            command.Parameters.AddWithValue(name, value);
    }
}

/// <summary>
/// The SQL the server's Pivot Source asks of SQLite, written by hand for <c>trades</c> (ADR-0065,
/// ADR-0068). Every field's SQL is the server's own (<see cref="TradePivotField.Sql"/>), never a
/// question's text: a question names fields, and only a declared field's name finds one.
/// <para>
/// <b>Text is compared as a pivot tells Items apart: ignoring case</b> (ADR-0059), so a Hidden Item
/// <c>usd</c> hides <c>USD</c>. The engine compares with <see cref="StringComparison.OrdinalIgnoreCase"/>,
/// which folds every letter that has a case; SQLite's <c>NOCASE</c> folds the 26 ASCII letters only.
/// No other character is equal to an ASCII one ignoring case, so the two agree whenever the Item's
/// text is ASCII, and whenever the stored text is. A condition on ASCII Items is therefore written
/// <c>COLLATE NOCASE</c>; one on an Item with another letter in it uses <see cref="TradeDatabase.ItemCollation"/>,
/// the engine's own comparison registered with SQLite, which is exact for any text and slower,
/// since SQLite calls back into .NET for every comparison.
/// </para>
/// <para>
/// <b>Groups are the stored values</b>, compared as stored (<c>GROUP BY Region</c>): measured at a
/// million trades, grouping three text fields <c>COLLATE NOCASE</c> took 2.0 s and as stored 1.4 s.
/// The groups are then folded into Items by the engine's own comparison (<see cref="PivotItemKey"/>
/// equality), and their parts merge exactly — counts and sums add, extremes compare (ADR-0065) —
/// so two spellings of one Item are one leaf, whatever the text.
/// </para>
/// </summary>
internal static class TradePivotSql
{
    /// <summary>
    /// The aggregate for <paramref name="query"/> (ADR-0065): one <c>GROUP BY</c> over the row and
    /// column fields, the Hidden Items as <c>WHERE</c>, <c>count(*)</c> for the records, and for
    /// each field in Values its count and the parts asked for. For P&amp;L by region and desk,
    /// across products, with a Blank and <c>usd</c> hidden among the currencies:
    /// <code>
    /// SELECT Region, Desk, Product, count(*), count(Pnl), sum(Pnl), min(Pnl), max(Pnl)
    /// FROM trades
    /// WHERE (Currency IS NOT NULL AND Currency COLLATE NOCASE NOT IN (SELECT value FROM json_each($p0)))
    /// GROUP BY Region, Desk, Product
    /// </code>
    /// A field that holds no numbers answers its count alone: its sum and extremes are 0, as the
    /// engine's are for text, dates and Booleans (ADR-0059). Money is summed as integer cents,
    /// exactly; <c>AVG</c> is never asked, since it answers in floating point, and ExPivot computes
    /// an Average from the sum and the count.
    /// </summary>
    public static (string Sql, SqlParameters Parameters) Aggregate(
        PivotQuery query, IReadOnlyList<TradePivotField> axis, IReadOnlyList<TradePivotField> values)
    {
        var parameters = new SqlParameters();
        var select = new List<string>(axis.Select(field => field.Sql)) { "count(*)" };
        for (var v = 0; v < values.Count; v++)
        {
            var field = values[v];
            select.Add($"count({field.Sql})");
            if (!field.IsNumber)
                continue;
            if ((query.Values[v].Parts & PivotParts.Sum) != 0)
                select.Add($"sum({field.Sql})");
            if ((query.Values[v].Parts & PivotParts.Extremes) != 0)
            {
                select.Add($"min({field.Sql})");
                select.Add($"max({field.Sql})");
            }
        }

        var sql = new StringBuilder();
        sql.Append("SELECT ").AppendJoin(", ", select).Append("\nFROM trades");
        AppendWhere(sql, query.Placed.Select(placed => Keeping(Field(placed.Field), placed.HiddenItems, parameters)));
        if (axis.Count > 0)
            sql.Append("\nGROUP BY ").AppendJoin(", ", axis.Select(field => field.Sql));
        return (sql.ToString(), parameters);
    }

    /// <summary>A field's Items over all the data (ADR-0065): its distinct stored values, which are
    /// folded into Items, searched and ordered in .NET (<see cref="TradePivotSource"/>).</summary>
    public static string Items(TradePivotField field) => $"SELECT DISTINCT {field.Sql} FROM trades";

    /// <summary>
    /// The <c>WHERE</c> of the records behind a cell (ADR-0062/0065): each Item of the cell's row
    /// and column paths, and no Hidden Item of any placed field. Empty for the grand total of a
    /// layout that hides nothing.
    /// </summary>
    public static string DetailsWhere(PivotDetailsQuery query, SqlParameters parameters)
    {
        var sql = new StringBuilder();
        AppendWhere(sql, query.RowItems.Concat(query.ColumnItems)
            .Select(step => Carrying(Field(step.Field), step.Item, parameters))
            .Concat(query.HiddenItems.Select(field => Keeping(Field(field.Field), field.HiddenItems, parameters))));
        return sql.ToString();
    }

    /// <summary>Every field's SQL, in <see cref="TradePivotFields.All"/>' order: a record behind a
    /// cell as the page shows it.</summary>
    public static string DetailColumns { get; } = string.Join(", ", TradePivotFields.All.Select(declared => declared.Sql));

    /// <summary>
    /// The records a field's Hidden Items leave in, or null when they leave every record in — an
    /// Item no stored value of the field can be hides nothing (<see cref="TradePivotField.TryStored"/>).
    /// A hidden Blank is <c>IS NOT NULL</c>. Hidden values are one parameter however many there are:
    /// SQLite's <c>json_each</c> lists a JSON array's values.
    /// <para>
    /// A Blank that is not hidden must be kept by name. <c>Currency NOT IN ('EUR')</c> is NULL for a
    /// Blank currency, and <c>WHERE</c> drops a NULL, so every Blank would leave the totals with
    /// nothing said: the condition is <c>Currency IS NULL OR Currency NOT IN (…)</c>.
    /// </para>
    /// </summary>
    public static string? Keeping(TradePivotField field, IReadOnlyList<PivotItemKey> hidden, SqlParameters parameters)
    {
        var values = new List<object>();
        var blank = false;
        foreach (var item in hidden)
        {
            if (!field.TryStored(item, out var stored))
                continue;
            if (stored is null)
                blank = true;
            else
                values.Add(stored);
        }
        if (values.Count == 0)
            return blank ? $"{field.Sql} IS NOT NULL" : null;
        var notIn = $"{field.Sql}{Collation(field, values)} NOT IN (SELECT value FROM json_each({parameters.Add(JsonSerializer.Serialize(values))}))";
        return blank ? $"({field.Sql} IS NOT NULL AND {notIn})" : $"({field.Sql} IS NULL OR {notIn})";
    }

    /// <summary>The records that carry <paramref name="item"/> in <paramref name="field"/>: <c>0</c>,
    /// matching none, when no stored value of the field can be that Item.</summary>
    public static string Carrying(TradePivotField field, PivotItemKey item, SqlParameters parameters)
    {
        if (!field.TryStored(item, out var stored))
            return "0";
        if (stored is null)
            return $"{field.Sql} IS NULL";
        return $"{field.Sql}{Collation(field, [stored])} = {parameters.Add(stored)}";
    }

    // Text is compared ignoring case: NOCASE where it is the engine's comparison, the engine's own
    // registered as a collation where an Item holds a letter outside ASCII.
    private static string Collation(TradePivotField field, IReadOnlyList<object> values)
    {
        if (field.Kind != TradeValueKind.Text)
            return "";
        return values.All(value => System.Text.Ascii.IsValid((string)value))
            ? " COLLATE NOCASE"
            : " COLLATE " + TradeDatabase.ItemCollation;
    }

    private static void AppendWhere(StringBuilder sql, IEnumerable<string?> conditions)
    {
        var kept = conditions.OfType<string>().ToList();
        if (kept.Count > 0)
            sql.Append("\nWHERE ").AppendJoin("\n  AND ", kept);
    }

    private static TradePivotField Field(string name) =>
        TradePivotFields.Find(name) ?? throw new InvalidOperationException($"'{name}' was asked for without being checked; the source refuses an unknown field first.");
}
