using System.Globalization;

namespace ExPivot.Engine;

/// <summary>
/// The words ExPivot paints, by id, with their English (ADR-0059). A Consumer replaces any of
/// them through a function from id to text, returning null to keep the English. A word with
/// <c>{0}</c> (and <c>{1}</c>) is a template: the id says what goes in it.
/// </summary>
public static class PivotWords
{
    /// <summary>The Compact form's label column header: <c>Row Labels</c>.</summary>
    public const string RowLabels = "row-labels";

    /// <summary>The Σ Values pseudo-field's header, and a report's lone value column when the
    /// values stand in rows: <c>Values</c>.</summary>
    public const string Values = "values";

    /// <summary>The grand total row and column: <c>Grand Total</c>.</summary>
    public const string GrandTotal = "grand-total";

    /// <summary>An outer Item's subtotal: <c>{0} Total</c>, {0} the Item's label.</summary>
    public const string ItemTotal = "item-total";

    /// <summary>A Value Field's grand total row, with the values in rows: <c>Total {0}</c>, {0} its caption.</summary>
    public const string TotalOf = "total-of";

    /// <summary>An outer Item's subtotal row for one Value Field: <c>{0} {1}</c>, {0} the Item's
    /// label and {1} the caption.</summary>
    public const string ItemValue = "item-value";

    /// <summary>The Blank Item: <c>(blank)</c>.</summary>
    public const string Blank = "blank";

    /// <summary>A report filter with nothing hidden: <c>(All)</c>.</summary>
    public const string All = "all";

    /// <summary>A report filter showing several Items: <c>(Multiple Items)</c>.</summary>
    public const string MultipleItems = "multiple-items";

    /// <summary>The Σ Values entry in an Area: <c>Σ Values</c>.</summary>
    public const string ValuesPseudoField = "values-pseudo-field";

    /// <summary>The id of an Aggregation's name — <c>aggregation-sum</c> is <c>Sum</c>.</summary>
    public static string AggregationName(PivotAggregation aggregation) => "aggregation-" + Slug(aggregation);

    /// <summary>The id of a Value Field's default caption — <c>caption-sum</c> is
    /// <c>Sum of {0}</c>, {0} the field's caption.</summary>
    public static string CaptionOf(PivotAggregation aggregation) => "caption-" + Slug(aggregation);

    /// <summary>The id of a Show Values As choice's name — <c>show-percent-of-grand-total</c> is
    /// <c>% of Grand Total</c>.</summary>
    public static string ShowValuesAsName(PivotShowValuesAs showAs) => showAs switch
    {
        PivotShowValuesAs.NoCalculation => "show-no-calculation",
        PivotShowValuesAs.PercentOfGrandTotal => "show-percent-of-grand-total",
        PivotShowValuesAs.PercentOfColumnTotal => "show-percent-of-column-total",
        PivotShowValuesAs.PercentOfRowTotal => "show-percent-of-row-total",
        _ => throw new ArgumentOutOfRangeException(nameof(showAs), showAs, "Unknown PivotShowValuesAs."),
    };

    /// <summary>The English for an id, or null for an id this class does not know.</summary>
    public static string? EnglishFor(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return English.TryGetValue(id, out var word) ? word : null;
    }

    /// <summary>The word for <paramref name="id"/>: the Consumer's through
    /// <paramref name="label"/>, the English where it says nothing, and the id itself where
    /// neither knows it — an id painted is a missing word seen, never a blank.</summary>
    public static string Resolve(string id, Func<string, string?>? label)
        => label?.Invoke(id) ?? EnglishFor(id) ?? id;

    /// <summary>A template word with its arguments put in, under the invariant culture — the
    /// arguments are already text.</summary>
    public static string Fill(string template, params string[] arguments)
        => string.Format(CultureInfo.InvariantCulture, template, arguments);

    private static string Slug(PivotAggregation aggregation) => aggregation switch
    {
        PivotAggregation.Sum => "sum",
        PivotAggregation.Count => "count",
        PivotAggregation.Average => "average",
        PivotAggregation.Max => "max",
        PivotAggregation.Min => "min",
        PivotAggregation.Product => "product",
        PivotAggregation.CountNumbers => "count-numbers",
        PivotAggregation.StdDev => "stddev",
        PivotAggregation.StdDevp => "stddevp",
        PivotAggregation.Var => "var",
        PivotAggregation.Varp => "varp",
        _ => throw new ArgumentOutOfRangeException(nameof(aggregation), aggregation, "Unknown PivotAggregation."),
    };

    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        [RowLabels] = "Row Labels",
        [Values] = "Values",
        [GrandTotal] = "Grand Total",
        [ItemTotal] = "{0} Total",
        [TotalOf] = "Total {0}",
        [ItemValue] = "{0} {1}",
        [Blank] = "(blank)",
        [All] = "(All)",
        [MultipleItems] = "(Multiple Items)",
        [ValuesPseudoField] = "Σ Values",

