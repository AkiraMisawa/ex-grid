namespace ExPivot.Engine;

/// <summary>
/// The type a Consumer declares for a Pivot Field (ADR-0059). It decides the defaults only —
/// where a ticked field goes, which Aggregation a new Value Field takes, and how an Item is
/// labelled. The kind of each Item comes from the data: over a Snapshot, from its column's kind
/// (ADR-0063); read through an untyped accessor, from each value, so a field whose values are not
/// all of the declared type is still pivoted as its values are.
/// </summary>
public enum PivotFieldType
{
    /// <summary>Text. A ticked field goes to Rows; a Value Field of it counts.</summary>
    Text = 0,

    /// <summary>A number. A ticked field goes to Values, and a Value Field of it sums.</summary>
    Number,

    /// <summary>A date. A ticked field goes to Rows; a Value Field of it counts.</summary>
    Date,

    /// <summary>A Boolean, labelled <c>TRUE</c> / <c>FALSE</c> as Excel labels it.</summary>
    Boolean,
}

/// <summary>The four places a Pivot Field stands, in Excel's words (ADR-0060).</summary>
public enum PivotArea
{
    /// <summary>The report filter: its fields filter the report and show above it.</summary>
    Filters = 0,

    /// <summary>Column Labels: its fields' Items become the value columns.</summary>
    Columns,

    /// <summary>Row Labels: its fields' Items become the report's rows.</summary>
    Rows,

    /// <summary>Values: each entry is a Value Field, aggregated where rows and columns cross.</summary>
    Values,
}

/// <summary>Where Σ Values stands, when there are two or more Value Fields (ADR-0059).</summary>
public enum PivotAxis
{
    /// <summary>Innermost in Columns — Excel's default.</summary>
    Columns = 0,

    /// <summary>Innermost in Rows.</summary>
    Rows,
}

/// <summary>How a Value Field summarises the records at a cell — Excel's "Summarize Values By"
/// (ADR-0059).</summary>
public enum PivotAggregation
{
    /// <summary>The sum of the numbers; <c>0</c> where there are values but none is a number.</summary>
    Sum = 0,

    /// <summary>How many values are not Blank, numbers or not — Excel's <c>COUNTA</c>.</summary>
    Count,

    /// <summary>The mean of the numbers; <c>#DIV/0!</c> where there are values but none is a number.</summary>
    Average,

    /// <summary>The largest number.</summary>
    Max,

    /// <summary>The smallest number.</summary>
    Min,

    /// <summary>The product of the numbers.</summary>
    Product,

    /// <summary>How many values are numbers — Excel's <c>COUNT</c>.</summary>
    CountNumbers,

    /// <summary>The sample standard deviation, of two or more numbers.</summary>
    StdDev,

    /// <summary>The population standard deviation.</summary>
    StdDevp,

    /// <summary>The sample variance, of two or more numbers.</summary>
    Var,

    /// <summary>The population variance.</summary>
    Varp,
}

/// <summary>How a Value Field's values are shown, after aggregation (ADR-0059). The first
/// version holds the three percentages of a total.</summary>
public enum PivotShowValuesAs
{
    /// <summary>The aggregated value itself.</summary>
    NoCalculation = 0,

    /// <summary>The value divided by the Value Field's grand total.</summary>
    PercentOfGrandTotal,

    /// <summary>The value divided by its column's total.</summary>
    PercentOfColumnTotal,

    /// <summary>The value divided by its row's total.</summary>
    PercentOfRowTotal,
}

/// <summary>How a Pivot Report sets out its row labels — Excel's "Report Layout" (ADR-0059).</summary>
public enum PivotReportForm
{
    /// <summary>One indented label column, a group row per outer Item. Excel's default.</summary>
    Compact = 0,

    /// <summary>A label column per row field, a group row per outer Item.</summary>
    Outline,

    /// <summary>A label column per row field, no group rows; subtotals at the bottom.</summary>
    Tabular,
}

/// <summary>Ascending or descending, for an Item order (ADR-0059).</summary>
public enum PivotSortDirection
{
    /// <summary>A to Z, smallest to largest; <c>(blank)</c> last.</summary>
    Ascending = 0,

    /// <summary>Z to A, largest to smallest; <c>(blank)</c> still last.</summary>
    Descending,
}

/// <summary>
/// The kind of an Item, which comes from its value (ADR-0059). The declaration order is the
/// ascending order across kinds: numbers, dates, text, Booleans, the error Item, then
/// <c>(blank)</c>.
/// </summary>
public enum PivotItemKind
{
    /// <summary>A number, told apart by its value.</summary>
    Number = 0,

    /// <summary>A date, told apart by its clock value.</summary>
    Date,

    /// <summary>Text, told apart ignoring case.</summary>
    Text,

    /// <summary><c>TRUE</c> or <c>FALSE</c>.</summary>
    Boolean,

    /// <summary>A number no one can hold — a non-finite <c>double</c> — shown as <c>#NUM!</c>.</summary>
    Error,

    /// <summary>No value: the Item <c>(blank)</c>.</summary>
    Blank,
}

/// <summary>What a row of a Pivot Report stands for (ADR-0058/0059). The component paints it
/// as ExGrid's Row Kind: an Item row as Detail, a group row as Group, the others as Total.</summary>
public enum PivotRowRole
{
    /// <summary>An Item of the innermost row field, or one Value Field's row under an Item.</summary>
    Item = 0,

    /// <summary>A row heading an outer Item's block — carrying its subtotal when subtotals are at
    /// the top — or a collapsed Item's row, carrying its totals.</summary>
    Group,

    /// <summary>An outer Item's subtotal row, at the bottom of its block.</summary>
    Subtotal,

    /// <summary>The grand total row.</summary>
    GrandTotal,
}

/// <summary>What a value column of a Pivot Report stands for (ADR-0059).</summary>
public enum PivotColumnRole
{
    /// <summary>An Item of the innermost column field, a collapsed Item, or the one column of a
    /// report with no column field.</summary>
    Item = 0,

    /// <summary>An outer column Item's subtotal.</summary>
    Subtotal,

    /// <summary>The grand total column.</summary>
    GrandTotal,
}
