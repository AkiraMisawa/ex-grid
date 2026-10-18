namespace ExGrid.Chrome;

/// <summary>
/// The built-in Chrome's wording for the commands the core defines (ADR-0036), for the
/// accessible names of the core's own fields (<see cref="GridLabelIds"/>, ADR-0036/0051), and for
/// every refusal of a paste or a copy (<see cref="RefusalLabelIds"/>, ADR-0203), which the root's live
/// region announces: where a next step exists, the words say it.
///
/// These strings live in this assembly and belong to the <b>default Chrome</b>, not to the
/// core's decisions: the core says which commands exist by <c>Id</c>, and what they are
/// called is rendering. A substituted Chrome resolves its own wording from the same
/// ids and never sees this table — except a refusal's, which the grid says itself and leaves to
/// this table where the Chrome answers no words of its own (<see cref="IGridChrome.Words"/>); a
/// Consumer keeping the built-in menu replaces it a command at a time through
/// <c>ExGrid.CommandLabel</c>, which is what makes the menu translatable without reimplementing
/// it.
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
        GridCommandIds.SortAscending => "Sort ascending",
        GridCommandIds.SortDescending => "Sort descending",
        GridCommandIds.ClearFilter => "Clear filter",
        GridCommandIds.Hide => "Hide this column",
        GridCommandIds.Pin => "Pin up to this column",
        GridCommandIds.Unpin => "Unpin all columns",
        GridCommandIds.SizeToFit => "Size to fit",
        GridCommandIds.Copy => "Copy",
        GridCommandIds.CopyWithHeaders => "Copy with headers",
        GridLabelIds.NameBox => "Name Box",
        GridLabelIds.FormulaBar => "Formula Bar",
        ReloadNoticeLabelIds.Script => "This page holds another version of the grid's script. Reload the page.",
        ReloadNoticeLabelIds.Stylesheet => "This page holds another version's stylesheet. Reload the page.",
        KeyRefusalLabelIds.TabBeforeRelease =>
            "Tab was pressed before Escape had let the keyboard leave the grid, so the keyboard stayed. Press Tab again to leave.",
        KeyRefusalLabelIds.KeyboardDidNotArrive =>
            "Keys typed while the grid was still answering were not carried out: the keyboard did not arrive where they could be.",
        FindPanelLabelIds.Title => "Find",
        SummaryLabelIds.Average => "Average",
        SummaryLabelIds.Count => "Count",
        SummaryLabelIds.NumericalCount => "Numerical Count",
        SummaryLabelIds.Min => "Min",
        SummaryLabelIds.Max => "Max",
        SummaryLabelIds.Sum => "Sum",
        SummaryLabelIds.Pending => "Calculating…",
        SummaryLabelIds.Menu => "Status bar figures",
        FindPanelLabelIds.Field => "Find what",
        FindPanelLabelIds.MatchCase => "Match case",
        FindPanelLabelIds.WholeCell => "Match entire cell contents",
        FindPanelLabelIds.Next => "Find next",
        FindPanelLabelIds.Previous => "Find previous",
        FindPanelLabelIds.NotFound => "No match",
        FindPanelLabelIds.OrderChanged => "The rows moved; find again",
        FindPanelLabelIds.SourceChanged => "The data was replaced; find again",
        // ADR-0203: a paste, a fill and Delete go through one gate (ADR-0035), so a reason any of
        // them can meet is worded for all three; one only a paste or a fill meets says which.
        RefusalLabelIds.PasteEmptySelection => "Nothing was changed: no cell is selected. Select the cells first.",
        RefusalLabelIds.PasteSingleCellTarget =>
            "Nothing was pasted: the copied block would spill out of the one selected cell. Select a range of the copied block's shape.",
        RefusalLabelIds.PasteShapeMismatch =>
            "Nothing was pasted: the selection is not a whole multiple of the copied block. Select a range of the copied block's shape, or one it repeats over exactly.",
        // Where a paste may spill, one cell takes the block from its top-left (ADR-0050 item 3).
        RefusalLabelIds.PasteShapeMismatchMaySpill =>
            "Nothing was pasted: the selection is not a whole multiple of the copied block. Select a range of the copied block's shape, one it repeats over exactly, or a single cell.",
        RefusalLabelIds.PasteDisjointTarget =>
            "Nothing was pasted: a block of several cells goes into one range, not several. Select one range.",
        RefusalLabelIds.PasteTargetNotEditable =>
            "Nothing was changed: the selection covers " + RefusalLabelIds.ColumnPlaceholder
            + ", which cannot be edited. Select cells only in columns that can be edited.",
        RefusalLabelIds.PasteTooLarge => "Nothing was changed: that is more than the grid reads at once. Use a smaller block.",
        RefusalLabelIds.PasteSpillPastExtent =>
            "Nothing was pasted: the copied block would run past the last row or column. Select a cell further from the edge.",
        RefusalLabelIds.PasteNothingToFillFrom =>
            "Nothing was filled: there is no row above the selection, or column to its left, to fill from. Select the cells to fill from together with the cells to fill.",
        RefusalLabelIds.PasteMultipleRanges => "Nothing was filled: a fill works on one range. Select one range.",
        RefusalLabelIds.PasteSourceUnavailable => "Nothing was filled: the cells to fill from could not all be read.",
        RefusalLabelIds.PasteOrderMoved => "Nothing was changed: the rows moved after the cells were selected. Select the cells again.",
        RefusalLabelIds.PasteSourceChanged =>
            "Nothing was changed: the data was replaced after the cells were selected. Select the cells again.",
        RefusalLabelIds.CopyEmptySelection => "Nothing was copied: no cell is selected. Select the cells to copy.",
        RefusalLabelIds.CopyMisalignedShape =>
            "Nothing was copied: the selected ranges do not line up into one block. Select ranges that share their rows or their columns.",
        // ADR-0005: past the cap a copy points at export, never at a smaller selection — a part of
        // the rows pasted elsewhere is the quietly wrong total the cap exists to prevent.
        RefusalLabelIds.CopyTooLarge =>
            "Nothing was copied: the selection holds more cells than one copy carries. Export the data instead.",
        RefusalLabelIds.CopyRowsUnavailable => "Nothing was copied: the rows of the selection could not all be read.",
        RefusalLabelIds.CopyClipboardUnavailable =>
            "Nothing was copied: the browser refused the clipboard, which still holds what it held before.",
        RefusalLabelIds.CopyRefusedByConsumer => "Nothing was copied.",
        _ => id,
    };
}