        ["aggregation-sum"] = "Sum",
        ["aggregation-count"] = "Count",
        ["aggregation-average"] = "Average",
        ["aggregation-max"] = "Max",
        ["aggregation-min"] = "Min",
        ["aggregation-product"] = "Product",
        ["aggregation-count-numbers"] = "Count Numbers",
        ["aggregation-stddev"] = "StdDev",
        ["aggregation-stddevp"] = "StdDevp",
        ["aggregation-var"] = "Var",
        ["aggregation-varp"] = "Varp",

        ["caption-sum"] = "Sum of {0}",
        ["caption-count"] = "Count of {0}",
        ["caption-average"] = "Average of {0}",
        ["caption-max"] = "Max of {0}",
        ["caption-min"] = "Min of {0}",
        ["caption-product"] = "Product of {0}",
        ["caption-count-numbers"] = "Count Numbers of {0}",
        ["caption-stddev"] = "StdDev of {0}",
        ["caption-stddevp"] = "StdDevp of {0}",
        ["caption-var"] = "Var of {0}",
        ["caption-varp"] = "Varp of {0}",

        ["show-no-calculation"] = "No Calculation",
        ["show-percent-of-grand-total"] = "% of Grand Total",
        ["show-percent-of-column-total"] = "% of Column Total",
        ["show-percent-of-row-total"] = "% of Row Total",

        // The Field List and its panels (ADR-0060).
        ["field-list"] = "PivotTable Fields",
        ["choose-fields"] = "Choose fields to add to report:",
        ["drag-fields"] = "Drag fields between areas below:",
        ["search-fields"] = "Search",
        ["area-filters"] = "Filters",
        ["area-columns"] = "Columns",
        ["area-rows"] = "Rows",
        ["area-values"] = "Values",
        ["field-menu"] = "Options for {0}",
        ["filtered"] = "Filtered",
        ["drop-here"] = "Drop here",
        ["move-up"] = "Move Up",
        ["move-down"] = "Move Down",
        ["move-to-beginning"] = "Move to Beginning",
        ["move-to-end"] = "Move to End",
        ["move-to-filters"] = "Move to Report Filter",
        ["move-to-rows"] = "Move to Row Labels",
        ["move-to-columns"] = "Move to Column Labels",
        ["move-to-values"] = "Move to Values",
        ["remove-field"] = "Remove Field",
        ["field-settings"] = "Field Settings…",
        ["value-field-settings"] = "Value Field Settings…",
        ["sort-ascending"] = "Sort A to Z",
        ["sort-descending"] = "Sort Z to A",
        ["filter-items"] = "Filter…",
        ["expand-field"] = "Expand Entire Field",
        ["collapse-field"] = "Collapse Entire Field",
        ["ok"] = "OK",
        ["cancel"] = "Cancel",
        ["select-all"] = "(Select All)",
        ["search-items"] = "Search",
        ["too-many-items"] = "More than {0} items. Search to narrow the list.",
        ["no-items-match"] = "No items match.",
        ["custom-name"] = "Custom Name",
        ["summarize-by"] = "Summarize value field by",
        ["show-values-as"] = "Show values as",
        ["number-format"] = "Number format",
        ["number-format-general"] = "General",
        ["sample"] = "Sample: {0}",
        ["subtotals"] = "Subtotals",
        ["subtotals-automatic"] = "Automatic",
        ["subtotals-none"] = "None",
        ["sort-order"] = "Sort",
        ["sort-label-ascending"] = "Ascending (A to Z) by label",
        ["sort-label-descending"] = "Descending (Z to A) by label",
        ["sort-value-ascending"] = "Ascending by {0}",
        ["sort-value-descending"] = "Descending by {0}",
        ["refused-hides-every-item"] = "Select at least one item.",
        ["refused-caption-taken"] = "PivotTable field name already exists.",
        ["refused-caption-empty"] = "Enter a name.",
        ["refused-number-format"] = "This number format cannot be used.",
        ["empty-report"] = "To build a report, choose fields from the PivotTable Fields list.",
        ["report-filters"] = "Report filters",
        ["filter-of"] = "Filter {0}",

        // The report's Context Menu and its label cells (ADR-0058/0062).
        ["expand"] = "Expand",
        ["collapse"] = "Collapse",
        ["expand-item"] = "Expand {0}",
        ["collapse-item"] = "Collapse {0}",
        ["show-details"] = "Show Details",
        ["sort-smallest-to-largest"] = "Sort Smallest to Largest",
        ["sort-largest-to-smallest"] = "Sort Largest to Smallest",
        ["remove-named-field"] = "Remove \"{0}\"",
        ["show-field-list"] = "Show Field List",
        ["hide-field-list"] = "Hide Field List",
    };
}
