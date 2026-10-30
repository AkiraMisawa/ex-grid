namespace ExGrid.Chrome;

/// <summary>
/// The ids of the commands the core defines (ADR-0010/0036/0044), in one place: the core
/// builds its menus with them, and a Chrome — the built-in one's labels, a Wrapper's words
/// and icons — resolves its rendering from them. A Consumer's own command carries an id of
/// its own choosing, which none of these is.
/// </summary>
public static class GridCommandIds
{
    /// <summary>Sorts the column ascending. Excel's S.</summary>
    public const string SortAscending = "sort-ascending";

    /// <summary>Sorts the column descending. Excel's O.</summary>
    public const string SortDescending = "sort-descending";

    /// <summary>Removes the column's filter; enabled only while it has one. Excel's C.</summary>
    public const string ClearFilter = "clear-filter";

    /// <summary>Hides the column.</summary>
    public const string Hide = "hide";

    /// <summary>Pins the columns up to this one.</summary>
    public const string Pin = "pin";

    /// <summary>Unpins all columns.</summary>
    public const string Unpin = "unpin";

    /// <summary>Sizes the column to fit what the Window holds (ADR-0016).</summary>
    public const string SizeToFit = "size-to-fit";

    /// <summary>The Context Menu's copy of the selection (ADR-0036).</summary>
    public const string Copy = "copy";

    /// <summary>The Context Menu's copy of the selection with its headers (ADR-0005/0036).</summary>
    public const string CopyWithHeaders = "copy-with-headers";
}
