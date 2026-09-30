using ExPivot.Engine;

namespace ExPivot;

/// <summary>
/// The ids of ExPivot's commands (ADR-0058/0060): those of a placed field's menu and those it
/// appends to the report's Context Menu. Each id is also the id of its word in
/// <see cref="PivotWords"/>, so a Consumer replaces a command's name through ExPivot's
/// <c>Label</c> function. A Chrome keys an icon on them.
/// </summary>
public static class PivotCommandIds
{
    /// <summary>Move the entry one place up.</summary>
    public const string MoveUp = "move-up";

    /// <summary>Move the entry one place down.</summary>
    public const string MoveDown = "move-down";

    /// <summary>Move the entry to the beginning of its Area.</summary>
    public const string MoveToBeginning = "move-to-beginning";

    /// <summary>Move the entry to the end of its Area.</summary>
    public const string MoveToEnd = "move-to-end";

    /// <summary>Move the entry to Filters.</summary>
    public const string MoveToFilters = "move-to-filters";

    /// <summary>Move the entry to Rows.</summary>
    public const string MoveToRows = "move-to-rows";

    /// <summary>Move the entry to Columns.</summary>
    public const string MoveToColumns = "move-to-columns";

    /// <summary>Move the entry to Values.</summary>
    public const string MoveToValues = "move-to-values";

    /// <summary>Remove the entry.</summary>
    public const string RemoveField = "remove-field";

    /// <summary>Open Field Settings….</summary>
    public const string FieldSettings = "field-settings";

    /// <summary>Open Value Field Settings….</summary>
    public const string ValueFieldSettings = "value-field-settings";

    /// <summary>Order the field's Items A to Z.</summary>
    public const string SortAscending = "sort-ascending";

    /// <summary>Order the field's Items Z to A.</summary>
    public const string SortDescending = "sort-descending";

    /// <summary>Open Filter….</summary>
    public const string FilterItems = "filter-items";

    /// <summary>Expand every Item of the field.</summary>
    public const string ExpandField = "expand-field";

    /// <summary>Collapse every Item of the field.</summary>
    public const string CollapseField = "collapse-field";

    /// <summary>Expand the Item the row stands for.</summary>
    public const string Expand = "expand";

    /// <summary>Collapse the Item the row stands for.</summary>
    public const string Collapse = "collapse";

    /// <summary>Show the records behind the cell (ADR-0062).</summary>
    public const string ShowDetails = "show-details";

    /// <summary>Order the row field's Items by the cell's Value Field, smallest first.</summary>
    public const string SortSmallestToLargest = "sort-smallest-to-largest";

    /// <summary>Order the row field's Items by the cell's Value Field, largest first.</summary>
    public const string SortLargestToSmallest = "sort-largest-to-smallest";

    /// <summary>Remove the field the cell is labelled by. The Context Menu's command id is this, a
    /// colon and the field's caption — <c>remove-named-field:Region</c> — so that a Chrome that
    /// words commands itself can say <c>Remove "Region"</c> (<see cref="CaptionOfRemove"/>); its word
    /// is <c>Remove "{0}"</c>.</summary>
    public const string RemoveNamedField = "remove-named-field";

    /// <summary>The field caption a <see cref="RemoveNamedField"/> command id carries, or null for any
    /// other id.</summary>
    public static string? CaptionOfRemove(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return id.StartsWith(RemoveNamedField + ":", StringComparison.Ordinal) ? id[(RemoveNamedField.Length + 1)..] : null;
    }

    /// <summary>Show the Field List.</summary>
    public const string ShowFieldList = "show-field-list";

    /// <summary>Hide the Field List.</summary>
    public const string HideFieldList = "hide-field-list";
}

/// <summary>
/// The records behind one cell of the report — Show Details (ADR-0062) — raised to the Consumer,
/// who shows them where its application shows records. The Items say which cell they are behind.
/// </summary>
/// <typeparam name="TRecord">The Consumer's record type.</typeparam>
/// <param name="Records">The Source Records, in their order in the snapshot; none hidden.</param>
/// <param name="RowItems">The row's Items, outermost first: each field's caption and the Item's label.</param>
/// <param name="ColumnItems">The column's Items, outermost first.</param>
/// <param name="ValueField">The Value Field's caption, or null for a label cell.</param>
public sealed record PivotDetails<TRecord>(
    IReadOnlyList<TRecord> Records,
    IReadOnlyList<PivotDetailItem> RowItems,
    IReadOnlyList<PivotDetailItem> ColumnItems,
    string? ValueField);

/// <summary>One Item a detailed cell stands for.</summary>
/// <param name="Field">The field's caption.</param>
/// <param name="Item">The Item's label.</param>
public sealed record PivotDetailItem(string Field, string Item);
