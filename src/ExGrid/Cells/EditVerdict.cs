namespace ExGrid.Cells;

/// <summary>What a Consumer's verdict does to one commit (ADR-0034).</summary>
public enum EditVerdictKind
{
    /// <summary>The Edit Intent is raised unchanged. The absence of a verdict function
    /// means this, so a Consumer that declares none loses nothing.</summary>
    Accept = 0,

    /// <summary>Applied and marked. The intent is raised **unchanged** — the flag does
    /// not travel in it, exactly as Cell State does not travel through the Window
    /// (ADR-0007). The Consumer applies the value and answers `Error` plus a message
    /// through the display channels, so the screen shows what the user entered, marked
    /// as wrong. Loudly wrong, never quietly.</summary>
    Flag,

    /// <summary>The editor holds. Every commit gesture stops — Enter, Tab, the Overwrite
    /// arrows, click-away — the focus stays in the editor with the error shown, and
    /// Escape is the only way out without applying. Excel's *Stop* style.</summary>
    Reject,
}

/// <summary>
/// The Consumer's judgement on one commit, asked by the grid at the moment of committing
/// (ADR-0034). The grid enforces the verb and never learns the reason: which failures
/// Reject and which Flag is the Consumer's convention, not a classification the grid
/// imposes.
///
/// <para>The intended split — and the bundled ruleset's — is that <b>an unparseable text
/// Rejects and a parseable value a rule dislikes Flags</b>. A typed Row Model has nowhere
/// to put <c>abc</c> committed to a date column, while a date the business dislikes fits
/// the model and can be applied wearing its error.</para>
///
/// <para>It runs synchronously on the commit path, so it must be cheap. Anything needing
/// a round trip — uniqueness against a server — is Flag-tier by construction and reports
/// after the fact through the display channels.</para>
/// </summary>
public readonly record struct EditVerdict
{
    private EditVerdict(EditVerdictKind kind, string? message)
    {
        Kind = kind;
        Message = message;
    }

    /// <summary>Raise the intent unchanged.</summary>
    public static EditVerdict Accept { get; } = new(EditVerdictKind.Accept, null);

    /// <summary>Apply it, and say what is wrong with it.</summary>
    public static EditVerdict Flag(string message) => new(EditVerdictKind.Flag, Required(message));

    /// <summary>Hold the editor open, and say why.</summary>
    public static EditVerdict Reject(string message) => new(EditVerdictKind.Reject, Required(message));

    public EditVerdictKind Kind { get; }

    /// <summary>The Consumer's own sentence, or null for an Accept. The grid relays it —
    /// into the editor's error, into the popover, into the live region — and writes none
    /// of its own (ADR-0035's rule, which ADR-0036 made true).</summary>
    public string? Message { get; }

    /// <summary>A Flag or a Reject with nothing to say would paint an error the user
    /// cannot act on, which is the quiet wrongness this component refuses. The message is
    /// the point of both.</summary>
    private static string Required(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return message;
    }
}
