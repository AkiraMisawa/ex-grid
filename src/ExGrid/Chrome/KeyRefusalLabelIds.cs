namespace ExGrid.Chrome;

/// <summary>
/// The ids of the words a key or queued row command the grid refuses is said in (ADR-0230), resolved as a paste's or a copy's
/// refusal is (ADR-0203): a substituted Chrome's <see cref="IGridChrome.Words"/>, else the grid's
/// <c>CommandLabel</c>, else <see cref="BuiltInCommandLabels"/>. The root's live region announces the
/// words once, when the input is refused. A Consumer replaces them by answering the id, and silences
/// them by answering the empty string.
/// </summary>
public static class KeyRefusalLabelIds
{
    /// <summary>A Tab typed before the Escape ahead of it had let the keyboard leave the grid: held
    /// behind the Escape's answer, it cannot then move DOM focus, so it is refused, and the next Tab
    /// leaves (ADR-0230 section 1 (b), ADR-0012).</summary>
    public const string TabBeforeRelease = "key-refusal-tab-before-release";

    /// <summary>Keys typed while the grid was still answering an earlier key or click, held to be
    /// handed on where the keyboard went, which could not be carried out there: the keyboard never
    /// reached what was to take it — the editor, a popover, a frame of the Consumer's own — or reached
    /// a control that cannot take them as typed (a Tab, a composition's text on a control that is not
    /// a text field). They are refused together, never replayed against whatever holds the keyboard
    /// (ADR-0240 section 4, ADR-0010).</summary>
    public const string KeyboardDidNotArrive = "key-refusal-keyboard-did-not-arrive";

    /// <summary>A queued row command lost its target from the current Window (ADR-0160).</summary>
    public const string RowCommandTargetLeft = "key-refusal-row-command-target-left";
    /// <summary>Input made while the Blazor Server circuit was down — a key, a press, a paste — that
    /// never reached the grid: refused once the circuit is back and the next input finds it missing,
    /// never replayed against whatever stands then (ADR-0240 section 11).</summary>
    public const string LostOnTheWay = "key-refusal-lost-on-the-way";
}
