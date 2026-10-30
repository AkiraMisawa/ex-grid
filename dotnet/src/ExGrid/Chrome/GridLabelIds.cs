namespace ExGrid.Chrome;

/// <summary>
/// The ids of the words the built-in Chrome gives the core's own fields, beside
/// <see cref="GridCommandIds"/> (ADR-0036/0051): the core holds no English of its own for an
/// accessible name either. The built-in Chrome resolves each through <c>ExGrid.CommandLabel</c>,
/// falling back to <see cref="BuiltInCommandLabels"/>; a Chrome that paints the field names it
/// in its own words.
/// </summary>
public static class GridLabelIds
{
    /// <summary>The Formula Bar's Name Box, as its accessible name (ADR-0051).</summary>
    public const string NameBox = "name-box";

    /// <summary>The Formula Bar's text field, as its accessible name (ADR-0051).</summary>
    public const string FormulaBar = "formula-bar";

    /// <summary>The live region's sentence once a paste starts to be read, said once while it is under
    /// way, with the root <c>aria-busy</c> (ADR-0240 section 12).</summary>
    public const string PasteUnderWay = "paste-under-way";

    /// <summary>The live region's sentence when an Escape typed to cancel a paste came after the paste had
    /// raised its Edit Intent: the paste stands (ADR-0240 section 12).</summary>
    public const string PasteAlreadyLanded = "paste-already-landed";
}
