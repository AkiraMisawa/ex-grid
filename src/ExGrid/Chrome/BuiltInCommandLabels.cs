namespace ExGrid.Chrome;

/// <summary>
/// The built-in Chrome's wording for the commands the core defines
/// ([ADR-0036](../../docs/adr/0036-the-context-menu-is-the-column-menu-shape-over-a-selection.md)).
///
/// These strings live in this assembly and belong to the **default Chrome**, not to the
/// core's decisions: the core says which commands exist by `Id`, and what they are
/// called is rendering. A substituted Chrome resolves its own wording from the same
/// ids and never sees this table; a Consumer keeping the built-in menu replaces it a
/// command at a time through <c>ExGrid.CommandLabel</c>, which is what makes the menu
/// translatable without reimplementing it.
///
/// English is the default because a default has to be something, not because the grid
/// has an opinion about the reader's language.
/// </summary>
public static class BuiltInCommandLabels
{
    /// <summary>The wording for a command id, or the id itself when it is one this
    /// table does not know — a Consumer's own command reaching the built-in menu
    /// without a label is shown by name rather than blank, which says what happened.</summary>
    public static string For(string id) => id switch
    {
        "sort-ascending" => "Sort ascending",
        "sort-descending" => "Sort descending",
        "filter" => "Filter",
        "hide" => "Hide this column",
        "pin" => "Pin up to this column",
        "unpin" => "Unpin all columns",
        "size-to-fit" => "Size to fit",
        "copy" => "Copy",
        "copy-with-headers" => "Copy with headers",
        _ => id,
    };
}
