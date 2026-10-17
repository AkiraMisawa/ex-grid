namespace ExGrid.Chrome;

/// <summary>
/// The ids of the words a key the grid refuses is said in (ADR-0230), resolved as a paste's or a copy's
/// refusal is (ADR-0203): a substituted Chrome's <see cref="IGridChrome.Words"/>, else the grid's
/// <c>CommandLabel</c>, else <see cref="BuiltInCommandLabels"/>. The root's live region announces the
/// words once, when the key is refused. A Consumer replaces them by answering the id, and silences
/// them by answering the empty string.
/// </summary>
public static class KeyRefusalLabelIds
{
    /// <summary>A Tab typed before the Escape ahead of it had let the keyboard leave the grid: held
    /// behind the Escape's answer, it cannot then move DOM focus, so it is refused, and the next Tab
    /// leaves (ADR-0230 section 1 (b), ADR-0012).</summary>
    public const string TabBeforeRelease = "key-refusal-tab-before-release";
}
