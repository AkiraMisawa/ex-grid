using ExGrid.Clipboard;

namespace ExGrid.Chrome;

/// <summary>
/// The ids of the words a refusal is said in (ADR-0203), one per <see cref="PasteRefusalReason"/>
/// and one per <see cref="CopyRefusalReason"/>. The core refuses with the reason and holds no
/// sentence; the built-in Chrome words each id — through the grid's <c>CommandLabel</c>, falling
/// back to <see cref="BuiltInCommandLabels"/>, as it words its commands — and the root's live region
/// announces the words once, when the refusal happens (ADR-0033). A substituted Chrome answers the
/// same ids through <see cref="IGridChrome.Words"/>.
///
/// <para>A Consumer replaces the words for a reason by answering its id, and silences the built-in
/// announcement for it by answering the empty string: it then owns saying it, as ExSheet does with
/// its own words. Answering null keeps the built-in words. Binding <c>OnPasteRefused</c> or
/// <c>OnCopyRefused</c> silences nothing; both still receive the reason.</para>
///
/// <para>The words for <see cref="PasteTargetNotEditable"/> name the column that refused: the
/// grid writes that column's header where the words hold <see cref="ColumnPlaceholder"/>.</para>
/// </summary>
public static class RefusalLabelIds
{
    /// <summary>Where the words for <see cref="PasteTargetNotEditable"/> name the column that
    /// refused: the grid writes the column's header in its place.</summary>
    public const string ColumnPlaceholder = "{column}";

    /// <summary>The id of the words for a refused paste, fill or Delete — the one gate all three go
    /// through (ADR-0035).</summary>
    /// <param name="reason">Why the write was refused.</param>
    public static string For(PasteRefusalReason reason) => reason switch
    {
        PasteRefusalReason.EmptySelection => PasteEmptySelection,
        PasteRefusalReason.SingleCellTarget => PasteSingleCellTarget,
        PasteRefusalReason.ShapeMismatch => PasteShapeMismatch,
        PasteRefusalReason.DisjointTarget => PasteDisjointTarget,
        PasteRefusalReason.TargetNotEditable => PasteTargetNotEditable,
        PasteRefusalReason.TooLarge => PasteTooLarge,
        PasteRefusalReason.SpillPastExtent => PasteSpillPastExtent,
        PasteRefusalReason.NothingToFillFrom => PasteNothingToFillFrom,
        PasteRefusalReason.MultipleRanges => PasteMultipleRanges,
        PasteRefusalReason.SourceUnavailable => PasteSourceUnavailable,
        PasteRefusalReason.OrderMoved => PasteOrderMoved,
        PasteRefusalReason.SourceChanged => PasteSourceChanged,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "A paste refusal with no words: every reason has an id (ADR-0203)."),
    };

    /// <summary>The id of the words for a refused copy.</summary>
    /// <param name="reason">Why the copy was refused.</param>
    public static string For(CopyRefusalReason reason) => reason switch
    {
        CopyRefusalReason.EmptySelection => CopyEmptySelection,
        CopyRefusalReason.MisalignedShape => CopyMisalignedShape,
        CopyRefusalReason.TooLarge => CopyTooLarge,
        CopyRefusalReason.RowsUnavailable => CopyRowsUnavailable,
        CopyRefusalReason.ClipboardUnavailable => CopyClipboardUnavailable,
        CopyRefusalReason.RefusedByConsumer => CopyRefusedByConsumer,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "A copy refusal with no words: every reason has an id (ADR-0203)."),
    };

    /// <summary><see cref="PasteRefusalReason.EmptySelection"/>.</summary>
    public const string PasteEmptySelection = "refused-paste-empty-selection";

    /// <summary><see cref="PasteRefusalReason.SingleCellTarget"/>: the words say to select a target
    /// of the copied block's shape (ADR-0014).</summary>
    public const string PasteSingleCellTarget = "refused-paste-single-cell-target";

    /// <summary><see cref="PasteRefusalReason.ShapeMismatch"/>: the words say to select a target the
    /// copied block divides into (ADR-0014).</summary>
    public const string PasteShapeMismatch = "refused-paste-shape-mismatch";

    /// <summary><see cref="PasteRefusalReason.DisjointTarget"/>.</summary>
    public const string PasteDisjointTarget = "refused-paste-disjoint-target";

    /// <summary><see cref="PasteRefusalReason.TargetNotEditable"/>: the words name the column that
    /// refused (<see cref="ColumnPlaceholder"/>), and give no reselection advice, since none would be
    /// accepted (ADR-0035).</summary>
    public const string PasteTargetNotEditable = "refused-paste-target-not-editable";

    /// <summary><see cref="PasteRefusalReason.TooLarge"/>.</summary>
    public const string PasteTooLarge = "refused-paste-too-large";

    /// <summary><see cref="PasteRefusalReason.SpillPastExtent"/>.</summary>
    public const string PasteSpillPastExtent = "refused-paste-spill-past-extent";

    /// <summary><see cref="PasteRefusalReason.NothingToFillFrom"/>.</summary>
    public const string PasteNothingToFillFrom = "refused-paste-nothing-to-fill-from";

    /// <summary><see cref="PasteRefusalReason.MultipleRanges"/>.</summary>
    public const string PasteMultipleRanges = "refused-paste-multiple-ranges";

    /// <summary><see cref="PasteRefusalReason.SourceUnavailable"/>.</summary>
    public const string PasteSourceUnavailable = "refused-paste-source-unavailable";

    /// <summary><see cref="PasteRefusalReason.OrderMoved"/>.</summary>
    public const string PasteOrderMoved = "refused-paste-order-moved";

    /// <summary><see cref="PasteRefusalReason.SourceChanged"/>.</summary>
    public const string PasteSourceChanged = "refused-paste-source-changed";

    /// <summary><see cref="CopyRefusalReason.EmptySelection"/>.</summary>
    public const string CopyEmptySelection = "refused-copy-empty-selection";

    /// <summary><see cref="CopyRefusalReason.MisalignedShape"/>.</summary>
    public const string CopyMisalignedShape = "refused-copy-misaligned-shape";

    /// <summary><see cref="CopyRefusalReason.TooLarge"/>.</summary>
    public const string CopyTooLarge = "refused-copy-too-large";

    /// <summary><see cref="CopyRefusalReason.RowsUnavailable"/>.</summary>
    public const string CopyRowsUnavailable = "refused-copy-rows-unavailable";

    /// <summary><see cref="CopyRefusalReason.ClipboardUnavailable"/>.</summary>
    public const string CopyClipboardUnavailable = "refused-copy-clipboard-unavailable";

    /// <summary><see cref="CopyRefusalReason.RefusedByConsumer"/>. The Consumer's own sentence, from
    /// its copy answer, is what is announced; these words are said only where a substituted Chrome
    /// or a Consumer asks for them by id.</summary>
    public const string CopyRefusedByConsumer = "refused-copy-by-consumer";
}
